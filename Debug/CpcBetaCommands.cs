using CreaturePrefabCreator.Config;
using CreaturePrefabCreator.Config.Advanced;
using CreaturePrefabCreator.Patches;
using CreaturePrefabCreator.RuntimeModifiers;
using CreaturePrefabCreator.Utilities;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CreaturePrefabCreator.Debug
{
    /// <summary>
    /// Beta validation and testing command suite for CPC 1.1.0-beta.
    /// All commands are read-only by default. Mutation requires --apply or --spawn.
    /// </summary>
    internal static class CpcBetaCommands
    {
        private const string TestSubjectMarkerKey = "cpc_beta_test_subject";
        private const string TestSubjectPrefabKey = "cpc_beta_prefab";

        public static void Register()
        {
            try
            {
                CommandManager.Instance.AddConsoleCommand(new BetaValidateCommand());
                CommandManager.Instance.AddConsoleCommand(new CheckAdvancedCommand());
                CommandManager.Instance.AddConsoleCommand(new TargetSnapshotCommand());
                CommandManager.Instance.AddConsoleCommand(new RuntimeTraceCommand());
                CommandManager.Instance.AddConsoleCommand(new RestoreTargetCommand());
                CommandManager.Instance.AddConsoleCommand(new CheckSaddledCommand());
                CommandManager.Instance.AddConsoleCommand(new SpawnTestSubjectCommand());
                CommandManager.Instance.AddConsoleCommand(new CleanupTestSubjectsCommand());
                CommandManager.Instance.AddConsoleCommand(new BetaReportCommand());
                CreaturePrefabCreatorPlugin.Instance?.Log("[CpcBeta] Beta command suite registered.");
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[CpcBeta] Failed to register beta commands: {ex.Message}");
            }
        }

        // ── Shared helpers ────────────────────────────────────────────────────

        private static void Log(string msg) => CreaturePrefabCreatorPlugin.Instance?.Log(msg);
        private static void Warn(string msg) => CreaturePrefabCreatorPlugin.Instance?.LogWarning(msg);

        private static string GetDumpDir() =>
            Path.Combine(BepInEx.Paths.BepInExRootPath, "config", "CreaturePrefabCreator", "dumps");

        private static void WriteJsonFile(string prefix, string json)
        {
            try
            {
                string dir = GetDumpDir();
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"beta-validation-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
                File.WriteAllText(file, json, Encoding.UTF8);
                Log($"[CpcBeta] Report written: {file}");
            }
            catch (Exception ex)
            {
                Warn($"[CpcBeta] Failed to write report: {ex.Message}");
            }
        }

        private static Character ResolveTarget(string prefabNameHint, out string err)
        {
            err = null;
            if (Player.m_localPlayer == null) { err = "Local player not available."; return null; }

            var all = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);

            if (!string.IsNullOrEmpty(prefabNameHint))
            {
                foreach (var ch in all)
                {
                    if (ch == null || ch is Player) continue;
                    string n = NormalizeName(ch.gameObject.name);
                    if (string.Equals(n, prefabNameHint, StringComparison.OrdinalIgnoreCase))
                        return ch;
                }
                err = $"No creature with prefab name '{prefabNameHint}' found nearby.";
                return null;
            }

            // Closest non-player creature
            Character closest = null;
            float closestDist = float.MaxValue;
            Vector3 pos = Player.m_localPlayer.transform.position;
            foreach (var ch in all)
            {
                if (ch == null || ch is Player) continue;
                float d = Vector3.Distance(ch.transform.position, pos);
                if (d < closestDist) { closestDist = d; closest = ch; }
            }
            if (closest == null) { err = "No creatures found nearby."; return null; }
            return closest;
        }

        private static string NormalizeName(string name)
        {
            if (name == null) return "";
            if (name.EndsWith("(Clone)", StringComparison.Ordinal))
                return name.Substring(0, name.Length - 7).TrimEnd();
            return name;
        }

        // ── Snapshot helpers ─────────────────────────────────────────────────

        internal class CreatureSnapshot
        {
            public string PrefabName;
            public string ObjectName;
            public string DisplayName;
            public string ZDOID;
            public bool IsOwner;
            public float Health;
            public float MaxHealth;
            public int Level;
            public bool Tamed;
            public bool Saddled;
            public bool Ridden;
            public bool BaseAIExists;
            public bool BaseAIEnabled;
            public bool MonsterAIExists;
            public bool MonsterAIEnabled;
            public bool AnimalAIExists;
            public bool AnimalAIEnabled;
            public bool PermanentAIDisabledMarker;
            public bool RuntimeAIDisabledByCpc;
            public string RuntimeAIDisableReason;
            public bool RuntimeAIEnabledByCpc;
            public string RuntimeAIEnableReason;
            public float Speed;
            public float WalkSpeed;
            public float RunSpeed;
            public float SwimSpeed;
        }

        private static CreatureSnapshot TakeSnapshot(Character ch)
        {
            if (ch == null) return null;
            var nview = ch.GetComponent<ZNetView>();
            bool znvValid = nview != null && nview.IsValid();
            ZDOID zdoid = znvValid ? nview.GetZDO().m_uid : new ZDOID(0, 0);

            float maxHealth = 0f;
            try
            {
                var m = typeof(Character).GetMethod("GetMaxHealth", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (m != null) maxHealth = (float)m.Invoke(ch, null);
            }
            catch { }
            if (maxHealth <= 0f)
            {
                var f = typeof(Character).GetField("m_maxHealth", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (f != null) { var v = f.GetValue(ch); if (v is float fv) maxHealth = fv; }
            }
            if (maxHealth <= 0f) maxHealth = ch.m_health;

            RuntimeModifierManager.IsRuntimeAIDisabled(ch, out string disableReason);
            RuntimeModifierManager.IsRuntimeAIEnabled(ch, out string enableReason);

            var baseAI    = ch.GetComponent<BaseAI>();
            var monsterAI = ch.GetComponent<MonsterAI>();
            var animalAI  = ch.GetComponent<AnimalAI>();

            return new CreatureSnapshot
            {
                PrefabName = NormalizeName(ch.gameObject.name),
                ObjectName = ch.gameObject.name,
                DisplayName = ch.m_name,
                ZDOID = zdoid.ToString(),
                IsOwner = znvValid && nview.IsOwner(),
                Health = ch.m_health,
                MaxHealth = maxHealth,
                Level = ch.GetLevel(),
                Tamed = ch.IsTamed(),
                Saddled = SaddledCreaturePatch.IsSaddledViaCanonicalPath(ch),
                Ridden = SaddledCreaturePatch.IsActivelyRidden(ch),
                BaseAIExists = baseAI != null,
                BaseAIEnabled = baseAI?.enabled ?? false,
                MonsterAIExists = monsterAI != null,
                MonsterAIEnabled = monsterAI?.enabled ?? false,
                AnimalAIExists = animalAI != null,
                AnimalAIEnabled = animalAI?.enabled ?? false,
                PermanentAIDisabledMarker = ch.GetComponent<PermanentAIDisabledMarker>() != null,
                RuntimeAIDisabledByCpc = RuntimeModifierManager.IsRuntimeAIDisabled(ch, out _),
                RuntimeAIDisableReason = disableReason,
                RuntimeAIEnabledByCpc = RuntimeModifierManager.IsRuntimeAIEnabled(ch, out _),
                RuntimeAIEnableReason = enableReason,
                Speed = ch.m_speed,
                WalkSpeed = ch.m_walkSpeed,
                RunSpeed = ch.m_runSpeed,
                SwimSpeed = ch.m_swimSpeed,
            };
        }

        private static void PrintSnapshot(CreatureSnapshot s, string label)
        {
            if (s == null) { Log($"[CpcBeta] {label}: (null)"); return; }
            Log($"[CpcBeta] {label}: {s.PrefabName} ({s.DisplayName}) ZDOID={s.ZDOID} owner={s.IsOwner}");
            Log($"[CpcBeta]   health={s.Health:F1}/{s.MaxHealth:F1}  level={s.Level}  tamed={s.Tamed}  saddled={s.Saddled}  ridden={s.Ridden}");
            Log($"[CpcBeta]   baseAI={s.BaseAIExists}/{s.BaseAIEnabled}  monsterAI={s.MonsterAIExists}/{s.MonsterAIEnabled}  animalAI={s.AnimalAIExists}/{s.AnimalAIEnabled}");
            Log($"[CpcBeta]   permanentMarker={s.PermanentAIDisabledMarker}  runtimeDisabled={s.RuntimeAIDisabledByCpc}  runtimeEnabled={s.RuntimeAIEnabledByCpc}");
            Log($"[CpcBeta]   speed={s.Speed:F2}  walk={s.WalkSpeed:F2}  run={s.RunSpeed:F2}  swim={s.SwimSpeed:F2}");
        }

        // ── Config validation helpers ─────────────────────────────────────────

        internal class AdvancedConfigValidation
        {
            public List<string> InvalidRules = new List<string>();
            public List<string> UnsupportedTier2Fields = new List<string>();
            public List<string> UnsupportedTier3Fields = new List<string>();
            public List<string> InvalidDeathEffectModes = new List<string>();
            public List<string> RuntimePerTypeDamageFields = new List<string>();
            public List<string> MissingPrefabs = new List<string>();
            public List<string> MovementSpeedBroadUsage = new List<string>();
            public List<string> InvalidMultiplierRanges = new List<string>();
        }

        private static readonly string[] ValidDeathEffectModes = { "vanilla", "none", "copyFrom", "customPrefab" };

        private static AdvancedConfigValidation ValidateConfig(CreaturePrefabCreatorConfigRoot cfg)
        {
            var result = new AdvancedConfigValidation();
            if (cfg == null) return result;

            // Runtime modifiers
            if (cfg.RuntimeModifiers != null)
            {
                for (int i = 0; i < cfg.RuntimeModifiers.Count; i++)
                {
                    var rule = cfg.RuntimeModifiers[i];
                    if (!rule.IsValid(out string err))
                    {
                        result.InvalidRules.Add($"runtimeModifiers[{i}] '{rule.TargetPrefab}': {err}");
                        continue;
                    }
                    CheckAdvancedFields($"runtimeModifiers[{i}]:{rule.TargetPrefab}", rule.Effects?.Advanced, result);
                }
            }

            // Prefab overrides
            if (cfg.PrefabOverrides != null)
            {
                for (int i = 0; i < cfg.PrefabOverrides.Count; i++)
                {
                    var ov = cfg.PrefabOverrides[i];
                    CheckAdvancedFields($"prefabOverrides[{i}]:{ov.TargetPrefab}", ov.Advanced, result);
                    if (!string.IsNullOrEmpty(ov.TargetPrefab) && ZNetScene.instance != null)
                    {
                        if (ZNetScene.instance.GetPrefab(ov.TargetPrefab) == null)
                            result.MissingPrefabs.Add($"prefabOverrides[{i}]: targetPrefab '{ov.TargetPrefab}' not found in ZNetScene");
                    }
                }
            }

            // Generated prefabs
            if (cfg.GeneratedPrefabs != null)
            {
                for (int i = 0; i < cfg.GeneratedPrefabs.Count; i++)
                {
                    var gp = cfg.GeneratedPrefabs[i];
                    CheckAdvancedFields($"generatedPrefabs[{i}]:{gp.NewPrefab}", gp.Advanced, result);
                    if (!string.IsNullOrEmpty(gp.SourcePrefab) && ZNetScene.instance != null)
                    {
                        if (ZNetScene.instance.GetPrefab(gp.SourcePrefab) == null)
                            result.MissingPrefabs.Add($"generatedPrefabs[{i}]: sourcePrefab '{gp.SourcePrefab}' not found in ZNetScene");
                    }
                }
            }

            return result;
        }

        private static void CheckAdvancedFields(string context, AdvancedModifierConfig adv, AdvancedConfigValidation result)
        {
            if (adv == null) return;

            if (adv.Damage != null)
            {
                var perType = new[] { "blunt", "slash", "pierce", "chop", "pickaxe", "fire", "frost", "lightning", "poison", "spirit", "base" };
                var vals = new float?[] { adv.Damage.Blunt, adv.Damage.Slash, adv.Damage.Pierce, adv.Damage.Chop,
                    adv.Damage.Pickaxe, adv.Damage.Fire, adv.Damage.Frost, adv.Damage.Lightning,
                    adv.Damage.Poison, adv.Damage.Spirit, adv.Damage.Base };
                for (int k = 0; k < perType.Length; k++)
                    if (vals[k].HasValue)
                        result.RuntimePerTypeDamageFields.Add($"{context}: advanced.damage.{perType[k]}={vals[k].Value} (runtime no-op, schema-only)");
            }

            if (adv.DropsAndDeath?.DeathEffect?.Mode != null)
            {
                bool valid = false;
                foreach (var m in ValidDeathEffectModes)
                    if (adv.DropsAndDeath.DeathEffect.Mode.Equals(m, StringComparison.OrdinalIgnoreCase)) { valid = true; break; }
                if (!valid)
                    result.InvalidDeathEffectModes.Add($"{context}: advanced.dropsAndDeath.deathEffect.mode='{adv.DropsAndDeath.DeathEffect.Mode}' is invalid");
                if (adv.DropsAndDeath.DeathEffect.Mode.Equals("customPrefab", StringComparison.OrdinalIgnoreCase))
                    result.UnsupportedTier3Fields.Add($"{context}: advanced.dropsAndDeath.deathEffect.mode=customPrefab (Tier 3, no-op)");
                if (adv.DropsAndDeath.DeathEffect.Mode.Equals("copyFrom", StringComparison.OrdinalIgnoreCase) &&
                    string.IsNullOrEmpty(adv.DropsAndDeath.DeathEffect.CopyFrom))
                    result.InvalidDeathEffectModes.Add($"{context}: deathEffect.mode=copyFrom but copyFrom is empty");
            }

            if (adv.MovementSpeed?.Multiplier.HasValue == true)
                result.MovementSpeedBroadUsage.Add($"{context}: advanced.movementSpeed.multiplier={adv.MovementSpeed.Multiplier.Value} (broad fallback)");

            CheckMultiplierRange(context, "advanced.health.multiplier", adv.Health?.Multiplier, result);
            CheckMultiplierRange(context, "advanced.health.maxHealth (absolute)", adv.Health?.MaxHealth, result, 1f, 100000f);
            CheckMultiplierRange(context, "advanced.movementSpeed.multiplier", adv.MovementSpeed?.Multiplier, result);
            CheckMultiplierRange(context, "advanced.movementSpeed.base", adv.MovementSpeed?.Base, result);
            CheckMultiplierRange(context, "advanced.movementSpeed.walk", adv.MovementSpeed?.Walk, result);
            CheckMultiplierRange(context, "advanced.movementSpeed.run", adv.MovementSpeed?.Run, result);
            CheckMultiplierRange(context, "advanced.movementSpeed.swim", adv.MovementSpeed?.Swim, result);

            if (adv.HasTier2Value)
                result.UnsupportedTier2Fields.Add($"{context}: has Tier 2 (schema-only) fields");
            if (adv.HasTier3Value)
                result.UnsupportedTier3Fields.Add($"{context}: has Tier 3 (audit-required) fields");
        }

        private static void CheckMultiplierRange(string ctx, string field, float? value, AdvancedConfigValidation result,
            float min = 0.01f, float max = 100f)
        {
            if (!value.HasValue || value.Value == 0f || value.Value == 1f) return;
            float v = value.Value;
            if (v < min || v > max || float.IsNaN(v) || float.IsInfinity(v))
                result.InvalidMultiplierRanges.Add($"{ctx}: {field}={v} is out of valid range [{min}, {max}]");
        }

        private static void PrintValidationResults(AdvancedConfigValidation v)
        {
            int total = v.InvalidRules.Count + v.InvalidDeathEffectModes.Count +
                        v.RuntimePerTypeDamageFields.Count + v.InvalidMultiplierRanges.Count + v.MissingPrefabs.Count;
            int warnings = v.UnsupportedTier2Fields.Count + v.UnsupportedTier3Fields.Count + v.MovementSpeedBroadUsage.Count;

            if (total == 0 && warnings == 0) { Log("[CpcBeta] Config validation: PASS - no issues found."); return; }

            if (v.InvalidRules.Count > 0) { Log("[CpcBeta] FAIL - Invalid rules:"); foreach (var s in v.InvalidRules) Log($"  {s}"); }
            if (v.InvalidDeathEffectModes.Count > 0) { Log("[CpcBeta] FAIL - Invalid deathEffect modes:"); foreach (var s in v.InvalidDeathEffectModes) Log($"  {s}"); }
            if (v.InvalidMultiplierRanges.Count > 0) { Log("[CpcBeta] FAIL - Invalid multiplier ranges:"); foreach (var s in v.InvalidMultiplierRanges) Log($"  {s}"); }
            if (v.MissingPrefabs.Count > 0) { Log("[CpcBeta] FAIL - Missing prefabs:"); foreach (var s in v.MissingPrefabs) Log($"  {s}"); }
            if (v.RuntimePerTypeDamageFields.Count > 0) { Log("[CpcBeta] WARN - Runtime per-type damage (schema-only, no-op):"); foreach (var s in v.RuntimePerTypeDamageFields) Log($"  {s}"); }
            if (v.UnsupportedTier2Fields.Count > 0) { Log("[CpcBeta] WARN - Unsupported Tier 2 fields:"); foreach (var s in v.UnsupportedTier2Fields) Log($"  {s}"); }
            if (v.UnsupportedTier3Fields.Count > 0) { Log("[CpcBeta] WARN - Unsupported Tier 3 fields:"); foreach (var s in v.UnsupportedTier3Fields) Log($"  {s}"); }
            if (v.MovementSpeedBroadUsage.Count > 0) { Log("[CpcBeta] INFO - MovementSpeed broad multiplier:"); foreach (var s in v.MovementSpeedBroadUsage) Log($"  {s}"); }

            string status = total > 0 ? "FAIL" : "WARN";
            Log($"[CpcBeta] Config validation: {status} ({total} errors, {warnings} warnings)");
        }

        // ── JSON report builder ───────────────────────────────────────────────

        private static string BuildBetaReport(
            CreatureSnapshot before, CreatureSnapshot after,
            AdvancedConfigValidation validation,
            RuntimeCheckResult ruleTrace,
            bool healthRestored, bool speedRestored, bool aiRestored,
            List<string> cleanupErrors, string status, string summary,
            List<string> failedChecks, List<string> warnings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");

            var plugin = CreaturePrefabCreatorPlugin.Instance;
            var info = System.Reflection.Assembly.GetExecutingAssembly().GetName();

            sb.AppendLine("  \"schemaVersion\": 2,");
            sb.AppendLine("  \"reportType\": \"beta_validation\",");
            sb.AppendLine($"  \"timestamp\": \"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\",");
            sb.AppendLine("  \"header\": {");
            sb.AppendLine($"    \"pluginVersion\": \"{info.Version}\",");
            sb.AppendLine($"    \"gameVersion\": \"{Application.version}\",");
            sb.AppendLine($"    \"isServer\": {BoolStr(ZNet.instance != null && ZNet.instance.IsServer())}");
            sb.AppendLine("  },");

            sb.AppendLine("  \"environment\": {");
            sb.AppendLine("    \"detectedMods\": {");
            sb.AppendLine($"      \"Jotunn\": {BoolStr(BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("com.jotunn.jotunn"))},");
            sb.AppendLine($"      \"AllTameable\": {BoolStr(SaddledCreaturePatch.AllTameableDetected)},");
            sb.AppendLine($"      \"MountUpRestored\": {BoolStr(SaddledCreaturePatch.MountUpDetected)}");
            sb.AppendLine("    },");
            sb.AppendLine("    \"featureGates\": {");
            sb.AppendLine($"      \"EnableGeneratedPrefabs\": {BoolStr(plugin?.ConfigEnableGeneratedPrefabs?.Value ?? false)},");
            sb.AppendLine($"      \"EnablePrefabOverrides\": {BoolStr(plugin?.ConfigEnablePrefabOverrides?.Value ?? false)},");
            sb.AppendLine($"      \"EnableRuntimeModifiers\": {BoolStr(plugin?.ConfigEnableRuntimeModifiers?.Value ?? false)},");
            sb.AppendLine($"      \"EnableRidingAISuppression\": {BoolStr(plugin?.ConfigEnableRuntimeModifiers?.Value ?? false)}");
            sb.AppendLine("    }");
            sb.AppendLine("  },");

            AppendValidationSection(sb, validation);
            AppendSnapshotSection(sb, "targetSnapshotBefore", before);
            AppendRuleTraceSection(sb, ruleTrace);
            AppendSnapshotSection(sb, "targetSnapshotAfter", after);

            sb.AppendLine("  \"restoreResult\": {");
            sb.AppendLine($"    \"healthRestored\": {BoolStr(healthRestored)},");
            sb.AppendLine($"    \"speedRestored\": {BoolStr(speedRestored)},");
            sb.AppendLine($"    \"aiRestored\": {BoolStr(aiRestored)},");
            sb.Append("    \"cleanupErrors\": [");
            if (cleanupErrors != null && cleanupErrors.Count > 0)
            {
                sb.AppendLine();
                for (int i = 0; i < cleanupErrors.Count; i++)
                    sb.AppendLine($"      \"{Esc(cleanupErrors[i])}\"{(i < cleanupErrors.Count - 1 ? "," : "")}");
                sb.Append("    ");
            }
            sb.AppendLine("]");
            sb.AppendLine("  },");

            sb.AppendLine("  \"result\": {");
            sb.AppendLine($"    \"status\": \"{status}\",");
            sb.AppendLine($"    \"summary\": \"{Esc(summary)}\",");
            AppendStringList(sb, "failedChecks", failedChecks, "    ");
            sb.AppendLine(",");
            AppendStringList(sb, "warnings", warnings, "    ");
            sb.AppendLine();
            sb.AppendLine("  }");
            sb.Append("}");
            return sb.ToString();
        }

        private static void AppendValidationSection(StringBuilder sb, AdvancedConfigValidation v)
        {
            sb.AppendLine("  \"configValidation\": {");
            AppendStringList(sb, "invalidRules", v?.InvalidRules, "    ");
            sb.AppendLine(",");
            AppendStringList(sb, "unsupportedTier2Fields", v?.UnsupportedTier2Fields, "    ");
            sb.AppendLine(",");
            AppendStringList(sb, "unsupportedTier3Fields", v?.UnsupportedTier3Fields, "    ");
            sb.AppendLine(",");
            AppendStringList(sb, "invalidDeathEffectModes", v?.InvalidDeathEffectModes, "    ");
            sb.AppendLine(",");
            AppendStringList(sb, "riskyRuntimeDamageFields", v?.RuntimePerTypeDamageFields, "    ");
            sb.AppendLine(",");
            AppendStringList(sb, "missingPrefabs", v?.MissingPrefabs, "    ");
            sb.AppendLine();
            sb.AppendLine("  },");
        }

        private static void AppendSnapshotSection(StringBuilder sb, string key, CreatureSnapshot s)
        {
            sb.AppendLine($"  \"{key}\": {{");
            if (s == null) { sb.AppendLine("    \"present\": false"); sb.AppendLine("  },"); return; }
            sb.AppendLine($"    \"prefabName\": \"{Esc(s.PrefabName)}\",");
            sb.AppendLine($"    \"zdoid\": \"{Esc(s.ZDOID)}\",");
            sb.AppendLine($"    \"isOwner\": {BoolStr(s.IsOwner)},");
            sb.AppendLine($"    \"health\": {s.Health:G},");
            sb.AppendLine($"    \"maxHealth\": {s.MaxHealth:G},");
            sb.AppendLine($"    \"level\": {s.Level},");
            sb.AppendLine($"    \"tamed\": {BoolStr(s.Tamed)},");
            sb.AppendLine($"    \"saddled\": {BoolStr(s.Saddled)},");
            sb.AppendLine($"    \"ridden\": {BoolStr(s.Ridden)},");
            sb.AppendLine($"    \"speed\": {s.Speed:G},");
            sb.AppendLine($"    \"walkSpeed\": {s.WalkSpeed:G},");
            sb.AppendLine($"    \"runSpeed\": {s.RunSpeed:G},");
            sb.AppendLine($"    \"swimSpeed\": {s.SwimSpeed:G},");
            sb.AppendLine($"    \"baseAIExists\": {BoolStr(s.BaseAIExists)},");
            sb.AppendLine($"    \"baseAIEnabled\": {BoolStr(s.BaseAIEnabled)},");
            sb.AppendLine($"    \"monsterAIExists\": {BoolStr(s.MonsterAIExists)},");
            sb.AppendLine($"    \"monsterAIEnabled\": {BoolStr(s.MonsterAIEnabled)},");
            sb.AppendLine($"    \"animalAIExists\": {BoolStr(s.AnimalAIExists)},");
            sb.AppendLine($"    \"animalAIEnabled\": {BoolStr(s.AnimalAIEnabled)},");
            sb.AppendLine($"    \"permanentAIDisabledMarker\": {BoolStr(s.PermanentAIDisabledMarker)},");
            sb.AppendLine($"    \"runtimeAIDisabledByCpc\": {BoolStr(s.RuntimeAIDisabledByCpc)},");
            sb.AppendLine($"    \"runtimeAIEnabledByCpc\": {BoolStr(s.RuntimeAIEnabledByCpc)}");
            sb.AppendLine("  },");
        }

        private static void AppendRuleTraceSection(StringBuilder sb, RuntimeCheckResult rt)
        {
            sb.AppendLine("  \"ruleTrace\": {");
            if (rt == null) { sb.AppendLine("    \"present\": false"); sb.AppendLine("  },"); return; }
            sb.AppendLine($"    \"totalValidRules\": {rt.TotalValidRules},");
            sb.AppendLine($"    \"matchingRuleCount\": {rt.MatchingRuleCount},");
            sb.AppendLine($"    \"ownershipAllowed\": {BoolStr(rt.IsOwner)},");
            sb.Append("    \"matchingRules\": [");
            if (rt.RuleDetails != null && rt.RuleDetails.Count > 0)
            {
                sb.AppendLine();
                for (int i = 0; i < rt.RuleDetails.Count; i++)
                {
                    var d = rt.RuleDetails[i];
                    sb.AppendLine($"      {{ \"ruleIndex\": {d.RuleIndex}, \"conditionsPass\": {BoolStr(d.ConditionsPass)}, \"conditionCount\": {d.Conditions?.Count ?? 0} }}{(i < rt.RuleDetails.Count - 1 ? "," : "")}");
                }
                sb.Append("    ");
            }
            sb.AppendLine("]");
            sb.AppendLine("  },");
        }

        private static void AppendStringList(StringBuilder sb, string key, List<string> items, string indent)
        {
            sb.Append($"{indent}\"{key}\": [");
            if (items != null && items.Count > 0)
            {
                sb.AppendLine();
                for (int i = 0; i < items.Count; i++)
                    sb.AppendLine($"{indent}  \"{Esc(items[i])}\"{(i < items.Count - 1 ? "," : "")}");
                sb.Append($"{indent}]");
            }
            else sb.Append("]");
        }

        private static string BoolStr(bool v) => v ? "true" : "false";
        private static string Esc(string s) => s == null ? "" : s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "");

        // ── cpc_beta_validate ─────────────────────────────────────────────────

        private class BetaValidateCommand : ConsoleCommand
        {
            public override string Name => "cpc_beta_validate";
            public override string Help =>
                "Beta validation. Usage: cpc_beta_validate [prefabName] [--apply] [--spawn] [--keep] [--mountup] [--alltameable]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                string prefabHint = p.Positional.Count > 0 ? p.Positional[0] : null;
                bool doApply  = p.HasFlag("--apply");
                bool doSpawn  = p.HasFlag("--spawn");
                bool keepSpawn = p.HasFlag("--keep");
                bool mountup  = p.HasFlag("--mountup");
                bool allTame  = p.HasFlag("--alltameable");

                var plugin = CreaturePrefabCreatorPlugin.Instance;
                if (plugin == null) { Log("[cpc_beta_validate] Plugin not available."); return; }

                Log("[cpc_beta_validate] Starting beta validation...");

                // 1. Config validation (always safe)
                var cfg = plugin.LoadedConfig;
                var validation = ValidateConfig(cfg);
                PrintValidationResults(validation);

                // 2. Resolve target creature
                Character target = null;
                bool spawnedTestSubject = false;
                if (doSpawn && !string.IsNullOrEmpty(prefabHint))
                {
                    bool spawned = SpawnTestSubjectInternal(prefabHint, false, 1);
                    if (!spawned) { Log("[cpc_beta_validate] Failed to spawn test subject. Aborting."); return; }
                    spawnedTestSubject = true;
                    target = FindSpawnedTestSubject(prefabHint);
                    if (target == null) Log("[cpc_beta_validate] Spawn queued but creature not yet instantiated (normal). Proceeding without target.");
                }
                else
                {
                    target = ResolveTarget(prefabHint, out string resolveErr);
                    if (target == null) { Log($"[cpc_beta_validate] No target: {resolveErr}"); }
                }

                // 3. Snapshot before
                CreatureSnapshot before = target != null ? TakeSnapshot(target) : null;
                if (before != null) PrintSnapshot(before, "BEFORE");

                // 4. Rule trace (read-only)
                RuntimeCheckResult ruleTrace = target != null ? RuntimeModifierManager.GetRuntimeCheckDebug(target) : null;
                if (ruleTrace != null)
                {
                    Log($"[cpc_beta_validate] Rule trace: {ruleTrace.MatchingRuleCount} matching rule(s) (of {ruleTrace.TotalValidRules} total valid). isOwner={ruleTrace.IsOwner}");
                    foreach (var d in ruleTrace.RuleDetails)
                        Log($"  rule[{d.RuleIndex}] conditionsPass={d.ConditionsPass} disableAI={d.EffectDisableAI} enableAI={d.EffectEnableAI} health={d.EffectHealth} speed={d.EffectSpeed}");
                }

                // 5. Optional --apply: temporarily evaluate (owner-only)
                CreatureSnapshot after = null;
                bool healthRestored = false, speedRestored = false, aiRestored = false;
                var cleanupErrors = new List<string>();

                if (doApply && target != null)
                {
                    var nview = target.GetComponent<ZNetView>();
                    if (nview == null || !nview.IsValid() || !nview.IsOwner())
                    {
                        Log("[cpc_beta_validate] --apply skipped: not owner or no valid ZNetView.");
                    }
                    else
                    {
                        Log("[cpc_beta_validate] --apply: evaluating runtime rules...");
                        RuntimeModifierManager.ForceEvaluateSingle(target);
                        after = TakeSnapshot(target);
                        PrintSnapshot(after, "AFTER --apply");

                        // Restore
                        try
                        {
                            var (res, skip, stale) = RuntimeModifierManager.RestoreRuntimeAI(target);
                            aiRestored = res > 0 || stale > 0;
                        }
                        catch (Exception ex) { cleanupErrors.Add($"AI restore: {ex.Message}"); }
                        after = TakeSnapshot(target);
                        PrintSnapshot(after, "AFTER restore");
                        healthRestored = before != null && after != null && Math.Abs(after.MaxHealth - before.MaxHealth) < 0.1f;
                        speedRestored  = before != null && after != null && Math.Abs(after.RunSpeed - before.RunSpeed) < 0.01f;
                    }
                }

                // 6. Compat flags
                if (mountup && target != null)
                {
                    Log($"[cpc_beta_validate] MountUp compat: detected={SaddledCreaturePatch.MountUpDetected} typeResolved={SaddledCreaturePatch.MountUpTypeResolved}");
                    Log($"  canonicalSaddled={SaddledCreaturePatch.IsSaddledViaCanonicalPath(target)} canonicalRidden={SaddledCreaturePatch.IsActivelyRidden(target)}");
                }
                if (allTame && target != null)
                {
                    Log($"[cpc_beta_validate] AllTameable compat: detected={SaddledCreaturePatch.AllTameableDetected}");
                    bool hasGrowup = target.GetComponent<GeneratedPrefabs.OffspringGrowup>() != null;
                    Log($"  hasOffspringGrowup={hasGrowup} isTamed={target.IsTamed()}");
                }

                // 7. Determine status
                var failedChecks = new List<string>();
                var reportWarnings = new List<string>();
                failedChecks.AddRange(validation.InvalidRules);
                failedChecks.AddRange(validation.InvalidDeathEffectModes);
                failedChecks.AddRange(validation.InvalidMultiplierRanges);
                failedChecks.AddRange(validation.MissingPrefabs);
                reportWarnings.AddRange(validation.RuntimePerTypeDamageFields);
                reportWarnings.AddRange(validation.UnsupportedTier2Fields);
                reportWarnings.AddRange(validation.UnsupportedTier3Fields);
                if (cleanupErrors.Count > 0) failedChecks.AddRange(cleanupErrors);

                string status = failedChecks.Count > 0 ? "FAIL" : (reportWarnings.Count > 0 ? "WARN" : "PASS");
                string summary = $"{status}: {failedChecks.Count} error(s), {reportWarnings.Count} warning(s).";
                Log($"[cpc_beta_validate] Result: {summary}");

                // 8. Write JSON report
                string json = BuildBetaReport(before, after, validation, ruleTrace,
                    healthRestored, speedRestored, aiRestored, cleanupErrors,
                    status, summary, failedChecks, reportWarnings);
                WriteJsonFile("beta-validation", json);

                // 9. Clean up spawned test subject unless --keep
                if (spawnedTestSubject && !keepSpawn)
                {
                    if (target != null)
                    {
                        try { ZNetScene.instance?.Destroy(target.gameObject); Log("[cpc_beta_validate] Test subject removed."); }
                        catch (Exception ex) { Warn($"[cpc_beta_validate] Failed to remove test subject: {ex.Message}"); }
                    }
                    else Log("[cpc_beta_validate] Test subject not found for cleanup (may not have spawned yet). Use cpc_cleanup_test_subjects.");
                }
            }
        }

        // ── cpc_check_advanced ────────────────────────────────────────────────

        private class CheckAdvancedCommand : ConsoleCommand
        {
            public override string Name => "cpc_check_advanced";
            public override string Help => "Validate all advanced config fields. Usage: cpc_check_advanced";

            public override void Run(string[] args)
            {
                var cfg = CreaturePrefabCreatorPlugin.Instance?.LoadedConfig;
                if (cfg == null) { Log("[cpc_check_advanced] No config loaded."); return; }

                Log("[cpc_check_advanced] Validating advanced config fields...");
                var v = ValidateConfig(cfg);
                PrintValidationResults(v);
            }
        }

        // ── cpc_target_snapshot ───────────────────────────────────────────────

        private class TargetSnapshotCommand : ConsoleCommand
        {
            public override string Name => "cpc_target_snapshot";
            public override string Help => "Dump creature state. Usage: cpc_target_snapshot [prefabName]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                string hint = p.Positional.Count > 0 ? p.Positional[0] : null;
                var ch = ResolveTarget(hint, out string err);
                if (ch == null) { Log($"[cpc_target_snapshot] {err}"); return; }

                var snap = TakeSnapshot(ch);
                PrintSnapshot(snap, "SNAPSHOT");

                var rt = RuntimeModifierManager.GetRuntimeCheckDebug(ch);
                if (rt != null && rt.MatchingRuleCount > 0)
                {
                    Log($"[cpc_target_snapshot] {rt.MatchingRuleCount} matching runtime rule(s):");
                    foreach (var d in rt.RuleDetails)
                        Log($"  rule[{d.RuleIndex}] conditionsPass={d.ConditionsPass}");
                }
                else
                    Log("[cpc_target_snapshot] No matching runtime rules.");
            }
        }

        // ── cpc_runtime_trace ─────────────────────────────────────────────────

        private class RuntimeTraceCommand : ConsoleCommand
        {
            public override string Name => "cpc_runtime_trace";
            public override string Help => "Trace runtime rule evaluation for a creature. Usage: cpc_runtime_trace [prefabName]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                string hint = p.Positional.Count > 0 ? p.Positional[0] : null;
                var ch = ResolveTarget(hint, out string err);
                if (ch == null) { Log($"[cpc_runtime_trace] {err}"); return; }

                var rt = RuntimeModifierManager.GetRuntimeCheckDebug(ch);
                if (rt == null) { Log("[cpc_runtime_trace] Could not get runtime debug info."); return; }

                Log($"[cpc_runtime_trace] {rt.PrefabName} | ZDOID={rt.ZDOID} | isOwner={rt.IsOwner} | isServer={rt.IsServer}");
                Log($"  totalValidRules={rt.TotalValidRules}  matchingRuleCount={rt.MatchingRuleCount}");
                Log($"  runtimeAIDisabled={rt.RuntimeAIDisabledByCpc}  runtimeAIEnabled={rt.RuntimeAIEnabledByCpc}  permanentMarker={rt.HasPermanentMarker}");

                if (rt.RuleDetails == null || rt.RuleDetails.Count == 0)
                {
                    Log("[cpc_runtime_trace] No matching rules for this prefab.");
                    return;
                }

                foreach (var d in rt.RuleDetails)
                {
                    Log($"  --- rule[{d.RuleIndex}] conditionsPass={d.ConditionsPass} ---");
                    if (d.Conditions != null)
                    {
                        foreach (var c in d.Conditions)
                            Log($"    condition '{c.Name}': expected={c.Expected} actual={c.Actual} PASS={c.Pass} ({c.Source})");
                    }
                    Log($"    effects: disableAI={d.EffectDisableAI} enableAI={d.EffectEnableAI} health={d.EffectHealth} damage={d.EffectDamage} speed={d.EffectSpeed}");
                    Log($"    ownershipAllowed={rt.IsOwner}  finalAction={(d.ConditionsPass && rt.IsOwner ? "APPLY" : "SKIP")}");
                }
            }
        }

        // ── cpc_restore_target ────────────────────────────────────────────────

        private class RestoreTargetCommand : ConsoleCommand
        {
            public override string Name => "cpc_restore_target";
            public override string Help => "Emergency restore for a broken test creature. Usage: cpc_restore_target [prefabName]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                string hint = p.Positional.Count > 0 ? p.Positional[0] : null;
                var ch = ResolveTarget(hint, out string err);
                if (ch == null) { Log($"[cpc_restore_target] {err}"); return; }

                var nview = ch.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid() || !nview.IsOwner())
                {
                    Log("[cpc_restore_target] Not owner of this creature. Cannot restore."); return;
                }

                Log($"[cpc_restore_target] Restoring '{NormalizeName(ch.gameObject.name)}'...");

                var (res, skip, stale) = RuntimeModifierManager.RestoreRuntimeAI(ch);
                Log($"[cpc_restore_target] AI restore: restored={res} skipped={skip} stale={stale}");

                var snapAfter = TakeSnapshot(ch);
                PrintSnapshot(snapAfter, "AFTER restore");
                Log("[cpc_restore_target] Done. If issues persist, run cpc_reload_config.");
            }
        }

        // ── cpc_check_saddled ──────────────────────────────────────────────────

        private class CheckSaddledCommand : ConsoleCommand
        {
            public override string Name => "cpc_check_saddled";
            public override string Help => "Check saddle/ride state for the nearest (or named) creature. Usage: cpc_check_saddled [prefabName]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                string hint = p.Positional.Count > 0 ? p.Positional[0] : null;
                var ch = ResolveTarget(hint, out string err);
                if (ch == null) { Log($"[cpc_check_saddled] {err}"); return; }

                string prefabName = NormalizeName(ch.gameObject.name);
                bool saddled = SaddledCreaturePatch.IsSaddledViaCanonicalPath(ch);
                bool ridden  = SaddledCreaturePatch.IsActivelyRidden(ch);
                bool tamed   = ch.IsTamed();

                Log($"[cpc_check_saddled] {prefabName}");
                Log($"  tamed={tamed}  saddled={saddled}  ridden={ridden}");
                Log($"  mountUpDetected={SaddledCreaturePatch.MountUpDetected}  mountUpTypeResolved={SaddledCreaturePatch.MountUpTypeResolved}");

                if (!SaddledCreaturePatch.MountUpDetected)
                    Log("  [WARN] MountUpRestored not detected. Saddle detection uses vanilla path only.");
                else if (!SaddledCreaturePatch.MountUpTypeResolved)
                    Log("  [WARN] MountUpRestored detected but Mountable type could not be resolved via reflection.");
            }
        }

        // ── cpc_spawn_test_subject ────────────────────────────────────────────

        private static Vector3 _lastTestSubjectSpawnPos;

        private static bool SpawnTestSubjectInternal(string prefabName, bool tamed, int level)
        {
            if (Player.m_localPlayer == null || ZNetScene.instance == null) return false;

            var prefab = ZNetScene.instance.GetPrefab(prefabName);
            if (prefab == null) { Warn($"[CpcBeta] Prefab '{prefabName}' not found."); return false; }
            if (prefab.GetComponent<ZNetView>() == null) { Warn($"[CpcBeta] Prefab '{prefabName}' has no ZNetView."); return false; }

            Vector3 pos = Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.forward * 4f;
            _lastTestSubjectSpawnPos = pos;

            var tameable = tamed ? prefab.GetComponent<Tameable>() : null;
            bool orig = tameable != null && tameable.m_startsTamed;
            if (tameable != null) tameable.m_startsTamed = true;

            try
            {
                ZNetScene.instance.SpawnObject(pos, Quaternion.identity, prefab);
            }
            finally
            {
                if (tameable != null) tameable.m_startsTamed = orig;
            }

            // Defer ZDO marker to next frame so the ZDO has been created
            int markerHash = TestSubjectMarkerKey.GetStableHashCode();
            string capName = prefabName;
            Vector3 capPos = pos;
            CreaturePrefabCreatorPlugin.Instance?.RunCoroutine(MarkTestSubjectCoroutine(capName, capPos, markerHash));
            return true;
        }

        private static System.Collections.IEnumerator MarkTestSubjectCoroutine(string prefabName, Vector3 spawnPos, int markerHash)
        {
            yield return null;
            yield return null;
            var all = UnityEngine.Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None);
            foreach (var znv in all)
            {
                if (znv == null || znv.GetZDO() == null) continue;
                if (!znv.gameObject.name.StartsWith(prefabName)) continue;
                if (Vector3.Distance(znv.transform.position, spawnPos) > 5f) continue;
                znv.GetZDO().Set(markerHash, true);
                Log($"[CpcBeta] Test subject '{prefabName}' ZDO marked at {spawnPos}.");
                break;
            }
        }

        private static Character FindSpawnedTestSubject(string prefabName)
        {
            if (Player.m_localPlayer == null) return null;
            var candidates = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            Character best = null;
            float bestDist = float.MaxValue;
            foreach (var c in candidates)
            {
                if (c == null || c is Player) continue;
                if (!c.gameObject.name.StartsWith(prefabName)) continue;
                float d = Vector3.Distance(c.transform.position, _lastTestSubjectSpawnPos);
                if (d < bestDist) { bestDist = d; best = c; }
            }
            return best;
        }

        private class SpawnTestSubjectCommand : ConsoleCommand
        {
            public override string Name => "cpc_spawn_test_subject";
            public override string Help => "Spawn a marked beta test creature. Usage: cpc_spawn_test_subject <prefabName> [--tamed] [--level <n>] [--saddled]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                string prefabName = p.Positional.Count > 0 ? p.Positional[0] : p.GetOption("--prefab");
                if (string.IsNullOrEmpty(prefabName)) { Log("Usage: cpc_spawn_test_subject <prefabName> [--tamed] [--level <n>]"); return; }

                if (Player.m_localPlayer == null) { Log("[cpc_spawn_test_subject] Player not available."); return; }
                if (ZNetScene.instance == null) { Log("[cpc_spawn_test_subject] ZNetScene not available."); return; }

                bool tamed = p.HasFlag("--tamed") || p.HasFlag("--saddled");
                int level = 1;
                if (p.GetOption("--level") is string ls && int.TryParse(ls, out int lv)) level = Math.Max(1, lv);

                bool ok = SpawnTestSubjectInternal(prefabName, tamed, level);
                if (ok)
                    Log($"[cpc_spawn_test_subject] Spawned '{prefabName}' (tamed={tamed}, level={level}). ZDO will be marked next frame. Use cpc_cleanup_test_subjects to remove.");
                else
                    Log($"[cpc_spawn_test_subject] Failed to spawn '{prefabName}'.");
            }
        }

        // ── cpc_cleanup_test_subjects ─────────────────────────────────────────

        private class CleanupTestSubjectsCommand : ConsoleCommand
        {
            public override string Name => "cpc_cleanup_test_subjects";
            public override string Help => "Remove only CPC-spawned beta test creatures. Usage: cpc_cleanup_test_subjects [--dry-run]";

            public override void Run(string[] args)
            {
                var p = CpcCommandRouter.Parse(args);
                bool dryRun = p.HasFlag("--dry-run");

                int markerHash = TestSubjectMarkerKey.GetStableHashCode();
                var allZNVs = UnityEngine.Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None);
                int found = 0, removed = 0;

                foreach (var znv in allZNVs)
                {
                    if (znv == null || znv.GetZDO() == null) continue;
                    if (!znv.GetZDO().GetBool(markerHash)) continue;
                    found++;
                    if (!dryRun)
                    {
                        try
                        {
                            var ch = znv.GetComponent<Character>();
                            string name = ch != null ? NormalizeName(ch.gameObject.name) : znv.gameObject.name;
                            ZNetScene.instance.Destroy(znv.gameObject);
                            removed++;
                            Log($"[cpc_cleanup_test_subjects] Removed test subject: '{name}'.");
                        }
                        catch (Exception ex) { Warn($"[cpc_cleanup_test_subjects] Failed to remove: {ex.Message}"); }
                    }
                    else
                    {
                        string name = NormalizeName(znv.gameObject.name);
                        Log($"[cpc_cleanup_test_subjects] [dry-run] Would remove: '{name}'.");
                    }
                }

                if (found == 0) Log("[cpc_cleanup_test_subjects] No CPC test subjects found.");
                else if (dryRun) Log($"[cpc_cleanup_test_subjects] [dry-run] Found {found} test subject(s). Remove --dry-run to delete.");
                else Log($"[cpc_cleanup_test_subjects] Removed {removed}/{found} test subject(s).");
            }
        }

        // ── cpc_beta_report ───────────────────────────────────────────────────

        private class BetaReportCommand : ConsoleCommand
        {
            public override string Name => "cpc_beta_report";
            public override string Help => "Collect CPC runtime state and write JSON report. Usage: cpc_beta_report";

            public override void Run(string[] args)
            {
                var plugin = CreaturePrefabCreatorPlugin.Instance;
                if (plugin == null) { Log("[cpc_beta_report] Plugin not available."); return; }

                Log("[cpc_beta_report] Collecting runtime state...");

                var cfg = plugin.LoadedConfig;
                var validation = ValidateConfig(cfg);

                var status = RuntimeModifierManager.GetRuntimeStatus();
                var recentEvents = RuntimeModifierEventBuffer.GetRecent(20);

                var sb = new StringBuilder();
                sb.AppendLine("{");
                sb.AppendLine("  \"schemaVersion\": 2,");
                sb.AppendLine("  \"reportType\": \"beta_report\",");
                sb.AppendLine($"  \"timestamp\": \"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\",");

                sb.AppendLine("  \"featureGates\": {");
                sb.AppendLine($"    \"EnableGeneratedPrefabs\": {BoolStr(plugin.ConfigEnableGeneratedPrefabs?.Value ?? false)},");
                sb.AppendLine($"    \"EnablePrefabOverrides\": {BoolStr(plugin.ConfigEnablePrefabOverrides?.Value ?? false)},");
                sb.AppendLine($"    \"EnableRuntimeModifiers\": {BoolStr(plugin.ConfigEnableRuntimeModifiers?.Value ?? false)},");
                sb.AppendLine($"    \"VerboseLogging\": {BoolStr(plugin.ConfigVerboseLogging?.Value ?? false)},");
                sb.AppendLine($"    \"DebugAIState\": {BoolStr(plugin.ConfigDebugAIState?.Value ?? false)},");
                sb.AppendLine($"    \"DebugMountState\": {BoolStr(plugin.ConfigDebugMountState?.Value ?? false)}");
                sb.AppendLine("  },");

                sb.AppendLine("  \"runtimeStatus\": {");
                sb.AppendLine($"    \"enabled\": {BoolStr(status.Enabled)},");
                sb.AppendLine($"    \"totalRules\": {status.TotalRules},");
                sb.AppendLine($"    \"validRules\": {status.ValidRules},");
                sb.AppendLine($"    \"invalidRules\": {status.InvalidRules},");
                sb.AppendLine($"    \"runtimeAIDisabledCount\": {status.RuntimeAIDisabledCount},");
                sb.AppendLine($"    \"runtimeAIEnabledCount\": {status.RuntimeAIEnabledCount},");
                sb.AppendLine($"    \"mountUpDetected\": {BoolStr(status.MountUpDetected)},");
                sb.AppendLine($"    \"mountUpTypeResolved\": {BoolStr(status.MountUpTypeResolved)},");
                sb.AppendLine($"    \"allTameableDetected\": {BoolStr(status.AllTameableDetected)}");
                sb.AppendLine("  },");

                AppendValidationSection(sb, validation);

                sb.AppendLine("  \"recentEvents\": [");
                var evList = new List<RuntimeModifierEvent>(recentEvents);
                for (int i = 0; i < evList.Count; i++)
                {
                    var ev = evList[i];
                    sb.AppendLine($"    {{ \"time\": \"{ev.TimeUtc:HH:mm:ss}Z\", \"type\": \"{Esc(ev.EventType)}\", \"prefab\": \"{Esc(ev.PrefabName)}\", \"msg\": \"{Esc(ev.Message)}\" }}{(i < evList.Count - 1 ? "," : "")}");
                }
                sb.AppendLine("  ],");

                // Nearby creatures
                sb.AppendLine("  \"nearbyCreatures\": [");
                var allCh = UnityEngine.Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
                Vector3 playerPos = Player.m_localPlayer != null ? Player.m_localPlayer.transform.position : Vector3.zero;
                var nearby = allCh.Where(c => c != null && !(c is Player) && Vector3.Distance(c.transform.position, playerPos) < 30f).ToList();
                for (int i = 0; i < nearby.Count; i++)
                {
                    var ch = nearby[i];
                    string name = NormalizeName(ch.gameObject.name);
                    bool runtimeDisabled = RuntimeModifierManager.IsRuntimeAIDisabled(ch, out _);
                    bool runtimeEnabled  = RuntimeModifierManager.IsRuntimeAIEnabled(ch, out _);
                    sb.AppendLine($"    {{ \"prefab\": \"{Esc(name)}\", \"tamed\": {BoolStr(ch.IsTamed())}, \"runtimeAIDisabled\": {BoolStr(runtimeDisabled)}, \"runtimeAIEnabled\": {BoolStr(runtimeEnabled)} }}{(i < nearby.Count - 1 ? "," : "")}");
                }
                sb.AppendLine("  ]");
                sb.Append("}");

                WriteJsonFile("beta-report", sb.ToString());

                int totalIssues = validation.InvalidRules.Count + validation.InvalidDeathEffectModes.Count +
                                  validation.InvalidMultiplierRanges.Count + validation.MissingPrefabs.Count;
                Log($"[cpc_beta_report] Done. {totalIssues} config issue(s). {recentEvents.Count} recent events. {nearby.Count} nearby creatures.");
            }
        }
    }
}
