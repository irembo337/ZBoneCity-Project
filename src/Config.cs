using MelonLoader;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public static class Config
    {
        public const string ModName = "Bonelab Advanced Health";
        public const string ModVersion = "1.0.0";
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
        private static MelonPreferences_Entry<float>? _bloodVolumeMl;
        private static MelonPreferences_Entry<float>? _unconsciousBloodMl;
        private static MelonPreferences_Entry<float>? _criticalBloodMl;
        private static MelonPreferences_Entry<float>? _deathBloodMl;
        private static MelonPreferences_Entry<float>? _medicalApplyDistance;
        private static MelonPreferences_Entry<float>? _medicalSpawnDistance;
        private static MelonPreferences_Entry<float>? _bloodFxDensity;
        private static MelonPreferences_Entry<bool>? _bloodFxEnabled;

        public static bool Enabled => _enabled?.Value ?? true;
        public static bool HudEnabled => _hudEnabled?.Value ?? true;
        public static bool SpawnMedicalItems => false;
        public static bool NpcEnabled => _npcEnabled?.Value ?? true;
        public static float BleedTickInterval => Clamp(_bleedTickInterval?.Value ?? 0.5f, 0.1f, 2.0f);
        public static float SystemTickInterval => Clamp(_systemTickInterval?.Value ?? 0.2f, 0.05f, 1.0f);
        public static float PlayerDamageScale => Clamp(_playerDamageScale?.Value ?? 1.0f, 0.05f, 5.0f);
        public static float NpcDamageScale => Clamp(_npcDamageScale?.Value ?? 1.0f, 0.05f, 5.0f);
        public static float BloodVolumeMl => Clamp(_bloodVolumeMl?.Value ?? 5000f, 3000f, 8000f);
        public static float UnconsciousBloodMl => Clamp(_unconsciousBloodMl?.Value ?? 3100f, 1800f, 6000f);
        public static float CriticalBloodMl => Clamp(_criticalBloodMl?.Value ?? 2400f, 1200f, 5000f);
        public static float DeathBloodMl => Clamp(_deathBloodMl?.Value ?? 1800f, 800f, 4000f);
        public static float MedicalApplyDistance => Clamp(_medicalApplyDistance?.Value ?? 0.42f, 0.15f, 1.25f);
        public static float MedicalSpawnDistance => Clamp(_medicalSpawnDistance?.Value ?? 1.15f, 0.4f, 3.0f);
        public static bool BloodFxEnabled => (_bloodFxEnabled?.Value ?? true) && SettingsMenu.BloodEnabled;
        public static float BloodFxDensity => Clamp((_bloodFxDensity?.Value ?? 1.0f) * SettingsMenu.BloodIntensity, 0.0f, 3.5f);
        public static bool UnconsciousEffectsEnabled => SettingsMenu.UnconsciousEffectsEnabled;
        public static bool PainEffectsEnabled => SettingsMenu.PainEffectsEnabled;
        public static bool OrganSystemEnabled => SettingsMenu.OrganSystemEnabled;
        public static bool RealisticAudioEnabled => SettingsMenu.RealisticAudioEnabled;
        public static float FractureSeverity => SettingsMenu.FractureSeverity;
        public static bool DebugMode => SettingsMenu.DebugMode;
        public static int HudMode => SettingsMenu.HudMode;
        public static bool KnifePenetrationEnabled => SettingsMenu.KnifePenetrationEnabled;
        public static bool NativeBloodEnabled => SettingsMenu.NativeBloodEnabled;

        public static readonly float[] LimbMaxHp =
        {
            75f,
            160f,
            90f,
            90f,
            110f,
            110f
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
            _category = MelonPreferences.CreateCategory("BonelabAdvancedHealth", "BONELAB Advanced Health");
            _enabled = _category.CreateEntry("Enabled", true, "Enable advanced health simulation");
            _hudEnabled = _category.CreateEntry("HudEnabled", true, "Enable VR health HUD");
            _spawnMedicalItems = _category.CreateEntry("SpawnMedicalItems", false, "Spawn runtime medical items near the player");
            _npcEnabled = _category.CreateEntry("NpcEnabled", true, "Enable advanced health simulation for NPCs");
            _bleedTickInterval = _category.CreateEntry("BleedTickInterval", 0.5f, "Seconds between blood-loss calculations");
            _systemTickInterval = _category.CreateEntry("SystemTickInterval", 0.2f, "Seconds between main health system updates");
            _playerDamageScale = _category.CreateEntry("PlayerDamageScale", 1.0f, "Advanced damage multiplier for player");
            _npcDamageScale = _category.CreateEntry("NpcDamageScale", 1.0f, "Advanced damage multiplier for NPCs");
            _bloodVolumeMl = _category.CreateEntry("BloodVolumeMl", 5000f, "Normal adult blood volume in milliliters");
            _unconsciousBloodMl = _category.CreateEntry("UnconsciousBloodMl", 3100f, "Blood level where unconsciousness becomes likely");
            _criticalBloodMl = _category.CreateEntry("CriticalBloodMl", 2400f, "Critical blood level used for pulse and blackout");
            _deathBloodMl = _category.CreateEntry("DeathBloodMl", 1800f, "Blood level that causes death");
            _medicalApplyDistance = _category.CreateEntry("MedicalApplyDistance", 0.42f, "Distance from head/body to consume medical items");
            _medicalSpawnDistance = _category.CreateEntry("MedicalSpawnDistance", 1.15f, "Distance in front of player for spawned medical items");
            _bloodFxEnabled = _category.CreateEntry("BloodFxEnabled", true, "Enable pooled blood decals and drip particles");
            _bloodFxDensity = _category.CreateEntry("BloodFxDensity", 1.0f, "Blood FX density multiplier");
            SettingsMenu.Initialize();
            MelonPreferences.Save();
        }

        public static float GetBleedRate(BleedSeverity severity)
        {
            switch (severity)
            {
                case BleedSeverity.Light:
                    return 2.5f;
                case BleedSeverity.Medium:
                    return 8.5f;
                case BleedSeverity.Severe:
                    return 18.0f;
                case BleedSeverity.Arterial:
                    return 42.0f;
                default:
                    return 0f;
            }
        }

        public static Color GetMedicalColor(MedicalItemType type)
        {
            switch (type)
            {
                case MedicalItemType.Bandage:
                    return new Color(0.95f, 0.95f, 0.9f, 1f);
                case MedicalItemType.Tourniquet:
                    return new Color(0.05f, 0.05f, 0.06f, 1f);
                case MedicalItemType.Morphine:
                    return new Color(0.15f, 0.45f, 0.95f, 1f);
                case MedicalItemType.Medkit:
                    return new Color(0.8f, 0.08f, 0.08f, 1f);
                case MedicalItemType.Adrenaline:
                    return new Color(0.95f, 0.72f, 0.08f, 1f);
                case MedicalItemType.Splint:
                    return new Color(0.56f, 0.38f, 0.18f, 1f);
                case MedicalItemType.BloodPack:
                    return new Color(0.55f, 0.0f, 0.04f, 1f);
                default:
                    return Color.white;
            }
        }

        public static string GetMedicalLabel(MedicalItemType type)
        {
            switch (type)
            {
                case MedicalItemType.Bandage:
                    return "BANDAGE";
                case MedicalItemType.Tourniquet:
                    return "TOURNIQUET";
                case MedicalItemType.Morphine:
                    return "MORPHINE";
                case MedicalItemType.Medkit:
                    return "MEDKIT";
                case MedicalItemType.Adrenaline:
                    return "ADRENALINE";
                case MedicalItemType.Splint:
                    return "SPLINT";
                case MedicalItemType.BloodPack:
                    return "BLOOD PACK";
                default:
                    return "MED";
            }
        }

        public static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
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
        BloodPack = 6
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
