using System;
using System.Collections.Generic;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using UnityEngine;
using UnityEngine.XR;
using MarrowHand = Il2CppSLZ.Marrow.Hand;

namespace BonelabAdvancedHealth
{
    public sealed class MedicalItemRuntimeConfigurator
    {
        private const float ScanInterval = 1.5f;
        private const int InteractableLayerFallback = 0;
        private const float FallbackGrabRadius = 0.28f;
        private const float FallbackGrabGrip = 0.42f;
        private const float FallbackReleaseGrip = 0.22f;
        private const float FallbackReleaseCooldown = 0.18f;
        private const float TriggerDropCooldown = 0.36f;
        private const float NativeDropSearchRadius = 0.38f;
        private readonly HashSet<int> _configured = new HashSet<int>(64);
        private float _scanTimer;
        private float _fallbackCooldown;
        private bool _failureLogged;
        private bool _runtimeGripWarningLogged;
        private bool _fallbackGrabLogged;
        private GameObject? _fallbackGrabbedRoot;
        private Rigidbody? _fallbackGrabbedBody;
        private Transform? _fallbackGrabHand;
        private Transform? _fallbackOriginalParent;
        private bool _fallbackGrabRightHand;
        private bool _fallbackOriginalKinematic;
        private bool _fallbackOriginalUseGravity;
        private bool _fallbackOriginalDetectCollisions;
        private Vector3 _lastFallbackHandPosition;
        private Vector3 _fallbackHandVelocity;
        private bool _leftTriggerHeld;
        private bool _rightTriggerHeld;

        public static int LastTriggerDropFrame { get; private set; } = -1;
        public static bool TriggerDropConsumedThisFrame => LastTriggerDropFrame == Time.frameCount;

        public void Reset()
        {
            ReleaseFallbackGrab(false);
            _configured.Clear();
            _scanTimer = 0f;
            _fallbackCooldown = 0f;
            _failureLogged = false;
            _runtimeGripWarningLogged = false;
            _fallbackGrabLogged = false;
            _leftTriggerHeld = false;
            _rightTriggerHeld = false;
        }

        public void Update(float deltaTime)
        {
            MainMod? runtime = MainMod.Runtime;
            if (runtime == null || !runtime.IsPlayerRigReady())
            {
                ReleaseFallbackGrab(false);
                return;
            }

            UpdateFallbackGrab(deltaTime);
            if (!PlatformCompatibility.SupportsRuntimeMedicalGripInjection)
                return;

            _scanTimer -= deltaTime;
            if (_scanTimer > 0f)
                return;

            _scanTimer = ScanInterval;
            Rigidbody[] bodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (rb == null || rb.gameObject == null)
                    continue;

                Transform root = rb.transform;
                if (!LooksLikeZBoneCityMedicalItem(root))
                    continue;

                int id = root.gameObject.GetInstanceID();
                if (_configured.Contains(id))
                    continue;

                if (Configure(root.gameObject))
                    _configured.Add(id);
            }
        }

        private void UpdateFallbackGrab(float deltaTime)
        {
            _fallbackCooldown = Math.Max(0f, _fallbackCooldown - deltaTime);

            if (!TryGetFallbackHands(out FallbackHandState leftHand, out FallbackHandState rightHand))
            {
                ReleaseFallbackGrab(false);
                _leftTriggerHeld = false;
                _rightTriggerHeld = false;
                return;
            }

            bool leftTriggerDown = leftHand.Valid && leftHand.Trigger && !_leftTriggerHeld;
            bool rightTriggerDown = rightHand.Valid && rightHand.Trigger && !_rightTriggerHeld;
            _leftTriggerHeld = leftHand.Valid && leftHand.Trigger;
            _rightTriggerHeld = rightHand.Valid && rightHand.Trigger;

            if (_fallbackGrabbedRoot != null)
            {
                FallbackHandState hand = _fallbackGrabRightHand ? rightHand : leftHand;
                bool triggerDown = _fallbackGrabRightHand ? rightTriggerDown : leftTriggerDown;
                if (triggerDown && !IsTriggerDropExempt(_fallbackGrabbedRoot))
                {
                    TriggerDropFallbackGrab();
                    return;
                }

                if (!hand.Valid || hand.Grip < FallbackReleaseGrip)
                {
                    ReleaseFallbackGrab(true);
                    return;
                }

                FollowFallbackHand(hand, deltaTime);
                return;
            }

            if (_fallbackCooldown > 0f)
                return;

            if (leftTriggerDown && TryTriggerDropNativeMedicalItem(leftHand))
                return;
            if (rightTriggerDown && TryTriggerDropNativeMedicalItem(rightHand))
                return;

            bool leftPressed = leftHand.Valid && !leftHand.NativeAttached && leftHand.Grip >= FallbackGrabGrip;
            bool rightPressed = rightHand.Valid && !rightHand.NativeAttached && rightHand.Grip >= FallbackGrabGrip;
            if (!leftPressed && !rightPressed)
                return;

            TryBeginFallbackGrab(leftPressed ? leftHand : FallbackHandState.Invalid, rightPressed ? rightHand : FallbackHandState.Invalid);
        }

