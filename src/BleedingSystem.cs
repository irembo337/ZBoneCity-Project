using System;

namespace BonelabAdvancedHealth
{
    public struct BleedSource
    {
        public bool Active;
        public BodyPart BodyPart;
        public BleedSeverity Severity;
        public float RateMlPerSecond;
        public float RemainingSeconds;
        public bool TourniquetControlled;

        public void Clear()
        {
            Active = false;
            BodyPart = BodyPart.Torso;
            Severity = BleedSeverity.None;
            RateMlPerSecond = 0f;
            RemainingSeconds = 0f;
            TourniquetControlled = false;
        }
    }

    public sealed class BleedingSystem
    {
        private readonly HealthManager _manager;
        private readonly BleedSource[] _sources;
        private float _tickAccumulator;

        public float BloodVolumeMl { get; private set; }
        public float TotalBleedRateMlPerSecond { get; private set; }
        public int ActiveBleedCount { get; private set; }
        public float BloodNormalized => Config.Clamp(BloodVolumeMl / Config.BloodVolumeMl, 0f, 1f);
        public bool HasActiveBleeding => ActiveBleedCount > 0;

        public event Action<BleedingSystem>? BloodChanged;
        public event Action<BleedSource>? BleedStarted;
        public event Action<BodyPart>? BleedStopped;

        public BleedingSystem(HealthManager manager)
        {
            _manager = manager;
            _sources = new BleedSource[Config.MaxBleedSources];
            BloodVolumeMl = Config.BloodVolumeMl;
        }

        public void Reset()
        {
            for (int i = 0; i < _sources.Length; i++)
                _sources[i].Clear();

            _tickAccumulator = 0f;
            BloodVolumeMl = Config.BloodVolumeMl;
            TotalBleedRateMlPerSecond = 0f;
            ActiveBleedCount = 0;
            BloodChanged?.Invoke(this);
        }

        public void Update(float deltaTime)
        {
            if (_manager.IsDead)
                return;

            _tickAccumulator += deltaTime;
            float interval = Config.BleedTickInterval;
            if (_tickAccumulator < interval)
                return;

            float elapsed = _tickAccumulator;
            _tickAccumulator = 0f;
            float loss = 0f;
            TotalBleedRateMlPerSecond = 0f;
            ActiveBleedCount = 0;

            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].Active)
                    continue;

                BleedSource source = _sources[i];
                float rate = source.RateMlPerSecond;
                if (source.TourniquetControlled)
                    rate *= 0.12f;

                source.RemainingSeconds -= elapsed;
                if (source.RemainingSeconds <= 0f || rate <= 0f)
                {
                    BodyPart stoppedPart = source.BodyPart;
                    _sources[i].Clear();
                    BleedStopped?.Invoke(stoppedPart);
                    continue;
                }

