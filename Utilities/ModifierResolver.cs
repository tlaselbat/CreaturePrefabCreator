using CreaturePrefabCreator.Config.Advanced;
using UnityEngine;

namespace CreaturePrefabCreator.Utilities
{
    /// <summary>
    /// Resolved damage multipliers for all damage types.
    /// </summary>
    public struct DamageMultipliers
    {
        public float Base;
        public float Blunt;
        public float Slash;
        public float Pierce;
        public float Chop;
        public float Pickaxe;
        public float Fire;
        public float Frost;
        public float Lightning;
        public float Poison;
        public float Spirit;

        public bool HasAnyValue =>
            Base != 1f || Blunt != 1f || Slash != 1f || Pierce != 1f ||
            Chop != 1f || Pickaxe != 1f || Fire != 1f || Frost != 1f ||
            Lightning != 1f || Poison != 1f || Spirit != 1f;
    }

    /// <summary>
    /// Resolved movement speed multipliers.
    /// </summary>
    public struct MovementSpeedMultipliers
    {
        public float Base;
        public float Walk;
        public float Run;
        public float Swim;

        public bool HasAnyValue => Base != 1f || Walk != 1f || Run != 1f || Swim != 1f;
    }

    /// <summary>
    /// Resolved AI modifier result.
    /// </summary>
    public struct AIModifierResult
    {
        public bool? DisableMonsterAI;
        public bool? EnableMonsterAI;
        public MonsterAIModifierConfig MonsterAIConfig;

        public bool HasAnyValue => DisableMonsterAI.HasValue || EnableMonsterAI.HasValue || MonsterAIConfig != null;
    }

    /// <summary>
    /// Resolved death effect result.
    /// </summary>
    public struct DeathEffectResult
    {
        public string Mode; // vanilla, none, copyFrom
        public string CopyFrom;
        public bool? ClearExisting;
        public float? ScaleMultiplier;

        public bool HasAnyValue => Mode != null || CopyFrom != null || ClearExisting.HasValue || ScaleMultiplier.HasValue;
    }

    /// <summary>
    /// Centralized resolver for legacy and advanced modifier field priority rules.
    /// </summary>
    public static class ModifierResolver
    {
        /// <summary>
        /// Resolves health multiplier with priority: maxHealth > health.multiplier > legacy healthMultiplier
        /// </summary>
        public static float? ResolveHealthMultiplier(AdvancedModifierConfig advanced, float? legacyMultiplier)
        {
            // If advanced block is null, fall back to legacy
            if (advanced?.Health == null)
                return legacyMultiplier;

            var health = advanced.Health;

            // Priority: maxHealth overrides multiplier (maxHealth is an absolute value, not a multiplier)
            // We return maxHealth as a special marker via the resolver context
            // The caller should check for maxHealth first

            // advanced.health.multiplier overrides legacy healthMultiplier
            if (health.Multiplier.HasValue)
                return health.Multiplier.Value;

            // Legacy remains fallback
            return legacyMultiplier;
        }

        /// <summary>
        /// Resolves max health value with priority: advanced.maxHealth > null
        /// </summary>
        public static float? ResolveMaxHealth(AdvancedModifierConfig advanced)
        {
            return advanced?.Health?.MaxHealth;
        }

        /// <summary>
        /// Resolves damage multipliers with priority:
        /// specific type > damage.multiplier (broad) > legacy damageMultiplier
        /// </summary>
        public static DamageMultipliers ResolveDamageMultipliers(AdvancedModifierConfig advanced, float? legacyMultiplier)
        {
            float broadMultiplier = 1f;

            // Determine broad multiplier: advanced.damage.multiplier > legacy damageMultiplier
            if (advanced?.Damage?.Multiplier.HasValue ?? false)
                broadMultiplier = advanced.Damage.Multiplier.Value;
            else if (legacyMultiplier.HasValue)
                broadMultiplier = legacyMultiplier.Value;

            var damage = advanced?.Damage;
            if (damage == null)
            {
                // No advanced damage config, use broad multiplier for all types
                return new DamageMultipliers
                {
                    Base = broadMultiplier,
                    Blunt = broadMultiplier,
                    Slash = broadMultiplier,
                    Pierce = broadMultiplier,
                    Chop = broadMultiplier,
                    Pickaxe = broadMultiplier,
                    Fire = broadMultiplier,
                    Frost = broadMultiplier,
                    Lightning = broadMultiplier,
                    Poison = broadMultiplier,
                    Spirit = broadMultiplier
                };
            }

            // Specific advanced fields override broad multiplier
            return new DamageMultipliers
            {
                Base = damage.Base ?? broadMultiplier,
                Blunt = damage.Blunt ?? broadMultiplier,
                Slash = damage.Slash ?? broadMultiplier,
                Pierce = damage.Pierce ?? broadMultiplier,
                Chop = damage.Chop ?? broadMultiplier,
                Pickaxe = damage.Pickaxe ?? broadMultiplier,
                Fire = damage.Fire ?? broadMultiplier,
                Frost = damage.Frost ?? broadMultiplier,
                Lightning = damage.Lightning ?? broadMultiplier,
                Poison = damage.Poison ?? broadMultiplier,
                Spirit = damage.Spirit ?? broadMultiplier
            };
        }

