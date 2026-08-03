using System;
using Il2CppSLZ.Marrow.Combat;
using Il2CppSLZ.Marrow.Data;
using UnityEngine;
using PlayerBodyPart = Il2CppSLZ.Marrow.PlayerDamageReceiver.BodyPart;
using EnemyBodyPart = Il2CppSLZ.Bonelab.EnemyDamageReceiver.BodyPart;
using EnemyCollisionBodyPart = Il2CppSLZ.Bonelab.EnemyCollisonRelay.BodyPart;

namespace BonelabAdvancedHealth
{
    [Flags]
    public enum DamageContextFlags
    {
        None = 0,
        SelfInflicted = 1,
        HighEnergyImpact = 2,
        HardSurfaceImpact = 4,
        HighCaliber = 8,
        FatalHeadshot = 16,
        NeckHit = 32
    }

    public readonly struct DamageInfo
    {
        public readonly BodyPart BodyPart;
        public readonly AdvancedDamageType DamageType;
        public readonly float Damage;
        public readonly float BleedFactor;
        public readonly float FractureChance;
        public readonly float Pain;
        public readonly float UnconsciousnessImpulse;
        public readonly Vector3 Origin;
        public readonly Vector3 Direction;
        public readonly int SourceId;
        public readonly int AttackOrder;
        public readonly float Time;
        public readonly Collider? SourceCollider;
        public readonly DamageContextFlags Context;
        public readonly float ImpactVelocity;
        public bool IsSelfInflicted => (Context & DamageContextFlags.SelfInflicted) != 0;
        public bool IsHighEnergyImpact => (Context & DamageContextFlags.HighEnergyImpact) != 0;
        public bool IsHardSurfaceImpact => (Context & DamageContextFlags.HardSurfaceImpact) != 0;
        public bool IsHighCaliber => (Context & DamageContextFlags.HighCaliber) != 0;
        public bool IsFatalHeadshot => (Context & DamageContextFlags.FatalHeadshot) != 0;
        public bool IsNeckHit => (Context & DamageContextFlags.NeckHit) != 0;

        public DamageInfo(
            BodyPart bodyPart,
            AdvancedDamageType damageType,
            float damage,
            float bleedFactor,
            float fractureChance,
            float pain,
            float unconsciousnessImpulse,
            Vector3 origin,
            Vector3 direction,
            int sourceId,
            int attackOrder,
            float time,
            Collider? sourceCollider = null,
            DamageContextFlags context = DamageContextFlags.None,
            float impactVelocity = 0f)
        {
            BodyPart = bodyPart;
            DamageType = damageType;
            Damage = damage;
            BleedFactor = bleedFactor;
            FractureChance = fractureChance;
            Pain = pain;
            UnconsciousnessImpulse = unconsciousnessImpulse;
            Origin = origin;
            Direction = direction;
            SourceId = sourceId;
            AttackOrder = attackOrder;
            Time = time;
            SourceCollider = sourceCollider;
            Context = context;
            ImpactVelocity = impactVelocity;
        }
    }

    public readonly struct DamageProfile
    {
        public readonly float BleedFactor;
        public readonly float FractureBaseChance;
        public readonly float PainFactor;
        public readonly float UnconsciousnessFactor;

        public DamageProfile(float bleedFactor, float fractureBaseChance, float painFactor, float unconsciousnessFactor)
        {
            BleedFactor = bleedFactor;
            FractureBaseChance = fractureBaseChance;
            PainFactor = painFactor;
            UnconsciousnessFactor = unconsciousnessFactor;
        }
    }

    public static class DamageProcessor
    {
        private static readonly string[] ExplosionNames =
        {
            "explosion",
            "blast",
            "grenade",
            "frag",
            "rocket",
            "mine",
            "bomb",
            "m203"
        };

        public static DamageInfo FromPlayerAttack(Attack attack, PlayerBodyPart part)
        {
            DamageContextFlags context = part == PlayerBodyPart.Neck ? DamageContextFlags.NeckHit : DamageContextFlags.None;
            return FromAttack(attack, MapPlayerPart(part), Config.PlayerDamageScale, context);
        }

        public static DamageInfo FromNpcAttack(Attack attack, EnemyBodyPart part)
        {
            return FromAttack(attack, MapEnemyPart(part), Config.NpcDamageScale);
        }

