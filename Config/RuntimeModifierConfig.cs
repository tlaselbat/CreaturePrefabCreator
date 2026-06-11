using System.Runtime.Serialization;
using CreaturePrefabCreator.Config.Advanced;

namespace CreaturePrefabCreator.Config
{
    [DataContract]
    public class RuntimeModifierConfig
    {
        [DataMember(Name = "enabled", IsRequired = false)]
        public bool Enabled { get; set; } = true;

        [DataMember(Name = "targetPrefab", IsRequired = true)]
        public string TargetPrefab { get; set; } = "";

        [DataMember(Name = "conditions", IsRequired = false)]
        public RuntimeModifierConditionConfig Conditions { get; set; } = new RuntimeModifierConditionConfig();

        [DataMember(Name = "effects", IsRequired = false)]
        public RuntimeModifierEffectConfig Effects { get; set; } = new RuntimeModifierEffectConfig();

        public bool IsValid(out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(TargetPrefab))
            {
                error = "targetPrefab is empty";
                return false;
            }
            return true;
        }
    }

    [DataContract]
    public class RuntimeModifierConditionConfig
    {
        [DataMember(Name = "starLevel", IsRequired = false)]
        public int? StarLevel { get; set; } = null;

        [DataMember(Name = "tamed", IsRequired = false)]
        public bool? Tamed { get; set; } = null;

        [DataMember(Name = "saddled", IsRequired = false)]
        public bool? Saddled { get; set; } = null;

        [DataMember(Name = "ridden", IsRequired = false)]
        public bool? Ridden { get; set; } = null;

        public bool IsEmpty => StarLevel == null && Tamed == null && Saddled == null && Ridden == null;
    }

    [DataContract]
    public class RuntimeModifierEffectConfig
    {
        [DataMember(Name = "healthMultiplier", IsRequired = false)]
        public float? HealthMultiplier { get; set; } = null;

        [DataMember(Name = "damageMultiplier", IsRequired = false)]
        public float? DamageMultiplier { get; set; } = null;

        [DataMember(Name = "movementSpeedMultiplier", IsRequired = false)]
        public float? MovementSpeedMultiplier { get; set; } = null;

        [DataMember(Name = "disableAI", IsRequired = false)]
        public bool? DisableAI { get; set; } = null;

        [DataMember(Name = "enableAI", IsRequired = false)]
        public bool? EnableAI { get; set; } = null;

        // Advanced modifier configuration (optional, backwards-compatible)
        [DataMember(Name = "advanced", IsRequired = false)]
        public AdvancedModifierConfig Advanced { get; set; } = null;

        public bool IsEmpty => HealthMultiplier == null && DamageMultiplier == null && MovementSpeedMultiplier == null && DisableAI == null && EnableAI == null && Advanced == null;
    }
}
