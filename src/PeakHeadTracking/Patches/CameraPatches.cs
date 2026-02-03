using BepInEx.Configuration;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Extensions;
using CameraUnlock.Core.Unity.Rendering;
using CameraUnlock.Core.Unity.Tracking;
using UnityEngine;

namespace PeakHeadTracking.Patches
{
    /// <summary>
    /// Head tracking using VIEW MATRIX modification with ZERO-LATENCY design.
    ///
    /// Key design principles for minimum latency:
    /// 1. CameraController runs the TrackingProcessor pipeline (centering, deadzone, smoothing, sensitivity)
    /// 2. Pre-processed yaw/pitch/roll are written to volatile fields here
    /// 3. Render callback reads processed values and applies via ViewMatrixModifier
    ///
    /// This ONLY affects rendering - game logic (aiming, movement, reticle) remains unchanged.
    /// The camera transform is NEVER modified - only worldToCameraMatrix is changed.
    /// Reticle compensation moves the crosshair to show where you're actually aiming.
    /// Uses RenderPipelineManager.beginCameraRendering for Unity 6 / SRP compatibility.
    /// </summary>
    public static class CameraPatches
    {
        // Core receiver reference - set by plugin during initialization
        private static OpenTrackReceiver receiver;

        // Position processing
        private static PositionProcessor positionProcessor;
        private static PositionInterpolator positionInterpolator;
        private static ConfigEntry<bool> positionEnabledConfig;

        // Reticle compensation
        private static ConfigEntry<bool> showReticleConfig;

        // Near clip plane override
        private static ConfigEntry<float> nearClipConfig;
        private static float storedNearClipPlane;

        /// <summary>
        /// Set the core receiver reference for zero-latency access.
        /// </summary>
        public static void SetReceiver(OpenTrackReceiver coreReceiver)
        {
            receiver = coreReceiver;
        }

        public static void SetPositionProcessors(PositionProcessor posProccesor, PositionInterpolator posInterp, ConfigEntry<bool> posEnabled)
        {
            positionProcessor = posProccesor;
            positionInterpolator = posInterp;
            positionEnabledConfig = posEnabled;
        }

        public static void SetNearClipConfig(ConfigEntry<float> config)
        {
            nearClipConfig = config;
        }

        public static void SetReticleConfig(ConfigEntry<bool> config)
        {
            showReticleConfig = config;
        }

        /// <summary>
        /// Recenter position tracking to current head position.
        /// </summary>
        public static void RecenterPosition()
        {
            if (positionProcessor == null || receiver == null) return;
            positionProcessor.SetCenter(receiver.GetLatestPosition());
            positionInterpolator?.Reset();
        }

        // Processed values after the full pipeline (for compatibility)
        private static float currentYaw = 0f;
        private static float currentPitch = 0f;

        private static bool headTrackingEnabled = false;
        private static bool hasLoggedFirstApplication = false;

        // Pre-processed rotation values - written by CameraController after TrackingProcessor pipeline
        private static volatile float processedYaw = 0f;
        private static volatile float processedPitch = 0f;
        private static volatile float processedRoll = 0f;

        // Callback state - now managed by RenderPipelineHelper
        private static bool callbackRegistered = false;

        // Tracks whether OnPreRender actually modified the view matrix this frame,
        // so OnPostRender only resets when needed (avoids corrupting splash/loading cameras)
        private static bool matrixModifiedThisFrame = false;

        // Diagnostic logging state
        private static int lastDiagnosticFrame = -1;
        private const int DiagnosticLogInterval = 300; // Log every 300 frames (~5 seconds at 60fps)

        /// <summary>
        /// Current processed head tracking angles (degrees).
        /// </summary>
        public static float CurrentYaw => currentYaw;
        public static float CurrentPitch => currentPitch;

