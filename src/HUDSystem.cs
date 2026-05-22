using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace BonelabAdvancedHealth
{
    public sealed class HUDSystem
    {
        private const float CanvasWidth = 1920f;
        private const float CanvasHeight = 1080f;
        private static readonly Vector3 HeadLocalPosition = new Vector3(0f, 0f, 0.58f);
        private static readonly Vector3 HeadLocalScale = Vector3.one * 0.00074f;
        private readonly StringBuilder _builder = new StringBuilder(512);
        private readonly Color32[] _noisePixels = new Color32[160 * 90];
        private readonly Image?[] _limbVisuals = new Image?[Config.LimbCount];
        private readonly FullscreenPainEffects _fullscreenPain = new FullscreenPainEffects();
        private readonly PostProcessingTraumaFX _postFx = new PostProcessingTraumaFX();
        private GameObject? _root;
        private GameObject? _statusRoot;
        private GameObject? _tarkovRoot;
        private Image? _statusBack;
        private Text? _vitalsText;
        private Text? _limbText;
        private Text? _conditionText;
        private Image? _bloodFill;
        private Image? _painFill;
        private Image? _blackout;
        private Image? _vignette;
        private Image? _desaturation;
        private Image? _painOverlay;
        private Image? _headHitOverlay;
        private Image? _noiseOverlay;
        private Texture2D? _noiseTexture;
        private Font? _font;
        private float _lastBlackout;
        private float _lastPain;
        private float _painPulse;
        private float _headHitPulse;
        private float _noiseTimer;
        private ConsciousnessState _lastState;
        private OrganMonitorUI? _organMonitor;

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

        public void UpdateRealtime(float deltaTime, HealthManager manager)
        {
            if (!Config.HudEnabled)
                return;

            EnsureCreated();
            if (_root == null)
                return;

            _painPulse = Mathf.Max(0f, _painPulse - deltaTime * 0.85f);
            _headHitPulse = Mathf.Max(0f, _headHitPulse - deltaTime * 0.55f);
            SetConsciousnessEffects(manager.Consciousness.BlackoutIntensity, manager.PainNormalized, manager.Consciousness.State);
            SetPhysiologicalEffects(manager, deltaTime);
            TraumaFrameFx frameFx = _postFx.Evaluate(manager, deltaTime);
            _fullscreenPain.Update(frameFx, manager);
            ApplyFrameFx(frameFx);
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
            _builder.Append("\nO2 ");
            _builder.Append(Mathf.RoundToInt(manager.Lungs.OxygenNormalized * 100f));
            _builder.Append("%");
            _builder.Append("\nSTATE ");
            _builder.Append(GetStateLabel(manager.Consciousness.State));
            _builder.Append("\nRIBS ");
            _builder.Append(manager.Bones.FracturedRibCount);
            _builder.Append("/12");
            if (manager.Bones.TotalBrokenBoneCount > manager.Bones.FracturedRibCount)
            {
                _builder.Append("\nBONES ");
                _builder.Append(manager.Bones.TotalBrokenBoneCount);
            }
            if (manager.Organs.CardiacArrestActive)
                _builder.Append("\nCARDIAC ARREST");
            else if (manager.Lungs.HasCollapsedLung)
                _builder.Append("\nLUNG TRAUMA");
            else if (manager.Brain.HasActiveConcussion)
                _builder.Append("\nCONCUSSION");
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

            UpdateTarkovInterface(manager);
            _organMonitor?.SetVisible(Config.HudMode != 0 && manager.Consciousness.State != ConsciousnessState.Unconscious);
            _organMonitor?.Update(manager);
            UpdateStatusVisibility(manager);
            UpdateRealtime(Config.SystemTickInterval, manager);
        }

        public void OnDamageVisual(DamageInfo info, OrganDamageFeedback feedback)
        {
            float intensity = Config.Clamp(info.Damage / 85f, 0f, 1f);
            if (info.BodyPart == BodyPart.Head)
                _headHitPulse = Mathf.Max(_headHitPulse, Config.Clamp(0.32f + intensity * 0.9f, 0f, 1.25f));
            if (info.Pain > 8f || feedback.Pain > 5f)
                _painPulse = Mathf.Max(_painPulse, Config.Clamp((info.Pain + feedback.Pain) / 90f, 0f, 1f));
        }

        public void SetConsciousnessEffects(float blackout, float pain, ConsciousnessState state)
        {
            _lastBlackout = Config.Clamp(blackout, 0f, 1f);
            _lastPain = Config.Clamp(pain, 0f, 1f);
            _lastState = state;

            if (_blackout == null || _vignette == null || _desaturation == null || _painOverlay == null || _headHitOverlay == null)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(1.2f, 3.8f, _lastBlackout + _lastPain));
            float unconsciousBoost = state == ConsciousnessState.Unconscious ? 0.28f + pulse * 0.08f : 0f;
            float blackAlpha = state == ConsciousnessState.Dead
                ? 0.96f
                : Config.Clamp(_lastBlackout * 0.72f + unconsciousBoost, 0f, 0.94f);
            float vignetteAlpha = state == ConsciousnessState.Dead
                ? 0.98f
                : Config.Clamp(0.18f + _lastBlackout * 0.72f + _lastPain * 0.18f + _headHitPulse * 0.18f, 0f, 0.96f);
            float greyAlpha = state == ConsciousnessState.Dead
                ? 0.45f
                : Config.Clamp(_lastBlackout * 0.22f + _lastPain * 0.10f + _headHitPulse * 0.18f, 0f, 0.52f);

            _blackout.color = new Color(0f, 0f, 0f, blackAlpha);
            _vignette.color = new Color(0f, 0f, 0f, vignetteAlpha);
            _desaturation.color = new Color(0.48f, 0.50f, 0.48f, greyAlpha);
            _painOverlay.color = new Color(0.45f, 0.0f, 0.0f, Config.Clamp(_lastPain * 0.12f + _painPulse * 0.22f, 0f, 0.38f));
            _headHitOverlay.color = new Color(0.95f, 0.96f, 0.90f, Config.Clamp(_headHitPulse * 0.28f, 0f, 0.42f));
        }

        public void SetPhysiologicalEffects(HealthManager manager, float deltaTime)
        {
            if (_blackout == null || _vignette == null || _desaturation == null || _noiseOverlay == null)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(1.4f, 4.8f, manager.PainNormalized + manager.Lungs.OxygenStress + _headHitPulse));
            float tunnel = Config.Clamp(manager.Consciousness.BlackoutIntensity * 0.60f + manager.Lungs.OxygenStress * 0.45f + manager.Brain.DisorientationNormalized * 0.40f, 0f, 1f);
            float darkAlpha = Config.Clamp(_blackout.color.a + tunnel * 0.14f + pulse * manager.PainNormalized * 0.045f, 0f, manager.Consciousness.State == ConsciousnessState.Dead ? 0.97f : 0.94f);
            float greyAlpha = Config.Clamp(_desaturation.color.a + manager.Lungs.OxygenStress * 0.22f + manager.Brain.DisorientationNormalized * 0.22f, 0f, 0.62f);
            float noiseAlpha = Config.Clamp(_headHitPulse * 0.18f + manager.Brain.RingingIntensity * 0.08f + manager.Lungs.WhiteNoiseIntensity * 0.10f, 0f, 0.24f);

            _blackout.color = new Color(0f, 0f, 0f, darkAlpha);
            _desaturation.color = new Color(0.48f, 0.50f, 0.48f, greyAlpha);
            _vignette.transform.localScale = Vector3.one * Mathf.Lerp(1.0f, 1.22f, tunnel + pulse * 0.06f);
            _noiseOverlay.color = new Color(1f, 1f, 1f, noiseAlpha);

            _noiseTimer += deltaTime;
            if (_noiseTimer >= 0.055f && noiseAlpha > 0.01f)
            {
                _noiseTimer = 0f;
                RefreshNoiseTexture(noiseAlpha);
            }

            UpdateStatusVisibility(manager);
        }

        public void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.Destroy(_root);
                _root = null;
            }

            _statusRoot = null;
            _statusBack = null;
            _vitalsText = null;
            _limbText = null;
            _conditionText = null;
            _tarkovRoot = null;
            _bloodFill = null;
            _painFill = null;
            _blackout = null;
            _vignette = null;
            _desaturation = null;
            _painOverlay = null;
            _headHitOverlay = null;
            _noiseOverlay = null;
            _noiseTexture = null;
            _organMonitor = null;
            _fullscreenPain.Reset();
            _postFx.Reset();
            for (int i = 0; i < _limbVisuals.Length; i++)
                _limbVisuals[i] = null;
        }

        private void Build(Transform head)
        {
            _font = Font.GetDefault();
            _root = new GameObject("AHS_Fullscreen_Trauma_HUD");
            Canvas canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 32000;
            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 12f;
            scaler.referencePixelsPerUnit = 100f;

            RectTransform rootRect = _root.GetComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
            AttachToHead(head);

            _desaturation = CreatePanel("Desaturation", _root.transform, FullSize(), Vector2.zero, new Color(0.48f, 0.50f, 0.48f, 0f)).GetComponent<Image>();
            _painOverlay = CreatePanel("PainPulse", _root.transform, FullSize(), Vector2.zero, new Color(0.45f, 0f, 0f, 0f)).GetComponent<Image>();
            _headHitOverlay = CreatePanel("HeadHitFlash", _root.transform, FullSize(), Vector2.zero, new Color(1f, 1f, 0.92f, 0f)).GetComponent<Image>();
            _vignette = CreatePanel("TunnelVignette", _root.transform, FullSize(), Vector2.zero, new Color(0f, 0f, 0f, 0f)).GetComponent<Image>();
            Sprite vignetteSprite = CreateVignetteSprite();
            _vignette.sprite = vignetteSprite;
            _blackout = CreatePanel("Blackout", _root.transform, FullSize(), Vector2.zero, new Color(0f, 0f, 0f, 0f)).GetComponent<Image>();
            _noiseOverlay = CreatePanel("Noise", _root.transform, FullSize(), Vector2.zero, new Color(1f, 1f, 1f, 0f)).GetComponent<Image>();
            _noiseTexture = new Texture2D(160, 90, TextureFormat.RGBA32, false);
            _noiseTexture.wrapMode = TextureWrapMode.Repeat;
            _noiseTexture.filterMode = FilterMode.Point;
            _noiseOverlay.sprite = Sprite.Create(_noiseTexture, new Rect(0f, 0f, 160f, 90f), new Vector2(0.5f, 0.5f), 100f);
            _fullscreenPain.Build(_root.transform, vignetteSprite);

            BuildTarkovInterface(_root.transform);

            _statusRoot = new GameObject("Status");
            _statusRoot.transform.SetParent(_root.transform, false);
            RectTransform statusRect = _statusRoot.AddComponent<RectTransform>();
            statusRect.sizeDelta = new Vector2(470f, 245f);
            statusRect.anchoredPosition = new Vector2(-650f, -332f);
            _statusBack = CreatePanel("StatusBack", _statusRoot.transform, new Vector2(470f, 245f), Vector2.zero, new Color(0f, 0f, 0f, 0.24f)).GetComponent<Image>();

            CreatePanel("BloodBack", _statusRoot.transform, new Vector2(405f, 14f), new Vector2(0f, 92f), new Color(0.03f, 0.03f, 0.03f, 0.58f));
            _bloodFill = CreatePanel("BloodFill", _statusRoot.transform, new Vector2(405f, 14f), new Vector2(0f, 92f), new Color(0.65f, 0.02f, 0.02f, 0.9f)).GetComponent<Image>();
            _bloodFill.type = Image.Type.Filled;
            _bloodFill.fillMethod = Image.FillMethod.Horizontal;

            CreatePanel("PainBack", _statusRoot.transform, new Vector2(405f, 8f), new Vector2(0f, 72f), new Color(0.03f, 0.03f, 0.03f, 0.52f));
            _painFill = CreatePanel("PainFill", _statusRoot.transform, new Vector2(405f, 8f), new Vector2(0f, 72f), new Color(0.9f, 0.55f, 0.05f, 0.82f)).GetComponent<Image>();
            _painFill.type = Image.Type.Filled;
            _painFill.fillMethod = Image.FillMethod.Horizontal;

            _vitalsText = CreateText("Vitals", _statusRoot.transform, new Vector2(212f, 150f), new Vector2(-108f, -20f), 18, TextAnchor.UpperLeft);
            _limbText = CreateText("Limbs", _statusRoot.transform, new Vector2(202f, 150f), new Vector2(112f, -20f), 15, TextAnchor.UpperLeft);

            _organMonitor = new OrganMonitorUI(_font);
            _organMonitor.Build(_root.transform);
            SetOverlayRaycasts(false);
        }

        private void BuildTarkovInterface(Transform parent)
        {
            _tarkovRoot = new GameObject("TarkovBodyMonitor");
            _tarkovRoot.transform.SetParent(parent, false);
            RectTransform rootRect = _tarkovRoot.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(430f, 600f);
            rootRect.anchoredPosition = new Vector2(-560f, -68f);

            Image back = _tarkovRoot.AddComponent<Image>();
            back.color = new Color(0f, 0f, 0f, 0.18f);
            back.raycastTarget = false;

            CreateLimbSegment(BodyPart.Head, new Vector2(74f, 70f), new Vector2(0f, 205f), 0f);
            CreateLimbSegment(BodyPart.Torso, new Vector2(126f, 215f), new Vector2(0f, 68f), 0f);
            CreateLimbSegment(BodyPart.LeftArm, new Vector2(58f, 245f), new Vector2(-112f, 48f), -13f);
            CreateLimbSegment(BodyPart.RightArm, new Vector2(58f, 245f), new Vector2(112f, 48f), 13f);
            CreateLimbSegment(BodyPart.LeftLeg, new Vector2(62f, 235f), new Vector2(-45f, -183f), 5f);
            CreateLimbSegment(BodyPart.RightLeg, new Vector2(62f, 235f), new Vector2(45f, -183f), -5f);

            GameObject spine = CreatePanel("SkeletonSpine", _tarkovRoot.transform, new Vector2(20f, 265f), new Vector2(0f, 26f), new Color(0.72f, 0.78f, 0.80f, 0.28f));
            spine.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            CreatePanel("RibHint", _tarkovRoot.transform, new Vector2(160f, 70f), new Vector2(0f, 100f), new Color(0.72f, 0.78f, 0.80f, 0.16f));
            _conditionText = CreateText("ConditionText", _tarkovRoot.transform, new Vector2(350f, 88f), new Vector2(0f, -255f), 16, TextAnchor.UpperCenter);
        }

        private void CreateLimbSegment(BodyPart part, Vector2 size, Vector2 position, float rotation)
        {
            if (_tarkovRoot == null)
                return;

            GameObject go = CreatePanel("Limb_" + part, _tarkovRoot.transform, size, position, new Color(0.62f, 0.68f, 0.70f, 0.30f));
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            _limbVisuals[(int)part] = go.GetComponent<Image>();
        }

        private void UpdateTarkovInterface(HealthManager manager)
        {
            if (_tarkovRoot == null)
                return;

            bool show = Config.HudMode != 0 && manager.Consciousness.State != ConsciousnessState.Unconscious;
            if (_tarkovRoot.activeSelf != show)
                _tarkovRoot.SetActive(show);
            if (!show)
                return;

            for (int i = 0; i < Config.LimbCount; i++)
            {
                Image? image = _limbVisuals[i];
                if (image == null)
                    continue;

                BodyPart part = (BodyPart)i;
                LimbHealth limb = manager.GetLimb(part);
                float damage = limb.DamagePercent;
                BleedSeverity bleed = manager.Bleeding.GetWorstBleedingSeverity(part);
                float bleedPulse = bleed == BleedSeverity.None ? 0f : 0.18f + Mathf.Sin(Time.time * 6.5f) * 0.10f;
                float fracturePulse = limb.Fracture == FractureState.None ? 0f : 0.22f + Mathf.Sin(Time.time * 9.0f) * 0.16f;
                Color baseColor = new Color(0.62f, 0.68f, 0.70f, 0.30f);
                Color damageColor = limb.Hp <= 1f
                    ? new Color(0.18f, 0f, 0f, 0.86f)
                    : Color.Lerp(baseColor, new Color(1f, 0.02f, 0f, 0.86f), Config.Clamp(damage + bleedPulse, 0f, 1f));
                if (fracturePulse > 0f)
                    damageColor = Color.Lerp(damageColor, new Color(1f, 0.42f, 0.02f, 0.94f), Config.Clamp(fracturePulse, 0f, 1f));
                image.color = damageColor;
            }

            if (_conditionText != null)
            {
                _builder.Length = 0;
                _builder.Append("PULSE ");
                _builder.Append(CalculatePulse(manager));
                _builder.Append("  BLOOD ");
                _builder.Append(Mathf.RoundToInt(manager.Bleeding.BloodNormalized * 100f));
                _builder.Append("%  O2 ");
                _builder.Append(Mathf.RoundToInt(manager.Lungs.OxygenNormalized * 100f));
                _builder.Append("%\n");
                _builder.Append(GetStateLabel(manager.Consciousness.State));
                if (manager.PainSystem.InPainShock)
                    _builder.Append("  PAIN SHOCK");
                if (manager.Brain.HasActiveConcussion)
                    _builder.Append("  CONCUSSION");
                if (manager.Bleeding.HasActiveBleeding)
                    _builder.Append("  BLEEDING");
                _conditionText.text = _builder.ToString();
                _conditionText.color = Color.Lerp(new Color(0.84f, 0.94f, 1f, 0.86f), new Color(1f, 0.28f, 0.18f, 0.96f), manager.PainNormalized);
            }
        }

        private void AttachToHead(Transform head)
        {
            if (_root == null)
                return;

            _root.transform.SetParent(head, false);
            _root.transform.localPosition = HeadLocalPosition;
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = HeadLocalScale;
        }

        private void ApplyFrameFx(TraumaFrameFx fx)
        {
            if (_root == null)
                return;

            Vector3 offset = new Vector3(fx.Offset.x * 0.00022f, fx.Offset.y * 0.00022f, 0f);
            _root.transform.localPosition = HeadLocalPosition + offset;
            _root.transform.localScale = HeadLocalScale * fx.Scale;
        }

        private void UpdateStatusVisibility(HealthManager manager)
        {
            if (_statusRoot == null || _statusBack == null || _vitalsText == null || _limbText == null || _bloodFill == null || _painFill == null)
                return;

            float target = manager.Consciousness.State == ConsciousnessState.Unconscious || manager.Consciousness.BlackoutIntensity > 0.72f ? 0f : 1f;
            bool active = target > 0.01f;
            if (_statusRoot.activeSelf != active)
                _statusRoot.SetActive(active);

            if (!active)
                return;

            float alpha = Config.Clamp(0.34f - manager.Consciousness.BlackoutIntensity * 0.28f, 0.04f, 0.34f);
            _statusBack.color = new Color(0f, 0f, 0f, alpha);
            Color textColor = new Color(0.88f, 0.96f, 1f, Config.Clamp(0.92f - manager.Consciousness.BlackoutIntensity * 0.42f, 0.32f, 0.92f));
            _vitalsText.color = textColor;
            _limbText.color = textColor;
            SetImageAlpha(_bloodFill, textColor.a);
            SetImageAlpha(_painFill, textColor.a);
        }

        private void RefreshNoiseTexture(float intensity)
        {
            if (_noiseTexture == null)
                return;

            byte alphaMax = (byte)Mathf.Clamp(Mathf.RoundToInt(80f * intensity), 0, 80);
            for (int i = 0; i < _noisePixels.Length; i++)
            {
                byte value = (byte)UnityEngine.Random.Range(180, 255);
                byte alpha = (byte)UnityEngine.Random.Range(0, alphaMax + 1);
                _noisePixels[i] = new Color32(value, value, value, alpha);
            }

            _noiseTexture.SetPixels32(_noisePixels);
            _noiseTexture.Apply(false, false);
        }

        private void SetOverlayRaycasts(bool raycastTarget)
        {
            SetRaycast(_blackout, raycastTarget);
            SetRaycast(_vignette, raycastTarget);
            SetRaycast(_desaturation, raycastTarget);
            SetRaycast(_painOverlay, raycastTarget);
            SetRaycast(_headHitOverlay, raycastTarget);
            SetRaycast(_noiseOverlay, raycastTarget);
        }

        private static Vector2 FullSize()
        {
            return new Vector2(CanvasWidth, CanvasHeight);
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
            text.color = new Color(0.88f, 0.96f, 1f, 0.92f);
            text.supportRichText = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 9;
            text.resizeTextMaxSize = fontSize;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite CreateVignetteSprite()
        {
            const int size = 256;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
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
                    float alpha = Mathf.SmoothStep(0.16f, 0.96f, d);
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void SetRaycast(Image? image, bool value)
        {
            if (image != null)
                image.raycastTarget = value;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            Color color = image.color;
            color.a = alpha;
            image.color = color;
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
            float pulse = 66f + manager.PainNormalized * 34f + bloodStress * 38f + manager.Bleeding.TotalBleedRateMlPerSecond * 0.14f;
            pulse += manager.Lungs.BreathingPanic * 20f;
            pulse *= Mathf.Lerp(0.40f, 1f, manager.PulseModifier);
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
