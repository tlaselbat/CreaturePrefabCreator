using CreaturePrefabCreator.Config;
using CreaturePrefabCreator.GeneratedPrefabs;
using CreaturePrefabCreator.Overrides;
using CreaturePrefabCreator.Patches;
using CreaturePrefabCreator.RuntimeModifiers;
using Jotunn.Entities;
using Jotunn.Managers;
using System.Collections;
using System.Reflection;
using System.Linq;
using UnityEngine;
using static CreaturePrefabCreator.GeneratedPrefabs.OffspringGrowup;

namespace CreaturePrefabCreator.Debug
{
    public static class CreaturePrefabDebugCommands
    {
        public static void Register()
        {
            try
            {
                CommandManager.Instance.AddConsoleCommand(new HelpCommand());
                CommandManager.Instance.AddConsoleCommand(new StatusCommand());
                CommandManager.Instance.AddConsoleCommand(new ReloadConfigCommand());
                CommandManager.Instance.AddConsoleCommand(new SpawnCommand());
                CommandManager.Instance.AddConsoleCommand(new PrintConsoleCommand());
                CommandManager.Instance.AddConsoleCommand(new DumpJsonCommand());
                CommandManager.Instance.AddConsoleCommand(new RepairWorldCommand());
                CreaturePrefabCreatorPlugin.Instance.Log("CPC debug console commands registered.");
            }
            catch (System.Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance.LogWarning($"Failed to register debug commands: {ex.Message}");
            }
        }

        // ── cpc_help ─────────────────────────────────────────────────────────

        private class HelpCommand : ConsoleCommand
        {
            public override string Name => "cpc_help";
            public override string Help => "CPC help. Usage: cpc_help [--command <name>]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                string cmd = p.GetOption("--command");
                CpcConsoleRenderer.RenderHelp(cmd);
            }
        }

        // ── cpc_status ───────────────────────────────────────────────────────

