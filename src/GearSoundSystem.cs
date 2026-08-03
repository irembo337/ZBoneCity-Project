using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class GearSoundSystem
    {
        private const int GearClipCount = 6;
        private const float MovementStartSpeed = 0.18f;
        private const float MovementFullSpeed = 3.2f;
        private const float SlowInterval = 1.35f;
        private const float FastInterval = 0.32f;

        private readonly AudioClip?[] _clips = new AudioClip?[GearClipCount];
        private readonly string?[] _paths = new string?[GearClipCount];
        private GameObject? _root;
        private AudioSource? _source;
        private Vector3 _lastBodyPosition;
        private float _smoothedSpeed;
        private float _cadenceTimer;
        private int _nextClipIndex;
        private bool _hasLastPosition;
        private bool _pathsResolved;

        public void Initialize()
        {
            ResolvePaths();
        }

        public void Update(float deltaTime, HealthManager? player)
        {
            if (!CanRun(deltaTime, player))
            {
                EaseToStop(deltaTime);
                return;
            }

            if (!TryGetBodyPosition(out Vector3 bodyPosition))
            {
                EaseToStop(deltaTime);
                return;
            }

            EnsureRuntimeObjects();
            if (_source == null)
                return;

            float speed = MeasureHorizontalSpeed(bodyPosition, deltaTime);
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, speed, 1f - Mathf.Exp(-deltaTime * 8.0f));
            if (_smoothedSpeed < MovementStartSpeed)
            {
                _cadenceTimer = Math.Min(_cadenceTimer, SlowInterval);
                return;
            }

            float speed01 = Config.Clamp((_smoothedSpeed - MovementStartSpeed) / (MovementFullSpeed - MovementStartSpeed), 0f, 1f);
            float interval = Mathf.Lerp(SlowInterval, FastInterval, speed01) / Config.GearSoundSpeedMultiplier;
            _cadenceTimer -= deltaTime;
            if (_cadenceTimer > 0f)
                return;

            PlayNext(bodyPosition, speed01);
            _cadenceTimer = interval;
        }

        public void PlayRemote(Vector3 bodyPosition, int clipIndex, float speed01)
        {
            if (!Config.Enabled || !Config.CustomSoundsEnabled || !Config.GearSoundsEnabled)
                return;

            EnsureRuntimeObjects();
            PlayClip(bodyPosition, clipIndex, Config.Clamp(speed01, 0f, 1f), false);
        }

        public void Reset()
        {
            _hasLastPosition = false;
            _smoothedSpeed = 0f;
            _cadenceTimer = 0f;
            _nextClipIndex = 0;
            if (_source != null)
                _source.Stop();
        }

        public void Destroy()
        {
            Reset();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _source = null;
            _pathsResolved = false;
        }

        private bool CanRun(float deltaTime, HealthManager? player)
        {
            if (deltaTime <= 0f || player == null)
                return false;
            if (!Config.Enabled || !Config.CustomSoundsEnabled || !Config.GearSoundsEnabled)
                return false;
            if (Config.MasterVolume <= 0f || Config.GearVolume <= 0f)
                return false;
            if (!(MainMod.Runtime?.IsPlayerRigReady() ?? false))
                return false;
            if (MainMod.Runtime?.CanRunPlayerTrauma == false)
                return false;
            if (player.IsDead || player.Coma.IsActive || player.Consciousness.State != ConsciousnessState.Awake)
                return false;

            return true;
        }

        private void PlayNext(Vector3 bodyPosition, float speed01)
        {
            int clipIndex = _nextClipIndex;
            _nextClipIndex = (_nextClipIndex + 1) % GearClipCount;
            if (PlayClip(bodyPosition, clipIndex, speed01, true))
                MainMod.Runtime?.NotifyFusionGearSound(clipIndex, bodyPosition, speed01);
        }

        private bool PlayClip(Vector3 bodyPosition, int clipIndex, float speed01, bool local)
        {
            if (clipIndex < 0 || clipIndex >= GearClipCount)
                return false;

            AudioClip? clip = GetClip(clipIndex);
            if (clip == null || _source == null)
                return false;

            _source.transform.position = bodyPosition;
            _source.maxDistance = Config.GearDistance;
            _source.pitch = Mathf.Lerp(0.92f, 1.08f, speed01);
            float volume = Mathf.Lerp(0.20f, 0.72f, speed01) * Config.GearVolume * Config.MasterVolume;
            _source.PlayOneShot(clip, Config.Clamp(volume, 0f, 1f));

            if (local && Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("[ZBC] Gear sound played: Gear" + (clipIndex + 1) + " speed=" + _smoothedSpeed.ToString("0.00"));

            return true;
        }

        private AudioClip? GetClip(int index)
        {
            AudioClip? clip = _clips[index];
            if (clip != null)
                return clip;

            ResolvePaths();
            string? path = _paths[index];
            if (string.IsNullOrEmpty(path))
                return null;

            clip = ExternalAudioClipLoader.TryGetClip(path, "ZBC_Gear_" + (index + 1));
            _clips[index] = clip;
            return clip;
        }

        private void ResolvePaths()
        {
            if (_pathsResolved)
                return;

            _pathsResolved = true;
            for (int i = 0; i < GearClipCount; i++)
            {
                string padded = (i + 1).ToString("00");
                _paths[i] = ExternalAudioClipLoader.ResolveAudioPath(
                    "ZCity\\Gear\\gear" + padded,
                    "Gear\\gear" + (i + 1),
                    "gear" + (i + 1),
                    "Gear" + (i + 1));
            }
        }

        private void EnsureRuntimeObjects()
        {
            ResolvePaths();
            if (_root != null && _source != null)
                return;

            if (_root == null)
            {
                _root = new GameObject("ZBC_GearSounds");
                UnityEngine.Object.DontDestroyOnLoad(_root);
            }

            if (_source != null)
                return;

            _source = _root.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 1f;
            _source.minDistance = 0.65f;
            _source.maxDistance = Config.GearDistance;
            _source.rolloffMode = AudioRolloffMode.Logarithmic;
            _source.dopplerLevel = 0.05f;
            _source.priority = 36;
            _source.bypassReverbZones = false;
        }

        private float MeasureHorizontalSpeed(Vector3 bodyPosition, float deltaTime)
        {
            if (!_hasLastPosition)
            {
                _hasLastPosition = true;
                _lastBodyPosition = bodyPosition;
                return 0f;
            }

            Vector3 delta = bodyPosition - _lastBodyPosition;
            _lastBodyPosition = bodyPosition;
            delta.y = 0f;
            return delta.magnitude / Math.Max(0.0001f, deltaTime);
        }

        private void EaseToStop(float deltaTime)
        {
            _hasLastPosition = false;
            _smoothedSpeed = Mathf.MoveTowards(_smoothedSpeed, 0f, deltaTime * 4.0f);
            _cadenceTimer = Math.Min(_cadenceTimer, SlowInterval);
        }

        private static bool TryGetBodyPosition(out Vector3 bodyPosition)
        {
            bodyPosition = Vector3.zero;
            MainMod? runtime = MainMod.Runtime;
            if (runtime == null)
                return false;

            bool hasFeet = runtime.TryGetPlayerFeetPosition(out Vector3 feet);
            Transform? head = runtime.GetHeadTransform();
            if (head != null && hasFeet)
            {
                bodyPosition = Vector3.Lerp(feet, head.position, 0.58f);
                return true;
            }

            if (hasFeet)
            {
                bodyPosition = feet + Vector3.up * 1.05f;
                return true;
            }

            if (head != null)
            {
                bodyPosition = head.position + Vector3.down * 0.42f;
                return true;
            }

            return false;
        }
    }
}
