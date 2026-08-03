using System;
using System.Collections.Generic;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class PersistentCorpseSystem
    {
        private const float CleanupIntervalSeconds = 0.5f;
        private readonly List<CorpseRecord> _corpses = new List<CorpseRecord>(8);
        private float _cleanupAccumulator;
        private int _nextCorpseId;

        public int Count => _corpses.Count;

        public void Update(float deltaTime)
        {
            if (_corpses.Count == 0)
                return;

            _cleanupAccumulator += deltaTime;
            if (_cleanupAccumulator < CleanupIntervalSeconds)
                return;

            _cleanupAccumulator = 0f;
            RemoveExpired();
            EnforceLimits();
        }

        public void CapturePlayerCorpse(HealthManager manager, DeathCause cause)
        {
            if (!Config.Enabled || !Config.PersistentCorpsesEnabled || Config.PersistentCorpseMaxCount <= 0)
                return;
            if (manager.Kind != HealthOwnerKind.Player)
                return;

            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                RigManager? rigManager = refs != null && refs.HasRefs ? refs.PlayerRigManager : null;
                if (physicsRig == null && rigManager == null)
                {
                    MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Persistent corpse skipped: player rig is not ready.");
                    return;
                }

                GameObject sourceRoot = rigManager != null ? rigManager.gameObject : physicsRig!.gameObject;
                if (sourceRoot == null)
                    return;

                Dictionary<string, RigidbodySnapshot> rigidbodySnapshots = CaptureRigidbodies(sourceRoot.transform);
                GameObject corpse = UnityEngine.Object.Instantiate(sourceRoot, sourceRoot.transform.position, sourceRoot.transform.rotation);
                corpse.name = "ZBC_PersistentCorpse_" + (++_nextCorpseId).ToString("000");
                corpse.SetActive(false);
                SanitizeCorpseRoot(corpse);
                RestoreRigidbodySnapshots(corpse.transform, rigidbodySnapshots);
                AddMedicalMarker(corpse, manager, cause);
                corpse.SetActive(true);

                _corpses.Add(new CorpseRecord(corpse, Time.time, _nextCorpseId));
                EnforceLimits();
                MainMod.Runtime?.NotifyFusionPersistentCorpse(manager, cause, corpse.transform.position, _nextCorpseId);
                if (Config.DebugMode)
                    MainMod.Runtime?.Logger.Msg("[ZBC] Persistent corpse created: " + corpse.name);
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Persistent corpse capture failed safely: " + ex.Message);
            }
        }

        public void EnforceLimits()
        {
            int max = Config.PersistentCorpseMaxCount;
            while (_corpses.Count > max)
                RemoveAt(0);
        }

        public void ClearAll()
        {
            for (int i = _corpses.Count - 1; i >= 0; i--)
                RemoveAt(i);
            _cleanupAccumulator = 0f;
        }

        private void RemoveExpired()
        {
            float lifetime = Config.PersistentCorpseLifetimeSeconds;
            float now = Time.time;
            for (int i = _corpses.Count - 1; i >= 0; i--)
            {
                CorpseRecord record = _corpses[i];
                if (record.Root == null || now - record.CreatedAt >= lifetime)
                    RemoveAt(i);
            }
        }

        private void RemoveAt(int index)
        {
            if (index < 0 || index >= _corpses.Count)
                return;

            GameObject? root = _corpses[index].Root;
            _corpses.RemoveAt(index);
            if (root != null)
                UnityEngine.Object.Destroy(root);
        }

        private static Dictionary<string, RigidbodySnapshot> CaptureRigidbodies(Transform root)
        {
            Dictionary<string, RigidbodySnapshot> snapshots = new Dictionary<string, RigidbodySnapshot>(64, StringComparer.Ordinal);
            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (rb == null)
                    continue;

                string path = GetPath(root, rb.transform);
                snapshots[path] = new RigidbodySnapshot(rb.velocity, rb.angularVelocity);
            }

            return snapshots;
        }

        private static void RestoreRigidbodySnapshots(Transform root, Dictionary<string, RigidbodySnapshot> snapshots)
        {
            Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
            for (int i = 0; i < bodies.Length; i++)
            {
                Rigidbody rb = bodies[i];
                if (rb == null)
                    continue;

                rb.isKinematic = false;
                rb.useGravity = true;
                rb.detectCollisions = true;
                rb.maxAngularVelocity = Mathf.Min(Mathf.Max(rb.maxAngularVelocity, 8f), 14f);
                string path = GetPath(root, rb.transform);
                if (snapshots.TryGetValue(path, out RigidbodySnapshot snapshot))
                {
                    rb.velocity = ClampFinite(snapshot.Velocity, 4.5f);
                    rb.angularVelocity = ClampFinite(snapshot.AngularVelocity, 7.5f);
                }
                rb.WakeUp();
            }
        }

        private static void SanitizeCorpseRoot(GameObject corpse)
        {
            corpse.SetActive(true);
            StripPlayerOnlyComponents(corpse);
            DisableAnimatorBehaviours(corpse);
            DisableAudioLoops(corpse);
            SetLayerRecursive(corpse.transform, corpse.layer);
        }

        private static void StripPlayerOnlyComponents(GameObject corpse)
        {
            Component[] components = corpse.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component is Transform || component is Rigidbody || component is Joint || component is Renderer || component is MeshFilter)
                    continue;
                if (component is Collider && !(component is CharacterController))
                    continue;

                Type type = component.GetType();
                string name = type.Name ?? string.Empty;
                string fullName = type.FullName ?? name;
                if (!ShouldRemoveComponent(name, fullName))
                    continue;

                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        private static bool ShouldRemoveComponent(string name, string fullName)
        {
            if (fullName.IndexOf("Il2CppSLZ.Marrow.Interaction", StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullName.IndexOf("Il2CppSLZ.Marrow.Data", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Holster", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Grip", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Interactable", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("MarrowEntity", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            if (fullName.IndexOf("BonelabAdvancedHealth", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("AudioListener", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("PlayerRefs", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("RigManager", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("PhysicsRig", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("ControllerRig", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("OpenControllerRig", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("PlayerDamageReceiver", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.Equals("Player_Health", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Health", StringComparison.OrdinalIgnoreCase) ||
                name.IndexOf("CharacterController", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("Input", StringComparison.OrdinalIgnoreCase) >= 0 ||
                name.IndexOf("Locomotion", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private static Vector3 ClampFinite(Vector3 value, float maxMagnitude)
        {
            if (!IsFinite(value))
                return Vector3.zero;
            return Vector3.ClampMagnitude(value, maxMagnitude);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsNaN(value.z) &&
                   !float.IsInfinity(value.x) && !float.IsInfinity(value.y) && !float.IsInfinity(value.z);
        }

        private static void DisableAnimatorBehaviours(GameObject corpse)
        {
            Behaviour[] behaviours = corpse.GetComponentsInChildren<Behaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                Behaviour behaviour = behaviours[i];
                if (behaviour == null)
                    continue;

                string name = behaviour.GetType().Name ?? string.Empty;
                if (name.IndexOf("Animator", StringComparison.OrdinalIgnoreCase) >= 0)
                    behaviour.enabled = false;
            }
        }

        private static void DisableAudioLoops(GameObject corpse)
        {
            AudioSource[] sources = corpse.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource source = sources[i];
                if (source == null)
                    continue;
                if (source.loop)
                    source.Stop();
                source.loop = false;
                source.spatialBlend = 1f;
            }
        }

        private static void AddMedicalMarker(GameObject corpse, HealthManager manager, DeathCause cause)
        {
            corpse.name += "_" + cause;
            Transform markerRoot = corpse.transform;
            GameObject marker = new GameObject("ZBC_CorpseMedicalState");
            marker.transform.SetParent(markerRoot, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;
            marker.SetActive(false);
            marker.name =
                "ZBC_CorpseMedicalState_" +
                "Blood" + Mathf.RoundToInt(manager.Bleeding.BloodVolumeMl).ToString() +
                "_Pain" + Mathf.RoundToInt(manager.Pain).ToString() +
                "_Shock" + Mathf.RoundToInt(manager.Shock.Intensity * 100f).ToString();
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        private static string GetPath(Transform root, Transform current)
        {
            if (current == root)
                return string.Empty;

            string path = current.name;
            Transform parent = current.parent;
            while (parent != null && parent != root)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }

            return path;
        }

        private readonly struct CorpseRecord
        {
            public readonly GameObject? Root;
            public readonly float CreatedAt;
            public readonly int Id;

            public CorpseRecord(GameObject? root, float createdAt, int id)
            {
                Root = root;
                CreatedAt = createdAt;
                Id = id;
            }
        }

        private readonly struct RigidbodySnapshot
        {
            public readonly Vector3 Velocity;
            public readonly Vector3 AngularVelocity;

            public RigidbodySnapshot(Vector3 velocity, Vector3 angularVelocity)
            {
                Velocity = velocity;
                AngularVelocity = angularVelocity;
            }
        }
    }
}
