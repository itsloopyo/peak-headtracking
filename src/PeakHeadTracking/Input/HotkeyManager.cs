using UnityEngine;
using CameraUnlock.Core.Protocol;
using PeakHeadTracking.Config;
using PeakHeadTracking.Camera;

namespace PeakHeadTracking.Input
{
    /// <summary>
    /// Manages hotkey input for runtime control.
    /// Each standard action has two equivalent bindings (nav-cluster + Ctrl+Shift chord).
    /// </summary>
    public class HotkeyManager : MonoBehaviour
    {
        private ModConfiguration config;
        private CameraController cameraController;
        private OpenTrackReceiver coreReceiver;

        // Edge-detection state for each action (covers both binding sets per action)
        private bool wasTogglePressed = false;
        private bool wasRecenterPressed = false;
        private bool wasReloadPressed = false;
        private bool wasCyclePressed = false;
        private bool wasYawModePressed = false;

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
            HandleToggleTracking();
            HandleRecenterView();
            HandleReloadConfig();
            HandleCycleTrackingMode();
            HandleToggleYawMode();
        }

        private static bool IsChordHeld(KeyCode letter)
        {
            return (UnityEngine.Input.GetKey(KeyCode.LeftControl) || UnityEngine.Input.GetKey(KeyCode.RightControl))
                && (UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.RightShift))
                && UnityEngine.Input.GetKey(letter);
        }

        private void HandleToggleTracking()
        {
            bool isPressed = UnityEngine.Input.GetKey(config.ToggleTrackingKey.Value) || IsChordHeld(KeyCode.Y);

            if (isPressed && !wasTogglePressed)
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

            wasTogglePressed = isPressed;
        }

        private void HandleRecenterView()
        {
            bool isPressed = UnityEngine.Input.GetKey(config.RecenterKey.Value) || IsChordHeld(KeyCode.T);

            if (isPressed && !wasRecenterPressed)
            {
                cameraController.RecenterView();
                PeakHeadTrackingPlugin.Logger.LogInfo("View recentered");
            }

            wasRecenterPressed = isPressed;
        }

        private void HandleReloadConfig()
        {
            bool isPressed = UnityEngine.Input.GetKey(config.ReloadConfigKey.Value);

            if (isPressed && !wasReloadPressed)
            {
                config.Reload();

                coreReceiver.Dispose();
                coreReceiver.Start(config.UdpPort.Value);

                PeakHeadTrackingPlugin.Logger.LogInfo("Configuration reloaded");
            }

            wasReloadPressed = isPressed;
        }

        /// <summary>
        /// Cycle through the three tracking modes:
        ///   0: full head tracking (rotation + position)
        ///   1: rotation only (position disabled)
        ///   2: position only (rotation disabled)
        /// Bound to the legacy TogglePositionKey (default Page Up) and Ctrl+Shift+G.
        /// </summary>
        private void HandleCycleTrackingMode()
        {
            bool isPressed = UnityEngine.Input.GetKey(config.TogglePositionKey.Value) || IsChordHeld(KeyCode.G);

            if (isPressed && !wasCyclePressed)
            {
                trackingModeIndex = (trackingModeIndex + 1) % 3;
                ApplyTrackingMode();
            }

            wasCyclePressed = isPressed;
        }

        private void ApplyTrackingMode()
        {
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
        private void HandleToggleYawMode()
        {
            bool isPressed = UnityEngine.Input.GetKey(config.YawModeKey.Value) || IsChordHeld(KeyCode.H);

            if (isPressed && !wasYawModePressed)
            {
                bool newWorldSpace = !config.WorldSpaceYaw.Value;
                config.WorldSpaceYaw.Value = newWorldSpace;
                PeakHeadTrackingPlugin.Logger.LogInfo($"Yaw mode: {(newWorldSpace ? "world-space (horizon-locked)" : "camera-local")}");
            }

            wasYawModePressed = isPressed;
        }

        public void ResetStates()
        {
            wasTogglePressed = false;
            wasRecenterPressed = false;
            wasReloadPressed = false;
            wasCyclePressed = false;
            wasYawModePressed = false;
        }

        private void OnDisable()
        {
            ResetStates();
        }
    }
}
