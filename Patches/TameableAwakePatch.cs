using CreaturePrefabCreator.GeneratedPrefabs;
using HarmonyLib;

namespace CreaturePrefabCreator.Patches
{
    /// <summary>
    /// Patches Tameable.Awake postfix to re-apply CPC-preserved tamed state.
    ///
    /// Problem: AllTameable's PetManager.AddTame (called from MonsterAI.Awake prefix) calls
    /// Object.DestroyImmediate on the existing Tameable and adds a fresh one with m_startsTamed=false.
    /// This fires synchronously — no amount of frame deferral can write 'tamed' to the ZDO before
    /// AllTameable resets it.
    ///
    /// Solution: CPC writes its tamed intent to the ZDO key CPC_PreserveTamed (never touched by
    /// AllTameable). This patch reads that key in Tameable.Awake postfix and calls Character.SetTamed,
    /// which runs AFTER AllTameable has finished configuring the new Tameable instance.
    /// </summary>
    [HarmonyPatch(typeof(Tameable), "Awake")]
    [HarmonyAfter("meldurson.valheim.AllTameable")]
    public static class TameableAwakePatch
    {
        static void Postfix(Tameable __instance) => TameableInitHandler.Apply(__instance);
    }

    internal static class TameableInitHandler
    {
        internal static void Apply(Tameable __instance)
        {
            if (__instance == null) return;

            var znv = __instance.GetComponent<ZNetView>();
            if (znv == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[TameableAwakePatch] SKIP '{__instance.gameObject.name}' — no ZNetView.");
                return;
            }

            // Do NOT check znv.IsValid() here — ZNetView may not be registered yet during Awake.
            var zdo = znv.GetZDO();
            if (zdo == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[TameableAwakePatch] SKIP '{__instance.gameObject.name}' — ZNetView has no ZDO.");
                return;
            }

            var character = __instance.GetComponent<Character>();
            if (character == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[TameableAwakePatch] SKIP '{__instance.gameObject.name}' — no Character component.");
                return;
            }

            bool preserveRequested = zdo.GetBool(OffspringGrowup.PreserveTamedHash, false);
            bool currentlyTamed = character.IsTamed();

            CreaturePrefabCreatorPlugin.Instance?.Log(
                $"[TameableAwakePatch] FIRE on '{__instance.gameObject.name}': CPC_PreserveTamed={preserveRequested}, IsTamed={currentlyTamed}");

            if (preserveRequested && !currentlyTamed)
            {
                // CPC_PreserveTamed was set (e.g. from a prior growup transfer or deferred spawn).
                // Re-apply tamed after AllTameable finished recreating this Tameable.
                character.SetTamed(true);
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[TameableAwakePatch] Restored tamed state on '{__instance.gameObject.name}' from CPC_PreserveTamed. IsTamed after={character.IsTamed()}");
            }
            else if (currentlyTamed)
            {
                // Creature already woke up tamed (e.g. via m_startsTamed=true).
                // Write CPC_PreserveTamed so OffspringGrowup can carry it through to the adult.
                if (!preserveRequested)
                {
                    zdo.Set(OffspringGrowup.PreserveTamedHash, true);
                    CreaturePrefabCreatorPlugin.Instance?.Log(
                        $"[TameableAwakePatch] Wrote CPC_PreserveTamed on tamed '{__instance.gameObject.name}' for growup persistence.");
                }
            }
            else
            {
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[TameableAwakePatch] No action on '{__instance.gameObject.name}': CPC_PreserveTamed={preserveRequested}, IsTamed={currentlyTamed}");
            }
        }
    }

    /// <summary>
    /// Postfix on MonsterAI.Awake to run AFTER AllTameable's prefix has finished setting up the creature.
    /// This is the safest point to apply tamed state for a freshly spawned creature because:
    ///   1. AllTameable's MonsterAI.Awake prefix has already called AddTame (which recreates Tameable).
    ///   2. The new Tameable.Awake has already fired (triggered by AddComponent inside AllTameable prefix).
    ///   3. CPC_PreserveTamed may or may not be set yet (it's set by DeferredSetTamed 2 frames later).
    /// We log here to confirm AllTameable has finished and to check if the creature is already tamed.
    /// </summary>
    [HarmonyPatch(typeof(MonsterAI), "Awake")]
    [HarmonyAfter("meldurson.valheim.AllTameable")]
    public static class MonsterAIAwakePostPatch
    {
        static void Postfix(MonsterAI __instance)
        {
            if (__instance == null) return;
            var znv = __instance.GetComponent<ZNetView>();
            if (znv == null) return;
            var zdo = znv.GetZDO();
            if (zdo == null) return;
            var character = __instance.GetComponent<Character>();
            if (character == null) return;

            bool preserveRequested = zdo.GetBool(OffspringGrowup.PreserveTamedHash, false);
            bool isTamed = character.IsTamed();

            if (preserveRequested || isTamed || __instance.gameObject.name.Contains("Bjorn"))
            {
                CreaturePrefabCreatorPlugin.Instance?.Log(
                    $"[MonsterAIAwakePost] '{__instance.gameObject.name}': CPC_PreserveTamed={preserveRequested}, IsTamed={isTamed} — AllTameable setup complete.");

                if (preserveRequested && !isTamed)
                {
                    character.SetTamed(true);
                    CreaturePrefabCreatorPlugin.Instance?.Log(
                        $"[MonsterAIAwakePost] Applied SetTamed(true) on '{__instance.gameObject.name}'. IsTamed after={character.IsTamed()}");
                }
            }
        }
    }
}
