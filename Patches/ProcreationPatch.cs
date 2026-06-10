using HarmonyLib;
using CreaturePrefabCreator.GeneratedPrefabs;

namespace CreaturePrefabCreator.Patches
{
    /// <summary>
    /// Prevents Procreation.Procreate from running on baby/offspring prefabs.
    /// AllTameable may add Procreation at runtime even when procretion=false in config.
    /// Cubs must not breed, and calling Procreate on them can produce NullReferenceExceptions.
    /// This prefix skips Procreate entirely when the creature has an OffspringGrowup component.
    /// </summary>
    [HarmonyPatch(typeof(Procreation), "Procreate")]
    [HarmonyAfter("meldurson.valheim.AllTameable", "meldurson.MountUpRestored")]
    public static class ProcreationPatch
    {
        static bool Prefix(Procreation __instance)
        {
            if (__instance == null) return false;
            if (__instance.GetComponent<OffspringGrowup>() != null)
                return false;
            return true;
        }
    }
}