using System;

namespace BonelabAdvancedHealth
{
    public enum DeathCause
    {
        Trauma = 0,
        BloodLoss = 1,
        CriticalHeadDamage = 2,
        CriticalTorsoDamage = 3,
        CardiacArrest = 4,
        BrainFailure = 5,
        OxygenLoss = 6,
        OrganFailure = 7
    }

    public class HealthManager
    {
        private readonly LimbHealth[] _limbs;
        private float _lastDamageTime;
        private int _lastSourceId;
        private int _lastAttackOrder;
        private BodyPart _lastPart;
        private AdvancedDamageType _lastDamageType;
        private float _morphineSeconds;
        private float _morphineAwarenessPenalty;
        private int _morphineDoseCount;
        private float _adrenalineSeconds;
        private float _adrenalineCrashSeconds;
        private readonly float[] _tourniquetSeconds;
        private bool _deathRequested;

        public HealthOwnerKind Kind { get; }
        public int OwnerId { get; }
        public Random Random { get; }
        public BleedingSystem Bleeding { get; }
        public ConsciousnessSystem Consciousness { get; }
        public FractureSystem Fractures { get; }
        public BoneSystem Bones { get; }
        public OrganSystem Organs { get; }
        public LungDamageSystem Lungs { get; }
        public BrainTraumaSystem Brain { get; }
        public AudioTraumaSystem AudioTrauma { get; }
        public PainSystem PainSystem { get; }
        public KnifePenetrationSystem KnifePenetration { get; }
        public float Pain => PainSystem.TotalPain;
        public float PainNormalized => PainSystem.PainNormalized;
        public float AwarenessPenalty => Config.Clamp(_morphineAwarenessPenalty + Brain.DisorientationNormalized * 0.35f + Lungs.OxygenStress * 0.45f, 0f, 1f);
        public float AdrenalineNormalized => _adrenalineSeconds > 0f ? Config.Clamp(_adrenalineSeconds / 45f, 0f, 1f) : 0f;
        public float PulseModifier => Organs.HeartbeatStrength;
        public bool IsDead => Consciousness.State == ConsciousnessState.Dead || _deathRequested;
        public float TotalTraumaNormalized { get; private set; }
        public DeathCause LastDeathCause { get; private set; }

        public event Action<HealthManager, DamageInfo>? DamageProcessed;
        public event Action<HealthManager>? VitalsChanged;
        public event Action<HealthManager, DeathCause>? Died;
        public event Action<HealthManager, MedicalItemType>? MedicalApplied;

        public HealthManager(HealthOwnerKind kind, int ownerId)
        {
            Kind = kind;
            OwnerId = ownerId;
            _limbs = new LimbHealth[Config.LimbCount];
            for (int i = 0; i < Config.LimbCount; i++)
            {
                _limbs[i] = new LimbHealth(
                    (BodyPart)i,
                    Config.LimbMaxHp[i],
                    Config.LimbBleedingMultiplier[i],
                    Config.LimbPainMultiplier[i]);
                _limbs[i].FractureChanged += OnLimbFractureChanged;
            }

            Random = new Random(Environment.TickCount ^ ownerId);
            _tourniquetSeconds = new float[Config.LimbCount];
            Bleeding = new BleedingSystem(this);
            Consciousness = new ConsciousnessSystem(this);
            Bones = new BoneSystem(this);
            Fractures = new FractureSystem(this);
            Organs = new OrganSystem(this);
            Lungs = new LungDamageSystem(this);
            Brain = new BrainTraumaSystem(this);
            AudioTrauma = new AudioTraumaSystem(this);
            PainSystem = new PainSystem(this);
            KnifePenetration = new KnifePenetrationSystem(this);
            Bleeding.BloodChanged += OnBleedingChanged;
            LastDeathCause = DeathCause.Trauma;
        }

        public LimbHealth GetLimb(BodyPart part)
        {
            return _limbs[(int)part];
        }

