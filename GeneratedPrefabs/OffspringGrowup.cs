using System;
using System.Collections;
using UnityEngine;

namespace CreaturePrefabCreator.GeneratedPrefabs
{
    public class OffspringGrowup : MonoBehaviour
    {
        public string adultPrefabName = "";
        public float growTimeSeconds = 6000f;
        public bool preserveTamed = true;
        public bool preserveLevel = true;
        public bool preserveOwner = true;
        public bool preserveName = false;

        // ZDO key string constants - public for external reference
        public const string BirthTimeKey = "CreaturePrefabCreator_BirthTime";
        public const string GrowTriggeredKey = "CreaturePrefabCreator_GrowTriggered";
        public const string AdultPrefabKey = "CreaturePrefabCreator_AdultPrefab";
        public const string GrowTimeKey = "CreaturePrefabCreator_GrowTimeSeconds";
        // CPC-owned tamed preservation key — written at growup/spawn, read by TameableAwakePatch.
        // Using a CPC-owned key avoids races with AllTameable which destroys and recreates Tameable.
        public const string PreserveTamedKey = "CPC_PreserveTamed";
        public static readonly int PreserveTamedHash = PreserveTamedKey.GetStableHashCode();

        private static readonly int BirthTimeHash = BirthTimeKey.GetStableHashCode();
        private static readonly int GrowTriggeredHash = GrowTriggeredKey.GetStableHashCode();
        private static readonly int AdultPrefabHash = AdultPrefabKey.GetStableHashCode();
        private static readonly int GrowTimeHash = GrowTimeKey.GetStableHashCode();

        private ZNetView znv;
        private bool initialized;

        void Awake()
        {
            znv = GetComponent<ZNetView>();
            if (znv == null || znv.GetZDO() == null)
            {
                // This is expected during prefab creation - ZNetView will be added by delayed fix
                enabled = false;
                return;
            }

            var zdo = znv.GetZDO();
            zdo.Set(AdultPrefabHash, adultPrefabName);
            zdo.Set(GrowTimeHash, growTimeSeconds);

            // Always reset BirthTime on Awake unless growth was already triggered.
            // Without this, a reused or stale ZDO (from a previous cub session) can carry an
            // old BirthTime that makes elapsed time pass immediately on the first FixedUpdate.
            bool alreadyTriggered = zdo.GetBool(GrowTriggeredHash, false);
            if (!alreadyTriggered)
            {
                long currentTime = ZNet.instance?.GetTime().Ticks ?? DateTime.UtcNow.Ticks;
                zdo.Set(BirthTimeHash, currentTime);
            }

            initialized = true;
        }

        void FixedUpdate()
        {
            if (!initialized) return;
            if (!znv.IsOwner()) return;
            if (string.IsNullOrEmpty(adultPrefabName)) return;

            var zdo = znv.GetZDO();
            if (zdo.GetBool(GrowTriggeredHash, false)) return;

            long birthTicks = zdo.GetLong(BirthTimeHash, 0L);
            float growTime = zdo.GetFloat(GrowTimeHash, growTimeSeconds);
            string adultPrefab = zdo.GetString(AdultPrefabHash, adultPrefabName);

            if (birthTicks == 0L) return;

            // Use ZNet world time for growth calculation
            long currentTime = ZNet.instance?.GetTime().Ticks ?? DateTime.UtcNow.Ticks;
            TimeSpan elapsed = new DateTime(currentTime) - new DateTime(birthTicks);
            if (elapsed.TotalSeconds < growTime) return;

            zdo.Set(GrowTriggeredHash, true);
            GrowIntoAdult(zdo, adultPrefab);
        }

        private void GrowIntoAdult(ZDO zdo, string adultPrefabName)
        {
            if (string.IsNullOrEmpty(adultPrefabName))
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError("OffspringGrowup: adultPrefabName is empty.");
                return;
            }

            GameObject adultPrefab = GeneratedPrefabManager.FindSourcePrefab(adultPrefabName);

            if (adultPrefab == null)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogError($"Adult prefab '{adultPrefabName}' not found. Cannot grow {gameObject.name}.");
                return;
            }

            Vector3 pos = transform.position;
            Quaternion rot = transform.rotation;
            GameObject adult = Instantiate(adultPrefab, pos, rot);
            adult.SetActive(true);

