using CreaturePrefabCreator.Compatibility.MountUpRestored;
using System;
using System.Reflection;
using UnityEngine;

namespace CreaturePrefabCreator.Detection
{
    /// <summary>
    /// Comprehensive snapshot of mount/saddle detection state for a creature.
    /// Used for both runtime detection and diagnostic reporting.
    /// </summary>
    public class MountStateSnapshot
    {
        // Target identification
        public string PrefabName { get; set; }
        public string ObjectName { get; set; }

        // Tameable detection
        public bool HasTameable { get; set; }
        public bool TameableHaveSaddleResolved { get; set; }
        public bool TameableHaveSaddleResult { get; set; }

        // Sadle component detection
        public bool HasSadleComponent { get; set; }
        public bool SadleHaveValidUserResolved { get; set; }
        public bool SadleHaveValidUserResult { get; set; }

        // MountUp detection
        public bool MountUpDetected { get; set; }
        public bool MountUpMountableFound { get; set; }
        public bool MountUpGetSaddleReturnedObject { get; set; }
        public bool MountUpSaddleObjectActiveSelf { get; set; }
        public bool MountUpSaddleObjectActiveInHierarchy { get; set; }
        public bool MountUpGetRiderResolved { get; set; }
        public bool MountUpRiderPresent { get; set; }

        // Inventory fallback
        public bool InventoryFallbackUsed { get; set; }
        public bool InventoryFallbackResult { get; set; }

        // Final computed state
        public bool FinalSaddled { get; set; }
        public bool FinalRidden { get; set; }
        public string FinalReason { get; set; }

        /// <summary>
        /// Returns a human-readable summary of the detection result.
        /// </summary>
        public string GetSummary()
        {
            return $"Saddled={FinalSaddled}, Ridden={FinalRidden} ({FinalReason})";
        }
    }

    /// <summary>
    /// Detailed trace information for debugging mount/saddle detection.
    /// </summary>
    public class MountStateTrace
    {
        public string[] DetectionPath { get; set; }
        public string AuthoritySource { get; set; }
        public string Warning { get; set; }
    }

    /// <summary>
    /// Centralized, authoritative mount and saddle state detection.
    /// 
    /// CRITICAL SAFETY RULES:
    /// 1. Component presence alone is NOT sufficient for saddled=true
    /// 2. Must verify ACTIVE/EQUIPPED state, not just existence
    /// 3. Tameable.HaveSaddle() is authoritative when available
    /// 4. MountUp getSaddle() returning GameObject requires checking activeSelf
    /// 5. Ridden requires a real rider, not just a saddle object
    /// 
    /// This prevents false positives like unsaddled Wolves reporting saddled=true.
    /// </summary>
    public static class MountStateDetector
    {
        // Cached reflection for vanilla Valheim
        private static Type _tameableType;
        private static Type _sadleType;
        private static MethodInfo _tameableHaveSaddleMethod;
        private static MethodInfo _tameableHaveRiderMethod;
        private static MethodInfo _sadleHaveValidUserMethod;
        private static MethodInfo _sadleGetRiderMethod;

        // Cached reflection for MountUp
        private static Type _mountUpMountableType;
        private static MethodInfo _mountUpGetSaddleMethod;
        private static MethodInfo _mountUpGetRiderMethod;
        private static string _mountUpGetSaddleMethodName;

        private static bool _reflectionInitialized;

        /// <summary>
        /// Initializes reflection caches. Call once from plugin Awake.
        /// </summary>
        public static void Initialize()
        {
            if (_reflectionInitialized) return;

            try
            {
                // Cache Tameable type and methods
                _tameableType = Type.GetType("Tameable, assembly_valheim");
                if (_tameableType != null)
                {
                    _tameableHaveSaddleMethod = _tameableType.GetMethod("HaveSaddle", 
                        BindingFlags.Public | BindingFlags.Instance);
                    _tameableHaveRiderMethod = _tameableType.GetMethod("HaveRider",
                        BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                    $"[MountStateDetector] Failed to cache Tameable reflection: {ex.Message}");
            }

            try
            {
                // Cache Sadle type and methods
                _sadleType = Type.GetType("Sadle, assembly_valheim");
                if (_sadleType != null)
                {
                    _sadleHaveValidUserMethod = _sadleType.GetMethod("HaveValidUser",
                        BindingFlags.Public | BindingFlags.Instance);
                    _sadleGetRiderMethod = _sadleType.GetMethod("GetRider",
                        BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                    $"[MountStateDetector] Failed to cache Sadle reflection: {ex.Message}");
            }

            try
            {
                // Cache MountUp reflection
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        var t = assembly.GetType("MountUp.Mountable", throwOnError: false);
                        if (t != null)
                        {
                            _mountUpMountableType = t;
                            break;
                        }
                    }
                    catch { }
                }

                if (_mountUpMountableType != null)
                {
                    // Try getSaddle first, then getSadle (spelling varies by version)
                    _mountUpGetSaddleMethod = _mountUpMountableType.GetMethod("getSaddle",
                        BindingFlags.Public | BindingFlags.Instance);
                    if (_mountUpGetSaddleMethod != null)
                    {
                        _mountUpGetSaddleMethodName = "getSaddle";
                    }
                    else
                    {
                        _mountUpGetSaddleMethod = _mountUpMountableType.GetMethod("getSadle",
                            BindingFlags.Public | BindingFlags.Instance);
                        if (_mountUpGetSaddleMethod != null)
                        {
                            _mountUpGetSaddleMethodName = "getSadle";
                        }
                    }

                    _mountUpGetRiderMethod = _mountUpMountableType.GetMethod("GetRider",
                        BindingFlags.Public | BindingFlags.Instance) ??
                        _mountUpMountableType.GetMethod("getRider",
                        BindingFlags.Public | BindingFlags.Instance);
                }
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                    $"[MountStateDetector] Failed to cache MountUp reflection: {ex.Message}");
            }

