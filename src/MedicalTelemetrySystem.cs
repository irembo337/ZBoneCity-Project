using System;

namespace BonelabAdvancedHealth
{
    public readonly struct MedicalTelemetrySnapshot
    {
        public MedicalTelemetrySnapshot(
            float bloodVolumeMl,
            float bloodNormalized,
            float bloodLossNormalized,
            float activeBleedRateMlPerSecond,
            float pulseBpm,
            float heartbeatBpm,
            float systolicPressure,
            float diastolicPressure,
            float respirationRate,
            float oxygenNormalized,
            float oxygenStress,
            float painNormalized,
            float shockNormalized,
            ShockSeverity shockSeverity,
            float stressNormalized,
            float internalBleedingNormalized,
            float infectionRisk,
            float brainTraumaNormalized,
            float traumaNormalized,
            float consciousnessRisk,
            float vitalDanger,
            ConsciousnessState consciousnessState,
            bool isDead,
            bool isUnconscious,
            bool isCritical,
            bool requiresImmediateAid)
        {
            BloodVolumeMl = bloodVolumeMl;
            BloodNormalized = bloodNormalized;
            BloodLossNormalized = bloodLossNormalized;
            ActiveBleedRateMlPerSecond = activeBleedRateMlPerSecond;
            PulseBpm = pulseBpm;
            HeartbeatBpm = heartbeatBpm;
            SystolicPressure = systolicPressure;
            DiastolicPressure = diastolicPressure;
            RespirationRate = respirationRate;
            OxygenNormalized = oxygenNormalized;
            OxygenStress = oxygenStress;
            PainNormalized = painNormalized;
            ShockNormalized = shockNormalized;
            ShockSeverity = shockSeverity;
            StressNormalized = stressNormalized;
            InternalBleedingNormalized = internalBleedingNormalized;
            InfectionRisk = infectionRisk;
            BrainTraumaNormalized = brainTraumaNormalized;
            TraumaNormalized = traumaNormalized;
            ConsciousnessRisk = consciousnessRisk;
            VitalDanger = vitalDanger;
            ConsciousnessState = consciousnessState;
            IsDead = isDead;
            IsUnconscious = isUnconscious;
            IsCritical = isCritical;
            RequiresImmediateAid = requiresImmediateAid;
        }

        public float BloodVolumeMl { get; }
        public float BloodNormalized { get; }
        public float BloodLossNormalized { get; }
        public float ActiveBleedRateMlPerSecond { get; }
        public float PulseBpm { get; }
        public float HeartbeatBpm { get; }
        public float SystolicPressure { get; }
        public float DiastolicPressure { get; }
        public float RespirationRate { get; }
        public float OxygenNormalized { get; }
        public float OxygenStress { get; }
        public float PainNormalized { get; }
        public float ShockNormalized { get; }
        public ShockSeverity ShockSeverity { get; }
        public float StressNormalized { get; }
        public float InternalBleedingNormalized { get; }
        public float InfectionRisk { get; }
        public float BrainTraumaNormalized { get; }
        public float TraumaNormalized { get; }
        public float ConsciousnessRisk { get; }
        public float VitalDanger { get; }
        public ConsciousnessState ConsciousnessState { get; }
        public bool IsDead { get; }
        public bool IsUnconscious { get; }
        public bool IsCritical { get; }
        public bool RequiresImmediateAid { get; }
    }

    public sealed class MedicalTelemetrySystem
    {
        private readonly HealthManager _manager;
        private float _tickAccumulator;

        public MedicalTelemetrySnapshot Snapshot { get; private set; }

        public MedicalTelemetrySystem(HealthManager manager)
        {
            _manager = manager;
            Reset();
        }

        public void Reset()
        {
            _tickAccumulator = 0f;
            Snapshot = new MedicalTelemetrySnapshot(
                5000f,
                1f,
                0f,
                0f,
                72f,
                72f,
                120f,
                80f,
                14f,
                1f,
                0f,
                0f,
                0f,
                ShockSeverity.None,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                0f,
                ConsciousnessState.Awake,
                false,
                false,
                false,
                false);
        }

        public void Update(float deltaTime)
        {
            _tickAccumulator += deltaTime;
            if (_tickAccumulator < 0.25f)
                return;

            _tickAccumulator = 0f;
            Refresh();
        }

