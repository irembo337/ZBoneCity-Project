using System;

namespace BonelabAdvancedHealth
{
    public enum DeathCause
    {
        Trauma = 0,
        BloodLoss = 1,
        CriticalHeadDamage = 2,
        CriticalTorsoDamage = 3
    }

    public class HealthManager
    {
        private readonly LimbHealth[] _limbs;
        private float _lastDamageTime;
        private int _lastSourceId;
        private int _lastAttackOrder;
        private BodyPart _lastPart;
        private float _morphineSeconds;
        private bool _deathRequested;

        public HealthOwnerKind Kind { get; }
        public int OwnerId { get; }
        public Random Random { get; }
        public BleedingSystem Bleeding { get; }
        public ConsciousnessSystem Consciousness { get; }
        public FractureSystem Fractures { get; }
        public float Pain { get; private set; }
        public float PainNormalized => Config.Clamp(Pain / 100f, 0f, 1f);
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
            Bleeding = new BleedingSystem(this);
            Consciousness = new ConsciousnessSystem(this);
            Fractures = new FractureSystem(this);
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
            _morphineSeconds = 0f;
            Pain = 0f;
            TotalTraumaNormalized = 0f;
            LastDeathCause = DeathCause.Trauma;

            for (int i = 0; i < _limbs.Length; i++)
                _limbs[i].Reset();

            Bleeding.Reset();
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

            LimbHealth limb = GetLimb(info.BodyPart);
            float damageApplied = limb.ApplyDamage(info, Random);
            if (damageApplied <= 0f)
                return false;

            AddPain(info.Pain * limb.PainMultiplier);
            BleedSeverity severity = DamageProcessor.GetBleedSeverity(info, limb);
            Bleeding.AddBleed(info, limb, severity);
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
                Pain = Math.Max(0f, Pain - deltaTime * 4.8f);
            }
            else
            {
                Pain = Math.Max(0f, Pain - deltaTime * 1.1f);
            }

            Bleeding.Update(deltaTime);
            Fractures.Update(deltaTime);
            Consciousness.Update(deltaTime);
            VitalsChanged?.Invoke(this);
        }

        public void AddPain(float amount)
        {
            if (amount <= 0f || IsDead)
                return;

            float morphineReduction = _morphineSeconds > 0f ? 0.45f : 1f;
            Pain = Config.Clamp(Pain + amount * 0.32f * morphineReduction, 0f, 135f);
        }

        public void ReducePain(float amount, float morphineSeconds)
        {
            if (amount > 0f)
                Pain = Math.Max(0f, Pain - amount);
            if (morphineSeconds > 0f)
                _morphineSeconds = Math.Max(_morphineSeconds, morphineSeconds);
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

        private bool IsDuplicateDamage(DamageInfo info)
        {
            if (info.AttackOrder == 0 && info.SourceId == 0)
                return false;

            float timeDelta = Math.Abs(info.Time - _lastDamageTime);
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

            if (TotalTraumaNormalized >= 0.985f)
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
