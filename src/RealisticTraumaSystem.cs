namespace BonelabAdvancedHealth
{
    public sealed class RealisticTraumaSystem
    {
        private readonly HealthManager _manager;
        private float _internalShock;
        private float _recoveryAccumulator;

        public float InternalShock => _internalShock;

        public RealisticTraumaSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            _internalShock = 0f;
            _recoveryAccumulator = 0f;
        }

        public void OnDamage(DamageInfo info, OrganDamageFeedback organFeedback)
        {
            float trauma = info.Damage * 0.0032f + info.Pain * 0.0019f + info.UnconsciousnessImpulse * 0.18f;
            if (info.IsSelfInflicted)
                trauma *= info.DamageType == AdvancedDamageType.Bullet ? (info.BodyPart == BodyPart.Head || info.BodyPart == BodyPart.Torso ? 1.95f : 0.78f) : 1.35f;
            if (info.IsHighEnergyImpact)
                trauma *= 1.45f;
            if (organFeedback.InstantCollapse)
                trauma += 0.45f;
            if (organFeedback.BleedSeverity >= BleedSeverity.Severe)
                trauma += 0.18f;

            _internalShock = Config.Clamp(_internalShock + trauma, 0f, 2.5f);
            ApplyImmediateReactions(info, organFeedback);
        }

        public void Update(float deltaTime)
        {
            _recoveryAccumulator += deltaTime;
            if (_recoveryAccumulator < 0.5f)
                return;

            float elapsed = _recoveryAccumulator;
            _recoveryAccumulator = 0f;
            _internalShock = Config.Clamp(_internalShock - elapsed * 0.035f, 0f, 2.5f);
            if (_internalShock > 1.15f)
                _manager.AddPain(_internalShock * 0.35f);
        }

        private void ApplyImmediateReactions(DamageInfo info, OrganDamageFeedback organFeedback)
        {
            if (info.IsSelfInflicted && info.DamageType == AdvancedDamageType.Bullet)
            {
                bool vitalHit = info.BodyPart == BodyPart.Head || info.BodyPart == BodyPart.Torso || (info.IsHighCaliber && info.Damage >= 48f);
                if (vitalHit && (_manager.Kind != HealthOwnerKind.Player || TraumaStartupGuard.CanRunPlayerTrauma))
                    MainMod.Runtime?.SetPlayerRagdoll(true);
                if (info.BodyPart == BodyPart.Head)
                    _manager.Consciousness.SetUnconscious(18f);
                else if (info.BodyPart == BodyPart.Torso)
                    _manager.Consciousness.SetUnconscious(10f);
            }

            if (info.IsSelfInflicted && info.DamageType == AdvancedDamageType.Stab)
            {
                float seconds = info.BodyPart == BodyPart.Torso ? 4.5f : 2.0f;
                if (_internalShock > 1.25f)
                    _manager.Consciousness.SetUnconscious(seconds);
            }

            if (info.DamageType == AdvancedDamageType.Fall && info.IsHighEnergyImpact)
            {
                _manager.AddPain(18f + Config.Clamp(info.ImpactVelocity - 9f, 0f, 10f) * 4f);
                if (info.ImpactVelocity >= 13f || info.BodyPart == BodyPart.Head || organFeedback.InstantCollapse)
                    _manager.Consciousness.SetUnconscious(5f + Config.Clamp(info.ImpactVelocity - 10f, 0f, 8f));
            }
        }
    }
}
