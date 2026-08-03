using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class RandomDeathSoundSystem
    {
        private const string DeathFolderName = "Death";
        private const int PoolSize = 8;
        private const float DuplicateWindowSeconds = 4.0f;

        private readonly List<AudioSource> _sources = new List<AudioSource>(PoolSize);
        private readonly Dictionary<int, float> _recentDeaths = new Dictionary<int, float>(32);
        private string[] _clipPaths = Array.Empty<string>();
        private AudioClip?[] _clips = Array.Empty<AudioClip?>();
        private GameObject? _root;
        private int _lastClipIndex = -1;
        private bool _pathsScanned;

        public void Initialize()
        {
            ScanClipPaths();
            for (int i = 0; i < _clipPaths.Length; i++)
                GetClip(i);
        }

        public int ChooseClipIndex()
        {
            ScanClipPaths();
            int count = _clipPaths.Length;
            if (count <= 0)
                return -1;

            if (count == 1)
                return 0;

            int index = UnityEngine.Random.Range(0, count);
            if (index == _lastClipIndex)
                index = (index + UnityEngine.Random.Range(1, count)) % count;

            _lastClipIndex = index;
            return index;
        }

        public void PlayForDeath(HealthManager manager, DeathCause cause)
        {
            if (!CanPlay(manager))
                return;

            int clipIndex = ChooseClipIndex();
            if (clipIndex < 0)
                return;

            Vector3 position = GetDeathSoundPosition(manager);
            if (PlayClipIndex(position, clipIndex, cause))
                MainMod.Runtime?.NotifyFusionDeathSound(manager, cause, clipIndex, position);
        }

        public void PlayRemote(Vector3 position, int clipIndex, DeathCause cause)
        {
            if (!Config.Enabled || !Config.CustomSoundsEnabled || Config.MasterVolume <= 0f || Config.DeathSoundVolume <= 0f)
                return;

            PlayClipIndex(position, clipIndex, cause);
        }

        public void Reset()
        {
            _recentDeaths.Clear();
            for (int i = 0; i < _sources.Count; i++)
            {
                AudioSource source = _sources[i];
                if (source == null)
                    continue;

                source.Stop();
                source.clip = null;
            }
        }

        public void Destroy()
        {
            Reset();
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _sources.Clear();
        }

        private bool CanPlay(HealthManager manager)
        {
            if (!Config.Enabled || !Config.CustomSoundsEnabled || Config.MasterVolume <= 0f || Config.DeathSoundVolume <= 0f)
                return false;

            int key = GetDeathKey(manager);
            float now = Time.realtimeSinceStartup;
            if (_recentDeaths.TryGetValue(key, out float lastTime) && now - lastTime < DuplicateWindowSeconds)
                return false;

            _recentDeaths[key] = now;
            CleanupRecentDeaths(now);
            return true;
        }

        private bool PlayClipIndex(Vector3 position, int clipIndex, DeathCause cause)
        {
            ScanClipPaths();
            if (clipIndex < 0 || clipIndex >= _clipPaths.Length)
                return false;

            AudioClip? clip = GetClip(clipIndex);
            if (clip == null)
                return false;

            AudioSource? source = GetFreeSource();
            if (source == null)
            {
                if (Config.DebugMode)
                    MainMod.Runtime?.Logger.Warning("[ZBC] Random death sound skipped: audio pool is busy.");
                return false;
            }

            source.transform.position = position;
            source.clip = clip;
            source.volume = Config.Clamp(0.92f * Config.MasterVolume * Config.DeathSoundVolume * Config.AudioIntensity, 0f, 1f);
            source.pitch = cause == DeathCause.CriticalHeadDamage ? 0.96f : 1.0f;
            source.time = 0f;
            source.Play();

            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("[ZBC] Random death sound played: " + Path.GetFileName(_clipPaths[clipIndex]) + " index=" + clipIndex);

            return true;
        }

        private AudioClip? GetClip(int index)
        {
            if (index < 0 || index >= _clipPaths.Length)
                return null;

            if (_clips.Length != _clipPaths.Length)
                _clips = new AudioClip?[_clipPaths.Length];

            AudioClip? clip = _clips[index];
            if (clip != null)
                return clip;

            string path = _clipPaths[index];
            clip = ExternalAudioClipLoader.TryGetClip(path, "ZBC_Death_" + index.ToString("00"));
            _clips[index] = clip;
            return clip;
        }

        private AudioSource? GetFreeSource()
        {
            EnsureRoot();
            for (int i = 0; i < _sources.Count; i++)
            {
                AudioSource source = _sources[i];
                if (source != null && !source.isPlaying)
                    return source;
            }

            return null;
        }

        private void EnsureRoot()
        {
            if (_root != null)
                return;

            _root = new GameObject("ZBC_RandomDeathSounds");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            for (int i = 0; i < PoolSize; i++)
                _sources.Add(CreateSource(i));
        }

        private AudioSource CreateSource(int index)
        {
            GameObject sourceObject = new GameObject("ZBC_DeathVoice_" + index.ToString("00"));
            sourceObject.transform.SetParent(_root != null ? _root.transform : null, false);
            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 1f;
            source.minDistance = 1.0f;
            source.maxDistance = 18.0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0.15f;
            source.priority = 24;
            source.bypassReverbZones = false;
            return source;
        }

        private void ScanClipPaths()
        {
            if (_pathsScanned)
                return;

            _pathsScanned = true;
            List<string> paths = new List<string>(64);
            string[] roots = ExternalAudioClipLoader.GetAudioFolders();
            for (int i = 0; i < roots.Length; i++)
            {
                AddDeathFiles(paths, Path.Combine(roots[i], "ZCity", DeathFolderName));
                AddDeathFiles(paths, Path.Combine(roots[i], DeathFolderName));
                AddDeathFiles(paths, roots[i]);
            }

            paths.Sort(CompareDeathFileNames);
            _clipPaths = paths.ToArray();
            _clips = new AudioClip?[_clipPaths.Length];

            if (_clipPaths.Length == 0)
                MainMod.Runtime?.Logger.Warning("[ZBC ERROR] No random death sounds found in ZBoneCity/Audio/Death.");
            else if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("[ZBC] Random death sound pool loaded: " + _clipPaths.Length + " files");
        }

        private static void AddDeathFiles(List<string> paths, string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            string[] files = Directory.GetFiles(folder, "death*.*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string extension = Path.GetExtension(files[i]).ToLowerInvariant();
                if (extension != ".wav" && extension != ".mp3" && extension != ".ogg")
                    continue;

                string fullPath = Path.GetFullPath(files[i]);
                bool exists = false;
                for (int j = 0; j < paths.Count; j++)
                {
                    if (string.Equals(paths[j], fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    paths.Add(fullPath);
            }
        }

        private static int CompareDeathFileNames(string left, string right)
        {
            int leftNumber = ExtractTrailingNumber(Path.GetFileNameWithoutExtension(left));
            int rightNumber = ExtractTrailingNumber(Path.GetFileNameWithoutExtension(right));
            if (leftNumber != rightNumber)
                return leftNumber.CompareTo(rightNumber);

            return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static int ExtractTrailingNumber(string value)
        {
            int multiplier = 1;
            int number = 0;
            bool found = false;
            for (int i = value.Length - 1; i >= 0; i--)
            {
                char c = value[i];
                if (c < '0' || c > '9')
                    break;

                found = true;
                number += (c - '0') * multiplier;
                multiplier *= 10;
            }

            return found ? number : int.MaxValue;
        }

        private static Vector3 GetDeathSoundPosition(HealthManager manager)
        {
            if (manager is NPCHealth npc)
            {
                Transform? npcTransform = npc.GetEffectTransform();
                if (npcTransform != null)
                    return npcTransform.position + Vector3.up * 0.65f;
            }

            if (manager.Kind == HealthOwnerKind.Player)
            {
                Transform? head = MainMod.Runtime?.GetHeadTransform();
                if (head != null)
                    return head.position;

                if (MainMod.Runtime != null && MainMod.Runtime.TryGetPlayerFeetPosition(out Vector3 feet))
                    return feet + Vector3.up * 1.45f;
            }

            return Vector3.zero;
        }

        private static int GetDeathKey(HealthManager manager)
        {
            unchecked
            {
                return ((int)manager.Kind * 397) ^ manager.OwnerId;
            }
        }

        private void CleanupRecentDeaths(float now)
        {
            if (_recentDeaths.Count <= 64)
                return;

            List<int> stale = new List<int>();
            foreach (KeyValuePair<int, float> pair in _recentDeaths)
            {
                if (now - pair.Value > DuplicateWindowSeconds)
                    stale.Add(pair.Key);
            }

            for (int i = 0; i < stale.Count; i++)
                _recentDeaths.Remove(stale[i]);
        }
    }
}
