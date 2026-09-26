using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Input;
using PeakHeadTracking.Config;

namespace PeakHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// The migration: the owner's Load in a folder holding only the legacy file, as the converted
    /// plugin runs it, on the ConfigFile BaseUnityPlugin builds.
    /// </summary>
    internal sealed class Migration
    {
        public const string Version = "1.4.0";

        public bool BepInExRefused;
        public ConfigOwner<PeakConfig> Owner;
        public ConfigLoadResult<PeakConfig> Loaded;
        public string LegacyPath;
        public string ConfigPath;

        public static Migration Run(string folder, byte[] legacy, DefaultsFile defaults)
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            var m = new Migration { LegacyPath = Path.Combine(folder, BepInExHost.Guid + ".cfg") };
            m.ConfigPath = Path.Combine(folder, PeakConfigOwner.FileName);
            if (legacy != null) File.WriteAllBytes(m.LegacyPath, legacy);
            ConfigFile plugin;
            try
            {
                plugin = BepInExHost.Open(m.LegacyPath, Version);
            }
            catch (ArgumentException)
            {
                m.BepInExRefused = true;
                return m;
            }
            m.Owner = new ConfigOwner<PeakConfig>(PeakConfigOwner.Options(plugin, defaults, null));
            m.Loaded = m.Owner.Load();
            return m;
        }

        /// <summary>The owner the plugin builds at its next start, over a folder a Run left.</summary>
        public static ConfigOwner<PeakConfig> Reopen(string legacyPath, DefaultsFile defaults)
        {
            return new ConfigOwner<PeakConfig>(PeakConfigOwner.Options(BepInExHost.Open(legacyPath, Version), defaults, null));
        }

        /// <summary>
        /// Defaults.ini in a scratch profile folder, never the player's own. Load creates it with
        /// the built-in values.
        /// </summary>
        public static DefaultsFile ScratchDefaults(string scratch)
        {
            string profile = Path.Combine(scratch, "profile");
            Directory.CreateDirectory(profile);
            return DefaultsFile.At(Path.Combine(profile, "CameraUnlock", "Defaults.ini"));
        }

        /// <summary>The table's defaults: every row read from a file that sets none.</summary>
        public static PeakConfig Defaults()
        {
            var config = new PeakConfig();
            PeakConfig.Table().Apply(CanonicalIni.Parse(new byte[0]), config);
            return config;
        }

        /// <summary>
        /// Every setting the table binds, and the pose-shaping and position settings the mod
        /// builds from them, as text; floats by their bits.
        /// </summary>
        public static SortedDictionary<string, string> Fields(PeakConfig c)
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                { "UdpPort", c.UdpPort.ToString(CultureInfo.InvariantCulture) },
                { "EnableOnStartup", c.EnableOnStartup.ToString() },
                { "WorldSpaceYaw", c.WorldSpaceYaw.ToString() },
                { "RotationEnabled", c.RotationEnabled.ToString() },
                { "PositionEnabled", c.PositionEnabled.ToString() },
                { "LocalSmoothing", Bits(c.LocalSmoothing) },
                { "RemoteSmoothing", Bits(c.RemoteSmoothing) },
                { "Position.LimitX", Bits(c.Position.LimitX) },
                { "Position.LimitY", Bits(c.Position.LimitY) },
                { "Position.LimitYDown", Bits(c.Position.LimitYDown) },
                { "Position.LimitZ", Bits(c.Position.LimitZ) },
                { "Position.LimitZBack", Bits(c.Position.LimitZBack) },
                { "Position.LocalSmoothing", Bits(c.Position.LocalSmoothing) },
                { "Position.RemoteSmoothing", Bits(c.Position.RemoteSmoothing) },
                { "Position.Sensitivity", Bits(c.Position.SensitivityX) + " " + Bits(c.Position.SensitivityY) + " " + Bits(c.Position.SensitivityZ) },
                { "Position.Invert", c.Position.InvertX + " " + c.Position.InvertY + " " + c.Position.InvertZ },
                { "ToggleKey", c.ToggleKeyName },
                { "CycleTrackingModeKey", c.CycleTrackingModeKeyName },
                { "YawModeKey", c.YawModeKeyName },
                { "ReloadConfigKey", c.ReloadConfigKeyName },
                { "MinNearClip", Bits(c.MinNearClip) },
                { "DebugLogging", c.DebugLogging.ToString() },
            };
        }

        /// <summary>
        /// A key list as the bindings the mod polls, written as <see cref="LegacyReading.Bindings"/>
        /// writes them; null when the list does not parse.
        /// </summary>
        public static string Polled(string keyList)
        {
            KeyBinding[] bindings;
            string error;
            if (!KeyBindings.TryParse(keyList, out bindings, out error)) return null;
            var items = new List<string>();
            foreach (KeyBinding b in bindings)
            {
                items.Add(((int)b.Modifiers).ToString(CultureInfo.InvariantCulture) + ":" + b.UnityKeyCode.ToString(CultureInfo.InvariantCulture));
            }
            return string.Join(", ", items.ToArray());
        }

        private static string Bits(float value)
        {
            return BitConverter.ToInt32(BitConverter.GetBytes(value), 0).ToString("X8", CultureInfo.InvariantCulture);
        }
    }
}
