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

        private readonly PendingImpact[] _pendingImpacts = new PendingImpact[32];
        private readonly NativeBloodIntegration _nativeBlood = new NativeBloodIntegration();
        private float _playerTrailTimer;
        private Vector3 _lastPlayerTrailPosition;
        private int _nextPendingImpact;

        public bool UsingNativeBlood => _nativeBlood.UsingNativeBlood;

        public void Reset()
        {
            _playerTrailTimer = 0f;
            _lastPlayerTrailPosition = Vector3.zero;
            _nextPendingImpact = 0;
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
            if (isPlayer)
            {
                _playerTrailTimer += deltaTime;
                float moved = _lastPlayerTrailPosition == Vector3.zero ? 999f : (position - _lastPlayerTrailPosition).magnitude;
                if (_playerTrailTimer >= interval && moved > 0.18f)
                {
                    _playerTrailTimer = 0f;
                    _lastPlayerTrailPosition = position;
                    _nativeBlood.EmitTrail(position, intensity);
                }
            }
            else if (Random.value < deltaTime / interval)
            {
                _nativeBlood.EmitTrail(position, intensity);
            }
        }

        private static Vector3 GetBloodDirection(DamageInfo info)
        {
            if (info.Direction.sqrMagnitude > 0.01f)
                return info.Direction.normalized;
            return Vector3.down;
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

        private static Vector3 GetNpcPosition(HealthManager manager)
        {
            if (manager is NPCHealth npc)
                return npc.GetEffectPosition();
            return Vector3.zero;
        }
    }
}
