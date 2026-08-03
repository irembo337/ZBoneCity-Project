using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class ConsciousnessSystem
    {
        private readonly HealthManager _manager;
        private readonly ZCityAudioBank _zCityAudioBank;
        private float _shock;
        private float _unconsciousSeconds;
        private float _wakeCheckAccumulator;
        private float _standUpRecoverySeconds;
        private float _standUpRecoveryDuration;
        private float _standUpInputCooldown;
        private AudioSource? _heartbeatSource;
        private AudioSource? _heavyHeartbeatSource;
        private AudioSource? _breathingSource;
        private GameObject? _audioRig;
        private AudioClip? _heartbeatNormalClip;
        private AudioClip? _heartbeatHeavyClip;
        private AudioClip? _breathingClip;

        public ConsciousnessState State { get; private set; }
        public float BlackoutIntensity { get; private set; }
        public float Shock => _shock;
        public float WakeProbability { get; private set; }
        public bool IsConscious => State == ConsciousnessState.Awake || State == ConsciousnessState.Blackout;
        public float RecoveryUsageMultiplier => _standUpRecoverySeconds > 0f && _standUpRecoveryDuration > 0f
            ? Mathf.Lerp(0.35f, 1f, 1f - Config.Clamp(_standUpRecoverySeconds / _standUpRecoveryDuration, 0f, 1f))
            : 1f;

        public event Action<ConsciousnessState>? StateChanged;

        public ConsciousnessSystem(HealthManager manager)
        {
            _manager = manager;
            _zCityAudioBank = new ZCityAudioBank();
            State = ConsciousnessState.Awake;
        }

        public void Reset()
        {
            _shock = 0f;
            _unconsciousSeconds = 0f;
            _wakeCheckAccumulator = 0f;
            BlackoutIntensity = 0f;
            WakeProbability = 0f;
            SetState(ConsciousnessState.Awake);
            UpdateAudio(0f);
        }

        public void Update(float deltaTime)
        {
            if (_manager.Kind == HealthOwnerKind.Player && !TraumaStartupGuard.CanRunPlayerTrauma)
            {
                ForceAwakeForSpawn();
                return;
            }

            UpdateRecoveryTimers(deltaTime);

            if (_manager.IsDead)
            {
                _unconsciousSeconds = 0f;
                _wakeCheckAccumulator = 0f;
                _standUpRecoverySeconds = 0f;
                _standUpRecoveryDuration = 0f;
                SetState(ConsciousnessState.Dead);
                BlackoutIntensity = 1f;
                UpdateEffects();
                UpdateAudio(deltaTime);
                return;
            }

            if (_manager.Kind == HealthOwnerKind.Player && Config.RealisticAudioEnabled)
                EnsureAudioRig(MainMod.Runtime?.GetHeadTransform());

            if (_manager.Coma.IsActive)
            {
                SetState(ConsciousnessState.Unconscious);
                _unconsciousSeconds = Math.Max(_unconsciousSeconds, 2f);
                BlackoutIntensity = MoveToward(BlackoutIntensity, 1f, deltaTime * 2.4f);
                UpdateEffects();
                UpdateAudio(deltaTime);
                return;
            }

            _shock = Config.Clamp(_shock - deltaTime * 0.035f, 0f, 1.25f);
            float bloodDanger = GetBloodDanger();
            float painDanger = _manager.PainSystem.BlackoutPressure;
            float fractureDanger = _manager.Fractures.GetMovementPenalty() * 0.35f;
            float headDanger = _manager.GetLimb(BodyPart.Head).DamagePercent * 0.75f;
            float oxygenDanger = _manager.Lungs.OxygenStress * 0.95f;
            float brainDanger = _manager.Brain.DisorientationNormalized * 0.65f;
            float awarenessDanger = _manager.AwarenessPenalty * 0.5f;
            float cardiacDanger = _manager.Organs.CardiacArrestActive ? 1.4f : 0f;
            float systemicShockDanger = _manager.Shock.UnconsciousnessPressure + _manager.Shock.TunnelVision * 0.18f;
            float danger = Config.Clamp(bloodDanger + painDanger * 0.24f + fractureDanger * 0.48f + headDanger + oxygenDanger + brainDanger + awarenessDanger + cardiacDanger + systemicShockDanger + _shock, 0f, 3.5f);

            if (State == ConsciousnessState.Awake)
            {
                BlackoutIntensity = MoveToward(BlackoutIntensity, Math.Max(0f, danger - 0.55f), deltaTime * 1.6f);
                if (danger >= Config.UnconsciousDangerThreshold - 0.55f)
                    SetState(ConsciousnessState.Blackout);
                if (_manager.PainSystem.InPainShock && _manager.Pain >= Config.UnconsciousPainThreshold && danger >= Config.UnconsciousDangerThreshold)
                    SetUnconscious(2.4f + danger * 3.5f);
            }
            else if (State == ConsciousnessState.Blackout)
            {
                BlackoutIntensity = MoveToward(BlackoutIntensity, Config.Clamp(danger, 0.35f, 1f), deltaTime * 1.25f);
                if (ShouldEnterUnconscious(danger, 4.5f))
                    SetUnconscious(5.5f + danger * 8.5f);
                else if (danger < 0.42f)
                    SetState(ConsciousnessState.Awake);
            }
            else if (State == ConsciousnessState.Unconscious)
            {
                _unconsciousSeconds -= deltaTime;
                BlackoutIntensity = MoveToward(BlackoutIntensity, 1f, deltaTime * 1.8f);
                _wakeCheckAccumulator += deltaTime;
                if (_wakeCheckAccumulator >= 2.0f)
                {
                    _wakeCheckAccumulator = 0f;
                    WakeProbability = CalculateWakeProbability(danger);
                    if (_unconsciousSeconds <= 0f && _manager.Random.NextDouble() < WakeProbability)
                        SetState(ConsciousnessState.Blackout);
                }
            }

            UpdateEffects();
            UpdateAudio(deltaTime);
        }

        public void UpdateRecoveryInput(float deltaTime)
        {
            _standUpInputCooldown = Math.Max(0f, _standUpInputCooldown - deltaTime);
            if (!IsStandUpPressed() || _standUpInputCooldown > 0f)
                return;

            _standUpInputCooldown = 0.85f;
            if (_manager.Coma.IsActive)
            {
                if (_manager.Coma.TryRecoverByStandUpInput())
                    _manager.AudioTrauma.TriggerPainVoice(0.35f);
                return;
            }

            if (State != ConsciousnessState.Unconscious || !CanAttemptStandUp())
                return;

            BeginStandUpRecovery(8f + _manager.PainNormalized * 4f + _manager.Lungs.OxygenStress * 4f);
            _manager.AudioTrauma.TriggerPainVoice(0.45f);
        }

        public void ApplyDamageImpulse(DamageInfo info)
        {
            if (_manager.Kind == HealthOwnerKind.Player && !TraumaStartupGuard.CanProcessPlayerDamage)
                return;

            float impulse = info.UnconsciousnessImpulse;
            if (info.DamageType == AdvancedDamageType.Blunt)
                impulse *= info.BodyPart == BodyPart.Head ? 0.35f : 0.20f;
            if (info.DamageType == AdvancedDamageType.Fall && !info.IsHighEnergyImpact)
                impulse *= 0.25f;
            if (info.BodyPart == BodyPart.Head)
            {
                if (info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Explosion)
                    impulse *= 2.2f;
                else if (info.DamageType == AdvancedDamageType.Blunt)
                    impulse *= 1.35f;
                else if (info.DamageType == AdvancedDamageType.Fall)
                    impulse *= 1.20f;
                else
                    impulse *= 1.50f;
            }
            if (info.DamageType == AdvancedDamageType.Explosion)
                impulse *= 1.65f;
            if (info.IsSelfInflicted && info.DamageType == AdvancedDamageType.Bullet)
            {
                if (info.BodyPart == BodyPart.Head)
                    impulse *= 3.8f;
                else if (info.BodyPart == BodyPart.Torso)
                    impulse *= 2.0f;
                else
                    impulse *= 0.18f;
            }
            if (info.DamageType == AdvancedDamageType.Fall && info.IsHighEnergyImpact)
                impulse *= 1.60f + Config.Clamp((info.ImpactVelocity - 10f) * 0.08f, 0f, 0.72f);

            _shock = Config.Clamp(_shock + impulse, 0f, 2.1f);
            if (info.IsSelfInflicted && info.DamageType == AdvancedDamageType.Bullet)
            {
                if (info.BodyPart == BodyPart.Head)
                    SetUnconscious(18f);
                else if (info.BodyPart == BodyPart.Torso)
                    SetUnconscious(6.5f);
            }
            else if (info.DamageType == AdvancedDamageType.Fall && info.IsHighEnergyImpact && (info.BodyPart == BodyPart.Head || info.BodyPart == BodyPart.Torso || info.Damage >= 72f))
            {
                SetUnconscious(2.75f + Config.Clamp(info.ImpactVelocity - 10f, 0f, 8f));
            }

            if (info.BodyPart == BodyPart.Head &&
                info.Damage >= 62f &&
                (info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Explosion))
            {
                SetUnconscious(3.0f + info.Damage * 0.045f);
            }
        }

        public void SetUnconscious(float seconds)
        {
            if (_manager.IsDead)
                return;
            if (_manager.Kind == HealthOwnerKind.Player && !TraumaStartupGuard.CanRunPlayerTrauma)
                return;
            if (_manager.Kind == HealthOwnerKind.Player && !_manager.HasReceivedRealDamage && !_manager.Coma.IsActive)
                return;
            if (_manager.Kind == HealthOwnerKind.Player && !_manager.Coma.IsActive && !ShouldEnterUnconscious(GetCurrentDanger(), seconds))
                return;

            _unconsciousSeconds = Math.Max(_unconsciousSeconds, seconds);
            SetState(ConsciousnessState.Unconscious);
        }

        public void Kill()
        {
            _shock = 0f;
            _unconsciousSeconds = 0f;
            _wakeCheckAccumulator = 0f;
            _standUpRecoverySeconds = 0f;
            _standUpRecoveryDuration = 0f;
            _standUpInputCooldown = 0f;
            WakeProbability = 0f;
            SetState(ConsciousnessState.Dead);
            BlackoutIntensity = 1f;
            UpdateEffects();
            UpdateAudio(0f);
        }

        internal void SetRecoveryUnconscious(float seconds)
        {
            if (_manager.IsDead)
                return;
            if (_manager.Kind == HealthOwnerKind.Player && !TraumaStartupGuard.CanRunPlayerTrauma)
                return;

            _unconsciousSeconds = Math.Max(0.1f, seconds);
            SetState(ConsciousnessState.Unconscious);
        }

        internal void BeginStandUpRecovery(float seconds)
        {
            if (_manager.IsDead)
                return;
            if (_manager.Kind == HealthOwnerKind.Player && !TraumaStartupGuard.CanRunPlayerTrauma)
                return;

            _unconsciousSeconds = 0f;
            _wakeCheckAccumulator = 0f;
            _standUpRecoveryDuration = Math.Max(2.5f, seconds);
            _standUpRecoverySeconds = _standUpRecoveryDuration;
            _shock = Config.Clamp(_shock + 0.16f, 0f, 1.25f);
            BlackoutIntensity = Math.Max(BlackoutIntensity, 0.62f);
            SetState(ConsciousnessState.Blackout);
            MainMod.Runtime?.SetPlayerRagdoll(false);
            MainMod.Runtime?.SetPlayerControlSuppressed(false);
            MainMod.Runtime?.ApplyPlayerLimbUsage(_manager.Fractures);
        }

        internal void ForceAwakeForSpawn()
        {
            _shock = 0f;
            _unconsciousSeconds = 0f;
            _wakeCheckAccumulator = 0f;
            _standUpRecoverySeconds = 0f;
            _standUpRecoveryDuration = 0f;
            _standUpInputCooldown = 0f;
            BlackoutIntensity = 0f;
            WakeProbability = 0f;
            if (State != ConsciousnessState.Awake)
                State = ConsciousnessState.Awake;
            MainMod.Runtime?.SetPlayerControlSuppressed(false);
            MainMod.Runtime?.SetPlayerRagdoll(false);
            UpdateEffects();
            UpdateAudio(0f);
        }

        public void Destroy()
        {
            if (_audioRig != null)
            {
                UnityEngine.Object.Destroy(_audioRig);
                _audioRig = null;
            }
        }

        private float GetBloodDanger()
        {
            float blood = _manager.Bleeding.BloodVolumeMl;
            if (blood <= Config.DeathBloodMl)
                return 1.55f;
            if (blood <= Config.CriticalBloodMl)
                return 1.08f + (Config.CriticalBloodMl - blood) / Math.Max(1f, Config.CriticalBloodMl - Config.DeathBloodMl) * 0.38f;
            if (blood <= Config.UnconsciousBloodMl)
                return 0.64f + (Config.UnconsciousBloodMl - blood) / Math.Max(1f, Config.UnconsciousBloodMl - Config.CriticalBloodMl) * 0.32f;
            return Config.Clamp((Config.BloodVolumeMl - blood) / Config.BloodVolumeMl * 0.48f, 0f, 0.48f);
        }

        private float GetCurrentDanger()
        {
            float bloodDanger = GetBloodDanger();
            float painDanger = _manager.PainSystem.BlackoutPressure;
            float fractureDanger = _manager.Fractures.GetMovementPenalty() * 0.35f;
            float headDanger = _manager.GetLimb(BodyPart.Head).DamagePercent * 0.75f;
            float oxygenDanger = _manager.Lungs.OxygenStress * 0.95f;
            float brainDanger = _manager.Brain.DisorientationNormalized * 0.65f;
            float awarenessDanger = _manager.AwarenessPenalty * 0.5f;
            float cardiacDanger = _manager.Organs.CardiacArrestActive ? 1.4f : 0f;
            float systemicShockDanger = _manager.Shock.UnconsciousnessPressure + _manager.Shock.TunnelVision * 0.18f;
            return Config.Clamp(bloodDanger + painDanger * 0.24f + fractureDanger * 0.48f + headDanger + oxygenDanger + brainDanger + awarenessDanger + cardiacDanger + systemicShockDanger + _shock, 0f, 3.5f);
        }

        private bool ShouldEnterUnconscious(float danger, float requestedSeconds)
        {
            if (_manager.Coma.IsActive || _manager.IsDead)
                return true;
            if (_manager.Organs.CardiacArrestActive)
                return true;
            if (_manager.Bleeding.BloodVolumeMl <= Config.CriticalBloodMl)
                return true;
            if (_manager.Lungs.OxygenNormalized <= 0.38f)
                return true;
            if (_manager.Pain >= Config.UnconsciousPainThreshold && danger >= Config.UnconsciousDangerThreshold)
                return true;
            if (_manager.GetLimb(BodyPart.Head).DamagePercent >= Config.UnconsciousHeadDamageThreshold && danger >= Config.UnconsciousDangerThreshold - 0.32f)
                return true;
            if (_manager.Brain.DisorientationNormalized >= 0.78f && danger >= Config.UnconsciousDangerThreshold - 0.24f)
                return true;
            if (_manager.Shock.Intensity >= Config.UnconsciousShockThreshold || _manager.Shock.UnconsciousnessPressure >= 0.64f)
                return true;
            if (_manager.TotalTraumaNormalized >= 0.72f && danger >= Config.UnconsciousDangerThreshold)
                return true;
            if (requestedSeconds >= 8f && danger >= Config.UnconsciousDangerThreshold - 0.12f)
                return true;

            return false;
        }

        private float CalculateWakeProbability(float danger)
        {
            float bloodRecovery = Config.Clamp((_manager.Bleeding.BloodVolumeMl - Config.CriticalBloodMl) / Math.Max(1f, Config.BloodVolumeMl - Config.CriticalBloodMl), 0f, 1f);
            float painRecovery = 1f - _manager.PainNormalized;
            float oxygenRecovery = _manager.Lungs.OxygenNormalized;
            float brainRecovery = 1f - _manager.Brain.DisorientationNormalized;
            float chance = 0.02f + bloodRecovery * 0.18f + painRecovery * 0.10f + oxygenRecovery * 0.08f + brainRecovery * 0.08f - danger * 0.12f;
            return Config.Clamp(chance, 0.005f, 0.38f);
        }

        private void UpdateRecoveryTimers(float deltaTime)
        {
            _standUpInputCooldown = Math.Max(0f, _standUpInputCooldown - deltaTime);
            if (_standUpRecoverySeconds <= 0f)
                return;

            _standUpRecoverySeconds = Math.Max(0f, _standUpRecoverySeconds - deltaTime);
            float normalized = _standUpRecoveryDuration > 0f ? _standUpRecoverySeconds / _standUpRecoveryDuration : 0f;
            BlackoutIntensity = Math.Max(BlackoutIntensity, normalized * 0.46f);
            _shock = Math.Max(_shock, normalized * 0.18f);
            MainMod.Runtime?.ApplyPlayerLimbUsage(_manager.Fractures);
        }

        private bool CanAttemptStandUp()
        {
            if (_manager.Bleeding.BloodVolumeMl <= Config.CriticalBloodMl + 150f)
                return false;
            if (_manager.Lungs.OxygenNormalized < 0.42f)
                return false;
            if (_manager.Organs.CardiacArrestActive)
                return false;
            if (_manager.GetLimb(BodyPart.Head).Hp <= 0f)
                return false;
            if (_manager.Bleeding.TotalBleedRateMlPerSecond > 24f)
                return false;

            return _unconsciousSeconds <= 1.5f || WakeProbability >= 0.04f || BlackoutIntensity < 0.82f;
        }

        private static bool IsStandUpPressed()
        {
            return Input.GetKeyDown(KeyCode.JoystickButton9) ||
                   Input.GetKeyDown(KeyCode.JoystickButton10) ||
                   Input.GetKeyDown(KeyCode.JoystickButton11);
        }

        private void SetState(ConsciousnessState next)
        {
            if (State == next)
                return;

            State = next;
            if (_manager.Kind == HealthOwnerKind.Player)
            {
                bool ragdoll = next == ConsciousnessState.Unconscious || next == ConsciousnessState.Dead;
                MainMod.Runtime?.SetPlayerRagdoll(ragdoll);
                MainMod.Runtime?.SetPlayerControlSuppressed(ragdoll);
            }
            else if (_manager is NPCHealth npc)
            {
                npc.ApplyConsciousnessToGame(next);
            }

            try
            {
                StateChanged?.Invoke(next);
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("Consciousness StateChanged event failed safely: " + ex.Message);
            }
        }

        private void UpdateEffects()
        {
            if (_manager.Kind != HealthOwnerKind.Player)
                return;
            if (!Config.UnconsciousEffectsEnabled)
                return;

            MainMod.Runtime?.NotifyConsciousnessEffects(BlackoutIntensity, _manager.PainNormalized, State);
        }

        private void EnsureAudioRig(Transform? head)
        {
            if (head == null)
                return;

            if (_audioRig == null)
            {
                _audioRig = new GameObject("AHS_AudioRig");
                _audioRig.transform.SetParent(head, false);
                _audioRig.transform.localPosition = Vector3.zero;
                _audioRig.transform.localRotation = Quaternion.identity;
                _heartbeatSource = _audioRig.AddComponent<AudioSource>();
                _heavyHeartbeatSource = _audioRig.AddComponent<AudioSource>();
                _breathingSource = _audioRig.AddComponent<AudioSource>();
                Configure2dSource(_heartbeatSource);
                Configure2dSource(_heavyHeartbeatSource);
                Configure2dSource(_breathingSource);
                EnsureHeartbeatClips();
                _breathingClip = CreateBreathingClip();
                _heartbeatSource.clip = _heartbeatNormalClip;
                _heavyHeartbeatSource.clip = _heartbeatHeavyClip;
                _breathingSource.clip = _breathingClip;
                _heartbeatSource.loop = true;
                _heavyHeartbeatSource.loop = true;
                _breathingSource.loop = true;
            }
            else if (_audioRig.transform.parent != head)
            {
                _audioRig.transform.SetParent(head, false);
                _audioRig.transform.localPosition = Vector3.zero;
                _audioRig.transform.localRotation = Quaternion.identity;
            }
        }

        private void UpdateAudio(float deltaTime)
        {
            if (_manager.Kind != HealthOwnerKind.Player || _heartbeatSource == null || _heavyHeartbeatSource == null || _breathingSource == null || !Config.RealisticAudioEnabled)
                return;

            EnsureHeartbeatClips();
            _heartbeatSource.clip = _heartbeatNormalClip;
            _heavyHeartbeatSource.clip = _heartbeatHeavyClip;

            MedicalTelemetrySnapshot telemetry = _manager.TelemetrySnapshot;
            float danger = Config.Clamp(BlackoutIntensity + telemetry.VitalDanger * 0.85f + telemetry.ConsciousnessRisk * 0.35f + _manager.Lungs.BreathingPanic * 0.25f, 0f, 1.8f);
            float audioIntensity = Config.AudioIntensity * Config.MasterVolume;
            float heartbeatVolume = State == ConsciousnessState.Dead || !Config.CustomSoundsEnabled
                ? 0f
                : Config.Clamp((danger - 0.10f) * 0.55f * audioIntensity, 0f, 0.86f);
            float breathingVolume = State == ConsciousnessState.Dead ? 0f : Config.Clamp((0.08f + _manager.Fractures.BreathingPenalty * 0.35f + _manager.PainNormalized * 0.12f + _manager.Lungs.BreathingPanic * 0.28f) * audioIntensity, 0f, 0.72f);
            if (_manager.Coma.IsActive)
            {
                heartbeatVolume *= 0.45f;
                breathingVolume = Config.Clamp(breathingVolume * 0.55f + 0.08f * audioIntensity, 0f, 0.42f);
                danger = Math.Min(danger, 0.65f);
            }

            float heavyBlend = Config.Clamp((danger - 0.58f) / 0.76f + telemetry.ShockNormalized * 0.20f + telemetry.BloodLossNormalized * 0.22f, 0f, 1f);
            float normalTarget = _heartbeatNormalClip == null ? 0f : heartbeatVolume * (1f - heavyBlend * 0.72f);
            float heavyTarget = _heartbeatHeavyClip == null ? 0f : heartbeatVolume * heavyBlend;

            _heartbeatSource.volume = MoveToward(_heartbeatSource.volume, normalTarget, deltaTime * 1.25f);
            _heavyHeartbeatSource.volume = MoveToward(_heavyHeartbeatSource.volume, heavyTarget, deltaTime * 1.35f);
            _breathingSource.volume = MoveToward(_breathingSource.volume, breathingVolume, deltaTime * 0.65f);
            float pulsePitch = _manager.Coma.IsActive ? 0.62f : Config.Clamp(0.72f + danger * 0.42f + (telemetry.HeartbeatBpm - 70f) / 140f, 0.72f, 1.82f);
            _heartbeatSource.pitch = pulsePitch;
            _heavyHeartbeatSource.pitch = Config.Clamp(pulsePitch * 0.94f + heavyBlend * 0.18f, 0.68f, 1.88f);
            _breathingSource.pitch = _manager.Coma.IsActive ? 0.70f : Config.Clamp(0.85f + _manager.Fractures.BreathingPenalty * 0.45f, 0.75f, 1.35f);

            UpdateLoopingSource(_heartbeatSource);
            UpdateLoopingSource(_heavyHeartbeatSource);

            if (_breathingSource.volume > 0.02f && !_breathingSource.isPlaying)
                _breathingSource.Play();
            else if (_breathingSource.volume <= 0.005f && _breathingSource.isPlaying)
                _breathingSource.Stop();
        }

        private static void UpdateLoopingSource(AudioSource source)
        {
            if (source.clip == null)
            {
                if (source.isPlaying)
                    source.Stop();
                source.volume = 0f;
                return;
            }

            if (source.volume > 0.02f && !source.isPlaying)
                source.Play();
            else if (source.volume <= 0.005f && source.isPlaying)
                source.Stop();
        }

        private void EnsureHeartbeatClips()
        {
            if (_heartbeatNormalClip == null)
            {
                _heartbeatNormalClip = _zCityAudioBank.GetLongestClip("Heartbeat", "ZBC_ZCity_Heartbeat_Normal");
                if (_heartbeatNormalClip == null)
                {
                    string? normalPath = ExternalAudioClipLoader.ResolveAudioPath("heartthump", "Heartthump", "heartthump.wav");
                    _heartbeatNormalClip = ExternalAudioClipLoader.TryGetClip(normalPath, "ZBC_HeartThump");
                }
            }

            if (_heartbeatHeavyClip == null)
            {
                string? heavyPath = ExternalAudioClipLoader.ResolveAudioPath("ZCity\\Heartbeat\\heartbeat_04", "heartthump-heavy", "Heavy heart bum", "heartthump-heavy.wav");
                _heartbeatHeavyClip = ExternalAudioClipLoader.TryGetClip(heavyPath, "ZBC_HeartThump_Heavy");
            }
        }

        private static void Configure2dSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 32;
        }

        private static AudioClip CreateBreathingClip()
        {
            const int sampleRate = 22050;
            const int seconds = 4;
            int samples = sampleRate * seconds;
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float envelope = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 0.5f);
                float hiss = Mathf.Sin(t * 900f) * 0.015f + Mathf.Sin(t * 1730f) * 0.01f;
                data[i] = hiss * envelope;
            }

            AudioClip clip = AudioClip.Create("AHS_Breathing", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float MoveToward(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
                return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
    }
}
