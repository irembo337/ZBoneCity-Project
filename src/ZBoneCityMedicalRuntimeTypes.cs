using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Injection;
using UnityEngine;

namespace ZBoneCity.Medical
{
    public enum ZBCMedicalItemKind
    {
        SalewaFirstAidKit,
        IFAK,
        GrizzlyMedkit,
        SurvivalFirstAidRollupKit,
        MilitaryBandage,
        Adrenaline,
        SJ1Stimulator,
        AluminumSplint,
        Morphine,
        ETGStimulator,
        Painkillers,
        BloodBag,
        StandardMedkit,
        Tourniquet
    }

    public enum ZBCApplicationMode
    {
        HoldToUse,
        ApplyToLimb,
        InjectIntoArm
    }

    public enum MedicalContainerOpenMode
    {
        InventoryOnly,
        SpawnIntoWorld
    }

    [Serializable]
    public sealed class MedicalContainerSlot
    {
        public bool enabled = true;
        public GameObject medicalItemPrefab = null!;
        public int quantity = 1;
        public int stackSize = 1;
        public float spawnChance = 1f;

        public bool IsValid => enabled && medicalItemPrefab != null && quantity > 0 && spawnChance > 0f;
        public int StackSize => Mathf.Max(1, stackSize);
        public string DisplayName => medicalItemPrefab != null ? NicifyName(medicalItemPrefab.name) : "Medical Item";

        public int RollQuantity()
        {
            if (!IsValid)
                return 0;

            return UnityEngine.Random.value <= Mathf.Clamp01(spawnChance) ? quantity : 0;
        }

        private static string NicifyName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName))
                return "Medical Item";

            return rawName.Replace('_', ' ').Replace('-', ' ');
        }
    }

    public sealed class ZBCMedicalItem : MonoBehaviour
    {
        public ZBCMedicalItem(IntPtr pointer) : base(pointer)
        {
        }

        public ZBCMedicalItemKind itemKind;
        public string displayName = "Medical Item";
        public string description = "ZBoneCity item.";
        public Sprite icon = null!;
        public ZBCApplicationMode applicationMode;
        public int stackSize = 1;
        public int maxUses = 1;
        public int remainingUses = 1;
        public float applicationSeconds = 1f;
        public float healthRestore;
        public float painSuppression;
        public float staminaBoost;
        public float unconsciousWakeChance;
        public bool treatsMinorBleeding;
        public bool treatsModerateBleeding;
        public bool treatsSevereBleeding;
        public bool treatsArterialBleeding;
        public bool treatsFractures;
        public bool treatsPain;
        public bool treatsShock;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
    }

    public sealed class MedicalContainer : MonoBehaviour
    {
        public MedicalContainer(IntPtr pointer) : base(pointer)
        {
        }

        public string containerName = "Medical Container";
        public int containerCapacity = 8;
        public MedicalContainerOpenMode openMode = MedicalContainerOpenMode.SpawnIntoWorld;
        public bool consumeOnOpen = true;
        public Transform spawnPoint = null!;
        public float spawnRadius = 0.18f;
        public float spawnImpulse = 0.12f;
        public List<MedicalContainerSlot> contents = new List<MedicalContainerSlot>();

        private bool _hasBeenOpened;

        public string ContainerName => string.IsNullOrWhiteSpace(containerName) ? gameObject.name : containerName;

        public bool CanOpen()
        {
            return !consumeOnOpen || !_hasBeenOpened;
        }

        public bool OpenContainer()
        {
            if (!CanOpen())
                return false;

            _hasBeenOpened = true;
            if (openMode != MedicalContainerOpenMode.SpawnIntoWorld || contents == null)
                return true;

            Transform origin = spawnPoint != null ? spawnPoint : transform;
            int spawnedIndex = 0;
            for (int i = 0; i < contents.Count; i++)
            {
                MedicalContainerSlot slot = contents[i];
                if (slot == null || !slot.IsValid)
                    continue;

                int remaining = slot.RollQuantity();
                while (remaining > 0)
                {
                    int stackQuantity = Mathf.Min(slot.StackSize, remaining);
                    GameObject instance = Instantiate(slot.medicalItemPrefab, GetSpawnPosition(origin, spawnedIndex), GetSpawnRotation(origin));
                    instance.name = stackQuantity > 1 ? slot.DisplayName + " x" + stackQuantity : slot.DisplayName;

                    MedicalContainerStack stack = instance.GetComponent<MedicalContainerStack>();
                    if (stack == null)
                        stack = instance.AddComponent<MedicalContainerStack>();
                    stack.quantity = stackQuantity;
                    stack.itemDisplayName = slot.DisplayName;
                    stack.sourceContainer = this;

                    PrepareSpawnedPhysics(instance, spawnedIndex);
                    spawnedIndex++;
                    remaining -= stackQuantity;
                }
            }

            return true;
        }

        public void ResetContainer()
        {
            _hasBeenOpened = false;
        }

        private Vector3 GetSpawnPosition(Transform origin, int spawnedIndex)
        {
            if (spawnRadius <= 0f)
                return origin.position;

            float angle = spawnedIndex * 137.508f * Mathf.Deg2Rad;
            float radius = spawnRadius * Mathf.Sqrt((spawnedIndex % 8 + 1) / 8f);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, 0.04f + spawnedIndex * 0.005f, Mathf.Sin(angle) * radius);
            return origin.position + origin.TransformDirection(offset);
        }

        private Quaternion GetSpawnRotation(Transform origin)
        {
            return origin.rotation * Quaternion.Euler(0f, UnityEngine.Random.Range(-25f, 25f), 0f);
        }

        private void PrepareSpawnedPhysics(GameObject instance, int spawnedIndex)
        {
            Rigidbody body = instance.GetComponent<Rigidbody>() ?? instance.GetComponentInChildren<Rigidbody>();
            if (body == null)
                return;

            body.isKinematic = false;
            body.detectCollisions = true;
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            if (spawnImpulse <= 0f)
                return;

            Vector3 direction = (transform.up + transform.right * (((spawnedIndex % 2) * 2f) - 1f) * 0.2f).normalized;
            body.AddForce(direction * spawnImpulse, ForceMode.VelocityChange);
        }
    }

    public sealed class MedicalContainerStack : MonoBehaviour
    {
        public MedicalContainerStack(IntPtr pointer) : base(pointer)
        {
        }

        public int quantity = 1;
        public string itemDisplayName = "Medical Item";
        public MedicalContainer sourceContainer = null!;

        public int Quantity => Mathf.Max(0, quantity);
        public string ItemDisplayName => string.IsNullOrWhiteSpace(itemDisplayName) ? "Medical Item" : itemDisplayName;

        public bool TryTakeOne()
        {
            if (quantity <= 0)
                return false;

            quantity--;
            return true;
        }
    }

    public static class ZBCMedicalRuntimeTypeRegistration
    {
        private static bool _registered;

        public static void Register()
        {
            if (_registered)
                return;

            ClassInjector.RegisterTypeInIl2Cpp<ZBCMedicalItem>();
            ClassInjector.RegisterTypeInIl2Cpp<MedicalContainer>();
            ClassInjector.RegisterTypeInIl2Cpp<MedicalContainerStack>();
            _registered = true;
        }
    }
}
