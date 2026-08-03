using System;

namespace BonelabAdvancedHealth
{
    public sealed class ShockSystem
    {
        private readonly HealthManager _manager;
        private float _shock;
        private float _tickAccumulator;
        private static readonly float[] TreatmentRelief =
        {
            0.08f,
            0.14f,
            0.05f,
            0.18f,
            0.10f,
            0.00f,
            0.28f,
            0.05f
        };

        public float Intensity => _shock;
        public ShockSeverity Severity { get; private set; }
        public float MovementPenalty { get; private set; }
        public float StaminaPenalty { get; private set; }
        public float TunnelVision { get; private set; }
        public float UnconsciousnessPressure { get; private set; }
        public bool IsActive => Severity != ShockSeverity.None;

        public ShockSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            _shock = 0f;
            _tickAccumulator = 0f;
            Severity = ShockSeverity.None;
            MovementPenalty = 0f;
            StaminaPenalty = 0f;
            TunnelVision = 0f;
            UnconsciousnessPressure = 0f;
        }

        public void OnDamage(DamageInfo info, OrganDamageFeedback organFeedback)
        {
            if (_manager.IsDead)
                return;

            float trauma = info.Damage * 0.003f + info.Pain * 0.002f + info.UnconsciousnessImpulse * 0.12f;
            if (info.BodyPart == BodyPart.Head || info.BodyPart == BodyPart.Torso)
                trauma *= 1.18f;
            if (info.IsHighEnergyImpact)
                trauma *= 1.25f;
            if (organFeedback.InstantCollapse)
                trauma += 0.32f;
            if (organFeedback.BleedSeverity >= BleedSeverity.Severe)
                trauma += 0.18f;

            _shock = Config.Clamp(_shock + trauma, 0f, 1.75f);
            Recalculate();
        }

        public void Update(float deltaTime)
        {
            if (_manager.IsDead)
            {
                Reset();
                return;
            }

            _tickAccumulator += deltaTime;
            if (_tickAccumulator < 0.25f)
                return;

            float elapsed = _tickAccumulator;
            _tickAccumulator = 0f;

            // Shock is a derived vital state: wounds and blood loss drive it, medicine stabilizes it.
            float bloodLoss = 1f - _manager.Bleeding.BloodNormalized;
            float bleedPressure = Config.Clamp(_manager.Bleeding.TotalBleedRateMlPerSecond / 45f, 0f, 1f);
            float injuryPressure = Config.Clamp((_manager.Bleeding.ActiveBleedCount * 0.055f) + _manager.TotalTraumaNormalized * 0.32f, 0f, 0.75f);
            float painPressure = _manager.PainNormalized * 0.22f;
            float oxygenPressure = _manager.Lungs.OxygenStress * 0.18f;
            float target = Config.Clamp(bloodLoss * 1.22f + bleedPressure * 0.45f + injuryPressure + painPressure + oxygenPressure, 0f, 1.75f);
            float towardRate = target > _shock ? 0.42f : 0.065f;
            _shock = MoveToward(_shock, target, elapsed * towardRate);
            Recalculate();
        }

        public void RegisterTreatment(MedicalItemType type)
        {
            int index = (int)type;
            if (index >= 0 && index < TreatmentRelief.Length)
                _shock = Math.Max(0f, _shock - TreatmentRelief[index]);

            Recalculate();
        }

        private void Recalculate()
        {
            float n = Config.Clamp(_shock, 0f, 1f);
            if (_shock >= 1.15f)
                Severity = ShockSeverity.Critical;
            else if (_shock >= 0.82f)
                Severity = ShockSeverity.Severe;
            else if (_shock >= 0.52f)
                Severity = ShockSeverity.Moderate;
            else if (_shock >= 0.24f)
                Severity = ShockSeverity.Mild;
            else
                Severity = ShockSeverity.None;

            MovementPenalty = Config.Clamp(n * 0.36f, 0f, 0.42f);
            StaminaPenalty = Config.Clamp(n * 0.58f, 0f, 0.75f);
            TunnelVision = Config.Clamp(Math.Max(0f, n - 0.25f) * 1.08f, 0f, 1f);
            UnconsciousnessPressure = Config.Clamp(Math.Max(0f, n - 0.55f) * 0.82f, 0f, 0.74f);
        }

        private static float MoveToward(float value, float target, float maxDelta)
        {
            if (value < target)
                return Math.Min(value + maxDelta, target);
            return Math.Max(value - maxDelta, target);
        }
    }
}
