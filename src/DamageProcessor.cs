using System;
using Il2CppSLZ.Marrow.Combat;
using Il2CppSLZ.Marrow.Data;
using UnityEngine;
using PlayerBodyPart = Il2CppSLZ.Marrow.PlayerDamageReceiver.BodyPart;
using EnemyBodyPart = Il2CppSLZ.Bonelab.EnemyDamageReceiver.BodyPart;
using EnemyCollisionBodyPart = Il2CppSLZ.Bonelab.EnemyCollisonRelay.BodyPart;

namespace BonelabAdvancedHealth
{
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
            float time)
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
            return FromAttack(attack, MapPlayerPart(part), Config.PlayerDamageScale);
        }

        public static DamageInfo FromNpcAttack(Attack attack, EnemyBodyPart part)
        {
            return FromAttack(attack, MapEnemyPart(part), Config.NpcDamageScale);
        }

        public static DamageInfo FromPlayerCollision(Collision collision, PlayerBodyPart part)
        {
            return FromCollision(collision, MapPlayerPart(part), Config.PlayerDamageScale);
        }

        public static DamageInfo FromNpcCollision(Collision collision, EnemyCollisionBodyPart part)
        {
            return FromCollision(collision, MapEnemyCollisionPart(part), Config.NpcDamageScale);
        }

        public static BleedSeverity GetBleedSeverity(DamageInfo info, LimbHealth limb)
        {
            if (info.BleedFactor <= 0f || info.Damage < 3f)
                return BleedSeverity.None;

            float score = info.Damage * info.BleedFactor * limb.BleedingMultiplier;
            if (info.BodyPart == BodyPart.Head)
                score *= 1.25f;
            if (info.DamageType == AdvancedDamageType.Explosion)
                score *= 0.75f;

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

        private static DamageInfo FromAttack(Attack attack, BodyPart part, float ownerScale)
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
                damage = Math.Max(0f, attack.damage) * ownerScale;
                origin = attack.origin;
                direction = attack.direction;
                order = attack.OrderInPool;
                attackType = attack.attackType;
                sourceCollider = attack.collider;
                if (sourceCollider != null)
                    sourceId = sourceCollider.GetInstanceID();
            }

            AdvancedDamageType advancedType = InferAdvancedType(attackType, sourceCollider, damage);
            DamageProfile profile = GetProfile(advancedType);
            float limbMultiplier = GetBodyPartDamageMultiplier(part, advancedType);
            float finalDamage = damage * limbMultiplier;
            float fractureChance = Config.Clamp(profile.FractureBaseChance + finalDamage * GetFractureDamageCoefficient(advancedType), 0f, 0.95f);
            float pain = finalDamage * profile.PainFactor;
            float unconsciousness = finalDamage * profile.UnconsciousnessFactor;

            return new DamageInfo(
                part,
                advancedType,
                finalDamage,
                profile.BleedFactor,
                fractureChance,
                pain,
                unconsciousness,
                origin,
                direction,
                sourceId,
                order,
                Time.time);
        }

        private static DamageInfo FromCollision(Collision collision, BodyPart part, float ownerScale)
        {
            float velocity = 0f;
            Vector3 origin = Vector3.zero;
            Vector3 direction = Vector3.down;
            int sourceId = 0;

            if (collision != null)
            {
                Vector3 relativeVelocity = collision.relativeVelocity;
                velocity = relativeVelocity.magnitude;
                direction = relativeVelocity.sqrMagnitude > 0.0001f ? relativeVelocity.normalized : Vector3.down;
                Rigidbody rb = collision.rigidbody;
                if (rb != null)
                    sourceId = rb.GetInstanceID();
                ContactPoint contact = collision.GetContact(0);
                origin = contact.point;
            }

            float damage = Math.Max(0f, (velocity - 4.5f) * 8.0f) * ownerScale;
            DamageProfile profile = GetProfile(AdvancedDamageType.Fall);
            float limbMultiplier = GetBodyPartDamageMultiplier(part, AdvancedDamageType.Fall);
            float finalDamage = damage * limbMultiplier;
            float fractureChance = Config.Clamp(profile.FractureBaseChance + finalDamage * GetFractureDamageCoefficient(AdvancedDamageType.Fall), 0f, 0.98f);

            return new DamageInfo(
                part,
                AdvancedDamageType.Fall,
                finalDamage,
                profile.BleedFactor,
                fractureChance,
                finalDamage * profile.PainFactor,
                finalDamage * profile.UnconsciousnessFactor,
                origin,
                direction,
                sourceId,
                0,
                Time.time);
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

        private static DamageProfile GetProfile(AdvancedDamageType type)
        {
            switch (type)
            {
                case AdvancedDamageType.Bullet:
                    return new DamageProfile(1.35f, 0.08f, 1.05f, 0.0065f);
                case AdvancedDamageType.Blunt:
                    return new DamageProfile(0.08f, 0.22f, 1.22f, 0.0100f);
                case AdvancedDamageType.Explosion:
                    return new DamageProfile(0.75f, 0.38f, 1.65f, 0.0175f);
                case AdvancedDamageType.Stab:
                    return new DamageProfile(1.85f, 0.05f, 1.15f, 0.0040f);
                case AdvancedDamageType.Fall:
                    return new DamageProfile(0.04f, 0.34f, 1.30f, 0.0120f);
                default:
                    return new DamageProfile(0.2f, 0.05f, 1.0f, 0.0030f);
            }
        }

        private static float GetFractureDamageCoefficient(AdvancedDamageType type)
        {
            switch (type)
            {
                case AdvancedDamageType.Bullet:
                    return 0.0025f;
                case AdvancedDamageType.Blunt:
                    return 0.0075f;
                case AdvancedDamageType.Explosion:
                    return 0.0085f;
                case AdvancedDamageType.Stab:
                    return 0.0015f;
                case AdvancedDamageType.Fall:
                    return 0.0090f;
                default:
                    return 0.0030f;
            }
        }

        private static float GetBodyPartDamageMultiplier(BodyPart part, AdvancedDamageType type)
        {
            float partMult;
            switch (part)
            {
                case BodyPart.Head:
                    partMult = 1.85f;
                    break;
                case BodyPart.Torso:
                    partMult = 1.1f;
                    break;
                case BodyPart.LeftLeg:
                case BodyPart.RightLeg:
                    partMult = type == AdvancedDamageType.Fall ? 1.35f : 0.95f;
                    break;
                default:
                    partMult = 0.85f;
                    break;
            }

            if (type == AdvancedDamageType.Explosion && part != BodyPart.Head)
                partMult += 0.15f;

            return partMult;
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
