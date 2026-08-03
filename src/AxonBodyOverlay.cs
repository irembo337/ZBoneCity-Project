using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Il2CppTMPro;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace BonelabAdvancedHealth
{
    public sealed class AxonBodyOverlay
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float BeepIntervalSeconds = 120f;
        private static readonly string[] AssetFolderCandidates =
        {
            "ZBoneCity\\Axon",
            "Axon",
            "UserData\\ZBoneCity\\Axon"
        };

        private GameObject? _root;
        private RectTransform? _overlayRect;
        private CanvasGroup? _group;
        private TextMeshProUGUI? _dateText;
        private TextMeshProUGUI? _cameraText;
        private TextMeshProUGUI? _batteryText;
        private Image? _logoImage;
        private AudioSource? _beepSource;
        private AudioClip? _beepClip;
        private TMP_FontAsset? _fontAsset;
        private Sprite? _logoSprite;
        private Texture2D? _logoTexture;
        private AxonConfig _sourceConfig;
        private string _cameraId = string.Empty;
        private float _clockTimer = 999f;
        private float _beepTimer = BeepIntervalSeconds;
        private float _batteryNormalized = 1f;
        private bool _assetsLoaded;

        public void EnsureCreated(Transform parent)
        {
            if (_root != null)
                return;

            _sourceConfig = AxonConfig.Load();
            _cameraId = BuildCameraId();

            _root = new GameObject("ZBoneCity_AXON_BODY_3_Overlay");
            _root.transform.SetParent(parent, false);
            _root.layer = parent.gameObject.layer;

            RectTransform rootRect = _root.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            _group = _root.AddComponent<CanvasGroup>();
            _group.interactable = false;
            _group.blocksRaycasts = false;
            _group.alpha = Config.BodycamOverlayOpacity;

            GameObject overlay = new GameObject("AXONLayout");
            overlay.transform.SetParent(_root.transform, false);
            overlay.layer = _root.layer;
            _overlayRect = overlay.AddComponent<RectTransform>();
            _overlayRect.anchorMin = Vector2.one;
            _overlayRect.anchorMax = Vector2.one;
            _overlayRect.pivot = Vector2.one;
            _overlayRect.sizeDelta = new Vector2(430f, 112f);

            LoadAssets();
            _dateText = CreateText("Date", overlay.transform, new Vector2(330f, 28f), new Vector2(0f, -8f));
            _cameraText = CreateText("CameraId", overlay.transform, new Vector2(330f, 30f), new Vector2(0f, -32f));
            _batteryText = CreateText("Battery", overlay.transform, new Vector2(330f, 26f), new Vector2(0f, -58f));
            _logoImage = CreateLogo(overlay.transform);
            _beepSource = _root.AddComponent<AudioSource>();
            _beepSource.playOnAwake = false;
            _beepSource.loop = false;
            _beepSource.spatialBlend = 0f;
            _beepSource.priority = 96;

            RefreshText(true);
        }

        public void Update(float deltaTime)
        {
            if (_root == null)
                return;

            bool visible = Config.BodycamAxonOverlayEnabled;
            if (_root.activeSelf != visible)
                _root.SetActive(visible);
            if (!visible)
                return;

            ApplyLayout();
            UpdateBattery(deltaTime);
            _clockTimer += deltaTime;
            if (_clockTimer >= 1f)
                RefreshText(false);

            UpdateBeep(deltaTime);
        }

        public void ResetIdentity()
        {
            _sourceConfig = AxonConfig.Load();
            _cameraId = BuildCameraId();
            RefreshText(true);
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            if (_logoSprite != null)
            {
                UnityEngine.Object.Destroy(_logoSprite);
                _logoSprite = null;
            }

            if (_logoTexture != null)
            {
                UnityEngine.Object.Destroy(_logoTexture);
                _logoTexture = null;
            }

            if (_fontAsset != null)
            {
                UnityEngine.Object.Destroy(_fontAsset);
                _fontAsset = null;
            }

            _dateText = null;
            _cameraText = null;
            _batteryText = null;
            _logoImage = null;
            _beepSource = null;
            _beepClip = null;
            _overlayRect = null;
            _group = null;
            _assetsLoaded = false;
        }

        private void ApplyLayout()
        {
            if (_overlayRect != null)
            {
                _overlayRect.localScale = Vector3.one * Config.BodycamOverlayScale;
                _overlayRect.anchoredPosition = new Vector2(-28.8f + Config.BodycamOverlayOffsetX, -49.9f - Config.BodycamOverlayOffsetY);
            }

            if (_group != null)
                _group.alpha = Config.BodycamOverlayOpacity;

            if (_logoImage != null)
            {
                Color color = Color.white;
                color.a = 0.75f;
                _logoImage.color = color;
            }
        }

        private void RefreshText(bool force)
        {
            if (!force && _clockTimer < 1f)
                return;

            _clockTimer = 0f;
            if (_dateText != null)
                _dateText.text = BuildTimestamp();
            if (_cameraText != null)
                _cameraText.text = "AXON BODY 3 X" + _cameraId;
            if (_batteryText != null)
                _batteryText.text = _batteryNormalized <= 0.18f ? "BATTERY LOW" : string.Empty;
        }

        private void UpdateBattery(float deltaTime)
        {
            _batteryNormalized = Mathf.Max(0f, _batteryNormalized - deltaTime / 7200f);
            if (_batteryText == null)
                return;

            bool show = Config.BodycamBatteryIndicator && _batteryNormalized <= 0.18f;
            if (_batteryText.gameObject.activeSelf != show)
                _batteryText.gameObject.SetActive(show);
            if (show)
            {
                _batteryText.text = "BATTERY LOW";
                _batteryText.color = Color.Lerp(new Color(1f, 0.76f, 0.18f, 1f), Color.red, Mathf.PingPong(Time.time * 1.8f, 1f));
            }
        }

        private void UpdateBeep(float deltaTime)
        {
            if (!Config.BodycamBeepEnabled)
            {
                _beepTimer = BeepIntervalSeconds;
                return;
            }

            _beepTimer -= deltaTime;
            if (_beepTimer > 0f)
                return;

            _beepTimer = BeepIntervalSeconds;
            if (_beepSource == null)
                return;

            if (_beepClip == null)
                _beepClip = LoadBeepClip();
            if (_beepClip == null)
                return;

            _beepSource.volume = Config.BodycamBeepVolume;
            _beepSource.PlayOneShot(_beepClip, Config.BodycamBeepVolume);
        }

        private void LoadAssets()
        {
            if (_assetsLoaded)
                return;

            _assetsLoaded = true;
            _fontAsset = LoadFontAsset();
            _logoSprite = LoadLogoSprite();
            _beepClip = LoadBeepClip();
        }

        private TextMeshProUGUI CreateText(string name, Transform parent, Vector2 size, Vector2 position)
        {
            GameObject go = new GameObject("AXON_" + name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.raycastTarget = false;
            text.fontSize = 23f;
            text.fontStyle = FontStyles.Bold;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;
            text.alignment = TextAlignmentOptions.Left;
            text.color = Color.white;
            if (_fontAsset != null)
                text.font = _fontAsset;

            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = Color.black;
            shadow.effectDistance = new Vector2(1f, -1f);
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = Vector2.one;
            return text;
        }

        private Image CreateLogo(Transform parent)
        {
            GameObject go = new GameObject("AXON_Logo");
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.one;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one;
            rect.sizeDelta = new Vector2(67.2f, 67.2f);
            rect.anchoredPosition = Vector2.zero;

            Image image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.sprite = _logoSprite;
            image.color = new Color(1f, 1f, 1f, 0.75f);
            return image;
        }

        private static TMP_FontAsset? LoadFontAsset()
        {
            string? fontPath = ResolveAssetPath("KlartextMonoBold.ttf");
            if (fontPath == null)
                return null;

            try
            {
                Font font = new Font(fontPath);
                TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
                asset.name = "ZBC_KlartextMonoBold_TMP";
                return asset;
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Failed to load AXON font, using TMP fallback: " + ex.Message);
                return null;
            }
        }

        private Sprite? LoadLogoSprite()
        {
            string? path = ResolveAssetPath("logo.png");
            if (path == null)
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "ZBC_AXON_Logo";
                if (!texture.LoadImage(bytes, false))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                _logoTexture = texture;
                Rect rect = new Rect(0f, 0f, texture.width, texture.height);
                return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f);
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Failed to load AXON logo: " + ex.Message);
                return null;
            }
        }

        private static AudioClip? LoadBeepClip()
        {
            string? path = ResolveAssetPath("beep.wav");
            return ExternalAudioClipLoader.TryGetClip(path, "ZBC_AXON_Beep");
        }

        private string BuildCameraId()
        {
            string configured = SanitizeId(Config.BodycamCameraId);
            if (!string.IsNullOrEmpty(configured))
                return configured;

            configured = SanitizeId(_sourceConfig.CustomCameraId);
            if (!string.IsNullOrEmpty(configured))
                return configured;

            const string keys = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            System.Random random = new System.Random(Environment.TickCount ^ DateTime.Now.Millisecond);
            char[] chars = new char[8];
            chars[0] = '6';
            chars[1] = '0';
            chars[2] = '3';
            chars[3] = '9';
            for (int i = 4; i < chars.Length; i++)
                chars[i] = keys[random.Next(keys.Length)];
            return new string(chars);
        }

        private string BuildTimestamp()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            if (_sourceConfig.ForceUtcOffsetHours.HasValue)
            {
                TimeSpan offset = TimeSpan.FromHours(_sourceConfig.ForceUtcOffsetHours.Value);
                if (_sourceConfig.CalculateDaylightSavingsTime && TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now))
                    offset += TimeSpan.FromHours(1);
                now = DateTimeOffset.UtcNow.ToOffset(offset);
            }

            return now.ToString("yyyy-MM-dd HH:mm:ss ", CultureInfo.InvariantCulture) + FormatOffset(now.Offset);
        }

        private static string FormatOffset(TimeSpan offset)
        {
            char sign = offset < TimeSpan.Zero ? '-' : '+';
            offset = offset.Duration();
            return sign + ((int)offset.TotalHours).ToString("00", CultureInfo.InvariantCulture) + offset.Minutes.ToString("00", CultureInfo.InvariantCulture);
        }

        private static string SanitizeId(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            char[] buffer = new char[Math.Min(16, value.Length)];
            int count = 0;
            for (int i = 0; i < value.Length && count < buffer.Length; i++)
            {
                char c = char.ToUpperInvariant(value[i]);
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    buffer[count++] = c;
            }

            return count == 0 ? string.Empty : new string(buffer, 0, count);
        }

        private static string? ResolveAssetPath(string fileName)
        {
            string? assemblyDirectory = null;
            try
            {
                assemblyDirectory = Path.GetDirectoryName(typeof(MainMod).Assembly.Location);
            }
            catch
            {
                assemblyDirectory = null;
            }

            string[] roots =
            {
                assemblyDirectory ?? string.Empty,
                MelonEnvironment.GameRootDirectory,
                Path.Combine(MelonEnvironment.GameRootDirectory, "UserData")
            };

            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                string root = roots[rootIndex];
                if (string.IsNullOrWhiteSpace(root))
                    continue;

                for (int folderIndex = 0; folderIndex < AssetFolderCandidates.Length; folderIndex++)
                {
                    string path = Path.Combine(root, AssetFolderCandidates[folderIndex], fileName);
                    if (File.Exists(path))
                        return path;
                }
            }

            string projectAssetPath = Path.Combine("C:\\Users\\gorde\\BonelabAdvancedHealth\\assets\\Bodycam\\Axon", fileName);
            if (File.Exists(projectAssetPath))
                return projectAssetPath;

            MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Missing AXON overlay asset: " + fileName);
            return null;
        }

        private struct AxonConfig
        {
            public string CustomCameraId;
            public float? ForceUtcOffsetHours;
            public bool CalculateDaylightSavingsTime;

            public static AxonConfig Load()
            {
                AxonConfig config = new AxonConfig
                {
                    CustomCameraId = string.Empty,
                    ForceUtcOffsetHours = null,
                    CalculateDaylightSavingsTime = true
                };

                string? path = ResolveAssetPath("config.js");
                if (path == null)
                    return config;

                try
                {
                    string text = File.ReadAllText(path);
                    Match id = Regex.Match(text, "CustomCameraId\\s*:\\s*'([^']*)'", RegexOptions.IgnoreCase);
                    if (id.Success)
                        config.CustomCameraId = id.Groups[1].Value;

                    Match offset = Regex.Match(text, "ForceUTCTimeZoneOffset\\s*:\\s*(-?\\d+(?:\\.\\d+)?|false)", RegexOptions.IgnoreCase);
                    if (offset.Success && !string.Equals(offset.Groups[1].Value, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        if (float.TryParse(offset.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
                            config.ForceUtcOffsetHours = parsed;
                    }

                    Match dst = Regex.Match(text, "CalculateDaylightSavingsTime\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
                    if (dst.Success)
                        config.CalculateDaylightSavingsTime = string.Equals(dst.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Failed to read AXON config.js: " + ex.Message);
                }

                return config;
            }
        }
    }
}
