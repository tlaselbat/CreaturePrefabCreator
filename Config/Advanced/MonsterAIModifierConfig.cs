using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// MonsterAI-specific modifier configuration.
    /// Tier 1: enabled, aggravatable, fleeIfNotAlerted, fleeInLava, fleeRange, friendAttacked
    /// Tier 2: viewRange, viewAngle, hearRange, alertRange, consumeRange, consumeSearchRange, consumeInterval
    /// Tier 3: stoppingDistanceMultiplier
    /// </summary>
    [DataContract]
    public class MonsterAIModifierConfig
    {
        // Tier 1 fields
        [DataMember(Name = "enabled", IsRequired = false)]
        public bool? Enabled { get; set; } = null;

        [DataMember(Name = "aggravatable", IsRequired = false)]
        public bool? Aggravatable { get; set; } = null;

        [DataMember(Name = "fleeIfNotAlerted", IsRequired = false)]
        public bool? FleeIfNotAlerted { get; set; } = null;

        [DataMember(Name = "fleeInLava", IsRequired = false)]
        public bool? FleeInLava { get; set; } = null;

        [DataMember(Name = "fleeRange", IsRequired = false)]
        public float? FleeRange { get; set; } = null;

        [DataMember(Name = "friendAttacked", IsRequired = false)]
        public bool? FriendAttacked { get; set; } = null;

        // Tier 2 fields
        [DataMember(Name = "viewRange", IsRequired = false)]
        public float? ViewRange { get; set; } = null;

        [DataMember(Name = "viewAngle", IsRequired = false)]
        public float? ViewAngle { get; set; } = null;

        [DataMember(Name = "hearRange", IsRequired = false)]
        public float? HearRange { get; set; } = null;

        [DataMember(Name = "alertRange", IsRequired = false)]
        public float? AlertRange { get; set; } = null;

        [DataMember(Name = "consumeRange", IsRequired = false)]
        public float? ConsumeRange { get; set; } = null;

        [DataMember(Name = "consumeSearchRange", IsRequired = false)]
        public float? ConsumeSearchRange { get; set; } = null;

        [DataMember(Name = "consumeInterval", IsRequired = false)]
        public float? ConsumeInterval { get; set; } = null;

        // Tier 3 fields
        [DataMember(Name = "stoppingDistanceMultiplier", IsRequired = false)]
        public float? StoppingDistanceMultiplier { get; set; } = null;

        public bool HasAnyValue =>
            Enabled.HasValue || Aggravatable.HasValue || FleeIfNotAlerted.HasValue ||
            FleeInLava.HasValue || FleeRange.HasValue || FriendAttacked.HasValue ||
            ViewRange.HasValue || ViewAngle.HasValue || HearRange.HasValue ||
            AlertRange.HasValue || ConsumeRange.HasValue || ConsumeSearchRange.HasValue ||
            ConsumeInterval.HasValue || StoppingDistanceMultiplier.HasValue;

        public bool HasTier1Value =>
            Enabled.HasValue || Aggravatable.HasValue || FleeIfNotAlerted.HasValue ||
            FleeInLava.HasValue || FleeRange.HasValue || FriendAttacked.HasValue;

        public bool HasTier2Value =>
            ViewRange.HasValue || ViewAngle.HasValue || HearRange.HasValue ||
            AlertRange.HasValue || ConsumeRange.HasValue || ConsumeSearchRange.HasValue ||
            ConsumeInterval.HasValue;

        public bool HasTier3Value => StoppingDistanceMultiplier.HasValue;
    }
}