        public static DamageInfo FromPlayerCollision(Collision collision, PlayerBodyPart part)
        {
            return ImpactTraumaSystem.FromCollision(collision, MapPlayerPart(part), Config.PlayerDamageScale);
        }

        public static DamageInfo FromNpcCollision(Collision collision, EnemyCollisionBodyPart part)
        {
            return ImpactTraumaSystem.FromCollision(collision, MapEnemyCollisionPart(part), Config.NpcDamageScale);
        }

        public static BleedSeverity GetBleedSeverity(DamageInfo info, LimbHealth limb)
        {
            if (info.BleedFactor <= 0f || info.Damage < 3f)
                return BleedSeverity.None;

            float score = info.Damage * info.BleedFactor * limb.BleedingMultiplier;
            if (info.BodyPart == BodyPart.Head)
                score *= 1.25f;
            if (info.BodyPart == BodyPart.Torso && (info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Stab))
                score *= info.IsSelfInflicted ? 1.55f : 1.25f;
            if (info.IsNeckHit && (info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Stab))
            {
                score *= 2.25f;
                if (info.Damage >= 34f || info.IsHighCaliber || info.DamageType == AdvancedDamageType.Stab)
                    return BleedSeverity.Arterial;
            }
            if (info.DamageType == AdvancedDamageType.Explosion)
                score *= 0.75f;
            if (info.IsSelfInflicted && info.DamageType == AdvancedDamageType.Bullet)
                score *= info.BodyPart == BodyPart.Head || info.BodyPart == BodyPart.Torso ? 1.85f : 1.35f;
            if (info.DamageType == AdvancedDamageType.Fall && info.IsHighEnergyImpact)
                score *= 1.75f;

            if (score >= 65f)
                return BleedSeverity.Arterial;
            if (score >= 34f)
                return BleedSeverity.Severe;
            if (score >= 14f)
                return BleedSeverity.Medium;
            if (score >= 4f)
                return BleedSeverity.Light;

            return BleedSeverity.None;
        }

        public static BodyPart MapPlayerPart(PlayerBodyPart part)
        {
            switch (part)
            {
                case PlayerBodyPart.Head:
                case PlayerBodyPart.Neck:
                    return BodyPart.Head;
                case PlayerBodyPart.ArmUpperLf:
                case PlayerBodyPart.ArmLowerLf:
                case PlayerBodyPart.HandLf:
                    return BodyPart.LeftArm;
                case PlayerBodyPart.ArmUpperRt:
                case PlayerBodyPart.ArmLowerRt:
                case PlayerBodyPart.HandRt:
                    return BodyPart.RightArm;
                case PlayerBodyPart.LegUpperLf:
                case PlayerBodyPart.LegLowerLf:
                case PlayerBodyPart.FootLf:
                    return BodyPart.LeftLeg;
                case PlayerBodyPart.LegUpperRt:
                case PlayerBodyPart.LegLowerRt:
                case PlayerBodyPart.FootRt:
                    return BodyPart.RightLeg;
                case PlayerBodyPart.Chest:
                case PlayerBodyPart.Spine:
                case PlayerBodyPart.Pelvis:
                default:
                    return BodyPart.Torso;
            }
        }

        public static BodyPart MapEnemyPart(EnemyBodyPart part)
        {
            switch (part)
            {
                case EnemyBodyPart.Head:
                    return BodyPart.Head;
                case EnemyBodyPart.LeftArm:
                    return BodyPart.LeftArm;
                case EnemyBodyPart.RightArm:
                    return BodyPart.RightArm;
                case EnemyBodyPart.LeftLeg:
                    return BodyPart.LeftLeg;
                case EnemyBodyPart.RightLeg:
                    return BodyPart.RightLeg;
                case EnemyBodyPart.Chest:
                case EnemyBodyPart.Pelvis:
                default:
                    return BodyPart.Torso;
            }
        }

