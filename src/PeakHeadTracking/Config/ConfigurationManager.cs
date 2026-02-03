using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Configuration;

namespace PeakHeadTracking.Config
{
    /// <summary>
    /// Central configuration manager - uses CameraUnlock.Core's TrackingProcessor for processing
    /// </summary>
    public class ConfigurationManager
    {
        private static ConfigurationManager instance;
        public static ConfigurationManager Instance => instance ??= new ConfigurationManager();

        private ModConfiguration modConfig;
        private ProfileManager profileManager;
        private bool isInitialized = false;

        // Simple callback registry for setting changes
        private readonly Dictionary<string, List<Action>> settingCallbacks = new Dictionary<string, List<Action>>();

        /// <summary>
        /// Event fired when any configuration changes
        /// </summary>
        public event EventHandler<ConfigChangedEventArgs> ConfigChanged;

        /// <summary>
        /// Event fired when profile changes
        /// </summary>
        public event EventHandler<string> ProfileChanged;

        /// <summary>
        /// Get the current mod configuration
        /// </summary>
        public ModConfiguration Config => modConfig;

        /// <summary>
        /// Get the profile manager
        /// </summary>
        public ProfileManager Profiles => profileManager;

        /// <summary>
        /// Initialize configuration system
        /// </summary>
        public void Initialize(ConfigFile config)
        {
            if (isInitialized)
                return;

            // Initialize mod configuration
            modConfig = new ModConfiguration();
            modConfig.Initialize(config);

            // Initialize profile manager
            profileManager = new ProfileManager(config);

            // Load current settings from active profile
            var activeProfile = profileManager.GetActiveProfile();
            if (activeProfile != null)
            {
                profileManager.ApplyProfileToConfig(modConfig);
            }

            // Setup configuration change watchers
            SetupConfigurationWatchers();

            isInitialized = true;
            PeakHeadTrackingPlugin.Logger.LogInfo("Configuration manager initialized");
        }

        /// <summary>
        /// Setup watchers for configuration changes
        /// </summary>
        private void SetupConfigurationWatchers()
        {
            // Watch sensitivity changes
            modConfig.YawSensitivity.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("YawSensitivity", modConfig.YawSensitivity.Value, ConfigCategories.SENSITIVITY);
            };

            modConfig.PitchSensitivity.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("PitchSensitivity", modConfig.PitchSensitivity.Value, ConfigCategories.SENSITIVITY);
            };

