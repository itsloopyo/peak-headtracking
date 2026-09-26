using System;
using System.Collections.Generic;
using System.IO;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Config.Testing;
using PeakHeadTracking.Legacy;

namespace PeakHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// The differential test's inputs: no file, an empty file, the first-run output of every
    /// published build, and core's mutation corpus over the newest one. No published build shipped
    /// a config file or a launcher seed, and the predecessor repo peak-headtracking-old wrote
    /// com.peakmod.headtracking.cfg, which no build of this repo reads.
    /// </summary>
    internal static class Corpus
    {
        public static readonly string[] PublishedTags =
        {
            "v1.0.0", "v1.0.1", "v1.0.2", "v1.1.0", "v1.1.1", "v1.1.2", "v1.2.0", "v1.2.1", "v1.3.0",
        };

        private static readonly string[] None = new string[0];

        /// <summary>
        /// One descriptor per key the frozen reader binds: an alternate value BepInEx reads, and
        /// one value outside each AcceptableValueRange, which BepInEx clamps. No legacy row names
        /// a chord: ChordHotkeys added the Ctrl+Shift letter in code.
        /// </summary>
        public static readonly MutationKey[] Keys =
        {
            Number(LegacyConfigReader.Connection, "UDP Port", "5555", "1023", "65536"),
            Number(LegacyConfigReader.Connection, "Reconnect Timeout", "10", "0", "61"),
            Number(LegacyConfigReader.Connection, "Packet Buffer Size", "50", "9", "501"),
            Bool(LegacyConfigReader.General, "Tracking Enabled", "false"),
            Bool(LegacyConfigReader.General, "Enable Audio Feedback", "false"),
            Bool(LegacyConfigReader.General, "World Space Yaw", "false"),
            Number(LegacyConfigReader.Sensitivity, "Yaw Sensitivity", "2.5", "0.05", "5.5"),
            Number(LegacyConfigReader.Sensitivity, "Pitch Sensitivity", "2.5", "0.05", "5.5"),
            Number(LegacyConfigReader.Sensitivity, "Roll Sensitivity", "2.5", "0.05", "5.5"),
            Bool(LegacyConfigReader.Sensitivity, "Invert Yaw", "true"),
            Bool(LegacyConfigReader.Sensitivity, "Invert Pitch", "true"),
            Bool(LegacyConfigReader.Sensitivity, "Invert Roll", "false"),
            Bool(LegacyConfigReader.Limits, "Enable Pitch Limits", "false"),
            Number(LegacyConfigReader.Limits, "Minimum Pitch", "-45", "-91", "1"),
            Number(LegacyConfigReader.Limits, "Maximum Pitch", "45", "-1", "91"),
            Bool(LegacyConfigReader.Limits, "Enable Roll", "false"),
            Bool(LegacyConfigReader.Limits, "Enable Roll Limits", "false"),
            Number(LegacyConfigReader.Limits, "Maximum Roll", "15", "-1", "91"),
            Number(LegacyConfigReader.Smoothing, "Local Smoothing", "0.5", "-0.1", "1.1"),
            Number(LegacyConfigReader.Smoothing, "Remote Smoothing", "0.3", "-0.1", "1.1"),
            Bool(LegacyConfigReader.Deadzone, "Enable Deadzone", "true"),
            Number(LegacyConfigReader.Deadzone, "Yaw Deadzone", "2", "-1", "11"),
            Number(LegacyConfigReader.Deadzone, "Pitch Deadzone", "2", "-1", "11"),
            Number(LegacyConfigReader.Deadzone, "Roll Deadzone", "2", "-1", "11"),
            Hotkey(LegacyConfigReader.Hotkeys, "Toggle Tracking", "F8"),
            Hotkey(LegacyConfigReader.Hotkeys, "Reload Config", "F9"),
            Hotkey(LegacyConfigReader.Hotkeys, "Toggle Position", "F10"),
            Hotkey(LegacyConfigReader.Hotkeys, "Toggle Reticle", "F11"),
            Hotkey(LegacyConfigReader.Hotkeys, "Yaw Mode Key", "F7"),
            Bool(LegacyConfigReader.General, "Show Reticle", "false"),
            Bool(LegacyConfigReader.Advanced, "Debug Logging", "true"),
            Number(LegacyConfigReader.Advanced, "Update Rate", "90", "29", "121"),
            Bool(LegacyConfigReader.Advanced, "Maintain Relative Position", "false"),
            Bool(LegacyConfigReader.General, "Position Enabled", "false"),
            Number(LegacyConfigReader.Sensitivity, "Position Sensitivity X", "1", "-0.1", "5.5"),
            Number(LegacyConfigReader.Sensitivity, "Position Sensitivity Y", "1", "-0.1", "5.5"),
            Number(LegacyConfigReader.Sensitivity, "Position Sensitivity Z", "1", "-0.1", "5.5"),
            Number(LegacyConfigReader.Sensitivity, "Position Limit X", "0.25", "0.005", "0.6"),
            Number(LegacyConfigReader.Sensitivity, "Position Limit Y", "0.25", "0.005", "0.6"),
            Number(LegacyConfigReader.Sensitivity, "Position Limit Z", "0.25", "0.005", "0.6"),
            Number(LegacyConfigReader.Sensitivity, "Position Limit Z Back", "0.25", "0.005", "0.6"),
            Number(LegacyConfigReader.Advanced, "Near Clip Override", "0.2", "0.005", "0.6"),
        };

        /// <summary>Every input as (name, bytes); null bytes is no file.</summary>
        public static IEnumerable<KeyValuePair<string, byte[]>> Inputs()
        {
            yield return new KeyValuePair<string, byte[]>("no file", null);
            yield return new KeyValuePair<string, byte[]>("empty file", new byte[0]);
            foreach (string tag in PublishedTags)
            {
                yield return new KeyValuePair<string, byte[]>(tag + " first run", FirstRun(tag));
            }
            foreach (IniMutation m in IniMutations.Generate(FirstRun("v1.3.0"), LegacyConfigReader.Keys, Keys))
            {
                yield return new KeyValuePair<string, byte[]>("corpus: " + m.Name, m.Bytes);
            }
        }

        /// <summary>The .cfg a published build wrote at its first start, extracted from its release DLL once.</summary>
        public static byte[] FirstRun(string tag)
        {
            return File.ReadAllBytes(Path.Combine(RepoPaths.Root, "tests", "config_differential", "data", "first-run", tag + ".cfg"));
        }

        private static MutationKey Number(string section, string key, string alternate, params string[] outOfRange)
        {
            return new MutationKey(section, key, alternate, outOfRange, false, new ChordSwitch[0]);
        }

        private static MutationKey Bool(string section, string key, string alternate)
        {
            return new MutationKey(section, key, alternate, None, false, new ChordSwitch[0]);
        }

        private static MutationKey Hotkey(string section, string key, string alternate)
        {
            return new MutationKey(section, key, alternate, None, true, new ChordSwitch[0]);
        }
    }
}
