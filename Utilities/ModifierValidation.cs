using System.Collections.Generic;
using CreaturePrefabCreator.Config.Advanced;

namespace CreaturePrefabCreator.Utilities
{
    /// <summary>
    /// Centralized validation helpers for modifier fields.
    /// </summary>
    public static class ModifierValidation
    {
        public const float MinMultiplier = 0.01f;
        public const float MaxMultiplier = 100f;
        public const float VeryLargeThreshold = 10f;

        private static readonly HashSet<string> _warnedPrefabs = new HashSet<string>();
        private static readonly HashSet<string> _warnedTier2Fields = new HashSet<string>();
        private static readonly HashSet<string> _warnedTier3Fields = new HashSet<string>();

        /// <summary>
        /// Validates a multiplier value is within the acceptable range.
        /// </summary>
        public static bool IsValidMultiplier(float value, out string error)
        {
            error = null;

            if (value < MinMultiplier)
            {
                error = $"Value {value} is below minimum {MinMultiplier}";
                return false;
            }

            if (value > MaxMultiplier)
            {
                error = $"Value {value} exceeds maximum {MaxMultiplier}";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a multiplier is an identity value (0 or 1) that should be treated as no-op.
        /// </summary>
        public static bool IsIdentityValue(float value)
        {
            return value == 0f || value == 1f;
        }

        /// <summary>
        /// Validates and logs warnings for invalid multiplier values.
        /// Returns the safe value to use (null if invalid).
        /// </summary>
        public static float? ValidateAndGetMultiplier(string context, string fieldName, float? value)
        {
            if (!value.HasValue)
                return null;

            float v = value.Value;

            // Treat 0 and 1 as identity (no-op)
            if (IsIdentityValue(v))
                return null;

            if (!IsValidMultiplier(v, out string error))
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                    $"[{context}] '{fieldName}': {error}; skipping.");
                return null;
            }

            if (v > VeryLargeThreshold)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                    $"[{context}] '{fieldName}': value {v} is very large (>{VeryLargeThreshold}); applying.");
            }

