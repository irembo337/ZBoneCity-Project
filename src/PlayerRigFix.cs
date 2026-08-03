using System;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public static class PlayerRigFix
    {
        private static readonly RaycastHit[] FloorHits = new RaycastHit[8];

        public static bool ValidatePlayerRig(MainMod mod, out string reason)
        {
            reason = "valid";
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                if (refs == null || !refs.HasRefs)
                {
                    reason = "PlayerRefs not ready";
                    return false;
                }

                if (refs.PlayerRigManager == null)
                {
                    reason = "RigManager missing";
                    return false;
                }

                PhysicsRig? physicsRig = refs.PlayerPhysicsRig;
                if (physicsRig == null)
                {
                    reason = "PhysicsRig missing";
                    return false;
                }

                if (refs.OpenControllerRig == null || refs.OpenControllerRig.headset == null)
                {
                    reason = "controller rig/headset missing";
                    return false;
                }

                if (!IsFinite(refs.PlayerRigManager.transform.position) ||
                    !IsFinite(physicsRig.transform.position) ||
                    !IsFinite(refs.OpenControllerRig.headset.position))
                {
                    reason = "non-finite rig transform";
                    return false;
                }

                Rigidbody[] bodies = physicsRig.GetComponentsInChildren<Rigidbody>(true);
                if (bodies == null || bodies.Length < 4)
                {
                    reason = "not enough physics bodies";
                    return false;
                }

                if (physicsRig.rbFeet == null && !mod.TryGetPlayerFeetPosition(out _))
                {
                    reason = "feet reference missing";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                reason = "rig validation exception: " + ex.Message;
                return false;
            }
        }

        public static float GetMaxPlayerBodyVelocity()
        {
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                if (physicsRig == null)
                    return 0f;

                float max = 0f;
                Rigidbody[] bodies = physicsRig.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < bodies.Length; i++)
                {
                    Rigidbody rb = bodies[i];
                    if (rb == null)
                        continue;

                    Vector3 velocity = rb.velocity;
                    if (!IsFinite(velocity))
                        return float.PositiveInfinity;

                    max = Mathf.Max(max, velocity.magnitude);
                }

                return max;
            }
            catch (Exception)
            {
                return float.PositiveInfinity;
            }
        }

        public static bool TryLiftPlayerAboveFloor(MainMod mod, float minFeetClearance, float maxLift)
        {
            if (!mod.TryGetPlayerFeetPosition(out Vector3 feet))
                return false;

            float targetFeetY;
            if (!TryFindFloorY(feet, out targetFeetY))
                return false;

            float lift = targetFeetY + minFeetClearance - feet.y;
            if (lift <= 0.015f)
                return false;

            lift = Mathf.Min(lift, Mathf.Max(0.05f, maxLift));
            if (!TryMoveRig(Vector3.up * lift))
                return false;

            ZeroPlayerRigVelocity();
            return true;
        }

        public static void ResetSpawnRagdoll(MainMod mod)
        {
            mod.SetPlayerControlSuppressed(false);
            mod.ForceClearPlayerRagdollForSpawn();
            RestorePlayerUsage();
            ZeroPlayerRigVelocity();
            StabilizePlayerPhysics(mod, true);
        }

        public static void ZeroPlayerRigVelocity()
        {
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                if (physicsRig == null)
                    physicsRig = UnityEngine.Object.FindObjectOfType<PhysicsRig>();
                if (physicsRig == null)
                    return;

                Rigidbody[] bodies = physicsRig.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < bodies.Length; i++)
                {
                    Rigidbody rb = bodies[i];
                    if (rb == null)
                        continue;
                    rb.velocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                }
            }
            catch (Exception)
            {
            }
        }

        public static void StabilizePlayerPhysics(MainMod mod, bool aggressive)
        {
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                if (physicsRig == null)
                    physicsRig = UnityEngine.Object.FindObjectOfType<PhysicsRig>();
                if (physicsRig == null)
                    return;

                float maxLinearVelocity = aggressive ? 4.5f : 10.5f;
                float maxAngularVelocity = aggressive ? 6.0f : 14.0f;
                Rigidbody[] bodies = physicsRig.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < bodies.Length; i++)
                {
                    Rigidbody rb = bodies[i];
                    if (rb == null)
                        continue;

                    if (!IsFinite(rb.transform.position) || !IsFinite(rb.rotation.eulerAngles))
                        continue;

                    Vector3 velocity = rb.velocity;
                    if (!IsFinite(velocity))
                        rb.velocity = Vector3.zero;
                    else if (aggressive)
                        rb.velocity = Vector3.zero;
                    else if (velocity.magnitude > maxLinearVelocity)
                    {
                        Vector3 clamped = Vector3.ClampMagnitude(velocity, maxLinearVelocity);
                        if (clamped.y > 5.5f)
                            clamped.y = 5.5f;
                        rb.velocity = clamped;
                    }

                    Vector3 angularVelocity = rb.angularVelocity;
                    if (!IsFinite(angularVelocity))
                        rb.angularVelocity = Vector3.zero;
                    else if (aggressive)
                        rb.angularVelocity = Vector3.zero;
                    else if (angularVelocity.magnitude > maxAngularVelocity)
                        rb.angularVelocity = Vector3.ClampMagnitude(angularVelocity, maxAngularVelocity);

                    rb.maxAngularVelocity = Mathf.Max(rb.maxAngularVelocity, maxAngularVelocity);
                }

                if (aggressive)
                    RestorePlayerUsage();
            }
            catch (Exception ex)
            {
                if (Config.DebugMode)
                    mod.Logger.Warning("Player physics stabilization failed safely: " + ex.Message);
            }
        }

        public static void StabilizeUnconsciousRagdoll(MainMod mod, float settleStrength)
        {
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                if (physicsRig == null)
                    physicsRig = UnityEngine.Object.FindObjectOfType<PhysicsRig>();
                if (physicsRig == null)
                    return;

                float highLinearLimit = Mathf.Lerp(7.5f, 4.0f, settleStrength);
                float highAngularLimit = Mathf.Lerp(12.0f, 6.0f, settleStrength);
                float jitterLinear = Mathf.Lerp(0.025f, 0.11f, settleStrength);
                float jitterAngular = Mathf.Lerp(0.035f, 0.16f, settleStrength);
                Rigidbody[] bodies = physicsRig.GetComponentsInChildren<Rigidbody>(true);
                for (int i = 0; i < bodies.Length; i++)
                {
                    Rigidbody rb = bodies[i];
                    if (rb == null)
                        continue;

                    Vector3 velocity = rb.velocity;
                    if (!IsFinite(velocity))
                    {
                        rb.velocity = Vector3.zero;
                    }
                    else if (velocity.magnitude <= jitterLinear)
                    {
                        rb.velocity = Vector3.zero;
                    }
                    else if (velocity.magnitude > highLinearLimit)
                    {
                        rb.velocity = Vector3.ClampMagnitude(velocity, highLinearLimit);
                    }

                    Vector3 angularVelocity = rb.angularVelocity;
                    if (!IsFinite(angularVelocity))
                    {
                        rb.angularVelocity = Vector3.zero;
                    }
                    else if (angularVelocity.magnitude <= jitterAngular)
                    {
                        rb.angularVelocity = Vector3.zero;
                    }
                    else if (angularVelocity.magnitude > highAngularLimit)
                    {
                        rb.angularVelocity = Vector3.ClampMagnitude(angularVelocity, highAngularLimit);
                    }

                    rb.maxAngularVelocity = Mathf.Min(Mathf.Max(rb.maxAngularVelocity, 4f), highAngularLimit);
                }
            }
            catch (Exception ex)
            {
                if (Config.DebugMode)
                    mod.Logger.Warning("Unconscious ragdoll stabilization failed safely: " + ex.Message);
            }
        }

        private static bool TryFindFloorY(Vector3 feet, out float floorY)
        {
            floorY = 0f;
            Vector3 origin = feet + Vector3.up * 0.35f;
            int hitCount = Physics.RaycastNonAlloc(origin, Vector3.down, FloorHits, 1.2f, ~0, QueryTriggerInteraction.Ignore);
            Transform? rigRoot = GetPhysicsRigRoot();
            float bestY = float.NegativeInfinity;
            for (int i = 0; i < hitCount && i < FloorHits.Length; i++)
            {
                RaycastHit hit = FloorHits[i];
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;
                if (rigRoot != null && hitTransform != null && hitTransform.IsChildOf(rigRoot))
                    continue;

                if (hit.point.y > bestY)
                    bestY = hit.point.y;
            }

            if (bestY > float.NegativeInfinity)
            {
                floorY = bestY;
                return true;
            }

            if (feet.y < -0.05f)
            {
                floorY = 0f;
                return true;
            }

            return false;
        }

        private static bool TryMoveRig(Vector3 offset)
        {
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                Transform? root = null;
                if (refs != null && refs.HasRefs)
                {
                    if (refs.PlayerRigManager != null)
                        root = refs.PlayerRigManager.transform;
                    else if (refs.PlayerPhysicsRig != null)
                        root = refs.PlayerPhysicsRig.transform;
                }

                if (root == null)
                    return false;

                root.position += offset;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void RestorePlayerUsage()
        {
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                RigManager? rigManager = refs != null && refs.HasRefs ? refs.PlayerRigManager : null;
                if (rigManager != null && rigManager.health != null)
                    rigManager.health.SetUsage(1f, 1f, 1f, 1f, 1f, 1f);
            }
            catch (Exception)
            {
            }
        }

        private static Transform? GetPhysicsRigRoot()
        {
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                return physicsRig != null ? physicsRig.transform : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                     float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
        }
    }
}
