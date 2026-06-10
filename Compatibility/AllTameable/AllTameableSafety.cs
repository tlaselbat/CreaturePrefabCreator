using UnityEngine;

namespace CreaturePrefabCreator.Compatibility.AllTameable
{
    public static class AllTameableSafety
    {
        public static bool IsSafeToModify(GameObject go)
        {
            if (go == null) return false;
            return true;
        }
    }
}
