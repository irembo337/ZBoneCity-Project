using UnityEngine;

namespace BonelabAdvancedHealth
{
    public readonly struct TraumaFrameFx
    {
        public readonly float Pain;
        public readonly float BloodLoss;
        public readonly float HeadTrauma;
        public readonly float OxygenLoss;
        public readonly float Combined;
        public readonly Vector2 Offset;
        public readonly float Scale;
        public readonly float Pulse;

        public TraumaFrameFx(float pain, float bloodLoss, float headTrauma, float oxygenLoss, float combined, Vector2 offset, float scale, float pulse)
        {
            Pain = pain;
            BloodLoss = bloodLoss;
            HeadTrauma = headTrauma;
            OxygenLoss = oxygenLoss;
            Combined = combined;
            Offset = offset;
            Scale = scale;
            Pulse = pulse;
        }
    }

    public sealed class PostProcessingTraumaFX
    {
        private float _time;
        private float _smoothedCombined;
        private float _smoothedPain;
        private float _smoothedHead;

        public void Reset()
        {
            _time = 0f;
            _smoothedCombined = 0f;
            _smoothedPain = 0f;
            _smoothedHead = 0f;
        }

        public TraumaFrameFx Evaluate(HealthManager manager, float deltaTime)
        {
            float intensity = Config.ScreenEffectsIntensity;
            if (intensity <= 0f)
            {
                Reset();
                return new TraumaFrameFx(0f, 0f, 0f, 0f, 0f, Vector2.zero, 1f, 0f);
            }

            _time += deltaTime;
            float pain = Config.PainEffectsEnabled ? manager.PainNormalized : 0f;
            float bloodLoss = 1f - manager.Bleeding.BloodNormalized;
            float head = manager.Brain.DisorientationNormalized + manager.Brain.RingingIntensity * 0.35f;
            float oxygen = 1f - manager.Lungs.OxygenNormalized;
            float unconscious = manager.Consciousness.BlackoutIntensity;
            float combined = Config.Clamp(pain * 0.62f + bloodLoss * 0.42f + head * 0.52f + oxygen * 0.36f + unconscious * 0.45f, 0f, 1.65f);
            combined *= intensity;

            _smoothedPain = Mathf.MoveTowards(_smoothedPain, pain * intensity, deltaTime * 1.8f);
            _smoothedHead = Mathf.MoveTowards(_smoothedHead, head * intensity, deltaTime * 1.5f);
            _smoothedCombined = Mathf.MoveTowards(_smoothedCombined, combined, deltaTime * 1.35f);

            float pulseRate = Mathf.Lerp(1.3f, 5.5f, Config.Clamp(_smoothedPain + bloodLoss, 0f, 1f));
            float pulse = 0.5f + 0.5f * Mathf.Sin(_time * pulseRate);
            float shake = Config.Clamp(_smoothedPain * 0.65f + _smoothedHead * 0.8f + manager.PainSystem.ShakeIntensity * 0.35f, 0f, 1.2f);
            float x = (Mathf.PerlinNoise(_time * 11.7f, 0.25f) - 0.5f) * shake * 26f;
            float y = (Mathf.PerlinNoise(0.75f, _time * 13.1f) - 0.5f) * shake * 18f;
            float scale = 1f + Config.Clamp(_smoothedCombined * 0.025f + pulse * _smoothedPain * 0.012f, 0f, 0.065f);

            return new TraumaFrameFx(
                Config.Clamp(_smoothedPain, 0f, 1.4f),
                Config.Clamp(bloodLoss * intensity, 0f, 1.3f),
                Config.Clamp(_smoothedHead, 0f, 1.4f),
                Config.Clamp(oxygen * intensity, 0f, 1.3f),
                Config.Clamp(_smoothedCombined, 0f, 1.8f),
                new Vector2(x, y),
                scale,
                pulse);
        }
    }
}