        public LimbHealth[] GetLimbArray()
        {
            return _limbs;
        }

        public void Reset()
        {
            _deathRequested = false;
            _lastDamageTime = 0f;
            _lastSourceId = 0;
            _lastAttackOrder = 0;
            _lastPart = BodyPart.Torso;
            _lastDamageType = AdvancedDamageType.Blunt;
            _morphineSeconds = 0f;
            _morphineAwarenessPenalty = 0f;
            _morphineDoseCount = 0;
            _adrenalineSeconds = 0f;
            _adrenalineCrashSeconds = 0f;
            TotalTraumaNormalized = 0f;
            LastDeathCause = DeathCause.Trauma;

            for (int i = 0; i < _limbs.Length; i++)
            {
                _limbs[i].Reset();
                _tourniquetSeconds[i] = 0f;
            }

            Bleeding.Reset();
            Bones.Reset();
            Organs.Reset();
            Lungs.Reset();
            Brain.Reset();
            PainSystem.Reset();
            KnifePenetration.Reset();
            AudioTrauma.Reset();
            Fractures.Reset();
            Consciousness.Reset();
            VitalsChanged?.Invoke(this);
        }

        public bool ApplyDamage(DamageInfo info)
        {
            if (!Config.Enabled || IsDead || info.Damage <= 0f || IsDuplicateDamage(info))
                return false;

            _lastDamageTime = info.Time;
            _lastSourceId = info.SourceId;
            _lastAttackOrder = info.AttackOrder;
            _lastPart = info.BodyPart;
            _lastDamageType = info.DamageType;

            LimbHealth limb = GetLimb(info.BodyPart);
            float damageApplied = limb.ApplyDamage(info, Random);
            if (damageApplied <= 0f)
                return false;

            AddPain(info.Pain * limb.PainMultiplier);
            BoneDamageFeedback boneFeedback = Bones.ApplyDamage(info);
            if (boneFeedback.Pain > 0f)
                AddPain(boneFeedback.Pain);
            BleedSeverity severity = DamageProcessor.GetBleedSeverity(info, limb);
            Bleeding.AddBleed(info, limb, severity);
            OrganDamageFeedback organFeedback = Config.OrganSystemEnabled ? Organs.ApplyDamage(info) : OrganDamageFeedback.None;
            if (organFeedback.BleedSeverity != BleedSeverity.None)
                Bleeding.AddBleed(info, limb, organFeedback.BleedSeverity, organFeedback.BleedMultiplier, 1.5f, organFeedback.WoundSeverity);
            if (organFeedback.Pain > 0f)
                AddPain(organFeedback.Pain);
            if (organFeedback.InstantCollapse)
                Consciousness.SetUnconscious(organFeedback.CardiacArrest ? 12f : 5f);
            Lungs.ApplyDamage(info, organFeedback);
            Brain.ApplyDamage(info, organFeedback);
            KnifePenetration.ApplyDamage(info, organFeedback);
            AudioTrauma.OnDamage(info, organFeedback);
            if (Kind == HealthOwnerKind.Player)
                MainMod.Runtime?.Hud.OnDamageVisual(info, organFeedback);
            MainMod.Runtime?.BloodFx.OnDamage(this, info, organFeedback);
            Consciousness.ApplyDamageImpulse(info);
            RecalculateTrauma();
            DamageProcessed?.Invoke(this, info);
            VitalsChanged?.Invoke(this);
            EvaluateTraumaDeath();
            return true;
        }

