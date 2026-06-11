using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Simple vector3 configuration for offset values.
    /// Used by Tier 2/3 fields that require 3D positioning data.
    /// </summary>
    [DataContract]
    public class Vector3Config
    {
        [DataMember(Name = "x", IsRequired = false)]
        public float? X { get; set; } = null;

        [DataMember(Name = "y", IsRequired = false)]
        public float? Y { get; set; } = null;

        [DataMember(Name = "z", IsRequired = false)]
        public float? Z { get; set; } = null;

        public bool HasAnyValue => X.HasValue || Y.HasValue || Z.HasValue;
    }
}
