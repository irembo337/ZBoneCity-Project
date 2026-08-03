using System;
using System.Collections.Generic;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using UnityEngine;
using UnityEngine.XR;
using MarrowHand = Il2CppSLZ.Marrow.Hand;
using PlayerBodyPart = Il2CppSLZ.Marrow.PlayerDamageReceiver.BodyPart;

namespace BonelabAdvancedHealth
{
    public sealed class MedicalSystem
    {
        private const float UseCooldownSeconds = 0.18f;
        private const float HandNearDistance = 0.42f;
        private const float HandDirectTouchDistance = 0.18f;
        private const float BodyProbeRadius = 0.145f;
        private const int MaxBodyHits = 24;
        private const int MaxParentDepth = 5;

        private readonly Collider[] _bodyHits = new Collider[MaxBodyHits];
        private readonly List<GameObject> _spawnedRuntimeItems = new List<GameObject>(16);
        private Rigidbody[] _cachedRigidbodies = Array.Empty<Rigidbody>();
        private bool _lastUsePressed;
        private bool _bodyScanFailureLogged;
        private float _useCooldown;
        private float _nextBodyScanTime;

        public void OnSceneLoaded()
        {
            ClearSpawnedRuntimeItems();
            _lastUsePressed = false;
            _useCooldown = 0f;
            _cachedRigidbodies = Array.Empty<Rigidbody>();
            _nextBodyScanTime = 0f;
            _bodyScanFailureLogged = false;
        }

        public void Update(float deltaTime, HealthManager? player)
        {
            if (!Config.Enabled || player == null || player.IsDead)
                return;

            MainMod? runtime = MainMod.Runtime;
            if (runtime == null || !runtime.IsPlayerRigReady() || !runtime.CanRunPlayerTrauma)
                return;

            if (_useCooldown > 0f)
                _useCooldown = Math.Max(0f, _useCooldown - deltaTime);

            bool usePressed = IsUsePressed();
            bool pressedThisFrame = usePressed && !_lastUsePressed;
            _lastUsePressed = usePressed;
            if (MedicalItemRuntimeConfigurator.TriggerDropConsumedThisFrame)
                return;
            if (!pressedThisFrame || _useCooldown > 0f)
                return;

            _useCooldown = UseCooldownSeconds;
            TryUseHeldMedicalItem(player);
        }

        public void SpawnDefaultItemsAtPlayer()
        {
            SpawnMedicalItemAtPlayer(MedicalItemType.Medkit);
        }

        public void ClearWorldItems()
        {
            ClearSpawnedRuntimeItems();
            _lastUsePressed = false;
            _useCooldown = 0f;
        }

        public bool SpawnMedicalItemAtPlayer(MedicalItemType type)
        {
            try
            {
                MainMod? runtime = MainMod.Runtime;
                Transform? head = runtime?.GetHeadTransform();
                if (head == null)
                    return false;

                Vector3 forward = head.forward;
                forward.y = 0f;
                if (forward.sqrMagnitude < 0.001f)
                    forward = head.forward;
                forward.Normalize();

                Vector3 spawnPosition = head.position + forward * Config.MedicalSpawnDistance + Vector3.down * 0.18f;
                Quaternion spawnRotation = Quaternion.LookRotation(forward, Vector3.up);
                GameObject item = GameObject.CreatePrimitive(PrimitiveType.Cube);
                item.name = "ZBC_Medical_" + GetRuntimeItemName(type);
                item.transform.position = spawnPosition;
                item.transform.rotation = spawnRotation;
                item.transform.localScale = GetRuntimeItemScale(type);

                Renderer renderer = item.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.material.color = Config.GetMedicalColor(type);

                Rigidbody rb = item.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.mass = GetRuntimeItemMass(type);

                GameObject marker = new GameObject("ZBC_ItemType_" + GetRuntimeItemName(type));
                marker.transform.SetParent(item.transform, false);
                marker.transform.localPosition = Vector3.zero;
                marker.transform.localRotation = Quaternion.identity;
                marker.transform.localScale = Vector3.one;

                _spawnedRuntimeItems.Add(item);
                _cachedRigidbodies = Array.Empty<Rigidbody>();
                _nextBodyScanTime = 0f;
                runtime?.Logger.Msg("[ZBC] Spawned runtime " + GetRuntimeItemName(type) + " in front of player.");
                return true;
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Failed to spawn runtime medical item: " + ex.Message);
                return false;
            }
        }

        private void ClearSpawnedRuntimeItems()
        {
            for (int i = _spawnedRuntimeItems.Count - 1; i >= 0; i--)
            {
                GameObject item = _spawnedRuntimeItems[i];
                if (item != null)
                    UnityEngine.Object.Destroy(item);
            }

            _spawnedRuntimeItems.Clear();
        }

        public static void ApplyMedicalItem(HealthManager target, MedicalItemType type)
        {
            BodyPart part = ChooseBestTreatmentPart(target, type);
            ApplyMedicalItem(target, type, part, out _);
        }

