using System;
using System.IO;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class ZCityAudioSystem
    {
        private readonly HealthManager _manager;
        private GameObject? _root;
        private AudioSource? _unconsciousSource;
        private AudioSource? _painSource;
        private AudioSource? _headHitSource;
        private AudioSource? _deathSource;
        private AudioClip? _unconsciousClip;
        private AudioClip? _painClip;
        private AudioClip? _headHitClip;
        private AudioClip? _deathClip;
        private string? _unconsciousPath;
        private string? _painPath;
        private string? _headHitPath;
        private string? _deathPath;
        private float _headHitCooldown;
        private float _pendingHeadHitIntensity;
        private bool _initialized;
        private bool _hasUnconsciousTrack;
        private bool _hasPainTrack;
        private bool _hasHeadHitTrack;
        private bool _hasDeathTrack;
        private bool _warnedAboutMp4;
        private bool _deathSoundPlayed;

        public bool HasPainTrack => _hasPainTrack;

        public ZCityAudioSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            _headHitCooldown = 0f;
            _pendingHeadHitIntensity = 0f;
            _deathSoundPlayed = false;
            StopAndMute(_unconsciousSource);
            StopAndMute(_painSource);
            StopAndMute(_headHitSource);
            StopAndMute(_deathSource);
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _unconsciousSource = null;
            _painSource = null;
            _headHitSource = null;
            _deathSource = null;
            _unconsciousClip = null;
            _painClip = null;
            _headHitClip = null;
            _deathClip = null;
            _unconsciousPath = null;
            _painPath = null;
            _headHitPath = null;
            _deathPath = null;
            _initialized = false;
            _hasUnconsciousTrack = false;
            _hasPainTrack = false;
            _hasHeadHitTrack = false;
            _hasDeathTrack = false;
            _deathSoundPlayed = false;
        }

        public void OnDamage(DamageInfo info, OrganDamageFeedback feedback)
        {
            if (_manager.Kind != HealthOwnerKind.Player)
                return;

            if (info.BodyPart != BodyPart.Head && feedback.PrimaryOrgan != OrganType.Brain)
                return;

            float intensity = info.DamageType == AdvancedDamageType.Bullet
                ? Config.Clamp(0.65f + info.Damage / 85f, 0.65f, 1.2f)
                : Config.Clamp(0.35f + info.Damage / 95f, 0.35f, 1f);
            _pendingHeadHitIntensity = Math.Max(_pendingHeadHitIntensity, intensity);
            if (PlayHeadHit(intensity, true))
                _pendingHeadHitIntensity = 0f;
        }

        public void OnDeath(DeathCause cause)
        {
            if (_manager.Kind != HealthOwnerKind.Player || _deathSoundPlayed || !Config.CustomSoundsEnabled || Config.MasterVolume <= 0f || Config.DeathSoundVolume <= 0f)
                return;

            EnsureInitialized();
            EnsureSource(ref _deathSource, ref _deathClip, _deathPath, "DeathTrack", "AHS_ZCity_Dead", false);
            if (_deathSource == null)
                return;

            _deathSource.Stop();
            _deathSource.volume = Config.Clamp(0.82f * Config.AudioIntensity * Config.MasterVolume * Config.DeathSoundVolume, 0f, 1f);
            _deathSource.pitch = cause == DeathCause.CriticalHeadDamage ? 0.92f : 0.86f;
            _deathSource.time = 0f;
            _deathSource.Play();
            _deathSoundPlayed = true;
            MainMod.Runtime?.Logger.Msg("[ZBC] Death sound played");
        }

        public void Update(float deltaTime)
        {
            if (_manager.Kind != HealthOwnerKind.Player)
                return;

            Transform? head = MainMod.Runtime?.GetHeadTransform();
            if (head == null)
                return;

            EnsureInitialized();
            if (_root == null)
                return;

            if (_root.transform.parent != head)
            {
                _root.transform.SetParent(head, false);
                _root.transform.localPosition = Vector3.zero;
                _root.transform.localRotation = Quaternion.identity;
            }

            _headHitCooldown = Math.Max(0f, _headHitCooldown - deltaTime);
            PlayPendingHeadHit();

            if (!Config.CustomSoundsEnabled || !Config.RealisticAudioEnabled || Config.MasterVolume <= 0f)
            {
                UpdateLoop(_unconsciousSource, 0f, deltaTime, 0.65f, 0.96f);
                UpdateLoop(_painSource, 0f, deltaTime, 0.92f, 1.03f);
                return;
            }

            float unconsciousTarget = _manager.Consciousness.State == ConsciousnessState.Unconscious
                ? 0.68f
                : Config.Clamp((_manager.Consciousness.BlackoutIntensity - 0.42f) * 0.75f, 0f, 0.42f);
            float painTarget = _manager.Consciousness.State == ConsciousnessState.Dead
                ? 0f
                : Config.Clamp((_manager.PainNormalized - 0.30f) * 0.70f + _manager.Fractures.BreathingPenalty * 0.10f, 0f, 0.55f);

            if (unconsciousTarget > 0.015f)
                EnsureSource(ref _unconsciousSource, ref _unconsciousClip, _unconsciousPath, "UnconsciousTrack", "AHS_ZCity_Unconscious", true);
            if (painTarget > 0.015f)
                EnsureSource(ref _painSource, ref _painClip, _painPath, "PainTrack", "AHS_ZCity_Pain", true);
            UpdateLoop(_unconsciousSource, unconsciousTarget, deltaTime, 0.65f, 0.96f);
            UpdateLoop(_painSource, painTarget, deltaTime, 0.92f, 1.03f);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _unconsciousPath = ExternalAudioClipLoader.ResolveAudioPath("Unconscious", "zcity_unconscious");
            _painPath = ExternalAudioClipLoader.ResolveAudioPath("Pain", "zcity_pain");
            _headHitPath = ExternalAudioClipLoader.ResolveAudioPath("Headshot", "zcity_headshot", "zcity_headhit");
            _deathPath = ExternalAudioClipLoader.ResolveAudioPath("Dead", "zcity_dead");
            _hasUnconsciousTrack = !string.IsNullOrEmpty(_unconsciousPath);
            _hasPainTrack = !string.IsNullOrEmpty(_painPath);
            _hasHeadHitTrack = !string.IsNullOrEmpty(_headHitPath);
            _hasDeathTrack = !string.IsNullOrEmpty(_deathPath);

            WarnAboutMp4Tracks();
            PreloadTrack(_headHitPath, "AHS_ZCity_HeadHit");
            PreloadTrack(_deathPath, "AHS_ZCity_Dead");
            if (!_hasUnconsciousTrack && !_hasPainTrack && !_hasHeadHitTrack && !_hasDeathTrack)
            {
                _initialized = true;
                return;
            }

            _root = new GameObject("AHS_ZCityAudio");
            Transform? head = MainMod.Runtime?.GetHeadTransform();
            if (head != null)
            {
                _root.transform.SetParent(head, false);
                _root.transform.localPosition = Vector3.zero;
                _root.transform.localRotation = Quaternion.identity;
            }

            _initialized = true;
        }

        private void EnsureSource(ref AudioSource? source, ref AudioClip? clip, string? path, string sourceName, string clipName, bool loop)
        {
            if (source != null || string.IsNullOrEmpty(path))
                return;

            clip = ExternalAudioClipLoader.TryGetClip(path, clipName);
            if (clip != null)
                source = CreateSource(sourceName, clip, loop);
        }

        private static void PreloadTrack(string? path, string clipName)
        {
            if (!string.IsNullOrEmpty(path))
                ExternalAudioClipLoader.TryGetClip(path, clipName);
        }

        private AudioSource CreateSource(string name, AudioClip clip, bool loop)
        {
            GameObject go = new GameObject("AHS_" + name);
            go.transform.SetParent(_root != null ? _root.transform : null, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 12;
            source.bypassEffects = false;
            source.bypassListenerEffects = false;
            source.bypassReverbZones = true;
            return source;
        }

        private void PlayPendingHeadHit()
        {
            if (_pendingHeadHitIntensity <= 0f || _headHitCooldown > 0f)
                return;

            if (!PlayHeadHit(_pendingHeadHitIntensity, false))
                _pendingHeadHitIntensity = 0f;
            else
                _pendingHeadHitIntensity = 0f;
        }

        private bool PlayHeadHit(float intensity, bool force)
        {
            if (!Config.CustomSoundsEnabled || Config.MasterVolume <= 0f || Config.HeadshotSoundVolume <= 0f)
                return false;

            if (!force && _headHitCooldown > 0f)
                return true;

            EnsureInitialized();
            EnsureSource(ref _headHitSource, ref _headHitClip, _headHitPath, "HeadHitTrack", "AHS_ZCity_HeadHit", false);
            if (_headHitSource == null)
                return false;

            intensity = Config.Clamp(intensity, 0f, 1.25f);
            _headHitSource.Stop();
            _headHitSource.volume = Config.Clamp(Mathf.Lerp(0.45f, 0.92f, intensity) * Config.AudioIntensity * Config.MasterVolume * Config.HeadshotSoundVolume, 0f, 1f);
            _headHitSource.pitch = Mathf.Lerp(0.92f, 0.72f, intensity);
            _headHitSource.time = 0f;
            _headHitSource.Play();
            _headHitCooldown = force ? 0.04f : Mathf.Lerp(1.25f, 3.0f, intensity);
            MainMod.Runtime?.Logger.Msg("[ZBC] Headshot sound played");
            return true;
        }

        private static void UpdateLoop(AudioSource? source, float targetVolume, float deltaTime, float minPitch, float maxPitch)
        {
            if (source == null)
                return;

            source.volume = MoveToward(source.volume, targetVolume * Config.AudioIntensity * Config.MasterVolume, deltaTime * 0.55f);
            source.pitch = Mathf.Lerp(minPitch, maxPitch, Config.Clamp(targetVolume * 1.8f, 0f, 1f));

            if (source.volume > 0.015f)
            {
                if (!source.isPlaying)
                    source.Play();
            }
            else if (source.isPlaying)
            {
                source.Pause();
            }
        }

        private void WarnAboutMp4Tracks()
        {
            if (_warnedAboutMp4)
                return;

            string[] folders = ExternalAudioClipLoader.GetAudioFolders();
            bool hasMp4 = false;
            for (int i = 0; i < folders.Length && !hasMp4; i++)
            {
                string folder = folders[i];
                hasMp4 = File.Exists(Path.Combine(folder, "zcity_unconscious.mp4")) ||
                         File.Exists(Path.Combine(folder, "zcity_pain.mp4")) ||
                         File.Exists(Path.Combine(folder, "zcity_headhit.mp4"));
            }

            bool hasWav = _hasUnconsciousTrack || _hasPainTrack || _hasHeadHitTrack;
            if (hasMp4 && !hasWav)
            {
                MainMod.Runtime?.Logger.Warning("MP4 trauma tracks are ignored for stability. Use WAV files named zcity_unconscious.wav, zcity_pain.wav and zcity_headhit.wav.");
                _warnedAboutMp4 = true;
            }
        }

        private static void StopAndMute(AudioSource? source)
        {
            if (source == null)
                return;
            source.Stop();
            source.volume = 0f;
        }

        private static float MoveToward(float value, float target, float maxDelta)
        {
            if (value < target)
                return Mathf.Min(value + maxDelta, target);
            return Mathf.Max(value - maxDelta, target);
        }

        private static class WavAudioLoader
        {
            public static AudioClip Load(string path, string clipName)
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length < 44 || ReadFourCc(bytes, 0) != "RIFF" || ReadFourCc(bytes, 8) != "WAVE")
                    throw new InvalidDataException("Not a RIFF/WAVE file.");

                int offset = 12;
                int fmtOffset = -1;
                int fmtSize = 0;
                int dataOffset = -1;
                int dataSize = 0;
                while (offset + 8 <= bytes.Length)
                {
                    string chunk = ReadFourCc(bytes, offset);
                    int size = ReadInt32(bytes, offset + 4);
                    int payload = offset + 8;
                    if (size < 0 || payload + size > bytes.Length)
                        break;

                    if (chunk == "fmt ")
                    {
                        fmtOffset = payload;
                        fmtSize = size;
                    }
                    else if (chunk == "data")
                    {
                        dataOffset = payload;
                        dataSize = size;
                    }

                    offset = payload + size + (size & 1);
                }

                if (fmtOffset < 0 || dataOffset < 0 || fmtSize < 16)
                    throw new InvalidDataException("Missing fmt or data chunk.");

                ushort format = ReadUInt16(bytes, fmtOffset);
                ushort channels = ReadUInt16(bytes, fmtOffset + 2);
                int sampleRate = ReadInt32(bytes, fmtOffset + 4);
                ushort bitsPerSample = ReadUInt16(bytes, fmtOffset + 14);
                if (channels == 0 || sampleRate <= 0)
                    throw new InvalidDataException("Invalid WAV stream format.");
                if (format != 1 && format != 3)
                    throw new InvalidDataException("Only PCM and IEEE float WAV files are supported.");
                if (bitsPerSample != 8 && bitsPerSample != 16 && bitsPerSample != 24 && bitsPerSample != 32)
                    throw new InvalidDataException("Unsupported bit depth: " + bitsPerSample);
                if (format == 3 && bitsPerSample != 32)
                    throw new InvalidDataException("Float WAV must be 32-bit.");

                int bytesPerSample = bitsPerSample / 8;
                int sampleCount = dataSize / bytesPerSample;
                int frameCount = sampleCount / channels;
                float[] samples = new float[sampleCount];
                int cursor = dataOffset;
                for (int i = 0; i < sampleCount; i++)
                {
                    samples[i] = ReadSample(bytes, cursor, format, bitsPerSample);
                    cursor += bytesPerSample;
                }

                AudioClip clip = AudioClip.Create(clipName, frameCount, channels, sampleRate, false);
                clip.SetData(samples, 0);
                return clip;
            }

            private static float ReadSample(byte[] bytes, int offset, ushort format, ushort bits)
            {
                if (format == 3)
                    return BitConverter.ToSingle(bytes, offset);

                switch (bits)
                {
                    case 8:
                        return (bytes[offset] - 128) / 128f;
                    case 16:
                        return ReadInt16(bytes, offset) / 32768f;
                    case 24:
                        int value = bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16);
                        if ((value & 0x800000) != 0)
                            value |= unchecked((int)0xff000000);
                        return value / 8388608f;
                    case 32:
                        return ReadInt32(bytes, offset) / 2147483648f;
                    default:
                        return 0f;
                }
            }

            private static string ReadFourCc(byte[] bytes, int offset)
            {
                return new string(new[]
                {
                    (char)bytes[offset],
                    (char)bytes[offset + 1],
                    (char)bytes[offset + 2],
                    (char)bytes[offset + 3]
                });
            }

            private static ushort ReadUInt16(byte[] bytes, int offset)
            {
                return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
            }

            private static short ReadInt16(byte[] bytes, int offset)
            {
                return (short)(bytes[offset] | (bytes[offset + 1] << 8));
            }

            private static int ReadInt32(byte[] bytes, int offset)
            {
                return bytes[offset] |
                       (bytes[offset + 1] << 8) |
                       (bytes[offset + 2] << 16) |
                       (bytes[offset + 3] << 24);
            }
        }
    }
}
