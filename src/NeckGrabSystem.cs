using System;
using System.Collections.Generic;
using Il2CppSLZ.Marrow;
using UnityEngine;
using MarrowHand = Il2CppSLZ.Marrow.Hand;

namespace BonelabAdvancedHealth
{
    public sealed class NeckGrabSystem
    {
        private const float CloseRangeMeters = 1.35f;
        private const float NeckHandRadius = 0.32f;
        private const float HoldRequiredSeconds = 0.18f;
        private const float TwistAngleDegrees = 58f;
        private const float FastHandSpeed = 2.35f;
        private const float CooldownSeconds = 3.0f;

        private readonly Dictionary<int, float> _targetCooldowns = new Dictionary<int, float>(32);
        private readonly int[] _cooldownKeyScratch = new int[64];
        private int _candidateId;
        private float _holdSeconds;
        private Vector3 _previousLeft;
        private Vector3 _previousRight;
        private Vector3 _previousHandVector;
        private PhysicsRig? _cachedRig;
        private float _rigProbeCooldown;
        private bool _hasPrevious;
        private float _globalCooldown;

        public void Reset()
        {
            _targetCooldowns.Clear();
            _candidateId = 0;
            _holdSeconds = 0f;
            _previousLeft = Vector3.zero;
            _previousRight = Vector3.zero;
            _previousHandVector = Vector3.zero;
            _cachedRig = null;
            _rigProbeCooldown = 0f;
            _hasPrevious = false;
            _globalCooldown = 0f;
        }

        public void Update(float deltaTime, IEnumerable<NPCHealth> npcs)
        {
            if (!Config.NeckSnapEnabled || !Config.Enabled)
                return;

            _globalCooldown = Math.Max(0f, _globalCooldown - deltaTime);
            DecayCooldowns(deltaTime);
            if (_globalCooldown > 0f || !TryGetHands(deltaTime, out HandProbe left, out HandProbe right))
            {
                ResetCandidate();
                return;
            }

            NPCHealth? target = FindCandidate(npcs, left.Position, right.Position, out Vector3 neckPosition);
            if (target == null)
            {
                ResetCandidate();
                StorePrevious(left.Position, right.Position);
                return;
            }

            if (_candidateId != target.OwnerId)
            {
                _candidateId = target.OwnerId;
                _holdSeconds = 0f;
                _hasPrevious = false;
            }

            _holdSeconds += deltaTime;
            bool snapped = _holdSeconds >= HoldRequiredSeconds && IsForcefulTwist(left.Position, right.Position, neckPosition, deltaTime);
            StorePrevious(left.Position, right.Position);
            if (!snapped)
                return;

            target.Neck.ApplyForcedSnap(true);
            _targetCooldowns[target.OwnerId] = CooldownSeconds;
            _globalCooldown = CooldownSeconds;
            ResetCandidate();
            MainMod.Runtime?.NotifyHudMedicalFeedback("Neck restraint neutralized target");
        }

        private NPCHealth? FindCandidate(IEnumerable<NPCHealth> npcs, Vector3 leftHand, Vector3 rightHand, out Vector3 neckPosition)
        {
            neckPosition = Vector3.zero;
            Transform? playerHead = MainMod.Runtime?.GetHeadTransform();
            if (playerHead == null)
                return null;

            NPCHealth? best = null;
            float bestScore = float.PositiveInfinity;
            foreach (NPCHealth npc in npcs)
            {
                if (npc == null || !npc.CanReceiveNeckSnap() || _targetCooldowns.ContainsKey(npc.OwnerId))
                    continue;

                Vector3 neck = EstimateNeckPosition(npc);
                if (neck == Vector3.zero)
                    continue;

                float playerDistance = Vector3.Distance(playerHead.position, neck);
                if (playerDistance > CloseRangeMeters)
                    continue;

                float leftDistance = Vector3.Distance(leftHand, neck);
                float rightDistance = Vector3.Distance(rightHand, neck);
                if (leftDistance > NeckHandRadius || rightDistance > NeckHandRadius)
                    continue;

                float handSeparation = Vector3.Distance(leftHand, rightHand);
                if (handSeparation < 0.15f || handSeparation > 0.62f)
                    continue;

                float score = leftDistance + rightDistance + playerDistance * 0.15f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = npc;
                    neckPosition = neck;
                }
            }

