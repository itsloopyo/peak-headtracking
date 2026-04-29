using System;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakHeadTracking.Config
{
    /// <summary>
    /// Defines all configurable settings for the mod
    /// </summary>
    public class ModConfiguration
    {
        private ConfigFile configFile;

        // Connection Settings
        public ConfigEntry<int> UdpPort { get; private set; }
        public ConfigEntry<int> ReconnectTimeout { get; private set; }
        public ConfigEntry<int> PacketBufferSize { get; private set; }

        // General Settings
        public ConfigEntry<bool> TrackingEnabled { get; private set; }
        public ConfigEntry<bool> EnableAudioFeedback { get; private set; }

        // Sensitivity Settings
        public ConfigEntry<float> YawSensitivity { get; private set; }
        public ConfigEntry<float> PitchSensitivity { get; private set; }
        public ConfigEntry<float> RollSensitivity { get; private set; }
        public ConfigEntry<bool> InvertYaw { get; private set; }
        public ConfigEntry<bool> InvertPitch { get; private set; }
        public ConfigEntry<bool> InvertRoll { get; private set; }

        // Rotation Limits
        public ConfigEntry<bool> EnablePitchLimits { get; private set; }
        public ConfigEntry<float> MinPitch { get; private set; }
        public ConfigEntry<float> MaxPitch { get; private set; }
        public ConfigEntry<bool> EnableRoll { get; private set; }
        public ConfigEntry<bool> EnableRollLimits { get; private set; }
        public ConfigEntry<float> MaxRoll { get; private set; }

        // Smoothing Settings
        public ConfigEntry<float> Smoothing { get; private set; }

        // Deadzone Settings
        public ConfigEntry<bool> EnableDeadzone { get; private set; }
        public ConfigEntry<float> DeadzoneYaw { get; private set; }
        public ConfigEntry<float> DeadzonePitch { get; private set; }
        public ConfigEntry<float> DeadzoneRoll { get; private set; }

        // Hotkey Settings
        public ConfigEntry<KeyCode> ToggleTrackingKey { get; private set; }
        public ConfigEntry<KeyCode> RecenterKey { get; private set; }
        public ConfigEntry<KeyCode> ReloadConfigKey { get; private set; }
        public ConfigEntry<KeyCode> TogglePositionKey { get; private set; }
        public ConfigEntry<KeyCode> ToggleReticleKey { get; private set; }

        // UI Settings
        public ConfigEntry<bool> ShowReticle { get; private set; }

        // Advanced Settings
        public ConfigEntry<bool> DebugLogging { get; private set; }
        public ConfigEntry<int> UpdateRate { get; private set; }
        public ConfigEntry<bool> MaintainRelativePosition { get; private set; }

        // Position Settings
        public ConfigEntry<bool> PositionEnabled { get; private set; }
        public ConfigEntry<float> PositionSensitivityX { get; private set; }
        public ConfigEntry<float> PositionSensitivityY { get; private set; }
        public ConfigEntry<float> PositionSensitivityZ { get; private set; }
        public ConfigEntry<float> PositionLimitX { get; private set; }
        public ConfigEntry<float> PositionLimitY { get; private set; }
        public ConfigEntry<float> PositionLimitZ { get; private set; }
        public ConfigEntry<float> PositionSmoothing { get; private set; }

        // Camera Settings
        public ConfigEntry<float> NearClipOverride { get; private set; }

        /// <summary>
        /// Initialize all configuration entries
        /// </summary>
        public void Initialize(ConfigFile config)
        {
            configFile = config;

            // Connection Settings
            UdpPort = config.Bind(
                ConfigCategories.CONNECTION,
                "UDP Port",
                4242,
                new ConfigDescription(
                    "Port number for OpenTrack UDP connection",
                    new AcceptableValueRange<int>(1024, 65535)
                )
            );

            ReconnectTimeout = config.Bind(
                ConfigCategories.CONNECTION,
                "Reconnect Timeout",
                5,
                new ConfigDescription(
                    "Seconds to wait before attempting reconnection",
                    new AcceptableValueRange<int>(1, 60)
                )
            );

            PacketBufferSize = config.Bind(
                ConfigCategories.CONNECTION,
                "Packet Buffer Size",
                100,
                new ConfigDescription(
                    "Maximum number of packets to buffer",
                    new AcceptableValueRange<int>(10, 500)
                )
            );

            // General Settings
            TrackingEnabled = config.Bind(
                ConfigCategories.GENERAL,
                "Tracking Enabled",
                true,
                "Enable head tracking on startup"
            );

            EnableAudioFeedback = config.Bind(
                ConfigCategories.GENERAL,
                "Enable Audio Feedback",
                true,
                "Play sounds for tracking state changes"
            );

            // Sensitivity Settings
            YawSensitivity = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Yaw Sensitivity",
                1.0f,
                new ConfigDescription(
                    "Yaw (left/right) rotation multiplier",
                    new AcceptableValueRange<float>(0.1f, 5.0f)
                )
            );

            PitchSensitivity = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Pitch Sensitivity",
                1.0f,
                new ConfigDescription(
                    "Pitch (up/down) rotation multiplier",
                    new AcceptableValueRange<float>(0.1f, 5.0f)
                )
            );

            RollSensitivity = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Roll Sensitivity",
                1.0f,
                new ConfigDescription(
                    "Roll (tilt) rotation multiplier",
                    new AcceptableValueRange<float>(0.1f, 5.0f)
                )
            );

            InvertYaw = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Invert Yaw",
                false,
                "Invert yaw (left/right) axis"
            );

            InvertPitch = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Invert Pitch",
                false,
                "Invert pitch (up/down) axis"
            );

            InvertRoll = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Invert Roll",
                false,
                "Invert roll (tilt) axis"
            );

            // Rotation Limits
            EnablePitchLimits = config.Bind(
                ConfigCategories.LIMITS,
                "Enable Pitch Limits",
                true,
                "Limit pitch rotation range"
            );

            MinPitch = config.Bind(
                ConfigCategories.LIMITS,
                "Minimum Pitch",
                -85f,
                new ConfigDescription(
                    "Minimum pitch angle (looking down)",
                    new AcceptableValueRange<float>(-90f, 0f)
                )
            );

            MaxPitch = config.Bind(
                ConfigCategories.LIMITS,
                "Maximum Pitch",
                85f,
                new ConfigDescription(
                    "Maximum pitch angle (looking up)",
                    new AcceptableValueRange<float>(0f, 90f)
                )
            );

            EnableRoll = config.Bind(
                ConfigCategories.LIMITS,
                "Enable Roll",
                true,
                "Enable roll (head tilt) rotation"
            );

            EnableRollLimits = config.Bind(
                ConfigCategories.LIMITS,
                "Enable Roll Limits",
                true,
                "Limit roll rotation range"
            );

            MaxRoll = config.Bind(
                ConfigCategories.LIMITS,
                "Maximum Roll",
                30f,
                new ConfigDescription(
                    "Maximum roll angle in either direction",
                    new AcceptableValueRange<float>(0f, 90f)
                )
            );

            // Smoothing Settings
            Smoothing = config.Bind(
                ConfigCategories.SMOOTHING,
                "Smoothing",
                0.0f,
                new ConfigDescription(
                    "Smoothing factor (higher = smoother but adds latency). " +
                    "Remote connections automatically use a minimum of 0.15 for network latency compensation.",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            );

            // Deadzone Settings
            EnableDeadzone = config.Bind(
                ConfigCategories.DEADZONE,
                "Enable Deadzone",
                false,
                "Ignore small movements near center"
            );

            DeadzoneYaw = config.Bind(
                ConfigCategories.DEADZONE,
                "Yaw Deadzone",
                0f,
                new ConfigDescription(
                    "Deadzone for yaw axis (degrees)",
                    new AcceptableValueRange<float>(0f, 10f)
                )
            );

            DeadzonePitch = config.Bind(
                ConfigCategories.DEADZONE,
                "Pitch Deadzone",
                0f,
                new ConfigDescription(
                    "Deadzone for pitch axis (degrees)",
                    new AcceptableValueRange<float>(0f, 10f)
                )
            );

            DeadzoneRoll = config.Bind(
                ConfigCategories.DEADZONE,
                "Roll Deadzone",
                0f,
                new ConfigDescription(
                    "Deadzone for roll axis (degrees)",
                    new AcceptableValueRange<float>(0f, 10f)
                )
            );

            // Hotkey Settings
            ToggleTrackingKey = config.Bind(
                ConfigCategories.HOTKEYS,
                "Toggle Tracking",
                KeyCode.End,
                "Key to enable/disable tracking"
            );

            RecenterKey = config.Bind(
                ConfigCategories.HOTKEYS,
                "Recenter View",
                KeyCode.Home,
                "Key to recenter the view"
            );

            ReloadConfigKey = config.Bind(
                ConfigCategories.HOTKEYS,
                "Reload Config",
                KeyCode.F12,
                "Key to reload configuration"
            );

            TogglePositionKey = config.Bind(
                ConfigCategories.HOTKEYS,
                "Toggle Position",
                KeyCode.PageUp,
                "Key to cycle tracking mode (full -> rotation only -> position only)"
            );

            ToggleReticleKey = config.Bind(
                ConfigCategories.HOTKEYS,
                "Toggle Reticle",
                KeyCode.Insert,
                "Key to toggle reticle compensation on/off"
            );

            // UI Settings
            ShowReticle = config.Bind(
                ConfigCategories.GENERAL,
                "Show Reticle",
                true,
                "Show reticle compensation (moves crosshair to show aim point during head tracking)"
            );

            // Advanced Settings
            DebugLogging = config.Bind(
                ConfigCategories.ADVANCED,
                "Debug Logging",
                false,
                "Enable detailed debug logging"
            );

            UpdateRate = config.Bind(
                ConfigCategories.ADVANCED,
                "Update Rate",
                60,
                new ConfigDescription(
                    "Target update rate in Hz",
                    new AcceptableValueRange<int>(30, 120)
                )
            );

            MaintainRelativePosition = config.Bind(
                ConfigCategories.ADVANCED,
                "Maintain Relative Position",
                true,
                "Maintain camera position relative to target"
            );

            // Position Settings
            PositionEnabled = config.Bind(
                ConfigCategories.GENERAL,
                "Position Enabled",
                true,
                "Enable positional tracking (lean in/out/side-to-side)"
            );

            PositionSensitivityX = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Position Sensitivity X",
                2.0f,
                new ConfigDescription("Multiplier for lateral (left/right) position", new AcceptableValueRange<float>(0f, 5.0f))
            );

            PositionSensitivityY = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Position Sensitivity Y",
                2.0f,
                new ConfigDescription("Multiplier for vertical (up/down) position", new AcceptableValueRange<float>(0f, 5.0f))
            );

            PositionSensitivityZ = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Position Sensitivity Z",
                2.0f,
                new ConfigDescription("Multiplier for depth (forward/back) position", new AcceptableValueRange<float>(0f, 5.0f))
            );

            PositionLimitX = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Position Limit X",
                0.30f,
                new ConfigDescription("Maximum lateral displacement in meters", new AcceptableValueRange<float>(0.01f, 0.5f))
            );

            PositionLimitY = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Position Limit Y",
                0.20f,
                new ConfigDescription("Maximum vertical displacement in meters", new AcceptableValueRange<float>(0.01f, 0.5f))
            );

            PositionLimitZ = config.Bind(
                ConfigCategories.SENSITIVITY,
                "Position Limit Z",
                0.40f,
                new ConfigDescription("Maximum depth displacement in meters", new AcceptableValueRange<float>(0.01f, 0.5f))
            );

            PositionSmoothing = config.Bind(
                ConfigCategories.SMOOTHING,
                "Position Smoothing",
                0.15f,
                new ConfigDescription("Smoothing for positional tracking (0 = instant, 1 = very slow)", new AcceptableValueRange<float>(0f, 1f))
            );

            NearClipOverride = config.Bind(
                ConfigCategories.ADVANCED,
                "Near Clip Override",
                0.15f,
                new ConfigDescription(
                    "Minimum near clip plane distance in meters. " +
                    "Prevents seeing through the character model during head bobbing.",
                    new AcceptableValueRange<float>(0.01f, 0.5f))
            );

        }

        /// <summary>
        /// Reload configuration from file
        /// </summary>
        public void Reload()
        {
            configFile?.Reload();
        }

        /// <summary>
        /// Save current configuration to file
        /// </summary>
        public void Save()
        {
            configFile?.Save();
        }
    }
}
