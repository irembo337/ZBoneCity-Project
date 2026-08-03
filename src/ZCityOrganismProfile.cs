using System;

namespace BonelabAdvancedHealth
{
    public enum ZCityBloodType
    {
        ONegative,
        OPositive,
        ANegative,
        APositive,
        BNegative,
        BPositive,
        ABNegative,
        ABPositive
    }

    public sealed class ZCityOrganismProfile
    {
        private readonly HealthManager _manager;
        private float _tickAccumulator;
        private float _analgesiaAdd;
        private float _adrenalineAdd;
        private float _naloxoneAdd;
        private float _lastHitGrace;

        public ZCityBloodType BloodType { get; private set; }
        public float Consciousness { get; private set; }
        public float Fear { get; private set; }
        public float FearAdd { get; private set; }
        public float Shock { get; private set; }
        public float Pain { get; private set; }
        public float AveragePain { get; private set; }
        public float Analgesia { get; private set; }
        public float Adrenaline { get; private set; }
        public float Naloxone { get; private set; }
        public float HemotransfusionShock { get; private set; }
        public float TemperatureC { get; private set; }
        public float PulseBpm { get; private set; }
        public float HeartbeatBpm { get; private set; }
        public bool HeartStop { get; private set; }
        public bool Critical { get; private set; }
        public bool Incapacitated { get; private set; }
        public bool NeedUnconsciousness { get; private set; }
        public float RecoilMultiplier { get; private set; }
        public float LegStrength { get; private set; }
        public float MeleeSpeed { get; private set; }

        public ZCityOrganismProfile(HealthManager manager)
        {
            _manager = manager;
            Reset();
        }

        public void Reset()
        {
            _tickAccumulator = 0f;
            _analgesiaAdd = 0f;
            _adrenalineAdd = 0f;
            _naloxoneAdd = 0f;
            _lastHitGrace = 0f;
            BloodType = (ZCityBloodType)_manager.Random.Next(0, 8);
            Consciousness = 1f;
            Fear = 0f;
            FearAdd = 0f;
            Shock = 0f;
            Pain = 0f;
            AveragePain = 0f;
            Analgesia = 0f;
            Adrenaline = 0f;
            Naloxone = 0f;
            HemotransfusionShock = 0f;
            TemperatureC = 36.7f;
            PulseBpm = 70f;
            HeartbeatBpm = 70f;
            HeartStop = false;
            Critical = false;
            Incapacitated = false;
            NeedUnconsciousness = false;
            RecoilMultiplier = 1f;
            LegStrength = 1f;
            MeleeSpeed = 1f;
        }

        public void OnDamage(DamageInfo info, OrganDamageFeedback organFeedback)
        {
            if (_manager.IsDead)
                return;

            _lastHitGrace = 1.5f;
            float trauma = Config.Clamp(info.Damage / 85f, 0f, 1.6f);
            float painImpulse = Config.Clamp(info.Pain / 75f, 0f, 1.8f);
            AveragePain = Config.Clamp(AveragePain + info.Pain * 0.16f + trauma * 7f, 0f, 150f);
            Shock = Config.Clamp(Shock + trauma * 14f + painImpulse * 10f + info.UnconsciousnessImpulse * 8f, 0f, 120f);

            if (info.BodyPart == BodyPart.Head)
            {
                FearAdd = Math.Max(FearAdd, 1.5f);
                Consciousness = Math.Min(Consciousness, 1f - Config.Clamp(info.UnconsciousnessImpulse * 0.16f, 0f, 0.65f));
            }
            else if (info.BodyPart == BodyPart.Torso || organFeedback.InstantCollapse)
            {
                FearAdd = Math.Max(FearAdd, 1.2f);
            }

            if (organFeedback.BleedSeverity >= BleedSeverity.Severe || info.BleedFactor > 1.35f)
                FearAdd = Math.Max(FearAdd, 1.0f);

            if (organFeedback.PrimaryOrgan == OrganType.Heart || organFeedback.CardiacArrest)
                HeartStop = true;
        }

