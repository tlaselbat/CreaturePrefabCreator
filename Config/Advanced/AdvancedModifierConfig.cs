using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Root advanced modifier configuration containing all modifier categories.
    /// This is the main container for the "advanced" object in config files.
    /// </summary>
    [DataContract]
    public class AdvancedModifierConfig
    {
        [DataMember(Name = "transform", IsRequired = false)]
        public TransformModifierConfig Transform { get; set; } = null;

        [DataMember(Name = "health", IsRequired = false)]
        public HealthModifierConfig Health { get; set; } = null;

        [DataMember(Name = "damage", IsRequired = false)]
        public DamageModifierConfig Damage { get; set; } = null;

        [DataMember(Name = "defense", IsRequired = false)]
        public DefenseModifierConfig Defense { get; set; } = null;

        [DataMember(Name = "movementSpeed", IsRequired = false)]
        public MovementSpeedModifierConfig MovementSpeed { get; set; } = null;

        [DataMember(Name = "ai", IsRequired = false)]
        public AIModifierConfig AI { get; set; } = null;

        [DataMember(Name = "combat", IsRequired = false)]
        public CombatModifierConfig Combat { get; set; } = null;

        [DataMember(Name = "physics", IsRequired = false)]
        public PhysicsModifierConfig Physics { get; set; } = null;

        [DataMember(Name = "interaction", IsRequired = false)]
        public InteractionModifierConfig Interaction { get; set; } = null;

        [DataMember(Name = "dropsAndDeath", IsRequired = false)]
        public DropsAndDeathModifierConfig DropsAndDeath { get; set; } = null;

        /// <summary>
        /// Returns true if any advanced modifier field has a value.
        /// </summary>
        public bool HasAnyValue =>
            Transform?.HasAnyValue == true ||
            Health?.HasAnyValue == true ||
            Damage?.HasAnyValue == true ||
            Defense?.HasAnyValue == true ||
            MovementSpeed?.HasAnyValue == true ||
            AI?.HasAnyValue == true ||
            Combat?.HasAnyValue == true ||
            Physics?.HasAnyValue == true ||
            Interaction?.HasAnyValue == true ||
            DropsAndDeath?.HasAnyValue == true;

        /// <summary>
        /// Returns true if any Tier 1 (implemented) field has a value.
        /// </summary>
        public bool HasTier1Value =>
            (Health?.HasTier1Value ?? false) ||
            (Damage?.HasAnyValue ?? false) ||
            (MovementSpeed?.HasAnyValue ?? false) ||
            (AI?.MonsterAI?.HasTier1Value ?? false) ||
            (DropsAndDeath?.HasTier1Value ?? false);

        /// <summary>
        /// Returns true if any Tier 2 (Schema + No-op) field has a value.
        /// </summary>
        public bool HasTier2Value =>
            (Defense?.HasAnyValue ?? false) ||
            (AI?.MonsterAI?.HasTier2Value ?? false) ||
            (Combat?.HasTier2Value ?? false) ||
            (Interaction?.HasTier2Value ?? false);

        /// <summary>
        /// Returns true if any Tier 3 (Audit Required) field has a value.
        /// </summary>
        public bool HasTier3Value =>
            (Transform?.HasAnyValue ?? false) ||
            (Health?.HasTier3Value ?? false) ||
            (AI?.HasTier3DisableEnable ?? false) ||
            (AI?.MonsterAI?.HasTier3Value ?? false) ||
            (Combat?.HasTier3Value ?? false) ||
            (Physics?.HasAnyValue ?? false) ||
            (Interaction?.HasTier3Value ?? false) ||
            (DropsAndDeath?.HasTier3Value ?? false);
    }
}
