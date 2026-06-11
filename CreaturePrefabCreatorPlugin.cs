using BepInEx;
using BepInEx.Configuration;
using CreaturePrefabCreator.Config;
using CreaturePrefabCreator.GeneratedPrefabs;
using CreaturePrefabCreator.Overrides;
using CreaturePrefabCreator.Patches;
using CreaturePrefabCreator.RuntimeModifiers;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace CreaturePrefabCreator
{
    [BepInPlugin("com.clickcs.creatureprefabcreator", "CreaturePrefabCreator", "1.0.0")]
    [BepInDependency("com.jotunn.jotunn", BepInDependency.DependencyFlags.HardDependency)]
    public class CreaturePrefabCreatorPlugin : BaseUnityPlugin
    {
        public static CreaturePrefabCreatorPlugin Instance { get; private set; }

        internal ConfigEntry<bool> ConfigEnabled;
        internal ConfigEntry<bool> ConfigVerboseLogging;
        internal ConfigEntry<bool> ConfigRegisterConsoleCommands;
        internal ConfigEntry<bool> ConfigDebugMountState;
        internal ConfigEntry<bool> ConfigDebugAIState;

        // Feature Safety Gates - P0: All high-risk/beta features default to false
        internal ConfigEntry<bool> ConfigEnableGeneratedPrefabs;
        internal ConfigEntry<bool> ConfigEnablePrefabOverrides;
        internal ConfigEntry<bool> ConfigEnableConfigSync;
        internal ConfigEntry<bool> ConfigEnableRuntimeModifiers;
        internal ConfigEntry<bool> ConfigEnableVisualOverrides;
        internal ConfigEntry<bool> ConfigEnableFactionOverrides;
        internal ConfigEntry<bool> ConfigLogBetaFeatureWarnings;
        internal ConfigEntry<bool> ConfigEnableDebugDumpCommands;

        internal CreaturePrefabCreatorConfigRoot LoadedConfig { get; set; }

        private readonly Harmony _harmony = new Harmony("com.clickcs.creatureprefabcreator");

        private void Awake()
        {
            Instance = this;
            _harmony.PatchAll();
            Patches.RuntimeModifier_Sadle_OnDestroy.TryApply(_harmony);
            MountUpCompatibilityPatch.Initialize(_harmony);

            // General settings
            ConfigEnabled = Config.Bind("General", "Enabled", true, "Enable the CreaturePrefabCreator plugin.");
            ConfigVerboseLogging = Config.Bind("General", "VerboseLogging", true, "Enable verbose logging for debugging.");
            ConfigRegisterConsoleCommands = Config.Bind("General", "RegisterConsoleCommands", true, "Register debug console commands.");
            ConfigDebugMountState = Config.Bind("Debug", "DebugMountState", false, "Log mount/rider state transitions for diagnostics.");
            ConfigDebugAIState = Config.Bind("Debug", "DebugAIState", false, "Log AI suppression diagnostics.");
            ConfigEnableDebugDumpCommands = Config.Bind("Debug", "EnableDebugDumpCommands", false,
                "Enable developer-only console commands for dumping creature AI/component values to JSON. Read-only. Disabled by default.");

            // Feature Safety Gates - P0: All risky features default to false until tested
            ConfigEnableGeneratedPrefabs = Config.Bind("FeatureSafety", "EnableGeneratedPrefabs", true,
                "Enable generated prefab system (baby creatures). [Stable]");
            ConfigEnablePrefabOverrides = Config.Bind("FeatureSafety", "EnablePrefabOverrides", true,
                "Enable prefab override system. [Stable]");
            ConfigEnableConfigSync = Config.Bind("FeatureSafety", "EnableConfigSync", false,
                "EXPERIMENTAL: Sync config from server to clients. Not fully implemented. [ExperimentalDisabledByDefault]");
            ConfigEnableRuntimeModifiers = Config.Bind("FeatureSafety", "EnableRuntimeModifiers", false,
                "BETA: Apply dynamic stat multipliers based on creature state. Disabled until stacking caps and multiplayer tests pass. [BetaDisabledByDefault]");
            ConfigEnableVisualOverrides = Config.Bind("FeatureSafety", "EnableVisualOverrides", false,
                "BETA: Apply runtime tint/glow to creatures. Disabled until MaterialPropertyBlock-only implementation is verified. [BetaDisabledByDefault]");
            ConfigEnableFactionOverrides = Config.Bind("FeatureSafety", "EnableFactionOverrides", false,
                "BETA: Override creature factions. Disabled until enum validation is hardened. [BetaDisabledByDefault]");
            ConfigLogBetaFeatureWarnings = Config.Bind("FeatureSafety", "LogBetaFeatureWarnings", true,
                "Log warnings when beta/experimental features are enabled.");

            if (!ConfigEnabled.Value)
            {
                Log("Plugin is disabled in config.");
                return;
            }

            // Initialize saddle/rider reflection caches for RuntimeModifiers
            if (ConfigEnableRuntimeModifiers.Value)
            {
                SaddledCreaturePatch.Initialize();
            }

            // P0: Log warnings for any enabled beta features
            LogBetaWarnings();

            Log("Loading creature prefab creator configs...");
            LoadedConfig = CreaturePrefabCreatorConfigLoader.LoadAll(this);

            // Hook into Jotunn's prefab-ready event
            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;

            if (ConfigRegisterConsoleCommands.Value)
            {
                Debug.CreaturePrefabDebugCommands.Register();
                Debug.CpcBetaCommands.Register();
            }

            if (ConfigEnableDebugDumpCommands.Value)
            {
                Debug.CreatureAIDumpCommands.Register();
            }

            Log("CreaturePrefabCreator initialized. Waiting for vanilla prefabs...");
        }

        /// <summary>
        /// P0: Log warnings for enabled beta/experimental features
        /// </summary>
        private void LogBetaWarnings()
        {
            if (!ConfigLogBetaFeatureWarnings.Value) return;

            if (ConfigEnableConfigSync.Value)
            {
                LogWarning("EXPERIMENTAL FEATURE ENABLED: Config Sync is not fully implemented. " +
                    "RPC registration is incomplete. Server-client mismatch may occur. " +
                    "Ensure all clients have identical creaturePrefabCreator.json files until sync is complete.");
            }

            if (ConfigEnableRuntimeModifiers.Value)
            {
                LogWarning("BETA FEATURE ENABLED: Runtime Modifiers are active. " +
                    "Stat multipliers apply based on creature state. " +
                    "Multipliers may stack. Monitor for performance issues on busy servers.");
            }

            if (ConfigEnableVisualOverrides.Value)
            {
                LogWarning("BETA FEATURE ENABLED: Visual Overrides are active. " +
                    "Tint/glow effects apply at runtime. Current implementation may create material instances. " +
                    "Monitor memory usage if spawning many creatures.");
            }

            if (ConfigEnableFactionOverrides.Value)
            {
                LogWarning("BETA FEATURE ENABLED: Faction Overrides are active. " +
                    "Invalid faction names will be rejected with a warning. " +
                    "Use caution with 'Players' faction - may cause unexpected aggro behavior.");
            }

        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabsAvailable;
            _harmony.UnpatchSelf();
        }
        
        private void OnVanillaPrefabsAvailable()
        {
            Log("Vanilla prefabs are available. Processing configs...");

            PrefabOverrideManager.ClearAll();
            GeneratedPrefabManager.ClearAll();

            if (LoadedConfig == null)
            {
                LogError("No config available for prefab registration.");
            }
            else
            {
                Log("Registering prefabs from local config. Dedicated servers and clients must use matching creaturePrefabCreator.json files.");

                // P0: Gate features based on safety config
                if (ConfigEnablePrefabOverrides.Value)
                {
                    PrefabOverrideManager.ApplyAll(LoadedConfig.PrefabOverrides, ConfigEnableFactionOverrides.Value);
                }
                else
                {
                    Log("Prefab Overrides are disabled by FeatureSafety.EnablePrefabOverrides");
                }

                if (ConfigEnableGeneratedPrefabs.Value)
                {
                    GeneratedPrefabManager.GenerateAll(LoadedConfig.GeneratedPrefabs, ConfigEnableFactionOverrides.Value);
                }
                else
                {
                    Log("Generated Prefabs are disabled by FeatureSafety.EnableGeneratedPrefabs");
                }

                // P0: Runtime Modifiers disabled by default until stacking caps tested
                if (ConfigEnableRuntimeModifiers.Value)
                {
                    RuntimeModifierManager.Initialize(LoadedConfig.RuntimeModifiers);
                    if (gameObject.GetComponent<Patches.RuntimeModifierTicker>() == null)
                        gameObject.AddComponent<Patches.RuntimeModifierTicker>();
                }
                else
                {
                    Log("Runtime Modifiers are disabled by FeatureSafety.EnableRuntimeModifiers (default until tested)");
                }
            }

            // Run AI marker migration if config changed
            if (LoadedConfig != null && Patches.AIMarkerMigrationManager.IsMigrationNeeded(LoadedConfig))
            {
                Log("Config change detected for disableAI states. Running AI marker migration...");
                Patches.AIMarkerMigrationManager.MigrateOnStartup(LoadedConfig);
            }

            // Unregister to prevent duplicate work on scene reload
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabsAvailable;
        }

        public void Log(string message)
        {
            if (ConfigVerboseLogging != null && !ConfigVerboseLogging.Value)
            {
                if (!message.StartsWith("ERROR"))
                {
                    return;
                }
            }
            Logger.LogInfo($"[CreaturePrefabCreator] {message}");
        }

        public void LogWarning(string message)
        {
            Logger.LogWarning($"[CreaturePrefabCreator] {message}");
        }

        public void LogError(string message)
        {
            Logger.LogError($"[CreaturePrefabCreator] {message}");
        }

        // Public method to run coroutines from static contexts
        public void RunCoroutine(System.Collections.IEnumerator coroutine)
        {
            StartCoroutine(coroutine);
        }
    }
}
