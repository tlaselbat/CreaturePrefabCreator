using UnityEngine;

namespace CreaturePrefabCreator.Compatibility.MountUpRestored
{
    public static class MountUpRestoredAISafety
    {
        public static bool IsSafeToModifyAI(GameObject go)
        {
            if (go == null) return false;
            if (!MountUpRestoredCompat.IsAvailable) return true;

            try
            {
                if (MountUpRestoredInterop.IsMounted(go)) return false;
            }
            catch { }

            return true;
        }
    }
}
