using CreaturePrefabCreator.Config;
using CreaturePrefabCreator.Config.Advanced;
using CreaturePrefabCreator.Overrides;
using CreaturePrefabCreator.Patches;
using CreaturePrefabCreator.Utilities;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CreaturePrefabCreator.GeneratedPrefabs
{
    public static class GeneratedPrefabManager
    {
        private static readonly HashSet<string> RegisteredPrefabs = new HashSet<string>();
        private static GameObject _prefabContainer;

        // Maps sourcePrefab name → inherited EffectiveScaleMultiplier from an adult override
        private static readonly Dictionary<string, float> InheritedMultipliers = new Dictionary<string, float>();

        // Maps sourcePrefab name → inherited health/damage multipliers from an adult override
        private static readonly Dictionary<string, float> InheritedHealthMultipliers = new Dictionary<string, float>();
        private static readonly Dictionary<string, float> InheritedDamageMultipliers = new Dictionary<string, float>();

        // Maps source prefab name → vanilla localScale captured before any override is applied
        private static readonly Dictionary<string, Vector3> OriginalPrefabScales = new Dictionary<string, Vector3>();

        /// <summary>
        /// Called by PrefabOverrideManager after applying an override with propagateToGeneratedVariants=true.
        /// </summary>
        public static void RegisterInheritedMultiplier(string sourcePrefabName, float multiplier)
        {
            InheritedMultipliers[sourcePrefabName] = multiplier;
        }

        /// <summary>
        /// Returns the registered inherited multiplier for the given source prefab, or 1.0 if none.
        /// </summary>
        public static float GetInheritedMultiplier(string sourcePrefabName)
        {
            return InheritedMultipliers.TryGetValue(sourcePrefabName, out float m) ? m : 1f;
        }

        /// <summary>
        /// Called by PrefabOverrideManager after applying an override with propagateToGeneratedVariants=true.
        /// Stores health and/or damage multipliers for generated variants of the source prefab.
        /// Only values that pass IsValidMultiplier() and are not identity (1.0) are stored.
        /// </summary>
        public static void RegisterInheritedStatMultipliers(string sourcePrefabName, float? health, float? damage)
        {
            if (health.HasValue && IsValidMultiplier(health.Value) && health.Value != 1f)
                InheritedHealthMultipliers[sourcePrefabName] = health.Value;
            if (damage.HasValue && IsValidMultiplier(damage.Value) && damage.Value != 1f)
                InheritedDamageMultipliers[sourcePrefabName] = damage.Value;
        }

        /// <summary>
        /// Returns inherited health and damage multipliers for the given source prefab, or null if none.
        /// </summary>
        public static (float? health, float? damage) GetInheritedStatMultipliers(string sourcePrefabName)
        {
            float? health = InheritedHealthMultipliers.TryGetValue(sourcePrefabName, out float h) ? h : (float?)null;
            float? damage = InheritedDamageMultipliers.TryGetValue(sourcePrefabName, out float d) ? d : (float?)null;
            return (health, damage);
        }

        // Maps sourcePrefab name → inherited faction from an adult override
        private static readonly Dictionary<string, string> InheritedFactions = new Dictionary<string, string>();

        /// <summary>
        /// Called by PrefabOverrideManager after applying an override with propagateToGeneratedVariants=true.
        /// Stores the faction override for generated variants of the source prefab.
        /// </summary>
        public static void RegisterInheritedFaction(string sourcePrefabName, string faction)
        {
            if (!string.IsNullOrEmpty(faction))
                InheritedFactions[sourcePrefabName] = faction;
        }

        /// <summary>
        /// Returns the inherited faction override for the given source prefab, or null if none.
        /// </summary>
        public static string GetInheritedFaction(string sourcePrefabName)
        {
            return InheritedFactions.TryGetValue(sourcePrefabName, out string f) ? f : null;
        }

        private static bool IsValidMultiplier(float value)
        {
            return value >= 0.01f && value <= 100f;
        }

        public static void ClearAll()
        {
            RegisteredPrefabs.Clear();
            InheritedMultipliers.Clear();
            InheritedHealthMultipliers.Clear();
            InheritedDamageMultipliers.Clear();
            OriginalPrefabScales.Clear();
            InheritedFactions.Clear();
        }

        /// <summary>
        /// Snapshots the current localScale of each source prefab referenced by the given generated
        /// prefab configs, capturing the vanilla (pre-override) scale.
        /// MUST be called BEFORE PrefabOverrideManager.ReapplyAll/ApplyAll so the scale stored is
        /// the true original and not a value already multiplied by a previous override pass.
        /// </summary>
        public static void CaptureOriginalSourceScales(List<GeneratedPrefabConfig> configs)
        {
            if (configs == null) return;
            foreach (var cfg in configs)
            {
                if (!cfg.Enabled || string.IsNullOrEmpty(cfg.SourcePrefab)) continue;
                if (OriginalPrefabScales.ContainsKey(cfg.SourcePrefab)) continue;
                var prefab = FindSourcePrefab(cfg.SourcePrefab);
                if (prefab == null) continue;
                OriginalPrefabScales[cfg.SourcePrefab] = prefab.transform.localScale;
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[ScaleCapture] '{cfg.SourcePrefab}': captured originalSourceScale={prefab.transform.localScale} before override pass.");
            }
        }

        /// <summary>
        /// P2: Re-register all generated prefabs from a fresh config list.
        /// Used by runtime config reload. Existing spawned instances still reference old templates.
        /// </summary>
        public static void ReregisterAll(List<GeneratedPrefabConfig> configs, bool factionOverridesEnabled = false)
        {
            CreaturePrefabCreatorPlugin.Instance?.Log("[Reload] Clearing generated prefab registrations...");
            RegisteredPrefabs.Clear();
            InheritedMultipliers.Clear();
            InheritedHealthMultipliers.Clear();
            InheritedDamageMultipliers.Clear();
            // OriginalPrefabScales is intentionally NOT cleared here.
            // It was populated by CaptureOriginalSourceScales() before PrefabOverrideManager.ReapplyAll()
            // mutated the source prefabs. Clearing it here would cause scale compounding on each reload.
            InheritedFactions.Clear();

            GenerateAll(configs, factionOverridesEnabled);
            CreaturePrefabCreatorPlugin.Instance?.LogWarning("[Reload] Existing spawned instances still use old templates. New spawns will use updated templates.");
        }

        private static GameObject GetOrCreatePrefabContainer()
        {
            if (_prefabContainer != null) return _prefabContainer;
            _prefabContainer = new GameObject("CreaturePrefabCreator_PrefabContainer");
            _prefabContainer.SetActive(false);
            UnityEngine.Object.DontDestroyOnLoad(_prefabContainer);
            _prefabContainer.hideFlags = HideFlags.HideAndDontSave;
            return _prefabContainer;
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return "(null)";
            var sb = new System.Text.StringBuilder();
            sb.Append(t.name);
            while (t.parent != null)
            {
                t = t.parent;
                sb.Insert(0, t.name + "/");
            }
            return sb.ToString();
        }

        private static void LogPrefabDiagnostics(string label, GameObject obj)
        {
            if (obj == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG] {label}: NULL");
                return;
            }

            var znv = obj.GetComponent<ZNetView>();
            var character = obj.GetComponent<Character>();
            var humanoid = obj.GetComponent<Humanoid>();
            var monsterAI = obj.GetComponent<MonsterAI>();
            var baseAI = obj.GetComponent<BaseAI>();
            var rb = obj.GetComponent<Rigidbody>();
            var colliders = obj.GetComponentsInChildren<Collider>(true);
            var zst = obj.GetComponent<ZSyncTransform>();
            var zsa = obj.GetComponent<ZSyncAnimation>();
            var procreation = obj.GetComponentInChildren<Procreation>(true);
            var offspringGrowup = obj.GetComponent<OffspringGrowup>();

            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG] {label}:");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   name: {obj.name}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   instanceID: {obj.GetInstanceID()}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   activeSelf: {obj.activeSelf}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   activeInHierarchy: {obj.activeInHierarchy}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   parentPath: {GetTransformPath(obj.transform.parent)}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   scene: {obj.scene.name}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   hideFlags: {obj.hideFlags}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   has ZNetView: {znv != null}");
            if (znv != null)
                CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   ZNetView.enabled: {znv.enabled}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   has Character: {character != null}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   has Humanoid: {humanoid != null}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   has MonsterAI: {monsterAI != null}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   has BaseAI: {baseAI != null}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   has Rigidbody: {rb != null}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   colliderCount: {colliders.Length}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   has ZSyncTransform: {zst != null}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   has ZSyncAnimation: {zsa != null}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   has Procreation: {procreation != null}");
            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG]   has OffspringGrowup: {offspringGrowup != null}");
        }

        private static bool ValidateGeneratedTemplate(string prefabName, GameObject template, bool expectOffspringGrowup)
        {
            if (template == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"[DIAG] Validation FAILED for '{prefabName}': template is null.");
                return false;
            }
            if (template.name != prefabName)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"[DIAG] Validation FAILED for '{prefabName}': template.name='{template.name}' does not match.");
                return false;
            }
            if (!template.activeSelf)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"[DIAG] Validation FAILED for '{prefabName}': template.activeSelf is false. Spawned instances will be inactive.");
                return false;
            }
            if (template.activeInHierarchy)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"[DIAG] Validation FAILED for '{prefabName}': template.activeInHierarchy is true. Template is ticking in the scene.");
                return false;
            }
            if (template.GetComponent<ZNetView>() == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"[DIAG] Validation FAILED for '{prefabName}': template has no ZNetView.");
                return false;
            }
            var procreation = template.GetComponentInChildren<Procreation>(true);
            if (procreation != null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"[DIAG] Validation FAILED for '{prefabName}': template still has Procreation.");
                return false;
            }
            if (expectOffspringGrowup && template.GetComponent<OffspringGrowup>() == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"[DIAG] Validation FAILED for '{prefabName}': template missing OffspringGrowup.");
                return false;
            }

            CreaturePrefabCreatorPlugin.Instance?.Log($"[DIAG] Validation PASSED for '{prefabName}'.");
            return true;
        }

        /// <summary>
        /// P0: Generates all enabled prefabs.
        /// factionOverridesEnabled parameter gates faction changes (beta feature, default false).
        /// </summary>
        public static void GenerateAll(List<GeneratedPrefabConfig> configs, bool factionOverridesEnabled = false)
        {
            if (configs == null || configs.Count == 0)
            {
                CreaturePrefabCreatorPlugin.Instance.Log("No generated prefab configs to process.");
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
                        $"[FeatureSafety] {factionOverrideCount} generated prefab(s) have faction overrides in config but EnableFactionOverrides is false. " +
                        "Faction changes will be skipped. Enable 'EnableFactionOverrides' to apply faction overrides (beta feature).");
                }
            }

            CreaturePrefabCreatorPlugin.Instance.Log("Using safe inactive-clone + active-template registration for generated prefabs.");
            CreaturePrefabCreatorPlugin.Instance.Log("Procreation will be stripped from generated prefabs to prevent baby breeding and AllTameable InitProcPrefabs crashes.");

            foreach (var config in configs)
            {
                if (!config.Enabled)
                {
                    CreaturePrefabCreatorPlugin.Instance.Log($"Skipping disabled generated prefab config: {config.NewPrefab}");
                    continue;
                }

                if (!config.IsValid(out string error))
                {
                    CreaturePrefabCreatorPlugin.Instance.LogError($"Invalid generated prefab config for '{config.NewPrefab}': {error}. Skipping.");
                    continue;
                }

                if (RegisteredPrefabs.Contains(config.NewPrefab))
                {
                    CreaturePrefabCreatorPlugin.Instance.LogWarning($"Generated prefab '{config.NewPrefab}' was already registered. Skipping duplicate.");
                    continue;
                }

                GenerateConfiguredPrefab(config, factionOverridesEnabled);
            }
        }

        private static void GenerateConfiguredPrefab(GeneratedPrefabConfig config, bool factionOverridesEnabled = false)
        {
            // 1. Locate source prefab
            GameObject sourcePrefab = FindSourcePrefab(config.SourcePrefab);
            if (sourcePrefab == null)
            {
                CreaturePrefabCreatorPlugin.Instance.LogError($"Source prefab '{config.SourcePrefab}' was not found. Skipping '{config.NewPrefab}'.");
                return;
            }

            CreaturePrefabCreatorPlugin.Instance.Log($"Found source prefab {config.SourcePrefab}.");
            LogPrefabDiagnostics($"[{config.NewPrefab}] Source prefab", sourcePrefab);

            // 2. Check for ZNetView on source before clone
            bool sourceHasZNetView = sourcePrefab.GetComponent<ZNetView>() != null;
            CreaturePrefabCreatorPlugin.Instance.Log($"Source '{config.SourcePrefab}' has ZNetView: {sourceHasZNetView}");

            // 3. Clone the source prefab using the safe inactive-clone pattern.
            GameObject setupClone = null;
            bool sourceWasActive = sourcePrefab.activeSelf;
            try
            {
                sourcePrefab.SetActive(false);
                setupClone = UnityEngine.Object.Instantiate(sourcePrefab);
            }
            finally
            {
                sourcePrefab.SetActive(sourceWasActive);
            }

            if (setupClone == null)
            {
                CreaturePrefabCreatorPlugin.Instance.LogError($"Failed to clone source prefab '{config.SourcePrefab}'.");
                return;
            }

            setupClone.name = config.NewPrefab;

            // 4. Strip all Procreation components from the clone (root and children).
            var procreationComponents = setupClone.GetComponentsInChildren<Procreation>(true);
            foreach (var proc in procreationComponents)
                UnityEngine.Object.DestroyImmediate(proc);
            if (procreationComponents.Length > 0)
                CreaturePrefabCreatorPlugin.Instance.Log($"Removed {procreationComponents.Length} Procreation component(s) from generated prefab {config.NewPrefab}.");

            // 4b. Clear m_defaultItems on any Humanoid — prevents ZSyncTransform NRE spam from
            //     Humanoid.GiveDefaultItems() Instantiating item prefabs that have no valid ZNetView context.
            var humanoidComp = setupClone.GetComponent<Humanoid>();
            if (humanoidComp != null && humanoidComp.m_defaultItems != null && humanoidComp.m_defaultItems.Length > 0)
            {
                int defaultItemCount = humanoidComp.m_defaultItems.Length;
                humanoidComp.m_defaultItems = new GameObject[0];
                CreaturePrefabCreatorPlugin.Instance.Log($"Cleared {defaultItemCount} default item(s) on generated Humanoid prefab '{config.NewPrefab}' to prevent ZSyncTransform NREs.");
            }

            // 5. Strip optional mod components that assume adult state (MountUp, etc.)
            StripOptionalComponents(setupClone, config.NewPrefab);

            // 6. Set display name
            if (!string.IsNullOrEmpty(config.DisplayName))
            {
                var character = setupClone.GetComponent<Character>();
                if (character != null)
                {
                    character.m_name = config.DisplayName;
                }
            }

            // 7. Apply scale: originalSourceScale × baseScale × effectiveMultiplier
            //    originalSourceScale: vanilla scale of the source prefab, captured before any override
            //    config.Scale:        base scale from the generated config (e.g. 0.4 for a pup)
            //    effectiveMultiplier: direct override for this new prefab if one exists, else inherited from source override
            if (!OriginalPrefabScales.ContainsKey(config.SourcePrefab))
                OriginalPrefabScales[config.SourcePrefab] = sourcePrefab.transform.localScale;
            Vector3 originalSourceScale = OriginalPrefabScales[config.SourcePrefab];

            float? directOverride = PrefabOverrideManager.GetDirectOverrideScale(config.NewPrefab);
            float inheritedMultiplier = GetInheritedMultiplier(config.SourcePrefab);
            float effectiveMultiplier = directOverride.HasValue ? directOverride.Value : inheritedMultiplier;

            setupClone.transform.localScale = originalSourceScale * config.Scale * effectiveMultiplier;

            CreaturePrefabCreatorPlugin.Instance.Log(
                $"Scale '{config.NewPrefab}': originalSourceScale={originalSourceScale}, " +
                $"baseScale={config.Scale}, effectiveMultiplier={effectiveMultiplier} " +
                $"(direct={directOverride?.ToString() ?? "none"}, inherited={inheritedMultiplier}) " +
                $"→ finalScale={setupClone.transform.localScale}");

            // 7b. Apply death effect overrides from config (clearDeathEffects, copyDeathEffectsFrom)
            var cloneChar = setupClone.GetComponent<Character>();
            if (cloneChar != null)
            {
                if (config.ClearDeathEffects)
                {
                    cloneChar.m_deathEffects = new EffectList();
                    CreaturePrefabCreatorPlugin.Instance.Log($"Cleared death effects on generated prefab '{config.NewPrefab}'.");
                }

                if (!string.IsNullOrEmpty(config.CopyDeathEffectsFrom))
                {
                    GameObject deathSrc = FindSourcePrefab(config.CopyDeathEffectsFrom);
                    if (deathSrc == null)
                    {
                        CreaturePrefabCreatorPlugin.Instance.LogWarning(
                            $"copyDeathEffectsFrom: source prefab '{config.CopyDeathEffectsFrom}' not found. " +
                            $"Skipping death effect copy for generated '{config.NewPrefab}'.");
                    }
                    else
                    {
                        var srcChar = deathSrc.GetComponent<Character>();
                        if (srcChar == null)
                        {
                            CreaturePrefabCreatorPlugin.Instance.LogWarning(
                                $"copyDeathEffectsFrom: '{config.CopyDeathEffectsFrom}' has no Character component. Skipping.");
                        }
                        else
                        {
                            var srcEffects = srcChar.m_deathEffects?.m_effectPrefabs;
                            cloneChar.m_deathEffects = new EffectList();
                            cloneChar.m_deathEffects.m_effectPrefabs = srcEffects?.ToArray();
                            int count = cloneChar.m_deathEffects.m_effectPrefabs?.Length ?? 0;
                            bool hasRagdoll = cloneChar.m_deathEffects.m_effectPrefabs != null &&
                                System.Array.Exists(cloneChar.m_deathEffects.m_effectPrefabs,
                                    e => e?.m_prefab != null && e.m_prefab.GetComponent<Ragdoll>() != null);
                            CreaturePrefabCreatorPlugin.Instance.Log(
                                $"Copied {count} death effect(s) from '{config.CopyDeathEffectsFrom}' to generated '{config.NewPrefab}' " +
                                $"(hasRagdoll={hasRagdoll}, deathEffectScaleMultiplier={config.DeathEffectScaleMultiplier?.ToString() ?? "none"}).");
                        }
                    }
                }
            }

            // Register deathEffectScaleMultiplier with RagdollScalePatch (null = remove/no-op)
            RagdollScalePatch.RegisterDeathEffectScaleMultiplier(config.NewPrefab, config.DeathEffectScaleMultiplier);

            // P0: 7c. Apply faction override only if feature is enabled (beta, default false)
            // Priority: direct override > generated config > inherited from source override
            if (factionOverridesEnabled)
            {
                var directFaction = PrefabOverrideManager.GetDirectOverrideFaction(config.NewPrefab);
                var inheritedFaction = GetInheritedFaction(config.SourcePrefab);
                string effectiveFaction = directFaction ?? config.ForceFaction ?? inheritedFaction;
                if (!string.IsNullOrEmpty(effectiveFaction))
                {
                    ApplyFactionOverride(setupClone, config.NewPrefab, effectiveFaction);
                }
            }
            else if (!string.IsNullOrEmpty(config.ForceFaction))
            {
                CreaturePrefabCreatorPlugin.Instance.Log($"[FeatureSafety] Skipping faction override for generated '{config.NewPrefab}' - EnableFactionOverrides is false.");
            }

            // 7d. Apply stat multipliers (health/damage/movement/advanced) before registration
            // Priority: direct override > generated config > inherited from source override
            var directStats = PrefabOverrideManager.GetDirectOverrideStatMultipliers(config.NewPrefab);
            var inheritedStats = GetInheritedStatMultipliers(config.SourcePrefab);
            float? effectiveHealthMult = directStats.health ?? config.HealthMultiplier ?? inheritedStats.health;
            float? effectiveDamageMult = directStats.damage ?? config.DamageMultiplier ?? inheritedStats.damage;

            // Resolve advanced death effect settings
            var deathEffectResult = ModifierResolver.ResolveDeathEffect(config.Advanced,
                config.ClearDeathEffects, config.CopyDeathEffectsFrom, config.DeathEffectScaleMultiplier);

            // Apply advanced stat overrides including health, damage, movement speed, AI
            ApplyStatOverrides(setupClone, config.NewPrefab, effectiveHealthMult, effectiveDamageMult,
                null, config.Advanced, factionOverridesEnabled);

            // Apply advanced death effects if configured
            ApplyAdvancedDeathEffects(setupClone, config.NewPrefab, deathEffectResult);

            // 7e. Apply Phase 1 parameters (AI, audio, visual, faction controls) - legacy fields
            ApplyPhase1Parameters(setupClone, config.NewPrefab, config);

            // 8. Attach growth component if enabled
            if (config.GrowIntoAdult)
            {
                var znv = setupClone.GetComponent<ZNetView>();
                if (znv == null)
                {
                    CreaturePrefabCreatorPlugin.Instance.LogWarning($"Cannot add growth to '{config.NewPrefab}' - ZNetView missing. Growth disabled.");
                }
                else
                {
                    var growup = setupClone.GetComponent<OffspringGrowup>();
                    if (growup == null)
                        growup = setupClone.AddComponent<OffspringGrowup>();

                    growup.adultPrefabName = config.AdultPrefab;
                    growup.growTimeSeconds = config.GrowTimeSeconds;
                    growup.preserveTamed = config.PreserveTamed;
                    growup.preserveLevel = config.PreserveLevel;
                    growup.preserveOwner = config.PreserveOwner;
                    growup.preserveName = config.PreserveName;

                    if (FindSourcePrefab(config.AdultPrefab) == null)
                    {
                        CreaturePrefabCreatorPlugin.Instance.LogWarning($"Adult prefab '{config.AdultPrefab}' was not found. Growth for '{config.NewPrefab}' may fail.");
                    }
                    else
                    {
                        CreaturePrefabCreatorPlugin.Instance.Log($"Added growth: {config.NewPrefab} -> {config.AdultPrefab} after {config.GrowTimeSeconds} seconds.");
                    }
                }
            }

            // 9. Parent under persistent hidden container.
            //    Container is inactive so activeInHierarchy stays false while activeSelf is true.
            GameObject persistentContainer = GetOrCreatePrefabContainer();
            setupClone.transform.SetParent(persistentContainer.transform, false);
            setupClone.SetActive(true); // activeSelf=true, activeInHierarchy=false
            setupClone.hideFlags = HideFlags.HideAndDontSave;

            LogPrefabDiagnostics($"[{config.NewPrefab}] Setup clone ready for registration", setupClone);

            // 10. Validate the template BEFORE registration
            if (!ValidateGeneratedTemplate(config.NewPrefab, setupClone, config.GrowIntoAdult))
            {
                CreaturePrefabCreatorPlugin.Instance.LogError($"Template for '{config.NewPrefab}' failed validation. Aborting registration.");
                return;
            }

            // 11. Register with Jotunn — it handles ZNetScene injection when it processes its queue.
            try
            {
                var customPrefab = new CustomPrefab(setupClone, fixReference: true);
                PrefabManager.Instance.AddPrefab(customPrefab);
                CreaturePrefabCreatorPlugin.Instance.Log($"Registered '{config.NewPrefab}' with Jotunn (InstanceID={setupClone.GetInstanceID()}).");
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance.LogError($"Jotunn registration failed for '{config.NewPrefab}': {ex.Message}. Prefab will not be spawnable.");
                return;
            }

            // 12. Schedule delayed verification — confirms Jotunn injected into ZNetScene
            CreaturePrefabCreatorPlugin.Instance?.RunCoroutine(
                DelayedRegistrationVerification(config.NewPrefab, sourceHasZNetView, 2.0f));

            RegisteredPrefabs.Add(config.NewPrefab);
        }

        /// <summary>
        /// Verifies the generated prefab is discoverable via ZNetScene after Jotunn registration.
        /// Uses comprehensive diagnostics and validation.
        /// </summary>
        private static System.Collections.IEnumerator DelayedRegistrationVerification(string prefabName, bool shouldHaveZNetView, float delaySeconds)
        {
            yield return new UnityEngine.WaitForSeconds(delaySeconds);

            int waitAttempts = 0;
            while (ZNetScene.instance == null && waitAttempts < 30)
            {
                yield return new UnityEngine.WaitForSeconds(1f);
                waitAttempts++;
            }

            if (ZNetScene.instance == null) yield break;

            // Safety net: ensure Jotunn's registration made it into ZNetScene
            EnsureZNetSceneRegistration(prefabName);

            GameObject registeredPrefab = ZNetScene.instance.GetPrefab(prefabName);

            if (registeredPrefab == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"CRITICAL: ZNetScene.GetPrefab('{prefabName}') returned null after deferred registration. Spawn will fail.");
                yield break;
            }

            // Validate template state
            bool hasErrors = false;
            if (!registeredPrefab.activeSelf)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"CRITICAL: Registered prefab '{prefabName}' has activeSelf=false. Spawned instances will be inactive.");
                hasErrors = true;
            }
            if (registeredPrefab.activeInHierarchy)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"CRITICAL: Registered prefab '{prefabName}' has activeInHierarchy=true. Template is ticking in the scene.");
                hasErrors = true;
            }
            if (shouldHaveZNetView && registeredPrefab.GetComponent<ZNetView>() == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"CRITICAL: Generated prefab '{prefabName}' has no ZNetView. Spawned instances will fail to sync.");
                hasErrors = true;
            }

            if (!hasErrors)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log($"[VERIFIED] '{prefabName}' confirmed in ZNetScene (InstanceID={registeredPrefab.GetInstanceID()}, activeSelf={registeredPrefab.activeSelf}).");
            }
        }

        /// <summary>
        /// Ensures the prefab is present in ZNetScene's internal lists using the correct key type.
        /// Searches for the template in m_prefabs by name (our template is in the persistent container).
        /// </summary>
        private static bool EnsureZNetSceneRegistration(string prefabName)
        {
            if (ZNetScene.instance == null) return false;

            if (ZNetScene.instance.GetPrefab(prefabName) != null)
                return true;

            // Search in m_prefabs list by name to find our registered template
            GameObject prefab = null;
            foreach (var p in ZNetScene.instance.m_prefabs)
            {
                if (p != null && p.name == prefabName)
                {
                    prefab = p;
                    break;
                }
            }

            if (prefab == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"Generated prefab {prefabName} was registered but ZNetScene.GetPrefab still cannot resolve it.");
                return false;
            }

            // Register in named lookup
            RegisterInZNetScene(prefab, prefabName);

            // Final verification
            if (ZNetScene.instance.GetPrefab(prefabName) != null)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log($"Verified ZNetScene lookup for generated prefab {prefabName}.");
                return true;
            }
            else
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"Generated prefab {prefabName} was registered but ZNetScene.GetPrefab still cannot resolve it.");
                return false;
            }
        }

        /// <summary>
        /// Registers a prefab in ZNetScene's internal lists (m_prefabs and m_namedPrefabs).
        /// Must be called with the FINAL template object, never a temporary clone.
        /// </summary>
        private static void RegisterInZNetScene(GameObject prefab, string prefabName)
        {
            if (ZNetScene.instance == null)
            {
                CreaturePrefabCreatorPlugin.Instance.LogError("ZNetScene is not available. Cannot manually register prefab.");
                return;
            }

            if (prefab == null)
            {
                CreaturePrefabCreatorPlugin.Instance.LogError($"Cannot register null prefab for '{prefabName}'.");
                return;
            }

            if (!ZNetScene.instance.m_prefabs.Contains(prefab))
            {
                ZNetScene.instance.m_prefabs.Add(prefab);
            }

            var namedPrefabsField = typeof(ZNetScene).GetField("m_namedPrefabs", BindingFlags.NonPublic | BindingFlags.Instance);
            if (namedPrefabsField != null)
            {
                var namedPrefabs = namedPrefabsField.GetValue(ZNetScene.instance);
                if (namedPrefabs != null)
                {
                    var namedType = namedPrefabs.GetType();
                    if (namedType.IsGenericType)
                    {
                        var genericArgs = namedType.GetGenericArguments();
                        var keyType = genericArgs[0];
                        var containsMethod = namedType.GetMethod("ContainsKey");
                        var addMethod = namedType.GetMethod("Add");

                        if (keyType == typeof(int))
                        {
                            int hash = prefabName.GetStableHashCode();
                            bool alreadyHas = (bool)containsMethod.Invoke(namedPrefabs, new object[] { hash });
                            if (!alreadyHas && addMethod != null)
                                addMethod.Invoke(namedPrefabs, new object[] { hash, prefab });
                        }
                        else if (keyType == typeof(string))
                        {
                            bool alreadyHas = (bool)containsMethod.Invoke(namedPrefabs, new object[] { prefabName });
                            if (!alreadyHas && addMethod != null)
                                addMethod.Invoke(namedPrefabs, new object[] { prefabName, prefab });
                        }
                    }
                }
            }
        }

        private static void StripOptionalComponents(GameObject prefab, string prefabName)
        {
            string[] optionalTypeNames = new[]
            {
                "MountUp.Mountable",
                "MountUp.Rider"
            };

            foreach (var typeName in optionalTypeNames)
            {
                Type type = Type.GetType(typeName) ?? Type.GetType(typeName + ", MountUp");
                if (type == null) continue;

                var components = prefab.GetComponentsInChildren(type, true);
                foreach (var comp in components)
                {
                    if (comp != null)
                        UnityEngine.Object.DestroyImmediate(comp);
                }
                if (components.Length > 0)
                {
                    CreaturePrefabCreatorPlugin.Instance.Log($"Removed {components.Length} {type.Name} component(s) from generated prefab {prefabName}.");
                }
            }
        }

        /// <summary>
        /// NOTE: This method is DEPRECATED and kept for reference only.
        /// 
        /// The original approach cloned ragdoll prefabs at setup time, but this breaks internal
        /// references and causes NullReferenceException in Ragdoll.Setup() when creatures die.
        /// 
        /// The working solution is RagdollScalePatch, which scales the spawned ragdoll at runtime
        /// after death. This handles deathEffectScaleMultiplier correctly.
        /// </summary>
        public static void ScaleDeathEffectRagdolls(GameObject creaturePrefab, string creatureName, float scaleFactor)
        {
            // DEPRECATED: Runtime scaling via RagdollScalePatch is the working approach.
            // This method is no longer used but kept for reference.
        }

        public static GameObject FindSourcePrefab(string prefabName)
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
                var jotunnPrefab = PrefabManager.Instance.GetPrefab(prefabName);
                if (jotunnPrefab != null)
                    return jotunnPrefab;
            }
            catch { }

            return null;
        }

        /// <summary>
        /// Applies Phase 1 configurable parameters: AI controls, audio, visuals, and faction behaviors.
        /// Order: Character-level (faction) → AI settings → Audio → Visual.
        /// </summary>
        private static void ApplyPhase1Parameters(GameObject prefab, string prefabName, GeneratedPrefabConfig config)
        {
            // 1. Character-level settings (forceFaction is already handled earlier, skip here)

            // 2. AI settings (apply in order: aggro/flee/friend first, disableAI last)
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
                    if (prefab.GetComponent<CreaturePrefabCreator.Patches.PermanentAIDisabledMarker>() == null)
                        prefab.AddComponent<CreaturePrefabCreator.Patches.PermanentAIDisabledMarker>();
                }
            }
            else if (config.DisableAI || config.DisableAggro || config.DisableFleeing || config.FriendAttacked.HasValue)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[Phase1] '{prefabName}': has no MonsterAI component, AI settings not applied.");
            }

            // 3. Audio settings - disable idle sounds via reflection
            if (config.DisableIdleSounds)
            {
                DisableIdleSounds(prefab, prefabName);
            }

            // 4. Visual settings - tintColor
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

            // 4b. Visual settings - glowColor
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

        /// <summary>
        /// Applies health and damage multipliers to the prefab's Character and Humanoid components.
        /// All validation is performed here; bad/identity values are silently skipped.
        /// </summary>
        internal static void ApplyStatOverrides(GameObject prefab, string prefabName, float? healthMult, float? damageMult)
        {
            // --- Health block ---
            var character = prefab.GetComponent<Character>();
            if (character == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': no Character component, skipping health.");
            }
            else if (healthMult.HasValue)
            {
                float h = healthMult.Value;
                if (h == 0f || h == 1f)
                {
                    // no-op: zero treated as identity omission, 1.0 is identity
                }
                else if (!IsValidMultiplier(h))
                {
                    CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[StatOverride] '{prefabName}': healthMultiplier {h} is out of range [0.01, 100]; skipping.");
                }
                else
                {
                    if (h > 10f)
                        CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[StatOverride] '{prefabName}': healthMultiplier {h} is very large (>10); applying.");
                    float original = character.m_health;
                    character.m_health = original * h;
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': health {original} × {h} = {character.m_health}");
                }
            }

            // --- Damage block ---
            var humanoid = prefab.GetComponent<Humanoid>();
            if (humanoid == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': no Humanoid component, skipping damage.");
                return;
            }
            if (!damageMult.HasValue) return;
            float dm = damageMult.Value;
            if (dm == 0f || dm == 1f) return;
            if (!IsValidMultiplier(dm))
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[StatOverride] '{prefabName}': damageMultiplier {dm} is out of range [0.01, 100]; skipping.");
                return;
            }
            if (dm > 10f)
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[StatOverride] '{prefabName}': damageMultiplier {dm} is very large (>10); applying.");

            if (humanoid.m_defaultItems == null || humanoid.m_defaultItems.Length == 0)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': no defaultItems, skipping damage.");
                return;
            }

            for (int i = 0; i < humanoid.m_defaultItems.Length; i++)
            {
                GameObject item = humanoid.m_defaultItems[i];
                if (item == null) continue;

                // Clone the attack item GameObject to isolate damage overrides for this creature prefab.
                // The clone is parented under CreaturePrefabCreator_PrefabContainer (kept alive, inactive in hierarchy).
                // It is NOT registered with ObjectDB or Jötunn — m_defaultItems only needs a live GameObject reference.
                GameObject clonedItem = UnityEngine.Object.Instantiate(item);
                if (clonedItem == null)
                {
                    CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[StatOverride] '{prefabName}': Instantiate returned null for item '{item.name}'; skipping.");
                    continue;
                }
                clonedItem.name = $"{prefabName}_{item.name}_DamageOverride";
                clonedItem.transform.SetParent(GetOrCreatePrefabContainer().transform, false);
                humanoid.m_defaultItems[i] = clonedItem;

                var itemDrop = clonedItem.GetComponent<ItemDrop>();
                if (itemDrop == null)
                {
                    CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[StatOverride] '{prefabName}': cloned item '{clonedItem.name}' has no ItemDrop; skipping.");
                    continue;
                }
                if (itemDrop.m_itemData == null || itemDrop.m_itemData.m_shared == null)
                {
                    CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[StatOverride] '{prefabName}': cloned item '{clonedItem.name}' has null itemData or shared; skipping.");
                    continue;
                }

                var clonedShared = CloneSharedData(itemDrop.m_itemData.m_shared);
                if (clonedShared == null) continue; // already logged inside CloneSharedData

                float origDamage   = clonedShared.m_damages.m_damage;
                float origBlunt    = clonedShared.m_damages.m_blunt;
                float origSlash    = clonedShared.m_damages.m_slash;
                float origPierce   = clonedShared.m_damages.m_pierce;
                float origChop     = clonedShared.m_damages.m_chop;
                float origPickaxe  = clonedShared.m_damages.m_pickaxe;
                float origFire     = clonedShared.m_damages.m_fire;
                float origFrost    = clonedShared.m_damages.m_frost;
                float origLightning = clonedShared.m_damages.m_lightning;
                float origPoison   = clonedShared.m_damages.m_poison;
                float origSpirit   = clonedShared.m_damages.m_spirit;

                clonedShared.m_damages.m_damage    = origDamage    * dm;
                clonedShared.m_damages.m_blunt     = origBlunt     * dm;
                clonedShared.m_damages.m_slash     = origSlash     * dm;
                clonedShared.m_damages.m_pierce    = origPierce    * dm;
                clonedShared.m_damages.m_chop      = origChop      * dm;
                clonedShared.m_damages.m_pickaxe   = origPickaxe   * dm;
                clonedShared.m_damages.m_fire      = origFire      * dm;
                clonedShared.m_damages.m_frost     = origFrost     * dm;
                clonedShared.m_damages.m_lightning = origLightning * dm;
                clonedShared.m_damages.m_poison    = origPoison    * dm;
                clonedShared.m_damages.m_spirit    = origSpirit    * dm;

                itemDrop.m_itemData.m_shared = clonedShared;

                // Log each non-zero field
                void LogField(string field, float orig, float final)
                { if (orig != 0f) CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}' '{clonedItem.name}': {field} {orig} × {dm} = {final}"); }
                LogField("damage",    origDamage,    clonedShared.m_damages.m_damage);
                LogField("blunt",     origBlunt,     clonedShared.m_damages.m_blunt);
                LogField("slash",     origSlash,     clonedShared.m_damages.m_slash);
                LogField("pierce",    origPierce,    clonedShared.m_damages.m_pierce);
                LogField("chop",      origChop,      clonedShared.m_damages.m_chop);
                LogField("pickaxe",   origPickaxe,   clonedShared.m_damages.m_pickaxe);
                LogField("fire",      origFire,      clonedShared.m_damages.m_fire);
                LogField("frost",     origFrost,     clonedShared.m_damages.m_frost);
                LogField("lightning", origLightning, clonedShared.m_damages.m_lightning);
                LogField("poison",    origPoison,    clonedShared.m_damages.m_poison);
                LogField("spirit",    origSpirit,    clonedShared.m_damages.m_spirit);
            }
        }

        /// <summary>
        /// Applies health, damage, movement speed, AI, and death effect overrides using advanced modifier config.
        /// This overload resolves legacy and advanced fields using ModifierResolver and applies Tier 1 fields.
        /// </summary>
        internal static void ApplyStatOverrides(GameObject prefab, string prefabName, float? healthMult, float? damageMult,
            float? movementSpeedMult, AdvancedModifierConfig advanced, bool enableFactionOverrides)
        {
            // Log unsupported Tier 2/3 fields once per prefab
            if (advanced?.HasAnyValue == true)
            {
                ModifierValidation.LogUnsupportedFields($"GeneratedPrefab:{prefabName}", advanced);
            }

            // --- Health block with advanced support ---
            var character = prefab.GetComponent<Character>();
            if (character != null)
            {
                // Check for maxHealth first (absolute value)
                float? maxHealth = ModifierResolver.ResolveMaxHealth(advanced);
                if (maxHealth.HasValue)
                {
                    float mh = maxHealth.Value;
                    if (ModifierValidation.IsValidMultiplier(mh, out _))
                    {
                        float original = character.m_health;
                        character.m_health = original * mh;
                        CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': health {original} × {mh} (advanced.maxHealth) = {character.m_health}");
                    }
                }
                else
                {
                    // Fall back to resolved health multiplier
                    float? resolvedHealth = ModifierResolver.ResolveHealthMultiplier(advanced, healthMult);
                    if (resolvedHealth.HasValue)
                    {
                        float h = resolvedHealth.Value;
                        if (ModifierValidation.IsIdentityValue(h))
                        {
                            // no-op
                        }
                        else if (!ModifierValidation.IsValidMultiplier(h, out _))
                        {
                            CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[StatOverride] '{prefabName}': healthMultiplier {h} is out of range [0.01, 100]; skipping.");
                        }
                        else
                        {
                            if (h > 10f)
                                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[StatOverride] '{prefabName}': healthMultiplier {h} is very large (>10); applying.");
                            float original = character.m_health;
                            character.m_health = original * h;
                            CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': health {original} × {h} = {character.m_health}");
                        }
                    }
                }
            }
            else
            {
                CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': no Character component, skipping health.");
            }

            // --- Damage block with advanced per-type support ---
            var humanoid = prefab.GetComponent<Humanoid>();
            if (humanoid != null)
            {
                var damageMultipliers = ModifierResolver.ResolveDamageMultipliers(advanced, damageMult);
                if (damageMultipliers.HasAnyValue)
                {
                    ApplyAdvancedDamageMultipliers(prefab, prefabName, humanoid, damageMultipliers);
                }
            }
            else
            {
                CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': no Humanoid component, skipping damage.");
            }

            // --- Movement Speed block with advanced support ---
            if (character != null)
            {
                var speedMultipliers = ModifierResolver.ResolveMovementSpeedMultipliers(advanced, movementSpeedMult);
                if (speedMultipliers.HasAnyValue)
                {
                    ApplyAdvancedMovementSpeed(prefab, prefabName, character, speedMultipliers);
                }
            }

            // --- AI block with advanced support ---
            if (advanced?.AI != null)
            {
                // Note: legacy disableAI is handled in ApplyPhase1Parameters, but we check for advanced AI here
                var aiResult = ModifierResolver.ResolveAIModifiers(advanced, false, null);
                if (aiResult.HasAnyValue)
                {
                    ApplyAdvancedAI(prefab, prefabName, aiResult);
                }
            }

            // --- Death Effect block with advanced support ---
            // This is handled in the calling code alongside legacy death effect fields
        }

        /// <summary>
        /// Applies per-type damage multipliers to the humanoid's attack items.
        /// </summary>
        private static void ApplyAdvancedDamageMultipliers(GameObject prefab, string prefabName, Humanoid humanoid, DamageMultipliers multipliers)
        {
            if (humanoid.m_defaultItems == null || humanoid.m_defaultItems.Length == 0)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': no defaultItems, skipping advanced damage.");
                return;
            }

            for (int i = 0; i < humanoid.m_defaultItems.Length; i++)
            {
                GameObject item = humanoid.m_defaultItems[i];
                if (item == null) continue;

                // Clone the attack item if not already cloned
                GameObject clonedItem = UnityEngine.Object.Instantiate(item);
                if (clonedItem == null) continue;

                clonedItem.name = $"{prefabName}_{item.name}_AdvancedDamageOverride";
                clonedItem.transform.SetParent(GetOrCreatePrefabContainer().transform, false);
                humanoid.m_defaultItems[i] = clonedItem;

                var itemDrop = clonedItem.GetComponent<ItemDrop>();
                if (itemDrop?.m_itemData?.m_shared == null) continue;

                var clonedShared = CloneSharedData(itemDrop.m_itemData.m_shared);
                if (clonedShared == null) continue;

                // Store original values for logging
                float origDamage = clonedShared.m_damages.m_damage;
                float origBlunt = clonedShared.m_damages.m_blunt;
                float origSlash = clonedShared.m_damages.m_slash;
                float origPierce = clonedShared.m_damages.m_pierce;
                float origChop = clonedShared.m_damages.m_chop;
                float origPickaxe = clonedShared.m_damages.m_pickaxe;
                float origFire = clonedShared.m_damages.m_fire;
                float origFrost = clonedShared.m_damages.m_frost;
                float origLightning = clonedShared.m_damages.m_lightning;
                float origPoison = clonedShared.m_damages.m_poison;
                float origSpirit = clonedShared.m_damages.m_spirit;

                // Apply per-type multipliers
                clonedShared.m_damages.m_damage = origDamage * multipliers.Base;
                clonedShared.m_damages.m_blunt = origBlunt * multipliers.Blunt;
                clonedShared.m_damages.m_slash = origSlash * multipliers.Slash;
                clonedShared.m_damages.m_pierce = origPierce * multipliers.Pierce;
                clonedShared.m_damages.m_chop = origChop * multipliers.Chop;
                clonedShared.m_damages.m_pickaxe = origPickaxe * multipliers.Pickaxe;
                clonedShared.m_damages.m_fire = origFire * multipliers.Fire;
                clonedShared.m_damages.m_frost = origFrost * multipliers.Frost;
                clonedShared.m_damages.m_lightning = origLightning * multipliers.Lightning;
                clonedShared.m_damages.m_poison = origPoison * multipliers.Poison;
                clonedShared.m_damages.m_spirit = origSpirit * multipliers.Spirit;

                itemDrop.m_itemData.m_shared = clonedShared;

                // Log fields that were actually changed
                void LogFieldAdv(string field, float orig, float multiplier)
                {
                    if (orig != 0f && multiplier != 1f)
                        CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}' '{clonedItem.name}': {field} {orig} × {multiplier} = {orig * multiplier}");
                }
                LogFieldAdv("damage", origDamage, multipliers.Base);
                LogFieldAdv("blunt", origBlunt, multipliers.Blunt);
                LogFieldAdv("slash", origSlash, multipliers.Slash);
                LogFieldAdv("pierce", origPierce, multipliers.Pierce);
                LogFieldAdv("chop", origChop, multipliers.Chop);
                LogFieldAdv("pickaxe", origPickaxe, multipliers.Pickaxe);
                LogFieldAdv("fire", origFire, multipliers.Fire);
                LogFieldAdv("frost", origFrost, multipliers.Frost);
                LogFieldAdv("lightning", origLightning, multipliers.Lightning);
                LogFieldAdv("poison", origPoison, multipliers.Poison);
                LogFieldAdv("spirit", origSpirit, multipliers.Spirit);
            }
        }

        /// <summary>
        /// Applies advanced movement speed multipliers to Character speed fields.
        /// </summary>
        private static void ApplyAdvancedMovementSpeed(GameObject prefab, string prefabName, Character character, MovementSpeedMultipliers multipliers)
        {
            if (multipliers.Base != 1f)
            {
                float original = character.m_speed;
                character.m_speed = original * multipliers.Base;
                CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': speed (base) {original} × {multipliers.Base} = {character.m_speed}");
            }

            if (multipliers.Walk != 1f)
            {
                float original = character.m_walkSpeed;
                character.m_walkSpeed = original * multipliers.Walk;
                CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': walkSpeed {original} × {multipliers.Walk} = {character.m_walkSpeed}");
            }

            if (multipliers.Run != 1f)
            {
                float original = character.m_runSpeed;
                character.m_runSpeed = original * multipliers.Run;
                CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': runSpeed {original} × {multipliers.Run} = {character.m_runSpeed}");
            }

            if (multipliers.Swim != 1f)
            {
                float original = character.m_swimSpeed;
                character.m_swimSpeed = original * multipliers.Swim;
                CreaturePrefabCreatorPlugin.Instance?.Log($"[StatOverride] '{prefabName}': swimSpeed {original} × {multipliers.Swim} = {character.m_swimSpeed}");
            }
        }

        /// <summary>
        /// Applies advanced AI modifiers to MonsterAI component.
        /// </summary>
        private static void ApplyAdvancedAI(GameObject prefab, string prefabName, AIModifierResult aiResult)
        {
            var monsterAI = prefab.GetComponent<MonsterAI>();
            if (monsterAI == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[AI] '{prefabName}': no MonsterAI component, skipping advanced AI settings.");
                return;
            }

            var config = aiResult.MonsterAIConfig;
            if (config == null) return;

            // Apply Tier 1 MonsterAI fields
            if (config.Enabled.HasValue)
            {
                monsterAI.enabled = config.Enabled.Value;
                CreaturePrefabCreatorPlugin.Instance?.Log($"[AI] '{prefabName}': MonsterAI.enabled = {config.Enabled.Value}");

                // Add marker when disabling AI
                if (!config.Enabled.Value && prefab.GetComponent<PermanentAIDisabledMarker>() == null)
                {
                    prefab.AddComponent<PermanentAIDisabledMarker>();
                }
            }

            if (config.Aggravatable.HasValue)
            {
                monsterAI.m_aggravatable = config.Aggravatable.Value;
                CreaturePrefabCreatorPlugin.Instance?.Log($"[AI] '{prefabName}': MonsterAI.m_aggravatable = {config.Aggravatable.Value}");
            }

            // Use reflection for fields that may be version-sensitive
            if (config.FleeIfNotAlerted.HasValue)
            {
                SetMonsterAIField(monsterAI, "m_fleeIfNotAlerted", config.FleeIfNotAlerted.Value);
                CreaturePrefabCreatorPlugin.Instance?.Log($"[AI] '{prefabName}': MonsterAI.m_fleeIfNotAlerted = {config.FleeIfNotAlerted.Value}");
            }

            if (config.FleeInLava.HasValue)
            {
                SetMonsterAIField(monsterAI, "m_fleeInLava", config.FleeInLava.Value);
                CreaturePrefabCreatorPlugin.Instance?.Log($"[AI] '{prefabName}': MonsterAI.m_fleeInLava = {config.FleeInLava.Value}");
            }

            if (config.FleeRange.HasValue)
            {
                SetMonsterAIField(monsterAI, "m_fleeRange", config.FleeRange.Value);
                CreaturePrefabCreatorPlugin.Instance?.Log($"[AI] '{prefabName}': MonsterAI.m_fleeRange = {config.FleeRange.Value}");
            }

            if (config.FriendAttacked.HasValue)
            {
                SetMonsterAIField(monsterAI, "m_friendAttacked", config.FriendAttacked.Value);
                CreaturePrefabCreatorPlugin.Instance?.Log($"[AI] '{prefabName}': MonsterAI.m_friendAttacked = {config.FriendAttacked.Value}");
            }
        }

        /// <summary>
        /// Applies advanced death effect configuration to the prefab.
        /// Handles modes: vanilla, none, copyFrom (customPrefab is Tier 3/no-op).
        /// </summary>
        private static void ApplyAdvancedDeathEffects(GameObject prefab, string prefabName, DeathEffectResult deathEffect)
        {
            if (!deathEffect.HasAnyValue)
                return;

            // Validate mode
            if (!string.IsNullOrWhiteSpace(deathEffect.Mode))
            {
                var validModes = new[] { "vanilla", "none", "copyFrom", "customPrefab" };
                bool isValid = false;
                foreach (var valid in validModes)
                {
                    if (deathEffect.Mode.Equals(valid, System.StringComparison.OrdinalIgnoreCase))
                    {
                        isValid = true;
                        break;
                    }
                }
                if (!isValid)
                {
                    CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[DeathEffect] '{prefabName}': Invalid mode '{deathEffect.Mode}'. Valid values: vanilla, none, copyFrom, customPrefab");
                    return;
                }
            }

            var character = prefab.GetComponent<Character>();
            if (character == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[DeathEffect] '{prefabName}': no Character component, cannot apply death effects.");
                return;
            }

            // Handle mode "none" - clear death effects
            if (deathEffect.Mode?.Equals("none", System.StringComparison.OrdinalIgnoreCase) == true)
            {
                character.m_deathEffects = new EffectList();
                CreaturePrefabCreatorPlugin.Instance?.Log($"[DeathEffect] '{prefabName}': cleared death effects (mode=none).");
            }

            // Handle mode "copyFrom" - copy from another prefab
            else if (deathEffect.Mode?.Equals("copyFrom", System.StringComparison.OrdinalIgnoreCase) == true)
            {
                if (string.IsNullOrWhiteSpace(deathEffect.CopyFrom))
                {
                    CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[DeathEffect] '{prefabName}': mode=copyFrom but copyFrom is empty. Skipping.");
                    return;
                }

                GameObject sourcePrefab = FindSourcePrefab(deathEffect.CopyFrom);
                if (sourcePrefab == null)
                {
                    CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[DeathEffect] '{prefabName}': copyFrom source '{deathEffect.CopyFrom}' not found. Skipping.");
                    return;
                }

                var sourceChar = sourcePrefab.GetComponent<Character>();
                if (sourceChar == null)
                {
                    CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[DeathEffect] '{prefabName}': copyFrom source '{deathEffect.CopyFrom}' has no Character component. Skipping.");
                    return;
                }

                // Clear existing if requested
                if (deathEffect.ClearExisting.HasValue && deathEffect.ClearExisting.Value)
                {
                    character.m_deathEffects = new EffectList();
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[DeathEffect] '{prefabName}': cleared existing death effects before copy.");
                }

                // Copy effects from source
                var srcEffects = sourceChar.m_deathEffects?.m_effectPrefabs;
                character.m_deathEffects = new EffectList();
                character.m_deathEffects.m_effectPrefabs = srcEffects?.ToArray();
                int count = character.m_deathEffects.m_effectPrefabs?.Length ?? 0;
                CreaturePrefabCreatorPlugin.Instance?.Log($"[DeathEffect] '{prefabName}': copied {count} death effect(s) from '{deathEffect.CopyFrom}'.");
            }

            // Handle mode "customPrefab" - Tier 3 (no-op with warning)
            else if (deathEffect.Mode?.Equals("customPrefab", System.StringComparison.OrdinalIgnoreCase) == true)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[DeathEffect] '{prefabName}': mode=customPrefab is Tier 3 (not implemented). Using vanilla death effects.");
            }

            // Apply scale multiplier if provided
            if (deathEffect.ScaleMultiplier.HasValue)
            {
                RagdollScalePatch.RegisterDeathEffectScaleMultiplier(prefabName, deathEffect.ScaleMultiplier.Value);
                CreaturePrefabCreatorPlugin.Instance?.Log($"[DeathEffect] '{prefabName}': registered deathEffectScaleMultiplier={deathEffect.ScaleMultiplier.Value}");
            }
        }

        /// <summary>
        /// Shallow-clones SharedData via MemberwiseClone (reflection) to isolate m_damages mutations
        /// from other creatures that share the same attack item prefab. Reference fields are
        /// intentionally shared; only the m_damages struct will be mutated after this call.
        /// </summary>
        private static ItemDrop.ItemData.SharedData CloneSharedData(ItemDrop.ItemData.SharedData original)
        {
            if (original == null) return null;
            try
            {
                var method = typeof(object).GetMethod("MemberwiseClone",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                return (ItemDrop.ItemData.SharedData)method.Invoke(original, null);
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                    $"SharedData clone failed ({ex.Message}); skipping damage for this attack item.");
                return null;
            }
        }

        /// <summary>
        /// Applies a faction override to a creature prefab.
        /// Logs the original and new faction for verification.
        /// </summary>
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
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[Faction] '{prefabName}': invalid faction name '{factionName}'. Valid values: Players, ForestCreatures, PlainsMonsters, Dungeon, Demon, Other. Skipping.");
                return;
            }

            character.m_faction = newFaction.Value;
            CreaturePrefabCreatorPlugin.Instance?.Log($"[Faction] '{prefabName}': faction changed from {originalFaction} to {newFaction.Value}.");
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
