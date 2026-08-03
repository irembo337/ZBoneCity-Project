using MelonLoader;

namespace BonelabAdvancedHealth
{
    public static class SettingsMenu
    {
        private static MelonPreferences_Category? _category;
        private static MelonPreferences_Entry<bool>? _enableBlood;
        private static MelonPreferences_Entry<float>? _bloodIntensity;
        private static MelonPreferences_Entry<float>? _bloodFadeSeconds;
        private static MelonPreferences_Entry<bool>? _unconsciousEffects;
        private static MelonPreferences_Entry<bool>? _painEffects;
        private static MelonPreferences_Entry<bool>? _painOverlayEnabled;
        private static MelonPreferences_Entry<float>? _painOverlayOpacity;
        private static MelonPreferences_Entry<float>? _painOverlayPulseIntensity;
        private static MelonPreferences_Entry<float>? _painOverlayBloodOpacity;
        private static MelonPreferences_Entry<float>? _painOverlayAnimationSpeed;
        private static MelonPreferences_Entry<bool>? _organSystem;
        private static MelonPreferences_Entry<bool>? _realisticAudio;
        private static MelonPreferences_Entry<bool>? _customSoundsEnabled;
        private static MelonPreferences_Entry<float>? _masterVolume;
        private static MelonPreferences_Entry<float>? _audioIntensity;
        private static MelonPreferences_Entry<float>? _headshotSoundVolume;
        private static MelonPreferences_Entry<float>? _deathSoundVolume;
        private static MelonPreferences_Entry<float>? _metalImpactSoundVolume;
        private static MelonPreferences_Entry<bool>? _gearSoundsEnabled;
        private static MelonPreferences_Entry<float>? _gearVolume;
        private static MelonPreferences_Entry<float>? _gearDistance;
        private static MelonPreferences_Entry<float>? _gearSoundSpeedMultiplier;
        private static MelonPreferences_Entry<float>? _screenEffectsIntensity;
        private static MelonPreferences_Entry<float>? _fractureSeverity;
        private static MelonPreferences_Entry<bool>? _debugMode;
        private static MelonPreferences_Entry<int>? _hudLayout;
        private static MelonPreferences_Entry<int>? _hudPosition;
        private static MelonPreferences_Entry<int>? _hudScaleMode;
        private static MelonPreferences_Entry<float>? _hudScaleCustom;
        private static MelonPreferences_Entry<float>? _hudOpacity;
        private static MelonPreferences_Entry<float>? _hudOffsetX;
        private static MelonPreferences_Entry<float>? _hudOffsetY;
        private static MelonPreferences_Entry<bool>? _hudShowBlood;
        private static MelonPreferences_Entry<bool>? _hudShowPain;
        private static MelonPreferences_Entry<bool>? _hudShowOxygen;
        private static MelonPreferences_Entry<bool>? _hudShowFractures;
        private static MelonPreferences_Entry<bool>? _hudShowOrganStatus;
        private static MelonPreferences_Entry<bool>? _hudShowBleeding;
        private static MelonPreferences_Entry<bool>? _hudShowConsciousness;
        private static MelonPreferences_Entry<bool>? _hudShowTreatment;
        private static MelonPreferences_Entry<bool>? _hudShowBody;
        private static MelonPreferences_Entry<bool>? _hudShowStats;
        private static MelonPreferences_Entry<bool>? _hudShowInjuryNames;
        private static MelonPreferences_Entry<bool>? _hudShowVitalSigns;
        private static MelonPreferences_Entry<float>? _hudBodyOpacity;
        private static MelonPreferences_Entry<int>? _hudColorTheme;
        private static MelonPreferences_Entry<bool>? _hudHideWhileConscious;
        private static MelonPreferences_Entry<bool>? _hudForceAlwaysVisible;
        private static MelonPreferences_Entry<bool>? _medicalMenuEnabled;
        private static MelonPreferences_Entry<bool>? _miniHudEnabled;
        private static MelonPreferences_Entry<float>? _miniHudScale;
        private static MelonPreferences_Entry<float>? _miniHudOpacity;
        private static MelonPreferences_Entry<float>? _medicalMenuScale;
        private static MelonPreferences_Entry<float>? _medicalMenuOpacity;
        private static MelonPreferences_Entry<bool>? _showDetailedStats;
        private static MelonPreferences_Entry<bool>? _disableHudCompletely;
        private static MelonPreferences_Entry<bool>? _knifePenetration;
        private static MelonPreferences_Entry<bool>? _nativeBlood;
        private static MelonPreferences_Entry<bool>? _forensicsEnabled;
        private static MelonPreferences_Entry<float>? _forensicTraceLifetimeSeconds;
        private static MelonPreferences_Entry<float>? _forensicCasingChance;
        private static MelonPreferences_Entry<bool>? _casualtyDraggingEnabled;
        private static MelonPreferences_Entry<bool>? _neckTraumaEnabled;
        private static MelonPreferences_Entry<bool>? _neckGrabEnabled;
        private static MelonPreferences_Entry<bool>? _neckSnapEnabled;
        private static MelonPreferences_Entry<bool>? _allowNeckSnapOnPlayers;
        private static MelonPreferences_Entry<bool>? _neckSnapSoundEnabled;
        private static MelonPreferences_Entry<bool>? _persistentCorpsesEnabled;
        private static MelonPreferences_Entry<int>? _persistentCorpseMaxCount;
        private static MelonPreferences_Entry<float>? _persistentCorpseLifetimeSeconds;
        private static MelonPreferences_Entry<bool>? _bodycamEnabled;
        private static MelonPreferences_Entry<int>? _bodycamPreset;
        private static MelonPreferences_Entry<bool>? _bodycamHelmetCamera;
        private static MelonPreferences_Entry<float>? _bodycamFov;
        private static MelonPreferences_Entry<float>? _bodycamForwardOffset;
        private static MelonPreferences_Entry<float>? _bodycamSmoothing;
        private static MelonPreferences_Entry<bool>? _bodycamShowBorders;
        private static MelonPreferences_Entry<float>? _bodycamBorderThickness;
        private static MelonPreferences_Entry<bool>? _bodycamAxonOverlayEnabled;
        private static MelonPreferences_Entry<bool>? _bodycamBeepEnabled;
        private static MelonPreferences_Entry<float>? _bodycamBeepVolume;
        private static MelonPreferences_Entry<string>? _bodycamCameraId;
        private static MelonPreferences_Entry<float>? _bodycamOverlayScale;
        private static MelonPreferences_Entry<float>? _bodycamOverlayOpacity;
        private static MelonPreferences_Entry<float>? _bodycamOverlayOffsetX;
        private static MelonPreferences_Entry<float>? _bodycamOverlayOffsetY;
        private static MelonPreferences_Entry<float>? _bodycamShakeIntensity;
        private static MelonPreferences_Entry<float>? _bodycamNoiseIntensity;
        private static MelonPreferences_Entry<float>? _bodycamCompressionIntensity;
        private static MelonPreferences_Entry<bool>? _bodycamBatteryIndicator;

