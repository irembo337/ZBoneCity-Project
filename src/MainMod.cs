using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppSLZ.Bonelab;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Combat;
using MelonLoader;
using UnityEngine;
using PlayerDamageReceiver = Il2CppSLZ.Marrow.PlayerDamageReceiver;
using PlayerHealth = Il2CppSLZ.Marrow.Health;

[assembly: MelonInfo(typeof(BonelabAdvancedHealth.MainMod), BonelabAdvancedHealth.Config.ModName, BonelabAdvancedHealth.Config.ModVersion, "Codex")]
[assembly: MelonGame("Stress Level Zero", "BONELAB")]

namespace BonelabAdvancedHealth
{
    public sealed class MainMod : MelonMod
    {
        private readonly Dictionary<int, NPCHealth> _npcHealth = new Dictionary<int, NPCHealth>(128);
        private readonly MedicalSystem _medical = new MedicalSystem();
        private readonly PlayerControlLockSystem _controlLock = new PlayerControlLockSystem();
        private float _tickAccumulator;
        private HealthManager? _player;

        public static MainMod? Runtime { get; private set; }
        public HUDSystem Hud { get; } = new HUDSystem();
        public ThoughtUI ThoughtUi { get; } = new ThoughtUI();
        public BloodFXSystem BloodFx { get; } = new BloodFXSystem();
        public MelonLogger.Instance Logger => LoggerInstance;
        public bool ArePlayerHandsLocked => _controlLock.IsLocked;

        public override void OnInitializeMelon()
        {
            Runtime = this;
            Config.Load();
            HarmonyInstance.PatchAll();
            LoggerInstance.Msg("Initialized with BONELAB runtime references and Harmony patches.");
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg("Scene initialized: " + sceneName + " (" + buildIndex + ")");
            ResetRuntimeForScene();
        }

        public override void OnUpdate()
        {
            if (!Config.Enabled)
                return;

            float deltaTime = Time.deltaTime;
            _tickAccumulator += deltaTime;
            _controlLock.Enforce(deltaTime);
            _medical.Update(deltaTime, _player);

            if (_tickAccumulator < Config.SystemTickInterval)
                return;

            float elapsed = _tickAccumulator;
            _tickAccumulator = 0f;
            HealthManager? player = GetOrCreatePlayerManager();
            player.UpdateSystems(elapsed);
            Hud.UpdateHud(player);
            ThoughtUi.Update(elapsed, player);
            BloodFx.Update(elapsed, player, _npcHealth.Values);

            foreach (NPCHealth npc in _npcHealth.Values)
            {
                npc.SyncFromGameHealth();
                npc.UpdateSystems(elapsed);
                npc.ApplyNpcRuntimeEffects(elapsed);
            }
        }

        public override void OnApplicationQuit()
        {
            _medical.ClearWorldItems();
            Hud.Destroy();
            ThoughtUi.Destroy();
            BloodFx.Reset();
            _controlLock.Reset();
            if (_player != null)
                _player.Consciousness.Destroy();
            Runtime = null;
        }

        public HealthManager GetOrCreatePlayerManager()
        {
            if (_player == null)
            {
                _player = new HealthManager(HealthOwnerKind.Player, 0);
                LoggerInstance.Msg("Player advanced health manager created.");
            }

            return _player;
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
                if (rigManager != null && rigManager.health != null)
                {
                    rigManager.health.SetUsage(
                        fractures.HipsUsage,
                        fractures.SpineUsage,
                        fractures.LeftLegUsage,
                        fractures.RightLegUsage,
                        fractures.LeftArmUsage,
                        fractures.RightArmUsage);
                }

                PhysicsRig? physicsRig = refs.PlayerPhysicsRig;
                if (physicsRig == null)
                    return;

                ApplyHandGrip(physicsRig.leftHand, fractures.GetGripStrength(BodyPart.LeftArm));
                ApplyHandGrip(physicsRig.rightHand, fractures.GetGripStrength(BodyPart.RightArm));
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Failed to apply player limb usage: " + ex.Message);
            }
        }

