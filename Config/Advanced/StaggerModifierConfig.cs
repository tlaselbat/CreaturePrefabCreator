using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Stagger-related modifier configuration.
    /// Tier 2: Schema + No-op + Warning
    /// </summary>
    [DataContract]
    public class StaggerModifierConfig
    {
        [DataMember(Name = "multiplier", IsRequired = false)]
        public float? Multiplier { get; set; } = null;

        [DataMember(Name = "threshold", IsRequired = false)]
        public float? Threshold { get; set; } = null;

        public bool HasAnyValue => Multiplier.HasValue || Threshold.HasValue;
    }
}