        private void TryBeginFallbackGrab(FallbackHandState leftHand, FallbackHandState rightHand)
        {
            Rigidbody[] bodies;
            try
            {
                bodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
            }
            catch (Exception)
            {
                return;
            }

            GameObject? bestRoot = null;
            Rigidbody? bestBody = null;
            Transform? bestHand = null;
            bool bestRight = false;
            float bestDistance = FallbackGrabRadius;

            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (rb == null || rb.transform == null)
                    continue;

                if (!TryResolveMedicalRoot(rb.transform, out GameObject? root, out _))
                    continue;
                if (root == null || root == _fallbackGrabbedRoot)
                    continue;

                int id = root.GetInstanceID();
                if (!_configured.Contains(id) && Configure(root))
                    _configured.Add(id);

                Vector3 itemPosition = GetItemGrabPoint(root, rb);
                EvaluateFallbackCandidate(root, rb, itemPosition, leftHand, false, ref bestRoot, ref bestBody, ref bestHand, ref bestRight, ref bestDistance);
                EvaluateFallbackCandidate(root, rb, itemPosition, rightHand, true, ref bestRoot, ref bestBody, ref bestHand, ref bestRight, ref bestDistance);
            }

            if (bestRoot == null || bestBody == null || bestHand == null)
                return;

            _fallbackGrabbedRoot = bestRoot;
            _fallbackGrabbedBody = bestBody;
            _fallbackGrabHand = bestHand;
            _fallbackGrabRightHand = bestRight;
            _fallbackOriginalParent = bestRoot.transform.parent;
            _fallbackOriginalKinematic = bestBody.isKinematic;
            _fallbackOriginalUseGravity = bestBody.useGravity;
            _fallbackOriginalDetectCollisions = bestBody.detectCollisions;
            _lastFallbackHandPosition = bestHand.position;
            _fallbackHandVelocity = Vector3.zero;

            bestBody.velocity = Vector3.zero;
            bestBody.angularVelocity = Vector3.zero;
            bestBody.isKinematic = true;
            bestBody.useGravity = false;
            bestBody.detectCollisions = false;

            bestRoot.transform.SetParent(bestHand, false);
            bestRoot.transform.localPosition = GetHeldLocalOffset(bestRoot, bestRight);
            bestRoot.transform.localRotation = GetHeldLocalRotation(bestRoot);

