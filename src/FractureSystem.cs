using System;

namespace BonelabAdvancedHealth
{
    public sealed class FractureSystem
    {
        private readonly HealthManager _manager;
        private float _applyAccumulator;

        public float LeftLegUsage { get; private set; } = 1f;
        public float RightLegUsage { get; private set; } = 1f;
        public float LeftArmUsage { get; private set; } = 1f;
        public float RightArmUsage { get; private set; } = 1f;
        public float SpineUsage { get; private set; } = 1f;
        public float HipsUsage { get; private set; } = 1f;
        public float BreathingPenalty { get; private set; }
        public float AimInstability { get; private set; }

        public event Action<FractureSystem>? UsageChanged;

        public FractureSystem(HealthManager manager)
        {
            _manager = manager;
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
            UsageChanged?.Invoke(this);
        }

        public void Update(float deltaTime)
        {
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
                return Config.Clamp(LeftArmUsage, 0.15f, 1f);
            if (arm == BodyPart.RightArm)
                return Config.Clamp(RightArmUsage, 0.15f, 1f);
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
                float concussionPenalty = _manager.Brain.DisorientationNormalized * 0.18f;
                LeftLegUsage = Config.Clamp(LeftLegUsage - concussionPenalty, 0.12f, 1f);
                RightLegUsage = Config.Clamp(RightLegUsage - concussionPenalty, 0.12f, 1f);
            }

            HipsUsage = Math.Min(SpineUsage, Math.Min(LeftLegUsage, RightLegUsage) + 0.12f);

            if (torso.Fracture == FractureState.Shattered)
                BreathingPenalty = 1.0f;
            else if (torso.Fracture == FractureState.Fractured)
                BreathingPenalty = 0.55f;
            else if (torso.Fracture == FractureState.Sprain)
                BreathingPenalty = 0.2f;
            else
                BreathingPenalty = torso.DamagePercent * 0.18f;

            BreathingPenalty = Config.Clamp(BreathingPenalty + _manager.Bones.RibBreathingPenalty + _manager.Lungs.BreathingPanic * 0.65f, 0f, 1.5f);

            AimInstability = (1f - Math.Min(LeftArmUsage, RightArmUsage)) + head.DamagePercent * 0.25f + _manager.PainNormalized * 0.2f;
            AimInstability += _manager.Brain.DisorientationNormalized * 0.55f + _manager.Lungs.OxygenStress * 0.25f;
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
            switch (limb.Fracture)
            {
                case FractureState.Sprain:
                    usage *= 0.86f;
                    break;
                case FractureState.Fractured:
                    usage *= 0.55f;
                    break;
                case FractureState.Shattered:
                    usage *= 0.28f;
                    break;
            }

            return Config.Clamp(usage, 0.12f, 1f);
        }
    }
}
