using CreaturePrefabCreator.Config;
using CreaturePrefabCreator.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CreaturePrefabCreator.RuntimeModifiers
{
    // ── Diagnostic data types ────────────────────────────────────────────────

    public sealed class RuntimeStatusSnapshot
    {
        public bool Enabled;
        public int TotalRules;
        public int ValidRules;
        public int InvalidRules;
        public int RuntimeAIDisabledCount;
        public int RuntimeAIEnabledCount;
        public List<string> RuleCachePrefabs;
        public bool MountUpDetected;
        public bool MountUpTypeResolved;
        public bool AllTameableDetected;
        public float EvaluationInterval;
        public float LastEvalTick;
        public bool DebugAIState;
        public bool DebugMountState;
    }

    public sealed class RuntimeRuleDebugInfo
    {
        public int Index;
        public string ConfigPrefab;
        public bool Enabled;
        public bool Valid;
        public string ValidationError;
        public bool? CondTamed;
        public bool? CondSaddled;
        public bool? CondRidden;
        public int? CondStarLevel;
        public float? EffectHealth;
        public float? EffectDamage;
        public float? EffectSpeed;
        public bool? EffectDisableAI;
        public bool? EffectEnableAI;
    }

    public sealed class RuntimeConditionTrace
    {
        public string Name;
        public bool? Expected;
        public bool Actual;
        public bool Pass;
        public string Source;
    }

    public sealed class RuntimeCheckResult
    {
        public string ObjectName;
        public string PrefabName;
        public string CharacterName;
        public bool ZNetViewValid;
        public string ZDOID;
        public bool IsOwner;
        public bool IsServer;
        public int TotalValidRules;
        public int MatchingRuleCount;
        public List<RuntimeRuleCheckDetail> RuleDetails;
        public bool RuntimeAIDisabledByCpc;
        public string DisableReason;
        public float DisabledAt;
        public bool RuntimeAIEnabledByCpc;
        public string EnableReason;
        public float EnabledAt;
        public bool OriginalBaseAIEnabled;
        public bool OriginalMonsterAIEnabled;
        public bool OriginalAnimalAIEnabled;
        public bool BaseAIExists;
        public bool BaseAIEnabled;
        public bool MonsterAIExists;
        public bool MonsterAIEnabled;
        public bool AnimalAIExists;
        public bool AnimalAIEnabled;
        public bool HasPermanentMarker;
    }

    public sealed class RuntimeRuleCheckDetail
    {
        public int RuleIndex;
        public bool ConditionsPass;
        public List<RuntimeConditionTrace> Conditions;
        public bool? EffectDisableAI;
        public bool? EffectEnableAI;
        public float? EffectHealth;
        public float? EffectDamage;
        public float? EffectSpeed;
    }

    public static class RuntimeModifierManager
    {
        private struct SpeedSnapshot
        {
            public float Speed;
            public float WalkSpeed;
            public float RunSpeed;
            public float SwimSpeed;
        }

        private class RuntimeAIState
        {
            public bool HadBaseAI { get; set; }
            public bool HadMonsterAI { get; set; }
            public bool HadAnimalAI { get; set; }
            public bool BaseAIWasEnabled { get; set; }
            public bool MonsterAIWasEnabled { get; set; }
            public bool AnimalAIWasEnabled { get; set; }
            public float DisabledAt { get; set; }
            public string Reason { get; set; }
            public string PrefabName { get; set; }
            public ZDOID OwnerZDOID { get; set; }
        }

        private static List<RuntimeModifierConfig> _rules = new List<RuntimeModifierConfig>();

        private static readonly Dictionary<ZDOID, float> _originalHealth = new Dictionary<ZDOID, float>();
        private static readonly Dictionary<ZDOID, SpeedSnapshot> _originalSpeeds = new Dictionary<ZDOID, SpeedSnapshot>();
        private static readonly Dictionary<ZDOID, float> _currentDamageMult = new Dictionary<ZDOID, float>();
        private static readonly Dictionary<ZDOID, RuntimeAIState> _runtimeAIDisabled = new Dictionary<ZDOID, RuntimeAIState>();
        private static readonly Dictionary<ZDOID, RuntimeAIState> _runtimeAIEnabled = new Dictionary<ZDOID, RuntimeAIState>();

        /// <summary>
        /// Checks if we have permission to modify this creature (network safety).
        /// Must be called before any runtime creature mutation.
        /// </summary>
        private static bool CanModifyCreature(Character character)
        {
            if (character == null) return false;
            var nview = character.GetComponent<ZNetView>();
            return nview != null && nview.IsValid() && nview.IsOwner();
        }

        // Track creatures we've already logged "Skipping" for to prevent log spam
        private static readonly HashSet<ZDOID> _skipLoggedCreatures = new HashSet<ZDOID>();
        private static float _lastSkipLogResetTime;
        private const float SkipLogResetInterval = 60f; // Reset every minute to allow re-logging after long periods

        /// <summary>
        /// Gets the network-safe key for a creature.
        /// Uses ZDOID for networked creatures, falls back to InstanceID for non-networked.
        /// </summary>
        private static ZDOID GetCreatureKey(Character character)
        {
            if (character == null) return new ZDOID(0, 0);
            var nview = character.GetComponent<ZNetView>();
            return nview?.GetZDO()?.m_uid ?? new ZDOID(0, (uint)character.GetInstanceID());
        }

        /// <summary>
        /// Restores AI state for a creature from stored state.
        /// </summary>
        private static void RestoreAIState(Character character, RuntimeAIState state)
        {
            if (character == null || state == null) return;

            try
            {
                // Restore BaseAI if it existed
                if (state.HadBaseAI)
                {
                    var baseAI = character.GetComponent<BaseAI>();
                    if (baseAI != null && baseAI.enabled != state.BaseAIWasEnabled)
                    {
                        baseAI.enabled = state.BaseAIWasEnabled;
                        if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                            CreaturePrefabCreatorPlugin.Instance.Log($"[RuntimeModifier] Restored BaseAI.enabled = {state.BaseAIWasEnabled} for '{character.name}'");
                    }
                }

                // Restore MonsterAI if it existed
                if (state.HadMonsterAI)
                {
                    var monsterAI = character.GetComponent<MonsterAI>();
                    if (monsterAI != null && monsterAI.enabled != state.MonsterAIWasEnabled)
                    {
                        monsterAI.enabled = state.MonsterAIWasEnabled;
                        if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                            CreaturePrefabCreatorPlugin.Instance.Log($"[RuntimeModifier] Restored MonsterAI.enabled = {state.MonsterAIWasEnabled} for '{character.name}'");
                    }
                }

                // Restore AnimalAI if it existed
                if (state.HadAnimalAI)
                {
                    var animalAI = character.GetComponent<AnimalAI>();
                    if (animalAI != null && animalAI.enabled != state.AnimalAIWasEnabled)
                    {
                        animalAI.enabled = state.AnimalAIWasEnabled;
                        if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                            CreaturePrefabCreatorPlugin.Instance.Log($"[RuntimeModifier] Restored AnimalAI.enabled = {state.AnimalAIWasEnabled} for '{character.name}'");
                    }
                }
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[RuntimeModifier] Failed to restore AI state for '{character?.name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Captures current AI state before disabling.
        /// </summary>
        private static RuntimeAIState CaptureAIState(Character character, string reason)
        {
            if (character == null) return null;

            var state = new RuntimeAIState
            {
                DisabledAt = Time.time,
                Reason = reason,
                PrefabName = character.gameObject.name,
                OwnerZDOID = GetCreatureKey(character)
            };

            var baseAI = character.GetComponent<BaseAI>();
            if (baseAI != null)
            {
                state.HadBaseAI = true;
                state.BaseAIWasEnabled = baseAI.enabled;
            }

            var monsterAI = character.GetComponent<MonsterAI>();
            if (monsterAI != null)
            {
                state.HadMonsterAI = true;
                state.MonsterAIWasEnabled = monsterAI.enabled;
            }

            var animalAI = character.GetComponent<AnimalAI>();
            if (animalAI != null)
            {
                state.HadAnimalAI = true;
                state.AnimalAIWasEnabled = animalAI.enabled;
            }

            return state;
        }

        public static void Initialize(List<RuntimeModifierConfig> rules)
        {
            _rules = rules ?? new List<RuntimeModifierConfig>();

            int valid = 0;
            foreach (var rule in _rules)
            {
                if (!rule.Enabled) continue;
                if (!rule.IsValid(out string err))
                {
                    CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[RuntimeModifier] Invalid rule for '{rule.TargetPrefab}': {err}. Skipping.");
                    RuntimeModifierEventBuffer.Record("RuleInvalid", rule.TargetPrefab, null, $"Validation error: {err}");
                    continue;
                }
                ValidateEffects(rule);
                RuntimeModifierEventBuffer.Record("RuleLoaded", rule.TargetPrefab, null, $"Rule #{valid} loaded");
                valid++;
            }

            CreaturePrefabCreatorPlugin.Instance?.Log($"[RuntimeModifier] Initialized with {valid} valid rule(s) (of {_rules.Count} total).");
        }

        private static void ValidateEffects(RuntimeModifierConfig rule)
        {
            var e = rule.Effects;
            if (e == null) return;
            ValidateMult(rule.TargetPrefab, "healthMultiplier", e.HealthMultiplier);
            ValidateMult(rule.TargetPrefab, "damageMultiplier", e.DamageMultiplier);
            ValidateMult(rule.TargetPrefab, "movementSpeedMultiplier", e.MovementSpeedMultiplier);

            var c = rule.Conditions;
            if (c?.StarLevel.HasValue == true && c.StarLevel.Value < 1)
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[RuntimeModifier] '{rule.TargetPrefab}': starLevel {c.StarLevel} is < 1 and will never match.");
        }

        private static void ValidateMult(string prefab, string field, float? value)
        {
            if (!value.HasValue || value.Value == 0f || value.Value == 1f) return;
            float v = value.Value;
            if (v < 0.01f || v > 100f)
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[RuntimeModifier] '{prefab}': {field} = {v} is out of range [0.01, 100]. It will be skipped at apply time.");
            else if (v > 10f)
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[RuntimeModifier] '{prefab}': {field} = {v} is very large (>10). Will apply.");
        }

        public static void EvaluateAndApply(Character character)
        {
            if (character == null || _rules.Count == 0) return;

            // CRITICAL: Check network ownership before any modifications
            if (!CanModifyCreature(character))
            {
                // Rate-limit "Skipping" logs to prevent spam - once per creature per minute
                float now = Time.time;
                if (now - _lastSkipLogResetTime > SkipLogResetInterval)
                {
                    _skipLoggedCreatures.Clear();
                    _lastSkipLogResetTime = now;
                }

                var skipKey = GetCreatureKey(character);
                if (!_skipLoggedCreatures.Contains(skipKey))
                {
                    _skipLoggedCreatures.Add(skipKey);
                    if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                        CreaturePrefabCreatorPlugin.Instance.Log($"[RuntimeModifier] Skipping '{character.name}' - not owner or no ZNetView (logging once per {SkipLogResetInterval}s)");
                    RuntimeModifierEventBuffer.Record("SkippedNotOwner", GetNormalizedName(character), null, character.name);
                }
                return;
            }

            string prefabName = character.gameObject.name;
            if (string.IsNullOrEmpty(prefabName)) return;

            // Strip "(Clone)" suffix that Unity appends to instantiated objects
            if (prefabName.EndsWith("(Clone)", StringComparison.Ordinal))
                prefabName = prefabName.Substring(0, prefabName.Length - 7).TrimEnd();

            var matchingRules = _rules.Where(r => r.Enabled &&
                string.Equals(r.TargetPrefab, prefabName, StringComparison.Ordinal) &&
                r.IsValid(out _)).ToList();

            if (matchingRules.Count == 0)
            {
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                {
                    foreach (var r in _rules)
                    {
                        bool valid = r.IsValid(out _);
                        CreaturePrefabCreatorPlugin.Instance.Log($"[RuntimeModifier] EvaluateAndApply for '{prefabName}': rule dump -> Enabled={r.Enabled}, TargetPrefab='{r.TargetPrefab}', IsValid={valid}");
                    }
                    CreaturePrefabCreatorPlugin.Instance.Log($"[RuntimeModifier] EvaluateAndApply for '{prefabName}': no matching rules (runtimeRules={_rules.Count})");
                }
                return;
            }

            ZDOID matchedKey = GetCreatureKey(character);
            RuntimeModifierEventBuffer.Record("MatchedRule", prefabName, matchedKey, $"{matchingRules.Count} rule(s) matched");

            if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                CreaturePrefabCreatorPlugin.Instance.Log($"[RuntimeModifier] EvaluateAndApply for '{prefabName}': {matchingRules.Count} matching rule(s)");

            float combinedHealth = 1f;
            float combinedDamage = 1f;
            float combinedSpeed = 1f;
            bool anyHealthRule = false;
            bool anyDamageRule = false;
            bool anySpeedRule = false;
            bool anyDisableAIRule = false;
            bool anyEnableAIRule = false;

            foreach (var rule in matchingRules)
            {
                bool conditionsPass = EvaluateConditions(character, rule.Conditions);
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                    CreaturePrefabCreatorPlugin.Instance.Log($"[RuntimeModifier] Rule '{rule.TargetPrefab}': conditionsPass={conditionsPass}");
                if (!conditionsPass)
                {
                    RuntimeModifierEventBuffer.Record("ConditionFailed", prefabName, GetCreatureKey(character), $"Rule '{rule.TargetPrefab}': conditions not met");
                    continue;
                }
                if (rule.Effects == null) continue;

                float h = SafeMult(rule.Effects.HealthMultiplier);
                float d = SafeMult(rule.Effects.DamageMultiplier);
                float s = SafeMult(rule.Effects.MovementSpeedMultiplier);

                if (h != 1f) { combinedHealth *= h; anyHealthRule = true; }
                if (d != 1f) { combinedDamage *= d; anyDamageRule = true; }
                if (s != 1f) { combinedSpeed *= s; anySpeedRule = true; }
                if (rule.Effects.DisableAI == true) { anyDisableAIRule = true; }
                if (rule.Effects.EnableAI == true) { anyEnableAIRule = true; }
            }

            ZDOID key = GetCreatureKey(character);

            RestoreHealth(character);
            RestoreSpeed(character);

            if (anyHealthRule)
                ApplyHealth(character, combinedHealth);

            if (anySpeedRule)
                ApplySpeed(character, combinedSpeed);

            if (anyDamageRule)
                _currentDamageMult[key] = combinedDamage;
            else
                _currentDamageMult.Remove(key);

            // ── Runtime AI disable logic ─────────────────────────────────────
            // disableAI effect is conditional: it ONLY applies when the creature
            // is NOT actively ridden. This lets saddled creatures remain rideable.
            // Creatures with PermanentAIDisabledMarker (prefab-level disableAI=true)
            // are never touched by runtime modifiers.
            bool hasPermanentMarker = character.GetComponent<PermanentAIDisabledMarker>() != null;
            bool currentlyDisabled = _runtimeAIDisabled.ContainsKey(key);

            if (!hasPermanentMarker)
            {
                // anyDisableAIRule is already false when ridden=false condition failed,
                // so no need to re-query IsActivelyRidden (avoids TOCTOU race).
                bool wantsDisableAI = anyDisableAIRule;

                if (wantsDisableAI && !currentlyDisabled)
                {
                    // Capture AI state before disabling
                    var state = CaptureAIState(character, "runtime rule: saddled=true, ridden=false");
                    if (state != null)
                    {
                        _runtimeAIDisabled[key] = state;
                        SaddledCreaturePatch.DisableAIComponents(character);
                        CreaturePrefabCreatorPlugin.Instance?.Log(
                            $"[RuntimeModifier] '{character.gameObject.name}' (key={key}): saddled=true, ridden=false -> disabling AI");
                        RuntimeModifierEventBuffer.Record("AIDisabled", prefabName, key, "saddled=true, ridden=false");
                    }
                }
                else if (!wantsDisableAI && currentlyDisabled)
                {
                    // Restore AI state before removing
                    if (_runtimeAIDisabled.TryGetValue(key, out var state))
                    {
                        RestoreAIState(character, state);
                        _runtimeAIDisabled.Remove(key);
                    }
                    else
                    {
                        // Fallback if state is missing
                        SaddledCreaturePatch.EnableAIComponents(character);
                    }
                    
                    string reason = anyDisableAIRule
                        ? "saddled=true, ridden=true -> restoring AI for mounted control"
                        : "saddled=false -> restoring AI";
                    CreaturePrefabCreatorPlugin.Instance?.Log(
                        $"[RuntimeModifier] '{character.gameObject.name}' (key={key}): {reason}");
                    RuntimeModifierEventBuffer.Record("AIRestored", prefabName, key, reason);
                }
            }
            else if (currentlyDisabled)
            {
                // Safety: clean up if a PermanentAIDisabledMarker was added after the fact
                if (_runtimeAIDisabled.TryGetValue(key, out var state))
                {
                    RestoreAIState(character, state);
                    _runtimeAIDisabled.Remove(key);
                }
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[RuntimeModifier] '{character.gameObject.name}' (key={key}): skipped because PermanentAIDisabledMarker is present");
            }

            // ── Runtime AI enable logic ──────────────────────────────────────
            // enableAI effect is for creatures with PermanentAIDisabledMarker only.
            // It temporarily enables AI when conditions are met (e.g., ridden=true).
            // When conditions fail, AI is restored to disabled state.
            // Conflict rule: disableAI wins over enableAI (safer behavior).
            bool currentlyEnabled = _runtimeAIEnabled.ContainsKey(key);

            if (hasPermanentMarker && anyEnableAIRule && !anyDisableAIRule)
            {
                bool wantsEnableAI = anyEnableAIRule;

                if (wantsEnableAI && !currentlyEnabled)
                {
                    // Capture AI state before enabling (should be disabled)
                    var state = CaptureAIState(character, "runtime rule: enableAI condition met");
                    if (state != null)
                    {
                        _runtimeAIEnabled[key] = state;
                        SaddledCreaturePatch.EnableAIComponents(character);
                        CreaturePrefabCreatorPlugin.Instance?.Log(
                            $"[RuntimeModifier] '{character.gameObject.name}' (key={key}): enableAI condition met -> enabling AI (creature has PermanentAIDisabledMarker)");
                        RuntimeModifierEventBuffer.Record("AIEnabled", prefabName, key, "enableAI condition met, creature had PermanentAIDisabledMarker");
                    }
                }
                else if (!wantsEnableAI && currentlyEnabled)
                {
                    // Restore AI to disabled state
                    if (_runtimeAIEnabled.TryGetValue(key, out var state))
                    {
                        RestoreAIState(character, state);
                        _runtimeAIEnabled.Remove(key);
                    }
                    else
                    {
                        // Fallback if state is missing - disable AI to be safe
                        SaddledCreaturePatch.DisableAIComponents(character);
                    }

                    CreaturePrefabCreatorPlugin.Instance?.Log(
                        $"[RuntimeModifier] '{character.gameObject.name}' (key={key}): enableAI condition no longer met -> restoring AI to disabled");
                    RuntimeModifierEventBuffer.Record("AIDisabled", prefabName, key, "enableAI condition ended, restored to disabled");
                }
            }
            else if (currentlyEnabled && (!hasPermanentMarker || anyDisableAIRule))
            {
                // Safety: clean up if marker removed or disableAI takes precedence
                if (_runtimeAIEnabled.TryGetValue(key, out var state))
                {
                    RestoreAIState(character, state);
                    _runtimeAIEnabled.Remove(key);
                }
                string reason = !hasPermanentMarker ? "PermanentAIDisabledMarker removed" : "disableAI takes precedence over enableAI";
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[RuntimeModifier] '{character.gameObject.name}' (key={key}): enableAI cleanup -> {reason}");
            }
        }

        private static float SafeMult(float? value)
        {
            if (!value.HasValue || value.Value == 0f || value.Value == 1f) return 1f;
            float v = value.Value;
            if (v < 0.01f || v > 100f) return 1f;
            return v;
        }

        public static bool EvaluateConditions(Character character, RuntimeModifierConditionConfig cond)
        {
            if (cond == null || cond.IsEmpty) return true;

            if (cond.StarLevel.HasValue)
            {
                if (character.GetLevel() != cond.StarLevel.Value) return false;
            }

            if (cond.Tamed.HasValue)
            {
                if (character.IsTamed() != cond.Tamed.Value) return false;
            }

            if (cond.Saddled.HasValue)
            {
                bool isSaddled = IsSaddled(character);
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                    CreaturePrefabCreatorPlugin.Instance.Log($"[RuntimeModifier]   saddled condition: isSaddled={isSaddled}, expected={cond.Saddled.Value}");
                if (isSaddled != cond.Saddled.Value) return false;
            }

            if (cond.Ridden.HasValue)
            {
                bool isRidden = IsRidden(character);
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                    CreaturePrefabCreatorPlugin.Instance.Log($"[RuntimeModifier]   ridden condition: isRidden={isRidden}, expected={cond.Ridden.Value}");
                if (isRidden != cond.Ridden.Value) return false;
            }

            return true;
        }

        /// <summary>
        /// Delegates to SaddledCreaturePatch.IsSaddledViaCanonicalPath which uses pre-initialized
        /// cached reflection. SaddledCreaturePatch.Initialize() must have been called first
        /// (guaranteed when EnableRuntimeModifiers=true per CreaturePrefabCreatorPlugin.Awake).
        /// </summary>
        private static bool IsSaddled(Character character)
            => SaddledCreaturePatch.IsSaddledViaCanonicalPath(character);

        /// <summary>
        /// Delegates to SaddledCreaturePatch.IsActivelyRidden which uses pre-initialized
        /// cached reflection.
        /// </summary>
        private static bool IsRidden(Character character)
            => SaddledCreaturePatch.IsActivelyRidden(character);

        private static void ApplyHealth(Character character, float mult)
        {
            ZDOID key = GetCreatureKey(character);

            if (!_originalHealth.ContainsKey(key))
                _originalHealth[key] = character.m_health;

            float original = _originalHealth[key];
            float newHealth = original * mult;
            character.m_health = newHealth;

            if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                CreaturePrefabCreatorPlugin.Instance.Log(
                    $"[RuntimeModifier] '{character.gameObject.name}' (key={key}): health {original} × {mult} = {newHealth}");
        }

        private static void RestoreHealth(Character character)
        {
            ZDOID key = GetCreatureKey(character);
            if (_originalHealth.TryGetValue(key, out float original))
                character.m_health = original;
        }

        private static void ApplySpeed(Character character, float mult)
        {
            ZDOID key = GetCreatureKey(character);

            if (!_originalSpeeds.ContainsKey(key))
            {
                _originalSpeeds[key] = new SpeedSnapshot
                {
                    Speed = character.m_speed,
                    WalkSpeed = character.m_walkSpeed,
                    RunSpeed = character.m_runSpeed,
                    SwimSpeed = character.m_swimSpeed
                };
            }

            var snap = _originalSpeeds[key];
            character.m_speed = snap.Speed * mult;
            character.m_walkSpeed = snap.WalkSpeed * mult;
            character.m_runSpeed = snap.RunSpeed * mult;
            character.m_swimSpeed = snap.SwimSpeed * mult;

            if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                CreaturePrefabCreatorPlugin.Instance.Log(
                    $"[RuntimeModifier] '{character.gameObject.name}' (key={key}): speed ×{mult} " +
                    $"(walk {snap.WalkSpeed}→{character.m_walkSpeed}, run {snap.RunSpeed}→{character.m_runSpeed})");
        }

        private static void RestoreSpeed(Character character)
        {
            ZDOID key = GetCreatureKey(character);
            if (_originalSpeeds.TryGetValue(key, out SpeedSnapshot snap))
            {
                character.m_speed = snap.Speed;
                character.m_walkSpeed = snap.WalkSpeed;
                character.m_runSpeed = snap.RunSpeed;
                character.m_swimSpeed = snap.SwimSpeed;
            }
        }

        /// <summary>Total number of rules currently loaded (enabled + disabled).</summary>
        public static int RuleCount => _rules.Count;

        /// <summary>Number of enabled, valid rules that match the given normalized prefab name.</summary>
        public static int GetMatchingRuleCount(string normalizedPrefabName)
        {
            if (string.IsNullOrEmpty(normalizedPrefabName)) return 0;
            int count = 0;
            foreach (var r in _rules)
            {
                if (r.Enabled && string.Equals(r.TargetPrefab, normalizedPrefabName, StringComparison.Ordinal) && r.IsValid(out _))
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Returns true if CPC currently has a runtime AI-disabled entry for this creature.
        /// Outputs the reason string recorded when the state was captured.
        /// </summary>
        public static bool IsRuntimeAIDisabled(Character character, out string reason)
        {
            reason = null;
            if (character == null) return false;
            ZDOID key = GetCreatureKey(character);
            if (_runtimeAIDisabled.TryGetValue(key, out var state))
            {
                reason = state.Reason;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Returns true if CPC currently has a runtime AI-enabled entry for this creature
        /// (meaning AI was temporarily enabled via enableAI effect).
        /// Outputs the reason string recorded when the state was captured.
        /// </summary>
        public static bool IsRuntimeAIEnabled(Character character, out string reason)
        {
            reason = null;
            if (character == null) return false;
            ZDOID key = GetCreatureKey(character);
            if (_runtimeAIEnabled.TryGetValue(key, out var state))
            {
                reason = state.Reason;
                return true;
            }
            return false;
        }

        public static float GetOutgoingDamageMultiplier(Character attacker)
        {
            if (attacker == null) return 1f;
            ZDOID key = GetCreatureKey(attacker);
            return _currentDamageMult.TryGetValue(key, out float mult) ? mult : 1f;
        }

        public static void Cleanup(Character character)
        {
            if (character == null) return;
            ZDOID key = GetCreatureKey(character);
            
            // Restore AI state before cleanup (disableAI tracking)
            if (_runtimeAIDisabled.TryGetValue(key, out var disabledState))
            {
                RestoreAIState(character, disabledState);
                _runtimeAIDisabled.Remove(key);
                RuntimeModifierEventBuffer.Record("AIRestored", disabledState.PrefabName, key, "creature destroyed/cleanup");
            }

            // Restore AI state before cleanup (enableAI tracking)
            if (_runtimeAIEnabled.TryGetValue(key, out var enabledState))
            {
                RestoreAIState(character, enabledState);
                _runtimeAIEnabled.Remove(key);
                RuntimeModifierEventBuffer.Record("AIDisabled", enabledState.PrefabName, key, "creature destroyed/cleanup - enableAI state restored to disabled");
            }
            
            _originalHealth.Remove(key);
            _originalSpeeds.Remove(key);
            _currentDamageMult.Remove(key);
        }

        public static void ClearAll()
        {
            // CRITICAL: Restore all AI states before clearing.
            // Build a single ZDOID->Character map to avoid O(n*m) FindObjectsByType inside loop.
            var allCharacters = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            var zdoidToChar = new Dictionary<ZDOID, Character>(allCharacters.Length);
            foreach (var ch in allCharacters)
            {
                if (ch == null) continue;
                var nview = ch.GetComponent<ZNetView>();
                if (nview != null && nview.GetZDO() != null)
                    zdoidToChar[nview.GetZDO().m_uid] = ch;
            }

            // Restore disableAI states
            if (_runtimeAIDisabled.Count > 0)
            {
                int restored = 0;
                foreach (var kvp in _runtimeAIDisabled)
                {
                    if (zdoidToChar.TryGetValue(kvp.Key, out var character))
                    {
                        RestoreAIState(character, kvp.Value);
                        restored++;
                    }
                }

                if (restored > 0)
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[RuntimeModifier] Cleanup restored {restored} runtime AI-disabled state(s).");
            }

            // Restore enableAI states (restore to disabled)
            if (_runtimeAIEnabled.Count > 0)
            {
                int restored = 0;
                foreach (var kvp in _runtimeAIEnabled)
                {
                    if (zdoidToChar.TryGetValue(kvp.Key, out var character))
                    {
                        RestoreAIState(character, kvp.Value);
                        restored++;
                    }
                }

                if (restored > 0)
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[RuntimeModifier] Cleanup restored {restored} runtime AI-enabled state(s) to disabled.");
            }

            RuntimeModifierEventBuffer.Record("ConfigReloaded", null, null, "ClearAll called — all runtime states cleared");
            
            _originalHealth.Clear();
            _originalSpeeds.Clear();
            _currentDamageMult.Clear();
            _runtimeAIDisabled.Clear();
            _runtimeAIEnabled.Clear();
            _rules.Clear();
            
            // Clear skip log tracking to prevent stale entries
            _skipLoggedCreatures.Clear();
            _lastSkipLogResetTime = 0f;
        }

        // ── Diagnostic helpers ───────────────────────────────────────────────

        private static string GetNormalizedName(Character character)
        {
            if (character == null) return string.Empty;
            string n = character.gameObject.name;
            if (n.EndsWith("(Clone)", StringComparison.Ordinal))
                n = n.Substring(0, n.Length - 7).TrimEnd();
            return n;
        }

        // ── Public diagnostic API (used by debug commands) ───────────────────

        /// <summary>Number of creatures currently tracked as runtime-AI-disabled by CPC.</summary>
        public static int RuntimeAIDisabledCount => _runtimeAIDisabled.Count;

        public static RuntimeStatusSnapshot GetRuntimeStatus()
        {
            var plugin = CreaturePrefabCreatorPlugin.Instance;
            int valid = 0, invalid = 0;
            var prefabs = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in _rules)
            {
                if (!r.Enabled) continue;
                if (r.IsValid(out _)) { valid++; prefabs.Add(r.TargetPrefab); }
                else invalid++;
            }

            return new RuntimeStatusSnapshot
            {
                Enabled = plugin?.ConfigEnableRuntimeModifiers?.Value ?? false,
                TotalRules = _rules.Count,
                ValidRules = valid,
                InvalidRules = invalid,
                RuntimeAIDisabledCount = _runtimeAIDisabled.Count,
                RuntimeAIEnabledCount = _runtimeAIEnabled.Count,
                RuleCachePrefabs = new List<string>(prefabs),
                MountUpDetected = SaddledCreaturePatch.MountUpDetected,
                MountUpTypeResolved = SaddledCreaturePatch.MountUpTypeResolved,
                AllTameableDetected = SaddledCreaturePatch.AllTameableDetected,
                EvaluationInterval = 5f,
                LastEvalTick = Time.time,
                DebugAIState = plugin?.ConfigDebugAIState?.Value ?? false,
                DebugMountState = plugin?.ConfigDebugMountState?.Value ?? false
            };
        }

        public static IReadOnlyList<RuntimeRuleDebugInfo> GetLoadedRulesDebug()
        {
            var list = new List<RuntimeRuleDebugInfo>(_rules.Count);
            for (int i = 0; i < _rules.Count; i++)
            {
                var r = _rules[i];
                bool valid = r.IsValid(out string err);
                var info = new RuntimeRuleDebugInfo
                {
                    Index = i,
                    ConfigPrefab = r.TargetPrefab,
                    Enabled = r.Enabled,
                    Valid = valid,
                    ValidationError = err
                };
                if (r.Conditions != null)
                {
                    info.CondTamed = r.Conditions.Tamed;
                    info.CondSaddled = r.Conditions.Saddled;
                    info.CondRidden = r.Conditions.Ridden;
                    info.CondStarLevel = r.Conditions.StarLevel;
                }
                if (r.Effects != null)
                {
                    info.EffectHealth = r.Effects.HealthMultiplier;
                    info.EffectDamage = r.Effects.DamageMultiplier;
                    info.EffectSpeed = r.Effects.MovementSpeedMultiplier;
                    info.EffectDisableAI = r.Effects.DisableAI;
                    info.EffectEnableAI = r.Effects.EnableAI;
                }
                list.Add(info);
            }
            return list;
        }

        public static RuntimeCheckResult GetRuntimeCheckDebug(Character character)
        {
            if (character == null) return null;

            var nview = character.GetComponent<ZNetView>();
            bool znvValid = nview != null && nview.IsValid();
            bool isOwner = znvValid && nview.IsOwner();
            ZDOID zdoid = znvValid ? nview.GetZDO().m_uid : new ZDOID(0, 0);

            string prefabName = GetNormalizedName(character);

            int totalValid = 0;
            foreach (var r in _rules)
                if (r.Enabled && r.IsValid(out _)) totalValid++;

            var matchingRules = _rules.Where(r => r.Enabled &&
                string.Equals(r.TargetPrefab, prefabName, StringComparison.Ordinal) &&
                r.IsValid(out _)).ToList();

            var ruleDetails = new List<RuntimeRuleCheckDetail>();
            foreach (var rule in matchingRules)
            {
                var detail = new RuntimeRuleCheckDetail
                {
                    RuleIndex = _rules.IndexOf(rule),
                    Conditions = new List<RuntimeConditionTrace>(),
                    EffectDisableAI = rule.Effects?.DisableAI,
                    EffectEnableAI = rule.Effects?.EnableAI,
                    EffectHealth = rule.Effects?.HealthMultiplier,
                    EffectDamage = rule.Effects?.DamageMultiplier,
                    EffectSpeed = rule.Effects?.MovementSpeedMultiplier
                };

                bool pass = true;
                var c = rule.Conditions;
                if (c != null && !c.IsEmpty)
                {
                    if (c.Tamed.HasValue)
                    {
                        bool actual = character.IsTamed();
                        bool p = actual == c.Tamed.Value;
                        if (!p) pass = false;
                        detail.Conditions.Add(new RuntimeConditionTrace { Name = "tamed", Expected = c.Tamed, Actual = actual, Pass = p, Source = "Character.IsTamed()" });
                    }
                    if (c.Saddled.HasValue)
                    {
                        bool actual = SaddledCreaturePatch.IsSaddledViaCanonicalPath(character);
                        bool p = actual == c.Saddled.Value;
                        if (!p) pass = false;
                        detail.Conditions.Add(new RuntimeConditionTrace { Name = "saddled", Expected = c.Saddled, Actual = actual, Pass = p, Source = "Tameable.m_saddle+HaveSaddle / Sadle component / inventory" });
                    }
                    if (c.Ridden.HasValue)
                    {
                        bool actual = SaddledCreaturePatch.IsActivelyRidden(character);
                        bool p = actual == c.Ridden.Value;
                        if (!p) pass = false;
                        detail.Conditions.Add(new RuntimeConditionTrace { Name = "ridden", Expected = c.Ridden, Actual = actual, Pass = p, Source = "Tameable.HaveRider / Sadle.HaveValidUser" });
                    }
                    if (c.StarLevel.HasValue)
                    {
                        bool actual = character.GetLevel() == c.StarLevel.Value;
                        if (!actual) pass = false;
                        detail.Conditions.Add(new RuntimeConditionTrace { Name = "starLevel", Expected = null, Actual = actual, Pass = actual, Source = $"Character.GetLevel()={character.GetLevel()} expected={c.StarLevel.Value}" });
                    }
                }
                detail.ConditionsPass = pass;
                ruleDetails.Add(detail);
            }

            bool cpcDisabled = _runtimeAIDisabled.TryGetValue(zdoid, out var aiDisabledState);
            bool cpcEnabled = _runtimeAIEnabled.TryGetValue(zdoid, out var aiEnabledState);
            var baseAI = character.GetComponent<BaseAI>();
            var monsterAI = character.GetComponent<MonsterAI>();
            var animalAI = character.GetComponent<AnimalAI>();

            return new RuntimeCheckResult
            {
                ObjectName = character.gameObject.name,
                PrefabName = prefabName,
                CharacterName = character.m_name,
                ZNetViewValid = znvValid,
                ZDOID = zdoid.ToString(),
                IsOwner = isOwner,
                IsServer = ZNet.instance != null && ZNet.instance.IsServer(),
                TotalValidRules = totalValid,
                MatchingRuleCount = matchingRules.Count,
                RuleDetails = ruleDetails,
                RuntimeAIDisabledByCpc = cpcDisabled,
                DisableReason = aiDisabledState?.Reason,
                DisabledAt = aiDisabledState?.DisabledAt ?? 0f,
                RuntimeAIEnabledByCpc = cpcEnabled,
                EnableReason = aiEnabledState?.Reason,
                EnabledAt = aiEnabledState?.DisabledAt ?? 0f,
                OriginalBaseAIEnabled = (aiDisabledState?.BaseAIWasEnabled ?? false) || (aiEnabledState?.BaseAIWasEnabled ?? false),
                OriginalMonsterAIEnabled = (aiDisabledState?.MonsterAIWasEnabled ?? false) || (aiEnabledState?.MonsterAIWasEnabled ?? false),
                OriginalAnimalAIEnabled = (aiDisabledState?.AnimalAIWasEnabled ?? false) || (aiEnabledState?.AnimalAIWasEnabled ?? false),
                BaseAIExists = baseAI != null,
                BaseAIEnabled = baseAI?.enabled ?? false,
                MonsterAIExists = monsterAI != null,
                MonsterAIEnabled = monsterAI?.enabled ?? false,
                AnimalAIExists = animalAI != null,
                AnimalAIEnabled = animalAI?.enabled ?? false,
                HasPermanentMarker = character.GetComponent<PermanentAIDisabledMarker>() != null
            };
        }

        public static (int restored, int skippedNotOwner, int stale) RestoreRuntimeAI(Character character)
        {
            if (character == null) return (0, 0, 0);
            ZDOID key = GetCreatureKey(character);
            if (!_runtimeAIDisabled.TryGetValue(key, out var state)) return (0, 0, 0);

            if (!CanModifyCreature(character)) return (0, 1, 0);

            RestoreAIState(character, state);
            _runtimeAIDisabled.Remove(key);
            RuntimeModifierEventBuffer.Record("AIRestored", state.PrefabName, key, "manual restore via command");
            return (1, 0, 0);
        }

        public static (int restored, int skippedNotOwner, int stale) RestoreAllRuntimeAI()
        {
            if (_runtimeAIDisabled.Count == 0) return (0, 0, 0);

            var allCharacters = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            var zdoidToChar = new Dictionary<ZDOID, Character>(allCharacters.Length);
            foreach (var ch in allCharacters)
            {
                if (ch == null) continue;
                var nv = ch.GetComponent<ZNetView>();
                if (nv != null && nv.GetZDO() != null)
                    zdoidToChar[nv.GetZDO().m_uid] = ch;
            }

            int restored = 0, skipped = 0, stale = 0;
            var toRemove = new List<ZDOID>();

            foreach (var kvp in _runtimeAIDisabled)
            {
                if (!zdoidToChar.TryGetValue(kvp.Key, out var ch))
                {
                    stale++;
                    toRemove.Add(kvp.Key);
                    continue;
                }
                if (!CanModifyCreature(ch)) { skipped++; continue; }
                RestoreAIState(ch, kvp.Value);
                RuntimeModifierEventBuffer.Record("AIRestored", kvp.Value.PrefabName, kvp.Key, "RestoreAllRuntimeAI command");
                restored++;
                toRemove.Add(kvp.Key);
            }

            foreach (var k in toRemove) _runtimeAIDisabled.Remove(k);
            return (restored, skipped, stale);
        }

        public static (int evaluated, int matched, int disabled, int restored, int skippedNotOwner, int errors) ForceEvaluateAll()
        {
            int eval = 0, matched = 0, disabled = 0, restored = 0, skipped = 0, errors = 0;
            int beforeDisabled = _runtimeAIDisabled.Count;
            var allCharacters = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            foreach (var ch in allCharacters)
            {
                if (ch == null) continue;
                var nv = ch.GetComponent<ZNetView>();
                if (nv == null || !nv.IsValid() || !nv.IsOwner()) { skipped++; continue; }
                try
                {
                    int beforeCount = _runtimeAIDisabled.Count;
                    EvaluateAndApply(ch);
                    eval++;
                    string pn = GetNormalizedName(ch);
                    if (GetMatchingRuleCount(pn) > 0) matched++;
                    int afterCount = _runtimeAIDisabled.Count;
                    if (afterCount > beforeCount) disabled++;
                    else if (afterCount < beforeCount) restored++;
                }
                catch (Exception ex)
                {
                    errors++;
                    RuntimeModifierEventBuffer.Record("ExceptionCaught", ch.name, null, ex.Message);
                }
            }
            return (eval, matched, disabled, restored, skipped, errors);
        }

        public static (int evaluated, int matched, int disabled, int restored, int errors) ForceEvaluateSingle(Character character)
        {
            if (character == null) return (0, 0, 0, 0, 0);
            try
            {
                int before = _runtimeAIDisabled.Count;
                EvaluateAndApply(character);
                int after = _runtimeAIDisabled.Count;
                string pn = GetNormalizedName(character);
                int m = GetMatchingRuleCount(pn) > 0 ? 1 : 0;
                int dis = after > before ? 1 : 0;
                int res = after < before ? 1 : 0;
                return (1, m, dis, res, 0);
            }
            catch (Exception ex)
            {
                RuntimeModifierEventBuffer.Record("ExceptionCaught", character.name, null, ex.Message);
                return (1, 0, 0, 0, 1);
            }
        }

        public static (int repaired, int removedStale, int dryRunCount) RepairStaleEntries(bool dryRun)
        {
            var allCharacters = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            var liveZdoids = new HashSet<ZDOID>();
            var zdoidToChar = new Dictionary<ZDOID, Character>();
            foreach (var ch in allCharacters)
            {
                if (ch == null) continue;
                var nv = ch.GetComponent<ZNetView>();
                if (nv != null && nv.GetZDO() != null)
                {
                    liveZdoids.Add(nv.GetZDO().m_uid);
                    zdoidToChar[nv.GetZDO().m_uid] = ch;
                }
            }

            int repaired = 0, stale = 0, dryCount = 0;
            var toRemove = new List<ZDOID>();

            foreach (var kvp in _runtimeAIDisabled)
            {
                if (!liveZdoids.Contains(kvp.Key))
                {
                    stale++;
                    if (!dryRun) toRemove.Add(kvp.Key);
                    else dryCount++;
                    continue;
                }
                if (!zdoidToChar.TryGetValue(kvp.Key, out var ch)) continue;
                if (!CanModifyCreature(ch)) continue;
                if (!dryRun)
                {
                    RestoreAIState(ch, kvp.Value);
                    RuntimeModifierEventBuffer.Record("AIRestored", kvp.Value.PrefabName, kvp.Key, "RepairStaleEntries");
                    toRemove.Add(kvp.Key);
                    repaired++;
                }
                else dryCount++;
            }

            if (!dryRun)
                foreach (var k in toRemove) _runtimeAIDisabled.Remove(k);

            return (repaired, stale, dryCount);
        }
    }
}
