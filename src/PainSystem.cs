using System;

namespace BonelabAdvancedHealth
{
    public sealed class PainSystem
    {
        private readonly HealthManager _manager;
        private float _acutePain;
        private float _traumaPain;
        private float _panic;
        private float _spasmTimer;

        public float TotalPain { get; private set; }
        public float PainNormalized => Config.Clamp(TotalPain / 100f, 0f, 1f);
        public float MovementPenalty { get; private set; }
        public float AimInstability { get; private set; }
        public float BreathingStress { get; private set; }
        public float BlackoutPressure { get; private set; }
        public float ShakeIntensity { get; private set; }
        public float AudioIntensity { get; private set; }
        public bool InPainShock => BlackoutPressure > 0.72f || TotalPain >= 92f;

        public PainSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            _acutePain = 0f;
            _traumaPain = 0f;
            _panic = 0f;
            _spasmTimer = 0f;
            TotalPain = 0f;
            MovementPenalty = 0f;
            AimInstability = 0f;
            BreathingStress = 0f;
            BlackoutPressure = 0f;
            ShakeIntensity = 0f;
            AudioIntensity = 0f;
        }

        public void AddPain(float amount, BodyPart part, AdvancedDamageType damageType)
        {
            if (!Config.PainEffectsEnabled || amount <= 0f || _manager.IsDead)
                return;

            float partMultiplier = GetBodyPartMultiplier(part);
            float typeMultiplier = GetDamageTypeMultiplier(damageType);
            float scaled = amount * 0.62f * partMultiplier * typeMultiplier;
            _acutePain = Config.Clamp(_acutePain + scaled, 0f, 145f);
            _traumaPain = Config.Clamp(_traumaPain + scaled * 0.38f, 0f, 110f);
            _panic = Config.Clamp(_panic + scaled * 0.0065f, 0f, 1f);
            if (scaled >= 8f)
                _spasmTimer = Math.Max(_spasmTimer, Config.Clamp(0.3f + scaled * 0.025f, 0.35f, 3.0f));
            Recalculate();
        }

        public void ReducePain(float amount)
        {
            if (amount <= 0f)
                return;

            _acutePain = Math.Max(0f, _acutePain - amount * 0.92f);
            _traumaPain = Math.Max(0f, _traumaPain - amount * 0.38f);
            _panic = Math.Max(0f, _panic - amount * 0.006f);
            Recalculate();
        }

        public void Update(float deltaTime, bool morphineActive, bool adrenalineActive, bool adrenalineCrash, float organPainModifier)
        {
            if (!Config.PainEffectsEnabled)
            {
                Reset();
                return;
            }

            float acuteDecay = morphineActive ? 5.2f : adrenalineActive ? 3.4f : 1.05f;
            float traumaDecay = morphineActive ? 1.2f : 0.22f;
            _acutePain = Math.Max(0f, _acutePain - deltaTime * acuteDecay);
            _traumaPain = Math.Max(0f, _traumaPain - deltaTime * traumaDecay);
            _panic = Math.Max(0f, _panic - deltaTime * (morphineActive ? 0.035f : 0.012f));
            _spasmTimer = Math.Max(0f, _spasmTimer - deltaTime);

            if (adrenalineCrash)
            {
                _acutePain = Config.Clamp(_acutePain + deltaTime * 4.0f, 0f, 145f);
                _panic = Config.Clamp(_panic + deltaTime * 0.025f, 0f, 1f);
            }

            if (organPainModifier > 1f)
                _traumaPain = Config.Clamp(_traumaPain + deltaTime * (organPainModifier - 1f) * 1.15f, 0f, 110f);

            Recalculate();
        }

        private void Recalculate()
        {
            float basePain = _acutePain + _traumaPain * 0.58f;
            TotalPain = Config.Clamp(basePain, 0f, 135f);
            float n = PainNormalized;
            float spasm = _spasmTimer > 0f ? Config.Clamp(_spasmTimer / 2.5f, 0f, 1f) : 0f;
            MovementPenalty = Config.Clamp(n * 0.32f + _panic * 0.10f, 0f, 0.55f);
            AimInstability = Config.Clamp(n * 0.55f + spasm * 0.35f + _panic * 0.18f, 0f, 1.15f);
            BreathingStress = Config.Clamp(n * 0.42f + _panic * 0.35f, 0f, 1.05f);
            BlackoutPressure = Config.Clamp(Math.Max(0f, n - 0.48f) * 0.78f + _panic * 0.26f, 0f, 1f);
            ShakeIntensity = Config.Clamp(n * 0.48f + spasm * 0.42f + _panic * 0.22f, 0f, 1.2f);
            AudioIntensity = Config.Clamp(n * 0.85f + _panic * 0.35f, 0f, 1.25f);
        }

        private static float GetBodyPartMultiplier(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head:
                    return 1.25f;
                case BodyPart.Torso:
                    return 1.12f;
                case BodyPart.LeftLeg:
                case BodyPart.RightLeg:
                    return 1.05f;
                default:
                    return 1.0f;
            }
        }

        private static float GetDamageTypeMultiplier(AdvancedDamageType damageType)
        {
            switch (damageType)
            {
                case AdvancedDamageType.Bullet:
                    return 1.18f;
                case AdvancedDamageType.Stab:
                    return 1.26f;
                case AdvancedDamageType.Explosion:
                    return 1.36f;
                case AdvancedDamageType.Fall:
                    return 0.82f;
                default:
                    return 1.0f;
            }
        }
    }
}
