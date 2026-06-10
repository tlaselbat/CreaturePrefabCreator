using UnityEngine;

namespace CreaturePrefabCreator.Compatibility.MountUpRestored
{
    public static class MountUpRestoredInterop
    {
        public static bool IsMounted(GameObject go)
        {
            if (!MountUpRestoredCompat.IsAvailable) return false;
            if (go == null) return false;

            try
            {
                var type = MountUpRestoredReflection.MountableType;
                if (type == null) return false;

                var comp = go.GetComponent(type);
                if (comp == null) return false;

                var prop = type.GetProperty("IsMounted") ?? type.GetProperty("isMounted");
                if (prop != null)
                    return (bool)prop.GetValue(comp);
            }
            catch { }

            return false;
        }
    }
}
