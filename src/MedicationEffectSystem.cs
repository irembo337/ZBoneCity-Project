using System;

namespace BonelabAdvancedHealth
{
    public sealed class MedicationEffectSystem
    {
        private readonly HealthManager _manager;
        private float _morphineSeconds;
        private float _adrenalineSeconds;
        private float _painkillerSeconds;
        private float _etgSeconds;
        private float _sj1Seconds;
        private float _stimulantCrashSeconds;
        private float _morphineLoad;
        private float _adrenalineLoad;
        private float _painkillerLoad;
        private float _etgLoad;
        private float _sj1Load;
        private float _overdoseAccumulator;

        public bool MorphineActive => _morphineSeconds > 0f;
        public bool StimulantActive => _adrenalineSeconds > 0f || _etgSeconds > 0f || _sj1Seconds > 0f;
        public bool CrashActive => _stimulantCrashSeconds > 0f;
        public float StimulantNormalized => Config.Clamp((_adrenalineSeconds / 45f) + (_etgSeconds / 70f) * 0.85f + (_sj1Seconds / 85f), 0f, 1f);
        public float PainIntakeMultiplier => Config.Clamp(1f - PainSuppression * 0.55f, 0.28f, 1f);
        public float PainSuppression { get; private set; }
        public float RespiratoryDepression { get; private set; }
        public float ReactionPenalty { get; private set; }
        public float HandShake { get; private set; }
        public float StaminaBoost { get; private set; }
        public float FatiguePenalty { get; private set; }
        public float TachycardiaBpm { get; private set; }
        public float BloodPressureModifier { get; private set; }
        public float BreathingAudioStress { get; private set; }
        public float OverdoseRisk { get; private set; }

        public MedicationEffectSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            _morphineSeconds = 0f;
            _adrenalineSeconds = 0f;
            _painkillerSeconds = 0f;
            _etgSeconds = 0f;
            _sj1Seconds = 0f;
            _stimulantCrashSeconds = 0f;
            _morphineLoad = 0f;
            _adrenalineLoad = 0f;
            _painkillerLoad = 0f;
            _etgLoad = 0f;
            _sj1Load = 0f;
            _overdoseAccumulator = 0f;
            PainSuppression = 0f;
            RespiratoryDepression = 0f;
            ReactionPenalty = 0f;
            HandShake = 0f;
            StaminaBoost = 0f;
            FatiguePenalty = 0f;
            TachycardiaBpm = 0f;
            BloodPressureModifier = 0f;
            BreathingAudioStress = 0f;
            OverdoseRisk = 0f;
        }

        public void RegisterDose(MedicalItemType type)
        {
            if (!Config.MedicationOverdoseEnabled || _manager.IsDead)
                return;

            switch (type)
            {
                case MedicalItemType.Morphine:
                    _morphineSeconds = Math.Max(_morphineSeconds, 95f);
                    _morphineLoad = Config.Clamp(_morphineLoad + 1f, 0f, 4.5f);
                    break;
                case MedicalItemType.Adrenaline:
                    _adrenalineSeconds = Math.Max(_adrenalineSeconds, 50f);
                    _adrenalineLoad = Config.Clamp(_adrenalineLoad + 1f, 0f, 4.5f);
                    _stimulantCrashSeconds = 0f;
                    break;
                case MedicalItemType.Painkillers:
                    _painkillerSeconds = Math.Max(_painkillerSeconds, 85f);
                    _painkillerLoad = Config.Clamp(_painkillerLoad + 0.7f, 0f, 3f);
                    break;
                case MedicalItemType.ETGStimulator:
                    _etgSeconds = Math.Max(_etgSeconds, 70f);
                    _etgLoad = Config.Clamp(_etgLoad + 1f, 0f, 4f);
                    _stimulantCrashSeconds = 0f;
                    break;
                case MedicalItemType.SJ1Stimulator:
                    _sj1Seconds = Math.Max(_sj1Seconds, 90f);
                    _sj1Load = Config.Clamp(_sj1Load + 1f, 0f, 4f);
                    _stimulantCrashSeconds = 0f;
                    break;
            }

            MainMod.Runtime?.NotifyFusionMedication(_manager, type, OverdoseRisk);
        }

        public void Update(float deltaTime)
        {
            if (!Config.MedicationOverdoseEnabled || _manager.IsDead)
            {
                ResetTransientOutputs();
                return;
            }

            DecayTimers(deltaTime);
            DecayLoads(deltaTime);
            RecalculateOutputs();
            EvaluateOverdose(deltaTime);
        }

        private void DecayTimers(float deltaTime)
        {
            bool hadStimulant = StimulantActive;
            _morphineSeconds = Math.Max(0f, _morphineSeconds - deltaTime);
            _adrenalineSeconds = Math.Max(0f, _adrenalineSeconds - deltaTime);
            _painkillerSeconds = Math.Max(0f, _painkillerSeconds - deltaTime);
            _etgSeconds = Math.Max(0f, _etgSeconds - deltaTime);
            _sj1Seconds = Math.Max(0f, _sj1Seconds - deltaTime);
            if (hadStimulant && !StimulantActive)
                _stimulantCrashSeconds = Math.Max(_stimulantCrashSeconds, 22f);
            else
                _stimulantCrashSeconds = Math.Max(0f, _stimulantCrashSeconds - deltaTime);
        }

