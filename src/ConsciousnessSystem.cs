using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class ConsciousnessSystem
    {
        private readonly HealthManager _manager;
        private float _shock;
        private float _unconsciousSeconds;
        private float _wakeCheckAccumulator;
        private AudioSource? _heartbeatSource;
        private AudioSource? _breathingSource;
        private GameObject? _audioRig;
        private AudioClip? _heartbeatClip;
        private AudioClip? _breathingClip;

        public ConsciousnessState State { get; private set; }
        public float BlackoutIntensity { get; private set; }
        public float Shock => _shock;
        public float WakeProbability { get; private set; }
        public bool IsConscious => State == ConsciousnessState.Awake || State == ConsciousnessState.Blackout;

        public event Action<ConsciousnessState>? StateChanged;

        public ConsciousnessSystem(HealthManager manager)
        {
            _manager = manager;
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
            if (_manager.IsDead)
            {
                SetState(ConsciousnessState.Dead);
                BlackoutIntensity = 1f;
                UpdateEffects();
                UpdateAudio(deltaTime);
                return;
            }

            if (_manager.Kind == HealthOwnerKind.Player)
                EnsureAudioRig(MainMod.Runtime?.GetHeadTransform());

            _shock = Config.Clamp(_shock - deltaTime * 0.035f, 0f, 1.25f);
            float bloodDanger = GetBloodDanger();
            float painDanger = _manager.PainNormalized;
            float fractureDanger = _manager.Fractures.GetMovementPenalty() * 0.35f;
            float headDanger = _manager.GetLimb(BodyPart.Head).DamagePercent * 0.75f;
            float danger = Config.Clamp(bloodDanger + painDanger * 0.45f + fractureDanger + headDanger + _shock, 0f, 2.5f);

            if (State == ConsciousnessState.Awake)
            {
                BlackoutIntensity = MoveToward(BlackoutIntensity, Math.Max(0f, danger - 0.55f), deltaTime * 1.6f);
                if (danger >= 1.0f)
                    SetState(ConsciousnessState.Blackout);
            }
            else if (State == ConsciousnessState.Blackout)
            {
                BlackoutIntensity = MoveToward(BlackoutIntensity, Config.Clamp(danger, 0.35f, 1f), deltaTime * 1.25f);
                if (danger >= 1.28f || _manager.Bleeding.BloodVolumeMl <= Config.CriticalBloodMl)
                    SetUnconscious(6f + danger * 10f);
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

        public void ApplyDamageImpulse(DamageInfo info)
        {
            float impulse = info.UnconsciousnessImpulse;
            if (info.BodyPart == BodyPart.Head)
                impulse *= 2.2f;
            if (info.DamageType == AdvancedDamageType.Explosion)
                impulse *= 1.65f;

            _shock = Config.Clamp(_shock + impulse, 0f, 1.35f);
            if (info.BodyPart == BodyPart.Head && info.Damage >= 45f)
                SetUnconscious(3.5f + info.Damage * 0.08f);
        }

        public void SetUnconscious(float seconds)
        {
            _unconsciousSeconds = Math.Max(_unconsciousSeconds, seconds);
            SetState(ConsciousnessState.Unconscious);
        }

        public void Kill()
        {
            SetState(ConsciousnessState.Dead);
            BlackoutIntensity = 1f;
            UpdateEffects();
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
                return 1.6f;
            if (blood <= Config.CriticalBloodMl)
                return 1.15f + (Config.CriticalBloodMl - blood) / Math.Max(1f, Config.CriticalBloodMl - Config.DeathBloodMl) * 0.45f;
            if (blood <= Config.UnconsciousBloodMl)
                return 0.72f + (Config.UnconsciousBloodMl - blood) / Math.Max(1f, Config.UnconsciousBloodMl - Config.CriticalBloodMl) * 0.38f;
            return Config.Clamp((Config.BloodVolumeMl - blood) / Config.BloodVolumeMl * 0.55f, 0f, 0.55f);
        }

        private float CalculateWakeProbability(float danger)
        {
            float bloodRecovery = Config.Clamp((_manager.Bleeding.BloodVolumeMl - Config.CriticalBloodMl) / Math.Max(1f, Config.BloodVolumeMl - Config.CriticalBloodMl), 0f, 1f);
            float painRecovery = 1f - _manager.PainNormalized;
            float chance = 0.03f + bloodRecovery * 0.22f + painRecovery * 0.12f - danger * 0.10f;
            return Config.Clamp(chance, 0.005f, 0.38f);
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
            }
            else if (_manager is NPCHealth npc)
            {
                npc.ApplyConsciousnessToGame(next);
            }

            StateChanged?.Invoke(next);
        }

        private void UpdateEffects()
        {
            if (_manager.Kind != HealthOwnerKind.Player)
                return;

            MainMod.Runtime?.Hud.SetConsciousnessEffects(BlackoutIntensity, _manager.PainNormalized, State);
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
                _breathingSource = _audioRig.AddComponent<AudioSource>();
                Configure2dSource(_heartbeatSource);
                Configure2dSource(_breathingSource);
                _heartbeatClip = CreateHeartbeatClip();
                _breathingClip = CreateBreathingClip();
                _heartbeatSource.clip = _heartbeatClip;
                _breathingSource.clip = _breathingClip;
                _heartbeatSource.loop = true;
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
            if (_manager.Kind != HealthOwnerKind.Player || _heartbeatSource == null || _breathingSource == null)
                return;

            float danger = Config.Clamp(BlackoutIntensity + _manager.PainNormalized * 0.65f + GetBloodDanger() * 0.55f, 0f, 1.5f);
            float heartbeatVolume = State == ConsciousnessState.Dead ? 0f : Config.Clamp(danger * 0.45f, 0f, 0.55f);
            float breathingVolume = State == ConsciousnessState.Dead ? 0f : Config.Clamp(0.08f + _manager.Fractures.BreathingPenalty * 0.35f + _manager.PainNormalized * 0.12f, 0f, 0.42f);

            _heartbeatSource.volume = MoveToward(_heartbeatSource.volume, heartbeatVolume, deltaTime * 0.9f);
            _breathingSource.volume = MoveToward(_breathingSource.volume, breathingVolume, deltaTime * 0.65f);
            _heartbeatSource.pitch = Config.Clamp(0.78f + danger * 0.5f, 0.75f, 1.55f);
            _breathingSource.pitch = Config.Clamp(0.85f + _manager.Fractures.BreathingPenalty * 0.45f, 0.75f, 1.35f);

            if (_heartbeatSource.volume > 0.02f && !_heartbeatSource.isPlaying)
                _heartbeatSource.Play();
            else if (_heartbeatSource.volume <= 0.005f && _heartbeatSource.isPlaying)
                _heartbeatSource.Stop();

            if (_breathingSource.volume > 0.02f && !_breathingSource.isPlaying)
                _breathingSource.Play();
            else if (_breathingSource.volume <= 0.005f && _breathingSource.isPlaying)
                _breathingSource.Stop();
        }

        private static void Configure2dSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 32;
        }

        private static AudioClip CreateHeartbeatClip()
        {
            const int sampleRate = 22050;
            const int seconds = 2;
            int samples = sampleRate * seconds;
            float[] data = new float[samples];
            AddPulse(data, sampleRate, 0.08f, 0.055f, 0.85f);
            AddPulse(data, sampleRate, 0.28f, 0.04f, 0.55f);
            AddPulse(data, sampleRate, 1.08f, 0.055f, 0.85f);
            AddPulse(data, sampleRate, 1.28f, 0.04f, 0.55f);
            AudioClip clip = AudioClip.Create("AHS_Heartbeat", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
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

        private static void AddPulse(float[] data, int sampleRate, float startSeconds, float lengthSeconds, float amplitude)
        {
            int start = (int)(startSeconds * sampleRate);
            int length = Math.Max(1, (int)(lengthSeconds * sampleRate));
            for (int i = 0; i < length && start + i < data.Length; i++)
            {
                float x = i / (float)length;
                float envelope = (float)Math.Sin(x * Math.PI);
                data[start + i] += envelope * amplitude * (float)Math.Sin(x * Math.PI * 10f);
            }
        }

        private static float MoveToward(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
                return target;
            return current + Math.Sign(target - current) * maxDelta;
        }
    }
}
