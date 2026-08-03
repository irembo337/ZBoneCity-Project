using MelonLoader;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public static class Config
    {
        public const string ModName = "ZBoneCity";
        public const string ModVersion = "1.0.4";
        public const int LimbCount = 6;
        public const int OrganCount = 6;
        public const int BoneCount = 23;
        public const int MaxBleedSources = 24;
        public const int MaxMedicalItems = 24;
        public const int MaxBloodDecals = 96;
        public const int MaxBloodParticles = 12;

        private static MelonPreferences_Category? _category;
        private static MelonPreferences_Entry<bool>? _enabled;
        private static MelonPreferences_Entry<bool>? _hudEnabled;
        private static MelonPreferences_Entry<bool>? _spawnMedicalItems;
        private static MelonPreferences_Entry<bool>? _npcEnabled;
        private static MelonPreferences_Entry<float>? _bleedTickInterval;
        private static MelonPreferences_Entry<float>? _systemTickInterval;
        private static MelonPreferences_Entry<float>? _playerDamageScale;
        private static MelonPreferences_Entry<float>? _npcDamageScale;
        private static MelonPreferences_Entry<float>? _weaponDamageMultiplier;
        private static MelonPreferences_Entry<float>? _environmentDamageMultiplier;
        private static MelonPreferences_Entry<float>? _bloodVolumeMl;
        private static MelonPreferences_Entry<float>? _unconsciousBloodMl;
        private static MelonPreferences_Entry<float>? _unconsciousDangerThreshold;
        private static MelonPreferences_Entry<float>? _unconsciousPainThreshold;
        private static MelonPreferences_Entry<float>? _unconsciousHeadDamageThreshold;
        private static MelonPreferences_Entry<float>? _unconsciousShockThreshold;
        private static MelonPreferences_Entry<float>? _criticalBloodMl;
        private static MelonPreferences_Entry<float>? _deathBloodMl;
        private static MelonPreferences_Entry<float>? _medicalApplyDistance;
        private static MelonPreferences_Entry<float>? _medicalSpawnDistance;
        private static MelonPreferences_Entry<float>? _bloodFxDensity;
        private static MelonPreferences_Entry<bool>? _bloodFxEnabled;
        private static MelonPreferences_Entry<bool>? _magazineCheckEnabled;
        private static MelonPreferences_Entry<bool>? _medicationOverdoseEnabled;
        private static MelonPreferences_Entry<bool>? _rehabilitationEnabled;

        private static readonly float[] BleedRatesMlPerSecond =
        {
            0f,
            2.5f,
            8.5f,
            18.0f,
            42.0f
        };

        private static readonly MedicalPresentation[] MedicalPresentationTable =
        {
            new MedicalPresentation("BANDAGE", new Color(0.95f, 0.95f, 0.9f, 1f)),
            new MedicalPresentation("TOURNIQUET", new Color(0.05f, 0.05f, 0.06f, 1f)),
            new MedicalPresentation("MORPHINE", new Color(0.15f, 0.45f, 0.95f, 1f)),
            new MedicalPresentation("MEDKIT", new Color(0.8f, 0.08f, 0.08f, 1f)),
            new MedicalPresentation("ADRENALINE", new Color(0.95f, 0.72f, 0.08f, 1f)),
            new MedicalPresentation("SPLINT", new Color(0.56f, 0.38f, 0.18f, 1f)),
            new MedicalPresentation("BLOOD PACK", new Color(0.55f, 0.0f, 0.04f, 1f)),
            new MedicalPresentation("PAINKILLERS", new Color(0.92f, 0.90f, 0.78f, 1f)),
            new MedicalPresentation("ETG", new Color(0.28f, 0.95f, 0.70f, 1f)),
            new MedicalPresentation("SJ1", new Color(0.60f, 0.80f, 1.00f, 1f))
        };

        public static bool Enabled => _enabled?.Value ?? true;
        public static bool HudEnabled => _hudEnabled?.Value ?? true;
        public static bool SpawnMedicalItems => false;
        public static bool NpcEnabled => _npcEnabled?.Value ?? true;
        public static float BleedTickInterval => Clamp(_bleedTickInterval?.Value ?? 0.5f, 0.1f, 2.0f);
        public static float SystemTickInterval => Clamp(_systemTickInterval?.Value ?? 0.2f, 0.05f, 1.0f);
        public static float PlayerDamageScale => Clamp(_playerDamageScale?.Value ?? 0.65f, 0.05f, 5.0f);
        public static float NpcDamageScale => Clamp(_npcDamageScale?.Value ?? 1.0f, 0.05f, 5.0f);
        public static float WeaponDamageMultiplier => Clamp(_weaponDamageMultiplier?.Value ?? 1.14f, 0.75f, 1.75f);
        public static float EnvironmentDamageMultiplier => Clamp(_environmentDamageMultiplier?.Value ?? 1.18f, 0.75f, 1.85f);
        public static float BloodVolumeMl => Clamp(_bloodVolumeMl?.Value ?? 5000f, 3000f, 8000f);
        public static float UnconsciousBloodMl => Clamp(_unconsciousBloodMl?.Value ?? 3100f, 1800f, 6000f);
        public static float UnconsciousDangerThreshold => Clamp(_unconsciousDangerThreshold?.Value ?? 2.45f, 1.2f, 3.5f);
        public static float UnconsciousPainThreshold => Clamp(_unconsciousPainThreshold?.Value ?? 96f, 45f, 145f);
        public static float UnconsciousHeadDamageThreshold => Clamp(_unconsciousHeadDamageThreshold?.Value ?? 0.58f, 0.20f, 1.0f);
        public static float UnconsciousShockThreshold => Clamp(_unconsciousShockThreshold?.Value ?? 0.78f, 0.25f, 1.25f);
        public static float CriticalBloodMl => Clamp(_criticalBloodMl?.Value ?? 2400f, 1200f, 5000f);
        public static float DeathBloodMl => Clamp(_deathBloodMl?.Value ?? 1800f, 800f, 4000f);
        public static float MedicalApplyDistance => Clamp(_medicalApplyDistance?.Value ?? 0.42f, 0.15f, 1.25f);
        public static float MedicalSpawnDistance => Clamp(_medicalSpawnDistance?.Value ?? 1.15f, 0.4f, 3.0f);
        public static bool BloodFxEnabled => (_bloodFxEnabled?.Value ?? true) && SettingsMenu.BloodEnabled;
        public static float BloodFxDensity => Clamp((_bloodFxDensity?.Value ?? 1.0f) * SettingsMenu.BloodIntensity, 0.0f, 3.5f);
        public static bool MagazineCheckEnabled => _magazineCheckEnabled?.Value ?? true;
        public static bool MedicationOverdoseEnabled => _medicationOverdoseEnabled?.Value ?? true;
        public static bool RehabilitationEnabled => _rehabilitationEnabled?.Value ?? true;
        public static float BloodFadeSeconds => SettingsMenu.BloodFadeSeconds;
        public static bool UnconsciousEffectsEnabled => SettingsMenu.UnconsciousEffectsEnabled;
        public static bool PainEffectsEnabled => SettingsMenu.PainEffectsEnabled;
        public static bool PainOverlayEnabled => SettingsMenu.PainOverlayEnabled;
        public static float PainOverlayOpacity => SettingsMenu.PainOverlayOpacity;
        public static float PainOverlayPulseIntensity => SettingsMenu.PainOverlayPulseIntensity;
        public static float PainOverlayBloodOpacity => SettingsMenu.PainOverlayBloodOpacity;
        public static float PainOverlayAnimationSpeed => SettingsMenu.PainOverlayAnimationSpeed;
        public static bool OrganSystemEnabled => SettingsMenu.OrganSystemEnabled;
        public static bool RealisticAudioEnabled => SettingsMenu.RealisticAudioEnabled;
        public static bool CustomSoundsEnabled => SettingsMenu.CustomSoundsEnabled;
        public static float MasterVolume => SettingsMenu.MasterVolume;
        public static float AudioIntensity => SettingsMenu.AudioIntensity;
        public static float HeadshotSoundVolume => SettingsMenu.HeadshotSoundVolume;
        public static float DeathSoundVolume => SettingsMenu.DeathSoundVolume;
        public static float MetalImpactSoundVolume => SettingsMenu.MetalImpactSoundVolume;
        public static bool GearSoundsEnabled => SettingsMenu.GearSoundsEnabled;
        public static float GearVolume => SettingsMenu.GearVolume;
        public static float GearDistance => SettingsMenu.GearDistance;
        public static float GearSoundSpeedMultiplier => SettingsMenu.GearSoundSpeedMultiplier;
        public static float ScreenEffectsIntensity => SettingsMenu.ScreenEffectsIntensity;
        public static float FractureSeverity => SettingsMenu.FractureSeverity;
        public static bool DebugMode => SettingsMenu.DebugMode;
        public static int HudMode => SettingsMenu.HudLayout;
        public static int HudLayout => SettingsMenu.HudLayout;
        public static int HudPosition => SettingsMenu.HudPosition;
        public static int HudScaleMode => SettingsMenu.HudScaleMode;
        public static float HudScaleCustom => SettingsMenu.HudScaleCustom;
        public static float HudOpacity => SettingsMenu.HudOpacity;
        public static float HudOffsetX => SettingsMenu.HudOffsetX;
        public static float HudOffsetY => SettingsMenu.HudOffsetY;
        public static bool HudShowBlood => SettingsMenu.HudShowBlood;
        public static bool HudShowPain => SettingsMenu.HudShowPain;
        public static bool HudShowOxygen => SettingsMenu.HudShowOxygen;
        public static bool HudShowFractures => SettingsMenu.HudShowFractures;
        public static bool HudShowOrganStatus => SettingsMenu.HudShowOrganStatus;
        public static bool HudShowBleeding => SettingsMenu.HudShowBleeding;
        public static bool HudShowConsciousness => SettingsMenu.HudShowConsciousness;
        public static bool HudShowTreatment => SettingsMenu.HudShowTreatment;
        public static bool HudShowBody => SettingsMenu.HudShowBody;
        public static bool HudShowStats => SettingsMenu.HudShowStats;
        public static bool HudShowInjuryNames => SettingsMenu.HudShowInjuryNames;
        public static bool HudShowVitalSigns => SettingsMenu.HudShowVitalSigns;
        public static float HudBodyOpacity => SettingsMenu.HudBodyOpacity;
        public static int HudColorTheme => SettingsMenu.HudColorTheme;
        public static bool HudHideWhileConscious => SettingsMenu.HudHideWhileConscious;
        public static bool HudForceAlwaysVisible => SettingsMenu.HudForceAlwaysVisible;
        public static bool MedicalMenuEnabled => SettingsMenu.MedicalMenuEnabled;
        public static bool MiniHudEnabled => SettingsMenu.MiniHudEnabled;
        public static float MiniHudScale => SettingsMenu.MiniHudScale;
        public static float MiniHudOpacity => SettingsMenu.MiniHudOpacity;
        public static float MedicalMenuScale => SettingsMenu.MedicalMenuScale;
        public static float MedicalMenuOpacity => SettingsMenu.MedicalMenuOpacity;
        public static bool ShowDetailedStats => SettingsMenu.ShowDetailedStats;
        public static bool DisableHudCompletely => SettingsMenu.DisableHudCompletely;
        public static bool KnifePenetrationEnabled => SettingsMenu.KnifePenetrationEnabled;
        public static bool NativeBloodEnabled => SettingsMenu.NativeBloodEnabled;
        public static bool ForensicsEnabled => SettingsMenu.ForensicsEnabled;
        public static float ForensicTraceLifetimeSeconds => SettingsMenu.ForensicTraceLifetimeSeconds;
        public static float ForensicCasingChance => SettingsMenu.ForensicCasingChance;
        public static bool CasualtyDraggingEnabled => SettingsMenu.CasualtyDraggingEnabled;
        public static bool NeckTraumaEnabled => SettingsMenu.NeckTraumaEnabled;
        public static bool NeckSnapEnabled => SettingsMenu.NeckSnapEnabled;
        public static bool NeckGrabEnabled => SettingsMenu.NeckGrabEnabled;
        public static bool AllowNeckSnapOnPlayers => SettingsMenu.AllowNeckSnapOnPlayers;
        public static bool NeckSnapSoundEnabled => SettingsMenu.NeckSnapSoundEnabled;
        public static bool PersistentCorpsesEnabled => SettingsMenu.PersistentCorpsesEnabled;
        public static int PersistentCorpseMaxCount => SettingsMenu.PersistentCorpseMaxCount;
        public static float PersistentCorpseLifetimeSeconds => SettingsMenu.PersistentCorpseLifetimeSeconds;
        public static bool BodycamEnabled => SettingsMenu.BodycamEnabled;
        public static int BodycamPreset => SettingsMenu.BodycamPreset;
        public static bool BodycamHelmetCamera => SettingsMenu.BodycamHelmetCamera;
        public static bool BodycamChestCamera => SettingsMenu.BodycamChestCamera;
        public static float BodycamFov => SettingsMenu.BodycamFov;
        public static float BodycamForwardOffset => SettingsMenu.BodycamForwardOffset;
        public static float BodycamSmoothing => SettingsMenu.BodycamSmoothing;
        public static bool BodycamShowBorders => SettingsMenu.BodycamShowBorders;
        public static float BodycamBorderThickness => SettingsMenu.BodycamBorderThickness;
        public static bool BodycamAxonOverlayEnabled => SettingsMenu.BodycamAxonOverlayEnabled;
        public static bool BodycamBeepEnabled => SettingsMenu.BodycamBeepEnabled;
        public static float BodycamBeepVolume => SettingsMenu.BodycamBeepVolume;
        public static string BodycamCameraId => SettingsMenu.BodycamCameraId;
        public static string BodycamCameraIdDisplay => SettingsMenu.BodycamCameraIdDisplay;
        public static float BodycamOverlayScale => SettingsMenu.BodycamOverlayScale;
        public static float BodycamOverlayOpacity => SettingsMenu.BodycamOverlayOpacity;
        public static float BodycamOverlayOffsetX => SettingsMenu.BodycamOverlayOffsetX;
        public static float BodycamOverlayOffsetY => SettingsMenu.BodycamOverlayOffsetY;
        public static float BodycamShakeIntensity => SettingsMenu.BodycamShakeIntensity;
        public static float BodycamNoiseIntensity => SettingsMenu.BodycamNoiseIntensity;
        public static float BodycamCompressionIntensity => SettingsMenu.BodycamCompressionIntensity;
        public static bool BodycamBatteryIndicator => SettingsMenu.BodycamBatteryIndicator;

        public static readonly float[] LimbMaxHp =
        {
            95f,
            220f,
            125f,
            125f,
            145f,
            145f
        };

        public static readonly float[] LimbBleedingMultiplier =
        {
            1.65f,
            1.25f,
            0.9f,
            0.9f,
            1.15f,
            1.15f
        };

        public static readonly float[] LimbPainMultiplier =
        {
            1.85f,
            1.35f,
            1.0f,
            1.0f,
            1.15f,
            1.15f
        };

        public static void Load()
        {
            _category = MelonPreferences.CreateCategory("ZBoneCity", "ZBoneCity");
            _enabled = _category.CreateEntry("Enabled", true, "Enable ZBoneCity");
            _hudEnabled = _category.CreateEntry("HudEnabled", true, "Enable VR health HUD");
            _spawnMedicalItems = _category.CreateEntry("SpawnMedicalItems", false, "Spawn runtime medical items near the player");
            _npcEnabled = _category.CreateEntry("NpcEnabled", true, "Enable advanced health simulation for NPCs");
            _bleedTickInterval = _category.CreateEntry("BleedTickInterval", 0.5f, "Seconds between blood-loss calculations");
            _systemTickInterval = _category.CreateEntry("SystemTickInterval", 0.2f, "Seconds between main health system updates");
            _playerDamageScale = _category.CreateEntry("PlayerDamageScale", 0.65f, "Advanced damage multiplier for player");
            _npcDamageScale = _category.CreateEntry("NpcDamageScale", 1.0f, "Advanced damage multiplier for NPCs");
            _weaponDamageMultiplier = _category.CreateEntry("WeaponDamageMultiplier", 1.14f, "Additional firearm and explosion danger multiplier");
            _environmentDamageMultiplier = _category.CreateEntry("EnvironmentDamageMultiplier", 1.18f, "Additional damage multiplier for falls and hard environmental impacts above the safe threshold");
            _bloodVolumeMl = _category.CreateEntry("BloodVolumeMl", 5000f, "Normal adult blood volume in milliliters");
            _unconsciousBloodMl = _category.CreateEntry("UnconsciousBloodMl", 2850f, "Blood level where unconsciousness becomes likely");
            _unconsciousDangerThreshold = _category.CreateEntry("UnconsciousDangerThreshold", 2.45f, "Combined danger threshold required before ordinary trauma can cause unconsciousness");
            _unconsciousPainThreshold = _category.CreateEntry("UnconsciousPainThreshold", 96f, "Pain threshold required before pain shock can cause unconsciousness");
            _unconsciousHeadDamageThreshold = _category.CreateEntry("UnconsciousHeadDamageThreshold", 0.58f, "Head damage threshold required before head trauma can cause unconsciousness");
            _unconsciousShockThreshold = _category.CreateEntry("UnconsciousShockThreshold", 0.78f, "Shock threshold required before shock can cause unconsciousness");
            _criticalBloodMl = _category.CreateEntry("CriticalBloodMl", 2400f, "Critical blood level used for pulse and blackout");
            _deathBloodMl = _category.CreateEntry("DeathBloodMl", 1800f, "Blood level that causes death");
            _medicalApplyDistance = _category.CreateEntry("MedicalApplyDistance", 0.42f, "Distance from head/body to consume medical items");
            _medicalSpawnDistance = _category.CreateEntry("MedicalSpawnDistance", 1.15f, "Distance in front of player for spawned medical items");
            _bloodFxEnabled = _category.CreateEntry("BloodFxEnabled", true, "Enable pooled blood decals and drip particles");
            _bloodFxDensity = _category.CreateEntry("BloodFxDensity", 1.0f, "Blood FX density multiplier");
            _magazineCheckEnabled = _category.CreateEntry("MagazineCheckEnabled", true, "Enable approximate Ready Or Not style magazine checks");
            _medicationOverdoseEnabled = _category.CreateEntry("MedicationOverdoseEnabled", true, "Enable medicine dose stacking, side effects and overdose risk");
            _rehabilitationEnabled = _category.CreateEntry("RehabilitationEnabled", true, "Enable post-treatment limping, weakness and gradual recovery");
            SettingsMenu.Initialize();
            MelonPreferences.Save();
        }

        public static void SetHudEnabled(bool value)
        {
            if (_hudEnabled == null)
                return;

            _hudEnabled.Value = value;
            if (!value)
                MainMod.Runtime?.Hud.Destroy();
            MelonPreferences.Save();
        }

        public static void SetEnabled(bool value)
        {
            if (_enabled == null)
                return;

            _enabled.Value = value;
            MainMod.Runtime?.ApplyMasterEnabledChanged(value);
            MelonPreferences.Save();
        }

        public static void SetMagazineCheckEnabled(bool value)
        {
            if (_magazineCheckEnabled == null)
                return;

            _magazineCheckEnabled.Value = value;
            if (!value)
                MainMod.Runtime?.MagazineCheck.Reset();
            MelonPreferences.Save();
        }

        public static void SetMedicationOverdoseEnabled(bool value)
        {
            if (_medicationOverdoseEnabled == null)
                return;

            _medicationOverdoseEnabled.Value = value;
            MelonPreferences.Save();
        }

        public static void SetRehabilitationEnabled(bool value)
        {
            if (_rehabilitationEnabled == null)
                return;

            _rehabilitationEnabled.Value = value;
            MelonPreferences.Save();
        }

        public static float GetBleedRate(BleedSeverity severity)
        {
            int index = (int)severity;
            return index >= 0 && index < BleedRatesMlPerSecond.Length ? BleedRatesMlPerSecond[index] : 0f;
        }

        public static Color GetMedicalColor(MedicalItemType type)
        {
            return TryGetMedicalPresentation(type, out MedicalPresentation item) ? item.Color : Color.white;
        }

        public static string GetMedicalLabel(MedicalItemType type)
        {
            return TryGetMedicalPresentation(type, out MedicalPresentation item) ? item.Label : "MED";
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static bool TryGetMedicalPresentation(MedicalItemType type, out MedicalPresentation presentation)
        {
            int index = (int)type;
            if (index >= 0 && index < MedicalPresentationTable.Length)
            {
                presentation = MedicalPresentationTable[index];
                return true;
            }

            presentation = default;
            return false;
        }

        private readonly struct MedicalPresentation
        {
            public readonly string Label;
            public readonly Color Color;

            public MedicalPresentation(string label, Color color)
            {
                Label = label;
                Color = color;
            }
        }
    }

    public enum BodyPart
    {
        Head = 0,
        Torso = 1,
        LeftArm = 2,
        RightArm = 3,
        LeftLeg = 4,
        RightLeg = 5
    }

    public enum FractureState
    {
        None = 0,
        Sprain = 1,
        Fractured = 2,
        Shattered = 3
    }

    public enum BleedSeverity
    {
        None = 0,
        Light = 1,
        Medium = 2,
        Severe = 3,
        Arterial = 4
    }

    public enum ShockSeverity
    {
        None = 0,
        Mild = 1,
        Moderate = 2,
        Severe = 3,
        Critical = 4
    }

    public enum ConsciousnessState
    {
        Awake = 0,
        Blackout = 1,
        Unconscious = 2,
        Dead = 3
    }

    public enum AdvancedDamageType
    {
        Bullet = 0,
        Blunt = 1,
        Explosion = 2,
        Stab = 3,
        Fall = 4
    }

    public enum MedicalItemType
    {
        Bandage = 0,
        Tourniquet = 1,
        Morphine = 2,
        Medkit = 3,
        Adrenaline = 4,
        Splint = 5,
        BloodPack = 6,
        Painkillers = 7,
        ETGStimulator = 8,
        SJ1Stimulator = 9
    }

    public enum WoundSeverity
    {
        None = 0,
        SurfaceCut = 1,
        DeepCut = 2,
        ArterialCut = 3,
        OrganRupture = 4
    }

    public enum OrganType
    {
        Brain = 0,
        Heart = 1,
        Lungs = 2,
        Liver = 3,
        Stomach = 4,
        Muscles = 5
    }

    public enum OrganFailureState
    {
        Healthy = 0,
        Damaged = 1,
        Critical = 2,
        Failed = 3
    }

    public enum BoneType
    {
        Skull = 0,
        Spine = 1,
        Pelvis = 2,
        LeftHumerus = 3,
        RightHumerus = 4,
        LeftForearm = 5,
        RightForearm = 6,
        LeftFemur = 7,
        RightFemur = 8,
        LeftShin = 9,
        RightShin = 10,
        LeftRib1 = 11,
        LeftRib2 = 12,
        LeftRib3 = 13,
        LeftRib4 = 14,
        LeftRib5 = 15,
        LeftRib6 = 16,
        RightRib1 = 17,
        RightRib2 = 18,
        RightRib3 = 19,
        RightRib4 = 20,
        RightRib5 = 21,
        RightRib6 = 22
    }

    public enum HealthOwnerKind
    {
        Player = 0,
        Npc = 1
    }
}
