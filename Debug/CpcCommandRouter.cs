using System;
using System.Collections.Generic;
using System.Linq;

namespace CreaturePrefabCreator.Debug
{
    internal static class CpcCommandRouter
    {
        // ── Parsed command context ────────────────────────────────────────────

        internal class ParsedArgs
        {
            public string Mode { get; set; }
            public List<string> Positional { get; } = new List<string>();
            public HashSet<string> Flags { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public Dictionary<string, string> Options { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public bool HasFlag(string flag) => Flags.Contains(flag);
            public string GetOption(string key, string defaultValue = null) =>
                Options.TryGetValue(key, out string v) ? v : defaultValue;
        }

        // ── Parser ────────────────────────────────────────────────────────────

        internal static ParsedArgs Parse(string[] args)
        {
            var result = new ParsedArgs();
            if (args == null || args.Length == 0) return result;

            int i = 0;

            if (args.Length > 0 && !args[0].StartsWith("--"))
            {
                result.Mode = args[0].ToLowerInvariant();
                i = 1;
            }

            while (i < args.Length)
            {
                string a = args[i];

                if (a.StartsWith("--"))
                {
                    string key = a.ToLowerInvariant();
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                    {
                        result.Options[key] = args[i + 1];
                        i += 2;
                    }
                    else
                    {
                        result.Flags.Add(key);
                        i++;
                    }
                }
                else
                {
                    result.Positional.Add(a);
                    i++;
                }
            }

            return result;
        }

        // ── Radius helper ─────────────────────────────────────────────────────

        internal static bool TryParseRadius(ParsedArgs parsed, float defaultRadius, out float radius)
        {
            radius = defaultRadius;
            if (parsed.Positional.Count > 0 && float.TryParse(parsed.Positional[0], out float r))
            {
                radius = r;
                return true;
            }
            return false;
        }

        // ── Deprecation shim ──────────────────────────────────────────────────

        internal static void PrintDeprecation(string oldCmd, string newCmd)
        {
            Log($"[CPC] '{oldCmd}' has been removed. Use: {newCmd}");
        }

        internal static void PrintUnknownMode(string command, string mode, IEnumerable<string> validModes)
        {
            Log($"[CPC] Unknown mode '{mode}' for {command}. Valid modes: {string.Join(", ", validModes)}");
            Log($"[CPC] Use 'cpc_help --command {command}' for usage.");
        }

        internal static void PrintMissingMode(string command, IEnumerable<string> validModes)
        {
            Log($"[CPC] {command} requires a mode: {string.Join(", ", validModes)}");
            Log($"[CPC] Use 'cpc_help --command {command}' for usage.");
        }

        private static void Log(string msg) => CreaturePrefabCreatorPlugin.Instance?.Log(msg);
    }
}
