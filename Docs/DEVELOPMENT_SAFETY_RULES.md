# CreaturePrefabCreator Development Safety Rules

> **Version:** 1.1.0-beta  
> **Purpose:** Prevent unsafe code changes that cause subtle bugs  
> **Audience:** All developers and AI agents working on CPC

---

## Critical Safety Rules

### Rule 1: Never Recapture Original Prefab Values From Already-Mutated ZNetScene Prefabs

**Why:** Doing so causes scale compounding and other mutation accumulation on each reload.

**Correct Pattern:**
```csharp
// GOOD: Capture baselines ONCE during initial boot, before any mutations
PrefabBaselineCache.CaptureOriginalScale(prefabName, prefab);

// GOOD: Use cached baseline for all subsequent reads
Vector3 originalScale = PrefabBaselineCache.GetOriginalScale(prefabName);
```

**Incorrect Pattern:**
```csharp
// BAD: Reading from potentially mutated prefab during reload
Vector3 currentScale = ZNetScene.instance.GetPrefab(prefabName).transform.localScale;
// This may already be scaled from a previous override pass!
```

**See Also:** `docs/RELOAD_LIFECYCLE.md`

---

### Rule 2: Never Treat Component Presence as Equipped State Without Verifying Active/Equipped Logic

**Why:** Components like `Sadle` exist on creatures by default. Only checking for presence causes false positives.

**Correct Pattern:**
```csharp
// GOOD: Check HaveValidUser() or HaveSaddle() for actual equipped state
bool isSaddled = tameable.HaveSaddle(); // Authoritative method
// OR
bool isSaddled = sadle.HaveValidUser(); // Component's equipped check
// OR for MountUp
bool isSaddled = saddleGO != null && saddleGO.activeSelf; // Active check
```

**Incorrect Pattern:**
```csharp
// BAD: Just checking component existence
bool isSaddled = character.GetComponent<Sadle>() != null; // WRONG!
// Wolves always have Sadle component, even without saddle equipped
```

**See Also:** `Detection/MountStateDetector.cs`

---

### Rule 3: Never Mutate Live Creature Runtime State Without ZNetView Ownership Check

**Why:** Modifying creatures you don't own causes desync and multiplayer bugs.

**Correct Pattern:**
```csharp
// GOOD: Check ownership before mutation
var nview = character.GetComponent<ZNetView>();
if (nview != null && nview.IsValid() && nview.IsOwner())
{
    // Safe to modify
    ai.enabled = false;
}
```

**Incorrect Pattern:**
```csharp
// BAD: No ownership check
var ai = character.GetComponent<BaseAI>();
ai.enabled = false; // May cause desync!
```

---

### Rule 4: Never Apply Tier 2/Tier 3 Fields Silently

**Why:** Advanced fields should only apply when explicitly configured. Silent application confuses users.

**Correct Pattern:**
```csharp
// GOOD: Check if field is explicitly configured
if (config.Advanced?.DeathEffect?.Mode != null)
{
    ApplyDeathEffectOverride(prefab, config.Advanced.DeathEffect);
}
else
{
    // Leave at default - no silent changes
}
```

**Incorrect Pattern:**
```csharp
// BAD: Applying advanced defaults without checking
ApplyDeathEffectOverride(prefab, config.Advanced?.DeathEffect ?? defaultEffect);
// User didn't configure this, but we're changing it anyway
```

---

### Rule 5: Every No-Op Advanced Field Must Warn Once Per Context

**Why:** Users need to know when their config isn't being applied.

**Correct Pattern:**
```csharp
// GOOD: Warn once per context
if (config.Advanced?.Health?.RegenRate != null && !FeatureSafety.IsHealthRegenImplemented)
{
    ModifierValidation.WarnOnce($"Health.RegenRate for '{prefabName}' is set but not implemented.");
    // Don't apply - leave at default
}
```

---

### Rule 6: Every New Risky Runtime Mutation Needs Restore/Cleanup Logic

**Why:** Users need a way to undo changes without restarting.

**Correct Pattern:**
```csharp
// GOOD: Track what was changed for restoration
public static void ApplyRuntimeModifier(Character character, ModifierConfig config)
{
    // Store original values
    StoreOriginalValue(character, "speed", character.m_speed);
    
    // Apply modification
    character.m_speed *= config.SpeedMultiplier;
    
    // Mark for restoration
    character.gameObject.AddComponent<ModifierTracker>().Track("speed");
}

public static void RestoreOriginalValues(Character character)
{
    var tracker = character.GetComponent<ModifierTracker>();
    if (tracker != null)
    {
        foreach (var tracked in tracker.TrackedFields)
        {
            RestoreValue(character, tracked);
        }
    }
}
```

