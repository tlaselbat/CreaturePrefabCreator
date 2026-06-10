using Jotunn.Managers;
using UnityEngine;

namespace CreaturePrefabCreator.Compatibility.Jotunn
{
    public static class JotunnPrefabInterop
    {
        public static GameObject GetPrefab(string name)
        {
            return PrefabManager.Instance?.GetPrefab(name);
        }
    }
}
