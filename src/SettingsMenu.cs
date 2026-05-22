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
        private static MelonPreferences_Entry<float>? _audioIntensity;
        private static MelonPreferences_Entry<float>? _screenEffectsIntensity;
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
        public static float AudioIntensity => Config.Clamp(_audioIntensity?.Value ?? 1f, 0f, 2f);
        public static float ScreenEffectsIntensity => Config.Clamp(_screenEffectsIntensity?.Value ?? 1f, 0f, 2f);
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
            _audioIntensity = _category.CreateEntry("AudioIntensity", 1.0f, "Trauma audio volume and layering intensity");
            _screenEffectsIntensity = _category.CreateEntry("ScreenEffectsIntensity", 1.0f, "Fullscreen pain and unconscious screen effect intensity");
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

        public static void SetBloodEnabled(bool value)
        {
            Set(_enableBlood, value);
        }

        public static void SetBloodIntensity(float value)
        {
            Set(_bloodIntensity, Config.Clamp(value, 0f, 2.5f));
        }

        public static void SetUnconsciousEffects(bool value)
        {
            Set(_unconsciousEffects, value);
        }

        public static void SetPainEffects(bool value)
        {
            Set(_painEffects, value);
        }

        public static void SetOrganSystem(bool value)
        {
            Set(_organSystem, value);
        }

        public static void SetRealisticAudio(bool value)
        {
            Set(_realisticAudio, value);
        }

        public static void SetAudioIntensity(float value)
        {
            Set(_audioIntensity, Config.Clamp(value, 0f, 2f));
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

        public static void SetHudMode(int value)
        {
            Set(_hudMode, value < 0 ? 0 : value > 2 ? 2 : value);
        }

        public static void SetKnifePenetration(bool value)
        {
            Set(_knifePenetration, value);
        }

        public static void SetNativeBlood(bool value)
        {
            Set(_nativeBlood, value);
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

        private static void Set(MelonPreferences_Entry<int>? entry, int value)
        {
            if (entry == null)
                return;
            entry.Value = value;
            Save();
        }
    }
}
