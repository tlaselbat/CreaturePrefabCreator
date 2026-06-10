# Stat Override Support (Health & Damage Multipliers)

Add optional `healthMultiplier` and `damageMultiplier` to both config types, applied at prefab-setup time before Jötunn registration, with priority/inheritance matching the existing scale system.

---

## Files to Change

| File | Change |
|---|---|
| `Config/GeneratedPrefabConfig.cs` | Add `HealthMultiplier`, `DamageMultiplier` nullable float fields |
| `Config/PrefabOverrideConfig.cs` | Same two fields |
| `GeneratedPrefabs/GeneratedPrefabManager.cs` | Inherited stat dictionaries, `ApplyStatOverrides()`, `CloneSharedData()`, call site after step 7b |
| `Overrides/PrefabOverrideManager.cs` | `GetDirectOverrideStatMultipliers()`, call `ApplyStatOverrides()` after scale, propagate when `propagateToGeneratedVariants=true` |

No new files. No new Harmony patches.

---

## Step 1 — Config schema

Add to both `GeneratedPrefabConfig` and `PrefabOverrideConfig`. Null/absent = no-op, fully backward-compatible:

```csharp
[DataMember(Name = "healthMultiplier", IsRequired = false)]
public float? HealthMultiplier { get; set; } = null;

[DataMember(Name = "damageMultiplier", IsRequired = false)]
public float? DamageMultiplier { get; set; } = null;
```

`IsValid()` unchanged — bad values are warned and skipped at application time only.

---

## Step 2 — Inherited stat multiplier infrastructure (`GeneratedPrefabManager`)

Mirror the existing `InheritedMultipliers` (scale) pattern:

```csharp
private static readonly Dictionary<string, float> InheritedHealthMultipliers = ...;
private static readonly Dictionary<string, float> InheritedDamageMultipliers = ...;

public static void RegisterInheritedStatMultipliers(string sourcePrefabName, float? health, float? damage)
// Only store values that pass IsValidMultiplier() and are not identity (1.0)

public static (float? health, float? damage) GetInheritedStatMultipliers(string sourcePrefabName)
```

---

## Step 3 — Effective multiplier resolution

In `GenerateConfiguredPrefab`, after step 7 (scale), before step 8 (growth):

```csharp
// 7c. Apply stat multipliers (health/damage) before registration
var directStats = PrefabOverrideManager.GetDirectOverrideStatMultipliers(config.NewPrefab);
var inheritedStats = GetInheritedStatMultipliers(config.SourcePrefab);
float? effectiveHealthMult = directStats.health ?? config.HealthMultiplier ?? inheritedStats.health;
float? effectiveDamageMult = directStats.damage ?? config.DamageMultiplier ?? inheritedStats.damage;
ApplyStatOverrides(setupClone, config.NewPrefab, effectiveHealthMult, effectiveDamageMult);
```

Priority: direct override > generated config > inherited from source override.

`PrefabOverrideManager.GetDirectOverrideStatMultipliers(string prefabName)` mirrors `GetDirectOverrideScale` — returns `(float? health, float? damage)` from the first matching enabled override.

---

## Step 4 — Multiplier validation

Applied inside `ApplyStatOverrides` separately per multiplier before any mutation:

| Value | Action |
|---|---|
| `null` / `0` / `1.0` | Skip (no-op) |
| `< 0.01` or `> 100` | LogWarning + skip |
| `> 10` | LogWarning + apply |
| `0.01–10` | Apply |

Never throw. Never break prefab registration.

---

## Step 5 — `ApplyStatOverrides` — health block

```csharp
internal static void ApplyStatOverrides(GameObject prefab, string prefabName, float? healthMult, float? damageMult)
```

- Get `Character`; if null → debug log "no Character, skipping health" and proceed to damage
- Validate `healthMult`; if invalid/skip → no-op
- Read `character.m_health` (original)
- Set `character.m_health = original * healthMult`
- Log: prefab name, original, multiplier, final

---

## Step 6 — `ApplyStatOverrides` — damage block

- Get `Humanoid`; if null → debug log "no Humanoid, skipping damage" and return
- If `humanoid.m_defaultItems` null/empty → debug log "no defaultItems, skipping damage" and return
- Validate `damageMult`; if invalid/skip → return

