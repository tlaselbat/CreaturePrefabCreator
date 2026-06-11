using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Health-related modifier configuration.
    /// Tier 1: multiplier, maxHealth
    /// Tier 3: healthRegenMultiplier (schema-only, logs warning)
    /// </summary>
    [DataContract]
    public class HealthModifierConfig
    {
        [DataMember(Name = "multiplier", IsRequired = false)]
        public float? Multiplier { get; set; } = null;

        [DataMember(Name = "maxHealth", IsRequired = false)]
        public float? MaxHealth { get; set; } = null;

        [DataMember(Name = "healthRegenMultiplier", IsRequired = false)]
        public float? HealthRegenMultiplier { get; set; } = null;

        public bool HasAnyValue => Multiplier.HasValue || MaxHealth.HasValue || HealthRegenMultiplier.HasValue;

        public bool HasTier1Value => Multiplier.HasValue || MaxHealth.HasValue;

        public bool HasTier3Value => HealthRegenMultiplier.HasValue;
    }
}
