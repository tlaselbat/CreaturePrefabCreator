using UnityEngine;

namespace CreaturePrefabCreator.Compatibility.AllTameable
{
    public static class AllTameableInterop
    {
        public static bool IsTamed(GameObject go)
        {
            if (!AllTameableCompat.IsAvailable) return false;
            if (go == null) return false;

            try
            {
                var type = AllTameableReflection.TameableType;
                if (type == null) return false;

                var comp = go.GetComponent(type);
                if (comp == null) return false;

                var isTamedProp = type.GetProperty("IsTamed") ?? type.GetProperty("isTamed");
                if (isTamedProp != null)
                    return (bool)isTamedProp.GetValue(comp);
            }
            catch { }

            return false;
        }
    }
}
