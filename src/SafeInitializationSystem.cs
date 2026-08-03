namespace BonelabAdvancedHealth
{
    public enum SafeInitializationPhase
    {
        SceneLoading = 0,
        WaitingForRig = 1,
        StabilizingRig = 2,
        Ready = 3
    }

    public sealed class SafeInitializationSystem
    {
        private const float MinimumSceneSeconds = 1.75f;
        private const float MinimumStableSeconds = 2.0f;
        private const float MaximumProtectionSeconds = 16f;
        private const float ReadyDamageGraceSeconds = 3.0f;
        private const float MaximumStableVelocity = 2.35f;

        private float _sceneSeconds;
        private float _stableSeconds;
        private float _readyGraceSeconds;
        private SafeInitializationPhase _lastLoggedPhase;

        public SafeInitializationPhase Phase { get; private set; }
        public string Reason { get; private set; } = "scene loading";
        public bool IsProtected => Phase != SafeInitializationPhase.Ready || _readyGraceSeconds > 0f;
        public bool ShouldStabilizeRig => Phase != SafeInitializationPhase.Ready;
        public bool CanInitializeHealth => Phase == SafeInitializationPhase.StabilizingRig || Phase == SafeInitializationPhase.Ready;
        public bool CanRunTraumaSystems => Phase == SafeInitializationPhase.Ready && _readyGraceSeconds <= 0f;
        public bool CanProcessDamage => CanRunTraumaSystems;
        public void Reset()
        {
            _sceneSeconds = 0f;
            _stableSeconds = 0f;
            _readyGraceSeconds = ReadyDamageGraceSeconds;
            Phase = SafeInitializationPhase.SceneLoading;
            _lastLoggedPhase = SafeInitializationPhase.SceneLoading;
            Reason = "scene loading";
        }

        public void Update(float deltaTime, MainMod mod)
        {
            _sceneSeconds += deltaTime;
            if (Phase == SafeInitializationPhase.Ready)
            {
                if (_readyGraceSeconds > 0f)
                {
                    _readyGraceSeconds = System.Math.Max(0f, _readyGraceSeconds - deltaTime);
                    Reason = _readyGraceSeconds > 0f ? "damage grace" : "ready";
                }

                return;
            }

            if (_sceneSeconds < MinimumSceneSeconds)
            {
                SetPhase(SafeInitializationPhase.SceneLoading, "waiting for scene physics");
                return;
            }

            if (!PlayerRigFix.ValidatePlayerRig(mod, out string validationReason))
            {
                _stableSeconds = 0f;
                SetPhase(SafeInitializationPhase.WaitingForRig, validationReason);
                return;
            }

            float maxVelocity = PlayerRigFix.GetMaxPlayerBodyVelocity();
            if (maxVelocity > MaximumStableVelocity && _sceneSeconds < MaximumProtectionSeconds)
            {
                _stableSeconds = 0f;
                SetPhase(SafeInitializationPhase.StabilizingRig, "rig velocity " + maxVelocity.ToString("0.00"));
                return;
            }

            _stableSeconds += deltaTime;
            SetPhase(SafeInitializationPhase.StabilizingRig, "stable " + _stableSeconds.ToString("0.00") + "s");
            if (_stableSeconds >= MinimumStableSeconds || _sceneSeconds >= MaximumProtectionSeconds)
            {
                Phase = SafeInitializationPhase.Ready;
                Reason = _sceneSeconds >= MaximumProtectionSeconds ? "ready after timeout" : "ready";
                _readyGraceSeconds = ReadyDamageGraceSeconds;
                LogPhase();
            }
        }

        private void SetPhase(SafeInitializationPhase phase, string reason)
        {
            Phase = phase;
            Reason = reason;
            LogPhase();
        }

        private void LogPhase()
        {
            if (!Config.DebugMode || _lastLoggedPhase == Phase)
                return;

            _lastLoggedPhase = Phase;
            MainMod.Runtime?.Logger.Msg("[ZBC] Spawn initialization phase: " + Phase + " (" + Reason + ")");
        }
    }
}