        private void DecayLoads(float deltaTime)
        {
            _morphineLoad = Math.Max(0f, _morphineLoad - deltaTime / 150f);
            _adrenalineLoad = Math.Max(0f, _adrenalineLoad - deltaTime / 125f);
            _painkillerLoad = Math.Max(0f, _painkillerLoad - deltaTime / 135f);
            _etgLoad = Math.Max(0f, _etgLoad - deltaTime / 120f);
            _sj1Load = Math.Max(0f, _sj1Load - deltaTime / 135f);
        }

        private void RecalculateOutputs()
        {
            float opioid = MorphineActive ? Config.Clamp(_morphineLoad / 2.8f, 0f, 1.35f) : 0f;
            float adrenaline = _adrenalineSeconds > 0f ? Config.Clamp(_adrenalineLoad / 2.2f, 0f, 1.45f) : 0f;
            float etg = _etgSeconds > 0f ? Config.Clamp(_etgLoad / 2.4f, 0f, 1.25f) : 0f;
            float sj1 = _sj1Seconds > 0f ? Config.Clamp(_sj1Load / 2.6f, 0f, 1.3f) : 0f;
            float crash = CrashActive ? Config.Clamp(_stimulantCrashSeconds / 22f, 0f, 1f) : 0f;

            PainSuppression = Config.Clamp(opioid * 0.60f + (_painkillerSeconds > 0f ? 0.28f : 0f) + etg * 0.42f + sj1 * 0.50f + adrenaline * 0.16f, 0f, 0.92f);
            RespiratoryDepression = Config.Clamp(Math.Max(0f, _morphineLoad - 1.55f) * 0.34f + _painkillerLoad * 0.035f, 0f, 1f);
            ReactionPenalty = Config.Clamp(RespiratoryDepression * 0.48f + crash * 0.20f + Math.Max(0f, _painkillerLoad - 1.8f) * 0.08f, 0f, 0.82f);
            HandShake = Config.Clamp(adrenaline * 0.16f + etg * 0.12f + sj1 * 0.10f + crash * 0.46f + Math.Max(0f, _adrenalineLoad - 2.2f) * 0.24f, 0f, 1.15f);
            StaminaBoost = Config.Clamp(adrenaline * 0.14f + etg * 0.22f + sj1 * 0.28f, 0f, 0.42f);
            FatiguePenalty = Config.Clamp(crash * 0.35f + RespiratoryDepression * 0.24f, 0f, 0.75f);
            TachycardiaBpm = Config.Clamp(adrenaline * 32f + etg * 22f + sj1 * 28f + OverdoseRisk * 20f - RespiratoryDepression * 10f, -14f, 68f);
            BloodPressureModifier = Config.Clamp(adrenaline * 14f + sj1 * 10f - RespiratoryDepression * 18f - crash * 8f, -34f, 28f);
            BreathingAudioStress = Config.Clamp(adrenaline * 0.18f + etg * 0.12f + sj1 * 0.14f + RespiratoryDepression * 0.72f + crash * 0.22f, 0f, 1.15f);
            OverdoseRisk = Config.Clamp(Math.Max(0f, _morphineLoad - 2.1f) * 0.38f + Math.Max(0f, _adrenalineLoad - 2.35f) * 0.28f + Math.Max(0f, _etgLoad + _sj1Load - 2.8f) * 0.22f, 0f, 1f);
        }

        private void EvaluateOverdose(float deltaTime)
        {
            if (OverdoseRisk <= 0.45f || _manager.Consciousness.State != ConsciousnessState.Awake)
            {
                _overdoseAccumulator = Math.Max(0f, _overdoseAccumulator - deltaTime * 0.15f);
                return;
            }

            float bloodStress = 1f - _manager.Bleeding.BloodNormalized;
            float oxygenStress = _manager.Lungs.OxygenStress + RespiratoryDepression * 0.65f;
            _overdoseAccumulator += deltaTime * (OverdoseRisk * 0.018f + bloodStress * 0.010f + oxygenStress * 0.012f);
            if (_overdoseAccumulator < 1f)
                return;

            _overdoseAccumulator = 0f;
            if (_manager.Random.NextDouble() > OverdoseRisk * 0.32f)
                return;

            _manager.Consciousness.SetUnconscious(RespiratoryDepression > 0.58f ? 9f : 4f);
            _manager.AudioTrauma.TriggerPainVoice(0.35f + OverdoseRisk * 0.35f);
            MainMod.Runtime?.NotifyHudMedicalFeedback(RespiratoryDepression > 0.58f ? "Medication Overdose - Slow Breathing" : "Stimulant Crash");
        }

        private void ResetTransientOutputs()
        {
            PainSuppression = 0f;
            RespiratoryDepression = 0f;
            ReactionPenalty = 0f;
            HandShake = 0f;
            StaminaBoost = 0f;
            FatiguePenalty = 0f;
            TachycardiaBpm = 0f;
            BloodPressureModifier = 0f;
            BreathingAudioStress = 0f;
            OverdoseRisk = 0f;
        }
    }
}