For each `GameObject item` in `humanoid.m_defaultItems`:

### 6a — Clone the attack item GameObject

> Attack item prefabs in `m_defaultItems` are shared between all creatures using the same prefab. Mutating `m_itemData.m_shared` in place would affect every creature globally. The attack item `GameObject` is cloned per-creature to fully isolate this prefab's damage override.

```csharp
// Clone the shared attack item GameObject to isolate damage overrides for this creature prefab.
// The clone is parented under CreaturePrefabCreator_PrefabContainer (kept alive, inactive in hierarchy).
// It is NOT registered with ObjectDB or Jötunn — m_defaultItems only needs a live GameObject reference.
GameObject clonedItem = UnityEngine.Object.Instantiate(item);
clonedItem.name = $"{prefabName}_{item.name}_DamageOverride";
clonedItem.transform.SetParent(GetOrCreatePrefabContainer().transform, false);
humanoid.m_defaultItems[i] = clonedItem;
```

Guard: if `Instantiate` returns null → LogWarning + skip this item, leave original reference.

### 6b — Clone SharedData and apply damage

> `SharedData` has many reference-type fields (EffectList, Attack, StatusEffect, icons, etc.) that must not be mutated. `m_damages` / `m_damagesPerLevel` are `HitData.DamageTypes` **structs** — value-copied by a shallow clone. `MemberwiseClone()` is therefore safe and avoids fragile field-by-field copying.

```csharp
private static ItemDrop.ItemData.SharedData CloneSharedData(ItemDrop.ItemData.SharedData original)
{
    // MemberwiseClone is protected on System.Object; invoke via reflection.
    // Shallow clone is safe here: only m_damages (a struct) will be mutated.
    // All reference fields (EffectList, Attack, StatusEffect, etc.) remain intentionally shared.
    if (original == null) return null;
    try
    {
        var method = typeof(object).GetMethod("MemberwiseClone",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return (ItemDrop.ItemData.SharedData)method.Invoke(original, null);
    }
    catch (Exception ex)
    {
        CreaturePrefabCreatorPlugin.Instance?.LogWarning(
            $"SharedData clone failed ({ex.Message}); skipping damage for this attack item.");
        return null;
    }
}
```

- Get `ItemDrop` from cloned item; guard null → LogWarning + skip
- Guard `m_itemData`, `m_shared` → LogWarning + skip
- `CloneSharedData(m_shared)` → if null → already logged, skip
- Multiply all 11 fields on `clonedShared.m_damages`: `m_damage`, `m_blunt`, `m_slash`, `m_pierce`, `m_chop`, `m_pickaxe`, `m_fire`, `m_frost`, `m_lightning`, `m_poison`, `m_spirit`
- `clonedItem.GetComponent<ItemDrop>().m_itemData.m_shared = clonedShared`
- Log per item: prefab name, cloned item name, each non-zero field → original, multiplier, final
- `m_damagesPerLevel` is left unchanged

---

## Step 7 — Override prefab call site (`PrefabOverrideManager`)

After the existing scale block in `ApplyConfiguredOverride`:

```csharp
GeneratedPrefabManager.ApplyStatOverrides(
    targetPrefab, config.TargetPrefab,
    config.HealthMultiplier, config.DamageMultiplier);
```

After the existing `RegisterInheritedMultiplier` call:

```csharp
if (config.PropagateToGeneratedVariants)
    GeneratedPrefabManager.RegisterInheritedStatMultipliers(
        config.TargetPrefab, config.HealthMultiplier, config.DamageMultiplier);
```

---

## What must not change

- `IsValid()` contracts, scale logic, ragdoll/death effect scaling, procreation stripping, ZNetScene registration, network sync
- No `staminaMultiplier`
- No Harmony patches on `Character`, `Humanoid`, `MonsterAI`, `AnimalAI`, `HitData`
- No ObjectDB / Jötunn registration of cloned attack items (revisit only if resolution fails in testing)
- Existing configs continue working exactly as before

---

## Estimated scope

~150–170 lines across 4 files. No new files. No breaking changes.
