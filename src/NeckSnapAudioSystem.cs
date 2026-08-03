using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class NeckSnapAudioSystem
    {
        private const float RepeatGuardSeconds = 0.75f;

        private GameObject? _root;
        private AudioSource? _source;
        private AudioClip? _clip;
        private string? _clipPath;
        private float _lastPlayRealtime = -100f;
        private bool _missingLogged;

        public void PlayAt(Vector3 position)
        {
            if (!Config.Enabled || !Config.NeckSnapSoundEnabled || !Config.CustomSoundsEnabled || Config.MasterVolume <= 0f)
                return;

            float now = Time.realtimeSinceStartup;
            if (now - _lastPlayRealtime < RepeatGuardSeconds)
                return;

            EnsureSource();
            EnsureClip();
            if (_source == null || _clip == null)
                return;

            if (_source.isPlaying)
                return;

            _lastPlayRealtime = now;
            _source.transform.position = position;
            _source.clip = _clip;
            _source.volume = Config.Clamp(0.9f * Config.MasterVolume * Config.AudioIntensity, 0f, 1f);
            _source.pitch = 1f;
            _source.Play();

            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("[ZBC] Neck snap sound played");
        }

        public void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);

            _root = null;
            _source = null;
            _clip = null;
            _clipPath = null;
            _missingLogged = false;
            _lastPlayRealtime = -100f;
        }

        private void EnsureSource()
        {
            if (_source != null)
                return;

            _root = new GameObject("ZBC_NeckSnapAudio");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            _source = _root.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 1f;
            _source.minDistance = 1f;
            _source.maxDistance = 10f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.dopplerLevel = 0f;
        }

        private void EnsureClip()
        {
            if (_clip != null)
                return;

            _clipPath ??= ExternalAudioClipLoader.ResolveAudioPath("snapcracklepop");
            _clip = ExternalAudioClipLoader.TryGetClip(_clipPath, "ZBC_NeckSnap_SnapCracklePop");
            if (_clip == null && !_missingLogged)
            {
                _missingLogged = true;
                MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Failed to load neck snap sound: snapcracklepop.wav");
            }
        }
    }
}
