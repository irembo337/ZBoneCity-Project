namespace BonelabAdvancedHealth
{
    public sealed class SpawnManagerFix
    {
        private readonly SpawnProtectionSystem _protection = new SpawnProtectionSystem();

        public bool IsProtected => _protection.IsProtected;
        public bool CanInitializeHealth => _protection.CanInitializeHealth;
        public bool CanProcessDamage => _protection.CanProcessDamage;
        public bool CanRunTraumaSystems => _protection.CanRunTraumaSystems;
        public string Reason => _protection.Reason;

        public void OnSceneLoaded()
        {
            _protection.OnSceneLoaded();
        }

        public void Update(float deltaTime, MainMod mod, HealthManager? player)
        {
            _protection.Update(deltaTime, mod, player);
        }
    }
}
