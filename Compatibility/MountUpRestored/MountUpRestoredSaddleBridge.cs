using UnityEngine;

namespace CreaturePrefabCreator.Compatibility.MountUpRestored
{
    public static class MountUpRestoredSaddleBridge
    {
        public static bool HasSaddle(GameObject go)
        {
            if (!MountUpRestoredCompat.IsAvailable) return false;
            if (go == null) return false;

            try
            {
                var type = MountUpRestoredReflection.MountableType;
                if (type == null) return false;
                return go.GetComponent(type) != null;
            }
            catch { }

            return false;
        }
    }
}