        private class StatusCommand : ConsoleCommand
        {
            public override string Name => "cpc_status";
            public override string Help => "CPC plugin status. Usage: cpc_status [--verbose] [--mods] [--debug-runtime] [--generated]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                CpcConsoleRenderer.RenderStatus(
                    verbose: p.HasFlag("--verbose"),
                    showMods: p.HasFlag("--mods"),
                    showRuntime: p.HasFlag("--debug-runtime"),
                    showGenerated: p.HasFlag("--generated")
                );
            }
        }

        // ── cpc_spawn ────────────────────────────────────────────────────────

        private class SpawnCommand : ConsoleCommand
        {
            public override string Name => "cpc_spawn";
            public override string Help => "Spawn a prefab. Usage: cpc_spawn --prefab <name> [--count <n>] [--level <n>] [--tamed] [--distance <m>]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                string prefabName = p.GetOption("--prefab");

                if (string.IsNullOrEmpty(prefabName))
                {
                    CreaturePrefabCreatorPlugin.Instance.Log("Usage: cpc_spawn --prefab <name> [--count <n>] [--level <n>] [--tamed] [--distance <m>]");
                    return;
                }

                if (ZNetScene.instance == null)
                {
                    CreaturePrefabCreatorPlugin.Instance.Log("[CPC spawn] ZNetScene not available.");
                    return;
                }

                GameObject prefab = ZNetScene.instance.GetPrefab(prefabName);
                if (prefab == null)
                {
                    CreaturePrefabCreatorPlugin.Instance.LogError($"[CPC spawn] Prefab '{prefabName}' not found in ZNetScene.");
                    return;
                }

                if (prefab.GetComponent<ZNetView>() == null)
                {
                    CreaturePrefabCreatorPlugin.Instance.LogError($"[CPC spawn] Prefab '{prefabName}' has no ZNetView — cannot spawn.");
                    return;
                }

                bool spawnTamed = p.HasFlag("--tamed");
                int count = 1;
                if (p.GetOption("--count") is string countStr && int.TryParse(countStr, out int c)) count = System.Math.Max(1, c);
                float distance = 3f;
                if (p.GetOption("--distance") is string distStr && float.TryParse(distStr, out float d)) distance = d;
                int level = 1;
                if (p.GetOption("--level") is string levelStr && int.TryParse(levelStr, out int lv)) level = System.Math.Max(1, lv);

                bool hasCharacter = prefab.GetComponent<Character>() != null;
                if (level > 1 && !hasCharacter)
                    CreaturePrefabCreatorPlugin.Instance.Log($"[CPC spawn] Warning: '{prefabName}' has no Character component — --level will be ignored.");

                Vector3 basePos = GetPlayerPos() + Vector3.forward * distance;

                // If spawning tamed, set m_startsTamed on the prefab Tameable before spawning.
                // SpawnObject doesn't return the instance, so synchronous pre-spawn flag is the
                // only race-proof approach (survives skiptime/instant growup in same frame).
                Tameable prefabTameable = spawnTamed ? prefab.GetComponent<Tameable>() : null;
                bool originalStartsTamed = prefabTameable != null && prefabTameable.m_startsTamed;
                if (prefabTameable != null)
                    prefabTameable.m_startsTamed = true;

                for (int i = 0; i < count; i++)
                {
                    Vector3 pos = basePos + new Vector3(i * 2f, 0f, 0f);
                    ZNetScene.instance.SpawnObject(pos, Quaternion.identity, prefab);

                    if (spawnTamed || level > 1)
                    {
                        bool capTamed = spawnTamed;
                        bool capHasCharacter = hasCharacter;
                        int capLevel = level;
                        long ownerID = Player.m_localPlayer?.GetPlayerID() ?? 0L;
                        Vector3 capPos = pos;
                        string capPrefabName = prefabName;
                        CreaturePrefabCreatorPlugin.Instance?.RunCoroutine(
                            DeferredSetTamed(capPrefabName, capPos, capTamed, capHasCharacter, capLevel, ownerID));
                    }
                }

                // Restore prefab Tameable to original state so subsequent non-tamed spawns are unaffected.
                if (prefabTameable != null)
                    prefabTameable.m_startsTamed = originalStartsTamed;

                string levelSuffix = level > 1 && hasCharacter ? $" level={level}" : "";
                string suffix = spawnTamed ? $" (tamed{levelSuffix})" : (level > 1 && hasCharacter ? $" ({levelSuffix.TrimStart()})" : "");
                CreaturePrefabCreatorPlugin.Instance.Log($"[CPC spawn] Spawned {count}x '{prefabName}'{suffix} near {basePos}.");
            }
        }

        // ── cpc_print_console ────────────────────────────────────────────────

        private class PrintConsoleCommand : ConsoleCommand
        {
            public override string Name => "cpc_print_console";
            public override string Help => "Console diagnostics. Usage: cpc_print_console <live|prefab|world-zdos> [args] [flags]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                string mode = p.Mode;

                if (string.IsNullOrEmpty(mode))
                {
                    CpcCommandRouter.PrintMissingMode("cpc_print_console", new[] { "live", "prefab", "world-zdos" });
                    return;
                }

                switch (mode)
                {
                    case "live":
                        RunLive(p);
                        break;
                    case "prefab":
                        RunPrefab(p);
                        break;
                    case "world-zdos":
                        RunWorldZdos(p);
                        break;
                    default:
                        CpcCommandRouter.PrintUnknownMode("cpc_print_console", mode, new[] { "live", "prefab", "world-zdos" });
                        break;
                }
            }

            private static void RunLive(CpcCommandRouter.ParsedArgs p)
            {
                bool verbose = p.HasFlag("--verbose");
                bool ai = p.HasFlag("--ai");
                bool runtime = p.HasFlag("--debug-runtime");
                bool mountup = p.HasFlag("--debug-mountup");
                bool alltameable = p.HasFlag("--debug-alltameable");
                bool zdo = p.HasFlag("--zdo");
                bool generated = p.HasFlag("--generated");

                if (p.HasFlag("--target") && p.Positional.Count > 0)
                {
                    CreaturePrefabCreatorPlugin.Instance.Log("[CPC] Error: --target and a radius value are mutually exclusive. Use either --target OR a numeric radius, not both.");
                    CreaturePrefabCreatorPlugin.Instance.Log("[CPC] Use 'cpc_help --command cpc_print_console' for usage.");
                    return;
                }

                if (p.HasFlag("--target"))
                {
                    var ch = CpcDiagnosticEngine.ResolveTargetCharacter(out string err);
                    if (ch == null) { CreaturePrefabCreatorPlugin.Instance.Log($"[CPC] {err}"); return; }
                    var report = CpcDiagnosticEngine.BuildLiveTargetReport(ch, ai, runtime, mountup, alltameable, zdo, generated);
                    CpcConsoleRenderer.RenderLiveTarget(report, verbose);
                }
                else
                {
                    CpcCommandRouter.TryParseRadius(p, 20f, out float radius);
                    var report = CpcDiagnosticEngine.BuildRadiusReport(radius, runtime);
                    CpcConsoleRenderer.RenderRadius(report, verbose);
                }
            }

            private static void RunPrefab(CpcCommandRouter.ParsedArgs p)
            {
                bool verbose = p.HasFlag("--verbose");

                if (p.HasFlag("--verify-generated"))
                {
                    var report = CpcDiagnosticEngine.BuildGeneratedVerifyReport(p.HasFlag("--leaks"));
                    CpcConsoleRenderer.RenderPrefab(report, verbose);
                    return;
                }
                if (p.HasFlag("--list-generated"))
                {
                    var report = CpcDiagnosticEngine.BuildGeneratedListReport();
                    CpcConsoleRenderer.RenderPrefab(report, verbose);
                    return;
                }
                if (p.Options.ContainsKey("--find"))
                {
                    var report = CpcDiagnosticEngine.BuildPrefabFindReport(p.GetOption("--find"));
                    CpcConsoleRenderer.RenderPrefab(report, verbose);
                    return;
                }
                if (p.Options.ContainsKey("--compare"))
                {
                    string nameA = p.GetOption("--compare");
                    string nameB = p.Positional.Count > 0 ? p.Positional[0] : null;
                    if (string.IsNullOrEmpty(nameB)) { CreaturePrefabCreatorPlugin.Instance.Log("[CPC] --compare requires two prefab names."); return; }
                    var report = CpcDiagnosticEngine.BuildPrefabCompareReport(nameA, nameB);
                    CpcConsoleRenderer.RenderPrefab(report, verbose);
                    return;
                }
                if (p.Options.ContainsKey("--name"))
                {
                    bool chain = p.HasFlag("--chain");
                    bool gen = p.HasFlag("--generated");
                    bool overrides = p.HasFlag("--overrides");
                    var report = CpcDiagnosticEngine.BuildPrefabReport(p.GetOption("--name"), gen, overrides, chain);
                    CpcConsoleRenderer.RenderPrefab(report, verbose);
                    return;
                }

                CreaturePrefabCreatorPlugin.Instance.Log("[CPC] cpc_print_console prefab requires --name, --find, --compare, --list-generated, or --verify-generated.");
            }

            private static void RunWorldZdos(CpcCommandRouter.ParsedArgs p)
            {
                bool verbose = p.HasFlag("--verbose");
                string prefabName = p.Positional.Count > 0 ? p.Positional[0] : null;
                if (string.IsNullOrEmpty(prefabName)) { CreaturePrefabCreatorPlugin.Instance.Log("[CPC] world-zdos requires a prefab name."); return; }
                var report = CpcDiagnosticEngine.BuildWorldZdoReport(prefabName);
                CpcConsoleRenderer.RenderWorldZdos(report, verbose);
            }
        }

        // ── cpc_dump_json ────────────────────────────────────────────────────

        private class DumpJsonCommand : ConsoleCommand
        {
            public override string Name => "cpc_dump_json";
            public override string Help => "Dump JSON diagnostics. Usage: cpc_dump_json <live|prefab|world-zdos> [args] [flags] [--output <file>]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                string mode = p.Mode;

                if (string.IsNullOrEmpty(mode))
                {
                    CpcCommandRouter.PrintMissingMode("cpc_dump_json", new[] { "live", "prefab", "world-zdos" });
                    return;
                }

                string output = p.GetOption("--output");

                switch (mode)
                {
                    case "live":
                        RunLive(p, output);
                        break;
                    case "prefab":
                        RunPrefab(p, output);
                        break;
                    case "world-zdos":
                        RunWorldZdos(p, output);
                        break;
                    default:
                        CpcCommandRouter.PrintUnknownMode("cpc_dump_json", mode, new[] { "live", "prefab", "world-zdos" });
                        break;
                }
            }

            private static void RunLive(CpcCommandRouter.ParsedArgs p, string output)
            {
                bool ai = p.HasFlag("--ai");
                bool runtime = p.HasFlag("--debug-runtime");
                bool mountup = p.HasFlag("--debug-mountup");
                bool alltameable = p.HasFlag("--debug-alltameable");
                bool zdo = p.HasFlag("--zdo");
                bool generated = p.HasFlag("--generated");

                if (p.HasFlag("--target") && p.Positional.Count > 0)
                {
                    CreaturePrefabCreatorPlugin.Instance.Log("[CPC] Error: --target and a radius value are mutually exclusive. Use either --target OR a numeric radius, not both.");
                    CreaturePrefabCreatorPlugin.Instance.Log("[CPC] Use 'cpc_help --command cpc_dump_json' for usage.");
                    return;
                }

                if (p.HasFlag("--target"))
                {
                    var ch = CpcDiagnosticEngine.ResolveTargetCharacter(out string err);
                    if (ch == null) { CreaturePrefabCreatorPlugin.Instance.Log($"[CPC] {err}"); return; }
                    var report = CpcDiagnosticEngine.BuildLiveTargetReport(ch, ai, runtime, mountup, alltameable, zdo, generated);
                    CpcJsonRenderer.WriteLiveTarget(report, output);
                }
                else
                {
                    CpcCommandRouter.TryParseRadius(p, 20f, out float radius);
                    var report = CpcDiagnosticEngine.BuildRadiusReport(radius, runtime);
                    CpcJsonRenderer.WriteRadius(report, output);
                }
            }

            private static void RunPrefab(CpcCommandRouter.ParsedArgs p, string output)
            {
                if (p.HasFlag("--verify-generated"))
                {
                    var report = CpcDiagnosticEngine.BuildGeneratedVerifyReport(p.HasFlag("--leaks"));
                    CpcJsonRenderer.WritePrefab(report, output);
                    return;
                }
                if (p.HasFlag("--list-generated"))
                {
                    var report = CpcDiagnosticEngine.BuildGeneratedListReport();
                    CpcJsonRenderer.WritePrefab(report, output);
                    return;
                }
                if (p.Options.ContainsKey("--find"))
                {
                    var report = CpcDiagnosticEngine.BuildPrefabFindReport(p.GetOption("--find"));
                    CpcJsonRenderer.WritePrefab(report, output);
                    return;
                }
                if (p.Options.ContainsKey("--compare"))
                {
                    string nameA = p.GetOption("--compare");
                    string nameB = p.Positional.Count > 0 ? p.Positional[0] : null;
                    if (string.IsNullOrEmpty(nameB)) { CreaturePrefabCreatorPlugin.Instance.Log("[CPC] --compare requires two prefab names."); return; }
                    var report = CpcDiagnosticEngine.BuildPrefabCompareReport(nameA, nameB);
                    CpcJsonRenderer.WritePrefab(report, output);
                    return;
                }
                if (p.Options.ContainsKey("--name"))
                {
                    bool chain = p.HasFlag("--chain");
                    bool gen = p.HasFlag("--generated");
                    bool overrides = p.HasFlag("--overrides");
                    var report = CpcDiagnosticEngine.BuildPrefabReport(p.GetOption("--name"), gen, overrides, chain);
                    CpcJsonRenderer.WritePrefab(report, output);
                    return;
                }

                CreaturePrefabCreatorPlugin.Instance.Log("[CPC] cpc_dump_json prefab requires --name, --find, --compare, --list-generated, or --verify-generated.");
            }

            private static void RunWorldZdos(CpcCommandRouter.ParsedArgs p, string output)
            {
                string prefabName = p.Positional.Count > 0 ? p.Positional[0] : null;
                if (string.IsNullOrEmpty(prefabName)) { CreaturePrefabCreatorPlugin.Instance.Log("[CPC] world-zdos requires a prefab name."); return; }
                var report = CpcDiagnosticEngine.BuildWorldZdoReport(prefabName);
                CpcJsonRenderer.WriteWorldZdos(report, output);
            }
        }

        // ── cpc_repair_world ─────────────────────────────────────────────────

        private class RepairWorldCommand : ConsoleCommand
        {
            public override string Name => "cpc_repair_world";
            public override string Help => "World repair. Usage: cpc_repair_world --cleanup-zdos <prefab> | --orphans | --restore-runtime | --force-grow [--dry-run|--confirm] [--verbose]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                bool dryRun = !p.HasFlag("--confirm");
                bool verbose = p.HasFlag("--verbose");

                if (p.Options.ContainsKey("--cleanup-zdos"))
                {
                    string prefab = p.GetOption("--cleanup-zdos");
                    if (string.IsNullOrEmpty(prefab)) { CreaturePrefabCreatorPlugin.Instance.Log("[CPC repair] --cleanup-zdos requires a prefab name."); return; }
                    CpcRepairWorld.CleanupZdos(prefab, dryRun, verbose);
                }
                else if (p.HasFlag("--orphans"))
                {
                    CpcRepairWorld.CleanupOrphans(dryRun, verbose);
                }
                else if (p.HasFlag("--restore-runtime"))
                {
                    CpcRepairWorld.RestoreRuntime(verbose);
                }
                else if (p.HasFlag("--force-grow"))
                {
                    CpcCommandRouter.TryParseRadius(p, 50f, out float radius);
                    CpcRepairWorld.ForceGrow(dryRun, radius);
                }
                else
                {
                    CreaturePrefabCreatorPlugin.Instance.Log("[CPC repair] No action specified. Use cpc_help --command cpc_repair_world for usage.");
                }
            }
        }

        // ── cpc_reload_config ────────────────────────────────────────────────

        private class ReloadConfigCommand : ConsoleCommand
        {
            public override string Name => "cpc_reload_config";
            public override string Help => "Reload config. Usage: cpc_reload_config [--dry-run] [--prefabs-only] [--debug-runtime-only] [--force]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                bool dryRun = p.HasFlag("--dry-run");
                bool prefabsOnly = p.HasFlag("--prefabs-only");
                bool runtimeOnly = p.HasFlag("--debug-runtime-only");
                bool force = p.HasFlag("--force");

                var plugin = CreaturePrefabCreatorPlugin.Instance;

                if (dryRun)
                {
                    plugin.Log("[cpc_reload_config --dry-run] Validation only — no changes applied.");
                    var testConfig = CreaturePrefabCreatorConfigLoader.Reload(plugin);
                    if (testConfig == null)
                    {
                        plugin.LogError("[dry-run] Config failed to parse. Fix errors before reloading.");
                        return;
                    }
                    plugin.Log($"[dry-run] Config parsed OK. PrefabOverrides={testConfig.PrefabOverrides?.Count ?? 0}  GeneratedPrefabs={testConfig.GeneratedPrefabs?.Count ?? 0}  RuntimeModifiers={testConfig.RuntimeModifiers?.Count ?? 0}");
                    plugin.Log("[dry-run] No changes applied. Remove --dry-run to apply.");
                    return;
                }

                plugin.Log("[cpc_reload_config] Starting runtime config reload...");

                // 1. Load fresh config from disk
                var newConfig = CreaturePrefabCreatorConfigLoader.Reload(plugin);
                if (newConfig == null)
                {
                    plugin.LogError("[Reload] Config reload failed. Keeping existing config.");
                    return;
                }

                // 2. Re-apply prefab overrides
                bool doPrefabs = !runtimeOnly && (prefabsOnly || (!prefabsOnly && !runtimeOnly));
                bool doRuntime = !prefabsOnly && (runtimeOnly || (!prefabsOnly && !runtimeOnly));

                if (doPrefabs && plugin.ConfigEnablePrefabOverrides.Value)
                {
                    PrefabOverrideManager.ReapplyAll(newConfig.PrefabOverrides, plugin.ConfigEnableFactionOverrides.Value);

                    // 2b. Push AI changes to live instances for each override
                    foreach (var cfg in newConfig.PrefabOverrides)
                    {
                        if (!cfg.Enabled) continue;
                        if (cfg.DisableAI)
                        {
                            PrefabOverrideManager.ApplyToLiveInstances(cfg.TargetPrefab, go =>
                            {
                                var monsterAI = go.GetComponent<MonsterAI>();
                                if (monsterAI != null && monsterAI.enabled)
                                {
                                    monsterAI.enabled = false;
                                    if (go.GetComponent<PermanentAIDisabledMarker>() == null)
                                        go.AddComponent<PermanentAIDisabledMarker>();
                                    plugin.Log($"[Reload] Live instance '{go.name}': disabled MonsterAI (disableAI=true).");
                                }
                            });
                        }
                        else
                        {
                            // If disableAI was removed from config, re-enable AI on live instances
                            PrefabOverrideManager.ApplyToLiveInstances(cfg.TargetPrefab, go =>
                            {
                                var marker = go.GetComponent<PermanentAIDisabledMarker>();
                                if (marker != null)
                                {
                                    var monsterAI = go.GetComponent<MonsterAI>();
                                    if (monsterAI != null && !monsterAI.enabled)
                                    {
                                        monsterAI.enabled = true;
                                        plugin.Log($"[Reload] Live instance '{go.name}': re-enabled MonsterAI (disableAI removed from config).");
                                    }
                                    UnityEngine.Object.Destroy(marker);
                                }
                            });
                        }

                        if (cfg.DisableAggro)
                        {
                            PrefabOverrideManager.ApplyToLiveInstances(cfg.TargetPrefab, go =>
                            {
                                var monsterAI = go.GetComponent<MonsterAI>();
                                if (monsterAI != null) monsterAI.m_aggravatable = false;
                            });
                        }

                        if (cfg.DisableFleeing)
                        {
                            PrefabOverrideManager.ApplyToLiveInstances(cfg.TargetPrefab, go =>
                            {
                                var monsterAI = go.GetComponent<MonsterAI>();
                                if (monsterAI != null)
                                {
                                    var type = typeof(MonsterAI);
                                    var f1 = type.GetField("m_fleeIfHurtWhenTargetCantSeeTarget", BindingFlags.Public | BindingFlags.Instance);
                                    var f2 = type.GetField("m_fleeIfNotAlerted", BindingFlags.Public | BindingFlags.Instance);
                                    var f3 = type.GetField("m_fleeInLava", BindingFlags.Public | BindingFlags.Instance);
                                    var f4 = type.GetField("m_fleeRange", BindingFlags.Public | BindingFlags.Instance);
                                    if (f1 != null) f1.SetValue(monsterAI, false);
                                    if (f2 != null) f2.SetValue(monsterAI, false);
                                    if (f3 != null) f3.SetValue(monsterAI, false);
                                    if (f4 != null) f4.SetValue(monsterAI, 0f);
                                }
                            });
                        }

                        if (cfg.FriendAttacked.HasValue)
                        {
                            PrefabOverrideManager.ApplyToLiveInstances(cfg.TargetPrefab, go =>
                            {
                                var monsterAI = go.GetComponent<MonsterAI>();
                                if (monsterAI != null)
                                {
                                    var field = typeof(MonsterAI).GetField("m_friendAttacked", BindingFlags.Public | BindingFlags.Instance);
                                    if (field != null) field.SetValue(monsterAI, cfg.FriendAttacked.Value);
                                }
                            });
                        }
                    }
                }
                else if (doPrefabs)
                {
                    plugin.Log("[cpc_reload_config] Prefab Overrides are disabled by FeatureSafety.EnablePrefabOverrides");
                }

                // 3. Re-register generated prefabs
                if (doPrefabs && plugin.ConfigEnableGeneratedPrefabs.Value)
                {
                    GeneratedPrefabManager.ReregisterAll(newConfig.GeneratedPrefabs, plugin.ConfigEnableFactionOverrides.Value);
                }
                else if (doPrefabs)
                {
                    plugin.Log("[cpc_reload_config] Generated Prefabs are disabled by FeatureSafety.EnableGeneratedPrefabs");
                }

                // 4. Re-initialize runtime modifiers
                if (doRuntime && plugin.ConfigEnableRuntimeModifiers.Value)
                {
                    RuntimeModifierManager.ClearAll();
                    RuntimeModifierManager.Initialize(newConfig.RuntimeModifiers);
                    plugin.Log($"[cpc_reload_config] Runtime modifiers reloaded: {newConfig.RuntimeModifiers?.Count ?? 0} rules.");
                }
                else if (doRuntime)
                {
                    plugin.Log("[cpc_reload_config] Runtime Modifiers are disabled by FeatureSafety.EnableRuntimeModifiers");
                }

                // 5. Update loaded config reference
                plugin.LoadedConfig = newConfig;
                string scope = prefabsOnly ? " (prefabs only)" : runtimeOnly ? " (runtime only)" : "";
                plugin.Log($"[cpc_reload_config] Reload complete{scope}. PrefabOverrides={newConfig.PrefabOverrides?.Count ?? 0}  GeneratedPrefabs={newConfig.GeneratedPrefabs?.Count ?? 0}  RuntimeModifiers={newConfig.RuntimeModifiers?.Count ?? 0}");
                plugin.Log("[cpc_reload_config] Note: scale/tint/glow/faction changes only affect NEW spawns. AI changes applied to live instances.");
            }
        }

        private static IEnumerator DeferredSetTamed(string prefabName, Vector3 spawnPos, bool tamed, bool hasCharacter, int level, long ownerID)
        {
            yield return null;
            yield return null;

            CreaturePrefabCreatorPlugin.Instance?.Log(
                $"[DeferredSetTamed] Searching for '{prefabName}' near {spawnPos} tamed={tamed}");

            var allZNVs = Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None);
            bool found = false;
            foreach (var znv in allZNVs)
            {
                if (znv == null || znv.GetZDO() == null) continue;
                if (!znv.gameObject.name.StartsWith(prefabName)) continue;
                if (Vector3.Distance(znv.transform.position, spawnPos) > 5f) continue;

                found = true;
                var zdo = znv.GetZDO();
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[DeferredSetTamed] Found '{znv.gameObject.name}' at {znv.transform.position}");

                if (tamed)
                {
                    // Write CPC-owned key for persistence across respawns/reloads.
                    zdo.Set(PreserveTamedHash, true);
                    zdo.Set("creator".GetStableHashCode(), ownerID);
                    // Also call SetTamed directly — Tameable.Awake has already fired so
                    // TameableAwakePatch won't fire again for this instance.
                    var character = znv.GetComponent<Character>();
                    if (character != null)
                    {
                        bool wasTamed = character.IsTamed();
                        if (!wasTamed)
                            character.SetTamed(true);
                        CreaturePrefabCreatorPlugin.Instance?.Log(
                            $"[DeferredSetTamed] SetTamed on '{znv.gameObject.name}': wasTamed={wasTamed}, IsTamed after={character.IsTamed()}");
                    }
                    else
                    {
                        CreaturePrefabCreatorPlugin.Instance?.Log(
                            $"[DeferredSetTamed] '{znv.gameObject.name}' has no Character component — SetTamed skipped.");
                    }
                }
                if (level > 1 && hasCharacter)
                    zdo.Set("level".GetStableHashCode(), level);
                break;
            }

            if (!found)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[DeferredSetTamed] WARN: No '{prefabName}' found within 5m of {spawnPos} after 2 frames.");
            }
        }

        private static Vector3 GetPlayerPos()
        {
            var player = Player.m_localPlayer;
            if (player != null)
                return player.transform.position;
            return Vector3.zero;
        }
    }
}
