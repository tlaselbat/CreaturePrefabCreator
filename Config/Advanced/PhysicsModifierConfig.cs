using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Physics-related modifier configuration.
    /// Tier 3: All fields are Schema + No-op + Warning (requires testing and safety notes).
    /// </summary>
    [DataContract]
    public class PhysicsModifierConfig
    {
        [DataMember(Name = "massMultiplier", IsRequired = false)]
        public float? MassMultiplier { get; set; } = null;

        [DataMember(Name = "colliderScale", IsRequired = false)]
        public float? ColliderScale { get; set; } = null;

        [DataMember(Name = "colliderHeight", IsRequired = false)]
        public float? ColliderHeight { get; set; } = null;

        [DataMember(Name = "colliderRadius", IsRequired = false)]
        public float? ColliderRadius { get; set; } = null;

        [DataMember(Name = "centerOfMassOffset", IsRequired = false)]
        public Vector3Config CenterOfMassOffset { get; set; } = null;

        public bool HasAnyValue =>
            MassMultiplier.HasValue || ColliderScale.HasValue || ColliderHeight.HasValue ||
            ColliderRadius.HasValue || CenterOfMassOffset?.HasAnyValue == true;
    }
}
