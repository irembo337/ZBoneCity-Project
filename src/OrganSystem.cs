using System;

namespace BonelabAdvancedHealth
{
    public readonly struct OrganDamageFeedback
    {
        public readonly OrganType PrimaryOrgan;
        public readonly float Pain;
        public readonly BleedSeverity BleedSeverity;
        public readonly float BleedMultiplier;
        public readonly WoundSeverity WoundSeverity;
        public readonly bool InstantCollapse;
        public readonly bool CardiacArrest;
        public readonly bool LungCollapsed;
        public readonly bool BrainTrauma;

        public OrganDamageFeedback(
            OrganType primaryOrgan,
            float pain,
            BleedSeverity bleedSeverity,
            float bleedMultiplier,
            WoundSeverity woundSeverity,
            bool instantCollapse,
            bool cardiacArrest,
            bool lungCollapsed,
            bool brainTrauma)
        {
            PrimaryOrgan = primaryOrgan;
            Pain = pain;
            BleedSeverity = bleedSeverity;
            BleedMultiplier = bleedMultiplier;
            WoundSeverity = woundSeverity;
            InstantCollapse = instantCollapse;
            CardiacArrest = cardiacArrest;
            LungCollapsed = lungCollapsed;
            BrainTrauma = brainTrauma;
        }

        public static OrganDamageFeedback None => new OrganDamageFeedback(
            OrganType.Muscles,
            0f,
            BleedSeverity.None,
            1f,
            WoundSeverity.None,
            false,
            false,
            false,
            false);
    }

    public sealed class OrganHealth
    {
        public OrganType Type { get; }
        public float Integrity { get; private set; }
        public float BleedingModifier { get; }
        public float PainModifier { get; }
        public OrganFailureState FailureState { get; private set; }
        public bool HasFailed => FailureState == OrganFailureState.Failed;

        public OrganHealth(OrganType type, float bleedingModifier, float painModifier)
        {
            Type = type;
            Integrity = 100f;
            BleedingModifier = bleedingModifier;
            PainModifier = painModifier;
            FailureState = OrganFailureState.Healthy;
        }

        public void Reset()
        {
            Integrity = 100f;
            FailureState = OrganFailureState.Healthy;
        }

        public float ApplyDamage(float damage)
        {
            if (damage <= 0f)
                return 0f;

            float previous = Integrity;
            Integrity = Config.Clamp(Integrity - damage, 0f, 100f);
            if (Integrity <= 0f)
                FailureState = OrganFailureState.Failed;
            else if (Integrity <= 30f)
                FailureState = OrganFailureState.Critical;
            else if (Integrity <= 70f)
                FailureState = OrganFailureState.Damaged;
            else
                FailureState = OrganFailureState.Healthy;

            return previous - Integrity;
        }

        public void Heal(float amount)
        {
            if (amount <= 0f)
                return;

            Integrity = Config.Clamp(Integrity + amount, 0f, 100f);
            if (Integrity <= 0f)
                FailureState = OrganFailureState.Failed;
            else if (Integrity <= 30f)
                FailureState = OrganFailureState.Critical;
            else if (Integrity <= 70f)
                FailureState = OrganFailureState.Damaged;
            else
                FailureState = OrganFailureState.Healthy;
        }
    }

    public sealed class OrganSystem
    {
        private readonly HealthManager _manager;
        private readonly OrganHealth[] _organs;
        private float _cardiacArrestSeconds;
        private float _brainFailureSeconds;

        public bool CardiacArrestActive => _cardiacArrestSeconds > 0f || GetOrgan(OrganType.Heart).HasFailed;
        public float HeartbeatStrength { get; private set; } = 1f;
        public float OrganPainModifier { get; private set; } = 1f;

