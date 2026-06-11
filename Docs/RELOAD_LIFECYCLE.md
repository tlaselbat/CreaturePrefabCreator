# CreaturePrefabCreator Reload Lifecycle Documentation

> **Version:** 1.1.0-beta  
> **Purpose:** Document the exact boot and reload order to prevent mutation of already-mutated prefabs  
> **Warning:** Violating these lifecycle rules causes scale compounding, state corruption, and hard-to-debug issues.

---

## Overview

CreaturePrefabCreator (CPC) modifies vanilla and generated creature prefabs at runtime. The most critical safety rule is:

> **NEVER recapture original prefab values from already-mutated ZNetScene prefabs.**

This document explains the exact lifecycle stages, when it's safe to capture baselines, and how reload operations must preserve those baselines.

---

## Lifecycle Stages

### Stage 1: Plugin Awake

**When:** During BepInEx plugin initialization (before any game objects exist)

**Actions:**
1. Bind configuration entries (BepInEx Config.Bind)
2. Initialize Harmony instance
3. Apply Harmony patches
4. Initialize soft-dependency detectors (MountUpRestored, AllTameable)
5. Register console commands
6. Subscribe to `PrefabManager.OnVanillaPrefabsAvailable`

**Safety Notes:**
- **DO NOT** attempt to access ZNetScene or prefabs here - they don't exist yet
- **DO NOT** cache any prefab-related data here
- Configuration loading happens here, but prefab application does NOT

```csharp
private void Awake()
{
    Instance = this;
    _harmony.PatchAll();
    MountUpCompatibilityPatch.Initialize(_harmony);
    
    // Bind configs...
    
    // Hook into Jotunn's prefab-ready event
    PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;
}
```

---

### Stage 2: Vanilla Prefabs Available

**When:** Jotunn raises `OnVanillaPrefabsAvailable` (vanilla prefabs have been added to ZNetScene)

**Actions:**
1. Clear any stale state from previous world sessions
2. **CAPTURE ORIGINAL PREFAB BASELINES** (critical - only safe time to do this)
3. Apply prefab overrides (mutates ZNetScene prefabs)
4. Generate and register new prefabs (clones with mutations)
5. Initialize runtime modifiers

**Critical Ordering:**
```csharp
private void OnVanillaPrefabsAvailable()
{
    // 1. Clear all cached state
    PrefabOverrideManager.ClearAll();
    GeneratedPrefabManager.ClearAll();
    
    // 2. CAPTURE ORIGINAL SCALES BEFORE ANY MUTATION
    // This is the ONLY safe time to capture baselines
    GeneratedPrefabManager.CaptureOriginalSourceScales(LoadedConfig.GeneratedPrefabs);
    
    // 3. Apply overrides (mutates ZNetScene prefabs)
    PrefabOverrideManager.ApplyAll(LoadedConfig.PrefabOverrides, ConfigEnableFactionOverrides.Value);
    
    // 4. Generate new prefabs (uses captured baselines)
    GeneratedPrefabManager.GenerateAll(LoadedConfig.GeneratedPrefabs, ConfigEnableFactionOverrides.Value);
    
    // 5. Initialize runtime systems
    RuntimeModifierManager.Initialize(LoadedConfig.RuntimeModifiers);
}
```

**⚠️ CRITICAL WARNING:**
- `CaptureOriginalSourceScales()` MUST run before `ApplyAll()`
- After `ApplyAll()` runs, ZNetScene prefabs are MUTATED
- Any baseline capture after this point would capture mutated values, not original values

---

### Stage 3: World Running

**When:** Player is in-world, creatures are spawning

**Actions:**
- Runtime modifiers monitor creature states
- AI enable/disable applies to live instances
- Mount/rider detection operates on live creatures

**Safety Notes:**
- Live creatures are instances spawned from prefabs
- Modifying live creatures is different from modifying prefab templates
- Always check ZNetView ownership before modifying live creatures

---

### Stage 4: Config Reload (`cpc_reload_config`)

**When:** Player executes `cpc_reload_config` console command

**Actions:**
1. Re-read configuration files from disk
2. Clear override tracking (but NOT original baselines)
3. Re-apply prefab overrides
4. Re-register generated prefabs
5. Re-initialize runtime modifiers

**Critical Safety Rules:**

```csharp
public static void ReapplyAll(List<PrefabOverrideConfig> configs, ...)
{
    // OK: Clear applied override tracking
    AppliedOverrides.Clear();
    
    // OK: Clear config cache
    _allConfigs = null;
    
    // ⚠️ NEVER CLEAR OriginalScales HERE!
    // OriginalScales contains TRUE vanilla baselines captured at boot
    // Clearing it would cause re-capture of already-mutated prefabs
    
    ApplyAll(configs, factionOverridesEnabled);
}
```

**Why OriginalScales Must Persist:**
- Original baselines were captured from clean vanilla prefabs
- ZNetScene prefabs are now mutated (scaled, etc.)
- If we recapture, we'd get mutated values, not original values
- Re-applying scale overrides would compound: 1.5x * 1.5x = 2.25x

---

## Baseline Cache Lifecycle

### What Must Be Preserved Across Reloads

| Cache | Type | Persist Across Reload? | Clear on World Unload? |
|-------|------|------------------------|------------------------|
| `OriginalScales` | Original prefab localScale | ✅ YES | ✅ YES |
| `OriginalPrefabScales` | Source prefab scales for generated | ✅ YES | ✅ YES |
| `AppliedOverrides` | Which overrides are applied | ❌ NO (cleared) | ✅ YES |
| `_allConfigs` | Config cache | ❌ NO (cleared) | ✅ YES |
| Runtime modifier state | Live creature states | ❌ NO (rebuilt) | ✅ YES |

