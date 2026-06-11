using System;
using System.Runtime.Serialization;
using CreaturePrefabCreator.Config.Advanced;

namespace CreaturePrefabCreator.Config
{
    [DataContract]
    public class PrefabOverrideConfig
    {
        [DataMember(Name = "enabled", IsRequired = false)]
        public bool Enabled { get; set; } = true;

        [DataMember(Name = "targetPrefab", IsRequired = true)]
        public string TargetPrefab { get; set; } = "";

        [DataMember(Name = "displayName", IsRequired = false)]
        public string DisplayName { get; set; } = "";

        [DataMember(Name = "scale", IsRequired = false)]
        public float Scale { get; set; } = 1.0f;

        [DataMember(Name = "applyToExistingSpawnedCreatures", IsRequired = false)]
        public bool ApplyToExistingSpawnedCreatures { get; set; } = false;

        [DataMember(Name = "propagateToGeneratedVariants", IsRequired = false)]
        public bool PropagateToGeneratedVariants { get; set; } = true;

        [DataMember(Name = "healthMultiplier", IsRequired = false)]
        public float? HealthMultiplier { get; set; } = null;

        [DataMember(Name = "damageMultiplier", IsRequired = false)]
        public float? DamageMultiplier { get; set; } = null;

        [DataMember(Name = "clearDeathEffects", IsRequired = false)]
        public bool ClearDeathEffects { get; set; } = false;

        [DataMember(Name = "copyDeathEffectsFrom", IsRequired = false)]
        public string CopyDeathEffectsFrom { get; set; } = null;

        [DataMember(Name = "deathEffectScaleMultiplier", IsRequired = false)]
        public float? DeathEffectScaleMultiplier { get; set; } = null;

        [DataMember(Name = "forceFaction", IsRequired = false)]
        public string ForceFaction { get; set; } = null;

        // Phase 1: AI, Audio, Visual, and Faction Controls
        [DataMember(Name = "disableAI", IsRequired = false)]
        public bool DisableAI { get; set; } = false;

        [DataMember(Name = "disableIdleSounds", IsRequired = false)]
        public bool DisableIdleSounds { get; set; } = false;

        [DataMember(Name = "disableAggro", IsRequired = false)]
        public bool DisableAggro { get; set; } = false;

        [DataMember(Name = "disableFleeing", IsRequired = false)]
        public bool DisableFleeing { get; set; } = false;

        [DataMember(Name = "tintColor", IsRequired = false)]
        public string TintColor { get; set; } = null;

        [DataMember(Name = "glowColor", IsRequired = false)]
        public string GlowColor { get; set; } = null;

        [DataMember(Name = "friendAttacked", IsRequired = false)]
        public bool? FriendAttacked { get; set; } = null;

        // Advanced modifier configuration (optional, backwards-compatible)
        [DataMember(Name = "advanced", IsRequired = false)]
        public AdvancedModifierConfig Advanced { get; set; } = null;

        public bool IsValid(out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(TargetPrefab))
            {
                error = "targetPrefab is empty";
                return false;
            }

            if (Scale <= 0f)
            {
                error = $"scale must be greater than 0 (got {Scale})";
                return false;
            }

            return true;
        }
    }
}