        public void SetPlayerRagdoll(bool ragdoll)
        {
            try
            {
                if (ragdoll)
                    _controlLock.SetLocked(true);

                PlayerRefs? refs = PlayerRefs.Instance;
                PhysicsRig? physicsRig = refs != null && refs.HasRefs ? refs.PlayerPhysicsRig : null;
                if (physicsRig == null)
                    physicsRig = UnityEngine.Object.FindObjectOfType<PhysicsRig>();
                if (physicsRig == null)
                    return;

                if (ragdoll)
                {
                    physicsRig.RagdollRig();
                }
                else
                {
                    physicsRig.UnRagdollRig();
                    _controlLock.SetLocked(false);
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
            _medical.SpawnDefaultItemsAtPlayer();
        }

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

        public bool IsPlayerPhysHand(PhysHand physHand)
        {
            return _controlLock.IsPlayerPhysHand(physHand);
        }

        public void SuppressController(BaseController controller)
        {
            _controlLock.SuppressController(controller);
        }

        private void ResetRuntimeForScene()
        {
            _tickAccumulator = 0f;
            if (_player != null)
                _player.Reset();
            _npcHealth.Clear();
            Hud.Destroy();
            ThoughtUi.Destroy();
            BloodFx.Reset();
            _controlLock.Reset();
            _medical.OnSceneLoaded();
        }

        private static void ApplyHandGrip(Hand hand, float strength)
        {
            if (hand == null)
                return;

            if (Runtime != null && Runtime.ArePlayerHandsLocked)
                strength = 0f;
            else
                strength = Config.Clamp(strength, 0.12f, 1f);
            hand.SetGripStrength(strength);
            if (hand.physHand != null)
                hand.physHand.gripMult = strength;
        }

        private static bool ShouldSuppressController(BaseController controller)
        {
            if (Runtime == null || !Runtime.ArePlayerHandsLocked)
                return false;
            Runtime.SuppressController(controller);
            return true;
        }

        private static bool ShouldSuppressPlayerPhysHand(PhysHand physHand)
        {
            return Runtime != null && Runtime.ArePlayerHandsLocked && Runtime.IsPlayerPhysHand(physHand);
        }

        [HarmonyPatch(typeof(PlayerDamageReceiver), nameof(PlayerDamageReceiver.ReceiveAttack))]
        private static class PlayerAttackPatch
        {
            private static void Postfix(PlayerDamageReceiver __instance, Attack attack)
            {
                if (!Config.Enabled || Runtime == null || __instance == null)
                    return;

                HealthManager manager = Runtime.GetOrCreatePlayerManager();
                DamageInfo info = DamageProcessor.FromPlayerAttack(attack, __instance.bodyPart);
                manager.ApplyDamage(info);
            }
        }

        [HarmonyPatch]
        private static class SuppressHandPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Hand), nameof(Hand.AttachObject));
                yield return AccessTools.Method(typeof(Hand), nameof(Hand.UpdateHovering));
                yield return AccessTools.Method(typeof(Hand), nameof(Hand.EarlyUpdateHeldObjectInputs));
                yield return AccessTools.Method(typeof(Hand), nameof(Hand.OnPhysRigEarlyUpdate));
                yield return AccessTools.Method(typeof(Hand), nameof(Hand.OnPhysRigUpdate));
            }

            private static bool Prefix(Hand __instance)
            {
                if (Runtime == null || !Runtime.ArePlayerHandsLocked)
                    return true;

                if (__instance != null && __instance.physHand != null && Runtime.IsPlayerPhysHand(__instance.physHand))
                    return false;

                return true;
            }
        }

        [HarmonyPatch]
        private static class SuppressGripPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(Grip), nameof(Grip.OnGrabConfirm));
                yield return AccessTools.Method(typeof(Grip), nameof(Grip.Snatch));
            }

            private static bool Prefix(Hand hand)
            {
                if (Runtime == null || !Runtime.ArePlayerHandsLocked)
                    return true;

                return hand == null || hand.physHand == null || !Runtime.IsPlayerPhysHand(hand.physHand);
            }
        }

        [HarmonyPatch]
        private static class SuppressPhysHandPatch
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(PhysHand), nameof(PhysHand.EarlyUpdateArm));
                yield return AccessTools.Method(typeof(PhysHand), nameof(PhysHand.UpdateArmTargets));
                yield return AccessTools.Method(typeof(PhysHand), nameof(PhysHand.UpdateArmDrives));
                yield return AccessTools.Method(typeof(PhysHand), nameof(PhysHand.UpdateArmSupportDrives));
                yield return AccessTools.Method(typeof(PhysHand), nameof(PhysHand.FixedUpdateArm));
                yield return AccessTools.Method(typeof(PhysHand), nameof(PhysHand.ApplyForce));
            }

            private static bool Prefix(PhysHand __instance)
            {
                return !ShouldSuppressPlayerPhysHand(__instance);
            }
        }

        [HarmonyPatch(typeof(PlayerDamageReceiver), nameof(PlayerDamageReceiver.OnCollisionEnter))]
        private static class PlayerCollisionPatch
        {
            private static void Postfix(PlayerDamageReceiver __instance, Collision collision)
            {
                if (!Config.Enabled || Runtime == null || __instance == null || collision == null)
                    return;

                DamageInfo info = DamageProcessor.FromPlayerCollision(collision, __instance.bodyPart);
                Runtime.GetOrCreatePlayerManager().ApplyDamage(info);
            }
        }

        [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.SetFullHealth))]
        private static class PlayerHealthResetPatch
        {
            private static void Postfix()
            {
                Runtime?._player?.Reset();
            }
        }

        [HarmonyPatch(typeof(PlayerHealth), nameof(PlayerHealth.Respawn))]
        private static class PlayerRespawnPatch
        {
            private static void Postfix()
            {
                Runtime?._player?.Reset();
            }
        }

        [HarmonyPatch(typeof(Player_Health), nameof(Player_Health.SetFullHealth))]
        private static class LegacyPlayerHealthResetPatch
        {
            private static void Postfix()
            {
                Runtime?._player?.Reset();
            }
        }

        [HarmonyPatch(typeof(EnemyDamageReceiver), nameof(EnemyDamageReceiver.ReceiveAttack))]
        private static class NpcAttackPatch
        {
            private static void Postfix(EnemyDamageReceiver __instance, Attack attack)
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
                manager.ApplyDamage(info);
            }
        }

        [HarmonyPatch(typeof(Enemy_Health), nameof(Enemy_Health.OnReceivedCollison))]
        private static class NpcCollisionPatch
        {
            private static void Postfix(Enemy_Health __instance, Collision collison, float relVelocitySqr, EnemyCollisonRelay.BodyPart part, bool isStay)
            {
                if (!Config.Enabled || !Config.NpcEnabled || Runtime == null || __instance == null || collison == null || isStay)
                    return;

                if (relVelocitySqr < 18f)
                    return;

                NPCHealth? manager = Runtime.GetOrCreateNpcManager(__instance);
                if (manager == null)
                    return;

                DamageInfo info = DamageProcessor.FromNpcCollision(collison, part);
                manager.ApplyDamage(info);
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
