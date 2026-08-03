namespace BonelabAdvancedHealth
{
    public static class TraumaStartupGuard
    {
        public static bool IsPlayerSpawnProtected
        {
            get
            {
                MainMod? runtime = MainMod.Runtime;
                return runtime != null && runtime.IsSpawnProtected;
            }
        }

        public static bool CanProcessPlayerDamage
        {
            get
            {
                MainMod? runtime = MainMod.Runtime;
                return runtime != null && runtime.CanProcessPlayerDamage;
            }
        }

        public static bool CanRunPlayerTrauma
        {
            get
            {
                MainMod? runtime = MainMod.Runtime;
                return runtime != null && runtime.CanRunPlayerTrauma;
            }
        }

        public static bool CanApplyPlayerRagdoll(bool ragdoll)
        {
            if (!ragdoll)
                return true;

            MainMod? runtime = MainMod.Runtime;
            return runtime == null || runtime.CanApplyPlayerRagdoll;
        }
    }
}