        public void RegisterTreatment(MedicalItemType type, BodyPart part)
        {
            switch (type)
            {
                case MedicalItemType.Morphine:
                    _analgesiaAdd = Math.Min(_analgesiaAdd + 1f, 4f);
                    break;
                case MedicalItemType.Adrenaline:
                    _adrenalineAdd = Math.Min(_adrenalineAdd + 4f, 4f);
                    if (HeartStop && _manager.Random.NextDouble() < 0.25)
                        HeartStop = false;
                    break;
                case MedicalItemType.Painkillers:
                    _analgesiaAdd = Math.Min(_analgesiaAdd + 0.30f, 4f);
                    break;
                case MedicalItemType.ETGStimulator:
                    _adrenalineAdd = Math.Min(_adrenalineAdd + 2.25f, 4f);
                    _analgesiaAdd = Math.Min(_analgesiaAdd + 0.42f, 4f);
                    break;
                case MedicalItemType.SJ1Stimulator:
                    _adrenalineAdd = Math.Min(_adrenalineAdd + 2.8f, 4f);
                    _analgesiaAdd = Math.Min(_analgesiaAdd + 0.50f, 4f);
                    break;
                case MedicalItemType.Bandage:
                case MedicalItemType.Tourniquet:
                case MedicalItemType.Medkit:
                case MedicalItemType.BloodPack:
                case MedicalItemType.Splint:
                    FearAdd = Math.Max(0f, FearAdd - 0.18f);
                    Shock = Math.Max(0f, Shock - 3.5f);
                    break;
            }
        }

        public void RegisterBloodRestore(float amountMl, bool compatible = true)
        {
            if (!compatible)
                HemotransfusionShock = Config.Clamp(HemotransfusionShock + amountMl / 500f, 0f, 4f);
            else
                Shock = Math.Max(0f, Shock - amountMl / 120f);
        }

        public void Update(float deltaTime)
        {
            if (_manager.IsDead)
            {
                PulseBpm = 0f;
                HeartbeatBpm = 0f;
                HeartStop = true;
                Critical = true;
                Incapacitated = true;
                return;
            }

            _tickAccumulator += deltaTime;
            if (_tickAccumulator < 0.2f)
                return;

            float elapsed = _tickAccumulator;
            _tickAccumulator = 0f;
            _lastHitGrace = Math.Max(0f, _lastHitGrace - elapsed);

            UpdateMedication(elapsed);
            UpdatePainAndShock(elapsed);
            UpdatePulse(elapsed);
            UpdateBloodRegeneration(elapsed);
            UpdateConsciousness(elapsed);
            UpdateMovementOutputs();
        }

        private void UpdateMedication(float elapsed)
        {
            if (_analgesiaAdd > 0f)
            {
                Analgesia = MoveToward(Analgesia, 4f, elapsed / 15f);
                _analgesiaAdd = MoveToward(_analgesiaAdd, 0f, elapsed / 15f);
            }
            else
            {
                Analgesia = MoveToward(Analgesia, 0f, elapsed / 240f * (Naloxone * 25f + 1f));
            }

            if (_adrenalineAdd > 0f)
                Adrenaline = MoveToward(Adrenaline, 4f, elapsed / 5f);

            _adrenalineAdd = MoveToward(_adrenalineAdd, 0f, _adrenalineAdd < 0f ? elapsed / 30f : elapsed / 5f);
            Adrenaline = MoveToward(Adrenaline, 0f, elapsed / 25f);
            Naloxone = MoveToward(Naloxone, _naloxoneAdd > 0f ? 4f : 0f, _naloxoneAdd > 0f ? elapsed / 30f : elapsed / 60f);
            _naloxoneAdd = MoveToward(_naloxoneAdd, 0f, elapsed / 15f);
            HemotransfusionShock = MoveToward(HemotransfusionShock, 0f, elapsed / 200f);
        }

