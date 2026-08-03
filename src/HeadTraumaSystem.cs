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
        private static readonly float[] DamageTypeScale =
        {
            1.15f,
            1.35f,
            1.25f,
            0.75f,
            0.95f
        };

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
                float koChance = Config.Clamp(0.02f + normalized * 0.12f + currentConcussion * 0.12f, 0.01f, 0.30f);
                if (info.Damage >= 28f && manager.Random.NextDouble() < koChance)
                    instantKo = true;
                else if (info.Damage >= 22f)
                {
                    delayed = true;
                    delayedSeconds = Config.Clamp(0.9f + normalized * 1.8f, 0.9f, 4.0f);
                }
            }
            else if (info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Explosion)
            {
                instantKo = info.Damage >= 54f || organFeedback.PrimaryOrgan == OrganType.Brain && info.Damage >= 30f;
                delayed = !instantKo && info.Damage >= 24f;
                delayedSeconds = delayed ? Config.Clamp(0.65f + normalized * 1.25f, 0.65f, 3.0f) : 0f;
            }

            return new HeadTraumaFeedback(concussion, dizziness, ringing, recoverySeconds, delayedSeconds, instantKo, delayed);
        }

        private static float GetTypeScale(AdvancedDamageType damageType)
        {
            int index = (int)damageType;
            return index >= 0 && index < DamageTypeScale.Length ? DamageTypeScale[index] : 0.75f;
        }
    }
}
