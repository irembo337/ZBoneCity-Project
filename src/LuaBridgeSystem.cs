using System;
using System.Reflection;
using MelonLoader;

namespace BonelabAdvancedHealth
{
    public sealed class LuaBridgeSystem
    {
        private MelonLogger.Instance? _logger;
        private Type? _dynValueType;
        private MethodInfo? _invokeEvent;
        private MethodInfo? _newNumber;
        private MethodInfo? _newString;
        private MethodInfo? _newBoolean;
        private float _snapshotTimer;
        private bool _available;
        private bool _loggedUnavailable;

        public void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
            ResolveLuaMod();
        }

        public void OnSceneLoaded()
        {
            _snapshotTimer = 0f;
            if (!_available)
                ResolveLuaMod();
        }

        public void Update(float deltaTime, HealthManager? player)
        {
            if (!_available || player == null || !Config.Enabled)
                return;

            _snapshotTimer += deltaTime;
            if (_snapshotTimer < 0.2f)
                return;

            _snapshotTimer = 0f;
            Invoke(
                "ZBC_MedicalSnapshot",
                player.Bleeding.BloodNormalized,
                player.Lungs.OxygenNormalized,
                player.PainNormalized,
                player.Shock.Intensity,
                player.Stress.Normalized,
                player.PulseBpm,
                player.Bleeding.TotalBleedRateMlPerSecond,
                player.InternalBleedingNormalized,
                player.InfectionRisk,
                CountFractures(player),
                player.Consciousness.State == ConsciousnessState.Unconscious,
                player.IsDead);
        }

        public void NotifyDamage(HealthManager manager, DamageInfo info)
        {
            if (!_available || manager.Kind != HealthOwnerKind.Player || !Config.Enabled)
                return;

            Invoke(
                "ZBC_DamageProcessed",
                info.BodyPart.ToString(),
                info.DamageType.ToString(),
                info.Damage,
                info.Pain,
                info.BleedFactor,
                info.UnconsciousnessImpulse);
        }

        public void NotifyTreatment(HealthManager manager, MedicalItemType itemType, BodyPart part)
        {
            if (!_available || manager.Kind != HealthOwnerKind.Player || !Config.Enabled)
                return;

            Invoke("ZBC_TreatmentApplied", itemType.ToString(), part.ToString());
        }

        private void ResolveLuaMod()
        {
            try
            {
                Type? eventsType = Type.GetType("LuaMod.LuaAPI.API_Events, LuaMod", false);
                Type? dynValueType = Type.GetType("MoonSharp.Interpreter.DynValue, MoonSharp.Interpreter", false);
                if (eventsType == null || dynValueType == null)
                {
                    LogUnavailableOnce();
                    return;
                }

                MethodInfo? invoke = eventsType.GetMethod("BL_InvokeEvent", BindingFlags.Public | BindingFlags.Static);
                MethodInfo? newNumber = dynValueType.GetMethod("NewNumber", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(double) }, null);
                MethodInfo? newString = dynValueType.GetMethod("NewString", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                MethodInfo? newBoolean = dynValueType.GetMethod("NewBoolean", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(bool) }, null);
                if (invoke == null || newNumber == null || newString == null || newBoolean == null)
                {
                    LogUnavailableOnce();
                    return;
                }

                _dynValueType = dynValueType;
                _invokeEvent = invoke;
                _newNumber = newNumber;
                _newString = newString;
                _newBoolean = newBoolean;
                _available = true;
                _logger?.Msg("[ZBC] Lua bridge connected.");
            }
            catch (Exception ex)
            {
                _available = false;
                _logger?.Warning("[ZBC ERROR] Lua bridge initialization failed: " + ex.Message);
            }
        }

        private void Invoke(string eventName, params object[] values)
        {
            try
            {
                if (_invokeEvent == null || _dynValueType == null)
                    return;

                Array dynArgs = Array.CreateInstance(_dynValueType, values.Length);
                for (int i = 0; i < values.Length; i++)
                    dynArgs.SetValue(CreateDynValue(values[i]), i);

                _invokeEvent.Invoke(null, new object[] { eventName, dynArgs });
            }
            catch (Exception ex)
            {
                _available = false;
                _logger?.Warning("[ZBC ERROR] Lua bridge event failed and was disabled: " + ex.Message);
            }
        }

        private object? CreateDynValue(object value)
        {
            if (value is bool boolValue)
                return _newBoolean?.Invoke(null, new object[] { boolValue });

            if (value is string stringValue)
                return _newString?.Invoke(null, new object[] { stringValue });

            return _newNumber?.Invoke(null, new object[] { Convert.ToDouble(value) });
        }

        private void LogUnavailableOnce()
        {
            _available = false;
            if (_loggedUnavailable)
                return;

            _loggedUnavailable = true;
            _logger?.Msg("[ZBC] LuaMod bridge inactive; running native C# medical systems.");
        }

        private static int CountFractures(HealthManager manager)
        {
            int count = 0;
            LimbHealth[] limbs = manager.GetLimbArray();
            for (int i = 0; i < limbs.Length; i++)
            {
                if (limbs[i].IsBroken)
                    count++;
            }

            return count;
        }
    }
}
