using CreaturePrefabCreator.Patches;
using CreaturePrefabCreator.RuntimeModifiers;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace CreaturePrefabCreator.Debug
{
    // ── Report model types ───────────────────────────────────────────────────

    internal class CpcIdentityInfo
    {
        public string ObjectName;
        public string PrefabName;
        public Vector3 Position;
        public string ZdoId;
        public bool ZNetViewValid;
        public bool IsOwner;
        public bool IsServer;
        public float DistanceFromPlayer;
    }

    internal class CpcAIInfo
    {
        public bool BaseAIExists;
        public bool BaseAIEnabled;
        public bool MonsterAIExists;
        public bool MonsterAIEnabled;
        public bool AnimalAIExists;
        public bool AnimalAIEnabled;
        public float ViewRange;
        public float ViewAngle;
        public float HearRange;
        public float RandomMoveInterval;
        public float RandomMoveRange;
        public bool AvoidWater;
        public int ConsumeItemCount;
        public float ConsumeRange;
        public float ConsumeSearchRange;
        public bool EnableHuntPlayer;
        public bool PermanentAIDisabled;
        public bool RuntimeAIDisabledByCpc;
        public string RuntimeDisableReason;
        public string MovementBlockerSummary;
    }

    internal class CpcRuntimeInfo
    {
        public bool RuntimeEnabled;
        public int TotalRules;
        public int ValidRules;
        public int InvalidRules;
        public int MatchingRuleCount;
        public bool RuntimeAIDisabledByCpc;
        public string DisableReason;
        public float DisabledAt;
        public bool OriginalBaseAIEnabled;
        public bool OriginalMonsterAIEnabled;
        public bool OriginalAnimalAIEnabled;
        public List<RuntimeRuleDetail> RuleDetails = new List<RuntimeRuleDetail>();
        public List<string> RecentEventLines = new List<string>();
    }

    internal class RuntimeRuleDetail
    {
        public int RuleIndex;
        public bool ConditionsPass;
        public List<RuntimeConditionDetail> Conditions = new List<RuntimeConditionDetail>();
        public bool? EffectDisableAI;
        public float? EffectHealth;
        public float? EffectDamage;
        public float? EffectSpeed;
    }

    internal class RuntimeConditionDetail
    {
        public string Name;
        public bool Actual;
        public bool? Expected;
        public bool Pass;
        public string Source;
    }

    internal class CpcMountUpInfo
    {
        public bool PluginDetected;
        public bool TypeResolved;
        public bool TameableExists;
        public bool IsTamed;
        public bool HaveSaddle;
        public bool HaveRider;
        public bool SadleComponentOnRoot;
        public bool SadleComponentOnChild;
        public bool SadleHaveValidUser;
        public string SadleUser;
        public bool CanonicalSaddled;
        public bool CanonicalRidden;
        public bool HasRidingAITempEnabled;
        public float RidingAIEnabledSince;
        public bool HasPermanentAIDisabledMarker;
        public bool EnableRidingAISuppression;
    }

    internal class CpcAllTameableInfo
    {
        public bool AllTameableDetected;
        public bool HasOffspringGrowup;
        public bool GrowTriggered;
        public long BirthTimeTicks;
        public string AdultPrefab;
        public bool HasTameable;
#pragma warning disable CS0649
        public float TameTimeRemaining; // populated via AllTameable reflection when compat is extended
#pragma warning restore CS0649
        public bool IsTamed;
    }

    internal class CpcZdoInfo
    {
        public string ZdoId;
        public bool ZNetViewValid;
        public bool IsOwner;
        public long OwnerPeerId;
        public bool IsServer;
        public Vector3 Position;
        public int PrefabHash;
        public bool CanModify;
    }

    internal class CpcGeneratedLiveInfo
    {
        public string ConfiguredNewPrefab;
        public string ConfiguredSourcePrefab;
        public string ConfiguredAdultPrefab;
        public bool ConfigEnabled;
        public bool HasOffspringGrowup;
        public bool IsUnderPrefabContainer;
    }

    internal class CpcLiveDiagnosticReport
    {
        public string ReportType = "live_target";
        public string CreatedAt;
        public CpcIdentityInfo Identity;
        public CpcAIInfo AI;
        public CpcRuntimeInfo Runtime;
        public CpcMountUpInfo MountUp;
        public CpcAllTameableInfo AllTameable;
        public CpcZdoInfo Zdo;
        public CpcGeneratedLiveInfo Generated;
        public List<string> Warnings = new List<string>();
        public List<string> SuggestedCommands = new List<string>();
    }

    internal class CpcRadiusEntry
    {
        public CpcIdentityInfo Identity;
        public bool HasCharacter;
        public bool HasMonsterAI;
        public bool HasAnimalAI;
        public bool HasTameable;
        public bool HasOffspringGrowup;
        public bool RuntimeAIDisabled;
    }

    internal class CpcRadiusDiagnosticReport
    {
        public string ReportType = "live_radius";
        public string CreatedAt;
        public float Radius;
        public Vector3 PlayerPosition;
        public List<CpcRadiusEntry> Entries = new List<CpcRadiusEntry>();
        public CpcRuntimeInfo RuntimeSummary;
        public List<string> Warnings = new List<string>();
        public List<string> SuggestedCommands = new List<string>();
    }

    internal class CpcPrefabInfo
    {
        public string PrefabName;
        public bool FoundInZNetScene;
        public Vector3 Scale;
        public bool HasCharacter;
        public bool HasZNetView;
        public bool HasOffspringGrowup;
        public bool HasMonsterAI;
        public bool HasAnimalAI;
        public bool HasTameable;
        public bool HasBaseAI;
        public CpcGeneratedPrefabEntry GeneratedEntry;
        public CpcPrefabOverrideEntry OverrideEntry;
        public CpcPrefabChain Chain;
    }

    internal class CpcGeneratedPrefabEntry
    {
        public string NewPrefab;
        public string SourcePrefab;
        public string AdultPrefab;
        public bool Enabled;
        public bool RegisteredInZNetScene;
    }

    internal class CpcPrefabOverrideEntry
    {
        public string TargetPrefab;
        public bool Enabled;
        public float Scale;
        public bool DisableAI;
        public bool DisableAggro;
        public bool DisableFleeing;
        public bool? FriendAttacked;
    }

    internal class CpcPrefabChain
    {
        public string SourcePrefab;
        public string GeneratedPrefab;
        public string AdultPrefab;
    }

    internal class CpcPrefabDiagnosticReport
    {
        public string ReportType = "prefab";
        public string CreatedAt;
        public string QueryType;
        public string QueryValue;
        public List<CpcPrefabInfo> Results = new List<CpcPrefabInfo>();
        public CpcGeneratedVerifyReport VerifyReport;
        public List<string> Warnings = new List<string>();
        public List<string> SuggestedCommands = new List<string>();
    }

    internal class CpcGeneratedVerifyReport
    {
        public int ConfiguredCount;
        public int RegisteredCount;
        public int MissingCount;
        public int LeakedCount;
        public List<string> MissingPrefabs = new List<string>();
        public List<CpcLeakedTemplate> LeakedTemplates = new List<CpcLeakedTemplate>();
        public List<string> Notes = new List<string>();
    }

    internal class CpcLeakedTemplate
    {
        public string Name;
        public string HierarchyPath;
        public bool ActiveInHierarchy;
        public int InstanceId;
    }

    internal class CpcWorldZdoEntry
    {
        public string ZdoId;
        public Vector3 Position;
        public long OwnerPeerId;
        public int PrefabHash;
        public string PrefabName;
    }

    internal class CpcWorldZdoDiagnosticReport
    {
        public string ReportType = "world_zdos";
        public string CreatedAt;
        public string PrefabName;
        public int PrefabHash;
        public int Count;
        public List<CpcWorldZdoEntry> Entries = new List<CpcWorldZdoEntry>();
        public List<string> Warnings = new List<string>();
        public List<string> SuggestedCommands = new List<string>();
    }

    // ── Engine ───────────────────────────────────────────────────────────────

    internal static class CpcDiagnosticEngine
    {
        private static void Log(string msg) => CreaturePrefabCreatorPlugin.Instance?.Log(msg);

        // ── Identity ─────────────────────────────────────────────────────────

        internal static CpcIdentityInfo BuildIdentity(GameObject go)
        {
            if (go == null) return null;
            var nview = go.GetComponent<ZNetView>();
            bool znvValid = nview != null && nview.IsValid();
            string zdoid = znvValid ? nview.GetZDO().m_uid.ToString() : "(invalid)";
            bool isOwner = znvValid && nview.IsOwner();
            bool isServer = ZNet.instance != null && ZNet.instance.IsServer();

            float dist = 0f;
            if (Player.m_localPlayer != null)
                dist = Vector3.Distance(go.transform.position, Player.m_localPlayer.transform.position);

            return new CpcIdentityInfo
            {
                ObjectName = go.name,
                PrefabName = NormalizeName(go.name),
                Position = go.transform.position,
                ZdoId = zdoid,
                ZNetViewValid = znvValid,
                IsOwner = isOwner,
                IsServer = isServer,
                DistanceFromPlayer = dist
            };
        }

        // ── AI ───────────────────────────────────────────────────────────────

        internal static CpcAIInfo BuildAI(GameObject go)
        {
            if (go == null) return null;
            var baseAI = go.GetComponent<BaseAI>();
            var monsterAI = go.GetComponent<MonsterAI>();
            var animalAI = go.GetComponent<AnimalAI>();

            bool runtimeDisabled = RuntimeModifierManager.IsRuntimeAIDisabled(go.GetComponent<Character>(), out string reason);
            bool permanentDisabled = go.GetComponent<PermanentAIDisabledMarker>() != null;

            var info = new CpcAIInfo
            {
                BaseAIExists = baseAI != null,
                BaseAIEnabled = baseAI?.enabled ?? false,
                MonsterAIExists = monsterAI != null,
                MonsterAIEnabled = monsterAI?.enabled ?? false,
                AnimalAIExists = animalAI != null,
                AnimalAIEnabled = animalAI?.enabled ?? false,
                PermanentAIDisabled = permanentDisabled,
                RuntimeAIDisabledByCpc = runtimeDisabled,
                RuntimeDisableReason = reason
            };

            if (baseAI != null)
            {
                info.ViewRange = baseAI.m_viewRange;
                info.ViewAngle = baseAI.m_viewAngle;
                info.HearRange = baseAI.m_hearRange;
                info.RandomMoveInterval = baseAI.m_randomMoveInterval;
                info.RandomMoveRange = baseAI.m_randomMoveRange;
                info.AvoidWater = baseAI.m_avoidWater;
            }

            if (monsterAI != null)
            {
                info.ConsumeItemCount = monsterAI.m_consumeItems?.Count ?? 0;
                info.ConsumeRange = monsterAI.m_consumeRange;
                info.ConsumeSearchRange = monsterAI.m_consumeSearchRange;
                info.EnableHuntPlayer = monsterAI.m_enableHuntPlayer;
            }

            var blockers = new System.Text.StringBuilder();
            if (!info.BaseAIEnabled && info.BaseAIExists) blockers.Append("BaseAI disabled; ");
            if (!info.MonsterAIEnabled && info.MonsterAIExists) blockers.Append("MonsterAI disabled; ");
            if (info.PermanentAIDisabled) blockers.Append("permanent disableAI marker; ");
            if (info.RuntimeAIDisabledByCpc) blockers.Append($"runtime modifier ({reason}); ");
            info.MovementBlockerSummary = blockers.Length > 0 ? blockers.ToString().TrimEnd(' ', ';') : "none";

            return info;
        }

        // ── Runtime ──────────────────────────────────────────────────────────

        internal static CpcRuntimeInfo BuildRuntime(Character ch)
        {
            var status = RuntimeModifierManager.GetRuntimeStatus();
            var rules = RuntimeModifierManager.GetLoadedRulesDebug();
            var recentEvents = RuntimeModifierEventBuffer.GetRecent(20);

            var info = new CpcRuntimeInfo
            {
                RuntimeEnabled = status.Enabled,
                TotalRules = status.TotalRules,
                ValidRules = status.ValidRules,
                InvalidRules = status.InvalidRules
            };

            if (ch != null)
            {
                var checkResult = RuntimeModifierManager.GetRuntimeCheckDebug(ch);
                if (checkResult != null)
                {
                    info.MatchingRuleCount = checkResult.MatchingRuleCount;
                    info.RuntimeAIDisabledByCpc = checkResult.RuntimeAIDisabledByCpc;
                    info.DisableReason = checkResult.DisableReason;
                    info.DisabledAt = checkResult.DisabledAt;
                    info.OriginalBaseAIEnabled = checkResult.OriginalBaseAIEnabled;
                    info.OriginalMonsterAIEnabled = checkResult.OriginalMonsterAIEnabled;
                    info.OriginalAnimalAIEnabled = checkResult.OriginalAnimalAIEnabled;

                    if (checkResult.RuleDetails != null)
                    {
                        foreach (var d in checkResult.RuleDetails)
                        {
                            var detail = new RuntimeRuleDetail
                            {
                                RuleIndex = d.RuleIndex,
                                ConditionsPass = d.ConditionsPass,
                                EffectDisableAI = d.EffectDisableAI,
                                EffectHealth = d.EffectHealth,
                                EffectDamage = d.EffectDamage,
                                EffectSpeed = d.EffectSpeed
                            };
                            if (d.Conditions != null)
                            {
                                foreach (var c in d.Conditions)
                                    detail.Conditions.Add(new RuntimeConditionDetail { Name = c.Name, Actual = c.Actual, Expected = c.Expected, Pass = c.Pass, Source = c.Source ?? "" });
                            }
                            info.RuleDetails.Add(detail);
                        }
                    }
                }
            }

            foreach (var ev in recentEvents)
            {
                string zdoidStr = ev.ZdoId.HasValue ? $" id={ev.ZdoId.Value}" : "";
                string prefabStr = !string.IsNullOrEmpty(ev.PrefabName) ? $" [{ev.PrefabName}]" : "";
                info.RecentEventLines.Add($"[{ev.TimeUtc:HH:mm:ss}] {ev.EventType}{prefabStr}{zdoidStr} {ev.Message}");
            }

            return info;
        }

        // ── MountUp ──────────────────────────────────────────────────────────

        internal static CpcMountUpInfo BuildMountUp(Character ch)
        {
            if (ch == null) return null;
            var plugin = CreaturePrefabCreatorPlugin.Instance;

            var info = new CpcMountUpInfo
            {
                PluginDetected = SaddledCreaturePatch.MountUpDetected,
                TypeResolved = SaddledCreaturePatch.MountUpTypeResolved,
                CanonicalSaddled = SaddledCreaturePatch.IsSaddledViaCanonicalPath(ch),
                CanonicalRidden = SaddledCreaturePatch.IsActivelyRidden(ch),
                EnableRidingAISuppression = plugin?.ConfigEnableRidingAISuppression?.Value ?? false
            };

            var tameable = ch.GetComponent<Tameable>();
            info.HasPermanentAIDisabledMarker = ch.GetComponent<PermanentAIDisabledMarker>() != null;

            if (tameable != null)
            {
                info.TameableExists = true;
                info.IsTamed = SafeInvoke<bool>(tameable, "IsTamed");
                info.HaveSaddle = SafeInvoke<bool>(tameable, "HaveSaddle");
                info.HaveRider = SafeInvoke<bool>(tameable, "HaveRider");
            }

            Type sadleType = null;
            try { sadleType = Type.GetType("Sadle, assembly_valheim"); } catch { }
            Component sadleRoot = sadleType != null ? ch.GetComponent(sadleType) : null;
            Component sadleChild = sadleType != null ? ch.GetComponentInChildren(sadleType, true) : null;
            info.SadleComponentOnRoot = sadleRoot != null;
            info.SadleComponentOnChild = sadleChild != null && sadleChild != sadleRoot;
            Component sadleToUse = sadleRoot ?? sadleChild;
            if (sadleToUse != null)
            {
                info.SadleHaveValidUser = SafeInvoke<bool>(sadleToUse, "HaveValidUser");
                var userId = TryGetField(sadleToUse, "_user") ?? InvokeMethod(sadleToUse, "GetUser");
                info.SadleUser = userId?.ToString();
            }

            var marker = ch.GetComponent<Patches.RidingAITempEnabledMarker>();
            if (marker != null)
            {
                info.HasRidingAITempEnabled = true;
                info.RidingAIEnabledSince = UnityEngine.Time.time - marker.EnabledTime;
            }

            return info;
        }

        // ── AllTameable ──────────────────────────────────────────────────────

        internal static CpcAllTameableInfo BuildAllTameable(Character ch)
        {
            if (ch == null) return null;
            var status = RuntimeModifierManager.GetRuntimeStatus();

            var info = new CpcAllTameableInfo
            {
                AllTameableDetected = status.AllTameableDetected
            };

            var growup = ch.GetComponent<GeneratedPrefabs.OffspringGrowup>();
            if (growup != null)
            {
                info.HasOffspringGrowup = true;
                var znv = ch.GetComponent<ZNetView>();
                if (znv != null && znv.IsValid())
                {
                    info.GrowTriggered = znv.GetZDO().GetBool(GeneratedPrefabs.OffspringGrowup.GrowTriggeredKey.GetStableHashCode());
                    info.BirthTimeTicks = znv.GetZDO().GetLong(GeneratedPrefabs.OffspringGrowup.BirthTimeKey.GetStableHashCode());
                }
                info.AdultPrefab = growup.adultPrefabName;
            }

            var tameable = ch.GetComponent<Tameable>();
            if (tameable != null)
            {
                info.HasTameable = true;
                info.IsTamed = tameable.IsTamed();
            }

            return info;
        }

        // ── ZDO ─────────────────────────────────────────────────────────────

        internal static CpcZdoInfo BuildZdo(GameObject go)
        {
            if (go == null) return null;
            var nview = go.GetComponent<ZNetView>();
            bool znvValid = nview != null && nview.IsValid();

            long ownerId = 0L;
            try { if (znvValid) ownerId = (long)(TryGetField(nview.GetZDO(), "m_owner") ?? 0L); } catch { }

            return new CpcZdoInfo
            {
                ZdoId = znvValid ? nview.GetZDO().m_uid.ToString() : "(invalid)",
                ZNetViewValid = znvValid,
                IsOwner = znvValid && nview.IsOwner(),
                OwnerPeerId = ownerId,
                IsServer = ZNet.instance != null && ZNet.instance.IsServer(),
                Position = go.transform.position,
                PrefabHash = znvValid ? nview.GetZDO().GetPrefab() : 0,
                CanModify = go.GetComponent<Character>() is Character ch2 && SaddledCreaturePatch.CanModifyCreature(ch2)
            };
        }

        // ── Generated (live) ─────────────────────────────────────────────────

        internal static CpcGeneratedLiveInfo BuildGeneratedLive(GameObject go)
        {
            if (go == null) return null;
            string prefabName = NormalizeName(go.name);
            var configs = CreaturePrefabCreatorPlugin.Instance?.LoadedConfig?.GeneratedPrefabs;
            if (configs == null) return null;

            foreach (var cfg in configs)
            {
                if (!cfg.Enabled) continue;
                if (!string.Equals(cfg.NewPrefab, prefabName, StringComparison.OrdinalIgnoreCase)) continue;

                bool underContainer = false;
                Transform p = go.transform.parent;
                while (p != null)
                {
                    if (p.name == "CreaturePrefabCreator_PrefabContainer") { underContainer = true; break; }
                    p = p.parent;
                }

                return new CpcGeneratedLiveInfo
                {
                    ConfiguredNewPrefab = cfg.NewPrefab,
                    ConfiguredSourcePrefab = cfg.SourcePrefab,
                    ConfiguredAdultPrefab = cfg.AdultPrefab,
                    ConfigEnabled = cfg.Enabled,
                    HasOffspringGrowup = go.GetComponent<GeneratedPrefabs.OffspringGrowup>() != null,
                    IsUnderPrefabContainer = underContainer
                };
            }
            return null;
        }

        // ── Live target report ────────────────────────────────────────────────

        internal static CpcLiveDiagnosticReport BuildLiveTargetReport(Character ch, bool includeAI, bool includeRuntime, bool includeMountUp, bool includeAllTameable, bool includeZdo, bool includeGenerated)
        {
            if (ch == null) return null;
            var go = ch.gameObject;
            bool allCategories = !includeAI && !includeRuntime && !includeMountUp && !includeAllTameable && !includeZdo && !includeGenerated;

            var report = new CpcLiveDiagnosticReport
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Identity = BuildIdentity(go)
            };

            if (allCategories || includeAI)
                report.AI = BuildAI(go);
            if (allCategories || includeRuntime)
                report.Runtime = BuildRuntime(ch);
            if (allCategories || includeMountUp)
                report.MountUp = BuildMountUp(ch);
            if (allCategories || includeAllTameable)
                report.AllTameable = BuildAllTameable(ch);
            if (allCategories || includeZdo)
                report.Zdo = BuildZdo(go);
            if (allCategories || includeGenerated)
                report.Generated = BuildGeneratedLive(go);

            BuildLiveWarnings(report);
            BuildLiveSuggestions(report);
            return report;
        }

        private static void BuildLiveWarnings(CpcLiveDiagnosticReport report)
        {
            if (report.AI != null)
            {
                if (report.AI.PermanentAIDisabled && !report.AI.RuntimeAIDisabledByCpc)
                    report.Warnings.Add("Creature has permanent disableAI marker. AI is permanently suppressed by prefab config.");
                if (report.AI.RuntimeAIDisabledByCpc)
                    report.Warnings.Add($"Creature AI is currently disabled by CPC runtime modifier. Reason: {report.AI.RuntimeDisableReason}");
            }
            if (report.MountUp != null && report.MountUp.CanonicalRidden && !report.MountUp.HasRidingAITempEnabled)
                report.Warnings.Add("Creature is being ridden but RidingAITempEnabled marker is absent.");
            if (report.Identity != null && !report.Identity.IsOwner)
                report.Warnings.Add("You are not the network owner of this creature. Some diagnostics may be incomplete.");
        }

        private static void BuildLiveSuggestions(CpcLiveDiagnosticReport report)
        {
            string prefab = report.Identity?.PrefabName ?? "?";
            if (report.AI?.RuntimeAIDisabledByCpc == true)
                report.SuggestedCommands.Add($"cpc_print_console live --target --debug-runtime");
            if (report.MountUp?.PluginDetected == true)
                report.SuggestedCommands.Add($"cpc_print_console live --target --debug-mountup");
            if (report.AllTameable?.AllTameableDetected == true)
                report.SuggestedCommands.Add($"cpc_print_console live --target --debug-alltameable");
            report.SuggestedCommands.Add($"cpc_print_console prefab --name {prefab}");
            report.SuggestedCommands.Add($"cpc_dump_json live --target");
        }

        // ── Radius report ─────────────────────────────────────────────────────

        internal static CpcRadiusDiagnosticReport BuildRadiusReport(float radius, bool includeRuntime)
        {
            var report = new CpcRadiusDiagnosticReport
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                Radius = radius
            };

            if (Player.m_localPlayer == null)
            {
                report.Warnings.Add("Local player not available.");
                return report;
            }

            Vector3 pos = Player.m_localPlayer.transform.position;
            report.PlayerPosition = pos;

            var characters = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            foreach (var ch in characters)
            {
                if (ch == null || ch is Player) continue;
                if (Vector3.Distance(ch.transform.position, pos) > radius) continue;

                bool runtimeDisabled = RuntimeModifierManager.IsRuntimeAIDisabled(ch, out _);
                report.Entries.Add(new CpcRadiusEntry
                {
                    Identity = BuildIdentity(ch.gameObject),
                    HasCharacter = true,
                    HasMonsterAI = ch.GetComponent<MonsterAI>() != null,
                    HasAnimalAI = ch.GetComponent<AnimalAI>() != null,
                    HasTameable = ch.GetComponent<Tameable>() != null,
                    HasOffspringGrowup = ch.GetComponent<GeneratedPrefabs.OffspringGrowup>() != null,
                    RuntimeAIDisabled = runtimeDisabled
                });
            }

            if (includeRuntime)
                report.RuntimeSummary = BuildRuntime(null);

            if (report.Entries.Count == 0)
                report.Warnings.Add($"No creatures found within {radius}m.");

            report.SuggestedCommands.Add("cpc_print_console live --target (aim at a creature for details)");
            report.SuggestedCommands.Add($"cpc_dump_json live {radius}");
            return report;
        }

        // ── Prefab report ────────────────────────────────────────────────────

        internal static CpcPrefabDiagnosticReport BuildPrefabReport(string prefabName, bool includeGenerated, bool includeOverrides, bool includeChain)
        {
            var report = new CpcPrefabDiagnosticReport
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                QueryType = "name",
                QueryValue = prefabName
            };

            if (ZNetScene.instance == null)
            {
                report.Warnings.Add("ZNetScene not available.");
                return report;
            }

            var go = ZNetScene.instance.GetPrefab(prefabName);
            if (go == null)
            {
                report.Warnings.Add($"Prefab '{prefabName}' not found in ZNetScene.");
                report.SuggestedCommands.Add($"cpc_print_console prefab --find {prefabName}");
                return report;
            }

            var info = BuildPrefabInfo(go, includeGenerated, includeOverrides, includeChain);
            report.Results.Add(info);

            report.SuggestedCommands.Add($"cpc_print_console prefab --name {prefabName} --chain");
            report.SuggestedCommands.Add($"cpc_dump_json prefab --name {prefabName}");
            return report;
        }

        internal static CpcPrefabDiagnosticReport BuildPrefabFindReport(string partial)
        {
            var report = new CpcPrefabDiagnosticReport
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                QueryType = "find",
                QueryValue = partial
            };

            if (ZNetScene.instance == null)
            {
                report.Warnings.Add("ZNetScene not available.");
                return report;
            }

            foreach (var prefab in ZNetScene.instance.m_prefabs)
            {
                if (prefab == null) continue;
                if (prefab.name.IndexOf(partial, StringComparison.OrdinalIgnoreCase) < 0) continue;
                report.Results.Add(BuildPrefabInfo(prefab, false, false, false));
            }

            if (report.Results.Count == 0)
                report.Warnings.Add($"No prefabs found matching '{partial}'.");
            else
                report.SuggestedCommands.Add($"cpc_print_console prefab --name <name> (from the list above)");

            return report;
        }

        internal static CpcPrefabDiagnosticReport BuildPrefabCompareReport(string nameA, string nameB)
        {
            var report = new CpcPrefabDiagnosticReport
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                QueryType = "compare",
                QueryValue = $"{nameA} vs {nameB}"
            };

            if (ZNetScene.instance == null)
            {
                report.Warnings.Add("ZNetScene not available.");
                return report;
            }

            var goA = ZNetScene.instance.GetPrefab(nameA);
            var goB = ZNetScene.instance.GetPrefab(nameB);

            if (goA != null) report.Results.Add(BuildPrefabInfo(goA, true, true, false));
            else report.Warnings.Add($"Prefab '{nameA}' not found.");

            if (goB != null) report.Results.Add(BuildPrefabInfo(goB, true, true, false));
            else report.Warnings.Add($"Prefab '{nameB}' not found.");

            return report;
        }

        internal static CpcPrefabDiagnosticReport BuildGeneratedListReport()
        {
            var report = new CpcPrefabDiagnosticReport
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                QueryType = "list-generated",
                QueryValue = "(all)"
            };

            var configs = CreaturePrefabCreatorPlugin.Instance?.LoadedConfig?.GeneratedPrefabs;
            if (configs == null || configs.Count == 0)
            {
                report.Warnings.Add("No generated prefab configs loaded.");
                return report;
            }

            foreach (var cfg in configs)
            {
                bool registered = ZNetScene.instance != null && ZNetScene.instance.GetPrefab(cfg.NewPrefab) != null;
                var info = new CpcPrefabInfo
                {
                    PrefabName = cfg.NewPrefab,
                    FoundInZNetScene = registered
                };
                info.GeneratedEntry = new CpcGeneratedPrefabEntry
                {
                    NewPrefab = cfg.NewPrefab,
                    SourcePrefab = cfg.SourcePrefab,
                    AdultPrefab = cfg.AdultPrefab,
                    Enabled = cfg.Enabled,
                    RegisteredInZNetScene = registered
                };
                report.Results.Add(info);
            }

            report.SuggestedCommands.Add("cpc_print_console prefab --verify-generated");
            return report;
        }

        internal static CpcPrefabDiagnosticReport BuildGeneratedVerifyReport(bool includeLeaks)
        {
            var report = new CpcPrefabDiagnosticReport
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                QueryType = includeLeaks ? "verify-generated-leaks" : "verify-generated",
                QueryValue = "(all)"
            };

            var verify = new CpcGeneratedVerifyReport();
            report.VerifyReport = verify;

            var configs = CreaturePrefabCreatorPlugin.Instance?.LoadedConfig?.GeneratedPrefabs;
            if (configs == null)
            {
                report.Warnings.Add("No config loaded.");
                return report;
            }

            verify.ConfiguredCount = configs.Count;
            foreach (var cfg in configs)
            {
                if (!cfg.Enabled) continue;
                bool registered = ZNetScene.instance != null && ZNetScene.instance.GetPrefab(cfg.NewPrefab) != null;
                if (registered) verify.RegisteredCount++;
                else { verify.MissingCount++; verify.MissingPrefabs.Add(cfg.NewPrefab); }
            }

            if (includeLeaks)
            {
                var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                foreach (var go in allObjects)
                {
                    if (go == null) continue;
                    foreach (var cfg in configs)
                    {
                        if (!cfg.Enabled) continue;
                        if (go.name != cfg.NewPrefab || !go.activeInHierarchy) continue;
                        bool underContainer = false;
                        Transform p = go.transform.parent;
                        while (p != null)
                        {
                            if (p.name == "CreaturePrefabCreator_PrefabContainer") { underContainer = true; break; }
                            p = p.parent;
                        }
                        if (!underContainer)
                        {
                            verify.LeakedCount++;
                            verify.LeakedTemplates.Add(new CpcLeakedTemplate
                            {
                                Name = go.name,
                                HierarchyPath = GetTransformPath(go.transform),
                                ActiveInHierarchy = go.activeInHierarchy,
                                InstanceId = go.GetInstanceID()
                            });
                        }
                    }
                }
            }

            if (verify.MissingCount > 0)
                report.Warnings.Add($"{verify.MissingCount} generated prefab(s) are configured but not registered in ZNetScene.");
            if (verify.LeakedCount > 0)
                report.Warnings.Add($"{verify.LeakedCount} leaked template(s) found outside the prefab container.");

            report.SuggestedCommands.Add("cpc_repair_world --orphans --dry-run");
            return report;
        }

        private static CpcPrefabInfo BuildPrefabInfo(GameObject go, bool includeGenerated, bool includeOverrides, bool includeChain)
        {
            var info = new CpcPrefabInfo
            {
                PrefabName = go.name,
                FoundInZNetScene = true,
                Scale = go.transform.localScale,
                HasCharacter = go.GetComponent<Character>() != null,
                HasZNetView = go.GetComponent<ZNetView>() != null,
                HasOffspringGrowup = go.GetComponent<GeneratedPrefabs.OffspringGrowup>() != null,
                HasMonsterAI = go.GetComponent<MonsterAI>() != null,
                HasAnimalAI = go.GetComponent<AnimalAI>() != null,
                HasTameable = go.GetComponent<Tameable>() != null,
                HasBaseAI = go.GetComponent<BaseAI>() != null
            };

            if (includeGenerated)
            {
                var configs = CreaturePrefabCreatorPlugin.Instance?.LoadedConfig?.GeneratedPrefabs;
                if (configs != null)
                {
                    foreach (var cfg in configs)
                    {
                        if (!string.Equals(cfg.NewPrefab, go.name, StringComparison.OrdinalIgnoreCase)) continue;
                        info.GeneratedEntry = new CpcGeneratedPrefabEntry
                        {
                            NewPrefab = cfg.NewPrefab,
                            SourcePrefab = cfg.SourcePrefab,
                            AdultPrefab = cfg.AdultPrefab,
                            Enabled = cfg.Enabled,
                            RegisteredInZNetScene = ZNetScene.instance?.GetPrefab(cfg.NewPrefab) != null
                        };
                        break;
                    }
                }
            }

            if (includeOverrides)
            {
                var overrides = CreaturePrefabCreatorPlugin.Instance?.LoadedConfig?.PrefabOverrides;
                if (overrides != null)
                {
                    foreach (var ov in overrides)
                    {
                        if (!string.Equals(ov.TargetPrefab, go.name, StringComparison.OrdinalIgnoreCase)) continue;
                        info.OverrideEntry = new CpcPrefabOverrideEntry
                        {
                            TargetPrefab = ov.TargetPrefab,
                            Enabled = ov.Enabled,
                            Scale = ov.Scale,
                            DisableAI = ov.DisableAI,
                            DisableAggro = ov.DisableAggro,
                            DisableFleeing = ov.DisableFleeing,
                            FriendAttacked = ov.FriendAttacked
                        };
                        break;
                    }
                }
            }

            if (includeChain)
            {
                var configs = CreaturePrefabCreatorPlugin.Instance?.LoadedConfig?.GeneratedPrefabs;
                if (configs != null)
                {
                    foreach (var cfg in configs)
                    {
                        if (!string.Equals(cfg.NewPrefab, go.name, StringComparison.OrdinalIgnoreCase)) continue;
                        info.Chain = new CpcPrefabChain
                        {
                            SourcePrefab = cfg.SourcePrefab,
                            GeneratedPrefab = cfg.NewPrefab,
                            AdultPrefab = cfg.AdultPrefab
                        };
                        break;
                    }
                }
            }

            return info;
        }

        // ── World ZDO report ─────────────────────────────────────────────────

        internal static CpcWorldZdoDiagnosticReport BuildWorldZdoReport(string prefabName)
        {
            var report = new CpcWorldZdoDiagnosticReport
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                PrefabName = prefabName
            };

            if (ZNetScene.instance == null)
            {
                report.Warnings.Add("ZNetScene not available.");
                return report;
            }

            int hash = prefabName.GetStableHashCode();
            report.PrefabHash = hash;

            var allZNVs = UnityEngine.Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None);
            foreach (var znv in allZNVs)
            {
                if (znv == null || znv.GetZDO() == null) continue;
                if (znv.GetZDO().GetPrefab() != hash) continue;

                long ownerId = 0L;
                try { ownerId = (long)(TryGetField(znv.GetZDO(), "m_owner") ?? 0L); } catch { }

                report.Entries.Add(new CpcWorldZdoEntry
                {
                    ZdoId = znv.GetZDO().m_uid.ToString(),
                    Position = znv.GetZDO().GetPosition(),
                    OwnerPeerId = ownerId,
                    PrefabHash = hash,
                    PrefabName = prefabName
                });
            }

            report.Count = report.Entries.Count;

            if (report.Count == 0)
                report.Warnings.Add($"No live ZDOs found for prefab '{prefabName}'.");

            report.SuggestedCommands.Add($"cpc_repair_world --cleanup-zdos {prefabName} --dry-run");
            report.SuggestedCommands.Add($"cpc_dump_json world-zdos {prefabName}");
            return report;
        }

        // ── Crosshair target resolver ─────────────────────────────────────────

        internal static Character ResolveTargetCharacter(out string error)
        {
            error = null;
            if (Player.m_localPlayer == null) { error = "Local player not available."; return null; }

            Vector3 pos = Player.m_localPlayer.transform.position;
            Character closest = null;
            float closestDist = float.MaxValue;

            var characters = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            foreach (var ch in characters)
            {
                if (ch == null || ch is Player) continue;
                float d = Vector3.Distance(ch.transform.position, pos);
                if (d < closestDist) { closestDist = d; closest = ch; }
            }

            if (closest == null) { error = "No creatures found nearby."; return null; }
            return closest;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        internal static string NormalizeName(string name)
        {
            if (name == null) return "";
            if (name.EndsWith("(Clone)", StringComparison.Ordinal))
                return name.Substring(0, name.Length - 7).TrimEnd();
            return name;
        }

        private static string GetTransformPath(Transform t)
        {
            if (t == null) return "(null)";
            var sb = new System.Text.StringBuilder();
            sb.Append(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        private static T SafeInvoke<T>(object instance, string methodName)
        {
            if (instance == null) return default;
            try
            {
                var mi = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (mi == null) return default;
                return (T)mi.Invoke(instance, null);
            }
            catch { return default; }
        }

        private static object InvokeMethod(object instance, string methodName)
        {
            if (instance == null) return null;
            try
            {
                var mi = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                return mi?.Invoke(instance, null);
            }
            catch { return null; }
        }

        private static object TryGetField(object instance, string fieldName)
        {
            if (instance == null) return null;
            try
            {
                Type t = instance.GetType();
                FieldInfo fi = null;
                while (fi == null && t != null)
                {
                    fi = t.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    t = t.BaseType;
                }
                return fi?.GetValue(instance);
            }
            catch { return null; }
        }
    }
}