        public static BodyPart MapEnemyCollisionPart(EnemyCollisionBodyPart part)
        {
            switch (part)
            {
                case EnemyCollisionBodyPart.Head:
                    return BodyPart.Head;
                case EnemyCollisionBodyPart.LeftArm:
                    return BodyPart.LeftArm;
                case EnemyCollisionBodyPart.RightArm:
                    return BodyPart.RightArm;
                case EnemyCollisionBodyPart.LeftLeg:
                    return BodyPart.LeftLeg;
                case EnemyCollisionBodyPart.RightLeg:
                    return BodyPart.RightLeg;
                case EnemyCollisionBodyPart.Chest:
                case EnemyCollisionBodyPart.Pelvis:
                default:
                    return BodyPart.Torso;
            }
        }

        private static DamageInfo FromAttack(Attack attack, BodyPart part, float ownerScale, DamageContextFlags initialContext = DamageContextFlags.None)
        {
            float damage = 0f;
            int sourceId = 0;
            int order = 0;
            Vector3 origin = Vector3.zero;
            Vector3 direction = Vector3.zero;
            AttackType attackType = AttackType.None;
            Collider? sourceCollider = null;

            if (attack != null)
            {
                damage = SanitizeFloat(Math.Max(0f, attack.damage) * ownerScale);
                origin = SanitizeVector(attack.origin, Vector3.zero);
                direction = SanitizeVector(attack.direction, Vector3.zero);
                order = attack.OrderInPool;
                attackType = attack.attackType;
                sourceCollider = attack.collider;
                if (sourceCollider != null)
                    sourceId = sourceCollider.GetInstanceID();
            }

            AdvancedDamageType advancedType = InferAdvancedType(attackType, sourceCollider, damage);
            DamageProfile profile = GetProfile(advancedType);
            damage = NormalizeWeaponDamage(damage, advancedType, sourceCollider);
            float limbMultiplier = GetBodyPartDamageMultiplier(part, advancedType);
            float balanceMultiplier = GetWeaponBalanceMultiplier(advancedType, part);
            float finalDamage = StabilizeAttackDamage(damage * limbMultiplier * balanceMultiplier, advancedType, part);
            DamageContextFlags context = initialContext;
            if (advancedType == AdvancedDamageType.Bullet && LooksHighCaliber(sourceCollider, damage, finalDamage))
                context |= DamageContextFlags.HighCaliber;
            if (part == BodyPart.Head && advancedType == AdvancedDamageType.Explosion && finalDamage >= 72f)
                context |= DamageContextFlags.FatalHeadshot;
            float fractureChance = Config.Clamp((profile.FractureBaseChance + finalDamage * GetFractureDamageCoefficient(advancedType)) * Config.FractureSeverity, 0f, 0.95f);
            float pain = finalDamage * profile.PainFactor;
            float unconsciousness = finalDamage * profile.UnconsciousnessFactor;

            return new DamageInfo(
                part,
                advancedType,
                finalDamage,
                profile.BleedFactor * GetWeaponBleedBalanceMultiplier(advancedType, part),
                fractureChance,
                pain,
                unconsciousness,
                origin,
                direction,
                sourceId,
                order,
                Time.time,
                sourceCollider,
                context);
        }

        public static bool IsFatalPlayerHeadshot(DamageInfo info)
        {
            if (info.BodyPart != BodyPart.Head)
                return false;
            if (info.IsNeckHit)
                return false;
            if (info.IsFatalHeadshot)
                return true;
            if (info.DamageType == AdvancedDamageType.Bullet)
                return info.IsSelfInflicted || info.IsHighCaliber || info.Damage >= 74f;
            if (info.DamageType == AdvancedDamageType.Explosion)
                return info.Damage >= 72f || info.IsHighEnergyImpact;

            return false;
        }

        private static AdvancedDamageType InferAdvancedType(AttackType attackType, Collider? sourceCollider, float damage)
        {
            if (sourceCollider != null && SourceLooksExplosive(sourceCollider))
                return AdvancedDamageType.Explosion;

            if (attackType == AttackType.Stabbing || attackType == AttackType.Slicing)
                return AdvancedDamageType.Stab;

            if (attackType == AttackType.Blunt || attackType == AttackType.None)
            {
                if (damage >= 80f && sourceCollider != null && SourceNameContains(sourceCollider, "projectile"))
                    return AdvancedDamageType.Explosion;
                return AdvancedDamageType.Blunt;
            }

            if (attackType == AttackType.Piercing)
                return AdvancedDamageType.Bullet;

            if (attackType == AttackType.Fire)
                return AdvancedDamageType.Explosion;

            return AdvancedDamageType.Blunt;
        }

