using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace CreaturePrefabCreator.Debug
{
    internal static class CpcJsonRenderer
    {
        private static void Log(string msg) => CreaturePrefabCreatorPlugin.Instance?.Log(msg);

        private static string GetDumpDirectory()
        {
            return Path.Combine(BepInEx.Paths.BepInExRootPath, "config", "CreaturePrefabCreator", "dumps");
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "dump";

            // SECURITY: Block path traversal attempts
            // Remove directory separators and parent directory references
            name = name.Replace("../", "_").Replace("..\\", "_");
            name = name.Replace("/", "_").Replace("\\", "_");
            name = name.Replace("..", "_");

            // Remove invalid filename characters
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            name = name.Replace(' ', '_');

            // Ensure .json extension
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) name += ".json";

            return name;
        }

        private static string BuildTimestampedName(string prefix)
        {
            return $"{prefix}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        }

        private static void Write(string dir, string fileName, string json)
        {
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllText(path, json, Encoding.UTF8);
            Log($"[CPC] Wrote: {path}");
        }

        // ── Live target ───────────────────────────────────────────────────────

        internal static void WriteLiveTarget(CpcLiveDiagnosticReport report, string outputOverride)
        {
            if (report == null) { Log("[CPC] No data to write."); return; }
            var sb = new StringBuilder();
            var w = new JsonWriter(sb);

            w.BeginObject();
            w.Field("schemaVersion", 1);
            w.Field("dumpType", "live_target");
            w.Field("createdAt", report.CreatedAt);
            WritePluginSection(w);
            WriteGameSection(w);
            w.BeginKey("target");
            WriteIdentityObject(w, report.Identity);
            w.BeginKey("diagnostics");
            w.BeginObject();
            if (report.AI != null) { w.BeginKey("ai"); WriteAIObject(w, report.AI); }
            if (report.Zdo != null) { w.BeginKey("zdo"); WriteZdoObject(w, report.Zdo); }
            if (report.Runtime != null) { w.BeginKey("runtime"); WriteRuntimeObject(w, report.Runtime); }
            if (report.MountUp != null) { w.BeginKey("mountUp"); WriteMountUpObject(w, report.MountUp); }
            if (report.AllTameable != null) { w.BeginKey("allTameable"); WriteAllTameableObject(w, report.AllTameable); }
            if (report.Generated != null) { w.BeginKey("generated"); WriteGeneratedLiveObject(w, report.Generated); }
            w.EndObject();
            WriteStringArray(w, "warnings", report.Warnings);
            WriteStringArray(w, "suggestedCommands", report.SuggestedCommands);
            w.EndObject();

            string prefab = CpcDiagnosticEngine.NormalizeName(report.Identity?.PrefabName ?? "unknown");
            string fileName = outputOverride != null ? SanitizeFileName(outputOverride) : BuildTimestampedName($"live_target_{prefab}");
            Write(GetDumpDirectory(), fileName, sb.ToString());
        }

        // ── Radius ────────────────────────────────────────────────────────────

        internal static void WriteRadius(CpcRadiusDiagnosticReport report, string outputOverride)
        {
            if (report == null) { Log("[CPC] No data to write."); return; }
            var sb = new StringBuilder();
            var w = new JsonWriter(sb);

            w.BeginObject();
            w.Field("schemaVersion", 1);
            w.Field("dumpType", "live_radius");
            w.Field("createdAt", report.CreatedAt);
            WritePluginSection(w);
            WriteGameSection(w);
            w.Field("radius", report.Radius);
            w.BeginKey("playerPosition"); WriteVec3(w, report.PlayerPosition);
            w.BeginKey("entries");
            w.BeginArray();
            foreach (var e in report.Entries)
            {
                w.BeginObject();
                w.BeginKey("identity"); WriteIdentityObject(w, e.Identity);
                w.Field("hasMonsterAI", e.HasMonsterAI);
                w.Field("hasAnimalAI", e.HasAnimalAI);
                w.Field("hasTameable", e.HasTameable);
                w.Field("hasOffspringGrowup", e.HasOffspringGrowup);
                w.Field("runtimeAIDisabled", e.RuntimeAIDisabled);
                w.Field("runtimeAIEnabled", e.RuntimeAIEnabled);
                w.EndObject();
            }
            w.EndArray();
            if (report.RuntimeSummary != null) { w.BeginKey("runtimeSummary"); WriteRuntimeObject(w, report.RuntimeSummary); }
            WriteStringArray(w, "warnings", report.Warnings);
            WriteStringArray(w, "suggestedCommands", report.SuggestedCommands);
            w.EndObject();

            string fileName = outputOverride != null ? SanitizeFileName(outputOverride) : BuildTimestampedName($"live_radius_{(int)report.Radius}m");
            Write(GetDumpDirectory(), fileName, sb.ToString());
        }

        // ── Prefab ────────────────────────────────────────────────────────────

        internal static void WritePrefab(CpcPrefabDiagnosticReport report, string outputOverride)
        {
            if (report == null) { Log("[CPC] No data to write."); return; }
            var sb = new StringBuilder();
            var w = new JsonWriter(sb);

            w.BeginObject();
            w.Field("schemaVersion", 1);
            w.Field("dumpType", "prefab");
            w.Field("createdAt", report.CreatedAt);
            WritePluginSection(w);
            WriteGameSection(w);
            w.Field("queryType", report.QueryType);
            w.Field("queryValue", report.QueryValue);

            if (report.VerifyReport != null)
            {
                w.BeginKey("verifyReport");
                WriteVerifyObject(w, report.VerifyReport);
            }
            else
            {
                w.BeginKey("results");
                w.BeginArray();
                foreach (var info in report.Results) WritePrefabInfoObject(w, info);
                w.EndArray();
            }

            WriteStringArray(w, "warnings", report.Warnings);
            WriteStringArray(w, "suggestedCommands", report.SuggestedCommands);
            w.EndObject();

            string tag = report.QueryValue?.Replace(' ', '_') ?? "prefab";
            string fileName = outputOverride != null ? SanitizeFileName(outputOverride) : BuildTimestampedName($"prefab_{tag}");
            Write(GetDumpDirectory(), fileName, sb.ToString());
        }

        // ── World ZDOs ────────────────────────────────────────────────────────

        internal static void WriteWorldZdos(CpcWorldZdoDiagnosticReport report, string outputOverride)
        {
            if (report == null) { Log("[CPC] No data to write."); return; }
            var sb = new StringBuilder();
            var w = new JsonWriter(sb);

            w.BeginObject();
            w.Field("schemaVersion", 1);
            w.Field("dumpType", "world_zdos");
            w.Field("createdAt", report.CreatedAt);
            WritePluginSection(w);
            WriteGameSection(w);
            w.Field("prefabName", report.PrefabName);
            w.Field("prefabHash", report.PrefabHash);
            w.Field("count", report.Count);
            w.BeginKey("entries");
            w.BeginArray();
            foreach (var e in report.Entries)
            {
                w.BeginObject();
                w.Field("zdoId", e.ZdoId);
                w.BeginKey("position"); WriteVec3(w, e.Position);
                w.Field("ownerPeerId", e.OwnerPeerId);
                w.Field("prefabHash", e.PrefabHash);
                w.Field("prefabName", e.PrefabName);
                w.EndObject();
            }
            w.EndArray();
            WriteStringArray(w, "warnings", report.Warnings);
            WriteStringArray(w, "suggestedCommands", report.SuggestedCommands);
            w.EndObject();

            string fileName = outputOverride != null ? SanitizeFileName(outputOverride) : BuildTimestampedName($"world_zdos_{report.PrefabName}");
            Write(GetDumpDirectory(), fileName, sb.ToString());
        }

        // ── Object serializers ────────────────────────────────────────────────

        private static void WritePluginSection(JsonWriter w)
        {
            var plugin = CreaturePrefabCreatorPlugin.Instance;
            w.BeginKey("plugin");
            w.BeginObject();
            w.Field("guid", "com.clickcs.creatureprefabcreator");
            w.Field("version", "0.1.0");
            w.EndObject();
        }

        private static void WriteGameSection(JsonWriter w)
        {
            w.BeginKey("game");
            w.BeginObject();
            w.Field("znetSceneReady", ZNetScene.instance != null);
            w.Field("isServer", ZNet.instance != null && ZNet.instance.IsServer());
            w.EndObject();
        }

        private static void WriteIdentityObject(JsonWriter w, CpcIdentityInfo id)
        {
            if (id == null) { w.Null(); return; }
            w.BeginObject();
            w.Field("objectName", id.ObjectName);
            w.Field("prefabName", id.PrefabName);
            w.BeginKey("position"); WriteVec3(w, id.Position);
            w.Field("distanceFromPlayer", id.DistanceFromPlayer);
            w.Field("zdoId", id.ZdoId);
            w.Field("znetViewValid", id.ZNetViewValid);
            w.Field("isOwner", id.IsOwner);
            w.Field("isServer", id.IsServer);
            w.EndObject();
        }

        private static void WriteAIObject(JsonWriter w, CpcAIInfo ai)
        {
            w.BeginObject();
            w.Field("baseAIExists", ai.BaseAIExists);
            w.Field("baseAIEnabled", ai.BaseAIEnabled);
            w.Field("monsterAIExists", ai.MonsterAIExists);
            w.Field("monsterAIEnabled", ai.MonsterAIEnabled);
            w.Field("animalAIExists", ai.AnimalAIExists);
            w.Field("animalAIEnabled", ai.AnimalAIEnabled);
            w.Field("permanentAIDisabled", ai.PermanentAIDisabled);
            w.Field("runtimeAIDisabledByCpc", ai.RuntimeAIDisabledByCpc);
            w.Field("runtimeDisableReason", ai.RuntimeDisableReason);
            w.Field("runtimeAIEnabledByCpc", ai.RuntimeAIEnabledByCpc);
            w.Field("runtimeEnableReason", ai.RuntimeEnableReason);
            w.Field("movementBlockerSummary", ai.MovementBlockerSummary);
            w.Field("viewRange", ai.ViewRange);
            w.Field("viewAngle", ai.ViewAngle);
            w.Field("hearRange", ai.HearRange);
            w.Field("randomMoveInterval", ai.RandomMoveInterval);
            w.Field("randomMoveRange", ai.RandomMoveRange);
            w.Field("avoidWater", ai.AvoidWater);
            w.Field("consumeItemCount", ai.ConsumeItemCount);
            w.Field("consumeRange", ai.ConsumeRange);
            w.Field("consumeSearchRange", ai.ConsumeSearchRange);
            w.Field("enableHuntPlayer", ai.EnableHuntPlayer);
            w.EndObject();
        }

        private static void WriteRuntimeObject(JsonWriter w, CpcRuntimeInfo rt)
        {
            w.BeginObject();
            w.Field("enabled", rt.RuntimeEnabled);
            w.Field("totalRules", rt.TotalRules);
            w.Field("validRules", rt.ValidRules);
            w.Field("invalidRules", rt.InvalidRules);
            w.Field("matchingRuleCount", rt.MatchingRuleCount);
            w.Field("runtimeAIDisabledByCpc", rt.RuntimeAIDisabledByCpc);
            w.Field("disableReason", rt.DisableReason);
            w.Field("runtimeAIEnabledByCpc", rt.RuntimeAIEnabledByCpc);
            w.Field("enableReason", rt.EnableReason);
            w.Field("originalBaseAIEnabled", rt.OriginalBaseAIEnabled);
            w.Field("originalMonsterAIEnabled", rt.OriginalMonsterAIEnabled);
            w.Field("originalAnimalAIEnabled", rt.OriginalAnimalAIEnabled);
            w.BeginKey("ruleDetails");
            w.BeginArray();
            foreach (var d in rt.RuleDetails)
            {
                w.BeginObject();
                w.Field("ruleIndex", d.RuleIndex);
                w.Field("conditionsPass", d.ConditionsPass);
                w.BeginKey("conditions");
                w.BeginArray();
                foreach (var c in d.Conditions)
                {
                    w.BeginObject();
                    w.Field("name", c.Name);
                    w.Field("actual", c.Actual);
                    w.FieldNullableBool("expected", c.Expected);
                    w.Field("pass", c.Pass);
                    w.Field("source", c.Source);
                    w.EndObject();
                }
                w.EndArray();
                w.FieldNullableBool("effectDisableAI", d.EffectDisableAI);
                w.FieldNullableBool("effectEnableAI", d.EffectEnableAI);
                w.FieldNullableFloat("effectHealth", d.EffectHealth);
                w.FieldNullableFloat("effectDamage", d.EffectDamage);
                w.FieldNullableFloat("effectSpeed", d.EffectSpeed);
                w.EndObject();
            }
            w.EndArray();
            w.BeginKey("recentEvents");
            w.BeginArray();
            foreach (var line in rt.RecentEventLines) w.StringValue(line);
            w.EndArray();
            w.EndObject();
        }

        private static void WriteMountUpObject(JsonWriter w, CpcMountUpInfo mu)
        {
            w.BeginObject();
            w.Field("pluginDetected", mu.PluginDetected);
            w.Field("typeResolved", mu.TypeResolved);
            w.Field("tameableExists", mu.TameableExists);
            w.Field("isTamed", mu.IsTamed);
            w.Field("haveSaddle", mu.HaveSaddle);
            w.Field("haveRider", mu.HaveRider);
            w.Field("sadleComponentOnRoot", mu.SadleComponentOnRoot);
            w.Field("sadleComponentOnChild", mu.SadleComponentOnChild);
            w.Field("sadleHaveValidUser", mu.SadleHaveValidUser);
            w.Field("sadleUser", mu.SadleUser);
            w.Field("canonicalSaddled", mu.CanonicalSaddled);
            w.Field("canonicalRidden", mu.CanonicalRidden);
            w.Field("hasPermanentAIDisabledMarker", mu.HasPermanentAIDisabledMarker);
            w.EndObject();
        }

        private static void WriteAllTameableObject(JsonWriter w, CpcAllTameableInfo at)
        {
            w.BeginObject();
            w.Field("allTameableDetected", at.AllTameableDetected);
            w.Field("hasOffspringGrowup", at.HasOffspringGrowup);
            w.Field("growTriggered", at.GrowTriggered);
            w.Field("birthTimeTicks", at.BirthTimeTicks);
            w.Field("adultPrefab", at.AdultPrefab);
            w.Field("hasTameable", at.HasTameable);
            w.Field("isTamed", at.IsTamed);
            w.EndObject();
        }

        private static void WriteGeneratedLiveObject(JsonWriter w, CpcGeneratedLiveInfo gen)
        {
            w.BeginObject();
            w.Field("configuredNewPrefab", gen.ConfiguredNewPrefab);
            w.Field("configuredSourcePrefab", gen.ConfiguredSourcePrefab);
            w.Field("configuredAdultPrefab", gen.ConfiguredAdultPrefab);
            w.Field("configEnabled", gen.ConfigEnabled);
            w.Field("hasOffspringGrowup", gen.HasOffspringGrowup);
            w.Field("isUnderPrefabContainer", gen.IsUnderPrefabContainer);
            w.EndObject();
        }

        private static void WriteZdoObject(JsonWriter w, CpcZdoInfo zdo)
        {
            w.BeginObject();
            w.Field("zdoId", zdo.ZdoId);
            w.Field("znetViewValid", zdo.ZNetViewValid);
            w.Field("isOwner", zdo.IsOwner);
            w.Field("ownerPeerId", zdo.OwnerPeerId);
            w.Field("isServer", zdo.IsServer);
            w.BeginKey("position"); WriteVec3(w, zdo.Position);
            w.Field("prefabHash", zdo.PrefabHash);
            w.Field("canModify", zdo.CanModify);
            w.EndObject();
        }

        private static void WritePrefabInfoObject(JsonWriter w, CpcPrefabInfo info)
        {
            w.BeginObject();
            w.Field("prefabName", info.PrefabName);
            w.Field("foundInZNetScene", info.FoundInZNetScene);
            w.BeginKey("scale"); WriteVec3(w, info.Scale);
            w.Field("hasCharacter", info.HasCharacter);
            w.Field("hasZNetView", info.HasZNetView);
            w.Field("hasBaseAI", info.HasBaseAI);
            w.Field("hasMonsterAI", info.HasMonsterAI);
            w.Field("hasAnimalAI", info.HasAnimalAI);
            w.Field("hasTameable", info.HasTameable);
            w.Field("hasOffspringGrowup", info.HasOffspringGrowup);
            if (info.GeneratedEntry != null)
            {
                w.BeginKey("generatedEntry");
                w.BeginObject();
                w.Field("newPrefab", info.GeneratedEntry.NewPrefab);
                w.Field("sourcePrefab", info.GeneratedEntry.SourcePrefab);
                w.Field("adultPrefab", info.GeneratedEntry.AdultPrefab);
                w.Field("enabled", info.GeneratedEntry.Enabled);
                w.Field("registeredInZNetScene", info.GeneratedEntry.RegisteredInZNetScene);
                w.EndObject();
            }
            if (info.OverrideEntry != null)
            {
                w.BeginKey("overrideEntry");
                w.BeginObject();
                w.Field("targetPrefab", info.OverrideEntry.TargetPrefab);
                w.Field("enabled", info.OverrideEntry.Enabled);
                w.Field("scale", info.OverrideEntry.Scale);
                w.Field("disableAI", info.OverrideEntry.DisableAI);
                w.Field("disableAggro", info.OverrideEntry.DisableAggro);
                w.Field("disableFleeing", info.OverrideEntry.DisableFleeing);
                w.FieldNullableBool("friendAttacked", info.OverrideEntry.FriendAttacked);
                w.EndObject();
            }
            if (info.Chain != null)
            {
                w.BeginKey("chain");
                w.BeginObject();
                w.Field("sourcePrefab", info.Chain.SourcePrefab);
                w.Field("generatedPrefab", info.Chain.GeneratedPrefab);
                w.Field("adultPrefab", info.Chain.AdultPrefab);
                w.EndObject();
            }
            w.EndObject();
        }

        private static void WriteVerifyObject(JsonWriter w, CpcGeneratedVerifyReport verify)
        {
            w.BeginObject();
            w.Field("configuredCount", verify.ConfiguredCount);
            w.Field("registeredCount", verify.RegisteredCount);
            w.Field("missingCount", verify.MissingCount);
            w.Field("leakedCount", verify.LeakedCount);
            WriteStringArray(w, "missingPrefabs", verify.MissingPrefabs);
            w.BeginKey("leakedTemplates");
            w.BeginArray();
            foreach (var l in verify.LeakedTemplates)
            {
                w.BeginObject();
                w.Field("name", l.Name);
                w.Field("hierarchyPath", l.HierarchyPath);
                w.Field("activeInHierarchy", l.ActiveInHierarchy);
                w.Field("instanceId", l.InstanceId);
                w.EndObject();
            }
            w.EndArray();
            WriteStringArray(w, "notes", verify.Notes);
            w.EndObject();
        }

        private static void WriteVec3(JsonWriter w, Vector3 v)
        {
            w.BeginObject();
            w.Field("x", v.x);
            w.Field("y", v.y);
            w.Field("z", v.z);
            w.EndObject();
        }

        private static void WriteStringArray(JsonWriter w, string key, List<string> items)
        {
            w.BeginKey(key);
            w.BeginArray();
            if (items != null) foreach (var s in items) w.StringValue(s);
            w.EndArray();
        }

        // ── Minimal JSON writer ───────────────────────────────────────────────

        private class JsonWriter
        {
            private readonly StringBuilder _sb;
            private readonly Stack<bool> _needsComma = new Stack<bool>();
            private int _indent;

            public JsonWriter(StringBuilder sb) { _sb = sb; }

            private void Comma()
            {
                if (_needsComma.Count > 0 && _needsComma.Peek())
                    _sb.AppendLine(",");
                else if (_needsComma.Count > 0)
                    _sb.AppendLine();

                if (_needsComma.Count > 0)
                {
                    var v = _needsComma.Pop();
                    _needsComma.Push(true);
                    if (!v) { }
                }
                _sb.Append(new string(' ', _indent * 2));
            }

            public void BeginObject()
            {
                Comma();
                _sb.Append("{");
                _indent++;
                _needsComma.Push(false);
            }

            public void EndObject()
            {
                _indent--;
                _needsComma.Pop();
                _sb.AppendLine();
                _sb.Append(new string(' ', _indent * 2));
                _sb.Append("}");
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            public void BeginArray()
            {
                _sb.Append("[");
                _indent++;
                _needsComma.Push(false);
            }

            public void EndArray()
            {
                _indent--;
                _needsComma.Pop();
                if (_sb[_sb.Length - 1] != '[') { _sb.AppendLine(); _sb.Append(new string(' ', _indent * 2)); }
                _sb.Append("]");
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            public void BeginKey(string key)
            {
                Comma();
                _sb.Append($"\"{key}\": ");
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            public void Field(string key, string value)
            {
                Comma();
                _sb.Append($"\"{key}\": {Str(value)}");
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            public void Field(string key, bool value)
            {
                Comma();
                _sb.Append($"\"{key}\": {(value ? "true" : "false")}");
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            public void Field(string key, int value)
            {
                Comma();
                _sb.Append($"\"{key}\": {value}");
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            public void Field(string key, long value)
            {
                Comma();
                _sb.Append($"\"{key}\": {value}");
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            public void Field(string key, float value)
            {
                Comma();
                _sb.Append($"\"{key}\": {value:G}");
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            public void FieldNullableBool(string key, bool? value)
            {
                Comma();
                _sb.Append($"\"{key}\": {(value.HasValue ? (value.Value ? "true" : "false") : "null")}");
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            public void FieldNullableFloat(string key, float? value)
            {
                Comma();
                _sb.Append($"\"{key}\": {(value.HasValue ? value.Value.ToString("G") : "null")}");
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            public void StringValue(string value)
            {
                Comma();
                _sb.Append(Str(value));
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            public void Null()
            {
                _sb.Append("null");
                if (_needsComma.Count > 0) { _needsComma.Pop(); _needsComma.Push(true); }
            }

            private static string Str(string s)
            {
                if (s == null) return "null";
                return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "") + "\"";
            }
        }
    }
}
