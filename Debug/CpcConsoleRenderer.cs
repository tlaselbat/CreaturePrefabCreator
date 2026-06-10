using System.Collections.Generic;

namespace CreaturePrefabCreator.Debug
{
    internal static class CpcConsoleRenderer
    {
        private static void Log(string msg) => CreaturePrefabCreatorPlugin.Instance?.Log(msg);

        // ── Live target ──────────────────────────────────────────────────────

        internal static void RenderLiveTarget(CpcLiveDiagnosticReport report, bool verbose)
        {
            if (report == null) { Log("[CPC] No diagnostic data."); return; }

            Log("=== CPC Live Diagnostic ===");
            RenderIdentity(report.Identity);

            if (report.AI != null) RenderAI(report.AI, verbose);
            if (report.Zdo != null) RenderZdo(report.Zdo);
            if (report.Runtime != null) RenderRuntime(report.Runtime, verbose);
            if (report.MountUp != null) RenderMountUp(report.MountUp, verbose);
            if (report.AllTameable != null) RenderAllTameable(report.AllTameable);
            if (report.Generated != null) RenderGeneratedLive(report.Generated);

            RenderWarnings(report.Warnings);
            RenderSuggestions(report.SuggestedCommands);
        }

        // ── Radius ───────────────────────────────────────────────────────────

        internal static void RenderRadius(CpcRadiusDiagnosticReport report, bool verbose)
        {
            if (report == null) { Log("[CPC] No data."); return; }

            Log($"=== CPC Live Radius ({report.Radius}m) ===");
            Log($"  Found: {report.Entries.Count} creature(s)");

            if (!verbose)
            {
                foreach (var e in report.Entries)
                {
                    string flags = "";
                    if (e.HasTameable) flags += " [tamed?]";
                    if (e.HasOffspringGrowup) flags += " [offspring]";
                    if (e.RuntimeAIDisabled) flags += " [AI-off]";
                    Log($"  {e.Identity.PrefabName}  dist={e.Identity.DistanceFromPlayer:F1}m{flags}");
                }
            }
            else
            {
                foreach (var e in report.Entries)
                {
                    Log($"\n  [{e.Identity.PrefabName}]");
                    Log($"    dist={e.Identity.DistanceFromPlayer:F1}m  pos={FormatVec3(e.Identity.Position)}");
                    Log($"    zdoid={e.Identity.ZdoId}  owner={e.Identity.IsOwner}");
                    Log($"    MonsterAI={e.HasMonsterAI}  AnimalAI={e.HasAnimalAI}  Tameable={e.HasTameable}  Offspring={e.HasOffspringGrowup}  RuntimeAIDisabled={e.RuntimeAIDisabled}");
                }
            }

            if (report.RuntimeSummary != null)
                RenderRuntimeSummaryLine(report.RuntimeSummary);

            RenderWarnings(report.Warnings);
            RenderSuggestions(report.SuggestedCommands);
        }

        // ── Prefab ───────────────────────────────────────────────────────────

        internal static void RenderPrefab(CpcPrefabDiagnosticReport report, bool verbose)
        {
            if (report == null) { Log("[CPC] No data."); return; }

            Log($"=== CPC Prefab: {report.QueryType} '{report.QueryValue}' ===");

            if (report.VerifyReport != null)
            {
                RenderVerify(report.VerifyReport, verbose);
            }
            else
            {
                foreach (var info in report.Results)
                    RenderPrefabInfo(info, verbose);
            }

            RenderWarnings(report.Warnings);
            RenderSuggestions(report.SuggestedCommands);
        }

        private static void RenderPrefabInfo(CpcPrefabInfo info, bool verbose)
        {
            Log($"\n  Prefab: {info.PrefabName}");
            Log($"  Scale: {FormatVec3(info.Scale)}  Character={info.HasCharacter}  ZNetView={info.HasZNetView}");
            Log($"  BaseAI={info.HasBaseAI}  MonsterAI={info.HasMonsterAI}  AnimalAI={info.HasAnimalAI}  Tameable={info.HasTameable}  OffspringGrowup={info.HasOffspringGrowup}");

            if (info.GeneratedEntry != null)
            {
                Log($"  [Generated]");
                Log($"    source={info.GeneratedEntry.SourcePrefab}  adult={info.GeneratedEntry.AdultPrefab}  enabled={info.GeneratedEntry.Enabled}  registered={info.GeneratedEntry.RegisteredInZNetScene}");
            }
            if (info.OverrideEntry != null)
            {
                Log($"  [Override]");
                Log($"    scale={info.OverrideEntry.Scale}  disableAI={info.OverrideEntry.DisableAI}  disableAggro={info.OverrideEntry.DisableAggro}  disableFleeing={info.OverrideEntry.DisableFleeing}");
            }
            if (info.Chain != null)
            {
                Log($"  [Chain]");
                Log($"    {info.Chain.SourcePrefab} -> {info.Chain.GeneratedPrefab} -> {info.Chain.AdultPrefab}");
            }
        }

