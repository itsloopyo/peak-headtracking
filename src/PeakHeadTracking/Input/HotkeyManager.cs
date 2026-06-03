using UnityEngine;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Unity.Extensions;
using PeakHeadTracking.Config;
using PeakHeadTracking.Camera;

namespace PeakHeadTracking.Input
{
    /// <summary>
    /// Manages hotkey input for runtime control.
    /// Each standard action has two equivalent bindings (nav-cluster + Ctrl+Shift chord),
    /// using the shared ChordHotkeys letter assignments from cameraunlock-core.
    /// </summary>
    public class HotkeyManager : MonoBehaviour
    {
        private ModConfiguration config;
        private CameraController cameraController;
        private OpenTrackReceiver coreReceiver;

        // Three-state cycle index: 0 = full, 1 = rotation only, 2 = position only.
        private int trackingModeIndex = 0;

        public void Initialize(ModConfiguration modConfig, CameraController camController, OpenTrackReceiver trackReceiver)
        {
            config = modConfig;
            cameraController = camController;
            coreReceiver = trackReceiver;

            PeakHeadTrackingPlugin.Logger.LogDebug("HotkeyManager initialized");
        }

        private void Update()
        {
            if (config == null) return;

            if (ChordHotkeys.IsActionPressed(config.ToggleTrackingKey.Value, ChordHotkeys.ToggleLetter))
            {
                ToggleTracking();
            }

            if (ChordHotkeys.IsActionPressed(config.RecenterKey.Value, ChordHotkeys.RecenterLetter))
            {
                cameraController.RecenterView();
                PeakHeadTrackingPlugin.Logger.LogInfo("View recentered");
            }

            if (UnityEngine.Input.GetKeyDown(config.ReloadConfigKey.Value))
            {
                ReloadConfig();
            }

            if (ChordHotkeys.IsActionPressed(config.TogglePositionKey.Value, ChordHotkeys.PositionLetter))
            {
                CycleTrackingMode();
            }

            if (ChordHotkeys.IsActionPressed(config.YawModeKey.Value, ChordHotkeys.FourthToggleLetter))
            {
                ToggleYawMode();
            }
        }

        private void ToggleTracking()
        {
            bool newState = !config.TrackingEnabled.Value;
            config.TrackingEnabled.Value = newState;

            if (newState && !coreReceiver.IsReceiving && !coreReceiver.IsFailed)
            {
                coreReceiver.Start(config.UdpPort.Value);
            }

            cameraController.SetTrackingEnabled(newState);

            PeakHeadTrackingPlugin.Logger.LogInfo($"Tracking toggled: {(newState ? "ON" : "OFF")}");
        }

        private void ReloadConfig()
        {
            config.Reload();

            coreReceiver.Dispose();
            coreReceiver.Start(config.UdpPort.Value);

            PeakHeadTrackingPlugin.Logger.LogInfo("Configuration reloaded");
        }

        /// <summary>
        /// Cycle through the three tracking modes:
        ///   0: full head tracking (rotation + position)
        ///   1: rotation only (position disabled)
        ///   2: position only (rotation disabled)
        /// Bound to TogglePositionKey (default Page Up) and Ctrl+Shift+G.
        /// </summary>
        private void CycleTrackingMode()
        {
            trackingModeIndex = (trackingModeIndex + 1) % 3;

            bool rotation;
            bool position;
            string label;
            switch (trackingModeIndex)
            {
                case 1:
                    rotation = true;
                    position = false;
                    label = "rotation only (position disabled)";
                    break;
                case 2:
                    rotation = false;
                    position = true;
                    label = "position only (rotation disabled)";
                    break;
                default:
                    rotation = true;
                    position = true;
                    label = "full (rotation + position)";
                    break;
            }

            config.PositionEnabled.Value = position;
            Patches.CameraPatches.SetRotationEnabled(rotation);
            PeakHeadTrackingPlugin.Logger.LogInfo($"Tracking mode: {label}");
        }

        /// <summary>
        /// Toggle world-space (horizon-locked) vs camera-local yaw.
        /// Bound to YawModeKey (default Page Down) and Ctrl+Shift+H.
        /// </summary>
        private void ToggleYawMode()
        {
            bool newWorldSpace = !config.WorldSpaceYaw.Value;
            config.WorldSpaceYaw.Value = newWorldSpace;
            PeakHeadTrackingPlugin.Logger.LogInfo($"Yaw mode: {(newWorldSpace ? "world-space (horizon-locked)" : "camera-local")}");
        }
    }
}
