using System;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Combat;
using Il2CppSLZ.Marrow.Data;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class ImpactAudioSystem
    {
        private const int SourceCount = 8;
        private readonly AudioSource[] _sources = new AudioSource[SourceCount];
        private GameObject? _root;
        private string? _metalPath;
        private AudioClip? _metalClip;
        private int _nextSource;
        private bool _initialized;

        public void Reset()
        {
            for (int i = 0; i < _sources.Length; i++)
            {
                if (_sources[i] != null)
                    _sources[i].Stop();
            }
        }

        public void Destroy()
        {
            if (_root != null)
                UnityEngine.Object.Destroy(_root);
            _root = null;
            _metalClip = null;
            _metalPath = null;
            _initialized = false;
            _nextSource = 0;
            for (int i = 0; i < _sources.Length; i++)
                _sources[i] = null!;
        }

        public void TryPlayMetalImpact(ImpactProperties? impactProperties, Attack attack)
        {
            if (!Config.CustomSoundsEnabled || Config.MasterVolume <= 0f || Config.MetalImpactSoundVolume <= 0f || impactProperties == null || attack == null)
                return;
            if (attack.attackType != AttackType.Piercing)
                return;
            if (!LooksMetal(impactProperties))
                return;

            EnsureInitialized();
            if (string.IsNullOrEmpty(_metalPath))
                return;

            _metalClip ??= ExternalAudioClipLoader.TryGetClip(_metalPath, "AHS_MetalShot");
            if (_metalClip == null)
                return;

            AudioSource source = GetNextSource();
            Transform transform = impactProperties.transform;
            Vector3 position = transform != null ? transform.position : DamageProcessor.SanitizeVector(attack.origin, Vector3.zero);
            if (position.sqrMagnitude < 0.001f)
                position = DamageProcessor.SanitizeVector(attack.origin, Vector3.zero);

            float damage = DamageProcessor.SanitizeFloat(Math.Max(1f, attack.damage));
            source.transform.position = position;
            source.clip = _metalClip;
            Configure3dMetalSource(source);
            source.volume = Config.Clamp((0.24f + damage / 130f) * Config.AudioIntensity * Config.MasterVolume * Config.MetalImpactSoundVolume, 0f, 1f);
            source.pitch = UnityEngine.Random.Range(0.92f, 1.08f);
            source.Stop();
            source.Play();
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _initialized = true;
            _metalPath = ExternalAudioClipLoader.ResolveAudioPath("MetalShot", "zcity_metalshot", "metalshot");
            if (string.IsNullOrEmpty(_metalPath))
                return;

            _root = new GameObject("AHS_ImpactAudio");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            for (int i = 0; i < _sources.Length; i++)
            {
                GameObject go = new GameObject("AHS_MetalImpact_" + i);
                go.transform.SetParent(_root.transform, false);
                AudioSource source = go.AddComponent<AudioSource>();
                Configure3dMetalSource(source);
                _sources[i] = source;
            }

            ExternalAudioClipLoader.TryGetClip(_metalPath, "AHS_MetalShot");
        }

        private AudioSource GetNextSource()
        {
            AudioSource source = _sources[_nextSource++ % _sources.Length];
            if (source != null)
                return source;

            EnsureInitialized();
            return _sources[0];
        }

        private static void Configure3dMetalSource(AudioSource source)
        {
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.priority = 64;
            source.minDistance = 1f;
            source.maxDistance = 10f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.dopplerLevel = 0f;
            source.spread = 0f;
        }

        private static bool LooksMetal(ImpactProperties impactProperties)
        {
            Transform? current = impactProperties.transform;
            for (int depth = 0; depth < 5 && current != null; depth++)
            {
                if (ContainsMetalToken(current.name))
                    return true;
                current = current.parent;
            }

            Collider collider = impactProperties.GetComponent<Collider>();
            if (collider != null && collider.sharedMaterial != null && ContainsMetalToken(collider.sharedMaterial.name))
                return true;

            Renderer renderer = impactProperties.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = renderer.sharedMaterial;
                if (material != null && ContainsMetalToken(material.name))
                    return true;
            }

            return false;
        }

        private static bool ContainsMetalToken(string? value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("metal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("steel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("iron", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("aluminum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("pipe", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("container", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
