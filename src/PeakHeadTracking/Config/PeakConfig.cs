using CameraUnlock.Core.Config;

namespace PeakHeadTracking.Config
{
    /// <summary>
    /// Everything the mod reads from BepInEx\config\CameraUnlock.ini. Unity-free, so the test
    /// project compiles it and holds the committed file to it.
    /// </summary>
    public sealed class PeakConfig : HeadTrackingConfigData
    {
        public const string DisplayName = "PEAK";

        public string ReloadConfigKeyName { get; set; } = "F12";

        public float MinNearClip { get; set; } = 0.15f;

        public bool DebugLogging { get; set; }

        public static ConfigTable<PeakConfig> Table()
        {
            return HeadTrackingConfigTable.Create<PeakConfig>(
                    ConfigConcepts.UdpPort,
                    ConfigConcepts.EnableOnStartup,
                    ConfigConcepts.WorldSpaceYaw,
                    ConfigConcepts.RotationEnabled,
                    ConfigConcepts.LocalSmoothing,
                    ConfigConcepts.RemoteSmoothing,
                    ConfigConcepts.PositionEnabled,
                    ConfigConcepts.PositionLimitX,
                    ConfigConcepts.PositionLimitY,
                    ConfigConcepts.PositionLimitYDown,
                    ConfigConcepts.PositionLimitZ,
                    ConfigConcepts.PositionLimitZBack,
                    ConfigConcepts.ToggleKey,
                    ConfigConcepts.CycleTrackingModeKey,
                    ConfigConcepts.YawModeKey)
                .Select(ConfigConcepts.WorldSpaceYaw).Writable()
                .Select(ConfigConcepts.RotationEnabled).Writable()
                .Select(ConfigConcepts.PositionEnabled).Writable()
                .Local("Hotkeys", "ReloadConfigKey", c => c.ReloadConfigKeyName, (c, v) => c.ReloadConfigKeyName = v,
                    new HotkeyCodec(),
                    "Reads this file and Defaults.ini again and applies them, without restarting\n" +
                    "the game.")
                .Local("Camera", "MinNearClip", c => c.MinNearClip, (c, v) => c.MinNearClip = v, new FloatCodec(),
                    "Metres. While head tracking moves the view, the camera's near clip plane is\n" +
                    "held at least this far out, so the view does not see through your character's\n" +
                    "model.")
                .Range(0.01, 0.5)
                .Local("Logging", "DebugLogging", c => c.DebugLogging, (c, v) => c.DebugLogging = v, new BoolCodec(),
                    "true: write the head tracking state to the BepInEx log every 120 frames,\n" +
                    "at the Debug level.");
        }
    }
}
