using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace CreaturePrefabCreator.Patches
{
    /// <summary>
    /// Fixes death ragdoll scale for all creatures whose root transform has been scaled
    /// or whose death effects have been replaced with an adult ragdoll that needs resizing.
    ///
    /// Design:
    ///   - Registry: PrefabOverrideManager registers an optional deathEffectScaleMultiplier
    ///     per prefab base-name. RagdollScalePatch owns and exposes this registry.
    ///   - Patch: Character.OnDeath Prefix captures dying creature context (scale, extra
    ///     multiplier, snapshot of existing ragdoll IDs) keyed by instance ID. Postfix
    ///     identifies the newly spawned Ragdoll via snapshot-diff and applies the final scale.
    ///     Instance-keyed to handle multiple simultaneous deaths.
    ///   - The identity-scale (Vector3.one) early-out is intentionally absent: a 1.0× creature
    ///     with a copied adult ragdoll still needs the extra multiplier applied.
    /// </summary>
    [HarmonyPatch(typeof(Character), "OnDeath")]
    public static class RagdollScalePatch
    {
        // ── Registry ────────────────────────────────────────────────────────────────

        private static readonly Dictionary<string, float> _deathEffectMultipliers =
            new Dictionary<string, float>();

        /// <summary>
        /// Called by PrefabOverrideManager after applying an override.
        /// Stores an extra scale multiplier applied to the ragdoll at death time.
        /// null / 0 / 1.0 = remove entry (no-op at death).
        /// Out-of-range values are warned and skipped.
        /// </summary>
        public static void RegisterDeathEffectScaleMultiplier(string prefabName, float? multiplier)
        {
            if (string.IsNullOrEmpty(prefabName)) return;

            if (!multiplier.HasValue || multiplier.Value == 0f || multiplier.Value == 1f)
            {
                _deathEffectMultipliers.Remove(prefabName);
                return;
            }

            float v = multiplier.Value;
            if (v < 0.01f || v > 100f)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                    $"[RagdollScale] RegisterDeathEffectScaleMultiplier: value {v} for '{prefabName}' is out of range (0.01–100). Skipping.");
                return;
            }

            if (v > 10f)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                    $"[RagdollScale] RegisterDeathEffectScaleMultiplier: value {v} for '{prefabName}' is unusually large (>10). Registering anyway.");
            }

            _deathEffectMultipliers[prefabName] = v;
        }

        /// <summary>Returns the registered extra multiplier, or 1.0 if none.</summary>
        public static float GetDeathEffectScaleMultiplier(string prefabName)
        {
            if (!string.IsNullOrEmpty(prefabName) &&
                _deathEffectMultipliers.TryGetValue(prefabName, out float v))
                return v;
            return 1f;
        }

        // ── Instance-keyed death context ─────────────────────────────────────────

        private struct DeathScaleContext
        {
            public Vector3 Scale;
            public string PrefabBaseName;
            public float ExtraMultiplier;
            public HashSet<int> RagdollSnapshotIds;
        }

        private static readonly Dictionary<int, DeathScaleContext> PendingDeaths =
            new Dictionary<int, DeathScaleContext>();

        // ── Harmony patches ───────────────────────────────────────────────────────

        static void Prefix(Character __instance)
        {
            if (__instance == null) return;

            string baseName = __instance.name.Replace("(Clone)", "").Trim();
            float extra = GetDeathEffectScaleMultiplier(baseName);

            // Snapshot all existing ragdoll instance IDs so Postfix can identify the newly spawned one.
            Ragdoll[] existing = UnityEngine.Object.FindObjectsByType<Ragdoll>(FindObjectsSortMode.None);
            var snapshot = new HashSet<int>();
            foreach (var r in existing)
                if (r != null) snapshot.Add(r.GetInstanceID());

            PendingDeaths[__instance.GetInstanceID()] = new DeathScaleContext
            {
                Scale = __instance.transform.localScale,
                PrefabBaseName = baseName,
                ExtraMultiplier = extra,
                RagdollSnapshotIds = snapshot
            };
        }

        static void Postfix(Character __instance)
        {
            if (__instance == null) return;

            int id = __instance.GetInstanceID();
            if (!PendingDeaths.TryGetValue(id, out DeathScaleContext ctx))
                return;

            PendingDeaths.Remove(id);

            // Find the Ragdoll that was not present before this creature died (snapshot diff).
            Ragdoll[] all = UnityEngine.Object.FindObjectsByType<Ragdoll>(FindObjectsSortMode.None);
            Ragdoll newRagdoll = null;
            foreach (var r in all)
            {
                if (r == null) continue;
                if (!ctx.RagdollSnapshotIds.Contains(r.GetInstanceID()))
                {
                    newRagdoll = r;
                    break;
                }
            }

            if (newRagdoll == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[RagdollScale] '{ctx.PrefabBaseName}' died but no new ragdoll found.");
                return;
            }

            Vector3 targetScale = ctx.Scale * ctx.ExtraMultiplier;

            if (Vector3.Distance(newRagdoll.transform.localScale, targetScale) < 0.001f)
                return;

            newRagdoll.transform.localScale = targetScale;
            CreaturePrefabCreatorPlugin.Instance?.Log(
                $"[RagdollScale] '{ctx.PrefabBaseName}' ragdoll scale set to {targetScale} " +
                $"(creatureScale={ctx.Scale}, extraMultiplier={ctx.ExtraMultiplier}).");
        }
    }
}
