using UnityEngine;

namespace PeakHeadTracking.Legacy
{
    /// <summary>
    /// Frozen: every setting the last build before the canonical config read from
    /// BepInEx\config\com.cameraunlock.peak.headtracking.cfg, with that build's defaults.
    /// A default the runtime config later moves changes only what a new file holds, never what
    /// an old file without the key means. Never edit this file.
    /// </summary>
    internal sealed class LegacyConfig
    {
        // 01. Connection
        public int UdpPort = 4242;
        public int ReconnectTimeout = 5;
        public int PacketBufferSize = 100;

        // 02. General
        public bool TrackingEnabled = true;
        public bool EnableAudioFeedback = true;
        public bool WorldSpaceYaw = true;
        public bool ShowReticle = true;
        public bool PositionEnabled = true;

        // 03. Sensitivity
        public float YawSensitivity = 1.0f;
        public float PitchSensitivity = 1.0f;
        public float RollSensitivity = 1.0f;
        public bool InvertYaw = false;
        public bool InvertPitch = false;
        public bool InvertRoll = true;
        public float PositionSensitivityX = 2.0f;
        public float PositionSensitivityY = 2.0f;
        public float PositionSensitivityZ = 2.0f;
        public float PositionLimitX = 0.30f;
        public float PositionLimitY = 0.20f;
        public float PositionLimitZ = 0.40f;
        public float PositionLimitZBack = 0.10f;

        // 04. Rotation Limits
        public bool EnablePitchLimits = true;
        public float MinPitch = -85f;
        public float MaxPitch = 85f;
        public bool EnableRoll = true;
        public bool EnableRollLimits = true;
        public float MaxRoll = 30f;

        // 05. Smoothing
        public float LocalSmoothing = 0.0f;
        public float RemoteSmoothing = 0.15f;

        // 06. Deadzone
        public bool EnableDeadzone = false;
        public float DeadzoneYaw = 0f;
        public float DeadzonePitch = 0f;
        public float DeadzoneRoll = 0f;

        // 07. Hotkeys
        public KeyCode ToggleTrackingKey = KeyCode.End;
        public KeyCode ReloadConfigKey = KeyCode.F12;
        public KeyCode TogglePositionKey = KeyCode.PageUp;
        public KeyCode ToggleReticleKey = KeyCode.Insert;
        public KeyCode YawModeKey = KeyCode.PageDown;

        // 08. Advanced
        public bool DebugLogging = false;
        public int UpdateRate = 60;
        public bool MaintainRelativePosition = true;
        public float NearClipOverride = 0.15f;
    }
}