        public void UpdateSystems(float deltaTime)
        {
            if (!Config.Enabled)
                return;

            if (_morphineSeconds > 0f)
            {
                _morphineSeconds = Math.Max(0f, _morphineSeconds - deltaTime);
                _morphineAwarenessPenalty = Config.Clamp(_morphineAwarenessPenalty + deltaTime * 0.006f * Math.Max(1, _morphineDoseCount), 0f, 0.45f);
            }
            else
            {
                _morphineAwarenessPenalty = Math.Max(0f, _morphineAwarenessPenalty - deltaTime * 0.01f);
            }

            if (_adrenalineSeconds > 0f)
            {
                _adrenalineSeconds = Math.Max(0f, _adrenalineSeconds - deltaTime);
                if (_adrenalineSeconds <= 0f)
                    _adrenalineCrashSeconds = 12f;
            }
            else if (_adrenalineCrashSeconds > 0f)
            {
                _adrenalineCrashSeconds = Math.Max(0f, _adrenalineCrashSeconds - deltaTime);
            }

            UpdateTourniquetDamage(deltaTime);

            Bleeding.Update(deltaTime);
            if (Config.OrganSystemEnabled)
                Organs.Update(deltaTime);
            Lungs.Update(deltaTime);
            Brain.Update(deltaTime);
            PainSystem.Update(deltaTime, _morphineSeconds > 0f, _adrenalineSeconds > 0f, _adrenalineCrashSeconds > 0f, Organs.OrganPainModifier);
            KnifePenetration.Update(deltaTime);
            Fractures.Update(deltaTime);
            Consciousness.Update(deltaTime);
            AudioTrauma.Update(deltaTime);
            VitalsChanged?.Invoke(this);
        }

        public void AddPain(float amount)
        {
            if (amount <= 0f || IsDead)
                return;

            float morphineReduction = _morphineSeconds > 0f ? 0.45f : 1f;
            float adrenalineReduction = _adrenalineSeconds > 0f ? 0.35f : 1f;
            PainSystem.AddPain(amount * morphineReduction * adrenalineReduction * Organs.OrganPainModifier, _lastPart, _lastDamageType);
        }

        public void ReducePain(float amount, float morphineSeconds)
        {
            if (amount > 0f)
                PainSystem.ReducePain(amount);
            if (morphineSeconds > 0f)
            {
                _morphineSeconds = Math.Max(_morphineSeconds, morphineSeconds);
                _morphineDoseCount++;
            }
            VitalsChanged?.Invoke(this);
        }

        public void ApplyAdrenaline(float seconds)
        {
            _adrenalineSeconds = Math.Max(_adrenalineSeconds, seconds);
            _adrenalineCrashSeconds = 0f;
            VitalsChanged?.Invoke(this);
        }

        public void RestoreBlood(float amountMl)
        {
            Bleeding.RestoreBlood(amountMl);
            VitalsChanged?.Invoke(this);
        }

        public void HealLimb(BodyPart part, float amount)
        {
            GetLimb(part).Heal(amount);
            RecalculateTrauma();
            VitalsChanged?.Invoke(this);
        }

        public void StabilizeFracture(BodyPart part, float strength)
        {
            GetLimb(part).StabilizeFracture(strength);
            Bones.StabilizeMostDamaged(strength);
            VitalsChanged?.Invoke(this);
        }

        public void StopBleeding(BodyPart part, BleedSeverity maxSeverity)
        {
            Bleeding.StopBleeding(part, maxSeverity);
            VitalsChanged?.Invoke(this);
        }

        public void ApplyTourniquet(BodyPart part)
        {
            Bleeding.ApplyTourniquet(part);
            _tourniquetSeconds[(int)part] = Math.Max(_tourniquetSeconds[(int)part], 1f);
            VitalsChanged?.Invoke(this);
        }

        public BodyPart GetMostInjuredLimb()
        {
            BodyPart best = BodyPart.Torso;
            float highest = -1f;
            for (int i = 0; i < _limbs.Length; i++)
            {
                float score = _limbs[i].DamagePercent;
                if (_limbs[i].IsBroken)
                    score += 0.35f;
                if (Bleeding.GetWorstBleedingSeverity(_limbs[i].Part) != BleedSeverity.None)
                    score += 0.5f;

                if (score > highest)
                {
                    highest = score;
                    best = _limbs[i].Part;
                }
            }

            return best;
        }

