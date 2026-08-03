namespace BonelabAdvancedHealth
{
    public static class RagdollController
    {
        public static bool CanApplyPlayerRagdoll(bool ragdoll)
        {
            return TraumaStartupGuard.CanApplyPlayerRagdoll(ragdoll);
        }
    }
}
