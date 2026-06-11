using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// AI-related modifier configuration.
    /// disable/enable are Tier 3 for baseAI/animalAI (requires explicit safety gate).
    /// monsterAI fields are Tier 1 safe.
    /// </summary>
    [DataContract]
    public class AIModifierConfig
    {
        [DataMember(Name = "disable", IsRequired = false)]
        public AIComponentToggleConfig Disable { get; set; } = null;

        [DataMember(Name = "enable", IsRequired = false)]
        public AIComponentToggleConfig Enable { get; set; } = null;

        [DataMember(Name = "baseAI", IsRequired = false)]
        public BaseAIModifierConfig BaseAI { get; set; } = null;

        [DataMember(Name = "monsterAI", IsRequired = false)]
        public MonsterAIModifierConfig MonsterAI { get; set; } = null;

        [DataMember(Name = "animalAI", IsRequired = false)]
        public AnimalAIModifierConfig AnimalAI { get; set; } = null;

        public bool HasAnyValue => Disable != null || Enable != null || BaseAI != null || MonsterAI != null || AnimalAI != null;

        public bool HasTier3DisableEnable =>
            (Disable?.HasTier3Value ?? false) || (Enable?.HasTier3Value ?? false) ||
            (BaseAI?.HasAnyValue ?? false) || (AnimalAI?.HasAnyValue ?? false);
    }
}
