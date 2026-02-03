using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Tracking;
using PeakHeadTracking.Config;

namespace PeakHeadTracking.Camera
{
    /// <summary>
    /// Controls camera rotation based on head tracking data.
    /// Owns the TrackingProcessor pipeline: raw pose -> interpolate -> process -> CameraPatches.
    /// CameraPatches handles the actual rotation application via render callbacks.
    /// ExecutionOrder 1000 ensures this runs AFTER MainCameraMovement (500)
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class CameraController : MonoBehaviour
    {
        private ModConfiguration config;
        private OpenTrackReceiver coreReceiver;
        private TrackingProcessor processor;
        private PoseInterpolator interpolator;

        private const int DebugLogIntervalFrames = 120;

        // Camera references
        private UnityEngine.Camera mainCamera;
        private Transform cameraTransform;

        // Camera finding state
        private const float CAMERA_SEARCH_INTERVAL_SECONDS = 1.0f;
        private int cameraSearchAttempts = 0;
        private const int MAX_CAMERA_SEARCH_ATTEMPTS = 10;

        // Tracking state
        private bool isTrackingActive = false;
        private bool isInitialized = false;
        private bool wasReceiving = false;

        /// <summary>
        /// Initialize the camera controller
        /// </summary>
        public void Initialize(ModConfiguration modConfig, OpenTrackReceiver trackReceiver, TrackingProcessor trackingProcessor, PoseInterpolator poseInterpolator)
        {
            config = modConfig;
            coreReceiver = trackReceiver;
            processor = trackingProcessor;
            interpolator = poseInterpolator;

            // Configuration validation - ensure required config entries exist
            if (config.MaintainRelativePosition == null)
                throw new InvalidOperationException("MaintainRelativePosition configuration is required");

            isInitialized = true;
            PeakHeadTrackingPlugin.Logger.LogDebug("CameraController initialized");
        }

        /// <summary>
        /// Unity Start - find camera references
        /// </summary>
        private void Start()
        {
            if (!isInitialized)
            {
                PeakHeadTrackingPlugin.Logger.LogWarning("CameraController started without initialization");
                return;
            }

            // Subscribe to scene changes to re-find camera
            SceneManager.sceneLoaded += OnSceneLoaded;

            StartCoroutine(FindCameraCoroutine());
        }

        /// <summary>
        /// Handle scene load - re-find camera in new scene
        /// </summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            PeakHeadTrackingPlugin.Logger.LogInfo($"Scene loaded: {scene.name} - re-finding camera");

            // Reset camera state
            mainCamera = null;
            cameraTransform = null;
            cameraSearchAttempts = 0;

            // Start searching for camera in new scene
            StartCoroutine(FindCameraCoroutine());
        }

        /// <summary>
        /// Coroutine to find and attach to camera
        /// </summary>
        private IEnumerator FindCameraCoroutine()
        {
            while (mainCamera == null && cameraSearchAttempts < MAX_CAMERA_SEARCH_ATTEMPTS)
            {
                FindMainCamera();

                if (mainCamera == null)
                {
                    cameraSearchAttempts++;
                    PeakHeadTrackingPlugin.Logger.LogDebug($"Camera search attempt {cameraSearchAttempts}/{MAX_CAMERA_SEARCH_ATTEMPTS}");
                    yield return new WaitForSeconds(CAMERA_SEARCH_INTERVAL_SECONDS);
                }
            }

            if (mainCamera == null)
            {
                PeakHeadTrackingPlugin.Logger.LogError("Failed to find camera after maximum attempts");
            }
        }

