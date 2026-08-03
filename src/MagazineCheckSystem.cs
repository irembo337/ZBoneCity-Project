using System;
using System.Reflection;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using UnityEngine;
using UnityEngine.XR;
using MarrowHand = Il2CppSLZ.Marrow.Hand;

namespace BonelabAdvancedHealth
{
    public sealed class MagazineCheckSystem
    {
        private const float InspectCooldownSeconds = 0.8f;
        private const float HandSearchRadius = 0.42f;
        private const float HeadInspectDistance = 1.15f;
        private float _cooldown;
        private bool _checkHeld;
        private bool _failureLogged;

        public void Reset()
        {
            _cooldown = 0f;
            _checkHeld = false;
            _failureLogged = false;
        }

        public void Update(float deltaTime, HealthManager? player)
        {
            if (!Config.Enabled || !Config.MagazineCheckEnabled || player == null || player.IsDead)
                return;

            _cooldown = Math.Max(0f, _cooldown - deltaTime);
            bool pressed = IsCheckPressed();
            bool pressedThisFrame = pressed && !_checkHeld;
            _checkHeld = pressed;
            if (!pressedThisFrame || _cooldown > 0f)
                return;

            _cooldown = InspectCooldownSeconds;
            TryInspectMagazine();
        }

        private void TryInspectMagazine()
        {
            try
            {
                if (!TryGetHandState(out HandSnapshot left, out HandSnapshot right))
                    return;

                Transform? head = MainMod.Runtime?.GetHeadTransform();
                if (head == null)
                    return;

                Rigidbody[] bodies = UnityEngine.Object.FindObjectsOfType<Rigidbody>();
                GameObject? best = null;
                float bestDistance = HandSearchRadius;
                for (int i = 0; i < bodies.Length; i++)
                {
                    Rigidbody body = bodies[i];
                    if (body == null || body.transform == null)
                        continue;

                    Transform root = ResolveLikelyRoot(body.transform);
                    if (!LooksLikeMagazine(root))
                        continue;

                    Vector3 center = GetObjectCenter(root.gameObject, body);
                    if (!IsNearInspectView(center, head))
                        continue;

                    float distance = GetHandDistance(center, left, right, out bool held);
                    if (!held || distance >= bestDistance)
                        continue;

                    bestDistance = distance;
                    best = root.gameObject;
                }

                if (best == null)
                    return;

                string estimate = EstimateMagazine(best);
                MainMod.Runtime?.NotifyHudMedicalFeedback("Magazine: " + estimate);
                MainMod.Runtime?.NotifyFusionMagazineCheck(estimate);
                if (Config.DebugMode)
                    MainMod.Runtime?.Logger.Msg("[ZBC] Magazine checked: " + estimate + " (" + best.name + ")");
                _failureLogged = false;
            }
            catch (Exception ex)
            {
                if (!_failureLogged)
                {
                    _failureLogged = true;
                    MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Magazine check failed safely: " + ex.Message);
                }
            }
        }

        private static string EstimateMagazine(GameObject magazine)
        {
            if (TryReadAmmoRatio(magazine, out float ratio))
            {
                if (ratio <= 0.02f)
                    return "Empty";
                if (ratio <= 0.25f)
                    return "Nearly Empty";
                if (ratio <= 0.65f)
                    return "Half Full";
                return "Almost Full";
            }

            return "Unknown Load";
        }

        private static bool TryReadAmmoRatio(GameObject root, out float ratio)
        {
            ratio = -1f;
            int bestCurrent = -1;
            int bestCapacity = -1;
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;

                Type type = component.GetType();
                ReadMembers(type, component, ref bestCurrent, ref bestCapacity);
            }

            if (bestCapacity <= 0)
                bestCapacity = InferCapacityFromName(root.name, bestCurrent);
            if (bestCurrent < 0 || bestCapacity <= 0)
                return false;

            ratio = Config.Clamp(bestCurrent / (float)bestCapacity, 0f, 1f);
            return true;
        }

