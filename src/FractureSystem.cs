using System;

namespace BonelabAdvancedHealth
{
    public sealed class FractureSystem
    {
        private const float BrokenLegGroundedSeconds = 30f;
        private static readonly float[] FractureUsageFactors =
        {
            1f,
            0.14f,
            0.45f,
            0.72f
        };

        private static readonly float[] TorsoBreathingByFracture =
        {
            0f,
            0.2f,
            0.55f,
            1.0f
        };

        private readonly HealthManager _manager;
        private float _applyAccumulator;
        private float _leftLegGroundedSeconds;
        private float _rightLegGroundedSeconds;
        private bool _leftLegWasBroken;
        private bool _rightLegWasBroken;
        private bool _legRagdollActive;

        public float LeftLegUsage { get; private set; } = 1f;
        public float RightLegUsage { get; private set; } = 1f;
        public float LeftArmUsage { get; private set; } = 1f;
        public float RightArmUsage { get; private set; } = 1f;
        public float SpineUsage { get; private set; } = 1f;
        public float HipsUsage { get; private set; } = 1f;
        public float BreathingPenalty { get; private set; }
        public float AimInstability { get; private set; }
        public bool IsLegRecoveryActive => _leftLegGroundedSeconds > 0f || _rightLegGroundedSeconds > 0f;
        public BrokenArmController BrokenArms { get; }

        public event Action<FractureSystem>? UsageChanged;

        public FractureSystem(HealthManager manager)
        {
            _manager = manager;
            BrokenArms = new BrokenArmController(manager);
        }

        public void Reset()
        {
            _applyAccumulator = 0f;
            LeftLegUsage = 1f;
            RightLegUsage = 1f;
            LeftArmUsage = 1f;
            RightArmUsage = 1f;
            SpineUsage = 1f;
            HipsUsage = 1f;
            BreathingPenalty = 0f;
            AimInstability = 0f;
            _leftLegGroundedSeconds = 0f;
            _rightLegGroundedSeconds = 0f;
            _leftLegWasBroken = false;
            _rightLegWasBroken = false;
            _legRagdollActive = false;
            BrokenArms.Reset();
            UsageChanged?.Invoke(this);
        }

        public void Update(float deltaTime)
        {
            UpdateBrokenLegRecovery(deltaTime);
            BrokenArms.Update(deltaTime);
            CalculateUsage();
            _applyAccumulator += deltaTime;
            if (_applyAccumulator < 0.25f)
                return;

            _applyAccumulator = 0f;
            if (_manager.Kind == HealthOwnerKind.Player)
                MainMod.Runtime?.ApplyPlayerLimbUsage(this);

            if (BreathingPenalty > 0f)
                _manager.AddPain(BreathingPenalty * 0.18f);
        }

        public float GetMovementPenalty()
        {
            float legAverage = (LeftLegUsage + RightLegUsage) * 0.5f;
            float torso = Math.Min(SpineUsage, HipsUsage);
            return Config.Clamp(1f - Math.Min(legAverage, torso), 0f, 0.85f);
        }

        public float GetGripStrength(BodyPart arm)
        {
            if (arm == BodyPart.LeftArm)
                return BrokenArms.GetGripStrength(arm, LeftArmUsage);
            if (arm == BodyPart.RightArm)
                return BrokenArms.GetGripStrength(arm, RightArmUsage);
            return 1f;
        }

