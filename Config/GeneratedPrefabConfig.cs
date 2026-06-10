using System;
using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config
{
    [DataContract]
    public class GeneratedPrefabConfig
    {
        [DataMember(Name = "enabled", IsRequired = false)]
        public bool Enabled { get; set; } = true;

        [DataMember(Name = "sourcePrefab", IsRequired = true)]
        public string SourcePrefab { get; set; } = "";

        [DataMember(Name = "newPrefab", IsRequired = true)]
        public string NewPrefab { get; set; } = "";

        [DataMember(Name = "adultPrefab", IsRequired = false)]
        public string AdultPrefab { get; set; } = "";

        [DataMember(Name = "displayName", IsRequired = false)]
        public string DisplayName { get; set; } = "";

        [DataMember(Name = "scale", IsRequired = false)]
        public float Scale { get; set; } = 0.35f;

        [DataMember(Name = "growIntoAdult", IsRequired = false)]
        public bool GrowIntoAdult { get; set; } = true;

        [DataMember(Name = "growTimeSeconds", IsRequired = false)]
        public float GrowTimeSeconds { get; set; } = 6000f;

        [DataMember(Name = "preserveTamed", IsRequired = false)]
        public bool PreserveTamed { get; set; } = true;

        [DataMember(Name = "preserveLevel", IsRequired = false)]
        public bool PreserveLevel { get; set; } = true;

        [DataMember(Name = "preserveOwner", IsRequired = false)]
        public bool PreserveOwner { get; set; } = true;

        [DataMember(Name = "preserveName", IsRequired = false)]
        public bool PreserveName { get; set; } = false;

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

        public bool IsValid(out string error)
        {
            error = null;

            if (string.IsNullOrWhiteSpace(SourcePrefab))
            {
                error = "sourcePrefab is empty";
                return false;
            }

            if (string.IsNullOrWhiteSpace(NewPrefab))
            {
                error = "newPrefab is empty";
                return false;
            }

            if (Scale <= 0f)
            {
                error = $"scale must be greater than 0 (got {Scale})";
                return false;
            }

            if (GrowIntoAdult)
            {
                if (string.IsNullOrWhiteSpace(AdultPrefab))
                {
                    error = "adultPrefab must not be empty when growIntoAdult is true";
                    return false;
                }
                if (GrowTimeSeconds <= 0f)
                {
                    error = $"growTimeSeconds must be greater than 0 when growIntoAdult is true (got {GrowTimeSeconds})";
                    return false;
                }
            }

            if (string.Equals(NewPrefab, SourcePrefab, StringComparison.OrdinalIgnoreCase))
            {
                error = "newPrefab must not equal sourcePrefab";
                return false;
            }

            return true;
        }
    }
}