        internal static DamageProfile GetProfile(AdvancedDamageType type)
        {
            switch (type)
            {
                case AdvancedDamageType.Bullet:
                    return new DamageProfile(1.28f, 0.08f, 1.02f, 0.0046f);
                case AdvancedDamageType.Blunt:
                    return new DamageProfile(0.014f, 0.030f, 0.38f, 0.00045f);
                case AdvancedDamageType.Explosion:
                    return new DamageProfile(0.70f, 0.34f, 1.35f, 0.0105f);
                case AdvancedDamageType.Stab:
                    return new DamageProfile(1.85f, 0.05f, 1.12f, 0.0034f);
                case AdvancedDamageType.Fall:
                    return new DamageProfile(0.016f, 0.032f, 0.52f, 0.00095f);
                default:
                    return new DamageProfile(0.2f, 0.05f, 1.0f, 0.0030f);
            }
        }

        internal static float GetFractureDamageCoefficient(AdvancedDamageType type)
        {
            switch (type)
            {
                case AdvancedDamageType.Bullet:
                    return 0.0025f;
                case AdvancedDamageType.Blunt:
                    return 0.0018f;
                case AdvancedDamageType.Explosion:
                    return 0.0085f;
                case AdvancedDamageType.Stab:
                    return 0.0015f;
                case AdvancedDamageType.Fall:
                    return 0.0028f;
                default:
                    return 0.0030f;
            }
        }

        internal static float GetBodyPartDamageMultiplier(BodyPart part, AdvancedDamageType type)
        {
            float partMult;
            switch (part)
            {
                case BodyPart.Head:
                    partMult = type == AdvancedDamageType.Bullet ? 1.65f :
                        type == AdvancedDamageType.Stab ? 1.38f :
                        type == AdvancedDamageType.Explosion ? 1.45f :
                        type == AdvancedDamageType.Fall ? 1.12f : 1.08f;
                    break;
                case BodyPart.Torso:
                    partMult = type == AdvancedDamageType.Fall ? 1.02f : type == AdvancedDamageType.Blunt ? 0.82f : 1.0f;
                    break;
                case BodyPart.LeftLeg:
                case BodyPart.RightLeg:
                    partMult = type == AdvancedDamageType.Fall ? 1.05f : 0.86f;
                    break;
                default:
                    partMult = type == AdvancedDamageType.Fall ? 0.74f : 0.70f;
                    break;
            }

            if (type == AdvancedDamageType.Explosion && part != BodyPart.Head)
                partMult += 0.15f;

            return partMult;
        }

        private static float GetWeaponBalanceMultiplier(AdvancedDamageType type, BodyPart part)
        {
            if (type != AdvancedDamageType.Bullet && type != AdvancedDamageType.Explosion)
                return 1f;

            float global = type == AdvancedDamageType.Bullet
                ? Config.WeaponDamageMultiplier
                : Mathf.Lerp(1f, Config.WeaponDamageMultiplier, 0.55f);

            float partBias;
            switch (part)
            {
                case BodyPart.Head:
                    partBias = type == AdvancedDamageType.Bullet ? 1.06f : 1.04f;
                    break;
                case BodyPart.Torso:
                    partBias = 1.04f;
                    break;
                case BodyPart.LeftLeg:
                case BodyPart.RightLeg:
                    partBias = 1.03f;
                    break;
                default:
                    partBias = 1.02f;
                    break;
            }

            return Config.Clamp(global * partBias, 0.75f, 1.95f);
        }

        private static float GetWeaponBleedBalanceMultiplier(AdvancedDamageType type, BodyPart part)
        {
            if (type != AdvancedDamageType.Bullet)
                return 1f;

            float baseMult = Mathf.Lerp(1f, Config.WeaponDamageMultiplier, 0.45f);
            if (part == BodyPart.Torso || part == BodyPart.Head)
                baseMult += 0.04f;
            else if (part == BodyPart.LeftLeg || part == BodyPart.RightLeg)
                baseMult += 0.03f;

            return Config.Clamp(baseMult, 0.85f, 1.55f);
        }

