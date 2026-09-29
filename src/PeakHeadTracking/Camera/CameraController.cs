using CameraUnlock.Core.Data;
using CameraUnlock.Core.Math;
using CameraUnlock.Core.Processing;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Extensions;
using UnityEngine;

namespace PeakHeadTracking.Camera
{
    /// <summary>
    /// Owns the tracking pipelines: raw pose -> interpolate -> process -> CameraPatches, once
    /// per frame. CameraPatches applies the result via render callbacks.
    /// ExecutionOrder 1000 runs this after MainCameraMovement (500) and before CameraQuad
    /// (100000), which mirrors this frame's pose onto the fog quad.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class CameraController : MonoBehaviour
    {
        private OpenTrackReceiver coreReceiver;
        private TrackingProcessor processor;
        private PoseInterpolator interpolator;
        private PositionProcessor positionProcessor;
        private PositionInterpolator positionInterpolator;

        private const int DebugLogIntervalFrames = 120;

        // Tracking state
        private bool isTrackingActive = false;
        private bool hasLoggedFirstPacket = false;

        public void Initialize(OpenTrackReceiver trackReceiver, TrackingProcessor trackingProcessor, PoseInterpolator poseInterpolator,
            PositionProcessor posProcessor, PositionInterpolator posInterpolator)
        {
            coreReceiver = trackReceiver;
            processor = trackingProcessor;
            interpolator = poseInterpolator;
            positionProcessor = posProcessor;
            positionInterpolator = posInterpolator;

            PeakHeadTrackingPlugin.Logger.LogDebug("CameraController initialized");
        }

        /// <summary>
        /// Unity LateUpdate - Polls receiver, runs processing pipeline, writes to CameraPatches.
        /// </summary>
        private void LateUpdate()
        {
            // Latched once: the only line in the log that proves tracker packets
            // reached the game. Outside the isTrackingActive gate so a user who
            // started with tracking toggled off can still see the tracker arrive.
            if (!hasLoggedFirstPacket && coreReceiver.IsReceiving)
            {
                hasLoggedFirstPacket = true;
                PeakHeadTrackingPlugin.Logger.LogInfo(
                    $"Tracker data received ({(coreReceiver.IsRemoteConnection ? "remote" : "local")} source)");
            }

            if (isTrackingActive)
            {
                // Real time, not game time: PEAK runs at timeScale 2 under a run setting and at
                // 0 while paused offline, and the head moves at the same speed either way.
                float dt = Time.unscaledDeltaTime;

                // Locality picks LocalSmoothing vs RemoteSmoothing. Re-read every
                // frame so swapping between a local tracker and a phone on the
                // network takes effect without a restart.
                bool remote = coreReceiver.IsRemoteConnection;
                processor.IsRemoteConnection = remote;
                positionProcessor.IsRemoteConnection = remote;

                var interpolated = interpolator.Update(coreReceiver.GetLatestPose(), dt);
                var processed = processor.Process(interpolated, dt);

                var interpolatedPos = positionInterpolator.Update(coreReceiver.GetLatestPosition(), dt);
                Quat4 headRotQ = QuaternionUtils.FromYawPitchRoll(processed.Yaw, -processed.Pitch, processed.Roll);
                Vec3 position = positionProcessor.Process(interpolatedPos, headRotQ, dt);

                bool rotation = Patches.CameraPatches.RotationEnabled;
                Patches.CameraPatches.SetProcessedPose(
                    rotation ? processed.Yaw : 0f,
                    rotation ? processed.Pitch : 0f,
                    rotation ? processed.Roll : 0f,
                    Patches.CameraPatches.PositionEnabled ? position.ToUnity() : Vector3.zero);
            }

            if (DebugLogging && Time.frameCount % DebugLogIntervalFrames == 0)
            {
                LogDebugState();
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void LogDebugState()
        {
            PeakHeadTrackingPlugin.Logger.LogDebug(
                $"[CameraController] isTrackingActive={isTrackingActive}, yaw={Patches.CameraPatches.ProcessedYaw:F1}, " +
                $"pitch={Patches.CameraPatches.ProcessedPitch:F1}, roll={Patches.CameraPatches.ProcessedRoll:F1}, " +
                $"position={Patches.CameraPatches.ProcessedPositionOffset}");
        }

        /// <summary>
        /// Enable or disable tracking
        /// </summary>
        public void SetTrackingEnabled(bool enabled)
        {
            isTrackingActive = enabled;

            if (enabled)
            {
                // Reset processing pipeline for clean start
                processor.ResetSmoothing();
                interpolator.Reset();
                positionProcessor.ResetSmoothing();
                positionInterpolator.Reset();
            }

            Patches.CameraPatches.SetHeadTrackingEnabled(enabled);

            PeakHeadTrackingPlugin.Logger.LogInfo($"Tracking {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>[Logging] DebugLogging: log the tracking state every DebugLogIntervalFrames frames.</summary>
        public bool DebugLogging { get; set; }

        /// <summary>Whether head tracking is on this session, which End toggles.</summary>
        public bool IsTrackingEnabled => isTrackingActive;
    }
}
