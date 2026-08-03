namespace BonelabAdvancedHealth
{
    public sealed class PainEffectManager
    {
        private readonly FullscreenPainEffects _overlay = new FullscreenPainEffects();
        private readonly PostProcessingTraumaFX _frameFx = new PostProcessingTraumaFX();

        public void Update(float deltaTime, HealthManager? manager)
        {
            if (manager == null)
                return;

            TraumaFrameFx fx = _frameFx.Evaluate(manager, deltaTime);
            _overlay.Update(fx, manager);
        }

        public TraumaFrameFx EvaluateHudFrame(HealthManager manager, float deltaTime)
        {
            return _frameFx.Evaluate(manager, deltaTime);
        }

        public void Reset()
        {
            _frameFx.Reset();
            _overlay.Reset();
        }

        public void Destroy()
        {
            _frameFx.Reset();
            _overlay.Destroy();
        }
    }
}
