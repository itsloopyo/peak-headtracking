using System.IO;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using UnityEngine;

namespace PeakHeadTracking.Legacy
{
    /// <summary>
    /// Frozen: the Bind calls the last build before the canonical config ran on the plugin's
    /// <see cref="ConfigFile"/>, each definition's section, key, type, default, description and
    /// acceptable values unchanged. Reads BepInEx\config\com.cameraunlock.peak.headtracking.cfg
    /// exactly as that build did and writes nothing. Never edit this file.
    /// </summary>
    internal static class LegacyConfigReader
    {
        public const string Connection = "01. Connection";
        public const string General = "02. General";
        public const string Sensitivity = "03. Sensitivity";
        public const string Limits = "04. Rotation Limits";
        public const string Smoothing = "05. Smoothing";
        public const string Deadzone = "06. Deadzone";
        public const string Hotkeys = "07. Hotkeys";
        public const string Advanced = "08. Advanced";

        /// <summary>Every section and key <see cref="Read"/> binds, in the order it binds them.</summary>
        public static readonly LegacyKey[] Keys =
        {
            new LegacyKey(Connection, "UDP Port"),
            new LegacyKey(Connection, "Reconnect Timeout"),
            new LegacyKey(Connection, "Packet Buffer Size"),
            new LegacyKey(General, "Tracking Enabled"),
            new LegacyKey(General, "Enable Audio Feedback"),
            new LegacyKey(General, "World Space Yaw"),
            new LegacyKey(Sensitivity, "Yaw Sensitivity"),
            new LegacyKey(Sensitivity, "Pitch Sensitivity"),
            new LegacyKey(Sensitivity, "Roll Sensitivity"),
            new LegacyKey(Sensitivity, "Invert Yaw"),
            new LegacyKey(Sensitivity, "Invert Pitch"),
            new LegacyKey(Sensitivity, "Invert Roll"),
            new LegacyKey(Limits, "Enable Pitch Limits"),
            new LegacyKey(Limits, "Minimum Pitch"),
            new LegacyKey(Limits, "Maximum Pitch"),
            new LegacyKey(Limits, "Enable Roll"),
            new LegacyKey(Limits, "Enable Roll Limits"),
            new LegacyKey(Limits, "Maximum Roll"),
            new LegacyKey(Smoothing, "Local Smoothing"),
            new LegacyKey(Smoothing, "Remote Smoothing"),
            new LegacyKey(Deadzone, "Enable Deadzone"),
            new LegacyKey(Deadzone, "Yaw Deadzone"),
            new LegacyKey(Deadzone, "Pitch Deadzone"),
            new LegacyKey(Deadzone, "Roll Deadzone"),
            new LegacyKey(Hotkeys, "Toggle Tracking"),
            new LegacyKey(Hotkeys, "Reload Config"),
            new LegacyKey(Hotkeys, "Toggle Position"),
            new LegacyKey(Hotkeys, "Toggle Reticle"),
            new LegacyKey(Hotkeys, "Yaw Mode Key"),
            new LegacyKey(General, "Show Reticle"),
            new LegacyKey(Advanced, "Debug Logging"),
            new LegacyKey(Advanced, "Update Rate"),
            new LegacyKey(Advanced, "Maintain Relative Position"),
            new LegacyKey(General, "Position Enabled"),
            new LegacyKey(Sensitivity, "Position Sensitivity X"),
            new LegacyKey(Sensitivity, "Position Sensitivity Y"),
            new LegacyKey(Sensitivity, "Position Sensitivity Z"),
            new LegacyKey(Sensitivity, "Position Limit X"),
            new LegacyKey(Sensitivity, "Position Limit Y"),
            new LegacyKey(Sensitivity, "Position Limit Z"),
            new LegacyKey(Sensitivity, "Position Limit Z Back"),
            new LegacyKey(Advanced, "Near Clip Override"),
        };