        public OrganSystem(HealthManager manager)
        {
            _manager = manager;
            _organs = new OrganHealth[Config.OrganCount];
            _organs[(int)OrganType.Brain] = new OrganHealth(OrganType.Brain, 0.3f, 1.9f);
            _organs[(int)OrganType.Heart] = new OrganHealth(OrganType.Heart, 2.7f, 2.1f);
            _organs[(int)OrganType.Lungs] = new OrganHealth(OrganType.Lungs, 1.6f, 1.55f);
            _organs[(int)OrganType.Liver] = new OrganHealth(OrganType.Liver, 2.0f, 1.45f);
            _organs[(int)OrganType.Stomach] = new OrganHealth(OrganType.Stomach, 1.1f, 1.25f);
            _organs[(int)OrganType.Muscles] = new OrganHealth(OrganType.Muscles, 0.75f, 0.9f);
        }

        public OrganHealth GetOrgan(OrganType type)
        {
            return _organs[(int)type];
        }

        public void Reset()
        {
            for (int i = 0; i < _organs.Length; i++)
                _organs[i].Reset();

            _cardiacArrestSeconds = 0f;
            _brainFailureSeconds = 0f;
            HeartbeatStrength = 1f;
            OrganPainModifier = 1f;
        }

        public void Update(float deltaTime)
        {
            OrganHealth heart = GetOrgan(OrganType.Heart);
            OrganHealth brain = GetOrgan(OrganType.Brain);
            HeartbeatStrength = Config.Clamp(heart.Integrity / 100f - (CardiacArrestActive ? 0.45f : 0f), 0f, 1f);
            OrganPainModifier = 1f + (1f - AverageIntegrityNormalized()) * 0.55f;

            if (_cardiacArrestSeconds > 0f)
            {
                _cardiacArrestSeconds -= deltaTime;
                _manager.Consciousness.SetUnconscious(3.0f);
                _manager.Bleeding.RestoreBlood(-18f * deltaTime);
                if (_cardiacArrestSeconds <= 0f || heart.Integrity <= 4f)
                    _manager.RequestDeath(DeathCause.CardiacArrest);
            }

            if (brain.HasFailed)
            {
                _brainFailureSeconds += deltaTime;
                if (_brainFailureSeconds >= 1.0f)
                    _manager.RequestDeath(DeathCause.BrainFailure);
            }
            else
            {
                _brainFailureSeconds = 0f;
            }
        }

        public OrganDamageFeedback ApplyDamage(DamageInfo info)
        {
            OrganType primary = PickPrimaryOrgan(info);
            OrganHealth organ = GetOrgan(primary);
            float organDamage = CalculateOrganDamage(info, primary);
            float applied = organ.ApplyDamage(organDamage);
            if (applied <= 0f)
                return OrganDamageFeedback.None;

            bool heart = primary == OrganType.Heart;
            bool lung = primary == OrganType.Lungs;
            bool brain = primary == OrganType.Brain;
            bool cardiac = heart && (info.Damage >= 35f || organ.Integrity <= 35f || info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Stab);
            bool instantCollapse = cardiac || (brain && organ.Integrity <= 45f);
            bool lungCollapsed = lung && (info.Damage >= 22f || organ.Integrity <= 65f);
            bool brainTrauma = brain && info.DamageType == AdvancedDamageType.Blunt;

            if (cardiac)
                _cardiacArrestSeconds = Math.Max(_cardiacArrestSeconds, organ.Integrity <= 15f ? 2.5f : 8.0f);

            BleedSeverity bleedSeverity = PickBleedSeverity(info, organ, primary);
            WoundSeverity woundSeverity = PickWoundSeverity(info, primary, bleedSeverity);
            float pain = applied * organ.PainModifier * (brain ? 0.65f : 0.9f);
            float bleedMultiplier = organ.BleedingModifier * (heart ? 2.2f : 1f);

            return new OrganDamageFeedback(primary, pain, bleedSeverity, bleedMultiplier, woundSeverity, instantCollapse, cardiac, lungCollapsed, brainTrauma);
        }