            _reflectionInitialized = true;

            CreaturePrefabCreatorPlugin.Instance?.Log(
                $"[MountStateDetector] Initialized. " +
                $"Tameable={_tameableType != null}, " +
                $"Sadle={_sadleType != null}, " +
                $"MountUp={_mountUpMountableType != null}"
            );
        }

        /// <summary>
        /// Gets a comprehensive snapshot of mount/saddle state for a creature.
        /// This is the primary API for detection.
        /// </summary>
        /// <param name="character">The character to check</param>
        /// <returns>MountStateSnapshot with all detection details</returns>
        public static MountStateSnapshot GetMountState(Character character)
        {
            var snapshot = new MountStateSnapshot();

            if (character == null)
            {
                snapshot.FinalReason = "Null character";
                return snapshot;
            }

            snapshot.PrefabName = character.gameObject.name;
            snapshot.ObjectName = character.gameObject.name;

            // Clean up name by removing (Clone) suffix
            if (snapshot.PrefabName.EndsWith("(Clone)"))
                snapshot.PrefabName = snapshot.PrefabName.Substring(0, snapshot.PrefabName.Length - 7).TrimEnd();

            // Run detection in order of confidence
            DetectTameableState(character, snapshot);
            DetectSadleState(character, snapshot);
            DetectMountUpState(character, snapshot);
            DetectInventoryFallback(character, snapshot);

            // Compute final state
            ComputeFinalState(snapshot);

            return snapshot;
        }

        /// <summary>
        /// Quick check: Is the creature saddled?
        /// Uses the same detection logic as GetMountState but returns just the boolean.
        /// </summary>
        /// <param name="character">The character to check</param>
        /// <returns>True if creature has an equipped/active saddle</returns>
        public static bool IsSaddled(Character character)
        {
            return GetMountState(character).FinalSaddled;
        }

        /// <summary>
        /// Quick check: Is the creature currently being ridden?
        /// </summary>
        /// <param name="character">The character to check</param>
        /// <returns>True if creature has a rider</returns>
        public static bool IsRidden(Character character)
        {
            return GetMountState(character).FinalRidden;
        }

        /// <summary>
        /// Gets a detailed trace explaining the detection path.
        /// </summary>
        /// <param name="character">The character to check</param>
        /// <returns>MountStateTrace with detection path details</returns>
        public static MountStateTrace GetMountTrace(Character character)
        {
            var snapshot = GetMountState(character);
            var trace = new MountStateTrace();

            var path = new System.Collections.Generic.List<string>();

            // Build detection path description
            if (snapshot.HasTameable)
            {
                path.Add($"Tameable.HaveSaddle() resolved={snapshot.TameableHaveSaddleResolved}, result={snapshot.TameableHaveSaddleResult}");
            }
            else
            {
                path.Add("No Tameable component");
            }

            if (snapshot.HasSadleComponent)
            {
                path.Add($"Sadle.HaveValidUser() resolved={snapshot.SadleHaveValidUserResolved}, result={snapshot.SadleHaveValidUserResult}");
            }

            if (snapshot.MountUpDetected)
            {
                path.Add($"MountUp detected: MountableFound={snapshot.MountUpMountableFound}");
                if (snapshot.MountUpMountableFound)
                {
                    path.Add($"  getSaddle() returned object={snapshot.MountUpGetSaddleReturnedObject}");
                    if (snapshot.MountUpGetSaddleReturnedObject)
                    {
                        path.Add($"  saddleGO.activeSelf={snapshot.MountUpSaddleObjectActiveSelf}, activeInHierarchy={snapshot.MountUpSaddleObjectActiveInHierarchy}");
                    }
                    path.Add($"  GetRider() resolved={snapshot.MountUpGetRiderResolved}, riderPresent={snapshot.MountUpRiderPresent}");
                }
            }

            if (snapshot.InventoryFallbackUsed)
            {
                path.Add($"Inventory fallback: HaveItem('Saddle')={snapshot.InventoryFallbackResult}");
            }

            trace.DetectionPath = path.ToArray();
            trace.AuthoritySource = snapshot.FinalReason;

            // Add warning if there are inconsistencies
            if (snapshot.HasSadleComponent && !snapshot.SadleHaveValidUserResult &&
                snapshot.TameableHaveSaddleResolved && snapshot.TameableHaveSaddleResult)
            {
                trace.Warning = "Inconsistency: Tameable says saddled but Sadle says no valid user";
            }

            return trace;
        }

