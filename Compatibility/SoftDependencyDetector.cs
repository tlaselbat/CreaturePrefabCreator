using BepInEx.Bootstrap;

namespace CreaturePrefabCreator.Compatibility
{
    public static class SoftDependencyDetector
    {
        public static bool IsLoaded(string guid)
        {
            return Chainloader.PluginInfos.ContainsKey(guid);
        }

        public static bool IsAllTameableLoaded =>
            IsLoaded(PluginGuids.AllTameable);

        public static bool IsMountUpRestoredLoaded =>
            IsLoaded(PluginGuids.MountUpRestored);

        public static bool IsJotunnLoaded =>
            IsLoaded(PluginGuids.Jotunn);
    }
}
