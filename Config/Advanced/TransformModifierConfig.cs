using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Transform-related modifier configuration.
    /// NOTE: Tier 3/Schema-only. Use existing top-level 'scale' field instead.
    /// These fields will log a warning and no-op if used.
    /// </summary>
    [DataContract]
    public class TransformModifierConfig
    {
        [DataMember(Name = "scale", IsRequired = false)]
        public float? Scale { get; set; } = null;

        [DataMember(Name = "visualScaleMultiplier", IsRequired = false)]
        public float? VisualScaleMultiplier { get; set; } = null;

        [DataMember(Name = "modelScale", IsRequired = false)]
        public float? ModelScale { get; set; } = null;

        public bool HasAnyValue => Scale.HasValue || VisualScaleMultiplier.HasValue || ModelScale.HasValue;
    }
}
