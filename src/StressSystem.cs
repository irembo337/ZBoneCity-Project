using System;

namespace BonelabAdvancedHealth
{
    public sealed class StressSystem
    {
        private readonly HealthManager _manager;
        private float _stress;
        private float _decayDelay;
        private float _nearGunfirePressure;
        private float _deathWitnessPressure;

        public float Stress => _stress;
        public float Normalized => Config.Clamp(_stress, 0f, 1f);
        public float HandShake => Config.Clamp(Normalized * 0.55f + _nearGunfirePressure * 0.22f, 0f, 1f);
        public float BreathingIntensity => Config.Clamp(Normalized * 0.65f + _manager.PainSystem.BreathingStress * 0.22f, 0f, 1f);
        public float PulseIncrease => Config.Clamp(Normalized * 42f + _nearGunfirePressure * 12f, 0f, 58f);
        public float WeaponInstability => Config.Clamp(Normalized * 0.42f + HandShake * 0.24f, 0f, 1f);
        public float BodycamShake => Config.Clamp(Normalized * 0.35f + _manager.PainSystem.ShakeIntensity * 0.16f, 0f, 1f);

        public StressSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            _stress = 0f;
            _decayDelay = 0f;
            _nearGunfirePressure = 0f;
            _deathWitnessPressure = 0f;
        }

        public void OnDamage(DamageInfo info, OrganDamageFeedback feedback)
        {
            if (_manager.IsDead)
                return;

            float added = info.Damage * 0.0032f + info.Pain * 0.0018f + info.UnconsciousnessImpulse * 0.08f;
            if (info.DamageType == AdvancedDamageType.Bullet)
                added += 0.10f;
            if (info.DamageType == AdvancedDamageType.Explosion)
                added += 0.22f;
            if (info.BodyPart == BodyPart.Head)
                added += 0.12f;
            if (feedback.BleedSeverity >= BleedSeverity.Severe)
                added += 0.16f;
            if (feedback.InstantCollapse)
                added += 0.24f;

            AddStress(added);
        }

        public void OnBloodLoss(float amountMl)
        {
            if (amountMl <= 0f || _manager.IsDead)
                return;

            float lowBlood = 1f - _manager.Bleeding.BloodNormalized;
            AddStress(amountMl * 0.00055f * (1f + lowBlood * 1.5f));
        }

        public void RegisterNearbyGunfire(float intensity)
        {
            _nearGunfirePressure = Config.Clamp(_nearGunfirePressure + intensity, 0f, 1f);
            AddStress(0.08f * Config.Clamp(intensity, 0f, 1f));
        }

        public void RegisterWitnessedDeath(float intensity)
        {
            _deathWitnessPressure = Config.Clamp(_deathWitnessPressure + intensity, 0f, 1f);
            AddStress(0.18f * Config.Clamp(intensity, 0f, 1f));
        }

        public void RegisterTraumaticEvent(float intensity)
        {
            AddStress(0.14f * Config.Clamp(intensity, 0f, 1f));
        }

        public void RegisterTreatment(MedicalItemType type)
        {
            float relief = type switch
            {
                MedicalItemType.Morphine => 0.18f,
                MedicalItemType.Adrenaline => 0.07f,
                MedicalItemType.Painkillers => 0.11f,
                MedicalItemType.Medkit => 0.08f,
                MedicalItemType.BloodPack => 0.14f,
                _ => 0.04f
            };
            _stress = Math.Max(0f, _stress - relief);
        }

        public void Update(float deltaTime)
        {
            if (_manager.IsDead)
            {
                Reset();
                return;
            }

            _nearGunfirePressure = Math.Max(0f, _nearGunfirePressure - deltaTime * 0.25f);
            _deathWitnessPressure = Math.Max(0f, _deathWitnessPressure - deltaTime * 0.055f);

            float bloodLoss = 1f - _manager.Bleeding.BloodNormalized;
            float medicalPressure = bloodLoss * 0.44f +
                                    _manager.Shock.Intensity * 0.30f +
                                    _manager.PainNormalized * 0.22f +
                                    _manager.Brain.DisorientationNormalized * 0.18f +
                                    _manager.Bleeding.ActiveBleedCount * 0.025f;
            float target = Config.Clamp(medicalPressure + _nearGunfirePressure * 0.20f + _deathWitnessPressure * 0.16f, 0f, 1f);
            if (target > _stress)
            {
                _stress = MoveToward(_stress, target, deltaTime * 0.42f);
                _decayDelay = 5f;
                return;
            }

            _decayDelay = Math.Max(0f, _decayDelay - deltaTime);
            if (_decayDelay <= 0f)
                _stress = MoveToward(_stress, target, deltaTime * 0.045f);
        }

        private void AddStress(float amount)
        {
            if (amount <= 0f)
                return;

            _stress = Config.Clamp(_stress + amount, 0f, 1f);
            _decayDelay = Math.Max(_decayDelay, 4.5f);
        }

        private static float MoveToward(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
                return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
    }
}