        public static bool ApplyMedicalItem(HealthManager target, MedicalItemType type, BodyPart part, out string feedback)
        {
            feedback = string.Empty;
            if (target == null || target.IsDead)
            {
                feedback = "Treatment Failed";
                return false;
            }

            switch (type)
            {
                case MedicalItemType.Bandage:
                    return ApplyBandage(target, part, out feedback);
                case MedicalItemType.Tourniquet:
                    return ApplyTourniquet(target, part, out feedback);
                case MedicalItemType.Splint:
                    return ApplySplint(target, part, out feedback);
                case MedicalItemType.Morphine:
                    return ApplyMorphine(target, part, out feedback);
                case MedicalItemType.Painkillers:
                    target.ReducePain(26f, 0f);
                    target.NotifyMedicalApplied(type, part);
                    feedback = "Painkillers Used";
                    return true;
                case MedicalItemType.Medkit:
                    return ApplyMedkit(target, out feedback);
                case MedicalItemType.Adrenaline:
                    return ApplyAdrenaline(target, part, out feedback);
                case MedicalItemType.ETGStimulator:
                    return ApplyEtgStimulator(target, part, out feedback);
                case MedicalItemType.SJ1Stimulator:
                    return ApplySj1Stimulator(target, part, out feedback);
                case MedicalItemType.BloodPack:
                    return ApplyBloodPack(target, part, out feedback);
                default:
                    feedback = "Invalid Treatment";
                    return false;
            }
        }

        private void TryUseHeldMedicalItem(HealthManager player)
        {
            if (!TryFindHeldMedicalItem(out GameObject? itemRoot, out MedicalItemType itemType))
            {
                TryPerformManualResuscitation(player);
                return;
            }
            if (itemRoot == null)
                return;

            if (TryOpenMedicalContainer(itemRoot, out string containerFeedback))
            {
                ShowFeedback(containerFeedback);
                ConsumeItem(itemRoot);
                return;
            }

            if (!TryDetectTreatmentTarget(itemRoot, player, out HealthManager target, out BodyPart touchedPart))
            {
                ShowFeedback("Invalid Treatment Area");
                return;
            }

            if (!ApplyMedicalItem(target, itemType, touchedPart, out string feedback))
            {
                ShowFeedback(feedback);
                return;
            }

            ShowFeedback(feedback);
            ConsumeItem(itemRoot);
        }

        private bool TryOpenMedicalContainer(GameObject itemRoot, out string feedback)
        {
            feedback = string.Empty;
            if (itemRoot == null)
                return false;

            if (TryGetBuiltInContainerContents(itemRoot.name, out string builtInName, out ContainerDrop[] builtInContents))
            {
                SpawnContainerContents(builtInContents);
                feedback = builtInName + " Opened";
                return true;
            }

            Component[] components;
            try
            {
                components = itemRoot.GetComponentsInChildren<Component>(true);
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("[ZBC] Could not scan medical container components: " + ex.Message);
                return false;
            }

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                Type type = component.GetType();
                if (!string.Equals(type.FullName, "ZBoneCity.Medical.MedicalContainer", StringComparison.Ordinal))
                    continue;

                try
                {
                    string containerName = TryGetContainerName(component, type);
                    object? canOpenValue = type.GetMethod("CanOpen", Type.EmptyTypes)?.Invoke(component, null);
                    if (canOpenValue is bool canOpen && !canOpen)
                    {
                        feedback = containerName + " Empty";
                        return true;
                    }

                    object? openedValue = type.GetMethod("OpenContainer", Type.EmptyTypes)?.Invoke(component, null);
                    if (openedValue is bool opened && opened)
                    {
                        feedback = containerName + " Opened";
                        return true;
                    }

                    feedback = containerName + " Empty";
                    return true;
                }
                catch (Exception ex)
                {
                    MainMod.Runtime?.Logger.Warning("[ZBC] Medical container open failed: " + ex.Message);
                    feedback = "Container Failed";
                    return true;
                }
            }

            return false;
        }

        private void SpawnContainerContents(ContainerDrop[] contents)
        {
            for (int i = 0; i < contents.Length; i++)
            {
                ContainerDrop drop = contents[i];
                int count = Math.Max(0, drop.Quantity);
                for (int itemIndex = 0; itemIndex < count; itemIndex++)
                    SpawnMedicalItemAtPlayer(drop.Type);
            }
        }

