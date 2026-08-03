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
        OrganFailure = 7,
        NeckTrauma = 8
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
        private bool _hasReceivedRealDamage;

        public HealthOwnerKind Kind { get; }
        public int OwnerId { get; }
        public Random Random { get; }
        public BleedingSystem Bleeding { get; }
        public ConsciousnessSystem Consciousness { get; }
        public ComaSystem Coma { get; }
        public FractureSystem Fractures { get; }
        public BoneSystem Bones { get; }
        public OrganSystem Organs { get; }
        public LungDamageSystem Lungs { get; }
        public BrainTraumaSystem Brain { get; }
        public AudioTraumaSystem AudioTrauma { get; }
        public PainSystem PainSystem { get; }
        public KnifePenetrationSystem KnifePenetration { get; }
        public RealisticTraumaSystem Trauma { get; }
        public ShockSystem Shock { get; }
        public StressSystem Stress { get; }
        public TacticalVitalsSystem TacticalVitals { get; }
        public ZCityOrganismProfile ZCityOrganism { get; }
        public MedicalTelemetrySystem Telemetry { get; }
        public WeaponHandlingSystem WeaponHandling { get; }
        public NeckTraumaSystem Neck { get; }
        public MedicationEffectSystem Medication { get; }
        public RehabilitationSystem Rehabilitation { get; }
        public float Pain => Config.Clamp(PainSystem.TotalPain + Rehabilitation.ResidualPain, 0f, 145f);
        public float PainNormalized => Config.Clamp(Pain / 100f, 0f, 1f);
        public float AwarenessPenalty => Config.Clamp(_morphineAwarenessPenalty + Medication.ReactionPenalty + Brain.DisorientationNormalized * 0.35f + Lungs.OxygenStress * 0.45f, 0f, 1f);
        public float AdrenalineNormalized => Math.Max(_adrenalineSeconds > 0f ? Config.Clamp(_adrenalineSeconds / 45f, 0f, 1f) : 0f, Medication.StimulantNormalized);
        public float PulseModifier => Organs.HeartbeatStrength + Stress.Normalized * 0.18f + Medication.TachycardiaBpm / 180f;
        public MedicalTelemetrySnapshot TelemetrySnapshot => Telemetry.Snapshot;
        public float PulseBpm => Telemetry.Snapshot.PulseBpm;
        public float BloodPressureSystolic => Telemetry.Snapshot.SystolicPressure;
        public float BloodPressureDiastolic => Telemetry.Snapshot.DiastolicPressure;
        public float InfectionRisk => Telemetry.Snapshot.InfectionRisk;
        public float InternalBleedingNormalized => Telemetry.Snapshot.InternalBleedingNormalized;
        public bool IsDead => Consciousness.State == ConsciousnessState.Dead || _deathRequested;
        public bool HasReceivedRealDamage => _hasReceivedRealDamage;
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
            Coma = new ComaSystem(this);
            Bones = new BoneSystem(this);
            Fractures = new FractureSystem(this);
            Organs = new OrganSystem(this);
            Lungs = new LungDamageSystem(this);
            Brain = new BrainTraumaSystem(this);
            AudioTrauma = new AudioTraumaSystem(this);
            PainSystem = new PainSystem(this);
            KnifePenetration = new KnifePenetrationSystem(this);
            Trauma = new RealisticTraumaSystem(this);
            Shock = new ShockSystem(this);
            Stress = new StressSystem(this);
            TacticalVitals = new TacticalVitalsSystem(this);
            ZCityOrganism = new ZCityOrganismProfile(this);
            Telemetry = new MedicalTelemetrySystem(this);
            WeaponHandling = new WeaponHandlingSystem(this);
            Neck = new NeckTraumaSystem(this);
            Medication = new MedicationEffectSystem(this);
            Rehabilitation = new RehabilitationSystem(this);
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
            Coma.Reset();
            Bones.Reset();
            Organs.Reset();
            Lungs.Reset();
            Brain.Reset();
            PainSystem.Reset();
            KnifePenetration.Reset();
            Trauma.Reset();
            Shock.Reset();
            Stress.Reset();
            TacticalVitals.Reset();
            ZCityOrganism.Reset();
            Telemetry.Reset();
            WeaponHandling.Reset();
            Neck.Reset();
            Medication.Reset();
            Rehabilitation.Reset();
            AudioTrauma.Reset();
            Fractures.Reset();
            Consciousness.Reset();
            RaiseVitalsChanged();
        }

        public void ResetForRespawn()
        {
            Reset();
            ForceSafeSpawnState();
        }

        public bool ApplyDamage(DamageInfo info)
        {
            if (!Config.Enabled || IsDead || info.Damage <= 0f || !IsValidDamage(info) || IsDuplicateDamage(info))
                return false;

            if (Kind == HealthOwnerKind.Player && !TraumaStartupGuard.CanProcessPlayerDamage)
            {
                if (Config.DebugMode)
                    MainMod.Runtime?.Logger.Msg("Ignored player damage while startup guard is active.");
                return false;
            }

            _lastDamageTime = info.Time;
            _lastSourceId = info.SourceId;
            _lastAttackOrder = info.AttackOrder;
            _lastPart = info.BodyPart;
            _lastDamageType = info.DamageType;
            _hasReceivedRealDamage = true;
            info = ApplyExternalCompatibility(info);

            if (Kind == HealthOwnerKind.Player && DamageProcessor.IsFatalPlayerHeadshot(info))
            {
                ApplyInstantFatalHeadshot(info);
                return true;
            }

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
            Trauma.OnDamage(info, organFeedback);
            Lungs.ApplyDamage(info, organFeedback);
            Brain.ApplyDamage(info, organFeedback);
            Shock.OnDamage(info, organFeedback);
            Stress.OnDamage(info, organFeedback);
            TacticalVitals.OnDamage(info, organFeedback);
            ZCityOrganism.OnDamage(info, organFeedback);
            Neck.ApplyDamage(info, organFeedback);
            KnifePenetration.ApplyDamage(info, organFeedback);
            AudioTrauma.OnDamage(info, organFeedback);
            if (Kind == HealthOwnerKind.Player)
                MainMod.Runtime?.NotifyHudDamageVisual(info, organFeedback);
            MainMod.Runtime?.NotifyBloodDamage(this, info, organFeedback);
            MainMod.Runtime?.NotifyForensicDamage(this, info, organFeedback);
            Consciousness.ApplyDamageImpulse(info);
            ApplyLimbShotCollapseGate(info, limb);
            RecalculateTrauma();
            RaiseDamageProcessed(info);
            MainMod.Runtime?.NotifyFusionDamage(this, info);
            RaiseVitalsChanged();
            EvaluateTraumaDeath();
            return true;
        }

        private DamageInfo ApplyExternalCompatibility(DamageInfo info)
        {
            float fractureDamping = MainMod.Runtime?.ExternalMods.GetExternalFractureDamping(Kind) ?? 1f;
            if (Math.Abs(fractureDamping - 1f) < 0.001f)
                return info;

            return new DamageInfo(
                info.BodyPart,
                info.DamageType,
                info.Damage,
                info.BleedFactor,
                info.FractureChance * fractureDamping,
                info.Pain,
                info.UnconsciousnessImpulse,
                info.Origin,
                info.Direction,
                info.SourceId,
                info.AttackOrder,
                info.Time,
                info.SourceCollider,
                info.Context,
                info.ImpactVelocity);
        }

        private void ApplyLimbShotCollapseGate(DamageInfo info, LimbHealth limb)
        {
            if (Kind != HealthOwnerKind.Player || info.DamageType != AdvancedDamageType.Bullet)
                return;
            if (info.BodyPart != BodyPart.LeftArm && info.BodyPart != BodyPart.RightArm)
                return;
            if (limb.BulletHitCount < 8)
                return;
            if (!limb.IsBroken && limb.DamagePercent < 0.72f)
                return;

            Shock.OnDamage(new DamageInfo(info.BodyPart, AdvancedDamageType.Bullet, 8f, 0f, 0f, 18f, 0.12f, info.Origin, info.Direction, info.SourceId, info.AttackOrder, info.Time), OrganDamageFeedback.None);
        }

        public void UpdateSystems(float deltaTime)
        {
            if (!Config.Enabled)
                return;

            if (Kind == HealthOwnerKind.Player && !TraumaStartupGuard.CanRunPlayerTrauma)
                return;

            if (IsDead)
            {
                Consciousness.Update(deltaTime);
                AudioTrauma.Update(deltaTime);
                ZCityOrganism.Update(deltaTime);
                Telemetry.Update(deltaTime);
                RaiseVitalsChanged();
                return;
            }

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
            Medication.Update(deltaTime);
            Rehabilitation.Update(deltaTime);

            Bleeding.Update(deltaTime);
            if (Config.OrganSystemEnabled)
                Organs.Update(deltaTime);
            Lungs.Update(deltaTime);
            Brain.Update(deltaTime);
            PainSystem.Update(deltaTime, _morphineSeconds > 0f || Medication.MorphineActive, _adrenalineSeconds > 0f || Medication.StimulantActive, _adrenalineCrashSeconds > 0f || Medication.CrashActive, Organs.OrganPainModifier);
            Shock.Update(deltaTime);
            Stress.Update(deltaTime);
            TacticalVitals.Update(deltaTime);
            ZCityOrganism.Update(deltaTime);
            if (ZCityOrganism.NeedUnconsciousness &&
                (Kind != HealthOwnerKind.Player || _hasReceivedRealDamage) &&
                Consciousness.State == ConsciousnessState.Awake &&
                !Coma.IsActive &&
                (ZCityOrganism.HeartStop ||
                 Bleeding.BloodVolumeMl <= Config.CriticalBloodMl ||
                 Shock.Intensity >= Config.UnconsciousShockThreshold ||
                 Pain >= Config.UnconsciousPainThreshold ||
                 GetLimb(BodyPart.Head).DamagePercent >= Config.UnconsciousHeadDamageThreshold))
                Consciousness.SetUnconscious(ZCityOrganism.HeartStop ? 10f : 4f);
            Telemetry.Update(deltaTime);
            WeaponHandling.Update(deltaTime);
            Neck.Update(deltaTime);
            KnifePenetration.Update(deltaTime);
            Trauma.Update(deltaTime);
            Fractures.Update(deltaTime);
            Coma.Update(deltaTime);
            Consciousness.Update(deltaTime);
            AudioTrauma.Update(deltaTime);
            RaiseVitalsChanged();
        }

        public void UpdateRecoveryInput(float deltaTime)
        {
            if (!Config.Enabled || Kind != HealthOwnerKind.Player || IsDead)
                return;
            if (!TraumaStartupGuard.CanRunPlayerTrauma)
                return;

            Consciousness.UpdateRecoveryInput(deltaTime);
        }

        public void AddPain(float amount)
        {
            if (amount <= 0f || IsDead)
                return;

            float morphineReduction = _morphineSeconds > 0f ? 0.45f : 1f;
            float adrenalineReduction = _adrenalineSeconds > 0f ? 0.35f : 1f;
            PainSystem.AddPain(amount * morphineReduction * adrenalineReduction * Medication.PainIntakeMultiplier * Organs.OrganPainModifier, _lastPart, _lastDamageType);
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
            RaiseVitalsChanged();
        }

        public void ApplyAdrenaline(float seconds)
        {
            _adrenalineSeconds = Math.Max(_adrenalineSeconds, seconds);
            _adrenalineCrashSeconds = 0f;
            RaiseVitalsChanged();
        }

        public void RestoreBlood(float amountMl)
        {
            float before = Bleeding.BloodVolumeMl;
            Bleeding.RestoreBlood(amountMl);
            ZCityOrganism.RegisterBloodRestore(amountMl);
            Coma.RegisterBloodRestore(amountMl);
            Rehabilitation.RegisterBloodRestoration(before, amountMl);
            RaiseVitalsChanged();
        }

        public void HealLimb(BodyPart part, float amount)
        {
            GetLimb(part).Heal(amount);
            RecalculateTrauma();
            RaiseVitalsChanged();
        }

        public void StabilizeFracture(BodyPart part, float strength)
        {
            FractureState previous = GetLimb(part).Fracture;
            GetLimb(part).StabilizeFracture(strength);
            Bones.StabilizeMostDamaged(strength);
            Rehabilitation.RegisterFractureTreatment(part, previous);
            RaiseVitalsChanged();
        }

        public void RepairFracture(BodyPart part)
        {
            FractureState previous = GetLimb(part).Fracture;
            GetLimb(part).ClearFracture();
            Bones.StabilizeMostDamaged(1.0f);
            Rehabilitation.RegisterFractureTreatment(part, previous);
            RaiseVitalsChanged();
        }

        public void StopBleeding(BodyPart part, BleedSeverity maxSeverity)
        {
            Bleeding.StopBleeding(part, maxSeverity);
            RaiseVitalsChanged();
        }

        public void ApplyBandage(BodyPart part, float seconds, float strength)
        {
            Bleeding.ApplyBandage(part, seconds, strength);
            Coma.RegisterTreatment(MedicalItemType.Bandage);
            RaiseVitalsChanged();
        }

        public void ApplyTourniquet(BodyPart part)
        {
            Bleeding.ApplyTourniquet(part);
            _tourniquetSeconds[(int)part] = Math.Max(_tourniquetSeconds[(int)part], 1f);
            RaiseVitalsChanged();
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
            NotifyMedicalApplied(type, BodyPart.Torso);
        }

        public void NotifyMedicalApplied(MedicalItemType type, BodyPart part)
        {
            Coma.RegisterTreatment(type);
            Shock.RegisterTreatment(type);
            Stress.RegisterTreatment(type);
            TacticalVitals.RegisterTreatment(type);
            ZCityOrganism.RegisterTreatment(type, part);
            Neck.RegisterTreatment(type);
            Medication.RegisterDose(type);
            Rehabilitation.RegisterTreatment(type);
            AudioTrauma.TriggerMedicalUse(type);
            RaiseMedicalApplied(type);
            MainMod.Runtime?.NotifyMedicalBloodContact(this, type, part);
            MainMod.Runtime?.NotifyFusionMedical(this, type, part);
            RaiseVitalsChanged();
        }

        public bool ApplyManualResuscitation(out string feedback)
        {
            feedback = string.Empty;
            if (IsDead)
            {
                feedback = "Patient Dead";
                return false;
            }

            bool needsCpr = Consciousness.State == ConsciousnessState.Unconscious ||
                            Coma.IsActive ||
                            Organs.CardiacArrestActive ||
                            Lungs.OxygenNormalized < 0.68f ||
                            Shock.Intensity > 0.58f;
            if (!needsCpr)
            {
                feedback = "Patient Stable";
                return false;
            }

            Lungs.Treat(0.10f);
            Shock.RegisterTreatment(MedicalItemType.Medkit);
            Stress.RegisterTreatment(MedicalItemType.Medkit);
            Coma.RegisterManualResuscitation(0.22f, 7.5f);
            ReducePain(2.5f, 0f);

            bool canRecover = Consciousness.State == ConsciousnessState.Unconscious &&
                              !Coma.IsActive &&
                              Bleeding.BloodVolumeMl > Config.CriticalBloodMl + 180f &&
                              Bleeding.TotalBleedRateMlPerSecond <= 14f &&
                              Lungs.OxygenNormalized > 0.50f &&
                              !Organs.CardiacArrestActive;
            if (canRecover)
            {
                Consciousness.BeginStandUpRecovery(9.5f);
                feedback = "CPR Successful";
            }
            else if (Coma.IsActive)
            {
                feedback = "CPR - Stabilizing";
            }
            else
            {
                feedback = "CPR Applied";
            }

            RaiseMedicalApplied(MedicalItemType.Medkit);
            MainMod.Runtime?.NotifyFusionMedical(this, MedicalItemType.Medkit, BodyPart.Torso);
            RaiseVitalsChanged();
            return true;
        }

        public void OnBloodLoss(float amountMl)
        {
            if (amountMl <= 0f)
                return;
            if (Kind == HealthOwnerKind.Player && !_hasReceivedRealDamage)
                return;

            float lowBloodStress = 1f - Bleeding.BloodNormalized;
            AddPain(amountMl * 0.015f * (1f + lowBloodStress));
            Stress.OnBloodLoss(amountMl);
            if (Bleeding.BloodVolumeMl <= Config.CriticalBloodMl)
                Consciousness.SetUnconscious(3f + lowBloodStress * 8f);
        }

        public void RequestDeath(DeathCause cause)
        {
            if (_deathRequested)
                return;

            if (Kind == HealthOwnerKind.Player && !TraumaStartupGuard.CanRunPlayerTrauma)
            {
                if (Config.DebugMode)
                    MainMod.Runtime?.Logger.Msg("Blocked player death request during spawn protection: " + cause);
                ForceSafeSpawnState();
                return;
            }

            if (Coma.TryEnter(cause))
            {
                LastDeathCause = cause;
                RaiseVitalsChanged();
                return;
            }

            CommitDeath(cause);
        }

        internal void CommitDeath(DeathCause cause)
        {
            if (_deathRequested)
                return;

            _deathRequested = true;
            LastDeathCause = cause;
            Coma.ClearForDeath();
            Consciousness.Kill();
            AudioTrauma.OnDeath(cause);
            MainMod.Runtime?.NotifyDeathSound(this, cause);
            MainMod.Runtime?.NotifyPersistentCorpseDeath(this, cause);
            KillInGame(cause);
            MainMod.Runtime?.NotifyForensicDeath(this, cause);
            RaiseDied(cause);
            MainMod.Runtime?.NotifyFusionDeath(this, cause);
            RaiseVitalsChanged();
        }

        private void ApplyInstantFatalHeadshot(DamageInfo info)
        {
            LimbHealth head = GetLimb(BodyPart.Head);
            DamageInfo fatalInfo = new DamageInfo(
                BodyPart.Head,
                info.DamageType,
                Math.Max(info.Damage, head.Hp + head.MaxHp),
                Math.Max(info.BleedFactor, 3.25f),
                Math.Max(info.FractureChance, 0.98f),
                Math.Max(info.Pain, 160f),
                Math.Max(info.UnconsciousnessImpulse, 4.0f),
                info.Origin,
                info.Direction,
                info.SourceId,
                info.AttackOrder,
                info.Time,
                info.SourceCollider,
                info.Context | DamageContextFlags.FatalHeadshot | DamageContextFlags.HighEnergyImpact,
                info.ImpactVelocity);

            head.ApplyDamage(fatalInfo, Random);
            AddPain(fatalInfo.Pain);
            Bleeding.AddBleed(fatalInfo, head, BleedSeverity.Arterial, 3.0f, 1.15f, WoundSeverity.OrganRupture);
            OrganDamageFeedback organFeedback = Config.OrganSystemEnabled ? Organs.ApplyDamage(fatalInfo) : OrganDamageFeedback.None;
            Brain.ApplyDamage(fatalInfo, organFeedback);
            Stress.OnDamage(fatalInfo, organFeedback);
            TacticalVitals.OnDamage(fatalInfo, organFeedback);
            ZCityOrganism.OnDamage(fatalInfo, organFeedback);
            Neck.ApplyDamage(fatalInfo, organFeedback);
            AudioTrauma.OnDamage(fatalInfo, organFeedback);
            MainMod.Runtime?.NotifyHudDamageVisual(fatalInfo, organFeedback);
            MainMod.Runtime?.NotifyBloodDamage(this, fatalInfo, organFeedback);
            MainMod.Runtime?.NotifyForensicDamage(this, fatalInfo, organFeedback);
            RecalculateTrauma();
            RaiseDamageProcessed(fatalInfo);
            MainMod.Runtime?.NotifyFusionDamage(this, fatalInfo);
            CommitDeath(DeathCause.CriticalHeadDamage);
        }

        public void ForceSafeSpawnState()
        {
            if (Kind != HealthOwnerKind.Player)
                return;

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
            _hasReceivedRealDamage = false;
            TotalTraumaNormalized = 0f;
            LastDeathCause = DeathCause.Trauma;

            for (int i = 0; i < _limbs.Length; i++)
            {
                _limbs[i].Reset();
                _tourniquetSeconds[i] = 0f;
            }

            Bleeding.Reset();
            Coma.Reset();
            Bones.Reset();
            Organs.Reset();
            Lungs.Reset();
            Brain.Reset();
            PainSystem.Reset();
            KnifePenetration.Reset();
            Trauma.Reset();
            Shock.Reset();
            Stress.Reset();
            TacticalVitals.Reset();
            ZCityOrganism.Reset();
            WeaponHandling.Reset();
            Neck.Reset();
            Medication.Reset();
            Rehabilitation.Reset();
            AudioTrauma.Reset();
            Fractures.Reset();
            Consciousness.ForceAwakeForSpawn();
            RaiseVitalsChanged();
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
            if ((info.DamageType == AdvancedDamageType.Bullet || info.DamageType == AdvancedDamageType.Stab) &&
                info.SourceId == _lastSourceId &&
                info.AttackOrder == _lastAttackOrder &&
                timeDelta < 0.055f)
                return true;

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

        private static bool IsValidDamage(DamageInfo info)
        {
            return IsFinite(info.Damage) &&
                   IsFinite(info.BleedFactor) &&
                   IsFinite(info.FractureChance) &&
                   IsFinite(info.Pain) &&
                   IsFinite(info.UnconsciousnessImpulse) &&
                   IsFinite(info.ImpactVelocity) &&
                   IsFinite(info.Origin) &&
                   IsFinite(info.Direction);
        }

        private static bool IsFinite(float value)
        {
            return !(float.IsNaN(value) || float.IsInfinity(value));
        }

        private static bool IsFinite(UnityEngine.Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
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

            float limbOnlyTrauma = (
                GetLimb(BodyPart.LeftArm).DamagePercent +
                GetLimb(BodyPart.RightArm).DamagePercent +
                GetLimb(BodyPart.LeftLeg).DamagePercent +
                GetLimb(BodyPart.RightLeg).DamagePercent) * 0.25f;
            bool vitalTraumaLow = GetLimb(BodyPart.Head).DamagePercent < 0.35f && GetLimb(BodyPart.Torso).DamagePercent < 0.45f;
            if (vitalTraumaLow && limbOnlyTrauma > 0.65f)
                return;

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

        private void RaiseDamageProcessed(DamageInfo info)
        {
            try
            {
                DamageProcessed?.Invoke(this, info);
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("DamageProcessed event failed safely: " + ex.Message);
            }
        }

        private void RaiseVitalsChanged()
        {
            try
            {
                Telemetry.Refresh();
                VitalsChanged?.Invoke(this);
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("VitalsChanged event failed safely: " + ex.Message);
            }
        }

        private void RaiseDied(DeathCause cause)
        {
            try
            {
                Died?.Invoke(this, cause);
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("Died event failed safely: " + ex.Message);
            }
        }

        private void RaiseMedicalApplied(MedicalItemType type)
        {
            try
            {
                MedicalApplied?.Invoke(this, type);
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("MedicalApplied event failed safely: " + ex.Message);
            }
        }

        private void OnLimbFractureChanged(LimbHealth limb, FractureState state)
        {
            if (state != FractureState.None && Config.DebugMode)
                MainMod.Runtime?.Logger.Msg("[ZBC] Fracture Applied: " + MedicalInspectionSystem.GetPartLabel(limb.Part) + " -> " + state);
            if (state != FractureState.None)
            {
                float severity = state == FractureState.Shattered ? 1.0f : state == FractureState.Fractured ? 0.72f : 0.38f;
                AudioTrauma.TriggerFractureSound(severity);
                PainSystem.AddPain(18f + severity * 38f, limb.Part, AdvancedDamageType.Blunt);
                Shock.OnDamage(new DamageInfo(limb.Part, AdvancedDamageType.Blunt, 10f + severity * 24f, 0f, 0f, 12f + severity * 28f, severity * 0.18f, UnityEngine.Vector3.zero, UnityEngine.Vector3.zero, 0, 0, 0f), OrganDamageFeedback.None);
                Stress.RegisterTraumaticEvent(0.45f + severity * 0.45f);
                if (Kind == HealthOwnerKind.Player && MedicalInspectionSystem.IsLeg(limb.Part) && state >= FractureState.Fractured)
                    MainMod.Runtime?.SetPlayerRagdoll(true);
            }
            RaiseVitalsChanged();
        }

        private void OnBleedingChanged(BleedingSystem bleeding)
        {
            RaiseVitalsChanged();
        }
    }
}