        /// <summary>
        /// Expose processed rotation for CameraQuadPatches to apply scoped transform modification.
        /// </summary>
        internal static float ProcessedYaw => processedYaw;
        internal static float ProcessedPitch => processedPitch;
        internal static float ProcessedRoll => processedRoll;

        /// <summary>
        /// Legacy method for compatibility - stores processed values for UI display
        /// </summary>
        public static void SetHeadTrackingInput(float yaw, float pitch)
        {
            currentYaw = yaw;
            currentPitch = pitch;
        }

        /// <summary>
        /// Write pre-processed rotation values from the TrackingProcessor pipeline.
        /// Called from CameraController.LateUpdate() after running the full pipeline.
        /// </summary>
        public static void SetProcessedRotation(float yaw, float pitch, float roll)
        {
            processedYaw = yaw;
            processedPitch = pitch;
            processedRoll = roll;

            // Update currentYaw/Pitch for any code that reads them
            currentYaw = yaw;
            currentPitch = pitch;
        }

        /// <summary>
        /// Enable or disable head tracking
        /// </summary>
        public static void SetHeadTrackingEnabled(bool enabled)
        {
            headTrackingEnabled = enabled;

            if (enabled && !callbackRegistered)
            {
                RegisterCameraCallback();
            }

            if (!enabled)
            {
                // Reset reticle to center when tracking disabled
                ReticleCompensation.ResetReticlePosition();
            }
        }

        /// <summary>
        /// Register the camera callback for view matrix modification.
        /// Uses RenderPipelineHelper for automatic SRP/Legacy detection.
        /// </summary>
        public static void RegisterCameraCallback()
        {
            if (callbackRegistered)
            {
                PeakHeadTrackingPlugin.Logger?.LogWarning("[RegisterCameraCallback] Already registered, skipping");
                return;
            }

            PeakHeadTrackingPlugin.Logger?.LogDebug($"Render pipeline: {(RenderPipelineHelper.IsSRP ? "SRP" : "Legacy")}");

            // Use RenderPipelineHelper for unified SRP/Legacy callback registration
            RenderPipelineHelper.RegisterCallbacks(OnPreRender, OnPostRender);

            callbackRegistered = true;
            PeakHeadTrackingPlugin.Logger?.LogDebug("Camera render callback registered");
        }

        /// <summary>
        /// Unregister the camera callback
        /// </summary>
        public static void UnregisterCameraCallback()
        {
            if (!callbackRegistered) return;

            RenderPipelineHelper.UnregisterCallbacks();

            callbackRegistered = false;
            PeakHeadTrackingPlugin.Logger?.LogDebug("Camera render callback unregistered");
        }

        /// <summary>
        /// Get current head tracking input
        /// </summary>
        public static Vector2 GetHeadTrackingInput()
        {
            return new Vector2(currentYaw, currentPitch);
        }

        /// <summary>
        /// Check if head tracking is enabled
        /// </summary>
        public static bool IsHeadTrackingEnabled()
        {
            return headTrackingEnabled;
        }

        /// <summary>
        /// Pre-render callback: apply head tracking via view matrix modification.
        /// The camera transform is NEVER modified — only worldToCameraMatrix changes.
        /// </summary>
        private static void OnPreRender(UnityEngine.Camera cam)
        {
            // Only apply to the main camera
            var mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null)
            {
                LogDiagnostic("[HeadTracking] Camera.main is null - waiting for camera");
                return;
            }
            if (cam != mainCamera)
            {
                return; // Skip non-main cameras silently (expected behavior)
            }

            if (!headTrackingEnabled)
                return;

            // Require receiver to be set
            if (receiver == null)
            {
                LogDiagnostic("[HeadTracking] ERROR: Receiver is null - tracking disabled");
                return;
            }

            // Don't apply head tracking during loading/splash screens or when not in gameplay
            if (GameplayStateDetection.ShouldSkipHeadTracking())
            {
                // Reset reticle to center when not in active gameplay
                ReticleCompensation.ResetReticlePosition();
                return;
            }