        private static float NormalizeWeaponDamage(float damage, AdvancedDamageType type, Collider? sourceCollider)
        {
            if (damage <= 0f)
                return 0f;

            if (type == AdvancedDamageType.Bullet)
            {
                float normalized = damage;
                if (damage < 4f)
                    normalized = 14f + damage * 18f;
                else if (damage < 12f)
                    normalized = 30f + (damage - 4f) * 3.2f;

                if (sourceCollider != null)
                {
                    if (SourceNameContains(sourceCollider, "shotgun") || SourceNameContains(sourceCollider, "slug"))
                        normalized *= 1.18f;
                    else if (SourceNameContains(sourceCollider, "rifle") || SourceNameContains(sourceCollider, "762") || SourceNameContains(sourceCollider, "308"))
                        normalized *= 1.12f;
                    else if (SourceNameContains(sourceCollider, "pistol") || SourceNameContains(sourceCollider, "handgun"))
                        normalized *= 0.92f;
                }

                return Config.Clamp(normalized, 18f, 92f);
            }

            if (type == AdvancedDamageType.Explosion && damage < 18f)
                return Config.Clamp(18f + damage * 2.4f, 18f, 120f);

            return damage;
        }

        internal static float GetFallDamageThreshold(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head:
                    return 8.25f;
                case BodyPart.LeftLeg:
                case BodyPart.RightLeg:
                    return 8.85f;
                case BodyPart.LeftArm:
                case BodyPart.RightArm:
                    return 8.65f;
                case BodyPart.Torso:
                default:
                    return 8.45f;
            }
        }

        internal static float StabilizeAttackDamage(float damage, AdvancedDamageType type, BodyPart part)
        {
            if (float.IsNaN(damage) || float.IsInfinity(damage) || damage <= 0f)
                return 0f;

            switch (type)
            {
                case AdvancedDamageType.Blunt:
                    {
                        float scaled = damage < 18f ? damage * 0.14f : damage < 45f ? damage * 0.28f : damage * 0.44f;
                        return Math.Min(scaled, part == BodyPart.Head ? 34f : part == BodyPart.Torso ? 30f : 24f);
                    }
                case AdvancedDamageType.Fall:
                    {
                        float scaled = damage < 20f ? damage * 0.30f : damage < 65f ? damage * 0.56f : damage * 0.80f;
                        return Math.Min(scaled, part == BodyPart.Head ? 78f : part == BodyPart.Torso ? 76f : 66f);
                    }
                case AdvancedDamageType.Explosion:
                    return Math.Min(damage * 0.88f, part == BodyPart.Head ? 120f : 150f);
                default:
                    return damage;
            }
        }

        private static bool SourceLooksExplosive(Collider sourceCollider)
        {
            for (int i = 0; i < ExplosionNames.Length; i++)
            {
                if (SourceNameContains(sourceCollider, ExplosionNames[i]))
                    return true;
            }

            return false;
        }

        private static bool LooksHighCaliber(Collider? sourceCollider, float rawDamage, float finalDamage)
        {
            if (rawDamage >= 55f || finalDamage >= 68f)
                return true;
            if (sourceCollider == null)
                return false;

            return SourceNameContains(sourceCollider, "rifle") ||
                   SourceNameContains(sourceCollider, "sniper") ||
                   SourceNameContains(sourceCollider, "magnum") ||
                   SourceNameContains(sourceCollider, "revolver") ||
                   SourceNameContains(sourceCollider, "shotgun") ||
                   SourceNameContains(sourceCollider, "slug") ||
                   SourceNameContains(sourceCollider, "762") ||
                   SourceNameContains(sourceCollider, "308") ||
                   SourceNameContains(sourceCollider, "50cal") ||
                   SourceNameContains(sourceCollider, "heavy");
        }

        internal static float SanitizeFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        internal static Vector3 SanitizeVector(Vector3 value, Vector3 fallback)
        {
            if (float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z))
                return fallback;
            return value;
        }

        private static bool SourceNameContains(Collider sourceCollider, string needle)
        {
            Transform? current = sourceCollider.transform;
            for (int i = 0; i < 4 && current != null; i++)
            {
                string name = current.name;
                if (!string.IsNullOrEmpty(name) && name.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                current = current.parent;
            }

            return false;
        }
    }
}
