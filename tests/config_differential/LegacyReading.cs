using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using BepInEx.Configuration;
using PeakHeadTracking.Legacy;
using UnityEngine;

namespace PeakHeadTracking.Tests.ConfigDifferential
{
    internal enum LoadStatus
    {
        /// <summary>The file was read.</summary>
        Usable,

        /// <summary>BepInEx threw reading the file, so the plugin never loaded.</summary>
        Refused,

        /// <summary>No file: the build ran on its defaults.</summary>
        Absent,
    }

    /// <summary>
    /// What one build ran on for one input: the load status, every setting, the startup state and
    /// the hotkeys it registered. A hotkey is written as its bindings, "modifiers:KeyCode" with the
    /// modifiers as core's KeyModifiers bits (3 is Ctrl+Shift), so a KeyCode with no name still
    /// compares.
    /// </summary>
    internal sealed class LegacyReading
    {
        public LoadStatus Status;
        public LegacyConfig Values;
        public bool Enabled;
        public bool RotationEnabled;
        public bool PositionEnabled;
        public bool WorldSpaceYaw;
        public SortedDictionary<string, string> Hotkeys = new SortedDictionary<string, string>(StringComparer.Ordinal);

        public static readonly FieldInfo[] Fields = typeof(LegacyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);

        /// <summary>Every difference between two readings, one line each; empty when they agree.</summary>
        public static List<string> Differences(LegacyReading a, LegacyReading b)
        {
            var d = new List<string>();
            if (a.Status != b.Status)
            {
                d.Add("status " + a.Status + " / " + b.Status);
                return d;
            }
            if (a.Status == LoadStatus.Refused) return d;
            foreach (FieldInfo f in Fields)
            {
                object x = f.GetValue(a.Values), y = f.GetValue(b.Values);
                if (!SameValue(x, y)) d.Add(f.Name + " " + Text(x) + " / " + Text(y));
            }
            if (a.Enabled != b.Enabled) d.Add("enabled " + a.Enabled + " / " + b.Enabled);
            if (a.RotationEnabled != b.RotationEnabled) d.Add("rotation " + a.RotationEnabled + " / " + b.RotationEnabled);
            if (a.PositionEnabled != b.PositionEnabled) d.Add("position " + a.PositionEnabled + " / " + b.PositionEnabled);
            if (a.WorldSpaceYaw != b.WorldSpaceYaw) d.Add("yaw " + a.WorldSpaceYaw + " / " + b.WorldSpaceYaw);
            var actions = new SortedSet<string>(a.Hotkeys.Keys, StringComparer.Ordinal);
            actions.UnionWith(b.Hotkeys.Keys);
            foreach (string action in actions)
            {
                string x, y;
                a.Hotkeys.TryGetValue(action, out x);
                b.Hotkeys.TryGetValue(action, out y);
                if (x != y) d.Add("hotkey " + action + " " + (x ?? "none") + " / " + (y ?? "none"));
            }
            return d;
        }

        public static bool SameValue(object x, object y)
        {
            if (x is float fx && y is float fy)
            {
                return BitConverter.ToInt32(BitConverter.GetBytes(fx), 0) == BitConverter.ToInt32(BitConverter.GetBytes(fy), 0);
            }
            return Equals(x, y);
        }

        public static string Text(object value)
        {
            if (value is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            if (value is KeyCode k) return ((int)k).ToString(CultureInfo.InvariantCulture) + "(" + k + ")";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        /// <summary>A primary key beside an optional Ctrl+Shift letter, as ChordHotkeys.IsActionPressed polled them.</summary>
        public static string Bindings(KeyCode primary, KeyCode? chordLetter)
        {
            var items = new List<string>();
            if (primary != KeyCode.None) items.Add("0:" + ((int)primary).ToString(CultureInfo.InvariantCulture));
            if (chordLetter.HasValue) items.Add("3:" + ((int)chordLetter.Value).ToString(CultureInfo.InvariantCulture));
            return string.Join(", ", items.ToArray());
        }

        /// <summary>
        /// The newest published build, v1.3.0: its ModConfiguration (tests/config_differential/Oracle,
        /// byte for byte the tag's file) on the ConfigFile BaseUnityPlugin builds, then its startup
        /// code and HotkeyManager.Update.
        /// </summary>
        public static LegacyReading Oracle(string path)
        {
            bool exists = File.Exists(path);
            ConfigFile file;
            try
            {
                file = BepInExHost.Open(path, "1.3.0");
            }
            catch (ArgumentException)
            {
                return new LegacyReading { Status = LoadStatus.Refused };
            }
            // v1.3.0 saved the whole file after every Bind. A save writes and never reads, so it
            // cannot change a value the build ran on; turned off here, where 42 saves of every
            // corpus input would cost minutes.
            file.SaveOnConfigSet = false;
            var config = new PeakHeadTracking.Config.ModConfiguration();
            config.Initialize(file);

            var values = new LegacyConfig();
            foreach (FieldInfo f in Fields)
            {
                PropertyInfo entry = typeof(PeakHeadTracking.Config.ModConfiguration).GetProperty(f.Name);
                object boxed = entry.GetValue(config, null);
                f.SetValue(values, ((ConfigEntryBase)boxed).BoxedValue);
            }

            var r = new LegacyReading
            {
                Status = exists ? LoadStatus.Usable : LoadStatus.Absent,
                Values = values,
                Enabled = values.TrackingEnabled,
                RotationEnabled = true,
                PositionEnabled = values.PositionEnabled,
                WorldSpaceYaw = values.WorldSpaceYaw,
            };
            r.Hotkeys["ToggleTracking"] = Bindings(values.ToggleTrackingKey, KeyCode.Y);
            r.Hotkeys["ReloadConfig"] = Bindings(values.ReloadConfigKey, null);
            r.Hotkeys["CycleTrackingMode"] = Bindings(values.TogglePositionKey, KeyCode.G);
            r.Hotkeys["YawMode"] = Bindings(values.YawModeKey, KeyCode.H);
            return r;
        }

        /// <summary>
        /// The frozen reader (src/PeakHeadTracking/Legacy) on the ConfigFile BaseUnityPlugin
        /// builds, then the startup code and HotkeyManager.Update of the commit that froze it.
        /// </summary>
        public static LegacyReading Import(string path)
        {
            bool exists = File.Exists(path);
            ConfigFile file;
            try
            {
                file = BepInExHost.Open(path, "1.3.0");
            }
            catch (ArgumentException)
            {
                return new LegacyReading { Status = LoadStatus.Refused };
            }
            LegacyConfig values = LegacyConfigReader.Read(file);
            var r = new LegacyReading
            {
                Status = exists ? LoadStatus.Usable : LoadStatus.Absent,
                Values = values,
                Enabled = values.TrackingEnabled,
                RotationEnabled = true,
                PositionEnabled = values.PositionEnabled,
                WorldSpaceYaw = values.WorldSpaceYaw,
            };
            r.Hotkeys["ToggleTracking"] = Bindings(values.ToggleTrackingKey, KeyCode.Y);
            r.Hotkeys["ReloadConfig"] = Bindings(values.ReloadConfigKey, null);
            r.Hotkeys["CycleTrackingMode"] = Bindings(values.TogglePositionKey, KeyCode.G);
            r.Hotkeys["YawMode"] = Bindings(values.YawModeKey, KeyCode.H);
            r.Hotkeys["ToggleReticle"] = Bindings(values.ToggleReticleKey, KeyCode.U);
            return r;
        }
    }
}