        private static void RenderVerify(CpcGeneratedVerifyReport verify, bool verbose)
        {
            Log($"  Configured: {verify.ConfiguredCount}  Registered: {verify.RegisteredCount}  Missing: {verify.MissingCount}  Leaked: {verify.LeakedCount}");
            if (verify.MissingPrefabs.Count > 0)
            {
                Log("  Missing from ZNetScene:");
                foreach (var name in verify.MissingPrefabs) Log($"    - {name}");
            }
            if (verify.LeakedTemplates.Count > 0)
            {
                Log("  Leaked templates:");
                foreach (var l in verify.LeakedTemplates)
                    Log($"    - {l.Name}  path={l.HierarchyPath}  activeInHierarchy={l.ActiveInHierarchy}  id={l.InstanceId}");
            }
            if (verify.Notes.Count > 0)
            {
                foreach (var note in verify.Notes) Log($"  Note: {note}");
            }
        }

        // ── World ZDOs ───────────────────────────────────────────────────────

        internal static void RenderWorldZdos(CpcWorldZdoDiagnosticReport report, bool verbose)
        {
            if (report == null) { Log("[CPC] No data."); return; }

            Log($"=== CPC World ZDOs: '{report.PrefabName}' ===");
            Log($"  Hash: {report.PrefabHash}  Count: {report.Count}");

            if (!verbose)
            {
                foreach (var e in report.Entries)
                    Log($"  zdoid={e.ZdoId}  pos={FormatVec3(e.Position)}  owner={e.OwnerPeerId}");
            }
            else
            {
                foreach (var e in report.Entries)
                {
                    Log($"\n  [ZDO]");
                    Log($"    id={e.ZdoId}");
                    Log($"    pos={FormatVec3(e.Position)}");
                    Log($"    ownerPeer={e.OwnerPeerId}");
                    Log($"    prefab={e.PrefabName} (hash={e.PrefabHash})");
                }
            }

            RenderWarnings(report.Warnings);
            RenderSuggestions(report.SuggestedCommands);
        }

        // ── Section renderers ─────────────────────────────────────────────────

        private static void RenderIdentity(CpcIdentityInfo id)
        {
            if (id == null) return;
            Log("Identity:");
            Log($"  Object: {id.ObjectName}  Prefab: {id.PrefabName}");
            Log($"  Position: {FormatVec3(id.Position)}  Dist: {id.DistanceFromPlayer:F1}m");
            Log($"  ZNetView: valid={id.ZNetViewValid}  isOwner={id.IsOwner}  isServer={id.IsServer}");
            Log($"  ZDOID: {id.ZdoId}");
        }

        private static void RenderAI(CpcAIInfo ai, bool verbose)
        {
            Log("AI:");
            Log($"  BaseAI: exists={ai.BaseAIExists}  enabled={ai.BaseAIEnabled}");
            Log($"  MonsterAI: exists={ai.MonsterAIExists}  enabled={ai.MonsterAIEnabled}");
            Log($"  AnimalAI: exists={ai.AnimalAIExists}  enabled={ai.AnimalAIEnabled}");
            Log($"  PermanentDisabledMarker: {ai.PermanentAIDisabled}");
            Log($"  RuntimeDisabledByCpc: {ai.RuntimeAIDisabledByCpc}" + (ai.RuntimeAIDisabledByCpc ? $"  reason={ai.RuntimeDisableReason}" : ""));
            Log($"  MovementBlockers: {ai.MovementBlockerSummary}");

            if (verbose && ai.BaseAIExists)
            {
                Log("  BaseAI fields:");
                Log($"    viewRange={ai.ViewRange}  viewAngle={ai.ViewAngle}  hearRange={ai.HearRange}");
                Log($"    randomMoveInterval={ai.RandomMoveInterval}  randomMoveRange={ai.RandomMoveRange}  avoidWater={ai.AvoidWater}");
            }
            if (verbose && ai.MonsterAIExists)
            {
                Log("  MonsterAI fields:");
                Log($"    consumeItems={ai.ConsumeItemCount}  consumeRange={ai.ConsumeRange}  consumeSearchRange={ai.ConsumeSearchRange}  huntPlayer={ai.EnableHuntPlayer}");
            }
        }

