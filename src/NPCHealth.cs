using System;
using Il2CppSLZ.Bonelab;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class NPCHealth : HealthManager
    {
        private readonly Enemy_Health? _enemyHealth;
        private bool _appliedUnconscious;

        public Enemy_Health? EnemyHealth => _enemyHealth;

        public NPCHealth(Enemy_Health enemyHealth)
            : base(HealthOwnerKind.Npc, enemyHealth != null ? enemyHealth.GetInstanceID() : 0)
        {
            _enemyHealth = enemyHealth;
        }

        public void SyncFromGameHealth()
        {
            if (_enemyHealth == null)
                return;

            if (!_enemyHealth.alive && !IsDead)
                RequestDeath(DeathCause.Trauma);
        }

        public void ApplyConsciousnessToGame(ConsciousnessState state)
        {
            if (_enemyHealth == null || IsDead)
                return;

            if (state == ConsciousnessState.Unconscious && !_appliedUnconscious)
            {
                _appliedUnconscious = true;
                TryStaggerNpc();
            }
            else if (state == ConsciousnessState.Awake || state == ConsciousnessState.Blackout)
            {
                _appliedUnconscious = false;
            }
        }

        protected override void KillInGame(DeathCause cause)
        {
            if (_enemyHealth == null)
                return;

            try
            {
                if (_enemyHealth.alive)
                    _enemyHealth.DIE();
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("NPC DIE call failed: " + ex.Message);
            }
        }

        private void TryStaggerNpc()
        {
            Enemy_Health? enemyHealth = _enemyHealth;
            if (enemyHealth == null)
                return;

            try
            {
                enemyHealth.Reaction(999f);
                Rigidbody rb = enemyHealth.rb_enemyBody;
                if (rb != null)
                {
                    rb.isKinematic = false;
                    rb.WakeUp();
                    rb.AddForce(Vector3.down * 4.5f, ForceMode.VelocityChange);
                    rb.AddTorque(UnityEngine.Random.insideUnitSphere * 3.0f, ForceMode.VelocityChange);
                }
            }
            catch (Exception ex)
            {
                MainMod.Runtime?.Logger.Warning("NPC unconsciousness reaction failed: " + ex.Message);
            }
        }
    }
}
