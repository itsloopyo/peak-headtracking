using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;
using CameraUnlock.Core.Config;
using CameraUnlock.Core.Data;
using CameraUnlock.Core.Input;
using PeakHeadTracking.Legacy;
using UnityEngine;

namespace PeakHeadTracking.Config
{
    /// <summary>
    /// The legacy import: the frozen reader on the plugin's own ConfigFile, then a map, field by
    /// field, from what it read into <see cref="PeakConfig"/>. The owner runs it once, while
    /// BepInEx\config\CameraUnlock.ini is absent, on BepInEx\config\com.cameraunlock.peak.headtracking.cfg,
    /// which it never writes.
    /// </summary>
    internal static class LegacyMigration
    {
        // What the last build before the canonical config shipped, and so what a pose-shaping
        // value is compared with.
        private static readonly LegacyConfig Shipped = new LegacyConfig();

        public static LegacyImport<PeakConfig> Import(ConfigFile pluginConfig)
        {
            return new LegacyImport<PeakConfig>((input, config) => Run(pluginConfig, input, config), LegacyConfigReader.Keys);
        }

        private static ImportResult Run(ConfigFile pluginConfig, LegacyImportInput input, PeakConfig config)
        {
            bool exists = File.Exists(input.Path);
            LegacyConfig legacy;
            try
            {
                legacy = LegacyConfigReader.Read(pluginConfig);
            }
            catch (ArgumentException e)
            {
                // BepInEx refuses a key it cannot name. The plugin's own ConfigFile already read
                // the file once without throwing, so the file changed in between.
                return ImportResult.Refused("BepInEx cannot read it: " + e.Message);
            }
            finally
            {
                // The frozen reader bound its entries on the plugin's ConfigFile. Unbound, they are
                // not listed by ConfigurationManager, where they would do nothing.
                pluginConfig.Clear();
            }

            var dropped = new List<DroppedValue>();
            var poseShaping = new List<PoseShapingValue>();
            Map(legacy, config, dropped, poseShaping);
            return exists ? ImportResult.Imported(dropped, poseShaping) : ImportResult.Absent(dropped, poseShaping);
        }

        public static void Map(LegacyConfig legacy, PeakConfig config, ICollection<DroppedValue> dropped,
            ICollection<PoseShapingValue> poseShaping)
        {
            config.UdpPort = legacy.UdpPort;
            config.EnableOnStartup = legacy.TrackingEnabled;
            config.WorldSpaceYaw = legacy.WorldSpaceYaw;
            // The published build always started with rotation on; Position Enabled was the
            // position half of the tracking mode, which the cycle key set and saved.
            config.RotationEnabled = true;
            config.PositionEnabled = legacy.PositionEnabled;
            config.LocalSmoothing = legacy.LocalSmoothing;
            config.RemoteSmoothing = legacy.RemoteSmoothing;
            // Position Limit Y bounded the view both up and down.
            PositionSettings p = config.Position;
            config.Position = new PositionSettings(
                p.SensitivityX, p.SensitivityY, p.SensitivityZ,
                legacy.PositionLimitX, legacy.PositionLimitY, legacy.PositionLimitY, legacy.PositionLimitZ, legacy.PositionLimitZBack,
                legacy.LocalSmoothing, legacy.RemoteSmoothing,
                p.InvertX, p.InvertY, p.InvertZ);

            config.ToggleKeyName = KeyList(legacy.ToggleTrackingKey, KeyCode.Y);
            config.CycleTrackingModeKeyName = KeyList(legacy.TogglePositionKey, KeyCode.G);
            config.YawModeKeyName = KeyList(legacy.YawModeKey, KeyCode.H);
            config.ReloadConfigKeyName = KeyList(legacy.ReloadConfigKey, null);

            config.MinNearClip = legacy.NearClipOverride;
            config.DebugLogging = legacy.DebugLogging;

            const string s = LegacyConfigReader.Sensitivity;
            const string d = LegacyConfigReader.Deadzone;
            LegacyPoseShaping.Record(legacy.YawSensitivity, Shipped.YawSensitivity, s, "Yaw Sensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PitchSensitivity, Shipped.PitchSensitivity, s, "Pitch Sensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.RollSensitivity, Shipped.RollSensitivity, s, "Roll Sensitivity", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertYaw, Shipped.InvertYaw, s, "Invert Yaw", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertPitch, Shipped.InvertPitch, s, "Invert Pitch", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.InvertRoll, Shipped.InvertRoll, s, "Invert Roll", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityX, Shipped.PositionSensitivityX, s, "Position Sensitivity X", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityY, Shipped.PositionSensitivityY, s, "Position Sensitivity Y", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.PositionSensitivityZ, Shipped.PositionSensitivityZ, s, "Position Sensitivity Z", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.EnableDeadzone, Shipped.EnableDeadzone, d, "Enable Deadzone", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.DeadzoneYaw, Shipped.DeadzoneYaw, d, "Yaw Deadzone", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.DeadzonePitch, Shipped.DeadzonePitch, d, "Pitch Deadzone", poseShaping, dropped);
            LegacyPoseShaping.Record(legacy.DeadzoneRoll, Shipped.DeadzoneRoll, d, "Roll Deadzone", poseShaping, dropped);

            dropped.Add(new DroppedValue(DropRule.Reticle, LegacyConfigReader.General, "Show Reticle", legacy.ShowReticle ? "true" : "false"));
            dropped.Add(new DroppedValue(DropRule.Reticle, LegacyConfigReader.Hotkeys, "Toggle Reticle", legacy.ToggleReticleKey.ToString()));
        }

        /// <summary>
        /// A legacy hotkey as a key list: the key the player set, then the Ctrl+Shift letter
        /// ChordHotkeys polled beside it. KeyCode.None bound nothing. A KeyCode with no name (a
        /// number BepInEx read into the enum) keeps its number, which the owner cannot write, so
        /// the import is deferred rather than the key changed.
        /// </summary>
        private static string KeyList(KeyCode primary, KeyCode? chordLetter)
        {
            var items = new List<string>();
            if (primary != KeyCode.None) items.Add(KeyName(primary));
            if (chordLetter.HasValue) items.Add("Ctrl+Shift+" + KeyName(chordLetter.Value));
            return string.Join(", ", items.ToArray());
        }

        private static string KeyName(KeyCode key)
        {
            string text = key.ToString();
            KeyBinding[] bindings;
            string error;
            if (!KeyBindings.TryParse(text, out bindings, out error)) return text;
            return KeyBindings.Format(bindings);
        }
    }
}
