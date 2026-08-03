using System;
using System.Reflection;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class FusionCompatibilitySystem
    {
        private float _probeAccumulator;
        private bool _logged;

        public bool IsInstalled { get; private set; }
        public bool IsNetworkSessionActive { get; private set; }

        public void Initialize()
        {
            Probe();
        }

        public void Update(float deltaTime)
        {
            _probeAccumulator += deltaTime;
            if (_probeAccumulator < 2.0f)
                return;

            _probeAccumulator = 0f;
            Probe();
        }

        public void NotifyLocalDamage(HealthManager manager, DamageInfo info)
        {
            if (!IsInstalled || manager.Kind != HealthOwnerKind.Player)
                return;

            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("Fusion compat local damage: " + info.BodyPart + " " + info.DamageType + " " + info.Damage.ToString("0.0"));
        }

        public void NotifyLocalDeath(HealthManager manager, DeathCause cause)
        {
            if (!IsInstalled || manager.Kind != HealthOwnerKind.Player)
                return;

            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("Fusion compat local death: " + cause);
        }

        public void NotifyLocalDeathSound(HealthManager manager, DeathCause cause, int soundIndex, Vector3 position)
        {
            if (!IsInstalled)
                return;

            if (Config.DebugMode)
            {
                MainMod.Runtime?.Logger.Msg(
                    "Fusion compat death sound: owner=" + manager.OwnerId +
                    " kind=" + manager.Kind +
                    " cause=" + cause +
                    " soundIndex=" + soundIndex +
                    " position=" + position);
            }
        }

        public void NotifyLocalGearSound(int soundIndex, Vector3 position, float speed01)
        {
            if (!IsInstalled)
                return;

            if (Config.DebugMode)
            {
                MainMod.Runtime?.Logger.Msg(
                    "Fusion compat gear sound: index=" + soundIndex +
                    " speed=" + speed01.ToString("0.00") +
                    " position=" + position);
            }
        }

        public void NotifyLocalMedical(HealthManager manager, MedicalItemType itemType)
        {
            NotifyLocalMedical(manager, itemType, BodyPart.Torso);
        }

        public void NotifyLocalMedical(HealthManager manager, MedicalItemType itemType, BodyPart part)
        {
            if (!IsInstalled || manager.Kind != HealthOwnerKind.Player)
                return;

            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("Fusion compat local medical: " + itemType + " on " + part);
        }

        public void NotifyLocalForensics(HealthManager manager, DamageInfo info, OrganDamageFeedback feedback)
        {
            if (!IsInstalled || manager.Kind != HealthOwnerKind.Player)
                return;

            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("Fusion compat forensic event: " + info.DamageType + " " + feedback.BleedSeverity + " " + info.BodyPart);
        }

        public void NotifyLocalCasualtyDrag(HealthManager manager, bool dragging)
        {
            if (!IsInstalled)
                return;

            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("Fusion compat casualty drag: owner=" + manager.OwnerId + " dragging=" + dragging);
        }

        public void NotifyLocalNeckTrauma(HealthManager manager, NeckInjuryState state, NeckTraumaCause cause)
        {
            if (!IsInstalled)
                return;

            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("Fusion compat neck trauma: owner=" + manager.OwnerId + " state=" + state + " cause=" + cause);
        }

        public void NotifyLocalMedication(HealthManager manager, MedicalItemType itemType, float overdoseRisk)
        {
            if (!IsInstalled)
                return;

            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("Fusion compat medication: owner=" + manager.OwnerId + " item=" + itemType + " overdoseRisk=" + overdoseRisk.ToString("0.00"));
        }

        public void NotifyLocalRehabilitation(HealthManager manager, BodyPart part, float recoverySeverity)
        {
            if (!IsInstalled)
                return;

            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("Fusion compat rehabilitation: owner=" + manager.OwnerId + " part=" + part + " severity=" + recoverySeverity.ToString("0.00"));
        }

        public void NotifyLocalMagazineCheck(string estimate)
        {
            if (!IsInstalled)
                return;

            if (Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("Fusion compat magazine check: " + estimate);
        }

        public void NotifyLocalPersistentCorpse(HealthManager manager, DeathCause cause, Vector3 position, int corpseId)
        {
            if (!IsInstalled || manager.Kind != HealthOwnerKind.Player)
                return;

            if (Config.DebugMode)
            {
                MainMod.Runtime?.Logger.Msg(
                    "Fusion compat persistent corpse: owner=" + manager.OwnerId +
                    " corpseId=" + corpseId +
                    " cause=" + cause +
                    " position=" + position);
            }
        }

        private void Probe()
        {
            bool installed = false;
            bool active = false;
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                string name = assemblies[i].GetName().Name ?? string.Empty;
                if (name.IndexOf("LabFusion", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Fusion", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                installed = true;
                active |= TryReadNetworkActive(assemblies[i]);
            }

            IsInstalled = installed;
            IsNetworkSessionActive = active;
            if (installed && !_logged)
            {
                _logged = true;
                MainMod.Runtime?.Logger.Msg("BONELAB Fusion detected. ZBoneCity is running in local-safe compatibility mode.");
            }
        }

        private static bool TryReadNetworkActive(Assembly assembly)
        {
            try
            {
                Type? networkInfo = assembly.GetType("LabFusion.Network.NetworkInfo", false);
                if (networkInfo == null)
                    return false;

                return ReadBoolProperty(networkInfo, "HasServer") ||
                       ReadBoolProperty(networkInfo, "IsClient") ||
                       ReadBoolProperty(networkInfo, "IsServer") ||
                       ReadBoolProperty(networkInfo, "InSession");
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool ReadBoolProperty(Type type, string propertyName)
        {
            PropertyInfo? property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
            if (property == null || property.PropertyType != typeof(bool))
                return false;

            object? value = property.GetValue(null, null);
            return value is bool boolValue && boolValue;
        }
    }
}
