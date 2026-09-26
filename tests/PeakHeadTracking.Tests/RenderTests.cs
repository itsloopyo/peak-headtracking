using System;
using System.IO;
using System.Linq;
using CameraUnlock.Core.Config;
using PeakHeadTracking.Config;
using PeakHeadTracking.Tests.ConfigDifferential;
using Xunit;

namespace PeakHeadTracking.Tests
{
    public class RenderTests
    {
        /// <summary>The committed file, at the path core's data/config-format.json records for the repo.</summary>
        public static readonly string CommittedPath = Path.Combine(RepoPaths.Root, "config", "CameraUnlock.ini");

        /// <summary>
        /// config/CameraUnlock.ini is the table's fresh render, byte for byte. With
        /// CAMERAUNLOCK_RENDER_CONFIG=write (pixi run render-config) this writes it instead.
        /// </summary>
        [Fact]
        public void CommittedFileIsTheRenderedDefaults()
        {
            byte[] rendered = PeakConfig.Table().RenderFresh(new RenderHeader(PeakConfig.DisplayName));
            string mode = Environment.GetEnvironmentVariable("CAMERAUNLOCK_RENDER_CONFIG");
            if (mode == "write")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CommittedPath));
                File.WriteAllBytes(CommittedPath, rendered);
                return;
            }
            Assert.True(string.IsNullOrEmpty(mode), "CAMERAUNLOCK_RENDER_CONFIG is '" + mode + "'; only 'write' is read");
            Assert.True(File.Exists(CommittedPath), CommittedPath + " is missing; run pixi run render-config");
            Assert.True(rendered.SequenceEqual(File.ReadAllBytes(CommittedPath)),
                CommittedPath + " is not the table's render; run pixi run render-config");
        }

        [Fact]
        public void CommittedFilePassesTheLint()
        {
            Assert.Null(MigrationTests.Lint(File.ReadAllBytes(CommittedPath)));
        }
    }
}
