using System;

namespace BonelabAdvancedHealth
{
    public sealed class TacticalVitalsSystem
    {
        private readonly HealthManager _manager;
        private float _infectionRisk;
        private float _internalBleeding;

        public float PulseBpm { get; private set; } = 72f;
        public float SystolicPressure { get; private set; } = 120f;
        public float DiastolicPressure { get; private set; } = 80f;
        public float RespirationRate { get; private set; } = 14f;
        public float InfectionRisk => _infectionRisk;
        public float InternalBleedingNormalized => _internalBleeding;
        public float ShockIndex => SystolicPressure <= 1f ? 0f : PulseBpm / SystolicPressure;

        public TacticalVitalsSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            _infectionRisk = 0f;
            _internalBleeding = 0f;
            PulseBpm = 72f;
            SystolicPressure = 120f;
            DiastolicPressure = 80f;
            RespirationRate = 14f;
        }

        public void OnDamage(DamageInfo info, OrganDamageFeedback feedback)
        {
            if (_manager.IsDead)
                return;

            if (info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Stab)
            {
                float woundContamination = info.DamageType == AdvancedDamageType.Stab ? 0.018f : 0.012f;
                if (feedback.BleedSeverity >= BleedSeverity.Severe)
                    woundContamination *= 1.5f;
                _infectionRisk = Config.Clamp(_infectionRisk + woundContamination, 0f, 1f);
            }

            if (feedback.WoundSeverity == WoundSeverity.OrganRupture || feedback.PrimaryOrgan == OrganType.Liver || feedback.PrimaryOrgan == OrganType.Stomach)
                _internalBleeding = Config.Clamp(_internalBleeding + info.Damage * 0.0055f, 0f, 1f);
        }

        public void RegisterTreatment(MedicalItemType type)
        {
            if (type == MedicalItemType.Medkit)
                _infectionRisk = Math.Max(0f, _infectionRisk - 0.06f);
            else if (type == MedicalItemType.Bandage)
                _infectionRisk = Math.Max(0f, _infectionRisk - 0.025f);
            else if (type == MedicalItemType.BloodPack)
                _internalBleeding = Math.Max(0f, _internalBleeding - 0.04f);
        }

        public void Update(float deltaTime)
        {
            if (_manager.IsDead)
                return;

            float bloodLoss = 1f - _manager.Bleeding.BloodNormalized;
            float shock = Config.Clamp(_manager.Shock.Intensity, 0f, 1.4f);
            float pain = _manager.PainNormalized;
            float stress = _manager.Stress.Normalized;
            float oxygen = _manager.Lungs.OxygenStress;

            PulseBpm = Config.Clamp(68f + stress * 42f + shock * 35f + pain * 18f + oxygen * 22f - bloodLoss * 10f + _manager.Medication.TachycardiaBpm, 36f, 198f);
            SystolicPressure = Config.Clamp(122f - bloodLoss * 52f - shock * 18f - _internalBleeding * 22f + stress * 8f + _manager.Medication.BloodPressureModifier, 48f, 184f);
            DiastolicPressure = Config.Clamp(78f - bloodLoss * 28f - shock * 10f - _internalBleeding * 12f + stress * 4f + _manager.Medication.BloodPressureModifier * 0.42f, 28f, 112f);
            RespirationRate = Config.Clamp(14f + pain * 10f + stress * 8f + oxygen * 14f + _manager.Lungs.BreathingPanic * 10f - _manager.Medication.RespiratoryDepression * 7f, 5f, 42f);

            _infectionRisk = Config.Clamp(_infectionRisk + deltaTime * GetInfectionGrowthRate(), 0f, 1f);
            _internalBleeding = Config.Clamp(_internalBleeding - deltaTime * 0.0012f, 0f, 1f);
        }

        private float GetInfectionGrowthRate()
        {
            if (_infectionRisk <= 0f)
                return 0f;

            float untreatedBleeding = _manager.Bleeding.HasActiveBleeding ? 0.00018f : 0.00004f;
            return untreatedBleeding * (1f + _manager.Bleeding.ActiveBleedCount * 0.12f);
        }
    }
}