            modConfig.RollSensitivity.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("RollSensitivity", modConfig.RollSensitivity.Value, ConfigCategories.SENSITIVITY);
            };

            // Watch inversion changes
            modConfig.InvertYaw.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("InvertYaw", modConfig.InvertYaw.Value, ConfigCategories.SENSITIVITY);
            };

            modConfig.InvertPitch.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("InvertPitch", modConfig.InvertPitch.Value, ConfigCategories.SENSITIVITY);
            };

            modConfig.InvertRoll.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("InvertRoll", modConfig.InvertRoll.Value, ConfigCategories.SENSITIVITY);
            };

            // Watch deadzone changes
            modConfig.EnableDeadzone.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("EnableDeadzone", modConfig.EnableDeadzone.Value, ConfigCategories.DEADZONE);
            };

            modConfig.DeadzoneYaw.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("DeadzoneYaw", modConfig.DeadzoneYaw.Value, ConfigCategories.DEADZONE);
            };

            modConfig.DeadzonePitch.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("DeadzonePitch", modConfig.DeadzonePitch.Value, ConfigCategories.DEADZONE);
            };

            modConfig.DeadzoneRoll.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("DeadzoneRoll", modConfig.DeadzoneRoll.Value, ConfigCategories.DEADZONE);
            };

            // Watch limit changes
            modConfig.EnablePitchLimits.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("EnablePitchLimits", modConfig.EnablePitchLimits.Value, ConfigCategories.LIMITS);
            };

            modConfig.MinPitch.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("MinPitch", modConfig.MinPitch.Value, ConfigCategories.LIMITS);
            };

            modConfig.MaxPitch.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("MaxPitch", modConfig.MaxPitch.Value, ConfigCategories.LIMITS);
            };

            modConfig.EnableRoll.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("EnableRoll", modConfig.EnableRoll.Value, ConfigCategories.LIMITS);
            };

            modConfig.EnableRollLimits.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("EnableRollLimits", modConfig.EnableRollLimits.Value, ConfigCategories.LIMITS);
            };

            modConfig.MaxRoll.SettingChanged += (sender, args) =>
            {
                OnConfigChanged("MaxRoll", modConfig.MaxRoll.Value, ConfigCategories.LIMITS);
            };
        }

        private void OnConfigChanged(string settingName, object newValue, string category)
        {
            ConfigChanged?.Invoke(this, new ConfigChangedEventArgs
            {
                SettingName = settingName,
                NewValue = newValue,
                Category = category
            });

            // Execute registered callbacks
            if (settingCallbacks.TryGetValue(settingName, out var callbacks))
            {
                foreach (var callback in callbacks)
                {
                    callback?.Invoke();
                }
            }
        }

        /// <summary>
        /// Register callback for setting change
        /// </summary>
        public void RegisterSettingChangeCallback(string settingName, Action callback)
        {
            if (!settingCallbacks.ContainsKey(settingName))
            {
                settingCallbacks[settingName] = new List<Action>();
            }
            settingCallbacks[settingName].Add(callback);
        }

        /// <summary>
        /// Unregister callback for setting change
        /// </summary>
        public void UnregisterSettingChangeCallback(string settingName, Action callback)
        {
            if (settingCallbacks.TryGetValue(settingName, out var callbacks))
            {
                callbacks.Remove(callback);
            }
        }

        /// <summary>
        /// Reload configuration from file
        /// </summary>
        public void ReloadConfiguration()
        {
            modConfig?.Reload();

            // Reapply profile settings
            var activeProfile = profileManager?.GetActiveProfile();
            if (activeProfile != null)
            {
                profileManager.ApplyProfileToConfig(modConfig);
            }

            PeakHeadTrackingPlugin.Logger.LogInfo("Configuration reloaded");
        }

        /// <summary>
        /// Save current configuration
        /// </summary>
        public void SaveConfiguration()
        {
            modConfig?.Save();
            profileManager?.SaveCurrentToProfile(modConfig);
            PeakHeadTrackingPlugin.Logger.LogInfo("Configuration saved");
        }

        /// <summary>
        /// Switch to different profile
        /// </summary>
        public void SwitchProfile(string profileName)
        {
            profileManager.LoadProfile(profileName);
            profileManager.ApplyProfileToConfig(modConfig);
            ProfileChanged?.Invoke(this, profileName);
            PeakHeadTrackingPlugin.Logger.LogInfo($"Switched to profile: {profileName}");
        }

        /// <summary>
        /// Reset configuration to defaults
        /// </summary>
        public void ResetToDefaults()
        {
            // Reset all config entries to defaults
            modConfig.YawSensitivity.Value = (float)modConfig.YawSensitivity.DefaultValue;
            modConfig.PitchSensitivity.Value = (float)modConfig.PitchSensitivity.DefaultValue;
            modConfig.RollSensitivity.Value = (float)modConfig.RollSensitivity.DefaultValue;
            modConfig.InvertYaw.Value = (bool)modConfig.InvertYaw.DefaultValue;
            modConfig.InvertPitch.Value = (bool)modConfig.InvertPitch.DefaultValue;
            modConfig.InvertRoll.Value = (bool)modConfig.InvertRoll.DefaultValue;

            SaveConfiguration();
        }

        /// <summary>
        /// Export configuration to file
        /// </summary>
        public void ExportConfiguration(string filePath)
        {
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("# PeakHeadTracking Configuration Export");
                writer.WriteLine($"# Exported: {DateTime.Now}");
                writer.WriteLine($"# Profile: {profileManager.GetActiveProfileName()}");
                writer.WriteLine();

                writer.WriteLine($"UdpPort={modConfig.UdpPort.Value}");
                writer.WriteLine($"TrackingEnabled={modConfig.TrackingEnabled.Value}");
                writer.WriteLine($"YawSensitivity={modConfig.YawSensitivity.Value}");
                writer.WriteLine($"PitchSensitivity={modConfig.PitchSensitivity.Value}");
                writer.WriteLine($"RollSensitivity={modConfig.RollSensitivity.Value}");
                writer.WriteLine($"InvertYaw={modConfig.InvertYaw.Value}");
                writer.WriteLine($"InvertPitch={modConfig.InvertPitch.Value}");
                writer.WriteLine($"InvertRoll={modConfig.InvertRoll.Value}");
            }

            PeakHeadTrackingPlugin.Logger.LogInfo($"Configuration exported to {filePath}");
        }

        /// <summary>
        /// Cleanup and dispose
        /// </summary>
        public void Cleanup()
        {
            settingCallbacks.Clear();
            ConfigChanged = null;
            ProfileChanged = null;
            isInitialized = false;
        }
    }
}