        private static void RenderRuntime(CpcRuntimeInfo rt, bool verbose)
        {
            Log("Runtime Modifiers:");
            Log($"  Enabled: {rt.RuntimeEnabled}  Rules: {rt.TotalRules} total / {rt.ValidRules} valid / {rt.InvalidRules} invalid");
            Log($"  Matching rules for target: {rt.MatchingRuleCount}");
            Log($"  RuntimeAIDisabledByCpc: {rt.RuntimeAIDisabledByCpc}" + (rt.RuntimeAIDisabledByCpc ? $"  reason={rt.DisableReason}" : ""));

            if (rt.RuntimeAIDisabledByCpc)
            {
                Log($"  Original AI: baseAI={rt.OriginalBaseAIEnabled}  monsterAI={rt.OriginalMonsterAIEnabled}  animalAI={rt.OriginalAnimalAIEnabled}");
            }

            if (verbose && rt.RuleDetails.Count > 0)
            {
                Log("  Matching rule details:");
                foreach (var d in rt.RuleDetails)
                {
                    Log($"    Rule #{d.RuleIndex}: conditionsPass={d.ConditionsPass}");
                    foreach (var c in d.Conditions)
                        Log($"      {c.Name}: actual={c.Actual.ToString().ToLower()}  expected={NullBool(c.Expected)}  pass={c.Pass}  source={c.Source}");
                    Log($"      effects: disableAI={NullBool(d.EffectDisableAI)}  health={NullFloat(d.EffectHealth)}  damage={NullFloat(d.EffectDamage)}  speed={NullFloat(d.EffectSpeed)}");
                }
            }

            if (verbose && rt.RecentEventLines.Count > 0)
            {
                Log("  Recent events:");
                foreach (var line in rt.RecentEventLines) Log($"    {line}");
            }
        }

        private static void RenderRuntimeSummaryLine(CpcRuntimeInfo rt)
        {
            Log($"  Runtime: enabled={rt.RuntimeEnabled}  rules={rt.TotalRules}/{rt.ValidRules} valid  ai-disabled={rt.RuntimeAIDisabledByCpc}");
        }

        private static void RenderMountUp(CpcMountUpInfo mu, bool verbose)
        {
            Log("MountUp:");
            Log($"  Plugin detected: {mu.PluginDetected}  type resolved: {mu.TypeResolved}");
            Log($"  Tameable: exists={mu.TameableExists}  tamed={mu.IsTamed}  haveSaddle={mu.HaveSaddle}  haveRider={mu.HaveRider}");
            Log($"  Sadle component: root={mu.SadleComponentOnRoot}  child={mu.SadleComponentOnChild}  haveValidUser={mu.SadleHaveValidUser}  user={mu.SadleUser ?? "(none)"}");
            Log($"  Canonical: saddled={mu.CanonicalSaddled}  ridden={mu.CanonicalRidden}");
            Log($"  PermanentAIDisabledMarker: {mu.HasPermanentAIDisabledMarker}");
        }

        private static void RenderAllTameable(CpcAllTameableInfo at)
        {
            Log("AllTameable:");
            Log($"  Plugin detected: {at.AllTameableDetected}");
            Log($"  HasOffspringGrowup: {at.HasOffspringGrowup}");
            if (at.HasOffspringGrowup)
            {
                Log($"    growTriggered={at.GrowTriggered}  birthTimeTicks={at.BirthTimeTicks}  adultPrefab={at.AdultPrefab ?? "(none)"}");
            }
            Log($"  Tameable: exists={at.HasTameable}  tamed={at.IsTamed}");
        }

