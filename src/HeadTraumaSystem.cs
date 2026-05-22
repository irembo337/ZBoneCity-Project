using System;

namespace BonelabAdvancedHealth
{
    public readonly struct HeadTraumaFeedback
    {
        public readonly float Concussion;
        public readonly float Dizziness;
        public readonly float Ringing;
        public readonly float RecoverySeconds;
        public readonly float DelayedCollapseSeconds;
        public readonly bool InstantKnockout;
        public readonly bool ArmDelayedCollapse;

        public HeadTraumaFeedback(
            float concussion,
            float dizziness,
            float ringing,
            float recoverySeconds,
            float delayedCollapseSeconds,
            bool instantKnockout,
            bool armDelayedCollapse)
        {
            Concussion = concussion;
            Dizziness = dizziness;
            Ringing = ringing;
            RecoverySeconds = recoverySeconds;
            DelayedCollapseSeconds = delayedCollapseSeconds;
            InstantKnockout = instantKnockout;
            ArmDelayedCollapse = armDelayedCollapse;
        }
    }

    public static class HeadTraumaSystem
    {
        public static HeadTraumaFeedback Evaluate(HealthManager manager, DamageInfo info, OrganDamageFeedback organFeedback, float currentConcussion)
        {
            if (info.BodyPart != BodyPart.Head && !organFeedback.BrainTrauma)
                return new HeadTraumaFeedback(0f, 0f, 0f, 0f, 0f, false, false);

            float normalized = Config.Clamp(info.Damage / 70f, 0f, 1.65f);
            float typeScale = GetTypeScale(info.DamageType);
            float concussion = normalized * 0.42f * typeScale;
            float dizziness = normalized * 0.36f * typeScale;
            float ringing = Config.Clamp(normalized * (info.DamageType == AdvancedDamageType.Blunt ? 0.9f : 0.55f), 0f, 1f);

            bool instantKo = false;
            bool delayed = false;
            float delayedSeconds = 0f;
            float recoverySeconds = normalized > 0.18f ? 18f + normalized * 22f : 0f;

            if (info.DamageType == AdvancedDamageType.Blunt)
            {
                float koChance = Config.Clamp(0.04f + normalized * 0.20f + currentConcussion * 0.18f, 0.02f, 0.42f);
                if (info.Damage >= 18f && manager.Random.NextDouble() < koChance)
                    instantKo = true;
                else if (info.Damage >= 16f)
                {
                    delayed = true;
                    delayedSeconds = Config.Clamp(0.75f + normalized * 2.2f, 0.75f, 4.2f);
                }
            }
            else if (info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Explosion)
            {
                instantKo = info.Damage >= 42f || organFeedback.PrimaryOrgan == OrganType.Brain && info.Damage >= 28f;
                delayed = !instantKo && info.Damage >= 20f;
                delayedSeconds = delayed ? Config.Clamp(0.5f + normalized * 1.4f, 0.5f, 3.0f) : 0f;
            }

            return new HeadTraumaFeedback(concussion, dizziness, ringing, recoverySeconds, delayedSeconds, instantKo, delayed);
        }

        private static float GetTypeScale(AdvancedDamageType damageType)
        {
            switch (damageType)
            {
                case AdvancedDamageType.Blunt:
                    return 1.35f;
                case AdvancedDamageType.Explosion:
                    return 1.25f;
                case AdvancedDamageType.Bullet:
                    return 1.15f;
                case AdvancedDamageType.Fall:
                    return 0.95f;
                default:
                    return 0.75f;
            }
        }
    }
}
