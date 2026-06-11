using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Combat-related modifier configuration.
    /// Tier 2: attackRangeMultiplier, turnSpeedMultiplier (Schema + No-op + Warning)
    /// Tier 3: attackHitboxScale, attackOriginOffset, attackHeightOffset, projectileSpawnOffset
    /// </summary>
    [DataContract]
    public class CombatModifierConfig
    {
        // Tier 2 fields
        [DataMember(Name = "attackRangeMultiplier", IsRequired = false)]
        public float? AttackRangeMultiplier { get; set; } = null;

        [DataMember(Name = "turnSpeedMultiplier", IsRequired = false)]
        public float? TurnSpeedMultiplier { get; set; } = null;

        // Tier 3 fields
        [DataMember(Name = "attackHitboxScale", IsRequired = false)]
        public float? AttackHitboxScale { get; set; } = null;

        [DataMember(Name = "attackOriginOffset", IsRequired = false)]
        public Vector3Config AttackOriginOffset { get; set; } = null;

        [DataMember(Name = "attackHeightOffset", IsRequired = false)]
        public float? AttackHeightOffset { get; set; } = null;

        [DataMember(Name = "projectileSpawnOffset", IsRequired = false)]
        public Vector3Config ProjectileSpawnOffset { get; set; } = null;

        public bool HasAnyValue =>
            AttackRangeMultiplier.HasValue || TurnSpeedMultiplier.HasValue ||
            AttackHitboxScale.HasValue || AttackOriginOffset?.HasAnyValue == true ||
            AttackHeightOffset.HasValue || ProjectileSpawnOffset?.HasAnyValue == true;

        public bool HasTier2Value => AttackRangeMultiplier.HasValue || TurnSpeedMultiplier.HasValue;

        public bool HasTier3Value =>
            AttackHitboxScale.HasValue || AttackOriginOffset?.HasAnyValue == true ||
            AttackHeightOffset.HasValue || ProjectileSpawnOffset?.HasAnyValue == true;
    }
}