        private void UpdatePainAndShock(float elapsed)
        {
            float analgesiaMul = Analgesia * 4f + 1f;
            float painkillerMul = _manager.Medication.PainSuppression * 0.5f + 1f;
            float adrenalineMul = Math.Min(Math.Max(1f + Adrenaline, 1f), 1.2f);
            float incomingPain = Config.Clamp(_manager.PainSystem.TotalPain, 0f, 145f);
            float add = Math.Min(elapsed * 20f, Math.Max(0f, incomingPain - AveragePain) * 0.45f);
            float sub = add <= 0.2f
                ? elapsed * 2f * (_manager.Consciousness.State == ConsciousnessState.Unconscious ? 2f : 1f) + elapsed * Analgesia * 4f + elapsed * _manager.Medication.PainSuppression * 3f
                : 0f;

            if (Adrenaline > 0.5f)
            {
                float adrenalMask = Math.Max(1f - Adrenaline, 0.05f) / 1.5f;
                sub *= adrenalMask;
                add *= adrenalMask;
            }

            if (Pain > 60f)
            {
                add /= 5f;
                sub /= Pain > 70f && add > 0.01f ? 20f : 5f;
                FearAdd = Math.Max(FearAdd, 1f);
            }

            AveragePain = Config.Clamp(AveragePain + add, 0f, 150f);
            if (_lastHitGrace <= 0f)
                AveragePain = Math.Max(0f, AveragePain - sub);

            Pain = AveragePain * Math.Max(1f - Adrenaline / 4f, 0.75f) * Math.Max(1f - Analgesia, 0f);
            if (Pain > 80f)
                Shock = MoveToward(Shock, 70f, elapsed * 4f);

            float bloodLoss = 1f - _manager.Bleeding.BloodNormalized;
            float bleedPressure = Config.Clamp(_manager.Bleeding.TotalBleedRateMlPerSecond / 42f, 0f, 1f);
            float targetShock = Config.Clamp(Shock + bloodLoss * 18f + bleedPressure * 10f + HemotransfusionShock * 10f, 0f, 120f);
            Shock = MoveToward(Shock, targetShock, elapsed * (targetShock > Shock ? 4f : 2f));
            Shock = Math.Max(0f, Shock - elapsed * (_lastHitGrace <= 0f ? 2f : 0.6f));
        }

        private void UpdatePulse(float elapsed)
        {
            float heart = HeartStop ? 0.1f : 1f;
            float brain = Config.Clamp(1f - _manager.Brain.DisorientationNormalized * 1.5f, 0f, 1f);
            float oxygen = Config.Clamp(_manager.Lungs.OxygenNormalized, 0f, 1f);
            float bloodFactor = Config.Clamp((_manager.Bleeding.BloodVolumeMl - 1000f) / 4000f, 0f, 1f);
            float temperatureFactor = Config.Clamp(Remap(TemperatureC, 28f, 36.7f, 0.5f, 1f), 0.5f, 1f);
            float targetPulse = 70f * heart * brain * oxygen * bloodFactor * temperatureFactor;
            targetPulse = Config.Clamp(targetPulse, 0f, 200f);
            PulseBpm = MoveToward(PulseBpm, targetPulse, elapsed * (HeartStop ? 10f : 5f));

            float heartbeat = PulseBpm < 70f ? 70f + (70f - PulseBpm) * 4f : PulseBpm;
            heartbeat += 40f * Math.Max(0f, Fear);
            heartbeat += Config.Clamp(Shock, 0f, 40f);
            heartbeat += Config.Clamp(Pain, 40f, 80f) - 40f;
            heartbeat += 40f * Math.Min(Adrenaline, 3f);
            heartbeat -= 40f * Math.Min(Analgesia / 2.5f, 1f);
            heartbeat += _manager.Medication.TachycardiaBpm;
            heartbeat = Config.Clamp(heartbeat, 0f, 300f);
            HeartbeatBpm = MoveToward(HeartbeatBpm, heartbeat, elapsed * (heartbeat > HeartbeatBpm ? 5f : 3f));

            if (HeartbeatBpm > 300f || PulseBpm < 10f || _manager.Brain.DisorientationNormalized >= 0.6f)
                HeartStop = true;
        }

