using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
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
        public const string PLUGIN_VERSION = "1.1.2";

        // Static logger instance for global access
        internal static new ManualLogSource Logger;

        // Harmony instance for runtime patching
        private Harmony harmony;

        // Core components
        private GameObject trackingManagerObject;
        private Config.ModConfiguration modConfig;
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
        /// Initialize the configuration system
        /// </summary>
        private void InitializeConfiguration()
        {
            Logger.LogDebug("Initializing configuration...");

            modConfig = new Config.ModConfiguration();
            modConfig.Initialize(base.Config);

            Logger.LogDebug($"Configuration loaded - UDP Port: {modConfig.UdpPort.Value}, " +
                           $"Tracking Enabled: {modConfig.TrackingEnabled.Value}");
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

            // Initialize TrackingProcessor with config values
            processor = new TrackingProcessor
            {
                SmoothingFactor = modConfig.Smoothing.Value,
                Sensitivity = new SensitivitySettings(
                    modConfig.YawSensitivity.Value,
                    modConfig.PitchSensitivity.Value,
                    modConfig.RollSensitivity.Value,
                    invertYaw: modConfig.InvertYaw.Value,
                    invertPitch: modConfig.InvertPitch.Value,
                    invertRoll: modConfig.InvertRoll.Value
                ),
                Deadzone = modConfig.EnableDeadzone.Value
                    ? new DeadzoneSettings(
                        modConfig.DeadzoneYaw.Value,
                        modConfig.DeadzonePitch.Value,
                        modConfig.DeadzoneRoll.Value)
                    : DeadzoneSettings.None
            };

            // Initialize PoseInterpolator
            interpolator = new PoseInterpolator();

            // Add camera controller component (primary camera control)
            cameraController = trackingManagerObject.AddComponent<Camera.CameraController>();
            cameraController.Initialize(modConfig, coreReceiver, processor, interpolator);

            // Initialize PositionProcessor with config values
            positionProcessor = new PositionProcessor
            {
                Settings = new PositionSettings(
                    modConfig.PositionSensitivityX.Value,
                    modConfig.PositionSensitivityY.Value,
                    modConfig.PositionSensitivityZ.Value,
                    modConfig.PositionLimitX.Value,
                    modConfig.PositionLimitY.Value,
                    0.10f,
                    modConfig.PositionLimitZ.Value,
                    modConfig.PositionSmoothing.Value,
                    invertX: true, invertY: false, invertZ: false
                )
            };
            positionInterpolator = new PositionInterpolator();

            // Expose receiver to CameraPatches for zero-latency access
            Patches.CameraPatches.SetReceiver(coreReceiver);
            Patches.CameraPatches.SetPositionProcessors(positionProcessor, positionInterpolator, modConfig.PositionEnabled);
            Patches.CameraPatches.SetNearClipConfig(modConfig.NearClipOverride);
            Patches.CameraPatches.SetReticleConfig(modConfig.ShowReticle);
            Patches.CameraPatches.SetYawModeConfig(modConfig.WorldSpaceYaw);

            // Add hotkey manager component
            hotkeyManager = trackingManagerObject.AddComponent<Input.HotkeyManager>();
            hotkeyManager.Initialize(modConfig, cameraController, coreReceiver);

            Logger.LogDebug("All components initialized");
        }

        /// <summary>
        /// Unity Start - called after Awake when the GameObject becomes active
        /// </summary>
        private void Start()
        {
            Logger.LogDebug("Plugin Start() called");

            // Start receiving UDP data if tracking is enabled
            if (modConfig.TrackingEnabled.Value)
            {
                coreReceiver.Start(modConfig.UdpPort.Value);
                cameraController.SetTrackingEnabled(true);
                Logger.LogInfo("Head tracking started");
            }
            else
            {
                Logger.LogInfo("Head tracking disabled by configuration");
            }
        }

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

            // Remove Harmony patches before disposing anything —
            // prevents patched game methods from calling into our code during teardown
            harmony?.UnpatchSelf();
            harmony = null;

            // Destroy the tracking manager GameObject — this stops CameraController.LateUpdate()
            // and HotkeyManager.Update() from running on disposed references.
            // Use DestroyImmediate during application quit (Destroy is deferred and won't
            // execute during quit, leaving the object alive with running Update loops).
            if (trackingManagerObject != null)
            {
                DestroyImmediate(trackingManagerObject);
                trackingManagerObject = null;
            }

            // Stop UDP receiver — close socket first to unblock the receive thread,
            // then Dispose() joins the thread (which exits immediately once socket is closed)
            if (coreReceiver != null)
            {
                coreReceiver.Dispose();
                coreReceiver = null;
            }

            // Clear receiver reference from CameraPatches
            Patches.CameraPatches.SetReceiver(null);

            modConfig = null;

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
