using MelonLoader;

namespace BonelabAdvancedHealth
{
    public static class SettingsMenu
    {
        private static MelonPreferences_Category? _category;
        private static MelonPreferences_Entry<bool>? _enableBlood;
        private static MelonPreferences_Entry<float>? _bloodIntensity;
        private static MelonPreferences_Entry<bool>? _unconsciousEffects;
        private static MelonPreferences_Entry<bool>? _painEffects;
        private static MelonPreferences_Entry<bool>? _organSystem;
        private static MelonPreferences_Entry<bool>? _realisticAudio;
        private static MelonPreferences_Entry<float>? _fractureSeverity;
        private static MelonPreferences_Entry<bool>? _debugMode;
        private static MelonPreferences_Entry<int>? _hudMode;
        private static MelonPreferences_Entry<bool>? _knifePenetration;
        private static MelonPreferences_Entry<bool>? _nativeBlood;

        public static bool BloodEnabled => _enableBlood?.Value ?? true;
        public static float BloodIntensity => Config.Clamp(_bloodIntensity?.Value ?? 1f, 0f, 2.5f);
        public static bool UnconsciousEffectsEnabled => _unconsciousEffects?.Value ?? true;
        public static bool PainEffectsEnabled => _painEffects?.Value ?? true;
        public static bool OrganSystemEnabled => _organSystem?.Value ?? true;
        public static bool RealisticAudioEnabled => _realisticAudio?.Value ?? true;
        public static float FractureSeverity => Config.Clamp(_fractureSeverity?.Value ?? 1f, 0.25f, 2.5f);
        public static bool DebugMode => _debugMode?.Value ?? false;
        public static int HudMode => _hudMode?.Value ?? 1;
        public static bool KnifePenetrationEnabled => _knifePenetration?.Value ?? true;
        public static bool NativeBloodEnabled => _nativeBlood?.Value ?? true;

        public static void Initialize()
        {
            _category = MelonPreferences.CreateCategory("BonelabAdvancedHealth_Settings", "AHS Settings");
            _enableBlood = _category.CreateEntry("EnableBlood", true, "Enable blood simulation and visible blood FX");
            _bloodIntensity = _category.CreateEntry("BloodIntensity", 1.0f, "Blood FX and bleed visual intensity");
            _unconsciousEffects = _category.CreateEntry("UnconsciousEffects", true, "Enable blackout, tunnel vision, ringing and recovery effects");
            _painEffects = _category.CreateEntry("PainEffects", true, "Enable pain accumulation, shakes, movement penalties and pain audio");
            _organSystem = _category.CreateEntry("OrganSystem", true, "Enable organ damage, cardiac arrest and internal trauma");
            _realisticAudio = _category.CreateEntry("RealisticAudio", true, "Enable trauma audio, muffle, ringing and breathing loops");
            _fractureSeverity = _category.CreateEntry("FractureSeverity", 1.0f, "Global fracture severity multiplier");
            _debugMode = _category.CreateEntry("DebugMode", false, "Enable extra mod diagnostics in MelonLoader logs");
            _hudMode = _category.CreateEntry("HudMode", 1, "0 compact, 1 Tarkov body monitor, 2 debug vitals");
            _knifePenetration = _category.CreateEntry("KnifePenetration", true, "Enable embedded knife wound tracking");
            _nativeBlood = _category.CreateEntry("NativeBlood", true, "Prefer discovered BONELAB blood particle effects");
        }

        public static void Save()
        {
            MelonPreferences.Save();
        }
    }
}
