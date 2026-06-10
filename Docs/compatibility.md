# Compatibility Notes

## AllTameable_TamingOverhaul

- **GUID**: `(fill in)`
- **Soft dependency**: yes — detected via `BepInEx.Bootstrap.Chainloader.PluginInfos`
- **Integration method**: Reflection / method invocation at runtime
- **Key interactions**: taming system, creature behavior AI, tamed creature config
- **Patch files**: `Patches/` (check for AllTameable-specific patches)
- **Compat code**: `Compatibility/AllTameable/`
- **Reference DLL**: `ExternalPlugins/DLLs/AllTameable_TamingOverhaul/AllTameable_TamingOverhaul.dll`
- **AI notes**: `ExternalPlugins/Notes/AllTameable_TamingOverhaul/alltameable_tamingoverhaul_ai_development_notes.yaml`

### Known Risks

- (document risks here)

---

## MountUpRestored

- **GUID**: `(fill in)`
- **Soft dependency**: yes
- **Integration method**: Reflection / saddle bridge
- **Key interactions**: saddle system, mounted creature movement, AI safety during mount
- **Patch files**: `Patches/MountUpCompatibilityPatch.cs`, `Patches/SaddledCreaturePatch.cs`
- **Compat code**: `Compatibility/MountUpRestored/`
- **Reference DLL**: `ExternalPlugins/DLLs/MountUpRestored/` (if obtained)
- **AI notes**: `ExternalPlugins/Notes/MountUpRestored/mountup_restored_ai_development_notes.yaml`

### Known Risks

- (document risks here)

---

## Jotunn

- **GUID**: `denikson-jotunn`
- **Dependency type**: Hard dependency (listed in BepInEx plugin attribute)
- **Integration method**: Direct API usage
- **Key interactions**: AssetBundle loading, config sync, prefab registration, network packets
- **Compile reference**: `libs/Jotunn.dll`
- **Reference copy**: `ExternalPlugins/DLLs/Jotunn/Jotunn.dll`

### Known Risks

- (document risks here)

---

## Soft Dependency Detection

Use `BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(guid)` to detect loaded plugins at runtime before attempting reflection.

Pattern:

```csharp
public static bool IsAllTameableLoaded =>
    BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(PluginGuids.AllTameable);
```

All optional plugin interactions must be gated behind these checks. Never hard-reference optional plugin types at compile time.

---

## Reflection Safety

- Always wrap reflection calls in `try/catch`
- Cache `MethodInfo`/`FieldInfo` results — do not re-resolve every frame
- Null-check all reflected values before use
- Log warnings (not errors) when optional features are unavailable

---

## Patch Order Risks

- BepInEx loads plugins in dependency order, then alphabetical
- AllTameable and MountUpRestored may apply patches that conflict with ours
- Use `[HarmonyBefore]` / `[HarmonyAfter]` attributes where ordering is critical
- Document all known patch conflicts here

---

## Multiplayer / Server-Client Sync Risks

- Jotunn's `ConfigSync` handles config distribution — do not send configs manually
- ZDO data written by AllTameable must not be overwritten without guards
- Check `ZDOMan` keys carefully against `ExternalPlugins/Notes/*/zdo-keys.md`
- Saddle system (MountUp) sets ZDOs on creature prefabs — test in multiplayer

---

## Debug Commands

See `Docs/debug-commands.md` for all available runtime debug commands.

---

## Known Issues

- (document known issues here)
