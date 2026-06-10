namespace CreaturePrefabCreator.Compatibility.Jotunn
{
    public static class JotunnCompat
    {
        public static bool IsAvailable => SoftDependencyDetector.IsJotunnLoaded;
    }
}
