# Creature Prefab Creator - Parameter Reference

Quick reference for all configuration parameters in the CreaturePrefabCreator mod.

---

## Overview: Mod Responsibilities

| Mod | Handles | Config File |
|-----|---------|-------------|
| **AllTameable** | Taming, food, breeding/procreation, population, faction changes | `AllTameable_TameList_From_Default.cfg` |
| **MountUp** | Mounting/rideability, saddle position, mount stamina | `meldurson.MountUpRestored.cfg` |
| **CreaturePrefabCreator** | Prefab creation, visual scaling, stat multipliers, death effects, growth transformation | `creaturePrefabCreator.json` |

**Key Principle:** AllTameable handles taming/breeding; MountUp handles riding; CreaturePrefabCreator handles **prefab lifecycle** (creation → scaling → growth → death effects).

---

## Configuration Types

| Type | Purpose | Config Key |
|------|---------|------------|
| **Prefab Override** | Modify existing creatures | `prefabOverrides[]` |
| **Generated Prefab** | Create new creatures (e.g., babies) | `generatedPrefabs[]` |

---

## 1. Core/Identity Parameters (CreaturePrefabCreator Only)

### Required

| Parameter | Type | Description | Override | Generated |
|-----------|------|-------------|:--------:|:---------:|
| `targetPrefab` | string | Prefab to modify | ✅ | — |
| `sourcePrefab` | string | Base prefab to clone | — | ✅ |
| `newPrefab` | string | Unique name for new creature | — | ✅ |

### Optional

| Parameter | Type | Default | Description | Override | Generated |
|-----------|------|---------|-------------|:--------:|:---------:|
| `enabled` | boolean | `true` | Activate this entry | ✅ | ✅ |
| `displayName` | string | `""` | In-game creature name | ✅ | ✅ |

---

## 2. Visual Scaling (⚠️ Overlaps with AllTameable)

**AllTameable also has:** `size` (column 15 or override) — sets visual scale for tamed creatures.

**Use CreaturePrefabCreator when:** You want scale applied to **all** creatures (wild + tamed), or for generated prefabs that don't exist in AllTameable yet.

| Parameter | Type | Default | Range | Description | Override | Generated |
|-----------|------|---------|-------|-------------|:--------:|:---------:|
| `scale` | float | `1.0` / `0.35` | > 0 | Model size multiplier | ✅ | ✅ |
| `deathEffectScaleMultiplier` | float? | `null` | 0.01–100 | Scale death effect ragdolls at runtime | ✅ | ✅ |

### Propagation (Overrides Only)

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `propagateToGeneratedVariants` | boolean | `true` | Pass scale/stats/faction to child generated variants |
| `applyToExistingSpawnedCreatures` | boolean | `false` | Apply to already-spawned creatures *(not implemented — requires runtime sync strategy)* |

---

## 3. Stat Modifiers (CreaturePrefabCreator Only)

**These are multipliers applied on top of base stats.** AllTameable sets base taming values; these modify the final result.

| Parameter | Type | Default | Range | Description | Override | Generated |
|-----------|------|---------|-------|-------------|:--------:|:---------:|
| `healthMultiplier` | float? | `null` | 0.01–100 | Health multiplier (null = no change) | ✅ | ✅ |
| `damageMultiplier` | float? | `null` | 0.01–100 | Damage output multiplier (null = no change) | ✅ | ✅ |

> **Note:** Damage multiplier affects all damage types: `damage`, `blunt`, `slash`, `pierce`, `chop`, `pickaxe`, `fire`, `frost`, `lightning`, `poison`, `spirit`

---

## 4. Death Effects (CreaturePrefabCreator Only)

AllTameable does not modify death effects. Use these to fix broken vanilla effects or copy from similar creatures.

| Parameter | Type | Default | Description | Override | Generated |
|-----------|------|---------|-------------|:--------:|:---------:|
| `clearDeathEffects` | boolean | `false` | Remove all death effects | ✅ | ✅ |
| `copyDeathEffectsFrom` | string? | `null` | Copy effects from another prefab | ✅ | ✅ |

---

## 5. Faction Override (CreaturePrefabCreator Only)

AllTameable has a boolean `changeFaction` that switches creatures to player faction when tamed. CPC's `forceFaction` directly sets a specific faction on the prefab itself.

| Parameter | Type | Default | Description | Override | Generated |
|-----------|------|---------|-------------|:--------:|:---------:|
| `forceFaction` | string? | `null` | Set specific faction (see valid values below) | ✅ | ✅ |

**Valid faction values:** `Players`, `Animals`, `ForestMonsters`, `PlainsMonsters`, `Undead`, `Demon`, `Mythical`, `Boss`, `PlayerFaction`, `Enemy`, `Dungeon` — case-insensitive, accepts aliases like "Player", "Forest", "Plains", "Undead", "Hell", etc.

