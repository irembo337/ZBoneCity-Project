using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class AudioTraumaSystem
    {
        private readonly HealthManager _manager;
        private readonly AudioClip[] _painClips;
        private readonly ZCityAudioSystem _zCityAudio;
        private readonly ZCityAudioBank _zCityBank;
        private GameObject? _rig;
        private AudioSource? _painSource;
        private AudioSource? _ringSource;
        private AudioSource? _noiseSource;
        private AudioSource? _breathSource;
        private AudioSource? _coughSource;
        private AudioLowPassFilter? _listenerLowPass;
        private AudioClip? _ringClip;
        private AudioClip? _noiseClip;
        private AudioClip? _breathClip;
        private AudioClip? _zCityBreathingClip;
        private AudioClip? _coughClip;
        private AudioClip? _fractureClip;
        private AudioClip? _neckCrackClip;
        private string? _neckCrackPath;
        private float _pendingPainIntensity;
        private float _pendingFractureIntensity;
        private float _pendingNeckCrackIntensity;
        private float _pendingMedicalIntensity;
        private float _painCooldown;
        private float _coughCooldown;
        private float _medicalCooldown;
        private float _impactShock;

        public float MuffleIntensity { get; private set; }
        public float RingingIntensity { get; private set; }

        public AudioTraumaSystem(HealthManager manager)
        {
            _manager = manager;
            _zCityAudio = new ZCityAudioSystem(manager);
            _zCityBank = new ZCityAudioBank();
            _painClips = new AudioClip[5];
            for (int i = 0; i < _painClips.Length; i++)
                _painClips[i] = CreatePainClip(i);
            _ringClip = CreateSoftRingingClip();
            _noiseClip = CreateNoiseClip("AHS_WhiteNoise", 2.0f, 0.12f);
            _breathClip = CreateBreathingClip();
            _coughClip = CreateCoughClip();
            _fractureClip = CreateFractureClip();
        }

        public void Reset()
        {
            _pendingPainIntensity = 0f;
            _pendingFractureIntensity = 0f;
            _pendingNeckCrackIntensity = 0f;
            _pendingMedicalIntensity = 0f;
            _painCooldown = 0f;
            _coughCooldown = 0f;
            _medicalCooldown = 0f;
            _impactShock = 0f;
            MuffleIntensity = 0f;
            RingingIntensity = 0f;
            if (_painSource != null)
                _painSource.Stop();
            if (_ringSource != null)
                _ringSource.volume = 0f;
            if (_noiseSource != null)
                _noiseSource.volume = 0f;
            if (_breathSource != null)
                _breathSource.volume = 0f;
            if (_coughSource != null)
                _coughSource.Stop();
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
            if (!Config.RealisticAudioEnabled)
                return;

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

        public void TriggerPainVoice(float intensity)
        {
            if (!Config.RealisticAudioEnabled)
                return;

            _pendingPainIntensity = Math.Max(_pendingPainIntensity, Config.Clamp(intensity, 0f, 1.25f));
        }

        public void TriggerFractureSound(float intensity)
        {
            if (!Config.RealisticAudioEnabled)
                return;

            _pendingFractureIntensity = Math.Max(_pendingFractureIntensity, Config.Clamp(intensity, 0f, 1.3f));
        }

        public void TriggerNeckCrack(float intensity)
        {
            if (!Config.RealisticAudioEnabled)
                return;

            _pendingNeckCrackIntensity = Math.Max(_pendingNeckCrackIntensity, Config.Clamp(intensity, 0f, 1.3f));
        }

        public void TriggerMedicalUse(MedicalItemType type)
        {
            if (!Config.RealisticAudioEnabled)
                return;

            float intensity = type == MedicalItemType.Medkit || type == MedicalItemType.BloodPack ? 0.9f : 0.65f;
            if (type == MedicalItemType.Morphine || type == MedicalItemType.Adrenaline || type == MedicalItemType.ETGStimulator || type == MedicalItemType.SJ1Stimulator)
                intensity = 0.72f;
            _pendingMedicalIntensity = Math.Max(_pendingMedicalIntensity, intensity);
        }

        public void OnDeath(DeathCause cause)
        {
            if (!Config.RealisticAudioEnabled)
                return;
        }

        public void Update(float deltaTime)
        {
            if (!Config.RealisticAudioEnabled)
                return;

            Transform? anchor = GetAnchor();
            if (anchor != null)
                EnsureRig(anchor);
            _zCityAudio.Update(deltaTime);

            _painCooldown = Math.Max(0f, _painCooldown - deltaTime);
            _coughCooldown = Math.Max(0f, _coughCooldown - deltaTime);
            _medicalCooldown = Math.Max(0f, _medicalCooldown - deltaTime);
            _impactShock = Math.Max(0f, _impactShock - deltaTime * 0.18f);

            float oxygenNoise = _manager.Lungs.WhiteNoiseIntensity;
            RingingIntensity = Config.Clamp(_manager.Brain.RingingIntensity + _impactShock * 0.75f, 0f, 1f);
            MuffleIntensity = Config.Clamp(_manager.Consciousness.BlackoutIntensity * 0.75f + _manager.AwarenessPenalty + _manager.Brain.DisorientationNormalized * 0.4f + _manager.Medication.RespiratoryDepression * 0.22f, 0f, 1f);
            float audioIntensity = Config.AudioIntensity;

            if (_pendingPainIntensity > 0.05f && _painCooldown <= 0f)
                PlayPain(_pendingPainIntensity);
            _pendingPainIntensity = Math.Max(0f, _pendingPainIntensity - deltaTime * 0.8f);

            if (_pendingFractureIntensity > 0.05f && _coughSource != null)
            {
                float volume = Config.Clamp(0.26f + _pendingFractureIntensity * 0.38f, 0f, 0.9f) * Config.AudioIntensity * Config.MasterVolume;
                float pitch = 0.72f + _pendingFractureIntensity * 0.18f;
                if (!_zCityBank.PlayRandomOneShot("Fracture", _coughSource, volume, pitch, "ZBC_ZCity_Fracture"))
                {
                    _coughSource.pitch = pitch;
                    _coughSource.volume = volume;
                    _coughSource.PlayOneShot(_fractureClip, _coughSource.volume);
                }

                _pendingFractureIntensity = 0f;
            }

            if (_pendingNeckCrackIntensity > 0.05f && _coughSource != null)
            {
                if (_neckCrackClip == null)
                {
                    _neckCrackPath ??= ExternalAudioClipLoader.ResolveAudioPath("bonerack", "neck_crack", "NeckCrack");
                    _neckCrackClip = ExternalAudioClipLoader.TryGetClip(_neckCrackPath, "ZBC_NeckCrack");
                }

                float volume = Config.Clamp(0.22f + _pendingNeckCrackIntensity * 0.34f, 0f, 0.82f) * Config.AudioIntensity * Config.MasterVolume;
                float pitch = Config.Clamp(0.82f - _pendingNeckCrackIntensity * 0.10f, 0.62f, 1.05f);
                if (!_zCityBank.PlayRandomOneShot("Fracture", _coughSource, volume, pitch, "ZBC_ZCity_NeckFracture"))
                {
                    AudioClip? clip = _neckCrackClip ?? _fractureClip;
                    _coughSource.pitch = pitch;
                    _coughSource.PlayOneShot(clip, volume);
                }

                _pendingNeckCrackIntensity = 0f;
            }

            if (_pendingMedicalIntensity > 0.05f && _medicalCooldown <= 0f && _coughSource != null)
            {
                float volume = Config.Clamp(0.20f + _pendingMedicalIntensity * 0.28f, 0f, 0.72f) * Config.AudioIntensity * Config.MasterVolume;
                float pitch = Config.Clamp(0.92f + _pendingMedicalIntensity * 0.08f, 0.86f, 1.08f);
                if (_zCityBank.PlayRandomOneShot("Medical", _coughSource, volume, pitch, "ZBC_ZCity_Medical"))
                    _medicalCooldown = 0.42f;
                _pendingMedicalIntensity = 0f;
            }

            if (_ringSource != null)
            {
                _ringSource.volume = MoveToward(_ringSource.volume, RingingIntensity * 0.12f * audioIntensity, deltaTime * 0.9f);
                _ringSource.pitch = 0.85f + RingingIntensity * 0.35f;
                EnsureLoop(_ringSource);
            }

            if (_noiseSource != null)
            {
                _noiseSource.volume = MoveToward(_noiseSource.volume, oxygenNoise * 0.32f * audioIntensity, deltaTime * 0.8f);
                _noiseSource.pitch = 0.82f + _manager.Lungs.BreathingPanic * 0.45f;
                EnsureLoop(_noiseSource);
            }

            if (_breathSource != null)
            {
                if (_zCityBreathingClip == null)
                    _zCityBreathingClip = _zCityBank.GetLongestClip("Breathing", "ZBC_ZCity_Breathing");
                if (_zCityBreathingClip != null && _breathSource.clip != _zCityBreathingClip)
                    _breathSource.clip = _zCityBreathingClip;

                float heavyBreathing = Config.Clamp(_manager.Stress.BreathingIntensity + _manager.Lungs.BreathingPanic * 0.45f + _manager.Shock.Intensity * 0.14f + _manager.Neck.BreathingStress * 0.45f + _manager.Medication.BreathingAudioStress * 0.42f + _manager.Rehabilitation.StaminaPenalty * 0.22f, 0f, 1.2f);
                _breathSource.volume = MoveToward(_breathSource.volume, heavyBreathing * 0.30f * audioIntensity, deltaTime * 0.75f);
                _breathSource.pitch = Config.Clamp(0.78f + heavyBreathing * 0.38f - _manager.Medication.RespiratoryDepression * 0.18f, 0.52f, 1.34f);
                EnsureLoop(_breathSource);
            }

            if (_coughSource != null && _coughCooldown <= 0f)
            {
                float coughRisk = Config.Clamp(_manager.Lungs.OxygenStress + _manager.InternalBleedingNormalized * 0.55f + (_manager.Bleeding.TotalBleedRateMlPerSecond / 80f) + _manager.Medication.RespiratoryDepression * 0.16f, 0f, 1f);
                if (coughRisk > 0.55f)
                {
                    _coughSource.pitch = 0.82f + coughRisk * 0.16f;
                    _coughSource.PlayOneShot(_coughClip, coughRisk * 0.38f * audioIntensity);
                    _coughCooldown = Mathf.Lerp(8f, 2.8f, coughRisk);
                }
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
                _breathSource = _rig.AddComponent<AudioSource>();
                _coughSource = _rig.AddComponent<AudioSource>();
                ConfigureSource(_painSource, _manager.Kind == HealthOwnerKind.Player, false);
                ConfigureSource(_ringSource, _manager.Kind == HealthOwnerKind.Player, true);
                ConfigureSource(_noiseSource, _manager.Kind == HealthOwnerKind.Player, true);
                ConfigureSource(_breathSource, _manager.Kind == HealthOwnerKind.Player, true);
                ConfigureSource(_coughSource, _manager.Kind == HealthOwnerKind.Player, false);
                _ringSource.clip = _ringClip;
                _noiseSource.clip = _noiseClip;
                _breathSource.clip = _breathClip;
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
            _painSource.volume = Config.Clamp((0.12f + intensity * 0.55f) * Config.AudioIntensity, 0f, 0.95f);
            _painSource.pitch = Config.Clamp(0.82f + (float)_manager.Random.NextDouble() * 0.26f - intensity * 0.1f, 0.62f, 1.2f);
            if (!_zCityBank.PlayRandomOneShot("Pain", _painSource, _painSource.volume * Config.MasterVolume, _painSource.pitch, "ZBC_ZCity_Pain"))
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

        private static AudioClip CreateSoftRingingClip()
        {
            const int sampleRate = 22050;
            int samples = sampleRate * 3;
            float[] data = new float[samples];
            uint seed = 0x9e3779b9u;
            float previous = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                seed ^= (uint)(i * 1103515245);
                seed *= 16777619u;
                float noise = (((seed >> 9) & 0xff) / 127.5f - 1f) * 0.035f;
                float softFlutter = Mathf.Sin(t * 790f * Mathf.PI * 2f) * 0.012f;
                previous = previous * 0.92f + (noise + softFlutter) * 0.08f;
                data[i] = previous;
            }

            AudioClip clip = AudioClip.Create("ZBC_SoftTinnitus", samples, 1, sampleRate, false);
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

        private static AudioClip CreateBreathingClip()
        {
            const int sampleRate = 22050;
            int samples = sampleRate * 3;
            float[] data = new float[samples];
            uint seed = 2166136261u;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)sampleRate;
                float breath = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 2f / 1.55f)), 1.8f);
                seed ^= (uint)i;
                seed *= 16777619u;
                float noise = (((seed >> 8) & 0xff) / 127.5f - 1f) * 0.035f;
                data[i] = (Mathf.Sin(t * 92f * Mathf.PI * 2f) * 0.06f + noise) * breath;
            }

            AudioClip clip = AudioClip.Create("ZBC_HeavyBreathing", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateCoughClip()
        {
            const int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.58f);
            float[] data = new float[samples];
            uint seed = 0x811c9dc5u;
            for (int i = 0; i < samples; i++)
            {
                float x = i / (float)samples;
                float envelope = Mathf.Sin(x * Mathf.PI);
                seed ^= (uint)(i * 16777619);
                seed *= 16777619u;
                float noise = (((seed >> 8) & 0xff) / 127.5f - 1f);
                data[i] = noise * envelope * (0.22f + Mathf.Sin(x * Mathf.PI * 8f) * 0.08f);
            }

            AudioClip clip = AudioClip.Create("ZBC_BloodCough", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip CreateFractureClip()
        {
            const int sampleRate = 22050;
            int samples = (int)(sampleRate * 0.22f);
            float[] data = new float[samples];
            uint seed = 1469598103u;
            for (int i = 0; i < samples; i++)
            {
                float x = i / (float)samples;
                float envelope = Mathf.Pow(1f - x, 2.4f);
                seed ^= (uint)(i + 0x9e3779b9u);
                seed *= 16777619u;
                float crack = (((seed >> 9) & 0xff) / 127.5f - 1f) * envelope;
                data[i] = crack * 0.38f + Mathf.Sin(i * 0.47f) * envelope * 0.08f;
            }

            AudioClip clip = AudioClip.Create("ZBC_FractureCrack", samples, 1, sampleRate, false);
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
