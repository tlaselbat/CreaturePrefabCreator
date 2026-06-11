using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Interaction-related modifier configuration.
    /// Tier 2: hoverTextOffset, useRangeMultiplier
    /// Tier 3: saddlePositionOffset, mountPointOffset (requires MountUp audit)
    /// </summary>
    [DataContract]
    public class InteractionModifierConfig
    {
        // Tier 2 fields
        [DataMember(Name = "hoverTextOffset", IsRequired = false)]
        public Vector3Config HoverTextOffset { get; set; } = null;

        [DataMember(Name = "useRangeMultiplier", IsRequired = false)]
        public float? UseRangeMultiplier { get; set; } = null;

        // Tier 3 fields
        [DataMember(Name = "saddlePositionOffset", IsRequired = false)]
        public Vector3Config SaddlePositionOffset { get; set; } = null;

        [DataMember(Name = "mountPointOffset", IsRequired = false)]
        public Vector3Config MountPointOffset { get; set; } = null;

        public bool HasAnyValue =>
            HoverTextOffset?.HasAnyValue == true || UseRangeMultiplier.HasValue ||
            SaddlePositionOffset?.HasAnyValue == true || MountPointOffset?.HasAnyValue == true;

        public bool HasTier2Value => HoverTextOffset?.HasAnyValue == true || UseRangeMultiplier.HasValue;

        public bool HasTier3Value =>
            SaddlePositionOffset?.HasAnyValue == true || MountPointOffset?.HasAnyValue == true;
    }
}
