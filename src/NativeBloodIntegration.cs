using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class NativeBloodIntegration
    {
        private enum BloodMarkKind
        {
            Pool,
            WallSplat,
            Trail,
            Footprint,
            Handprint,
            Transfer
        }

        private struct BloodPool
        {
            public GameObject? GameObject;
            public Transform? Transform;
            public Renderer? Renderer;
            public float Age;
            public float Lifetime;
            public float Size;
            public bool Active;
            public BloodMarkKind Kind;
        }

        private readonly ParticleSystem[] _sprays = new ParticleSystem[16];
        private readonly ParticleSystem[] _nativeSprays = new ParticleSystem[8];
        private readonly BloodPool[] _pools = new BloodPool[72];
        private Material? _bloodMaterial;
        private Texture2D? _bloodTexture;
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
                bool elongated = pool.Kind == BloodMarkKind.Footprint || pool.Kind == BloodMarkKind.Handprint || pool.Kind == BloodMarkKind.WallSplat;
                transform.localScale = elongated ? new Vector3(size * 0.65f, size * 1.35f, 1f) : new Vector3(size, size, 1f);
                Color color = renderer.material.color;
                float fade = 1f - Mathf.SmoothStep(0.18f, 1f, ageN);
                color.a = Mathf.Lerp(0f, 0.92f, fade);
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

        public void EmitStandingPool(Vector3 position, float intensity)
        {
            EnsureInitialized();
            intensity = Config.Clamp(intensity * Math.Max(0.15f, Config.BloodFxDensity), 0.08f, 2.0f);
            RaycastHit hit;
            if (!Physics.Raycast(position + Vector3.up * 0.38f, Vector3.down, out hit, 2.2f, ~0, QueryTriggerInteraction.Ignore))
                return;

            float size = Mathf.Lerp(0.12f, 0.58f, Config.Clamp(intensity, 0f, 1.35f));
            float lifetime = Mathf.Lerp(Config.BloodFadeSeconds * 0.55f, Config.BloodFadeSeconds * 1.15f, Config.Clamp(intensity, 0f, 1f));
            Color color = new Color(0.26f, 0f, 0.012f, Mathf.Lerp(0.58f, 0.94f, Config.Clamp(intensity, 0f, 1f)));
            PlaceMark(hit, size, lifetime, color, BloodMarkKind.Pool, false);
        }

        public void EmitWallSplat(Vector3 position, Vector3 direction, float intensity, bool arterial)
        {
            EnsureInitialized();
            intensity = Config.Clamp(intensity * Math.Max(0.15f, Config.BloodFxDensity), 0.05f, 2.0f);
            Vector3 rayDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
            RaycastHit hit;
            if (!Physics.Raycast(position - rayDirection * 0.12f, rayDirection, out hit, arterial ? 5.0f : 2.5f, ~0, QueryTriggerInteraction.Ignore))
                return;

            float vertical = Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up));
            if (vertical > 0.65f)
                return;

            float size = Mathf.Lerp(0.09f, arterial ? 0.42f : 0.28f, Config.Clamp(intensity, 0f, 1f));
            PlaceMark(hit, size, Config.BloodFadeSeconds, new Color(0.38f, 0f, 0.016f, Mathf.Lerp(0.46f, 0.90f, intensity)), BloodMarkKind.WallSplat, true);
        }

        public void EmitFootprint(Vector3 position, Vector3 forward, float intensity)
        {
            EnsureInitialized();
            RaycastHit hit;
            if (!Physics.Raycast(position + Vector3.up * 0.28f, Vector3.down, out hit, 1.0f, ~0, QueryTriggerInteraction.Ignore))
                return;

            float size = Mathf.Lerp(0.055f, 0.105f, Config.Clamp(intensity, 0f, 1f));
            BloodPool pool = PlaceMark(hit, size, Config.BloodFadeSeconds * 0.75f, new Color(0.27f, 0f, 0.012f, Mathf.Lerp(0.28f, 0.72f, intensity)), BloodMarkKind.Footprint, true);
            if (pool.Transform != null && forward.sqrMagnitude > 0.01f)
                pool.Transform.rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
        }

        public void EmitHandprint(Vector3 position, Vector3 direction, float intensity)
        {
            EnsureInitialized();
            Vector3 rayDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward;
            RaycastHit hit;
            if (!Physics.Raycast(position - rayDirection * 0.08f, rayDirection, out hit, 1.4f, ~0, QueryTriggerInteraction.Ignore))
                return;

            float size = Mathf.Lerp(0.075f, 0.15f, Config.Clamp(intensity, 0f, 1f));
            PlaceMark(hit, size, Config.BloodFadeSeconds * 0.85f, new Color(0.32f, 0f, 0.015f, Mathf.Lerp(0.35f, 0.82f, intensity)), BloodMarkKind.Handprint, true);
        }

        public void EmitBloodTransfer(Vector3 position, float intensity)
        {
            EnsureInitialized();
            RaycastHit hit;
            if (!Physics.Raycast(position + Vector3.up * 0.18f, Vector3.down, out hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
                return;

            float size = Mathf.Lerp(0.045f, 0.13f, Config.Clamp(intensity, 0f, 1f));
            PlaceMark(hit, size, Config.BloodFadeSeconds * 0.55f, new Color(0.30f, 0f, 0.012f, Mathf.Lerp(0.25f, 0.65f, intensity)), BloodMarkKind.Transfer, false);
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            try
            {
                _poolMesh = CreateDiscMesh(28);
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                    shader = Shader.Find("Unlit/Color");
                if (shader == null)
                    shader = Shader.Find("Hidden/Internal-Colored");
                if (shader == null)
                {
                    _initialized = true;
                    _usingNative = false;
                    MainMod.Runtime?.Logger.Warning("Blood FX disabled safely: no compatible shader found.");
                    return;
                }

                _bloodTexture = CreateBloodPoolTexture(96);
                _bloodMaterial = new Material(shader);
                _bloodMaterial.mainTexture = _bloodTexture;
                _bloodMaterial.color = new Color(0.34f, 0f, 0.018f, 0.94f);

                DiscoverNativeTemplate();
                CreateFallbackSprays();
                CreatePools();
                _initialized = true;
            }
            catch (Exception ex)
            {
                _initialized = true;
                _usingNative = false;
                MainMod.Runtime?.Logger.Warning("Blood FX initialization failed safely: " + ex.Message);
            }
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

            BloodPool pool = PlaceMark(hit, Mathf.Lerp(0.05f, impact ? 0.33f : 0.22f, Config.Clamp(intensity, 0f, 1f)), Mathf.Lerp(Config.BloodFadeSeconds * 0.38f, Config.BloodFadeSeconds, Config.Clamp(intensity, 0f, 1f)), new Color(0.34f, 0f, 0.018f, Mathf.Lerp(0.48f, 0.88f, Config.Clamp(intensity, 0f, 1f))), impact ? BloodMarkKind.Pool : BloodMarkKind.Trail, false);
            if (pool.GameObject == null)
                return;
        }

        private BloodPool PlaceMark(RaycastHit hit, float size, float lifetime, Color color, BloodMarkKind kind, bool elongated)
        {
            int index = _nextPool++ % _pools.Length;
            BloodPool pool = _pools[index];
            if (pool.GameObject == null || pool.Transform == null || pool.Renderer == null)
                return pool;

            pool.GameObject.SetActive(true);
            pool.Transform.position = hit.point + hit.normal * 0.004f;
            pool.Transform.rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            pool.Size = size;
            pool.Transform.localScale = elongated ? new Vector3(pool.Size * 0.65f, pool.Size * 1.35f, 1f) : new Vector3(pool.Size, pool.Size, 1f);
            pool.Age = 0f;
            pool.Lifetime = lifetime;
            pool.Active = true;
            pool.Kind = kind;
            pool.Renderer.material.color = color;
            _pools[index] = pool;
            return pool;
        }

        private static Mesh CreateDiscMesh(int segments)
        {
            Mesh mesh = new Mesh();
            Vector3[] vertices = new Vector3[segments + 1];
            Vector2[] uv = new Vector2[segments + 1];
            int[] triangles = new int[segments * 3];
            vertices[0] = Vector3.zero;
            uv[0] = new Vector2(0.5f, 0.5f);
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * 0.5f;
                float y = Mathf.Sin(angle) * 0.5f;
                vertices[i + 1] = new Vector3(x, y, 0f);
                uv[i + 1] = new Vector2(x + 0.5f, y + 0.5f);
            }

            for (int i = 0; i < segments; i++)
            {
                int tri = i * 3;
                triangles[tri] = 0;
                triangles[tri + 1] = i + 1;
                triangles[tri + 2] = i == segments - 1 ? 1 : i + 2;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Texture2D CreateBloodPoolTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            uint seed = 2166136261u;
            for (int y = 0; y < size; y++)
            {
                float ny = ((y + 0.5f) / size - 0.5f) * 2f;
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size - 0.5f) * 2f;
                    float angle = Mathf.Atan2(ny, nx);
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    seed ^= (uint)(x * 374761393 + y * 668265263);
                    seed *= 16777619u;
                    float noise = ((seed >> 8) & 0xff) / 255f;
                    float edge = 0.82f + Mathf.Sin(angle * 5.0f) * 0.08f + Mathf.Sin(angle * 11.0f) * 0.045f + (noise - 0.5f) * 0.12f;
                    float alpha = 1f - Mathf.SmoothStep(edge - 0.08f, edge + 0.08f, radius);
                    float core = 1f - Mathf.SmoothStep(0.0f, 0.58f, radius);
                    byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(80f, 150f, core)), 0, 255);
                    byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0f, 7f, core)), 0, 255);
                    byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(3f, 14f, core)), 0, 255);
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * (180f + noise * 55f)), 0, 235);
                    pixels[y * size + x] = new Color32(r, g, b, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }
    }
}
