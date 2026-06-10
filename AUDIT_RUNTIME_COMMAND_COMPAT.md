# CreaturePrefabCreator Runtime, Command, Compatibility Audit Report

**Audit Date:** 2026-06-10  
**Auditor:** Devin (AI Assistant)  
**Branch:** `devin/runtime-command-compat-audit-fixes`  
**Commit Range:** Changes since main  

---

## Executive Summary

This audit focused on safety, correctness, and documentation accuracy in the CreaturePrefabCreator mod. The primary concerns were:

1. **Critical:** Health multiplier resetting combat damage every evaluation tick
2. **High:** AI state tracking not atomic with actual mutation
3. **High:** Documentation mismatch (6 vs 7 commands)
4. **Medium:** cpc_spawn prefab mutation not exception-safe
5. **Medium:** Path traversal vulnerability in cpc_dump_json

**Build Status:** ✅ Succeeded (11 nullable reference warnings, pre-existing)

---

## Findings and Fixes

### CPC-001: Runtime Health Multiplier Resets Combat Damage [CRITICAL]

| | |
|---|---|
| **Severity** | Critical |
| **Area** | RuntimeModifiers |
| **File** | `RuntimeModifiers/RuntimeModifierManager.cs` |
| **Method** | `ApplyHealth`, `RestoreHealth` |

#### Evidence
The original implementation stored `character.m_health` (current health) as the baseline and reapplied the multiplier every evaluation tick:

```csharp
// BEFORE (dangerous)
if (!_originalHealth.ContainsKey(key))
    _originalHealth[key] = character.m_health;  // Current health!
float original = _originalHealth[key];
character.m_health = original * mult;  // Reset every tick!
```

#### Risk
- Combat damage would be undone every evaluation tick (5-second intervals)
- Healing effects would be amplified unintentionally
- Creature health would fluctuate unpredictably during combat

#### Fix Applied
Replaced with max-health-aware implementation that preserves health percentage:

```csharp
// AFTER (safe)
float baselineMax = GetCharacterMaxHealthBase(character);
// ... calculate ratio and preserve it when applying new max
SetCharacterMaxHealth(character, newMax);
character.m_health = Mathf.Min(newMax * healthPercent, newMax);
```

Added defensive reflection helpers `GetCharacterMaxHealthBase()` and `SetCharacterMaxHealth()` with multiple fallback strategies and warning logs.

---

### CPC-002: Documentation Mismatch (Six vs Seven Commands) [HIGH]

| | |
|---|---|
| **Severity** | High |
| **Area** | Documentation |
| **File** | `Docs/debug-commands.md` |

#### Evidence
Documentation stated "six primary commands" but listed seven:
- cpc_help
- cpc_status
- cpc_spawn
- cpc_print_console
- cpc_dump_json
- cpc_repair_world
- cpc_reload_config

#### Fix Applied
Changed "six" to "seven" in `Docs/debug-commands.md` line 9.

---

### CPC-004: AI State Tracking Not Atomic [HIGH]

| | |
|---|---|
| **Severity** | High |
| **Area** | RuntimeModifiers, Patches |
| **Files** | `RuntimeModifiers/RuntimeModifierManager.cs`, `Patches/SaddledCreaturePatch.cs` |

#### Evidence
Original `EnableAIComponents` and `DisableAIComponents` returned `void`. State was captured before attempting mutation, but if ownership changed between capture and mutation, an orphaned state entry could be created.

#### Risk
- Orphaned AI state entries if ownership transfers during evaluation
- Runtime AI tracking dictionaries could grow unbounded
- Creatures might not restore to correct AI state on cleanup

#### Fix Applied
1. Changed return type from `void` to `bool` for both methods
2. Methods now return `true` only if at least one AI component was actually modified
3. `RuntimeModifierManager` now only tracks state when `changed == true`:

```csharp
// ATOMIC: Only track state if DisableAIComponents actually changes something
bool changed = SaddledCreaturePatch.DisableAIComponents(character);
if (changed && state != null)
{
    _runtimeAIDisabled[key] = state;
    // ... log and record event
}
```

---

### CPC-007: Path Traversal in cpc_dump_json [MEDIUM]

| | |
|---|---|
| **Severity** | Medium |
| **Area** | Commands |
| **File** | `Debug/CpcJsonRenderer.cs` |

#### Evidence
`SanitizeFileName` only replaced invalid filename characters, but did not prevent path traversal via `../` or absolute paths.

#### Risk
- Potential file overwrite outside intended dumps directory
- Could theoretically overwrite game files or system files

#### Fix Applied
Added path traversal protection:

```csharp
// SECURITY: Block path traversal attempts
name = name.Replace("../", "_").Replace("..\\", "_");
name = name.Replace("/", "_").Replace("\\", "_");
name = name.Replace("..", "_");
```

---

### CPC-008: cpc_spawn Prefab Mutation Not Exception-Safe [MEDIUM]

| | |
|---|---|
| **Severity** | Medium |
| **Area** | Commands |
| **File** | `Debug/CreaturePrefabDebugCommands.cs` |

#### Evidence
The temporary mutation of `prefabTameable.m_startsTamed` was restored after the spawn loop, but if an exception occurred during spawning, the restore would not execute.

#### Risk
- Subsequent spawns of the same prefab would incorrectly spawn tamed
- Cross-contamination between different spawn commands

#### Fix Applied
Wrapped spawn loop in try/finally:

```csharp
Tameable prefabTameable = spawnTamed ? prefab.GetComponent<Tameable>() : null;
bool originalStartsTamed = prefabTameable != null && prefabTameable.m_startsTamed;
if (prefabTameable != null)
    prefabTameable.m_startsTamed = true;

try
{
    // ... spawn loop ...
}
finally
{
    // Restore prefab Tameable to original state so subsequent non-tamed spawns are unaffected.
    if (prefabTameable != null)
        prefabTameable.m_startsTamed = originalStartsTamed;
}
```

Also added:
- Player.m_localPlayer existence check before spawning
- Count clamping to max 50 to prevent performance issues

---

### CPC-011: MountUp Saddled Detection Missing getSaddle Path [MEDIUM]

| | |
|---|---|
| **Severity** | Medium |
| **Area** | MountUp Compatibility |
| **File** | `Patches/SaddledCreaturePatch.cs` |

#### Evidence
`IsSaddledViaCanonicalPath` checked Tameable, Sadle component, and inventory, but did not check MountUp.Mountable -> getSaddle/getSadle for saddled state.

#### Risk
- MountUp-mounted creatures might not be detected as saddled
- Runtime rules with `saddled=true` condition might not match MountUp creatures

#### Fix Applied
Added MountUp getSaddle detection as priority 3 in `IsSaddledViaCanonicalPath`:

```csharp
// 3. MountUpRestored: check via Mountable -> getSaddle/getSadle
if (_mountUpMountableType != null && _mountUpGetSaddleMethod != null)
{
    var mountable = character.GetComponent(_mountUpMountableType);
    if (mountable != null)
    {
        try
        {
            object saddleObj = _mountUpGetSaddleMethod.Invoke(mountable, null);
            if (saddleObj is GameObject saddleGO && saddleGO != null)
                return true;
        }
        catch { }
    }
}
```

---

## Verified Correct (No Changes Needed)

### Damage Multiplier Wiring
- **File:** `Patches/RuntimeModifierPatch.cs`
- **Status:** ✅ Correctly wired
- **Evidence:** `RuntimeModifier_Character_ApplyDamage.Prefix` calls `RuntimeModifierManager.GetOutgoingDamageMultiplier(attacker)` and applies via `hit.ApplyModifier(mult)`

