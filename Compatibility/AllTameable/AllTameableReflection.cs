using System;
using System.Reflection;

namespace CreaturePrefabCreator.Compatibility.AllTameable
{
    public static class AllTameableReflection
    {
        private static bool _cached;
        private static Type _tameableType;

        public static Type TameableType
        {
            get
            {
                if (!_cached) Cache();
                return _tameableType;
            }
        }

        private static void Cache()
        {
            _cached = true;
            if (!SoftDependencyDetector.IsAllTameableLoaded) return;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    _tameableType = assembly.GetType("AllTameable.Tameable", throwOnError: false);
                    if (_tameableType != null) return;
                }
                catch { }
            }
        }
    }
}
