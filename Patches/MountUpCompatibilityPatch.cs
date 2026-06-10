using HarmonyLib;
using CreaturePrefabCreator.GeneratedPrefabs;
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace CreaturePrefabCreator.Patches
{
    /// <summary>
    /// Conditional guard patches for MountUp compatibility.
    /// MountUp is optional; these patches are only applied if MountUp is loaded.
    /// They skip MountUp logic on generated baby/offspring prefabs (which have OffspringGrowup)
    /// while leaving adult/source creatures unaffected.
    /// Retries detection with a delay because MountUp may load after CreaturePrefabCreator.
    /// </summary>
    public static class MountUpCompatibilityPatch
    {
        private static bool _mountablePatched;
        private static bool _levelEffectsPatched;
        private static bool _retryStarted;

        public static void Initialize(Harmony harmony)
        {
            bool applied = TryApplyPatches(harmony);

            if (!_mountablePatched && !_retryStarted)
            {
                _retryStarted = true;
                CreaturePrefabCreatorPlugin.Instance?.Log("MountUp not detected yet; scheduling delayed compatibility retry.");
                CreaturePrefabCreatorPlugin.Instance?.RunCoroutine(RetryApplyPatches(harmony));
            }
        }

        /// <summary>
        /// Robustly finds a type by full name across all loaded assemblies.
        /// AccessTools.TypeByName can fail for types in assemblies loaded after initial search.
        /// </summary>
        private static Type FindTypeAcrossAssemblies(string fullName)
        {
            // Fast path: AccessTools
            var type = AccessTools.TypeByName(fullName);
            if (type != null) return type;

            // Fallback: search all loaded assemblies
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(fullName, throwOnError: false);
                    if (type != null) return type;

                    // Nested search if namespace-qualified name differs
                    foreach (var t in assembly.GetTypes())
                    {
                        if (t.FullName == fullName) return t;
                    }
                }
                catch { }
            }
            return null;
        }

        private static bool TryApplyPatches(Harmony harmony)
        {
            bool anyApplied = false;

            // MountUp.Mountable guard patches
            if (!_mountablePatched)
            {
                var mountableType = FindTypeAcrossAssemblies("MountUp.Mountable");
                if (mountableType != null)
                {
                    var awakeMethod = AccessTools.Method(mountableType, "Awake");
                    if (awakeMethod != null)
                    {
                        harmony.Patch(awakeMethod, prefix: new HarmonyMethod(typeof(MountUpCompatibilityPatch), nameof(MountablePrefix)));
                        _mountablePatched = true;
                        anyApplied = true;
                    }

                    var resetMethod = AccessTools.Method(mountableType, "ResetMountPoint");
                    if (resetMethod != null)
                    {
                        harmony.Patch(resetMethod, prefix: new HarmonyMethod(typeof(MountUpCompatibilityPatch), nameof(MountablePrefix)));
                        _mountablePatched = true;
                        anyApplied = true;
                    }
                }
            }

            // LevelEffects.SetupLevelVisualization guard patch (always safe to apply)
            if (!_levelEffectsPatched)
            {
                var setupMethod = AccessTools.Method(typeof(LevelEffects), "SetupLevelVisualization");
                if (setupMethod != null)
                {
                    harmony.Patch(setupMethod, prefix: new HarmonyMethod(typeof(MountUpCompatibilityPatch), nameof(LevelEffectsPrefix)));
                    _levelEffectsPatched = true;
                    anyApplied = true;
                }
            }

            if (_mountablePatched)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log("MountUp detected; installed generated-prefab guard patches.");
            }

            return anyApplied;
        }

        private static IEnumerator RetryApplyPatches(Harmony harmony)
        {
            int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                if (_mountablePatched) yield break;

                yield return new WaitForSeconds(2f);
                TryApplyPatches(harmony);

                if (_mountablePatched)
                {
                    CreaturePrefabCreatorPlugin.Instance?.Log($"MountUp compatibility initialized after delayed retry (attempt {i + 1}/{maxRetries}).");
                    yield break;
                }
                else
                {
                    var mountableType = FindTypeAcrossAssemblies("MountUp.Mountable");
                    bool awakePatched = false, resetPatched = false;
                    if (mountableType != null)
                    {
                        awakePatched = AccessTools.Method(mountableType, "Awake") != null;
                        resetPatched = AccessTools.Method(mountableType, "ResetMountPoint") != null;
                    }
                    CreaturePrefabCreatorPlugin.Instance?.Log($"MountUp compatibility retry failed (attempt {i + 1}/{maxRetries}): Mountable={(mountableType != null)}, Awake={awakePatched}, ResetMountPoint={resetPatched}");
                }
            }

            if (!_mountablePatched)
            {
                var mountableType = FindTypeAcrossAssemblies("MountUp.Mountable");
                bool awakePatched = false, resetPatched = false;
                if (mountableType != null)
                {
                    awakePatched = AccessTools.Method(mountableType, "Awake") != null;
                    resetPatched = AccessTools.Method(mountableType, "ResetMountPoint") != null;
                }
                CreaturePrefabCreatorPlugin.Instance?.Log($"MountUp compatibility failed after delayed retries: Mountable={(mountableType != null)}, Awake={awakePatched}, ResetMountPoint={resetPatched}");
            }
        }

        static bool MountablePrefix(MonoBehaviour __instance)
        {
            if (__instance == null) return true;
            if (__instance.GetComponent<OffspringGrowup>() != null)
                return false;
            return true;
        }

        static bool LevelEffectsPrefix(LevelEffects __instance)
        {
            if (__instance == null) return true;
            if (__instance.GetComponent<OffspringGrowup>() != null)
                return false;
            return true;
        }
    }
}