        private static bool TryGetBuiltInContainerContents(string objectName, out string containerName, out ContainerDrop[] contents)
        {
            containerName = string.Empty;
            contents = Array.Empty<ContainerDrop>();
            string lower = objectName == null ? string.Empty : objectName.ToLowerInvariant();

            if (lower.Contains("salewa"))
            {
                containerName = "Salewa";
                contents = new[]
                {
                    new ContainerDrop(MedicalItemType.Bandage, 2),
                    new ContainerDrop(MedicalItemType.Tourniquet, 1),
                    new ContainerDrop(MedicalItemType.Painkillers, 1),
                    new ContainerDrop(MedicalItemType.Morphine, 1)
                };
                return true;
            }

            if (lower.Contains("ifak"))
            {
                containerName = "IFAK";
                contents = new[]
                {
                    new ContainerDrop(MedicalItemType.Bandage, 2),
                    new ContainerDrop(MedicalItemType.Tourniquet, 1),
                    new ContainerDrop(MedicalItemType.Painkillers, 1)
                };
                return true;
            }

            if (lower.Contains("grizzly"))
            {
                containerName = "Grizzly Medkit";
                contents = new[]
                {
                    new ContainerDrop(MedicalItemType.Bandage, 4),
                    new ContainerDrop(MedicalItemType.Tourniquet, 2),
                    new ContainerDrop(MedicalItemType.Splint, 2),
                    new ContainerDrop(MedicalItemType.Morphine, 2),
                    new ContainerDrop(MedicalItemType.Adrenaline, 1),
                    new ContainerDrop(MedicalItemType.ETGStimulator, 1),
                    new ContainerDrop(MedicalItemType.SJ1Stimulator, 1),
                    new ContainerDrop(MedicalItemType.Painkillers, 2)
                };
                return true;
            }

            if (lower.Contains("auto") && lower.Contains("medkit"))
            {
                containerName = "Auto Medkit";
                contents = new[]
                {
                    new ContainerDrop(MedicalItemType.Bandage, 2),
                    new ContainerDrop(MedicalItemType.Tourniquet, 1),
                    new ContainerDrop(MedicalItemType.Painkillers, 1),
                    new ContainerDrop(MedicalItemType.Adrenaline, 1)
                };
                return true;
            }

            if (lower.Contains("surgical") || lower.Contains("core_medical"))
            {
                containerName = "Surgical Medical Kit";
                contents = new[]
                {
                    new ContainerDrop(MedicalItemType.Splint, 3),
                    new ContainerDrop(MedicalItemType.Bandage, 2),
                    new ContainerDrop(MedicalItemType.Tourniquet, 1),
                    new ContainerDrop(MedicalItemType.Morphine, 1),
                    new ContainerDrop(MedicalItemType.Painkillers, 1)
                };
                return true;
            }

            if (lower.Contains("medkit"))
            {
                containerName = "Medkit";
                contents = new[]
                {
                    new ContainerDrop(MedicalItemType.Bandage, 2),
                    new ContainerDrop(MedicalItemType.Painkillers, 1),
                    new ContainerDrop(MedicalItemType.Morphine, 1)
                };
                return true;
            }

            return false;
        }

        private static string TryGetContainerName(Component component, Type type)
        {
            try
            {
                object? value = type.GetProperty("ContainerName")?.GetValue(component, null);
                if (value is string name && !string.IsNullOrWhiteSpace(name))
                    return name;
            }
            catch (Exception)
            {
                // The display name is cosmetic; fallback keeps gameplay flowing.
            }

            return "Medical Container";
        }

        private bool TryFindHeldMedicalItem(out GameObject? itemRoot, out MedicalItemType itemType)
        {
            itemRoot = null;
            itemType = MedicalItemType.Bandage;

            if (!TryGetHandState(out HandState leftHand, out HandState rightHand))
                return false;

            Rigidbody[] bodies = GetCachedRigidbodies();
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (rb == null || rb.gameObject == null)
                    continue;

                if (!TryResolveMedicalRoot(rb.transform, out GameObject? root, out MedicalItemType type))
                    continue;
                if (root == null)
                    continue;

                float distance = GetHeldDistance(root.transform.position, leftHand, rightHand, out bool held);
                if (!held || distance >= bestDistance)
                    continue;

                bestDistance = distance;
                itemRoot = root;
                itemType = type;
            }

