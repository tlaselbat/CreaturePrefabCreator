using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Knockback-related modifier configuration.
    /// Tier 2: Schema + No-op + Warning
    /// </summary>
    [DataContract]
    public class KnockbackModifierConfig
    {
        [DataMember(Name = "takenMultiplier", IsRequired = false)]
        public float? TakenMultiplier { get; set; } = null;

        public bool HasAnyValue => TakenMultiplier.HasValue;
    }
}
