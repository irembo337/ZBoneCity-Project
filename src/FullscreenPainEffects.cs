using UnityEngine;
using UnityEngine.UI;

namespace BonelabAdvancedHealth
{
    public sealed class FullscreenPainEffects
    {
        private Image? _bloodPulse;
        private Image? _painDarken;
        private Image? _blurVeilA;
        private Image? _blurVeilB;
        private Image? _chromaticRed;
        private Image? _chromaticCyan;
        private Image? _doubleVision;
        private Image? _focusLoss;
        private Image? _tunnelPulse;
        private Sprite? _vignetteSprite;

        public void Build(Transform parent, Sprite vignetteSprite)
        {
            _vignetteSprite = vignetteSprite;
            _doubleVision = CreatePanel("PainDoubleVision", parent, new Color(0.86f, 0.94f, 1f, 0f), null);
            _blurVeilA = CreatePanel("PainBlurVeilA", parent, new Color(0.65f, 0.68f, 0.72f, 0f), null);
            _blurVeilB = CreatePanel("PainBlurVeilB", parent, new Color(0.65f, 0.68f, 0.72f, 0f), null);
            _chromaticRed = CreatePanel("ChromaticRed", parent, new Color(1f, 0.02f, 0.02f, 0f), _vignetteSprite);
            _chromaticCyan = CreatePanel("ChromaticCyan", parent, new Color(0.02f, 0.78f, 1f, 0f), _vignetteSprite);
            _bloodPulse = CreatePanel("FullscreenBloodPulse", parent, new Color(0.55f, 0f, 0f, 0f), _vignetteSprite);
            _painDarken = CreatePanel("PainDarken", parent, new Color(0f, 0f, 0f, 0f), null);
            _focusLoss = CreatePanel("EyeFocusLoss", parent, new Color(0.72f, 0.72f, 0.70f, 0f), _vignetteSprite);
            _tunnelPulse = CreatePanel("PainTunnelPulse", parent, new Color(0f, 0f, 0f, 0f), _vignetteSprite);
        }

        public void Reset()
        {
            SetAlpha(_bloodPulse, 0f);
            SetAlpha(_painDarken, 0f);
            SetAlpha(_blurVeilA, 0f);
            SetAlpha(_blurVeilB, 0f);
            SetAlpha(_chromaticRed, 0f);
            SetAlpha(_chromaticCyan, 0f);
            SetAlpha(_doubleVision, 0f);
            SetAlpha(_focusLoss, 0f);
            SetAlpha(_tunnelPulse, 0f);
        }

        public void Update(TraumaFrameFx fx, HealthManager manager)
        {
            float pain = fx.Pain;
            float head = fx.HeadTrauma;
            float blood = fx.BloodLoss;
            float combined = fx.Combined;
            float pulse = fx.Pulse;
            float unconscious = Config.UnconsciousEffectsEnabled ? manager.Consciousness.BlackoutIntensity : 0f;

            SetColor(_bloodPulse, new Color(0.62f, 0f, 0f, Config.Clamp(pain * 0.16f + blood * 0.18f + pulse * pain * 0.12f, 0f, 0.48f)));
            SetColor(_painDarken, new Color(0f, 0f, 0f, Config.Clamp(combined * 0.16f + unconscious * 0.18f, 0f, 0.56f)));
            SetColor(_blurVeilA, new Color(0.62f, 0.66f, 0.70f, Config.Clamp(pain * 0.035f + head * 0.055f, 0f, 0.12f)));
            SetColor(_blurVeilB, new Color(0.78f, 0.78f, 0.74f, Config.Clamp(head * 0.075f + unconscious * 0.06f, 0f, 0.16f)));
            SetColor(_chromaticRed, new Color(1f, 0f, 0f, Config.Clamp(head * 0.12f + pain * 0.06f, 0f, 0.24f)));
            SetColor(_chromaticCyan, new Color(0f, 0.85f, 1f, Config.Clamp(head * 0.10f + pain * 0.05f, 0f, 0.22f)));
            SetColor(_doubleVision, new Color(0.80f, 0.92f, 1f, Config.Clamp(head * 0.12f + manager.Lungs.OxygenStress * 0.10f, 0f, 0.24f)));
            SetColor(_focusLoss, new Color(0.70f, 0.70f, 0.66f, Config.Clamp(combined * 0.075f + manager.Lungs.OxygenStress * 0.09f, 0f, 0.20f)));
            SetColor(_tunnelPulse, new Color(0f, 0f, 0f, Config.Clamp(unconscious * 0.38f + pain * 0.18f + blood * 0.24f, 0f, 0.82f)));

            ApplyOffset(_chromaticRed, new Vector2(18f + head * 26f, 0f));
            ApplyOffset(_chromaticCyan, new Vector2(-18f - head * 22f, 0f));
            ApplyOffset(_doubleVision, new Vector2(Mathf.Sin(Time.time * 2.2f) * head * 38f, Mathf.Cos(Time.time * 1.7f) * head * 18f));
            ApplyOffset(_blurVeilA, new Vector2(12f * pain, 8f * pain));
            ApplyOffset(_blurVeilB, new Vector2(-10f * head, -6f * head));
            ApplyScale(_tunnelPulse, 1f + combined * 0.22f + pulse * pain * 0.05f);
            ApplyScale(_bloodPulse, 1f + pulse * pain * 0.12f);
        }

        private static Image CreatePanel(string name, Transform parent, Color color, Sprite? sprite)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1920f, 1080f);
            rect.anchoredPosition = Vector2.zero;
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            if (sprite != null)
                image.sprite = sprite;
            return image;
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
    }
}
