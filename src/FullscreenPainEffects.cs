using UnityEngine;
using UnityEngine.UI;

namespace BonelabAdvancedHealth
{
    public sealed class FullscreenPainEffects
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const int PainOverlayLayerFallback = 5;
        private GameObject? _root;
        private RectTransform? _rootRect;
        private Canvas? _canvas;
        private CanvasGroup? _canvasGroup;
        private Image? _darken;
        private Image? _redVeil;
        private Image? _edgeVignette;
        private Image? _bloodEdges;
        private Image? _blurPulse;
        private Image? _heartbeatTunnel;
        private Sprite? _vignetteSprite;
        private Sprite? _bloodEdgeSprite;
        private Texture2D? _vignetteTexture;
        private Texture2D? _bloodEdgeTexture;
        private float _smoothedPain;
        private float _smoothedBlood;
        private float _smoothedCombined;
        private float _pulseTime;
        private int _lastScreenWidth;
        private int _lastScreenHeight;

        public void EnsureCreated()
        {
            if (_root == null)
                Build();

            ApplyPresentation();
        }

        public void EnsureCreated(Transform head)
        {
            EnsureCreated();
        }

        public void Reset()
        {
            _smoothedPain = 0f;
            _smoothedBlood = 0f;
            _smoothedCombined = 0f;
            _pulseTime = 0f;
            SetAlpha(_darken, 0f);
            SetAlpha(_redVeil, 0f);
            SetAlpha(_edgeVignette, 0f);
            SetAlpha(_bloodEdges, 0f);
            SetAlpha(_blurPulse, 0f);
            SetAlpha(_heartbeatTunnel, 0f);
            if (_canvasGroup != null)
                _canvasGroup.alpha = 0f;
        }

        public void Destroy()
        {
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }

            if (_vignetteSprite != null)
            {
                Object.Destroy(_vignetteSprite);
                _vignetteSprite = null;
            }

            if (_bloodEdgeSprite != null)
            {
                Object.Destroy(_bloodEdgeSprite);
                _bloodEdgeSprite = null;
            }

            if (_vignetteTexture != null)
            {
                Object.Destroy(_vignetteTexture);
                _vignetteTexture = null;
            }

            if (_bloodEdgeTexture != null)
            {
                Object.Destroy(_bloodEdgeTexture);
                _bloodEdgeTexture = null;
            }

