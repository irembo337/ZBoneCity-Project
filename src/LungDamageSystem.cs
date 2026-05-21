using System;

namespace BonelabAdvancedHealth
{
    public sealed class LungDamageSystem
    {
        private readonly HealthManager _manager;
        private float _collapseSeverity;
        private float _coughTimer;
        private float _panic;

        public float OxygenNormalized { get; private set; } = 1f;
        public float CollapseSeverity => _collapseSeverity;
        public float Panic => _panic;
        public float OxygenStress => 1f - OxygenNormalized;
        public bool HasCollapsedLung => _collapseSeverity > 0.1f;
        public bool IsCoughing { get; private set; }
        public float WhiteNoiseIntensity => Config.Clamp(OxygenStress * 0.8f + _collapseSeverity * 0.55f, 0f, 1f);
        public float BreathingPanic => Config.Clamp(_panic + _collapseSeverity * 0.45f + OxygenStress * 0.55f, 0f, 1f);

        public LungDamageSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            _collapseSeverity = 0f;
            _coughTimer = 0f;
            _panic = 0f;
            OxygenNormalized = 1f;
            IsCoughing = false;
        }

        public void ApplyDamage(DamageInfo info, OrganDamageFeedback organFeedback)
        {
            bool lungHit = organFeedback.LungCollapsed || organFeedback.PrimaryOrgan == OrganType.Lungs;
            if (!lungHit)
                return;

            float collapseGain = info.Damage * 0.012f;
            if (info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Stab)
                collapseGain *= 1.55f;
            if (info.DamageType == AdvancedDamageType.Explosion)
                collapseGain *= 1.25f;

            _collapseSeverity = Config.Clamp(_collapseSeverity + collapseGain, 0f, 1f);
            _panic = Config.Clamp(_panic + 0.22f + collapseGain * 0.35f, 0f, 1f);
            _coughTimer = Math.Max(_coughTimer, 3.0f + collapseGain * 7.0f);
        }

        public void Update(float deltaTime)
        {
            if (_manager.IsDead)
                return;

            if (_collapseSeverity > 0f)
            {
                float oxygenDrain = deltaTime * (0.018f + _collapseSeverity * 0.07f);
                OxygenNormalized = Config.Clamp(OxygenNormalized - oxygenDrain, 0f, 1f);
                _manager.AddPain(deltaTime * (2.2f + _collapseSeverity * 6.0f));
            }
            else
            {
                OxygenNormalized = Config.Clamp(OxygenNormalized + deltaTime * 0.045f, 0f, 1f);
            }

            if (_collapseSeverity > 0f && _manager.Bleeding.BloodNormalized > 0.55f)
                _collapseSeverity = Math.Max(0f, _collapseSeverity - deltaTime * 0.006f);

            _panic = Math.Max(0f, _panic - deltaTime * 0.025f);
            if (_coughTimer > 0f)
            {
                _coughTimer -= deltaTime;
                IsCoughing = true;
            }
            else
            {
                IsCoughing = false;
            }

            if (OxygenNormalized < 0.28f)
                _manager.Consciousness.SetUnconscious(2.5f + (0.28f - OxygenNormalized) * 12f);

            if (OxygenNormalized <= 0.035f)
                _manager.RequestDeath(DeathCause.OxygenLoss);
        }

        public void Treat(float strength)
        {
            if (strength <= 0f)
                return;

            _collapseSeverity = Math.Max(0f, _collapseSeverity - strength);
            _panic = Math.Max(0f, _panic - strength * 0.7f);
            OxygenNormalized = Config.Clamp(OxygenNormalized + strength * 0.35f, 0f, 1f);
            _coughTimer = Math.Max(0f, _coughTimer - strength * 4f);
        }
    }
}
