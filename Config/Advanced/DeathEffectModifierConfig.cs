using System.Runtime.Serialization;

namespace CreaturePrefabCreator.Config.Advanced
{
    /// <summary>
    /// Death effect modifier configuration.
    /// Tier 1: mode, copyFrom, clearExisting, scaleMultiplier
    /// Tier 3: prefab (Schema + No-op + Warning)
    /// </summary>
    [DataContract]
    public class DeathEffectModifierConfig
    {
        /// <summary>
        /// Death effect mode. Valid values: vanilla, none, copyFrom, customPrefab.
        /// customPrefab is Tier 3 (Schema + No-op + Warning).
        /// </summary>
        [DataMember(Name = "mode", IsRequired = false)]
        public string Mode { get; set; } = null;

        /// <summary>
        /// Custom prefab name for death effects. Tier 3 (not implemented).
        /// </summary>
        [DataMember(Name = "prefab", IsRequired = false)]
        public string Prefab { get; set; } = null;

        /// <summary>
        /// Source prefab to copy death effects from. Used when mode is "copyFrom".
        /// </summary>
        [DataMember(Name = "copyFrom", IsRequired = false)]
        public string CopyFrom { get; set; } = null;

        /// <summary>
        /// Whether to clear existing death effects before applying.
        /// </summary>
        [DataMember(Name = "clearExisting", IsRequired = false)]
        public bool? ClearExisting { get; set; } = null;

        /// <summary>
        /// Multiplier for death effect scale (e.g., ragdoll size).
        /// </summary>
        [DataMember(Name = "scaleMultiplier", IsRequired = false)]
        public float? ScaleMultiplier { get; set; } = null;

        public bool HasAnyValue =>
            Mode != null || Prefab != null || CopyFrom != null ||
            ClearExisting.HasValue || ScaleMultiplier.HasValue;

        public bool HasTier1Value =>
            (Mode != null && Mode != "customPrefab") || CopyFrom != null ||
            ClearExisting.HasValue || ScaleMultiplier.HasValue;

        public bool HasTier3Value => Prefab != null || Mode == "customPrefab";

        /// <summary>
        /// Validates that the mode is one of the supported values.
        /// </summary>
        public bool IsValidMode(out string error)
        {
            error = null;
            if (Mode == null) return true;

            var validModes = new[] { "vanilla", "none", "copyFrom", "customPrefab" };
            foreach (var valid in validModes)
            {
                if (Mode.Equals(valid, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            error = $"Invalid deathEffect mode '{Mode}'. Valid values: vanilla, none, copyFrom, customPrefab";
            return false;
        }
    }
}