            _rootRect = null;
            _canvas = null;
            _canvasGroup = null;
            _darken = null;
            _redVeil = null;
            _edgeVignette = null;
            _bloodEdges = null;
            _blurPulse = null;
            _heartbeatTunnel = null;
            _lastScreenWidth = 0;
            _lastScreenHeight = 0;
            Reset();
        }

        public void Update(TraumaFrameFx fx, HealthManager manager)
        {
            EnsureCreated();
            if (_root == null)
                return;

            ApplyPresentation();
            float deltaTime = Mathf.Max(Time.deltaTime, 0.001f);
            if (!Config.PainOverlayEnabled || !Config.PainEffectsEnabled || manager.Consciousness.State == ConsciousnessState.Dead)
            {
                FadeOut(deltaTime);
                return;
            }

            float pain = Config.Clamp(Mathf.Max(manager.PainNormalized, fx.Pain), 0f, 1f);
            float blood = Config.Clamp(fx.BloodLoss, 0f, 1f);
            float combined = Config.Clamp(Mathf.Max(pain, fx.Combined * 0.72f), 0f, 1f);
            float unconscious = GetUnconsciousBlackout(manager);
            float animationSpeed = Config.PainOverlayAnimationSpeed;
            _smoothedPain = Mathf.MoveTowards(_smoothedPain, pain, deltaTime * Mathf.Lerp(1.8f, 4.8f, pain));
            _smoothedBlood = Mathf.MoveTowards(_smoothedBlood, blood, deltaTime * 2.1f);
            _smoothedCombined = Mathf.MoveTowards(_smoothedCombined, combined, deltaTime * 2.0f);
            _pulseTime += deltaTime * animationSpeed;

            float visiblePain = Mathf.InverseLerp(0.06f, 0.92f, Mathf.Max(_smoothedPain, _smoothedCombined * 0.72f));
            float pulseRate = Mathf.Lerp(1.15f, 5.6f, _smoothedPain);
            float pulse = 0.5f + 0.5f * Mathf.Sin(_pulseTime * pulseRate);
            float pulsePower = pulse * Config.PainOverlayPulseIntensity;
            float master = Config.PainOverlayOpacity;
            float bloodMaster = Config.PainOverlayBloodOpacity;

            if (_canvasGroup != null)
                _canvasGroup.alpha = Config.Clamp(Mathf.Max(master, unconscious), 0f, 1f);

            float edgeAlpha = Config.Clamp(0.04f + visiblePain * 0.78f + pulsePower * _smoothedPain * 0.24f, 0f, 0.96f);
            float redAlpha = Config.Clamp(Mathf.InverseLerp(0.18f, 0.95f, _smoothedPain) * 0.34f + pulsePower * _smoothedPain * 0.12f, 0f, 0.58f);
            float darkAlpha = Config.Clamp(Mathf.InverseLerp(0.16f, 1f, _smoothedCombined) * 0.34f + pulsePower * _smoothedPain * 0.08f, 0f, 0.70f);
            darkAlpha = Mathf.Max(darkAlpha, unconscious);
            float bloodAlpha = Config.Clamp((_smoothedBlood * 0.64f + _smoothedPain * 0.44f + pulsePower * _smoothedPain * 0.24f) * bloodMaster, 0f, 0.94f);
            float blurAlpha = Config.Clamp(Mathf.InverseLerp(0.32f, 1f, _smoothedPain) * 0.16f + pulsePower * _smoothedPain * 0.07f, 0f, 0.28f);
            float tunnelAlpha = Config.Clamp(Mathf.InverseLerp(0.44f, 1f, _smoothedCombined) * 0.72f + pulsePower * _smoothedPain * 0.24f, 0f, 0.94f);

            SetColor(_darken, new Color(0f, 0f, 0f, darkAlpha));
            SetColor(_redVeil, new Color(0.58f, 0.0f, 0.0f, redAlpha * (1f - unconscious)));
            SetColor(_edgeVignette, new Color(0f, 0f, 0f, Mathf.Max(edgeAlpha, unconscious * 0.85f)));
            SetColor(_bloodEdges, new Color(0.72f, 0.0f, 0.0f, bloodAlpha * (1f - unconscious * 0.65f)));
            SetColor(_blurPulse, new Color(0.62f, 0.06f, 0.03f, blurAlpha * (1f - unconscious)));
            SetColor(_heartbeatTunnel, new Color(0f, 0f, 0f, Mathf.Max(tunnelAlpha, unconscious)));

            ApplyScale(_edgeVignette, 1f + visiblePain * 0.10f + pulsePower * 0.045f);
            ApplyScale(_bloodEdges, 1f + pulsePower * 0.075f);
            ApplyScale(_heartbeatTunnel, 1f + _smoothedPain * 0.18f + pulsePower * 0.085f);
            ApplyOffset(_blurPulse, new Vector2(
                Mathf.Sin(_pulseTime * 1.37f) * _smoothedPain * 18f,
                Mathf.Cos(_pulseTime * 1.11f) * _smoothedPain * 12f));
        }

        private void Build()
        {
            _root = new GameObject("ZBoneCity_Fullscreen_PainOverlay");
            SetLayerRecursive(_root.transform, GetPainOverlayLayer());
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32720;
            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            _canvasGroup = _root.AddComponent<CanvasGroup>();
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            _canvasGroup.alpha = 0f;

            _rootRect = _root.GetComponent<RectTransform>();
            _rootRect.anchorMin = new Vector2(0.5f, 0.5f);
            _rootRect.anchorMax = new Vector2(0.5f, 0.5f);
            _rootRect.pivot = new Vector2(0.5f, 0.5f);
            Object.DontDestroyOnLoad(_root);

            _vignetteSprite = CreateVignetteSprite(out _vignetteTexture);
            _bloodEdgeSprite = CreateBloodEdgeSprite(out _bloodEdgeTexture);
            _darken = CreatePanel("Pain_Darken", _root.transform, new Color(0f, 0f, 0f, 0f), null);
            _redVeil = CreatePanel("Pain_RedVeil", _root.transform, new Color(0.42f, 0f, 0f, 0f), null);
            _blurPulse = CreatePanel("Pain_BlurPulse", _root.transform, new Color(0.78f, 0.70f, 0.66f, 0f), null);
            _edgeVignette = CreatePanel("Pain_EdgeVignette", _root.transform, new Color(0f, 0f, 0f, 0f), _vignetteSprite);
            _bloodEdges = CreatePanel("Pain_BloodEdges", _root.transform, new Color(0.58f, 0f, 0f, 0f), _bloodEdgeSprite);
            _heartbeatTunnel = CreatePanel("Pain_HeartbeatTunnel", _root.transform, new Color(0f, 0f, 0f, 0f), _vignetteSprite);
            ApplyPresentation();
        }

        private void ApplyPresentation()
        {
            if (_root == null || _rootRect == null)
                return;

            ApplyCanvasMode();

            int width = Mathf.Max(Screen.width, 1);
            int height = Mathf.Max(Screen.height, 1);
            if (width == _lastScreenWidth && height == _lastScreenHeight)
                return;

            _lastScreenWidth = width;
            _lastScreenHeight = height;
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.pivot = new Vector2(0.5f, 0.5f);
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;

            float aspect = GetViewportAspect();
            Vector2 size = new Vector2(Mathf.Max(ReferenceWidth, ReferenceHeight * aspect) * 1.18f, ReferenceHeight * 1.18f);
            Resize(_darken, size);
            Resize(_redVeil, size);
            Resize(_edgeVignette, size);
            Resize(_bloodEdges, size);
            Resize(_blurPulse, size);
            Resize(_heartbeatTunnel, size);
        }

        private void ApplyCanvasMode()
        {
            if (_root == null || _rootRect == null || _canvas == null)
                return;

            // The pain effect is a screen overlay, not a world object. Keeping it out of
            // world-space prevents the VR user from seeing it as a floating panel and
            // keeps the bodycam RenderTexture clean.
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.worldCamera = null;
            _canvas.sortingOrder = 32720;
            _root.transform.SetParent(null, false);
            _root.transform.localPosition = Vector3.zero;
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;
            _rootRect.anchorMin = Vector2.zero;
            _rootRect.anchorMax = Vector2.one;
            _rootRect.pivot = new Vector2(0.5f, 0.5f);
            _rootRect.offsetMin = Vector2.zero;
            _rootRect.offsetMax = Vector2.zero;
        }

        private void FadeOut(float deltaTime)
        {
            _smoothedPain = Mathf.MoveTowards(_smoothedPain, 0f, deltaTime * 3.8f);
            _smoothedBlood = Mathf.MoveTowards(_smoothedBlood, 0f, deltaTime * 3.0f);
            _smoothedCombined = Mathf.MoveTowards(_smoothedCombined, 0f, deltaTime * 3.2f);
            float alpha = Mathf.Max(_smoothedPain, _smoothedBlood, _smoothedCombined);
            if (_canvasGroup != null)
                _canvasGroup.alpha = Config.Clamp(alpha * Config.PainOverlayOpacity, 0f, 1f);
            SetAlpha(_darken, 0f);
            SetAlpha(_redVeil, 0f);
            SetAlpha(_edgeVignette, alpha * 0.18f);
            SetAlpha(_bloodEdges, alpha * 0.12f);
            SetAlpha(_blurPulse, 0f);
            SetAlpha(_heartbeatTunnel, 0f);
        }

        private static Image CreatePanel(string name, Transform parent, Color color, Sprite? sprite)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = parent.gameObject.layer;
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null)
                image.sprite = sprite;
            return image;
        }

        private static float GetUnconsciousBlackout(HealthManager manager)
        {
            float blackout = manager.Consciousness.BlackoutIntensity;
            if (manager.Coma.IsActive || manager.Consciousness.State == ConsciousnessState.Unconscious)
                blackout = Mathf.Max(blackout, 1f);
            return Config.Clamp(blackout, 0f, 1f);
        }

        private static int GetPainOverlayLayer()
        {
            int layer = LayerMask.NameToLayer("UI");
            return layer >= 0 ? layer : PainOverlayLayerFallback;
        }

        private static void SetLayerRecursive(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
                SetLayerRecursive(root.GetChild(i), layer);
        }

        private static void SetColor(Image? image, Color color)
        {
            if (image != null)
                image.color = color;
        }

        private static void SetAlpha(Image? image, float alpha)
        {
            if (image == null)
                return;
            Color color = image.color;
            color.a = alpha;
            image.color = color;
        }

        private static void ApplyOffset(Image? image, Vector2 offset)
        {
            if (image == null)
                return;
            RectTransform rect = image.rectTransform;
            rect.anchoredPosition = offset;
        }

        private static void ApplyScale(Image? image, float scale)
        {
            if (image != null)
                image.rectTransform.localScale = Vector3.one * scale;
        }

        private static void Resize(Image? image, Vector2 size)
        {
            if (image == null)
                return;
            image.rectTransform.sizeDelta = size;
        }

        private static float GetViewportAspect()
        {
            int width = Screen.width;
            int height = Screen.height;
            if (width > 0 && height > 0)
                return Config.Clamp(width / (float)height, 1.0f, 2.4f);
            return 16f / 9f;
        }

        private static Sprite CreateVignetteSprite(out Texture2D texture)
        {
            const int size = 256;
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float max = center.magnitude;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = (new Vector2(x, y) - center).magnitude / max;
                    float alpha = Mathf.SmoothStep(0.20f, 0.98f, d);
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite CreateBloodEdgeSprite(out Texture2D texture)
        {
            const int width = 384;
            const int height = 216;
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = y / (float)(height - 1);
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float edge = Mathf.Max(Mathf.Abs(u - 0.5f) * 2f, Mathf.Abs(v - 0.5f) * 2f);
                    float noise = Mathf.PerlinNoise(u * 15.5f + 2.0f, v * 12.0f + 7.0f);
                    float streak = Mathf.PerlinNoise(u * 4.0f, v * 34.0f + 11.0f);
                    float alpha = Mathf.SmoothStep(0.58f, 1.02f, edge) * (0.42f + noise * 0.44f);
                    alpha += Mathf.SmoothStep(0.74f, 1.0f, edge) * streak * 0.32f;
                    alpha = Config.Clamp(alpha, 0f, 1f);
                    pixels[y * width + x] = new Color32(125, 0, 0, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
        }
    }
}
