# CreaturePrefabCreator 1.1.0-beta Regression Checklist

> **Version:** 1.1.0-beta  
> **Purpose:** Repeatable validation checklist after every fix build  
> **Prerequisite:** Fresh game session with clean boot

---

## Pre-Test Setup

1. **Exit Valheim completely** (not just to main menu)
2. **Install the new DLL** in `BepInEx/plugins/CreaturePrefabCreator/`
3. **Start Valheim** with a fresh copied test world/session
4. **Verify test config** is in place: `BepInEx/config/creaturePrefabCreator.json`

---

## Quick Validation (Run First)

These tests must pass before continuing to advanced tests.

### Step 1: Basic Status
```
cpc_status
```
**Expected:**
- No errors in console
- Shows plugin initialized
- Shows expected prefab count

---

### Step 2: Advanced Config Check
```
cpc_check_advanced
```
**Expected:**
- No crash
- No exception spam
- Lists config rules (if any advanced configs exist)

---

### Step 3: Reload Stress Test (CRITICAL)
```
cpc_reload_stress_test 5 --json
```
**Expected:**
- `Overall: PASS`
- No `SCALE DRIFT` warnings
- No `DUPLICATES` warnings
- JSON report generated in `BepInEx/config/CreaturePrefabCreator/dumps/`

**If this FAILS:** Stop testing. Report the failure. Do not continue.

---

### Step 4: Mount Detection - Unsaddled Wolf
1. Spawn or find a Wolf with **no saddle equipped**
2. Run:
```
cpc_mount_trace --json
```
**Expected:**
- `FINAL: Saddled=False, Ridden=False`
- `Reason: Tameable.HaveSaddle() returned false` (or similar)
- No `Sadle.HaveValidUser()=true`
- No `MountUp saddle activeSelf=true`

**If this FAILS (saddled=true when unsaddled):** Stop testing. Report the false positive.

---

### Step 5: Mount Detection - Saddled Mount (if MountUp installed)
1. Equip a saddle on a tameable creature
2. Run:
```
cpc_mount_trace --json
```
**Expected:**
- `FINAL: Saddled=True, Ridden=False`
- `Reason` explains which path detected the saddle (e.g., `Tameable.HaveSaddle() returned true`)

---

### Step 6: Mount Detection - Mounted Creature (if MountUp installed)
1. Mount the saddled creature
2. Run:
```
cpc_mount_trace --json
cpc_beta_validate --mountup
```
**Expected:**
- `FINAL: Saddled=True, Ridden=True`
- `mountUpRiderPresent=true` in trace

---

### Step 7: Beta Validation Report
```
cpc_beta_validate
```
**Expected:**
- No crash
- Report generated
- No repeated warning spam

---

## Advanced Testing (Only After Quick Validation Passes)

**DO NOT run these if Steps 1-7 had any failures.**

### Step 8: Advanced Health Test
Use config: `01_advanced_health_maxhealth_200.json`
```
cpc_reload_config
cpc_target_snapshot --json
```
**Expected:**
- Config loads without errors
- Creature maxHealth reflects override

---

### Step 9: Advanced Movement Speed Test
Use config: `02_advanced_movement_speed.json`
```
cpc_reload_config
cpc_target_snapshot --json
```
**Expected:**
- Config loads without errors
- Creature runSpeed reflects override

---

### Step 10: AI Disable/Restore Test
Use config with `disableAI: true` for a prefab
1. Spawn creature with AI disabled
2. Verify AI is disabled
3. Run:
```
cpc_restore_target
```
4. Verify AI is restored

---

## Post-Test Cleanup

```
cpc_cleanup_test_subjects
```

---

## Test Config Reference

| Config File | Purpose |
|-------------|---------|
| `00_baseline_clean_boot.json` | Clean boot validation |
| `01_advanced_health_maxhealth_200.json` | Health modifier test |
| `02_advanced_movement_speed.json` | Movement speed test |
| `02b_movement_speed_restore_condition_off.json` | Conditional restore test |

---

## Sign-Off Checklist

Before considering the build validated:

- [ ] `cpc_reload_stress_test 5` passes with no scale drift
- [ ] Unsaddled Wolf reports `saddled=false`
- [ ] Saddled creature reports `saddled=true`
- [ ] Mounted creature reports `ridden=true`
- [ ] `cpc_beta_validate` agrees with `cpc_mount_trace`
- [ ] No exception spam in logs
- [ ] JSON dumps are generated in correct folder

---

## Troubleshooting

### Scale Drift Detected
- Check `PrefabBaselineCache` is being used (not local dictionaries)
- Verify baselines are captured BEFORE overrides applied
- Check logs for "Late baseline capture" warnings

### False Saddled=true
- Run `cpc_mount_trace` to see which detection path is triggering
- Check if `Sadle` component exists but `HaveValidUser()` returns false
- Verify `MountUp saddle activeSelf` is false when unsaddled

### Duplicate Prefab Registrations
- Check `GeneratedPrefabManager.ClearAll()` is being called
- Verify `RegisteredPrefabs` is cleared on reload
- Check `ZNetScene` for duplicate entries

---

## Changelog

| Date | Version | Change |
|------|---------|--------|
| 2025-06-11 | 1.1.0-beta | Initial beta regression checklist |