            // READ pre-processed values from volatile fields
            // These are written by CameraController.LateUpdate() after the TrackingProcessor pipeline
            float yaw = processedYaw;
            float pitch = processedPitch;
            float roll = processedRoll;

            // Skip rotation application if no significant head movement
            if (Mathf.Abs(yaw) < TrackingConstants.MovementThreshold &&
                Mathf.Abs(pitch) < TrackingConstants.MovementThreshold &&
                Mathf.Abs(roll) < TrackingConstants.MovementThreshold)
                return;

            // Apply rotation via view matrix — all axes in camera-local space
            // so yaw always feels like horizontal rotation regardless of game camera pitch.
            // This modifies worldToCameraMatrix WITHOUT touching camera.transform
            ViewMatrixModifier.ApplyHeadRotation(cam, yaw, -pitch, roll);
            matrixModifiedThisFrame = true;

            // Apply position offset in camera space via matrix translation
            if (positionProcessor != null && positionEnabledConfig != null && positionEnabledConfig.Value && receiver != null)
            {
                var rawPos = receiver.GetLatestPosition();
                var interpolatedPos = positionInterpolator.Update(rawPos, Time.deltaTime);
                var headRotQ = QuaternionUtils.FromYawPitchRoll(yaw, -pitch, roll);
                Vec3 posOffset = positionProcessor.Process(interpolatedPos, headRotQ, Time.deltaTime);
                // Translate in camera space: pre-multiply with translation matrix
                cam.worldToCameraMatrix = Matrix4x4.Translate(-posOffset.ToUnity()) * cam.worldToCameraMatrix;
            }

            // Store and override near clip plane
            storedNearClipPlane = cam.nearClipPlane;
            if (nearClipConfig != null && cam.nearClipPlane < nearClipConfig.Value)
            {
                cam.nearClipPlane = nearClipConfig.Value;
            }

            // Update reticle position — cam.transform.forward IS the game's aim direction
            // because view matrix modification doesn't touch the transform
            if (showReticleConfig != null && showReticleConfig.Value && ReticleCompensation.CanUpdateReticle())
            {
                ReticleCompensation.UpdateReticlePosition(cam);
            }
            else
            {
                ReticleCompensation.ResetReticlePosition();
            }

            if (!hasLoggedFirstApplication)
            {
                PeakHeadTrackingPlugin.Logger?.LogInfo($"[ApplyHeadTracking] SUCCESS! Applied via ViewMatrixModifier: Yaw={yaw:F2}, Pitch={pitch:F2}, Roll={roll:F2}");
                hasLoggedFirstApplication = true;
            }
        }

        /// <summary>
        /// Post-render callback: reset view matrix so game logic sees unmodified camera.
        /// </summary>
        private static void OnPostRender(UnityEngine.Camera cam)
        {
            // Only restore for main camera
            var mainCamera = UnityEngine.Camera.main;
            if (mainCamera == null || cam != mainCamera)
            {
                return;
            }

            // Only reset if we actually modified the matrix this frame
            if (!matrixModifiedThisFrame)
            {
                return;
            }
            matrixModifiedThisFrame = false;

            // Reset view matrix back to auto-calculated mode
            ViewMatrixModifier.Reset(cam);

            // Restore near clip plane
            cam.nearClipPlane = storedNearClipPlane;
        }

        /// <summary>
        /// Log diagnostic message at a limited rate to avoid spam.
        /// Logs at most once every DiagnosticLogInterval frames.
        /// </summary>
        private static void LogDiagnostic(string message)
        {
            int currentFrame = Time.frameCount;
            if (currentFrame - lastDiagnosticFrame >= DiagnosticLogInterval)
            {
                lastDiagnosticFrame = currentFrame;
                PeakHeadTrackingPlugin.Logger?.LogWarning(message);
            }
        }
    }
}
