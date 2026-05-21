using System;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class PlayerControlLockSystem
    {
        private struct HandSnapshot
        {
            public bool Captured;
            public bool FarHoverEnabled;
            public bool HoverLocked;
            public bool GrabLock;
            public bool PhysShutdown;
            public float GripMult;
            public float ForceMultiplier;
            public float ArmInternalMult;
            public float MaxTorque;
            public float XPosForce;
            public float XNegForce;
            public float YPosForce;
            public float YNegForce;
            public float ZPosForce;
            public float ZNegForce;
        }

        private readonly HandSnapshot[] _snapshots = new HandSnapshot[2];
        private bool _locked;
        private float _enforceTimer;

        public bool IsLocked => _locked;

        public void SetLocked(bool locked)
        {
            if (_locked == locked)
                return;

            _locked = locked;
            if (locked)
            {
                CaptureAndKillHands();
            }
            else
            {
                RestoreHands();
            }
        }

        public void Reset()
        {
            _locked = false;
            _enforceTimer = 0f;
            for (int i = 0; i < _snapshots.Length; i++)
                _snapshots[i].Captured = false;
        }

        public void Enforce(float deltaTime)
        {
            if (!_locked)
                return;

            _enforceTimer += deltaTime;
            if (_enforceTimer < 0.02f)
                return;

            _enforceTimer = 0f;
            PhysicsRig? rig = GetPlayerPhysicsRig();
            if (rig == null)
                return;

            KillHand(rig.leftHand, 0);
            KillHand(rig.rightHand, 1);
        }

        public bool IsPlayerPhysHand(PhysHand physHand)
        {
            if (physHand == null)
                return false;

            PhysicsRig? rig = GetPlayerPhysicsRig();
            return rig != null &&
                   ((rig.leftHand != null && rig.leftHand.physHand == physHand) ||
                    (rig.rightHand != null && rig.rightHand.physHand == physHand));
        }

        public void SuppressController(BaseController? controller)
        {
            if (!_locked || controller == null)
                return;

            controller.isGrabInputPressedFinal = false;
            controller.isGrabInputReleasedFinal = false;
            controller._isGrabInputPressedState = false;
            controller._isGrabInputReleasedState = true;
            controller._simGripAxisVive = 0f;
            controller._simGripAxisHolo = 0f;
            controller._simThumbAxis = 0f;
            controller._solvedGrip = 0f;
            controller._solvedGripVelocity = 0f;
            controller._primaryAxis = 0f;
            controller._gripForce = 0f;
            controller._thumbstickAxis = Vector2.zero;
            controller._touchPadAxis = Vector2.zero;
        }

        private void CaptureAndKillHands()
        {
            PhysicsRig? rig = GetPlayerPhysicsRig();
            if (rig == null)
                return;

            CaptureHand(rig.leftHand, 0);
            CaptureHand(rig.rightHand, 1);
            KillHand(rig.leftHand, 0);
            KillHand(rig.rightHand, 1);
        }

        private void CaptureHand(Hand hand, int index)
        {
            if (hand == null || index < 0 || index >= _snapshots.Length || _snapshots[index].Captured)
                return;

            HandSnapshot snapshot = _snapshots[index];
            snapshot.Captured = true;
            snapshot.FarHoverEnabled = hand.farHoverEnabled;
            snapshot.HoverLocked = hand.hoverLocked;
            snapshot.GrabLock = hand.GrabLock;

            PhysHand physHand = hand.physHand;
            if (physHand != null)
            {
                snapshot.PhysShutdown = physHand.shutdown;
                snapshot.GripMult = physHand.gripMult;
                snapshot.ForceMultiplier = physHand._forceMultiplier;
                snapshot.ArmInternalMult = physHand.armInternalMult;
                snapshot.MaxTorque = physHand.maxTorque;
                snapshot.XPosForce = physHand.xPosForce;
                snapshot.XNegForce = physHand.xNegForce;
                snapshot.YPosForce = physHand.yPosForce;
                snapshot.YNegForce = physHand.yNegForce;
                snapshot.ZPosForce = physHand.zPosForce;
                snapshot.ZNegForce = physHand.zNegForce;
            }

            _snapshots[index] = snapshot;
        }

        private void KillHand(Hand hand, int index)
        {
            if (hand == null)
                return;

            ForceRelease(hand);
            SuppressController(hand.Controller);
            hand.farHoverEnabled = false;
            hand.hoverLocked = true;
            hand.GrabLock = true;
            hand.HoveringReceiver = null;
            hand.farHoveringReciever = null;
            hand.SetGripStrength(0f);
            hand.DisableCollider();

            PhysHand physHand = hand.physHand;
            if (physHand == null)
                return;

            physHand.shutdown = true;
            physHand.gripMult = 0f;
            physHand._forceMultiplier = 0f;
            physHand.armInternalMult = 0f;
            physHand._armInternalMult = 0f;
            physHand.xPosForce = 0f;
            physHand.xNegForce = 0f;
            physHand.yPosForce = 0f;
            physHand.yNegForce = 0f;
            physHand.zPosForce = 0f;
            physHand.zNegForce = 0f;
            physHand.maxTorque = 0f;
            physHand.handSupported = 0f;
            physHand._handSupported = 0f;
            physHand._lastForce = Vector3.zero;
            physHand._lastForceMult = 0f;
            physHand.SetFrictionLow();

            Rigidbody rb = physHand.rbHand;
            if (rb != null)
            {
                rb.WakeUp();
                rb.angularDrag = Mathf.Max(rb.angularDrag, 1.4f);
            }
        }

        private void ForceRelease(Hand hand)
        {
            try
            {
                HandReciever receiver = hand.AttachedReceiver;
                if (receiver != null)
                {
                    Grip? grip = receiver as Grip;
                    if (grip != null)
                        grip.ForceDetach(hand);
                }

                if (hand.HasAttachedObject())
                    hand.DetachObject();
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("ForceRelease hand failed: " + ex.Message);
            }
        }

        private void RestoreHands()
        {
            PhysicsRig? rig = GetPlayerPhysicsRig();
            if (rig == null)
                return;

            RestoreHand(rig.leftHand, 0);
            RestoreHand(rig.rightHand, 1);
        }

        private void RestoreHand(Hand hand, int index)
        {
            if (hand == null || index < 0 || index >= _snapshots.Length)
                return;

            HandSnapshot snapshot = _snapshots[index];
            hand.EnableCollider();
            hand.farHoverEnabled = snapshot.Captured ? snapshot.FarHoverEnabled : true;
            hand.hoverLocked = snapshot.Captured && snapshot.HoverLocked;
            hand.GrabLock = snapshot.Captured && snapshot.GrabLock;
            if (!hand.hoverLocked)
                hand.HoverUnlock();

            PhysHand physHand = hand.physHand;
            if (physHand != null)
            {
                physHand.shutdown = snapshot.Captured && snapshot.PhysShutdown;
                physHand.gripMult = snapshot.Captured ? Mathf.Max(0.1f, snapshot.GripMult) : 1f;
                physHand._forceMultiplier = snapshot.Captured ? Mathf.Max(0.1f, snapshot.ForceMultiplier) : 1f;
                physHand.armInternalMult = snapshot.Captured ? Mathf.Max(0.1f, snapshot.ArmInternalMult) : 1f;
                physHand._armInternalMult = physHand.armInternalMult;
                physHand.maxTorque = snapshot.Captured ? Mathf.Max(1f, snapshot.MaxTorque) : 1f;
                physHand.xPosForce = snapshot.Captured ? snapshot.XPosForce : physHand.xPosForce;
                physHand.xNegForce = snapshot.Captured ? snapshot.XNegForce : physHand.xNegForce;
                physHand.yPosForce = snapshot.Captured ? snapshot.YPosForce : physHand.yPosForce;
                physHand.yNegForce = snapshot.Captured ? snapshot.YNegForce : physHand.yNegForce;
                physHand.zPosForce = snapshot.Captured ? snapshot.ZPosForce : physHand.zPosForce;
                physHand.zNegForce = snapshot.Captured ? snapshot.ZNegForce : physHand.zNegForce;
                physHand.SetFrictionNatural();
                physHand.SetRangeOfMotionNatural();
                physHand.ResetHand();
            }

            hand.SetGripStrength(physHand != null ? Mathf.Max(0.1f, physHand.gripMult) : 1f);
            _snapshots[index].Captured = false;
        }

        private static PhysicsRig? GetPlayerPhysicsRig()
        {
            try
            {
                PlayerRefs refs = PlayerRefs.Instance;
                if (refs != null && refs.HasRefs && refs.PlayerPhysicsRig != null)
                    return refs.PlayerPhysicsRig;
            }
            catch (Exception)
            {
                return null;
            }

            return null;
        }
    }
}