        /// <summary>
        /// Find and cache the main camera
        /// </summary>
        private void FindMainCamera()
        {
            // Try multiple methods to find the camera
            mainCamera = UnityEngine.Camera.main;

            if (mainCamera == null)
            {
                // Try finding by tag
                GameObject camObj = GameObject.FindWithTag("MainCamera");
                if (camObj != null)
                {
                    mainCamera = camObj.GetComponent<UnityEngine.Camera>();
                }
            }

            if (mainCamera == null)
            {
                // Try finding any active camera
                UnityEngine.Camera[] cameras = FindObjectsByType<UnityEngine.Camera>(FindObjectsSortMode.None);
                foreach (var cam in cameras)
                {
                    if (cam.enabled && cam.gameObject.activeInHierarchy)
                    {
                        mainCamera = cam;
                        break;
                    }
                }
            }

            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
                PeakHeadTrackingPlugin.Logger.LogInfo($"Attached to camera: {mainCamera.name}");
            }
        }

        /// <summary>
        /// Unity LateUpdate - Polls receiver, runs processing pipeline, writes to CameraPatches.
        /// </summary>
        private void LateUpdate()
        {
            if (coreReceiver != null && isTrackingActive)
            {
                bool isReceiving = coreReceiver.IsReceiving;

                // Auto-recenter when tracking data first arrives (or reconnects)
                if (isReceiving && !wasReceiving)
                {
                    RecenterView();
                    PeakHeadTrackingPlugin.Logger.LogInfo("Auto-recentered: tracking data connected");
                }
                wasReceiving = isReceiving;

                // Get raw pose from receiver
                var rawPose = coreReceiver.GetLatestPose();

                // Run through interpolation (fills 60Hz→240Hz gaps with linear lerp)
                var interpolated = interpolator.Update(rawPose, Time.deltaTime);

                // Apply centering, deadzone, sensitivity — but NOT exponential smoothing.
                // PoseInterpolator already produces smooth output at any frame rate;
                // adding exponential smoothing on top would double-smooth and add ~60-75ms latency.
                Quat4 rawQ = QuaternionUtils.FromYawPitchRoll(interpolated.Yaw, interpolated.Pitch, interpolated.Roll);
                Quat4 centeredQ = processor.CenterManager.ApplyOffsetQuat(rawQ);
                QuaternionUtils.ToEulerYXZ(centeredQ, out float yaw, out float pitch, out float roll);

                yaw = (float)DeadzoneUtils.Apply(yaw, processor.Deadzone.Yaw);
                pitch = (float)DeadzoneUtils.Apply(pitch, processor.Deadzone.Pitch);
                roll = (float)DeadzoneUtils.Apply(roll, processor.Deadzone.Roll);

                var processed = new TrackingPose(yaw, pitch, roll, interpolated.TimestampTicks)
                    .ApplySensitivity(processor.Sensitivity);

                // Write processed values to CameraPatches
                Patches.CameraPatches.SetProcessedRotation(processed.Yaw, processed.Pitch, processed.Roll);
            }

            if (config != null && config.DebugLogging.Value && Time.frameCount % DebugLogIntervalFrames == 0)
            {
                LogDebugState();
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void LogDebugState()
        {
            Vector2 headTracking = Patches.CameraPatches.GetHeadTrackingInput();
            PeakHeadTrackingPlugin.Logger?.LogDebug($"[CameraController] isTrackingActive={isTrackingActive}, headTracking=({headTracking.x:F1}, {headTracking.y:F1})");
        }

        /// <summary>
        /// Enable or disable tracking
        /// </summary>
        public void SetTrackingEnabled(bool enabled)
        {
            isTrackingActive = enabled;

            // Enable/disable the view matrix modification
            Patches.CameraPatches.SetHeadTrackingEnabled(enabled);

            if (!enabled)
            {
                // Reset so next enable triggers auto-recenter on connection
                wasReceiving = false;

                // Clear head tracking input
                Patches.CameraPatches.SetHeadTrackingInput(0, 0);

                // Reset view matrix to auto-calculated mode
                if (mainCamera != null)
                {
                    ViewMatrixModifier.Reset(mainCamera);
                }
            }
            else
            {
                // Reset processing pipeline for clean start
                processor.ResetSmoothing();
                interpolator.Reset();

                // Recenter to current head position
                var rawPose = coreReceiver.GetLatestPose();
                processor.RecenterTo(rawPose);
            }

            PeakHeadTrackingPlugin.Logger.LogInfo($"Tracking {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// Recenter the view (set current rotation as center)
        /// </summary>
        public void RecenterView()
        {
            var rawPose = coreReceiver.GetLatestPose();
            processor.RecenterTo(rawPose);
            interpolator.Reset();
            Patches.CameraPatches.RecenterPosition();
            PeakHeadTrackingPlugin.Logger.LogInfo("View recentered");
        }

        /// <summary>
        /// Get current tracking state
        /// </summary>
        public bool IsTrackingActive => isTrackingActive && coreReceiver != null && coreReceiver.IsReceiving;

        private void OnDestroy()
        {
            // Unsubscribe from scene events
            SceneManager.sceneLoaded -= OnSceneLoaded;

            // Unregister camera callback
            Patches.CameraPatches.UnregisterCameraCallback();

            // Reset view matrix to auto-calculated mode
            if (mainCamera != null)
            {
                ViewMatrixModifier.Reset(mainCamera);
            }
        }
    }
}
