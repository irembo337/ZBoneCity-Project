using System;
using System.Text;
using BoneLib.BoneMenu;
using MelonLoader;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class BoneMenuIntegration
    {
        private readonly StringBuilder _builder = new StringBuilder(768);
        private Page? _rootPage;
        private StringElement? _bodyStatus;
        private StringElement? _organStatus;
        private StringElement? _bleedingStatus;
        private StringElement? _fractureStatus;
        private StringElement? _vitalsStatus;
        private StringElement? _debugStatus;
        private float _updateAccumulator;
        private bool _initialized;
        private bool _warnedUnavailable;

        public bool IsInitialized => _initialized;

        public void Initialize(MelonLogger.Instance logger)
        {
            if (_initialized)
                return;

            try
            {
                Color rootColor = new Color(0.70f, 0.10f, 0.08f, 1f);
                _rootPage = Page.Root.CreatePage("Advanced Health System", rootColor, 9, true);
                BuildStatusPages(_rootPage);
                BuildSettingsPage(_rootPage);
                BuildDebugPage(_rootPage);
                _initialized = true;
                logger.Msg("BoneMenu Advanced Health System page created.");
            }
            catch (Exception ex)
            {
                if (!_warnedUnavailable)
                {
                    _warnedUnavailable = true;
                    logger.Warning("BoneMenu integration unavailable, will retry: " + ex.Message);
                }
            }
        }

        public void Update(float deltaTime, HealthManager? manager)
        {
            if (!_initialized)
                return;

            _updateAccumulator += deltaTime;
            if (_updateAccumulator < 0.25f)
                return;

            _updateAccumulator = 0f;
            if (manager == null)
            {
                SetValue(_vitalsStatus, "PLAYER RIG: WAITING\nNO HEALTH DATA YET");
                SetValue(_bodyStatus, "No player health manager is active.");
                SetValue(_organStatus, "No organ data is active.");
                SetValue(_bleedingStatus, "No bleeding data is active.");
                SetValue(_fractureStatus, "No fracture data is active.");
                SetValue(_debugStatus, BuildDebug(null));
                return;
            }

            SetValue(_vitalsStatus, BuildVitals(manager));
            SetValue(_bodyStatus, BuildBody(manager));
            SetValue(_organStatus, BuildOrgans(manager));
            SetValue(_bleedingStatus, BuildBleeding(manager));
            SetValue(_fractureStatus, BuildFractures(manager));
            SetValue(_debugStatus, BuildDebug(manager));
        }

        private void BuildStatusPages(Page root)
        {
            Page vitals = root.CreatePage("Vitals", new Color(0.82f, 0.18f, 0.12f, 1f), 5, true);
            _vitalsStatus = vitals.CreateString("Live Status", Color.white, "Waiting for player rig...", _ => { });
            vitals.CreateFunction("Refresh", Color.white, () => ForceRefresh());

            Page body = root.CreatePage("Body Status", new Color(0.78f, 0.12f, 0.10f, 1f), 5, true);
            _bodyStatus = body.CreateString("Limbs", Color.white, "Waiting for damage data...", _ => { });
            body.CreateFunction("Refresh", Color.white, () => ForceRefresh());

            Page organs = root.CreatePage("Organ Status", new Color(0.75f, 0.10f, 0.18f, 1f), 5, true);
            _organStatus = organs.CreateString("Organs", Color.white, "Waiting for organ data...", _ => { });
            organs.CreateFunction("Refresh", Color.white, () => ForceRefresh());

            Page bleeding = root.CreatePage("Bleeding", new Color(0.70f, 0.02f, 0.02f, 1f), 5, true);
            _bleedingStatus = bleeding.CreateString("Blood Loss", Color.white, "Waiting for bleeding data...", _ => { });
            bleeding.CreateFunction("Refresh", Color.white, () => ForceRefresh());

            Page fractures = root.CreatePage("Fractures", new Color(0.95f, 0.36f, 0.04f, 1f), 5, true);
            _fractureStatus = fractures.CreateString("Bones", Color.white, "Waiting for fracture data...", _ => { });
            fractures.CreateFunction("Refresh", Color.white, () => ForceRefresh());
        }

        private void BuildSettingsPage(Page root)
        {
            Page settings = root.CreatePage("Settings", new Color(0.16f, 0.42f, 0.84f, 1f), 12, true);
            settings.CreateBool("Enable Pain FX", Color.white, Config.PainEffectsEnabled, SettingsMenu.SetPainEffects);
            settings.CreateBool("Enable Unconscious FX", Color.white, Config.UnconsciousEffectsEnabled, SettingsMenu.SetUnconsciousEffects);
            settings.CreateBool("Enable Blood", Color.white, Config.BloodFxEnabled, SettingsMenu.SetBloodEnabled);
            settings.CreateFloat("Blood Intensity", Color.white, SettingsMenu.BloodIntensity, 0.10f, 0f, 2.5f, SettingsMenu.SetBloodIntensity);
            settings.CreateBool("Organ Damage", Color.white, Config.OrganSystemEnabled, SettingsMenu.SetOrganSystem);
            settings.CreateFloat("Fracture Realism", Color.white, Config.FractureSeverity, 0.10f, 0.25f, 2.5f, SettingsMenu.SetFractureSeverity);
            settings.CreateBool("Realistic Audio", Color.white, Config.RealisticAudioEnabled, SettingsMenu.SetRealisticAudio);
            settings.CreateFloat("Audio Intensity", Color.white, Config.AudioIntensity, 0.10f, 0f, 2f, SettingsMenu.SetAudioIntensity);
            settings.CreateFloat("Screen FX Intensity", Color.white, Config.ScreenEffectsIntensity, 0.10f, 0f, 2f, SettingsMenu.SetScreenEffectsIntensity);
            settings.CreateInt("HUD Mode", Color.white, Config.HudMode, 1, 0, 2, SettingsMenu.SetHudMode);
            settings.CreateBool("Native Blood", Color.white, Config.NativeBloodEnabled, SettingsMenu.SetNativeBlood);
            settings.CreateBool("Debug Mode", Color.white, Config.DebugMode, SettingsMenu.SetDebugMode);
            settings.CreateFunction("Save Config", new Color(0.45f, 0.90f, 1f, 1f), SettingsMenu.Save);
        }

        private void BuildDebugPage(Page root)
        {
            Page debug = root.CreatePage("Debug", new Color(0.48f, 0.48f, 0.48f, 1f), 5, true);
            _debugStatus = debug.CreateString("Runtime", Color.white, "Waiting...", _ => { });
            debug.CreateFunction("Spawn Medical Items", Color.white, () => MainMod.Runtime?.SpawnMedicalItems());
            debug.CreateFunction("Refresh Menu", Color.white, () => ForceRefresh());
        }

        private void ForceRefresh()
        {
            _updateAccumulator = 999f;
        }

        private string BuildVitals(HealthManager manager)
        {
            _builder.Length = 0;
            _builder.Append("STATE: ");
            _builder.Append(manager.Consciousness.State);
            _builder.Append("\nCONSCIOUSNESS: ");
            _builder.Append(Mathf.RoundToInt((1f - manager.Consciousness.BlackoutIntensity) * 100f));
            _builder.Append("%\nBLOOD: ");
            _builder.Append(Mathf.RoundToInt(manager.Bleeding.BloodVolumeMl));
            _builder.Append(" ml / ");
            _builder.Append(Mathf.RoundToInt(manager.Bleeding.BloodNormalized * 100f));
            _builder.Append("%\nPULSE: ");
            _builder.Append(CalculatePulse(manager));
            _builder.Append(" bpm\nOXYGEN: ");
            _builder.Append(Mathf.RoundToInt(manager.Lungs.OxygenNormalized * 100f));
            _builder.Append("%\nPAIN: ");
            _builder.Append(Mathf.RoundToInt(manager.Pain));
            _builder.Append(" / ");
            _builder.Append(Mathf.RoundToInt(manager.PainSystem.BlackoutPressure * 100f));
            _builder.Append("% shock\nMOVEMENT PENALTY: ");
            _builder.Append(Mathf.RoundToInt(manager.PainSystem.MovementPenalty * 100f));
            _builder.Append("%\nAIM INSTABILITY: ");
            _builder.Append(Mathf.RoundToInt(manager.Fractures.AimInstability * 100f));
            _builder.Append("%");
            return _builder.ToString();
        }

        private string BuildBody(HealthManager manager)
        {
            _builder.Length = 0;
            AppendLimb(manager, BodyPart.Head, "HEAD");
            AppendLimb(manager, BodyPart.Torso, "TORSO");
            AppendLimb(manager, BodyPart.LeftArm, "LEFT ARM");
            AppendLimb(manager, BodyPart.RightArm, "RIGHT ARM");
            AppendLimb(manager, BodyPart.LeftLeg, "LEFT LEG");
            AppendLimb(manager, BodyPart.RightLeg, "RIGHT LEG");
            return _builder.ToString();
        }

        private string BuildOrgans(HealthManager manager)
        {
            _builder.Length = 0;
            AppendOrgan(manager, OrganType.Brain, "BRAIN");
            AppendOrgan(manager, OrganType.Heart, "HEART");
            AppendOrgan(manager, OrganType.Lungs, "LUNGS");
            AppendOrgan(manager, OrganType.Liver, "LIVER");
            AppendOrgan(manager, OrganType.Stomach, "STOMACH");
            AppendOrgan(manager, OrganType.Muscles, "MUSCLES");
            _builder.Append("\nBLOOD PRESSURE: ");
            _builder.Append(GetBloodPressure(manager));
            _builder.Append("\nINTERNAL BLEEDING: ");
            _builder.Append(manager.Bleeding.TotalBleedRateMlPerSecond > 12f ? "ACTIVE" : "NONE");
            return _builder.ToString();
        }

        private string BuildBleeding(HealthManager manager)
        {
            _builder.Length = 0;
            _builder.Append("ACTIVE SOURCES: ");
            _builder.Append(manager.Bleeding.ActiveBleedCount);
            _builder.Append("\nLOSS RATE: ");
            _builder.Append(Mathf.RoundToInt(manager.Bleeding.TotalBleedRateMlPerSecond));
            _builder.Append(" ml/s\nBLOOD VOLUME: ");
            _builder.Append(Mathf.RoundToInt(manager.Bleeding.BloodVolumeMl));
            _builder.Append(" ml\nWORST PART: ");
            _builder.Append(manager.Bleeding.HasActiveBleeding ? manager.Bleeding.GetWorstBleedingPart().ToString() : "NONE");
            _builder.Append("\nBLOOD FX: ");
            _builder.Append(Config.BloodFxEnabled ? "ON" : "OFF");
            _builder.Append("\nNATIVE FX: ");
            _builder.Append(MainMod.Runtime != null && MainMod.Runtime.BloodFx.UsingNativeBlood ? "ACTIVE" : "FALLBACK");
            return _builder.ToString();
        }

        private string BuildFractures(HealthManager manager)
        {
            _builder.Length = 0;
            _builder.Append("BROKEN BONES: ");
            _builder.Append(manager.Bones.TotalBrokenBoneCount);
            _builder.Append("\nRIBS: ");
            _builder.Append(manager.Bones.FracturedRibCount);
            _builder.Append("/12\nBREATHING PENALTY: ");
            _builder.Append(Mathf.RoundToInt(manager.Fractures.BreathingPenalty * 100f));
            _builder.Append("%\nLEFT ARM USAGE: ");
            _builder.Append(Mathf.RoundToInt(manager.Fractures.LeftArmUsage * 100f));
            _builder.Append("%\nRIGHT ARM USAGE: ");
            _builder.Append(Mathf.RoundToInt(manager.Fractures.RightArmUsage * 100f));
            _builder.Append("%\nLEFT LEG USAGE: ");
            _builder.Append(Mathf.RoundToInt(manager.Fractures.LeftLegUsage * 100f));
            _builder.Append("%\nRIGHT LEG USAGE: ");
            _builder.Append(Mathf.RoundToInt(manager.Fractures.RightLegUsage * 100f));
            _builder.Append("%");
            return _builder.ToString();
        }

        private string BuildDebug(HealthManager? manager)
        {
            _builder.Length = 0;
            _builder.Append("DLL: ");
            _builder.Append(Config.ModVersion);
            _builder.Append("\nHUD MODE: ");
            _builder.Append(Config.HudMode);
            _builder.Append("\nBLOOD INTENSITY: ");
            _builder.Append(SettingsMenu.BloodIntensity.ToString("0.0"));
            _builder.Append("\nSCREEN FX: ");
            _builder.Append(Config.ScreenEffectsIntensity.ToString("0.0"));
            _builder.Append("\nAUDIO: ");
            _builder.Append(Config.RealisticAudioEnabled ? "ON" : "OFF");
            if (manager != null)
            {
                _builder.Append("\nEMBEDDED KNIVES: ");
                _builder.Append(manager.KnifePenetration.EmbeddedCount);
                _builder.Append("\nPAIN SHAKE: ");
                _builder.Append(Mathf.RoundToInt(manager.PainSystem.ShakeIntensity * 100f));
                _builder.Append("%");
            }
            return _builder.ToString();
        }

        private void AppendLimb(HealthManager manager, BodyPart part, string label)
        {
            LimbHealth limb = manager.GetLimb(part);
            _builder.Append(label);
            _builder.Append(": ");
            _builder.Append(Mathf.RoundToInt(limb.Hp));
            _builder.Append("/");
            _builder.Append(Mathf.RoundToInt(limb.MaxHp));
            _builder.Append(" HP");
            BleedSeverity bleed = manager.Bleeding.GetWorstBleedingSeverity(part);
            if (bleed != BleedSeverity.None)
            {
                _builder.Append("  BLEED ");
                _builder.Append(bleed);
            }
            if (limb.Fracture != FractureState.None)
            {
                _builder.Append("  ");
                _builder.Append(limb.Fracture);
            }
            _builder.Append('\n');
        }

        private void AppendOrgan(HealthManager manager, OrganType type, string label)
        {
            OrganHealth organ = manager.Organs.GetOrgan(type);
            _builder.Append(label);
            _builder.Append(": ");
            _builder.Append(Mathf.RoundToInt(organ.Integrity));
            _builder.Append("% ");
            _builder.Append(organ.FailureState);
            if (type == OrganType.Heart && manager.Organs.CardiacArrestActive)
                _builder.Append(" CARDIAC ARREST");
            if (type == OrganType.Lungs && manager.Lungs.HasCollapsedLung)
                _builder.Append(" COLLAPSED");
            if (type == OrganType.Brain && manager.Brain.HasActiveConcussion)
                _builder.Append(" CONCUSSION");
            _builder.Append('\n');
        }

        private static void SetValue(StringElement? element, string value)
        {
            if (element == null || element.Value == value)
                return;

            element.Value = value;
            element.OnElementChanged?.Invoke();
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
            return Mathf.Clamp(Mathf.RoundToInt(pulse), 0, 178);
        }

        private static string GetBloodPressure(HealthManager manager)
        {
            if (manager.Organs.CardiacArrestActive)
                return "ARREST";
            if (manager.Bleeding.BloodNormalized < 0.42f || manager.Organs.HeartbeatStrength < 0.35f)
                return "CRITICAL";
            if (manager.Bleeding.BloodNormalized < 0.62f || manager.Organs.HeartbeatStrength < 0.58f)
                return "LOW";
            if (manager.Bleeding.TotalBleedRateMlPerSecond > 18f)
                return "DROPPING";
            return "STABLE";
        }
    }
}
