using System;

namespace BonelabAdvancedHealth
{
    public sealed class BrokenArmController
    {
        private static readonly float[] FractureSeverity =
        {
            0f,
            0.34f,
            0.72f,
            1f
        };

        private static readonly float[] FractureShake =
        {
            0f,
            0.12f,
            0.32f,
            0.55f
        };

        private readonly HealthManager _manager;
        private float _painAccumulator;

        public float LeftArmSeverity { get; private set; }
        public float RightArmSeverity { get; private set; }
        public float LeftArmShake { get; private set; }
        public float RightArmShake { get; private set; }
        public float AimInstabilityBonus => Config.Clamp((LeftArmSeverity + RightArmSeverity) * 0.28f + Math.Max(LeftArmShake, RightArmShake) * 0.24f, 0f, 0.85f);

        public BrokenArmController(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            _painAccumulator = 0f;
            LeftArmSeverity = 0f;
            RightArmSeverity = 0f;
            LeftArmShake = 0f;
            RightArmShake = 0f;
        }

        public void Update(float deltaTime)
        {
            LimbHealth left = _manager.GetLimb(BodyPart.LeftArm);
            LimbHealth right = _manager.GetLimb(BodyPart.RightArm);
            LeftArmSeverity = CalculateSeverity(left);
            RightArmSeverity = CalculateSeverity(right);
            LeftArmShake = CalculateShake(left, LeftArmSeverity);
            RightArmShake = CalculateShake(right, RightArmSeverity);

            _painAccumulator += deltaTime;
            if (_painAccumulator < 0.45f)
                return;

            _painAccumulator = 0f;
            float movingBrokenArmPain = (LeftArmSeverity + RightArmSeverity) * 0.42f;
            if (movingBrokenArmPain > 0.08f)
                _manager.AddPain(movingBrokenArmPain);
        }

        public float GetUsageMultiplier(BodyPart arm)
        {
            float severity = arm == BodyPart.LeftArm ? LeftArmSeverity : RightArmSeverity;
            if (severity <= 0f)
                return 1f;

            if (severity >= 0.92f)
                return 0.16f;
            if (severity >= 0.68f)
                return Config.Clamp(1f - severity * 0.72f, 0.22f, 0.55f);
            return Config.Clamp(1f - severity * 0.42f, 0.58f, 1f);
        }

        public float GetGripStrength(BodyPart arm, float baseUsage)
        {
            float severity = arm == BodyPart.LeftArm ? LeftArmSeverity : RightArmSeverity;
            float grip = baseUsage;
            if (severity >= 0.92f)
                grip *= 0.12f;
            else if (severity >= 0.68f)
                grip *= 0.32f;
            else if (severity >= 0.36f)
                grip *= 0.58f;

            return Config.Clamp(grip, 0.02f, 1f);
        }

        private static float CalculateSeverity(LimbHealth limb)
        {
            float fracture = GetFractureContribution(limb.Fracture, FractureSeverity);
            float damage = limb.DamagePercent * 0.45f;
            return Config.Clamp(fracture + damage, 0f, 1f);
        }

        private static float CalculateShake(LimbHealth limb, float severity)
        {
            if (severity <= 0.25f)
                return 0f;

            float fractureShake = GetFractureContribution(limb.Fracture, FractureShake);
            return Config.Clamp(fractureShake + severity * 0.42f, 0f, 1f);
        }

        private static float GetFractureContribution(FractureState fracture, float[] table)
        {
            int index = (int)fracture;
            return index >= 0 && index < table.Length ? table[index] : 0f;
        }
    }
}
