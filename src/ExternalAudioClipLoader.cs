using System;
using System.Collections.Generic;
using System.IO;
using MelonLoader.Utils;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public static class ExternalAudioClipLoader
    {
        private const string PublicAudioFolderName = "ZBoneCity\\Audio";
        private const string LegacyAudioFolderName = "BonelabAdvancedHealth\\Audio";
        private static readonly Dictionary<string, AudioClip> LoadedClips = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> FailedClips = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> MissingSoundWarnings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static string GetAudioFolder()
        {
            string folder = Path.Combine(MelonEnvironment.UserDataDirectory, PublicAudioFolderName);
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            return folder;
        }

        public static string[] GetAudioFolders()
        {
            string? assemblyDirectory = null;
            try
            {
                string location = typeof(MainMod).Assembly.Location;
                if (!string.IsNullOrEmpty(location))
                    assemblyDirectory = Path.GetDirectoryName(location);
            }
            catch
            {
                assemblyDirectory = null;
            }

            string[] candidates =
            {
                Path.Combine(MelonEnvironment.GameRootDirectory, PublicAudioFolderName),
                Path.Combine(MelonEnvironment.GameRootDirectory, "Mods", PublicAudioFolderName),
                assemblyDirectory == null ? string.Empty : Path.Combine(assemblyDirectory, PublicAudioFolderName),
                assemblyDirectory == null ? string.Empty : Path.Combine(assemblyDirectory, "Audio"),
                Path.Combine(MelonEnvironment.UserDataDirectory, PublicAudioFolderName),
                Path.Combine(MelonEnvironment.UserDataDirectory, LegacyAudioFolderName),
                Path.Combine(MelonEnvironment.GameRootDirectory, "UserData", PublicAudioFolderName),
                Path.Combine(MelonEnvironment.GameRootDirectory, "UserData", LegacyAudioFolderName)
            };

            List<string> folders = new List<string>(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                string folder = candidates[i];
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                string normalized;
                try
                {
                    normalized = Path.GetFullPath(folder);
                }
                catch
                {
                    continue;
                }

                bool exists = false;
                for (int j = 0; j < folders.Count; j++)
                {
                    if (string.Equals(folders[j], normalized, StringComparison.OrdinalIgnoreCase))
                    {
                        exists = true;
                        break;
                    }
                }

                if (!exists)
                    folders.Add(normalized);
            }

            return folders.ToArray();
        }

        public static string? ResolveAudioPath(params string[] baseNames)
        {
            string[] folders = GetAudioFolders();
            for (int i = 0; i < baseNames.Length; i++)
            {
                string name = baseNames[i];
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (Path.HasExtension(name))
                {
                    if (Path.IsPathRooted(name))
                    {
                        if (File.Exists(name))
                            return name;
                    }
                    else
                    {
                        for (int folderIndex = 0; folderIndex < folders.Length; folderIndex++)
                        {
                            string exact = Path.Combine(folders[folderIndex], name);
                            if (File.Exists(exact))
                                return exact;
                        }
                    }
                    continue;
                }

                for (int folderIndex = 0; folderIndex < folders.Length; folderIndex++)
                {
                    string folder = folders[folderIndex];
                    string wav = Path.Combine(folder, name + ".wav");
                    if (File.Exists(wav))
                        return wav;
                    string mp3 = Path.Combine(folder, name + ".mp3");
                    if (File.Exists(mp3))
                        return mp3;
                    string ogg = Path.Combine(folder, name + ".ogg");
                    if (File.Exists(ogg))
                        return ogg;
                }
            }

            if (baseNames.Length > 0)
                WarnMissingSound(baseNames[0]);
            return null;
        }

        public static AudioClip? TryGetClip(string? path, string clipName)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            if (LoadedClips.TryGetValue(path, out AudioClip? cached) && cached != null)
                return cached;
            if (FailedClips.Contains(path))
                return null;

            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension == ".wav")
                return TryLoadWav(path, clipName);

            if (extension == ".mp3" || extension == ".ogg")
            {
                FailedClips.Add(path);
                LogAudioError("Compressed sound skipped for load safety: " + Path.GetFileName(path) + " (convert to WAV for BONELAB)");
                return null;
            }

            FailedClips.Add(path);
            LogAudioError("Unsupported audio file format: " + Path.GetFileName(path));
            return null;
        }

        public static void ForgetAll()
        {
            LoadedClips.Clear();
            FailedClips.Clear();
        }

        private static AudioClip? TryLoadWav(string path, string clipName)
        {
            try
            {
                AudioClip clip = WavAudioLoader.Load(path, clipName);
                LoadedClips[path] = clip;
                if (Config.DebugMode)
                    MainMod.Runtime?.Logger.Msg("[ZBC] Loaded sound: " + Path.GetFileName(path));
                return clip;
            }
            catch (Exception ex)
            {
                FailedClips.Add(path);
                LogAudioError("Failed to load sound: " + Path.GetFileName(path) + " (" + ex.Message + ")");
                return null;
            }
        }

        private static void WarnMissingSound(string name)
        {
            string fileName = Path.HasExtension(name) ? Path.GetFileName(name) : name + ".wav";
            if (string.IsNullOrEmpty(fileName) || MissingSoundWarnings.Contains(fileName))
                return;

            MissingSoundWarnings.Add(fileName);
            LogAudioError("Failed to load sound: " + fileName);
        }

        private static void LogAudioError(string message)
        {
            MainMod.Runtime?.Logger.Warning("[ZBC ERROR] " + message);
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
