using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Movement speed modifier configuration.
    /// Tier 1: All fields implemented.
    /// </summary>
    [DataContract]
    public class MovementSpeedModifierConfig
    {
        [DataMember(Name = "multiplier", IsRequired = false)]
        public float? Multiplier { get; set; } = null;

        [DataMember(Name = "base", IsRequired = false)]
        public float? Base { get; set; } = null;

        [DataMember(Name = "walk", IsRequired = false)]
        public float? Walk { get; set; } = null;

        [DataMember(Name = "run", IsRequired = false)]
        public float? Run { get; set; } = null;

        [DataMember(Name = "swim", IsRequired = false)]
        public float? Swim { get; set; } = null;

        public bool HasAnyValue => Multiplier.HasValue || Base.HasValue || Walk.HasValue || Run.HasValue || Swim.HasValue;
    }
}
