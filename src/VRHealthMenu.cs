using MelonLoader;

namespace BonelabAdvancedHealth
{
    public sealed class VRHealthMenu
    {
        private readonly BoneMenuIntegration _boneMenu = new BoneMenuIntegration();
        private MelonLogger.Instance? _logger;
        private float _retryAccumulator;

        public void Initialize(MelonLogger.Instance logger)
        {
            _logger = logger;
            _boneMenu.Initialize(logger);
        }

        public void Update(float deltaTime, HealthManager? manager)
        {
            if (!_boneMenu.IsInitialized && _logger != null)
            {
                _retryAccumulator += deltaTime;
                if (_retryAccumulator >= 2.0f)
                {
                    _retryAccumulator = 0f;
                    _boneMenu.Initialize(_logger);
                }
            }

            _boneMenu.Update(deltaTime, manager);
        }
    }
}
