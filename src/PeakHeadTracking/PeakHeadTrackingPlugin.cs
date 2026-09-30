using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using PeakHeadTracking.Config;
using UnityEngine;

namespace PeakHeadTracking
{
    /// <summary>
    /// Main BepInEx plugin entry point for Peak Head Tracking mod
    /// </summary>
    [BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
    [BepInProcess("Peak.exe")] // Target the Peak game executable
    public class PeakHeadTrackingPlugin : BaseUnityPlugin
    {
        // Plugin metadata constants
        public const string PLUGIN_GUID = "com.cameraunlock.peak.headtracking";
        public const string PLUGIN_NAME = "Peak Head Tracking";
        public const string PLUGIN_VERSION = "1.4.0";

        // Shipped as Position Sensitivity X/Y/Z = 2 and Invert Roll = true before the canonical
        // config, correcting the tracker's pose to PEAK's view. Folded into the code so the view
        // moves as it did at those defaults.
        private const float PositionScale = 2.0f;
        private static readonly SensitivitySettings RotationAxes = new SensitivitySettings(1f, 1f, 1f, invertRoll: true);

        // Static logger instance for global access
        internal static new ManualLogSource Logger;

        // Harmony instance for runtime patching
        private Harmony harmony;

        // Core components
        private GameObject trackingManagerObject;
        private ConfigOwner<PeakConfig> configOwner;
        private PeakConfig config;
        private OpenTrackReceiver coreReceiver;
        private TrackingProcessor processor;
        private PoseInterpolator interpolator;
        private Camera.CameraController cameraController;
        private Input.HotkeyManager hotkeyManager;
        private PositionProcessor positionProcessor;
        private PositionInterpolator positionInterpolator;

        /// <summary>
        /// Unity Awake - called when the plugin is first loaded by BepInEx
        /// </summary>
        private void Awake()
        {
            // Initialize static logger reference
            Logger = base.Logger;
            Logger.LogInfo($"Initializing {PLUGIN_NAME} v{PLUGIN_VERSION}");

            try
            {
                // Initialize configuration system
                InitializeConfiguration();

                // Create persistent GameObject for Unity components
                CreateTrackingManager();

                // Initialize Harmony patches
                InitializeHarmonyPatches();

                // Initialize core components
                InitializeComponents();

                Logger.LogInfo($"{PLUGIN_NAME} successfully loaded!");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize {PLUGIN_NAME}: {ex}");
                throw;
            }
        }

        /// <summary>
        /// The settings live in BepInEx\config\CameraUnlock.ini, read and written by core's config
        /// owner, with rows set to default following the player's Defaults.ini. Nothing is bound
        /// through BepInEx's ConfigFile at runtime, so ConfigurationManager does not list them.
        /// The plugin's .cfg, which earlier builds read, is imported once while CameraUnlock.ini is
        /// absent and never written.
        /// </summary>
        private void InitializeConfiguration()
        {
            configOwner = new ConfigOwner<PeakConfig>(
                PeakConfigOwner.Options(base.Config, DefaultsFile.PerUser(), message => Logger.LogWarning(message)));
            ConfigLoadResult<PeakConfig> loaded = configOwner.Load();
            bool usable = loaded.Status == ConfigLoadStatus.Canonical
                          || loaded.Status == ConfigLoadStatus.Migrated
                          || loaded.Status == ConfigLoadStatus.Created;
            WriteConfigLog(loaded.Log, loaded.Diagnostics, usable);
            Logger.LogInfo("Config: " + loaded.Status);

            // The published build did not load at all on a .cfg BepInEx refused to read.
            if (loaded.Status == ConfigLoadStatus.LegacyRefused)
            {
                throw new InvalidOperationException(loaded.Reason);
            }
            config = loaded.Config;
        }

        // The owner writes each diagnostic as "<path>: <description>" among lines that only report
        // what it did, so the complaints are picked out by their text.
        private static void WriteConfigLog(IEnumerable<string> lines, IEnumerable<CanonicalDiagnostic> diagnostics, bool usable)
        {
            var complaints = new HashSet<string>();
            foreach (CanonicalDiagnostic diagnostic in diagnostics) complaints.Add(diagnostic.Describe());
            foreach (string line in lines)
            {
                bool complaint = false;
                foreach (string c in complaints)
                {
                    if (line.EndsWith(c, StringComparison.Ordinal)) complaint = true;
                }
                if (usable && !complaint) Logger.LogInfo(line);
                else Logger.LogWarning(line);
            }
        }

        /// <summary>
        /// Called after a toggle has applied its new value. A save that fails is logged and the
        /// session keeps the new value.
        /// </summary>
        internal void SaveConfig(Action<PeakConfig> change)
        {
            ConfigSaveResult saved = configOwner.Save(change);
            if (saved.Status == ConfigSaveStatus.Saved)
            {
                foreach (string line in saved.Log) Logger.LogInfo(line);
                return;
            }
            foreach (string line in saved.Log) Logger.LogWarning(line);
            Logger.LogWarning("Config not saved (" + saved.Status + "): " + saved.Reason + " The change applies to this session only.");
        }

        /// <summary>
        /// Reads CameraUnlock.ini and Defaults.ini again and applies what they hold. The UDP
        /// listener restarts either way, as the reload key always did.
        /// </summary>
        internal void ReloadConfig()
        {
            ConfigReloadResult<PeakConfig> reloaded = configOwner.Reload();
            WriteConfigLog(reloaded.Log, reloaded.Diagnostics, reloaded.Status != ConfigReloadStatus.Unreadable);
            if (reloaded.Status == ConfigReloadStatus.Applied)
            {
                config = reloaded.Config;
                ApplyConfig();
            }
            else if (reloaded.Status == ConfigReloadStatus.Unreadable)
            {
                Logger.LogWarning("Config not reloaded: " + reloaded.Reason + " The settings in use are kept.");
            }

            coreReceiver.Stop();
            if (cameraController.IsTrackingEnabled)
            {
                coreReceiver.Start(config.UdpPort);
            }
            Logger.LogInfo("Configuration reloaded (" + reloaded.Status + ")");
        }

        /// <summary>
        /// Create the persistent GameObject that will hold our Unity components
        /// </summary>
        private void CreateTrackingManager()
        {
            Logger.LogDebug("Creating tracking manager GameObject...");

            // Create a new GameObject that persists between scene changes
            trackingManagerObject = new GameObject("PeakHeadTrackingManager");
            DontDestroyOnLoad(trackingManagerObject);

            // Hide it from the scene hierarchy in development builds
            trackingManagerObject.hideFlags = HideFlags.HideAndDontSave;
        }

        /// <summary>
        /// Initialize and apply Harmony patches to hook into game systems
        /// </summary>
        private void InitializeHarmonyPatches()
        {
            Logger.LogDebug("Initializing Harmony patches...");

            // Create Harmony instance with our plugin GUID
            harmony = new Harmony(PLUGIN_GUID);

            // Apply all patches in the assembly
            harmony.PatchAll();

            // Log successful patch count
            var patchedMethods = harmony.GetPatchedMethods();
            int patchCount = 0;
            foreach (var method in patchedMethods)
            {
                patchCount++;
                Logger.LogDebug($"Patched: {method.DeclaringType?.Name}.{method.Name}");
            }

            Logger.LogInfo($"Applied {patchCount} Harmony patches");
        }

        /// <summary>
        /// Initialize all core components
        /// </summary>
        private void InitializeComponents()
        {
            Logger.LogDebug("Initializing components...");

            // Initialize core OpenTrack receiver (non-MonoBehaviour)
            coreReceiver = new OpenTrackReceiver();
            coreReceiver.Log = msg => Logger.LogInfo(msg);

            processor = new TrackingProcessor
            {
                Sensitivity = RotationAxes,
                Deadzone = DeadzoneSettings.None
            };

            // Initialize PoseInterpolator
            interpolator = new PoseInterpolator();

            positionProcessor = new PositionProcessor();
            positionInterpolator = new PositionInterpolator();

            // Add camera controller component (primary camera control)
            cameraController = trackingManagerObject.AddComponent<Camera.CameraController>();
            cameraController.Initialize(coreReceiver, processor, interpolator, positionProcessor, positionInterpolator);

            // Add hotkey manager component
            hotkeyManager = trackingManagerObject.AddComponent<Input.HotkeyManager>();
            hotkeyManager.Initialize(this, cameraController, coreReceiver);

            ApplyConfig();

            Logger.LogDebug("All components initialized");
        }

        /// <summary>
        /// Hands the settings in <see cref="config"/> to every part of the mod that reads one.
        /// </summary>
        private void ApplyConfig()
        {
            processor.LocalSmoothing = config.LocalSmoothing;
            processor.RemoteSmoothing = config.RemoteSmoothing;

            PositionSettings limits = config.Position;
            positionProcessor.Settings = new PositionSettings(
                PositionScale, PositionScale, PositionScale,
                limits.LimitX, limits.LimitY, limits.LimitYDown, limits.LimitZ, limits.LimitZBack,
                config.LocalSmoothing, config.RemoteSmoothing,
                invertX: true, invertY: false, invertZ: false);

            Patches.CameraPatches.SetNearClip(config.MinNearClip);
            Patches.CameraPatches.SetWorldSpaceYaw(config.WorldSpaceYaw);
            Patches.CameraPatches.SetRotationEnabled(config.RotationEnabled);
            Patches.CameraPatches.SetPositionEnabled(config.PositionEnabled);
            cameraController.DebugLogging = config.DebugLogging;
            hotkeyManager.Apply(config);
        }

        /// <summary>
        /// Unity Start - called after Awake when the GameObject becomes active
        /// </summary>
        private void Start()
        {
            Logger.LogDebug("Plugin Start() called");

            // Start receiving UDP data if tracking is enabled
            if (config.EnableOnStartup)
            {
                coreReceiver.Start(config.UdpPort);
                cameraController.SetTrackingEnabled(true);
                Logger.LogInfo($"Head tracking started, listening on UDP port {config.UdpPort}");
            }
            else
            {
                Logger.LogInfo("Head tracking disabled by configuration");
            }
        }

        internal int UdpPort => config.UdpPort;

        private bool destroyed;

        /// <summary>
        /// Unity OnDestroy - cleanup when the plugin is unloaded
        /// </summary>
        private void OnDestroy()
        {
            if (destroyed) return;
            destroyed = true;

            Logger.LogInfo("Shutting down Peak Head Tracking...");

            // Unregister camera render callbacks first to stop rendering pipeline
            Patches.CameraPatches.UnregisterCameraCallback();

            // Remove Harmony patches before disposing anything -
            // prevents patched game methods from calling into our code during teardown
            harmony?.UnpatchSelf();
            harmony = null;

            // Destroy the tracking manager GameObject - this stops CameraController.LateUpdate()
            // and HotkeyManager.Update() from running on disposed references.
            // Use DestroyImmediate during application quit (Destroy is deferred and won't
            // execute during quit, leaving the object alive with running Update loops).
            if (trackingManagerObject != null)
            {
                DestroyImmediate(trackingManagerObject);
                trackingManagerObject = null;
            }

            // Stop UDP receiver - close socket first to unblock the receive thread,
            // then Dispose() joins the thread (which exits immediately once socket is closed)
            if (coreReceiver != null)
            {
                coreReceiver.Dispose();
                coreReceiver = null;
            }

            config = null;

            Logger.LogInfo("Cleanup complete");
        }

        /// <summary>
        /// Unity OnApplicationQuit - additional cleanup when the game is closing
        /// </summary>
        private void OnApplicationQuit()
        {
            OnDestroy();
        }
    }
}
