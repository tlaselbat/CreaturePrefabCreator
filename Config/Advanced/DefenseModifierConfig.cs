using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Defense-related modifier configuration.
    /// Tier 2: damageTaken, stagger, knockback are Schema + No-op + Warning
    /// </summary>
    [DataContract]
    public class DefenseModifierConfig
    {
        [DataMember(Name = "damageTaken", IsRequired = false)]
        public DamageTakenModifierConfig DamageTaken { get; set; } = null;

        [DataMember(Name = "stagger", IsRequired = false)]
        public StaggerModifierConfig Stagger { get; set; } = null;

        [DataMember(Name = "knockback", IsRequired = false)]
        public KnockbackModifierConfig Knockback { get; set; } = null;

        public bool HasAnyValue => DamageTaken != null || Stagger != null || Knockback != null;
    }
}
