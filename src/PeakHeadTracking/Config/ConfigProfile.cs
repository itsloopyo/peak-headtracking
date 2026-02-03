using System;
using System.Collections.Generic;
using UnityEngine;

namespace PeakHeadTracking.Config
{
    /// <summary>
    /// Configuration profile containing all settings
    /// </summary>
    public class ConfigProfile
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public string GameName { get; set; }
        public Dictionary<string, object> Settings { get; set; } = new Dictionary<string, object>();
        public bool IsDefault { get; set; }
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// Export profile settings from current configuration
        /// </summary>
        public void ExportFromConfig(ModConfiguration config)
        {
            Settings.Clear();

            // Connection settings
            Settings["UdpPort"] = config.UdpPort.Value;
            Settings["ReconnectTimeout"] = config.ReconnectTimeout.Value;
            Settings["PacketBufferSize"] = config.PacketBufferSize.Value;

            // General settings
            Settings["TrackingEnabled"] = config.TrackingEnabled.Value;
            Settings["EnableAudioFeedback"] = config.EnableAudioFeedback.Value;

            // Sensitivity settings
            Settings["YawSensitivity"] = config.YawSensitivity.Value;
            Settings["PitchSensitivity"] = config.PitchSensitivity.Value;
            Settings["RollSensitivity"] = config.RollSensitivity.Value;
            Settings["InvertYaw"] = config.InvertYaw.Value;
            Settings["InvertPitch"] = config.InvertPitch.Value;
            Settings["InvertRoll"] = config.InvertRoll.Value;

            // Roll enable setting
            Settings["EnableRoll"] = config.EnableRoll.Value;

            // Smoothing settings
            Settings["Smoothing"] = config.Smoothing.Value;

            // Deadzone settings
            Settings["EnableDeadzone"] = config.EnableDeadzone.Value;
            Settings["DeadzoneYaw"] = config.DeadzoneYaw.Value;
            Settings["DeadzonePitch"] = config.DeadzonePitch.Value;
            Settings["DeadzoneRoll"] = config.DeadzoneRoll.Value;

            // Hotkey settings
            Settings["ToggleTrackingKey"] = config.ToggleTrackingKey.Value;
            Settings["RecenterKey"] = config.RecenterKey.Value;
            Settings["ReloadConfigKey"] = config.ReloadConfigKey.Value;

            // Advanced settings
            Settings["DebugLogging"] = config.DebugLogging.Value;
            Settings["UpdateRate"] = config.UpdateRate.Value;

            ModifiedDate = DateTime.Now;
        }

        /// <summary>
        /// Import profile settings to configuration
        /// </summary>
        public void ImportToConfig(ModConfiguration config)
        {
            // Connection settings
            if (Settings.TryGetValue("UdpPort", out var udpPort))
                config.UdpPort.Value = Convert.ToInt32(udpPort);
            if (Settings.TryGetValue("ReconnectTimeout", out var reconnectTimeout))
                config.ReconnectTimeout.Value = Convert.ToInt32(reconnectTimeout);
            if (Settings.TryGetValue("PacketBufferSize", out var bufferSize))
                config.PacketBufferSize.Value = Convert.ToInt32(bufferSize);

            // General settings
            if (Settings.TryGetValue("TrackingEnabled", out var trackingEnabled))
                config.TrackingEnabled.Value = Convert.ToBoolean(trackingEnabled);
            if (Settings.TryGetValue("EnableAudioFeedback", out var audioFeedback))
                config.EnableAudioFeedback.Value = Convert.ToBoolean(audioFeedback);

            // Sensitivity settings
            if (Settings.TryGetValue("YawSensitivity", out var yawSens))
                config.YawSensitivity.Value = Convert.ToSingle(yawSens);
            if (Settings.TryGetValue("PitchSensitivity", out var pitchSens))
                config.PitchSensitivity.Value = Convert.ToSingle(pitchSens);
            if (Settings.TryGetValue("RollSensitivity", out var rollSens))
                config.RollSensitivity.Value = Convert.ToSingle(rollSens);
            if (Settings.TryGetValue("InvertYaw", out var invertYaw))
                config.InvertYaw.Value = Convert.ToBoolean(invertYaw);
            if (Settings.TryGetValue("InvertPitch", out var invertPitch))
                config.InvertPitch.Value = Convert.ToBoolean(invertPitch);
            if (Settings.TryGetValue("InvertRoll", out var invertRoll))
                config.InvertRoll.Value = Convert.ToBoolean(invertRoll);

            // Roll enable
            if (Settings.TryGetValue("EnableRoll", out var enableRoll))
                config.EnableRoll.Value = Convert.ToBoolean(enableRoll);

            // Smoothing settings
            if (Settings.TryGetValue("Smoothing", out var smoothing))
                config.Smoothing.Value = Convert.ToSingle(smoothing);

            // Deadzone settings
            if (Settings.TryGetValue("EnableDeadzone", out var enableDeadzone))
                config.EnableDeadzone.Value = Convert.ToBoolean(enableDeadzone);
            if (Settings.TryGetValue("DeadzoneYaw", out var dzYaw))
                config.DeadzoneYaw.Value = Convert.ToSingle(dzYaw);
            if (Settings.TryGetValue("DeadzonePitch", out var dzPitch))
                config.DeadzonePitch.Value = Convert.ToSingle(dzPitch);
            if (Settings.TryGetValue("DeadzoneRoll", out var dzRoll))
                config.DeadzoneRoll.Value = Convert.ToSingle(dzRoll);

            // Hotkey settings
            if (Settings.TryGetValue("ToggleTrackingKey", out var toggleKey))
                config.ToggleTrackingKey.Value = (KeyCode)toggleKey;
            if (Settings.TryGetValue("RecenterKey", out var recenterKey))
                config.RecenterKey.Value = (KeyCode)recenterKey;
            if (Settings.TryGetValue("ReloadConfigKey", out var reloadKey))
                config.ReloadConfigKey.Value = (KeyCode)reloadKey;

            // Advanced settings
            if (Settings.TryGetValue("DebugLogging", out var debugLog))
                config.DebugLogging.Value = Convert.ToBoolean(debugLog);
            if (Settings.TryGetValue("UpdateRate", out var updateRate))
                config.UpdateRate.Value = Convert.ToInt32(updateRate);
        }

        /// <summary>
        /// Clone this profile
        /// </summary>
        public ConfigProfile Clone(string newName)
        {
            var clone = new ConfigProfile
            {
                Name = newName,
                Description = Description + " (Copy)",
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                GameName = GameName,
                IsDefault = false,
                IsReadOnly = false,
                Settings = new Dictionary<string, object>(Settings)
            };
            return clone;
        }
    }
}