                loss += rate * elapsed;
                TotalBleedRateMlPerSecond += rate;
                ActiveBleedCount++;
                _sources[i] = source;
            }

            if (loss > 0f)
            {
                BloodVolumeMl = Config.Clamp(BloodVolumeMl - loss, 0f, Config.BloodVolumeMl);
                BloodChanged?.Invoke(this);
                _manager.OnBloodLoss(loss);
            }

            if (BloodVolumeMl <= Config.DeathBloodMl)
                _manager.RequestDeath(DeathCause.BloodLoss);
        }

        public void AddBleed(DamageInfo info, LimbHealth limb, BleedSeverity severity)
        {
            if (severity == BleedSeverity.None)
                return;

            int index = FindFreeOrReplaceableSource(severity);
            float baseRate = Config.GetBleedRate(severity);
            float damageFactor = Config.Clamp(0.65f + info.Damage * 0.0125f, 0.65f, 2.4f);
            float typeFactor = GetTypeBleedFactor(info.DamageType);
            _sources[index] = new BleedSource
            {
                Active = true,
                BodyPart = limb.Part,
                Severity = severity,
                RateMlPerSecond = baseRate * limb.BleedingMultiplier * damageFactor * typeFactor,
                RemainingSeconds = GetBleedDuration(severity, info.DamageType),
                TourniquetControlled = false
            };

            RecalculateStats();
            BleedStarted?.Invoke(_sources[index]);
        }

        public void StopBleeding(BodyPart bodyPart, BleedSeverity maxSeverity)
        {
            bool changed = false;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].Active || _sources[i].BodyPart != bodyPart || _sources[i].Severity > maxSeverity)
                    continue;

                _sources[i].Clear();
                changed = true;
            }

            if (changed)
            {
                RecalculateStats();
                BleedStopped?.Invoke(bodyPart);
                BloodChanged?.Invoke(this);
            }
        }

        public void ApplyTourniquet(BodyPart bodyPart)
        {
            bool changed = false;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].Active || _sources[i].BodyPart != bodyPart)
                    continue;

                BleedSource source = _sources[i];
                source.TourniquetControlled = true;
                if (source.Severity < BleedSeverity.Arterial)
                    source.RemainingSeconds = Math.Min(source.RemainingSeconds, 20f);
                _sources[i] = source;
                changed = true;
            }

            if (changed)
            {
                RecalculateStats();
                BloodChanged?.Invoke(this);
            }
        }

        public void RestoreBlood(float amountMl)
        {
            if (amountMl <= 0f)
                return;

            BloodVolumeMl = Config.Clamp(BloodVolumeMl + amountMl, 0f, Config.BloodVolumeMl);
            BloodChanged?.Invoke(this);
        }

        public BodyPart GetWorstBleedingPart()
        {
            BodyPart bestPart = BodyPart.Torso;
            float bestRate = -1f;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].Active)
                    continue;

                float rate = _sources[i].RateMlPerSecond;
                if (_sources[i].TourniquetControlled)
                    rate *= 0.12f;
                if (rate > bestRate)
                {
                    bestRate = rate;
                    bestPart = _sources[i].BodyPart;
                }
            }

            return bestPart;
        }

        public BleedSeverity GetWorstBleedingSeverity(BodyPart bodyPart)
        {
            BleedSeverity worst = BleedSeverity.None;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (_sources[i].Active && _sources[i].BodyPart == bodyPart && _sources[i].Severity > worst)
                    worst = _sources[i].Severity;
            }

            return worst;
        }

        public int CopySources(Span<BleedSource> destination)
        {
            int count = 0;
            for (int i = 0; i < _sources.Length && count < destination.Length; i++)
            {
                if (!_sources[i].Active)
                    continue;

                destination[count++] = _sources[i];
            }

            return count;
        }

        private int FindFreeOrReplaceableSource(BleedSeverity incomingSeverity)
        {
            int weakestIndex = 0;
            BleedSeverity weakestSeverity = BleedSeverity.Arterial;
            float shortestRemaining = float.MaxValue;

            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].Active)
                    return i;

                if (_sources[i].Severity < weakestSeverity ||
                    (_sources[i].Severity == weakestSeverity && _sources[i].RemainingSeconds < shortestRemaining))
                {
                    weakestSeverity = _sources[i].Severity;
                    shortestRemaining = _sources[i].RemainingSeconds;
                    weakestIndex = i;
                }
            }

            return incomingSeverity >= weakestSeverity ? weakestIndex : 0;
        }

        private void RecalculateStats()
        {
            TotalBleedRateMlPerSecond = 0f;
            ActiveBleedCount = 0;
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].Active)
                    continue;

                float rate = _sources[i].RateMlPerSecond;
                if (_sources[i].TourniquetControlled)
                    rate *= 0.12f;

                TotalBleedRateMlPerSecond += rate;
                ActiveBleedCount++;
            }
        }

        private static float GetBleedDuration(BleedSeverity severity, AdvancedDamageType damageType)
        {
            float duration;
            switch (severity)
            {
                case BleedSeverity.Light:
                    duration = 45f;
                    break;
                case BleedSeverity.Medium:
                    duration = 120f;
                    break;
                case BleedSeverity.Severe:
                    duration = 240f;
                    break;
                case BleedSeverity.Arterial:
                    duration = 480f;
                    break;
                default:
                    duration = 0f;
                    break;
            }

            if (damageType == AdvancedDamageType.Stab || damageType == AdvancedDamageType.Bullet)
                duration *= 1.25f;

            return duration;
        }

        private static float GetTypeBleedFactor(AdvancedDamageType type)
        {
            switch (type)
            {
                case AdvancedDamageType.Bullet:
                    return 1.15f;
                case AdvancedDamageType.Stab:
                    return 1.35f;
                case AdvancedDamageType.Explosion:
                    return 0.75f;
                case AdvancedDamageType.Fall:
                    return 0.35f;
                default:
                    return 1.0f;
            }
        }
    }
}
