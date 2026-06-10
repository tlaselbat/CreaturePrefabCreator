# CPC Debug Commands

All debug commands are registered as Jötunn `ConsoleCommand` instances via `CreaturePrefabDebugCommands.Register()`. Enable with `RegisterConsoleCommands = true` in the `.cfg` file, then open the in-game console (F5).

---

## Command Architecture

There are six primary commands. All old per-topic commands have been deprecated — they still exist as stubs that print the new equivalent, but will be removed in a future release.

| Command | Purpose |
|---------|---------|
| `cpc_help` | List commands or show detailed usage for one command |
| `cpc_status` | Plugin, config, feature-gate, and mod-detection summary |
| `cpc_spawn` | Spawn any registered prefab in front of the player |
| `cpc_print_console` | Diagnostics printed to the in-game console |
| `cpc_dump_json` | Same diagnostics written as JSON to the dumps folder |
| `cpc_repair_world` | Mutating world-repair operations (requires `--confirm`) |
| `cpc_reload_config` | Re-read `creaturePrefabCreator.json` from disk and re-apply |

`cpc_print_console` and `cpc_dump_json` are **read-only**. `cpc_repair_world` and `cpc_reload_config` mutate state.

---

## Source Files

| File | Role |
|------|------|
| `Debug/CreaturePrefabDebugCommands.cs` | All six primary command registrations and implementations |
| `Debug/CpcCommandRouter.cs` | Argument parser, flag helpers, deprecation printer |
| `Debug/CpcDiagnosticEngine.cs` | Data collection — builds all diagnostic report objects |
| `Debug/CpcConsoleRenderer.cs` | Console-side rendering of all report types + help text |
| `Debug/CpcJsonRenderer.cs` | JSON serialisation of all report types |
| `Debug/RuntimeDebugCommands.cs` | Deprecation stubs only — all real logic removed |
| `Debug/CreatureAIDumpCommands.cs` | Deprecation stubs only — all real logic removed |

---

## cpc_help

```
cpc_help [--command <name>]
```

Without `--command`, prints the full command list. With `--command`, prints syntax, flags, and examples for that command.

```
cpc_help --command cpc_print_console
cpc_help --command cpc_dump_json
cpc_help --command cpc_repair_world
cpc_help --command cpc_reload_config
cpc_help --command cpc_spawn
cpc_help --command cpc_status
```

---

## cpc_status

```
cpc_status [--verbose] [--mods] [--debug-runtime] [--generated]
```

| Flag | Output |
|------|--------|
| *(none)* | Plugin version, enabled state, config counts, feature gates, ZNetScene ready |
| `--verbose` | Extends all other sections |
| `--mods` | Optional-mod detection (MountUp, AllTameable), BepInEx plugin list, config sync state |
| `--debug-runtime` | Runtime modifier system: enabled, rule counts, eval interval, AI-disabled count, event buffer |
| `--generated` | Lists every configured generated prefab and whether it is registered in ZNetScene |

---

## cpc_spawn

```
cpc_spawn --prefab <name> [--count <n>] [--level <n>] [--tamed] [--distance <m>]
```

| Flag | Default | Description |
|------|---------|-------------|
| `--prefab <name>` | — | **Required.** Prefab name as registered in ZNetScene |
| `--count <n>` | `1` | Number of instances to spawn |
| `--level <n>` | `1` | Star level (`1`=no stars, `2`=1-star, `3`=2-star). Ignored on prefabs without `Character` |
| `--tamed` | off | Sets `tamed=true` and `creator=<local player ID>` on the ZDO |
| `--distance <m>` | `3` | Spawn distance in front of the player |

Examples:
```
cpc_spawn --prefab Wolf
cpc_spawn --prefab Bjorn_cub --tamed
cpc_spawn --prefab Wolf --count 3 --level 2 --distance 8
```

---

## cpc_print_console

```
cpc_print_console <mode> [args] [flags]
```

All output is **read-only**.

### Mode: `live`

Inspect live spawned creatures.

```
cpc_print_console live --target [flags]
cpc_print_console live <radius> [flags]
```

`--target` inspects the nearest creature within ~5 m (crosshair target). A positional number sets a scan radius (default 20 m) and lists all creatures in range.

| Flag | Adds to output |
|------|----------------|
| `--ai` | BaseAI / MonsterAI / AnimalAI field details |
| `--debug-runtime` | Runtime modifier matching, rule details, event history |
| `--debug-mountup` | MountUp saddle/rider/AI-suppression state |
| `--debug-alltameable` | AllTameable detection, OffspringGrowup state |
| `--zdo` | ZDO id, owner peer, prefab hash, canModify |
| `--generated` | Generated prefab config entry if applicable |
| `--verbose` | Extended output for all active sections |

Examples:
```
cpc_print_console live --target
cpc_print_console live --target --ai --zdo
cpc_print_console live --target --debug-runtime --verbose
cpc_print_console live --target --debug-mountup --debug-alltameable
cpc_print_console live 20
cpc_print_console live 50 --ai --debug-runtime
```

### Mode: `prefab`

Inspect prefab templates in ZNetScene.

```
cpc_print_console prefab --name <name> [--chain] [--generated] [--overrides]
cpc_print_console prefab --find <partial>
cpc_print_console prefab --compare <a> <b>
cpc_print_console prefab --list-generated
cpc_print_console prefab --verify-generated [--leaks] [--verbose]
```