        public static bool BloodEnabled => _enableBlood?.Value ?? true;
        public static float BloodIntensity => Config.Clamp(_bloodIntensity?.Value ?? 1f, 0f, 2.5f);
        public static float BloodFadeSeconds => Config.Clamp(_bloodFadeSeconds?.Value ?? 150f, 25f, 360f);
        public static bool UnconsciousEffectsEnabled => _unconsciousEffects?.Value ?? true;
        public static bool PainEffectsEnabled => _painEffects?.Value ?? true;
        public static bool PainOverlayEnabled => _painOverlayEnabled?.Value ?? true;
        public static float PainOverlayOpacity => Config.Clamp(_painOverlayOpacity?.Value ?? 0.88f, 0f, 1f);
        public static float PainOverlayPulseIntensity => Config.Clamp(_painOverlayPulseIntensity?.Value ?? 0.82f, 0f, 2f);
        public static float PainOverlayBloodOpacity => Config.Clamp(_painOverlayBloodOpacity?.Value ?? 0.72f, 0f, 1.5f);
        public static float PainOverlayAnimationSpeed => Config.Clamp(_painOverlayAnimationSpeed?.Value ?? 1.0f, 0.25f, 2.5f);
        public static bool OrganSystemEnabled => _organSystem?.Value ?? true;
        public static bool RealisticAudioEnabled => _realisticAudio?.Value ?? true;
        public static bool CustomSoundsEnabled => _customSoundsEnabled?.Value ?? true;
        public static float MasterVolume => Config.Clamp(_masterVolume?.Value ?? 1f, 0f, 2f);
        public static float AudioIntensity => Config.Clamp(_audioIntensity?.Value ?? 1f, 0f, 2f);
        public static float HeadshotSoundVolume => Config.Clamp(_headshotSoundVolume?.Value ?? 1f, 0f, 2f);
        public static float DeathSoundVolume => Config.Clamp(_deathSoundVolume?.Value ?? 1f, 0f, 2f);
        public static float MetalImpactSoundVolume => Config.Clamp(_metalImpactSoundVolume?.Value ?? 1f, 0f, 2f);
        public static bool GearSoundsEnabled => _gearSoundsEnabled?.Value ?? true;
        public static float GearVolume => Config.Clamp(_gearVolume?.Value ?? 0.72f, 0f, 2f);
        public static float GearDistance => Config.Clamp(_gearDistance?.Value ?? 12f, 3f, 35f);
        public static float GearSoundSpeedMultiplier => Config.Clamp(_gearSoundSpeedMultiplier?.Value ?? 1f, 0.35f, 2.5f);
        public static float ScreenEffectsIntensity => Config.Clamp(_screenEffectsIntensity?.Value ?? 1f, 0f, 2f);
        public static float FractureSeverity => Config.Clamp(_fractureSeverity?.Value ?? 1f, 0.25f, 2.5f);
        public static bool DebugMode => _debugMode?.Value ?? false;
        public static int HudLayout => ClampInt(_hudLayout?.Value ?? 2, 0, 2);
        public static int HudPosition => ClampInt(_hudPosition?.Value ?? 1, 0, 3);
        public static int HudScaleMode => ClampInt(_hudScaleMode?.Value ?? 1, 0, 3);
        public static float HudScaleCustom => Config.Clamp(_hudScaleCustom?.Value ?? 1f, 0.4f, 1.8f);
        public static float HudOpacity => Config.Clamp(_hudOpacity?.Value ?? 0.88f, 0.12f, 1f);
        public static float HudOffsetX => Config.Clamp(_hudOffsetX?.Value ?? 0f, -0.8f, 0.8f);
        public static float HudOffsetY => Config.Clamp(_hudOffsetY?.Value ?? 0f, -0.6f, 0.6f);
        public static bool HudShowBlood => _hudShowBlood?.Value ?? true;
        public static bool HudShowPain => _hudShowPain?.Value ?? true;
        public static bool HudShowOxygen => _hudShowOxygen?.Value ?? true;
        public static bool HudShowFractures => _hudShowFractures?.Value ?? true;
        public static bool HudShowOrganStatus => _hudShowOrganStatus?.Value ?? true;
        public static bool HudShowBleeding => _hudShowBleeding?.Value ?? true;
        public static bool HudShowConsciousness => _hudShowConsciousness?.Value ?? true;
        public static bool HudShowTreatment => _hudShowTreatment?.Value ?? true;
        public static bool HudShowBody => _hudShowBody?.Value ?? true;
        public static bool HudShowStats => _hudShowStats?.Value ?? true;
        public static bool HudShowInjuryNames => _hudShowInjuryNames?.Value ?? true;
        public static bool HudShowVitalSigns => _hudShowVitalSigns?.Value ?? true;
        public static float HudBodyOpacity => Config.Clamp(_hudBodyOpacity?.Value ?? 0.72f, 0.25f, 1f);
        public static int HudColorTheme => ClampInt(_hudColorTheme?.Value ?? 0, 0, 2);
        public static bool HudHideWhileConscious => _hudHideWhileConscious?.Value ?? false;
        public static bool HudForceAlwaysVisible => _hudForceAlwaysVisible?.Value ?? true;
        public static bool MedicalMenuEnabled => _medicalMenuEnabled?.Value ?? true;
        public static bool MiniHudEnabled => _miniHudEnabled?.Value ?? true;
        public static float MiniHudScale => Config.Clamp(_miniHudScale?.Value ?? 0.78f, 0.35f, 1.35f);
        public static float MiniHudOpacity => Config.Clamp(_miniHudOpacity?.Value ?? 0.82f, 0.45f, 1f);
        public static float MedicalMenuScale => Config.Clamp(_medicalMenuScale?.Value ?? 1.0f, 0.65f, 1.25f);
        public static float MedicalMenuOpacity => Config.Clamp(_medicalMenuOpacity?.Value ?? 0.98f, 0.45f, 1f);
        public static bool ShowDetailedStats => _showDetailedStats?.Value ?? true;
        public static bool DisableHudCompletely => _disableHudCompletely?.Value ?? false;
        public static bool KnifePenetrationEnabled => _knifePenetration?.Value ?? true;
        public static bool NativeBloodEnabled => _nativeBlood?.Value ?? true;
        public static bool ForensicsEnabled => _forensicsEnabled?.Value ?? true;
        public static float ForensicTraceLifetimeSeconds => Config.Clamp(_forensicTraceLifetimeSeconds?.Value ?? 240f, 30f, 900f);
        public static float ForensicCasingChance => Config.Clamp(_forensicCasingChance?.Value ?? 0.65f, 0f, 1f);
        public static bool CasualtyDraggingEnabled => _casualtyDraggingEnabled?.Value ?? true;
        public static bool NeckTraumaEnabled => _neckTraumaEnabled?.Value ?? true;
        public static bool NeckSnapEnabled => _neckSnapEnabled?.Value ?? _neckGrabEnabled?.Value ?? true;
        public static bool NeckGrabEnabled => NeckSnapEnabled;
        public static bool AllowNeckSnapOnPlayers => _allowNeckSnapOnPlayers?.Value ?? false;
        public static bool NeckSnapSoundEnabled => _neckSnapSoundEnabled?.Value ?? true;
        public static bool PersistentCorpsesEnabled => _persistentCorpsesEnabled?.Value ?? true;
        public static int PersistentCorpseMaxCount => ClampInt(_persistentCorpseMaxCount?.Value ?? 3, 0, 12);
        public static float PersistentCorpseLifetimeSeconds => Config.Clamp(_persistentCorpseLifetimeSeconds?.Value ?? 240f, 20f, 900f);
        public static bool BodycamEnabled => _bodycamEnabled?.Value ?? false;
        public static int BodycamPreset => ClampInt(_bodycamPreset?.Value ?? 0, 0, 3);
        public static bool BodycamHelmetCamera => _bodycamHelmetCamera?.Value ?? false;
        public static bool BodycamChestCamera => !BodycamHelmetCamera;
        public static float BodycamFov => Config.Clamp(_bodycamFov?.Value ?? 120f, 60f, 170f);
        public static float BodycamForwardOffset => Config.Clamp(_bodycamForwardOffset?.Value ?? 0.36f, 0f, 1.0f);
        public static float BodycamSmoothing => Config.Clamp(_bodycamSmoothing?.Value ?? 10.0f, 0.5f, 20f);
        public static bool BodycamShowBorders => _bodycamShowBorders?.Value ?? true;
        public static float BodycamBorderThickness => Config.Clamp(_bodycamBorderThickness?.Value ?? 0.075f, 0f, 0.22f);
        public static bool BodycamAxonOverlayEnabled => _bodycamAxonOverlayEnabled?.Value ?? true;
        public static bool BodycamBeepEnabled => _bodycamBeepEnabled?.Value ?? true;
        public static float BodycamBeepVolume => Config.Clamp(_bodycamBeepVolume?.Value ?? 0.35f, 0f, 1f);
        public static string BodycamCameraId => _bodycamCameraId?.Value ?? string.Empty;
        public static string BodycamCameraIdDisplay => string.IsNullOrWhiteSpace(BodycamCameraId) ? "AUTO" : BodycamCameraId;
        public static float BodycamOverlayScale => Config.Clamp(_bodycamOverlayScale?.Value ?? 1f, 0.55f, 1.8f);
        public static float BodycamOverlayOpacity => Config.Clamp(_bodycamOverlayOpacity?.Value ?? 1f, 0f, 1f);
        public static float BodycamOverlayOffsetX => Config.Clamp(_bodycamOverlayOffsetX?.Value ?? 0f, -500f, 500f);
        public static float BodycamOverlayOffsetY => Config.Clamp(_bodycamOverlayOffsetY?.Value ?? 0f, -300f, 300f);
        public static float BodycamShakeIntensity => Config.Clamp(_bodycamShakeIntensity?.Value ?? 0.35f, 0f, 1.5f);
        public static float BodycamNoiseIntensity => Config.Clamp(_bodycamNoiseIntensity?.Value ?? 0.10f, 0f, 1f);
        public static float BodycamCompressionIntensity => Config.Clamp(_bodycamCompressionIntensity?.Value ?? 0.08f, 0f, 1f);
        public static bool BodycamBatteryIndicator => _bodycamBatteryIndicator?.Value ?? true;