        public void NotifyMedicalApplied(MedicalItemType type)
        {
            MedicalApplied?.Invoke(this, type);
            VitalsChanged?.Invoke(this);
        }

        public void OnBloodLoss(float amountMl)
        {
            if (amountMl <= 0f)
                return;

            float lowBloodStress = 1f - Bleeding.BloodNormalized;
            AddPain(amountMl * 0.015f * (1f + lowBloodStress));
            if (Bleeding.BloodVolumeMl <= Config.UnconsciousBloodMl)
                Consciousness.SetUnconscious(3f + lowBloodStress * 8f);
        }

        public void RequestDeath(DeathCause cause)
        {
            if (_deathRequested)
                return;

            _deathRequested = true;
            LastDeathCause = cause;
            Consciousness.Kill();
            KillInGame(cause);
            Died?.Invoke(this, cause);
            VitalsChanged?.Invoke(this);
        }

        protected virtual void KillInGame(DeathCause cause)
        {
            if (Kind == HealthOwnerKind.Player)
                MainMod.Runtime?.KillPlayer(cause);
        }

        private void UpdateTourniquetDamage(float deltaTime)
        {
            for (int i = 0; i < _tourniquetSeconds.Length; i++)
            {
                if (_tourniquetSeconds[i] <= 0f)
                    continue;

                _tourniquetSeconds[i] += deltaTime;
                if (_tourniquetSeconds[i] > 60f)
                {
                    LimbHealth limb = _limbs[i];
                    float damage = deltaTime * 0.55f * Config.Clamp((_tourniquetSeconds[i] - 60f) / 120f, 0.1f, 1f);
                    limb.ApplyDamage(new DamageInfo(
                        limb.Part,
                        AdvancedDamageType.Blunt,
                        damage,
                        0f,
                        0f,
                        damage * 0.3f,
                        0f,
                        UnityEngine.Vector3.zero,
                        UnityEngine.Vector3.zero,
                        OwnerId,
                        0,
                        UnityEngine.Time.time), Random);
                }
            }
        }

        private bool IsDuplicateDamage(DamageInfo info)
        {
            if (info.AttackOrder == 0 && info.SourceId == 0)
                return false;

            float timeDelta = Math.Abs(info.Time - _lastDamageTime);
            if (info.DamageType == AdvancedDamageType.Fall && info.SourceId == _lastSourceId)
            {
                if (timeDelta < 0.18f && info.BodyPart == _lastPart)
                    return true;
                if (timeDelta < 0.075f)
                    return true;
            }

            return timeDelta < 0.035f &&
                   info.SourceId == _lastSourceId &&
                   info.AttackOrder == _lastAttackOrder &&
                   info.BodyPart == _lastPart;
        }

        private void EvaluateTraumaDeath()
        {
            if (GetLimb(BodyPart.Head).Hp <= 0f)
            {
                RequestDeath(DeathCause.CriticalHeadDamage);
                return;
            }

            if (GetLimb(BodyPart.Torso).Hp <= 0f)
            {
                RequestDeath(DeathCause.CriticalTorsoDamage);
                return;
            }

            if (TotalTraumaNormalized >= 0.995f)
                RequestDeath(DeathCause.Trauma);
        }

        private void RecalculateTrauma()
        {
            float max = 0f;
            float current = 0f;
            for (int i = 0; i < _limbs.Length; i++)
            {
                max += _limbs[i].MaxHp;
                current += _limbs[i].Hp;
            }

            TotalTraumaNormalized = max <= 0f ? 0f : Config.Clamp(1f - current / max, 0f, 1f);
        }

        private void OnLimbFractureChanged(LimbHealth limb, FractureState state)
        {
            VitalsChanged?.Invoke(this);
        }

        private void OnBleedingChanged(BleedingSystem bleeding)
        {
            VitalsChanged?.Invoke(this);
        }
    }
}
