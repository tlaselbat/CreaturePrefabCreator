using CreaturePrefabCreator.RuntimeModifiers;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace CreaturePrefabCreator.Patches
{
    /// <summary>
    /// P2: RIDING-ONLY AI ENABLE
    ///
    /// Inverts the old suppression behavior:
    /// - Creatures with disableAI=true (decorative/statue): normally have AI off
    /// - Actively ridden: temporarily ENABLE AI so the creature "comes alive"
    /// - On dismount: restore AI to disabled state
    ///
    /// Only affects creatures whose prefab was created with disableAI=true.
    /// Normal creatures (AI enabled) are completely unaffected.
    /// </summary>

    /// <summary>
    /// Marker attached to prefabs whose config sets disableAI=true.
    /// Used at runtime to identify which creatures should gain AI while ridden.
    /// Also runs the periodic mount check via its own Update loop.
    /// </summary>
    public class PermanentAIDisabledMarker : MonoBehaviour
    {
        private float _lastCheckTime;
        private const float CheckInterval = 0.5f;

        // State tracking to only log on changes, not every check
        private bool? _lastRiddenState;

        void Update()
        {
            var plugin = CreaturePrefabCreatorPlugin.Instance;
            if (plugin == null) return;
            if (plugin.ConfigEnableRidingAISuppression?.Value != true) return;

            float now = Time.time;
            if (now - _lastCheckTime < CheckInterval) return;
            _lastCheckTime = now;

            var character = GetComponent<Character>();
            if (character == null) return;

            // CRITICAL: Only modify AI if we are the network owner
            if (!SaddledCreaturePatch.CanModifyCreature(character))
            {
                if (plugin.ConfigDebugAIState?.Value == true)
                    plugin.Log($"[PermanentAIDisabledMarker] {character.name}: skipped - not owner or no ZNetView");
                return;
            }

            try
            {
                bool isRidden = SaddledCreaturePatch.IsActivelyRidden(character);
                var marker = character.GetComponent<RidingAITempEnabledMarker>();

                // Track state changes to reduce log spam - only log on transitions
                bool stateChanged = _lastRiddenState == null || _lastRiddenState != isRidden;
                _lastRiddenState = isRidden;

                if (isRidden && marker == null)
                {
                    SaddledCreaturePatch.EnableAIComponents(character);
                    SaddledCreaturePatch.MarkAIEnabled(character);

                    if (plugin.ConfigDebugAIState?.Value == true && stateChanged)
                        plugin.Log($"[PermanentAIDisabledMarker] {character.name}: >>> ENABLING AI (actively ridden, disableAI=true) <<<");
                }
                else if (!isRidden && marker != null)
                {
                    SaddledCreaturePatch.DisableAIComponents(character);
                    UnityEngine.Object.Destroy(marker);

                    if (plugin.ConfigDebugAIState?.Value == true && stateChanged)
                        plugin.Log($"[PermanentAIDisabledMarker] {character.name}: dismounted - disabling AI, restored to config state");
                }
            }
            catch (Exception ex)
            {
                plugin?.LogWarning($"[PermanentAIDisabledMarker] {character?.name}: ERROR - {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Marker component attached to creatures whose AI was temporarily enabled by this plugin.
    /// Tracks that we own the enable and should restore on dismount.
    /// </summary>
    public class RidingAITempEnabledMarker : MonoBehaviour
    {
        public float EnabledTime;
    }

    public static class SaddledCreaturePatch
    {
        // Cached reflection data to avoid per-frame lookups
        private static Type? _sadleType;
        private static MethodInfo? _sadleGetRiderMethod;
        private static MethodInfo? _sadleHaveValidUserMethod;
        private static MethodInfo? _sadleGetUserMethod;

        // MountUp support — MountUpRestored path: Mountable.getSaddle/getSadle() -> GameObject -> Sadle.GetRider()
        private static Type? _mountUpMountableType;
        private static MethodInfo? _mountUpGetSaddleMethod;
        private static string? _mountUpGetSaddleMethodName;

        // Tameable reflection for authoritative saddle/rider detection
        private static Type? _tameableType;
        private static MethodInfo? _tameableHaveSaddleMethod;
        private static MethodInfo? _tameableHaveRiderMethod;
        private static FieldInfo? _tameableSaddleField;

        private static bool _reflectionInitialized;
        private static bool _retryStarted;

        /// <summary>True if MountUp.Mountable type was found in any loaded assembly.</summary>
        public static bool MountUpDetected => _mountUpMountableType != null;

        /// <summary>True if MountUp.Mountable type was found AND getSaddle/getSadle method resolved.</summary>
        public static bool MountUpTypeResolved => _mountUpMountableType != null && _mountUpGetSaddleMethod != null;

        /// <summary>Placeholder — AllTameable does not require runtime type-cache here; detection is via BepInEx chainloader.</summary>
        public static bool AllTameableDetected { get; private set; }

        /// <summary>
        /// Checks if we have permission to modify this creature (network safety).
        /// Must be called before any AI component modifications.
        /// </summary>
        public static bool CanModifyCreature(Character character)
        {
            if (character == null) return false;
            var nview = character.GetComponent<ZNetView>();
            return nview != null && nview.IsValid() && nview.IsOwner();
        }

        /// <summary>
        /// Initializes cached reflection. Call once from plugin Awake (gated by EnableRidingAISuppression).
        /// </summary>
        public static void Initialize()
        {
            if (_reflectionInitialized) return;

            try
            {
                _sadleType = Type.GetType("Sadle, assembly_valheim");
                if (_sadleType != null)
                {
                    _sadleGetRiderMethod = _sadleType.GetMethod("GetRider", BindingFlags.Public | BindingFlags.Instance);
                    _sadleHaveValidUserMethod = _sadleType.GetMethod("HaveValidUser", BindingFlags.Public | BindingFlags.Instance);
                    _sadleGetUserMethod = _sadleType.GetMethod("GetUser", BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch { }

            // Initialize Tameable reflection for authoritative saddle/rider detection
            try
            {
                _tameableType = Type.GetType("Tameable, assembly_valheim");
                if (_tameableType != null)
                {
                    _tameableHaveSaddleMethod = _tameableType.GetMethod("HaveSaddle", BindingFlags.Public | BindingFlags.Instance);
                    _tameableHaveRiderMethod = _tameableType.GetMethod("HaveRider", BindingFlags.Public | BindingFlags.Instance);
                    _tameableSaddleField = _tameableType.GetField("m_saddle", BindingFlags.NonPublic | BindingFlags.Instance);
                }
            }
            catch { }

            // MountUp support — scan all loaded assemblies to handle MountUp / MountUpRestored / etc.
            bool mountableFound = false;
            bool getSaddleFound = false;
            try
            {
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var t = assembly.GetType("MountUp.Mountable", throwOnError: false);
                        if (t != null) { _mountUpMountableType = t; mountableFound = true; break; }
                    }
                    catch { }
                }

                if (_mountUpMountableType != null)
                {
                    // MountUpRestored uses getSaddle() or getSadle() (spelling varies)
                    _mountUpGetSaddleMethod = _mountUpMountableType.GetMethod("getSaddle", BindingFlags.Public | BindingFlags.Instance);
                    if (_mountUpGetSaddleMethod != null)
                    {
                        _mountUpGetSaddleMethodName = "getSaddle";
                        getSaddleFound = true;
                    }
                    else
                    {
                        _mountUpGetSaddleMethod = _mountUpMountableType.GetMethod("getSadle", BindingFlags.Public | BindingFlags.Instance);
                        if (_mountUpGetSaddleMethod != null)
                        {
                            _mountUpGetSaddleMethodName = "getSadle";
                            getSaddleFound = true;
                        }
                    }
                }
            }
            catch { }

            // Check AllTameable via chainloader
            try
            {
                foreach (var pluginInfo in BepInEx.Bootstrap.Chainloader.PluginInfos.Values)
                {
                    if (pluginInfo?.Metadata?.GUID != null &&
                        pluginInfo.Metadata.GUID.IndexOf("tameable", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        AllTameableDetected = true;
                        break;
                    }
                }
            }
            catch { }

            _reflectionInitialized = true;
            var plugin = CreaturePrefabCreatorPlugin.Instance;
            if (plugin != null)
            {
                if (mountableFound && getSaddleFound && _sadleGetRiderMethod != null)
                {
                    plugin.Log($"Riding AI Enable reflection initialized. MountUp detected (Mountable.{_mountUpGetSaddleMethodName}() -> Sadle.GetRider()).");
                    RuntimeModifierEventBuffer.Record("MountUpDetected", null, null, $"Mountable.{_mountUpGetSaddleMethodName}() resolved");
                }
                else if (mountableFound)
                {
                    plugin.Log($"MountUp reflection incomplete: Mountable={mountableFound}, getSaddle/getSadle={getSaddleFound}, Sadle.GetRider={_sadleGetRiderMethod != null}");
                    RuntimeModifierEventBuffer.Record("MountUpDetected", null, null, $"Mountable found but incomplete: getSaddle={getSaddleFound}, GetRider={_sadleGetRiderMethod != null}");
                }
                else
                {
                    plugin.Log("Riding AI Enable reflection initialized. MountUp NOT detected (vanilla saddles only).");
                }

                if (AllTameableDetected)
                    RuntimeModifierEventBuffer.Record("AllTameableDetected", null, null, "Found via BepInEx Chainloader GUID scan");
            }

            // Start delayed retry if MountUp not detected yet (it may load after us)
            if (!mountableFound && !_retryStarted)
            {
                _retryStarted = true;
                CreaturePrefabCreatorPlugin.Instance?.Log("MountUp not detected yet; scheduling delayed reflection retry.");
                CreaturePrefabCreatorPlugin.Instance?.RunCoroutine(RetryInitializeReflection());
            }
        }

        /// <summary>
        /// Retries MountUp reflection initialization after a delay.
        /// MountUp may load after CreaturePrefabCreator, so we retry a few times.
        /// </summary>
        private static System.Collections.IEnumerator RetryInitializeReflection()
        {
            int maxRetries = 5;
            for (int i = 0; i < maxRetries; i++)
            {
                if (_mountUpMountableType != null) yield break; // Already found

                yield return new WaitForSeconds(2f);

                bool mountableFound = false;
                bool getSaddleFound = false;

                try
                {
                    foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        try
                        {
                            var t = assembly.GetType("MountUp.Mountable", throwOnError: false);
                            if (t != null) { _mountUpMountableType = t; mountableFound = true; break; }
                        }
                        catch { }
                    }

                    if (_mountUpMountableType != null && _mountUpGetSaddleMethod == null)
                    {
                        _mountUpGetSaddleMethod = _mountUpMountableType.GetMethod("getSaddle", BindingFlags.Public | BindingFlags.Instance);
                        if (_mountUpGetSaddleMethod != null)
                        {
                            _mountUpGetSaddleMethodName = "getSaddle";
                            getSaddleFound = true;
                        }
                        else
                        {
                            _mountUpGetSaddleMethod = _mountUpMountableType.GetMethod("getSadle", BindingFlags.Public | BindingFlags.Instance);
                            if (_mountUpGetSaddleMethod != null)
                            {
                                _mountUpGetSaddleMethodName = "getSadle";
                                getSaddleFound = true;
                            }
                        }
                    }
                }
                catch { }

                if (mountableFound && getSaddleFound)
                {
                    CreaturePrefabCreatorPlugin.Instance?.Log($"MountUp reflection initialized after delayed retry (attempt {i + 1}/{maxRetries}): Mountable.{_mountUpGetSaddleMethodName}() resolved.");
                    RuntimeModifierEventBuffer.Record("MountUpDetected", null, null, $"Delayed retry success (attempt {i + 1}): Mountable.{_mountUpGetSaddleMethodName}() resolved");
                    yield break;
                }
                else
                {
                    CreaturePrefabCreatorPlugin.Instance?.Log($"MountUp reflection retry {i + 1}/{maxRetries}: Mountable={(mountableFound || _mountUpMountableType != null)}, getSaddle/getSadle={getSaddleFound}");
                }
            }

            if (_mountUpMountableType == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.Log("MountUp reflection failed after all delayed retries. Using vanilla saddle detection only.");
            }
        }

        /// <summary>
        /// P2: Determine if this creature is actively being ridden RIGHT NOW.
        /// Uses authoritative Valheim APIs in priority order.
        /// </summary>
        internal static bool IsActivelyRidden(Character character)
        {
            if (character == null) return false;

            bool debug = CreaturePrefabCreatorPlugin.Instance?.ConfigDebugMountState?.Value == true;

            try
            {
                // 1. HIGHEST PRIORITY: Tameable.HaveRider() - authoritative API
                if (_tameableType != null && _tameableHaveRiderMethod != null)
                {
                    var tameable = character.GetComponent(_tameableType);
                    if (tameable != null)
                    {
                        bool hasRider = (bool)_tameableHaveRiderMethod.Invoke(tameable, null);
                        if (hasRider)
                        {
                            if (debug) CreaturePrefabCreatorPlugin.Instance?.Log($"[IsActivelyRidden] {character.name}: true (Tameable.HaveRider)");
                            return true;
                        }
                    }
                }

                // 2. Character.HaveRider() - fallback API
                var haveRiderMethod = typeof(Character).GetMethod("HaveRider", BindingFlags.Public | BindingFlags.Instance);
                if (haveRiderMethod != null)
                {
                    bool hasRider = (bool)haveRiderMethod.Invoke(character, null);
                    if (hasRider)
                    {
                        if (debug) CreaturePrefabCreatorPlugin.Instance?.Log($"[IsActivelyRidden] {character.name}: true (Character.HaveRider)");
                        return true;
                    }
                }

                // 3. Tameable.m_saddle.HaveValidUser() - medium confidence
                if (_tameableType != null && _tameableSaddleField != null && _sadleHaveValidUserMethod != null)
                {
                    var tameable = character.GetComponent(_tameableType);
                    if (tameable != null)
                    {
                        object saddle = _tameableSaddleField.GetValue(tameable);
                        if (saddle != null)
                        {
                            bool hasValidUser = (bool)_sadleHaveValidUserMethod.Invoke(saddle, null);
                            if (hasValidUser)
                            {
                                if (debug) CreaturePrefabCreatorPlugin.Instance?.Log($"[IsActivelyRidden] {character.name}: true (Tameable.m_saddle.HaveValidUser)");
                                return true;
                            }
                        }
                    }
                }

                // 4. Sadle.HaveValidUser() or Sadle.GetUser() != 0 - lower confidence
                if (_sadleType != null)
                {
                    var sadle = character.GetComponent(_sadleType) ?? character.GetComponentInChildren(_sadleType, true);
                    if (sadle != null)
                    {
                        // Try Sadle.HaveValidUser()
                        if (_sadleHaveValidUserMethod != null)
                        {
                            bool hasValidUser = (bool)_sadleHaveValidUserMethod.Invoke(sadle, null);
                            if (hasValidUser)
                            {
                                if (debug) CreaturePrefabCreatorPlugin.Instance?.Log($"[IsActivelyRidden] {character.name}: true (Sadle.HaveValidUser)");
                                return true;
                            }
                        }

                        // Fallback to Sadle.GetUser() != 0
                        if (_sadleGetUserMethod != null)
                        {
                            long userId = (long)_sadleGetUserMethod.Invoke(sadle, null);
                            if (userId != 0)
                            {
                                if (debug) CreaturePrefabCreatorPlugin.Instance?.Log($"[IsActivelyRidden] {character.name}: true (Sadle.GetUser={userId})");
                                return true;
                            }
                        }
                    }
                }

                // 5. MountUpRestored diagnostic only (not authoritative)
                if (_mountUpMountableType != null && _mountUpGetSaddleMethod != null && _sadleGetRiderMethod != null)
                {
                    var mountable = character.GetComponent(_mountUpMountableType);
                    if (mountable != null)
                    {
                        object saddleObj = _mountUpGetSaddleMethod.Invoke(mountable, null);
                        if (saddleObj is GameObject saddleGO)
                        {
                            var sadle = saddleGO.GetComponent(_sadleType);
                            if (sadle != null)
                            {
                                var rider = _sadleGetRiderMethod.Invoke(sadle, null);
                                if (rider != null)
                                {
                                    if (debug) CreaturePrefabCreatorPlugin.Instance?.Log($"[IsActivelyRidden] {character.name}: MountUp diagnostic has rider (via {_mountUpGetSaddleMethodName} -> Sadle.GetRider)");
                                    // Note: This is diagnostic only, not authoritative
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"[IsActivelyRidden] {character?.name}: ERROR - {ex.Message}");
            }

            // Note: We don't log "false" here to prevent spam. The caller (PermanentAIDisabledMarker.Update)
            // tracks state changes and logs transitions only when the ridden state actually changes.
            return false;
        }

        /// <summary>
        /// Canonical saddle detection using the same cached reflection as IsActivelyRidden.
        /// Priority: Tameable.m_saddle + HaveSaddle() → Sadle component → Humanoid inventory "Saddle".
        /// Call SaddledCreaturePatch.Initialize() before first use.
        /// </summary>
        public static bool IsSaddledViaCanonicalPath(Character character)
        {
            if (character == null) return false;

            bool debug = CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true;

            // 1. HIGHEST confidence: Tameable.m_saddle + HaveSaddle()
            if (_tameableType != null && _tameableSaddleField != null && _tameableHaveSaddleMethod != null)
            {
                var tameable = character.GetComponent(_tameableType);
                if (tameable != null)
                {
                    try
                    {
                        object saddleInstance = _tameableSaddleField.GetValue(tameable);
                        bool hasSaddle = saddleInstance != null && (bool)_tameableHaveSaddleMethod.Invoke(tameable, null);
                        if (hasSaddle)
                        {
                            if (debug) CreaturePrefabCreatorPlugin.Instance.Log($"[IsSaddled] {character.name}: true (Tameable.m_saddle + HaveSaddle)");
                            return true;
                        }
                    }
                    catch { }
                }
            }

            // 2. MEDIUM confidence: Sadle component on self or children
            if (_sadleType != null)
            {
                var sadle = character.GetComponent(_sadleType) ?? character.GetComponentInChildren(_sadleType, true);
                if (sadle != null)
                {
                    if (debug) CreaturePrefabCreatorPlugin.Instance.Log($"[IsSaddled] {character.name}: true (Sadle component)");
                    return true;
                }
            }

            // 3. FALLBACK: Humanoid inventory has "Saddle" item
            if (character is Humanoid humanoid)
            {
                var inv = humanoid.GetInventory();
                if (inv != null && inv.HaveItem("Saddle"))
                {
                    if (debug) CreaturePrefabCreatorPlugin.Instance.Log($"[IsSaddled] {character.name}: true (inventory 'Saddle')");
                    return true;
                }
            }

            if (debug) CreaturePrefabCreatorPlugin.Instance.Log($"[IsSaddled] {character.name}: false");
            return false;
        }

        /// <summary>
        /// P2: Check if this creature was configured with disableAI=true at prefab creation.
        /// Uses the PermanentAIDisabledMarker component instead of broken ZDO flag.
        /// </summary>
        internal static bool HasPermanentAIDisable(Character character)
        {
            return character != null && character.GetComponent<PermanentAIDisabledMarker>() != null;
        }

        /// <summary>
        /// P2: Mark that we have temporarily enabled AI for riding.
        /// </summary>
        internal static void MarkAIEnabled(Character character)
        {
            if (character == null) return;

            var marker = character.GetComponent<RidingAITempEnabledMarker>();
            if (marker == null)
            {
                marker = character.gameObject.AddComponent<RidingAITempEnabledMarker>();
            }
            marker.EnabledTime = Time.time;

            var plugin = CreaturePrefabCreatorPlugin.Instance;
            if (plugin?.ConfigDebugAIState?.Value == true)
                plugin.Log($"[MarkAIEnabled] {character.name}: marked for AI disable on dismount");
        }

        /// <summary>
        /// P2: Check if we previously enabled AI and disable it if the creature is no longer ridden.
        /// </summary>
        internal static void CheckAndDisableAI(Character character)
        {
            if (character == null) return;

            var marker = character.GetComponent<RidingAITempEnabledMarker>();
            if (marker == null) return; // We never enabled this creature

            if (!IsActivelyRidden(character))
            {
                UnityEngine.Object.Destroy(marker);

                var plugin = CreaturePrefabCreatorPlugin.Instance;
                if (plugin?.ConfigDebugMountState?.Value == true)
                    plugin.Log($"[CheckAndDisableAI] {character.name}: dismounted - removing enable marker, AI will be restored to disabled");
            }
        }

        /// <summary>
        /// P2: Enable AI components on this creature.
        /// CRITICAL: Only modifies AI if we are the network owner.
        /// </summary>
        internal static void EnableAIComponents(Character character)
        {
            if (character == null) return;
            
            // CRITICAL: Check network ownership before modifying AI
            if (!CanModifyCreature(character))
            {
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[EnableAIComponents] {character.name}: skipped - not owner or no ZNetView");
                return;
            }

            var monsterAI = character.GetComponent<MonsterAI>();
            if (monsterAI != null && !monsterAI.enabled)
            {
                monsterAI.enabled = true;
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[EnableAIComponents] {character.name}: enabled MonsterAI");
            }

            var animalAI = character.GetComponent<AnimalAI>();
            if (animalAI != null && !animalAI.enabled)
            {
                animalAI.enabled = true;
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[EnableAIComponents] {character.name}: enabled AnimalAI");
            }

            var baseAI = character.GetComponent<BaseAI>();
            if (baseAI != null && !baseAI.enabled)
            {
                baseAI.enabled = true;
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[EnableAIComponents] {character.name}: enabled BaseAI");
            }
        }

        /// <summary>
        /// P2: Disable AI components on this creature.
        /// CRITICAL: Only modifies AI if we are the network owner.
        /// </summary>
        internal static void DisableAIComponents(Character character)
        {
            if (character == null) return;
            
            // CRITICAL: Check network ownership before modifying AI
            if (!CanModifyCreature(character))
            {
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[DisableAIComponents] {character.name}: skipped - not owner or no ZNetView");
                return;
            }

            var monsterAI = character.GetComponent<MonsterAI>();
            if (monsterAI != null && monsterAI.enabled)
            {
                monsterAI.enabled = false;
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[DisableAIComponents] {character.name}: disabled MonsterAI");
            }

            var animalAI = character.GetComponent<AnimalAI>();
            if (animalAI != null && animalAI.enabled)
            {
                animalAI.enabled = false;
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[DisableAIComponents] {character.name}: disabled AnimalAI");
            }

            var baseAI = character.GetComponent<BaseAI>();
            if (baseAI != null && baseAI.enabled)
            {
                baseAI.enabled = false;
                if (CreaturePrefabCreatorPlugin.Instance?.ConfigDebugAIState?.Value == true)
                    CreaturePrefabCreatorPlugin.Instance?.Log($"[DisableAIComponents] {character.name}: disabled BaseAI");
            }
        }
    }

}