### Safe Baseline Access Pattern

```csharp
// SAFE: Read original baseline from centralized cache
Vector3 originalScale = PrefabBaselineCache.GetOriginalScale(prefabName);

// SAFE: Compute final scale from original baseline
Vector3 finalScale = originalScale * config.Scale;

// UNSAFE: Never read from already-mutated prefab
Vector3 wrongScale = znetScenePrefab.transform.localScale; // May already be scaled!
```

---

## System Mutation Permissions

### Systems Allowed to Mutate ZNetScene Prefab Templates

| System | Can Mutate Prefab Template | Can Mutate Live Instances |
|--------|---------------------------|---------------------------|
| `PrefabOverrideManager` | ✅ YES (at boot only) | ❌ NO |
| `GeneratedPrefabManager` | ✅ YES (registration) | ❌ NO |
| `RuntimeModifierManager` | ❌ NO | ✅ YES (with ownership check) |
| `SaddledCreaturePatch` | ❌ NO | ✅ YES (AI enable/disable) |

### Important Distinction

- **Prefab Template:** The GameObject in ZNetScene that is used as a blueprint for spawning
- **Live Instance:** A spawned creature in the world (has ZDO, ZNetView, position in world)

Only mutate prefab templates during the initial boot pass. Never during reload.

---

## Reload Command: Exact Execution Order

### `cpc_reload_config` (Full Reload)

```
1. Log: "[Reload] Reading config..."
2. Load new CreaturePrefabCreatorConfig from JSON
3. Log: "[Reload] Clearing prefab override registrations..."
4. PrefabOverrideManager.ReapplyAll(newConfig.PrefabOverrides)
   a. AppliedOverrides.Clear()
   b. _allConfigs = null
   c. ApplyAll() - applies to ZNetScene prefabs
5. GeneratedPrefabManager.ReregisterAll(newConfig.GeneratedPrefabs)
   a. Unregisters old generated prefabs from ZNetScene
   b. Re-captures source scales (from baseline cache, NOT from ZNetScene)
   c. Generates and registers new prefabs
6. RuntimeModifierManager.ClearAll()
7. RuntimeModifierManager.Initialize(newConfig.RuntimeModifiers)
8. Log: "[Reload] Reload complete."
```

### `cpc_reload_config --prefabs-only`

Same as above, but skips runtime modifier re-initialization.

### `cpc_reload_config --runtime-only`

Only clears and re-initializes runtime modifiers. Does not touch prefabs.

---

## Common Pitfalls and How to Avoid Them

### Pitfall 1: Recapturing Baselines During Reload

**Problem:**
```csharp
public static void ReapplyAll(...)
{
    AppliedOverrides.Clear();
    OriginalScales.Clear(); // ❌ WRONG! Recaptures mutated values!
    ApplyAll(configs, ...);
}
```

**Consequence:** Each reload compounds scale: 1.0 -> 1.5 -> 2.25 -> 3.375

**Solution:** Never clear original baseline caches during reload. Only clear tracking caches.

---

### Pitfall 2: Generated Prefab Using Mutated Source Scale

**Problem:**
```csharp
var sourcePrefab = ZNetScene.instance.GetPrefab(config.SourcePrefab);
Vector3 sourceScale = sourcePrefab.transform.localScale; // May already be overridden!
Vector3 finalScale = sourceScale * config.Scale;
```

**Consequence:** Generated prefab gets double-scaled from already-overridden source.

**Solution:** Always use `PrefabBaselineCache.GetOriginalScale(config.SourcePrefab)` for source scale.

---

### Pitfall 3: Clearing Baselines on World Unload

**Problem:** Not clearing baselines when world completely unloads can cause stale data in next world.

**Solution:** Hook into world unload event and clear baselines:
```csharp
private void OnWorldUnload()
{
    PrefabBaselineCache.ClearAll();
}
```

---

## Testing Reload Safety

### Manual Test Protocol

1. **Clean Boot Test:**
   ```
   cpc_status
   cpc_check_advanced
   ```
   Verify no warnings, expected prefab count.

2. **Single Reload Test:**
   ```
   cpc_reload_config
   cpc_status
   ```
   Verify scales unchanged, no duplicate prefabs.

3. **Stress Test:**
   ```
   cpc_reload_stress_test 5
   ```
   All should PASS. No scale drift, no duplicate registrations.

4. **Verify Baseline Integrity:**
   ```
   cpc_mount_trace --json
   ```
   Check JSON includes `prefabBaselineScale` field matching original.

---

## Developer Checklist

Before modifying any reload-related code:

- [ ] Does this code run during the correct lifecycle stage?
- [ ] Does this code capture baselines BEFORE any mutation?
- [ ] Does this code preserve baselines across reloads?
- [ ] Does this code use `PrefabBaselineCache` instead of direct prefab reads?
- [ ] Does this code distinguish between prefab templates and live instances?
- [ ] Have you run `cpc_reload_stress_test 5` after changes?
- [ ] Do all beta validation tests pass?

---

## Related Documentation

- `docs/DEVELOPMENT_SAFETY_RULES.md` - General safety rules for all CPC development
- `docs/BETA_REGRESSION_CHECKLIST.md` - Testing checklist after any changes
- `Core/PrefabBaselineCache.cs` - Centralized baseline cache implementation
- `Detection/MountStateDetector.cs` - Centralized mount/saddle detection

---

## Changelog

| Date | Version | Change |
|------|---------|--------|
| 2025-06-11 | 1.1.0-beta | Initial reload lifecycle documentation |
