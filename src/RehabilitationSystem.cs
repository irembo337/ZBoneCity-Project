using System;

namespace BonelabAdvancedHealth
{
    public sealed class RehabilitationSystem
    {
        private readonly HealthManager _manager;
        private readonly float[] _limbRecovery = new float[Config.LimbCount];
        private float _bloodWeakness;
        private float _systemicWeakness;
        private float _residualPain;

        public float MovementPenalty { get; private set; }
        public float StaminaPenalty { get; private set; }
        public float AimPenalty { get; private set; }
        public float ResidualPain => _residualPain;
        public bool IsRecovering => MovementPenalty > 0.02f || StaminaPenalty > 0.02f || _residualPain > 0.8f;

        public RehabilitationSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            for (int i = 0; i < _limbRecovery.Length; i++)
                _limbRecovery[i] = 0f;
            _bloodWeakness = 0f;
            _systemicWeakness = 0f;
            _residualPain = 0f;
            MovementPenalty = 0f;
            StaminaPenalty = 0f;
            AimPenalty = 0f;
        }

        public void RegisterFractureTreatment(BodyPart part, FractureState previousState)
        {
            if (!Config.RehabilitationEnabled || previousState == FractureState.None)
                return;

            float baseRecovery = previousState == FractureState.Shattered ? 1f : previousState == FractureState.Fractured ? 0.72f : 0.38f;
            if (IsLeg(part))
                baseRecovery *= 1.1f;
            _limbRecovery[(int)part] = Math.Max(_limbRecovery[(int)part], baseRecovery);
            _residualPain = Math.Max(_residualPain, previousState == FractureState.Shattered ? 18f : 11f);
            _systemicWeakness = Math.Max(_systemicWeakness, 0.20f);
            MainMod.Runtime?.NotifyFusionRehabilitation(_manager, part, baseRecovery);
        }

        public void RegisterBloodRestoration(float bloodBeforeMl, float amountMl)
        {
            if (!Config.RehabilitationEnabled || amountMl <= 0f)
                return;

            float missingBefore = Config.Clamp(1f - bloodBeforeMl / Config.BloodVolumeMl, 0f, 1f);
            if (missingBefore < 0.16f)
                return;

            _bloodWeakness = Math.Max(_bloodWeakness, Config.Clamp(missingBefore * 0.85f, 0.12f, 0.85f));
            _residualPain = Math.Max(_residualPain, missingBefore * 10f);
            MainMod.Runtime?.NotifyFusionRehabilitation(_manager, BodyPart.Torso, _bloodWeakness);
        }

        public void RegisterTreatment(MedicalItemType type)
        {
            if (!Config.RehabilitationEnabled)
                return;

            if (type == MedicalItemType.Medkit)
                _systemicWeakness = Math.Max(_systemicWeakness, 0.12f);
            else if (type == MedicalItemType.BloodPack)
                _bloodWeakness = Math.Max(_bloodWeakness, 0.16f);
        }

        public void Update(float deltaTime)
        {
            if (!Config.RehabilitationEnabled || _manager.IsDead)
            {
                MovementPenalty = 0f;
                StaminaPenalty = 0f;
                AimPenalty = 0f;
                return;
            }

            for (int i = 0; i < _limbRecovery.Length; i++)
            {
                float rate = IsLeg((BodyPart)i) ? 0.0024f : 0.0034f;
                _limbRecovery[i] = Math.Max(0f, _limbRecovery[i] - deltaTime * rate);
            }

            _bloodWeakness = Math.Max(0f, _bloodWeakness - deltaTime * 0.0019f);
            _systemicWeakness = Math.Max(0f, _systemicWeakness - deltaTime * 0.0028f);
            _residualPain = Math.Max(0f, _residualPain - deltaTime * 0.045f);

            float legRecovery = Math.Max(_limbRecovery[(int)BodyPart.LeftLeg], _limbRecovery[(int)BodyPart.RightLeg]);
            float armRecovery = Math.Max(_limbRecovery[(int)BodyPart.LeftArm], _limbRecovery[(int)BodyPart.RightArm]);
            MovementPenalty = Config.Clamp(legRecovery * 0.34f + _bloodWeakness * 0.22f + _systemicWeakness * 0.12f, 0f, 0.58f);
            StaminaPenalty = Config.Clamp(_bloodWeakness * 0.42f + legRecovery * 0.22f + _systemicWeakness * 0.20f, 0f, 0.68f);
            AimPenalty = Config.Clamp(armRecovery * 0.22f + _bloodWeakness * 0.08f, 0f, 0.45f);
        }

        public float GetLimbUsageMultiplier(BodyPart part)
        {
            if (!Config.RehabilitationEnabled)
                return 1f;

            float recovery = _limbRecovery[(int)part];
            if (recovery <= 0f)
                return 1f;

            float penalty = IsLeg(part) ? recovery * 0.34f : recovery * 0.22f;
            return Config.Clamp(1f - penalty, 0.42f, 1f);
        }

        private static bool IsLeg(BodyPart part)
        {
            return part == BodyPart.LeftLeg || part == BodyPart.RightLeg;
        }
    }
}