        private void CalculateUsage()
        {
            float oldLeftLeg = LeftLegUsage;
            float oldRightLeg = RightLegUsage;
            float oldLeftArm = LeftArmUsage;
            float oldRightArm = RightArmUsage;
            float oldSpine = SpineUsage;
            float oldHips = HipsUsage;

            LimbHealth head = _manager.GetLimb(BodyPart.Head);
            LimbHealth torso = _manager.GetLimb(BodyPart.Torso);
            LimbHealth leftArm = _manager.GetLimb(BodyPart.LeftArm);
            LimbHealth rightArm = _manager.GetLimb(BodyPart.RightArm);
            LimbHealth leftLeg = _manager.GetLimb(BodyPart.LeftLeg);
            LimbHealth rightLeg = _manager.GetLimb(BodyPart.RightLeg);

            LeftLegUsage = UsageFromLimb(leftLeg);
            RightLegUsage = UsageFromLimb(rightLeg);
            LeftArmUsage = UsageFromLimb(leftArm);
            RightArmUsage = UsageFromLimb(rightArm);
            SpineUsage = UsageFromLimb(torso);
            bool leftLegBroken = leftLeg.IsBroken;
            bool rightLegBroken = rightLeg.IsBroken;
            if (leftLegBroken ^ rightLegBroken)
            {
                LeftLegUsage = leftLegBroken ? Math.Min(LeftLegUsage, 0.42f) : Math.Min(LeftLegUsage, 0.82f);
                RightLegUsage = rightLegBroken ? Math.Min(RightLegUsage, 0.42f) : Math.Min(RightLegUsage, 0.82f);
            }
            else if (leftLegBroken && rightLegBroken)
            {
                LeftLegUsage = Math.Min(LeftLegUsage, 0.08f);
                RightLegUsage = Math.Min(RightLegUsage, 0.08f);
                SpineUsage = Math.Min(SpineUsage, 0.35f);
            }
            if (_manager.AdrenalineNormalized > 0f)
            {
                float boost = _manager.AdrenalineNormalized * 0.28f;
                LeftLegUsage = Config.Clamp(LeftLegUsage + boost, 0.12f, 1f);
                RightLegUsage = Config.Clamp(RightLegUsage + boost, 0.12f, 1f);
                LeftArmUsage = Config.Clamp(LeftArmUsage + boost, 0.12f, 1f);
                RightArmUsage = Config.Clamp(RightArmUsage + boost, 0.12f, 1f);
                SpineUsage = Config.Clamp(SpineUsage + boost * 0.5f, 0.12f, 1f);
            }

            if (_manager.Brain.HasActiveConcussion)
            {
                float concussionPenalty = _manager.Brain.DisorientationNormalized * 0.18f + _manager.Brain.BalanceLoss * 0.12f;
                LeftLegUsage = Config.Clamp(LeftLegUsage - concussionPenalty, 0.12f, 1f);
                RightLegUsage = Config.Clamp(RightLegUsage - concussionPenalty, 0.12f, 1f);
            }

            if (Config.PainEffectsEnabled)
            {
                float painMove = _manager.PainSystem.MovementPenalty;
                LeftLegUsage = Config.Clamp(LeftLegUsage - painMove * 0.34f, 0.12f, 1f);
                RightLegUsage = Config.Clamp(RightLegUsage - painMove * 0.34f, 0.12f, 1f);
                LeftArmUsage = Config.Clamp(LeftArmUsage - painMove * 0.16f, 0.12f, 1f);
                RightArmUsage = Config.Clamp(RightArmUsage - painMove * 0.16f, 0.12f, 1f);
                SpineUsage = Config.Clamp(SpineUsage - painMove * 0.10f, 0.12f, 1f);
            }

            if (_manager.Shock.IsActive)
            {
                float shockMove = _manager.Shock.MovementPenalty;
                LeftLegUsage = Config.Clamp(LeftLegUsage - shockMove * 0.62f, 0.10f, 1f);
                RightLegUsage = Config.Clamp(RightLegUsage - shockMove * 0.62f, 0.10f, 1f);
                LeftArmUsage = Config.Clamp(LeftArmUsage - shockMove * 0.22f, 0.08f, 1f);
                RightArmUsage = Config.Clamp(RightArmUsage - shockMove * 0.22f, 0.08f, 1f);
                SpineUsage = Config.Clamp(SpineUsage - shockMove * 0.18f, 0.10f, 1f);
            }

            LeftArmUsage = Config.Clamp(LeftArmUsage * BrokenArms.GetUsageMultiplier(BodyPart.LeftArm), 0.02f, 1f);
            RightArmUsage = Config.Clamp(RightArmUsage * BrokenArms.GetUsageMultiplier(BodyPart.RightArm), 0.02f, 1f);
            HipsUsage = Math.Min(SpineUsage, Math.Min(LeftLegUsage, RightLegUsage) + (leftLegBroken && rightLegBroken ? 0.02f : 0.12f));
            if (_leftLegGroundedSeconds > 0f || _rightLegGroundedSeconds > 0f)
            {
                if (_leftLegGroundedSeconds > 0f)
                    LeftLegUsage = Math.Min(LeftLegUsage, 0.04f);
                if (_rightLegGroundedSeconds > 0f)
                    RightLegUsage = Math.Min(RightLegUsage, 0.04f);
                HipsUsage = Math.Min(HipsUsage, 0.08f);
            }

            BreathingPenalty = GetTorsoBreathingPenalty(torso);
            BreathingPenalty = Config.Clamp(BreathingPenalty + _manager.Bones.RibBreathingPenalty + _manager.Lungs.BreathingPanic * 0.65f + _manager.PainSystem.BreathingStress * 0.28f, 0f, 1.5f);

            AimInstability = (1f - Math.Min(LeftArmUsage, RightArmUsage)) + head.DamagePercent * 0.25f + _manager.PainNormalized * 0.2f;
            AimInstability += _manager.Brain.DisorientationNormalized * 0.55f + _manager.Lungs.OxygenStress * 0.25f + _manager.PainSystem.AimInstability * 0.42f;
            AimInstability += BrokenArms.AimInstabilityBonus;
            AimInstability = Config.Clamp(AimInstability, 0f, 1.35f);

            if (Math.Abs(oldLeftLeg - LeftLegUsage) > 0.001f ||
                Math.Abs(oldRightLeg - RightLegUsage) > 0.001f ||
                Math.Abs(oldLeftArm - LeftArmUsage) > 0.001f ||
                Math.Abs(oldRightArm - RightArmUsage) > 0.001f ||
                Math.Abs(oldSpine - SpineUsage) > 0.001f ||
                Math.Abs(oldHips - HipsUsage) > 0.001f)
            {
                UsageChanged?.Invoke(this);
            }
        }