        /// <summary>
        /// Resolves movement speed multipliers with priority:
        /// specific type > movementSpeed.multiplier (broad) > legacy movementSpeedMultiplier
        /// </summary>
        public static MovementSpeedMultipliers ResolveMovementSpeedMultipliers(AdvancedModifierConfig advanced, float? legacyMultiplier)
        {
            float broadMultiplier = 1f;

            // Determine broad multiplier: advanced.movementSpeed.multiplier > legacy movementSpeedMultiplier
            if (advanced?.MovementSpeed?.Multiplier.HasValue ?? false)
                broadMultiplier = advanced.MovementSpeed.Multiplier.Value;
            else if (legacyMultiplier.HasValue)
                broadMultiplier = legacyMultiplier.Value;

            var speed = advanced?.MovementSpeed;
            if (speed == null)
            {
                // No advanced movement speed config, use broad multiplier for all types
                return new MovementSpeedMultipliers
                {
                    Base = broadMultiplier,
                    Walk = broadMultiplier,
                    Run = broadMultiplier,
                    Swim = broadMultiplier
                };
            }

            // Specific advanced fields override broad multiplier
            return new MovementSpeedMultipliers
            {
                Base = speed.Base ?? broadMultiplier,
                Walk = speed.Walk ?? broadMultiplier,
                Run = speed.Run ?? broadMultiplier,
                Swim = speed.Swim ?? broadMultiplier
            };
        }

        /// <summary>
        /// Resolves AI modifiers with priority:
        /// - legacy disableAI maps to advanced.ai.disable.monsterAI=true unless explicit advanced AI state is set
        /// - advanced.ai.monsterAI.enabled is explicit and wins over ai.disable.monsterAI
        /// - baseAI and animalAI toggles require explicit safety gate (Tier 3)
        /// </summary>
        public static AIModifierResult ResolveAIModifiers(AdvancedModifierConfig advanced, bool legacyDisableAI, bool? legacyEnableAI)
        {
            var result = new AIModifierResult();

            // Check for explicit advanced MonsterAI config
            var monsterAI = advanced?.AI?.MonsterAI;
            if (monsterAI != null)
            {
                result.MonsterAIConfig = monsterAI;

                // explicit enabled field takes priority
                if (monsterAI.Enabled.HasValue)
                {
                    if (monsterAI.Enabled.Value)
                        result.EnableMonsterAI = true;
                    else
                        result.DisableMonsterAI = true;
                }
            }

            // Legacy enableAI (runtime-only) maps to enable.monsterAI
            if (legacyEnableAI.HasValue)
            {
                // Only apply if not already explicitly set by advanced
                if (!result.EnableMonsterAI.HasValue && !result.DisableMonsterAI.HasValue)
                {
                    if (legacyEnableAI.Value)
                        result.EnableMonsterAI = true;
                }
            }

            // Legacy disableAI maps to disable.monsterAI only if no explicit advanced AI state
            if (legacyDisableAI)
            {
                // Only apply if not already explicitly set by advanced
                if (!result.DisableMonsterAI.HasValue && !result.EnableMonsterAI.HasValue)
                {
                    result.DisableMonsterAI = true;
                }
            }

            // Check for advanced disable/enable toggles
            var disable = advanced?.AI?.Disable;
            var enable = advanced?.AI?.Enable;

            if (disable?.MonsterAI.HasValue ?? false)
            {
                if (disable.MonsterAI.Value)
                    result.DisableMonsterAI = true;
            }

            if (enable?.MonsterAI.HasValue ?? false)
            {
                if (enable.MonsterAI.Value)
                    result.EnableMonsterAI = true;
            }

            return result;
        }

        /// <summary>
        /// Resolves death effect configuration with priority:
        /// advanced.dropsAndDeath.deathEffect wins over legacy death-effect fields
        /// </summary>
        public static DeathEffectResult ResolveDeathEffect(AdvancedModifierConfig advanced,
            bool legacyClearDeathEffects, string legacyCopyDeathEffectsFrom, float? legacyDeathEffectScaleMultiplier)
        {
            var result = new DeathEffectResult();

            // Check if advanced deathEffect is provided
            var deathEffect = advanced?.DropsAndDeath?.DeathEffect;
            if (deathEffect != null && deathEffect.HasAnyValue)
            {
                // Advanced wins over legacy
                if (!string.IsNullOrWhiteSpace(deathEffect.Mode))
                    result.Mode = deathEffect.Mode.ToLowerInvariant();

                if (!string.IsNullOrWhiteSpace(deathEffect.CopyFrom))
                    result.CopyFrom = deathEffect.CopyFrom;

                if (deathEffect.ClearExisting.HasValue)
                    result.ClearExisting = deathEffect.ClearExisting.Value;

                if (deathEffect.ScaleMultiplier.HasValue)
                    result.ScaleMultiplier = deathEffect.ScaleMultiplier.Value;

                return result;
            }

            // Fall back to legacy fields
            if (legacyClearDeathEffects)
                result.Mode = "none";

            if (!string.IsNullOrWhiteSpace(legacyCopyDeathEffectsFrom))
            {
                result.Mode = "copyFrom";
                result.CopyFrom = legacyCopyDeathEffectsFrom;
            }

            if (legacyDeathEffectScaleMultiplier.HasValue)
                result.ScaleMultiplier = legacyDeathEffectScaleMultiplier.Value;

            return result;
        }

        /// <summary>
        /// Converts a Vector3Config to Unity Vector3 (nullable).
        /// Returns null if the config is null or has no values.
        /// </summary>
        public static Vector3? ResolveVector3(Vector3Config config)
        {
            if (config == null || !config.HasAnyValue)
                return null;

            return new Vector3(
                config.X ?? 0f,
                config.Y ?? 0f,
                config.Z ?? 0f
            );
        }
    }
}