        public static void Initialize()
        {
            _category = MelonPreferences.CreateCategory("ZBoneCity_Settings", "ZBoneCity Settings");
            _enableBlood = _category.CreateEntry("EnableBlood", true, "Enable blood simulation and visible blood FX");
            _bloodIntensity = _category.CreateEntry("BloodIntensity", 1.0f, "Blood FX and bleed visual intensity");
            _bloodFadeSeconds = _category.CreateEntry("BloodFadeSeconds", 150f, "Seconds before blood pools fade away");
            _unconsciousEffects = _category.CreateEntry("UnconsciousEffects", true, "Enable blackout, tunnel vision, ringing and recovery effects");
            _painEffects = _category.CreateEntry("PainEffects", true, "Enable pain accumulation, shakes, movement penalties and pain audio");
            _painOverlayEnabled = _category.CreateEntry("PainOverlayEnabled", true, "Enable fullscreen pain overlay");
            _painOverlayOpacity = _category.CreateEntry("PainOverlayOpacity", 0.88f, "Maximum opacity for the fullscreen pain overlay");
            _painOverlayPulseIntensity = _category.CreateEntry("PainOverlayPulseIntensity", 0.82f, "Heartbeat pulse strength for the fullscreen pain overlay");
            _painOverlayBloodOpacity = _category.CreateEntry("PainOverlayBloodOpacity", 0.72f, "Blood edge opacity for the fullscreen pain overlay");
            _painOverlayAnimationSpeed = _category.CreateEntry("PainOverlayAnimationSpeed", 1.0f, "Animation speed for fullscreen pain pulses");
            _organSystem = _category.CreateEntry("OrganSystem", true, "Enable organ damage, cardiac arrest and internal trauma");
            _realisticAudio = _category.CreateEntry("RealisticAudio", true, "Enable trauma audio, muffle, ringing and breathing loops");
            _customSoundsEnabled = _category.CreateEntry("CustomSoundsEnabled", true, "Enable packaged ZBoneCity custom sounds");
            _masterVolume = _category.CreateEntry("MasterVolume", 1.0f, "Master volume for packaged ZBoneCity custom sounds");
            _audioIntensity = _category.CreateEntry("AudioIntensity", 1.0f, "Trauma audio volume and layering intensity");
            _headshotSoundVolume = _category.CreateEntry("HeadshotSoundVolume", 1.0f, "Headshot sound effect volume");
            _deathSoundVolume = _category.CreateEntry("DeathSoundVolume", 1.0f, "Death sound effect volume");
            _metalImpactSoundVolume = _category.CreateEntry("MetalImpactSoundVolume", 1.0f, "Metal bullet impact sound effect volume");
            _gearSoundsEnabled = _category.CreateEntry("GearSoundsEnabled", true, "Enable sequential tactical gear sounds while moving");
            _gearVolume = _category.CreateEntry("GearVolume", 0.72f, "Gear movement sound volume");
            _gearDistance = _category.CreateEntry("GearDistance", 12f, "Maximum audible distance for gear movement sounds");
            _gearSoundSpeedMultiplier = _category.CreateEntry("GearSoundSpeedMultiplier", 1.0f, "Gear sound cadence multiplier based on movement speed");
            _screenEffectsIntensity = _category.CreateEntry("ScreenEffectsIntensity", 1.0f, "Fullscreen pain and unconscious screen effect intensity");
            _fractureSeverity = _category.CreateEntry("FractureSeverity", 1.0f, "Global fracture severity multiplier");
            _debugMode = _category.CreateEntry("DebugMode", false, "Enable extra mod diagnostics in MelonLoader logs");
            _hudLayout = _category.CreateEntry("HudLayout", 2, "0 compact, 1 body, 2 advanced");
            _hudPosition = _category.CreateEntry("HudPosition", 1, "0 left, 1 right, 2 center, 3 custom");
            _hudScaleMode = _category.CreateEntry("HudScaleMode", 1, "0 small, 1 medium, 2 large, 3 custom");
            _hudScaleCustom = _category.CreateEntry("HudScaleCustom", 1.0f, "Custom HUD scale multiplier");
            _hudOpacity = _category.CreateEntry("HudOpacity", 0.88f, "HUD opacity");
            _hudOffsetX = _category.CreateEntry("HudOffsetX", 0f, "Custom HUD horizontal offset");
            _hudOffsetY = _category.CreateEntry("HudOffsetY", 0f, "Custom HUD vertical offset");
            _hudShowBlood = _category.CreateEntry("HudShowBlood", true, "Show blood level");
            _hudShowPain = _category.CreateEntry("HudShowPain", true, "Show pain level");
            _hudShowOxygen = _category.CreateEntry("HudShowOxygen", true, "Show oxygen level");
            _hudShowFractures = _category.CreateEntry("HudShowFractures", true, "Show fracture info");
            _hudShowOrganStatus = _category.CreateEntry("HudShowOrganStatus", true, "Show organ monitor");
            _hudShowBleeding = _category.CreateEntry("HudShowBleeding", true, "Show bleeding info");
            _hudShowConsciousness = _category.CreateEntry("HudShowConsciousness", true, "Show consciousness state");
            _hudShowTreatment = _category.CreateEntry("HudShowTreatment", true, "Show treatment suggestions");
            _hudShowBody = _category.CreateEntry("HudShowBody", true, "Show body silhouette");
            _hudShowStats = _category.CreateEntry("HudShowStats", true, "Show Blood, O2 and Pain stats");
            _hudShowInjuryNames = _category.CreateEntry("HudShowInjuryNames", true, "Show injury names beside the body monitor");
            _hudShowVitalSigns = _category.CreateEntry("HudShowVitalSigns", true, "Show vital signs in the medical menu");
            _hudBodyOpacity = _category.CreateEntry("HudBodyOpacity", 0.72f, "Body model opacity in the medical menu");
            _hudColorTheme = _category.CreateEntry("HudColorTheme", 0, "0 tactical cyan, 1 neutral green, 2 red trauma");
            _hudHideWhileConscious = _category.CreateEntry("HudHideWhileConscious", false, "Hide HUD while stable and conscious");
            _hudForceAlwaysVisible = _category.CreateEntry("HudForceAlwaysVisible", true, "Force HUD to stay visible");
            _medicalMenuEnabled = _category.CreateEntry("MedicalMenuEnabled", true, "Enable the R3 medical menu");
            _miniHudEnabled = _category.CreateEntry("MiniHudEnabled", true, "Enable compact Blood/O2/Pain gameplay HUD");
            _miniHudScale = _category.CreateEntry("MiniHudScale", 0.78f, "Mini HUD scale");
            _miniHudOpacity = _category.CreateEntry("MiniHudOpacity", 0.82f, "Mini HUD opacity");
            _medicalMenuScale = _category.CreateEntry("MedicalMenuScale", 1.0f, "Medical menu scale");
            _medicalMenuOpacity = _category.CreateEntry("MedicalMenuOpacity", 0.98f, "Medical menu opacity");
            _showDetailedStats = _category.CreateEntry("ShowDetailedStats", true, "Show detailed medical stats in the menu");
            _disableHudCompletely = _category.CreateEntry("DisableHudCompletely", false, "Hide the gameplay HUD while keeping the R3 medical menu available");
            _knifePenetration = _category.CreateEntry("KnifePenetration", true, "Enable embedded knife wound tracking");
            _nativeBlood = _category.CreateEntry("NativeBlood", true, "Prefer discovered BONELAB blood particle effects");
            _forensicsEnabled = _category.CreateEntry("ForensicsEnabled", true, "Leave bullet holes, casings and forensic blood traces after combat");
            _forensicTraceLifetimeSeconds = _category.CreateEntry("ForensicTraceLifetimeSeconds", 240f, "Seconds before forensic traces fade out");
            _forensicCasingChance = _category.CreateEntry("ForensicCasingChance", 0.65f, "Chance to leave a visible casing marker for bullet impacts");
            _casualtyDraggingEnabled = _category.CreateEntry("CasualtyDraggingEnabled", true, "Enable unconscious casualty drag and Fusion sync hooks");
            _neckTraumaEnabled = _category.CreateEntry("NeckTraumaEnabled", true, "Enable rare severe neck trauma from high-energy impacts");
            _neckGrabEnabled = _category.CreateEntry("NeckGrabEnabled", true, "Legacy alias for physics-based neck snap");
            _neckSnapEnabled = _category.CreateEntry("NeckSnapEnabled", true, "Enable difficult physics-based neck snap on valid targets");
            _allowNeckSnapOnPlayers = _category.CreateEntry("AllowNeckSnapOnPlayers", false, "Allow neck snap attempts on LabFusion players when the server/modpack permits it");
            _neckSnapSoundEnabled = _category.CreateEntry("NeckSnapSoundEnabled", true, "Play the configured 3D neck snap sound");
            _persistentCorpsesEnabled = _category.CreateEntry("PersistentCorpsesEnabled", true, "Keep physical player corpses after death");
            _persistentCorpseMaxCount = _category.CreateEntry("PersistentCorpseMaxCount", 3, "Maximum persistent player corpses kept on the current map");
            _persistentCorpseLifetimeSeconds = _category.CreateEntry("PersistentCorpseLifetimeSeconds", 240f, "Seconds before persistent corpses are removed");
            _bodycamEnabled = _category.CreateEntry("BodycamEnabled", false, "Enable integrated ZBoneCity body camera view");
            _bodycamPreset = _category.CreateEntry("BodycamPreset", 0, "0 Tactical, 1 Helmet Wide, 2 Chest Heavy, 3 Night Patrol");
            _bodycamHelmetCamera = _category.CreateEntry("BodycamHelmetCamera", false, "Use helmet-mounted bodycam instead of chest-mounted bodycam");
            _bodycamFov = _category.CreateEntry("BodycamFOV", 120f, "Bodycam camera field of view");
            _bodycamForwardOffset = _category.CreateEntry("BodycamForwardOffset", 0.36f, "Bodycam forward offset from the player view");
            _bodycamSmoothing = _category.CreateEntry("BodycamSmoothing", 10.0f, "Bodycam chest rotation smoothing");
            _bodycamShowBorders = _category.CreateEntry("BodycamShowBorders", true, "Show cinematic bodycam border bars");
            _bodycamBorderThickness = _category.CreateEntry("BodycamBorderThickness", 0.075f, "Bodycam cinematic border thickness");
            _bodycamAxonOverlayEnabled = _category.CreateEntry("BodycamAxonOverlayEnabled", true, "Show native AXON BODY 3 overlay");
            _bodycamBeepEnabled = _category.CreateEntry("BodycamBeepEnabled", true, "Play AXON recording beep every two minutes");
            _bodycamBeepVolume = _category.CreateEntry("BodycamBeepVolume", 0.35f, "AXON beep volume");
            _bodycamCameraId = _category.CreateEntry("BodycamCameraId", string.Empty, "Override AXON camera ID. Leave empty for automatic ID.");
            _bodycamOverlayScale = _category.CreateEntry("BodycamOverlayScale", 1.0f, "AXON overlay scale");
            _bodycamOverlayOpacity = _category.CreateEntry("BodycamOverlayOpacity", 1.0f, "AXON overlay opacity");
            _bodycamOverlayOffsetX = _category.CreateEntry("BodycamOverlayOffsetX", 0f, "AXON overlay horizontal offset in pixels");
            _bodycamOverlayOffsetY = _category.CreateEntry("BodycamOverlayOffsetY", 0f, "AXON overlay vertical offset in pixels");
            _bodycamShakeIntensity = _category.CreateEntry("BodycamShakeIntensity", 0.35f, "Realistic bodycam shake from stress and injury");
            _bodycamNoiseIntensity = _category.CreateEntry("BodycamNoiseIntensity", 0.10f, "Digital sensor noise intensity");
            _bodycamCompressionIntensity = _category.CreateEntry("BodycamCompressionIntensity", 0.08f, "Digital compression tint intensity");
            _bodycamBatteryIndicator = _category.CreateEntry("BodycamBatteryIndicator", true, "Show low battery indicator in the AXON overlay");
        }