        private static void RenderGeneratedLive(CpcGeneratedLiveInfo gen)
        {
            Log("Generated:");
            Log($"  ConfiguredNewPrefab: {gen.ConfiguredNewPrefab}  enabled={gen.ConfigEnabled}");
            Log($"  SourcePrefab: {gen.ConfiguredSourcePrefab}  AdultPrefab: {gen.ConfiguredAdultPrefab}");
            Log($"  HasOffspringGrowup: {gen.HasOffspringGrowup}  UnderPrefabContainer: {gen.IsUnderPrefabContainer}");
        }

        private static void RenderZdo(CpcZdoInfo zdo)
        {
            Log("ZDO:");
            Log($"  id={zdo.ZdoId}  valid={zdo.ZNetViewValid}  isOwner={zdo.IsOwner}  isServer={zdo.IsServer}");
            Log($"  ownerPeer={zdo.OwnerPeerId}  prefabHash={zdo.PrefabHash}  canModify={zdo.CanModify}");
            Log($"  pos={FormatVec3(zdo.Position)}");
        }

        private static void RenderWarnings(List<string> warnings)
        {
            if (warnings == null || warnings.Count == 0) return;
            Log("Warnings:");
            foreach (var w in warnings) Log($"  ! {w}");
        }

        private static void RenderSuggestions(List<string> cmds)
        {
            if (cmds == null || cmds.Count == 0) return;
            Log("Suggested:");
            foreach (var c in cmds) Log($"  > {c}");
        }

        // ── Status command ────────────────────────────────────────────────────

