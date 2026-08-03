using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MelonLoader.Utils;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace BonelabAdvancedHealth
{
    public sealed class HUDSystem
    {
        private const float CanvasWidth = 1920f;
        private const float CanvasHeight = 1080f;
        private static readonly string[] StateLabels = { "AWAKE", "BLACKOUT", "UNCONSCIOUS", "DEAD" };
        private static readonly string[] BleedBadges = { "-", "L", "M", "S", "A" };
        private static readonly string[] FractureBadges = { "-", "SP", "BR", "SH" };
        private static readonly Dictionary<string, Sprite> UiSpriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private readonly StringBuilder _builder = new StringBuilder(512);
        private readonly Color32[] _noisePixels = new Color32[160 * 90];
        private readonly Image?[] _limbVisuals = new Image?[Config.LimbCount];
        private readonly PostProcessingTraumaFX _postFx = new PostProcessingTraumaFX();
        private GameObject? _root;
        private GameObject? _statusRoot;
        private GameObject? _monitorRoot;
        private GameObject? _bodyRoot;
        private GameObject? _statsRoot;
        private CanvasGroup? _monitorGroup;
        private Canvas? _canvas;
        private RectTransform? _canvasRect;
        private Image? _bodyBackdrop;
        private Image? _bodyImage;
        private Image? _neckVisual;
        private Image? _chestVisual;
        private Image? _stomachVisual;
        private Image? _statusBack;
        private Text? _vitalsText;
        private Text? _limbText;
        private Text? _conditionText;
        private Text? _treatmentText;
        private Text? _feedbackText;
        private Image? _oxygenBack;
        private Image? _bloodBack;
        private Image? _painBack;
        private Image? _oxygenFill;
        private Image? _bloodFill;
        private Image? _painFill;
        private Image? _oxygenIcon;
        private Image? _bloodIcon;
        private Image? _painIcon;
        private Image? _heartIcon;
        private Image? _shockIcon;
        private Image? _blackout;
        private Image? _vignette;
        private Image? _desaturation;
        private Image? _painOverlay;
        private Image? _headHitOverlay;
        private Image? _noiseOverlay;
        private Texture2D? _noiseTexture;
        private Texture2D? _bodyTexture;
        private Sprite? _bodySprite;
        private Font? _font;
        private float _lastBlackout;
        private float _lastPain;
        private float _painPulse;
        private float _headHitPulse;
        private float _noiseTimer;
        private float _feedbackSeconds;
        private string _feedbackMessage = string.Empty;
        private ConsciousnessState _lastState;
        private OrganMonitorUI? _organMonitor;
        private readonly BleedSource[] _bleedSourceScratch = new BleedSource[Config.MaxBleedSources];
        private bool _medicalMenuOpen;
        private bool _rightStickClickHeld;
        private float _lastMenuToggleTime;
        private bool _displayVitalsInitialized;
        private float _displayedBlood = 1f;
        private float _displayedPain;
        private float _displayedOxygen = 1f;

        public bool IsCreated => _root != null;

        public void EnsureCreated()
        {
            if (!Config.HudEnabled)
            {
                Destroy();
                return;
            }

            if (_root == null)
                Build();

            ApplyHudPresentation();
        }

        public void UpdateRealtime(float deltaTime, HealthManager manager)
        {
            if (!Config.HudEnabled)
                return;

            EnsureCreated();
            if (_root == null)
                return;

            UpdateMedicalMenuToggle();
            if (!Config.MedicalMenuEnabled && _medicalMenuOpen)
                _medicalMenuOpen = false;
            ApplyHudPresentation();
            _painPulse = Mathf.Max(0f, _painPulse - deltaTime * 0.85f);
            _headHitPulse = Mathf.Max(0f, _headHitPulse - deltaTime * 0.55f);
            SetConsciousnessEffects(manager.Consciousness.BlackoutIntensity, manager.PainNormalized, manager.Consciousness.State);
            SetPhysiologicalEffects(manager, deltaTime);
            UpdateBodyHighlights(manager);
            UpdateMedicalFeedback(deltaTime);
            TraumaFrameFx frameFx = _postFx.Evaluate(manager, deltaTime);
            ApplyFrameFx(frameFx);
            UpdateHudVisibility(manager, deltaTime);
        }

        public void UpdateHud(HealthManager manager)
        {
            if (!Config.HudEnabled)
                return;

            EnsureCreated();
            if (_root == null || _vitalsText == null || _limbText == null || _bloodFill == null || _painFill == null || _oxygenFill == null)
                return;

            ApplyHudPresentation();

            float blood = manager.Bleeding.BloodVolumeMl;
            bool menuOpen = _medicalMenuOpen && Config.MedicalMenuEnabled;
            bool showStats = WantsStats() && (!menuOpen || Config.HudShowVitalSigns);
            bool showBody = WantsBody();
            bool showInjuryNames = WantsInjuryNames();
            bool showBlood = showStats && Config.HudShowBlood;
            bool showPain = showStats && Config.HudShowPain;
            bool showOxygen = showStats && Config.HudShowOxygen;
            bool showFractures = menuOpen && Config.HudShowFractures;
            bool showBleeding = menuOpen && Config.HudShowBleeding;
            bool showConsciousness = menuOpen && Config.HudShowConsciousness;
            bool showOrganStatus = menuOpen && Config.ShowDetailedStats && Config.HudShowOrganStatus;

            SetGameObjectActive(_bodyRoot, showBody);
            SetGameObjectActive(_statsRoot, showStats);
            SetGameObjectActive(_bloodFill != null ? _bloodFill.gameObject : null, showBlood);
            SetGameObjectActive(_painFill != null ? _painFill.gameObject : null, showPain);
            SetGameObjectActive(_oxygenFill != null ? _oxygenFill.gameObject : null, showOxygen);
            SetGameObjectActive(_bloodIcon != null ? _bloodIcon.gameObject : null, showBlood);
            SetGameObjectActive(_painIcon != null ? _painIcon.gameObject : null, showPain);
            SetGameObjectActive(_oxygenIcon != null ? _oxygenIcon.gameObject : null, showOxygen);
            bool showDetailedVitalIcons = showStats && (_medicalMenuOpen && Config.MedicalMenuEnabled) && Config.ShowDetailedStats;
            SetGameObjectActive(_heartIcon != null ? _heartIcon.gameObject : null, showDetailedVitalIcons);
            SetGameObjectActive(_shockIcon != null ? _shockIcon.gameObject : null, showDetailedVitalIcons);
            SetGameObjectActive(_limbText != null ? _limbText.gameObject : null, showStats && showInjuryNames && (showFractures || showBleeding || showConsciousness || showOrganStatus));
            SetGameObjectActive(_vitalsText != null ? _vitalsText.gameObject : null, showStats);

            Image? bloodFill = _bloodFill;
            Image? painFill = _painFill;
            Image? oxygenFill = _oxygenFill;
            Text? vitalsText = _vitalsText;
            Text? limbText = _limbText;
            float targetBlood = manager.Bleeding.BloodNormalized;
            float targetPain = manager.PainNormalized;
            float targetOxygen = manager.Lungs.OxygenNormalized;
            if (!_displayVitalsInitialized)
            {
                _displayedBlood = targetBlood;
                _displayedPain = targetPain;
                _displayedOxygen = targetOxygen;
                _displayVitalsInitialized = true;
            }

            float barSpeed = (_medicalMenuOpen && Config.MedicalMenuEnabled) ? 4.5f : 3.2f;
            float step = Mathf.Max(Time.deltaTime, 0.001f) * barSpeed;
            _displayedBlood = Mathf.MoveTowards(_displayedBlood, targetBlood, step);
            _displayedPain = Mathf.MoveTowards(_displayedPain, targetPain, step);
            _displayedOxygen = Mathf.MoveTowards(_displayedOxygen, targetOxygen, step);

            if (showBlood && bloodFill != null)
            {
                bloodFill.fillAmount = _displayedBlood;
                bloodFill.color = GetBloodColor(_displayedBlood);
            }

            if (showPain && painFill != null)
                painFill.fillAmount = _displayedPain;

            if (showOxygen && oxygenFill != null)
            {
                oxygenFill.fillAmount = _displayedOxygen;
                oxygenFill.color = Color.Lerp(new Color(1f, 0.24f, 0.18f, 0.92f), new Color(0.24f, 0.84f, 1f, 0.92f), _displayedOxygen);
            }
            UpdateVitalIconColors(manager);

            _builder.Length = 0;
            if (showOxygen)
            {
                _builder.Append("O2     ");
                _builder.Append(Mathf.RoundToInt(manager.Lungs.OxygenNormalized * 100f));
                _builder.Append("%\n");
            }
            if (showBlood)
            {
                _builder.Append("BLOOD  ");
                _builder.Append(Mathf.RoundToInt(blood));
                _builder.Append(" ml\n");
            }
            if (showPain)
            {
                _builder.Append("PAIN   ");
                _builder.Append(Mathf.RoundToInt(manager.Pain));
                _builder.Append('\n');
            }
            if (showConsciousness && Config.ShowDetailedStats)
            {
                _builder.Append("STATE  ");
                _builder.Append(GetStateLabel(manager.Consciousness.State));
                _builder.Append('\n');
                _builder.Append("PULSE  ");
                _builder.Append(Mathf.RoundToInt(manager.TelemetrySnapshot.HeartbeatBpm));
                _builder.Append(" bpm\n");
                _builder.Append("LOSS   ");
                _builder.Append(Mathf.RoundToInt(manager.TelemetrySnapshot.BloodLossNormalized * 100f));
                _builder.Append("%\n");
                _builder.Append("SHOCK  ");
                _builder.Append(Mathf.RoundToInt(manager.TelemetrySnapshot.ShockNormalized * 100f));
                _builder.Append('%');
            }
            if (vitalsText != null)
                vitalsText.text = _builder.ToString();

            _builder.Length = 0;
            if (menuOpen && Config.ShowDetailedStats)
                AppendInspectionSummary(_builder, manager);
            else
                AppendInjuryAlerts(_builder, manager, showFractures, showBleeding, showConsciousness, showOrganStatus);
            if (limbText != null)
                limbText.text = _builder.ToString();

            UpdateMedicalMonitor(manager);
            UpdateHudVisibility(manager, Time.deltaTime);
            UpdateStatusVisibility(manager);
        }

        public void OnDamageVisual(DamageInfo info, OrganDamageFeedback feedback)
        {
            float intensity = Config.Clamp(info.Damage / 85f, 0f, 1f);
            if (info.BodyPart == BodyPart.Head)
                _headHitPulse = Mathf.Max(_headHitPulse, Config.Clamp(0.32f + intensity * 0.9f, 0f, 1.25f));
            if (info.Pain > 8f || feedback.Pain > 5f)
                _painPulse = Mathf.Max(_painPulse, Config.Clamp((info.Pain + feedback.Pain) / 90f, 0f, 1f));
        }

        public void ShowMedicalFeedback(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            _feedbackMessage = message;
            _feedbackSeconds = 2.4f;
            if (_feedbackText != null)
            {
                _feedbackText.text = message;
                _feedbackText.color = new Color(0.74f, 1f, 0.78f, 1f);
            }
        }

        public void SetConsciousnessEffects(float blackout, float pain, ConsciousnessState state)
        {
            _lastBlackout = Config.Clamp(blackout, 0f, 1f);
            _lastPain = Config.Clamp(pain, 0f, 1f);
            _lastState = state;

            if (_blackout == null || _vignette == null || _desaturation == null || _painOverlay == null || _headHitOverlay == null)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(1.2f, 3.8f, _lastBlackout + _lastPain));
            float unconsciousBoost = state == ConsciousnessState.Unconscious ? 0.84f + pulse * 0.08f : 0f;
            float blackAlpha = state == ConsciousnessState.Dead
                ? 0.99f
                : Config.Clamp(_lastBlackout * 0.78f + unconsciousBoost, 0f, state == ConsciousnessState.Unconscious ? 0.995f : 0.96f);
            float vignetteAlpha = state == ConsciousnessState.Dead
                ? 0.98f
                : Config.Clamp(0.18f + _lastBlackout * 0.72f + _headHitPulse * 0.18f, 0f, 0.96f);
            float greyAlpha = state == ConsciousnessState.Dead
                ? 0.45f
                : Config.Clamp(_lastBlackout * 0.22f + _headHitPulse * 0.18f, 0f, 0.52f);

            _blackout.color = new Color(0f, 0f, 0f, blackAlpha);
            _vignette.color = new Color(0f, 0f, 0f, vignetteAlpha);
            _desaturation.color = new Color(0.48f, 0.50f, 0.48f, greyAlpha);
            _painOverlay.color = new Color(0.45f, 0.0f, 0.0f, 0f);
            _headHitOverlay.color = new Color(0.95f, 0.96f, 0.90f, Config.Clamp(_headHitPulse * 0.28f, 0f, 0.42f));
        }

        public void SetPhysiologicalEffects(HealthManager manager, float deltaTime)
        {
            if (_blackout == null || _vignette == null || _desaturation == null || _noiseOverlay == null)
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(1.4f, 4.8f, manager.PainNormalized + manager.Lungs.OxygenStress + _headHitPulse));
            float tunnel = Config.Clamp(manager.Consciousness.BlackoutIntensity * 0.60f + manager.Lungs.OxygenStress * 0.45f + manager.Brain.DisorientationNormalized * 0.40f, 0f, 1f);
            float baseAlpha = manager.Consciousness.State == ConsciousnessState.Dead
                ? 0.98f
                : manager.Consciousness.State == ConsciousnessState.Unconscious
                    ? 0.72f
                    : 0.04f;
            float darkAlpha = Config.Clamp(baseAlpha + tunnel * 0.14f, 0f, manager.Consciousness.State == ConsciousnessState.Unconscious ? 0.995f : 0.96f);
            float greyAlpha = Config.Clamp(manager.Lungs.OxygenStress * 0.22f + manager.Brain.DisorientationNormalized * 0.22f, 0f, 0.62f);
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
            _treatmentText = null;
            _feedbackText = null;
            _oxygenBack = null;
            _bloodBack = null;
            _painBack = null;
            _feedbackSeconds = 0f;
            _feedbackMessage = string.Empty;
            _monitorRoot = null;
            _bodyRoot = null;
            _statsRoot = null;
            _monitorGroup = null;
            _canvas = null;
            _canvasRect = null;
            _bodyBackdrop = null;
            _bodyImage = null;
            _neckVisual = null;
            _chestVisual = null;
            _stomachVisual = null;
            _oxygenFill = null;
            _bloodFill = null;
            _painFill = null;
            _oxygenIcon = null;
            _bloodIcon = null;
            _painIcon = null;
            _heartIcon = null;
            _shockIcon = null;
            _blackout = null;
            _vignette = null;
            _desaturation = null;
            _painOverlay = null;
            _headHitOverlay = null;
            _noiseOverlay = null;
            if (_noiseTexture != null)
            {
                UnityEngine.Object.Destroy(_noiseTexture);
                _noiseTexture = null;
            }
            if (_bodySprite != null)
            {
                UnityEngine.Object.Destroy(_bodySprite);
                _bodySprite = null;
            }
            if (_bodyTexture != null)
            {
                UnityEngine.Object.Destroy(_bodyTexture);
                _bodyTexture = null;
            }
            if (_organMonitor != null)
            {
                _organMonitor.SetVisible(false);
                _organMonitor = null;
            }
            _medicalMenuOpen = false;
            _rightStickClickHeld = false;
            _lastMenuToggleTime = 0f;
            _displayVitalsInitialized = false;
            _displayedBlood = 1f;
            _displayedPain = 0f;
            _displayedOxygen = 1f;
            _postFx.Reset();
            for (int i = 0; i < _limbVisuals.Length; i++)
                _limbVisuals[i] = null;
        }

        private void Build()
        {
            _font = Font.GetDefault();
            GameObject existing = GameObject.Find("ZBoneCity_Trauma_And_Medical_HUD");
            if (existing != null)
                UnityEngine.Object.Destroy(existing);

            _root = new GameObject("ZBoneCity_Trauma_And_Medical_HUD");
            _canvas = _root.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 32000;
            CanvasScaler scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            _canvasRect = _root.GetComponent<RectTransform>();
            _canvasRect.anchorMin = Vector2.zero;
            _canvasRect.anchorMax = Vector2.one;
            _canvasRect.pivot = new Vector2(0.5f, 0.5f);
            _canvasRect.offsetMin = Vector2.zero;
            _canvasRect.offsetMax = Vector2.zero;
            UnityEngine.Object.DontDestroyOnLoad(_root);

            BuildMedicalMonitor(_root.transform);
            SetOverlayRaycasts(false);
            DisableAllRaycasts(_root);
            MainMod.Runtime?.Logger.Msg("[ZBoneCity] UI Loaded");
        }

        private void BuildMedicalMonitor(Transform parent)
        {
            _monitorRoot = new GameObject("ZBoneCityMedicalMonitor");
            _monitorRoot.transform.SetParent(parent, false);
            RectTransform rootRect = _monitorRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(1f, 0.5f);
            rootRect.anchorMax = new Vector2(1f, 0.5f);
            rootRect.pivot = new Vector2(1f, 0.5f);
            rootRect.sizeDelta = new Vector2(520f, 520f);
            rootRect.anchoredPosition = new Vector2(-185f, 0f);
            _monitorGroup = _monitorRoot.AddComponent<CanvasGroup>();
            _monitorGroup.alpha = 0f;
            _monitorGroup.interactable = false;
            _monitorGroup.blocksRaycasts = false;

            Image back = _monitorRoot.AddComponent<Image>();
            back.color = new Color(0.012f, 0.014f, 0.017f, 0.46f);
            back.raycastTarget = false;
            _statusRoot = _monitorRoot;
            _statusBack = back;

            _conditionText = CreateText("MonitorTitle", _monitorRoot.transform, new Vector2(486f, 34f), new Vector2(-3f, 232f), 16, TextAnchor.MiddleLeft);
            _conditionText.text = "ZBONECITY";
            _conditionText.color = GetThemeAccent(0.94f);

            GameObject bodyRoot = new GameObject("BodyRoot");
            bodyRoot.transform.SetParent(_monitorRoot.transform, false);
            RectTransform bodyRootRect = bodyRoot.AddComponent<RectTransform>();
            bodyRootRect.anchorMin = new Vector2(0f, 0.5f);
            bodyRootRect.anchorMax = new Vector2(0f, 0.5f);
            bodyRootRect.pivot = new Vector2(0f, 0.5f);
            bodyRootRect.sizeDelta = new Vector2(205f, 430f);
            bodyRootRect.anchoredPosition = new Vector2(18f, -15f);
            _bodyRoot = bodyRoot;

            _bodyBackdrop = CreatePanel("BodyBackdrop", bodyRoot.transform, new Vector2(190f, 416f), Vector2.zero, new Color(0.02f, 0.03f, 0.03f, 0.32f)).GetComponent<Image>();
            _bodyBackdrop.raycastTarget = false;

            _bodySprite = LoadBodySprite();
            _bodyImage = CreatePanel("BodyImage", bodyRoot.transform, new Vector2(152f, 382f), Vector2.zero, Color.white).GetComponent<Image>();
            _bodyImage.sprite = _bodySprite;
            _bodyImage.type = Image.Type.Simple;
            _bodyImage.preserveAspect = true;
            _bodyImage.color = new Color(0.24f, 0.30f, 0.34f, Config.HudBodyOpacity * 0.10f);

            CreateLimbSegment(bodyRoot.transform, BodyPart.Head, new Vector2(46f, 42f), new Vector2(0f, 151f), 0f);
            _neckVisual = CreateBodyRegionSegment(bodyRoot.transform, "Neck", new Vector2(30f, 30f), new Vector2(0f, 122f), 0f);
            CreateLimbSegment(bodyRoot.transform, BodyPart.Torso, new Vector2(86f, 132f), new Vector2(0f, 63f), 0f);
            _chestVisual = CreateBodyRegionSegment(bodyRoot.transform, "Chest", new Vector2(92f, 70f), new Vector2(0f, 98f), 0f);
            _stomachVisual = CreateBodyRegionSegment(bodyRoot.transform, "Stomach", new Vector2(78f, 70f), new Vector2(0f, 35f), 0f);
            CreateLimbSegment(bodyRoot.transform, BodyPart.LeftArm, new Vector2(38f, 156f), new Vector2(-58f, 54f), 10f);
            CreateLimbSegment(bodyRoot.transform, BodyPart.RightArm, new Vector2(38f, 156f), new Vector2(58f, 54f), -10f);
            CreateLimbSegment(bodyRoot.transform, BodyPart.LeftLeg, new Vector2(38f, 174f), new Vector2(-28f, -113f), 2f);
            CreateLimbSegment(bodyRoot.transform, BodyPart.RightLeg, new Vector2(38f, 174f), new Vector2(28f, -113f), -2f);

            GameObject statsRoot = new GameObject("StatsRoot");
            statsRoot.transform.SetParent(_monitorRoot.transform, false);
            RectTransform statsRect = statsRoot.AddComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(1f, 0.5f);
            statsRect.anchorMax = new Vector2(1f, 0.5f);
            statsRect.pivot = new Vector2(1f, 0.5f);
            statsRect.sizeDelta = new Vector2(266f, 386f);
            statsRect.anchoredPosition = new Vector2(-18f, 18f);
            _statsRoot = statsRoot;

            _oxygenBack = CreatePanel("O2Back", statsRoot.transform, new Vector2(242f, 10f), new Vector2(0f, 154f), new Color(0.03f, 0.03f, 0.03f, 0.56f)).GetComponent<Image>();
            _oxygenFill = CreatePanel("O2Fill", statsRoot.transform, new Vector2(242f, 10f), new Vector2(0f, 154f), new Color(0.24f, 0.84f, 1f, 0.92f)).GetComponent<Image>();
            _oxygenFill.type = Image.Type.Filled;
            _oxygenFill.fillMethod = Image.FillMethod.Horizontal;
            _oxygenIcon = CreateHudIcon("O2Icon", statsRoot.transform, "status_oxygen.png", new Vector2(34f, 34f), new Vector2(-146f, 154f));

            _bloodBack = CreatePanel("BloodBack", statsRoot.transform, new Vector2(242f, 10f), new Vector2(0f, 132f), new Color(0.03f, 0.03f, 0.03f, 0.56f)).GetComponent<Image>();
            _bloodFill = CreatePanel("BloodFill", statsRoot.transform, new Vector2(242f, 10f), new Vector2(0f, 132f), new Color(0.65f, 0.02f, 0.02f, 0.92f)).GetComponent<Image>();
            _bloodFill.type = Image.Type.Filled;
            _bloodFill.fillMethod = Image.FillMethod.Horizontal;
            _bloodIcon = CreateHudIcon("BloodIcon", statsRoot.transform, "status_blood_loss.png", new Vector2(34f, 34f), new Vector2(-146f, 132f));

            _painBack = CreatePanel("PainBack", statsRoot.transform, new Vector2(242f, 9f), new Vector2(0f, 110f), new Color(0.03f, 0.03f, 0.03f, 0.52f)).GetComponent<Image>();
            _painFill = CreatePanel("PainFill", statsRoot.transform, new Vector2(242f, 9f), new Vector2(0f, 110f), new Color(0.9f, 0.55f, 0.05f, 0.88f)).GetComponent<Image>();
            _painFill.type = Image.Type.Filled;
            _painFill.fillMethod = Image.FillMethod.Horizontal;
            _painIcon = CreateHudIcon("PainIcon", statsRoot.transform, "status_pain_icon.png", new Vector2(34f, 34f), new Vector2(-146f, 110f));
            _heartIcon = CreateHudIcon("HeartIcon", statsRoot.transform, "status_arrhythmia.png", new Vector2(34f, 34f), new Vector2(-146f, 82f));
            _shockIcon = CreateHudIcon("ShockIcon", statsRoot.transform, "status_shock.png", new Vector2(34f, 34f), new Vector2(-146f, 58f));

            _vitalsText = CreateText("Vitals", statsRoot.transform, new Vector2(242f, 98f), new Vector2(0f, 44f), 17, TextAnchor.UpperLeft);
            _limbText = CreateText("ActiveInjuries", statsRoot.transform, new Vector2(242f, 154f), new Vector2(0f, -86f), 14, TextAnchor.UpperLeft);
            _treatmentText = CreateText("TreatmentText", _monitorRoot.transform, new Vector2(486f, 78f), new Vector2(-2f, -188f), 14, TextAnchor.UpperLeft);
            _treatmentText.color = new Color(0.98f, 0.86f, 0.58f, 0.96f);
            _feedbackText = CreateText("MedicalFeedback", _monitorRoot.transform, new Vector2(486f, 28f), new Vector2(-2f, -242f), 15, TextAnchor.MiddleCenter);
            _feedbackText.text = string.Empty;
            _feedbackText.color = new Color(0.74f, 1f, 0.78f, 0f);

            _organMonitor = new OrganMonitorUI(_font ?? Font.GetDefault());
            _organMonitor.Build(_monitorRoot.transform);
            _organMonitor.SetVisible(false);
        }

        private void CreateLimbSegment(Transform parent, BodyPart part, Vector2 size, Vector2 position, float rotation)
        {
            if (_monitorRoot == null)
                return;

            GameObject go = CreatePanel("Limb_" + part, parent, size, position, new Color(0f, 0f, 0f, 0f));
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = go.GetComponent<Image>();
            image.type = Image.Type.Simple;
            Sprite? sprite = LoadHudSprite(GetBodyPartIconName(part));
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
            }
            _limbVisuals[(int)part] = image;
        }

        private Image CreateBodyRegionSegment(Transform parent, string name, Vector2 size, Vector2 position, float rotation)
        {
            GameObject go = CreatePanel("BodyRegion_" + name, parent, size, position, new Color(0f, 0f, 0f, 0f));
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            Image image = go.GetComponent<Image>();
            image.type = Image.Type.Simple;
            Sprite? sprite = LoadHudSprite(name == "Neck" ? "status_spine_fracture.png" : "status_organ_damage.png");
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
            }
            return image;
        }

        private void UpdateMedicalMonitor(HealthManager manager)
        {
            if (_monitorRoot == null)
                return;

            if (!_monitorRoot.activeSelf)
                _monitorRoot.SetActive(true);
            UpdateHudVisibility(manager, Time.deltaTime);
            if (!Config.HudEnabled)
                return;

            if (_conditionText != null)
            {
                SetGameObjectActive(_conditionText.gameObject, _medicalMenuOpen && Config.MedicalMenuEnabled);
                _conditionText.text = _medicalMenuOpen
                    ? (manager.Consciousness.State == ConsciousnessState.Dead ? "MEDICAL MENU - DECEASED" : "MEDICAL MENU")
                    : "ZBONECITY VITALS";
                _conditionText.color = Color.Lerp(GetThemeAccent(0.98f), new Color(1f, 0.32f, 0.22f, 1f), manager.PainNormalized);
            }

            if (_treatmentText != null)
            {
                bool showTreatment = ShouldShowTreatmentPanel();
                SetGameObjectActive(_treatmentText.gameObject, showTreatment);
                if (showTreatment)
                {
                    _builder.Length = 0;
                    AppendTreatmentAdvice(_builder, manager);
                    _treatmentText.text = _builder.Length == 0 ? "STABLE\nNo treatment required" : _builder.ToString();
                }
                else
                {
                    _treatmentText.text = string.Empty;
                }
            }

            if (_organMonitor != null)
            {
                bool showOrgans = ShouldShowOrganMonitor();
                _organMonitor.SetVisible(showOrgans);
                if (showOrgans)
                    _organMonitor.Update(manager);
            }
        }

        private bool ShouldShowTreatmentPanel()
        {
            return _medicalMenuOpen && Config.MedicalMenuEnabled && Config.HudShowTreatment;
        }

        private bool ShouldShowOrganMonitor()
        {
            if (!_medicalMenuOpen || !Config.MedicalMenuEnabled || !Config.ShowDetailedStats || !Config.HudShowOrganStatus)
                return false;
            return true;
        }

        private void ApplyHudPresentation()
        {
            if (_root == null)
                return;

            ApplyCanvasMode();
            ApplyPresetLayout();

            if (_statusBack != null)
                _statusBack.color = _medicalMenuOpen
                    ? new Color(0.006f, 0.008f, 0.011f, Config.Clamp(Config.MedicalMenuOpacity, 0.88f, 1f))
                    : new Color(0.012f, 0.014f, 0.017f, 0.46f);
            if (_bodyBackdrop != null)
                _bodyBackdrop.color = _medicalMenuOpen
                    ? new Color(0.014f, 0.020f, 0.024f, 0.76f)
                    : new Color(0.02f, 0.03f, 0.03f, 0.10f);
        }

        private void ApplyCanvasMode()
        {
            if (_root == null || _canvas == null || _canvasRect == null)
                return;

            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.worldCamera = null;
            _canvas.sortingOrder = 32000;
            _root.transform.SetParent(null, false);
            _root.transform.localPosition = Vector3.zero;
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;
            _canvasRect.anchorMin = Vector2.zero;
            _canvasRect.anchorMax = Vector2.one;
            _canvasRect.pivot = new Vector2(0.5f, 0.5f);
            _canvasRect.offsetMin = Vector2.zero;
            _canvasRect.offsetMax = Vector2.zero;
        }

        private void ApplyPresetLayout()
        {
            if (_monitorRoot == null)
                return;

            RectTransform monitor = _monitorRoot.GetComponent<RectTransform>();
            RectTransform? body = _bodyRoot != null ? _bodyRoot.GetComponent<RectTransform>() : null;
            RectTransform? stats = _statsRoot != null ? _statsRoot.GetComponent<RectTransform>() : null;
            Vector2 screenOffset = GetHudScreenOffset();
            monitor.localScale = Vector3.one * GetCurrentHudScaleMultiplier();
            if (_bodyRoot != null)
                _bodyRoot.transform.localScale = (_medicalMenuOpen && Config.MedicalMenuEnabled) ? Vector3.one * 1.72f : Vector3.one;

            if (_medicalMenuOpen && Config.MedicalMenuEnabled)
            {
                monitor.anchorMin = new Vector2(0.5f, 0.5f);
                monitor.anchorMax = new Vector2(0.5f, 0.5f);
                monitor.pivot = new Vector2(0.5f, 0.5f);
                SetRect(monitor, new Vector2(1720f, 980f), screenOffset);
                SetRect(body, new Vector2(520f, 800f), new Vector2(0f, -8f));
                SetRect(stats, new Vector2(500f, 738f), new Vector2(560f, 70f));
                SetRect(_conditionText, new Vector2(1600f, 72f), new Vector2(0f, 440f));
                SetBar(_oxygenBack, _oxygenFill, new Vector2(444f, 30f), new Vector2(0f, 294f));
                SetBar(_bloodBack, _bloodFill, new Vector2(444f, 30f), new Vector2(0f, 230f));
                SetBar(_painBack, _painFill, new Vector2(444f, 30f), new Vector2(0f, 166f));
                SetRect(_oxygenIcon, new Vector2(54f, 54f), new Vector2(-262f, 294f));
                SetRect(_bloodIcon, new Vector2(54f, 54f), new Vector2(-262f, 230f));
                SetRect(_painIcon, new Vector2(54f, 54f), new Vector2(-262f, 166f));
                SetRect(_heartIcon, new Vector2(50f, 50f), new Vector2(-262f, 104f));
                SetRect(_shockIcon, new Vector2(50f, 50f), new Vector2(-262f, 48f));
                SetRect(_vitalsText, new Vector2(444f, 222f), new Vector2(0f, 40f));
                SetRect(_limbText, new Vector2(470f, 330f), new Vector2(0f, -244f));
                SetRect(_treatmentText, new Vector2(500f, 188f), new Vector2(560f, -346f));
                SetRect(_feedbackText, new Vector2(760f, 56f), new Vector2(0f, -438f));
                ApplyMenuFontSizes(true);
                return;
            }

            Vector2 miniPosition = GetMiniHudAnchorAndPosition(monitor) + screenOffset;
            SetRect(monitor, new Vector2(540f, 242f), miniPosition);
            SetRect(stats, new Vector2(492f, 198f), new Vector2(-24f, -2f));
            SetRect(_conditionText, new Vector2(492f, 34f), new Vector2(-24f, 94f));
            SetBar(_oxygenBack, _oxygenFill, new Vector2(430f, 15f), new Vector2(0f, 62f));
            SetBar(_bloodBack, _bloodFill, new Vector2(430f, 15f), new Vector2(0f, 28f));
            SetBar(_painBack, _painFill, new Vector2(430f, 15f), new Vector2(0f, -6f));
            SetRect(_oxygenIcon, new Vector2(30f, 30f), new Vector2(-250f, 62f));
            SetRect(_bloodIcon, new Vector2(30f, 30f), new Vector2(-250f, 28f));
            SetRect(_painIcon, new Vector2(30f, 30f), new Vector2(-250f, -6f));
            SetRect(_heartIcon, new Vector2(26f, 26f), new Vector2(-250f, -40f));
            SetRect(_shockIcon, new Vector2(26f, 26f), new Vector2(-250f, -72f));
            SetRect(_vitalsText, new Vector2(430f, 112f), new Vector2(0f, -70f));
            SetRect(_limbText, new Vector2(20f, 10f), new Vector2(0f, -95f));
            SetRect(_treatmentText, new Vector2(20f, 10f), new Vector2(0f, -95f));
            SetRect(_feedbackText, new Vector2(492f, 26f), new Vector2(-24f, -108f));
            ApplyMenuFontSizes(false);
        }

        private void ApplyMenuFontSizes(bool menuOpen)
        {
            if (menuOpen)
            {
                SetFontSize(_conditionText, 48, 30);
                SetFontSize(_vitalsText, 38, 25);
                SetFontSize(_limbText, 31, 20);
                SetFontSize(_treatmentText, 30, 20);
                SetFontSize(_feedbackText, 30, 20);
                return;
            }

            SetFontSize(_conditionText, 24, 16);
            SetFontSize(_vitalsText, 22, 15);
            SetFontSize(_limbText, 14, 9);
            SetFontSize(_treatmentText, 14, 9);
            SetFontSize(_feedbackText, 18, 12);
        }

        private static void SetFontSize(Text? text, int maxSize, int minSize)
        {
            if (text == null)
                return;
            text.fontSize = maxSize;
            text.resizeTextMaxSize = maxSize;
            text.resizeTextMinSize = minSize;
        }

        private static void SetBar(Image? back, Image? fill, Vector2 size, Vector2 position)
        {
            SetRect(back, size, position);
            SetRect(fill, size, position);
        }

        private static void SetRect(Graphic? graphic, Vector2 size, Vector2 position)
        {
            if (graphic == null)
                return;
            SetRect(graphic.GetComponent<RectTransform>(), size, position);
        }

        private static void SetRect(RectTransform? rect, Vector2 size, Vector2 position)
        {
            if (rect == null)
                return;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private Vector2 GetMiniHudAnchorAndPosition(RectTransform monitor)
        {
            switch (Config.HudPosition)
            {
                case 0:
                    monitor.anchorMin = new Vector2(0f, 0.5f);
                    monitor.anchorMax = new Vector2(0f, 0.5f);
                    monitor.pivot = new Vector2(0f, 0.5f);
                    return new Vector2(60f, -70f);
                case 2:
                    monitor.anchorMin = new Vector2(0.5f, 0.5f);
                    monitor.anchorMax = new Vector2(0.5f, 0.5f);
                    monitor.pivot = new Vector2(0.5f, 0.5f);
                    return new Vector2(0f, -70f);
                case 3:
                    monitor.anchorMin = new Vector2(0.5f, 0.5f);
                    monitor.anchorMax = new Vector2(0.5f, 0.5f);
                    monitor.pivot = new Vector2(0.5f, 0.5f);
                    return Vector2.zero;
                case 1:
                default:
                    monitor.anchorMin = new Vector2(1f, 0.5f);
                    monitor.anchorMax = new Vector2(1f, 0.5f);
                    monitor.pivot = new Vector2(1f, 0.5f);
                    return new Vector2(-60f, -70f);
            }
        }

        private static Vector2 GetHudScreenOffset()
        {
            if (Config.HudPosition != 3)
                return Vector2.zero;

            return new Vector2(
                Config.HudOffsetX * CanvasWidth * 0.42f,
                Config.HudOffsetY * CanvasHeight * 0.42f);
        }

        private float GetCurrentHudScaleMultiplier()
        {
            if (_medicalMenuOpen && Config.MedicalMenuEnabled)
                return Config.Clamp(Config.MedicalMenuScale, 0.55f, 1.25f);

            return Config.Clamp(Config.MiniHudScale * 1.35f, 0.75f, 1.75f);
        }

        private static float GetHudScaleMultiplier()
        {
            float presetScale;
            switch (Config.HudScaleMode)
            {
                case 0:
                    presetScale = 0.68f;
                    break;
                case 1:
                    presetScale = 0.76f;
                    break;
                case 2:
                    presetScale = 0.88f;
                    break;
                case 3:
                default:
                    presetScale = Config.HudScaleCustom;
                    break;
            }

            switch (Config.HudLayout)
            {
                case 0:
                    presetScale *= 0.72f;
                    break;
                case 1:
                    presetScale *= 0.82f;
                    break;
                case 2:
                    presetScale *= 1.00f;
                    break;
            }

            return Config.Clamp(presetScale, 0.32f, 1.25f);
        }

        private void UpdateHudVisibility(HealthManager manager, float deltaTime)
        {
            CanvasGroup? group = _monitorGroup;
            if (group == null)
                return;

            float target = ShouldDisplayHud(manager)
                ? (_medicalMenuOpen && Config.MedicalMenuEnabled ? Config.MedicalMenuOpacity : Config.MiniHudOpacity)
                : 0f;
            float speed = target > group.alpha ? 4.8f : 3.4f;
            group.alpha = Mathf.MoveTowards(group.alpha, target, Mathf.Max(deltaTime, 0.001f) * speed);
        }

        private bool WantsStats()
        {
            if (_medicalMenuOpen && Config.MedicalMenuEnabled)
                return Config.HudShowStats || Config.ShowDetailedStats;

            return Config.MiniHudEnabled && !Config.DisableHudCompletely;
        }

        private bool WantsBody()
        {
            return _medicalMenuOpen && Config.MedicalMenuEnabled && Config.HudShowBody;
        }

        private bool WantsInjuryNames()
        {
            return _medicalMenuOpen && Config.MedicalMenuEnabled && Config.HudShowInjuryNames;
        }

        private bool ShouldDisplayHud(HealthManager manager)
        {
            if (!Config.HudEnabled)
                return false;
            if (_medicalMenuOpen && Config.MedicalMenuEnabled)
                return true;
            if (Config.DisableHudCompletely || !Config.MiniHudEnabled)
                return false;
            if (Config.HudForceAlwaysVisible)
                return true;
            if (!Config.HudHideWhileConscious)
                return true;

            return HasImportantHudEvent(manager);
        }

        private static bool HasImportantHudEvent(HealthManager manager)
        {
            if (manager.Consciousness.State != ConsciousnessState.Awake || manager.Coma.IsActive)
                return true;
            if (manager.Bleeding.HasActiveBleeding || manager.Bleeding.BloodNormalized < 0.96f)
                return true;
            if (manager.Pain >= 12f || manager.Lungs.OxygenNormalized < 0.94f)
                return true;
            if (manager.Bones.TotalBrokenBoneCount > 0 || manager.Bones.FracturedRibCount > 0)
                return true;
            if (manager.Lungs.HasCollapsedLung || manager.Brain.HasActiveConcussion)
                return true;

            for (int i = 0; i < Config.LimbCount; i++)
            {
                LimbHealth limb = manager.GetLimb((BodyPart)i);
                if (limb.Fracture != FractureState.None || limb.DamagePercent > 0.08f)
                    return true;
            }

            return false;
        }

        private static void SetGameObjectActive(GameObject? go, bool active)
        {
            if (go != null && go.activeSelf != active)
                go.SetActive(active);
        }

        private void UpdateBodyHighlights(HealthManager manager)
        {
            if (_bodyImage != null)
            {
                float bodyOpacity = Config.HudBodyOpacity;
                float bodyTint = Config.Clamp(0.02f + manager.PainNormalized * 0.04f + (1f - manager.Bleeding.BloodNormalized) * 0.03f, 0f, 0.12f);
                if (manager.Consciousness.State == ConsciousnessState.Dead)
                    _bodyImage.color = new Color(0.18f, 0.19f, 0.20f, bodyOpacity * 0.44f);
                else
                    _bodyImage.color = new Color(0.24f - bodyTint * 0.06f, 0.30f - bodyTint * 0.04f, 0.34f - bodyTint * 0.03f, bodyOpacity * 0.34f);
            }

            UpdateHighlight(_limbVisuals[(int)BodyPart.Head], manager.GetLimb(BodyPart.Head), manager);
            UpdateNeckHighlight(_neckVisual, manager);
            UpdateHighlight(_limbVisuals[(int)BodyPart.Torso], manager.GetLimb(BodyPart.Torso), manager);
            UpdateHighlight(_chestVisual, manager.GetLimb(BodyPart.Torso), manager, 1.10f);
            UpdateHighlight(_stomachVisual, manager.GetLimb(BodyPart.Torso), manager, 0.92f);
            UpdateHighlight(_limbVisuals[(int)BodyPart.LeftArm], manager.GetLimb(BodyPart.LeftArm), manager);
            UpdateHighlight(_limbVisuals[(int)BodyPart.RightArm], manager.GetLimb(BodyPart.RightArm), manager);
            UpdateHighlight(_limbVisuals[(int)BodyPart.LeftLeg], manager.GetLimb(BodyPart.LeftLeg), manager);
            UpdateHighlight(_limbVisuals[(int)BodyPart.RightLeg], manager.GetLimb(BodyPart.RightLeg), manager);
        }

        private void UpdateHighlight(Image? image, LimbHealth limb, HealthManager manager)
        {
            UpdateHighlight(image, limb, manager, 1f);
        }

        private void UpdateHighlight(Image? image, LimbHealth limb, HealthManager manager, float intensityMultiplier)
        {
            if (image == null)
                return;

            BleedSeverity bleed = manager.Bleeding.GetWorstBleedingSeverity(limb.Part);
            float damage = Config.Clamp(limb.DamagePercent, 0f, 1f);
            float bodyOpacity = Config.HudBodyOpacity;
            float bleedBoost = bleed == BleedSeverity.None ? 0f : bleed == BleedSeverity.Light ? 0.18f : bleed == BleedSeverity.Medium ? 0.30f : bleed == BleedSeverity.Severe ? 0.46f : 0.58f;
            float fractureBoost = limb.Fracture == FractureState.None ? 0f : limb.Fracture == FractureState.Sprain ? 0.24f : limb.Fracture == FractureState.Fractured ? 0.40f : 0.56f;
            float pulse = (limb.IsBroken || bleed != BleedSeverity.None) ? 0.03f * (0.5f + 0.5f * Mathf.Sin(Time.time * 7.5f + (int)limb.Part * 0.65f)) : 0f;
            float painBoost = limb.Fracture == FractureState.None && bleed == BleedSeverity.None && limb.DamagePercent > 0.06f ? Config.Clamp(manager.PainNormalized * 0.32f, 0f, 0.26f) : 0f;
            float alpha = Config.Clamp((0.42f + damage * 0.38f + bleedBoost + fractureBoost + painBoost + pulse) * intensityMultiplier * bodyOpacity, 0.30f, 0.92f);

            Color baseColor = GetBodyConditionColor(limb, bleed, damage, painBoost, alpha);
            if (bleed == BleedSeverity.Arterial)
                baseColor = new Color(0.75f, 0.02f, 0.02f, alpha);
            else if (bleed == BleedSeverity.Severe)
                baseColor = Color.Lerp(baseColor, new Color(1f, 0.18f, 0.06f, alpha), 0.35f);
            if (IsTourniquetControlled(limb.Part, manager))
                baseColor = new Color(0.18f, 0.42f, 1f, Config.Clamp(alpha + 0.16f, 0.18f, 0.78f));

            image.color = baseColor;
        }

        private static Color GetBodyConditionColor(LimbHealth limb, BleedSeverity bleed, float damage, float painBoost, float alpha)
        {
            if (limb.Fracture == FractureState.Shattered || damage >= 0.92f)
                return new Color(0.42f, 0.02f, 0.02f, alpha);
            if (limb.Fracture == FractureState.Fractured || limb.Fracture == FractureState.Sprain)
                return new Color(1f, 0.52f, 0.08f, alpha);
            if (bleed != BleedSeverity.None)
                return new Color(0.95f, 0.08f, 0.06f, alpha);
            if (damage > 0.10f || painBoost > 0.01f)
                return new Color(1f, 0.86f, 0.12f, alpha);
            return new Color(0.16f, 0.86f, 0.46f, alpha);
        }

        private static void UpdateNeckHighlight(Image? image, HealthManager manager)
        {
            if (image == null)
                return;

            float alpha = Config.Clamp((manager.Neck.HasInjury ? 0.58f : 0.24f) * Config.HudBodyOpacity + manager.Neck.CameraInstability * 0.18f, 0.16f, 0.92f);
            Color color = manager.Neck.State switch
            {
                NeckInjuryState.Critical => new Color(0.42f, 0.02f, 0.02f, alpha),
                NeckInjuryState.Fractured => new Color(1f, 0.52f, 0.08f, alpha),
                NeckInjuryState.Sprained => new Color(1f, 0.86f, 0.12f, alpha),
                _ => new Color(0.16f, 0.86f, 0.46f, alpha)
            };

            if (manager.Neck.State >= NeckInjuryState.Fractured)
                color.a = Config.Clamp(color.a + Mathf.Sin(Time.time * 7.0f) * 0.06f, 0.20f, 0.95f);
            image.color = color;
        }

        private static Color GetThemeAccent(float alpha)
        {
            switch (Config.HudColorTheme)
            {
                case 1:
                    return new Color(0.58f, 1f, 0.72f, alpha);
                case 2:
                    return new Color(1f, 0.58f, 0.46f, alpha);
                default:
                    return new Color(0.62f, 0.94f, 1f, alpha);
            }
        }

        private bool IsTourniquetControlled(BodyPart part, HealthManager manager)
        {
            int count = manager.Bleeding.CopySources(_bleedSourceScratch);
            for (int i = 0; i < count; i++)
            {
                BleedSource source = _bleedSourceScratch[i];
                if (source.Active && source.BodyPart == part && source.TourniquetControlled)
                    return true;
            }

            return false;
        }

        private void UpdateMedicalMenuToggle()
        {
            if (!Config.MedicalMenuEnabled)
                return;

            if (!IsMedicalMenuTogglePressed())
                return;

            if (Time.unscaledTime - _lastMenuToggleTime < 0.24f)
                return;

            _lastMenuToggleTime = Time.unscaledTime;
            _medicalMenuOpen = !_medicalMenuOpen;
        }

        private bool IsMedicalMenuTogglePressed()
        {
            bool pressed = false;
            try
            {
                InputDevice device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
                if (device.isValid && device.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool clickPressed) && clickPressed)
                    pressed = true;
            }
            catch (Exception)
            {
            }

            if (Input.GetKey(KeyCode.JoystickButton9))
                pressed = true;

            bool down = pressed && !_rightStickClickHeld;
            _rightStickClickHeld = pressed;
            return down;
        }

        private void UpdateVitalIconColors(HealthManager manager)
        {
            if (_oxygenIcon != null)
                _oxygenIcon.color = Color.Lerp(new Color(1f, 0.24f, 0.18f, 0.96f), new Color(0.34f, 0.88f, 1f, 0.96f), manager.Lungs.OxygenNormalized);
            if (_bloodIcon != null)
                _bloodIcon.color = GetBloodColor(manager.Bleeding.BloodNormalized);
            if (_painIcon != null)
                _painIcon.color = Color.Lerp(new Color(0.85f, 0.92f, 0.78f, 0.90f), new Color(1f, 0.48f, 0.05f, 1f), manager.PainNormalized);
            if (_heartIcon != null)
            {
                float tachy = Config.Clamp((manager.PulseBpm - 70f) / 110f, 0f, 1f);
                _heartIcon.color = Color.Lerp(new Color(0.70f, 1f, 0.74f, 0.90f), new Color(1f, 0.16f, 0.10f, 1f), tachy);
            }
            if (_shockIcon != null)
                _shockIcon.color = Color.Lerp(new Color(0.74f, 0.90f, 1f, 0.86f), new Color(1f, 0.24f, 0.08f, 1f), Config.Clamp(manager.Shock.Intensity, 0f, 1f));
        }

        private void AttachToHead(Transform head)
        {
            if (_root == null)
                return;

            _root.transform.SetParent(null, false);
            _root.transform.localPosition = Vector3.zero;
            _root.transform.localRotation = Quaternion.identity;
            _root.transform.localScale = Vector3.one;
        }

        private void ApplyFrameFx(TraumaFrameFx fx)
        {
            if (_monitorRoot == null)
                return;

            RectTransform monitor = _monitorRoot.GetComponent<RectTransform>();
            Vector2 offset = new Vector2(fx.Offset.x * 0.18f, fx.Offset.y * 0.18f);
            monitor.anchoredPosition += offset;
            monitor.localScale = Vector3.one * GetCurrentHudScaleMultiplier() * fx.Scale;
        }

        private void UpdateStatusVisibility(HealthManager manager)
        {
            if (_statusRoot == null || _statusBack == null || _vitalsText == null || _limbText == null || _bloodFill == null || _painFill == null)
                return;

            if (!_statusRoot.activeSelf)
                _statusRoot.SetActive(true);

            float danger = Mathf.Max(manager.PainNormalized, 1f - manager.Bleeding.BloodNormalized, 1f - manager.Lungs.OxygenNormalized);
            float baseAlpha = _medicalMenuOpen ? 0.94f : 0.22f;
            float maxAlpha = _medicalMenuOpen ? 0.98f : 0.42f;
            _statusBack.color = new Color(0.015f, 0.018f, 0.020f, Config.Clamp(baseAlpha + danger * 0.18f, baseAlpha, maxAlpha));
        }

        private void UpdateMedicalFeedback(float deltaTime)
        {
            if (_feedbackText == null)
                return;

            if (_feedbackSeconds > 0f)
                _feedbackSeconds = Mathf.Max(0f, _feedbackSeconds - deltaTime);

            float alpha = _feedbackSeconds <= 0f ? 0f : Config.Clamp(_feedbackSeconds * 2.2f, 0f, 1f);
            _feedbackText.text = alpha <= 0.001f ? string.Empty : _feedbackMessage;
            _feedbackText.color = new Color(0.74f, 1f, 0.78f, alpha);
        }

        private Sprite LoadBodySprite()
        {
            Sprite? torso = LoadHudSprite("health_torso.png");
            if (torso != null)
                return torso;

            try
            {
                Texture2D texture = CreateOriginalBodyTexture();
                texture.name = "AHS_HealthBody";
                _bodyTexture = texture;
                return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            }
            catch (Exception)
            {
            }

            Texture2D fallback = CreateSimpleSilhouetteTexture();
            fallback.name = "AHS_HealthBodyFallback";
            _bodyTexture = fallback;
            return Sprite.Create(fallback, new Rect(0f, 0f, fallback.width, fallback.height), new Vector2(0.5f, 0.5f), 100f);
        }

        private static string GetBodyPartIconName(BodyPart part)
        {
            switch (part)
            {
                case BodyPart.Head:
                    return "health_head.png";
                case BodyPart.Torso:
                    return "health_torso.png";
                case BodyPart.LeftArm:
                    return "health_left_arm.png";
                case BodyPart.RightArm:
                    return "health_right_arm.png";
                case BodyPart.LeftLeg:
                    return "health_left_leg.png";
                case BodyPart.RightLeg:
                    return "health_right_leg.png";
                default:
                    return "health_torso.png";
            }
        }

        private static Sprite? LoadHudSprite(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            if (UiSpriteCache.TryGetValue(fileName, out Sprite? cached) && cached != null)
                return cached;

            string? path = ResolveUiPath(fileName);
            if (path == null)
                return null;

            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                texture.name = "ZBC_UI_" + Path.GetFileNameWithoutExtension(fileName);
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
                if (!texture.LoadImage(bytes, false))
                {
                    UnityEngine.Object.Destroy(texture);
                    return null;
                }

                Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
                sprite.name = texture.name;
                UiSpriteCache[fileName] = sprite;
                return sprite;
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Failed to load HUD icon: " + fileName + " (" + ex.Message + ")");
                return null;
            }
        }

        private static string? ResolveUiPath(string fileName)
        {
            string[] folders = GetUiFolders();
            for (int i = 0; i < folders.Length; i++)
            {
                string path = Path.Combine(folders[i], fileName);
                if (File.Exists(path))
                    return path;
            }

            MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Failed to load HUD icon: " + fileName);
            return null;
        }

        private static string[] GetUiFolders()
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

            string userData = MelonEnvironment.UserDataDirectory;
            string gameRoot = MelonEnvironment.GameRootDirectory;
            string[] candidates =
            {
                Path.Combine(userData, "ZBoneCity", "UI"),
                Path.Combine(userData, "BonelabAdvancedHealth", "UI"),
                Path.Combine(gameRoot, "UserData", "ZBoneCity", "UI"),
                Path.Combine(gameRoot, "UserData", "BonelabAdvancedHealth", "UI"),
                assemblyDirectory == null ? string.Empty : Path.Combine(assemblyDirectory, "ZBoneCity", "UI"),
                assemblyDirectory == null ? string.Empty : Path.Combine(assemblyDirectory, "UI")
            };

            List<string> folders = new List<string>(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                string folder = candidates[i];
                if (string.IsNullOrWhiteSpace(folder))
                    continue;

                try
                {
                    string normalized = Path.GetFullPath(folder);
                    if (!folders.Exists(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
                        folders.Add(normalized);
                }
                catch
                {
                }
            }

            return folders.ToArray();
        }

        private static Texture2D CreateOriginalBodyTexture()
        {
            const int width = 256;
            const int height = 512;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32 body = new Color32(205, 211, 214, 180);
            Color32 core = new Color32(238, 240, 244, 150);
            Color32[] pixels = new Color32[width * height];

            for (int y = 0; y < height; y++)
            {
                float ny = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f) / width;
                    float alpha = 0f;
                    alpha = Mathf.Max(alpha, CircleMask(nx, ny, 0.5f, 0.86f, 0.085f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.5f, 0.80f, 0.5f, 0.58f, 0.16f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.5f, 0.59f, 0.5f, 0.28f, 0.18f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.38f, 0.71f, 0.26f, 0.50f, 0.045f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.62f, 0.71f, 0.74f, 0.50f, 0.045f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.46f, 0.31f, 0.44f, 0.07f, 0.066f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.54f, 0.31f, 0.56f, 0.07f, 0.066f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.43f, 0.07f, 0.41f, 0.00f, 0.058f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.57f, 0.07f, 0.59f, 0.00f, 0.058f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.44f, 0.81f, 0.33f, 0.97f, 0.030f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.56f, 0.81f, 0.67f, 0.97f, 0.030f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.37f, 0.29f, 0.27f, 0.15f, 0.033f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.63f, 0.29f, 0.73f, 0.15f, 0.033f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.50f, 0.96f, 0.50f, 0.92f, 0.072f));

                    if (alpha <= 0.001f)
                    {
                        pixels[y * width + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float coreBoost = Mathf.Max(0f, CircleMask(nx, ny, 0.5f, 0.57f, 0.12f) * 0.35f);
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt((alpha * 0.78f + coreBoost) * body.a), 0, 255);
                    pixels[y * width + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(body.r, core.r, coreBoost)), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(body.g, core.g, coreBoost)), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(body.b, core.b, coreBoost)), 0, 255),
                        a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static Texture2D CreateSimpleSilhouetteTexture()
        {
            const int width = 64;
            const int height = 128;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32 fill = new Color32(210, 214, 216, 180);
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float ny = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float nx = (x + 0.5f) / width;
                    float alpha = 0f;
                    alpha = Mathf.Max(alpha, CircleMask(nx, ny, 0.5f, 0.84f, 0.11f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.5f, 0.76f, 0.5f, 0.50f, 0.18f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.5f, 0.50f, 0.5f, 0.20f, 0.19f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.39f, 0.66f, 0.24f, 0.43f, 0.05f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.61f, 0.66f, 0.76f, 0.43f, 0.05f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.47f, 0.26f, 0.45f, 0.07f, 0.07f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.53f, 0.26f, 0.55f, 0.07f, 0.07f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.44f, 0.06f, 0.42f, 0.00f, 0.06f));
                    alpha = Mathf.Max(alpha, CapsuleMask(nx, ny, 0.56f, 0.06f, 0.58f, 0.00f, 0.06f));

                    pixels[y * width + x] = alpha <= 0f
                        ? new Color32(0, 0, 0, 0)
                        : new Color32(fill.r, fill.g, fill.b, (byte)Mathf.Clamp(Mathf.RoundToInt(fill.a * alpha), 0, 255));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static float CircleMask(float x, float y, float cx, float cy, float radius)
        {
            float dx = x - cx;
            float dy = y - cy;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return SoftStep(radius, radius + 0.02f, d);
        }

        private static float CapsuleMask(float x, float y, float ax, float ay, float bx, float by, float radius)
        {
            Vector2 p = new Vector2(x, y);
            Vector2 a = new Vector2(ax, ay);
            Vector2 b = new Vector2(bx, by);
            Vector2 ab = b - a;
            float denom = Vector2.Dot(ab, ab);
            float t = denom <= 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / denom);
            Vector2 c = a + ab * t;
            float dx = x - c.x;
            float dy = y - c.y;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            return SoftStep(radius, radius + 0.018f, d);
        }

        private static float SoftStep(float inner, float outer, float value)
        {
            if (value <= inner)
                return 1f;
            if (value >= outer)
                return 0f;

            float t = 1f - Mathf.Clamp01((value - inner) / Mathf.Max(0.0001f, outer - inner));
            return t * t * (3f - 2f * t);
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
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            Image image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return go;
        }

        private Image CreateHudIcon(string name, Transform parent, string spriteName, Vector2 size, Vector2 anchoredPosition)
        {
            GameObject go = CreatePanel(name, parent, size, anchoredPosition, Color.white);
            Image image = go.GetComponent<Image>();
            Sprite? sprite = LoadHudSprite(spriteName);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.preserveAspect = true;
            }
            else
            {
                image.color = new Color(0.82f, 0.90f, 1f, 0.24f);
            }

            return image;
        }

        private Text CreateText(string name, Transform parent, Vector2 size, Vector2 anchoredPosition, int fontSize, TextAnchor anchor)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
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
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
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

        private static void AppendInjuryAlerts(StringBuilder builder, HealthManager manager, bool showFractures, bool showBleeding, bool showConsciousness, bool showOrganStatus)
        {
            int start = builder.Length;
            if (showConsciousness)
            {
                if (manager.Consciousness.State == ConsciousnessState.Dead)
                    builder.Append("Death state\n");
                else if (manager.Coma.IsActive)
                    builder.Append("Coma\n");
                else if (manager.Consciousness.State == ConsciousnessState.Unconscious)
                    builder.Append("Unconscious\n");
            }
            if (showFractures)
            {
                if (manager.Bones.GetBone(BoneType.Skull).IsBroken || manager.GetLimb(BodyPart.Head).Fracture != FractureState.None)
                    builder.Append("Skull Fracture\n");
                if (manager.Neck.HasInjury)
                {
                    builder.Append("Neck ");
                    builder.Append(NeckTraumaSystem.GetDisplayState(manager.Neck.State));
                    builder.Append('\n');
                }
                AppendBrokenPartAlert(builder, manager, BodyPart.Torso, "Rib Fracture");
                AppendBrokenPartAlert(builder, manager, BodyPart.LeftArm, "Left Arm Fracture");
                AppendBrokenPartAlert(builder, manager, BodyPart.RightArm, "Right Arm Fracture");
                AppendBrokenPartAlert(builder, manager, BodyPart.LeftLeg, "Left Leg Fracture");
                AppendBrokenPartAlert(builder, manager, BodyPart.RightLeg, "Right Leg Fracture");
            }
            if (showBleeding)
                AppendBleedAlert(builder, manager);
            if (showOrganStatus)
            {
                if (manager.Lungs.HasCollapsedLung)
                    builder.Append("Lung Damage\n");
                else if (manager.Lungs.OxygenNormalized < 0.72f)
                    builder.Append("Low Oxygen\n");
            }
            if (Config.HudShowPain)
            {
                if (manager.Pain >= 80f)
                    builder.Append("Severe Pain\n");
                else if (manager.Pain >= 45f)
                    builder.Append("Moderate Pain\n");
            }
            if (manager.Medication.OverdoseRisk > 0.55f)
                builder.Append("Medication Overdose Risk\n");
            if (manager.Medication.CrashActive)
                builder.Append("Stimulant Crash\n");
            if (manager.Rehabilitation.IsRecovering)
                builder.Append("Recovery Weakness\n");
            if (showOrganStatus && manager.Brain.HasActiveConcussion)
                builder.Append("Head Trauma\n");
            if (showOrganStatus && manager.Shock.IsActive)
            {
                builder.Append(MedicalInspectionSystem.GetShockName(manager.Shock.Severity));
                builder.Append('\n');
            }
            if (builder.Length == start)
                builder.Append("No active injuries\n");
        }

        private static void AppendInspectionSummary(StringBuilder builder, HealthManager manager)
        {
            AppendInspectionPart(builder, manager, BodyPart.Head);
            AppendNeckInspection(builder, manager);
            AppendInspectionPart(builder, manager, BodyPart.Torso);
            AppendInspectionPart(builder, manager, BodyPart.LeftArm);
            AppendInspectionPart(builder, manager, BodyPart.RightArm);
            AppendInspectionPart(builder, manager, BodyPart.LeftLeg);
            AppendInspectionPart(builder, manager, BodyPart.RightLeg);
            builder.Append(MedicalInspectionSystem.BuildMedicationInspection(manager));
            builder.Append('\n');

            if (builder.Length == 0)
                builder.Append("Full body inspection: stable\n");
        }

        private static void AppendInspectionPart(StringBuilder builder, HealthManager manager, BodyPart part)
        {
            LimbHealth limb = manager.GetLimb(part);
            BleedSeverity bleed = manager.Bleeding.GetWorstBleedingSeverity(part);
            bool headTrauma = part == BodyPart.Head && manager.Brain.HasActiveConcussion;
            bool lungTrauma = part == BodyPart.Torso && (manager.Lungs.HasCollapsedLung || manager.Lungs.OxygenNormalized < 0.72f);
            bool ribFracture = part == BodyPart.Torso && manager.Bones.FracturedRibCount > 0;
            bool injured = bleed != BleedSeverity.None ||
                           limb.Fracture != FractureState.None ||
                           limb.DamagePercent > 0.18f ||
                           headTrauma ||
                           lungTrauma ||
                           ribFracture;
            if (!injured)
                return;

            builder.Append(MedicalInspectionSystem.GetPartLabel(part));
            builder.Append('\n');
            if (bleed != BleedSeverity.None)
            {
                builder.Append("  ");
                builder.Append(MedicalInspectionSystem.GetBleedingName(bleed));
                builder.Append('\n');
            }
            if (limb.Fracture != FractureState.None)
                builder.Append(limb.Fracture == FractureState.Shattered ? "  Critical Fracture\n" : "  Fracture\n");
            if (headTrauma)
                builder.Append(manager.Brain.Concussion >= 0.55f ? "  Severe Concussion\n" : "  Concussion\n");
            if (lungTrauma)
                builder.Append("  Lung Damage\n");
            if (ribFracture)
            {
                builder.Append("  Rib Fracture x");
                builder.Append(manager.Bones.FracturedRibCount);
                builder.Append('\n');
            }

            builder.Append("  Treatment: ");
            int treatmentStart = builder.Length;
            AppendInlineTreatment(builder, manager, part);
            if (builder.Length == treatmentStart)
                builder.Append("Rest");
            builder.Append("\n\n");
        }

        private static void AppendNeckInspection(StringBuilder builder, HealthManager manager)
        {
            if (!manager.Neck.HasInjury)
                return;

            builder.Append("Neck\n");
            builder.Append("  ");
            builder.Append(NeckTraumaSystem.GetDisplayState(manager.Neck.State));
            builder.Append('\n');
            if (manager.Neck.CameraInstability > 0.25f)
                builder.Append("  Unstable head control\n");
            if (manager.Neck.BreathingStress > 0.2f)
                builder.Append("  Heavy breathing\n");
            builder.Append("  Treatment: ");
            bool appended = false;
            if (manager.Pain >= 45f)
                AppendTreatmentToken(builder, "Morphine", ref appended);
            AppendTreatmentToken(builder, "Medkit", ref appended);
            AppendTreatmentToken(builder, "Rest", ref appended);
            builder.Append("\n\n");
        }

        private static void AppendInlineTreatment(StringBuilder builder, HealthManager manager, BodyPart part)
        {
            bool appended = false;
            BleedSeverity bleed = manager.Bleeding.GetWorstBleedingSeverity(part);
            if (bleed == BleedSeverity.Arterial || bleed == BleedSeverity.Severe && MedicalInspectionSystem.IsLimb(part))
                AppendTreatmentToken(builder, "Tourniquet", ref appended);
            if (bleed != BleedSeverity.None)
                AppendTreatmentToken(builder, "Bandage", ref appended);
            if (MedicalInspectionSystem.IsLimb(part) && manager.GetLimb(part).Fracture != FractureState.None)
                AppendTreatmentToken(builder, "Splint", ref appended);
            if (manager.Pain >= 55f)
                AppendTreatmentToken(builder, "Morphine", ref appended);
            if (manager.Shock.Severity >= ShockSeverity.Moderate || manager.Bleeding.BloodVolumeMl < Config.UnconsciousBloodMl)
                AppendTreatmentToken(builder, "Blood Pack", ref appended);
        }

        private static void AppendTreatmentToken(StringBuilder builder, string value, ref bool appended)
        {
            if (appended)
                builder.Append(", ");
            builder.Append(value);
            appended = true;
        }

        private static void AppendBrokenPartAlert(StringBuilder builder, HealthManager manager, BodyPart part, string label)
        {
            LimbHealth limb = manager.GetLimb(part);
            if (limb.Fracture != FractureState.None)
            {
                builder.Append(label);
                if (limb.Fracture == FractureState.Shattered)
                    builder.Append(" Critical\n");
                else
                    builder.Append('\n');
            }
            else if (part == BodyPart.Torso && manager.Bones.FracturedRibCount > 0)
            {
                builder.Append("Rib Fracture x");
                builder.Append(manager.Bones.FracturedRibCount);
                builder.Append('\n');
            }
        }

        private static void AppendTreatmentAdvice(StringBuilder builder, HealthManager manager)
        {
            if (manager.Consciousness.State == ConsciousnessState.Dead)
            {
                builder.Append("No recovery possible");
                return;
            }

            if (Config.HudShowBleeding && manager.Bleeding.HasActiveBleeding)
            {
                BodyPart part = manager.Bleeding.GetWorstBleedingPart();
                BleedSeverity worst = manager.Bleeding.GetWorstBleedingSeverity(part);
                builder.Append(MedicalInspectionSystem.GetPartLabel(part));
                builder.Append(" - ");
                builder.Append(MedicalInspectionSystem.GetBleedingName(worst));
                builder.Append('\n');
                MedicalInspectionSystem.AppendTreatmentForPart(builder, manager, part);
            }

            if (Config.HudShowFractures)
            {
                AppendBrokenTreatment(builder, manager, BodyPart.LeftArm, "Left Arm");
                AppendBrokenTreatment(builder, manager, BodyPart.RightArm, "Right Arm");
                AppendBrokenTreatment(builder, manager, BodyPart.LeftLeg, "Left Leg");
                AppendBrokenTreatment(builder, manager, BodyPart.RightLeg, "Right Leg");
                if (manager.Bones.GetBone(BoneType.Skull).IsBroken || manager.GetLimb(BodyPart.Head).Fracture != FractureState.None)
                    builder.Append("Skull Fracture - Medkit + rest\n");
                if (manager.GetLimb(BodyPart.Torso).Fracture != FractureState.None || manager.Bones.FracturedRibCount > 0)
                    builder.Append("Rib Fracture - Rest required\n");
            }
            if (Config.HudShowPain && manager.Pain >= 55f)
                builder.Append("Severe pain - Use morphine\n");
            if (Config.HudShowOxygen && (manager.Lungs.OxygenNormalized < 0.65f || manager.Lungs.HasCollapsedLung))
                builder.Append("O2 low - Stop bleeding, rest\n");
            if (manager.Shock.Severity >= ShockSeverity.Moderate)
                builder.Append("Shock - Control bleeding, give blood\n");
            if (manager.Medication.OverdoseRisk > 0.55f)
                builder.Append("Overdose - stop dosing, monitor breathing\n");
            if (manager.Rehabilitation.IsRecovering)
                builder.Append("Recovery - move slowly, rest\n");
            if (Config.HudShowConsciousness && manager.Coma.IsActive)
                builder.Append("Stabilize before wake-up\n");
            if (builder.Length == 0)
                builder.Append("No treatment required");
        }

        private static void AppendBleedAlert(StringBuilder builder, HealthManager manager)
        {
            if (!manager.Bleeding.HasActiveBleeding)
                return;

            BleedSeverity severity = manager.Bleeding.GetWorstBleedingSeverity(manager.Bleeding.GetWorstBleedingPart());
            switch (severity)
            {
                case BleedSeverity.Arterial:
                    builder.Append("Arterial Bleeding\n");
                    break;
                case BleedSeverity.Severe:
                    builder.Append("Severe Bleeding\n");
                    break;
                case BleedSeverity.Medium:
                    builder.Append("Moderate Bleeding\n");
                    break;
                case BleedSeverity.Light:
                    builder.Append("Minor Bleeding\n");
                    break;
            }
        }

        private static void AppendBrokenTreatment(StringBuilder builder, HealthManager manager, BodyPart part, string label)
        {
            LimbHealth limb = manager.GetLimb(part);
            if (limb.Fracture == FractureState.None)
                return;

            builder.Append(label);
            builder.Append(limb.Fracture == FractureState.Shattered ? " Shattered - Use splint or rest\n" : " Broken - Use splint or rest\n");
        }

        private static void SetRaycast(Image? image, bool value)
        {
            if (image != null)
                image.raycastTarget = value;
        }

        private static void DisableAllRaycasts(GameObject root)
        {
            if (root == null)
                return;

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic != null)
                    graphic.raycastTarget = false;
            }
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
            if (manager.TelemetrySnapshot.IsDead)
                return 0;

            return Mathf.Clamp(Mathf.RoundToInt(manager.TelemetrySnapshot.HeartbeatBpm), 28, 220);
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
            int index = (int)state;
            return index >= 0 && index < StateLabels.Length ? StateLabels[index] : "UNKNOWN";
        }

        private static string GetBleedLabel(BleedSeverity severity)
        {
            int index = (int)severity;
            return index >= 0 && index < BleedBadges.Length ? BleedBadges[index] : "-";
        }

        private static string GetFractureLabel(FractureState fracture)
        {
            int index = (int)fracture;
            return index >= 0 && index < FractureBadges.Length ? FractureBadges[index] : "-";
        }
    }
}
