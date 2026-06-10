# MountUpRestored 3.4.4 Compatibility Fix

Update CreaturePrefabCreator's MountUp reflection to work with MountUpRestored 3.4.4's actual rider-state path: `MountUp.Mountable` → `getSaddle()`/`getSadle()` → saddle `GameObject` → vanilla `Sadle` component → `Sadle.GetRider()`. Also improve delayed-retry logging so failed compatibility initialization is obvious in BepInEx logs.

## Context / Audit Results

- `MountUp.Mountable` does exist in MountUpRestored 3.4.4 and is a public `MonoBehaviour`.
- CPC currently tries to detect MountUp too early and/or too narrowly.
- Current CPC log shows:
  - `AccessTools.TypeByName: Could not find type named MountUp.Mountable` 
  - `MountUp not detected yet; scheduling delayed compatibility retry` 
  - `Riding AI Enable reflection initialized. MountUp NOT detected (vanilla saddles only)` 
- MountUpRestored later loads successfully and registers:
  - `Bjorn` 
  - `Wolf_red_dire` 
  - `Wolf` 
  - `Boar` 
- Therefore the generated prefab and MountUp config are not the main issue. The issue is CPC's MountUp reflection/rider-state detection.

## Actual MountUpRestored Rider-State Path

MountUpRestored does not expose rider state directly on `Mountable` through the APIs CPC was checking.

Remove reliance on these stale/nonexistent Mountable APIs:

- `Mountable.GetRider()` 
- `Mountable.IsMounted` 
- `Mountable.m_isMounted` 
- `Mountable.m_rider` 

Instead, detect active riding through:

1. `character.GetComponent(MountUp.Mountable)` 
2. Call `Mountable.getSaddle()` or `Mountable.getSadle()` via reflection
3. The returned value should be a `UnityEngine.GameObject` 
4. Get vanilla `Sadle` from that GameObject
5. Call `Sadle.GetRider()` 
6. Treat `rider != null` as actively ridden

Important: because there is spelling ambiguity between `Saddle` and Valheim's vanilla `Sadle`, support both reflected method names:
- `getSaddle` 
- `getSadle` 

Log exactly which method was found.

## File 1: `Patches/SaddledCreaturePatch.cs` 

### In `Initialize()` 

Replace the old MountUp rider-state reflection with reflection for the actual MountUpRestored path.

Required fields:

```csharp
private static Type _mountUpMountableType;
private static MethodInfo _mountUpGetSaddleMethod;
private static bool _mountUpReflectionInitialized;
```

Remove stale fields if they exist:

```csharp
_mountUpGetRiderMethod
_mountUpIsMountedProperty
_mountUpIsMountedField
_mountUpRiderField
```

Implementation requirements:

* Find `MountUp.Mountable` across all loaded assemblies.
* Do not rely only on `AccessTools.TypeByName`.
* Prefer the repo's existing `FindTypeAcrossAssemblies` helper if available.
* Find an instance method named either:

  * `getSaddle` 
  * `getSadle` 
* Store the found method in `_mountUpGetSaddleMethod`.
* Log success with the exact path:

```txt
MountUp detected (Mountable.<methodName>() -> Sadle.GetRider())
```

* Log partial failure with a useful breakdown:

```txt
MountUp reflection incomplete: Mountable=<true/false>, getSaddle/getSadle=<true/false>, Sadle.GetRider=<true/false>
```

### In `IsActivelyRidden(Character character)` 

Keep the vanilla `Sadle` check first.

After the vanilla check, add the MountUpRestored reflection path:

```csharp
if (_mountUpMountableType != null && _mountUpGetSaddleMethod != null)
{
    Component mountable = character.GetComponent(_mountUpMountableType);
    if (mountable != null)
    {
        object saddleObj = _mountUpGetSaddleMethod.Invoke(mountable, null);

        if (saddleObj is GameObject saddleGO)
        {
            Sadle sadle = saddleGO.GetComponent<Sadle>();
            if (sadle != null && sadle.GetRider() != null)
            {
                return true;
            }
        }
    }
}
```

Make it null-safe and exception-safe:

* Catch reflection exceptions.
* Log the first failure or debug-log it, but do not spam every frame.
* If reflection fails, return false and let vanilla behavior continue.

Do not make CPC require MountUpRestored as a compile-time dependency.

## File 2: `Patches/MountUpCompatibilityPatch.cs` 

### In `RetryApplyPatches()` 

After each `TryApplyPatches(harmony)` call, log whether the attempt succeeded.

Required logging:

On success:

```txt
MountUp compatibility initialized after delayed retry (attempt X/Y)
```

On retry failure:

```txt
MountUp compatibility retry failed (attempt X/Y): Mountable=<found>, Awake=<patched>, ResetMountPoint=<patched>
```

On final failure:

```txt
MountUp compatibility failed after delayed retries: Mountable=<found>, Awake=<patched>, ResetMountPoint=<patched>
```

Also make sure the retry system does not only log initial failure and final failure. It should log per-attempt success/failure clearly.

## File 3: Verify Type Finder

Audit `FindTypeAcrossAssemblies`.

Requirements:

* It should search all currently loaded assemblies.
* It should match `MountUp.Mountable` by full name.
* It should not fail just because MountUpRestored loads after CPC.
* If type is not found at plugin init, delayed retry must try again after more assemblies are loaded.
* If multiple candidates are found, log assembly name and selected type.

No changes are needed if the existing helper already satisfies this.

## Test Plan

1. Build CPC.
2. Launch Valheim with:

   * CreaturePrefabCreator
   * AllTameable
   * MountUpRestored 3.4.4
3. Confirm logs show MountUpRestored loads and registers:

```txt
List of Mounts: Bjorn, Wolf_red_dire, Wolf, Boar
```

4. Confirm CPC logs one of these:

```txt
MountUp detected (Mountable.getSaddle() -> Sadle.GetRider())
```

or:

```txt
MountUp detected (Mountable.getSadle() -> Sadle.GetRider())
```

5. Spawn a fresh generated prefab:

```txt
spawn Wolf_red_dire 1
```

6. Tame it:

```txt
tame
```

7. Equip the correct saddle and mount it.

8. Confirm BepInEx log shows:

```txt
>>> ENABLING AI (actively ridden, disableAI=true) <<<<
```

9. Dismount and confirm:

```txt
dismounted - disabling AI
```

10. Also test normal `Wolf`:

```txt
spawn Wolf 1
tame
```

Expected result:

* Normal `Wolf` with `disableAI=false` is unaffected.
* No permanent AI suppression marker behavior should apply to normal Wolf.
* MountUp riding should still work normally.

## Acceptance Criteria

* CPC no longer relies on nonexistent Mountable rider APIs.
* CPC detects MountUpRestored 3.4.4 through `MountUp.Mountable`.
* CPC detects active MountUp riding through `Mountable.getSaddle()/getSadle() -> Sadle.GetRider()`.
* `Wolf_red_dire` remains inert while not ridden.
* `Wolf_red_dire` temporarily enables AI while actively ridden.
* AI disables again after dismount.
* Normal creatures with `disableAI=false` are unaffected.
* Delayed compatibility retries clearly log success/failure.
* No new hard dependency on MountUpRestored is introduced.
