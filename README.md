# CreaturePrefabCreator

A Valheim BepInEx/Jötunn plugin for generating custom creature prefabs and overriding existing ones. Supports baby-to-adult growth, runtime stat scaling, AI/audio/visual tuning, faction control, and deep AllTameable integration — all driven by a single JSON config file.

---

## Table of Contents

1. [Installation](#installation)
2. [File Locations](#file-locations)
3. [Feature Overview](#feature-overview)
4. [Global Settings (.cfg)](#global-settings-cfg)
5. [Feature Safety Gates](#feature-safety-gates)
6. [Generated Prefabs](#generated-prefabs)
7. [Prefab Overrides](#prefab-overrides)
8. [Runtime Modifiers](#runtime-modifiers)
9. [Advanced Modifier Schema](#advanced-modifier-schema-v110)
10. [AllTameable Integration](#alltameable-integration)
11. [Debug Commands](#debug-commands)
12. [Multiplayer & Dedicated Servers](#multiplayer--dedicated-servers)
13. [Troubleshooting](#troubleshooting)
14. [Building from Source](#building-from-source)

---

## Installation

1. Install [BepInEx](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
2. Install [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/)
3. Place `CreaturePrefabCreator.dll` in `BepInEx/plugins/`
4. Launch the game once — config files are auto-generated on first run

Optional mod support (no installation required, auto-detected at runtime):
- **AllTameable_TamingOverhaul** — enables `specificOffspring` baby spawning
- **MountUp** — enables saddle/ridden detection in runtime modifier conditions

---

## File Locations

| File | Purpose |
|------|---------|
| `BepInEx/config/com.clickcs.creatureprefabcreator.cfg` | Global on/off switches and debug flags |
| `BepInEx/config/CreaturePrefabCreator/creaturePrefabCreator.json` | All creature definitions (generated prefabs, overrides, runtime rules) |
| `BepInEx/config/CreaturePrefabCreator/dumps/` | Output folder for AI dump commands |

The JSON file is **shared** — server and all clients must have the same `creaturePrefabCreator.json`. When using a dedicated server, place the file in the server's BepInEx config directory and distribute the same file to all players.

---

## Feature Overview

| Feature | Stable? | Default On? | Description |
|---------|---------|------------|-------------|
| Generated Prefabs | Stable | Yes | Clone any creature into a new prefab with a unique name, scale, and growth behaviour |
| Prefab Overrides | Stable | Yes | Modify any existing creature's scale, name, health, damage, AI, audio, and visuals |
| Baby-to-Adult Growth | Stable | Yes (per-entry) | ZDO-backed growth timer — babies grow into adults, preserving tame/level/owner/name |
| Runtime Modifiers | Beta | No | Dynamic stat multipliers applied per-creature based on star level, tame state, saddle, or ridden status |
| Faction Overrides | Beta | No | Change creature faction (e.g. make a Wolf friendly to players) |
| Visual Overrides | Beta | No | Tint and glow colour applied to prefab materials |
| Riding AI Suppression | Beta | No | Creatures with `disableAI=true` regain AI while actively being ridden |
| Config Sync | Experimental | No | Server-to-client config sync via RPC (not fully implemented) |

---

## Global Settings (.cfg)

`BepInEx/config/com.clickcs.creatureprefabcreator.cfg`

```ini
[General]
Enabled = true
VerboseLogging = true
RegisterConsoleCommands = true

[Debug]
DebugMountState = false
DebugAIState = false
EnableDebugDumpCommands = false

[FeatureSafety]
EnableGeneratedPrefabs = true
EnablePrefabOverrides = true
EnableRuntimeModifiers = false
EnableFactionOverrides = false
EnableVisualOverrides = false
EnableRidingAISuppression = false
EnableConfigSync = false
LogBetaFeatureWarnings = true
```

**`VerboseLogging`** — When `true`, all `[CreaturePrefabCreator]` log lines appear in `BepInEx/LogOutput.log`. Set to `false` on production servers to reduce log noise; errors are always written regardless.

**`RegisterConsoleCommands`** — Enables the `cpc_*` debug commands in-game. Safe to leave on.

**`EnableDebugDumpCommands`** — Enables the `cpc_dump_ai_*` commands that write creature AI data to JSON files. Read-only. Disabled by default.

---

## Feature Safety Gates

All high-risk or beta features are individually gated in `[FeatureSafety]`. This lets you enable individual features as you gain confidence without touching the main plugin toggle.

If a beta feature is enabled, a warning is written to the log at startup describing the risk. Set `LogBetaFeatureWarnings = false` to suppress these after you've acknowledged them.

---

## Generated Prefabs

Generated prefabs are **clones** of an existing creature prefab, given a new name and optional modifications. They are registered into the game as first-class networked objects, visible to ZNetScene and compatible with AllTameable's `specificOffspring` field.

When a generated prefab is created:
- The source prefab is cloned and renamed
- All `Procreation` components are stripped (prevents baby-breeding loops and AllTameable crashes)
- Default items on Humanoid prefabs are cleared (prevents ZSyncTransform errors)
- Optional components (MountUp `Mountable`, etc.) are stripped if present
- The growth component (`OffspringGrowup`) is attached if `growIntoAdult = true`
- The template is stored in a hidden persistent container — it never ticks in the game world

### Config structure

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
      "healthMultiplier": null,
      "damageMultiplier": null,
      "clearDeathEffects": false,
      "copyDeathEffectsFrom": null,
      "deathEffectScaleMultiplier": null,
      "forceFaction": null,
      "disableAI": false,
      "disableAggro": false,
      "disableFleeing": false,
      "disableIdleSounds": false,
      "tintColor": null,
      "glowColor": null,
      "friendAttacked": null
    }
  ]
}
```

### Field reference

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `enabled` | bool | `true` | Enable or disable this entry without removing it |
| `sourcePrefab` | string | — | **Required.** The prefab to clone (e.g. `"Wolf"`, `"Bjorn"`, `"Lox"`) |
| `newPrefab` | string | — | **Required.** Unique name for the generated prefab. Must differ from `sourcePrefab` |
| `adultPrefab` | string | `""` | Target prefab to grow into. Required when `growIntoAdult = true` |
| `displayName` | string | `""` | In-game hover name. Leave empty to inherit from source |
| `scale` | float | `0.35` | Size multiplier relative to the source prefab's vanilla scale. `0.35` = 35% size, `1.0` = same as source, `2.0` = double |
| `growIntoAdult` | bool | `true` | Attach growth logic — the creature despawns and is replaced by `adultPrefab` after `growTimeSeconds` |
| `growTimeSeconds` | float | `6000` | Real-world seconds until growth (not affected by in-game time speed) |
| `preserveTamed` | bool | `true` | Copy tame status from baby ZDO to adult ZDO on growth |
| `preserveLevel` | bool | `true` | Copy star level (1 = no stars, 2 = 1-star, 3 = 2-star) to adult |
| `preserveOwner` | bool | `true` | Copy owner player ID to adult — keeps the taming credit |
| `preserveName` | bool | `false` | Copy custom `cname` to adult. Use `true` if players name their pets |
| `healthMultiplier` | float? | `null` | Multiply base health on the prefab template. `null` or `1.0` = no change |
| `damageMultiplier` | float? | `null` | Multiply outgoing damage on the prefab template. Applied at attack time |
| `clearDeathEffects` | bool | `false` | Remove all death effects (ragdoll, particles, sounds) from this prefab |
| `copyDeathEffectsFrom` | string? | `null` | Copy death effects from another named prefab (e.g. copy `Wolf` ragdoll onto a custom cub) |
| `deathEffectScaleMultiplier` | float? | `null` | Scale the ragdoll spawned on death. `0.5` = half size ragdoll. Requires `copyDeathEffectsFrom` or existing effects |
| `forceFaction` | string? | `null` | Override creature faction. Requires `EnableFactionOverrides = true`. See [Faction Values](#faction-values) |
| `disableAI` | bool | `false` | Disable `MonsterAI` component — creature stands still, does not attack or flee. Marks creature with `PermanentAIDisabledMarker` |
| `disableAggro` | bool | `false` | Set `m_aggravatable = false` — creature can never be alerted or aggroed |
| `disableFleeing` | bool | `false` | Zero all flee fields — creature never runs away even when hurt |
| `disableIdleSounds` | bool | `false` | Clear `m_idleSounds` on the `CreatureAudio` component — silent creature |
| `tintColor` | string? | `null` | HTML hex colour to tint all renderers (e.g. `"#FF0000"` for red). Requires `EnableVisualOverrides = true` |
| `glowColor` | string? | `null` | HTML hex colour for emission/glow (e.g. `"#00FFFF"` for cyan glow). Requires `EnableVisualOverrides = true` |
| `friendAttacked` | bool? | `null` | Set `m_friendAttacked` on `MonsterAI`. Controls whether creature defends allies when they are attacked |

### Scale inheritance

Scale is computed as:

```
finalScale = originalSourceScale × config.scale × effectiveMultiplier
```

Where `effectiveMultiplier` is, in priority order:
1. A direct `prefabOverride` targeting the `newPrefab` name
2. An inherited multiplier from a `prefabOverride` targeting the `sourcePrefab` with `propagateToGeneratedVariants = true`
3. `1.0` (no multiplier)

This means if you double a Wolf via override, any Wolf-based generated cubs are also automatically proportionally scaled without touching their own config.

### Real-world examples

**Bear cub that grows into an adult bear (AllTameable workflow):**
```json
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
  "preserveName": false
}
```

**Tiny decorative wolf statue — no AI, no sounds:**
```json
{
  "enabled": true,
  "sourcePrefab": "Wolf",
  "newPrefab": "Wolf_statue",
  "displayName": "Stone Wolf",
  "scale": 0.5,
  "growIntoAdult": false,
  "disableAI": true,
  "disableIdleSounds": true,
  "tintColor": "#888888"
}
```

**Wolf cub with proper death ragdoll from adult wolf:**
```json
{
  "enabled": true,
  "sourcePrefab": "Wolf",
  "newPrefab": "Wolf_cub",
  "adultPrefab": "Wolf",
  "displayName": "Wolf Cub",
  "scale": 0.4,
  "growIntoAdult": true,
  "growTimeSeconds": 4800,
  "clearDeathEffects": true,
  "copyDeathEffectsFrom": "Wolf",
  "deathEffectScaleMultiplier": 0.4,
  "preserveTamed": true,
  "preserveLevel": true,
  "preserveOwner": true,
  "preserveName": true
}
```

**Non-aggressive lox calf that grows into an adult, silently:**
```json
{
  "enabled": true,
  "sourcePrefab": "Lox",
  "newPrefab": "Lox_calf",
  "adultPrefab": "Lox",
  "displayName": "Lox Calf",
  "scale": 0.3,
  "growIntoAdult": true,
  "growTimeSeconds": 9000,
  "disableAggro": true,
  "disableFleeing": true,
  "disableIdleSounds": true,
  "preserveTamed": true,
  "preserveLevel": true,
  "preserveOwner": true
}
```

---

## Prefab Overrides

Prefab overrides modify **existing** creature prefabs in-place at load time. Changes apply to all future spawns of that creature. They do not retroactively change already-spawned creatures by default.

Overrides also propagate selected values to any generated prefabs that use this creature as their `sourcePrefab` — controlled by `propagateToGeneratedVariants`.

### Config structure

```json
{
  "prefabOverrides": [
    {
      "enabled": true,
      "targetPrefab": "Wolf",
      "displayName": "",
      "scale": 1.5,
      "applyToExistingSpawnedCreatures": false,
      "propagateToGeneratedVariants": true,
      "healthMultiplier": null,
      "damageMultiplier": null,
      "clearDeathEffects": false,
      "copyDeathEffectsFrom": null,
      "deathEffectScaleMultiplier": null,
      "forceFaction": null,
      "disableAI": false,
      "disableAggro": false,
      "disableFleeing": false,
      "disableIdleSounds": false,
      "tintColor": null,
      "glowColor": null,
      "friendAttacked": null
    }
  ]
}
```

### Field reference

| Field | Type | Default | Description |
|-------|------|---------|-------------|
| `enabled` | bool | `true` | Enable or disable this entry |
| `targetPrefab` | string | — | **Required.** Name of the existing prefab to modify (e.g. `"Wolf"`, `"Boar"`, `"Lox"`) |
| `displayName` | string | `""` | Rename the creature's hover text. Empty = no change |
| `scale` | float | `1.0` | Absolute scale multiplier. Applied relative to the prefab's original vanilla scale, so `2.0` always means double vanilla — not double of a previous override |
| `applyToExistingSpawnedCreatures` | bool | `false` | Reserved. Not yet implemented — future spawns are always affected, existing ones are not |
| `propagateToGeneratedVariants` | bool | `true` | When `true`, generated prefabs using this as `sourcePrefab` inherit this override's scale, health, damage, and faction multipliers |
| `healthMultiplier` | float? | `null` | Multiply max health of the prefab template |
| `damageMultiplier` | float? | `null` | Multiply outgoing damage |
| `clearDeathEffects` | bool | `false` | Remove all death effects |
| `copyDeathEffectsFrom` | string? | `null` | Copy death effects from another prefab |
| `deathEffectScaleMultiplier` | float? | `null` | Scale the death ragdoll |
| `forceFaction` | string? | `null` | Override faction. Requires `EnableFactionOverrides = true` |
| `disableAI` | bool | `false` | Disable `MonsterAI` — creature stands still permanently |
| `disableAggro` | bool | `false` | Make creature non-aggroable |
| `disableFleeing` | bool | `false` | Prevent fleeing behaviour |
| `disableIdleSounds` | bool | `false` | Silence idle audio |
| `tintColor` | string? | `null` | HTML hex tint. Requires `EnableVisualOverrides = true` |
| `glowColor` | string? | `null` | HTML hex emission colour. Requires `EnableVisualOverrides = true` |
| `friendAttacked` | bool? | `null` | Set `m_friendAttacked` behaviour on `MonsterAI` |

### Real-world examples

**Double the size of all wolves on your server:**
```json
{
  "enabled": true,
  "targetPrefab": "Wolf",
  "scale": 2.0,
  "propagateToGeneratedVariants": true
}
```
Any Wolf cubs you define in `generatedPrefabs` will automatically be twice as large as well.

**Rename all Lox to "Plains Mammoth" and buff their health by 50%:**
```json
{
  "enabled": true,
  "targetPrefab": "Lox",
  "displayName": "Plains Mammoth",
  "healthMultiplier": 1.5
}
```

**Make all Necks completely passive — useful for fishing ponds or decorative builds:**
```json
{
  "enabled": true,
  "targetPrefab": "Neck",
  "disableAI": true,
  "disableAggro": true,
  "disableFleeing": true,
  "disableIdleSounds": true
}
```

**Give Boars 50% extra damage and make them never flee:**
```json
{
  "enabled": true,
  "targetPrefab": "Boar",
  "damageMultiplier": 1.5,
  "disableFleeing": true
}
```

### Faction values

Valid strings for `forceFaction` (case-insensitive, several aliases supported):

| Value | Aliases | Notes |
|-------|---------|-------|
| `"Players"` | `"player"`, `"players"` | Friendly to players — **use with caution**, may cause unexpected AI behaviour |
| `"ForestMonsters"` | `"forest"`, `"forestcreature"` | Black forest creatures |
| `"PlainsMonsters"` | `"plains"`, `"plainsmonster"` | Plains creatures |
| `"Undead"` | `"undead"`, `"dungeon"` | Undead / dungeon creatures |
| `"Demon"` | `"hell"` | Ashlands demon faction |
| `"Boss"` | `"boss"`, `"bosses"` | Boss creatures |
| `"Mathical"` | — | Mistlands mythical creatures |

---

## Runtime Modifiers

Runtime modifiers apply **dynamic stat changes** to live creature instances based on their current state. Unlike prefab overrides and generated prefabs which modify the template at load time, runtime modifiers evaluate conditions on every affected creature and adjust stats accordingly in real time.

Runtime modifiers are **disabled by default** (`EnableRuntimeModifiers = false`). Enable in `[FeatureSafety]` when ready.

### When re-evaluation happens

- Creature spawns (`Character.Awake`)
- Star level changes (`Character.SetLevel`)
- Tame state changes (`Character.SetTamed`)
- Equipment changes (`Humanoid.EquipItem` / `UnequipItem`)
- Periodic ticker (every ~5 seconds)

### Config structure

```json
{
  "runtimeModifiers": [
    {
      "enabled": true,
      "targetPrefab": "Bjorn",
      "conditions": {
        "starLevel": null,
        "tamed": true,
        "saddled": null,
        "ridden": null
      },
      "effects": {
        "healthMultiplier": 1.5,
        "damageMultiplier": 1.2,
        "movementSpeedMultiplier": null,
        "disableAI": null
      }
    }
  ]
}
```

### Conditions

All conditions are **AND**ed together. An empty or omitted `conditions` block matches every instance of the prefab.

| Field | Type | Description |
|-------|------|-------------|
| `starLevel` | `int?` | Match exact internal level. `1` = no stars, `2` = 1-star, `3` = 2-star, `4` = 3-star. `null` = ignore |
| `tamed` | `bool?` | `true` = only tamed creatures, `false` = only wild, `null` = ignore |
| `saddled` | `bool?` | `true` = saddle is equipped (vanilla `Sadle` or MountUp `Mountable`), `false` = no saddle, `null` = ignore |
| `ridden` | `bool?` | `true` = a player is actively mounted on the creature right now, `false` = saddled but not ridden, `null` = ignore |

### Effects

| Field | Type | Valid Range | Description |
|-------|------|-------------|-------------|
| `healthMultiplier` | `float?` | `0.01` – `100` | Multiplies the creature's max health. Original health is restored before each re-evaluation, then this is re-applied |
| `damageMultiplier` | `float?` | `0.01` – `100` | Multiplies outgoing damage. Applied attacker-side when the creature hits something |
| `movementSpeedMultiplier` | `float?` | `0.01` – `100` | Multiplies walk, run, swim, and base movement speed. All four values are independently scaled |
| `disableAI` | `bool?` | — | `true` = disable AI on this creature when conditions are met (e.g. when saddled but not ridden). AI is re-enabled automatically when conditions no longer apply or when the creature is mounted |

`null` or omitted = no change for that stat. Values of `0` or `1.0` are also treated as no-op.

### Stacking

Multiple matching rules **stack multiplicatively**. If two rules both match a creature and each has `healthMultiplier: 1.5`, the result is `1.5 × 1.5 = 2.25×` health.

Original stats are captured on first match and always restored before the combined multiplier is re-applied, preventing drift across multiple evaluations.

### Network ownership

Runtime modifiers only apply on the ZDO **owner** client (the server on dedicated servers, or the local player who owns the creature in peer-to-peer). Damage multipliers are applied attacker-side, so the attacking client must have the mod.

### Real-world examples

**Tamed 1-star bear gains 50% health and 20% damage:**
```json
{
  "enabled": true,
  "targetPrefab": "Bjorn",
  "conditions": { "starLevel": 2, "tamed": true },
  "effects": { "healthMultiplier": 1.5, "damageMultiplier": 1.2 }
}
```

**Saddled Lox moves 20% slower (rider weight penalty):**
```json
{
  "enabled": true,
  "targetPrefab": "Lox",
  "conditions": { "tamed": true, "saddled": true },
  "effects": { "movementSpeedMultiplier": 0.8 }
}
```

**All wild wolves deal 30% more damage:**
```json
{
  "enabled": true,
  "targetPrefab": "Wolf",
  "conditions": { "tamed": false },
  "effects": { "damageMultiplier": 1.3 }
}
```

**2-star event creatures are significantly tougher:**
```json
{
  "enabled": true,
  "targetPrefab": "Wolf",
  "conditions": { "starLevel": 3 },
  "effects": { "healthMultiplier": 2.0, "damageMultiplier": 1.5 }
}
```

**Saddled (but not ridden) creature has AI disabled — acts as a statue until mounted:**
```json
{
  "enabled": true,
  "targetPrefab": "Lox",
  "conditions": { "tamed": true, "saddled": true, "ridden": false },
  "effects": { "disableAI": true }
}
```
When a player mounts this Lox, `ridden` becomes `true`, the condition no longer matches, and AI is automatically restored so the creature responds to steering.

**Stacking example — all tamed wolves on a server get progressively stronger by star:**
```json
[
  {
    "enabled": true,
    "targetPrefab": "Wolf",
    "conditions": { "tamed": true },
    "effects": { "healthMultiplier": 1.2 }
  },
  {
    "enabled": true,
    "targetPrefab": "Wolf",
    "conditions": { "tamed": true, "starLevel": 2 },
    "effects": { "healthMultiplier": 1.3 }
  },
  {
    "enabled": true,
    "targetPrefab": "Wolf",
    "conditions": { "tamed": true, "starLevel": 3 },
    "effects": { "healthMultiplier": 1.5 }
  }
]
```
A 1-star tamed wolf gets `1.2 × 1.3 = 1.56×` health. A 2-star gets `1.2 × 1.5 = 1.8×` health.

---

## Advanced Modifier Schema (v1.1.0+)

The `advanced` object provides granular creature customization beyond the legacy simple fields. It is fully backwards-compatible — existing configs continue to work unchanged.

### Usage

Add an `advanced` object to any `generatedPrefabs`, `prefabOverrides`, or `runtimeModifiers` entry:

```json
{
  "enabled": true,
  "sourcePrefab": "Wolf",
  "newPrefab": "Wolf_dire",
  "advanced": {
    "health": { "maxHealth": 1.75 },
    "damage": { "multiplier": 1.25, "fire": 1.5, "poison": 0.5 },
    "movementSpeed": { "walk": 1.1, "run": 1.25 },
    "ai": {
      "monsterAI": {
        "enabled": true,
        "aggravatable": true,
        "fleeIfNotAlerted": false
      }
    },
    "dropsAndDeath": {
      "deathEffect": {
        "mode": "copyFrom",
        "copyFrom": "Wolf",
        "scaleMultiplier": 1.2
      }
    }
  }
}
```

### Field Tiers

| Tier | Status | Fields |
|------|--------|--------|
| **Tier 1** | ✅ Implemented | `health.multiplier`, `health.maxHealth`, `damage.*` (all types), `movementSpeed.*` (all types), `ai.monsterAI.enabled`, `ai.monsterAI.aggravatable`, `ai.monsterAI.fleeIfNotAlerted`, `ai.monsterAI.fleeInLava`, `ai.monsterAI.fleeRange`, `ai.monsterAI.friendAttacked`, `dropsAndDeath.deathEffect.mode/copyFrom/clearExisting/scaleMultiplier` |
| **Tier 1 Beta** | ⚠️ Runtime-only limitations | `advanced.damage.*` per-type at runtime (use `damageMultiplier` broad for now) |
| **Tier 2** | 📝 Schema + No-op | `defense.*`, `ai.monsterAI.viewRange/viewAngle/hearRange/alertRange/consume*`, `combat.attackRangeMultiplier/turnSpeedMultiplier`, `interaction.hoverTextOffset/useRangeMultiplier` |
| **Tier 3** | 🔒 Audit Required | `health.healthRegenMultiplier`, `ai.baseAI/animalAI`, `ai.disable/enable.baseAI/animalAI`, `ai.monsterAI.stoppingDistanceMultiplier`, `combat.attackHitboxScale/attackOriginOffset/attackHeightOffset/projectileSpawnOffset`, `physics.*`, `interaction.saddlePositionOffset/mountPointOffset`, `dropsAndDeath.deathEffect.prefab/dropTableScaleAware`, `transform.*` |

### Priority Rules

When both legacy and advanced fields are present:

- **Health**: `advanced.health.maxHealth` > `advanced.health.multiplier` > `healthMultiplier`
- **Damage**: Per-type fields (`advanced.damage.fire`) > `advanced.damage.multiplier` > `damageMultiplier`
- **Movement Speed**: Per-type fields (`advanced.movementSpeed.walk`) > `advanced.movementSpeed.multiplier` > `movementSpeedMultiplier`
- **AI**: `advanced.ai.monsterAI.enabled` explicit value wins over legacy `disableAI`
- **Death Effects**: `advanced.dropsAndDeath.deathEffect.*` wins over legacy `clearDeathEffects`/`copyDeathEffectsFrom`

### Death Effect Modes

- `vanilla` — Keep existing death effects (default)
- `none` — Clear all death effects
- `copyFrom` — Copy death effects from another prefab (requires `copyFrom` field)
- `customPrefab` — ⚠️ Tier 3 (not implemented, will log warning)

### Full Example

```json
{
  "version": "1.1.0",
  "schemaVersion": 2,
  "generatedPrefabs": [
    {
      "enabled": true,
      "sourcePrefab": "Wolf",
      "newPrefab": "Wolf_red_dire",
      "displayName": "Red Dire Wolf",
      "scale": 1.35,
      "advanced": {
        "health": { "maxHealth": 1.75 },
        "damage": {
          "multiplier": 1.25,
          "fire": 1.5,
          "poison": 0.5,
          "blunt": 1.1,
          "slash": 1.3
        },
        "movementSpeed": {
          "walk": 1.1,
          "run": 1.25,
          "swim": 0.9
        },
        "ai": {
          "monsterAI": {
            "enabled": true,
            "aggravatable": true,
            "fleeIfNotAlerted": false,
            "fleeInLava": false,
            "fleeRange": 0,
            "friendAttacked": true
          }
        },
        "dropsAndDeath": {
          "deathEffect": {
            "mode": "copyFrom",
            "copyFrom": "Wolf",
            "clearExisting": true,
            "scaleMultiplier": 1.35
          }
        }
      }
    }
  ]
}
```

### Runtime Modifier Example

```json
{
  "enabled": true,
  "targetPrefab": "Wolf_red_dire",
  "conditions": { "tamed": true, "saddled": true, "ridden": true },
  "effects": {
    "advanced": {
      "movementSpeed": { "walk": 1.15, "run": 1.3 },
      "ai": { "enable": { "monsterAI": true } }
    }
  }
}
```

### Unsupported Fields

Tier 2 and Tier 3 fields are recognized by the schema but will log a warning and have no effect. These require additional testing, safety gates, or implementation work before they can be safely enabled.

---

## AllTameable Integration

Use the generated prefab's `newPrefab` name in AllTameable's `specificOffspring` field to make tamed creatures spawn your custom cub instead of a vanilla offspring.

### Example AllTameable entry

```text
Bjorn,true,2400,300,3,15,30,20,Honey:FishRaw:RawMeat,true,true,8,0.33,300,6000,specificOffspring=Bjorn(Bjorn_cub:100),offspringName=Bjorn Cub
```

**Key points:**
- `specificOffspring=Bjorn(Bjorn_cub:100)` — tells AllTameable to spawn the `Bjorn_cub` prefab with 100% probability. This is the `newPrefab` value from your JSON config.
- `offspringName=Bjorn Cub` — cosmetic display name shown in AllTameable UI. This does **not** create a prefab and does **not** need to match any config value.
- Both the `newPrefab` name and `specificOffspring` value must be an **exact match** (case-sensitive).
- The generated prefab must be registered before AllTameable tries to spawn it. CreaturePrefabCreator hooks into `PrefabManager.OnVanillaPrefabsAvailable` — the same event AllTameable uses — so load order matters. If AllTameable loads first, add CreaturePrefabCreator to your load order before it.

### Wolf cub example

**creaturePrefabCreator.json:**
```json
{
  "generatedPrefabs": [
    {
      "enabled": true,
      "sourcePrefab": "Wolf",
      "newPrefab": "Wolf_cub",
      "adultPrefab": "Wolf",
      "displayName": "Wolf Cub",
      "scale": 0.4,
      "growIntoAdult": true,
      "growTimeSeconds": 4800,
      "clearDeathEffects": true,
      "copyDeathEffectsFrom": "Wolf",
      "deathEffectScaleMultiplier": 0.4,
      "preserveTamed": true,
      "preserveLevel": true,
      "preserveOwner": true,
      "preserveName": true
    }
  ]
}
```

**AllTameable_TameList entry:**
```text
Wolf,true,1800,180,2,10,20,15,RawMeat:NeckTail,true,true,5,0.5,180,4800,specificOffspring=Wolf(Wolf_cub:100),offspringName=Wolf Pup
```

---

## Debug Commands

Enable `RegisterConsoleCommands = true` in the `.cfg` file, then open the in-game console (F5). Full reference: `Docs/debug-commands.md`.

### Primary commands

| Command | Description |
|---------|-------------|
| `cpc_help [--command <name>]` | List commands or show detailed usage for one command |
| `cpc_status [--verbose] [--mods] [--debug-runtime] [--generated]` | Plugin, config, feature-gate, and mod-detection summary |
| `cpc_spawn --prefab <name> [--count <n>] [--level <n>] [--tamed] [--distance <m>]` | Spawn any ZNetScene prefab in front of the player |
| `cpc_print_console <live\|prefab\|world-zdos> [flags]` | Read-only diagnostics to the in-game console |
| `cpc_dump_json <live\|prefab\|world-zdos> [flags] [--output <file>]` | Same diagnostics written as JSON to the dumps folder |
| `cpc_repair_world <action> [--dry-run\|--confirm] [--verbose]` | Mutating world-repair operations — safe preview with `--dry-run` |
| `cpc_reload_config [--dry-run] [--prefabs-only] [--debug-runtime-only] [--force]` | Re-read `creaturePrefabCreator.json` from disk and re-apply |

### cpc_print_console / cpc_dump_json modes

Both commands share the same modes and flags. `cpc_print_console` writes to console; `cpc_dump_json` writes JSON to `BepInEx/config/CreaturePrefabCreator/dumps/`.

**`live`** — inspect spawned creatures:
```
cpc_print_console live --target [--ai] [--debug-runtime] [--debug-mountup] [--debug-alltameable] [--zdo] [--generated] [--verbose]
cpc_print_console live <radius>  [--ai] [--debug-runtime] [--verbose]
```

**`prefab`** — inspect prefab templates:
```
cpc_print_console prefab --name <name> [--chain] [--generated] [--overrides]
cpc_print_console prefab --find <partial>
cpc_print_console prefab --compare <a> <b>
cpc_print_console prefab --list-generated
cpc_print_console prefab --verify-generated [--leaks] [--verbose]
```

**`world-zdos`** — list all live ZDOs for a prefab:
```
cpc_print_console world-zdos <prefab> [--verbose]
```

### cpc_repair_world actions

| Action | Description |
|--------|-------------|
| `--cleanup-zdos <prefab>` | Destroy all ZDOs for the named prefab |
| `--orphans` | Destroy ZDOs with no matching registered prefab |
| `--restore-runtime` | Re-enable AI on CPC-runtime-disabled creatures |
| `--force-grow` | Force nearby `OffspringGrowup` components to grow immediately |

Destructive operations default to `--dry-run`. Pass `--confirm` to apply.

### cpc_reload_config flags

| Flag | Behaviour |
|------|-----------|
| `--dry-run` | Validate config parse only — no changes applied |
| `--prefabs-only` | Re-apply prefab overrides and generated prefabs; skip runtime modifiers |
| `--debug-runtime-only` | Reinitialize runtime modifiers only |
| `--force` | Skip safety checks |

AI state changes (`disableAI`, `disableAggro`, `disableFleeing`) propagate to already-spawned live instances. Scale, tint, glow, and faction changes affect new spawns only.

### Deprecated commands

Old per-topic commands (`cpc_runtime_status`, `cpc_ai_state`, `cpc_dump_ai_nearby`, etc.) are still registered as stubs that print the new equivalent. They will be removed in a future release. See `Docs/debug-commands.md` for the full deprecation table.

---

## Multiplayer & Dedicated Servers

- Install the mod on **both the server and all clients**. Missing the mod on any machine will cause ZNetScene to be unable to resolve generated prefab hashes, causing sync errors and invisible creatures.
- Growth logic (`OffspringGrowup.FixedUpdate`) runs only on the ZDO **owner**. On a dedicated server this is always the server. The `GrowTriggered` ZDO flag prevents double-spawning if ownership transfers.
- Runtime modifiers evaluate only on the ZDO owner. Damage multipliers are applied attacker-side, so the attacker's client needs the mod for damage rules to take effect.
- The `creaturePrefabCreator.json` must be **identical** on server and all clients. Use a file distribution mod or include the config in your modpack.
- Config Sync (`EnableConfigSync`) is experimental and not fully implemented. Do not rely on it for production use.

---

## Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `Source prefab not found` in log | Creature mod not loaded before CPC | Ensure the mod providing the source prefab loads before CreaturePrefabCreator in BepInEx load order |
| Baby not spawning via AllTameable | `specificOffspring` name mismatch | The value after `(` must exactly match `newPrefab` in your JSON, case-sensitive |
| Baby spawns but immediately disappears | Template `activeSelf` issue or missing ZNetView | Check log for `CRITICAL:` messages; run `cpc_debug_prefab <name>` |
| Growth never triggers | `growTimeSeconds` > 0 and `growIntoAdult = true` not set, or world time not advancing | Verify config; use `cpc_force_grow_nearby` to test manually |
| Duplicate adults spawning | Multiple clients triggered growth | Ensure all players have the mod installed so `GrowTriggered` ZDO flag is respected |
| Scale looks wrong | Override and generated config both apply a multiplier | Scale is `originalVanillaScale × config.scale × inheritedMultiplier`. Check `cpc_debug_prefab` and verbose log output |
| Runtime modifier not applying | `EnableRuntimeModifiers = false` | Set `EnableRuntimeModifiers = true` in `[FeatureSafety]` |
| Runtime modifier not matching | `starLevel` value wrong | `starLevel: 1` = no stars, `2` = 1-star, `3` = 2-star. Check with `cpc_runtime_check <PrefabName>` |
| Faction override not applying | `EnableFactionOverrides = false` | Set `EnableFactionOverrides = true` in `[FeatureSafety]` |
| Malformed JSON crashes config load | JSON syntax error | A timestamped backup is auto-created in the config folder. Fix the JSON and reload |
| Creature visually wrong size but no error | Config reload changed scale but existing instances weren't updated | Respawn or use `cpc_reload_config`; scale changes affect future spawns only |

---

## Building from Source

1. Copy required Valheim and BepInEx DLLs into `libs/` — see `libs/README.txt` for the full list
2. Run `dotnet build` or use `build.ps1`
3. Output: `bin/Debug/CreaturePrefabCreator.dll`
4. Copy to `BepInEx/plugins/` or use `dev-profile.ps1` to deploy to the local dev profile automatically

---

## License

MIT License — ClickCS
