using System;

namespace BonelabAdvancedHealth
{
    public readonly struct BoneDamageFeedback
    {
        public readonly float Pain;
        public readonly bool RibFractured;
        public readonly bool MajorBoneFractured;

        public BoneDamageFeedback(float pain, bool ribFractured, bool majorBoneFractured)
        {
            Pain = pain;
            RibFractured = ribFractured;
            MajorBoneFractured = majorBoneFractured;
        }
    }

    public sealed class BoneHealth
    {
        public BoneType Type { get; }
        public float MaxIntegrity { get; }
        public float Integrity { get; private set; }
        public FractureState Fracture { get; private set; }
        public bool IsRib => Type >= BoneType.LeftRib1;
        public bool IsBroken => Fracture == FractureState.Fractured || Fracture == FractureState.Shattered;

        public BoneHealth(BoneType type, float maxIntegrity)
        {
            Type = type;
            MaxIntegrity = maxIntegrity;
            Integrity = maxIntegrity;
            Fracture = FractureState.None;
        }

        public BoneDamageFeedback ApplyDamage(DamageInfo info, float damage, Random random)
        {
            if (damage <= 0f)
                return new BoneDamageFeedback(0f, false, false);

            float previous = Integrity;
            Integrity = Config.Clamp(Integrity - damage, 0f, MaxIntegrity);

            FractureState previousFracture = Fracture;
            FractureState fracture = DetermineFracture(info, damage, random);
            if (fracture > Fracture)
                Fracture = fracture;

            float applied = previous - Integrity;
            bool newBreak = Fracture > previousFracture && IsBroken;
            float pain = applied * (IsRib ? 0.34f : 0.24f);
            if (newBreak)
                pain += IsRib ? 7.5f : 11.0f;

            return new BoneDamageFeedback(pain, newBreak && IsRib, newBreak && !IsRib);
        }

        public void Stabilize(float strength)
        {
            if (strength <= 0f || Fracture == FractureState.None)
                return;

            if (Fracture == FractureState.Shattered && strength >= 0.75f)
                Fracture = FractureState.Fractured;
            else if (Fracture == FractureState.Fractured && strength >= 0.55f)
                Fracture = FractureState.Sprain;
            else if (Fracture == FractureState.Sprain && strength >= 0.3f)
                Fracture = FractureState.None;
        }

        public void Reset()
        {
            Integrity = MaxIntegrity;
            Fracture = FractureState.None;
        }

        private FractureState DetermineFracture(DamageInfo info, float damage, Random random)
        {
            if (info.DamageType == AdvancedDamageType.Stab || info.DamageType == AdvancedDamageType.Bullet)
                damage *= IsRib ? 0.45f : 0.32f;

            float chance = info.FractureChance * (IsRib ? 0.75f : 0.55f);
            if (info.DamageType == AdvancedDamageType.Fall)
                chance *= IsRib ? 0.42f : 0.55f;
            if (damage < (IsRib ? 18f : 26f))
                chance *= 0.35f;

            if (random.NextDouble() > Config.Clamp(chance, 0f, 0.82f))
                return Fracture;

            float severity = damage;
            if (info.DamageType == AdvancedDamageType.Explosion)
                severity *= 1.2f;
            if (info.DamageType == AdvancedDamageType.Fall)
                severity *= 0.82f;

            if (severity >= (IsRib ? 58f : 92f))
                return FractureState.Shattered;
            if (severity >= (IsRib ? 26f : 42f))
                return FractureState.Fractured;
            return FractureState.Sprain;
        }
    }

    public sealed class BoneSystem
    {
        private readonly HealthManager _manager;
        private readonly BoneHealth[] _bones = new BoneHealth[Config.BoneCount];

        public int FracturedRibCount { get; private set; }
        public int TotalBrokenBoneCount { get; private set; }
        public float RibBreathingPenalty { get; private set; }
        public float StructuralInstability { get; private set; }

        public BoneSystem(HealthManager manager)
        {
            _manager = manager;
            for (int i = 0; i < _bones.Length; i++)
                _bones[i] = new BoneHealth((BoneType)i, GetMaxIntegrity((BoneType)i));
        }

        public BoneHealth GetBone(BoneType type)
        {
            return _bones[(int)type];
        }

        public void Reset()
        {
            for (int i = 0; i < _bones.Length; i++)
                _bones[i].Reset();
            Recalculate();
        }

        public BoneDamageFeedback ApplyDamage(DamageInfo info)
        {
            BoneDamageFeedback total = new BoneDamageFeedback(0f, false, false);
            BoneType primary = SelectPrimaryBone(info);
            total = AddFeedback(total, ApplyToBone(primary, info, GetPrimaryBoneDamage(info)));

            if (info.BodyPart == BodyPart.Torso)
            {
                BoneType ribA = SelectRib(info, 0);
                BoneType ribB = SelectRib(info, 1);
                total = AddFeedback(total, ApplyToBone(ribA, info, GetRibDamage(info, 0)));
                if (ribB != ribA)
                    total = AddFeedback(total, ApplyToBone(ribB, info, GetRibDamage(info, 1)));
            }

            Recalculate();
            return total;
        }