        public void HealInternal(float amount)
        {
            for (int i = 0; i < _organs.Length; i++)
                _organs[i].Heal(amount);

            if (GetOrgan(OrganType.Heart).Integrity > 35f)
                _cardiacArrestSeconds = 0f;
        }

        private float AverageIntegrityNormalized()
        {
            float total = 0f;
            for (int i = 0; i < _organs.Length; i++)
                total += _organs[i].Integrity;
            return total / (_organs.Length * 100f);
        }

        private OrganType PickPrimaryOrgan(DamageInfo info)
        {
            if (info.BodyPart == BodyPart.Head)
                return OrganType.Brain;

            if (info.BodyPart != BodyPart.Torso)
                return OrganType.Muscles;

            double roll = _manager.Random.NextDouble();
            if (info.DamageType == AdvancedDamageType.Explosion)
            {
                if (roll < 0.34)
                    return OrganType.Lungs;
                if (roll < 0.54)
                    return OrganType.Heart;
                if (roll < 0.78)
                    return OrganType.Liver;
                return OrganType.Muscles;
            }

            if (info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Stab)
            {
                if (roll < 0.16)
                    return OrganType.Heart;
                if (roll < 0.52)
                    return OrganType.Lungs;
                if (roll < 0.74)
                    return OrganType.Liver;
                if (roll < 0.88)
                    return OrganType.Stomach;
                return OrganType.Muscles;
            }

            if (info.DamageType == AdvancedDamageType.Blunt)
            {
                if (roll < 0.24)
                    return OrganType.Lungs;
                if (roll < 0.42)
                    return OrganType.Liver;
                return OrganType.Muscles;
            }

            if (info.DamageType == AdvancedDamageType.Fall)
                return roll < 0.18 ? OrganType.Liver : OrganType.Muscles;

            return OrganType.Muscles;
        }

        private static float CalculateOrganDamage(DamageInfo info, OrganType organ)
        {
            float amount = info.Damage;
            switch (organ)
            {
                case OrganType.Brain:
                    return amount * (info.DamageType == AdvancedDamageType.Blunt ? 0.85f : 1.45f);
                case OrganType.Heart:
                    return amount * 1.65f;
                case OrganType.Lungs:
                    return amount * 1.2f;
                case OrganType.Liver:
                    return amount * 1.05f;
                case OrganType.Stomach:
                    return amount * 0.9f;
                default:
                    return amount * 0.42f;
            }
        }

        private static BleedSeverity PickBleedSeverity(DamageInfo info, OrganHealth organ, OrganType type)
        {
            float score = info.Damage * organ.BleedingModifier;
            if (type == OrganType.Heart)
                score *= 2.4f;
            if (type == OrganType.Liver)
                score *= 1.4f;
            if (info.DamageType == AdvancedDamageType.Stab || info.DamageType == AdvancedDamageType.Bullet)
                score *= 1.2f;

            if (score >= 85f || type == OrganType.Heart)
                return BleedSeverity.Arterial;
            if (score >= 42f)
                return BleedSeverity.Severe;
            if (score >= 18f)
                return BleedSeverity.Medium;
            if (score >= 8f)
                return BleedSeverity.Light;
            return BleedSeverity.None;
        }

        private static WoundSeverity PickWoundSeverity(DamageInfo info, OrganType organ, BleedSeverity bleed)
        {
            if (organ == OrganType.Heart || organ == OrganType.Liver || bleed == BleedSeverity.Arterial)
                return WoundSeverity.OrganRupture;
            if (info.DamageType == AdvancedDamageType.Stab || info.DamageType == AdvancedDamageType.Bullet)
                return bleed >= BleedSeverity.Severe ? WoundSeverity.DeepCut : WoundSeverity.SurfaceCut;
            if (info.DamageType == AdvancedDamageType.Explosion)
                return WoundSeverity.DeepCut;
            return WoundSeverity.None;
        }
    }
}
