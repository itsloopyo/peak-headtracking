using UnityEngine;
using CameraUnlock.Core.Protocol;
using PeakHeadTracking.Config;
using PeakHeadTracking.Camera;

namespace PeakHeadTracking.Input
{
    /// <summary>
    /// Manages hotkey input for runtime control
    /// </summary>
    public class HotkeyManager : MonoBehaviour
    {
        private ModConfiguration config;
        private CameraController cameraController;
        private OpenTrackReceiver coreReceiver;

        // Key press state tracking
        private bool wasTogglePressed = false;
        private bool wasRecenterPressed = false;
        private bool wasReloadPressed = false;
        private bool wasPositionTogglePressed = false;

        /// <summary>
        /// Initialize the hotkey manager
        /// </summary>
        public void Initialize(ModConfiguration modConfig, CameraController camController, OpenTrackReceiver trackReceiver)
        {
            config = modConfig;
            cameraController = camController;
            coreReceiver = trackReceiver;

            PeakHeadTrackingPlugin.Logger.LogDebug("HotkeyManager initialized");
        }

        /// <summary>
        /// Unity Update - check for hotkey presses
        /// </summary>
        private void Update()
        {
            if (config == null) return;
            HandleToggleTracking();
            HandleRecenterView();
            HandleReloadConfig();
            HandleTogglePosition();
        }

        /// <summary>
        /// Handle toggle tracking hotkey
        /// </summary>
        private void HandleToggleTracking()
        {
            KeyCode key = config.ToggleTrackingKey.Value;
            bool isPressed = UnityEngine.Input.GetKey(key);

            // Detect key down (transition from not pressed to pressed)
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

        /// <summary>
        /// Handle recenter view hotkey
        /// </summary>
        private void HandleRecenterView()
        {
            KeyCode key = config.RecenterKey.Value;
            bool isPressed = UnityEngine.Input.GetKey(key);

            // Detect key down
            if (isPressed && !wasRecenterPressed)
            {
                cameraController.RecenterView();
                PeakHeadTrackingPlugin.Logger.LogInfo("View recentered");
            }

            wasRecenterPressed = isPressed;
        }

        /// <summary>
        /// Handle reload configuration hotkey
        /// </summary>
        private void HandleReloadConfig()
        {
            KeyCode key = config.ReloadConfigKey.Value;
            bool isPressed = UnityEngine.Input.GetKey(key);

            // Detect key down
            if (isPressed && !wasReloadPressed)
            {
                config.Reload();

                // Restart receiver with new port
                coreReceiver.Dispose();
                coreReceiver.Start(config.UdpPort.Value);

                PeakHeadTrackingPlugin.Logger.LogInfo("Configuration reloaded");
            }

            wasReloadPressed = isPressed;
        }

        /// <summary>
        /// Handle toggle position hotkey
        /// </summary>
        private void HandleTogglePosition()
        {
            KeyCode key = config.TogglePositionKey.Value;
            bool isPressed = UnityEngine.Input.GetKey(key);

            if (isPressed && !wasPositionTogglePressed)
            {
                config.PositionEnabled.Value = !config.PositionEnabled.Value;
                PeakHeadTrackingPlugin.Logger.LogInfo($"Position tracking {(config.PositionEnabled.Value ? "enabled" : "disabled")}");
            }

            wasPositionTogglePressed = isPressed;
        }

        /// <summary>
        /// Reset key press states
        /// </summary>
        public void ResetStates()
        {
            wasTogglePressed = false;
            wasRecenterPressed = false;
            wasReloadPressed = false;
            wasPositionTogglePressed = false;
        }

        private void OnDisable()
        {
            ResetStates();
        }
    }
}
