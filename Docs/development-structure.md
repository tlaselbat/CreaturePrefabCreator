# Development Structure

## Repository Separation Rule

The `CreaturePrefabCreator` plugin repo contains **only**:

- Our plugin source code (`Config/`, `Debug/`, `GeneratedPrefabs/`, `Network/`, `Overrides/`, `Patches/`, `RuntimeModifiers/`, `Compatibility/`, `Utilities/`)
- Build scripts (`build.ps1`, `dev-profile.ps1`)
- Project configs (`CreaturePrefabCreator.csproj`, `.vscode/`, `.windsurf/`, `.devin/`)
- Compile-time reference DLLs (`libs/`) — Valheim, Unity, BepInEx, Jotunn only
- Dev environment profile (`ValheimDevProfile/`)
- Docs (`Docs/`)
- Tests (`Tests/`)
- Root documentation (`README.md`, etc.)

---

## External Plugin Research Rule

**Do NOT put external plugin source, decompiled code, or research blobs inside the plugin repo's `Compatibility/` folder.**

The `Compatibility/` folder contains **our compatibility implementation code only** — C# classes that soft-detect and interop with external plugins at runtime.

Use the sibling repository for all external plugin material:

```
C:\Users\16265\DevelopmentProjects\ValheimModding\ExternalPlugins\
```

Use `ExternalPlugins/` for:

- External plugin source code
- External plugin DLLs (reference copies, not compile deps)
- Decompiled code / blob dumps
- AI development notes
- Compatibility maps
- Public API notes
- ZDO key reference tables
- Patch maps
- Config maps

---

## Folder Map

```
CreaturePrefabCreator\
├── Compatibility\          <- Our compat code only (C# interop/reflection/safety)
│   ├── AllTameable\
│   ├── MountUpRestored\
│   └── Jotunn\
├── Config\                 <- BepInEx config bindings
├── Debug\                  <- Debug console commands
├── Docs\                   <- This folder. Developer documentation.
├── GeneratedPrefabs\       <- Prefab creation + offspring logic
├── Network\                <- Config sync
├── Overrides\              <- Prefab override management
├── Patches\                <- Harmony patches
├── RuntimeModifiers\       <- Runtime modifier system
├── Tests\                  <- Test configs and scenario scripts
├── Utilities\              <- Shared utility code
├── libs\                   <- Compile-time DLL references only
└── ValheimDevProfile\      <- Dev game profile (excluded from publishing)
```

```
ExternalPlugins\
├── Source\                 <- External plugin source code
├── Decompiled\             <- Decompiled blobs / IL dumps
├── DLLs\                   <- Reference copies of external plugin DLLs
└── Notes\                  <- AI notes, API maps, ZDO keys, patch maps
```

---

## libs/ Allowed Contents

Only compile-time references:

- `BepInEx.dll`, `BepInEx.Harmony.dll`, `BepInEx.Preloader.dll`
- `0Harmony.dll`, `0Harmony20.dll`, `HarmonyXInterop.dll`
- `Jotunn.dll`
- `assembly_valheim.dll`, `assembly_utils.dll`, other `assembly_*.dll`
- `UnityEngine*.dll`, `Unity.*.dll`
- `System*.dll`, `mscorlib.dll`, `netstandard.dll`
- `Mono*.dll`, `MonoMod*.dll`
- `PlayFab*.dll`, `Splatform*.dll`, `Steamworks-related DLLs`
- `gui_framework.dll`, `SoftReferenceableAssets.dll`

**Not allowed in libs/**:

- External plugin DLLs (AllTameable, MountUpRestored, etc.)
- Decompiled blobs
- YAML development notes
- Source code research files
