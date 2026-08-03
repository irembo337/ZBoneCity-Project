using System;

namespace BonelabAdvancedHealth
{
    public sealed class ComaSystem
    {
        private const float DefaultComaSeconds = 30f;
        private static readonly float[] TreatmentScoreByItem =
        {
            0f,
            0f,
            0.25f,
            1.15f,
            0.75f,
            0.35f,
            1.15f,
            0f
        };

        private readonly HealthManager _manager;
        private float _controlApplyAccumulator;
        private float _treatmentScore;

        public bool IsActive { get; private set; }
        public float RemainingSeconds { get; private set; }
        public DeathCause Cause { get; private set; }
        public float NormalizedTimeRemaining => Config.Clamp(RemainingSeconds / DefaultComaSeconds, 0f, 1f);
        public bool IsStabilized => _treatmentScore >= 1f && HasSurvivableVitals();

        public ComaSystem(HealthManager manager)
        {
            _manager = manager;
            Cause = DeathCause.Trauma;
        }

        public void Reset()
        {
            IsActive = false;
            RemainingSeconds = 0f;
            _controlApplyAccumulator = 0f;
            _treatmentScore = 0f;
            Cause = DeathCause.Trauma;
        }

        public bool TryEnter(DeathCause cause)
        {
            if (_manager.Kind != HealthOwnerKind.Player)
                return false;
            if (_manager.IsDead)
                return false;
            if (!TraumaStartupGuard.CanRunPlayerTrauma)
                return false;

            Cause = cause;
            if (!IsActive)
            {
                IsActive = true;
                RemainingSeconds = DefaultComaSeconds;
                _treatmentScore = 0f;
                _controlApplyAccumulator = 0f;
                _manager.Consciousness.SetUnconscious(DefaultComaSeconds + 3f);
                MainMod.Runtime?.SetPlayerRagdoll(true);
                MainMod.Runtime?.SetPlayerControlSuppressed(true);
            }
            else
            {
                _manager.Consciousness.SetUnconscious(Math.Max(3f, RemainingSeconds));
            }

            return true;
        }

        public void Update(float deltaTime)
        {
            if (!IsActive || _manager.Kind != HealthOwnerKind.Player)
                return;
            if (_manager.IsDead)
            {
                ClearForDeath();
                return;
            }
            if (!TraumaStartupGuard.CanRunPlayerTrauma)
            {
                Reset();
                _manager.Consciousness.ForceAwakeForSpawn();
                return;
            }

            RemainingSeconds = Math.Max(0f, RemainingSeconds - deltaTime);
            _controlApplyAccumulator += deltaTime;
            if (_controlApplyAccumulator >= 0.25f)
            {
                _controlApplyAccumulator = 0f;
                MainMod.Runtime?.SetPlayerRagdoll(true);
                MainMod.Runtime?.SetPlayerControlSuppressed(true);
            }

            if (RemainingSeconds > 0f)
                return;

            if (IsStabilized)
            {
                RecoverFromComa();
                return;
            }

            _manager.CommitDeath(Cause);
        }

        public void RegisterTreatment(MedicalItemType itemType)
        {
            if (!IsActive)
                return;

            _treatmentScore += GetTreatmentScore(itemType);
            if (itemType == MedicalItemType.Adrenaline)
                RemainingSeconds = Math.Max(RemainingSeconds, 10f);
        }

        public void RegisterBloodRestore(float amountMl)
        {
            if (!IsActive || amountMl <= 0f)
                return;

            _treatmentScore += Config.Clamp(amountMl / 650f, 0.05f, 1.25f);
        }

        public void RegisterManualResuscitation(float score, float minimumTimeSeconds)
        {
            if (!IsActive)
                return;

            _treatmentScore += Config.Clamp(score, 0.02f, 0.45f);
            RemainingSeconds = Math.Max(RemainingSeconds, minimumTimeSeconds);
        }

        public bool TryRecoverByStandUpInput()
        {
            if (!IsActive || _manager.Kind != HealthOwnerKind.Player || _manager.IsDead || !IsStabilized)
                return false;

            IsActive = false;
            RemainingSeconds = 0f;
            _controlApplyAccumulator = 0f;
            _manager.Consciousness.BeginStandUpRecovery(12f);
            MainMod.Runtime?.SetPlayerControlSuppressed(false);
            MainMod.Runtime?.SetPlayerRagdoll(false);
            return true;
        }

        public void ClearForDeath()
        {
            IsActive = false;
            RemainingSeconds = 0f;
            _controlApplyAccumulator = 0f;
            _treatmentScore = 0f;
        }

        private void RecoverFromComa()
        {
            IsActive = false;
            RemainingSeconds = 0f;
            _controlApplyAccumulator = 0f;
            _manager.Consciousness.BeginStandUpRecovery(10f);
            MainMod.Runtime?.SetPlayerControlSuppressed(false);
        }

        private bool HasSurvivableVitals()
        {
            if (_manager.Bleeding.BloodVolumeMl <= Config.CriticalBloodMl + 250f)
                return false;
            if (_manager.Lungs.OxygenNormalized < 0.48f)
                return false;
            if (_manager.Organs.CardiacArrestActive && _treatmentScore < 2.0f)
                return false;
            if (_manager.GetLimb(BodyPart.Head).Hp <= 0f && _treatmentScore < 2.0f)
                return false;
            if (_manager.GetLimb(BodyPart.Torso).Hp <= 0f && _treatmentScore < 1.5f)
                return false;
            if (_manager.Bleeding.TotalBleedRateMlPerSecond > 18f && _treatmentScore < 2.0f)
                return false;

            return true;
        }

        private float GetTreatmentScore(MedicalItemType itemType)
        {
            if (itemType == MedicalItemType.Bandage || itemType == MedicalItemType.Tourniquet)
                return _manager.Bleeding.TotalBleedRateMlPerSecond <= 10f ? 0.65f : 0.35f;

            int index = (int)itemType;
            return index >= 0 && index < TreatmentScoreByItem.Length ? TreatmentScoreByItem[index] : 0f;
        }
    }
}
