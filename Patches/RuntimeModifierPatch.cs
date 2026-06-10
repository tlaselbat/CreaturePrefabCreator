using CreaturePrefabCreator.RuntimeModifiers;
using HarmonyLib;
using UnityEngine;

namespace CreaturePrefabCreator.Patches
{
    /// <summary>
    /// Harmony patches that drive the RuntimeModifierManager lifecycle.
    ///
    /// Health and speed modifiers are re-evaluated whenever creature state changes
    /// (spawn, level change, tame state change, equipment change, saddle attach/detach).
    ///
    /// Damage modifiers are applied attacker-side inside Character.ApplyDamage:
    /// the patch reads the attacker from HitData.GetAttacker() and multiplies
    /// outgoing damage — it does NOT modify damage taken by the victim.
    ///
    /// A periodic re-scan ticker (RuntimeModifierTicker) catches saddle/rider state
    /// changes that are not covered by discrete Harmony events (e.g. ownership transfer,
    /// programmatic saddle addition by AllTameable, rider mount/dismount).
    /// </summary>

    [HarmonyPatch(typeof(Character), "Awake")]
    public static class RuntimeModifier_Character_Awake
    {
        static void Postfix(Character __instance)
        {
            RuntimeModifierManager.EvaluateAndApply(__instance);
        }
    }

    [HarmonyPatch(typeof(Character), "SetLevel")]
    public static class RuntimeModifier_Character_SetLevel
    {
        static void Postfix(Character __instance)
        {
            RuntimeModifierManager.EvaluateAndApply(__instance);
        }
    }

    [HarmonyPatch(typeof(Character), "SetTamed")]
    public static class RuntimeModifier_Character_SetTamed
    {
        static void Postfix(Character __instance)
        {
            RuntimeModifierManager.EvaluateAndApply(__instance);
        }
    }

    [HarmonyPatch(typeof(Humanoid), "EquipItem")]
    public static class RuntimeModifier_Humanoid_EquipItem
    {
        static void Postfix(Humanoid __instance)
        {
            RuntimeModifierManager.EvaluateAndApply(__instance as Character);
        }
    }

    [HarmonyPatch(typeof(Humanoid), "UnequipItem")]
    public static class RuntimeModifier_Humanoid_UnequipItem
    {
        static void Postfix(Humanoid __instance)
        {
            RuntimeModifierManager.EvaluateAndApply(__instance as Character);
        }
    }

    /// <summary>
    /// Fires when a saddle component is attached to a creature.
    /// This is the primary trigger for saddled=true conditions.
    /// </summary>
    [HarmonyPatch(typeof(Sadle), "Awake")]
    public static class RuntimeModifier_Sadle_Awake
    {
        static void Postfix(Sadle __instance)
        {
            if (__instance == null) return;
            var character = __instance.GetComponentInParent<Character>();
            if (character != null)
                RuntimeModifierManager.EvaluateAndApply(character);
        }
    }

    /// <summary>
    /// Fires when a saddle component is destroyed (saddle removed from creature).
    /// Triggers re-evaluation so saddled=true conditions are cleared.
    /// Applied manually so a missing Sadle.OnDestroy method does not abort plugin init.
    /// </summary>
    public static class RuntimeModifier_Sadle_OnDestroy
    {
        public static void TryApply(Harmony harmony)
        {
            var target = AccessTools.Method(typeof(Sadle), "OnDestroy");
            if (target == null) return;
            var postfix = new HarmonyMethod(typeof(RuntimeModifier_Sadle_OnDestroy), nameof(Postfix));
            harmony.Patch(target, postfix: postfix);
        }

        static void Postfix(Sadle __instance)
        {
            if (__instance == null) return;
            var character = __instance.GetComponentInParent<Character>();
            if (character != null)
                RuntimeModifierManager.EvaluateAndApply(character);
        }
    }

    [HarmonyPatch(typeof(Character), "OnDestroy")]
    public static class RuntimeModifier_Character_OnDestroy
    {
        static void Postfix(Character __instance)
        {
            RuntimeModifierManager.Cleanup(__instance);
        }
    }

    /// <summary>
    /// Intercepts damage being applied to a victim character.
    /// Looks up the ATTACKER from HitData and applies that attacker's
    /// outgoing damage multiplier. The victim's stats are never changed here.
    /// </summary>
    [HarmonyPatch(typeof(Character), "ApplyDamage")]
    public static class RuntimeModifier_Character_ApplyDamage
    {
        static void Prefix(HitData hit)
        {
            if (hit == null) return;

            Character attacker = hit.GetAttacker();
            if (attacker == null) return;

            float mult = RuntimeModifierManager.GetOutgoingDamageMultiplier(attacker);
            if (Mathf.Approximately(mult, 1f)) return;

            hit.ApplyModifier(mult);

            if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                CreaturePrefabCreatorPlugin.Instance.Log(
                    $"[RuntimeModifier] Outgoing damage ×{mult} applied for attacker '{attacker.gameObject.name}'.");
        }
    }

    /// <summary>
    /// Periodic re-scan ticker. Catches ridden/saddle state changes that are not
    /// covered by discrete Harmony events: ownership transfers, MountUp rider changes,
    /// programmatic saddle additions by AllTameable, and mount/dismount transitions
    /// that do not fire a patched method on the creature itself.
    ///
    /// Attached to the plugin GameObject in Awake when RuntimeModifiers are enabled.
    /// Scans only owned, active Character instances that have a matching runtime rule.
    /// </summary>
    public class RuntimeModifierTicker : MonoBehaviour
    {
        private const float TickInterval = 5f;
        private float _lastTick;

        void Update()
        {
            var plugin = CreaturePrefabCreatorPlugin.Instance;
            if (plugin == null) return;
            if (plugin.ConfigEnableRuntimeModifiers?.Value != true) return;

            float now = Time.time;
            if (now - _lastTick < TickInterval) return;
            _lastTick = now;

            var characters = Object.FindObjectsByType<Character>(FindObjectsSortMode.None);
            foreach (var character in characters)
            {
                if (character == null) continue;
                var nview = character.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid() || !nview.IsOwner()) continue;

                RuntimeModifierManager.EvaluateAndApply(character);
            }
        }
    }
}
