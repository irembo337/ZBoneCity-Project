using System.Collections.Generic;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class BloodFXSystem
    {
        private struct BloodDecal
        {
            public GameObject GameObject;
            public Transform Transform;
            public Renderer Renderer;
            public float Age;
            public float Lifetime;
            public float Size;
            public bool Active;
        }

        private struct BloodEmitter
        {
            public GameObject GameObject;
            public Transform Transform;
            public ParticleSystem Particles;
            public float Age;
            public bool Active;
        }

        private readonly BloodDecal[] _decals = new BloodDecal[Config.MaxBloodDecals];
        private readonly BloodEmitter[] _emitters = new BloodEmitter[Config.MaxBloodParticles];
        private Material? _bloodMaterial;
        private float _playerTrailTimer;
        private Vector3 _lastPlayerTrailPosition;
        private bool _initialized;
        private int _nextDecal;
        private int _nextEmitter;

        public void Reset()
        {
            EnsureInitialized();
            for (int i = 0; i < _decals.Length; i++)
            {
                if (_decals[i].GameObject != null)
                    _decals[i].GameObject.SetActive(false);
                _decals[i].Active = false;
            }

            for (int i = 0; i < _emitters.Length; i++)
            {
                if (_emitters[i].GameObject != null)
                {
                    _emitters[i].Particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    _emitters[i].GameObject.SetActive(false);
                }
                _emitters[i].Active = false;
            }

            _playerTrailTimer = 0f;
            _lastPlayerTrailPosition = Vector3.zero;
        }

        public void Update(float deltaTime, HealthManager? player, IEnumerable<NPCHealth> npcs)
        {
            if (!Config.BloodFxEnabled)
                return;

            EnsureInitialized();
            UpdateDecals(deltaTime);
            UpdateEmitters(deltaTime);

            if (player != null)
                UpdateActorBlood(deltaTime, player, GetPlayerBloodPosition(), true);

            foreach (NPCHealth npc in npcs)
                UpdateActorBlood(deltaTime, npc, npc.GetEffectPosition(), false);
        }

        public void OnDamage(HealthManager manager, DamageInfo info, OrganDamageFeedback organFeedback)
        {
            if (!Config.BloodFxEnabled)
                return;

            EnsureInitialized();
            bool shouldBleed = organFeedback.BleedSeverity != BleedSeverity.None ||
                               info.DamageType == AdvancedDamageType.Bullet ||
                               info.DamageType == AdvancedDamageType.Stab;
            if (!shouldBleed)
                return;

            Vector3 origin = info.Origin;
            if (origin.sqrMagnitude < 0.001f)
                origin = manager.Kind == HealthOwnerKind.Player ? GetPlayerBloodPosition() : GetNpcPosition(manager);

            SpawnDrip(origin, GetBloodDirection(info), GetIntensity(info, organFeedback));
            TrySpawnSurfaceBlood(origin, GetIntensity(info, organFeedback), true);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            Shader shader = Shader.Find("Sprites/Default");
            _bloodMaterial = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            _bloodMaterial.color = new Color(0.32f, 0.0f, 0.015f, 0.86f);

            for (int i = 0; i < _decals.Length; i++)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                go.name = "AHS_BloodPool_" + i;
                Collider col = go.GetComponent<Collider>();
                if (col != null)
                    Object.Destroy(col);
                Renderer renderer = go.GetComponent<Renderer>();
                renderer.material = _bloodMaterial;
                go.SetActive(false);
                _decals[i] = new BloodDecal
                {
                    GameObject = go,
                    Transform = go.transform,
                    Renderer = renderer,
                    Lifetime = 120f
                };
            }

            for (int i = 0; i < _emitters.Length; i++)
            {
                GameObject go = new GameObject("AHS_BloodDrip_" + i);
                ParticleSystem ps = go.AddComponent<ParticleSystem>();
                ps.playOnAwake = false;
                ps.loop = false;
                ps.startColor = new Color(0.45f, 0.0f, 0.018f, 1f);
                ps.startLifetime = 1.25f;
                ps.startSpeed = 1.35f;
                ps.startSize = 0.035f;
                ps.gravityModifier = 1.4f;
                ps.maxParticles = 48;
                ps.emissionRate = 0f;
                go.SetActive(false);
                _emitters[i] = new BloodEmitter
                {
                    GameObject = go,
                    Transform = go.transform,
                    Particles = ps
                };
            }

            _initialized = true;
        }

        private void UpdateActorBlood(float deltaTime, HealthManager manager, Vector3 position, bool isPlayer)
        {
            if (position.sqrMagnitude < 0.001f || manager.IsDead)
                return;

            float rate = manager.Bleeding.TotalBleedRateMlPerSecond;
            if (rate <= 2.0f)
                return;

            float intensity = Config.Clamp(rate / 55f, 0.08f, 1f);
            if (rate >= 8f)
                SpawnDrip(position + Vector3.up * 0.55f, Vector3.down, intensity);

            float interval = Mathf.Lerp(1.1f, 0.16f, intensity) / Config.BloodFxDensity;
            if (isPlayer)
            {
                _playerTrailTimer += deltaTime;
                float moved = (_lastPlayerTrailPosition == Vector3.zero ? 999f : (position - _lastPlayerTrailPosition).magnitude);
                if (_playerTrailTimer >= interval && moved > 0.18f)
                {
                    _playerTrailTimer = 0f;
                    _lastPlayerTrailPosition = position;
                    TrySpawnSurfaceBlood(position, intensity, false);
                }
            }
            else if (Random.value < deltaTime / interval)
            {
                TrySpawnSurfaceBlood(position, intensity, false);
            }
        }

        private void UpdateDecals(float deltaTime)
        {
            for (int i = 0; i < _decals.Length; i++)
            {
                if (!_decals[i].Active)
                    continue;

                BloodDecal decal = _decals[i];
                decal.Age += deltaTime;
                if (decal.Age >= decal.Lifetime)
                {
                    decal.Active = false;
                    decal.GameObject.SetActive(false);
                    _decals[i] = decal;
                    continue;
                }

                float grow = Mathf.Min(decal.Size + deltaTime * 0.012f, decal.Size * 1.55f);
                decal.Transform.localScale = new Vector3(grow, grow, 1f);
                Color color = decal.Renderer.material.color;
                color.a = Mathf.Lerp(0.86f, 0.38f, decal.Age / decal.Lifetime);
                decal.Renderer.material.color = color;
                _decals[i] = decal;
            }
        }

        private void UpdateEmitters(float deltaTime)
        {
            for (int i = 0; i < _emitters.Length; i++)
            {
                if (!_emitters[i].Active)
                    continue;

                BloodEmitter emitter = _emitters[i];
                emitter.Age += deltaTime;
                if (emitter.Age >= 1.4f)
                {
                    emitter.Active = false;
                    emitter.Particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                    emitter.GameObject.SetActive(false);
                    _emitters[i] = emitter;
                }
            }
        }

        private void SpawnDrip(Vector3 position, Vector3 direction, float intensity)
        {
            int index = _nextEmitter++ % _emitters.Length;
            BloodEmitter emitter = _emitters[index];
            emitter.GameObject.SetActive(true);
            emitter.Transform.position = position;
            emitter.Transform.rotation = direction.sqrMagnitude > 0.01f ? Quaternion.LookRotation(direction.normalized) : Quaternion.identity;
            emitter.Age = 0f;
            emitter.Active = true;
            emitter.Particles.startSize = Mathf.Lerp(0.018f, 0.055f, intensity);
            emitter.Particles.startSpeed = Mathf.Lerp(0.45f, 2.2f, intensity);
            emitter.Particles.Emit(Mathf.Clamp(Mathf.RoundToInt(4 + intensity * 18f * Config.BloodFxDensity), 2, 28));
            _emitters[index] = emitter;
        }

        private void TrySpawnSurfaceBlood(Vector3 position, float intensity, bool impact)
        {
            RaycastHit hit;
            if (!Physics.Raycast(position + Vector3.up * 0.35f, Vector3.down, out hit, 2.5f, ~0, QueryTriggerInteraction.Ignore))
                return;

            int index = _nextDecal++ % _decals.Length;
            BloodDecal decal = _decals[index];
            decal.GameObject.SetActive(true);
            decal.Transform.position = hit.point + hit.normal * 0.006f;
            decal.Transform.rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            decal.Size = Mathf.Lerp(0.08f, impact ? 0.38f : 0.24f, intensity);
            decal.Transform.localScale = new Vector3(decal.Size, decal.Size, 1f);
            decal.Age = 0f;
            decal.Lifetime = Mathf.Lerp(75f, 240f, intensity);
            decal.Active = true;
            Color color = decal.Renderer.material.color;
            color.a = Mathf.Lerp(0.58f, 0.92f, intensity);
            decal.Renderer.material.color = color;
            _decals[index] = decal;
        }

        private static Vector3 GetBloodDirection(DamageInfo info)
        {
            if (info.Direction.sqrMagnitude > 0.01f)
                return info.Direction.normalized;
            return Vector3.down;
        }

        private static float GetIntensity(DamageInfo info, OrganDamageFeedback feedback)
        {
            float intensity = Config.Clamp(info.Damage / 75f, 0.08f, 1f);
            if (feedback.BleedSeverity == BleedSeverity.Arterial)
                intensity = Mathf.Max(intensity, 0.9f);
            else if (feedback.BleedSeverity == BleedSeverity.Severe)
                intensity = Mathf.Max(intensity, 0.65f);
            return intensity;
        }

        private static Vector3 GetPlayerBloodPosition()
        {
            MainMod? runtime = MainMod.Runtime;
            if (runtime != null && runtime.TryGetPlayerFeetPosition(out Vector3 feet))
                return feet;

            Transform? head = runtime?.GetHeadTransform();
            if (head != null)
                return head.position + Vector3.down * 1.2f;
            return Vector3.zero;
        }

        private static Vector3 GetNpcPosition(HealthManager manager)
        {
            if (manager is NPCHealth npc)
                return npc.GetEffectPosition();
            return Vector3.zero;
        }
    }
}
