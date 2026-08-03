using System;

namespace BonelabAdvancedHealth
{
    public sealed class LimbHealth
    {
        public BodyPart Part { get; }
        public float MaxHp { get; }
        public float Hp { get; private set; }
        public FractureState Fracture { get; private set; }
        public int BulletHitCount { get; private set; }
        public float BleedingMultiplier { get; }
        public float PainMultiplier { get; }
        public bool IsDisabled => Hp <= MaxHp * 0.18f || Fracture == FractureState.Shattered;
        public bool IsBroken => Fracture == FractureState.Fractured || Fracture == FractureState.Shattered;
        public float DamagePercent => 1f - (Hp / MaxHp);

        public event Action<LimbHealth, DamageInfo>? DamageApplied;
        public event Action<LimbHealth, FractureState>? FractureChanged;

        public LimbHealth(BodyPart part, float maxHp, float bleedingMultiplier, float painMultiplier)
        {
            Part = part;
            MaxHp = maxHp;
            Hp = maxHp;
            BleedingMultiplier = bleedingMultiplier;
            PainMultiplier = painMultiplier;
            Fracture = FractureState.None;
        }

        public float ApplyDamage(DamageInfo info, Random random)
        {
            if (info.Damage <= 0f)
                return 0f;

            float previousHp = Hp;
            if (info.DamageType == AdvancedDamageType.Bullet)
                BulletHitCount++;
            float structuralDamage = GetStructuralDamage(info);
            Hp = Config.Clamp(Hp - structuralDamage, 0f, MaxHp);
            EvaluateFracture(info, random);
            DamageApplied?.Invoke(this, info);
            return previousHp - Hp;
        }

        public void Heal(float amount)
        {
            if (amount <= 0f)
                return;

            Hp = Config.Clamp(Hp + amount, 0f, MaxHp);
        }

        public void StabilizeFracture(float strength)
        {
            if (strength <= 0f || Fracture == FractureState.None)
                return;

            FractureState previous = Fracture;
            if (Fracture == FractureState.Shattered && strength >= 0.7f)
                Fracture = FractureState.Fractured;
            else if (Fracture == FractureState.Fractured && strength >= 0.45f)
                Fracture = FractureState.Sprain;
            else if (Fracture == FractureState.Sprain && strength >= 0.25f)
                Fracture = FractureState.None;

            if (previous != Fracture)
                FractureChanged?.Invoke(this, Fracture);
        }

        public void ClearFracture()
        {
            if (Fracture == FractureState.None)
                return;

            Fracture = FractureState.None;
            FractureChanged?.Invoke(this, Fracture);
        }

        public void Reset()
        {
            Hp = MaxHp;
            BulletHitCount = 0;
            if (Fracture != FractureState.None)
            {
                Fracture = FractureState.None;
                FractureChanged?.Invoke(this, Fracture);
            }
        }

        private void EvaluateFracture(DamageInfo info, Random random)
        {
            if (!CanFractureFromHit(info))
                return;
            if (info.FractureChance <= 0f || random.NextDouble() > info.FractureChance)
                return;

            FractureState next = DetermineFractureState(info);
            if (next <= Fracture)
                return;

            Fracture = next;
            FractureChanged?.Invoke(this, Fracture);
        }

        private FractureState DetermineFractureState(DamageInfo info)
        {
            float severity = info.Damage;
            if (info.DamageType == AdvancedDamageType.Explosion)
                severity *= 1.25f;
            else if (info.DamageType == AdvancedDamageType.Fall)
                severity *= info.IsHighEnergyImpact ? 0.88f : 0.68f;
            else if (info.DamageType == AdvancedDamageType.Bullet && MedicalInspectionSystem.IsArm(Part))
                severity *= info.IsHighCaliber ? 0.82f : 0.48f;
            else if (info.DamageType == AdvancedDamageType.Bullet && MedicalInspectionSystem.IsLeg(Part))
                severity *= info.IsHighCaliber ? 0.90f : 0.58f;
            if (Part == BodyPart.Head || Part == BodyPart.Torso)
                severity *= 0.8f;
            if (MedicalInspectionSystem.IsArm(Part) && info.DamageType == AdvancedDamageType.Fall)
                severity *= 0.9f;

            if (severity >= 118f)
                return FractureState.Shattered;
            if (severity >= 48f)
                return FractureState.Fractured;
            return FractureState.Sprain;
        }

        private float GetStructuralDamage(DamageInfo info)
        {
            if (info.DamageType != AdvancedDamageType.Bullet)
                return info.Damage;

            if (MedicalInspectionSystem.IsArm(Part))
                return info.Damage * (info.IsHighCaliber ? 0.58f : 0.34f);
            if (MedicalInspectionSystem.IsLeg(Part))
                return info.Damage * (info.IsHighCaliber ? 0.68f : 0.42f);

            return info.Damage;
        }

        private bool CanFractureFromHit(DamageInfo info)
        {
            if (info.DamageType != AdvancedDamageType.Bullet)
                return true;

            bool arm = MedicalInspectionSystem.IsArm(Part);
            bool leg = MedicalInspectionSystem.IsLeg(Part);
            if (!arm && !leg)
                return true;

            if (info.IsHighCaliber)
                return BulletHitCount >= (arm ? 7 : 6) && (info.Damage >= 26f || DamagePercent >= (arm ? 0.48f : 0.42f));

            return BulletHitCount >= (arm ? 9 : 8) && (info.Damage >= (arm ? 38f : 42f) || DamagePercent >= (arm ? 0.68f : 0.60f));
        }
    }
}
