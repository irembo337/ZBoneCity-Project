using System.Collections.Generic;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class BloodFXSystem
    {
        private struct PendingImpact
        {
            public Vector3 Origin;
            public Vector3 Direction;
            public float Intensity;
            public bool Arterial;
            public bool Active;
        }

        private struct ActorBloodState
        {
            public Vector3 LastPosition;
            public float TrailTimer;
            public float FootprintTimer;
            public float StandingPoolTimer;
            public bool LeftFootNext;
            public bool HasPosition;
        }

        private readonly PendingImpact[] _pendingImpacts = new PendingImpact[32];
        private readonly Dictionary<int, ActorBloodState> _actorStates = new Dictionary<int, ActorBloodState>(64);
        private readonly NativeBloodIntegration _nativeBlood = new NativeBloodIntegration();
        private int _nextPendingImpact;

        public bool UsingNativeBlood => _nativeBlood.UsingNativeBlood;

        public void Reset()
        {
            _nextPendingImpact = 0;
            _actorStates.Clear();
            for (int i = 0; i < _pendingImpacts.Length; i++)
                _pendingImpacts[i].Active = false;
            _nativeBlood.Reset();
        }

        public void Update(float deltaTime, HealthManager? player, IEnumerable<NPCHealth> npcs)
        {
            if (!Config.BloodFxEnabled)
                return;

            FlushPendingImpacts();
            _nativeBlood.Update(deltaTime);

            if (player != null)
                UpdateActorBlood(deltaTime, player, GetPlayerBloodPosition(), true);

            foreach (NPCHealth npc in npcs)
                UpdateActorBlood(deltaTime, npc, npc.GetEffectPosition(), false);
        }

        public void OnDamage(HealthManager manager, DamageInfo info, OrganDamageFeedback organFeedback)
        {
            if (!Config.BloodFxEnabled)
                return;

            bool shouldBleed = organFeedback.BleedSeverity != BleedSeverity.None ||
                               info.DamageType == AdvancedDamageType.Bullet ||
                               info.DamageType == AdvancedDamageType.Stab;
            if (!shouldBleed)
                return;

            Vector3 origin = info.Origin;
            if (origin.sqrMagnitude < 0.001f)
                origin = manager.Kind == HealthOwnerKind.Player ? GetPlayerBloodPosition() : GetNpcPosition(manager);

            float intensity = GetIntensity(info, organFeedback.BleedSeverity);
            QueueImpact(origin, GetBloodDirection(info), intensity, organFeedback.BleedSeverity == BleedSeverity.Arterial);
            _nativeBlood.EmitWallSplat(origin, GetBloodDirection(info), intensity, organFeedback.BleedSeverity == BleedSeverity.Arterial);
        }

        public void OnKnifeRemoved(DamageInfo info, BleedSeverity severity)
        {
            if (!Config.BloodFxEnabled)
                return;

            QueueImpact(info.Origin, GetBloodDirection(info), Config.Clamp(info.Damage / 55f, 0.45f, 1.4f), severity == BleedSeverity.Arterial);
        }

        private void QueueImpact(Vector3 origin, Vector3 direction, float intensity, bool arterial)
        {
            int index = _nextPendingImpact++ % _pendingImpacts.Length;
            _pendingImpacts[index] = new PendingImpact
            {
                Origin = origin,
                Direction = direction,
                Intensity = intensity,
                Arterial = arterial,
                Active = true
            };
        }

        private void FlushPendingImpacts()
        {
            for (int i = 0; i < _pendingImpacts.Length; i++)
            {
                if (!_pendingImpacts[i].Active)
                    continue;

                PendingImpact impact = _pendingImpacts[i];
                _pendingImpacts[i].Active = false;
                _nativeBlood.EmitImpact(impact.Origin, impact.Direction, impact.Intensity, impact.Arterial);
            }
        }

        private void UpdateActorBlood(float deltaTime, HealthManager manager, Vector3 position, bool isPlayer)
        {
            if (position.sqrMagnitude < 0.001f || manager.IsDead)
                return;

            float pressure = manager.Organs.HeartbeatStrength;
            float rate = manager.Bleeding.TotalBleedRateMlPerSecond * Mathf.Lerp(0.45f, 1.25f, pressure);
            if (rate <= 2.0f)
                return;

            float intensity = Config.Clamp(rate / 55f, 0.08f, 1.3f);
            if (rate >= 8f)
                _nativeBlood.EmitImpact(position + Vector3.up * 0.55f, Vector3.down, intensity * 0.65f, rate >= 36f);

            float interval = Mathf.Lerp(1.1f, 0.16f, Config.Clamp(intensity, 0f, 1f)) / Mathf.Max(0.15f, Config.BloodFxDensity);
            int actorKey = isPlayer ? -1 : manager.OwnerId;
            if (!_actorStates.TryGetValue(actorKey, out ActorBloodState state))
            {
                state = new ActorBloodState
                {
                    LastPosition = position,
                    LeftFootNext = true,
                    HasPosition = true
                };
            }

            float moved = state.HasPosition ? (position - state.LastPosition).magnitude : 999f;
            bool walking = moved > 0.055f;
            bool stronglyBleeding = rate >= 14f;
            state.TrailTimer += deltaTime;
            state.FootprintTimer += deltaTime;
            state.StandingPoolTimer += deltaTime;

            if (isPlayer)
            {
                if (state.TrailTimer >= interval && moved > 0.18f)
                {
                    state.TrailTimer = 0f;
                    state.LastPosition = position;
                    state.HasPosition = true;
                    _nativeBlood.EmitTrail(position, intensity);
                }

                if (state.FootprintTimer >= 0.36f && moved > 0.12f)
                {
                    state.FootprintTimer = 0f;
                    Vector3 side = GetPlayerRightVector() * (state.LeftFootNext ? -0.09f : 0.09f);
                    state.LeftFootNext = !state.LeftFootNext;
                    _nativeBlood.EmitFootprint(position + side, GetPlayerForwardVector(), intensity);
                }
            }
            else if (walking && Random.value < deltaTime / interval)
            {
                _nativeBlood.EmitTrail(position, intensity);
                state.LastPosition = position;
                state.HasPosition = true;
            }

            if (!walking && stronglyBleeding && state.StandingPoolTimer >= Mathf.Lerp(3.2f, 0.65f, Config.Clamp(intensity, 0f, 1f)))
            {
                state.StandingPoolTimer = 0f;
                _nativeBlood.EmitStandingPool(position, intensity * (manager.Bleeding.GetWorstBleedingSeverity(manager.Bleeding.GetWorstBleedingPart()) == BleedSeverity.Arterial ? 1.35f : 1f));
            }

            if (walking)
                state.StandingPoolTimer = Mathf.Min(state.StandingPoolTimer, 0.35f);
            state.LastPosition = position;
            state.HasPosition = true;
            _actorStates[actorKey] = state;
        }

        private static Vector3 GetBloodDirection(DamageInfo info)
        {
            if (info.Direction.sqrMagnitude > 0.01f)
                return info.Direction.normalized;
            return Vector3.down;
        }

        public void OnMedicalTreatment(HealthManager manager, MedicalItemType itemType, BodyPart part)
        {
            if (!Config.BloodFxEnabled)
                return;

            if (manager.Bleeding.GetWorstBleedingSeverity(part) == BleedSeverity.None && manager.Bleeding.TotalBleedRateMlPerSecond < 3f)
                return;

            Transform? head = MainMod.Runtime?.GetHeadTransform();
            if (head == null)
                return;

            Vector3 origin = head.position + head.forward * 0.35f + Vector3.down * 0.18f;
            float intensity = itemType == MedicalItemType.Tourniquet ? 0.9f : 0.55f;
            _nativeBlood.EmitHandprint(origin, head.forward, intensity);
            _nativeBlood.EmitBloodTransfer(origin + Vector3.down * 0.3f, intensity);
        }

        private static float GetIntensity(DamageInfo info, BleedSeverity severity)
        {
            float intensity = Config.Clamp(info.Damage / 75f, 0.08f, 1.15f);
            if (severity == BleedSeverity.Arterial)
                intensity = Mathf.Max(intensity, 0.95f);
            else if (severity == BleedSeverity.Severe)
                intensity = Mathf.Max(intensity, 0.65f);
            return intensity;
        }

        private static Vector3 GetPlayerBloodPosition()
        {
            MainMod? runtime = MainMod.Runtime;
            if (runtime != null && runtime.TryGetPlayerFeetPosition(out Vector3 feet))
                return feet;

            Transform? head = runtime?.GetHeadTransform();
            if (head != null)
                return head.position + Vector3.down * 1.2f;
            return Vector3.zero;
        }

        private static Vector3 GetPlayerForwardVector()
        {
            Transform? head = MainMod.Runtime?.GetHeadTransform();
            if (head == null)
                return Vector3.forward;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            return forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        }

        private static Vector3 GetPlayerRightVector()
        {
            Transform? head = MainMod.Runtime?.GetHeadTransform();
            if (head == null)
                return Vector3.right;
            Vector3 right = Vector3.ProjectOnPlane(head.right, Vector3.up);
            return right.sqrMagnitude > 0.01f ? right.normalized : Vector3.right;
        }

        private static Vector3 GetNpcPosition(HealthManager manager)
        {
            if (manager is NPCHealth npc)
                return npc.GetEffectPosition();
            return Vector3.zero;
        }
    }
}