            if (Config.DebugMode && !_fallbackGrabLogged)
            {
                _fallbackGrabLogged = true;
                MainMod.Runtime?.Logger.Msg("[ZBC] Fallback VR pickup active for SDK medical items.");
            }
        }

        private static void EvaluateFallbackCandidate(
            GameObject root,
            Rigidbody rb,
            Vector3 itemPosition,
            FallbackHandState hand,
            bool rightHand,
            ref GameObject? bestRoot,
            ref Rigidbody? bestBody,
            ref Transform? bestHand,
            ref bool bestRight,
            ref float bestDistance)
        {
            if (!hand.Valid)
                return;

            float distance = Vector3.Distance(itemPosition, hand.Position);
            if (distance >= bestDistance)
                return;

            bestDistance = distance;
            bestRoot = root;
            bestBody = root.GetComponent<Rigidbody>() ?? rb;
            bestHand = hand.Transform;
            bestRight = rightHand;
        }

        private void FollowFallbackHand(FallbackHandState hand, float deltaTime)
        {
            GameObject? root = _fallbackGrabbedRoot;
            Rigidbody? body = _fallbackGrabbedBody;
            Transform? grabHand = _fallbackGrabHand;
            if (root == null || body == null || grabHand == null)
            {
                ReleaseFallbackGrab(false);
                return;
            }

            Vector3 handPosition = hand.Position;
            _fallbackHandVelocity = (handPosition - _lastFallbackHandPosition) / Mathf.Max(0.001f, deltaTime);
            _lastFallbackHandPosition = handPosition;

            if (root.transform.parent != grabHand)
                root.transform.SetParent(grabHand, false);

            root.transform.localPosition = Vector3.Lerp(root.transform.localPosition, GetHeldLocalOffset(root, _fallbackGrabRightHand), 0.45f);
            root.transform.localRotation = Quaternion.Slerp(root.transform.localRotation, GetHeldLocalRotation(root), 0.45f);
            body.velocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        private void ReleaseFallbackGrab(bool throwWithHand)
        {
            GameObject? root = _fallbackGrabbedRoot;
            Rigidbody? body = _fallbackGrabbedBody;
            if (root != null)
                root.transform.SetParent(_fallbackOriginalParent, true);

            if (body != null)
            {
                body.isKinematic = _fallbackOriginalKinematic;
                body.useGravity = _fallbackOriginalUseGravity;
                body.detectCollisions = _fallbackOriginalDetectCollisions;
                body.velocity = throwWithHand ? Vector3.ClampMagnitude(_fallbackHandVelocity, 3.0f) : Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            _fallbackGrabbedRoot = null;
            _fallbackGrabbedBody = null;
            _fallbackGrabHand = null;
            _fallbackOriginalParent = null;
            _fallbackHandVelocity = Vector3.zero;
            _fallbackCooldown = throwWithHand ? FallbackReleaseCooldown : 0f;
        }

        private void TriggerDropFallbackGrab()
        {
            LastTriggerDropFrame = Time.frameCount;
            GameObject? dropped = _fallbackGrabbedRoot;
            ReleaseFallbackGrab(false);
            _fallbackCooldown = TriggerDropCooldown;
            if (dropped != null)
                StabilizeDroppedItem(dropped);
            MainMod.Runtime?.NotifyHudMedicalFeedback("Medical Item Dropped");
        }

        private bool TryTriggerDropNativeMedicalItem(FallbackHandState hand)
        {
            if (!hand.Valid || !hand.NativeAttached || hand.Hand == null)
                return false;
            if (!TryFindNearestMedicalItem(hand.Position, NativeDropSearchRadius, out GameObject? root, out Rigidbody? body))
                return false;
            if (root == null || IsTriggerDropExempt(root))
                return false;

            try
            {
                hand.Hand.DetachObject();
            }
            catch (Exception ex)
            {
                if (Config.DebugMode)
                    MainMod.Runtime?.Logger.Warning("[ZBC] Native medical detach failed safely: " + ex.Message);
                return false;
            }

            LastTriggerDropFrame = Time.frameCount;
            _fallbackCooldown = TriggerDropCooldown;
            StabilizeDroppedItem(root, body);
            MainMod.Runtime?.NotifyHudMedicalFeedback("Medical Item Dropped");
            return true;
        }

        private bool TryFindNearestMedicalItem(Vector3 position, float radius, out GameObject? root, out Rigidbody? body)
        {
            root = null;
            body = null;
            float bestDistance = radius;

            Rigidbody[] bodies;
            try
            {
                bodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
            }
            catch (Exception)
            {
                return false;
            }

            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody candidateBody = bodies[i];
                if (candidateBody == null || candidateBody.transform == null)
                    continue;
                if (!TryResolveMedicalRoot(candidateBody.transform, out GameObject? candidateRoot, out _))
                    continue;
                if (candidateRoot == null)
                    continue;

                Vector3 grabPoint = GetItemGrabPoint(candidateRoot, candidateBody);
                float distance = Vector3.Distance(position, grabPoint);
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                root = candidateRoot;
                body = candidateRoot.GetComponent<Rigidbody>() ?? candidateBody;
            }

            return root != null;
        }

        private static void StabilizeDroppedItem(GameObject root, Rigidbody? body = null)
        {
            if (root == null)
                return;

            Rigidbody rb = body ?? root.GetComponent<Rigidbody>();
            if (rb == null)
                return;

            rb.isKinematic = false;
            rb.useGravity = true;
            rb.detectCollisions = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();
        }

        private static bool TryGetFallbackHands(out FallbackHandState leftHand, out FallbackHandState rightHand)
        {
            leftHand = FallbackHandState.Invalid;
            rightHand = FallbackHandState.Invalid;
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                if (physicsRig == null)
                    return false;

                leftHand = GetFallbackHandState(physicsRig.leftHand, XRNode.LeftHand);
                rightHand = GetFallbackHandState(physicsRig.rightHand, XRNode.RightHand);
                return leftHand.Valid || rightHand.Valid;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static FallbackHandState GetFallbackHandState(MarrowHand hand, XRNode node)
        {
            if (hand == null || hand.transform == null)
                return FallbackHandState.Invalid;

            bool nativeAttached = false;
            try
            {
                nativeAttached = hand.HasAttachedObject();
            }
            catch (Exception)
            {
                nativeAttached = false;
            }

            return new FallbackHandState(true, hand, hand.transform, hand.transform.position, ReadGrip(node), ReadTrigger(node), nativeAttached);
        }

        private static float ReadGrip(XRNode node)
        {
            float value = 0f;
            try
            {
                InputDevice device = InputDevices.GetDeviceAtXRNode(node);
                if (!device.isValid)
                    return 0f;
                if (device.TryGetFeatureValue(CommonUsages.gripButton, out bool pressed) && pressed)
                    value = 1f;
                if (device.TryGetFeatureValue(CommonUsages.grip, out float axis))
                    value = Math.Max(value, axis);
            }
            catch (Exception)
            {
                value = 0f;
            }

            return Config.Clamp(value, 0f, 1f);
        }

        private static bool ReadTrigger(XRNode node)
        {
            try
            {
                InputDevice device = InputDevices.GetDeviceAtXRNode(node);
                if (!device.isValid)
                    return false;
                if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed)
                    return true;
                if (device.TryGetFeatureValue(CommonUsages.trigger, out float axis) && axis >= 0.62f)
                    return true;
            }
            catch (Exception)
            {
            }

            return false;
        }

        private static Vector3 GetItemGrabPoint(GameObject root, Rigidbody body)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && !collider.isTrigger)
                    return collider.bounds.center;
            }

            return body != null ? body.worldCenterOfMass : root.transform.position;
        }

        private static Vector3 GetHeldLocalOffset(GameObject root, bool rightHand)
        {
            string type = ResolveItemType(root.transform);
            float side = rightHand ? -1f : 1f;
            switch (type)
            {
                case "Medkit":
                    return new Vector3(side * 0.015f, -0.035f, 0.095f);
                case "Splint":
                    return new Vector3(side * 0.01f, -0.025f, 0.13f);
                case "BloodPack":
                    return new Vector3(side * 0.012f, -0.025f, 0.105f);
                default:
                    return new Vector3(side * 0.012f, -0.02f, 0.075f);
            }
        }

        private static Quaternion GetHeldLocalRotation(GameObject root)
        {
            string type = ResolveItemType(root.transform);
            if (IsCylinderItem(type) || string.Equals(type, "Splint", StringComparison.OrdinalIgnoreCase))
                return Quaternion.Euler(0f, 0f, 90f);
            return Quaternion.identity;
        }

        private bool Configure(GameObject root)
        {
            try
            {
                string type = ResolveItemType(root.transform);
                if (string.IsNullOrEmpty(type))
                    return false;

                Rigidbody rb = root.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = root.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.isKinematic = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                rb.mass = GetMass(type);

                int interactableLayer = LayerMask.NameToLayer("Interactable");
                SetLayerRecursive(root.transform, interactableLayer >= 0 ? interactableLayer : InteractableLayerFallback);

                EnsureStableCollider(root, type);
                EnsureGripPointMarker(root.transform, type);
                if (!_runtimeGripWarningLogged)
                {
                    _runtimeGripWarningLogged = true;
                    MainMod.Runtime?.Logger.Warning("[ZBC] Runtime Marrow grip/entity injection is disabled for load safety. Use SDK-built medical prefabs for native grab support.");
                }

                if (Config.DebugMode)
                    MainMod.Runtime?.Logger.Msg("Runtime stabilized medical item physics: " + root.name);
                _failureLogged = false;
                return true;
            }
            catch (Exception ex)
            {
                if (!_failureLogged)
                {
                    _failureLogged = true;
                    MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Medical pickup runtime setup failed safely: " + ex.Message);
                }

                return false;
            }
        }

        private static Collider EnsureStableCollider(GameObject root, string itemType)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (collider != null && !collider.isTrigger)
                    return collider;
            }

            BoxCollider box = root.AddComponent<BoxCollider>();
            box.size = GetDefaultSize(itemType);
            box.center = Vector3.zero;
            box.isTrigger = false;
            return box;
        }

        private static Transform EnsureGripPointMarker(Transform root, string itemType)
        {
            Transform marker = root.Find("AHS_GripPoint");
            if (marker != null)
                return marker;

            GameObject go = new GameObject("AHS_GripPoint");
            go.transform.SetParent(root, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = IsCylinderItem(itemType) ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static bool LooksLikeZBoneCityMedicalItem(Transform root)
        {
            if (root == null)
                return false;
            if (root.name.IndexOf("ZBC_Medical_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                root.name.IndexOf("AHS_Medical_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                TryGetMedicalTypeFromName(root.name, out _))
                return true;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null &&
                    (child.name.StartsWith("ZBC_ItemType_", StringComparison.Ordinal) ||
                     child.name.StartsWith("AHS_ItemType_", StringComparison.Ordinal)))
                    return true;
            }

            return false;
        }

        private static bool TryResolveMedicalRoot(Transform start, out GameObject? root, out string type)
        {
            root = null;
            type = string.Empty;
            Transform? current = start;
            for (int depth = 0; depth < 6 && current != null; depth++)
            {
                type = ResolveItemType(current);
                if (!string.IsNullOrEmpty(type) || LooksLikeZBoneCityMedicalItem(current))
                {
                    if (string.IsNullOrEmpty(type))
                        TryGetMedicalTypeFromName(current.name, out type);
                    root = current.gameObject;
                    return root != null;
                }

                current = current.parent;
            }

            return false;
        }

        private static string ResolveItemType(Transform root)
        {
            string name = root.name;
            int marker = name.IndexOf("ZBC_Medical_", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0)
                return name.Substring(marker + "ZBC_Medical_".Length).Trim();
            marker = name.IndexOf("AHS_Medical_", StringComparison.OrdinalIgnoreCase);
            if (marker >= 0)
                return name.Substring(marker + "AHS_Medical_".Length).Trim();
            if (TryGetMedicalTypeFromName(name, out string namedType))
                return namedType;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name.StartsWith("ZBC_ItemType_", StringComparison.Ordinal))
                    return child.name.Substring("ZBC_ItemType_".Length).Trim();
                if (child != null && child.name.StartsWith("AHS_ItemType_", StringComparison.Ordinal))
                    return child.name.Substring("AHS_ItemType_".Length).Trim();
            }

            return string.Empty;
        }

        private static bool TryGetMedicalTypeFromName(string name, out string type)
        {
            type = string.Empty;
            if (string.IsNullOrEmpty(name))
                return false;

            string lower = name.ToLowerInvariant();
            if (TryGetKnownMedicalType(lower, out type))
                return true;
            if (lower.Contains("bandage") || lower.Contains("бинт"))
                type = "Bandage";
            else if (lower.Contains("tourniquet") || lower.Contains("турникет"))
                type = "Tourniquet";
            else if (lower.Contains("splint") || lower.Contains("шина"))
                type = "Splint";
            else if (lower.Contains("morphine") || lower.Contains("морфин"))
                type = "Morphine";
            else if (lower.Contains("adrenaline") || lower.Contains("адреналин") || lower.Contains("epipen"))
                type = "Adrenaline";
            else if (lower.Contains("painkiller") || lower.Contains("обезбол"))
                type = "Painkillers";
            else if (lower.Contains("bloodpack") || lower.Contains("blood bag") || lower.Contains("bloodbag") || lower.Contains("blood_bag"))
                type = "BloodPack";
            else if (lower.Contains("medkit") || lower.Contains("medcit") || lower.Contains("salewa") || lower.Contains("аптеч"))
                type = "Medkit";

            return !string.IsNullOrEmpty(type);
        }

        private static bool TryGetKnownMedicalType(string lowerName, out string type)
        {
            type = string.Empty;
            if (string.IsNullOrEmpty(lowerName))
                return false;

            if (lowerName.Contains("bandage") || lowerName.Contains("military") || lowerName.Contains("army"))
                type = "Bandage";
            else if (lowerName.Contains("tourniquet"))
                type = "Tourniquet";
            else if (lowerName.Contains("splint") || lowerName.Contains("alusplint") || lowerName.Contains("aluminum") || lowerName.Contains("survival") || lowerName.Contains("rollup"))
                type = "Splint";
            else if (lowerName.Contains("morphine"))
                type = "Morphine";
            else if (lowerName.Contains("etg"))
                type = "ETGStimulator";
            else if (lowerName.Contains("sj1") || lowerName.Contains("sj6"))
                type = "SJ1Stimulator";
            else if (lowerName.Contains("adrenaline") || lowerName.Contains("zagustin") || lowerName.Contains("stim") || lowerName.Contains("epipen"))
                type = "Adrenaline";
            else if (lowerName.Contains("painkiller") || lowerName.Contains("ibuprofen") || lowerName.Contains("analgin") || lowerName.Contains("vaselin") || lowerName.Contains("golden_star") || lowerName.Contains("goldenstar") || lowerName.Contains("augmentin") || lowerName.Contains("propital"))
                type = "Painkillers";
            else if (lowerName.Contains("bloodpack") || lowerName.Contains("blood bag") || lowerName.Contains("bloodbag") || lowerName.Contains("blood_bag"))
                type = "BloodPack";
            else if (lowerName.Contains("medkit") || lowerName.Contains("medcit") || lowerName.Contains("salewa") || lowerName.Contains("ifak") || lowerName.Contains("grizzly") || lowerName.Contains("automedkit") || lowerName.Contains("core_medical") || lowerName.Contains("first_aid"))
                type = "Medkit";

            return !string.IsNullOrEmpty(type);
        }

        private static bool IsTriggerDropExempt(GameObject root)
        {
            if (root == null)
                return false;

            if (HasMedicalContainerComponent(root))
                return true;

            Transform? current = root.transform;
            for (int depth = 0; current != null && depth < 6; depth++)
            {
                string name = current.name ?? string.Empty;
                if (name.IndexOf("survival", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("rollup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("salewa", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("ifak", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("grizzly", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("automedkit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("auto medkit", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("core_medical", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("surgical", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (name.IndexOf("medkit", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    name.IndexOf("medical bandage", StringComparison.OrdinalIgnoreCase) < 0)
                    return true;
                current = current.parent;
            }

            return false;
        }

        private static bool HasMedicalContainerComponent(GameObject root)
        {
            try
            {
                Component[] components = root.GetComponentsInChildren<Component>(true);
                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];
                    if (component == null)
                        continue;

                    Type type = component.GetType();
                    if (string.Equals(type.FullName, "ZBoneCity.Medical.MedicalContainer", StringComparison.Ordinal))
                        return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null)
                return child;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        private static bool IsCylinderItem(string itemType)
        {
            return string.Equals(itemType, "Morphine", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(itemType, "Adrenaline", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(itemType, "ETGStimulator", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(itemType, "SJ1Stimulator", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(itemType, "Tourniquet", StringComparison.OrdinalIgnoreCase);
        }

        private static float GetMass(string itemType)
        {
            switch (itemType)
            {
                case "Medkit":
                    return 0.72f;
                case "Splint":
                    return 0.28f;
                case "BloodPack":
                    return 0.34f;
                case "Bandage":
                    return 0.16f;
                case "Painkillers":
                    return 0.10f;
                default:
                    return 0.12f;
            }
        }

        private static Vector3 GetDefaultSize(string itemType)
        {
            switch (itemType)
            {
                case "Medkit":
                    return new Vector3(0.22f, 0.10f, 0.16f);
                case "Splint":
                    return new Vector3(0.09f, 0.045f, 0.36f);
                case "Morphine":
                    return new Vector3(0.035f, 0.035f, 0.16f);
                case "Adrenaline":
                case "ETGStimulator":
                case "SJ1Stimulator":
                    return new Vector3(0.04f, 0.04f, 0.17f);
                case "Tourniquet":
                    return new Vector3(0.06f, 0.055f, 0.23f);
                case "BloodPack":
                    return new Vector3(0.16f, 0.035f, 0.22f);
                case "Painkillers":
                    return new Vector3(0.09f, 0.045f, 0.055f);
                default:
                    return new Vector3(0.12f, 0.10f, 0.08f);
            }
        }

        private readonly struct FallbackHandState
        {
            public static readonly FallbackHandState Invalid = new FallbackHandState(false, null, null, Vector3.zero, 0f, false, false);

            public readonly bool Valid;
            public readonly MarrowHand? Hand;
            public readonly Transform? Transform;
            public readonly Vector3 Position;
            public readonly float Grip;
            public readonly bool Trigger;
            public readonly bool NativeAttached;

            public FallbackHandState(bool valid, MarrowHand? hand, Transform? transform, Vector3 position, float grip, bool trigger, bool nativeAttached)
            {
                Valid = valid;
                Hand = hand;
                Transform = transform;
                Position = position;
                Grip = grip;
                Trigger = trigger;
                NativeAttached = nativeAttached;
            }
        }
    }
}
