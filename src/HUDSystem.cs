using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace BonelabAdvancedHealth
{
    public sealed class HUDSystem
    {
        private readonly StringBuilder _builder = new StringBuilder(512);
        private GameObject? _root;
        private Text? _vitalsText;
        private Text? _limbText;
        private Image? _bloodFill;
        private Image? _painFill;
        private Image? _vignette;
        private Image? _desaturation;
        private Font? _font;
        private float _lastBlackout;
        private float _lastPain;
        private ConsciousnessState _lastState;

        public bool IsCreated => _root != null;

        public void EnsureCreated()
        {
            if (!Config.HudEnabled)
            {
                Destroy();
                return;
            }

            Transform? head = MainMod.Runtime?.GetHeadTransform();
            if (head == null)
                return;

            if (_root == null)
                Build(head);
            else if (_root.transform.parent != head)
                AttachToHead(head);
        }

        public void UpdateHud(HealthManager manager)
        {
            if (!Config.HudEnabled)
                return;

            EnsureCreated();
            if (_root == null || _vitalsText == null || _limbText == null || _bloodFill == null || _painFill == null)
                return;

            float blood = manager.Bleeding.BloodVolumeMl;
            int pulse = CalculatePulse(manager);
            _bloodFill.fillAmount = manager.Bleeding.BloodNormalized;
            _bloodFill.color = GetBloodColor(manager.Bleeding.BloodNormalized);
            _painFill.fillAmount = manager.PainNormalized;

            _builder.Length = 0;
            _builder.Append("BLOOD ");
            _builder.Append(Mathf.RoundToInt(blood));
            _builder.Append(" ml\nPULSE ");
            _builder.Append(pulse);
            _builder.Append(" bpm\nPAIN ");
            _builder.Append(Mathf.RoundToInt(manager.Pain));
            _builder.Append("\nSTATE ");
            _builder.Append(GetStateLabel(manager.Consciousness.State));
            if (manager.Bleeding.HasActiveBleeding)
            {
                _builder.Append("\nBLEED ");
                _builder.Append(manager.Bleeding.ActiveBleedCount);
                _builder.Append(" / ");
                _builder.Append(Mathf.RoundToInt(manager.Bleeding.TotalBleedRateMlPerSecond));
                _builder.Append(" ml/s");
            }

            _vitalsText.text = _builder.ToString();

            _builder.Length = 0;
            AppendLimbLine(_builder, manager, BodyPart.Head, "HEAD");
            AppendLimbLine(_builder, manager, BodyPart.Torso, "TORSO");
            AppendLimbLine(_builder, manager, BodyPart.LeftArm, "L ARM");
            AppendLimbLine(_builder, manager, BodyPart.RightArm, "R ARM");
            AppendLimbLine(_builder, manager, BodyPart.LeftLeg, "L LEG");
            AppendLimbLine(_builder, manager, BodyPart.RightLeg, "R LEG");
            _limbText.text = _builder.ToString();

            SetConsciousnessEffects(_lastBlackout, _lastPain, _lastState);
        }

        public void SetConsciousnessEffects(float blackout, float pain, ConsciousnessState state)
        {
            _lastBlackout = Config.Clamp(blackout, 0f, 1f);
            _lastPain = Config.Clamp(pain, 0f, 1f);
            _lastState = state;

            if (_vignette == null || _desaturation == null)
                return;

            float darkAlpha = state == ConsciousnessState.Dead ? 0.92f : Config.Clamp(_lastBlackout * 0.68f + _lastPain * 0.12f, 0f, 0.78f);
            float greyAlpha = state == ConsciousnessState.Dead ? 0.35f : Config.Clamp(_lastBlackout * 0.24f + _lastPain * 0.08f, 0f, 0.32f);
            _vignette.color = new Color(0f, 0f, 0f, darkAlpha);
            _desaturation.color = new Color(0.55f, 0.55f, 0.55f, greyAlpha);
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _vitalsText = null;
            _limbText = null;
            _bloodFill = null;
            _painFill = null;
            _vignette = null;
            _desaturation = null;
        }

        private void Build(Transform head)
        {
            _font = Font.GetDefault();
            _root = new GameObject("AHS_VR_HUD");
            Canvas canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 10f;
            scaler.referencePixelsPerUnit = 100f;
            _root.AddComponent<GraphicRaycaster>();

            RectTransform rootRect = _root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(420f, 260f);
            AttachToHead(head);

            GameObject panel = CreatePanel("Panel", _root.transform, new Vector2(420f, 260f), new Vector2(0f, 0f), new Color(0f, 0f, 0f, 0.42f));
            CreatePanel("BloodBack", panel.transform, new Vector2(370f, 16f), new Vector2(0f, 100f), new Color(0.05f, 0.05f, 0.05f, 0.8f));
            _bloodFill = CreatePanel("BloodFill", panel.transform, new Vector2(370f, 16f), new Vector2(0f, 100f), new Color(0.65f, 0.02f, 0.02f, 0.95f)).GetComponent<Image>();
            _bloodFill.type = Image.Type.Filled;
            _bloodFill.fillMethod = Image.FillMethod.Horizontal;
            _bloodFill.fillOrigin = 0;

            CreatePanel("PainBack", panel.transform, new Vector2(370f, 10f), new Vector2(0f, 78f), new Color(0.05f, 0.05f, 0.05f, 0.75f));
            _painFill = CreatePanel("PainFill", panel.transform, new Vector2(370f, 10f), new Vector2(0f, 78f), new Color(0.9f, 0.55f, 0.05f, 0.88f)).GetComponent<Image>();
            _painFill.type = Image.Type.Filled;
            _painFill.fillMethod = Image.FillMethod.Horizontal;
            _painFill.fillOrigin = 0;

            _vitalsText = CreateText("Vitals", panel.transform, new Vector2(190f, 142f), new Vector2(-94f, -12f), 18, TextAnchor.UpperLeft);
            _limbText = CreateText("Limbs", panel.transform, new Vector2(178f, 142f), new Vector2(98f, -12f), 15, TextAnchor.UpperLeft);

            _desaturation = CreatePanel("Desaturation", _root.transform, new Vector2(920f, 520f), new Vector2(0f, 0f), new Color(0.55f, 0.55f, 0.55f, 0f)).GetComponent<Image>();
            _vignette = CreatePanel("Vignette", _root.transform, new Vector2(980f, 560f), new Vector2(0f, 0f), new Color(0f, 0f, 0f, 0f)).GetComponent<Image>();
            _desaturation.raycastTarget = false;
            _vignette.raycastTarget = false;
            _desaturation.transform.SetAsFirstSibling();
            _vignette.transform.SetAsFirstSibling();
        }

        private void AttachToHead(Transform head)
        {
            if (_root == null)
                return;

            _root.transform.SetParent(head, false);
            _root.transform.localPosition = new Vector3(0f, -0.19f, 0.72f);
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one * 0.00135f;
        }

        private GameObject CreatePanel(string name, Transform parent, Vector2 size, Vector2 anchoredPosition, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return go;
        }

        private Text CreateText(string name, Transform parent, Vector2 size, Vector2 anchoredPosition, int fontSize, TextAnchor anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            Text text = go.AddComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.alignment = anchor;
            text.color = new Color(0.92f, 0.98f, 1f, 0.96f);
            text.supportRichText = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private static void AppendLimbLine(StringBuilder builder, HealthManager manager, BodyPart part, string label)
        {
            LimbHealth limb = manager.GetLimb(part);
            builder.Append(label);
            builder.Append(' ');
            builder.Append(Mathf.RoundToInt(limb.Hp));
            builder.Append('/');
            builder.Append(Mathf.RoundToInt(limb.MaxHp));
            BleedSeverity bleed = manager.Bleeding.GetWorstBleedingSeverity(part);
            if (bleed != BleedSeverity.None)
            {
                builder.Append(" B:");
                builder.Append(GetBleedLabel(bleed));
            }
            if (limb.Fracture != FractureState.None)
            {
                builder.Append(" F:");
                builder.Append(GetFractureLabel(limb.Fracture));
            }
            builder.Append('\n');
        }

        private static int CalculatePulse(HealthManager manager)
        {
            if (manager.Consciousness.State == ConsciousnessState.Dead)
                return 0;

            float bloodStress = 1f - manager.Bleeding.BloodNormalized;
            float pulse = 66f + manager.PainNormalized * 38f + bloodStress * 44f + manager.Bleeding.TotalBleedRateMlPerSecond * 0.18f;
            if (manager.Consciousness.State == ConsciousnessState.Unconscious)
                pulse *= 0.72f;
            return Mathf.Clamp(Mathf.RoundToInt(pulse), 28, 178);
        }

        private static Color GetBloodColor(float normalized)
        {
            if (normalized < 0.42f)
                return new Color(0.95f, 0.02f, 0.02f, 0.95f);
            if (normalized < 0.62f)
                return new Color(0.95f, 0.32f, 0.02f, 0.95f);
            return new Color(0.6f, 0.02f, 0.02f, 0.95f);
        }

        private static string GetStateLabel(ConsciousnessState state)
        {
            switch (state)
            {
                case ConsciousnessState.Awake:
                    return "AWAKE";
                case ConsciousnessState.Blackout:
                    return "BLACKOUT";
                case ConsciousnessState.Unconscious:
                    return "UNCONSCIOUS";
                case ConsciousnessState.Dead:
                    return "DEAD";
                default:
                    return "UNKNOWN";
            }
        }

        private static string GetBleedLabel(BleedSeverity severity)
        {
            switch (severity)
            {
                case BleedSeverity.Light:
                    return "L";
                case BleedSeverity.Medium:
                    return "M";
                case BleedSeverity.Severe:
                    return "S";
                case BleedSeverity.Arterial:
                    return "A";
                default:
                    return "-";
            }
        }

        private static string GetFractureLabel(FractureState fracture)
        {
            switch (fracture)
            {
                case FractureState.Sprain:
                    return "SP";
                case FractureState.Fractured:
                    return "BR";
                case FractureState.Shattered:
                    return "SH";
                default:
                    return "-";
            }
        }
    }
}
