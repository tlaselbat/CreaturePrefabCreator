using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace CreaturePrefabCreator.Config
{
    [DataContract]
    public class CreaturePrefabCreatorConfigRoot
    {
        [DataMember(Name = "version")]
        public string Version { get; set; } = "1.0.0";
        
        [DataMember(Name = "schemaVersion")]
        public int SchemaVersion { get; set; } = 1;

        [DataMember(Name = "generatedPrefabs")]
        public List<GeneratedPrefabConfig> GeneratedPrefabs { get; set; } = new List<GeneratedPrefabConfig>();

        [DataMember(Name = "prefabOverrides")]
        public List<PrefabOverrideConfig> PrefabOverrides { get; set; } = new List<PrefabOverrideConfig>();

        [DataMember(Name = "runtimeModifiers")]
        public List<RuntimeModifierConfig> RuntimeModifiers { get; set; } = new List<RuntimeModifierConfig>();
    }

    public static class CreaturePrefabCreatorConfigLoader
    {
        private const string ConfigFileName = "creaturePrefabCreator.json";
        private static readonly string ConfigDir = Path.Combine(BepInEx.Paths.ConfigPath, "CreaturePrefabCreator");
        private static readonly string ConfigPath = Path.Combine(ConfigDir, ConfigFileName);

        public static CreaturePrefabCreatorConfigRoot LoadAll(CreaturePrefabCreatorPlugin plugin)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                if (!File.Exists(ConfigPath))
                {
                    plugin.Log("Config file not found. Creating default creaturePrefabCreator.json.");
                    SaveDefault();
                }

                string json = File.ReadAllText(ConfigPath);
                var serializer = new DataContractJsonSerializer(typeof(CreaturePrefabCreatorConfigRoot));
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var root = (CreaturePrefabCreatorConfigRoot)serializer.ReadObject(stream);
                    if (root == null)
                    {
                        plugin.Log("Config root is null.");
                        return new CreaturePrefabCreatorConfigRoot();
                    }
                    plugin.Log($"Loaded {root.GeneratedPrefabs?.Count ?? 0} generated prefab entries.");
                    plugin.Log($"Loaded {root.PrefabOverrides?.Count ?? 0} prefab override entries.");
                    plugin.Log($"Loaded {root.RuntimeModifiers?.Count ?? 0} runtime modifier entries.");
                    return root;
                }
            }
            catch (Exception ex)
            {
                plugin.LogError($"ERROR loading creature config: {ex.Message}");
                BackupMalformedConfig(plugin);
                return new CreaturePrefabCreatorConfigRoot();
            }
        }

        /// <summary>
        /// P2: Re-reads the config file from disk and returns a fresh config root.
        /// Used by runtime config reload (cpc_reload_config).
        /// </summary>
        public static CreaturePrefabCreatorConfigRoot Reload(CreaturePrefabCreatorPlugin plugin)
        {
            plugin.Log("Reloading creaturePrefabCreator.json from disk...");
            return LoadAll(plugin);
        }

        private static void BackupMalformedConfig(CreaturePrefabCreatorPlugin plugin)
        {
            try
            {
                if (!File.Exists(ConfigPath)) return;

                string backupPath = Path.Combine(ConfigDir, $"creaturePrefabCreator_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                File.Copy(ConfigPath, backupPath);
                plugin.LogWarning($"Malformed config backed up to: {backupPath}");
            }
            catch (Exception backupEx)
            {
                plugin.LogError($"Failed to backup malformed config: {backupEx.Message}");
            }
        }

        public static void SaveDefault()
        {
            var defaultConfig = new CreaturePrefabCreatorConfigRoot
            {
                GeneratedPrefabs = new List<GeneratedPrefabConfig>
                {
                    new GeneratedPrefabConfig
                    {
                        Enabled = true,
                        SourcePrefab = "Bjorn",
                        NewPrefab = "Bjorn_cub",
                        AdultPrefab = "Bjorn",
                        DisplayName = "Bjorn Cub",
                        Scale = 0.35f,
                        GrowIntoAdult = true,
                        GrowTimeSeconds = 6000f,
                        PreserveTamed = true,
                        PreserveLevel = true,
                        PreserveOwner = true,
                        PreserveName = false
                    }
                },
                PrefabOverrides = new List<PrefabOverrideConfig>
                {
                    new PrefabOverrideConfig
                    {
                        Enabled = false,
                        TargetPrefab = "Wolf",
                        DisplayName = "",
                        Scale = 2.0f,
                        ApplyToExistingSpawnedCreatures = false,
                        PropagateToGeneratedVariants = true
                    }
                },
                RuntimeModifiers = new List<RuntimeModifierConfig>
                {
                    new RuntimeModifierConfig
                    {
                        Enabled = false,
                        TargetPrefab = "Wolf",
                        Conditions = new RuntimeModifierConditionConfig { StarLevel = 3 },
                        Effects = new RuntimeModifierEffectConfig { DamageMultiplier = 1.5f }
                    },
                    new RuntimeModifierConfig
                    {
                        Enabled = false,
                        TargetPrefab = "Lox",
                        Conditions = new RuntimeModifierConditionConfig { Tamed = true, Saddled = true },
                        Effects = new RuntimeModifierEffectConfig { MovementSpeedMultiplier = 0.2f }
                    }
                }
            };

            string json = Serialize(defaultConfig);
            File.WriteAllText(ConfigPath, json);
        }

        private static string Serialize(CreaturePrefabCreatorConfigRoot root)
        {
            var serializer = new DataContractJsonSerializer(typeof(CreaturePrefabCreatorConfigRoot));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, root);
                string json = Encoding.UTF8.GetString(stream.ToArray());
                return json;
            }
        }
    }
}
