using System;
using System.IO;
using BepInEx;
using BepInEx.Configuration;

namespace PeakHeadTracking.Tests.ConfigDifferential
{
    /// <summary>
    /// Runs BepInEx's own config code in the test host. ConfigFile's static constructor opens
    /// BepInEx's core config at Paths.BepInExConfigPath, which the chainloader sets in game and
    /// nothing sets here, so a scratch path is set before the first ConfigFile is built.
    /// </summary>
    internal static class BepInExHost
    {
        public const string Guid = "com.cameraunlock.peak.headtracking";
        public const string Name = "Peak Head Tracking";

        private static readonly object Gate = new object();
        private static bool ready;

        /// <summary>
        /// The plugin's ConfigFile as BaseUnityPlugin builds it: the constructor reads an existing
        /// file, and writes nothing. Throws what BepInEx throws for a file it cannot read, which
        /// in game stops the plugin from loading at all.
        /// </summary>
        public static ConfigFile Open(string path, string version)
        {
            Ensure();
            return new ConfigFile(path, false, new BepInPlugin(Guid, Name, version));
        }

        private static void Ensure()
        {
            lock (Gate)
            {
                if (ready) return;
                string dir = Path.Combine(Path.GetTempPath(), "PeakHeadTracking.Tests", "bepinex-" + System.Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(dir);
                typeof(Paths).GetProperty("BepInExConfigPath").GetSetMethod(true)
                    .Invoke(null, new object[] { Path.Combine(dir, "BepInEx.cfg") });
                ready = true;
            }
        }
    }
}
