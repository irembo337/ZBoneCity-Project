namespace BonelabAdvancedHealth
{
    public sealed class SpawnSafetyFix
    {
        private float _activeSeconds;
        private float _probeAccumulator;
        private bool _hasLifted;

        public void OnSceneLoaded()
        {
            _activeSeconds = 8f;
            _probeAccumulator = 0f;
            _hasLifted = false;
        }

        public void Update(float deltaTime, MainMod mod, HealthManager? player)
        {
            if (_activeSeconds <= 0f || mod == null || !mod.IsPlayerRigReady())
                return;

            _activeSeconds -= deltaTime;
            _probeAccumulator += deltaTime;
            if (_probeAccumulator < 0.25f)
                return;

            _probeAccumulator = 0f;
            if (player != null && player.Consciousness.State != ConsciousnessState.Awake)
                return;
            if (player != null && player.Coma.IsActive)
                return;

            PlayerRigFix.ResetSpawnRagdoll(mod);
            if (PlayerRigFix.TryLiftPlayerAboveFloor(mod, 0.08f, _hasLifted ? 0.18f : 0.85f))
                _hasLifted = true;
        }
    }
}
