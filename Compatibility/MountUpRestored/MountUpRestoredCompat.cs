namespace CreaturePrefabCreator.Compatibility.MountUpRestored
{
    public static class MountUpRestoredCompat
    {
        public static bool IsAvailable => SoftDependencyDetector.IsMountUpRestoredLoaded;
    }
}