        public void StabilizeMostDamaged(float strength)
        {
            int best = -1;
            float lowest = float.MaxValue;
            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i].Fracture == FractureState.None)
                    continue;
                if (_bones[i].Integrity < lowest)
                {
                    lowest = _bones[i].Integrity;
                    best = i;
                }
            }

            if (best >= 0)
            {
                _bones[best].Stabilize(strength);
                Recalculate();
            }
        }

        private BoneDamageFeedback ApplyToBone(BoneType type, DamageInfo info, float damage)
        {
            return _bones[(int)type].ApplyDamage(info, damage, _manager.Random);
        }

        private void Recalculate()
        {
            int ribs = 0;
            int broken = 0;
            float structural = 0f;
            for (int i = 0; i < _bones.Length; i++)
            {
                BoneHealth bone = _bones[i];
                if (!bone.IsBroken)
                    continue;

                broken++;
                if (bone.IsRib)
                    ribs++;
                else
                    structural += bone.Fracture == FractureState.Shattered ? 0.22f : 0.12f;
            }

            FracturedRibCount = ribs;
            TotalBrokenBoneCount = broken;
            RibBreathingPenalty = Config.Clamp(ribs * 0.075f + CountShatteredRibs() * 0.045f, 0f, 0.92f);
            StructuralInstability = Config.Clamp(structural, 0f, 0.75f);
        }

        private int CountShatteredRibs()
        {
            int count = 0;
            for (int i = (int)BoneType.LeftRib1; i < _bones.Length; i++)
            {
                if (_bones[i].Fracture == FractureState.Shattered)
                    count++;
            }

            return count;
        }

        private static BoneDamageFeedback AddFeedback(BoneDamageFeedback a, BoneDamageFeedback b)
        {
            return new BoneDamageFeedback(
                a.Pain + b.Pain,
                a.RibFractured || b.RibFractured,
                a.MajorBoneFractured || b.MajorBoneFractured);
        }

        private static BoneType SelectPrimaryBone(DamageInfo info)
        {
            switch (info.BodyPart)
            {
                case BodyPart.Head:
                    return BoneType.Skull;
                case BodyPart.LeftArm:
                    return info.Origin.y > 0f && info.Direction.y < -0.25f ? BoneType.LeftHumerus : BoneType.LeftForearm;
                case BodyPart.RightArm:
                    return info.Origin.y > 0f && info.Direction.y < -0.25f ? BoneType.RightHumerus : BoneType.RightForearm;
                case BodyPart.LeftLeg:
                    return info.DamageType == AdvancedDamageType.Fall ? BoneType.LeftShin : BoneType.LeftFemur;
                case BodyPart.RightLeg:
                    return info.DamageType == AdvancedDamageType.Fall ? BoneType.RightShin : BoneType.RightFemur;
                case BodyPart.Torso:
                default:
                    return info.DamageType == AdvancedDamageType.Fall ? BoneType.Pelvis : BoneType.Spine;
            }
        }

        private static BoneType SelectRib(DamageInfo info, int offset)
        {
            int side = info.Direction.x >= 0f ? 0 : 1;
            int rib = MathfAbsHash(info.SourceId + info.AttackOrder + offset * 17) % 6;
            return (BoneType)((side == 0 ? (int)BoneType.LeftRib1 : (int)BoneType.RightRib1) + rib);
        }

        private static int MathfAbsHash(int value)
        {
            unchecked
            {
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
            }

            return value < 0 ? -value : value;
        }

        private static float GetPrimaryBoneDamage(DamageInfo info)
        {
            float damage = info.Damage * 0.52f;
            if (info.DamageType == AdvancedDamageType.Fall)
                damage *= 0.62f;
            if (info.DamageType == AdvancedDamageType.Blunt)
                damage *= 0.85f;
            return damage;
        }

        private static float GetRibDamage(DamageInfo info, int offset)
        {
            float damage = info.Damage * (offset == 0 ? 0.40f : 0.24f);
            if (info.DamageType == AdvancedDamageType.Fall)
                damage *= 0.48f;
            if (info.DamageType == AdvancedDamageType.Stab || info.DamageType == AdvancedDamageType.Bullet)
                damage *= 0.55f;
            return damage;
        }

        private static float GetMaxIntegrity(BoneType type)
        {
            if (type >= BoneType.LeftRib1)
                return 55f;
            switch (type)
            {
                case BoneType.Skull:
                    return 95f;
                case BoneType.Spine:
                    return 130f;
                case BoneType.Pelvis:
                    return 120f;
                case BoneType.LeftFemur:
                case BoneType.RightFemur:
                    return 125f;
                case BoneType.LeftShin:
                case BoneType.RightShin:
                    return 105f;
                default:
                    return 85f;
            }
        }
    }
}