### Cache Cleanup
- **File:** `RuntimeModifiers/RuntimeModifierManager.cs`
- **Status:** ✅ Correctly implemented
- **Evidence:** `Cleanup()` method clears all dictionaries (`_originalHealth`, `_originalSpeeds`, `_currentDamageMult`, `_runtimeAIDisabled`, `_runtimeAIEnabled`) on creature destroy

### cpc_repair_world Dry-Run by Default
- **File:** `Debug/CpcRepairWorld.cs`
- **Status:** ✅ Correctly implemented
- **Evidence:** All destructive actions (`CleanupZdos`, `CleanupOrphans`, `ForceGrow`) check `if (dryRun)` before executing and log preview information

### AllTameable Detection
- **File:** `Compatibility/PluginGuids.cs`
- **Status:** ✅ Correctly uses exact GUID
- **Evidence:** `AllTameable = "Tamboli.AllTameable_TamingOverhaul"` - exact GUID, no substring matching

### MountUp Detection
- **File:** `Compatibility/MountUpRestored/`
- **Status:** ✅ Correctly implemented with delayed retry
- **Evidence:** `MountUpCompatibilityPatch.RetryApplyPatches()` handles load order variations with 5 retries at 2-second intervals

---

## Manual Validation Checklist

### Runtime Modifiers
```
cpc_status --debug-runtime --mods
```
- [ ] RuntimeModifiers enabled state shown
- [ ] MountUp detection shown
- [ ] AllTameable detection shown
- [ ] Runtime cache counts shown

```
cpc_print_console live --target --ai --zdo --debug-runtime --debug-mountup --debug-alltameable --verbose
```
- [ ] ZDO owner info shown
- [ ] BaseAI/MonsterAI/AnimalAI state shown
- [ ] Runtime rule match shown
- [ ] Saddled/ridden condition trace shown

### MountUp
- [ ] Spawn/find tamed Bjorn with MountUpRestored
- [ ] Inspect before saddle: saddled=false
- [ ] Inspect after saddle: saddled=true
- [ ] Inspect while mounted: ridden=true
- [ ] Inspect after dismount: ridden=false, AI restored

### Spawn Safety
```
cpc_spawn --prefab Wolf --count 1000  # Should clamp to 50
cpc_spawn --prefab Wolf --tamed        # Should restore m_startsTamed even if error occurs
```

### Path Traversal Protection
```
cpc_dump_json live --target --output "../../../dangerous.json"  # Should be sanitized to ___dangerous.json
```

---

## Files Changed

1. `RuntimeModifiers/RuntimeModifierManager.cs` - Health multiplier safety, AI atomicity
2. `Patches/SaddledCreaturePatch.cs` - AI component methods return bool, MountUp saddled detection
3. `Debug/CreaturePrefabDebugCommands.cs` - cpc_spawn try/finally, Player check, count clamp
4. `Debug/CpcJsonRenderer.cs` - Path traversal protection
5. `Docs/debug-commands.md` - Fix "six" to "seven"

---

## Remaining Risks

| Risk | Mitigation | Status |
|------|------------|--------|
| Health multiplier reflection may fail on some Valheim versions | Multiple fallback strategies, warning logs | Accepted |
| Config Sync is experimental | Documented as experimental, disabled by default | Accepted |
| Visual Overrides are beta | Documented as beta, disabled by default | Accepted |
| Faction Overrides are beta | Documented as beta, disabled by default | Accepted |

---

## Recommendations for Future Work

1. **Testing:** Add unit tests for `SanitizeFileName` with various path traversal attempts
2. **Testing:** Verify health multiplier behavior with damage-over-time effects
3. **Documentation:** Add dedicated troubleshooting section for MountUp detection issues
4. **Feature:** Consider adding `cpc_status --cache-sizes` for runtime cache diagnostics

---

## Build Verification

```powershell
dotnet build
# Expected: 0 errors, 11 nullable reference warnings (pre-existing)
```

**Result:** ✅ Build succeeded

---

*End of Audit Report*
