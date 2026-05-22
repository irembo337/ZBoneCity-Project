using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class NativeBloodIntegration
    {
        private struct BloodPool
        {
            public GameObject? GameObject;
            public Transform? Transform;
            public Renderer? Renderer;
            public float Age;
            public float Lifetime;
            public float Size;
            public bool Active;
        }

        private readonly ParticleSystem[] _sprays = new ParticleSystem[16];
        private readonly ParticleSystem[] _nativeSprays = new ParticleSystem[8];
        private readonly BloodPool[] _pools = new BloodPool[72];
        private Material? _bloodMaterial;
        private Mesh? _poolMesh;
        private ParticleSystem? _nativeTemplate;
        private bool _initialized;
        private bool _usingNative;
        private int _nextSpray;
        private int _nextNative;
        private int _nextPool;

        public bool UsingNativeBlood => _usingNative;

        public void Reset()
        {
            if (!_initialized)
                return;

            for (int i = 0; i < _sprays.Length; i++)
            {
                if (_sprays[i] != null)
                    _sprays[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            for (int i = 0; i < _nativeSprays.Length; i++)
            {
                if (_nativeSprays[i] != null)
                    _nativeSprays[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            for (int i = 0; i < _pools.Length; i++)
            {
                GameObject? go = _pools[i].GameObject;
                if (go != null)
                    go.SetActive(false);
                _pools[i].Active = false;
            }
        }

        public void Update(float deltaTime)
        {
            if (!_initialized)
                return;

            for (int i = 0; i < _pools.Length; i++)
            {
                if (!_pools[i].Active || _pools[i].GameObject == null || _pools[i].Transform == null || _pools[i].Renderer == null)
                    continue;

                BloodPool pool = _pools[i];
                GameObject go = pool.GameObject!;
                Transform transform = pool.Transform!;
                Renderer renderer = pool.Renderer!;
                pool.Age += deltaTime;
                if (pool.Age >= pool.Lifetime)
                {
                    pool.Active = false;
                    go.SetActive(false);
                    _pools[i] = pool;
                    continue;
                }

                float ageN = Config.Clamp(pool.Age / Math.Max(1f, pool.Lifetime), 0f, 1f);
                float size = Mathf.Min(pool.Size * 1.65f, pool.Size + pool.Age * 0.018f);
                transform.localScale = new Vector3(size, size, 1f);
                Color color = renderer.material.color;
                color.a = Mathf.Lerp(0.88f, 0.28f, ageN);
                renderer.material.color = color;
                _pools[i] = pool;
            }
        }

        public void EmitImpact(Vector3 position, Vector3 direction, float intensity, bool arterial)
        {
            EnsureInitialized();
            intensity = Config.Clamp(intensity * Math.Max(0.15f, Config.BloodFxDensity), 0.05f, 2.2f);
            Vector3 sprayDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.down;
            if (_usingNative)
                EmitNative(position, sprayDirection, intensity, arterial);
            EmitFallbackSpray(position, sprayDirection, intensity, arterial);
            TryPlacePool(position, intensity, true);
        }

        public void EmitTrail(Vector3 position, float intensity)
        {
            EnsureInitialized();
            intensity = Config.Clamp(intensity * Math.Max(0.15f, Config.BloodFxDensity), 0.05f, 1.5f);
            EmitFallbackSpray(position + Vector3.up * 0.15f, Vector3.down, intensity * 0.45f, false);
            TryPlacePool(position, intensity, false);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _poolMesh = CreateDiscMesh(28);
            Shader shader = Shader.Find("Sprites/Default");
            _bloodMaterial = new Material(shader != null ? shader : Shader.Find("Unlit/Color"));
            _bloodMaterial.color = new Color(0.23f, 0f, 0.012f, 0.88f);

            DiscoverNativeTemplate();
            CreateFallbackSprays();
            CreatePools();
            _initialized = true;
        }

        private void DiscoverNativeTemplate()
        {
            if (!Config.NativeBloodEnabled)
                return;

            try
            {
                ParticleSystem[] systems = Resources.FindObjectsOfTypeAll<ParticleSystem>();
                for (int i = 0; i < systems.Length; i++)
                {
                    ParticleSystem ps = systems[i];
                    if (ps == null || ps.gameObject == null)
                        continue;

                    string name = ps.gameObject.name;
                    if (string.IsNullOrEmpty(name))
                        continue;

                    if (name.IndexOf("blood", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("bleed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("gore", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _nativeTemplate = ps;
                        break;
                    }
                }

                if (_nativeTemplate == null)
                    return;

                for (int i = 0; i < _nativeSprays.Length; i++)
                {
                    GameObject go = UnityEngine.Object.Instantiate(_nativeTemplate.gameObject);
                    go.name = "AHS_NativeBlood_" + i;
                    go.SetActive(false);
                    ParticleSystem ps = go.GetComponent<ParticleSystem>();
                    if (ps != null)
                        _nativeSprays[i] = ps;
                }

                _usingNative = _nativeSprays[0] != null;
                if (_usingNative && Config.DebugMode)
                    MainMod.Runtime?.Logger.Msg("Native blood template discovered: " + _nativeTemplate.gameObject.name);
            }
            catch (Exception ex)
            {
                _usingNative = false;
                MainMod.Runtime?.Logger.Warning("Native blood discovery failed: " + ex.Message);
            }
        }

        private void CreateFallbackSprays()
        {
            for (int i = 0; i < _sprays.Length; i++)
            {
                GameObject go = new GameObject("AHS_BloodSpray_" + i);
                ParticleSystem ps = go.AddComponent<ParticleSystem>();
                ps.playOnAwake = false;
                ps.loop = false;
                ps.startColor = new Color(0.46f, 0f, 0.018f, 1f);
                ps.startLifetime = 1.2f;
                ps.startSpeed = 1.8f;
                ps.startSize = 0.032f;
                ps.gravityModifier = 1.2f;
                ps.maxParticles = 96;
                ps.emissionRate = 0f;
                _sprays[i] = ps;
            }
        }

        private void CreatePools()
        {
            for (int i = 0; i < _pools.Length; i++)
            {
                GameObject go = new GameObject("AHS_RoundBloodPool_" + i);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                filter.mesh = _poolMesh;
                renderer.material = _bloodMaterial;
                go.SetActive(false);
                _pools[i] = new BloodPool
                {
                    GameObject = go,
                    Transform = go.transform,
                    Renderer = renderer,
                    Lifetime = 180f
                };
            }
        }

        private void EmitNative(Vector3 position, Vector3 direction, float intensity, bool arterial)
        {
            ParticleSystem ps = _nativeSprays[_nextNative++ % _nativeSprays.Length];
            if (ps == null)
                return;

            GameObject go = ps.gameObject;
            go.SetActive(true);
            go.transform.position = position;
            go.transform.rotation = direction.sqrMagnitude > 0.01f ? Quaternion.LookRotation(direction.normalized) : Quaternion.identity;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Emit(Mathf.Clamp(Mathf.RoundToInt((arterial ? 22 : 10) * intensity), 4, 72));
        }

        private void EmitFallbackSpray(Vector3 position, Vector3 direction, float intensity, bool arterial)
        {
            ParticleSystem ps = _sprays[_nextSpray++ % _sprays.Length];
            if (ps == null)
                return;

            Transform transform = ps.transform;
            transform.position = position;
            transform.rotation = direction.sqrMagnitude > 0.01f ? Quaternion.LookRotation(direction.normalized) : Quaternion.identity;
            ps.startSize = Mathf.Lerp(0.018f, arterial ? 0.072f : 0.048f, Config.Clamp(intensity, 0f, 1f));
            ps.startSpeed = Mathf.Lerp(0.55f, arterial ? 4.5f : 2.4f, Config.Clamp(intensity, 0f, 1f));
            ps.Emit(Mathf.Clamp(Mathf.RoundToInt((arterial ? 18 : 6) * intensity), 2, arterial ? 90 : 42));
        }

        private void TryPlacePool(Vector3 position, float intensity, bool impact)
        {
            RaycastHit hit;
            if (!Physics.Raycast(position + Vector3.up * 0.35f, Vector3.down, out hit, 2.8f, ~0, QueryTriggerInteraction.Ignore))
                return;

            int index = _nextPool++ % _pools.Length;
            BloodPool pool = _pools[index];
            if (pool.GameObject == null || pool.Transform == null || pool.Renderer == null)
                return;

            pool.GameObject.SetActive(true);
            pool.Transform.position = hit.point + hit.normal * 0.004f;
            pool.Transform.rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            pool.Size = Mathf.Lerp(0.05f, impact ? 0.33f : 0.22f, Config.Clamp(intensity, 0f, 1f));
            pool.Transform.localScale = new Vector3(pool.Size, pool.Size, 1f);
            pool.Age = 0f;
            pool.Lifetime = Mathf.Lerp(70f, 240f, Config.Clamp(intensity, 0f, 1f));
            pool.Active = true;
            Color color = pool.Renderer.material.color;
            color.a = Mathf.Lerp(0.48f, 0.88f, Config.Clamp(intensity, 0f, 1f));
            pool.Renderer.material.color = color;
            _pools[index] = pool;
        }

        private static Mesh CreateDiscMesh(int segments)
        {
            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[segments + 1];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                vertices[i + 1] = new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f);
            }

            for (int i = 0; i < segments; i++)
            {
                int tri = i * 3;
                triangles[tri] = 0;
                triangles[tri + 1] = i + 1;
                triangles[tri + 2] = i == segments - 1 ? 1 : i + 2;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
