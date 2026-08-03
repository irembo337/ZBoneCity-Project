namespace BonelabAdvancedHealth
{
    public static class AdvancedBleedingSystem
    {
        public static float GetRateMultiplier(DamageInfo info, BleedSeverity severity, WoundSeverity woundSeverity)
        {
            float multiplier = 1f;
            if (info.IsSelfInflicted)
            {
                if (info.DamageType == AdvancedDamageType.Bullet)
                    multiplier *= info.BodyPart == BodyPart.Head || info.BodyPart == BodyPart.Torso ? 1.85f : 1.35f;
                else if (info.DamageType == AdvancedDamageType.Stab)
                    multiplier *= info.BodyPart == BodyPart.Torso ? 1.45f : 1.22f;
                else if (info.DamageType == AdvancedDamageType.Blunt)
                    multiplier *= 1.15f;
            }

            if (woundSeverity == WoundSeverity.OrganRupture)
                multiplier *= 1.65f;
            else if (woundSeverity == WoundSeverity.ArterialCut)
                multiplier *= 1.35f;

            if (info.IsHighEnergyImpact && info.DamageType == AdvancedDamageType.Fall)
                multiplier *= severity >= BleedSeverity.Severe ? 1.45f : 1.20f;

            return Config.Clamp(multiplier, 0.25f, 5.5f);
        }

        public static float GetDurationMultiplier(DamageInfo info, BleedSeverity severity, WoundSeverity woundSeverity)
        {
            float multiplier = 1f;
            if (info.IsSelfInflicted && (info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Stab))
                multiplier *= 1.25f;
            if (woundSeverity == WoundSeverity.OrganRupture)
                multiplier *= 1.5f;
            if (severity == BleedSeverity.Arterial)
                multiplier *= 1.2f;
            return Config.Clamp(multiplier, 0.5f, 3.5f);
        }

        public static bool IsInternalBleed(WoundSeverity woundSeverity)
        {
            return woundSeverity == WoundSeverity.OrganRupture;
        }
    }
}
