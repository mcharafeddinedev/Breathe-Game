using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Procedural particle effects for environmental zones.
    // Color-coded per zone type; matches existing art style (BoatSplashEffect, AIStunEffect).
    [RequireComponent(typeof(EnvironmentalZoneEffect))]
    public class ZoneVisualController : MonoBehaviour
    {
        [SerializeField] private int _poolSize = 48;

        [Header("Spawn")]
        [SerializeField] private float _spawnInterval = 0.04f;
        [SerializeField] private int _spawnCountPerInterval = 4;

        [SerializeField] private int _sortingOrder = -5;

        [Header("Visibility")]
        [SerializeField, Tooltip("Only show particles when zone is within this distance of the camera.")]
        private float _maxVisibleDistance = 24f;

        private EnvironmentalZoneEffect _zone;
        private Collider2D _collider;
        private float _spawnTimer;
        private Particle[] _particles;
        private int _poolIndex;
        private static Sprite _windStreakSprite;   // Elongated wind lines (tailwind, crosswind)
        private static Sprite _headwindSprite;     // Horizontal striations — wind flow, not snow
        private static Sprite _swirlSprite;        // Cyclone — spiral/whirl
        private static Sprite _puffSprite;         // Doldrums, Calm — soft cloud puffs
        private static Sprite _dropletSprite;      // WaveChop, Waves — water droplets

        private struct Particle
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public float Age;
            public float MaxAge;
            public Vector2 Velocity;
            public float Phase;
            public bool Active;
        }

        // Call when zone type/radius changed at runtime (e.g. from spawner).
        public void RefreshFromZone()
        {
            _zone = GetComponent<EnvironmentalZoneEffect>();
            foreach (var col in GetComponents<Collider2D>())
            {
                if (col.enabled)
                {
                    _collider = col;
                    return;
                }
            }
            _collider = GetComponent<Collider2D>();
        }

        private void Awake()
        {
            _zone = GetComponent<EnvironmentalZoneEffect>();
            _collider = GetComponent<Collider2D>();
        }

        private void Start()
        {
            EnsureSprites();
            BuildPool();
        }

        private void Update()
        {
            if (_zone == null) return;

            // Only show particles when zone is near the camera (which follows the boat).
            var cam = Camera.main;
            if (cam != null && Vector3.Distance(transform.position, cam.transform.position) > _maxVisibleDistance)
            {
                DeactivateAllParticles();
                return;
            }

            float radius = _collider switch
            {
                CircleCollider2D c => c.radius,
                CapsuleCollider2D cap => cap.size.x * 0.5f,
                _ => 4f
            };
            ZoneType type = _zone.ZoneType;

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                SpawnParticles(type, radius);
                _spawnTimer = _spawnInterval;
            }

            UpdateParticles(type, radius);
        }

        private void DeactivateAllParticles()
        {
            if (_particles == null) return;
            for (int i = 0; i < _particles.Length; i++)
            {
                if (_particles[i].Active)
                {
                    _particles[i].Active = false;
                    _particles[i].Go.SetActive(false);
                }
            }
        }

        private void SpawnParticles(ZoneType type, float radius)
        {
            var config = FindZoneConfig();
            var color = config != null ? config.GetParameters(type).visualColor : GetDefaultColor(type);

            for (int i = 0; i < _spawnCountPerInterval; i++)
            {
                int idx = FindInactive();
                if (idx < 0) return;

                ref Particle p = ref _particles[idx];
                // Spawn in local-space circle; parent scale matches collider ellipse (e.g. crosswind oval)
                Vector2 localPos = Random.insideUnitCircle * radius;
                p.Go.transform.localPosition = localPos;
                p.Go.transform.localRotation = Quaternion.identity;
                p.Age = 0f;
                p.Phase = Random.Range(0f, Mathf.PI * 2f);

                Sprite sprite;
                float scale;
                Vector2 vel;

                // Particle directions match boat forces: Y+ = forward, headwind Y-, tailwind Y+
                float crossDir = _zone != null ? _zone.LateralDirection : (Random.value > 0.5f ? 1f : -1f);

                switch (type)
                {
                    case ZoneType.Headwind:
                        sprite = _headwindSprite;
                        scale = Random.Range(0.5f, 1f);
                        vel = new Vector2(Random.Range(-0.6f, 0.6f), -3f - Random.Range(0f, 2f));
                        p.Go.transform.localScale = new Vector3(scale * 1.1f, scale * 0.7f, 1f);
                        p.Go.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f));
                        break;
                    case ZoneType.Tailwind:
                        sprite = _windStreakSprite;
                        scale = Random.Range(0.35f, 0.7f);
                        vel = new Vector2(Random.Range(-0.5f, 0.5f), 3f + Random.Range(0f, 2f));
                        p.Go.transform.localScale = new Vector3(scale * 0.35f, scale * 1.4f, 1f);
                        p.Go.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
                        break;
                    case ZoneType.Crosswind:
                        sprite = _windStreakSprite;
                        scale = Random.Range(0.3f, 0.55f);
                        vel = new Vector2(crossDir * (3f + Random.Range(0f, 1.5f)), Random.Range(-0.5f, 0.5f));
                        p.Go.transform.localScale = new Vector3(scale * 1.2f, scale * 0.35f, 1f);
                        p.Go.transform.localRotation = Quaternion.identity;
                        break;
                    case ZoneType.Cyclone:
                        sprite = _swirlSprite;
                        scale = Random.Range(0.25f, 0.5f);
                        float angle = p.Phase + Time.time * 4f;
                        vel = new Vector2(Mathf.Cos(angle) * 2.5f, Mathf.Sin(angle) * 2.5f);
                        p.Go.transform.localScale = new Vector3(scale, scale, 1f);
                        break;
                    case ZoneType.Doldrums:
                        sprite = _puffSprite;
                        scale = Random.Range(0.4f, 0.8f);
                        vel = new Vector2(Random.Range(-0.4f, 0.4f), Random.Range(-0.2f, 0.3f));
                        p.Go.transform.localScale = new Vector3(scale, scale, 1f);
                        break;
                    case ZoneType.WaveChop:
                    case ZoneType.Waves:
                        sprite = _dropletSprite;
                        scale = Random.Range(0.2f, 0.45f);
                        vel = new Vector2(Mathf.Sin(Time.time * 5f + p.Phase) * 1.5f, Mathf.Cos(Time.time * 4f + p.Phase * 1.3f) * 1.8f);
                        p.Go.transform.localScale = new Vector3(scale, scale, 1f);
                        break;
                    case ZoneType.Calm:
                        sprite = _puffSprite;
                        scale = Random.Range(0.25f, 0.5f);
                        vel = new Vector2(Random.Range(-0.15f, 0.15f), Random.Range(0.05f, 0.2f));
                        p.Go.transform.localScale = new Vector3(scale, scale, 1f);
                        break;
                    default:
                        sprite = _puffSprite;
                        scale = 0.35f;
                        vel = Vector2.zero;
                        p.Go.transform.localScale = new Vector3(scale, scale, 1f);
                        break;
                }

                p.Go.GetComponent<SpriteRenderer>().sprite = sprite;
                p.Velocity = vel;
                p.MaxAge = Random.Range(0.8f, 1.5f);
                p.Sr.color = new Color(color.r, color.g, color.b, 0f);
                p.Active = true;
                p.Go.SetActive(true);
            }
        }

        private void UpdateParticles(ZoneType type, float radius)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                ref Particle p = ref _particles[i];
                if (!p.Active) continue;

                p.Age += Time.deltaTime;
                float t = p.Age / p.MaxAge;

                Vector3 pos = p.Go.transform.localPosition;
                pos += (Vector3)(p.Velocity * Time.deltaTime);
                p.Go.transform.localPosition = pos;

                if (type == ZoneType.Cyclone)
                {
                    float angle = Mathf.Atan2(pos.y, pos.x) + Time.deltaTime * 5f;
                    float r = new Vector2(pos.x, pos.y).magnitude;
                    float pullIn = 1f - Time.deltaTime * 2f;
                    p.Go.transform.localPosition = new Vector3(Mathf.Cos(angle) * r * pullIn, Mathf.Sin(angle) * r * pullIn, 0f);
                }

                var config = FindZoneConfig();
                var color = config != null ? config.GetParameters(type).visualColor : GetDefaultColor(type);
                float alpha = t < 0.15f ? (t / 0.15f) * 0.85f : (t > 0.75f ? (1f - t) / 0.25f * 0.85f : 0.85f);
                p.Sr.color = new Color(color.r, color.g, color.b, alpha);

                if (t >= 1f || pos.magnitude > radius * 1.2f)
                {
                    p.Active = false;
                    p.Go.SetActive(false);
                }
            }
        }

        private EnvironmentalZoneConfig FindZoneConfig()
        {
            var cm = FindAnyObjectByType<CourseManager>();
            return cm?.CourseConfig?.ZoneConfig;
        }

        private static Color GetDefaultColor(ZoneType type)
        {
            return type switch
            {
                ZoneType.Headwind => new Color(0.7f, 0.74f, 0.8f, 0.6f),
                ZoneType.Tailwind => new Color(0.6f, 0.95f, 0.7f, 0.5f),
                ZoneType.Crosswind => new Color(0.6f, 0.65f, 0.7f, 0.5f),
                ZoneType.Cyclone => new Color(0.4f, 0.5f, 0.6f, 0.6f),
                ZoneType.Doldrums => new Color(0.7f, 0.72f, 0.75f, 0.25f),
                ZoneType.WaveChop or ZoneType.Waves => new Color(0.5f, 0.7f, 0.9f, 0.5f),
                _ => new Color(0.7f, 0.7f, 0.7f, 0.4f)
            };
        }

        private int FindInactive()
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                if (!_particles[i].Active) return i;
            }
            return -1;
        }

        private void BuildPool()
        {
            var parent = new GameObject("ZoneParticles");
            parent.transform.SetParent(transform, false);
            parent.transform.localPosition = Vector3.zero;

            _particles = new Particle[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"Particle_{i}");
                go.transform.SetParent(parent.transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sortingOrder = _sortingOrder;
                sr.material = new Material(Shader.Find("Sprites/Default"));

                go.SetActive(false);

                _particles[i] = new Particle { Go = go, Sr = sr, Active = false };
            }
        }

        private static void EnsureSprites()
        {
            // Soft elongated ellipse for tailwind/crosswind
            if (_windStreakSprite == null)
            {
                int w = 20, h = 6;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                float cx = w / 2f, cy = h / 2f;
                float rx = w / 2f - 0.5f, ry = h / 2f - 0.5f;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float dx = (x - cx) / rx;
                        float dy = (y - cy) / ry;
                        float ellipse = dx * dx + dy * dy;
                        float a = ellipse <= 1f ? (1f - ellipse) * 0.95f : 0f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                tex.filterMode = FilterMode.Bilinear;
                _windStreakSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
            }

            // Diagonal slash/stroke — reads as wind resistance
            if (_headwindSprite == null)
            {
                int w = 36, h = 10;
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                float cx = w / 2f, cy = h / 2f;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        float dx = x - cx;
                        float dy = y - cy;
                        float along = dx * 0.707f + dy * 0.707f;
                        float across = -dx * 0.707f + dy * 0.707f;
                        float strokeHalfW = 1.8f;
                        float strokeHalfLen = 16f;
                        float a = 0f;
                        if (Mathf.Abs(across) < strokeHalfW && Mathf.Abs(along) < strokeHalfLen)
                        {
                            float lenT = 1f - Mathf.Abs(along) / strokeHalfLen;
                            float acrossT = 1f - Mathf.Abs(across) / strokeHalfW;
                            a = Mathf.Clamp01(lenT * acrossT) * 0.98f;
                        }
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                tex.Apply();
                tex.filterMode = FilterMode.Bilinear;
                _headwindSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
            }

            // Curved comma/arm — suggests rotation, distinct from puffs
            if (_swirlSprite == null)
            {
                int s = 18;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
                float c = s / 2f;
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = x - c, dy = y - c;
                        float r = Mathf.Sqrt(dx * dx + dy * dy) / (c - 0.5f);
                        float angle = Mathf.Atan2(dy, dx);
                        float spiral = (angle + Mathf.PI) / (Mathf.PI * 2f) + r * 0.4f;
                        spiral = spiral - Mathf.Floor(spiral);
                        float arm = 1f - Mathf.Abs(spiral - 0.5f) * 4f;
                        arm = Mathf.Clamp01(arm) * (1f - r * 0.2f);
                        float a = r <= 1f ? Mathf.Clamp01(arm + (1f - r) * 0.3f) : 0f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                tex.filterMode = FilterMode.Bilinear;
                _swirlSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 16f);
            }

            // Soft circular cloud for doldrums/calm zones
            if (_puffSprite == null)
            {
                int s = 16;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
                float c = s / 2f, r = c - 1f;
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                        float a = d <= 1f ? (d > 0.5f ? (1f - d) * 2f : 1f) : 0f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                tex.filterMode = FilterMode.Bilinear;
                _puffSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 16f);
            }

            // Round water drop for wave chop/waves
            if (_dropletSprite == null)
            {
                int s = 14;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
                float c = s / 2f, r = c - 0.5f;
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / r;
                        float a = d <= 1f ? (1f - d * 0.6f) : 0f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                tex.Apply();
                tex.filterMode = FilterMode.Bilinear;
                _dropletSprite = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 16f);
            }
        }
    }
}
