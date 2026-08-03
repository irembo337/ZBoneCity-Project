using System;

namespace BonelabAdvancedHealth
{
    public struct BleedSource
    {
        public bool Active;
        public BodyPart BodyPart;
        public BleedSeverity Severity;
        public WoundSeverity WoundSeverity;
        public float RateMlPerSecond;
        public float RemainingSeconds;
        public bool TourniquetControlled;
        public bool Internal;

        public void Clear()
        {
            Active = false;
            BodyPart = BodyPart.Torso;
            Severity = BleedSeverity.None;
            WoundSeverity = WoundSeverity.None;
            RateMlPerSecond = 0f;
            RemainingSeconds = 0f;
            TourniquetControlled = false;
            Internal = false;
        }
    }

    public sealed class BleedingSystem
    {
        private static readonly float[] BleedDurationSeconds =
        {
            0f,
            45f,
            120f,
            240f,
            480f
        };

        private static readonly float[] DamageTypeBleedFactor =
        {
            1.15f,
            1.0f,
            0.75f,
            1.35f,
            0.35f
        };

        private readonly HealthManager _manager;
        private readonly BleedSource[] _sources;
        private readonly float[] _bandageSeconds;
        private readonly float[] _bandageStrength;
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
            _bandageSeconds = new float[Config.LimbCount];
            _bandageStrength = new float[Config.LimbCount];
            BloodVolumeMl = Config.BloodVolumeMl;
        }

        public void Reset()
        {
            for (int i = 0; i < _sources.Length; i++)
                _sources[i].Clear();
            for (int i = 0; i < _bandageSeconds.Length; i++)
            {
                _bandageSeconds[i] = 0f;
                _bandageStrength[i] = 0f;
            }

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
                int bodyIndex = (int)source.BodyPart;
                if (!source.Internal && _bandageSeconds[bodyIndex] > 0f)
                {
                    float bandageReduction = Config.Clamp(_bandageStrength[bodyIndex] * elapsed * 0.18f, 0f, 0.85f);
                    source.RateMlPerSecond = Math.Max(0f, source.RateMlPerSecond * (1f - bandageReduction));
                    source.RemainingSeconds = Math.Min(source.RemainingSeconds, 20f + source.RemainingSeconds * 0.82f);
                    rate = source.RateMlPerSecond;
                    _bandageSeconds[bodyIndex] = Math.Max(0f, _bandageSeconds[bodyIndex] - elapsed);
                }
                if (source.TourniquetControlled && !source.Internal)
                    rate *= 0.015f;

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
            WoundSeverity wound = GetWoundSeverity(info, severity);
            AddBleed(info, limb, severity, 1f, 1f, wound);
        }

