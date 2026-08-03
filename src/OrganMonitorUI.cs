using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace BonelabAdvancedHealth
{
    public sealed class OrganMonitorUI
    {
        private static readonly string[] OrganFailureLabels = { "OK", "DAMAGED", "CRITICAL", "FAILED" };
        private readonly StringBuilder _builder = new StringBuilder(384);
        private readonly Font _font;
        private GameObject? _root;
        private Text? _text;

        public OrganMonitorUI(Font font)
        {
            _font = font;
        }

        public void Build(Transform parent)
        {
            _root = new GameObject("OrganMonitor");
            _root.transform.SetParent(parent, false);
            RectTransform rect = _root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(365f, 310f);
            rect.anchoredPosition = new Vector2(560f, -120f);

            Image back = _root.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.22f);
            back.raycastTarget = false;

            GameObject textGo = new GameObject("OrganText");
            textGo.transform.SetParent(_root.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(330f, 275f);
            textRect.anchoredPosition = new Vector2(0f, -4f);
            _text = textGo.AddComponent<Text>();
            _text.font = _font;
            _text.fontSize = 17;
            _text.resizeTextForBestFit = true;
            _text.resizeTextMinSize = 10;
            _text.resizeTextMaxSize = 17;
            _text.alignment = TextAnchor.UpperLeft;
            _text.supportRichText = false;
            _text.raycastTarget = false;
            _text.color = new Color(0.86f, 0.95f, 1f, 0.90f);
        }

        public void Update(HealthManager manager)
        {
            if (_text == null)
                return;

            _builder.Length = 0;
            _builder.Append("ORGAN MONITOR\n");
            AppendOrgan(manager, OrganType.Heart, "HEART");
            AppendOrgan(manager, OrganType.Brain, "BRAIN");
            AppendOrgan(manager, OrganType.Lungs, "LUNGS");
            AppendOrgan(manager, OrganType.Liver, "LIVER");
            AppendOrgan(manager, OrganType.Stomach, "STOMACH");
            AppendOrgan(manager, OrganType.Muscles, "MUSCLE");
            _builder.Append('\n');
            _builder.Append("BP ");
            _builder.Append(GetBloodPressureLabel(manager));
            _builder.Append("\nPAIN ");
            _builder.Append(Mathf.RoundToInt(manager.Pain));
            _builder.Append(" / SHOCK ");
            _builder.Append(Mathf.RoundToInt(manager.PainSystem.BlackoutPressure * 100f));
            _builder.Append("%\nO2 LOSS ");
            _builder.Append(Mathf.RoundToInt((1f - manager.Lungs.OxygenNormalized) * 100f));
            _builder.Append("%\nINTERNAL ");
            _builder.Append(GetInternalBleedLabel(manager));
            _builder.Append("\nKNIVES ");
            _builder.Append(manager.KnifePenetration.EmbeddedCount);

            _text.text = _builder.ToString();
            float danger = Mathf.Max(manager.PainNormalized, 1f - manager.Bleeding.BloodNormalized, manager.Lungs.OxygenStress, manager.Brain.DisorientationNormalized);
            _text.color = Color.Lerp(new Color(0.86f, 0.95f, 1f, 0.90f), new Color(1f, 0.38f, 0.28f, 0.96f), Config.Clamp(danger, 0f, 1f));
        }

        public void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible)
                _root.SetActive(visible);
        }

        private void AppendOrgan(HealthManager manager, OrganType type, string label)
        {
            OrganHealth organ = manager.Organs.GetOrgan(type);
            _builder.Append(label);
            _builder.Append(' ');
            _builder.Append(Mathf.RoundToInt(organ.Integrity));
            _builder.Append("% ");
            _builder.Append(GetFailureLabel(organ.FailureState));
            _builder.Append('\n');
        }

        private static string GetFailureLabel(OrganFailureState state)
        {
            int index = (int)state;
            return index >= 0 && index < OrganFailureLabels.Length ? OrganFailureLabels[index] : "OK";
        }

        private static string GetBloodPressureLabel(HealthManager manager)
        {
            float blood = manager.Bleeding.BloodNormalized;
            float pulse = manager.Organs.HeartbeatStrength;
            if (manager.Organs.CardiacArrestActive)
                return "ARREST";
            if (blood < 0.42f || pulse < 0.35f)
                return "CRITICAL";
            if (blood < 0.62f || pulse < 0.58f)
                return "LOW";
            if (manager.Bleeding.TotalBleedRateMlPerSecond > 18f)
                return "DROPPING";
            return "STABLE";
        }

        private static string GetInternalBleedLabel(HealthManager manager)
        {
            if ((int)manager.Organs.GetOrgan(OrganType.Heart).FailureState >= (int)OrganFailureState.Damaged)
                return "CARDIAC";
            if ((int)manager.Organs.GetOrgan(OrganType.Liver).FailureState >= (int)OrganFailureState.Damaged)
                return "LIVER";
            if ((int)manager.Organs.GetOrgan(OrganType.Lungs).FailureState >= (int)OrganFailureState.Damaged)
                return "CHEST";
            return manager.Bleeding.TotalBleedRateMlPerSecond > 12f ? "ACTIVE" : "NONE";
        }
    }
}