        private static void DetectTameableState(Character character, MountStateSnapshot snapshot)
        {
            if (_tameableType == null) return;

            var tameable = character.GetComponent(_tameableType);
            snapshot.HasTameable = tameable != null;

            if (tameable == null || _tameableHaveSaddleMethod == null) return;

            try
            {
                var result = _tameableHaveSaddleMethod.Invoke(tameable, null);
                snapshot.TameableHaveSaddleResolved = true;
                snapshot.TameableHaveSaddleResult = result is bool b && b;
            }
            catch (Exception ex)
            {
                snapshot.TameableHaveSaddleResolved = false;
                CreaturePrefabCreatorPlugin.Instance?.LogWarning(
                    $"[MountStateDetector] Tameable.HaveSaddle() failed: {ex.Message}");
            }
        }

        private static void DetectSadleState(Character character, MountStateSnapshot snapshot)
        {
            if (_sadleType == null) return;

            var sadle = character.GetComponent(_sadleType) ??
                         character.GetComponentInChildren(_sadleType, true);
            snapshot.HasSadleComponent = sadle != null;

            if (sadle == null || _sadleHaveValidUserMethod == null) return;

            try
            {
                var result = _sadleHaveValidUserMethod.Invoke(sadle, null);
                snapshot.SadleHaveValidUserResolved = true;
                snapshot.SadleHaveValidUserResult = result is bool b && b;
            }
            catch
            {
                snapshot.SadleHaveValidUserResolved = false;
            }

            // Check for rider via Sadle.GetRider() for ridden detection
            if (_sadleGetRiderMethod != null)
            {
                try
                {
                    var rider = _sadleGetRiderMethod.Invoke(sadle, null);
                    // GetRider returns the Humanoid/Player component of the rider
                    snapshot.MountUpRiderPresent = rider != null;
                }
                catch { }
            }
        }

        private static void DetectMountUpState(Character character, MountStateSnapshot snapshot)
        {
            snapshot.MountUpDetected = MountUpRestoredCompat.IsAvailable;

            if (_mountUpMountableType == null || _mountUpGetSaddleMethod == null)
                return;

            var mountable = character.GetComponent(_mountUpMountableType);
            snapshot.MountUpMountableFound = mountable != null;

            if (mountable == null) return;

            // Get the saddle GameObject
            try
            {
                var saddleObj = _mountUpGetSaddleMethod.Invoke(mountable, null);
                snapshot.MountUpGetSaddleReturnedObject = saddleObj is GameObject && saddleObj != null;

                if (saddleObj is GameObject saddleGO && saddleGO != null)
                {
                    snapshot.MountUpSaddleObjectActiveSelf = saddleGO.activeSelf;
                    snapshot.MountUpSaddleObjectActiveInHierarchy = saddleGO.activeInHierarchy;
                }
            }
            catch
            {
                snapshot.MountUpGetSaddleReturnedObject = false;
            }

            // Check for rider via MountUp
            if (_mountUpGetRiderMethod != null)
            {
                try
                {
                    var rider = _mountUpGetRiderMethod.Invoke(mountable, null);
                    snapshot.MountUpGetRiderResolved = true;
                    snapshot.MountUpRiderPresent = rider != null;
                }
                catch
                {
                    snapshot.MountUpGetRiderResolved = false;
                }
            }
        }

        private static void DetectInventoryFallback(Character character, MountStateSnapshot snapshot)
        {
            // Only use fallback if no other detection succeeded
            if (snapshot.TameableHaveSaddleResult ||
                snapshot.SadleHaveValidUserResult ||
                snapshot.MountUpSaddleObjectActiveSelf)
                return;

            if (character is Humanoid humanoid)
            {
                var inv = humanoid.GetInventory();
                if (inv != null)
                {
                    snapshot.InventoryFallbackUsed = true;
                    snapshot.InventoryFallbackResult = inv.HaveItem("Saddle");
                }
            }
        }