            return best;
        }

        private bool IsForcefulTwist(Vector3 left, Vector3 right, Vector3 neck, float deltaTime)
        {
            if (!_hasPrevious || deltaTime <= 0f)
                return false;

            Vector3 currentVector = right - left;
            if (currentVector.sqrMagnitude < 0.02f || _previousHandVector.sqrMagnitude < 0.02f)
                return false;

            float angle = Vector3.Angle(_previousHandVector, currentVector);
            float leftSpeed = (left - _previousLeft).magnitude / deltaTime;
            float rightSpeed = (right - _previousRight).magnitude / deltaTime;
            float radialTorque = Mathf.Abs(Vector3.Dot(Vector3.Cross(_previousHandVector.normalized, currentVector.normalized), Vector3.up));
            bool oneHandAnchored = leftSpeed < 1.25f || rightSpeed < 1.25f;
            bool fastTwist = Math.Max(leftSpeed, rightSpeed) >= FastHandSpeed;
            bool closeToNeck = Vector3.Distance((left + right) * 0.5f, neck) <= NeckHandRadius;
            return closeToNeck && fastTwist && oneHandAnchored && angle >= TwistAngleDegrees && radialTorque > 0.42f;
        }

        private static Vector3 EstimateNeckPosition(NPCHealth npc)
        {
            Transform? transform = npc.GetEffectTransform();
            if (transform == null)
                return Vector3.zero;

            return transform.position + Vector3.up * 1.18f + transform.forward * 0.04f;
        }

        private bool TryGetHands(float deltaTime, out HandProbe left, out HandProbe right)
        {
            left = default;
            right = default;
            try
            {
                PhysicsRig? physicsRig = _cachedRig;
                if (physicsRig == null)
                {
                    _rigProbeCooldown = Math.Max(0f, _rigProbeCooldown - deltaTime);
                    if (_rigProbeCooldown > 0f)
                        return false;

                    _rigProbeCooldown = 0.75f;
                    physicsRig = UnityEngine.Object.FindObjectOfType<PhysicsRig>();
                    _cachedRig = physicsRig;
                }

                if (physicsRig == null)
                    return false;

                left = FromHand(physicsRig.leftHand);
                right = FromHand(physicsRig.rightHand);
                return left.Valid && right.Valid;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static HandProbe FromHand(MarrowHand hand)
        {
            return hand == null ? default : new HandProbe(true, hand.transform.position);
        }

        private void StorePrevious(Vector3 left, Vector3 right)
        {
            _previousLeft = left;
            _previousRight = right;
            _previousHandVector = right - left;
            _hasPrevious = true;
        }

        private void ResetCandidate()
        {
            _candidateId = 0;
            _holdSeconds = 0f;
            _hasPrevious = false;
        }

        private void DecayCooldowns(float deltaTime)
        {
            if (_targetCooldowns.Count == 0)
                return;

            int count = 0;
            foreach (KeyValuePair<int, float> pair in _targetCooldowns)
            {
                if (count >= _cooldownKeyScratch.Length)
                    break;
                _cooldownKeyScratch[count++] = pair.Key;
            }

            for (int i = 0; i < count; i++)
            {
                int key = _cooldownKeyScratch[i];
                float next = _targetCooldowns[key] - deltaTime;
                if (next <= 0f)
                    _targetCooldowns.Remove(key);
                else
                    _targetCooldowns[key] = next;
            }
        }

        private readonly struct HandProbe
        {
            public readonly bool Valid;
            public readonly Vector3 Position;

            public HandProbe(bool valid, Vector3 position)
            {
                Valid = valid;
                Position = position;
            }
        }
    }
}
