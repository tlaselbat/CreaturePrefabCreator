using System.Collections.Generic;
using UnityEngine;

namespace CreaturePrefabCreator.Core
{
    /// <summary>
    /// Centralized cache for original prefab baseline values.
    /// 
    /// CRITICAL SAFETY RULES:
    /// 1. Baselines are captured ONCE from clean vanilla prefabs during initial boot
    /// 2. Baselines are NEVER recaptured during reload - this would compound mutations
    /// 3. Baselines are preserved across config reloads
    /// 4. Baselines are only cleared on true world unload
    /// 
    /// This prevents the "scale accumulation after reload" bug where repeated
    /// cpc_reload_config calls would compound scale values.
    /// </summary>
    public static class PrefabBaselineCache
    {
        // Original prefab localScale values captured from clean vanilla prefabs
        private static readonly Dictionary<string, Vector3> OriginalScales = new Dictionary<string, Vector3>();

        // Tracks which baselines have been captured to avoid duplicate logging
        private static readonly HashSet<string> CapturedBaselines = new HashSet<string>();

        /// <summary>
        /// True if any baselines have been captured this session.
        /// </summary>
        public static bool HasCapturedBaselines => OriginalScales.Count > 0;

        /// <summary>
        /// Number of prefabs with captured baselines.
        /// </summary>
        public static int CapturedCount => OriginalScales.Count;

        /// <summary>
        /// Captures the original localScale from a prefab.
        /// This should ONLY be called during initial boot, before any mutations.
        /// Will not overwrite existing entries (prevents accidental recapture).
        /// </summary>
        /// <param name="prefabName">The prefab name/identifier</param>
        /// <param name="prefab">The GameObject to capture from</param>
        /// <returns>True if baseline was captured, false if already exists or prefab is null</returns>
        public static bool CaptureOriginalScale(string prefabName, GameObject prefab)
        {
            if (string.IsNullOrEmpty(prefabName) || prefab == null)
                return false;

            // CRITICAL: Never overwrite existing baselines - prevents recapture of mutated values
            if (OriginalScales.ContainsKey(prefabName))
                return false;

            Vector3 originalScale = prefab.transform.localScale;
            OriginalScales[prefabName] = originalScale;
            CapturedBaselines.Add(prefabName);

            // Log once per unique prefab to avoid spam
            CreaturePrefabCreatorPlugin.Instance?.Log(
                $"[PrefabBaselineCache] Captured original scale for '{prefabName}': {originalScale}"
            );

            return true;
        }

        /// <summary>
        /// Captures original scale from a prefab if it exists in ZNetScene.
        /// Safe to call multiple times - only captures if not already cached.
        /// </summary>
        /// <param name="prefabName">The prefab name to look up in ZNetScene</param>
        /// <returns>True if captured or already exists, false if prefab not found</returns>
        public static bool CaptureOriginalScaleFromZNetScene(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return false;

            // Already captured - don't recapture
            if (OriginalScales.ContainsKey(prefabName))
                return true;

            if (ZNetScene.instance == null)
                return false;

            GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null)
                return false;

            return CaptureOriginalScale(prefabName, prefab);
        }

        /// <summary>
        /// Gets the original scale for a prefab.
        /// Returns Vector3.one if no baseline was captured (safe fallback).
        /// </summary>
        /// <param name="prefabName">The prefab name</param>
        /// <returns>Original localScale or Vector3.one if not found</returns>
        public static Vector3 GetOriginalScale(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
                return Vector3.one;

            if (OriginalScales.TryGetValue(prefabName, out Vector3 scale))
                return scale;

            // Fallback: try to capture from ZNetScene if available
            // This is a safety net but should not happen during normal operation
            if (ZNetScene.instance != null)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
                if (prefab != null)
                {
                    // Only log warning once per prefab to avoid spam
                    if (!CapturedBaselines.Contains(prefabName))
                    {
                        CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                            $"[PrefabBaselineCache] Late capture of original scale for '{prefabName}' - " +
                            "this should have been captured during boot. Using current value."
                        );
                        CapturedBaselines.Add(prefabName);
                    }
                    return prefab.transform.localScale;
                }
            }

            // Ultimate fallback
            return Vector3.one;
        }

        /// <summary>
        /// Checks if an original scale baseline exists for a prefab.
        /// </summary>
        /// <param name="prefabName">The prefab name</param>
        /// <returns>True if baseline exists</returns>
        public static bool HasOriginalScale(string prefabName)
        {
            return !string.IsNullOrEmpty(prefabName) && OriginalScales.ContainsKey(prefabName);
        }

        /// <summary>
        /// Computes final scale from original baseline and a multiplier.
        /// This is the ONLY safe way to compute scaled prefab values.
        /// </summary>
        /// <param name="prefabName">The prefab name</param>
        /// <param name="scaleMultiplier">The scale multiplier to apply</param>
        /// <returns>Final scale value</returns>
        public static Vector3 ComputeScaledValue(string prefabName, float scaleMultiplier)
        {
            Vector3 original = GetOriginalScale(prefabName);
            return original * scaleMultiplier;
        }

        /// <summary>
        /// Gets all captured baseline prefab names.
        /// Used for diagnostics and stress testing.
        /// </summary>
        /// <returns>Array of prefab names with baselines</returns>
        public static string[] GetAllCapturedPrefabNames()
        {
            string[] result = new string[OriginalScales.Count];
            OriginalScales.Keys.CopyTo(result, 0);
            return result;
        }

        /// <summary>
        /// Gets a diagnostic snapshot of all baselines.
        /// Used by stress testing and validation commands.
        /// </summary>
        /// <returns>Dictionary of prefab name to original scale</returns>
        public static Dictionary<string, Vector3> GetBaselineSnapshot()
        {
            return new Dictionary<string, Vector3>(OriginalScales);
        }

        /// <summary>
        /// Clears all captured baselines.
        /// This should ONLY be called on true world unload, NOT during reload.
        /// </summary>
        public static void ClearAll()
        {
            int count = OriginalScales.Count;
            OriginalScales.Clear();
            CapturedBaselines.Clear();

            if (count > 0)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[PrefabBaselineCache] Cleared {count} original scale baselines (world unload)."
                );
            }
        }

        /// <summary>
        /// Clears only the tracking of which baselines were logged.
        /// Preserves actual baseline values. Used during reload to re-enable logging.
        /// </summary>
        public static void ClearLogTracking()
        {
            CapturedBaselines.Clear();
        }

        /// <summary>
        /// Validates that a prefab's current scale matches expected baseline computation.
        /// Used by stress testing to detect scale drift.
        /// </summary>
        /// <param name="prefabName">The prefab name</param>
        /// <param name="expectedMultiplier">The expected scale multiplier</param>
        /// <param name="currentScale">The current scale to validate</param>
        /// <param name="tolerance">Tolerance for floating point comparison</param>
        /// <returns>True if scale matches expected value within tolerance</returns>
        public static bool ValidateScale(string prefabName, float expectedMultiplier, Vector3 currentScale, float tolerance = 0.001f)
        {
            Vector3 original = GetOriginalScale(prefabName);
            Vector3 expected = original * expectedMultiplier;

            return Mathf.Abs(currentScale.x - expected.x) < tolerance &&
                   Mathf.Abs(currentScale.y - expected.y) < tolerance &&
                   Mathf.Abs(currentScale.z - expected.z) < tolerance;
        }
    }
}