        private static void ReadMembers(Type type, object instance, ref int current, ref int capacity)
        {
            const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            FieldInfo[] fields = type.GetFields(Flags);
            for (int i = 0; i < fields.Length; i++)
                TryReadValue(fields[i].Name, SafeGetField(fields[i], instance), ref current, ref capacity);

            PropertyInfo[] properties = type.GetProperties(Flags);
            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];
                if (property.GetIndexParameters().Length != 0)
                    continue;
                TryReadValue(property.Name, SafeGetProperty(property, instance), ref current, ref capacity);
            }
        }

        private static object? SafeGetField(FieldInfo field, object instance)
        {
            try
            {
                return field.GetValue(instance);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object? SafeGetProperty(PropertyInfo property, object instance)
        {
            try
            {
                return property.GetValue(instance, null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void TryReadValue(string name, object? value, ref int current, ref int capacity)
        {
            if (value == null)
                return;

            int number;
            if (value is int intValue)
                number = intValue;
            else if (value is float floatValue)
                number = Mathf.RoundToInt(floatValue);
            else if (value is double doubleValue)
                number = (int)Math.Round(doubleValue);
            else
                return;

            if (number < 0 || number > 300)
                return;

            string lower = name.ToLowerInvariant();
            bool ammoName = lower.Contains("ammo") || lower.Contains("round") || lower.Contains("cartridge") || lower.Contains("cart") || lower.Contains("bullet");
            bool currentName = ammoName || lower.Contains("loaded") || lower.Contains("remaining") || lower.Contains("current") || lower.Contains("count") || lower.Contains("cur");
            bool capacityName = lower.Contains("capacity") || lower.Contains("max") || lower.Contains("size") || lower.Contains("total");

            if (capacityName && number > capacity)
                capacity = number;
            else if (currentName && (current < 0 || number <= Math.Max(capacity, 300)))
                current = Math.Max(current, number);
        }

        private static int InferCapacityFromName(string name, int current)
        {
            string lower = name.ToLowerInvariant();
            if (lower.Contains("drum"))
                return Math.Max(50, current);
            if (lower.Contains("pistol") || lower.Contains("glock") || lower.Contains("1911"))
                return Math.Max(15, current);
            if (lower.Contains("shotgun"))
                return Math.Max(8, current);
            return Math.Max(30, current);
        }

        private static Transform ResolveLikelyRoot(Transform transform)
        {
            Transform current = transform;
            for (int i = 0; i < 4 && current.parent != null; i++)
            {
                string name = current.parent.name ?? string.Empty;
                if (!LooksLikeMagazineName(name))
                    break;
                current = current.parent;
            }

            return current;
        }

        private static bool LooksLikeMagazine(Transform root)
        {
            if (root == null)
                return false;
            if (LooksLikeMagazineName(root.name))
                return true;

            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                    continue;
                string typeName = component.GetType().FullName ?? string.Empty;
                if (LooksLikeMagazineName(typeName))
                    return true;
            }

            return false;
        }

        private static bool LooksLikeMagazineName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            string lower = name.ToLowerInvariant();
            if (lower.Contains("image") || lower.Contains("manager"))
                return false;
            return lower.Contains("magazine") ||
                   lower.Contains("mag_") ||
                   lower.Contains("_mag") ||
                   lower.Contains("ammo mag") ||
                   lower.Contains("cartridgebox") ||
                   lower.Contains("stanag");
        }

        private static bool IsNearInspectView(Vector3 point, Transform head)
        {
            Vector3 toPoint = point - head.position;
            if (toPoint.sqrMagnitude > HeadInspectDistance * HeadInspectDistance)
                return false;
            if (Vector3.Dot(head.forward, toPoint.normalized) < 0.25f)
                return false;
            return true;
        }

        private static float GetHandDistance(Vector3 point, HandSnapshot left, HandSnapshot right, out bool held)
        {
            held = false;
            float best = float.PositiveInfinity;
            EvaluateHand(point, left, ref best, ref held);
            EvaluateHand(point, right, ref best, ref held);
            return best;
        }

        private static void EvaluateHand(Vector3 point, HandSnapshot hand, ref float best, ref bool held)
        {
            if (!hand.Valid)
                return;

            float distance = Vector3.Distance(point, hand.Position);
            if (distance < best)
                best = distance;
            if (distance <= HandSearchRadius && hand.HasAttachedObject)
                held = true;
        }

        private static Vector3 GetObjectCenter(GameObject root, Rigidbody body)
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

        private static bool TryGetHandState(out HandSnapshot left, out HandSnapshot right)
        {
            left = HandSnapshot.Invalid;
            right = HandSnapshot.Invalid;
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                if (physicsRig == null)
                    return false;

                left = Snapshot(physicsRig.leftHand);
                right = Snapshot(physicsRig.rightHand);
                return left.Valid || right.Valid;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static HandSnapshot Snapshot(MarrowHand hand)
        {
            if (hand == null)
                return HandSnapshot.Invalid;

            bool attached = false;
            try
            {
                attached = hand.HasAttachedObject();
            }
            catch (Exception)
            {
            }

            return new HandSnapshot(true, hand.transform.position, attached);
        }

        private static bool IsCheckPressed()
        {
            if (Input.GetKeyDown(KeyCode.R))
                return true;

            return ReadButton(XRNode.LeftHand) || ReadButton(XRNode.RightHand);
        }

        private static bool ReadButton(XRNode node)
        {
            try
            {
                InputDevice device = InputDevices.GetDeviceAtXRNode(node);
                if (!device.isValid)
                    return false;
                if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool primary) && primary)
                    return true;
                return device.TryGetFeatureValue(CommonUsages.secondaryButton, out bool secondary) && secondary;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private readonly struct HandSnapshot
        {
            public static readonly HandSnapshot Invalid = new HandSnapshot(false, Vector3.zero, false);
            public readonly bool Valid;
            public readonly Vector3 Position;
            public readonly bool HasAttachedObject;

            public HandSnapshot(bool valid, Vector3 position, bool hasAttachedObject)
            {
                Valid = valid;
                Position = position;
                HasAttachedObject = hasAttachedObject;
            }
        }
    }
}
