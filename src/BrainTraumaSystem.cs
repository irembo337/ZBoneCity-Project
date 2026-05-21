using System;

namespace BonelabAdvancedHealth
{
    public sealed class BrainTraumaSystem
    {
        private readonly HealthManager _manager;
        private float _concussion;
        private float _dizziness;
        private float _recoverySeconds;
        private float _delayedCollapseSeconds;
        private bool _delayedCollapseArmed;

        public float Concussion => _concussion;
        public float Dizziness => _dizziness;
        public float RecoverySeconds => _recoverySeconds;
        public float DisorientationNormalized => Config.Clamp((_concussion * 0.6f) + (_dizziness * 0.4f) + (_recoverySeconds / 30f * 0.35f), 0f, 1f);
        public float RingingIntensity => Config.Clamp(_concussion * 0.8f + _dizziness * 0.35f, 0f, 1f);
        public bool HasActiveConcussion => _concussion > 0.05f || _recoverySeconds > 0f;

        public BrainTraumaSystem(HealthManager manager)
        {
            _manager = manager;
            _manager.Consciousness.StateChanged += OnConsciousnessChanged;
        }

        public void Reset()
        {
            _concussion = 0f;
            _dizziness = 0f;
            _recoverySeconds = 0f;
            _delayedCollapseSeconds = 0f;
            _delayedCollapseArmed = false;
        }

        public void ApplyDamage(DamageInfo info, OrganDamageFeedback organFeedback)
        {
            if (info.BodyPart != BodyPart.Head && !organFeedback.BrainTrauma)
                return;

            float trauma = info.Damage * 0.012f;
            if (info.DamageType == AdvancedDamageType.Blunt)
                trauma *= 1.45f;
            if (info.DamageType == AdvancedDamageType.Explosion)
                trauma *= 1.2f;

            _concussion = Config.Clamp(_concussion + trauma, 0f, 1f);
            _dizziness = Config.Clamp(_dizziness + trauma * 0.85f, 0f, 1f);

            if (info.DamageType == AdvancedDamageType.Blunt && info.Damage >= 18f)
            {
                if (_manager.Random.NextDouble() < 0.20)
                {
                    _manager.Consciousness.SetUnconscious(5f + info.Damage * 0.08f);
                }
                else
                {
                    _delayedCollapseArmed = true;
                    _delayedCollapseSeconds = Math.Max(_delayedCollapseSeconds, 1.2f + info.Damage * 0.035f);
                }
            }
            else if (info.BodyPart == BodyPart.Head && info.Damage >= 45f)
            {
                _manager.Consciousness.SetUnconscious(4f + trauma * 5f);
            }
        }

        public void Update(float deltaTime)
        {
            if (_manager.IsDead)
                return;

            if (_delayedCollapseArmed)
            {
                _delayedCollapseSeconds -= deltaTime;
                _manager.AddPain(deltaTime * 4f);
                if (_delayedCollapseSeconds <= 0f)
                {
                    _delayedCollapseArmed = false;
                    _manager.Consciousness.SetUnconscious(4f + _concussion * 8f);
                }
            }

            if (_recoverySeconds > 0f)
            {
                _recoverySeconds = Math.Max(0f, _recoverySeconds - deltaTime);
                _dizziness = Config.Clamp(_dizziness + deltaTime * 0.012f, 0f, 1f);
            }

            _concussion = Math.Max(0f, _concussion - deltaTime * 0.010f);
            _dizziness = Math.Max(0f, _dizziness - deltaTime * 0.018f);
        }

        private void OnConsciousnessChanged(ConsciousnessState state)
        {
            if (state == ConsciousnessState.Blackout || state == ConsciousnessState.Awake)
            {
                if (_concussion > 0.18f)
                    _recoverySeconds = Math.Max(_recoverySeconds, 30f);
            }
        }
    }
}
