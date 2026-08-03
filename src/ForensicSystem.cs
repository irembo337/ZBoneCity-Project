using System;
using UnityEngine;

namespace BonelabAdvancedHealth
{
    public sealed class ForensicSystem
    {
        private enum MarkKind
        {
            BulletHole,
            ImpactTrace,
            Casing,
            BloodDrag
        }

        private struct ForensicMark
        {
            public GameObject? GameObject;
            public Transform? Transform;
            public Renderer? Renderer;
            public float Age;
            public float Lifetime;
            public bool Active;
            public MarkKind Kind;
        }

        private readonly ForensicMark[] _marks = new ForensicMark[160];
        private Material? _bulletMaterial;
        private Material? _bloodMaterial;
        private Material? _brassMaterial;
        private Mesh? _discMesh;
        private int _nextMark;
        private float _lastDeathRealtime = -1f;
        private float _lastKnownBloodLossMl;
        private bool _initialized;

        public float LastDeathAgeSeconds => _lastDeathRealtime < 0f ? -1f : Time.realtimeSinceStartup - _lastDeathRealtime;
        public float LastKnownBloodLossMl => _lastKnownBloodLossMl;

        public void Reset()
        {
            for (int i = 0; i < _marks.Length; i++)
            {
                if (_marks[i].GameObject != null)
                    _marks[i].GameObject!.SetActive(false);
                _marks[i].Active = false;
            }

            _lastDeathRealtime = -1f;
            _lastKnownBloodLossMl = 0f;
        }

        public void Update(float deltaTime)
        {
            if (!_initialized)
                return;

            for (int i = 0; i < _marks.Length; i++)
            {
                if (!_marks[i].Active || _marks[i].GameObject == null || _marks[i].Renderer == null)
                    continue;

                ForensicMark mark = _marks[i];
                mark.Age += deltaTime;
                if (mark.Age >= mark.Lifetime)
                {
                    mark.Active = false;
                    mark.GameObject!.SetActive(false);
                    _marks[i] = mark;
                    continue;
                }

                float fade = 1f - Mathf.SmoothStep(0.70f, 1f, mark.Age / Math.Max(1f, mark.Lifetime));
                Color color = mark.Renderer!.material.color;
                color.a = Mathf.Lerp(0f, color.a, fade);
                mark.Renderer.material.color = color;
                _marks[i] = mark;
            }
        }

        public void OnDamage(HealthManager manager, DamageInfo info, OrganDamageFeedback feedback)
        {
            if (!Config.ForensicsEnabled)
                return;

            EnsureInitialized();
            if (info.DamageType == AdvancedDamageType.Bullet)
            {
                TryPlaceSurfaceDisc(info.Origin, info.Direction, 0.045f, _bulletMaterial, MarkKind.BulletHole, Config.ForensicTraceLifetimeSeconds);
                TrySpawnCasing(info.Origin, info.Direction);
            }
            else if (info.DamageType == AdvancedDamageType.Explosion || info.DamageType == AdvancedDamageType.Blunt)
            {
                TryPlaceSurfaceDisc(info.Origin, info.Direction, 0.075f, _bulletMaterial, MarkKind.ImpactTrace, Config.ForensicTraceLifetimeSeconds * 0.7f);
            }

            if (feedback.BleedSeverity >= BleedSeverity.Medium)
                TryPlaceSurfaceDisc(info.Origin, Vector3.down, Mathf.Lerp(0.08f, 0.22f, Config.Clamp(info.Damage / 120f, 0f, 1f)), _bloodMaterial, MarkKind.BloodDrag, Config.BloodFadeSeconds);

            _lastKnownBloodLossMl = Config.BloodVolumeMl - manager.Bleeding.BloodVolumeMl;
        }

        public void OnDeath(HealthManager manager, DeathCause cause)
        {
            if (!Config.ForensicsEnabled)
                return;

            _lastDeathRealtime = Time.realtimeSinceStartup;
            _lastKnownBloodLossMl = Config.BloodVolumeMl - manager.Bleeding.BloodVolumeMl;
        }