        public void Refresh()
        {
            float bloodNormalized = Config.Clamp(_manager.Bleeding.BloodNormalized, 0f, 1f);
            float bloodLoss = 1f - bloodNormalized;
            float oxygenNormalized = Config.Clamp(_manager.Lungs.OxygenNormalized, 0f, 1f);
            float oxygenStress = 1f - oxygenNormalized;
            float pain = _manager.PainNormalized;
            float shock = Config.Clamp(_manager.Shock.Intensity, 0f, 1.75f);
            float shockNormalized = Config.Clamp(shock, 0f, 1f);
            float stress = _manager.Stress.Normalized;
            float brain = _manager.Brain.DisorientationNormalized;
            float internalBleeding = _manager.TacticalVitals.InternalBleedingNormalized;
            float trauma = _manager.TotalTraumaNormalized;
            ZCityOrganismProfile zcity = _manager.ZCityOrganism;
            bool unconscious = _manager.Consciousness.State == ConsciousnessState.Unconscious || _manager.Coma.IsActive;
            bool dead = _manager.IsDead;

            float pulse = dead ? 0f : Config.Clamp((_manager.TacticalVitals.PulseBpm * 0.45f) + (zcity.PulseBpm * 0.55f), 0f, 220f);
            float heartbeat = dead ? 0f : Config.Clamp(
                (zcity.HeartbeatBpm * 0.62f) +
                (pulse * 0.38f) +
                stress * 14f +
                shockNormalized * 18f +
                pain * 10f -
                Math.Max(0f, bloodLoss - 0.42f) * 28f,
                0f,
                230f);

            float consciousnessRisk = dead ? 1f : Config.Clamp(
                Math.Max(_manager.Consciousness.BlackoutIntensity, _manager.Coma.IsActive ? 1f : 0f) +
                _manager.Shock.UnconsciousnessPressure * 0.65f +
                oxygenStress * 0.36f +
                brain * 0.28f +
                Math.Max(0f, bloodLoss - 0.42f) * 0.48f +
                Math.Max(0f, 1f - zcity.Consciousness) * 0.45f,
                0f,
                1f);

            float vitalDanger = dead ? 1f : Config.Clamp(
                bloodLoss * 0.32f +
                shockNormalized * 0.24f +
                pain * 0.12f +
                oxygenStress * 0.18f +
                brain * 0.10f +
                internalBleeding * 0.14f +
                trauma * 0.12f +
                (zcity.Critical ? 0.16f : 0f) +
                zcity.HemotransfusionShock * 0.06f,
                0f,
                1f);

            bool critical = dead ||
                            _manager.Shock.Severity == ShockSeverity.Critical ||
                            zcity.Critical ||
                            zcity.HeartStop ||
                            _manager.Bleeding.BloodVolumeMl <= Config.CriticalBloodMl ||
                            _manager.Organs.CardiacArrestActive ||
                            _manager.Lungs.OxygenNormalized < 0.28f;

            bool needsAid = !dead && (
                critical ||
                unconscious ||
                zcity.Incapacitated ||
                _manager.Bleeding.HasActiveBleeding ||
                _manager.Bleeding.TotalBleedRateMlPerSecond > 8f ||
                internalBleeding > 0.18f ||
                _manager.Shock.Severity >= ShockSeverity.Moderate);

            Snapshot = new MedicalTelemetrySnapshot(
                _manager.Bleeding.BloodVolumeMl,
                bloodNormalized,
                bloodLoss,
                _manager.Bleeding.TotalBleedRateMlPerSecond,
                pulse,
                heartbeat,
                dead ? 0f : _manager.TacticalVitals.SystolicPressure,
                dead ? 0f : _manager.TacticalVitals.DiastolicPressure,
                dead ? 0f : _manager.TacticalVitals.RespirationRate,
                oxygenNormalized,
                oxygenStress,
                pain,
                shockNormalized,
                _manager.Shock.Severity,
                stress,
                internalBleeding,
                _manager.TacticalVitals.InfectionRisk,
                brain,
                trauma,
                consciousnessRisk,
                vitalDanger,
                _manager.Consciousness.State,
                dead,
                unconscious,
                critical,
                needsAid);
        }
    }
}
