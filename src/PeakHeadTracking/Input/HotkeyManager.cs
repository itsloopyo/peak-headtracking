using System;
using CameraUnlock.Core.Input;
using CameraUnlock.Core.Protocol;
using CameraUnlock.Core.Tracking;
using CameraUnlock.Core.Unity.Extensions;
using PeakHeadTracking.Camera;
using PeakHeadTracking.Config;
using UnityEngine;

namespace PeakHeadTracking.Input
{
    /// <summary>
    /// Polls the hotkey lists from CameraUnlock.ini. Every entry of a list fires its action, the
    /// Ctrl+Shift chords included. The tracking mode and the yaw mode are saved the moment they
    /// change; End changes the session only.
    /// </summary>
    public class HotkeyManager : MonoBehaviour
    {
        private PeakHeadTrackingPlugin plugin;
        private CameraController cameraController;
        private OpenTrackReceiver coreReceiver;

        private KeyBinding[] toggleKeys = new KeyBinding[0];
        private KeyBinding[] cycleTrackingModeKeys = new KeyBinding[0];
        private KeyBinding[] yawModeKeys = new KeyBinding[0];
        private KeyBinding[] reloadConfigKeys = new KeyBinding[0];

        private TrackingMode trackingMode;
        private bool worldSpaceYaw;

        internal void Initialize(PeakHeadTrackingPlugin owner, CameraController camController, OpenTrackReceiver trackReceiver)
        {
            plugin = owner;
            cameraController = camController;
            coreReceiver = trackReceiver;

            PeakHeadTrackingPlugin.Logger.LogDebug("HotkeyManager initialized");
        }

        /// <summary>Takes the hotkeys, the tracking mode and the yaw mode from a loaded config.</summary>
        internal void Apply(PeakConfig config)
        {
            toggleKeys = Parse("ToggleKey", config.ToggleKeyName);
            cycleTrackingModeKeys = Parse("CycleTrackingModeKey", config.CycleTrackingModeKeyName);
            yawModeKeys = Parse("YawModeKey", config.YawModeKeyName);
            reloadConfigKeys = Parse("ReloadConfigKey", config.ReloadConfigKeyName);
            trackingMode = TrackingModeChannels.Decode(config.RotationEnabled, config.PositionEnabled).Value;
            worldSpaceYaw = config.WorldSpaceYaw;
        }

        // The table's hotkey codec has read every list of a loaded file, and the legacy import
        // writes only key lists, so a list that does not parse is a bug.
        private static KeyBinding[] Parse(string row, string text)
        {
            KeyBinding[] bindings;
            string error;
            if (!KeyBindings.TryParse(text, out bindings, out error))
                throw new InvalidOperationException(row + "=" + text + " is not a hotkey list: " + error);
            return bindings;
        }

        private void Update()
        {
            if (plugin == null || !UnityEngine.Input.anyKeyDown) return;

            if (KeyBindingInput.IsTriggered(toggleKeys))
            {
                ToggleTracking();
            }

            if (KeyBindingInput.IsTriggered(reloadConfigKeys))
            {
                plugin.ReloadConfig();
            }

            if (KeyBindingInput.IsTriggered(cycleTrackingModeKeys))
            {
                CycleTrackingMode();
            }

            if (KeyBindingInput.IsTriggered(yawModeKeys))
            {
                ToggleYawMode();
            }
        }

        /// <summary>Turns head tracking on or off for this session. Never saved.</summary>
        private void ToggleTracking()
        {
            bool newState = !cameraController.IsTrackingEnabled;

            if (newState && !coreReceiver.IsReceiving && !coreReceiver.IsFailed)
            {
                coreReceiver.Start(plugin.UdpPort);
            }

            cameraController.SetTrackingEnabled(newState);

            PeakHeadTrackingPlugin.Logger.LogInfo($"Tracking toggled: {(newState ? "ON" : "OFF")}");
        }

        /// <summary>
        /// Cycles rotation and position, rotation only, position only, and saves the mode as the
        /// RotationEnabled and PositionEnabled pair.
        /// </summary>
        private void CycleTrackingMode()
        {
            trackingMode = (TrackingMode)(((int)trackingMode + 1) % 3);
            bool rotation, position;
            TrackingModeChannels.Encode(trackingMode, out rotation, out position);

            Patches.CameraPatches.SetRotationEnabled(rotation);
            Patches.CameraPatches.SetPositionEnabled(position);
            PeakHeadTrackingPlugin.Logger.LogInfo($"Tracking mode: {trackingMode.Description()}");

            plugin.SaveConfig(c =>
            {
                c.RotationEnabled = rotation;
                c.PositionEnabled = position;
            });
        }

        /// <summary>
        /// Switches between world-space (horizon-locked) and camera-local yaw, and saves it as
        /// WorldSpaceYaw.
        /// </summary>
        private void ToggleYawMode()
        {
            worldSpaceYaw = !worldSpaceYaw;
            bool saved = worldSpaceYaw;
            Patches.CameraPatches.SetWorldSpaceYaw(saved);
            PeakHeadTrackingPlugin.Logger.LogInfo($"Yaw mode: {(saved ? "world-space (horizon-locked)" : "camera-local")}");

            plugin.SaveConfig(c => c.WorldSpaceYaw = saved);
        }
    }
}
