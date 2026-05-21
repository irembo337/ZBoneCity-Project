using System;
using System.Collections.Generic;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Interaction;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class MedicalSystem
    {
        private sealed class MedicalWorldItem
        {
            public readonly MedicalItemType Type;
            public readonly GameObject GameObject;
            public float DwellSeconds;
            public bool Consumed;

            public MedicalWorldItem(MedicalItemType type, GameObject gameObject)
            {
                Type = type;
                GameObject = gameObject;
            }
        }

        private readonly List<MedicalWorldItem> _items = new List<MedicalWorldItem>(Config.MaxMedicalItems);
        private float _updateAccumulator;
        private float _spawnRetryAccumulator;
        private bool _spawnedForScene;

        public void OnSceneLoaded()
        {
            ClearWorldItems();
            _spawnedForScene = false;
            _spawnRetryAccumulator = 0f;
        }

        public void Update(float deltaTime, HealthManager? player)
        {
            if (!Config.Enabled)
                return;

            _updateAccumulator += deltaTime;
            if (_updateAccumulator < 0.12f)
                return;

            float elapsed = _updateAccumulator;
            _updateAccumulator = 0f;

            if (Config.SpawnMedicalItems && !_spawnedForScene)
            {
                _spawnRetryAccumulator += elapsed;
                if (_spawnRetryAccumulator >= 1.0f)
                {
                    _spawnRetryAccumulator = 0f;
                    TrySpawnDefaultItems();
                }
            }

            if (player == null || player.IsDead)
                return;

            Transform? head = MainMod.Runtime?.GetHeadTransform();
            if (head == null)
                return;

            Vector3 applyPoint = head.position + head.forward * 0.08f + Vector3.down * 0.12f;
            float applyDistanceSqr = Config.MedicalApplyDistance * Config.MedicalApplyDistance;
            for (int i = 0; i < _items.Count; i++)
            {
                MedicalWorldItem item = _items[i];
                if (item.Consumed || item.GameObject == null)
                    continue;

                float distanceSqr = (item.GameObject.transform.position - applyPoint).sqrMagnitude;
                if (distanceSqr <= applyDistanceSqr)
                {
                    item.DwellSeconds += elapsed;
                    if (item.DwellSeconds >= 0.45f)
                        ConsumeItem(item, player);
                }
                else
                {
                    item.DwellSeconds = Math.Max(0f, item.DwellSeconds - elapsed * 0.5f);
                }
            }
        }

        public void SpawnDefaultItemsAtPlayer()
        {
            TrySpawnDefaultItems();
        }

        public void ClearWorldItems()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (_items[i].GameObject != null)
                    UnityEngine.Object.Destroy(_items[i].GameObject);
            }

            _items.Clear();
        }

        public static void ApplyMedicalItem(HealthManager target, MedicalItemType type)
        {
            BodyPart part = target.Bleeding.HasActiveBleeding ? target.Bleeding.GetWorstBleedingPart() : target.GetMostInjuredLimb();

            switch (type)
            {
                case MedicalItemType.Bandage:
                    target.StopBleeding(part, BleedSeverity.Severe);
                    target.ReducePain(8f, 0f);
                    target.HealLimb(part, 8f);
                    break;
                case MedicalItemType.Tourniquet:
                    target.ApplyTourniquet(part);
                    target.ReducePain(3f, 0f);
                    break;
                case MedicalItemType.Morphine:
                    target.ReducePain(55f, 90f);
                    break;
                case MedicalItemType.Medkit:
                    ApplyMedkit(target);
                    break;
                case MedicalItemType.Adrenaline:
                    target.ApplyAdrenaline(45f);
                    target.ReducePain(12f, 0f);
                    break;
                case MedicalItemType.Splint:
                    target.StabilizeFracture(part, 1.0f);
                    target.ReducePain(6f, 0f);
                    break;
                case MedicalItemType.BloodPack:
                    target.RestoreBlood(1100f);
                    target.ReducePain(4f, 0f);
                    break;
            }

            target.NotifyMedicalApplied(type);
        }

        private static void ApplyMedkit(HealthManager target)
        {
            target.RestoreBlood(650f);
            target.Organs.HealInternal(10f);
            target.Lungs.Treat(0.35f);
            for (int i = 0; i < Config.LimbCount; i++)
            {
                BodyPart part = (BodyPart)i;
                target.HealLimb(part, Config.LimbMaxHp[i] * 0.22f);
                target.StabilizeFracture(part, 0.85f);
                target.StopBleeding(part, BleedSeverity.Medium);
            }

            target.ReducePain(28f, 20f);
        }

        private void TrySpawnDefaultItems()
        {
            if (_spawnedForScene || _items.Count > 0)
            {
                _spawnedForScene = true;
                return;
            }

            Transform? head = MainMod.Runtime?.GetHeadTransform();
            if (head == null)
                return;

            Vector3 forward = head.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 center = head.position + forward * Config.MedicalSpawnDistance + Vector3.down * 0.55f;
            SpawnItem(MedicalItemType.Bandage, center - right * 0.66f + Vector3.up * 0.08f);
            SpawnItem(MedicalItemType.Tourniquet, center - right * 0.44f + Vector3.up * 0.08f);
            SpawnItem(MedicalItemType.Morphine, center - right * 0.22f + Vector3.up * 0.08f);
            SpawnItem(MedicalItemType.Adrenaline, center + Vector3.up * 0.08f);
            SpawnItem(MedicalItemType.Splint, center + right * 0.24f + Vector3.up * 0.08f);
            SpawnItem(MedicalItemType.BloodPack, center + right * 0.48f + Vector3.up * 0.08f);
            SpawnItem(MedicalItemType.Medkit, center + right * 0.74f + Vector3.up * 0.08f);
            _spawnedForScene = true;
        }

        private void SpawnItem(MedicalItemType type, Vector3 position)
        {
            if (_items.Count >= Config.MaxMedicalItems)
                return;

            PrimitiveType primitive = type == MedicalItemType.Morphine || type == MedicalItemType.Tourniquet || type == MedicalItemType.Adrenaline
                ? PrimitiveType.Cylinder
                : PrimitiveType.Cube;
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = "AHS_Medical_" + Config.GetMedicalLabel(type);
            go.transform.position = position;
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = GetItemScale(type);

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = Config.GetMedicalColor(type);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = type == MedicalItemType.Medkit ? 0.65f : 0.18f;
            rb.drag = 0.2f;
            rb.angularDrag = 0.15f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            TryConfigureMarrowGrip(go, rb);
            AddLabel(go, type);
            _items.Add(new MedicalWorldItem(type, go));
        }

        private static Vector3 GetItemScale(MedicalItemType type)
        {
            switch (type)
            {
                case MedicalItemType.Bandage:
                    return new Vector3(0.18f, 0.055f, 0.09f);
                case MedicalItemType.Tourniquet:
                    return new Vector3(0.055f, 0.20f, 0.055f);
                case MedicalItemType.Morphine:
                    return new Vector3(0.045f, 0.22f, 0.045f);
                case MedicalItemType.Medkit:
                    return new Vector3(0.24f, 0.13f, 0.18f);
                case MedicalItemType.Adrenaline:
                    return new Vector3(0.045f, 0.21f, 0.045f);
                case MedicalItemType.Splint:
                    return new Vector3(0.07f, 0.34f, 0.045f);
                case MedicalItemType.BloodPack:
                    return new Vector3(0.17f, 0.12f, 0.05f);
                default:
                    return Vector3.one * 0.1f;
            }
        }

        private static void TryConfigureMarrowGrip(GameObject go, Rigidbody rb)
        {
            try
            {
                MarrowEntity entity = go.AddComponent<MarrowEntity>();
                MarrowBody body = go.AddComponent<MarrowBody>();
                body.Validate(rb, entity);
                BoxGrip grip = go.AddComponent<BoxGrip>();
                grip.isThrowable = true;
                grip.radius = 0.28f;
                grip.gripDistance = 0.34f;
                grip.canBeFaceGrabbed = true;
                grip.canBeEdgeGrabbed = true;
                grip.canBeCornerGrabbed = true;
                entity.Validate();
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("Marrow grip setup failed for medical item: " + ex.Message);
            }
        }

        private static void AddLabel(GameObject parent, MedicalItemType type)
        {
            GameObject label = new GameObject("AHS_Label");
            label.transform.SetParent(parent.transform, false);
            label.transform.localPosition = new Vector3(0f, 0.075f, 0f);
            label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            TextMesh text = label.AddComponent<TextMesh>();
            text.text = Config.GetMedicalLabel(type);
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.characterSize = 0.035f;
            text.fontSize = 42;
            text.color = type == MedicalItemType.Tourniquet ? Color.white : Color.black;
        }

        private void ConsumeItem(MedicalWorldItem item, HealthManager player)
        {
            if (item.Consumed)
                return;

            item.Consumed = true;
            ApplyMedicalItem(player, item.Type);
            if (item.GameObject != null)
                UnityEngine.Object.Destroy(item.GameObject);
        }
    }
}