        public void AddBleed(DamageInfo info, LimbHealth limb, BleedSeverity severity, float rateMultiplier, float durationMultiplier, WoundSeverity woundSeverity)
        {
            if (severity == BleedSeverity.None)
                return;

            int index = FindFreeOrReplaceableSource(severity);
            float baseRate = Config.GetBleedRate(severity);
            float damageFactor = Config.Clamp(0.65f + info.Damage * 0.0125f, 0.65f, 2.4f);
            float typeFactor = GetTypeBleedFactor(info.DamageType);
            float advancedRate = AdvancedBleedingSystem.GetRateMultiplier(info, severity, woundSeverity);
            float advancedDuration = AdvancedBleedingSystem.GetDurationMultiplier(info, severity, woundSeverity);
            _sources[index] = new BleedSource
            {
                Active = true,
                BodyPart = limb.Part,
                Severity = severity,
                WoundSeverity = woundSeverity,
                RateMlPerSecond = baseRate * limb.BleedingMultiplier * damageFactor * typeFactor * advancedRate * Config.Clamp(rateMultiplier, 0.1f, 6.0f),
                RemainingSeconds = GetBleedDuration(severity, info.DamageType) * advancedDuration * Config.Clamp(durationMultiplier, 0.25f, 4.0f),
                TourniquetControlled = false,
                Internal = AdvancedBleedingSystem.IsInternalBleed(woundSeverity)
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

        public void ApplyBandage(BodyPart bodyPart, float seconds, float strength)
        {
            int index = (int)bodyPart;
            if (index < 0 || index >= _bandageSeconds.Length)
                return;

            _bandageSeconds[index] = Math.Max(_bandageSeconds[index], seconds);
            _bandageStrength[index] = Math.Max(_bandageStrength[index], Config.Clamp(strength, 0.05f, 2.0f));
            BleedSeverity immediateStopSeverity = strength >= 1.15f ? BleedSeverity.Medium : BleedSeverity.Light;
            StopBleeding(bodyPart, immediateStopSeverity);
            ApplyPressureToHeavyBleeds(bodyPart, strength);
        }

        private void ApplyPressureToHeavyBleeds(BodyPart bodyPart, float strength)
        {
            bool changed = false;
            float clampedStrength = Config.Clamp(strength, 0.05f, 2.0f);
            for (int i = 0; i < _sources.Length; i++)
            {
                if (!_sources[i].Active || _sources[i].BodyPart != bodyPart || _sources[i].Internal)
                    continue;

                BleedSource source = _sources[i];
                if (source.Severity == BleedSeverity.Severe)
                {
                    source.RateMlPerSecond *= Config.Clamp(1f - clampedStrength * 0.48f, 0.18f, 0.78f);
                    source.RemainingSeconds = Math.Min(source.RemainingSeconds, 96f);
                    if (source.RateMlPerSecond <= Config.GetBleedRate(BleedSeverity.Medium) * 0.55f)
                        source.Severity = BleedSeverity.Medium;
                    _sources[i] = source;
                    changed = true;
                }
                else if (source.Severity == BleedSeverity.Arterial && (bodyPart == BodyPart.Head || bodyPart == BodyPart.Torso))
                {
                    source.RateMlPerSecond *= Config.Clamp(1f - clampedStrength * 0.42f, 0.22f, 0.82f);
                    source.RemainingSeconds = Math.Min(source.RemainingSeconds, 150f);
                    if (clampedStrength >= 1.15f || source.RateMlPerSecond <= Config.GetBleedRate(BleedSeverity.Severe) * 0.95f)
                    {
                        source.Severity = BleedSeverity.Severe;
                        source.WoundSeverity = WoundSeverity.DeepCut;
                    }
                    _sources[i] = source;
                    changed = true;
                }
            }

            if (changed)
            {
                RecalculateStats();
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
                if (source.Internal)
                    continue;

                if (source.Severity < BleedSeverity.Arterial)
                {
                    _sources[i].Clear();
                    changed = true;
                    continue;
                }

                source.TourniquetControlled = true;
                source.RemainingSeconds = Math.Min(source.RemainingSeconds, 420f);
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
            if (amountMl == 0f)
                return;

            BloodVolumeMl = Config.Clamp(BloodVolumeMl + amountMl, 0f, Config.BloodVolumeMl);
            BloodChanged?.Invoke(this);
            if (BloodVolumeMl <= Config.DeathBloodMl)
                _manager.RequestDeath(DeathCause.BloodLoss);
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
                if (_sources[i].TourniquetControlled && !_sources[i].Internal)
                    rate *= 0.015f;
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
                if (_sources[i].TourniquetControlled && !_sources[i].Internal)
                    rate *= 0.015f;

                TotalBleedRateMlPerSecond += rate;
                ActiveBleedCount++;
            }
        }

        private static float GetBleedDuration(BleedSeverity severity, AdvancedDamageType damageType)
        {
            int severityIndex = (int)severity;
            float duration = severityIndex >= 0 && severityIndex < BleedDurationSeconds.Length ? BleedDurationSeconds[severityIndex] : 0f;

            if (damageType == AdvancedDamageType.Stab || damageType == AdvancedDamageType.Bullet)
                duration *= 1.25f;

            return duration;
        }

        private static float GetTypeBleedFactor(AdvancedDamageType type)
        {
            int index = (int)type;
            return index >= 0 && index < DamageTypeBleedFactor.Length ? DamageTypeBleedFactor[index] : 1.0f;
        }

        private static WoundSeverity GetWoundSeverity(DamageInfo info, BleedSeverity severity)
        {
            if (severity == BleedSeverity.Arterial)
                return WoundSeverity.ArterialCut;
            if (info.DamageType == AdvancedDamageType.Stab || info.DamageType == AdvancedDamageType.Bullet)
                return severity >= BleedSeverity.Severe ? WoundSeverity.DeepCut : WoundSeverity.SurfaceCut;
            if (info.DamageType == AdvancedDamageType.Explosion)
                return WoundSeverity.DeepCut;
            return WoundSeverity.None;
        }
    }
}