        public static void Save()
        {
            MelonPreferences.Save();
        }

        public static void SetBloodEnabled(bool value)
        {
            Set(_enableBlood, value);
        }

        public static void SetBloodIntensity(float value)
        {
            Set(_bloodIntensity, Config.Clamp(value, 0f, 2.5f));
        }

        public static void SetBloodFadeSeconds(float value)
        {
            Set(_bloodFadeSeconds, Config.Clamp(value, 25f, 360f));
        }

        public static void SetUnconsciousEffects(bool value)
        {
            Set(_unconsciousEffects, value);
        }

        public static void SetPainEffects(bool value)
        {
            Set(_painEffects, value);
        }

        public static void SetPainOverlayEnabled(bool value)
        {
            Set(_painOverlayEnabled, value);
        }

        public static void SetPainOverlayOpacity(float value)
        {
            Set(_painOverlayOpacity, Config.Clamp(value, 0f, 1f));
        }

        public static void SetPainOverlayPulseIntensity(float value)
        {
            Set(_painOverlayPulseIntensity, Config.Clamp(value, 0f, 2f));
        }

        public static void SetPainOverlayBloodOpacity(float value)
        {
            Set(_painOverlayBloodOpacity, Config.Clamp(value, 0f, 1.5f));
        }

        public static void SetPainOverlayAnimationSpeed(float value)
        {
            Set(_painOverlayAnimationSpeed, Config.Clamp(value, 0.25f, 2.5f));
        }

