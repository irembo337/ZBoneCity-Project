using System;
using System.Text;
using BoneLib.BoneMenu;
using MelonLoader;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class BoneMenuIntegration
    {
        private readonly StringBuilder _builder = new StringBuilder(512);
        private Page? _rootPage;
        private StringElement? _runtimeStatus;
        private HealthManager? _manager;
        private float _updateAccumulator;
        private float _retrySilenceSeconds;
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
                _rootPage = Page.Root.CreatePage("ZBoneCity", rootColor, 8, true);
                _rootPage.ElementSpacing = 58f;
                BuildRootPage(_rootPage);
                BuildGameplayPage(_rootPage);
                BuildMedicalPage(_rootPage);
                BuildVitalsPage(_rootPage);
                BuildHudPage(_rootPage);
                BuildBodycamPage(_rootPage);
                BuildAudioPage(_rootPage);
                BuildSettingsPage(_rootPage);
                _initialized = true;
                logger.Msg("BoneMenu ZBoneCity hierarchy created.");
            }
            catch (Exception ex)
            {
                if (_rootPage != null)
                    _initialized = true;

                if (!_warnedUnavailable || _retrySilenceSeconds <= 0f)
                {
                    _warnedUnavailable = true;
                    _retrySilenceSeconds = 8f;
                    string retryText = _rootPage == null ? "will retry" : "partial page kept to avoid duplicate categories";
                    logger.Warning("BoneMenu integration unavailable, " + retryText + ": " + ex.Message);
                }
            }
        }

        public void Update(float deltaTime, HealthManager? manager)
        {
            if (_retrySilenceSeconds > 0f)
                _retrySilenceSeconds = Mathf.Max(0f, _retrySilenceSeconds - deltaTime);

            if (!_initialized)
                return;

            _manager = manager;
            _updateAccumulator += deltaTime;
            if (_updateAccumulator < 1.0f)
                return;

            _updateAccumulator = 0f;
            RefreshRuntimeStatus();
        }

        private void BuildRootPage(Page root)
        {
            Page page = root.CreatePage("Root", new Color(0.70f, 0.10f, 0.08f, 1f), 8, true);
            page.ElementSpacing = 56f;
            BoolElement enabled = page.CreateBool("Enable ZBoneCity", Color.white, Config.Enabled, Config.SetEnabled);
            enabled.SetTooltip("Master switch. When disabled, ZBoneCity gameplay, HUD, bodycam, effects and sounds are shut down.");
            _runtimeStatus = page.CreateString("Runtime", Color.white, "Waiting for player manager...", OnStatusSelected);
            page.CreateFunction("Refresh Status", Color.white, RefreshRuntimeStatus);
            page.CreateFunction("Save Settings", new Color(0.52f, 0.92f, 1f, 1f), SettingsMenu.Save);
        }

        private static void BuildGameplayPage(Page root)
        {
            Page page = root.CreatePage("Gameplay", new Color(0.92f, 0.38f, 0.08f, 1f), 8, true);
            page.ElementSpacing = 54f;
            BoolElement neckTrauma = page.CreateBool("Neck Trauma", Color.white, Config.NeckTraumaEnabled, SettingsMenu.SetNeckTraumaEnabled);
            neckTrauma.SetTooltip("Rare severe neck injuries from high-energy head impacts, falls and neck hits.");
            BoolElement neckSnap = page.CreateBool("Enable Neck Snap", Color.white, Config.NeckSnapEnabled, SettingsMenu.SetNeckSnapEnabled);
            neckSnap.SetTooltip("Physics-based two-hand neck snap. Requires close range, both hands near the neck, and a forceful twist.");
            BoolElement neckPlayers = page.CreateBool("Allow Neck Snap On Players", Color.white, Config.AllowNeckSnapOnPlayers, SettingsMenu.SetAllowNeckSnapOnPlayers);
            neckPlayers.SetTooltip("Only enables player-target attempts when LabFusion/server rules allow it. NPC support remains local-safe.");
            BoolElement neckSound = page.CreateBool("Enable Neck Snap Sound", Color.white, Config.NeckSnapSoundEnabled, SettingsMenu.SetNeckSnapSoundEnabled);
            neckSound.SetTooltip("Play the configured 3D bonerack/neck crack sound once on successful snap.");
            BoolElement dragging = page.CreateBool("Casualty Dragging", Color.white, Config.CasualtyDraggingEnabled, SettingsMenu.SetCasualtyDraggingEnabled);
            dragging.SetTooltip("Enables unconscious casualty drag/carry hooks and Fusion-safe state notifications.");
            BoolElement corpses = page.CreateBool("Persistent Corpses", Color.white, Config.PersistentCorpsesEnabled, SettingsMenu.SetPersistentCorpsesEnabled);
            corpses.SetTooltip("Leaves a physical ragdoll body after player death until the limit or lifetime removes it.");
            IntElement corpseLimit = page.CreateInt("Corpse Limit", Color.white, Config.PersistentCorpseMaxCount, 1, 0, 12, SettingsMenu.SetPersistentCorpseMaxCount);
            corpseLimit.SetTooltip("Maximum player corpses kept on the current map. Oldest bodies are removed first.");
            FloatElement corpseLifetime = page.CreateFloat("Corpse Lifetime", Color.white, Config.PersistentCorpseLifetimeSeconds, 15f, 20f, 900f, SettingsMenu.SetPersistentCorpseLifetimeSeconds);
            corpseLifetime.SetTooltip("Seconds before persistent bodies are cleaned up automatically.");
            BoolElement magazineCheck = page.CreateBool("Magazine Check", Color.white, Config.MagazineCheckEnabled, Config.SetMagazineCheckEnabled);
            magazineCheck.SetTooltip("Approximate Ready Or Not style magazine load checks. No exact round counts.");
            page.CreateFunction("Save Gameplay Settings", new Color(0.52f, 0.92f, 1f, 1f), SettingsMenu.Save);
        }

        private static void BuildMedicalPage(Page root)
        {
            Page page = root.CreatePage("Medical", new Color(0.78f, 0.12f, 0.10f, 1f), 10, true);
            page.ElementSpacing = 54f;
            BoolElement organs = page.CreateBool("Organ System", Color.white, Config.OrganSystemEnabled, SettingsMenu.SetOrganSystem);
            organs.SetTooltip("Brain, heart, lung, liver, stomach and muscle trauma.");
            BoolElement knife = page.CreateBool("Knife Penetration", Color.white, Config.KnifePenetrationEnabled, SettingsMenu.SetKnifePenetration);
            knife.SetTooltip("Embedded knife wound tracking.");
            BoolElement blood = page.CreateBool("Blood FX", Color.white, Config.BloodFxEnabled, SettingsMenu.SetBloodEnabled);
            blood.SetTooltip("Pooled blood decals, drips and visible wound blood.");
            FloatElement bloodIntensity = page.CreateFloat("Blood Intensity", Color.white, SettingsMenu.BloodIntensity, 0.10f, 0f, 2.5f, SettingsMenu.SetBloodIntensity);
            bloodIntensity.SetTooltip("Visible blood FX density.");
            FloatElement bloodFade = page.CreateFloat("Blood Fade Seconds", Color.white, Config.BloodFadeSeconds, 5f, 25f, 360f, SettingsMenu.SetBloodFadeSeconds);
            bloodFade.SetTooltip("How long pooled blood remains before fading.");
            BoolElement nativeBlood = page.CreateBool("Native BONELAB Blood", Color.white, Config.NativeBloodEnabled, SettingsMenu.SetNativeBlood);
            nativeBlood.SetTooltip("Prefer discovered BONELAB native blood particles when available.");
            BoolElement forensics = page.CreateBool("Forensic Scene Traces", Color.white, Config.ForensicsEnabled, SettingsMenu.SetForensicsEnabled);
            forensics.SetTooltip("Leaves bullet holes, blood transfer marks and casing markers after combat.");
            page.CreateFloat("Trace Lifetime", Color.white, Config.ForensicTraceLifetimeSeconds, 15f, 30f, 900f, SettingsMenu.SetForensicTraceLifetimeSeconds);
            page.CreateFloat("Casing Chance", Color.white, Config.ForensicCasingChance, 0.05f, 0f, 1f, SettingsMenu.SetForensicCasingChance);
            BoolElement overdose = page.CreateBool("Medication Side Effects", Color.white, Config.MedicationOverdoseEnabled, Config.SetMedicationOverdoseEnabled);
            overdose.SetTooltip("Dose stacking, overdose risk, stimulant crashes and medicine side effects.");
            BoolElement rehab = page.CreateBool("Rehabilitation", Color.white, Config.RehabilitationEnabled, Config.SetRehabilitationEnabled);
            rehab.SetTooltip("Post-treatment limping, weakness and gradual recovery.");
            page.CreateFunction("Spawn Medkit", new Color(0.95f, 0.18f, 0.14f, 1f), () => MainMod.Runtime?.SpawnMedicalItems());
            page.CreateFunction("Save Medical Settings", new Color(0.52f, 0.92f, 1f, 1f), SettingsMenu.Save);
        }

        private static void BuildVitalsPage(Page root)
        {
            Page page = root.CreatePage("Vitals", new Color(0.26f, 0.56f, 0.24f, 1f), 10, true);
            page.ElementSpacing = 54f;
            BoolElement pain = page.CreateBool("Pain System", Color.white, Config.PainEffectsEnabled, SettingsMenu.SetPainEffects);
            pain.SetTooltip("Pain accumulation, shaking, movement penalties and pain audio.");
            BoolElement unconscious = page.CreateBool("Unconsciousness FX", Color.white, Config.UnconsciousEffectsEnabled, SettingsMenu.SetUnconsciousEffects);
            unconscious.SetTooltip("Blackout, tunnel vision, breathing and recovery effects.");
            FloatElement fractureRealism = page.CreateFloat("Fracture Realism", Color.white, Config.FractureSeverity, 0.10f, 0.25f, 2.5f, SettingsMenu.SetFractureSeverity);
            fractureRealism.SetTooltip("Scales fracture severity and movement penalties.");
            FloatElement screen = page.CreateFloat("Screen FX Intensity", Color.white, Config.ScreenEffectsIntensity, 0.10f, 0f, 2f, SettingsMenu.SetScreenEffectsIntensity);
            screen.SetTooltip("Scales fullscreen pain and unconscious effects.");
            BoolElement painOverlay = page.CreateBool("Fullscreen Pain Overlay", Color.white, Config.PainOverlayEnabled, SettingsMenu.SetPainOverlayEnabled);
            painOverlay.SetTooltip("Immersive full-field pain vignette.");
            FloatElement painOpacity = page.CreateFloat("Pain Overlay Opacity", Color.white, Config.PainOverlayOpacity, 0.05f, 0f, 1f, SettingsMenu.SetPainOverlayOpacity);
            painOpacity.SetTooltip("Maximum opacity for fullscreen pain.");
            FloatElement painPulse = page.CreateFloat("Pain Pulse Intensity", Color.white, Config.PainOverlayPulseIntensity, 0.05f, 0f, 2f, SettingsMenu.SetPainOverlayPulseIntensity);
            painPulse.SetTooltip("Heartbeat pulse strength for high pain.");
            FloatElement painBlood = page.CreateFloat("Pain Blood Opacity", Color.white, Config.PainOverlayBloodOpacity, 0.05f, 0f, 1.5f, SettingsMenu.SetPainOverlayBloodOpacity);
            painBlood.SetTooltip("Blood edge visibility for severe pain and trauma.");
            FloatElement painSpeed = page.CreateFloat("Pain Animation Speed", Color.white, Config.PainOverlayAnimationSpeed, 0.05f, 0.25f, 2.5f, SettingsMenu.SetPainOverlayAnimationSpeed);
            painSpeed.SetTooltip("Pain pulse and blur drift speed.");
        }

        private static void BuildHudPage(Page root)
        {
            Page page = root.CreatePage("HUD", new Color(0.18f, 0.44f, 0.50f, 1f), 14, true);
            page.ElementSpacing = 52f;
            BoolElement enabled = page.CreateBool("Enable HUD", Color.white, Config.HudEnabled, Config.SetHudEnabled);
            enabled.SetTooltip("Master switch for ZBoneCity UI rendering.");
            BoolElement medicalMenu = page.CreateBool("Medical Menu", Color.white, Config.MedicalMenuEnabled, SettingsMenu.SetMedicalMenuEnabled);
            medicalMenu.SetTooltip("Allows opening the medical menu with right thumbstick click.");
            BoolElement miniHud = page.CreateBool("Mini HUD", Color.white, Config.MiniHudEnabled, SettingsMenu.SetMiniHudEnabled);
            miniHud.SetTooltip("Compact Blood/O2/Pain display during gameplay.");
            IntElement layout = page.CreateInt("HUD Layout", Color.white, Config.HudLayout, 1, 0, 2, SettingsMenu.SetHudLayout);
            layout.SetTooltip("0 Compact, 1 Body, 2 Advanced.");
            IntElement position = page.CreateInt("HUD Position", Color.white, Config.HudPosition, 1, 0, 3, SettingsMenu.SetHudPosition);
            position.SetTooltip("0 Left, 1 Right, 2 Center, 3 Custom.");
            IntElement scaleMode = page.CreateInt("HUD Scale", Color.white, Config.HudScaleMode, 1, 0, 3, SettingsMenu.SetHudScaleMode);
            scaleMode.SetTooltip("0 Small, 1 Medium, 2 Large, 3 Custom.");
            page.CreateFloat("HUD Custom Scale", Color.white, Config.HudScaleCustom, 0.05f, 0.4f, 1.8f, SettingsMenu.SetHudScaleCustom);
            page.CreateFloat("HUD Opacity", Color.white, Config.HudOpacity, 0.05f, 0.12f, 1f, SettingsMenu.SetHudOpacity);
            page.CreateFloat("HUD Offset X", Color.white, Config.HudOffsetX, 0.01f, -0.8f, 0.8f, SettingsMenu.SetHudOffsetX);
            page.CreateFloat("HUD Offset Y", Color.white, Config.HudOffsetY, 0.01f, -0.6f, 0.6f, SettingsMenu.SetHudOffsetY);
            page.CreateBool("Show Blood", Color.white, Config.HudShowBlood, SettingsMenu.SetHudShowBlood);
            page.CreateBool("Show Pain", Color.white, Config.HudShowPain, SettingsMenu.SetHudShowPain);
            page.CreateBool("Show Oxygen", Color.white, Config.HudShowOxygen, SettingsMenu.SetHudShowOxygen);
            page.CreateBool("Show Fractures", Color.white, Config.HudShowFractures, SettingsMenu.SetHudShowFractures);
            page.CreateBool("Show Organ Status", Color.white, Config.HudShowOrganStatus, SettingsMenu.SetHudShowOrganStatus);
            page.CreateBool("Show Bleeding", Color.white, Config.HudShowBleeding, SettingsMenu.SetHudShowBleeding);
            page.CreateBool("Show Consciousness", Color.white, Config.HudShowConsciousness, SettingsMenu.SetHudShowConsciousness);
            page.CreateBool("Show Treatment", Color.white, Config.HudShowTreatment, SettingsMenu.SetHudShowTreatment);
            page.CreateBool("Show Vital Signs", Color.white, Config.HudShowVitalSigns, SettingsMenu.SetHudShowVitalSigns);
            page.CreateBool("Show Detailed Stats", Color.white, Config.ShowDetailedStats, SettingsMenu.SetShowDetailedStats);
            page.CreateFloat("Mini HUD Scale", Color.white, Config.MiniHudScale, 0.05f, 0.35f, 1.35f, SettingsMenu.SetMiniHudScale);
            page.CreateFloat("Mini HUD Opacity", Color.white, Config.MiniHudOpacity, 0.05f, 0f, 1f, SettingsMenu.SetMiniHudOpacity);
            page.CreateFloat("Medical Menu Scale", Color.white, Config.MedicalMenuScale, 0.05f, 0.65f, 1.25f, SettingsMenu.SetMedicalMenuScale);
            page.CreateFloat("Medical Menu Opacity", Color.white, Config.MedicalMenuOpacity, 0.05f, 0.45f, 1f, SettingsMenu.SetMedicalMenuOpacity);
            page.CreateFloat("Body Opacity", Color.white, Config.HudBodyOpacity, 0.05f, 0.25f, 1f, SettingsMenu.SetHudBodyOpacity);
            page.CreateInt("Color Theme", Color.white, Config.HudColorTheme, 1, 0, 2, SettingsMenu.SetHudColorTheme);
            page.CreateBool("Disable HUD Completely", Color.white, Config.DisableHudCompletely, SettingsMenu.SetDisableHudCompletely);
            page.CreateFunction("Save HUD Settings", new Color(0.52f, 0.92f, 1f, 1f), SettingsMenu.Save);
        }

        private static void BuildBodycamPage(Page root)
        {
            Page page = root.CreatePage("Bodycam", new Color(1f, 0.84f, 0.18f, 1f), 14, true);
            page.ElementSpacing = 52f;
            BoolElement enabled = page.CreateBool("Enable Bodycam", Color.white, Config.BodycamEnabled, SettingsMenu.SetBodycamEnabled);
            enabled.SetTooltip("Enables the integrated ZBoneCity body camera view.");
            IntElement preset = page.CreateInt("Camera Preset", Color.white, Config.BodycamPreset, 1, 0, 3, SettingsMenu.SetBodycamPreset);
            preset.SetTooltip("0 Tactical, 1 Helmet Wide, 2 Chest Heavy, 3 Night Patrol.");
            page.CreateBool("Helmet Camera", Color.white, Config.BodycamHelmetCamera, SettingsMenu.SetBodycamHelmetCamera);
            page.CreateBool("Chest Camera", Color.white, Config.BodycamChestCamera, SettingsMenu.SetBodycamChestCamera);
            page.CreateFloat("Bodycam FOV", Color.white, Config.BodycamFov, 5f, 60f, 170f, SettingsMenu.SetBodycamFov);
            page.CreateFloat("Forward Offset", Color.white, Config.BodycamForwardOffset, 0.025f, 0f, 1f, SettingsMenu.SetBodycamForwardOffset);
            page.CreateFloat("Camera Smoothing", Color.white, Config.BodycamSmoothing, 0.5f, 0.5f, 20f, SettingsMenu.SetBodycamSmoothing);
            page.CreateBool("Show Cinematic Borders", Color.white, Config.BodycamShowBorders, SettingsMenu.SetBodycamShowBorders);
            page.CreateFloat("Border Size", Color.white, Config.BodycamBorderThickness, 0.01f, 0f, 0.22f, SettingsMenu.SetBodycamBorderThickness);
            page.CreateBool("Enable AXON Overlay", Color.white, Config.BodycamAxonOverlayEnabled, SettingsMenu.SetBodycamAxonOverlayEnabled);
            page.CreateFloat("Overlay Scale", Color.white, Config.BodycamOverlayScale, 0.05f, 0.55f, 1.8f, SettingsMenu.SetBodycamOverlayScale);
            page.CreateFloat("Overlay Opacity", Color.white, Config.BodycamOverlayOpacity, 0.05f, 0f, 1f, SettingsMenu.SetBodycamOverlayOpacity);
            StringElement cameraId = page.CreateString("Camera ID", Color.white, Config.BodycamCameraIdDisplay, SettingsMenu.SetBodycamCameraId);
            cameraId.SetTooltip("Leave empty in preferences/config.js for automatic 6039XXXX generation.");
            page.CreateFunction("Randomize Camera ID", new Color(1f, 0.84f, 0.18f, 1f), SettingsMenu.RandomizeBodycamCameraId);
            page.CreateBool("Enable Beep", Color.white, Config.BodycamBeepEnabled, SettingsMenu.SetBodycamBeepEnabled);
            page.CreateFloat("Beep Volume", Color.white, Config.BodycamBeepVolume, 0.05f, 0f, 1f, SettingsMenu.SetBodycamBeepVolume);
            page.CreateFloat("Overlay Offset X", Color.white, Config.BodycamOverlayOffsetX, 5f, -500f, 500f, SettingsMenu.SetBodycamOverlayOffsetX);
            page.CreateFloat("Overlay Offset Y", Color.white, Config.BodycamOverlayOffsetY, 5f, -300f, 300f, SettingsMenu.SetBodycamOverlayOffsetY);
            page.CreateFloat("Bodycam Shake", Color.white, Config.BodycamShakeIntensity, 0.05f, 0f, 1.5f, SettingsMenu.SetBodycamShakeIntensity);
            page.CreateFloat("Digital Noise", Color.white, Config.BodycamNoiseIntensity, 0.05f, 0f, 1f, SettingsMenu.SetBodycamNoiseIntensity);
            page.CreateFloat("Compression", Color.white, Config.BodycamCompressionIntensity, 0.05f, 0f, 1f, SettingsMenu.SetBodycamCompressionIntensity);
            page.CreateBool("Battery Indicator", Color.white, Config.BodycamBatteryIndicator, SettingsMenu.SetBodycamBatteryIndicator);
        }

        private static void BuildAudioPage(Page root)
        {
            Page page = root.CreatePage("Audio", new Color(0.34f, 0.34f, 0.72f, 1f), 10, true);
            page.ElementSpacing = 54f;
            BoolElement audio = page.CreateBool("Realistic Audio", Color.white, Config.RealisticAudioEnabled, SettingsMenu.SetRealisticAudio);
            audio.SetTooltip("Trauma breathing, heartbeat, ringing and pain audio.");
            BoolElement customSounds = page.CreateBool("Custom Sounds", Color.white, Config.CustomSoundsEnabled, SettingsMenu.SetCustomSoundsEnabled);
            customSounds.SetTooltip("Packaged ZBoneCity sounds from ZBoneCity/Audio.");
            page.CreateFloat("Master Volume", Color.white, Config.MasterVolume, 0.10f, 0f, 2f, SettingsMenu.SetMasterVolume);
            page.CreateFloat("Trauma Audio Intensity", Color.white, Config.AudioIntensity, 0.10f, 0f, 2f, SettingsMenu.SetAudioIntensity);
            page.CreateFloat("Headshot Volume", Color.white, Config.HeadshotSoundVolume, 0.10f, 0f, 2f, SettingsMenu.SetHeadshotSoundVolume);
            page.CreateFloat("Death Volume", Color.white, Config.DeathSoundVolume, 0.10f, 0f, 2f, SettingsMenu.SetDeathSoundVolume);
            page.CreateFloat("Metal Impact Volume", Color.white, Config.MetalImpactSoundVolume, 0.10f, 0f, 2f, SettingsMenu.SetMetalImpactSoundVolume);
            BoolElement gear = page.CreateBool("Enable Gear Sounds", Color.white, Config.GearSoundsEnabled, SettingsMenu.SetGearSoundsEnabled);
            gear.SetTooltip("Sequential Gear1-Gear6 movement sounds from the body while walking or running.");
            page.CreateFloat("Gear Volume", Color.white, Config.GearVolume, 0.05f, 0f, 2f, SettingsMenu.SetGearVolume);
            page.CreateFloat("Gear Distance", Color.white, Config.GearDistance, 1f, 3f, 35f, SettingsMenu.SetGearDistance);
            page.CreateFloat("Gear Sound Speed Multiplier", Color.white, Config.GearSoundSpeedMultiplier, 0.05f, 0.35f, 2.5f, SettingsMenu.SetGearSoundSpeedMultiplier);
            page.CreateFunction("Save Audio Settings", new Color(0.52f, 0.92f, 1f, 1f), SettingsMenu.Save);
        }

        private static void BuildSettingsPage(Page root)
        {
            Page page = root.CreatePage("Settings", new Color(0.16f, 0.42f, 0.84f, 1f), 10, true);
            page.ElementSpacing = 54f;
            BoolElement debug = page.CreateBool("Debug Mode", Color.white, Config.DebugMode, SettingsMenu.SetDebugMode);
            debug.SetTooltip("Extra ZBoneCity diagnostics in MelonLoader logs.");
            page.CreateFunction("Save All Settings", new Color(0.52f, 0.92f, 1f, 1f), SettingsMenu.Save);
        }

        private void RefreshRuntimeStatus()
        {
            StringElement? status = _runtimeStatus;
            if (status == null)
                return;

            string value = BuildRuntimeStatus(_manager);
            if (status.Value == value)
                return;

            status.Value = value;
        }

        private string BuildRuntimeStatus(HealthManager? manager)
        {
            _builder.Length = 0;
            _builder.Append("ZBoneCity: ");
            _builder.Append(Config.Enabled ? "ENABLED" : "DISABLED");
            _builder.Append("\nVersion: ");
            _builder.Append(Config.ModVersion);
            _builder.Append("\nMenu: Root / Medical / Vitals / HUD / Bodycam / Audio / Settings");
            _builder.Append("\nDebug: ");
            _builder.Append(Config.DebugMode ? "ON" : "OFF");

            if (!Config.Enabled)
            {
                _builder.Append("\nRuntime systems: OFF");
                return _builder.ToString();
            }

            if (manager == null)
            {
                _builder.Append("\nPlayer manager: waiting");
                return _builder.ToString();
            }

            _builder.Append("\nState: ");
            _builder.Append(manager.Consciousness.State);
            _builder.Append("\nBlood: ");
            _builder.Append(Mathf.RoundToInt(manager.Bleeding.BloodVolumeMl));
            _builder.Append(" ml");
            _builder.Append("\nPain: ");
            _builder.Append(Mathf.RoundToInt(manager.Pain));
            _builder.Append("\nPulse/BP: ");
            _builder.Append(Mathf.RoundToInt(manager.PulseBpm));
            _builder.Append(" bpm ");
            _builder.Append(Mathf.RoundToInt(manager.BloodPressureSystolic));
            _builder.Append("/");
            _builder.Append(Mathf.RoundToInt(manager.BloodPressureDiastolic));
            _builder.Append("\nBleeds: ");
            _builder.Append(manager.Bleeding.ActiveBleedCount);
            _builder.Append("\nStress: ");
            _builder.Append(Mathf.RoundToInt(manager.Stress.Normalized * 100f));
            _builder.Append("%");
            _builder.Append("\nBroken bones: ");
            _builder.Append(manager.Bones.TotalBrokenBoneCount);
            return _builder.ToString();
        }

        private static void OnStatusSelected(string value)
        {
            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("ZBoneCity BoneMenu status selected, text length: " + value.Length);
        }
    }
}