            CreaturePrefabCreatorPlugin.Instance?.Log($"{gameObject.name} grew into {adultPrefabName}.");

            var adultZNV = adult.GetComponent<ZNetView>();
            if (adultZNV != null)
            {
                // Capture tamed state NOW before the baby is destroyed.
                // AllTameable destroys and recreates Tameable inside MonsterAI.Awake, so
                // IsTamed() is unreliable. Read from the ZDO 'tamed' key or our own CPC key.
                bool capturedTamed = false;
                if (preserveTamed)
                {
                    bool fromCpcKey = zdo.GetBool(PreserveTamedHash, false);
                    bool fromTamedKey = zdo.GetBool("tamed".GetStableHashCode(), false);
                    capturedTamed = fromCpcKey || fromTamedKey;
                    CreaturePrefabCreatorPlugin.Instance?.Log(
                        $"[OffspringGrowup] Growup tamed capture: CPC_PreserveTamed={fromCpcKey}, 'tamed' ZDO key={fromTamedKey}, capturedTamed={capturedTamed}");
                }
                ZDO capturedFromZDO = zdo;
                CreaturePrefabCreatorPlugin.Instance?.RunCoroutine(DeferredTransferData(adultZNV, capturedFromZDO, capturedTamed));
            }
            else
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"Spawned adult '{adultPrefabName}' has no ZNetView. Data transfer skipped.");
            }

            if (ZNetScene.instance != null)
                ZNetScene.instance.Destroy(gameObject);
            else
                Destroy(gameObject);
        }


        private IEnumerator DeferredTransferData(ZNetView adultZNV, ZDO fromZDO, bool capturedTamed)
        {
            yield return null;

            if (adultZNV == null) yield break;
            var toZDO = adultZNV.GetZDO();
            if (toZDO == null) yield break;

            CreaturePrefabCreatorPlugin.Instance?.Log(
                $"[OffspringGrowup] DeferredTransferData: adult='{adultZNV.gameObject.name}', capturedTamed={capturedTamed}");

            TransferData(fromZDO, toZDO, capturedTamed);

            // Tameable.Awake already fired when the adult was Instantiated, so TameableAwakePatch
            // will not fire again. Apply SetTamed directly here after AllTameable has run.
            if (capturedTamed && preserveTamed)
            {
                var character = adultZNV.GetComponent<Character>();
                if (character != null)
                {
                    bool wasTamed = character.IsTamed();
                    if (!wasTamed)
                        character.SetTamed(true);
                    CreaturePrefabCreatorPlugin.Instance?.Log(
                        $"[OffspringGrowup] SetTamed on adult '{adultZNV.gameObject.name}': wasTamed={wasTamed}, IsTamed after={character.IsTamed()}");
                }
                else
                {
                    CreaturePrefabCreatorPlugin.Instance?.Log(
                        $"[OffspringGrowup] Adult '{adultZNV.gameObject.name}' has no Character component \u2014 SetTamed skipped.");
                }
            }
        }

        private void TransferData(ZDO fromZDO, ZDO toZDO, bool capturedTamed)
        {
            try
            {
                if (preserveTamed)
                {
                    // Write to CPC-owned key. TameableAwakePatch reads this after AllTameable
                    // finishes recreating Tameable (in Tameable.Awake postfix) and calls SetTamed.
                    toZDO.Set(PreserveTamedHash, capturedTamed);
                }
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"Failed to transfer tamed state: {ex.Message}");
            }

            try
            {
                if (preserveLevel)
                {
                    int levelHash = "level".GetStableHashCode();
                    toZDO.Set(levelHash, fromZDO.GetInt(levelHash, 1));
                }
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"Failed to transfer level: {ex.Message}");
            }

            try
            {
                if (preserveOwner)
                {
                    int creatorHash = "creator".GetStableHashCode();
                    toZDO.Set(creatorHash, fromZDO.GetLong(creatorHash, 0L));
                }
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"Failed to transfer owner: {ex.Message}");
            }

            try
            {
                if (preserveName)
                {
                    int nameHash = "cname".GetStableHashCode();
                    string name = fromZDO.GetString(nameHash, "");
                    if (!string.IsNullOrEmpty(name))
                        toZDO.Set(nameHash, name);
                }
            }
            catch (Exception ex)
            {
                CreaturePrefabCreatorPlugin.Instance?.LogWarning($"Failed to transfer custom name: {ex.Message}");
            }
        }
    }
}
