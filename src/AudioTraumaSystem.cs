using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class AudioTraumaSystem
    {
        private readonly HealthManager _manager;
        private readonly AudioClip[] _painClips;
        private readonly ZCityAudioSystem _zCityAudio;
        private GameObject? _rig;
        private AudioSource? _painSource;
        private AudioSource? _ringSource;
        private AudioSource? _noiseSource;
        private AudioSource? _lowPulseSource;
        private AudioLowPassFilter? _listenerLowPass;
        private AudioClip? _ringClip;
        private AudioClip? _noiseClip;
        private AudioClip? _lowPulseClip;
        private float _pendingPainIntensity;
        private float _painCooldown;
        private float _impactShock;

        public float MuffleIntensity { get; private set; }
        public float RingingIntensity { get; private set; }

        public AudioTraumaSystem(HealthManager manager)
        {
            _manager = manager;
            _zCityAudio = new ZCityAudioSystem(manager);
            _painClips = new AudioClip[5];
            for (int i = 0; i < _painClips.Length; i++)
                _painClips[i] = CreatePainClip(i);
            _ringClip = CreateToneClip("AHS_EarRing", 4400f, 3.0f, 0.18f);
            _noiseClip = CreateNoiseClip("AHS_WhiteNoise", 2.0f, 0.12f);
            _lowPulseClip = CreateLowPulseClip();
        }

        public void Reset()
        {
            _pendingPainIntensity = 0f;
            _painCooldown = 0f;
            _impactShock = 0f;
            MuffleIntensity = 0f;
            RingingIntensity = 0f;
            if (_painSource != null)
                _painSource.Stop();
            if (_ringSource != null)
                _ringSource.volume = 0f;
            if (_noiseSource != null)
                _noiseSource.volume = 0f;
            if (_lowPulseSource != null)
                _lowPulseSource.volume = 0f;
            _zCityAudio.Reset();
        }

        public void Destroy()
        {
            _zCityAudio.Destroy();
            if (_rig != null)
            {
                UnityEngine.Object.Destroy(_rig);
                _rig = null;
            }
        }

        public void OnDamage(DamageInfo info, OrganDamageFeedback organFeedback)
        {
            _zCityAudio.OnDamage(info, organFeedback);
            float intensity = Config.Clamp(info.Damage / 85f + _manager.PainNormalized * 0.5f, 0f, 1f);
            if (info.BodyPart == BodyPart.Head)
                intensity += 0.22f;
            if (organFeedback.PrimaryOrgan == OrganType.Lungs || organFeedback.CardiacArrest)
                intensity += 0.25f;

            _pendingPainIntensity = Math.Max(_pendingPainIntensity, Config.Clamp(intensity, 0f, 1.25f));
            if (info.BodyPart == BodyPart.Head || info.DamageType == AdvancedDamageType.Explosion)
                _impactShock = Config.Clamp(_impactShock + 0.45f + intensity * 0.4f, 0f, 1f);
        }

        public void Update(float deltaTime)
        {
            Transform? anchor = GetAnchor();
            if (anchor != null)
                EnsureRig(anchor);
            _zCityAudio.Update(deltaTime);

            _painCooldown = Math.Max(0f, _painCooldown - deltaTime);
            _impactShock = Math.Max(0f, _impactShock - deltaTime * 0.18f);

            float oxygenNoise = _manager.Lungs.WhiteNoiseIntensity;
            RingingIntensity = Config.Clamp(_manager.Brain.RingingIntensity + _impactShock * 0.75f + _manager.Consciousness.BlackoutIntensity * 0.25f, 0f, 1f);
            MuffleIntensity = Config.Clamp(_manager.Consciousness.BlackoutIntensity * 0.75f + _manager.AwarenessPenalty + _manager.Brain.DisorientationNormalized * 0.4f, 0f, 1f);

            if (_pendingPainIntensity > 0.05f && _painCooldown <= 0f)
                PlayPain(_pendingPainIntensity);
            _pendingPainIntensity = Math.Max(0f, _pendingPainIntensity - deltaTime * 0.8f);

            if (_ringSource != null)
            {
                _ringSource.volume = MoveToward(_ringSource.volume, RingingIntensity * 0.22f, deltaTime * 0.9f);
                _ringSource.pitch = 0.85f + RingingIntensity * 0.35f;
                EnsureLoop(_ringSource);
            }

            if (_noiseSource != null)
            {
                _noiseSource.volume = MoveToward(_noiseSource.volume, oxygenNoise * 0.32f, deltaTime * 0.8f);
                _noiseSource.pitch = 0.82f + _manager.Lungs.BreathingPanic * 0.45f;
                EnsureLoop(_noiseSource);
            }

            if (_lowPulseSource != null)
            {
                float heartDanger = 1f - _manager.Organs.HeartbeatStrength;
                _lowPulseSource.volume = MoveToward(_lowPulseSource.volume, heartDanger * 0.26f, deltaTime * 0.8f);
                _lowPulseSource.pitch = 0.75f + _manager.Organs.HeartbeatStrength * 0.45f;
                EnsureLoop(_lowPulseSource);
            }

            UpdateListenerMuffle(deltaTime);
        }

        private void EnsureRig(Transform anchor)
        {
            if (_rig == null)
            {
                _rig = new GameObject("AHS_TraumaAudio");
                _painSource = _rig.AddComponent<AudioSource>();
                _ringSource = _rig.AddComponent<AudioSource>();
                _noiseSource = _rig.AddComponent<AudioSource>();
                _lowPulseSource = _rig.AddComponent<AudioSource>();
                ConfigureSource(_painSource, _manager.Kind == HealthOwnerKind.Player, false);
                ConfigureSource(_ringSource, _manager.Kind == HealthOwnerKind.Player, true);
                ConfigureSource(_noiseSource, _manager.Kind == HealthOwnerKind.Player, true);
                ConfigureSource(_lowPulseSource, _manager.Kind == HealthOwnerKind.Player, true);
                _ringSource.clip = _ringClip;
                _noiseSource.clip = _noiseClip;
                _lowPulseSource.clip = _lowPulseClip;
            }

            if (_rig.transform.parent != anchor)
            {
                _rig.transform.SetParent(anchor, false);
                _rig.transform.localPosition = Vector3.zero;
                _rig.transform.localRotation = Quaternion.identity;
            }
        }

        private Transform? GetAnchor()
        {
            if (_manager.Kind == HealthOwnerKind.Player)
                return MainMod.Runtime?.GetHeadTransform();
            if (_manager is NPCHealth npc)
                return npc.GetEffectTransform();
            return null;
        }

        private void PlayPain(float intensity)
        {
            if (_painSource == null)
                return;
            if (_manager.Kind == HealthOwnerKind.Player && _zCityAudio.HasPainTrack)
            {
                _painCooldown = Config.Clamp(1.2f - intensity * 0.45f, 0.45f, 1.2f);
                return;
            }

            int index = _manager.Random.Next(0, _painClips.Length);
            _painSource.clip = _painClips[index];
            _painSource.volume = Config.Clamp(0.12f + intensity * 0.55f, 0f, 0.78f);
            _painSource.pitch = Config.Clamp(0.82f + (float)_manager.Random.NextDouble() * 0.26f - intensity * 0.1f, 0.62f, 1.2f);
            _painSource.Play();
            _painCooldown = Config.Clamp(1.8f - intensity * 0.7f, 0.55f, 1.8f);
        }

        private void UpdateListenerMuffle(float deltaTime)
        {
            if (_manager.Kind != HealthOwnerKind.Player)
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            if (_listenerLowPass == null)
                _listenerLowPass = camera.gameObject.GetComponent<AudioLowPassFilter>() ?? camera.gameObject.AddComponent<AudioLowPassFilter>();

            float targetCutoff = Mathf.Lerp(22000f, 850f, MuffleIntensity);
            _listenerLowPass.cutoffFrequency = MoveToward(_listenerLowPass.cutoffFrequency <= 0f ? 22000f : _listenerLowPass.cutoffFrequency, targetCutoff, deltaTime * 12000f);
            _listenerLowPass.lowpassResonanceQ = 1f + MuffleIntensity * 1.6f;
        }

        private static void ConfigureSource(AudioSource source, bool player2d, bool loop)
        {
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = player2d ? 0f : 1f;
            source.volume = 0f;
            source.priority = player2d ? 20 : 96;
            source.minDistance = 0.6f;
            source.maxDistance = 10f;
            source.rolloffMode = AudioRolloffMode.Linear;
        }

        private static void EnsureLoop(AudioSource source)
        {
            if (source.volume > 0.005f && !source.isPlaying)
                source.Play();
            else if (source.volume <= 0.002f && source.isPlaying)
                source.Stop();
        }

        private static AudioClip CreatePainClip(int variant)
        {
            const int sampleRate = 22050;
            float seconds = 0.55f + variant * 0.09f;
            int samples = (int)(sampleRate * seconds);
            float[] data = new float[samples];
            float baseFreq = 115f + variant * 18f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float x = i / (float)samples;
                float envelope = Mathf.Sin(x * Mathf.PI);
                float vibrato = Mathf.Sin(t * (6f + variant)) * 7f;
                float tone = Mathf.Sin((baseFreq + vibrato) * t * Mathf.PI * 2f);
                float throat = Mathf.Sin((baseFreq * 0.48f) * t * Mathf.PI * 2f) * 0.45f;
                data[i] = (tone + throat) * envelope * 0.18f;
            }

            AudioClip clip = AudioClip.Create("AHS_Pain_" + variant, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateToneClip(string name, float frequency, float seconds, float amplitude)
        {
            const int sampleRate = 22050;
            int samples = (int)(sampleRate * seconds);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                data[i] = Mathf.Sin(t * frequency * Mathf.PI * 2f) * amplitude;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateNoiseClip(string name, float seconds, float amplitude)
        {
            const int sampleRate = 22050;
            int samples = (int)(sampleRate * seconds);
            float[] data = new float[samples];
            uint seed = 2166136261u;
            for (int i = 0; i < samples; i++)
            {
                seed ^= (uint)i + 0x9e3779b9u;
                seed *= 16777619u;
                float value = ((seed & 0xffff) / 32768f) - 1f;
                data[i] = value * amplitude;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateLowPulseClip()
        {
            const int sampleRate = 22050;
            int samples = sampleRate * 2;
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float pulse = Mathf.Sin(t * Mathf.PI * 2f);
                float envelope = Mathf.Pow(Mathf.Max(0f, pulse), 12f);
                data[i] = Mathf.Sin(t * 62f * Mathf.PI * 2f) * envelope * 0.32f;
            }

            AudioClip clip = AudioClip.Create("AHS_LowPulse", samples, 1, sampleRate, false);
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
