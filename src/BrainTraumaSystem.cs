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
        private float _ringing;
        private float _balanceLoss;
        private bool _delayedCollapseArmed;

        public float Concussion => _concussion;
        public float Dizziness => _dizziness;
        public float RecoverySeconds => _recoverySeconds;
        public float BalanceLoss => _balanceLoss;
        public float DisorientationNormalized => Config.Clamp((_concussion * 0.58f) + (_dizziness * 0.42f) + (_recoverySeconds / 30f * 0.30f) + _balanceLoss * 0.24f, 0f, 1f);
        public float RingingIntensity => Config.Clamp(_ringing + _concussion * 0.35f + _dizziness * 0.22f, 0f, 1f);
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
            _ringing = 0f;
            _balanceLoss = 0f;
            _delayedCollapseArmed = false;
        }

        public void ApplyDamage(DamageInfo info, OrganDamageFeedback organFeedback)
        {
            if (info.BodyPart != BodyPart.Head && !organFeedback.BrainTrauma)
                return;

            HeadTraumaFeedback feedback = HeadTraumaSystem.Evaluate(_manager, info, organFeedback, _concussion);
            _concussion = Config.Clamp(_concussion + feedback.Concussion, 0f, 1f);
            _dizziness = Config.Clamp(_dizziness + feedback.Dizziness, 0f, 1f);
            _ringing = Config.Clamp(Math.Max(_ringing, feedback.Ringing), 0f, 1f);
            _balanceLoss = Config.Clamp(_balanceLoss + feedback.Dizziness * 0.45f, 0f, 1f);
            _recoverySeconds = Math.Max(_recoverySeconds, feedback.RecoverySeconds);

            if (feedback.InstantKnockout)
                _manager.Consciousness.SetUnconscious(4f + info.Damage * 0.055f);
            else if (feedback.ArmDelayedCollapse)
            {
                _delayedCollapseArmed = true;
                _delayedCollapseSeconds = Math.Max(_delayedCollapseSeconds, feedback.DelayedCollapseSeconds);
            }
        }

        public void Update(float deltaTime)
        {
            if (_manager.IsDead)
                return;

            if (_delayedCollapseArmed)
            {
                _delayedCollapseSeconds -= deltaTime;
                _manager.AddPain(deltaTime * 2.6f);
                _balanceLoss = Config.Clamp(_balanceLoss + deltaTime * 0.22f, 0f, 1f);
                if (_delayedCollapseSeconds <= 0f)
                {
                    _delayedCollapseArmed = false;
                    if (_concussion > 0.45f || _dizziness > 0.60f)
                        _manager.Consciousness.SetUnconscious(3.5f + _concussion * 6f);
                }
            }

            if (_recoverySeconds > 0f)
            {
                _recoverySeconds = Math.Max(0f, _recoverySeconds - deltaTime);
                _dizziness = Config.Clamp(_dizziness + deltaTime * 0.012f, 0f, 1f);
            }

            _concussion = Math.Max(0f, _concussion - deltaTime * 0.010f);
            _dizziness = Math.Max(0f, _dizziness - deltaTime * 0.018f);
            _ringing = Math.Max(0f, _ringing - deltaTime * 0.055f);
            _balanceLoss = Math.Max(0f, _balanceLoss - deltaTime * 0.030f);
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