        private static void ComputeFinalState(MountStateSnapshot snapshot)
        {
            // SADDLED DETECTION - Priority order:
            // 1. Tameable.HaveSaddle() - authoritative
            // 2. Sadle.HaveValidUser() - equipped saddle
            // 3. MountUp saddleGO.activeSelf - active/equipped saddle
            // 4. Inventory fallback - lower confidence

            // 1. Tameable.HaveSaddle() is authoritative
            if (snapshot.HasTameable && snapshot.TameableHaveSaddleResolved)
            {
                if (snapshot.TameableHaveSaddleResult)
                {
                    snapshot.FinalSaddled = true;
                    snapshot.FinalReason = "Tameable.HaveSaddle() returned true";
                    ComputeRiddenState(snapshot);
                    return;
                }
                else
                {
                    // HaveSaddle resolved and said false - trust it, skip further checks
                    snapshot.FinalSaddled = false;
                    snapshot.FinalReason = "Tameable.HaveSaddle() returned false";
                    ComputeRiddenState(snapshot);
                    return;
                }
            }

            // 2. Sadle component with valid user
            if (snapshot.HasSadleComponent && snapshot.SadleHaveValidUserResolved &&
                snapshot.SadleHaveValidUserResult)
            {
                snapshot.FinalSaddled = true;
                snapshot.FinalReason = "Sadle.HaveValidUser() returned true";
                ComputeRiddenState(snapshot);
                return;
            }

            // 3. MountUp with active saddle object
            if (snapshot.MountUpMountableFound && snapshot.MountUpGetSaddleReturnedObject &&
                snapshot.MountUpSaddleObjectActiveSelf)
            {
                snapshot.FinalSaddled = true;
                snapshot.FinalReason = $"MountUp.{_mountUpGetSaddleMethodName}() returned active saddle GameObject";
                ComputeRiddenState(snapshot);
                return;
            }

            // 4. Inventory fallback
            if (snapshot.InventoryFallbackUsed && snapshot.InventoryFallbackResult)
            {
                snapshot.FinalSaddled = true;
                snapshot.FinalReason = "Inventory contains 'Saddle' item (fallback)";
                ComputeRiddenState(snapshot);
                return;
            }

            // Not saddled
            snapshot.FinalSaddled = false;
            snapshot.FinalReason = BuildNotSaddledReason(snapshot);
            ComputeRiddenState(snapshot);
        }

        private static void ComputeRiddenState(MountStateSnapshot snapshot)
        {
            // Can only be ridden if saddled
            if (!snapshot.FinalSaddled)
            {
                snapshot.FinalRidden = false;
                return;
            }

            // Check for rider presence
            // Priority: Sadle.GetRider() or MountUp.GetRider()
            if (snapshot.MountUpRiderPresent)
            {
                snapshot.FinalRidden = true;
                snapshot.FinalReason += ", has rider";
                return;
            }

            // Check via Tameable.HaveRider()
            if (snapshot.HasTameable && _tameableHaveRiderMethod != null)
            {
                // We need to re-get the tameable and check
                // For now, rely on the Sadle/MountUp rider check
            }

            snapshot.FinalRidden = false;
        }

        private static string BuildNotSaddledReason(MountStateSnapshot snapshot)
        {
            var reasons = new System.Collections.Generic.List<string>();

            if (!snapshot.HasTameable)
                reasons.Add("no Tameable component");
            else if (snapshot.TameableHaveSaddleResolved && !snapshot.TameableHaveSaddleResult)
                reasons.Add("Tameable.HaveSaddle()=false");

            if (snapshot.HasSadleComponent)
            {
                if (snapshot.SadleHaveValidUserResolved && !snapshot.SadleHaveValidUserResult)
                    reasons.Add("Sadle.HaveValidUser()=false");
            }

            if (snapshot.MountUpMountableFound)
            {
                if (!snapshot.MountUpGetSaddleReturnedObject)
                    reasons.Add("MountUp.getSaddle() returned null");
                else if (!snapshot.MountUpSaddleObjectActiveSelf)
                    reasons.Add("MountUp saddle GameObject not active");
            }

            if (snapshot.InventoryFallbackUsed && !snapshot.InventoryFallbackResult)
                reasons.Add("inventory has no 'Saddle' item");

            if (reasons.Count == 0)
                return "no saddle detected";

            return string.Join(", ", reasons);
        }
    }
}
