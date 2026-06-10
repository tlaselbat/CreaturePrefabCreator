using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;

namespace CreaturePrefabCreator.Patches
{
    /// <summary>
    /// Applies visual overrides (tint/glow) at runtime via MaterialPropertyBlock.
    /// Prevents mutation of shared materials on prefabs.
    /// </summary>
    public static class VisualOverridePatch
    {
        private static readonly Dictionary<string, Color> TintRegistry = new Dictionary<string, Color>();
        private static readonly Dictionary<string, Color> GlowRegistry = new Dictionary<string, Color>();

        public static void RegisterTint(string prefabName, Color tint)
        {
            if (string.IsNullOrEmpty(prefabName)) return;
            TintRegistry[prefabName] = tint;
        }

        public static void RegisterGlow(string prefabName, Color glow)
        {
            if (string.IsNullOrEmpty(prefabName)) return;
            GlowRegistry[prefabName] = glow;
        }

        [HarmonyPatch(typeof(Character), "Awake")]
        internal static class CharacterAwakePostfix
        {
            static void Postfix(Character __instance)
            {
                if (__instance == null) return;

                // Skip on dedicated server (no rendering)
                if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                    return;

                string prefabBaseName = __instance.name.Replace("(Clone)", "").Trim();
                bool hasTint = TintRegistry.TryGetValue(prefabBaseName, out var tint);
                bool hasGlow = GlowRegistry.TryGetValue(prefabBaseName, out var glow);
                if (!hasTint && !hasGlow) return;

                var renderers = __instance.GetComponentsInChildren<Renderer>(true);
                if (renderers == null || renderers.Length == 0) return;

                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    var propBlock = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(propBlock);

                    if (hasTint)
                    {
                        if (renderer.material.HasProperty("_Color"))
                            propBlock.SetColor("_Color", tint);
                        if (renderer.material.HasProperty("_BaseColor"))
                            propBlock.SetColor("_BaseColor", tint);
                    }

                    if (hasGlow)
                    {
                        if (renderer.material.HasProperty("_EmissionColor"))
                        {
                            propBlock.SetColor("_EmissionColor", glow);
                            renderer.material.EnableKeyword("_EMISSION");
                        }
                    }

                    renderer.SetPropertyBlock(propBlock);
                }

                CreaturePrefabCreatorPlugin.Instance?.Log($"[VisualOverride] Applied runtime visual to '{__instance.name}': tint={hasTint}, glow={hasGlow}");
            }
        }
    }
}