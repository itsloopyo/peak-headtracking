using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using BepInEx.Configuration;
using PeakHeadTracking.Legacy;
using Xunit;

namespace PeakHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// Comparison 1: the newest published build's reader (the oracle) against the frozen reader
    /// (the import), over every input. What differs is what players see change that the
    /// conversion did not cause, and each difference is listed here with the commit that made it.
    /// </summary>
    public class DifferentialTests
    {
        // The oracle is v1.3.0's own reader, byte for byte; `git show v1.3.0:<path> | sha256sum`.
        private static readonly Dictionary<string, string> OracleHashes = new Dictionary<string, string>
        {
            { "tests/config_differential/Oracle/ModConfiguration.cs", "63f604cde8e92b803b8d5ab5741f93df17e3f282269311f41c3ef8fff9df533b" },
            { "tests/config_differential/Oracle/ConfigCategories.cs", "6a10aae252638dfa1ff3b5eb0edd1a918fb9a2c4af00be427464b0055dcf0705" },
        };

        // The frozen import. A change to either file changes how players' legacy files are read.
        private static readonly Dictionary<string, string> FrozenHashes = new Dictionary<string, string>
        {
            { "src/PeakHeadTracking/Legacy/LegacyConfig.cs", "c44f9d79f91f4b5be5ed8c76d89e06f8b8f3bdc2eff16bde83a75b653c26d2c7" },
            { "src/PeakHeadTracking/Legacy/LegacyConfigReader.cs", "5d480dc5c15ac0f08dc9b15aa58db0d9f2655bb50a75b1af1eae08e2f0a5dde6" },
        };

        /// <summary>
        /// b3e2cca (fix(smoothing): run rotation through TrackingProcessor.Process) wired
        /// Toggle Reticle, which v1.3.0 bound and never polled, to Insert and Ctrl+Shift+U.
        /// </summary>
        private const string ReticleToggleWired = "hotkey ToggleReticle none / ";

        [Fact]
        public void OracleIsThePublishedReader()
        {
            foreach (KeyValuePair<string, string> file in OracleHashes)
            {
                Assert.Equal(file.Value, Sha256(file.Key));
            }
        }

        [Fact]
        public void FrozenImportIsUnchanged()
        {
            foreach (KeyValuePair<string, string> file in FrozenHashes)
            {
                Assert.Equal(file.Value, Sha256(file.Key));
            }
        }

        [Fact]
        public void KeysAreEveryDefinitionTheReaderBinds()
        {
            string dir = RepoPaths.Scratch();
            ConfigFile file = BepInExHost.Open(Path.Combine(dir, BepInExHost.Guid + ".cfg"), "1.3.0");
            LegacyConfigReader.Read(file);
            var bound = file.Keys.Select(d => d.Section + "\n" + d.Key).ToList();
            var listed = LegacyConfigReader.Keys.Select(k => k.Section + "\n" + k.Key).ToList();
            Assert.Equal(bound.OrderBy(x => x, StringComparer.Ordinal), listed.OrderBy(x => x, StringComparer.Ordinal));
            Assert.Empty(Directory.GetFiles(dir));
            Directory.Delete(dir, true);
        }

        [Fact]
        public void ComparisonOne()
        {
            string dir = RepoPaths.Scratch();
            var unexpected = new List<string>();
            int inputs = 0;
            int refused = 0;
            foreach (KeyValuePair<string, byte[]> input in Corpus.Inputs())
            {
                inputs++;
                string oraclePath = Place(dir, "oracle", input.Value);
                string importPath = Place(dir, "import", input.Value);
                DateTime written = input.Value == null ? default(DateTime) : File.GetLastWriteTimeUtc(importPath);

                LegacyReading oracle = LegacyReading.Oracle(oraclePath);
                LegacyReading import = LegacyReading.Import(importPath);

                if (input.Value == null)
                {
                    Assert.False(File.Exists(importPath), input.Key + ": the frozen reader created the file");
                }
                else
                {
                    Assert.True(File.ReadAllBytes(importPath).SequenceEqual(input.Value), input.Key + ": the frozen reader changed the file");
                    Assert.Equal(written, File.GetLastWriteTimeUtc(importPath));
                }

                List<string> differences = LegacyReading.Differences(oracle, import);
                if (oracle.Status == LoadStatus.Refused && import.Status == LoadStatus.Refused)
                {
                    refused++;
                    continue;
                }
                if (differences.Count != 1 || !differences[0].StartsWith(ReticleToggleWired, StringComparison.Ordinal))
                {
                    unexpected.Add(input.Key + ": " + string.Join("; ", differences.ToArray()));
                }
            }
            Directory.Delete(dir, true);
            Assert.True(unexpected.Count == 0, string.Join("\n", unexpected.Take(40).ToArray()));
            Assert.True(inputs > 1000, "the corpus produced " + inputs + " inputs");
            Assert.True(refused < inputs / 10, refused + " of " + inputs + " inputs made BepInEx refuse the file");
        }

        /// <summary>
        /// The readings above run BepInEx's own ConfigFile, the one vendor/bepinex ships. What a
        /// published build read depends on the BepInEx the player has, which the installer takes
        /// from that zip.
        /// </summary>
        [Fact]
        public void ReadsWithTheVendoredBepInEx()
        {
            Assert.Equal(new Version(5, 4, 23, 3), typeof(ConfigFile).Assembly.GetName().Version);
        }

        internal static string Place(string dir, string name, byte[] bytes)
        {
            string folder = Path.Combine(dir, name);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, BepInExHost.Guid + ".cfg");
            if (bytes != null) File.WriteAllBytes(path, bytes);
            return path;
        }

        internal static string Sha256(string repoPath)
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(RepoPaths.Root, repoPath.Replace('/', Path.DirectorySeparatorChar)));
            using (SHA256 sha = SHA256.Create())
            {
                var text = new StringBuilder();
                foreach (byte b in sha.ComputeHash(bytes)) text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }
    }
}
