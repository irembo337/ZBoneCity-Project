using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class KnifePenetrationSystem
    {
        private struct EmbeddedKnife
        {
            public bool Active;
            public Collider? Collider;
            public BodyPart BodyPart;
            public OrganType Organ;
            public Vector3 AnchorPosition;
            public Vector3 Direction;
            public float Depth;
            public float Age;
            public float BleedPulse;
        }

        private readonly HealthManager _manager;
        private readonly EmbeddedKnife[] _embedded = new EmbeddedKnife[8];

        public int EmbeddedCount { get; private set; }

        public KnifePenetrationSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            for (int i = 0; i < _embedded.Length; i++)
                _embedded[i] = default;
            EmbeddedCount = 0;
        }

        public void ApplyDamage(DamageInfo info, OrganDamageFeedback organFeedback)
        {
            if (!Config.KnifePenetrationEnabled || info.DamageType != AdvancedDamageType.Stab || info.Damage < 7f || info.SourceCollider == null)
                return;

            int index = FindSlot(info.SourceCollider.GetInstanceID());
            float depth = Config.Clamp(info.Damage / 55f, 0.08f, 0.82f);
            Vector3 anchor = info.Origin.sqrMagnitude > 0.001f ? info.Origin : info.SourceCollider.transform.position;
            _embedded[index] = new EmbeddedKnife
            {
                Active = true,
                Collider = info.SourceCollider,
                BodyPart = info.BodyPart,
                Organ = organFeedback.PrimaryOrgan,
                AnchorPosition = anchor,
                Direction = info.Direction.sqrMagnitude > 0.01f ? info.Direction.normalized : Vector3.forward,
                Depth = depth,
                Age = 0f,
                BleedPulse = 0f
            };
            Recount();

            LimbHealth limb = _manager.GetLimb(info.BodyPart);
            BleedSeverity severity = depth > 0.62f ? BleedSeverity.Severe : BleedSeverity.Medium;
            _manager.Bleeding.AddBleed(info, limb, severity, 0.55f + depth, 1.5f, depth > 0.62f ? WoundSeverity.DeepCut : WoundSeverity.SurfaceCut);
            _manager.AddPain(8f + depth * 22f);
        }

        public void Update(float deltaTime)
        {
            if (EmbeddedCount == 0 || _manager.IsDead)
                return;

            for (int i = 0; i < _embedded.Length; i++)
            {
                if (!_embedded[i].Active)
                    continue;

                EmbeddedKnife knife = _embedded[i];
                knife.Age += deltaTime;
                knife.BleedPulse += deltaTime;

                bool removed = IsRemoved(knife);
                if (removed && knife.Age > 0.12f)
                {
                    TriggerRemoval(i, knife);
                    continue;
                }

                if (knife.BleedPulse >= 1.0f)
                {
                    knife.BleedPulse = 0f;
                    _manager.AddPain(0.8f + knife.Depth * 2.6f);
                }

                _embedded[i] = knife;
            }
        }

        private bool IsRemoved(EmbeddedKnife knife)
        {
            try
            {
                if (knife.Collider == null)
                    return true;

                Vector3 current = knife.Collider.transform.position;
                float allowed = 0.10f + knife.Depth * 0.18f;
                return (current - knife.AnchorPosition).sqrMagnitude > allowed * allowed;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private void TriggerRemoval(int index, EmbeddedKnife knife)
        {
            _embedded[index] = default;
            Recount();

            LimbHealth limb = _manager.GetLimb(knife.BodyPart);
            BleedSeverity spike = knife.Depth > 0.55f || knife.Organ == OrganType.Heart || knife.Organ == OrganType.Liver
                ? BleedSeverity.Arterial
                : BleedSeverity.Severe;

            DamageInfo removalInfo = new DamageInfo(
                knife.BodyPart,
                AdvancedDamageType.Stab,
                24f + knife.Depth * 38f,
                1.85f,
                0f,
                35f + knife.Depth * 45f,
                0.08f,
                knife.AnchorPosition,
                -knife.Direction,
                _manager.OwnerId,
                0,
                Time.time,
                knife.Collider);

            _manager.Bleeding.AddBleed(removalInfo, limb, spike, 1.25f + knife.Depth * 2.2f, 1.35f, spike == BleedSeverity.Arterial ? WoundSeverity.ArterialCut : WoundSeverity.DeepCut);
            _manager.AddPain(removalInfo.Pain);
            MainMod.Runtime?.BloodFx.OnKnifeRemoved(removalInfo, spike);
        }

        private int FindSlot(int colliderId)
        {
            int oldest = 0;
            float oldestAge = -1f;
            for (int i = 0; i < _embedded.Length; i++)
            {
                if (!_embedded[i].Active)
                    return i;
                Collider? collider = _embedded[i].Collider;
                if (collider != null && collider.GetInstanceID() == colliderId)
                    return i;
                if (_embedded[i].Age > oldestAge)
                {
                    oldestAge = _embedded[i].Age;
                    oldest = i;
                }
            }

            return oldest;
        }

        private void Recount()
        {
            int count = 0;
            for (int i = 0; i < _embedded.Length; i++)
            {
                if (_embedded[i].Active)
                    count++;
            }

            EmbeddedCount = count;
        }
    }
}
