using Jotunn.Entities;
using Jotunn.Managers;
using System;

namespace CreaturePrefabCreator.Debug
{
    public static class RuntimeDebugCommands
    {
        public static void Register(CommandManager mgr)
        {
            try
            {
                mgr.AddConsoleCommand(new DeprecatedRuntimeStatusCommand());
                mgr.AddConsoleCommand(new DeprecatedRuntimeRulesCommand());
                mgr.AddConsoleCommand(new DeprecatedRuntimeCheckCommand());
                mgr.AddConsoleCommand(new DeprecatedRuntimeRestoreCommand());
                mgr.AddConsoleCommand(new DeprecatedRuntimeRecentCommand());
                mgr.AddConsoleCommand(new DeprecatedRuntimeForceEvalCommand());
                mgr.AddConsoleCommand(new DeprecatedMountStateCommand());
                mgr.AddConsoleCommand(new DeprecatedAIStateCommand());
                mgr.AddConsoleCommand(new DeprecatedOwnerStateCommand());
                mgr.AddConsoleCommand(new DeprecatedSyncStatusCommand());
                mgr.AddConsoleCommand(new DeprecatedCompatStatusCommand());
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[RuntimeDebugCommands] Failed to register deprecation stubs: {ex.Message}");
            }
        }

        public static void RegisterDumpCommand(CommandManager mgr)
        {
            try
            {
                mgr.AddConsoleCommand(new DeprecatedRuntimeDumpJsonCommand());
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[RuntimeDebugCommands] Failed to register dump deprecation stub: {ex.Message}");
            }
        }

        // ── Deprecation stubs ─────────────────────────────────────────────────

        private class DeprecatedRuntimeStatusCommand : ConsoleCommand
        {
            public override string Name => "cpc_runtime_status";
            public override string Help => "[DEPRECATED] Use: cpc_status --debug-runtime";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_runtime_status", "cpc_status --debug-runtime");
        }

        private class DeprecatedRuntimeRulesCommand : ConsoleCommand
        {
            public override string Name => "cpc_runtime_rules";
            public override string Help => "[DEPRECATED] Use: cpc_print_console live --target --debug-runtime --verbose";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_runtime_rules", "cpc_print_console live --target --debug-runtime --verbose");
        }

        private class DeprecatedRuntimeCheckCommand : ConsoleCommand
        {
            public override string Name => "cpc_runtime_check";
            public override string Help => "[DEPRECATED] Use: cpc_print_console live --target --debug-runtime";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_runtime_check", "cpc_print_console live --target --debug-runtime");
        }

        private class DeprecatedRuntimeRestoreCommand : ConsoleCommand
        {
            public override string Name => "cpc_runtime_restore";
            public override string Help => "[DEPRECATED] Use: cpc_repair_world --restore-runtime";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_runtime_restore", "cpc_repair_world --restore-runtime");
        }

        private class DeprecatedRuntimeRecentCommand : ConsoleCommand
        {
            public override string Name => "cpc_runtime_recent";
            public override string Help => "[DEPRECATED] Use: cpc_status --debug-runtime --verbose";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_runtime_recent", "cpc_status --debug-runtime --verbose");
        }

        private class DeprecatedRuntimeForceEvalCommand : ConsoleCommand
        {
            public override string Name => "cpc_runtime_force_eval";
            public override string Help => "[DEPRECATED] Use: cpc_repair_world --restore-runtime";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_runtime_force_eval", "cpc_repair_world --restore-runtime");
        }

        private class DeprecatedMountStateCommand : ConsoleCommand
        {
            public override string Name => "cpc_mount_state";
            public override string Help => "[DEPRECATED] Use: cpc_print_console live --target --debug-mountup";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_mount_state", "cpc_print_console live --target --debug-mountup");
        }

        private class DeprecatedAIStateCommand : ConsoleCommand
        {
            public override string Name => "cpc_ai_state";
            public override string Help => "[DEPRECATED] Use: cpc_print_console live --target --ai";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_ai_state", "cpc_print_console live --target --ai");
        }

        private class DeprecatedOwnerStateCommand : ConsoleCommand
        {
            public override string Name => "cpc_owner_state";
            public override string Help => "[DEPRECATED] Use: cpc_print_console live --target --zdo";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_owner_state", "cpc_print_console live --target --zdo");
        }

        private class DeprecatedSyncStatusCommand : ConsoleCommand
        {
            public override string Name => "cpc_sync_status";
            public override string Help => "[DEPRECATED] Use: cpc_status --mods";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_sync_status", "cpc_status --mods");
        }

        private class DeprecatedCompatStatusCommand : ConsoleCommand
        {
            public override string Name => "cpc_compat_status";
            public override string Help => "[DEPRECATED] Use: cpc_status --mods";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_compat_status", "cpc_status --mods");
        }

        private class DeprecatedRuntimeDumpJsonCommand : ConsoleCommand
        {
            public override string Name => "cpc_runtime_dump_json";
            public override string Help => "[DEPRECATED] Use: cpc_dump_json live --target --debug-runtime";
            public override void Run(string[] args) =>
                CpcCommandRouter.PrintDeprecation("cpc_runtime_dump_json", "cpc_dump_json live --target --debug-runtime");
        }
    }
}
