# Extend dev-profile.ps1

Adds CPC DLL auto-copy, $Required verification, timestamped log file output, and gated stale-file reporting to the existing sync script with minimal changes to existing behavior.

## New Parameters

| Param | Type | Default | Purpose |
|---|---|---|---|
| `$CpcBuildDir` | string | `...\bin\Debug` | Root to search for `CreaturePrefabCreator.dll` |
| `-NoBuild` | switch | off | Skip CPC DLL auto-copy step |
| `-NoLog` | switch | off | Disable log file output |
| `-CleanStale` | switch | off | Move stale target files to quarantine (default: report only) |

## Sections Added (in order)

1. **Log helper** — `$_log` list + `function Log($msg)` strips ANSI; writes to list.
2. **Updated param block** — 3 new params appended; existing params unchanged.
3. **CPC DLL auto-copy** — after main sync loop: `Get-ChildItem -Recurse` for `CreaturePrefabCreator.dll` under `$CpcBuildDir`, pick newest, SHA-256 compare, copy or skip, warn if not found.
4. **Stale-file report / cleanup** — `Get-ChildItem $Target -Depth 2` vs included set; any file in Target not in Source is "stale". Default: print `[STALE]` list. With `-CleanStale`: move to `$Target\_quarantine\YYYY-MM-DD_HH-mm-ss\`. All gated behind `-WhatIf` (show only, no move).
5. **$Required verification** — loop $Required, `Test-Path $Target\$name`, collect missing, print OK/MISSING table, set `$exitCode = 1` if any missing.
6. **Log file write** — at end of script, unless `-NoLog`, create `$PSScriptRoot\Logs\` if absent, write `$_log` to `dev-profile-YYYY-MM-DD_HH-mm-ss.log`.
7. **Updated summary block** — adds CPC copy result, required result, stale count, log path.

## WhatIf Safety
- All file writes, moves, and directory creates are inside `if (-not $WhatIf)` guards.
- Log dir/file creation is also gated: log is still built in memory but not written to disk under `-WhatIf`.
- Stale moves are gated by both `-CleanStale` and `(-not $WhatIf)`.

## Example Commands

```powershell
# Normal sync
.\dev-profile.ps1

# Dry run (no changes, full report)
.\dev-profile.ps1 -WhatIf

# Skip CPC DLL copy
.\dev-profile.ps1 -NoBuild

# Move stale files to quarantine
.\dev-profile.ps1 -CleanStale

# Dry run with stale report
.\dev-profile.ps1 -WhatIf -CleanStale

# No log file, custom target
.\dev-profile.ps1 -NoLog -Target "C:\DevServer\BepInEx\plugins"
```

## Final Summary Output (example)
```
────────────────────────────────────────
Excluded : 9 items
Included : 47 items
Copied   : 3
Skipped  : 44

CPC DLL  : COPY CreaturePrefabCreator.dll (updated)

Stale    : 2 file(s) in Target not in Source  [use -CleanStale to quarantine]

Required : OK (3/3)

Target   : V:\server\BepInEx\plugins
Log      : C:\...\Logs\dev-profile-2026-06-09_04-28-00.log
```