        /// <summary>
        /// Reads the settings the way the published build did, through BepInEx's own parser and
        /// clamping. BepInEx read the .cfg once already, in the ConfigFile constructor, so this
        /// turns saving off, reads the file again and then binds. Returns the defaults when the
        /// file is absent, as that build ran on a first start.
        /// </summary>
        public static LegacyConfig Read(ConfigFile config)
        {
            config.SaveOnConfigSet = false;
            if (File.Exists(config.ConfigFilePath))
            {
                config.Reload();
            }

            var c = new LegacyConfig();

            c.UdpPort = config.Bind(
                Connection,
                "UDP Port",
                c.UdpPort,
                new ConfigDescription(
                    "Port number for OpenTrack UDP connection",
                    new AcceptableValueRange<int>(1024, 65535)
                )
            ).Value;

            c.ReconnectTimeout = config.Bind(
                Connection,
                "Reconnect Timeout",
                c.ReconnectTimeout,
                new ConfigDescription(
                    "Seconds to wait before attempting reconnection",
                    new AcceptableValueRange<int>(1, 60)
                )
            ).Value;

            c.PacketBufferSize = config.Bind(
                Connection,
                "Packet Buffer Size",
                c.PacketBufferSize,
                new ConfigDescription(
                    "Maximum number of packets to buffer",
                    new AcceptableValueRange<int>(10, 500)
                )
            ).Value;

            c.TrackingEnabled = config.Bind(
                General,
                "Tracking Enabled",
                c.TrackingEnabled,
                "Enable head tracking on startup"
            ).Value;

            c.EnableAudioFeedback = config.Bind(
                General,
                "Enable Audio Feedback",
                c.EnableAudioFeedback,
                "Play sounds for tracking state changes"
            ).Value;

            c.WorldSpaceYaw = config.Bind(
                General,
                "World Space Yaw",
                c.WorldSpaceYaw,
                "Yaw mode: true = horizon-locked yaw (default), false = camera-local. " +
                "Horizon-locked rotates yaw around the world up-axis so 'up' stays constant. " +
                "Camera-local rotates around the camera's current up-axis (rolls/leans at extreme pitches)."
            ).Value;

            c.YawSensitivity = config.Bind(
                Sensitivity,
                "Yaw Sensitivity",
                c.YawSensitivity,
                new ConfigDescription(
                    "Yaw (left/right) rotation multiplier",
                    new AcceptableValueRange<float>(0.1f, 5.0f)
                )
            ).Value;

            c.PitchSensitivity = config.Bind(
                Sensitivity,
                "Pitch Sensitivity",
                c.PitchSensitivity,
                new ConfigDescription(
                    "Pitch (up/down) rotation multiplier",
                    new AcceptableValueRange<float>(0.1f, 5.0f)
                )
            ).Value;

            c.RollSensitivity = config.Bind(
                Sensitivity,
                "Roll Sensitivity",
                c.RollSensitivity,
                new ConfigDescription(
                    "Roll (tilt) rotation multiplier",
                    new AcceptableValueRange<float>(0.1f, 5.0f)
                )
            ).Value;

            c.InvertYaw = config.Bind(
                Sensitivity,
                "Invert Yaw",
                c.InvertYaw,
                "Invert yaw (left/right) axis"
            ).Value;

            c.InvertPitch = config.Bind(
                Sensitivity,
                "Invert Pitch",
                c.InvertPitch,
                "Invert pitch (up/down) axis"
            ).Value;

            c.InvertRoll = config.Bind(
                Sensitivity,
                "Invert Roll",
                c.InvertRoll,
                "Invert roll (tilt) axis"
            ).Value;

            c.EnablePitchLimits = config.Bind(
                Limits,
                "Enable Pitch Limits",
                c.EnablePitchLimits,
                "Limit pitch rotation range"
            ).Value;

            c.MinPitch = config.Bind(
                Limits,
                "Minimum Pitch",
                c.MinPitch,
                new ConfigDescription(
                    "Minimum pitch angle (looking down)",
                    new AcceptableValueRange<float>(-90f, 0f)
                )
            ).Value;

            c.MaxPitch = config.Bind(
                Limits,
                "Maximum Pitch",
                c.MaxPitch,
                new ConfigDescription(
                    "Maximum pitch angle (looking up)",
                    new AcceptableValueRange<float>(0f, 90f)
                )
            ).Value;

            c.EnableRoll = config.Bind(
                Limits,
                "Enable Roll",
                c.EnableRoll,
                "Enable roll (head tilt) rotation"
            ).Value;

            c.EnableRollLimits = config.Bind(
                Limits,
                "Enable Roll Limits",
                c.EnableRollLimits,
                "Limit roll rotation range"
            ).Value;

            c.MaxRoll = config.Bind(
                Limits,
                "Maximum Roll",
                c.MaxRoll,
                new ConfigDescription(
                    "Maximum roll angle in either direction",
                    new AcceptableValueRange<float>(0f, 90f)
                )
            ).Value;

            c.LocalSmoothing = config.Bind(
                Smoothing,
                "Local Smoothing",
                c.LocalSmoothing,
                new ConfigDescription(
                    "Smoothing applied when the tracker runs on this machine (loopback). " +
                    "0 = no smoothing, 1 = heavy. Covers rotation and position.",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            ).Value;

            c.RemoteSmoothing = config.Bind(
                Smoothing,
                "Remote Smoothing",
                c.RemoteSmoothing,
                new ConfigDescription(
                    "Smoothing applied when the tracker is a remote device on the network. " +
                    "0 = no smoothing, 1 = heavy. Covers rotation and position.",
                    new AcceptableValueRange<float>(0f, 1f)
                )
            ).Value;

            c.EnableDeadzone = config.Bind(
                Deadzone,
                "Enable Deadzone",
                c.EnableDeadzone,
                "Ignore small movements near center"
            ).Value;

            c.DeadzoneYaw = config.Bind(
                Deadzone,
                "Yaw Deadzone",
                c.DeadzoneYaw,
                new ConfigDescription(
                    "Deadzone for yaw axis (degrees)",
                    new AcceptableValueRange<float>(0f, 10f)
                )
            ).Value;

            c.DeadzonePitch = config.Bind(
                Deadzone,
                "Pitch Deadzone",
                c.DeadzonePitch,
                new ConfigDescription(
                    "Deadzone for pitch axis (degrees)",
                    new AcceptableValueRange<float>(0f, 10f)
                )
            ).Value;

            c.DeadzoneRoll = config.Bind(
                Deadzone,
                "Roll Deadzone",
                c.DeadzoneRoll,
                new ConfigDescription(
                    "Deadzone for roll axis (degrees)",
                    new AcceptableValueRange<float>(0f, 10f)
                )
            ).Value;

            c.ToggleTrackingKey = config.Bind(
                Hotkeys,
                "Toggle Tracking",
                c.ToggleTrackingKey,
                "Key to enable/disable tracking"
            ).Value;

            c.ReloadConfigKey = config.Bind(
                Hotkeys,
                "Reload Config",
                c.ReloadConfigKey,
                "Key to reload configuration"
            ).Value;

            c.TogglePositionKey = config.Bind(
                Hotkeys,
                "Toggle Position",
                c.TogglePositionKey,
                "Key to cycle tracking mode (full -> rotation only -> position only)"
            ).Value;

            c.ToggleReticleKey = config.Bind(
                Hotkeys,
                "Toggle Reticle",
                c.ToggleReticleKey,
                "Key to toggle reticle compensation on/off"
            ).Value;

            c.YawModeKey = config.Bind(
                Hotkeys,
                "Yaw Mode Key",
                c.YawModeKey,
                "Key to toggle between world-space (horizon-locked) and camera-local yaw"
            ).Value;

            c.ShowReticle = config.Bind(
                General,
                "Show Reticle",
                c.ShowReticle,
                "Show reticle compensation (moves crosshair to show aim point during head tracking)"
            ).Value;

            c.DebugLogging = config.Bind(
                Advanced,
                "Debug Logging",
                c.DebugLogging,
                "Enable detailed debug logging"
            ).Value;

            c.UpdateRate = config.Bind(
                Advanced,
                "Update Rate",
                c.UpdateRate,
                new ConfigDescription(
                    "Target update rate in Hz",
                    new AcceptableValueRange<int>(30, 120)
                )
            ).Value;

            c.MaintainRelativePosition = config.Bind(
                Advanced,
                "Maintain Relative Position",
                c.MaintainRelativePosition,
                "Maintain camera position relative to target"
            ).Value;

            c.PositionEnabled = config.Bind(
                General,
                "Position Enabled",
                c.PositionEnabled,
                "Enable positional tracking (lean in/out/side-to-side)"
            ).Value;

            c.PositionSensitivityX = config.Bind(
                Sensitivity,
                "Position Sensitivity X",
                c.PositionSensitivityX,
                new ConfigDescription("Multiplier for lateral (left/right) position", new AcceptableValueRange<float>(0f, 5.0f))
            ).Value;

            c.PositionSensitivityY = config.Bind(
                Sensitivity,
                "Position Sensitivity Y",
                c.PositionSensitivityY,
                new ConfigDescription("Multiplier for vertical (up/down) position", new AcceptableValueRange<float>(0f, 5.0f))
            ).Value;

            c.PositionSensitivityZ = config.Bind(
                Sensitivity,
                "Position Sensitivity Z",
                c.PositionSensitivityZ,
                new ConfigDescription("Multiplier for depth (forward/back) position", new AcceptableValueRange<float>(0f, 5.0f))
            ).Value;

            c.PositionLimitX = config.Bind(
                Sensitivity,
                "Position Limit X",
                c.PositionLimitX,
                new ConfigDescription("Maximum lateral displacement in meters", new AcceptableValueRange<float>(0.01f, 0.5f))
            ).Value;

            c.PositionLimitY = config.Bind(
                Sensitivity,
                "Position Limit Y",
                c.PositionLimitY,
                new ConfigDescription("Maximum vertical displacement in meters", new AcceptableValueRange<float>(0.01f, 0.5f))
            ).Value;

            c.PositionLimitZ = config.Bind(
                Sensitivity,
                "Position Limit Z",
                c.PositionLimitZ,
                new ConfigDescription("Maximum forward displacement in meters", new AcceptableValueRange<float>(0.01f, 0.5f))
            ).Value;

            c.PositionLimitZBack = config.Bind(
                Sensitivity,
                "Position Limit Z Back",
                c.PositionLimitZBack,
                new ConfigDescription("Maximum backward displacement in meters. Leaning back is restricted more tightly than leaning forward to stop the camera pulling into the player body.", new AcceptableValueRange<float>(0.01f, 0.5f))
            ).Value;

            c.NearClipOverride = config.Bind(
                Advanced,
                "Near Clip Override",
                c.NearClipOverride,
                new ConfigDescription(
                    "Minimum near clip plane distance in meters. " +
                    "Prevents seeing through the character model during head bobbing.",
                    new AcceptableValueRange<float>(0.01f, 0.5f))
            ).Value;

            return c;
        }
    }
}
