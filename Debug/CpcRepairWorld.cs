using CreaturePrefabCreator.RuntimeModifiers;
using System.Collections.Generic;
using UnityEngine;

namespace CreaturePrefabCreator.Debug
{
    internal static class CpcRepairWorld
    {
        private static void Log(string msg) => CreaturePrefabCreatorPlugin.Instance?.Log(msg);

        // ── Cleanup ZDOs for a named prefab ───────────────────────────────────

        internal static void CleanupZdos(string prefabName, bool dryRun, bool verbose)
        {
            if (ZNetScene.instance == null) { Log("[CPC repair] ZNetScene not available."); return; }

            int hash = prefabName.GetStableHashCode();
            var allZNVs = Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None);
            var matched = new List<(ZNetView znv, string zdoId, Vector3 pos, long owner)>();

            foreach (var znv in allZNVs)
            {
                if (znv == null || znv.GetZDO() == null) continue;
                if (znv.GetZDO().GetPrefab() != hash) continue;

                long ownerId = 0L;
                try { ownerId = (long)(TryGetField(znv.GetZDO(), "m_owner") ?? 0L); } catch { }
                matched.Add((znv, znv.GetZDO().m_uid.ToString(), znv.GetZDO().GetPosition(), ownerId));
            }

            string mode = dryRun ? "[DRY-RUN]" : "[CONFIRMED]";
            Log($"{mode} cpc_repair_world --cleanup-zdos {prefabName}");
            Log($"  Prefab hash: {hash}  Matched: {matched.Count}");

            if (matched.Count == 0) { Log("  No matching ZDOs found."); return; }

            foreach (var (znv, zdoId, pos, owner) in matched)
            {
                Log($"  ZDO id={zdoId}  pos=({pos.x:F1},{pos.y:F1},{pos.z:F1})  ownerPeer={owner}  prefab={prefabName}  reason=manual-cleanup");
            }

            if (dryRun)
            {
                Log($"  {matched.Count} object(s) would be destroyed. Add --confirm to execute.");
            }
            else
            {
                int destroyed = 0;
                foreach (var (znv, zdoId, pos, _) in matched)
                {
                    if (znv == null || znv.gameObject == null) continue;
                    ZNetScene.instance.Destroy(znv.gameObject);
                    if (verbose) Log($"  Destroyed: id={zdoId}  pos=({pos.x:F1},{pos.y:F1},{pos.z:F1})");
                    destroyed++;
                }
                Log($"  Destroyed {destroyed} object(s).");
            }
        }

        // ── Orphan ZDO cleanup ────────────────────────────────────────────────

        internal static void CleanupOrphans(bool dryRun, bool verbose)
        {
            if (ZNetScene.instance == null) { Log("[CPC repair] ZNetScene not available."); return; }

            var allZNVs = Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None);
            var orphans = new List<(ZNetView znv, string zdoId, Vector3 pos, int hash)>();

            foreach (var znv in allZNVs)
            {
                if (znv == null || znv.GetZDO() == null) continue;
                int hash = znv.GetZDO().GetPrefab();
                if (hash == 0) continue;

                var prefab = ZNetScene.instance.GetPrefab(hash);
                if (prefab == null)
                    orphans.Add((znv, znv.GetZDO().m_uid.ToString(), znv.GetZDO().GetPosition(), hash));
            }

            string mode = dryRun ? "[DRY-RUN]" : "[CONFIRMED]";
            Log($"{mode} cpc_repair_world --orphans");
            Log($"  Orphaned ZDOs found: {orphans.Count}");

            if (orphans.Count == 0) { Log("  No orphans found."); return; }

            foreach (var (_, zdoId, pos, hash) in orphans)
                Log($"  Orphan: id={zdoId}  pos=({pos.x:F1},{pos.y:F1},{pos.z:F1})  prefabHash={hash}  reason=no-registered-prefab");

            if (dryRun)
            {
                Log($"  {orphans.Count} orphan(s) would be removed. Add --confirm to execute.");
            }
            else
            {
                int removed = 0;
                foreach (var (znv, zdoId, pos, hash) in orphans)
                {
                    if (znv == null || znv.gameObject == null) continue;
                    ZNetScene.instance.Destroy(znv.gameObject);
                    if (verbose) Log($"  Removed orphan: id={zdoId}  hash={hash}");
                    removed++;
                }
                Log($"  Removed {removed} orphan(s).");
            }
        }

        // ── Restore CPC runtime AI states ─────────────────────────────────────

        internal static void RestoreRuntime(bool verbose)
        {
            var (restored, skipped, stale) = RuntimeModifierManager.RestoreAllRuntimeAI();
            Log("[CPC repair] cpc_repair_world --restore-runtime");
            Log($"  Restored: {restored}  Skipped (not owner): {skipped}  Stale entries removed: {stale}");
            if (restored == 0 && skipped == 0 && stale == 0)
                Log("  No CPC runtime-disabled creatures found.");
        }

        // ── Force grow nearby offspring ───────────────────────────────────────

        internal static void ForceGrow(bool dryRun, float radius)
        {
            if (Player.m_localPlayer == null) { Log("[CPC repair] Player not available."); return; }
            Vector3 center = Player.m_localPlayer.transform.position;
            var growups = Object.FindObjectsByType<GeneratedPrefabs.OffspringGrowup>(FindObjectsSortMode.None);

            var eligible = new List<(GeneratedPrefabs.OffspringGrowup g, string prefab)>();
            foreach (var g in growups)
            {
                if (g == null) continue;
                if (Vector3.Distance(g.transform.position, center) > radius) continue;
                var znv = g.GetComponent<ZNetView>();
                if (znv == null || znv.GetZDO() == null) continue;
                eligible.Add((g, CpcDiagnosticEngine.NormalizeName(g.gameObject.name)));
            }

            string mode = dryRun ? "[DRY-RUN]" : "[CONFIRMED]";
            Log($"{mode} cpc_repair_world --force-grow  radius={radius}m  found={eligible.Count}");

            if (eligible.Count == 0) { Log("  No eligible offspring found nearby."); return; }

            foreach (var (g, prefab) in eligible)
                Log($"  Offspring: {prefab}  pos=({g.transform.position.x:F1},{g.transform.position.y:F1},{g.transform.position.z:F1})");

            if (dryRun)
            {
                Log($"  {eligible.Count} offspring would be force-grown. Add --confirm to execute.");
            }
            else
            {
                int count = 0;
                foreach (var (g, _) in eligible)
                {
                    var znv = g.GetComponent<ZNetView>();
                    if (znv == null || znv.GetZDO() == null) continue;
                    znv.GetZDO().Set(GeneratedPrefabs.OffspringGrowup.GrowTriggeredKey.GetStableHashCode(), false);
                    long pastTime = (ZNet.instance?.GetTime() ?? System.DateTime.UtcNow).AddDays(-1).Ticks;
                    znv.GetZDO().Set(GeneratedPrefabs.OffspringGrowup.BirthTimeKey.GetStableHashCode(), pastTime);
                    count++;
                }
                Log($"  Forced growth on {count} offspring. They will grow on next update.");
            }
        }

        private static object TryGetField(object instance, string fieldName)
        {
            if (instance == null) return null;
            try
            {
                var t = instance.GetType();
                System.Reflection.FieldInfo fi = null;
                while (fi == null && t != null)
                {
                    fi = t.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    t = t.BaseType;
                }
                return fi?.GetValue(instance);
            }
            catch { return null; }
        }
    }
}
