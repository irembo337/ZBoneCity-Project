namespace BonelabAdvancedHealth
{
    public sealed class SpawnProtectionSystem
    {
        private readonly SafeInitializationSystem _initialization = new SafeInitializationSystem();
        private float _fixAccumulator;
        private float _liftLogCooldown;
        private bool _hasLifted;
        private bool _playerResetForSpawn;

        public bool IsProtected => _initialization.IsProtected;
        public bool CanInitializeHealth => _initialization.CanInitializeHealth;
        public bool CanRunTraumaSystems => _initialization.CanRunTraumaSystems;
        public bool CanProcessDamage => _initialization.CanProcessDamage;
        public string Reason => _initialization.Reason;

        public void OnSceneLoaded()
        {
            _initialization.Reset();
            _fixAccumulator = 0f;
            _liftLogCooldown = 0f;
            _hasLifted = false;
            _playerResetForSpawn = false;
        }

        public void Update(float deltaTime, MainMod mod, HealthManager? player)
        {
            if (_liftLogCooldown > 0f)
                _liftLogCooldown = System.Math.Max(0f, _liftLogCooldown - deltaTime);

            _initialization.Update(deltaTime, mod);
            if (!IsProtected)
                return;

            if (player != null && !_playerResetForSpawn)
            {
                player.ForceSafeSpawnState();
                _playerResetForSpawn = true;
                if (Config.DebugMode)
                    mod.Logger.Msg("Player health reset for protected spawn.");
            }

            _fixAccumulator += deltaTime;
            if (_fixAccumulator < 0.2f)
                return;

            _fixAccumulator = 0f;
            if (!_initialization.ShouldStabilizeRig)
                return;
            if (!mod.IsPlayerRigReady())
                return;

            PlayerRigFix.ResetSpawnRagdoll(mod);
            PlayerRigFix.StabilizePlayerPhysics(mod, true);
            if (PlayerRigFix.TryLiftPlayerAboveFloor(mod, 0.08f, _hasLifted ? 0.18f : 0.9f))
            {
                _hasLifted = true;
                if (Config.DebugMode && _liftLogCooldown <= 0f)
                {
                    mod.Logger.Msg("Spawn floor penetration corrected.");
                    _liftLogCooldown = 2f;
                }
            }
        }
    }
}