        public static void SetOrganSystem(bool value)
        {
            Set(_organSystem, value);
        }

        public static void SetRealisticAudio(bool value)
        {
            Set(_realisticAudio, value);
        }

        public static void SetCustomSoundsEnabled(bool value)
        {
            Set(_customSoundsEnabled, value);
        }

        public static void SetMasterVolume(float value)
        {
            Set(_masterVolume, Config.Clamp(value, 0f, 2f));
        }

        public static void SetAudioIntensity(float value)
        {
            Set(_audioIntensity, Config.Clamp(value, 0f, 2f));
        }

        public static void SetHeadshotSoundVolume(float value)
        {
            Set(_headshotSoundVolume, Config.Clamp(value, 0f, 2f));
        }

        public static void SetDeathSoundVolume(float value)
        {
            Set(_deathSoundVolume, Config.Clamp(value, 0f, 2f));
        }

        public static void SetMetalImpactSoundVolume(float value)
        {
            Set(_metalImpactSoundVolume, Config.Clamp(value, 0f, 2f));
        }

        public static void SetGearSoundsEnabled(bool value)
        {
            Set(_gearSoundsEnabled, value);
            if (!value)
                MainMod.Runtime?.GearSounds.Reset();
        }

        public static void SetGearVolume(float value)
        {
            Set(_gearVolume, Config.Clamp(value, 0f, 2f));
        }