        private void UpdateBloodRegeneration(float elapsed)
        {
            if (HeartStop || PulseBpm <= 5f)
                return;
            if (_manager.Bleeding.TotalBleedRateMlPerSecond >= 0.05f || _manager.TacticalVitals.InternalBleedingNormalized >= 0.5f)
                return;
            if (_manager.Bleeding.BloodVolumeMl >= Config.BloodVolumeMl)
                return;

            float adrenaline = Math.Min(Adrenaline, 2f);
            float regen = elapsed * 5f * (adrenaline * 1.5f + 1f) * Config.Clamp(PulseBpm / 70f, 0.3f, 1.8f);
            _manager.Bleeding.RestoreBlood(regen);
        }

        private void UpdateConsciousness(float elapsed)
        {
            float bloodConsciousness = _manager.Bleeding.BloodVolumeMl < 3000f
                ? Config.Clamp((_manager.Bleeding.BloodVolumeMl - 2500f) / 500f, 0f, 1f)
                : 1f;
            float target = Math.Min(1f, bloodConsciousness);

            if (Shock > 30f * (Analgesia * 4f + 1f))
                target = Math.Min(target, 0.1f);
            if (HeartStop)
                target = 0f;

            Consciousness = MoveToward(Consciousness, target, elapsed / (target < Consciousness ? 5f : 15f));
            NeedUnconsciousness = Consciousness < 0.1f || Shock > 40f * (Analgesia * 4f + 1f);
            Critical = _manager.Brain.DisorientationNormalized > 0.4f || HeartStop || _manager.Bleeding.BloodVolumeMl <= Config.CriticalBloodMl;
            Incapacitated = _manager.Consciousness.State == ConsciousnessState.Unconscious &&
                            (Critical || _manager.Bleeding.BloodVolumeMl < 3000f || PulseBpm < 15f || _manager.Lungs.OxygenNormalized < 0.05f);

            bool gainFear = Pain > 30f || _manager.Bleeding.BloodVolumeMl < 3000f || _manager.Bleeding.TotalBleedRateMlPerSecond > 1f;
            FearAdd = MoveToward(FearAdd, gainFear ? 1f : 0f, gainFear ? elapsed / 5f : elapsed / 4.9f);
            Fear = MoveToward(Fear, gainFear ? 1f : -1f, gainFear ? elapsed / 5f * Math.Max(FearAdd, 0.25f) : elapsed / 50f);
            Fear = Config.Clamp(Fear, 0f, 1f);
        }

        private void UpdateMovementOutputs()
        {
            float legFracturePenalty = 0f;
            if (_manager.GetLimb(BodyPart.LeftLeg).IsBroken)
                legFracturePenalty += 0.32f;
            if (_manager.GetLimb(BodyPart.RightLeg).IsBroken)
                legFracturePenalty += 0.32f;

            float armFracturePenalty = 0f;
            if (_manager.GetLimb(BodyPart.LeftArm).IsBroken)
                armFracturePenalty += 0.18f;
            if (_manager.GetLimb(BodyPart.RightArm).IsBroken)
                armFracturePenalty += 0.18f;

            LegStrength = Config.Clamp(1f - legFracturePenalty - _manager.Shock.MovementPenalty - _manager.Medication.FatiguePenalty * 0.25f, 0.18f, 1.15f);
            RecoilMultiplier = Config.Clamp(1f + armFracturePenalty + _manager.PainNormalized * 0.35f + Fear * 0.20f - _manager.Medication.StaminaBoost * 0.22f, 0.55f, 2.2f);
            MeleeSpeed = Config.Clamp(1f - armFracturePenalty * 0.4f - _manager.PainNormalized * 0.25f, 0.45f, 1.2f);
        }

        private static float MoveToward(float value, float target, float maxDelta)
        {
            if (value < target)
                return Math.Min(value + Math.Max(0f, maxDelta), target);
            return Math.Max(value - Math.Max(0f, maxDelta), target);
        }

        private static float Remap(float value, float from1, float to1, float from2, float to2)
        {
            if (Math.Abs(to1 - from1) < 0.0001f)
                return from2;
            return from2 + (value - from1) * (to2 - from2) / (to1 - from1);
        }
    }
}
