using System;

namespace BonelabAdvancedHealth
{
    public sealed class LimbHealth
    {
        public BodyPart Part { get; }
        public float MaxHp { get; }
        public float Hp { get; private set; }
        public FractureState Fracture { get; private set; }
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
            Hp = Config.Clamp(Hp - info.Damage, 0f, MaxHp);
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

        public void Reset()
        {
            Hp = MaxHp;
            if (Fracture != FractureState.None)
            {
                Fracture = FractureState.None;
                FractureChanged?.Invoke(this, Fracture);
            }
        }

        private void EvaluateFracture(DamageInfo info, Random random)
        {
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
            if (info.DamageType == AdvancedDamageType.Explosion || info.DamageType == AdvancedDamageType.Fall)
                severity *= 1.25f;
            if (Part == BodyPart.Head || Part == BodyPart.Torso)
                severity *= 0.8f;

            if (severity >= 85f)
                return FractureState.Shattered;
            if (severity >= 32f)
                return FractureState.Fractured;
            return FractureState.Sprain;
        }
    }
}