        public static void SetGearDistance(float value)
        {
            Set(_gearDistance, Config.Clamp(value, 3f, 35f));
        }

        public static void SetGearSoundSpeedMultiplier(float value)
        {
            Set(_gearSoundSpeedMultiplier, Config.Clamp(value, 0.35f, 2.5f));
        }

        public static void SetScreenEffectsIntensity(float value)
        {
            Set(_screenEffectsIntensity, Config.Clamp(value, 0f, 2f));
        }

        public static void SetFractureSeverity(float value)
        {
            Set(_fractureSeverity, Config.Clamp(value, 0.25f, 2.5f));
        }

        public static void SetDebugMode(bool value)
        {
            Set(_debugMode, value);
        }

        public static void SetHudLayout(int value)
        {
            Set(_hudLayout, ClampInt(value, 0, 2));
        }

        public static void SetHudMode(int value)
        {
            SetHudLayout(value);
        }

        public static void SetHudPosition(int value)
        {
            Set(_hudPosition, ClampInt(value, 0, 3));
        }

        public static void SetHudScaleMode(int value)
        {
            Set(_hudScaleMode, ClampInt(value, 0, 3));
        }

        public static void SetHudScaleCustom(float value)
        {
            Set(_hudScaleCustom, Config.Clamp(value, 0.4f, 1.8f));
        }