        internal static void RenderStatus(bool verbose, bool showMods, bool showRuntime, bool showGenerated)
        {
            var plugin = CreaturePrefabCreatorPlugin.Instance;
            Log("=== CPC Status ===");
            Log($"  Version: 0.1.0  GUID: com.clickcs.creatureprefabcreator");
            Log($"  Plugin enabled: {plugin?.ConfigEnabled?.Value}");
            Log($"  VerboseLogging: {plugin?.ConfigVerboseLogging?.Value}");

            var cfg = plugin?.LoadedConfig;
            Log("Config:");
            Log($"  Loaded: {cfg != null}");
            if (cfg != null)
            {
                Log($"  SchemaVersion: {cfg.SchemaVersion}  Version: {cfg.Version ?? "(none)"}");
                Log($"  PrefabOverrides: {cfg.PrefabOverrides?.Count ?? 0}");
                Log($"  GeneratedPrefabs: {cfg.GeneratedPrefabs?.Count ?? 0}");
                Log($"  RuntimeModifiers: {cfg.RuntimeModifiers?.Count ?? 0}");
            }

            Log("Features:");
            Log($"  EnablePrefabOverrides: {plugin?.ConfigEnablePrefabOverrides?.Value}");
            Log($"  EnableGeneratedPrefabs: {plugin?.ConfigEnableGeneratedPrefabs?.Value}");
            Log($"  EnableRuntimeModifiers: {plugin?.ConfigEnableRuntimeModifiers?.Value}");
            Log($"  EnableConfigSync: {plugin?.ConfigEnableConfigSync?.Value}");

            Log("ZNetScene:");
            Log($"  Ready: {ZNetScene.instance != null}");

            if (showRuntime)
            {
                var status = RuntimeModifiers.RuntimeModifierManager.GetRuntimeStatus();
                Log("Runtime Modifiers:");
                Log($"  Enabled: {status.Enabled}  Rules: {status.TotalRules}/{status.ValidRules} valid/{status.InvalidRules} invalid");
                Log($"  AI disabled count: {status.RuntimeAIDisabledCount}  AI enabled (enableAI) count: {status.RuntimeAIEnabledCount}");
                Log($"  Eval interval: {status.EvaluationInterval}s");
                Log($"  Debug flags: DebugAIState={status.DebugAIState}  DebugMountState={status.DebugMountState}");
                Log($"  Event buffer: {RuntimeModifiers.RuntimeModifierEventBuffer.Count}");
                if (verbose)
                {
                    Log($"  Rule cache prefabs: {(status.RuleCachePrefabs.Count == 0 ? "(none)" : string.Join(", ", status.RuleCachePrefabs))}");
                    var events = RuntimeModifiers.RuntimeModifierEventBuffer.GetRecent(10);
                    if (events.Count > 0) { Log("  Recent events:"); foreach (var ev in events) Log($"    [{ev.TimeUtc:HH:mm:ss}] {ev.EventType} [{ev.PrefabName}] {ev.Message}"); }
                }
            }

            if (showGenerated)
            {
                var configs = cfg?.GeneratedPrefabs;
                Log("Generated Prefabs:");
                if (configs == null || configs.Count == 0) { Log("  (none configured)"); }
                else
                {
                    foreach (var c in configs)
                    {
                        bool reg = ZNetScene.instance?.GetPrefab(c.NewPrefab) != null;
                        Log($"  {c.NewPrefab} <- {c.SourcePrefab}  enabled={c.Enabled}  registered={reg}");
                    }
                }
            }

            if (showMods)
            {
                Log("Optional Mods:");
                var status = RuntimeModifiers.RuntimeModifierManager.GetRuntimeStatus();
                Log($"  MountUpRestored: detected={status.MountUpDetected}  typeResolved={status.MountUpTypeResolved}");
                Log($"  AllTameable: detected={status.AllTameableDetected}");

                Log("BepInEx Plugins (relevant):");
                try
                {
                    foreach (var kv in BepInEx.Bootstrap.Chainloader.PluginInfos)
                    {
                        var meta = kv.Value?.Metadata;
                        if (meta == null) continue;
                        string guid = meta.GUID ?? "";
                        bool isMU = guid.IndexOf("mountup", System.StringComparison.OrdinalIgnoreCase) >= 0 || guid.IndexOf("meldurson", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isAT = guid.IndexOf("tameable", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        bool isJ = guid.IndexOf("jotunn", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        if (isMU || isAT || isJ) Log($"  [{(isMU ? "MountUp" : isAT ? "AllTameable" : "Jotunn")}] {guid} v{meta.Version}");
                    }
                }
                catch (System.Exception ex) { Log($"  (scan failed: {ex.Message})"); }

                Log("Config Sync:");
                var localConfig = plugin?.LoadedConfig;
                var activeConfig = Network.ConfigSyncManager.GetActiveConfig();
                bool usingServer = activeConfig != localConfig && activeConfig != null;
                Log($"  ConfigEnableConfigSync: {plugin?.ConfigEnableConfigSync?.Value}");
                Log($"  UsingServerConfig: {usingServer}");
                if (ZNet.instance != null) Log($"  ZNet role: {(ZNet.instance.IsServer() ? "server" : "client")}");
                else Log("  ZNet: not initialized");
            }
        }

        // ── Help ─────────────────────────────────────────────────────────────

        internal static void RenderHelp(string commandFilter)
        {
            if (commandFilter == null)
            {
                Log("=== CPC Commands ===");
                Log("  cpc_help [--command <name>]         Show help.");
                Log("  cpc_status [--verbose] [--mods] [--debug-runtime] [--generated]");
                Log("  cpc_reload_config [--dry-run] [--prefabs-only] [--debug-runtime-only] [--force]");
                Log("  cpc_spawn --prefab <name> [--count <n>] [--level <n>] [--tamed] [--distance <m>]");
                Log("  cpc_print_console <mode> [options]  Modes: live, prefab, world-zdos");
                Log("  cpc_dump_json <mode> [options]      Modes: live, prefab, world-zdos");
                Log("  cpc_repair_world [--cleanup-zdos <prefab>|--orphans] [--dry-run|--confirm] [--verbose]");
                Log("Use: cpc_help --command <name> for detailed usage.");
            }
            else
            {
                switch (commandFilter.ToLowerInvariant())
                {
                    case "cpc_print_console":
                        Log("=== cpc_print_console ===");
                        Log("Syntax: cpc_print_console <mode> [args] [flags]");
                        Log("Modes:");
                        Log("  live --target [--ai] [--debug-runtime] [--debug-mountup] [--debug-alltameable] [--zdo] [--generated] [--verbose]");
                        Log("  live <radius> [--ai] [--debug-runtime] [--verbose]");
                        Log("  prefab --name <name> [--chain] [--generated] [--overrides]");
                        Log("  prefab --find <partial>");
                        Log("  prefab --compare <a> <b>");
                        Log("  prefab --list-generated");
                        Log("  prefab --verify-generated [--leaks] [--verbose]");
                        Log("  world-zdos <prefab> [--verbose]");
                        Log("Examples:");
                        Log("  cpc_print_console live --target");
                        Log("  cpc_print_console live --target --ai");
                        Log("  cpc_print_console live --target --debug-runtime --debug-mountup");
                        Log("  cpc_print_console live 20");
                        Log("  cpc_print_console live 50 --ai --debug-runtime");
                        Log("  cpc_print_console prefab --name Wolf");
                        Log("  cpc_print_console prefab --compare Wolf Bjorn_cub");
                        Log("  cpc_print_console prefab --verify-generated --leaks");
                        Log("  cpc_print_console world-zdos Bjorn_cub");
                        break;

                    case "cpc_dump_json":
                        Log("=== cpc_dump_json ===");
                        Log("Syntax: cpc_dump_json <mode> [args] [flags] [--output <filename>]");
                        Log("Output folder: BepInEx/config/CreaturePrefabCreator/dumps/");
                        Log("Modes mirror cpc_print_console exactly, but write JSON files.");
                        Log("Examples:");
                        Log("  cpc_dump_json live --target");
                        Log("  cpc_dump_json live --target --ai --output bjorn_test.json");
                        Log("  cpc_dump_json live 20 --debug-runtime");
                        Log("  cpc_dump_json prefab --name Wolf");
                        Log("  cpc_dump_json prefab --verify-generated --leaks");
                        Log("  cpc_dump_json world-zdos Bjorn_cub --output zdos.json");
                        break;

                    case "cpc_repair_world":
                        Log("=== cpc_repair_world ===");
                        Log("Syntax: cpc_repair_world <action> [--dry-run|--confirm] [--verbose]");
                        Log("Actions:");
                        Log("  --cleanup-zdos <prefab>   Remove ZDOs for a named prefab.");
                        Log("  --orphans                 Remove ZDOs with no matching registered prefab.");
                        Log("  --restore-runtime         Restore CPC runtime-disabled AI states.");
                        Log("  --force-grow              Force nearby offspring to grow immediately.");
                        Log("Safety:");
                        Log("  Default behavior is --dry-run (no changes).");
                        Log("  Add --confirm to apply destructive changes.");
                        Log("Examples:");
                        Log("  cpc_repair_world --cleanup-zdos Bjorn_cub --dry-run");
                        Log("  cpc_repair_world --cleanup-zdos Bjorn_cub --confirm");
                        Log("  cpc_repair_world --orphans --dry-run --verbose");
                        Log("  cpc_repair_world --restore-runtime");
                        Log("  cpc_repair_world --force-grow --confirm");
                        break;

                    case "cpc_status":
                        Log("=== cpc_status ===");
                        Log("Usage: cpc_status [--verbose] [--mods] [--debug-runtime] [--generated]");
                        Log("  --verbose          Extended output.");
                        Log("  --mods             Show mod detection + config sync state.");
                        Log("  --debug-runtime    Show runtime modifier system details.");
                        Log("  --generated        Show generated prefab subsystem status.");
                        break;

                    case "cpc_reload_config":
                        Log("=== cpc_reload_config ===");
                        Log("Usage: cpc_reload_config [--dry-run] [--prefabs-only] [--debug-runtime-only] [--force]");
                        Log("  --dry-run              Validate only, do not apply.");
                        Log("  --prefabs-only         Re-apply prefab overrides and generated prefabs only.");
                        Log("  --debug-runtime-only   Re-initialize runtime modifiers only.");
                        Log("  --force                Skip safety checks.");
                        break;

                    case "cpc_spawn":
                        Log("=== cpc_spawn ===");
                        Log("Usage: cpc_spawn --prefab <name> [--count <n>] [--level <n>] [--tamed] [--distance <m>]");
                        Log("  --prefab <name>    Required. Prefab name to spawn.");
                        Log("  --count <n>        Spawn multiple (default 1).");
                        Log("  --level <n>        Star level when applicable.");
                        Log("  --tamed            Spawn as tamed.");
                        Log("  --distance <m>     Distance from player (default 3).");
                        break;

                    default:
                        Log($"[CPC] Unknown command '{commandFilter}'. Use cpc_help for the command list.");
                        break;
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string FormatVec3(UnityEngine.Vector3 v) => $"({v.x:F1},{v.y:F1},{v.z:F1})";
        private static string NullBool(bool? v) => v.HasValue ? v.Value.ToString().ToLower() : "(omitted)";
        private static string NullFloat(float? v) => v.HasValue ? v.Value.ToString("G") : "(none)";
    }
}
