using CreaturePrefabCreator.Config;
using CreaturePrefabCreator.GeneratedPrefabs;
using CreaturePrefabCreator.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CreaturePrefabCreator.Overrides
{
    public static class PrefabOverrideManager
    {
        private static readonly HashSet<string> AppliedOverrides = new HashSet<string>();
        // Track original scales to ensure absolute scaling relative to prefab's original
        private static readonly Dictionary<string, Vector3> OriginalScales = new Dictionary<string, Vector3>();

        // Cache configs for direct-override lookup by generated prefab managers
        private static List<PrefabOverrideConfig> _allConfigs;

        /// <summary>
        /// Checks if AllTameable_TamingOverhaul is loaded.
        /// </summary>
        private static bool IsAllTameableLoaded()
        {
            try
            {
                return BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("meldurson.valheim.AllTameable");
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Preserves AllTameable modifications when applying CPC overrides.
        /// Implements non-destructive merge behavior.
        /// </summary>
        private static void PreserveAllTameableFields(GameObject prefab, string prefabName)
        {
            if (!IsAllTameableLoaded()) return;

            try
            {
                var tameable = prefab.GetComponent<Tameable>();
                if (tameable == null) return;

                // Preserve AllTameable-modified fields that we shouldn't overwrite
                // This is a safety net - specific field preservation would require more detailed AllTameable knowledge
                CreaturePrefabCreatorPlugin.Instance?.Log($"[AllTameableCompat] Preserving AllTameable modifications for '{prefabName}'");
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[AllTameableCompat] Failed to preserve AllTameable fields for '{prefabName}': {ex.Message}");
            }
        }

        /// <summary>
        /// Clears all applied overrides and cached state. Call on world unload or config reload.
        /// </summary>
        public static void ClearAll()
        {
            AppliedOverrides.Clear();
            OriginalScales.Clear();
            _allConfigs = null;
        }

        /// <summary>
        /// P2: Re-apply all prefab overrides from a fresh config list.
        /// Used by runtime config reload.
        /// </summary>
        public static void ReapplyAll(List<PrefabOverrideConfig> configs, bool factionOverridesEnabled = false)
        {
            CreaturePrefabCreatorPlugin.Instance?.Log("[Reload] Clearing prefab override registrations...");
            AppliedOverrides.Clear();
            OriginalScales.Clear();
            _allConfigs = null;
            ApplyAll(configs, factionOverridesEnabled);
        }

        /// <summary>
        /// P2: Applies an action to every live spawned instance matching the given prefab name.
        /// Used to push AI changes to already-spawned creatures during runtime config reload.
        /// </summary>
        public static void ApplyToLiveInstances(string prefabName, Action<GameObject> action)
        {
            if (string.IsNullOrEmpty(prefabName) || action == null) return;

            var characters = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            int count = 0;
            foreach (var character in characters)
            {
                if (character == null) continue;
                string instanceName = character.gameObject.name;
                if (string.IsNullOrEmpty(instanceName)) continue;
                // Strip "(Clone)" suffix that Unity appends to instantiated objects
                if (instanceName.EndsWith("(Clone)", StringComparison.Ordinal))
                    instanceName = instanceName.Substring(0, instanceName.Length - 7).TrimEnd();

                if (string.Equals(instanceName, prefabName, StringComparison.Ordinal))
                {
                    try
                    {
                        action(character.gameObject);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[ApplyToLiveInstances] {character.gameObject.name}: error applying action: {ex.Message}");
                    }
                }
            }
            if (count > 0)
                CreaturePrefabCreatorPlugin.Instance?.Log($"[ApplyToLiveInstances] Applied changes to {count} live instance(s) of '{prefabName}'.");
        }

        /// <summary>
        /// Returns the Scale from an enabled override targeting this exact prefab name, or null if none.
        /// </summary>
        public static float? GetDirectOverrideScale(string prefabName)
        {
            if (_allConfigs == null) return null;
            foreach (var cfg in _allConfigs)
            {
                if (cfg.Enabled && string.Equals(cfg.TargetPrefab, prefabName, System.StringComparison.Ordinal))
                    return cfg.Scale;
            }
            return null;
        }

        /// <summary>
        /// Returns HealthMultiplier and DamageMultiplier from the first enabled override targeting
        /// this exact prefab name, or (null, null) if none.
        /// </summary>
        public static (float? health, float? damage) GetDirectOverrideStatMultipliers(string prefabName)
        {
            if (_allConfigs == null) return (null, null);
            foreach (var cfg in _allConfigs)
            {
                if (cfg.Enabled && string.Equals(cfg.TargetPrefab, prefabName, System.StringComparison.Ordinal))
                    return (cfg.HealthMultiplier, cfg.DamageMultiplier);
            }
            return (null, null);
        }

        /// <summary>
        /// Returns ForceFaction from the first enabled override targeting this exact prefab name,
        /// or null if none.
        /// </summary>
        public static string GetDirectOverrideFaction(string prefabName)
        {
            if (_allConfigs == null) return null;
            foreach (var cfg in _allConfigs)
            {
                if (cfg.Enabled && string.Equals(cfg.TargetPrefab, prefabName, System.StringComparison.Ordinal))
                    return cfg.ForceFaction;
            }
            return null;
        }

        /// <summary>
        /// P0: Applies all enabled prefab overrides.
        /// factionOverridesEnabled parameter gates faction changes (beta feature, default false).
        /// </summary>
        public static void ApplyAll(List<PrefabOverrideConfig> configs, bool factionOverridesEnabled = false)
        {
            _allConfigs = configs;
            if (configs == null || configs.Count == 0)
            {
                CreaturePrefabCreatorPlugin.Instance.Log("No prefab override configs to process.");
                return;
            }

            // P0: Log if faction overrides are disabled but present in config
            if (!factionOverridesEnabled)
            {
                int factionOverrideCount = 0;
                foreach (var cfg in configs)
                {
                    if (cfg.Enabled && !string.IsNullOrEmpty(cfg.ForceFaction))
                        factionOverrideCount++;
                }
                if (factionOverrideCount > 0)
                {
                    CreaturePrefabCreatorPlugin.Instance.LogWarning(
                        $"[FeatureSafety] {factionOverrideCount} faction override(s) found in config but EnableFactionOverrides is false. " +
                        "Faction changes will be skipped. Enable 'EnableFactionOverrides' to apply faction overrides (beta feature).");
                }
            }

            foreach (var config in configs)
            {
                if (!config.Enabled)
                {
                    CreaturePrefabCreatorPlugin.Instance.Log($"Skipping disabled prefab override: {config.TargetPrefab}");
                    continue;
                }

                if (!config.IsValid(out string error))
                {
                    CreaturePrefabCreatorPlugin.Instance.LogError($"Invalid prefab override config for '{config.TargetPrefab}': {error}. Skipping.");
                    continue;
                }

                if (AppliedOverrides.Contains(config.TargetPrefab))
                {
                    CreaturePrefabCreatorPlugin.Instance.LogWarning($"Override for '{config.TargetPrefab}' was already applied. Skipping duplicate.");
                    continue;
                }

                ApplyConfiguredOverride(config, factionOverridesEnabled);
            }
        }

        private static void ApplyConfiguredOverride(PrefabOverrideConfig config, bool factionOverridesEnabled = false)
        {
            GameObject targetPrefab = FindTargetPrefab(config.TargetPrefab);
            if (targetPrefab == null)
            {
                CreaturePrefabCreatorPlugin.Instance.LogError($"Target prefab '{config.TargetPrefab}' was not found. Skipping override.");
                return;
            }

            // Apply display name if provided
            if (!string.IsNullOrEmpty(config.DisplayName))
            {
                var character = targetPrefab.GetComponent<Character>();
                if (character != null)
                {
                    character.m_name = config.DisplayName;
                }
                else
                {
                    CreaturePrefabCreatorPlugin.Instance.LogWarning($"Target prefab '{config.TargetPrefab}' has no Character component. Display name not applied.");
                }
            }

            // Apply absolute scale relative to prefab's original scale
            if (!OriginalScales.ContainsKey(config.TargetPrefab))
            {
                OriginalScales[config.TargetPrefab] = targetPrefab.transform.localScale;
            }
            Vector3 originalScale = OriginalScales[config.TargetPrefab];
            targetPrefab.transform.localScale = originalScale * config.Scale;

            // Clear death effects if requested (e.g. vanilla Wolf_cub has a broken ragdoll death effect)
            if (config.ClearDeathEffects)
            {
                var character = targetPrefab.GetComponent<Character>();
                if (character != null)
                {
                    character.m_deathEffects = new EffectList();
                    CreaturePrefabCreatorPlugin.Instance.Log($"Cleared death effects on '{config.TargetPrefab}'.");
                }
            }

            // Copy death effects from a named source prefab (e.g. give Wolf_cub the Wolf ragdoll effect)
            if (!string.IsNullOrEmpty(config.CopyDeathEffectsFrom))
            {
                GameObject sourcePrefab = FindTargetPrefab(config.CopyDeathEffectsFrom);
                if (sourcePrefab == null)
                {
                    CreaturePrefabCreatorPlugin.Instance.LogWarning($"copyDeathEffectsFrom: source prefab '{config.CopyDeathEffectsFrom}' not found. Skipping death effect copy for '{config.TargetPrefab}'.");
                }
                else
                {
                    var sourceChar = sourcePrefab.GetComponent<Character>();
                    var targetChar = targetPrefab.GetComponent<Character>();
                    if (sourceChar == null || targetChar == null)
                    {
                        CreaturePrefabCreatorPlugin.Instance.LogWarning($"copyDeathEffectsFrom: missing Character on source '{config.CopyDeathEffectsFrom}' or target '{config.TargetPrefab}'. Skipping.");
                    }
                    else
                    {
                        var srcEffects = sourceChar.m_deathEffects?.m_effectPrefabs;
                        targetChar.m_deathEffects = new EffectList();
                        targetChar.m_deathEffects.m_effectPrefabs = srcEffects?.ToArray();
                        int count = targetChar.m_deathEffects.m_effectPrefabs?.Length ?? 0;
                        bool hasRagdoll = targetChar.m_deathEffects.m_effectPrefabs != null &&
                            System.Array.Exists(targetChar.m_deathEffects.m_effectPrefabs,
                                e => e?.m_prefab != null && e.m_prefab.GetComponent<Ragdoll>() != null);
                        CreaturePrefabCreatorPlugin.Instance.Log(
                            $"Copied {count} death effect(s) from '{config.CopyDeathEffectsFrom}' to '{config.TargetPrefab}' " +
                            $"(hasRagdoll={hasRagdoll}, deathEffectScaleMultiplier={config.DeathEffectScaleMultiplier?.ToString() ?? "none"}).");
                    }
                }
            }

            // Register death-effect scale multiplier with RagdollScalePatch for runtime scaling
            RagdollScalePatch.RegisterDeathEffectScaleMultiplier(config.TargetPrefab, config.DeathEffectScaleMultiplier);

            // P0: Apply faction override only if feature is enabled (beta, default false)
            if (!string.IsNullOrEmpty(config.ForceFaction))
            {
                if (factionOverridesEnabled)
                {
                    ApplyFactionOverride(targetPrefab, config.TargetPrefab, config.ForceFaction);
                }
                else
                {
                    CreaturePrefabCreatorPlugin.Instance.Log($"[FeatureSafety] Skipping faction override for '{config.TargetPrefab}' - EnableFactionOverrides is false.");
                }
            }

            // Apply stat multipliers (health/damage) to this prefab
            GeneratedPrefabManager.ApplyStatOverrides(
                targetPrefab, config.TargetPrefab,
                config.HealthMultiplier, config.DamageMultiplier);

            // Apply Phase 1 parameters (AI, audio, visual, faction controls)
            ApplyPhase1Parameters(targetPrefab, config);

            // Register inherited values so generated variants can inherit correctly
            if (config.PropagateToGeneratedVariants)
            {
                GeneratedPrefabManager.RegisterInheritedMultiplier(config.TargetPrefab, config.Scale);
                GeneratedPrefabManager.RegisterInheritedStatMultipliers(
                    config.TargetPrefab, config.HealthMultiplier, config.DamageMultiplier);
                // P0: Only propagate faction if feature is enabled
                if (factionOverridesEnabled && !string.IsNullOrEmpty(config.ForceFaction))
                    GeneratedPrefabManager.RegisterInheritedFaction(config.TargetPrefab, config.ForceFaction);
            }

            CreaturePrefabCreatorPlugin.Instance.Log($"Applied prefab override: {config.TargetPrefab} scale = {config.Scale} (propagateToGeneratedVariants={config.PropagateToGeneratedVariants}).");

            // Runtime instance scaling (v1: log that it's not implemented if enabled)
            if (config.ApplyToExistingSpawnedCreatures)
            {
                CreaturePrefabCreatorPlugin.Instance.Log("Runtime instance scaling is not implemented yet. This override affects future spawns only.");
            }

            AppliedOverrides.Add(config.TargetPrefab);
        }

        private static GameObject FindTargetPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return null;

            if (ZNetScene.instance != null)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
                if (prefab != null)
                    return prefab;
            }

            try
            {
                var jotunnPrefab = Jotunn.Managers.PrefabManager.Instance.GetPrefab(prefabName);
                if (jotunnPrefab != null)
                    return jotunnPrefab;
            }
            catch { }

            return null;
        }

        private static void ApplyFactionOverride(GameObject targetPrefab, string prefabName, string factionName)
        {
            var character = targetPrefab.GetComponent<Character>();
            if (character == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[Faction] '{prefabName}': no Character component, cannot apply faction override.");
                return;
            }

            Character.Faction originalFaction = character.m_faction;
            Character.Faction? newFaction = ParseFaction(factionName);

            if (!newFaction.HasValue)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[Faction] '{prefabName}': invalid faction name '{factionName}'. Valid: Players, ForestMonsters, PlainsMonsters, Undead, Demon, Boss, etc. Skipping.");
                return;
            }

            character.m_faction = newFaction.Value;
            CreaturePrefabCreatorPlugin.Instance?.Log($"[Faction] '{prefabName}': faction changed from {originalFaction} to {newFaction.Value}.");
        }

        /// <summary>
        /// Applies Phase 1 configurable parameters to prefab override targets.
        /// Order: AI settings → Audio → Visual.
        /// </summary>
        private static void ApplyPhase1Parameters(GameObject prefab, PrefabOverrideConfig config)
        {
            string prefabName = config.TargetPrefab;

            // Preserve AllTameable modifications before applying CPC overrides
            PreserveAllTameableFields(prefab, prefabName);

            // 1. AI settings (apply in order: aggro/flee/friend first, disableAI last)
            var monsterAI = prefab.GetComponent<MonsterAI>();
            if (monsterAI != null)
            {
                // disableAggro - prevent aggravation
                if (config.DisableAggro)
                {
                    monsterAI.m_aggravatable = false;
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[Phase1] '{prefabName}': disabled aggro (m_aggravatable = false).");
                }

                // disableFleeing - disable all flee behaviors (use reflection for safety)
                if (config.DisableFleeing)
                {
                    SetMonsterAIField(monsterAI, "m_fleeIfHurtWhenTargetCantSeeTarget", false);
                    SetMonsterAIField(monsterAI, "m_fleeIfNotAlerted", false);
                    SetMonsterAIField(monsterAI, "m_fleeInLava", false);
                    SetMonsterAIField(monsterAI, "m_fleeRange", 0f);
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[Phase1] '{prefabName}': disabled fleeing.");
                }

                // friendAttacked - only apply if explicitly set (use reflection for safety)
                if (config.FriendAttacked.HasValue)
                {
                    if (SetMonsterAIField(monsterAI, "m_friendAttacked", config.FriendAttacked.Value))
                    {
                        CreaturePrefabCreatorPlugin.Instance?.Log($"[Phase1] '{prefabName}': set friendAttacked = {config.FriendAttacked.Value}.");
                    }
                }

                // disableAI - disable the component last
                if (config.DisableAI)
                {
                    monsterAI.enabled = false;
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[Phase1] '{prefabName}': disabled MonsterAI component.");

                    // P2: Add marker so SaddledCreaturePatch knows this creature should gain AI while ridden
                    if (prefab.GetComponent<PermanentAIDisabledMarker>() == null)
                        prefab.AddComponent<PermanentAIDisabledMarker>();
                }
            }
            else if (config.DisableAI || config.DisableAggro || config.DisableFleeing || config.FriendAttacked.HasValue)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[Phase1] '{prefabName}': has no MonsterAI component, AI settings not applied.");
            }

            // 2. Audio settings - disable idle sounds via reflection
            if (config.DisableIdleSounds)
            {
                DisableIdleSounds(prefab, prefabName);
            }

            // 3. Visual settings - tintColor
            if (!string.IsNullOrWhiteSpace(config.TintColor) &&
                ColorUtility.TryParseHtmlString(config.TintColor, out var tint))
            {
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.materials)
                    {
                        if (material == null) continue;
                        if (material.HasProperty("_Color"))
                            material.color = tint;
                        if (material.HasProperty("_BaseColor"))
                            material.SetColor("_BaseColor", tint);
                    }
                }
                CreaturePrefabCreatorPlugin.Instance?.Log($"[Phase1] '{prefabName}': applied tintColor {config.TintColor}.");
            }
            else if (!string.IsNullOrWhiteSpace(config.TintColor))
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[Phase1] '{prefabName}': invalid tintColor '{config.TintColor}'.");
            }

            // 3b. Visual settings - glowColor
            if (!string.IsNullOrWhiteSpace(config.GlowColor) &&
                ColorUtility.TryParseHtmlString(config.GlowColor, out var glow))
            {
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.materials)
                    {
                        if (material == null) continue;
                        if (material.HasProperty("_EmissionColor"))
                        {
                            material.SetColor("_EmissionColor", glow);
                            material.EnableKeyword("_EMISSION");
                        }
                    }
                }
                CreaturePrefabCreatorPlugin.Instance?.Log($"[Phase1] '{prefabName}': applied glowColor {config.GlowColor}.");
            }
            else if (!string.IsNullOrWhiteSpace(config.GlowColor))
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[Phase1] '{prefabName}': invalid glowColor '{config.GlowColor}'.");
            }
        }

        /// <summary>
        /// Safely sets a field on MonsterAI using reflection. Returns true if field was found and set.
        /// </summary>
        private static bool SetMonsterAIField(MonsterAI monsterAI, string fieldName, object value)
        {
            if (monsterAI == null) return false;
            try
            {
                var field = typeof(MonsterAI).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(monsterAI, value);
                    return true;
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Disables idle sounds on a creature by finding CreatureAudio and clearing m_idleSounds.
        /// Uses reflection to avoid dependency on specific Valheim assembly version.
        /// </summary>
        private static void DisableIdleSounds(GameObject prefab, string prefabName)
        {
            try
            {
                // Try to find CreatureAudio component by name (safer than typeof)
                var creatureAudio = prefab.GetComponent("CreatureAudio");
                if (creatureAudio == null)
                {
                    // Also check children
                    creatureAudio = prefab.GetComponentInChildren(System.Type.GetType("CreatureAudio, assembly_valheim"), true);
                }

                if (creatureAudio != null)
                {
                    var idleSoundsField = creatureAudio.GetType().GetField("m_idleSounds", BindingFlags.Public | BindingFlags.Instance);
                    if (idleSoundsField != null)
                    {
                        var audioClipType = System.Type.GetType("UnityEngine.AudioClip, UnityEngine.AudioModule") ??
                                           System.Type.GetType("UnityEngine.AudioClip, UnityEngine");
                        if (audioClipType != null)
                        {
                            var emptyArray = System.Array.CreateInstance(audioClipType, 0);
                            idleSoundsField.SetValue(creatureAudio, emptyArray);
                            CreaturePrefabCreatorPlugin.Instance?.Log($"[Phase1] '{prefabName}': disabled idle sounds.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[Phase1] '{prefabName}': could not disable idle sounds: {ex.Message}");
            }
        }

        private static Character.Faction? ParseFaction(string factionName)
        {
            if (string.IsNullOrWhiteSpace(factionName)) return null;

            // Use direct enum parsing with case-insensitive matching
            // Common Valheim factions: Players, ForestMonsters, PlainsMonsters,
            // Undead, Demon, Mythical, Boss, PlayerFaction, Enemy
            if (System.Enum.TryParse<Character.Faction>(factionName.Trim(), true, out var result))
                return result;

            // Common aliases for convenience
            string normalized = factionName.Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "player":
                case "players":
                    return Character.Faction.Players;
                case "forest":
                case "forestcreature":
                case "forestcreatures":
                    return Character.Faction.ForestMonsters;
                case "plains":
                case "plainsmonster":
                case "plainsmonsters":
                    return Character.Faction.PlainsMonsters;
                case "undead":
                case "dungeon":
                    return Character.Faction.Undead;
                case "hell":
                    return Character.Faction.Demon;
                case "boss":
                case "bosses":
                    return Character.Faction.Boss;
                default:
                    return null;
            }
        }
    }
}
