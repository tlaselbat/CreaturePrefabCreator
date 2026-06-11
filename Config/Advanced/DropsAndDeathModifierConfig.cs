using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Drops and death-related modifier configuration.
    /// Tier 1: deathEffect fields (mode, copyFrom, clearExisting, scaleMultiplier)
    /// Tier 3: deathEffect.prefab, dropTableScaleAware
    /// </summary>
    [DataContract]
    public class DropsAndDeathModifierConfig
    {
        [DataMember(Name = "deathEffect", IsRequired = false)]
        public DeathEffectModifierConfig DeathEffect { get; set; } = null;

        /// <summary>
        /// Tier 3: Schema + No-op + Warning
        /// </summary>
        [DataMember(Name = "dropTableScaleAware", IsRequired = false)]
        public bool? DropTableScaleAware { get; set; } = null;

        public bool HasAnyValue => DeathEffect != null || DropTableScaleAware.HasValue;

        public bool HasTier1Value => (DeathEffect?.HasTier1Value ?? false);

        public bool HasTier3Value =>
            (DeathEffect?.HasTier3Value ?? false) || DropTableScaleAware.HasValue;
    }
}
