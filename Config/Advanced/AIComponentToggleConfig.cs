using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// AI component toggle configuration.
    /// Tier 3: baseAI and animalAI are Schema + No-op + Warning (requires explicit safety gate).
    /// MonsterAI is Tier 1 for disable, runtime-only for enable.
    /// </summary>
    [DataContract]
    public class AIComponentToggleConfig
    {
        [DataMember(Name = "baseAI", IsRequired = false)]
        public bool? BaseAI { get; set; } = null;

        [DataMember(Name = "monsterAI", IsRequired = false)]
        public bool? MonsterAI { get; set; } = null;

        [DataMember(Name = "animalAI", IsRequired = false)]
        public bool? AnimalAI { get; set; } = null;

        public bool HasAnyValue => BaseAI.HasValue || MonsterAI.HasValue || AnimalAI.HasValue;

        public bool HasTier3Value => BaseAI.HasValue || AnimalAI.HasValue;
    }
}
