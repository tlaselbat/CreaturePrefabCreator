namespace CreaturePrefabCreator.Compatibility.AllTameable
{
    public static class AllTameableCompat
    {
        public static bool IsAvailable => SoftDependencyDetector.IsAllTameableLoaded;
    }
}
