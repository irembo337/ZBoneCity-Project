using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Combat;
using Il2CppSLZ.Marrow.Data;
using MelonLoader;
using UnityEngine;
using ZBoneCity.Medical;
using PlayerDamageReceiver = Il2CppSLZ.Marrow.PlayerDamageReceiver;
using PlayerHealth = Il2CppSLZ.Marrow.Health;

[assembly: MelonInfo(typeof(BonelabAdvancedHealth.MainMod), BonelabAdvancedHealth.Config.ModName, BonelabAdvancedHealth.Config.ModVersion, "naxock")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace BonelabAdvancedHealth
{
    public sealed class MainMod : MelonMod
    {
        private readonly Dictionary<int, NPCHealth> _npcHealth = new Dictionary<int, NPCHealth>(128);
        private readonly MedicalSystem _medical = new MedicalSystem();
        private readonly MedicalItemRuntimeConfigurator _medicalItemRuntimeConfigurator = new MedicalItemRuntimeConfigurator();
        private readonly SpawnManagerFix _spawnFix = new SpawnManagerFix();
        private float _tickAccumulator;
        private float _playerProbeAccumulator;
        private float _sceneLifetime;
        private float _unconsciousRagdollSettleSeconds;
        private float _manualRagdollCollisionGraceSeconds;
        private float _lastSpawnResetRealtime = -100f;
        private int _lastSpawnResetFrame = -1;
        private bool _playerControlsSuppressed;
        private int _internalRagdollRequestDepth;
        private bool _hudSubsystemEnabled = true;
        private bool _painEffectSubsystemEnabled = true;
        private bool _thoughtSubsystemEnabled = true;
        private bool _bloodFxSubsystemEnabled = true;
        private bool _boneMenuSubsystemEnabled = true;
        private bool _fusionSubsystemEnabled = true;
        private bool _medicalSubsystemEnabled = true;
        private bool _bodycamSubsystemEnabled = true;
        private bool _forensicSubsystemEnabled = true;
        private bool _deathSoundSubsystemEnabled = true;
        private bool _gearSoundSubsystemEnabled = true;
        private bool _casualtyDragSubsystemEnabled = true;
        private bool _neckGrabSubsystemEnabled = true;
        private bool _magazineCheckSubsystemEnabled = true;
        private bool _magSlideSubsystemEnabled = true;
        private bool _persistentCorpseSubsystemEnabled = true;
        private bool _externalCompatSubsystemEnabled = true;
        private bool _luaBridgeSubsystemEnabled = true;
        private bool _harmonyPatchesEnabled = true;
        private HealthManager? _player;

        public static MainMod? Runtime { get; private set; }
        public HUDSystem Hud { get; } = new HUDSystem();
        public PainEffectManager PainEffects { get; } = new PainEffectManager();
        public ThoughtUI ThoughtUi { get; } = new ThoughtUI();
        public BloodFXSystem BloodFx { get; } = new BloodFXSystem();
        public ImpactAudioSystem ImpactAudio { get; } = new ImpactAudioSystem();
        public RandomDeathSoundSystem DeathSounds { get; } = new RandomDeathSoundSystem();
        public GearSoundSystem GearSounds { get; } = new GearSoundSystem();
        public BodycamSystem Bodycam { get; } = new BodycamSystem();
        public ForensicSystem Forensics { get; } = new ForensicSystem();
        public CasualtyDragSystem CasualtyDrag { get; } = new CasualtyDragSystem();
        public NeckGrabSystem NeckGrab { get; } = new NeckGrabSystem();
        public NeckSnapAudioSystem NeckSnapAudio { get; } = new NeckSnapAudioSystem();
        public MagazineCheckSystem MagazineCheck { get; } = new MagazineCheckSystem();
        public PersistentCorpseSystem PersistentCorpses { get; } = new PersistentCorpseSystem();
        public VRHealthMenu HealthMenu { get; } = new VRHealthMenu();
        public FusionCompatibilitySystem Fusion { get; } = new FusionCompatibilitySystem();
        public ExternalModCompatibilitySystem ExternalMods { get; } = new ExternalModCompatibilitySystem();
        public LuaBridgeSystem LuaBridge { get; } = new LuaBridgeSystem();
        public MelonLogger.Instance Logger => LoggerInstance;
        public HealthManager? PlayerManager => _player;

        public override void OnInitializeMelon()
        {
            Runtime = this;
            LoggerInstance.Msg("[ZBC] Mod Loading");
            LoggerInstance.Msg("[ZBC] Runtime platform: " + PlatformCompatibility.RuntimeLabel);
            RunStartupStep("Medical runtime types", ZBCMedicalRuntimeTypeRegistration.Register);
            RunStartupStep("Config", Config.Load);
            LoggerInstance.Msg("[ZBC] Assets Loaded");
            LoggerInstance.Msg("[ZBC] Health System Loaded");
            _hudSubsystemEnabled = RunStartupStep("HUD", () => LoggerInstance.Msg("[ZBC] HUD deferred until player rig is ready"));
            _bodycamSubsystemEnabled = RunStartupStep("Bodycam", () => LoggerInstance.Msg("[ZBC] Bodycam integrated into ZBoneCity runtime"));
            _boneMenuSubsystemEnabled = RunStartupStep("BoneMenu", () => HealthMenu.Initialize(LoggerInstance));
            _externalCompatSubsystemEnabled = RunStartupStep("External mod compatibility", () =>
            {
                if (!PlatformCompatibility.SupportsExternalDllScanning)
                {
                    LoggerInstance.Msg("[ZBC] External DLL scan skipped on Android.");
                    return;
                }

                ExternalMods.Initialize();
            });
            _fusionSubsystemEnabled = RunStartupStep("Fusion", Fusion.Initialize);
            _luaBridgeSubsystemEnabled = RunStartupStep("Lua bridge", () => LuaBridge.Initialize(LoggerInstance));
            _harmonyPatchesEnabled = RunStartupStep("Harmony patches", () =>
            {
                if (!PlatformCompatibility.SupportsMelonHarmonyRuntime)
                {
                    LoggerInstance.Warning("[ZBC] Harmony runtime patches skipped on Android. Quest gameplay code requires a compatible Android code-loader.");
                    return;
                }

                HarmonyInstance.PatchAll();
            });
            _medicalSubsystemEnabled = RunStartupStep("Medical System", () => LoggerInstance.Msg("[ZBC] Medical System deferred until gameplay is ready"));
            _magSlideSubsystemEnabled = RunStartupStep("MagSlide", () => MagSlideIntegration.Initialize(HarmonyInstance, LoggerInstance));
            _deathSoundSubsystemEnabled = RunStartupStep("Random Death Sounds", DeathSounds.Initialize);
            _gearSoundSubsystemEnabled = RunStartupStep("Gear Sounds", GearSounds.Initialize);
            LoggerInstance.Msg("[ZBC] Audio Loaded");
            LoggerInstance.Msg("[ZBC] Startup Complete");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (Config.DebugMode)
                LoggerInstance.Msg("[ZBC] Scene initialized: " + sceneName + " (" + buildIndex + ")");
            try
            {
                ResetRuntimeForScene();
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("[ZBC ERROR] Scene reset failed safely: " + ex);
            }
        }

        public override void OnUpdate()
        {
            try
            {
                UpdateRuntime();
            }
            catch (Exception ex)
            {
                _tickAccumulator = 0f;
                LoggerInstance.Warning("[ZBC ERROR] Main update failed safely: " + ex);
            }
        }

        public override void OnLateUpdate()
        {
            try
            {
                if (Config.Enabled && _bodycamSubsystemEnabled)
                    _bodycamSubsystemEnabled = TryRunSubsystem("Bodycam late update", _bodycamSubsystemEnabled, () => Bodycam.LateUpdate(Time.deltaTime));
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("[ZBC ERROR] Bodycam late update failed safely: " + ex.Message);
            }
        }

        private void UpdateRuntime()
        {
            float deltaTime = Time.deltaTime;
            if (!Config.Enabled)
            {
                _boneMenuSubsystemEnabled = TryRunSubsystem("BoneMenu update", _boneMenuSubsystemEnabled, () => HealthMenu.Update(deltaTime, _player));
                return;
            }

            _sceneLifetime += deltaTime;
            if (_manualRagdollCollisionGraceSeconds > 0f)
                _manualRagdollCollisionGraceSeconds = Math.Max(0f, _manualRagdollCollisionGraceSeconds - deltaTime);
            if (Config.HudEnabled && !_hudSubsystemEnabled)
                _hudSubsystemEnabled = true;
            if (Config.PainEffectsEnabled && !_painEffectSubsystemEnabled)
                _painEffectSubsystemEnabled = true;
            _spawnFix.Update(deltaTime, this, _player);
            if (_player == null)
                TryCreatePlayerManagerForHud(deltaTime);
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion update", _fusionSubsystemEnabled, () => Fusion.Update(deltaTime));
            _luaBridgeSubsystemEnabled = TryRunSubsystem("Lua bridge update", _luaBridgeSubsystemEnabled, () => LuaBridge.Update(deltaTime, _player));
            _boneMenuSubsystemEnabled = TryRunSubsystem("BoneMenu update", _boneMenuSubsystemEnabled, () => HealthMenu.Update(deltaTime, _player));
            _bodycamSubsystemEnabled = TryRunSubsystem("Bodycam update", _bodycamSubsystemEnabled, () => Bodycam.Update(deltaTime));
            _casualtyDragSubsystemEnabled = TryRunSubsystem("Casualty drag update", _casualtyDragSubsystemEnabled, () => CasualtyDrag.Update(deltaTime));
            _neckGrabSubsystemEnabled = TryRunSubsystem("Neck grab update", _neckGrabSubsystemEnabled, () => NeckGrab.Update(deltaTime, _npcHealth.Values));
            _magazineCheckSubsystemEnabled = TryRunSubsystem("Magazine check update", _magazineCheckSubsystemEnabled, () => MagazineCheck.Update(deltaTime, _player));
            _magSlideSubsystemEnabled = TryRunSubsystem("MagSlide update", _magSlideSubsystemEnabled, MagSlideIntegration.Update);
            _persistentCorpseSubsystemEnabled = TryRunSubsystem("Persistent corpse update", _persistentCorpseSubsystemEnabled, () => PersistentCorpses.Update(deltaTime));

            if (_player == null && _npcHealth.Count == 0)
            {
                _tickAccumulator = 0f;
                return;
            }

            if (_player != null && !IsPlayerRigReady())
            {
                _tickAccumulator = 0f;
                return;
            }

            if (_player != null && !CanRunPlayerTrauma)
            {
                _tickAccumulator = 0f;
                _painEffectSubsystemEnabled = TryRunSubsystem("Pain overlay realtime during spawn protection", _painEffectSubsystemEnabled, () => PainEffects.Update(deltaTime, _player));
                _hudSubsystemEnabled = TryRunSubsystem("HUD realtime during spawn protection", _hudSubsystemEnabled, () => Hud.UpdateRealtime(deltaTime, _player));
                return;
            }

            _tickAccumulator += deltaTime;
            if (_player != null)
            {
                _player.UpdateRecoveryInput(deltaTime);
                MaintainUnconsciousPlayerRagdoll(deltaTime);
                _gearSoundSubsystemEnabled = TryRunSubsystem("Gear sound update", _gearSoundSubsystemEnabled, () => GearSounds.Update(deltaTime, _player));
                _painEffectSubsystemEnabled = TryRunSubsystem("Pain overlay realtime", _painEffectSubsystemEnabled, () => PainEffects.Update(deltaTime, _player));
                _hudSubsystemEnabled = TryRunSubsystem("HUD realtime", _hudSubsystemEnabled, () => Hud.UpdateRealtime(deltaTime, _player));
                _medicalSubsystemEnabled = TryRunSubsystem("Medical pickup runtime setup", _medicalSubsystemEnabled, () => _medicalItemRuntimeConfigurator.Update(deltaTime));
                _medicalSubsystemEnabled = TryRunSubsystem("Medical update", _medicalSubsystemEnabled, () => _medical.Update(deltaTime, _player));
            }

            if (_tickAccumulator < Config.SystemTickInterval)
                return;

            float elapsed = _tickAccumulator;
            _tickAccumulator = 0f;
            HealthManager? player = _player;
            if (player != null)
            {
                player.UpdateSystems(elapsed);
                _hudSubsystemEnabled = TryRunSubsystem("HUD status update", _hudSubsystemEnabled, () => Hud.UpdateHud(player));
                _thoughtSubsystemEnabled = TryRunSubsystem("Thought UI update", _thoughtSubsystemEnabled, () => ThoughtUi.Update(elapsed, player));
            }

            _bloodFxSubsystemEnabled = TryRunSubsystem("Blood FX update", _bloodFxSubsystemEnabled, () => BloodFx.Update(elapsed, player, _npcHealth.Values));
            _forensicSubsystemEnabled = TryRunSubsystem("Forensics update", _forensicSubsystemEnabled, () => Forensics.Update(elapsed));

            foreach (NPCHealth npc in _npcHealth.Values)
            {
                try
                {
                    npc.SyncFromGameHealth();
                    npc.UpdateSystems(elapsed);
                    npc.ApplyNpcRuntimeEffects(elapsed);
                }
                catch (Exception ex)
                {
                    LoggerInstance.Warning("[ZBoneCity] NPC health update failed safely: " + ex.Message);
                }
            }
        }

        public override void OnGUI()
        {
            try
            {
                Bodycam.RenderGui();
            }
            catch (Exception ex)
            {
                if (Config.DebugMode)
                    LoggerInstance.Warning("[ZBC ERROR] Bodycam GUI failed safely: " + ex.Message);
            }
        }

        public override void OnApplicationQuit()
        {
            TryCleanup("Medical cleanup", () => _medical.ClearWorldItems());
            TryCleanup("Medical pickup runtime cleanup", () => _medicalItemRuntimeConfigurator.Reset());
            TryCleanup("HUD cleanup", () => Hud.Destroy());
            TryCleanup("Pain overlay cleanup", () => PainEffects.Destroy());
            TryCleanup("Thought UI cleanup", () => ThoughtUi.Destroy());
            TryCleanup("Blood FX cleanup", () => BloodFx.Reset());
            TryCleanup("Forensics cleanup", () => Forensics.Reset());
            TryCleanup("Casualty drag cleanup", () => CasualtyDrag.Reset());
            TryCleanup("Neck grab cleanup", () => NeckGrab.Reset());
            TryCleanup("Neck snap audio cleanup", () => NeckSnapAudio.Destroy());
            TryCleanup("Random death sound cleanup", () => DeathSounds.Destroy());
            TryCleanup("Gear sound cleanup", () => GearSounds.Destroy());
            TryCleanup("Magazine check cleanup", () => MagazineCheck.Reset());
            TryCleanup("MagSlide cleanup", MagSlideIntegration.CleanupForLevelChange);
            TryCleanup("Persistent corpse cleanup", () => PersistentCorpses.ClearAll());
            TryCleanup("Impact audio cleanup", () => ImpactAudio.Destroy());
            TryCleanup("Bodycam cleanup", () => Bodycam.Destroy());
            TryCleanup("Control cleanup", () => SetPlayerControlSuppressed(false));
            if (_player != null)
            {
                TryCleanup("Consciousness cleanup", () => _player.Consciousness.Destroy());
                TryCleanup("Audio trauma cleanup", () => _player.AudioTrauma.Destroy());
            }
            Runtime = null;
        }

        public void ApplyMasterEnabledChanged(bool enabled)
        {
            if (enabled)
            {
                LoggerInstance.Msg("[ZBC] ZBoneCity enabled");
                return;
            }

            LoggerInstance.Msg("[ZBC] ZBoneCity disabled; restoring vanilla-safe runtime state");
            _tickAccumulator = 0f;
            _playerProbeAccumulator = 0f;
            TryCleanup("Medical cleanup", () => _medical.ClearWorldItems());
            TryCleanup("Medical pickup runtime cleanup", () => _medicalItemRuntimeConfigurator.Reset());
            TryCleanup("HUD cleanup", () => Hud.Destroy());
            TryCleanup("Pain overlay cleanup", () => PainEffects.Destroy());
            TryCleanup("Thought UI cleanup", () => ThoughtUi.Destroy());
            TryCleanup("Blood FX cleanup", () => BloodFx.Reset());
            TryCleanup("Forensics cleanup", () => Forensics.Reset());
            TryCleanup("Casualty drag cleanup", () => CasualtyDrag.Reset());
            TryCleanup("Neck grab cleanup", () => NeckGrab.Reset());
            TryCleanup("Neck snap audio cleanup", () => NeckSnapAudio.Destroy());
            TryCleanup("Random death sound cleanup", () => DeathSounds.Destroy());
            TryCleanup("Gear sound cleanup", () => GearSounds.Destroy());
            TryCleanup("Magazine check cleanup", () => MagazineCheck.Reset());
            TryCleanup("MagSlide cleanup", MagSlideIntegration.CleanupForLevelChange);
            TryCleanup("Persistent corpse cleanup", () => PersistentCorpses.ClearAll());
            TryCleanup("Impact audio cleanup", () => ImpactAudio.Destroy());
            TryCleanup("Bodycam cleanup", () => Bodycam.Destroy());
            TryCleanup("Control cleanup", () => SetPlayerControlSuppressed(false));
            TryCleanup("Ragdoll cleanup", () => SetPlayerRagdoll(false));
            if (_player != null)
            {
                TryCleanup("Player medical state reset", () => _player.ResetForRespawn());
                TryCleanup("Consciousness cleanup", () => _player.Consciousness.Destroy());
                TryCleanup("Audio trauma cleanup", () => _player.AudioTrauma.Destroy());
            }

            _npcHealth.Clear();
        }

        public HealthManager GetOrCreatePlayerManager()
        {
            if (_player == null)
            {
                _player = new HealthManager(HealthOwnerKind.Player, 0);
                _player.ForceSafeSpawnState();
                LoggerInstance.Msg("Player advanced health manager created.");
            }

            return _player;
        }

        private void TryCreatePlayerManagerForHud(float deltaTime)
        {
            if (_sceneLifetime < 2.0f || !_spawnFix.CanInitializeHealth)
                return;

            _playerProbeAccumulator += deltaTime;
            if (_playerProbeAccumulator < 0.5f)
                return;

            _playerProbeAccumulator = 0f;
            if (!IsPlayerRigReady())
                return;

            HealthManager manager = GetOrCreatePlayerManager();
            _painEffectSubsystemEnabled = TryRunSubsystem("Pain overlay probe create", _painEffectSubsystemEnabled, () => PainEffects.Update(deltaTime, manager));
            if (!Config.HudEnabled)
                return;

            _hudSubsystemEnabled = TryRunSubsystem("HUD probe create", _hudSubsystemEnabled, () =>
            {
                Hud.EnsureCreated();
                Hud.UpdateHud(manager);
            });
        }

        public NPCHealth? GetOrCreateNpcManager(Enemy_Health enemyHealth)
        {
            if (!Config.NpcEnabled || enemyHealth == null)
                return null;

            int id = enemyHealth.GetInstanceID();
            if (_npcHealth.TryGetValue(id, out NPCHealth? existing))
                return existing;

            NPCHealth created = new NPCHealth(enemyHealth);
            _npcHealth.Add(id, created);
            return created;
        }

        public void ResetNpc(Enemy_Health enemyHealth)
        {
            if (enemyHealth == null)
                return;

            int id = enemyHealth.GetInstanceID();
            if (_npcHealth.TryGetValue(id, out NPCHealth? existing))
                existing.Reset();
        }

        public void ApplyPlayerLimbUsage(FractureSystem fractures)
        {
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                if (refs == null || !refs.HasRefs)
                    return;

                RigManager? rigManager = refs.PlayerRigManager;
                float recoveryUsage = _player != null ? _player.Consciousness.RecoveryUsageMultiplier : 1f;
                if (rigManager != null && rigManager.health != null)
                {
                    if (IsPlayerInForcedRagdollState())
                    {
                        rigManager.health.SetUsage(0f, 0f, 0f, 0f, 0f, 0f);
                    }
                    else if (_playerControlsSuppressed)
                    {
                        rigManager.health.SetUsage(0.08f, 0.08f, 0.05f, 0.05f, 0.05f, 0.05f);
                    }
                    else
                    {
                        float staminaEfficiency = _player != null ? _player.WeaponHandling.StaminaEfficiency : 1f;
                        float armEfficiency = _player != null ? (1f - _player.WeaponHandling.ReloadPenalty * 0.22f) : 1f;
                        float neckEfficiency = _player != null ? (1f - _player.Neck.MovementPenalty) : 1f;
                        float headControlEfficiency = _player != null ? (1f - _player.Neck.HeadControlPenalty * 0.18f) : 1f;
                        float rehabMovement = _player != null ? (1f - _player.Rehabilitation.MovementPenalty) : 1f;
                        rigManager.health.SetUsage(
                            Mathf.Clamp(fractures.HipsUsage * recoveryUsage * staminaEfficiency * neckEfficiency * rehabMovement, 0.20f, 1f),
                            Mathf.Clamp(fractures.SpineUsage * recoveryUsage * staminaEfficiency * neckEfficiency * rehabMovement, 0.18f, 1f),
                            Mathf.Clamp(fractures.LeftLegUsage * recoveryUsage * staminaEfficiency * neckEfficiency * (_player?.Rehabilitation.GetLimbUsageMultiplier(BodyPart.LeftLeg) ?? 1f), 0.16f, 1f),
                            Mathf.Clamp(fractures.RightLegUsage * recoveryUsage * staminaEfficiency * neckEfficiency * (_player?.Rehabilitation.GetLimbUsageMultiplier(BodyPart.RightLeg) ?? 1f), 0.16f, 1f),
                            Mathf.Clamp(fractures.LeftArmUsage * recoveryUsage * armEfficiency * headControlEfficiency * (_player?.Rehabilitation.GetLimbUsageMultiplier(BodyPart.LeftArm) ?? 1f), 0.10f, 1f),
                            Mathf.Clamp(fractures.RightArmUsage * recoveryUsage * armEfficiency * headControlEfficiency * (_player?.Rehabilitation.GetLimbUsageMultiplier(BodyPart.RightArm) ?? 1f), 0.10f, 1f));
                    }
                }

                PhysicsRig? physicsRig = refs.PlayerPhysicsRig;
                if (physicsRig == null)
                    return;

                if (_playerControlsSuppressed || IsPlayerInForcedRagdollState())
                {
                    ApplyHandSuppression(physicsRig.leftHand, true, false);
                    ApplyHandSuppression(physicsRig.rightHand, true, false);
                    return;
                }

                float leftGrip = _player != null ? _player.WeaponHandling.LeftGripMultiplier : 1f;
                float rightGrip = _player != null ? _player.WeaponHandling.RightGripMultiplier : 1f;
                ApplyHandGrip(physicsRig.leftHand, fractures.GetGripStrength(BodyPart.LeftArm) * recoveryUsage * leftGrip);
                ApplyHandGrip(physicsRig.rightHand, fractures.GetGripStrength(BodyPart.RightArm) * recoveryUsage * rightGrip);
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Failed to apply player limb usage: " + ex.Message);
            }
        }

        public void SetPlayerControlSuppressed(bool suppressed)
        {
            if (_playerControlsSuppressed == suppressed && !suppressed)
                return;

            _playerControlsSuppressed = suppressed;
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                if (physicsRig == null)
                    physicsRig = UnityEngine.Object.FindObjectOfType<PhysicsRig>();
                if (physicsRig == null)
                    return;

                ApplyHandSuppression(physicsRig.leftHand, suppressed, true);
                ApplyHandSuppression(physicsRig.rightHand, suppressed, true);
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Failed to suppress player controls: " + ex.Message);
            }
        }

        public void SetPlayerRagdoll(bool ragdoll)
        {
            SetPlayerRagdollInternal(ragdoll, false);
        }

        public void ForceClearPlayerRagdollForSpawn()
        {
            SetPlayerRagdollInternal(false, true);
            PlayerRigFix.ZeroPlayerRigVelocity();
            PlayerRigFix.StabilizePlayerPhysics(this, true);
        }

        private void SetPlayerRagdollInternal(bool ragdoll, bool forceSpawnClear)
        {
            try
            {
                if (!ragdoll && !forceSpawnClear && ShouldBlockExternalUnragdoll())
                {
                    if (Config.DebugMode)
                        LoggerInstance.Msg("[ZBC] Blocked unragdoll while player is unconscious.");
                    SetPlayerControlSuppressed(true);
                    return;
                }

                if (!forceSpawnClear && !RagdollController.CanApplyPlayerRagdoll(ragdoll))
                {
                    if (Config.DebugMode)
                        LoggerInstance.Msg("Blocked player ragdoll during spawn protection: " + _spawnFix.Reason);
                    return;
                }

                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                if (physicsRig == null)
                    physicsRig = UnityEngine.Object.FindObjectOfType<PhysicsRig>();
                if (physicsRig == null)
                    return;

                if (ragdoll)
                {
                    _unconsciousRagdollSettleSeconds = Math.Max(_unconsciousRagdollSettleSeconds, 1.25f);
                    PlayerRigFix.StabilizePlayerPhysics(this, false);
                    _internalRagdollRequestDepth++;
                    try
                    {
                        physicsRig.RagdollRig();
                        PlayerRigFix.StabilizeUnconsciousRagdoll(this, 0.65f);
                    }
                    finally
                    {
                        _internalRagdollRequestDepth = Math.Max(0, _internalRagdollRequestDepth - 1);
                    }
                }
                else
                {
                    _manualRagdollCollisionGraceSeconds = 0f;
                    _unconsciousRagdollSettleSeconds = 0f;
                    if (forceSpawnClear)
                        SetPlayerControlSuppressed(false);
                    _internalRagdollRequestDepth++;
                    try
                    {
                        PlayerRigFix.ZeroPlayerRigVelocity();
                        physicsRig.UnRagdollRig();
                        PlayerRigFix.ZeroPlayerRigVelocity();
                    }
                    finally
                    {
                        _internalRagdollRequestDepth = Math.Max(0, _internalRagdollRequestDepth - 1);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Failed to toggle player ragdoll: " + ex.Message);
            }
        }

        public void KillPlayer(DeathCause cause)
        {
            try
            {
                PlayerHealth health = UnityEngine.Object.FindObjectOfType<PlayerHealth>();
                if (health != null && health.alive)
                {
                    health.Death();
                    return;
                }

                Player_Health legacyHealth = UnityEngine.Object.FindObjectOfType<Player_Health>();
                if (legacyHealth != null)
                    legacyHealth.Death();
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Failed to execute player death for " + cause + ": " + ex.Message);
            }
        }

        public Transform? GetHeadTransform()
        {
            try
            {
                PlayerRefs refs = PlayerRefs.Instance;
                if (refs != null && refs.HasRefs && refs.OpenControllerRig != null && refs.OpenControllerRig.headset != null)
                    return refs.OpenControllerRig.headset;
            }
            catch (Exception)
            {
                return Camera.main != null ? Camera.main.transform : null;
            }

            return Camera.main != null ? Camera.main.transform : null;
        }

        public void SpawnMedicalItems()
        {
            if (!Config.Enabled)
                return;

            _medical.SpawnDefaultItemsAtPlayer();
        }

        public void NotifyHudDamageVisual(DamageInfo info, OrganDamageFeedback feedback)
        {
            _hudSubsystemEnabled = TryRunSubsystem("HUD damage visual", _hudSubsystemEnabled, () => Hud.OnDamageVisual(info, feedback));
        }

        public void NotifyHudMedicalFeedback(string message)
        {
            _hudSubsystemEnabled = TryRunSubsystem("HUD medical feedback", _hudSubsystemEnabled, () => Hud.ShowMedicalFeedback(message));
        }

        public void NotifyConsciousnessEffects(float blackout, float pain, ConsciousnessState state)
        {
            _hudSubsystemEnabled = TryRunSubsystem("HUD consciousness effects", _hudSubsystemEnabled, () => Hud.SetConsciousnessEffects(blackout, pain, state));
        }

        public void NotifyBloodDamage(HealthManager manager, DamageInfo info, OrganDamageFeedback feedback)
        {
            _bloodFxSubsystemEnabled = TryRunSubsystem("Blood FX damage", _bloodFxSubsystemEnabled, () => BloodFx.OnDamage(manager, info, feedback));
            if (manager.Kind == HealthOwnerKind.Player || IsNearPlayer(info.Origin, 1.35f))
            {
                float lensIntensity = Config.Clamp(info.Damage / 120f + (feedback.BleedSeverity >= BleedSeverity.Severe ? 0.28f : 0f), 0f, 1f);
                if (lensIntensity > 0.12f)
                    _bodycamSubsystemEnabled = TryRunSubsystem("Bodycam lens blood", _bodycamSubsystemEnabled, () => Bodycam.AddLensBlood(lensIntensity));
            }
        }

        public bool ShouldBlockExternalUnragdoll()
        {
            HealthManager? player = _player;
            if (player == null || !Config.Enabled)
                return false;

            return player.Consciousness.State == ConsciousnessState.Unconscious ||
                   player.Consciousness.State == ConsciousnessState.Dead ||
                   player.Coma.IsActive;
        }

        public bool IsInternalRagdollRequest => _internalRagdollRequestDepth > 0;

        public void MarkExternalManualRagdoll()
        {
            if (!Config.Enabled || IsPlayerInForcedRagdollState())
                return;

            _manualRagdollCollisionGraceSeconds = 999999f;
            if (Config.DebugMode)
                LoggerInstance.Msg("[ZBC] External manual ragdoll detected; collision trauma suppressed until unragdoll.");
        }

        public void ClearExternalManualRagdoll()
        {
            if (_manualRagdollCollisionGraceSeconds <= 0f)
                return;

            _manualRagdollCollisionGraceSeconds = 0f;
            if (Config.DebugMode)
                LoggerInstance.Msg("[ZBC] External manual ragdoll cleared.");
        }

        public bool ShouldIgnorePlayerCollisionTrauma()
        {
            return _manualRagdollCollisionGraceSeconds > 0f && !IsPlayerInForcedRagdollState();
        }

        private bool IsPlayerInForcedRagdollState()
        {
            HealthManager? player = _player;
            if (player == null || !Config.Enabled)
                return false;

            return player.Consciousness.State == ConsciousnessState.Unconscious ||
                   player.Consciousness.State == ConsciousnessState.Dead ||
                   player.Coma.IsActive;
        }

        private void MaintainUnconsciousPlayerRagdoll(float deltaTime)
        {
            if (!IsPlayerInForcedRagdollState())
            {
                _unconsciousRagdollSettleSeconds = 0f;
                return;
            }

            SetPlayerControlSuppressed(true);
            if (_player != null)
                ApplyPlayerLimbUsage(_player.Fractures);

            float settleStrength = _unconsciousRagdollSettleSeconds > 0f ? 1f : 0.35f;
            PlayerRigFix.StabilizeUnconsciousRagdoll(this, settleStrength);
            _unconsciousRagdollSettleSeconds = Math.Max(0f, _unconsciousRagdollSettleSeconds - deltaTime);
        }

        public void NotifyMedicalBloodContact(HealthManager manager, MedicalItemType itemType, BodyPart part)
        {
            _bloodFxSubsystemEnabled = TryRunSubsystem("Blood FX medical contact", _bloodFxSubsystemEnabled, () => BloodFx.OnMedicalTreatment(manager, itemType, part));
        }

        public void NotifyForensicDamage(HealthManager manager, DamageInfo info, OrganDamageFeedback feedback)
        {
            _forensicSubsystemEnabled = TryRunSubsystem("Forensic damage", _forensicSubsystemEnabled, () => Forensics.OnDamage(manager, info, feedback));
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion forensic notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalForensics(manager, info, feedback));
        }

        public void NotifyForensicDeath(HealthManager manager, DeathCause cause)
        {
            _forensicSubsystemEnabled = TryRunSubsystem("Forensic death", _forensicSubsystemEnabled, () => Forensics.OnDeath(manager, cause));
            NotifyWitnessedDeath(manager);
        }

        public void NotifyDeathSound(HealthManager manager, DeathCause cause)
        {
            _deathSoundSubsystemEnabled = TryRunSubsystem("Random death sound", _deathSoundSubsystemEnabled, () => DeathSounds.PlayForDeath(manager, cause));
        }

        public void NotifyKnifeBloodRemoved(DamageInfo info, BleedSeverity severity)
        {
            _bloodFxSubsystemEnabled = TryRunSubsystem("Blood FX knife removal", _bloodFxSubsystemEnabled, () => BloodFx.OnKnifeRemoved(info, severity));
        }

        public void NotifyFusionDamage(HealthManager manager, DamageInfo info)
        {
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion damage notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalDamage(manager, info));
            _luaBridgeSubsystemEnabled = TryRunSubsystem("Lua damage notify", _luaBridgeSubsystemEnabled, () => LuaBridge.NotifyDamage(manager, info));
        }

        public void NotifyFusionDeath(HealthManager manager, DeathCause cause)
        {
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion death notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalDeath(manager, cause));
        }

        public void NotifyFusionDeathSound(HealthManager manager, DeathCause cause, int soundIndex, Vector3 position)
        {
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion death sound notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalDeathSound(manager, cause, soundIndex, position));
        }

        public void NotifyFusionGearSound(int soundIndex, Vector3 position, float speed01)
        {
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion gear sound notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalGearSound(soundIndex, position, speed01));
        }

        public void NotifyFusionMedical(HealthManager manager, MedicalItemType itemType)
        {
            NotifyFusionMedical(manager, itemType, BodyPart.Torso);
        }

        public void NotifyFusionMedical(HealthManager manager, MedicalItemType itemType, BodyPart part)
        {
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion medical notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalMedical(manager, itemType, part));
            _luaBridgeSubsystemEnabled = TryRunSubsystem("Lua medical notify", _luaBridgeSubsystemEnabled, () => LuaBridge.NotifyTreatment(manager, itemType, part));
        }

        public void NotifyFusionCasualtyDrag(HealthManager manager, bool dragging)
        {
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion casualty drag notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalCasualtyDrag(manager, dragging));
        }

        public void NotifyFusionNeckTrauma(HealthManager manager, NeckInjuryState state, NeckTraumaCause cause)
        {
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion neck trauma notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalNeckTrauma(manager, state, cause));
        }

        public void NotifyFusionMedication(HealthManager manager, MedicalItemType itemType, float overdoseRisk)
        {
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion medication notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalMedication(manager, itemType, overdoseRisk));
        }

        public void NotifyFusionRehabilitation(HealthManager manager, BodyPart part, float recoverySeverity)
        {
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion rehabilitation notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalRehabilitation(manager, part, recoverySeverity));
        }

        public void NotifyFusionMagazineCheck(string estimate)
        {
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion magazine check notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalMagazineCheck(estimate));
        }

        public void NotifyPersistentCorpseDeath(HealthManager manager, DeathCause cause)
        {
            _persistentCorpseSubsystemEnabled = TryRunSubsystem("Persistent corpse capture", _persistentCorpseSubsystemEnabled, () => PersistentCorpses.CapturePlayerCorpse(manager, cause));
        }

        public void NotifyFusionPersistentCorpse(HealthManager manager, DeathCause cause, Vector3 position, int corpseId)
        {
            _fusionSubsystemEnabled = TryRunSubsystem("Fusion persistent corpse notify", _fusionSubsystemEnabled, () => Fusion.NotifyLocalPersistentCorpse(manager, cause, position, corpseId));
        }

        public void NotifyNearbyGunfire(Vector3 position, float rawDamage)
        {
            if (!Config.Enabled)
                return;

            float baseIntensity = Config.Clamp(rawDamage / 85f + 0.18f, 0.12f, 1.0f);
            if (_player != null && !_player.IsDead)
            {
                float distanceFactor = GetDistanceFactorToPlayer(position, 18f);
                if (distanceFactor > 0f)
                    _player.Stress.RegisterNearbyGunfire(baseIntensity * distanceFactor);
            }

            foreach (NPCHealth npc in _npcHealth.Values)
            {
                if (npc == null || npc.IsDead)
                    continue;

                Vector3 npcPosition = npc.GetEffectPosition();
                float distance = Vector3.Distance(position, npcPosition);
                if (distance > 16f)
                    continue;

                npc.Stress.RegisterNearbyGunfire(baseIntensity * Config.Clamp(1f - distance / 16f, 0f, 1f));
            }
        }

        private void NotifyWitnessedDeath(HealthManager deadManager)
        {
            Vector3 deathPosition = GetManagerPosition(deadManager);
            if (deathPosition.sqrMagnitude < 0.001f)
                return;

            if (_player != null && _player != deadManager && !_player.IsDead)
            {
                float distanceFactor = GetDistanceFactorToPlayer(deathPosition, 22f);
                if (distanceFactor > 0f)
                    _player.Stress.RegisterWitnessedDeath(distanceFactor);
            }

            foreach (NPCHealth npc in _npcHealth.Values)
            {
                if (npc == null || npc == deadManager || npc.IsDead)
                    continue;

                float distance = Vector3.Distance(deathPosition, npc.GetEffectPosition());
                if (distance <= 18f)
                    npc.Stress.RegisterWitnessedDeath(Config.Clamp(1f - distance / 18f, 0f, 1f));
            }
        }

        private bool IsNearPlayer(Vector3 position, float maxDistance)
        {
            return GetDistanceFactorToPlayer(position, maxDistance) > 0f;
        }

        private float GetDistanceFactorToPlayer(Vector3 position, float maxDistance)
        {
            if (position.sqrMagnitude < 0.001f)
                return 0f;

            Transform? head = GetHeadTransform();
            if (head == null)
                return 0f;

            float distance = Vector3.Distance(position, head.position);
            return distance <= maxDistance ? Config.Clamp(1f - distance / maxDistance, 0f, 1f) : 0f;
        }

        private Vector3 GetManagerPosition(HealthManager manager)
        {
            if (manager is NPCHealth npc)
                return npc.GetEffectPosition();
            if (manager.Kind == HealthOwnerKind.Player)
            {
                Transform? head = GetHeadTransform();
                if (head != null)
                    return head.position;
            }

            return Vector3.zero;
        }

        public bool IsPlayerRigReady()
        {
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                return refs != null &&
                       refs.HasRefs &&
                       refs.PlayerRigManager != null &&
                       refs.PlayerPhysicsRig != null &&
                       refs.OpenControllerRig != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool IsSpawnProtected => _spawnFix.IsProtected;
        public bool CanProcessPlayerDamage => _spawnFix.CanProcessDamage;
        public bool CanRunPlayerTrauma => _spawnFix.CanRunTraumaSystems;
        public bool CanApplyPlayerRagdoll => _spawnFix.CanRunTraumaSystems;

        public bool TryGetPlayerFeetPosition(out Vector3 feet)
        {
            feet = Vector3.zero;
            try
            {
                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                if (physicsRig != null && physicsRig.rbFeet != null)
                {
                    feet = physicsRig.rbFeet.position;
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }

            Transform? head = GetHeadTransform();
            if (head == null)
                return false;
            feet = head.position + Vector3.down * 1.25f;
            return true;
        }

        private void ResetRuntimeForScene()
        {
            _tickAccumulator = 0f;
            _playerProbeAccumulator = 0f;
            _sceneLifetime = 0f;
            _lastSpawnResetRealtime = -100f;
            _lastSpawnResetFrame = -1;
            _hudSubsystemEnabled = true;
            _painEffectSubsystemEnabled = true;
            _thoughtSubsystemEnabled = true;
            _persistentCorpseSubsystemEnabled = true;
            SetPlayerControlSuppressed(false);
            if (_player != null)
            {
                _player.Consciousness.Destroy();
                _player.AudioTrauma.Destroy();
                _player = null;
            }

            _npcHealth.Clear();
            TryCleanup("HUD scene cleanup", () => Hud.Destroy());
            TryCleanup("Pain overlay scene cleanup", () => PainEffects.Destroy());
            TryCleanup("Thought UI scene cleanup", () => ThoughtUi.Destroy());
            TryCleanup("Blood FX scene cleanup", () => BloodFx.Reset());
            TryCleanup("Forensics scene cleanup", () => Forensics.Reset());
            TryCleanup("Casualty drag scene cleanup", () => CasualtyDrag.Reset());
            TryCleanup("Neck grab scene cleanup", () => NeckGrab.Reset());
            TryCleanup("Random death sound scene reset", () => DeathSounds.Reset());
            TryCleanup("Gear sound scene reset", () => GearSounds.Reset());
            TryCleanup("Magazine check scene cleanup", () => MagazineCheck.Reset());
            TryCleanup("MagSlide scene cleanup", MagSlideIntegration.CleanupForLevelChange);
            TryCleanup("Persistent corpse scene cleanup", () => PersistentCorpses.ClearAll());
            TryCleanup("Impact audio scene reset", () => ImpactAudio.Reset());
            TryCleanup("Bodycam scene cleanup", () => Bodycam.Destroy());
            TryCleanup("External audio scene cache cleanup", ExternalAudioClipLoader.ForgetAll);
            _luaBridgeSubsystemEnabled = TryRunSubsystem("Lua bridge scene reset", _luaBridgeSubsystemEnabled, LuaBridge.OnSceneLoaded);
            _spawnFix.OnSceneLoaded();
            TryCleanup("Medical pickup runtime scene reset", () => _medicalItemRuntimeConfigurator.Reset());
            _medicalSubsystemEnabled = TryRunSubsystem("Medical scene reset", _medicalSubsystemEnabled, () => _medical.OnSceneLoaded());
        }

        private void ResetPlayerForSpawn(bool force = false)
        {
            try
            {
                float now = Time.realtimeSinceStartup;
                int frame = Time.frameCount;
                if (!force && (frame == _lastSpawnResetFrame || now - _lastSpawnResetRealtime < 0.35f))
                {
                    if (Config.DebugMode)
                        LoggerInstance.Msg("Skipped duplicate spawn reset.");
                    return;
                }

                _lastSpawnResetRealtime = now;
                _lastSpawnResetFrame = frame;
                _hudSubsystemEnabled = true;
                _painEffectSubsystemEnabled = true;
                _thoughtSubsystemEnabled = true;
                _player?.ResetForRespawn();
                _spawnFix.OnSceneLoaded();
                _medicalItemRuntimeConfigurator.Reset();
                _medicalSubsystemEnabled = TryRunSubsystem("Medical respawn reset", _medicalSubsystemEnabled, () => _medical.OnSceneLoaded());
                TryCleanup("HUD respawn cleanup", () => Hud.Destroy());
                TryCleanup("Pain overlay respawn cleanup", () => PainEffects.Destroy());
                TryCleanup("Thought UI respawn cleanup", () => ThoughtUi.Destroy());
                TryCleanup("Blood FX respawn cleanup", () => BloodFx.Reset());
                TryCleanup("Casualty drag respawn cleanup", () => CasualtyDrag.Reset());
                TryCleanup("Neck grab respawn cleanup", () => NeckGrab.Reset());
                TryCleanup("Random death sound respawn reset", () => DeathSounds.Reset());
                TryCleanup("Gear sound respawn reset", () => GearSounds.Reset());
                TryCleanup("Magazine check respawn cleanup", () => MagazineCheck.Reset());
                TryCleanup("MagSlide respawn cleanup", MagSlideIntegration.CleanupForLevelChange);
                TryCleanup("Impact audio respawn reset", () => ImpactAudio.Reset());
                TryCleanup("Bodycam respawn reset", () => Bodycam.Reset());
                SetPlayerControlSuppressed(false);
                ForceClearPlayerRagdollForSpawn();
                if (IsPlayerRigReady())
                {
                    PlayerRigFix.ResetSpawnRagdoll(this);
                    PlayerRigFix.StabilizePlayerPhysics(this, true);
                    PlayerRigFix.TryLiftPlayerAboveFloor(this, 0.08f, 0.9f);
                    PlayerRigFix.ZeroPlayerRigVelocity();
                    if (_player != null)
                        ApplyPlayerLimbUsage(_player.Fractures);
                    _hudSubsystemEnabled = TryRunSubsystem("HUD respawn update", _hudSubsystemEnabled, () => Hud.UpdateHud(GetOrCreatePlayerManager()));
                }
                LoggerInstance.Msg("[ZBC] Respawn Reset Complete");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("[ZBC ERROR] Player spawn reset failed safely: " + ex.Message);
            }
        }

        private bool RunStartupStep(string stepName, Action action)
        {
            LoggerInstance.Msg("[ZBC] Loading " + stepName + "...");
            try
            {
                action();
                LoggerInstance.Msg("[ZBC] " + stepName + " Loaded");
                return true;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("[ZBC ERROR] " + stepName + " startup failed safely: " + ex);
                return false;
            }
        }

        private bool TryRunSubsystem(string name, bool enabled, Action action)
        {
            if (!enabled)
                return false;

            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("[ZBC ERROR] " + name + " failed; subsystem disabled for this run: " + ex);
                return false;
            }
        }

        private void TryCleanup(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("[ZBC ERROR] " + name + " cleanup failed safely: " + ex.Message);
            }
        }

        private static void ApplyHandGrip(Hand hand, float strength)
        {
            if (hand == null)
                return;

            strength = Config.Clamp(strength, 0.02f, 1f);
            hand.SetGripStrength(strength);
            if (hand.physHand != null)
                hand.physHand.gripMult = strength;
        }

        private static void ApplyHandSuppression(Hand hand, bool suppressed, bool forceRelease)
        {
            if (hand == null)
                return;

            try
            {
                if (suppressed && forceRelease && hand.HasAttachedObject())
                    hand.DetachObject();
            }
            catch (Exception)
            {
            }

            try
            {
                hand.GrabLock = suppressed;
                hand.hoverLocked = suppressed;
                if (suppressed)
                    hand.HoverLock();
                else
                    hand.HoverUnlock();
            }
            catch (Exception)
            {
            }

            try
            {
                hand.SetGripStrength(suppressed ? 0f : 1f);
                if (hand.physHand != null)
                    hand.physHand.gripMult = suppressed ? 0f : 1f;
            }
            catch (Exception)
            {
            }
        }

        [HarmonyPatch(typeof(PlayerDamageReceiver), nameof(PlayerDamageReceiver.ReceiveAttack))]
        private static class PlayerAttackPatch
        {
            private static void Postfix(PlayerDamageReceiver __instance, Attack attack)
            {
                try
                {
                    if (!Config.Enabled || Runtime == null || __instance == null)
                        return;
                    if (!Runtime.IsPlayerRigReady())
                        return;
                    if (!Runtime.CanProcessPlayerDamage)
                    {
                        if (Config.DebugMode)
                            Runtime.Logger.Msg("Ignored player attack during spawn protection: " + Runtime._spawnFix.Reason);
                        return;
                    }

                    HealthManager manager = Runtime.GetOrCreatePlayerManager();
                    DamageInfo info = DamageProcessor.FromPlayerAttack(attack, __instance.bodyPart);
                    manager.ApplyDamage(info);
                }
                catch (Exception ex)
                {
                    Runtime?.Logger.Warning("Player attack health patch failed: " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(ImpactProperties), nameof(ImpactProperties.ReceiveAttack))]
        private static class ImpactPropertiesAudioPatch
        {
            private static void Postfix(ImpactProperties __instance, Attack attack)
            {
                try
                {
                    if (!Config.Enabled || Runtime == null || __instance == null || attack == null)
                        return;

                    if (attack.attackType == AttackType.Piercing)
                    {
                        Vector3 impactPosition = __instance.transform != null
                            ? __instance.transform.position
                            : DamageProcessor.SanitizeVector(attack.origin, Vector3.zero);
                        Runtime.NotifyNearbyGunfire(impactPosition, DamageProcessor.SanitizeFloat(attack.damage));
                    }

                    Runtime.ImpactAudio.TryPlayMetalImpact(__instance, attack);
                }
                catch (Exception ex)
                {
                    if (Config.DebugMode)
                        Runtime?.Logger.Warning("Impact audio patch failed safely: " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(Il2CppSLZ.Marrow.Interaction.MarrowEntity), "OnCullResolve")]
        private static class MedicalMarrowEntityCullGuardPatch
        {
            private static Exception? Finalizer(Il2CppSLZ.Marrow.Interaction.MarrowEntity __instance, Exception __exception)
            {
                if (__exception == null)
                    return null;

                if (IsZBoneCityMedicalObject(__instance))
                {
                    Runtime?.Logger.Warning("[ZBC ERROR] Suppressed malformed medical MarrowEntity cull error: " + __exception.Message);
                    return null;
                }

                return __exception;
            }
        }

        [HarmonyPatch(typeof(PlayerDamageReceiver), nameof(PlayerDamageReceiver.OnCollisionEnter))]
        private static class PlayerCollisionPatch
        {
            private static void Postfix(PlayerDamageReceiver __instance, Collision collision)
            {
                try
                {
                    if (!Config.Enabled || Runtime == null || __instance == null || collision == null)
                        return;
                    Vector3 relativeVelocity = collision.relativeVelocity;
                    if (float.IsNaN(relativeVelocity.x) || float.IsNaN(relativeVelocity.y) || float.IsNaN(relativeVelocity.z) ||
                        float.IsInfinity(relativeVelocity.x) || float.IsInfinity(relativeVelocity.y) || float.IsInfinity(relativeVelocity.z))
                        return;
                    if (relativeVelocity.sqrMagnitude < 81f)
                        return;
                    if (!Runtime.IsPlayerRigReady())
                        return;
                    if (relativeVelocity.sqrMagnitude > 144f)
                        PlayerRigFix.StabilizePlayerPhysics(Runtime, false);
                    if (Runtime.ShouldIgnorePlayerCollisionTrauma())
                    {
                        if (Config.DebugMode)
                            Runtime.Logger.Msg("Ignored player collision from external manual ragdoll.");
                        return;
                    }
                    if (!Runtime.CanProcessPlayerDamage)
                    {
                        if (Config.DebugMode)
                            Runtime.Logger.Msg("Ignored player collision during spawn protection: " + Runtime._spawnFix.Reason);
                        return;
                    }

                    DamageInfo info = DamageProcessor.FromPlayerCollision(collision, __instance.bodyPart);
                    Runtime.GetOrCreatePlayerManager().ApplyDamage(info);
                }
                catch (Exception ex)
                {
                    Runtime?.Logger.Warning("Player collision health patch failed: " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(PhysicsRig), nameof(PhysicsRig.RagdollRig))]
        private static class ExternalManualRagdollPatch
        {
            private static void Prefix()
            {
                try
                {
                    if (Runtime == null || Runtime.IsInternalRagdollRequest)
                        return;
                    if (Runtime.ShouldBlockExternalUnragdoll())
                        return;

                    Runtime.MarkExternalManualRagdoll();
                }
                catch (Exception ex)
                {
                    Runtime?.Logger.Warning("[ZBC ERROR] External ragdoll compatibility failed safely: " + ex.Message);
                }
            }
        }

        private static bool IsZBoneCityMedicalObject(Component component)
        {
            try
            {
                if (component == null)
                    return false;

                Transform current = component.transform;
                for (int depth = 0; current != null && depth < 8; depth++)
                {
                    string name = current.name ?? string.Empty;
                    if (name.IndexOf("ZBC_Medical_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("AHS_Medical_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("ZBC_ItemType_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("AHS_ItemType_", StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;

                    for (int i = 0; i < current.childCount; i++)
                    {
                        Transform child = current.GetChild(i);
                        if (child == null)
                            continue;

                        string childName = child.name ?? string.Empty;
                        if (childName.IndexOf("ZBC_ItemType_", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            childName.IndexOf("AHS_ItemType_", StringComparison.OrdinalIgnoreCase) >= 0)
                            return true;
                    }

                    current = current.parent;
                }
            }
            catch (Exception)
            {
            }

            return false;
        }

        [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.SetFullHealth))]
        private static class PlayerHealthResetPatch
        {
            private static void Postfix()
            {
                Runtime?.ResetPlayerForSpawn(false);
            }
        }

        [HarmonyPatch(typeof(PhysicsRig), nameof(PhysicsRig.UnRagdollRig))]
        private static class ToggleRagdollCompatibilityPatch
        {
            private static bool Prefix()
            {
                try
                {
                    if (Runtime == null || !Runtime.ShouldBlockExternalUnragdoll())
                    {
                        Runtime?.ClearExternalManualRagdoll();
                        return true;
                    }

                    Runtime.SetPlayerControlSuppressed(true);
                    if (Config.DebugMode)
                        Runtime.Logger.Msg("[ZBC] Toggle Ragdoll unragdoll blocked by unconscious state.");
                    return false;
                }
                catch (Exception ex)
                {
                    Runtime?.Logger.Warning("[ZBC ERROR] Toggle Ragdoll compatibility failed safely: " + ex.Message);
                    return true;
                }
            }
        }

        [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Respawn))]
        private static class PlayerRespawnPatch
        {
            private static void Prefix()
            {
                Runtime?.ResetPlayerForSpawn(false);
            }

            private static void Postfix()
            {
                Runtime?.ResetPlayerForSpawn(true);
            }
        }

        [HarmonyPatch(typeof(Player_Health), nameof(Player_Health.SetFullHealth))]
        private static class LegacyPlayerHealthResetPatch
        {
            private static void Postfix()
            {
                Runtime?.ResetPlayerForSpawn(false);
            }
        }

        [HarmonyPatch(typeof(EnemyDamageReceiver), nameof(EnemyDamageReceiver.ReceiveAttack))]
        private static class NpcAttackPatch
        {
            private static void Postfix(EnemyDamageReceiver __instance, Attack attack)
            {
                try
                {
                    if (!Config.Enabled || !Config.NpcEnabled || Runtime == null || __instance == null)
                        return;

                    Enemy_Health enemyHealth = __instance.e_health;
                    if (enemyHealth == null)
                        enemyHealth = __instance.GetComponentInParent<Enemy_Health>();
                    if (enemyHealth == null)
                        return;

                    NPCHealth? manager = Runtime.GetOrCreateNpcManager(enemyHealth);
                    if (manager == null)
                        return;

                    DamageInfo info = DamageProcessor.FromNpcAttack(attack, __instance.bodyPart);
                    if (manager.ApplyDamage(info))
                        manager.TryExecuteIfCritical(info);
                }
                catch (Exception ex)
                {
                    Runtime?.Logger.Warning("NPC attack health patch failed: " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(Enemy_Health), nameof(Enemy_Health.OnReceivedCollison))]
        private static class NpcCollisionPatch
        {
            private static void Postfix(Enemy_Health __instance, Collision collison, float relVelocitySqr, EnemyCollisonRelay.BodyPart part, bool isStay)
            {
                try
                {
                    if (!Config.Enabled || !Config.NpcEnabled || Runtime == null || __instance == null || collison == null || isStay)
                        return;

                    if (relVelocitySqr < 36f)
                        return;

                    NPCHealth? manager = Runtime.GetOrCreateNpcManager(__instance);
                    if (manager == null)
                        return;

                    DamageInfo info = DamageProcessor.FromNpcCollision(collison, part);
                    if (manager.ApplyDamage(info))
                        manager.TryExecuteIfCritical(info);
                }
                catch (Exception ex)
                {
                    Runtime?.Logger.Warning("NPC collision health patch failed: " + ex.Message);
                }
            }
        }

        [HarmonyPatch(typeof(Enemy_Health), nameof(Enemy_Health.SPAWNSTART))]
        private static class NpcSpawnResetPatch
        {
            private static void Postfix(Enemy_Health __instance)
            {
                Runtime?.ResetNpc(__instance);
            }
        }
    }
}
