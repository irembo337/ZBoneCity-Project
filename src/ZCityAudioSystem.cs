using System;
using System.IO;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.Video;

namespace BonelabAdvancedHealth
{
    public sealed class ZCityAudioSystem
    {
        private const string AudioFolderName = "BonelabAdvancedHealth\\Audio";
        private readonly HealthManager _manager;
        private GameObject? _root;
        private AudioSource? _unconsciousSource;
        private AudioSource? _painSource;
        private AudioSource? _headHitSource;
        private VideoPlayer? _unconsciousPlayer;
        private VideoPlayer? _painPlayer;
        private VideoPlayer? _headHitPlayer;
        private float _headHitCooldown;
        private bool _initialized;
        private bool _hasUnconsciousTrack;
        private bool _hasPainTrack;
        private bool _hasHeadHitTrack;

        public bool HasPainTrack => _hasPainTrack;

        public ZCityAudioSystem(HealthManager manager)
        {
            _manager = manager;
        }

        public void Reset()
        {
            _headHitCooldown = 0f;
            if (_unconsciousSource != null)
                _unconsciousSource.volume = 0f;
            if (_painSource != null)
                _painSource.volume = 0f;
            if (_headHitSource != null)
                _headHitSource.volume = 0f;
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
            _unconsciousPlayer = null;
            _painPlayer = null;
            _headHitPlayer = null;
            _initialized = false;
        }

        public void OnDamage(DamageInfo info, OrganDamageFeedback feedback)
        {
            if (_manager.Kind != HealthOwnerKind.Player)
                return;

            EnsureInitialized();
            if (!_hasHeadHitTrack || _headHitPlayer == null || _headHitSource == null || _headHitCooldown > 0f)
                return;

            if (info.BodyPart != BodyPart.Head && feedback.PrimaryOrgan != OrganType.Brain)
                return;

            float intensity = Config.Clamp(0.35f + info.Damage / 95f, 0.35f, 1f);
            _headHitSource.volume = Mathf.Lerp(0.45f, 0.92f, intensity);
            _headHitSource.pitch = Mathf.Lerp(0.92f, 0.72f, intensity);
            _headHitPlayer.Stop();
            _headHitPlayer.time = 0.0;
            _headHitPlayer.Play();
            _headHitCooldown = Mathf.Lerp(1.25f, 3.0f, intensity);
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

            float unconsciousTarget = _manager.Consciousness.State == ConsciousnessState.Unconscious
                ? 0.68f
                : Config.Clamp((_manager.Consciousness.BlackoutIntensity - 0.42f) * 0.75f, 0f, 0.42f);
            float painTarget = _manager.Consciousness.State == ConsciousnessState.Dead
                ? 0f
                : Config.Clamp((_manager.PainNormalized - 0.30f) * 0.70f + _manager.Fractures.BreathingPenalty * 0.10f, 0f, 0.55f);

            UpdateLoop(_unconsciousPlayer, _unconsciousSource, _hasUnconsciousTrack, unconsciousTarget, deltaTime, 0.65f, 0.96f);
            UpdateLoop(_painPlayer, _painSource, _hasPainTrack, painTarget, deltaTime, 0.92f, 1.03f);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            string folder = GetAudioFolder();
            string unconscious = Path.Combine(folder, "zcity_unconscious.mp4");
            string pain = Path.Combine(folder, "zcity_pain.mp4");
            string headHit = Path.Combine(folder, "zcity_headhit.mp4");
            _hasUnconsciousTrack = File.Exists(unconscious);
            _hasPainTrack = File.Exists(pain);
            _hasHeadHitTrack = File.Exists(headHit);

            if (!_hasUnconsciousTrack && !_hasPainTrack && !_hasHeadHitTrack)
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

            if (_hasUnconsciousTrack)
                CreateTrack("UnconsciousTrack", unconscious, true, out _unconsciousPlayer, out _unconsciousSource);
            if (_hasPainTrack)
                CreateTrack("PainTrack", pain, true, out _painPlayer, out _painSource);
            if (_hasHeadHitTrack)
                CreateTrack("HeadHitTrack", headHit, false, out _headHitPlayer, out _headHitSource);

            _initialized = true;
        }

        private void CreateTrack(string name, string path, bool loop, out VideoPlayer player, out AudioSource source)
        {
            GameObject go = new GameObject("AHS_" + name);
            go.transform.SetParent(_root != null ? _root.transform : null, false);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.priority = 12;

            player = go.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.Url;
            player.url = path.Replace('\\', '/');
            player.isLooping = loop;
            player.skipOnDrop = true;
            player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            player.controlledAudioTrackCount = 1;
            player.EnableAudioTrack(0, true);
            player.SetTargetAudioSource(0, source);
        }

        private static void UpdateLoop(VideoPlayer? player, AudioSource? source, bool hasTrack, float targetVolume, float deltaTime, float minPitch, float maxPitch)
        {
            if (!hasTrack || player == null || source == null)
                return;

            source.volume = MoveToward(source.volume, targetVolume, deltaTime * 0.55f);
            source.pitch = Mathf.Lerp(minPitch, maxPitch, Config.Clamp(targetVolume * 1.8f, 0f, 1f));

            if (source.volume > 0.015f)
            {
                if (!player.isPlaying)
                    player.Play();
            }
            else if (player.isPlaying)
            {
                player.Pause();
            }
        }

        private static string GetAudioFolder()
        {
            string folder = Path.Combine(MelonEnvironment.UserDataDirectory, AudioFolderName);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        private static float MoveToward(float value, float target, float maxDelta)
        {
            if (value < target)
                return Mathf.Min(value + maxDelta, target);
            return Mathf.Max(value - maxDelta, target);
        }
    }
}
