using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakHeadTracking.Config
{
    /// <summary>
    /// Manages multiple configuration profiles for different games or scenarios
    /// </summary>
    public class ProfileManager
    {
        private readonly string profilesDirectory;
        private readonly ConfigFile mainConfig;
        private readonly Dictionary<string, ConfigProfile> profiles = new Dictionary<string, ConfigProfile>();
        private ConfigProfile activeProfile;
        private string activeProfileName;
        private List<string> cachedProfileNames;  // Invalidated on profile add/remove

        public ProfileManager(ConfigFile config)
        {
            mainConfig = config;
            profilesDirectory = Path.Combine(Paths.ConfigPath, "PeakHeadTracking", "Profiles");

            // Create profiles directory if it doesn't exist
            if (!Directory.Exists(profilesDirectory))
            {
                Directory.CreateDirectory(profilesDirectory);
            }

            // Load all profiles
            LoadProfiles();

            // Create default profiles if none exist
            if (profiles.Count == 0)
            {
                CreateDefaultProfiles();
            }

            // Load the active profile
            string lastProfile = mainConfig.Bind("Profile", "LastActiveProfile", "Default",
                "Last active profile name").Value;

            if (profiles.ContainsKey(lastProfile))
            {
                LoadProfile(lastProfile);
            }
            else
            {
                LoadProfile("Default");
            }
        }

        /// <summary>
        /// Create default profiles
        /// </summary>
        private void CreateDefaultProfiles()
        {
            // Default profile
            var defaultProfile = new ConfigProfile
            {
                Name = "Default",
                Description = "Default configuration for most games",
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                GameName = "General",
                IsDefault = true,
                IsReadOnly = false
            };
            // Default sensitivity values stored in Settings dictionary
            defaultProfile.Settings["YawSensitivity"] = 1.0f;
            defaultProfile.Settings["PitchSensitivity"] = 1.0f;
            defaultProfile.Settings["RollSensitivity"] = 1.0f;
            defaultProfile.Settings["Smoothing"] = 0.0f;
            profiles["Default"] = defaultProfile;
            SaveProfile(defaultProfile);

            // FPS Competitive profile
            var fpsProfile = new ConfigProfile
            {
                Name = "FPS_Competitive",
                Description = "Optimized for competitive FPS games",
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                GameName = "FPS",
                IsDefault = false,
                IsReadOnly = false
            };
            fpsProfile.Settings["YawSensitivity"] = 0.8f;
            fpsProfile.Settings["PitchSensitivity"] = 0.8f;
            fpsProfile.Settings["RollSensitivity"] = 0.5f;
            fpsProfile.Settings["Smoothing"] = 0.0f;
            profiles["FPS_Competitive"] = fpsProfile;
            SaveProfile(fpsProfile);

            // Simulation profile
            var simProfile = new ConfigProfile
            {
                Name = "Simulation",
                Description = "Realistic head movement for simulation games",
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                GameName = "Simulation",
                IsDefault = false,
                IsReadOnly = false
            };
            simProfile.Settings["YawSensitivity"] = 1.2f;
            simProfile.Settings["PitchSensitivity"] = 1.2f;
            simProfile.Settings["RollSensitivity"] = 1.0f;
            simProfile.Settings["Smoothing"] = 0.1f;
            profiles["Simulation"] = simProfile;
            SaveProfile(simProfile);
        }

        /// <summary>
        /// Load all profiles from disk
        /// </summary>
        private void LoadProfiles()
        {
            profiles.Clear();

            foreach (var file in Directory.GetFiles(profilesDirectory, "*.profile"))
            {
                var profileName = Path.GetFileNameWithoutExtension(file);
                var profile = LoadProfileFromFile(file);
                if (profile != null)
                {
                    profiles[profileName] = profile;
                }
            }
        }

        /// <summary>
        /// Load profile from file
        /// </summary>
        private ConfigProfile LoadProfileFromFile(string filePath)
        {
            var lines = File.ReadAllLines(filePath);
            var profile = new ConfigProfile();

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split('=');
                if (parts.Length != 2)
                    continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                // Parse profile metadata
                switch (key)
                {
                    case "Name":
                        profile.Name = value;
                        break;
                    case "Description":
                        profile.Description = value;
                        break;
                    case "GameName":
                        profile.GameName = value;
                        break;
                    case "CreatedDate":
                        profile.CreatedDate = DateTime.Parse(value);
                        break;
                    case "ModifiedDate":
                        profile.ModifiedDate = DateTime.Parse(value);
                        break;
                    case "IsDefault":
                        profile.IsDefault = bool.Parse(value);
                        break;
                    case "IsReadOnly":
                        profile.IsReadOnly = bool.Parse(value);
                        break;
                    default:
                        // Parse settings
                        if (key.StartsWith("Setting."))
                        {
                            var settingKey = key.Substring(8);
                            profile.Settings[settingKey] = ParseValue(value);
                        }
                        break;
                }
            }

            return profile;
        }

        /// <summary>
        /// Parse value from string
        /// </summary>
        private object ParseValue(string value)
        {
            if (bool.TryParse(value, out bool boolVal))
                return boolVal;
            if (int.TryParse(value, out int intVal))
                return intVal;
            if (float.TryParse(value, out float floatVal))
                return floatVal;
            if (Enum.TryParse<KeyCode>(value, out KeyCode keyVal))
                return keyVal;
            return value;
        }

        /// <summary>
        /// Save profile to disk
        /// </summary>
        public void SaveProfile(ConfigProfile profile)
        {
            if (profile.IsReadOnly)
            {
                throw new InvalidOperationException($"Cannot save read-only profile: {profile.Name}");
            }

            var filePath = Path.Combine(profilesDirectory, $"{profile.Name}.profile");
            using (var writer = new StreamWriter(filePath))
            {
                writer.WriteLine("# PeakHeadTracking Configuration Profile");
                writer.WriteLine($"# Generated: {DateTime.Now}");
                writer.WriteLine();

                // Write metadata
                writer.WriteLine($"Name={profile.Name}");
                writer.WriteLine($"Description={profile.Description}");
                writer.WriteLine($"GameName={profile.GameName}");
                writer.WriteLine($"CreatedDate={profile.CreatedDate:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"ModifiedDate={profile.ModifiedDate:yyyy-MM-dd HH:mm:ss}");
                writer.WriteLine($"IsDefault={profile.IsDefault}");
                writer.WriteLine($"IsReadOnly={profile.IsReadOnly}");
                writer.WriteLine();

                // Write settings
                writer.WriteLine("# Configuration Settings");
                foreach (var setting in profile.Settings)
                {
                    writer.WriteLine($"Setting.{setting.Key}={setting.Value}");
                }
            }
        }

        /// <summary>
        /// Load a profile by name
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown when profile does not exist</exception>
        public void LoadProfile(string profileName)
        {
            if (!profiles.ContainsKey(profileName))
            {
                throw new KeyNotFoundException($"Profile not found: {profileName}");
            }

            activeProfile = profiles[profileName];
            activeProfileName = profileName;

            // Save as last active profile
            mainConfig.Bind("Profile", "LastActiveProfile", "Default").Value = profileName;
            mainConfig.Save();

            PeakHeadTrackingPlugin.Logger.LogInfo($"Loaded profile: {profileName}");
        }

        /// <summary>
        /// Create new profile
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when profile already exists</exception>
        public ConfigProfile CreateProfile(string name, string description, string gameName = "General")
        {
            if (profiles.ContainsKey(name))
            {
                throw new InvalidOperationException($"Profile already exists: {name}");
            }

            var profile = new ConfigProfile
            {
                Name = name,
                Description = description,
                CreatedDate = DateTime.Now,
                ModifiedDate = DateTime.Now,
                GameName = gameName,
                IsDefault = false,
                IsReadOnly = false
            };

            profiles[name] = profile;
            cachedProfileNames = null;
            SaveProfile(profile);

            return profile;
        }

        /// <summary>
        /// Delete a profile
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown when profile does not exist</exception>
        /// <exception cref="InvalidOperationException">Thrown when profile is read-only or default</exception>
        public void DeleteProfile(string profileName)
        {
            if (!profiles.ContainsKey(profileName))
            {
                throw new KeyNotFoundException($"Profile not found: {profileName}");
            }

            var profile = profiles[profileName];
            if (profile.IsReadOnly || profile.IsDefault)
            {
                throw new InvalidOperationException($"Cannot delete protected profile: {profileName}");
            }

            profiles.Remove(profileName);
            cachedProfileNames = null;
            var filePath = Path.Combine(profilesDirectory, $"{profileName}.profile");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Switch to default if active profile was deleted
            if (activeProfileName == profileName)
            {
                LoadProfile("Default");
            }
        }

        /// <summary>
        /// Duplicate existing profile
        /// </summary>
        /// <exception cref="KeyNotFoundException">Thrown when source profile does not exist</exception>
        /// <exception cref="InvalidOperationException">Thrown when target profile already exists</exception>
        public ConfigProfile DuplicateProfile(string sourceName, string newName)
        {
            if (!profiles.ContainsKey(sourceName))
            {
                throw new KeyNotFoundException($"Source profile not found: {sourceName}");
            }

            if (profiles.ContainsKey(newName))
            {
                throw new InvalidOperationException($"Profile already exists: {newName}");
            }

            var source = profiles[sourceName];
            var duplicate = source.Clone(newName);
            duplicate.IsReadOnly = false;
            duplicate.IsDefault = false;

            profiles[newName] = duplicate;
            cachedProfileNames = null;
            SaveProfile(duplicate);

            return duplicate;
        }

        /// <summary>
        /// Get all profile names
        /// </summary>
        public List<string> GetProfileNames()
        {
            if (cachedProfileNames == null)
            {
                cachedProfileNames = profiles.Keys.ToList();
                cachedProfileNames.Sort(StringComparer.Ordinal);
            }
            return cachedProfileNames;
        }

        /// <summary>
        /// Get active profile
        /// </summary>
        public ConfigProfile GetActiveProfile()
        {
            return activeProfile;
        }

        /// <summary>
        /// Get active profile name
        /// </summary>
        public string GetActiveProfileName()
        {
            return activeProfileName;
        }

        /// <summary>
        /// Export current configuration to active profile
        /// </summary>
        public void SaveCurrentToProfile(ModConfiguration config)
        {
            if (activeProfile != null && !activeProfile.IsReadOnly)
            {
                activeProfile.ExportFromConfig(config);
                SaveProfile(activeProfile);
            }
        }

        /// <summary>
        /// Apply active profile to configuration
        /// </summary>
        public void ApplyProfileToConfig(ModConfiguration config)
        {
            if (activeProfile != null)
            {
                activeProfile.ImportToConfig(config);
                config.Save();
            }
        }
    }
}