| Flag | Description |
|------|-------------|
| `--name <name>` | Look up a single prefab by exact name |
| `--chain` | Show source → generated → adult growth chain |
| `--generated` | Include generated prefab config entry |
| `--overrides` | Include prefab override config entry |
| `--find <partial>` | Substring search across all ZNetScene prefab names |
| `--compare <a> <b>` | Side-by-side component diff of two prefabs |
| `--list-generated` | List all configured generated prefabs and their registration status |
| `--verify-generated` | Cross-check config entries against ZNetScene registrations |
| `--leaks` | Also scan scene for active template objects (requires `--verify-generated`) |

Examples:
```
cpc_print_console prefab --name Wolf
cpc_print_console prefab --name Bjorn_cub --chain --generated
cpc_print_console prefab --find wolf
cpc_print_console prefab --compare Wolf Bjorn_cub
cpc_print_console prefab --list-generated
cpc_print_console prefab --verify-generated --leaks --verbose
```

### Mode: `world-zdos`

List all live ZDOs for a prefab by name.

```
cpc_print_console world-zdos <prefab> [--verbose]
```

Example:
```
cpc_print_console world-zdos Bjorn_cub
cpc_print_console world-zdos Wolf_cub --verbose
```

---

## cpc_dump_json

```
cpc_dump_json <mode> [args] [flags] [--output <filename>]
```

Mirrors `cpc_print_console` exactly — same modes, same flags — but writes timestamped JSON files instead of console output.

Output folder: `BepInEx/config/CreaturePrefabCreator/dumps/`

`--output <filename>` overrides the generated filename. Relative to the dumps folder.

Examples:
```
cpc_dump_json live --target
cpc_dump_json live --target --ai --output bjorn_test.json
cpc_dump_json live 20 --debug-runtime
cpc_dump_json prefab --name Wolf
cpc_dump_json prefab --verify-generated --leaks
cpc_dump_json world-zdos Bjorn_cub --output zdos.json
```

---

## cpc_repair_world

```
cpc_repair_world <action> [--dry-run|--confirm] [--verbose]
```

**Mutating.** Default behaviour is `--dry-run` (preview only). Pass `--confirm` to apply changes.

| Action | Description |
|--------|-------------|
| `--cleanup-zdos <prefab>` | Destroy all ZDOs for the named prefab |
| `--orphans` | Destroy ZDOs whose prefab hash has no registered prefab in ZNetScene |
| `--restore-runtime` | Re-enable AI on creatures that CPC runtime-disabled |
| `--force-grow` | Force all `OffspringGrowup` components within radius to grow immediately |

Examples:
```
cpc_repair_world --cleanup-zdos Bjorn_cub --dry-run
cpc_repair_world --cleanup-zdos Bjorn_cub --confirm
cpc_repair_world --orphans --dry-run --verbose
cpc_repair_world --restore-runtime
cpc_repair_world --force-grow --confirm
```

---

## cpc_reload_config

```
cpc_reload_config [--dry-run] [--prefabs-only] [--debug-runtime-only] [--force]
```

| Flag | Behaviour |
|------|-----------|
| *(none)* | Full reload: prefab overrides, generated prefabs, runtime modifiers |
| `--dry-run` | Parse config and validate only — no changes applied |
| `--prefabs-only` | Re-apply prefab overrides and generated prefabs; skip runtime modifiers |
| `--debug-runtime-only` | Reinitialize runtime modifiers only; skip prefab subsystems |
| `--force` | Skip safety checks |

AI state changes (`disableAI`, `disableAggro`, `disableFleeing`, `m_friendAttacked`) are propagated to already-spawned live instances. Scale, tint, glow, and faction changes affect new spawns only.

---

## Deprecated Commands

These commands are registered as stubs that print the replacement command. They will be removed in a future release.

| Deprecated | Use instead |
|------------|-------------|
| `cpc_runtime_status` | `cpc_status --debug-runtime` |
| `cpc_runtime_rules` | `cpc_print_console live --target --debug-runtime --verbose` |
| `cpc_runtime_check` | `cpc_print_console live --target --debug-runtime` |
| `cpc_runtime_restore` | `cpc_repair_world --restore-runtime` |
| `cpc_runtime_recent` | `cpc_status --debug-runtime --verbose` |
| `cpc_runtime_force_eval` | `cpc_repair_world --restore-runtime` |
| `cpc_mount_state` | `cpc_print_console live --target --debug-mountup` |
| `cpc_ai_state` | `cpc_print_console live --target --ai` |
| `cpc_owner_state` | `cpc_print_console live --target --zdo` |
| `cpc_sync_status` | `cpc_status --mods` |
| `cpc_compat_status` | `cpc_status --mods` |
| `cpc_dump_ai_nearby` | `cpc_dump_json live <radius>` |
| `cpc_dump_ai_prefab` | `cpc_dump_json prefab --name <name>` |
| `cpc_dump_ai_all_prefabs` | `cpc_dump_json prefab --list-generated` |
| `cpc_runtime_dump_json` | `cpc_dump_json live --target --debug-runtime` |

---

## Usage Notes

- Commands use `cpc_` prefix throughout.
- `cpc_print_console` and `cpc_dump_json` are read-only and safe at any time.
- `cpc_repair_world` and `cpc_reload_config` mutate state — use `--dry-run` first.
- `--confirm` is required for all destructive `cpc_repair_world` operations.
- Commands are only registered when `RegisterConsoleCommands = true` in the `.cfg` file.
