using CreaturePrefabCreator.Config;
using CreaturePrefabCreator.Overrides;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace CreaturePrefabCreator.Patches
{
    /// <summary>
    /// Manages automatic migration of PermanentAIDisabledMarker components when disableAI
    /// configuration changes from true to false. Ensures existing spawned creatures regain AI.
    /// </summary>
    public static class AIMarkerMigrationManager
    {
        private const string ConfigHashKey = "AIMarkerMigrationConfigHash";
        private static string _currentConfigHash;

        /// <summary>
        /// Computes a hash of all disableAI states from prefab overrides and generated prefabs.
        /// Used to detect configuration changes between game sessions.
        /// </summary>
        public static string ComputeConfigHash(CreaturePrefabCreatorConfigRoot config)
        {
            if (config == null) return string.Empty;

            var sb = new StringBuilder();
            
            // Hash prefab override disableAI states
            if (config.PrefabOverrides != null)
            {
                foreach (var po in config.PrefabOverrides.OrderBy(p => p.TargetPrefab))
                {
                    sb.Append($"PO:{po.TargetPrefab}:{po.Enabled}:{po.DisableAI};");
                }
            }
            
            // Hash generated prefab disableAI states
            if (config.GeneratedPrefabs != null)
            {
                foreach (var gp in config.GeneratedPrefabs.OrderBy(p => p.NewPrefab))
                {
                    sb.Append($"GP:{gp.NewPrefab}:{gp.Enabled}:{gp.DisableAI};");
                }
            }

            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                return BitConverter.ToString(hash).Replace("-", "");
            }
        }

        /// <summary>
        /// Checks if migration is needed by comparing stored hash with current config hash.
        /// </summary>
        public static bool IsMigrationNeeded(CreaturePrefabCreatorConfigRoot currentConfig)
        {
            _currentConfigHash = ComputeConfigHash(currentConfig);
            string storedHash = GetStoredHash();
            
            return storedHash != _currentConfigHash;
        }

        /// <summary>
        /// Runs migration to clean up PermanentAIDisabledMarker components for prefabs
        /// that changed from disableAI=true to disableAI=false.
        /// </summary>
        public static void MigrateOnStartup(CreaturePrefabCreatorConfigRoot currentConfig)
        {
            var plugin = CreaturePrefabCreatorPlugin.Instance;
            if (plugin == null) return;

            plugin.Log("[AIMarkerMigration] Checking for AI marker migrations...");

            int migratedCount = 0;

            // Check prefab overrides
            if (currentConfig?.PrefabOverrides != null)
            {
                foreach (var po in currentConfig.PrefabOverrides)
                {
                    if (!po.Enabled) continue;
                    
                    // If disableAI is now false, clean up any existing markers
                    if (!po.DisableAI)
                    {
                        int count = MigrateLiveInstances(po.TargetPrefab);
                        if (count > 0)
                        {
                            plugin.Log($"[AIMarkerMigration] Migrated {count} instance(s) of '{po.TargetPrefab}' (disableAI=false).");
                            migratedCount += count;
                        }
                    }
                }
            }

            // Check generated prefabs (for their adultPrefab references)
            if (currentConfig?.GeneratedPrefabs != null)
            {
                foreach (var gp in currentConfig.GeneratedPrefabs)
                {
                    if (!gp.Enabled) continue;
                    
                    // If disableAI is now false, clean up any existing markers
                    if (!gp.DisableAI)
                    {
                        int count = MigrateLiveInstances(gp.NewPrefab);
                        if (count > 0)
                        {
                            plugin.Log($"[AIMarkerMigration] Migrated {count} instance(s) of '{gp.NewPrefab}' (disableAI=false).");
                            migratedCount += count;
                        }
                    }
                }
            }

            if (migratedCount > 0)
            {
                plugin.Log($"[AIMarkerMigration] Total migrated: {migratedCount} creature(s).");
            }
            else
            {
                plugin.Log("[AIMarkerMigration] No migrations needed.");
            }

            // Store current hash for next time
            StoreHash(_currentConfigHash);
        }

        /// <summary>
        /// Manually migrates all live instances of a specific prefab.
        /// Used by console command cpc_migrate_ai_markers.
        /// </summary>
        public static int MigratePrefab(string prefabName)
        {
            return MigrateLiveInstances(prefabName);
        }

        /// <summary>
        /// Manually migrates all live instances across all prefabs.
        /// </summary>
        public static int MigrateAll()
        {
            var characters = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            int totalMigrated = 0;

            foreach (var character in characters)
            {
                if (character == null) continue;
                
                if (TryMigrateCreature(character.gameObject))
                {
                    totalMigrated++;
                }
            }

            return totalMigrated;
        }

        /// <summary>
        /// Migrates all live instances matching the given prefab name.
        /// Removes PermanentAIDisabledMarker and re-enables AI components.
        /// </summary>
        private static int MigrateLiveInstances(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return 0;

            var characters = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            int migratedCount = 0;

            foreach (var character in characters)
            {
                if (character == null) continue;

                string instanceName = character.gameObject.name;
                if (string.IsNullOrEmpty(instanceName)) continue;

                // Strip "(Clone)" suffix
                if (instanceName.EndsWith("(Clone)", System.StringComparison.Ordinal))
                    instanceName = instanceName.Substring(0, instanceName.Length - 7).TrimEnd();

                if (string.Equals(instanceName, prefabName, System.StringComparison.Ordinal))
                {
                    if (TryMigrateCreature(character.gameObject))
                    {
                        migratedCount++;
                    }
                }
            }

            return migratedCount;
        }

        /// <summary>
        /// Attempts to migrate a single creature by removing its marker and re-enabling AI.
        /// Returns true if migration was performed (marker was present).
        /// </summary>
        private static bool TryMigrateCreature(GameObject go)
        {
            var marker = go.GetComponent<PermanentAIDisabledMarker>();
            if (marker == null) return false;

            var plugin = CreaturePrefabCreatorPlugin.Instance;

            // Re-enable MonsterAI
            var monsterAI = go.GetComponent<MonsterAI>();
            if (monsterAI != null && !monsterAI.enabled)
            {
                monsterAI.enabled = true;
                plugin?.Log($"[AIMarkerMigration] '{go.name}': re-enabled MonsterAI.");
            }

            // Re-enable AnimalAI
            var animalAI = go.GetComponent<AnimalAI>();
            if (animalAI != null && !animalAI.enabled)
            {
                animalAI.enabled = true;
                plugin?.Log($"[AIMarkerMigration] '{go.name}': re-enabled AnimalAI.");
            }

            // Re-enable BaseAI
            var baseAI = go.GetComponent<BaseAI>();
            if (baseAI != null && !baseAI.enabled)
            {
                baseAI.enabled = true;
                plugin?.Log($"[AIMarkerMigration] '{go.name}': re-enabled BaseAI.");
            }

            // Destroy the marker
            UnityEngine.Object.Destroy(marker);
            plugin?.Log($"[AIMarkerMigration] '{go.name}': removed PermanentAIDisabledMarker.");

            return true;
        }

        /// <summary>
        /// Retrieves the stored config hash from BepInEx config.
        /// </summary>
        private static string GetStoredHash()
        {
            try
            {
                var plugin = CreaturePrefabCreatorPlugin.Instance;
                if (plugin == null) return string.Empty;

                var entry = plugin.Config.Bind(
                    "Internal", 
                    ConfigHashKey, 
                    string.Empty, 
                    "Internal hash of disableAI states for migration tracking. Do not modify.");
                
                return entry.Value ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// Stores the current config hash to BepInEx config.
        /// </summary>
        private static void StoreHash(string hash)
        {
            try
            {
                var plugin = CreaturePrefabCreatorPlugin.Instance;
                if (plugin == null) return;

                var entry = plugin.Config.Bind(
                    "Internal", 
                    ConfigHashKey, 
                    string.Empty, 
                    "Internal hash of disableAI states for migration tracking. Do not modify.");
                
                entry.Value = hash;
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[AIMarkerMigration] Failed to store hash: {ex.Message}");
            }
        }
    }
}
