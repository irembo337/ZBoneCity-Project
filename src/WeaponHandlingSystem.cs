namespace BonelabAdvancedHealth
{
    public sealed class WeaponHandlingSystem
    {
        private readonly HealthManager _manager;

        public float AimShake { get; private set; }
        public float RecoilControlPenalty { get; private set; }
        public float ReloadPenalty { get; private set; }
        public float LeftGripMultiplier { get; private set; } = 1f;
        public float RightGripMultiplier { get; private set; } = 1f;
        public float StaminaEfficiency { get; private set; } = 1f;

        public WeaponHandlingSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            AimShake = 0f;
            RecoilControlPenalty = 0f;
            ReloadPenalty = 0f;
            LeftGripMultiplier = 1f;
            RightGripMultiplier = 1f;
            StaminaEfficiency = 1f;
        }

        public void Update(float deltaTime)
        {
            float pain = _manager.PainSystem.AimInstability;
            float stress = _manager.Stress.WeaponInstability;
            float fracture = _manager.Fractures.AimInstability;
            float oxygen = _manager.Lungs.OxygenStress;
            float shock = _manager.Shock.StaminaPenalty;
            float medicationShake = _manager.Medication.HandShake;
            float rehabAim = _manager.Rehabilitation.AimPenalty;
            float rehabStamina = _manager.Rehabilitation.StaminaPenalty;
            float zcityRecoil = _manager.ZCityOrganism.RecoilMultiplier;
            float zcityLegStrength = _manager.ZCityOrganism.LegStrength;

            AimShake = Config.Clamp(fracture * 0.52f + pain * 0.30f + stress * 0.26f + oxygen * 0.16f + medicationShake * 0.38f + rehabAim * 0.25f + (zcityRecoil - 1f) * 0.22f, 0f, 1.65f);
            RecoilControlPenalty = Config.Clamp(AimShake * 0.42f + shock * 0.22f + _manager.Medication.ReactionPenalty * 0.16f + (zcityRecoil - 1f) * 0.18f, 0f, 0.92f);
            ReloadPenalty = Config.Clamp((1f - _manager.Fractures.LeftArmUsage) * 0.28f + (1f - _manager.Fractures.RightArmUsage) * 0.28f + pain * 0.12f + stress * 0.12f + _manager.Medication.ReactionPenalty * 0.18f + (1f - _manager.ZCityOrganism.MeleeSpeed) * 0.18f, 0f, 0.86f);
            LeftGripMultiplier = Config.Clamp(1f - RecoilControlPenalty * 0.22f - stress * 0.16f, 0.18f, 1f);
            RightGripMultiplier = Config.Clamp(1f - RecoilControlPenalty * 0.22f - stress * 0.16f, 0.18f, 1f);
            StaminaEfficiency = Config.Clamp((1f - shock * 0.45f - stress * 0.22f - oxygen * 0.28f - rehabStamina * 0.35f - _manager.Medication.FatiguePenalty * 0.30f + _manager.Medication.StaminaBoost) * zcityLegStrength, 0.18f, 1.12f);
        }
    }
}
