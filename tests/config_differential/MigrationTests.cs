using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CameraUnlock.Core.Config;
using PeakHeadTracking.Config;
using PeakHeadTracking.Legacy;
using Xunit;

namespace PeakHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// Comparison 2: the frozen reader (the import) against the owner's Load, which imports the
    /// legacy file into a new CameraUnlock.ini (the migration), over every input. What may differ
    /// is only what data/config-format.json approves: pose shaping (the sensitivities, inversions
    /// and deadzones) and reticle settings. Keys the published build parsed and never applied
    /// (Reconnect Timeout, Packet Buffer Size, Enable Audio Feedback, the rotation limits, Update
    /// Rate and Maintain Relative Position) are dead, and the migration writes the effective
    /// state without them.
    /// </summary>
    public class MigrationTests : IDisposable
    {
        private readonly string scratch = RepoPaths.Scratch();
        private readonly DefaultsFile defaults;

        public MigrationTests()
        {
            defaults = Migration.ScratchDefaults(scratch);
        }

        public void Dispose()
        {
            foreach (string file in Directory.GetFiles(scratch, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(file, FileAttributes.Normal);
            }
            Directory.Delete(scratch, true);
        }

        [Fact]
        public void ComparisonTwo()
        {
            var failures = new List<string>();
            int migrated = 0, deferred = 0, created = 0, refused = 0;
            foreach (KeyValuePair<string, byte[]> input in Corpus.Inputs())
            {
                string where = input.Key + ": ";
                LegacyReading import = LegacyReading.Import(DifferentialTests.Place(scratch, "import", input.Value));
                Migration m = Migration.Run(Path.Combine(scratch, "game"), input.Value, defaults);

                if (import.Status == LoadStatus.Refused)
                {
                    // BaseUnityPlugin's own ConfigFile throws on this file before any mod code
                    // runs, in the published build and in this one alike.
                    if (!m.BepInExRefused) failures.Add(where + "the frozen reader refused it and BepInEx did not");
                    refused++;
                    continue;
                }

                if (import.Status == LoadStatus.Absent)
                {
                    Check(failures, where, m.Loaded.Status == ConfigLoadStatus.Created, "status " + m.Loaded.Status + ", not Created");
                    Check(failures, where, SameFields(Migration.Defaults(), m.Loaded.Config), "a first start does not run on the defaults");
                    Check(failures, where, Names(m) == PeakConfigOwner.FileName, "the folder holds " + Names(m));
                    created++;
                    continue;
                }

                var expected = Migration.Defaults();
                var dropped = new List<DroppedValue>();
                var poseShaping = new List<PoseShapingValue>();
                LegacyMigration.Map(import.Values, expected, dropped, poseShaping);
                CheckRules(failures, where, import, expected, dropped, poseShaping);

                if (Unwritable(expected) != null)
                {
                    // A hotkey the published build read as a KeyCode with no name: no codec
                    // writes it and no approved rule covers it, so the owner defers the import and
                    // the session runs on what it read.
                    Check(failures, where, m.Loaded.Status == ConfigLoadStatus.Deferred, "status " + m.Loaded.Status + ", not Deferred");
                    Check(failures, where, m.Loaded.Reason.Contains("cannot be converted"), "reason: " + m.Loaded.Reason);
                    Check(failures, where, Names(m) == BepInExHost.Guid + ".cfg", "the folder holds " + Names(m));
                    Check(failures, where, SameFields(expected, m.Loaded.Config), "the deferred session does not run on the import");
                    deferred++;
                }
                else
                {
                    Check(failures, where, m.Loaded.Status == ConfigLoadStatus.Migrated, "status " + m.Loaded.Status + ", not Migrated");
                    if (m.Loaded.Status != ConfigLoadStatus.Migrated) continue;
                    Check(failures, where, SameFields(expected, m.Loaded.Config), Difference(expected, m.Loaded.Config));
                    Check(failures, where, Names(m) == PeakConfigOwner.FileName + ", " + BepInExHost.Guid + ".cfg", "the folder holds " + Names(m));
                    foreach (DroppedValue d in dropped)
                    {
                        string line = m.LegacyPath + ": " + d.Describe();
                        Check(failures, where, m.Loaded.Log.Contains(line), "the log does not name " + d.Describe());
                    }
                    string lint = Lint(File.ReadAllBytes(m.ConfigPath));
                    Check(failures, where, lint == null, "the migrated file " + lint);
                    SecondLoad(failures, where, m);
                    migrated++;
                }
                Check(failures, where, File.ReadAllBytes(m.LegacyPath).SequenceEqual(input.Value), "the legacy file changed");
            }

            Assert.True(failures.Count == 0, string.Join("\n", failures.Take(40).ToArray()));
            Assert.True(migrated > 1000, migrated + " inputs migrated");
            Assert.True(created == 1, created + " inputs were a first start");
            Assert.True(refused + deferred < migrated / 5, refused + " refused by BepInEx and " + deferred + " deferred, of " + migrated);
        }

        /// <summary>
        /// A read-only legacy file imports as a writable one does and keeps its attribute, bytes
        /// and write time. Run over the published builds' first-run files and a player's edits.
        /// </summary>
        [Fact]
        public void ReadOnlyLegacyFileImportsTheSame()
        {
            var inputs = new List<byte[]>();
            foreach (string tag in Corpus.PublishedTags) inputs.Add(Corpus.FirstRun(tag));
            inputs.Add(Edited());
            foreach (byte[] bytes in inputs)
            {
                Migration writable = Migration.Run(Path.Combine(scratch, "writable"), bytes, defaults);
                string folder = Path.Combine(scratch, "readonly");
                if (Directory.Exists(folder))
                {
                    foreach (string file in Directory.GetFiles(folder)) File.SetAttributes(file, FileAttributes.Normal);
                    Directory.Delete(folder, true);
                }
                Directory.CreateDirectory(folder);
                string legacy = Path.Combine(folder, BepInExHost.Guid + ".cfg");
                File.WriteAllBytes(legacy, bytes);
                File.SetAttributes(legacy, FileAttributes.ReadOnly);
                DateTime written = File.GetLastWriteTimeUtc(legacy);

                ConfigOwner<PeakConfig> owner = Migration.Reopen(legacy, defaults);
                ConfigLoadResult<PeakConfig> loaded = owner.Load();

                Assert.Equal(ConfigLoadStatus.Migrated, loaded.Status);
                Assert.True(SameFields(writable.Loaded.Config, loaded.Config));
                Assert.Equal(File.ReadAllBytes(writable.ConfigPath), File.ReadAllBytes(Path.Combine(folder, PeakConfigOwner.FileName)));
                Assert.Equal(bytes, File.ReadAllBytes(legacy));
                Assert.Equal(written, File.GetLastWriteTimeUtc(legacy));
                Assert.True((File.GetAttributes(legacy) & FileAttributes.ReadOnly) != 0);
            }
        }

        /// <summary>
        /// Fresh equals upgrade: the first-run file of every published build, and so the newest
        /// one's, imports with Defaults.ini at the built-in values into exactly the committed
        /// file. No build shipped a config file or a launcher seed.
        /// </summary>
        [Fact]
        public void EveryPublishedFirstRunImportsIntoTheCommittedFile()
        {
            byte[] committed = File.ReadAllBytes(RenderTests.CommittedPath);
            foreach (string tag in Corpus.PublishedTags)
            {
                Migration m = Migration.Run(Path.Combine(scratch, "game"), Corpus.FirstRun(tag), defaults);
                Assert.Equal(ConfigLoadStatus.Migrated, m.Loaded.Status);
                Assert.True(committed.SequenceEqual(File.ReadAllBytes(m.ConfigPath)), tag);
            }
        }

        /// <summary>
        /// Players whose file was first written by v1.0.0 to v1.1.1 hold Invert Roll = false, the
        /// default those builds shipped; 79eaef0 moved it to true, and BepInEx kept the value
        /// already in the file. It is dropped as pose shaping, like any value away from the
        /// shipped default, and roll follows the folded default.
        /// </summary>
        [Fact]
        public void EarlyFirstRunsDropInvertRoll()
        {
            foreach (string tag in Corpus.PublishedTags)
            {
                LegacyReading import = LegacyReading.Import(DifferentialTests.Place(scratch, "import", Corpus.FirstRun(tag)));
                var dropped = new List<DroppedValue>();
                LegacyMigration.Map(import.Values, Migration.Defaults(), dropped, new List<PoseShapingValue>());
                bool early = Array.IndexOf(new[] { "v1.0.0", "v1.0.1", "v1.0.2", "v1.1.0", "v1.1.1" }, tag) >= 0;
                Assert.Equal(early, dropped.Any(d => d.Rule == DropRule.PoseShaping && d.Key == "Invert Roll"));
                Assert.Equal(early ? 1 : 0, dropped.Count(d => d.Rule == DropRule.PoseShaping));
            }
        }

        private void SecondLoad(List<string> failures, string where, Migration first)
        {
            byte[] config = File.ReadAllBytes(first.ConfigPath);
            byte[] legacy = File.ReadAllBytes(first.LegacyPath);
            DateTime configWritten = File.GetLastWriteTimeUtc(first.ConfigPath);
            ConfigOwner<PeakConfig> owner = Migration.Reopen(first.LegacyPath, defaults);
            ConfigLoadResult<PeakConfig> again = owner.Load();
            Check(failures, where, again.Status == ConfigLoadStatus.Canonical, "second load " + again.Status);
            Check(failures, where, SameFields(first.Loaded.Config, again.Config), "the second load reads other values");
            Check(failures, where, again.Log.Any(l => l.Contains(first.LegacyPath + " is left as it was and is not read.")), "the second load does not say the legacy file is not read");
            Check(failures, where, config.SequenceEqual(File.ReadAllBytes(first.ConfigPath)) && configWritten == File.GetLastWriteTimeUtc(first.ConfigPath), "the second load rewrote CameraUnlock.ini");
            Check(failures, where, legacy.SequenceEqual(File.ReadAllBytes(first.LegacyPath)), "the second load changed the legacy file");
        }

        /// <summary>The approved rules, field by field, from the frozen reader to the map.</summary>
        private static void CheckRules(List<string> failures, string where, LegacyReading import, PeakConfig mapped,
            List<DroppedValue> dropped, List<PoseShapingValue> poseShaping)
        {
            LegacyConfig l = import.Values;
            Check(failures, where, mapped.UdpPort == l.UdpPort, "UdpPort");
            Check(failures, where, mapped.EnableOnStartup == import.Enabled, "EnableOnStartup");
            Check(failures, where, mapped.WorldSpaceYaw == import.WorldSpaceYaw, "WorldSpaceYaw");
            Check(failures, where, mapped.RotationEnabled == import.RotationEnabled && mapped.PositionEnabled == import.PositionEnabled, "tracking mode");
            Check(failures, where, LegacyReading.SameValue(mapped.LocalSmoothing, l.LocalSmoothing) && LegacyReading.SameValue(mapped.RemoteSmoothing, l.RemoteSmoothing), "smoothing");
            Check(failures, where, LegacyReading.SameValue(mapped.Position.LimitX, l.PositionLimitX)
                && LegacyReading.SameValue(mapped.Position.LimitY, l.PositionLimitY)
                && LegacyReading.SameValue(mapped.Position.LimitYDown, l.PositionLimitY)
                && LegacyReading.SameValue(mapped.Position.LimitZ, l.PositionLimitZ)
                && LegacyReading.SameValue(mapped.Position.LimitZBack, l.PositionLimitZBack), "position limits");
            Check(failures, where, LegacyReading.SameValue(mapped.MinNearClip, l.NearClipOverride), "MinNearClip");
            Check(failures, where, mapped.DebugLogging == l.DebugLogging, "DebugLogging");

            var polled = new Dictionary<string, string>
            {
                { "ToggleTracking", mapped.ToggleKeyName },
                { "CycleTrackingMode", mapped.CycleTrackingModeKeyName },
                { "YawMode", mapped.YawModeKeyName },
                { "ReloadConfig", mapped.ReloadConfigKeyName },
            };
            foreach (KeyValuePair<string, string> action in polled)
            {
                string bindings = Migration.Polled(action.Value);
                if (bindings == null) continue;
                Check(failures, where, bindings == import.Hotkeys[action.Key], action.Key + " polls " + bindings + ", the published build " + import.Hotkeys[action.Key]);
            }

            var shipped = new LegacyConfig();
            var expectedShaping = new[]
            {
                Shaping(LegacyConfigReader.Sensitivity, "Yaw Sensitivity", l.YawSensitivity, shipped.YawSensitivity),
                Shaping(LegacyConfigReader.Sensitivity, "Pitch Sensitivity", l.PitchSensitivity, shipped.PitchSensitivity),
                Shaping(LegacyConfigReader.Sensitivity, "Roll Sensitivity", l.RollSensitivity, shipped.RollSensitivity),
                Shaping(LegacyConfigReader.Sensitivity, "Invert Yaw", l.InvertYaw, shipped.InvertYaw),
                Shaping(LegacyConfigReader.Sensitivity, "Invert Pitch", l.InvertPitch, shipped.InvertPitch),
                Shaping(LegacyConfigReader.Sensitivity, "Invert Roll", l.InvertRoll, shipped.InvertRoll),
                Shaping(LegacyConfigReader.Sensitivity, "Position Sensitivity X", l.PositionSensitivityX, shipped.PositionSensitivityX),
                Shaping(LegacyConfigReader.Sensitivity, "Position Sensitivity Y", l.PositionSensitivityY, shipped.PositionSensitivityY),
                Shaping(LegacyConfigReader.Sensitivity, "Position Sensitivity Z", l.PositionSensitivityZ, shipped.PositionSensitivityZ),
                Shaping(LegacyConfigReader.Deadzone, "Enable Deadzone", l.EnableDeadzone, shipped.EnableDeadzone),
                Shaping(LegacyConfigReader.Deadzone, "Yaw Deadzone", l.DeadzoneYaw, shipped.DeadzoneYaw),
                Shaping(LegacyConfigReader.Deadzone, "Pitch Deadzone", l.DeadzonePitch, shipped.DeadzonePitch),
                Shaping(LegacyConfigReader.Deadzone, "Roll Deadzone", l.DeadzoneRoll, shipped.DeadzoneRoll),
            };
            Check(failures, where, poseShaping.Count == expectedShaping.Length, poseShaping.Count + " pose-shaping values");
            var expectedDropped = new List<string>();
            foreach (object[] e in expectedShaping)
            {
                PoseShapingValue v = poseShaping.FirstOrDefault(p => p.Section == (string)e[0] && p.Key == (string)e[1]);
                Check(failures, where, v != null && v.Folded == (bool)e[2], e[1] + " pose shaping");
                if (!(bool)e[2] && v != null) expectedDropped.Add("PoseShaping [" + e[0] + "] " + e[1] + "=" + v.Value);
            }
            expectedDropped.Add("Reticle [" + LegacyConfigReader.General + "] Show Reticle=" + (l.ShowReticle ? "true" : "false"));
            expectedDropped.Add("Reticle [" + LegacyConfigReader.Hotkeys + "] Toggle Reticle=" + l.ToggleReticleKey);
            var actualDropped = dropped.Select(d => d.Rule + " [" + d.Section + "] " + d.Key + "=" + d.Value).ToList();
            Check(failures, where, expectedDropped.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(actualDropped.OrderBy(x => x, StringComparer.Ordinal)),
                "dropped " + string.Join("; ", actualDropped.ToArray()));
        }

        private static object[] Shaping(string section, string key, float value, float shipped)
        {
            return new object[] { section, key, value == shipped };
        }

        private static object[] Shaping(string section, string key, bool value, bool shipped)
        {
            return new object[] { section, key, value == shipped };
        }

        // The first hotkey list no codec writes, or null.
        private static string Unwritable(PeakConfig c)
        {
            foreach (string list in new[] { c.ToggleKeyName, c.CycleTrackingModeKeyName, c.YawModeKeyName, c.ReloadConfigKeyName })
            {
                if (Migration.Polled(list) == null) return list;
            }
            return null;
        }

        private static bool SameFields(PeakConfig a, PeakConfig b)
        {
            return Difference(a, b) == null;
        }

        private static string Difference(PeakConfig a, PeakConfig b)
        {
            SortedDictionary<string, string> x = Migration.Fields(a), y = Migration.Fields(b);
            foreach (KeyValuePair<string, string> f in x)
            {
                if (f.Value != y[f.Key]) return f.Key + " " + f.Value + " / " + y[f.Key];
            }
            return null;
        }

        private static string Names(Migration m)
        {
            return string.Join(", ", Directory.GetFiles(Path.GetDirectoryName(m.LegacyPath)).Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray());
        }

        /// <summary>
        /// What core's canonical config lint checks that a file the owner wrote can get wrong:
        /// the reader finds nothing to report, the stamp, CRLF only, ASCII only, and the table
        /// reads every line.
        /// </summary>
        internal static string Lint(byte[] bytes)
        {
            CanonicalIni doc = CanonicalIni.Parse(bytes);
            if (!doc.IsReadable) return "is unreadable";
            if (!CanonicalIni.HasStamp(bytes) || doc.FormatVersion != CanonicalIni.ConfigFormat) return "has no [CameraUnlock] ConfigFormat=1";
            if (doc.Diagnostics.Count > 0) return "draws " + doc.Diagnostics[0].Describe();
            for (int i = 0; i < bytes.Length; i++)
            {
                if (bytes[i] > 0x7E || (bytes[i] < 0x20 && bytes[i] != 0x0D && bytes[i] != 0x0A)) return "holds byte " + bytes[i] + " at " + i;
                if (bytes[i] == 0x0A && (i == 0 || bytes[i - 1] != 0x0D)) return "has an LF without CR at " + i;
                if (bytes[i] == 0x0D && (i + 1 == bytes.Length || bytes[i + 1] != 0x0A)) return "has a CR without LF at " + i;
            }
            if (bytes.Length < 2 || bytes[bytes.Length - 2] != 0x0D || bytes[bytes.Length - 1] != 0x0A) return "does not end in CRLF";
            ApplyReport report = PeakConfig.Table().Apply(doc, new PeakConfig());
            if (report.Diagnostics.Count > 0) return "draws " + report.Diagnostics[0].Describe();
            return null;
        }

        /// <summary>A legacy file a player edited away from every default the map carries.</summary>
        internal static byte[] Edited()
        {
            string text = System.Text.Encoding.ASCII.GetString(Corpus.FirstRun("v1.3.0"));
            var edits = new Dictionary<string, string>
            {
                { "UDP Port = 4242", "UDP Port = 5555" },
                { "Tracking Enabled = true", "Tracking Enabled = false" },
                { "World Space Yaw = true", "World Space Yaw = false" },
                { "Position Enabled = true", "Position Enabled = false" },
                { "Local Smoothing = 0", "Local Smoothing = 0.25" },
                { "Remote Smoothing = 0.15", "Remote Smoothing = 0.4" },
                { "Position Limit X = 0.3", "Position Limit X = 0.25" },
                { "Position Limit Y = 0.2", "Position Limit Y = 0.1" },
                { "Toggle Tracking = End", "Toggle Tracking = F8" },
                { "Toggle Position = PageUp", "Toggle Position = None" },
                { "Near Clip Override = 0.15", "Near Clip Override = 0.3" },
                { "Yaw Sensitivity = 1", "Yaw Sensitivity = 1.5" },
            };
            foreach (KeyValuePair<string, string> e in edits)
            {
                if (!text.Contains(e.Key)) throw new InvalidOperationException("v1.3.0's first run has no line " + e.Key);
                text = text.Replace(e.Key, e.Value);
            }
            return System.Text.Encoding.ASCII.GetBytes(text);
        }

        private static void Check(List<string> failures, string where, bool ok, string what)
        {
            if (!ok) failures.Add(where + what);
        }
    }
}
