using System;

namespace CreaturePrefabCreator.Compatibility.MountUpRestored
{
    public static class MountUpRestoredReflection
    {
        private static bool _cached;
        private static Type _mountableType;

        public static Type MountableType
        {
            get
            {
                if (!_cached) Cache();
                return _mountableType;
            }
        }

        private static void Cache()
        {
            _cached = true;
            if (!SoftDependencyDetector.IsMountUpRestoredLoaded) return;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    _mountableType = assembly.GetType("MountUp.Mountable", throwOnError: false);
                    if (_mountableType != null) return;
                }
                catch { }
            }
        }
    }
}
