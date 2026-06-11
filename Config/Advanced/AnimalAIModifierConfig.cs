using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// AnimalAI-specific modifier configuration.
    /// Tier 3: Schema + No-op + Warning (requires explicit safety gate).
    /// </summary>
    [DataContract]
    public class AnimalAIModifierConfig
    {
        [DataMember(Name = "enabled", IsRequired = false)]
        public bool? Enabled { get; set; } = null;

        public bool HasAnyValue => Enabled.HasValue;
    }
}
