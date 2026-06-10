namespace CreaturePrefabCreator.Compatibility
{
    public static class CompatibilityManager
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            var plugin = CreaturePrefabCreatorPlugin.Instance;

            if (SoftDependencyDetector.IsAllTameableLoaded)
                plugin?.Log("[Compatibility] AllTameable_TamingOverhaul detected.");
            else
                plugin?.Log("[Compatibility] AllTameable_TamingOverhaul not loaded.");

            if (SoftDependencyDetector.IsMountUpRestoredLoaded)
                plugin?.Log("[Compatibility] MountUpRestored detected.");
            else
                plugin?.Log("[Compatibility] MountUpRestored not loaded.");
        }
    }
}