        private static float UsageFromLimb(LimbHealth limb)
        {
            float usage = 1f - limb.DamagePercent * 0.34f;
            int fractureIndex = (int)limb.Fracture;
            if (fractureIndex > 0 && fractureIndex < FractureUsageFactors.Length)
            {
                float severity = FractureUsageFactors[fractureIndex];
                float minUsage = fractureIndex == (int)FractureState.Sprain ? 0.55f : fractureIndex == (int)FractureState.Fractured ? 0.22f : 0.10f;
                float maxUsage = fractureIndex == (int)FractureState.Sprain ? 0.94f : fractureIndex == (int)FractureState.Fractured ? 0.75f : 0.48f;
                usage *= Config.Clamp(1f - severity * Config.FractureSeverity, minUsage, maxUsage);
            }

            return Config.Clamp(usage, 0.12f, 1f);
        }

        private static float GetTorsoBreathingPenalty(LimbHealth torso)
        {
            int fractureIndex = (int)torso.Fracture;
            if (fractureIndex > 0 && fractureIndex < TorsoBreathingByFracture.Length)
                return TorsoBreathingByFracture[fractureIndex];

            return torso.DamagePercent * 0.18f;
        }

        private void UpdateBrokenLegRecovery(float deltaTime)
        {
            if (_manager.Kind != HealthOwnerKind.Player)
                return;

            LimbHealth leftLeg = _manager.GetLimb(BodyPart.LeftLeg);
            LimbHealth rightLeg = _manager.GetLimb(BodyPart.RightLeg);
            bool leftBroken = leftLeg.IsBroken;
            bool rightBroken = rightLeg.IsBroken;

            if (_manager.IsDead || !TraumaStartupGuard.CanRunPlayerTrauma)
            {
                ClearLegGroundedRecovery(false);
                _leftLegWasBroken = leftBroken;
                _rightLegWasBroken = rightBroken;
                return;
            }

            if (leftBroken && !_leftLegWasBroken)
                StartBrokenLegRecovery(BodyPart.LeftLeg);
            if (rightBroken && !_rightLegWasBroken)
                StartBrokenLegRecovery(BodyPart.RightLeg);

            if (!leftBroken)
                _leftLegGroundedSeconds = 0f;
            if (!rightBroken)
                _rightLegGroundedSeconds = 0f;

            if (_leftLegGroundedSeconds > 0f)
                _leftLegGroundedSeconds = Math.Max(0f, _leftLegGroundedSeconds - deltaTime);
            if (_rightLegGroundedSeconds > 0f)
                _rightLegGroundedSeconds = Math.Max(0f, _rightLegGroundedSeconds - deltaTime);

            bool active = _leftLegGroundedSeconds > 0f || _rightLegGroundedSeconds > 0f;
            if (active && !_legRagdollActive && _manager.Consciousness.State == ConsciousnessState.Awake && !_manager.Coma.IsActive)
            {
                _legRagdollActive = true;
                MainMod.Runtime?.SetPlayerRagdoll(true);
            }
            else if (!active && _legRagdollActive)
            {
                _legRagdollActive = false;
                if (_manager.Consciousness.State == ConsciousnessState.Awake && !_manager.Coma.IsActive)
                    MainMod.Runtime?.SetPlayerRagdoll(false);
            }

            _leftLegWasBroken = leftBroken;
            _rightLegWasBroken = rightBroken;
        }

        private void StartBrokenLegRecovery(BodyPart part)
        {
            if (part == BodyPart.LeftLeg)
                _leftLegGroundedSeconds = Math.Max(_leftLegGroundedSeconds, BrokenLegGroundedSeconds);
            else if (part == BodyPart.RightLeg)
                _rightLegGroundedSeconds = Math.Max(_rightLegGroundedSeconds, BrokenLegGroundedSeconds);

            MainMod.Runtime?.NotifyHudMedicalFeedback(MedicalInspectionSystem.GetPartLabel(part) + " fractured - wait or use splint");
        }

        private void ClearLegGroundedRecovery(bool unragdoll)
        {
            _leftLegGroundedSeconds = 0f;
            _rightLegGroundedSeconds = 0f;
            if (_legRagdollActive)
            {
                _legRagdollActive = false;
                if (unragdoll)
                    MainMod.Runtime?.SetPlayerRagdoll(false);
            }
        }

    }
}
