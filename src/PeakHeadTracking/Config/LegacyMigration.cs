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
            LegacyFollowsDefaultsIni follows = Map(legacy, config, dropped, poseShaping);
            return exists
                ? ImportResult.Imported(dropped, poseShaping, follows.Concepts)
                : ImportResult.Absent(dropped, poseShaping, follows.Concepts);
        }

        /// <summary>
        /// Sets every field from the legacy values and returns the rows left to Defaults.ini: each
        /// global row whose legacy setting holds what the last build shipped (owner rule of
        /// 2026-09-26).
        /// </summary>
        public static LegacyFollowsDefaultsIni Map(LegacyConfig legacy, PeakConfig config, ICollection<DroppedValue> dropped,
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

            config.ToggleKeyName = KeyList(legacy.ToggleTrackingKey, KeyCode.Y, "Toggle Tracking", dropped);
            config.CycleTrackingModeKeyName = KeyList(legacy.TogglePositionKey, KeyCode.G, "Toggle Position", dropped);
            config.YawModeKeyName = KeyList(legacy.YawModeKey, KeyCode.H, "Yaw Mode Key", dropped);
            config.ReloadConfigKeyName = KeyList(legacy.ReloadConfigKey, null, "Reload Config", dropped);

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

            var follows = new LegacyFollowsDefaultsIni();
            follows.Setting(ConfigConcepts.UdpPort, legacy.UdpPort, Shipped.UdpPort);
            follows.Setting(ConfigConcepts.EnableOnStartup, legacy.TrackingEnabled, Shipped.TrackingEnabled);
            follows.Setting(ConfigConcepts.WorldSpaceYaw, legacy.WorldSpaceYaw, Shipped.WorldSpaceYaw);
            follows.TrackingMode(legacy.PositionEnabled, Shipped.PositionEnabled);
            follows.Setting(ConfigConcepts.LocalSmoothing, legacy.LocalSmoothing, Shipped.LocalSmoothing);
            follows.Setting(ConfigConcepts.RemoteSmoothing, legacy.RemoteSmoothing, Shipped.RemoteSmoothing);
            follows.Setting(ConfigConcepts.PositionLimitX, legacy.PositionLimitX, Shipped.PositionLimitX);
            follows.Setting(ConfigConcepts.PositionLimitY, legacy.PositionLimitY, Shipped.PositionLimitY);
            follows.Setting(ConfigConcepts.PositionLimitYDown, legacy.PositionLimitY, Shipped.PositionLimitY);
            follows.Setting(ConfigConcepts.PositionLimitZ, legacy.PositionLimitZ, Shipped.PositionLimitZ);
            follows.Setting(ConfigConcepts.PositionLimitZBack, legacy.PositionLimitZBack, Shipped.PositionLimitZBack);
            // The Ctrl+Shift letter was fixed in code, so a hotkey is unchanged exactly where its key is.
            follows.Setting(ConfigConcepts.ToggleKey, legacy.ToggleTrackingKey, Shipped.ToggleTrackingKey);
            follows.Setting(ConfigConcepts.CycleTrackingModeKey, legacy.TogglePositionKey, Shipped.TogglePositionKey);
            follows.Setting(ConfigConcepts.YawModeKey, legacy.YawModeKey, Shipped.YawModeKey);
            return follows;
        }

        /// <summary>
        /// A legacy hotkey as a key list: the key the player set, through core's N3 (a Ctrl, Shift
        /// or Alt key alone unbinds and is logged) and N1 (a KeyCode with no name in core's key
        /// list, a number BepInEx read into the enum, unbinds and is logged), then the Ctrl+Shift
        /// letter ChordHotkeys polled beside it.
        /// </summary>
        private static string KeyList(KeyCode primary, KeyCode? chordLetter, string legacyKey, ICollection<DroppedValue> dropped)
        {
            var items = new List<string>();
            string plain = LegacyNormalisations.KeyCodeToBindings((int)primary, LegacyConfigReader.Hotkeys, legacyKey, dropped);
            if (plain.Length > 0) items.Add(plain);
            if (chordLetter.HasValue)
            {
                items.Add(KeyBindings.Format(new[] { new KeyBinding(KeyModifiers.Ctrl | KeyModifiers.Shift, (int)chordLetter.Value) }));
            }
            return string.Join(", ", items.ToArray());
        }
    }
}
