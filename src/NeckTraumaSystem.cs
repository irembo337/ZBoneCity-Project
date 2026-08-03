using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public enum NeckInjuryState
    {
        Healthy = 0,
        Sprained = 1,
        Fractured = 2,
        Critical = 3
    }

    public enum NeckTraumaCause
    {
        Impact = 0,
        HighEnergyFall = 1,
        HighCaliberShot = 2,
        ForcedRotation = 3
    }

    public sealed class NeckTraumaSystem
    {
        private readonly HealthManager _manager;
        private float _trauma;
        private float _instabilitySeconds;
        private float _breathingStressSeconds;
        private bool _collapseApplied;
        private bool _neckSnapSoundPlayed;

        public NeckInjuryState State { get; private set; }
        public float TraumaNormalized => Config.Clamp(_trauma, 0f, 1f);
        public float CameraInstability => State == NeckInjuryState.Healthy ? 0f : Config.Clamp(_trauma * 0.55f + _instabilitySeconds * 0.025f, 0f, 1f);
        public float MovementPenalty => State == NeckInjuryState.Healthy ? 0f : State == NeckInjuryState.Sprained ? 0.18f : State == NeckInjuryState.Fractured ? 0.46f : 0.72f;
        public float HeadControlPenalty => State == NeckInjuryState.Healthy ? 0f : State == NeckInjuryState.Sprained ? 0.25f : State == NeckInjuryState.Fractured ? 0.62f : 0.92f;
        public float BreathingStress => _breathingStressSeconds > 0f ? Config.Clamp(_breathingStressSeconds / 12f, 0f, 1f) : 0f;
        public bool HasInjury => State != NeckInjuryState.Healthy;

        public NeckTraumaSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            _trauma = 0f;
            _instabilitySeconds = 0f;
            _breathingStressSeconds = 0f;
            _collapseApplied = false;
            _neckSnapSoundPlayed = false;
            State = NeckInjuryState.Healthy;
        }

        public void ApplyDamage(DamageInfo info, OrganDamageFeedback feedback)
        {
            if (_manager.IsDead || !Config.NeckTraumaEnabled)
                return;

            float score = GetTraumaScore(info, feedback, out NeckTraumaCause cause);
            if (score < 0.52f)
                return;

            NeckInjuryState next = score >= 1.12f ? NeckInjuryState.Critical :
                                   score >= 0.82f ? NeckInjuryState.Fractured :
                                   NeckInjuryState.Sprained;
            ApplyState(next, score, cause, false);
        }

        public void ApplyForcedSnap(bool lethalNpc)
        {
            if (_manager.IsDead || !Config.NeckTraumaEnabled || !Config.NeckSnapEnabled)
                return;

            ApplyState(NeckInjuryState.Critical, 1.35f, NeckTraumaCause.ForcedRotation, lethalNpc);
        }

        public void RegisterTreatment(MedicalItemType type)
        {
            if (State == NeckInjuryState.Healthy)
                return;

            if (type == MedicalItemType.Morphine || type == MedicalItemType.Painkillers)
            {
                _instabilitySeconds = Math.Max(0f, _instabilitySeconds - 4f);
                _breathingStressSeconds = Math.Max(0f, _breathingStressSeconds - 3f);
                return;
            }

            if (type != MedicalItemType.Medkit)
                return;

            _trauma = Math.Max(0f, _trauma - 0.18f);
            if (State == NeckInjuryState.Critical && _trauma < 0.88f)
                State = NeckInjuryState.Fractured;
            else if (State == NeckInjuryState.Fractured && _trauma < 0.58f)
                State = NeckInjuryState.Sprained;
            else if (State == NeckInjuryState.Sprained && _trauma < 0.28f)
                State = NeckInjuryState.Healthy;
        }

        public void Update(float deltaTime)
        {
            if (_manager.IsDead)
            {
                Reset();
                return;
            }

            _instabilitySeconds = Math.Max(0f, _instabilitySeconds - deltaTime);
            _breathingStressSeconds = Math.Max(0f, _breathingStressSeconds - deltaTime);

            if (State == NeckInjuryState.Sprained)
            {
                _trauma = Math.Max(0f, _trauma - deltaTime * 0.006f);
                if (_trauma <= 0.12f)
                    State = NeckInjuryState.Healthy;
            }
            else if (State == NeckInjuryState.Fractured)
            {
                _trauma = Math.Max(0.62f, _trauma - deltaTime * 0.0012f);
            }
        }

        private void ApplyState(NeckInjuryState next, float score, NeckTraumaCause cause, bool lethalNpc)
        {
            if (next < State)
                return;

            State = next;
            _trauma = Config.Clamp(Math.Max(_trauma, score), 0f, 1.45f);
            _instabilitySeconds = Math.Max(_instabilitySeconds, next == NeckInjuryState.Sprained ? 8f : next == NeckInjuryState.Fractured ? 20f : 36f);
            _breathingStressSeconds = Math.Max(_breathingStressSeconds, next == NeckInjuryState.Sprained ? 5f : next == NeckInjuryState.Fractured ? 16f : 28f);

            float pain = next == NeckInjuryState.Sprained ? 18f : next == NeckInjuryState.Fractured ? 48f : 86f;
            _manager.AddPain(pain);
            _manager.Stress.RegisterNearbyGunfire(next == NeckInjuryState.Critical ? 0.32f : 0.16f);
            if (cause == NeckTraumaCause.ForcedRotation)
            {
                if (!_neckSnapSoundPlayed)
                {
                    _neckSnapSoundPlayed = true;
                    MainMod.Runtime?.NeckSnapAudio.PlayAt(GetSoundPosition());
                }
            }
            else
            {
                _manager.AudioTrauma.TriggerNeckCrack(next == NeckInjuryState.Sprained ? 0.45f : next == NeckInjuryState.Fractured ? 0.78f : 1.1f);
            }

            if (!_collapseApplied && next >= NeckInjuryState.Fractured)
            {
                _collapseApplied = true;
                if (_manager.Kind == HealthOwnerKind.Player)
                    MainMod.Runtime?.SetPlayerRagdoll(true);
            }

            if (next == NeckInjuryState.Critical)
            {
                if (_manager.Kind == HealthOwnerKind.Npc && lethalNpc)
                    _manager.CommitDeath(DeathCause.NeckTrauma);
                else
                    _manager.Consciousness.SetUnconscious(cause == NeckTraumaCause.ForcedRotation ? 24f : 14f);
            }

            MainMod.Runtime?.NotifyHudMedicalFeedback("Neck " + GetDisplayState(State));
            MainMod.Runtime?.NotifyFusionNeckTrauma(_manager, State, cause);
        }

        private Vector3 GetSoundPosition()
        {
            if (_manager is NPCHealth npc)
                return npc.GetEffectPosition();

            Transform? head = MainMod.Runtime?.GetHeadTransform();
            return head != null ? head.position : Vector3.zero;
        }

        private static float GetTraumaScore(DamageInfo info, OrganDamageFeedback feedback, out NeckTraumaCause cause)
        {
            cause = NeckTraumaCause.Impact;
            if (info.BodyPart != BodyPart.Head && info.BodyPart != BodyPart.Torso)
                return 0f;

            float score = 0f;
            if (info.DamageType == AdvancedDamageType.Fall)
            {
                if (!info.IsHighEnergyImpact && info.Damage < 74f && info.ImpactVelocity < 13.5f)
                    return 0f;
                cause = NeckTraumaCause.HighEnergyFall;
                score = info.Damage / 95f + Math.Max(0f, info.ImpactVelocity - 10f) * 0.055f;
                if (info.BodyPart == BodyPart.Head)
                    score += 0.18f;
            }
            else if (info.DamageType == AdvancedDamageType.Blunt)
            {
                if (info.Damage < 82f && info.ImpactVelocity < 14f)
                    return 0f;
                score = info.Damage / 110f + Math.Max(0f, info.ImpactVelocity - 12f) * 0.045f;
            }
            else if (info.DamageType == AdvancedDamageType.Explosion)
            {
                if (info.Damage < 70f)
                    return 0f;
                score = info.Damage / 115f + (info.IsHighEnergyImpact ? 0.22f : 0f);
            }
            else if (info.DamageType == AdvancedDamageType.Bullet)
            {
                if (!info.IsHighCaliber && !LooksLikeNeckHit(info))
                    return 0f;
                cause = NeckTraumaCause.HighCaliberShot;
                score = info.Damage / 105f + (info.IsHighCaliber ? 0.26f : 0.08f);
            }
            else if (info.DamageType == AdvancedDamageType.Stab && LooksLikeNeckHit(info))
            {
                score = info.Damage / 120f + 0.18f;
            }

            if (feedback.PrimaryOrgan == OrganType.Brain)
                score += 0.08f;
            return Config.Clamp(score, 0f, 1.45f);
        }

        private static bool LooksLikeNeckHit(DamageInfo info)
        {
            Collider? collider = info.SourceCollider;
            if (collider == null)
                return false;

            string name = collider.name ?? string.Empty;
            Transform? transform = collider.transform;
            for (int i = 0; i < 4 && transform != null; i++)
            {
                name += " " + (transform.name ?? string.Empty);
                transform = transform.parent;
            }

            return name.IndexOf("neck", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("throat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("cervical", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static string GetDisplayState(NeckInjuryState state)
        {
            return state switch
            {
                NeckInjuryState.Sprained => "Sprained",
                NeckInjuryState.Fractured => "Fractured",
                NeckInjuryState.Critical => "Critical",
                _ => "Healthy"
            };
        }
    }
}
