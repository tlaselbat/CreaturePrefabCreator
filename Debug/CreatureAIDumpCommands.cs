using System;
using Jotunn.Entities;
using Jotunn.Managers;

namespace CreaturePrefabCreator.Debug
{
    public static class CreatureAIDumpCommands
    {
        private static bool _registered;

        public static void Register()
        {
            if (_registered) return;
            _registered = true;

            try
            {
                CommandManager.Instance.AddConsoleCommand(new DeprecatedDumpAINearbyCommand());
                CommandManager.Instance.AddConsoleCommand(new DeprecatedDumpAIPrefabCommand());
                CommandManager.Instance.AddConsoleCommand(new DeprecatedDumpAIAllPrefabsCommand());
                RuntimeDebugCommands.RegisterDumpCommand(CommandManager.Instance);
                CreaturePrefabCreatorPlugin.Instance.Log("AI dump deprecation stubs registered.");
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance.LogWarning($"Failed to register AI dump deprecation stubs: {ex.Message}");
            }
        }

        // ── Deprecation stubs ─────────────────────────────────────────────────

        private class DeprecatedDumpAINearbyCommand : ConsoleCommand
        {
            public override string Name => "cpc_dump_ai_nearby";
            public override string Help => "[DEPRECATED] Use: cpc_dump_json live <radius>";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_dump_ai_nearby", "cpc_dump_json live <radius>");
        }

        private class DeprecatedDumpAIPrefabCommand : ConsoleCommand
        {
            public override string Name => "cpc_dump_ai_prefab";
            public override string Help => "[DEPRECATED] Use: cpc_dump_json prefab --name <name>";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_dump_ai_prefab", "cpc_dump_json prefab --name <name>");
        }

        private class DeprecatedDumpAIAllPrefabsCommand : ConsoleCommand
        {
            public override string Name => "cpc_dump_ai_all_prefabs";
            public override string Help => "[DEPRECATED] Use: cpc_dump_json prefab --list-generated";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_dump_ai_all_prefabs", "cpc_dump_json prefab --list-generated");
        }
    }
}
