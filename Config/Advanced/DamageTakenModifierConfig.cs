using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Damage taken modifier configuration.
    /// Tier 2: Schema + No-op + Warning
    /// </summary>
    [DataContract]
    public class DamageTakenModifierConfig
    {
        [DataMember(Name = "multiplier", IsRequired = false)]
        public float? Multiplier { get; set; } = null;

        [DataMember(Name = "physical", IsRequired = false)]
        public float? Physical { get; set; } = null;

        [DataMember(Name = "elemental", IsRequired = false)]
        public float? Elemental { get; set; } = null;

        [DataMember(Name = "blunt", IsRequired = false)]
        public float? Blunt { get; set; } = null;

        [DataMember(Name = "slash", IsRequired = false)]
        public float? Slash { get; set; } = null;

        [DataMember(Name = "pierce", IsRequired = false)]
        public float? Pierce { get; set; } = null;

        [DataMember(Name = "chop", IsRequired = false)]
        public float? Chop { get; set; } = null;

        [DataMember(Name = "pickaxe", IsRequired = false)]
        public float? Pickaxe { get; set; } = null;

        [DataMember(Name = "fire", IsRequired = false)]
        public float? Fire { get; set; } = null;

        [DataMember(Name = "frost", IsRequired = false)]
        public float? Frost { get; set; } = null;

        [DataMember(Name = "lightning", IsRequired = false)]
        public float? Lightning { get; set; } = null;

        [DataMember(Name = "poison", IsRequired = false)]
        public float? Poison { get; set; } = null;

        [DataMember(Name = "spirit", IsRequired = false)]
        public float? Spirit { get; set; } = null;

        public bool HasAnyValue => Multiplier.HasValue || Physical.HasValue || Elemental.HasValue ||
                                   Blunt.HasValue || Slash.HasValue || Pierce.HasValue ||
                                   Chop.HasValue || Pickaxe.HasValue || Fire.HasValue ||
                                   Frost.HasValue || Lightning.HasValue || Poison.HasValue || Spirit.HasValue;
    }
}