            return v;
        }

        /// <summary>
        /// Logs a warning for unsupported Tier 2 fields (once per prefab/field combination).
        /// </summary>
        public static void LogUnsupportedTier2Field(string context, string fieldName)
        {
            string key = $"{context}:{fieldName}";
            if (_warnedTier2Fields.Contains(key))
                return;

            _warnedTier2Fields.Add(key);
            CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                $"[{context}] '{fieldName}' is a Tier 2 field (Schema + No-op). " +
                "This field is recognized but not yet implemented. It will have no effect.");
        }

        /// <summary>
        /// Logs a warning for unsupported Tier 3 fields (once per prefab/field combination).
        /// </summary>
        public static void LogUnsupportedTier3Field(string context, string fieldName)
        {
            string key = $"{context}:{fieldName}";
            if (_warnedTier3Fields.Contains(key))
                return;

            _warnedTier3Fields.Add(key);
            CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                $"[{context}] '{fieldName}' is a Tier 3 field (Audit Required). " +
                "This field requires explicit safety gate or testing before use. It will have no effect.");
        }

        /// <summary>
        /// Checks an entire AdvancedModifierConfig and logs warnings for Tier 2/3 fields.
        /// </summary>
        public static void LogUnsupportedFields(string context, AdvancedModifierConfig advanced)
        {
            if (advanced == null)
                return;

            // Tier 3: Transform
            if (advanced.Transform?.HasAnyValue == true)
            {
                LogUnsupportedTier3Field(context, "advanced.transform");
            }

            // Tier 3: Health regen
            if (advanced.Health?.HealthRegenMultiplier.HasValue == true)
            {
                LogUnsupportedTier3Field(context, "advanced.health.healthRegenMultiplier");
            }

            // Tier 2/3: Defense
            if (advanced.Defense?.HasAnyValue == true)
            {
                if (advanced.Defense.DamageTaken?.HasAnyValue == true)
                    LogUnsupportedTier2Field(context, "advanced.defense.damageTaken");
                if (advanced.Defense.Stagger?.HasAnyValue == true)
                    LogUnsupportedTier2Field(context, "advanced.defense.stagger");
                if (advanced.Defense.Knockback?.HasAnyValue == true)
                    LogUnsupportedTier2Field(context, "advanced.defense.knockback");
            }

            // Tier 3: AI disable/enable for baseAI and animalAI
            if (advanced.AI?.Disable?.BaseAI.HasValue == true)
                LogUnsupportedTier3Field(context, "advanced.ai.disable.baseAI");
            if (advanced.AI?.Disable?.AnimalAI.HasValue == true)
                LogUnsupportedTier3Field(context, "advanced.ai.disable.animalAI");
            if (advanced.AI?.Enable?.BaseAI.HasValue == true)
                LogUnsupportedTier3Field(context, "advanced.ai.enable.baseAI");
            if (advanced.AI?.Enable?.AnimalAI.HasValue == true)
                LogUnsupportedTier3Field(context, "advanced.ai.enable.animalAI");

            // Tier 3: BaseAI and AnimalAI configs
            if (advanced.AI?.BaseAI?.HasAnyValue == true)
                LogUnsupportedTier3Field(context, "advanced.ai.baseAI");
            if (advanced.AI?.AnimalAI?.HasAnyValue == true)
                LogUnsupportedTier3Field(context, "advanced.ai.animalAI");

            // Tier 2: MonsterAI view/hear/consume fields
            var monsterAI = advanced.AI?.MonsterAI;
            if (monsterAI != null)
            {
                if (monsterAI.ViewRange.HasValue)
                    LogUnsupportedTier2Field(context, "advanced.ai.monsterAI.viewRange");
                if (monsterAI.ViewAngle.HasValue)
                    LogUnsupportedTier2Field(context, "advanced.ai.monsterAI.viewAngle");
                if (monsterAI.HearRange.HasValue)
                    LogUnsupportedTier2Field(context, "advanced.ai.monsterAI.hearRange");
                if (monsterAI.AlertRange.HasValue)
                    LogUnsupportedTier2Field(context, "advanced.ai.monsterAI.alertRange");
                if (monsterAI.ConsumeRange.HasValue)
                    LogUnsupportedTier2Field(context, "advanced.ai.monsterAI.consumeRange");
                if (monsterAI.ConsumeSearchRange.HasValue)
                    LogUnsupportedTier2Field(context, "advanced.ai.monsterAI.consumeSearchRange");
                if (monsterAI.ConsumeInterval.HasValue)
                    LogUnsupportedTier2Field(context, "advanced.ai.monsterAI.consumeInterval");

                // Tier 3: stoppingDistanceMultiplier
                if (monsterAI.StoppingDistanceMultiplier.HasValue)
                    LogUnsupportedTier3Field(context, "advanced.ai.monsterAI.stoppingDistanceMultiplier");
            }

            // Tier 2: Combat attackRangeMultiplier, turnSpeedMultiplier
            if (advanced.Combat?.HasTier2Value == true)
            {
                if (advanced.Combat.AttackRangeMultiplier.HasValue)
                    LogUnsupportedTier2Field(context, "advanced.combat.attackRangeMultiplier");
                if (advanced.Combat.TurnSpeedMultiplier.HasValue)
                    LogUnsupportedTier2Field(context, "advanced.combat.turnSpeedMultiplier");
            }

            // Tier 3: Combat hitbox/offset fields
            if (advanced.Combat?.HasTier3Value == true)
            {
                if (advanced.Combat.AttackHitboxScale.HasValue)
                    LogUnsupportedTier3Field(context, "advanced.combat.attackHitboxScale");
                if (advanced.Combat.AttackOriginOffset?.HasAnyValue == true)
                    LogUnsupportedTier3Field(context, "advanced.combat.attackOriginOffset");
                if (advanced.Combat.AttackHeightOffset.HasValue)
                    LogUnsupportedTier3Field(context, "advanced.combat.attackHeightOffset");
                if (advanced.Combat.ProjectileSpawnOffset?.HasAnyValue == true)
                    LogUnsupportedTier3Field(context, "advanced.combat.projectileSpawnOffset");
            }

            // Tier 3: Physics
            if (advanced.Physics?.HasAnyValue == true)
            {
                LogUnsupportedTier3Field(context, "advanced.physics");
            }

            // Tier 2: Interaction hoverTextOffset, useRangeMultiplier
            if (advanced.Interaction?.HasTier2Value == true)
            {
                if (advanced.Interaction.HoverTextOffset?.HasAnyValue == true)
                    LogUnsupportedTier2Field(context, "advanced.interaction.hoverTextOffset");
                if (advanced.Interaction.UseRangeMultiplier.HasValue)
                    LogUnsupportedTier2Field(context, "advanced.interaction.useRangeMultiplier");
            }

            // Tier 3: Interaction saddle/mount offsets
            if (advanced.Interaction?.HasTier3Value == true)
            {
                if (advanced.Interaction.SaddlePositionOffset?.HasAnyValue == true)
                    LogUnsupportedTier3Field(context, "advanced.interaction.saddlePositionOffset");
                if (advanced.Interaction.MountPointOffset?.HasAnyValue == true)
                    LogUnsupportedTier3Field(context, "advanced.interaction.mountPointOffset");
            }

            // Tier 3: DropsAndDeath fields
            if (advanced.DropsAndDeath?.HasTier3Value == true)
            {
                if (advanced.DropsAndDeath.DropTableScaleAware.HasValue)
                    LogUnsupportedTier3Field(context, "advanced.dropsAndDeath.dropTableScaleAware");
                if (!string.IsNullOrWhiteSpace(advanced.DropsAndDeath.DeathEffect?.Prefab))
                    LogUnsupportedTier3Field(context, "advanced.dropsAndDeath.deathEffect.prefab");
                if (advanced.DropsAndDeath.DeathEffect?.Mode == "customPrefab")
                    LogUnsupportedTier3Field(context, "advanced.dropsAndDeath.deathEffect.mode=customPrefab");
            }
        }

        /// <summary>
        /// Clears all warning tracking (useful for config reloads).
        /// </summary>
        public static void ClearWarningTracking()
        {
            _warnedPrefabs.Clear();
            _warnedTier2Fields.Clear();
            _warnedTier3Fields.Clear();
        }
    }
}