**Inheritance:** When `propagateToGeneratedVariants=true`, generated prefabs inherit the faction from their source prefab override.

---

## 6. Growth Transformation (CreaturePrefabCreator Only — ⚠️ Different from AllTameable)

**AllTameable's `growTime` (column 15):** Time for offspring to visually grow to adult size while staying the same prefab.

**CreaturePrefabCreator's growth system:** Transform the creature into a **different prefab** entirely (e.g., `Bjorn_cub` → `Bjorn`).

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `adultPrefab` | string | `""` | Target prefab when growing up |
| `growIntoAdult` | boolean | `true` | Enable transformation to adultPrefab |
| `growTimeSeconds` | float | `6000` | Seconds before transformation (~100 min) |
| `preserveTamed` | boolean | `true` | Keep tamed status after transformation |
| `preserveLevel` | boolean | `true` | Keep creature level after transformation |
| `preserveOwner` | boolean | `true` | Keep ownership after transformation |
| `preserveName` | boolean | `false` | Keep custom name after transformation |

---

## Parameter Quick Reference

### Generated Prefabs Only
```
adultPrefab, growIntoAdult, growTimeSeconds, newPrefab, preserveLevel,
preserveName, preserveOwner, preserveTamed, sourcePrefab
```

### Prefab Overrides Only
```
applyToExistingSpawnedCreatures, propagateToGeneratedVariants, targetPrefab
```

### Shared (Both)
```
clearDeathEffects, copyDeathEffectsFrom, damageMultiplier,
deathEffectScaleMultiplier, displayName, enabled, forceFaction,
healthMultiplier, scale
```

---

## AllTameable vs CreaturePrefabCreator: Feature Comparison

| Feature | AllTameable | CreaturePrefabCreator |
|---------|-------------|----------------------|
| **Makes creatures tamable** | ✅ Yes | ❌ No |
| **Taming time** | ✅ `tamingTime` | ❌ N/A |
| **Food/consume items** | ✅ `consumeItems` | ❌ N/A |
| **Breeding/procreation** | ✅ `procreation`, `pregnancyChance` | ❌ N/A — *strips Procreation from generated prefabs* |
| **Visual scale** | ✅ `size` (tamed only) | ✅ `scale` (all creatures) |
| **Population caps** | ✅ `maxCreatures` | ❌ N/A |
| **Offspring name** | ✅ `offspringName` | ❌ N/A — uses `newPrefab` |
| **Growth to adult size** | ✅ `growTime` (visual growth) | ✅ `growTimeSeconds` (prefab transformation) |
| **Stat multipliers** | ❌ Base stats only | ✅ `healthMultiplier`, `damageMultiplier` |
| **Death effects** | ❌ No | ✅ `clearDeathEffects`, `copyDeathEffectsFrom` |
| **Growth preservation** | ❌ N/A | ✅ `preserveTamed`, `preserveLevel`, etc. |
| **Faction control** | ✅ `changeFaction` (boolean, tamed only) | ✅ `forceFaction` (specific faction: Players, ForestMonsters, PlainsMonsters, Undead, Demon, etc.) |

---

## Example Configurations

### Generated Prefab (Baby Creature)

```json
{
  "generatedPrefabs": [
    {
      "enabled": true,
      "sourcePrefab": "Bjorn",
      "newPrefab": "Bjorn_cub",
      "adultPrefab": "Bjorn",
      "displayName": "Bjorn Cub",
      "scale": 0.35,
      "growIntoAdult": true,
      "growTimeSeconds": 6000,
      "preserveTamed": true,
      "preserveLevel": true,
      "preserveOwner": true,
      "preserveName": false,
      "healthMultiplier": 0.5,
      "damageMultiplier": 0.3,
      "copyDeathEffectsFrom": "Bjorn",
      "deathEffectScaleMultiplier": 0.35
    }
  ]
}
```

### Prefab Override (Modify Existing)

```json
{
  "prefabOverrides": [
    {
      "enabled": true,
      "targetPrefab": "Wolf",
      "displayName": "Dire Wolf",
      "scale": 1.5,
      "healthMultiplier": 2.0,
      "damageMultiplier": 1.5,
      "propagateToGeneratedVariants": true
    }
  ]
}
```

---

## Notes

- **Config location:** `BepInEx/config/CreaturePrefabCreator/creaturePrefabCreator.json`
- Nullable floats accept `null` or a number
- Generated prefabs automatically strip `Procreation` components to prevent breeding loops
- `OffspringGrowup` component auto-added when `growIntoAdult` is enabled

---

*Config file: `creaturePrefabCreator.json`*