        public static void SetHudOpacity(float value)
        {
            Set(_hudOpacity, Config.Clamp(value, 0.12f, 1f));
        }

        public static void SetHudOffsetX(float value)
        {
            Set(_hudOffsetX, Config.Clamp(value, -0.8f, 0.8f));
        }

        public static void SetHudOffsetY(float value)
        {
            Set(_hudOffsetY, Config.Clamp(value, -0.6f, 0.6f));
        }

        public static void SetHudShowBlood(bool value)
        {
            Set(_hudShowBlood, value);
        }

        public static void SetHudShowPain(bool value)
        {
            Set(_hudShowPain, value);
        }

        public static void SetHudShowOxygen(bool value)
        {
            Set(_hudShowOxygen, value);
        }

        public static void SetHudShowFractures(bool value)
        {
            Set(_hudShowFractures, value);
        }

        public static void SetHudShowOrganStatus(bool value)
        {
            Set(_hudShowOrganStatus, value);
        }

        public static void SetHudShowBleeding(bool value)
        {
            Set(_hudShowBleeding, value);
        }

        public static void SetHudShowConsciousness(bool value)
        {
            Set(_hudShowConsciousness, value);
        }

        public static void SetHudShowTreatment(bool value)
        {
            Set(_hudShowTreatment, value);
        }

        public static void SetHudShowBody(bool value)
        {
            Set(_hudShowBody, value);
        }

        public static void SetHudShowStats(bool value)
        {
            Set(_hudShowStats, value);
        }

        public static void SetHudShowInjuryNames(bool value)
        {
            Set(_hudShowInjuryNames, value);
        }

        public static void SetHudShowVitalSigns(bool value)
        {
            Set(_hudShowVitalSigns, value);
        }

        public static void SetHudBodyOpacity(float value)
        {
            Set(_hudBodyOpacity, Config.Clamp(value, 0.25f, 1f));
        }

        public static void SetHudColorTheme(int value)
        {
            Set(_hudColorTheme, ClampInt(value, 0, 2));
        }

        public static void SetHudHideWhileConscious(bool value)
        {
            Set(_hudHideWhileConscious, value);
        }

        public static void SetHudForceAlwaysVisible(bool value)
        {
            Set(_hudForceAlwaysVisible, value);
        }

        public static void SetMedicalMenuEnabled(bool value)
        {
            Set(_medicalMenuEnabled, value);
        }

        public static void SetMiniHudEnabled(bool value)
        {
            Set(_miniHudEnabled, value);
        }

        public static void SetMiniHudScale(float value)
        {
            Set(_miniHudScale, Config.Clamp(value, 0.35f, 1.35f));
        }

        public static void SetMiniHudOpacity(float value)
        {
            Set(_miniHudOpacity, Config.Clamp(value, 0.45f, 1f));
        }

        public static void SetMedicalMenuScale(float value)
        {
            Set(_medicalMenuScale, Config.Clamp(value, 0.65f, 1.25f));
        }

        public static void SetMedicalMenuOpacity(float value)
        {
            Set(_medicalMenuOpacity, Config.Clamp(value, 0.45f, 1f));
        }

        public static void SetShowDetailedStats(bool value)
        {
            Set(_showDetailedStats, value);
        }

        public static void SetDisableHudCompletely(bool value)
        {
            Set(_disableHudCompletely, value);
        }

        public static void SetKnifePenetration(bool value)
        {
            Set(_knifePenetration, value);
        }

        public static void SetNativeBlood(bool value)
        {
            Set(_nativeBlood, value);
        }

        public static void SetForensicsEnabled(bool value)
        {
            Set(_forensicsEnabled, value);
            if (!value)
                MainMod.Runtime?.Forensics.Reset();
        }

        public static void SetForensicTraceLifetimeSeconds(float value)
        {
            Set(_forensicTraceLifetimeSeconds, Config.Clamp(value, 30f, 900f));
        }

        public static void SetForensicCasingChance(float value)
        {
            Set(_forensicCasingChance, Config.Clamp(value, 0f, 1f));
        }

        public static void SetCasualtyDraggingEnabled(bool value)
        {
            Set(_casualtyDraggingEnabled, value);
            if (!value)
                MainMod.Runtime?.CasualtyDrag.Reset();
        }

        public static void SetNeckTraumaEnabled(bool value)
        {
            Set(_neckTraumaEnabled, value);
        }

        public static void SetNeckGrabEnabled(bool value)
        {
            SetNeckSnapEnabled(value);
        }

        public static void SetNeckSnapEnabled(bool value)
        {
            Set(_neckSnapEnabled, value);
            Set(_neckGrabEnabled, value);
            if (!value)
                MainMod.Runtime?.NeckGrab.Reset();
        }

