using CameraUnlock.Core.Unity.Rendering;
using CameraUnlock.Core.Unity.Tracking;
using CameraUnlock.Core.Unity.Utilities;
using UnityEngine;

namespace PeakHeadTracking.Patches
{
    /// <summary>
    /// Head tracking using VIEW MATRIX modification.
    ///
    /// CameraController runs the rotation and position pipelines once per frame in LateUpdate
    /// and publishes the result here; the render callback applies it via ViewMatrixModifier.
    ///
    /// This ONLY affects rendering - game logic (aiming, movement, reticle) remains unchanged.
    /// The camera transform is NEVER modified - only worldToCameraMatrix is changed.
    /// Reticle compensation moves the crosshair to show where you're actually aiming.
    /// Uses RenderPipelineManager.beginCameraRendering for Unity 6 / SRP compatibility.
    /// </summary>
    public static class CameraPatches
    {
        // Camera.main calls FindGameObjectWithTag internally; cache it per frame since
        // render callbacks fire once per camera per frame (shadows, reflections, UI cams)
        private static readonly PerFrameCache<UnityEngine.Camera> mainCameraCache =
            new PerFrameCache<UnityEngine.Camera>(() => UnityEngine.Camera.main);

        /// <summary>
        /// The main camera, fetched at most once per frame.
        /// </summary>
        internal static UnityEngine.Camera MainCamera => mainCameraCache.Get();

        private static bool positionEnabled;

        // Yaw mode: true = world-space (horizon-locked), false = camera-local
        private static bool worldSpaceYaw;

        // Near clip plane minimum, [Camera] MinNearClip
        private static float minNearClip;
        private static float storedNearClipPlane;

        /// <summary>Enable or disable positional head tracking. Rotation is gated separately.</summary>
        public static void SetPositionEnabled(bool enabled)
        {
            positionEnabled = enabled;
        }

        internal static bool PositionEnabled => positionEnabled;

        public static void SetNearClip(float minimum)
        {
            minNearClip = minimum;
        }

        public static void SetWorldSpaceYaw(bool worldSpace)
        {
            worldSpaceYaw = worldSpace;
        }

        private static bool headTrackingEnabled = false;
        private static bool rotationEnabled;
        private static bool hasLoggedFirstApplication = false;

        // Written by CameraController.LateUpdate, zero on any channel the tracking mode has off.
        private static float processedYaw = 0f;
        private static float processedPitch = 0f;
        private static float processedRoll = 0f;

        // Position offset in the processor's view-space convention (X=right, Y=up, negative Z
        // is the forward lean), relative to the clean camera.
        private static Vector3 processedPosition = Vector3.zero;

        // Callback state - now managed by RenderPipelineHelper
        private static bool callbackRegistered = false;

        // Tracks whether OnPreRender actually modified the view matrix this frame,
        // so OnPostRender only resets when needed (avoids corrupting splash/loading cameras)
        private static bool matrixModifiedThisFrame = false;

        // Diagnostic logging state
        private static int lastDiagnosticFrame = -1;
        private const int DiagnosticLogInterval = 300; // Log every 300 frames (~5 seconds at 60fps)

        internal static float ProcessedYaw => processedYaw;
        internal static float ProcessedPitch => processedPitch;
        internal static float ProcessedRoll => processedRoll;
        internal static Vector3 ProcessedPositionOffset => processedPosition;
        internal static bool WorldSpaceYaw => worldSpaceYaw;

        /// <summary>
        /// Whether the render has anything to apply. A tracker that has sent nothing yields an
        /// exact zero pose, and the render then leaves the camera, near clip included, alone.
        /// </summary>
        internal static bool HasPoseToApply =>
            processedYaw != 0f || processedPitch != 0f || processedRoll != 0f || processedPosition != Vector3.zero;

        /// <summary>
        /// The near clip plane the render enforces for a given base value (it never lowers the
        /// plane, only raises it to the configured minimum). Dependent passes such as the fog
        /// quad must build against this same plane or they get clipped by the render.
        /// </summary>
        internal static float GetEffectiveNearClip(float baseNearClip)
        {
            return baseNearClip < minNearClip ? minNearClip : baseNearClip;
        }

        /// <summary>
        /// Publish this frame's processed pose. Called from CameraController.LateUpdate, which
        /// zeroes the channels the tracking mode has off.
        /// </summary>
        public static void SetProcessedPose(float yaw, float pitch, float roll, Vector3 position)
        {
            processedYaw = yaw;
            processedPitch = pitch;
            processedRoll = roll;
            processedPosition = position;
        }