        private void EnsureInitialized()
        {
            if (_initialized)
                return;

            _discMesh = CreateDiscMesh(18);
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Hidden/Internal-Colored");
            if (shader == null)
            {
                _initialized = true;
                MainMod.Runtime?.Logger.Warning("[ZBC ERROR] Forensic marks disabled safely: no compatible shader found.");
                return;
            }

            _bulletMaterial = new Material(shader) { color = new Color(0.015f, 0.012f, 0.010f, 0.84f) };
            _bloodMaterial = new Material(shader) { color = new Color(0.30f, 0.0f, 0.018f, 0.72f) };
            _brassMaterial = new Material(shader) { color = new Color(0.82f, 0.56f, 0.18f, 0.95f) };
            CreatePool();
            _initialized = true;
        }

        private void CreatePool()
        {
            for (int i = 0; i < _marks.Length; i++)
            {
                GameObject go = new GameObject("ZBC_ForensicMark_" + i);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                filter.mesh = _discMesh;
                renderer.material = _bulletMaterial;
                go.SetActive(false);
                _marks[i] = new ForensicMark
                {
                    GameObject = go,
                    Transform = go.transform,
                    Renderer = renderer
                };
            }
        }

        private void TryPlaceSurfaceDisc(Vector3 origin, Vector3 direction, float size, Material? material, MarkKind kind, float lifetime)
        {
            if (material == null || origin.sqrMagnitude < 0.001f)
                return;

            Vector3 rayDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.down;
            RaycastHit hit;
            if (!Physics.Raycast(origin - rayDirection * 0.12f, rayDirection, out hit, 2.8f, ~0, QueryTriggerInteraction.Ignore) &&
                !Physics.Raycast(origin + Vector3.up * 0.35f, Vector3.down, out hit, 2.6f, ~0, QueryTriggerInteraction.Ignore))
                return;

            ForensicMark mark = GetNextMark();
            if (mark.GameObject == null || mark.Transform == null || mark.Renderer == null)
                return;

            mark.GameObject.SetActive(true);
            mark.Transform.position = hit.point + hit.normal * 0.006f;
            mark.Transform.rotation = Quaternion.LookRotation(hit.normal) * Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
            mark.Transform.localScale = new Vector3(size, size, 1f);
            mark.Renderer.material = material;
            mark.Age = 0f;
            mark.Lifetime = Math.Max(5f, lifetime);
            mark.Active = true;
            mark.Kind = kind;
            _marks[(_nextMark - 1 + _marks.Length) % _marks.Length] = mark;
        }

        private void TrySpawnCasing(Vector3 origin, Vector3 direction)
        {
            if (_brassMaterial == null || origin.sqrMagnitude < 0.001f || UnityEngine.Random.value > Config.ForensicCasingChance)
                return;

            ForensicMark mark = GetNextMark();
            if (mark.GameObject == null || mark.Transform == null || mark.Renderer == null)
                return;

            Vector3 side = Vector3.Cross(direction.sqrMagnitude > 0.01f ? direction.normalized : Vector3.forward, Vector3.up);
            if (side.sqrMagnitude < 0.01f)
                side = Vector3.right;

            mark.GameObject.SetActive(true);
            mark.Transform.position = origin + side.normalized * UnityEngine.Random.Range(0.08f, 0.22f) + Vector3.down * 0.04f;
            mark.Transform.rotation = UnityEngine.Random.rotation;
            mark.Transform.localScale = new Vector3(0.013f, 0.032f, 1f);
            mark.Renderer.material = _brassMaterial;
            mark.Age = 0f;
            mark.Lifetime = Config.ForensicTraceLifetimeSeconds;
            mark.Active = true;
            mark.Kind = MarkKind.Casing;
            _marks[(_nextMark - 1 + _marks.Length) % _marks.Length] = mark;
        }

        private ForensicMark GetNextMark()
        {
            int index = _nextMark++ % _marks.Length;
            return _marks[index];
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
                float angle = i / (float)segments * Mathf.PI * 2f;
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
    }
}