        public static void SetAllowNeckSnapOnPlayers(bool value)
        {
            Set(_allowNeckSnapOnPlayers, value);
        }

        public static void SetNeckSnapSoundEnabled(bool value)
        {
            Set(_neckSnapSoundEnabled, value);
        }

        public static void SetPersistentCorpsesEnabled(bool value)
        {
            Set(_persistentCorpsesEnabled, value);
            if (!value)
                MainMod.Runtime?.PersistentCorpses.ClearAll();
        }

        public static void SetPersistentCorpseMaxCount(int value)
        {
            Set(_persistentCorpseMaxCount, ClampInt(value, 0, 12));
            MainMod.Runtime?.PersistentCorpses.EnforceLimits();
        }

        public static void SetPersistentCorpseLifetimeSeconds(float value)
        {
            Set(_persistentCorpseLifetimeSeconds, Config.Clamp(value, 20f, 900f));
        }

        public static void SetBodycamEnabled(bool value)
        {
            Set(_bodycamEnabled, value);
            if (!value)
                MainMod.Runtime?.Bodycam.Destroy();
        }

        public static void SetBodycamPreset(int value)
        {
            Set(_bodycamPreset, ClampInt(value, 0, 3));
        }

        public static void SetBodycamHelmetCamera(bool value)
        {
            Set(_bodycamHelmetCamera, value);
        }

        public static void SetBodycamChestCamera(bool value)
        {
            Set(_bodycamHelmetCamera, !value);
        }

        public static void SetBodycamFov(float value)
        {
            Set(_bodycamFov, Config.Clamp(value, 60f, 170f));
        }

        public static void SetBodycamForwardOffset(float value)
        {
            Set(_bodycamForwardOffset, Config.Clamp(value, 0f, 1.0f));
        }

        public static void SetBodycamSmoothing(float value)
        {
            Set(_bodycamSmoothing, Config.Clamp(value, 0.5f, 20f));
        }

        public static void SetBodycamShowBorders(bool value)
        {
            Set(_bodycamShowBorders, value);
        }

        public static void SetBodycamBorderThickness(float value)
        {
            Set(_bodycamBorderThickness, Config.Clamp(value, 0f, 0.22f));
        }

        public static void SetBodycamAxonOverlayEnabled(bool value)
        {
            Set(_bodycamAxonOverlayEnabled, value);
        }

        public static void SetBodycamBeepEnabled(bool value)
        {
            Set(_bodycamBeepEnabled, value);
        }

        public static void SetBodycamBeepVolume(float value)
        {
            Set(_bodycamBeepVolume, Config.Clamp(value, 0f, 1f));
        }

        public static void SetBodycamCameraId(string value)
        {
            string sanitized = SanitizeCameraId(value);
            Set(_bodycamCameraId, sanitized);
            MainMod.Runtime?.Bodycam.ResetAxonIdentity();
        }

        public static void RandomizeBodycamCameraId()
        {
            Set(_bodycamCameraId, string.Empty);
            MainMod.Runtime?.Bodycam.ResetAxonIdentity();
        }

        public static void SetBodycamOverlayScale(float value)
        {
            Set(_bodycamOverlayScale, Config.Clamp(value, 0.55f, 1.8f));
        }

        public static void SetBodycamOverlayOpacity(float value)
        {
            Set(_bodycamOverlayOpacity, Config.Clamp(value, 0f, 1f));
        }

        public static void SetBodycamOverlayOffsetX(float value)
        {
            Set(_bodycamOverlayOffsetX, Config.Clamp(value, -500f, 500f));
        }

        public static void SetBodycamOverlayOffsetY(float value)
        {
            Set(_bodycamOverlayOffsetY, Config.Clamp(value, -300f, 300f));
        }

        public static void SetBodycamShakeIntensity(float value)
        {
            Set(_bodycamShakeIntensity, Config.Clamp(value, 0f, 1.5f));
        }

        public static void SetBodycamNoiseIntensity(float value)
        {
            Set(_bodycamNoiseIntensity, Config.Clamp(value, 0f, 1f));
        }

        public static void SetBodycamCompressionIntensity(float value)
        {
            Set(_bodycamCompressionIntensity, Config.Clamp(value, 0f, 1f));
        }

        public static void SetBodycamBatteryIndicator(bool value)
        {
            Set(_bodycamBatteryIndicator, value);
        }

        private static string SanitizeCameraId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            char[] buffer = new char[value.Length < 16 ? value.Length : 16];
            int count = 0;
            for (int i = 0; i < value.Length && count < buffer.Length; i++)
            {
                char c = char.ToUpperInvariant(value[i]);
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    buffer[count++] = c;
            }

            return count == 0 ? string.Empty : new string(buffer, 0, count);
        }

        private static void Set(MelonPreferences_Entry<bool>? entry, bool value)
        {
            if (entry == null)
                return;
            entry.Value = value;
            Save();
        }

        private static void Set(MelonPreferences_Entry<float>? entry, float value)
        {
            if (entry == null)
                return;
            entry.Value = value;
            Save();
        }

        private static void Set(MelonPreferences_Entry<string>? entry, string value)
        {
            if (entry == null)
                return;
            entry.Value = value;
            Save();
        }

        private static void Set(MelonPreferences_Entry<int>? entry, int value)
        {
            if (entry == null)
                return;
            entry.Value = value;
            Save();
        }

        private static int ClampInt(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