---

### Rule 7: Every Reload-Sensitive Change Must Pass cpc_reload_stress_test

**Why:** Reload is the most common source of subtle state corruption.

**Mandatory Test:**
```
cpc_reload_stress_test 5 --json
```

**Must Pass:**
- No scale drift
- No duplicate prefabs
- All iterations PASS

**See Also:** `docs/BETA_REGRESSION_CHECKLIST.md`

---

### Rule 8: Debug Commands Must Be Read-Only by Default Unless Explicitly Named as Apply/Spawn/Cleanup

**Why:** Users expect debug commands to be safe. Only commands with explicit action names should mutate.

**Naming Convention:**

| Prefix | Safe? | Examples |
|--------|-------|----------|
| `cpc_check_*` | ✅ Safe (read-only) | `cpc_check_saddled`, `cpc_check_advanced` |
| `cpc_dump_*` | ✅ Safe (read-only) | `cpc_dump_json`, `cpc_dump_prefab` |
| `cpc_trace_*` | ✅ Safe (read-only) | `cpc_mount_trace`, `cpc_runtime_trace` |
| `cpc_validate_*` | ✅ Safe (read-only) | `cpc_beta_validate` |
| `cpc_spawn_*` | ⚠️ Mutating (explicit) | `cpc_spawn`, `cpc_spawn_test_subject` |
| `cpc_cleanup_*` | ⚠️ Mutating (explicit) | `cpc_cleanup_test_subjects` |
| `cpc_restore_*` | ⚠️ Mutating (explicit) | `cpc_restore_target` |
| `cpc_apply_*` | ⚠️ Mutating (explicit) | `cpc_apply_override` |

**Correct Pattern:**
```csharp
// GOOD: Read-only debug command
public class CheckSaddledCommand : ConsoleCommand
{
    public override void Run(string[] args)
    {
        var state = GetState(); // Only reads, never modifies
        Log($"State: {state}");
    }
}

// GOOD: Explicitly named mutating command with safety check
public class SpawnCommand : ConsoleCommand
{
    public override void Run(string[] args)
    {
        if (!args.Contains("--confirm"))
        {
            Log("This command spawns creatures. Use --confirm to proceed.");
            return;
        }
        // Spawn logic...
    }
}
```

---

## Code Review Checklist

Before submitting changes:

- [ ] No recapture of baselines during reload
- [ ] Component presence checked with active/equipped verification
- [ ] ZNetView ownership checked before live creature mutation
- [ ] No silent Tier 2/3 field application
- [ ] Warnings for unimplemented features
- [ ] Restore/cleanup logic for runtime mutations
- [ ] `cpc_reload_stress_test 5` passes
- [ ] Debug command naming follows convention
- [ ] JSON dump paths are path-traversal safe

---

## Common Anti-Patterns to Avoid

### Anti-Pattern 1: Dictionary Clearing During Reload
```csharp
// BAD - clears baselines
public static void ReapplyAll(...)
{
    AppliedOverrides.Clear();
    OriginalScales.Clear(); // ❌ WRONG! Causes recapture of mutated values
    ApplyAll(configs);
}
```

### Anti-Pattern 2: False Positive Saddle Detection
```csharp
// BAD - component != null is not sufficient
bool isSaddled = character.GetComponent<Sadle>() != null; // ❌ FALSE POSITIVE
```

### Anti-Pattern 3: Silent Failures
```csharp
// BAD - no warning that config wasn't applied
if (config.Advanced?.Health?.MaxHealth != null)
{
    // This feature isn't implemented yet
    // But user doesn't know their config is being ignored!
}
```

### Anti-Pattern 4: Unsafe Debug Commands
```csharp
// BAD - modifies state without warning
public class DebugCommand : ConsoleCommand
{
    public override void Run(string[] args)
    {
        DeleteAllCreatures(); // Surprise! This deletes things!
    }
}
```

---

## Enforcement

These rules are enforced by:

1. **Code Review:** All PRs must be reviewed against this checklist
2. **Automated Testing:** `cpc_reload_stress_test` must pass
3. **Documentation:** Violations must be documented with rationale

---

## Changelog

| Date | Version | Change |
|------|---------|--------|
| 2025-06-11 | 1.1.0-beta | Initial development safety rules |
