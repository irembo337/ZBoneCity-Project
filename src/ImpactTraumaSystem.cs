using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public static class ImpactTraumaSystem
    {
        public static DamageInfo FromCollision(Collision collision, BodyPart part, float ownerScale)
        {
            float velocity = 0f;
            Vector3 origin = Vector3.zero;
            Vector3 direction = Vector3.down;
            int sourceId = 0;
            Collider? collider = null;
            float surfaceHardness = 1f;
            float massScale = 1f;

            if (collision != null)
            {
                collider = collision.collider;
                Vector3 relativeVelocity = DamageProcessor.SanitizeVector(collision.relativeVelocity, Vector3.zero);
                velocity = DamageProcessor.SanitizeFloat(relativeVelocity.magnitude);
                direction = relativeVelocity.sqrMagnitude > 0.0001f ? relativeVelocity.normalized : Vector3.down;
                Rigidbody rb = collision.rigidbody;
                if (rb != null)
                {
                    sourceId = rb.GetInstanceID();
                    massScale = Config.Clamp(Mathf.Sqrt(Mathf.Max(1f, rb.mass) / 75f), 0.85f, 1.28f);
                }
                else if (collider != null)
                {
                    sourceId = collider.GetInstanceID();
                }

                if (collision.contactCount > 0)
                {
                    ContactPoint contact = collision.GetContact(0);
                    origin = DamageProcessor.SanitizeVector(contact.point, Vector3.zero);
                }

                surfaceHardness = EstimateSurfaceHardness(collider);
            }

            float threshold = DamageProcessor.GetFallDamageThreshold(part) + GetPlayerCollisionGrace(part);
            if (velocity < threshold + 0.75f)
            {
                DamageProfile safeProfile = DamageProcessor.GetProfile(AdvancedDamageType.Fall);
                return new DamageInfo(
                    part,
                    AdvancedDamageType.Fall,
                    0f,
                    safeProfile.BleedFactor,
                    0f,
                    0f,
                    0f,
                    origin,
                    direction,
                    sourceId,
                    0,
                    Time.time,
                    collider,
                    DamageContextFlags.None,
                    velocity);
            }

            float excess = Math.Max(0f, velocity - threshold);
            float impactEnergy = excess <= 0f ? 0f : excess * excess * 0.70f + excess * 0.45f;
            float highVelocityBonus = velocity > 15f ? (velocity - 15f) * (velocity - 15f) * 0.16f : 0f;
            float severity = Config.Clamp((velocity - (threshold + 1.35f)) / 8.0f, 0f, 1f);
            float environmentMultiplier = Mathf.Lerp(1f, Config.EnvironmentDamageMultiplier, severity);
            float rawDamage = (impactEnergy + highVelocityBonus) * surfaceHardness * massScale * ownerScale * environmentMultiplier;
            float finalDamage = DamageProcessor.StabilizeAttackDamage(rawDamage * DamageProcessor.GetBodyPartDamageMultiplier(part, AdvancedDamageType.Fall), AdvancedDamageType.Fall, part);
            DamageProfile profile = DamageProcessor.GetProfile(AdvancedDamageType.Fall);
            DamageContextFlags flags = DamageContextFlags.None;
            if (velocity >= 15.5f || finalDamage >= 78f)
                flags |= DamageContextFlags.HighEnergyImpact;
            if (surfaceHardness >= 1.12f)
                flags |= DamageContextFlags.HardSurfaceImpact;

            float fractureChance = Config.Clamp(
                (profile.FractureBaseChance + finalDamage * DamageProcessor.GetFractureDamageCoefficient(AdvancedDamageType.Fall) + Math.Max(0f, velocity - 11.5f) * 0.011f) * Config.FractureSeverity,
                0f,
                0.74f);
            if (MedicalInspectionSystem.IsLeg(part))
                fractureChance = Math.Max(fractureChance, Config.Clamp((velocity - 10.8f) * 0.055f, 0f, 0.62f));
            if (flags.HasFlag(DamageContextFlags.HighEnergyImpact) && part == BodyPart.Torso)
                fractureChance = Math.Max(fractureChance, 0.32f);

            return new DamageInfo(
                part,
                AdvancedDamageType.Fall,
                finalDamage,
                profile.BleedFactor * (flags.HasFlag(DamageContextFlags.HighEnergyImpact) ? 1.75f : 1f),
                fractureChance,
                finalDamage * profile.PainFactor * (flags.HasFlag(DamageContextFlags.HighEnergyImpact) ? 1.25f : 1f),
                finalDamage * profile.UnconsciousnessFactor + Math.Max(0f, velocity - 14f) * 0.018f,
                origin,
                direction,
                sourceId,
                0,
                Time.time,
                collider,
                flags,
                velocity);
        }

        private static float GetPlayerCollisionGrace(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.LeftArm:
                case BodyPart.RightArm:
                    return 2.1f;
                case BodyPart.LeftLeg:
                case BodyPart.RightLeg:
                    return 1.2f;
                case BodyPart.Head:
                    return 0.8f;
                case BodyPart.Torso:
                default:
                    return 1.0f;
            }
        }

        private static float EstimateSurfaceHardness(Collider? collider)
        {
            if (collider == null)
                return 1f;

            PhysicMaterial material = collider.sharedMaterial;
            if (material == null)
                return 1.08f;

            float friction = Math.Max(material.dynamicFriction, material.staticFriction);
            float hardness = 1.0f + Config.Clamp(friction * 0.18f - material.bounciness * 0.35f, -0.22f, 0.32f);
            return Config.Clamp(hardness, 0.78f, 1.34f);
        }
    }
}