            return itemRoot != null;
        }

        private Rigidbody[] GetCachedRigidbodies()
        {
            float now = Time.unscaledTime;
            if (_cachedRigidbodies.Length > 0 && now < _nextBodyScanTime)
                return _cachedRigidbodies;

            try
            {
                // SDK-built items expose different grip components, but every physical item still has a Rigidbody.
                _cachedRigidbodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                _nextBodyScanTime = now + 0.75f;
                _bodyScanFailureLogged = false;
            }
            catch (Exception ex)
            {
                _cachedRigidbodies = Array.Empty<Rigidbody>();
                _nextBodyScanTime = now + 2.0f;
                if (!_bodyScanFailureLogged)
                {
                    _bodyScanFailureLogged = true;
                    MainMod.Runtime?.Logger.Warning("Medical item scan failed safely: " + ex.Message);
                }
            }

            return _cachedRigidbodies;
        }

        private bool TryDetectTreatmentTarget(GameObject itemRoot, HealthManager fallbackPlayer, out HealthManager target, out BodyPart part)
        {
            target = fallbackPlayer;
            part = BodyPart.Torso;
            if (itemRoot == null)
                return false;

            Transform item = itemRoot.transform;
            if (TryDetectTreatmentTargetAtPoint(GetTreatmentProbePoint(item), item, fallbackPlayer, out target, out part))
                return true;
            if (TryDetectTreatmentTargetAtPoint(item.position, item, fallbackPlayer, out target, out part))
                return true;
            if (TryDetectTreatmentTargetAtPoint(item.position - item.forward.normalized * 0.055f, item, fallbackPlayer, out target, out part))
                return true;
            if (TryDetectTreatmentTargetAtPoint(item.position + item.up.normalized * 0.045f, item, fallbackPlayer, out target, out part))
                return true;

            return false;
        }

        private bool TryDetectTreatmentTargetAtPoint(Vector3 probePoint, Transform? itemRoot, HealthManager fallbackPlayer, out HealthManager target, out BodyPart part)
        {
            target = fallbackPlayer;
            part = BodyPart.Torso;
            int hitCount = Physics.OverlapSphereNonAlloc(probePoint, BodyProbeRadius, _bodyHits, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < hitCount && i < _bodyHits.Length; i++)
            {
                Collider hit = _bodyHits[i];
                if (hit == null || hit.transform == null || itemRoot != null && hit.transform.IsChildOf(itemRoot))
                    continue;

                PlayerDamageReceiver receiver = hit.GetComponentInParent<PlayerDamageReceiver>();
                if (receiver != null)
                {
                    part = DamageProcessor.MapPlayerPart(receiver.bodyPart);
                    target = fallbackPlayer;
                    ClearHitBuffer(hitCount);
                    return true;
                }

                EnemyDamageReceiver enemyReceiver = hit.GetComponentInParent<EnemyDamageReceiver>();
                if (enemyReceiver == null)
                    continue;

                Enemy_Health enemyHealth = enemyReceiver.e_health;
                if (enemyHealth == null)
                    enemyHealth = enemyReceiver.GetComponentInParent<Enemy_Health>();
                NPCHealth? npc = enemyHealth != null ? MainMod.Runtime?.GetOrCreateNpcManager(enemyHealth) : null;
                if (npc == null)
                    continue;

                part = DamageProcessor.MapEnemyPart(enemyReceiver.bodyPart);
                target = npc;
                ClearHitBuffer(hitCount);
                return true;
            }

            ClearHitBuffer(hitCount);
            return false;
        }

        private void TryPerformManualResuscitation(HealthManager fallbackPlayer)
        {
            if (!TryGetHandState(out HandState leftHand, out HandState rightHand))
                return;
            if (!leftHand.Valid || !rightHand.Valid || leftHand.HasAttachedObject || rightHand.HasAttachedObject)
                return;

            if (!TryDetectTreatmentTargetAtPoint(leftHand.Position, null, fallbackPlayer, out HealthManager leftTarget, out BodyPart leftPart))
                return;
            if (!TryDetectTreatmentTargetAtPoint(rightHand.Position, null, fallbackPlayer, out HealthManager rightTarget, out BodyPart rightPart))
                return;
            if (!ReferenceEquals(leftTarget, rightTarget))
                return;
            if (leftPart != BodyPart.Torso && rightPart != BodyPart.Torso)
                return;

            if (leftTarget.Kind == HealthOwnerKind.Player)
                return;

            if (!leftTarget.ApplyManualResuscitation(out string feedback))
            {
                ShowFeedback(feedback);
                return;
            }

            ShowFeedback(feedback);
        }

        private static bool ApplyBandage(HealthManager target, BodyPart part, out string feedback)
        {
            if (!MedicalInspectionSystem.IsLimb(part) && part != BodyPart.Head && part != BodyPart.Torso)
            {
                feedback = "Cannot Apply Bandage Here";
                return false;
            }

            BleedSeverity severity = target.Bleeding.GetWorstBleedingSeverity(part);
            if (severity == BleedSeverity.None)
            {
                feedback = "No Bleeding Detected";
                return false;
            }

            float strength = severity == BleedSeverity.Arterial && part == BodyPart.Head ? 1.22f :
                             severity >= BleedSeverity.Severe ? 0.92f : 1.1f;
            float seconds = part == BodyPart.Head ? 20f : part == BodyPart.Torso ? 16f : 13f;
            target.ApplyBandage(part, seconds, strength);
            target.ReducePain(7f, 0f);
            target.HealLimb(part, 5f);
            target.NotifyMedicalApplied(MedicalItemType.Bandage, part);
            if (severity == BleedSeverity.Arterial && part == BodyPart.Head)
                feedback = "Pressure Bandage Applied - Neck Bleeding Slowed";
            else if (severity >= BleedSeverity.Severe)
                feedback = "Bandage Applied - Blood Pack Recommended";
            else if (severity == BleedSeverity.Medium)
                feedback = "Bandage Applied - Monitor Bleeding";
            else
                feedback = "Bandage Applied";
            return true;
        }

        private static bool ApplyTourniquet(HealthManager target, BodyPart part, out string feedback)
        {
            if (!MedicalInspectionSystem.IsLimb(part))
            {
                feedback = "Cannot Apply Tourniquet Here";
                return false;
            }

            BleedSeverity severity = target.Bleeding.GetWorstBleedingSeverity(part);
            if (severity < BleedSeverity.Severe)
            {
                feedback = severity == BleedSeverity.None ? "No Bleeding Detected" : "Bleeding Too Light For Tourniquet";
                return false;
            }

            target.ApplyTourniquet(part);
            target.ReducePain(3f, 0f);
            target.NotifyMedicalApplied(MedicalItemType.Tourniquet, part);
            feedback = "Tourniquet Applied";
            return true;
        }

        private static bool ApplySplint(HealthManager target, BodyPart part, out string feedback)
        {
            if (!MedicalInspectionSystem.IsLimb(part))
            {
                feedback = "Cannot Apply Splint Here";
                return false;
            }

            LimbHealth limb = target.GetLimb(part);
            if (limb.Fracture == FractureState.None)
            {
                feedback = "No Fracture Detected";
                return false;
            }

            target.RepairFracture(part);
            target.HealLimb(part, limb.MaxHp * 0.10f);
            target.ReducePain(10f, 0f);
            target.NotifyMedicalApplied(MedicalItemType.Splint, part);
            feedback = MedicalInspectionSystem.GetPartLabel(part) + " Splinted";
            return true;
        }

        private static bool ApplyMorphine(HealthManager target, BodyPart part, out string feedback)
        {
            if (!MedicalInspectionSystem.IsArm(part))
            {
                feedback = "Inject Into Arm";
                return false;
            }

            target.ReducePain(55f, 90f);
            target.NotifyMedicalApplied(MedicalItemType.Morphine, part);
            feedback = "Morphine Injected";
            return true;
        }

        private static bool ApplyAdrenaline(HealthManager target, BodyPart part, out string feedback)
        {
            if (!MedicalInspectionSystem.IsArm(part))
            {
                feedback = "Inject Into Arm";
                return false;
            }

            target.ApplyAdrenaline(45f);
            target.ReducePain(10f, 0f);
            target.NotifyMedicalApplied(MedicalItemType.Adrenaline, part);
            if (target.Consciousness.State == ConsciousnessState.Unconscious && !target.Coma.IsActive && target.Bleeding.BloodVolumeMl > Config.CriticalBloodMl + 250f)
                target.Consciousness.BeginStandUpRecovery(9f);
            feedback = "Adrenaline Injected";
            return true;
        }

        private static bool ApplyEtgStimulator(HealthManager target, BodyPart part, out string feedback)
        {
            if (!MedicalInspectionSystem.IsArm(part))
            {
                feedback = "Inject Into Arm";
                return false;
            }

            target.ReducePain(34f, 0f);
            target.NotifyMedicalApplied(MedicalItemType.ETGStimulator, part);
            feedback = "ETG Stimulator Injected";
            return true;
        }

        private static bool ApplySj1Stimulator(HealthManager target, BodyPart part, out string feedback)
        {
            if (!MedicalInspectionSystem.IsArm(part))
            {
                feedback = "Inject Into Arm";
                return false;
            }

            target.ReducePain(42f, 0f);
            target.NotifyMedicalApplied(MedicalItemType.SJ1Stimulator, part);
            feedback = "SJ1 Stimulator Injected";
            return true;
        }

        private static bool ApplyMedkit(HealthManager target, out string feedback)
        {
            bool needsTreatment = target.Bleeding.HasActiveBleeding ||
                                  target.Pain > 8f ||
                                  target.Lungs.OxygenNormalized < 0.98f ||
                                  HasDamagedLimb(target);
            if (!needsTreatment)
            {
                feedback = "No Treatment Needed";
                return false;
            }

            BodyPart priorityPart = ChooseBestTreatmentPart(target, MedicalItemType.Medkit);
            BleedSeverity priorityBleed = target.Bleeding.GetWorstBleedingSeverity(priorityPart);
            if (priorityBleed != BleedSeverity.None)
                target.ApplyBandage(priorityPart, priorityBleed >= BleedSeverity.Severe ? 18f : 24f, priorityBleed >= BleedSeverity.Severe ? 0.58f : 0.95f);

            target.Organs.HealInternal(6f);
            target.Lungs.Treat(0.18f);
            for (int i = 0; i < Config.LimbCount; i++)
            {
                BodyPart part = (BodyPart)i;
                target.HealLimb(part, Config.LimbMaxHp[i] * 0.12f);
                target.StopBleeding(part, BleedSeverity.Light);
            }

            target.ReducePain(18f, 0f);
            target.NotifyMedicalApplied(MedicalItemType.Medkit, BodyPart.Torso);
            feedback = target.Bleeding.HasActiveBleeding ? "Medkit Applied - Further Treatment Needed" : "Medkit Applied";
            return true;
        }

        private static bool ApplyBloodPack(HealthManager target, BodyPart part, out string feedback)
        {
            if (part != BodyPart.Torso && !MedicalInspectionSystem.IsArm(part))
            {
                feedback = "Use Blood Pack On Arm Or Torso";
                return false;
            }

            if (target.Bleeding.BloodVolumeMl >= Config.BloodVolumeMl - 75f)
            {
                feedback = "Blood Volume Stable";
                return false;
            }

            target.RestoreBlood(850f);
            target.ReducePain(4f, 0f);
            target.NotifyMedicalApplied(MedicalItemType.BloodPack, part);
            feedback = "Blood Pack Used";
            return true;
        }

        private static BodyPart ChooseBestTreatmentPart(HealthManager target, MedicalItemType type)
        {
            switch (type)
            {
                case MedicalItemType.Bandage:
                case MedicalItemType.Tourniquet:
                    return target.Bleeding.HasActiveBleeding ? target.Bleeding.GetWorstBleedingPart() : target.GetMostInjuredLimb();
                case MedicalItemType.Splint:
                    return GetWorstBrokenLimb(target);
                case MedicalItemType.Morphine:
                case MedicalItemType.Adrenaline:
                case MedicalItemType.ETGStimulator:
                case MedicalItemType.SJ1Stimulator:
                case MedicalItemType.BloodPack:
                    return BodyPart.LeftArm;
                case MedicalItemType.Medkit:
                    return target.Bleeding.HasActiveBleeding ? target.Bleeding.GetWorstBleedingPart() : target.GetMostInjuredLimb();
                default:
                    return target.GetMostInjuredLimb();
            }
        }

        private static BodyPart GetWorstBrokenLimb(HealthManager target)
        {
            BodyPart best = BodyPart.LeftArm;
            float bestScore = -1f;
            for (int i = (int)BodyPart.LeftArm; i <= (int)BodyPart.RightLeg; i++)
            {
                BodyPart part = (BodyPart)i;
                LimbHealth limb = target.GetLimb(part);
                float score = limb.Fracture == FractureState.None ? -1f : limb.DamagePercent + (int)limb.Fracture * 0.35f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = part;
                }
            }

            return best;
        }

        private static bool HasDamagedLimb(HealthManager target)
        {
            for (int i = 0; i < Config.LimbCount; i++)
            {
                LimbHealth limb = target.GetLimb((BodyPart)i);
                if (limb.DamagePercent > 0.08f || limb.Fracture != FractureState.None)
                    return true;
            }

            return false;
        }

        private static bool TryGetHandState(out HandState leftHand, out HandState rightHand)
        {
            leftHand = HandState.Invalid;
            rightHand = HandState.Invalid;
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                if (physicsRig == null)
                    return false;

                leftHand = GetHandState(physicsRig.leftHand);
                rightHand = GetHandState(physicsRig.rightHand);
                return leftHand.Valid || rightHand.Valid;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static HandState GetHandState(MarrowHand hand)
        {
            if (hand == null)
                return HandState.Invalid;

            bool attached = false;
            try
            {
                attached = hand.HasAttachedObject();
            }
            catch (Exception)
            {
                attached = false;
            }

            return new HandState(true, hand.transform.position, attached);
        }

        private static float GetHeldDistance(Vector3 itemPosition, HandState leftHand, HandState rightHand, out bool held)
        {
            held = false;
            float best = float.PositiveInfinity;
            EvaluateHand(itemPosition, leftHand, ref best, ref held);
            EvaluateHand(itemPosition, rightHand, ref best, ref held);
            return best;
        }

        private static void EvaluateHand(Vector3 itemPosition, HandState hand, ref float best, ref bool held)
        {
            if (!hand.Valid)
                return;

            float distance = Vector3.Distance(itemPosition, hand.Position);
            if (distance < best)
                best = distance;

            if ((hand.HasAttachedObject && distance <= HandNearDistance) || distance <= HandDirectTouchDistance)
                held = true;
        }

        private static Vector3 GetTreatmentProbePoint(Transform item)
        {
            Vector3 forward = item.forward;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            return item.position + forward.normalized * 0.055f;
        }

        private static bool TryResolveMedicalRoot(Transform start, out GameObject? root, out MedicalItemType type)
        {
            root = null;
            type = MedicalItemType.Bandage;
            Transform? current = start;
            for (int depth = 0; depth < MaxParentDepth && current != null; depth++)
            {
                if (TryGetMedicalTypeFromName(current.name, out type))
                {
                    root = current.gameObject;
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool TryGetMedicalTypeFromName(string name, out MedicalItemType type)
        {
            type = MedicalItemType.Bandage;
            if (string.IsNullOrEmpty(name))
                return false;

            string lower = name.ToLowerInvariant();
            if (TryGetKnownMedicalType(lower, out type))
                return true;
            if (lower.IndexOf("bandage", StringComparison.Ordinal) >= 0 || lower.IndexOf("бинт", StringComparison.Ordinal) >= 0)
            {
                type = MedicalItemType.Bandage;
                return true;
            }

            if (lower.IndexOf("tourniquet", StringComparison.Ordinal) >= 0 || lower.IndexOf("турникет", StringComparison.Ordinal) >= 0)
            {
                type = MedicalItemType.Tourniquet;
                return true;
            }

            if (lower.IndexOf("splint", StringComparison.Ordinal) >= 0 || lower.IndexOf("шина", StringComparison.Ordinal) >= 0)
            {
                type = MedicalItemType.Splint;
                return true;
            }

            if (lower.IndexOf("morphine", StringComparison.Ordinal) >= 0)
            {
                type = MedicalItemType.Morphine;
                return true;
            }

            if (lower.IndexOf("adrenaline", StringComparison.Ordinal) >= 0 || lower.IndexOf("адреналин", StringComparison.Ordinal) >= 0)
            {
                type = MedicalItemType.Adrenaline;
                return true;
            }

            if (lower.IndexOf("painkiller", StringComparison.Ordinal) >= 0)
            {
                type = MedicalItemType.Painkillers;
                return true;
            }

            if (lower.IndexOf("bloodpack", StringComparison.Ordinal) >= 0 || lower.IndexOf("blood_pack", StringComparison.Ordinal) >= 0 || lower.IndexOf("bloodbag", StringComparison.Ordinal) >= 0)
            {
                type = MedicalItemType.BloodPack;
                return true;
            }

            if (lower.IndexOf("medkit", StringComparison.Ordinal) >= 0 || lower.IndexOf("medcit", StringComparison.Ordinal) >= 0 || lower.IndexOf("salewa", StringComparison.Ordinal) >= 0)
            {
                type = MedicalItemType.Medkit;
                return true;
            }

            return false;
        }

        private static bool TryGetKnownMedicalType(string lowerName, out MedicalItemType type)
        {
            type = MedicalItemType.Bandage;
            if (string.IsNullOrEmpty(lowerName))
                return false;

            if (lowerName.Contains("bandage") || lowerName.Contains("military") || lowerName.Contains("army"))
                type = MedicalItemType.Bandage;
            else if (lowerName.Contains("tourniquet"))
                type = MedicalItemType.Tourniquet;
            else if (lowerName.Contains("splint") || lowerName.Contains("alusplint") || lowerName.Contains("aluminum") || lowerName.Contains("survival") || lowerName.Contains("rollup"))
                type = MedicalItemType.Splint;
            else if (lowerName.Contains("surgical") || lowerName.Contains("core_medical"))
                type = MedicalItemType.Splint;
            else if (lowerName.Contains("morphine"))
                type = MedicalItemType.Morphine;
            else if (lowerName.Contains("etg"))
                type = MedicalItemType.ETGStimulator;
            else if (lowerName.Contains("sj1") || lowerName.Contains("sj6"))
                type = MedicalItemType.SJ1Stimulator;
            else if (lowerName.Contains("adrenaline") || lowerName.Contains("zagustin") || lowerName.Contains("stim") || lowerName.Contains("epipen"))
                type = MedicalItemType.Adrenaline;
            else if (lowerName.Contains("painkiller") || lowerName.Contains("ibuprofen") || lowerName.Contains("analgin") || lowerName.Contains("vaselin") || lowerName.Contains("golden_star") || lowerName.Contains("goldenstar") || lowerName.Contains("augmentin") || lowerName.Contains("propital"))
                type = MedicalItemType.Painkillers;
            else if (lowerName.Contains("bloodpack") || lowerName.Contains("blood bag") || lowerName.Contains("bloodbag") || lowerName.Contains("blood_bag"))
                type = MedicalItemType.BloodPack;
            else if (lowerName.Contains("medkit") || lowerName.Contains("medcit") || lowerName.Contains("salewa") || lowerName.Contains("ifak") || lowerName.Contains("grizzly") || lowerName.Contains("automedkit") || lowerName.Contains("core_medical") || lowerName.Contains("first_aid"))
                type = MedicalItemType.Medkit;
            else
                return false;

            return true;
        }

        private static string GetRuntimeItemName(MedicalItemType type)
        {
            switch (type)
            {
                case MedicalItemType.Bandage:
                    return "Bandage";
                case MedicalItemType.Tourniquet:
                    return "Tourniquet";
                case MedicalItemType.Splint:
                    return "Splint";
                case MedicalItemType.Morphine:
                    return "Morphine";
                case MedicalItemType.Adrenaline:
                    return "Adrenaline";
                case MedicalItemType.ETGStimulator:
                    return "ETGStimulator";
                case MedicalItemType.SJ1Stimulator:
                    return "SJ1Stimulator";
                case MedicalItemType.BloodPack:
                    return "BloodPack";
                case MedicalItemType.Painkillers:
                    return "Painkillers";
                case MedicalItemType.Medkit:
                default:
                    return "Medkit";
            }
        }

        private static Vector3 GetRuntimeItemScale(MedicalItemType type)
        {
            switch (type)
            {
                case MedicalItemType.Bandage:
                    return new Vector3(0.14f, 0.08f, 0.10f);
                case MedicalItemType.Splint:
                    return new Vector3(0.08f, 0.04f, 0.34f);
                case MedicalItemType.BloodPack:
                    return new Vector3(0.16f, 0.035f, 0.22f);
                case MedicalItemType.Morphine:
                case MedicalItemType.Adrenaline:
                case MedicalItemType.ETGStimulator:
                case MedicalItemType.SJ1Stimulator:
                    return new Vector3(0.04f, 0.04f, 0.16f);
                case MedicalItemType.Painkillers:
                    return new Vector3(0.09f, 0.045f, 0.055f);
                case MedicalItemType.Tourniquet:
                    return new Vector3(0.06f, 0.055f, 0.23f);
                case MedicalItemType.Medkit:
                default:
                    return new Vector3(0.22f, 0.10f, 0.16f);
            }
        }

        private static float GetRuntimeItemMass(MedicalItemType type)
        {
            switch (type)
            {
                case MedicalItemType.Medkit:
                    return 0.72f;
                case MedicalItemType.Splint:
                    return 0.28f;
                case MedicalItemType.BloodPack:
                    return 0.34f;
                case MedicalItemType.Bandage:
                    return 0.16f;
                case MedicalItemType.Painkillers:
                    return 0.10f;
                default:
                    return 0.12f;
            }
        }

        private static bool IsUsePressed()
        {
            if (ReadTriggerButton(XRNode.RightHand) || ReadTriggerButton(XRNode.LeftHand))
                return true;

            return Input.GetMouseButton(0) || Input.GetKey(KeyCode.E);
        }

        private static bool ReadTriggerButton(XRNode node)
        {
            try
            {
                InputDevice device = InputDevices.GetDeviceAtXRNode(node);
                return device.isValid && device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void ClearHitBuffer(int hitCount)
        {
            int count = Math.Min(hitCount, _bodyHits.Length);
            for (int i = 0; i < count; i++)
                _bodyHits[i] = default!;
        }

        private static void ConsumeItem(GameObject itemRoot)
        {
            if (itemRoot == null)
                return;

            try
            {
                if (TryConsumeMedicalStackUse(itemRoot))
                    return;

                Rigidbody rb = itemRoot.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }

                UnityEngine.Object.Destroy(itemRoot);
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("Failed to consume medical item: " + ex.Message);
            }
        }

        private static bool TryConsumeMedicalStackUse(GameObject itemRoot)
        {
            try
            {
                Component[] components = itemRoot.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null)
                        continue;

                    Type type = component.GetType();
                    if (!string.Equals(type.FullName, "ZBoneCity.Medical.MedicalContainerStack", StringComparison.Ordinal))
                        continue;

                    object? quantityValue = type.GetProperty("Quantity")?.GetValue(component, null);
                    int quantity = quantityValue is int count ? count : 1;
                    if (quantity <= 1)
                        return false;

                    object? consumedValue = type.GetMethod("TryTakeOne", Type.EmptyTypes)?.Invoke(component, null);
                    if (consumedValue is bool consumed && consumed)
                    {
                        UpdateStackObjectName(itemRoot, component, type);
                        return true;
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("[ZBC] Could not consume medical stack use: " + ex.Message);
            }

            return false;
        }

        private static void UpdateStackObjectName(GameObject itemRoot, Component stackComponent, Type stackType)
        {
            try
            {
                object? quantityValue = stackType.GetProperty("Quantity")?.GetValue(stackComponent, null);
                object? nameValue = stackType.GetProperty("ItemDisplayName")?.GetValue(stackComponent, null);
                int quantity = quantityValue is int count ? count : 1;
                string itemName = nameValue is string displayName && !string.IsNullOrWhiteSpace(displayName) ? displayName : itemRoot.name;
                itemRoot.name = quantity > 1 ? itemName + " x" + quantity : itemName;
            }
            catch (Exception)
            {
                // Stack naming is only visual; failed reflection should never block treatment.
            }
        }

        private static void ShowFeedback(string message)
        {
            MainMod.Runtime?.NotifyHudMedicalFeedback(message);
            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("Medical item feedback: " + message);
        }

        private readonly struct HandState
        {
            public static readonly HandState Invalid = new HandState(false, Vector3.zero, false);

            public readonly bool Valid;
            public readonly Vector3 Position;
            public readonly bool HasAttachedObject;

            public HandState(bool valid, Vector3 position, bool hasAttachedObject)
            {
                Valid = valid;
                Position = position;
                HasAttachedObject = hasAttachedObject;
            }
        }

        private readonly struct ContainerDrop
        {
            public readonly MedicalItemType Type;
            public readonly int Quantity;

            public ContainerDrop(MedicalItemType type, int quantity)
            {
                Type = type;
                Quantity = quantity;
            }
        }
    }
}