        /// <summary>
        /// Enable or disable rotational head tracking. Position tracking is gated separately.
        /// </summary>
        public static void SetRotationEnabled(bool enabled)
        {
            rotationEnabled = enabled;
        }

        internal static bool RotationEnabled => rotationEnabled;

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
                SetProcessedPose(0f, 0f, 0f, Vector3.zero);
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
        /// Check if head tracking is enabled
        /// </summary>
        public static bool IsHeadTrackingEnabled()
        {
            return headTrackingEnabled;
        }

        /// <summary>
        /// The camera rotation the render draws from, for passes that read the transform
        /// instead of the view matrix. Mirrors the two ViewMatrixModifier calls in OnPreRender.
        /// </summary>
        internal static Quaternion ComposeTrackedRotation(Quaternion clean, float yaw, float pitch, float roll)
        {
            if (worldSpaceYaw)
            {
                return Quaternion.AngleAxis(yaw, Vector3.up) * clean * Quaternion.Euler(-pitch, 0f, -roll);
            }

            // ApplyHeadRotation rotates view space, which Unity flips in z relative to the
            // transform: the same rotation seen from the transform keeps x and y and negates z.
            Quaternion view = Quaternion.Euler(-pitch, yaw, roll);
            return clean * new Quaternion(view.x, view.y, -view.z, view.w);
        }

        /// <summary>
        /// Pre-render callback: apply head tracking via view matrix modification.
        /// The camera transform is NEVER modified - only worldToCameraMatrix changes.
        /// </summary>
        private static void OnPreRender(UnityEngine.Camera cam)
        {
            // Only apply to the main camera
            var mainCamera = MainCamera;
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

            // Don't apply head tracking during loading/splash screens or when not in gameplay
            if (GameplayStateDetection.ShouldSkipHeadTracking())
            {
                // Reset reticle to center when not in active gameplay
                ReticleCompensation.ResetReticlePosition();
                return;
            }

            if (!HasPoseToApply)
                return;

            float yaw = processedYaw;
            float pitch = processedPitch;
            float roll = processedRoll;

            // World-space (default): yaw rotates around world up, pitch/roll camera-local
            //   - looking down + yawing still pans across the floor (horizon-stable).
            // Camera-local: all three axes composed and applied in camera space
            //   - yaw at extreme pitches rolls/leans the view.
            // Both take the same -roll: ApplyHeadRotation negates roll in view space, which is
            // the same camera roll as the unnegated roll ApplyHeadRotationDecomposed applies.
            // The position offset is applied in the clean camera's frame, so a lean follows
            // the body rather than the head-rotated view.
            // cam.transform.forward (the game's aim direction) is unchanged in both modes.
            if (worldSpaceYaw)
            {
                ViewMatrixModifier.ApplyHeadRotationDecomposed(cam, yaw, -pitch, -roll, processedPosition);
            }
            else
            {
                ViewMatrixModifier.ApplyHeadRotation(cam, yaw, -pitch, -roll, processedPosition);
            }
            matrixModifiedThisFrame = true;

            // Store and override near clip plane
            storedNearClipPlane = cam.nearClipPlane;
            if (cam.nearClipPlane < minNearClip)
            {
                cam.nearClipPlane = minNearClip;
            }

            // The crosshair always follows the aim: cam.transform.forward IS the game's aim
            // direction because view matrix modification doesn't touch the transform
            if (ReticleCompensation.CanUpdateReticle())
            {
                ReticleCompensation.UpdateReticlePosition(cam);
            }
            else
            {
                ReticleCompensation.ResetReticlePosition();
            }

            if (!hasLoggedFirstApplication)
            {
                PeakHeadTrackingPlugin.Logger?.LogInfo($"[ApplyHeadTracking] SUCCESS! Applied via ViewMatrixModifier: Yaw={yaw:F2}, Pitch={pitch:F2}, Roll={roll:F2}, Position={processedPosition}");
                hasLoggedFirstApplication = true;
            }
        }

        /// <summary>
        /// Post-render callback: reset view matrix so game logic sees unmodified camera.
        /// </summary>
        private static void OnPostRender(UnityEngine.Camera cam)
        {
            // Only restore for main camera
            var mainCamera = MainCamera;
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
