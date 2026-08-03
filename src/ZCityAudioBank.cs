using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class ZCityAudioBank
    {
        private sealed class ClipGroup
        {
            public string[] Paths = Array.Empty<string>();
            public AudioClip?[] Clips = Array.Empty<AudioClip?>();
            public int LastIndex = -1;
            public bool Scanned;
        }

        private static readonly Dictionary<string, ClipGroup> Groups = new Dictionary<string, ClipGroup>(StringComparer.OrdinalIgnoreCase);

        public int Count(string category)
        {
            return GetGroup(category).Paths.Length;
        }

        public void Preload(string category)
        {
            ClipGroup group = GetGroup(category);
            for (int i = 0; i < group.Paths.Length; i++)
                GetClip(group, category, i);
        }

        public AudioClip? GetLongestClip(string category, string clipNamePrefix)
        {
            ClipGroup group = GetGroup(category);
            int bestIndex = -1;
            long bestLength = -1;
            for (int i = 0; i < group.Paths.Length; i++)
            {
                long length = 0;
                try
                {
                    length = new FileInfo(group.Paths[i]).Length;
                }
                catch
                {
                    length = 0;
                }

                if (length > bestLength)
                {
                    bestLength = length;
                    bestIndex = i;
                }
            }

            return bestIndex < 0 ? null : GetClip(group, clipNamePrefix, bestIndex);
        }

        public bool PlayRandomOneShot(string category, AudioSource? source, float volume, float pitch, string clipNamePrefix)
        {
            if (source == null || volume <= 0f)
                return false;

            ClipGroup group = GetGroup(category);
            if (group.Paths.Length == 0)
                return false;

            int index = ChooseIndex(group);
            AudioClip? clip = GetClip(group, clipNamePrefix, index);
            if (clip == null && group.Paths.Length > 1)
            {
                index = ChooseIndex(group);
                clip = GetClip(group, clipNamePrefix, index);
            }

            if (clip == null)
                return false;

            source.pitch = pitch;
            source.PlayOneShot(clip, Config.Clamp(volume, 0f, 1f));
            return true;
        }

        private static int ChooseIndex(ClipGroup group)
        {
            if (group.Paths.Length <= 1)
            {
                group.LastIndex = 0;
                return 0;
            }

            int index = UnityEngine.Random.Range(0, group.Paths.Length);
            if (index == group.LastIndex)
                index = (index + UnityEngine.Random.Range(1, group.Paths.Length)) % group.Paths.Length;

            group.LastIndex = index;
            return index;
        }

        private static AudioClip? GetClip(ClipGroup group, string clipNamePrefix, int index)
        {
            if (index < 0 || index >= group.Paths.Length)
                return null;

            if (group.Clips.Length != group.Paths.Length)
                group.Clips = new AudioClip?[group.Paths.Length];

            AudioClip? clip = group.Clips[index];
            if (clip != null)
                return clip;

            clip = ExternalAudioClipLoader.TryGetClip(group.Paths[index], clipNamePrefix + "_" + index.ToString("00"));
            group.Clips[index] = clip;
            return clip;
        }

        private static ClipGroup GetGroup(string category)
        {
            if (!Groups.TryGetValue(category, out ClipGroup? group))
            {
                group = new ClipGroup();
                Groups[category] = group;
            }

            if (!group.Scanned)
            {
                group.Paths = ScanCategory(category);
                group.Clips = new AudioClip?[group.Paths.Length];
                group.Scanned = true;
                if (Config.DebugMode)
                    MainMod.Runtime?.Logger.Msg("[ZBC] Z-City audio group loaded: " + category + " (" + group.Paths.Length + ")");
            }

            return group;
        }

        private static string[] ScanCategory(string category)
        {
            List<string> paths = new List<string>(32);
            string[] roots = ExternalAudioClipLoader.GetAudioFolders();
            for (int i = 0; i < roots.Length; i++)
            {
                AddFiles(paths, Path.Combine(roots[i], "ZCity", category));
                AddFiles(paths, Path.Combine(roots[i], category));
            }

            paths.Sort(StringComparer.OrdinalIgnoreCase);
            return paths.ToArray();
        }

        private static void AddFiles(List<string> paths, string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
                return;

            string[] files = Directory.GetFiles(folder, "*.*", SearchOption.TopDirectoryOnly);
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
    }
}
