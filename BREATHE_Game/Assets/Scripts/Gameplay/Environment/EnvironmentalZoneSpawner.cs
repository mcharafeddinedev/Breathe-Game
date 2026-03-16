using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Spawns environmental zones randomly along the course.
    // Scrolls with the camera and pools zone instances for reuse.
    public class EnvironmentalZoneSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CourseMarkers _courseMarkers;
        [SerializeField] private CourseConfig _courseConfig;

        [Header("Spawn Region")]
        [SerializeField] private float _spawnAheadDistance = 35f;
        [SerializeField] private float _despawnBehindDistance = 20f;
        [SerializeField, Tooltip("World Y below which no zones spawn (~2 buoys at default interval).")]
        private float _gracePeriodY = 10f;

        [SerializeField, Tooltip("Min/max X in world space for zone placement.")]
        private Vector2 _xRange = new Vector2(-18f, 18f);

        [Header("Zone Types")]
        [SerializeField, Tooltip("Zone types that can spawn. Empty = all types.")]
        private ZoneType[] _allowedTypes = new ZoneType[]
        {
            ZoneType.Headwind, ZoneType.Tailwind, ZoneType.Crosswind,
            ZoneType.Cyclone, ZoneType.Doldrums, ZoneType.WaveChop
        };

        [Header("Zone Size")]
        [SerializeField] private float _minRadius = 2.8f;
        [SerializeField] private float _maxRadius = 5.5f;
        [SerializeField, Range(0.7f, 1.3f)] private float _radiusVariance = 0.2f;

        [Header("Crosswind Shape")]
        [SerializeField] private float _crosswindOvalX = 3.4f;
        [SerializeField] private float _crosswindOvalY = 0.55f;

        [Header("Density")]
        [SerializeField] private int _minZonesPerRace = 3;
        [SerializeField] private float _spawnInterval = 18f;
        [SerializeField, Range(0f, 1f)] private float _spawnChance = 0.6f;
        [SerializeField] private float _minZoneSpacing = 12f;
        [SerializeField, Tooltip("Normalized Y range (0–1) along course. Ignored if IgnoreCourseBounds=true.")]
        private Vector2 _spawnRange = new Vector2(0.05f, 0.95f);
        [SerializeField] private bool _ignoreCourseBounds = true;

        [SerializeField, Range(0f, 1f), Tooltip("0 = all random X, 1 = all on course path.")]
        private float _onCoursePathWeight = 0.5f;

        [SerializeField] private int _maxZones = 10;

        [Header("Debug")]
        [SerializeField, Tooltip("Spawns one of each zone type in fixed order. Uncheck for fair random spawning.")]
        private bool _debugSpawnOneOfEach = false;
        [SerializeField] private float _debugZoneSpacing = 14f;
        [SerializeField] private float _debugStartY = 12f;

        public void SetDebugSpawnOneOfEach(bool enable) => _debugSpawnOneOfEach = enable;

        private Camera _cam;
        private Transform _zonesRoot;
        private float _nextSpawnY;
        private readonly List<ZoneInstance> _activeZones = new List<ZoneInstance>();
        private readonly Queue<ZoneInstance> _pool = new Queue<ZoneInstance>();

        // Track recent spawns so no zone type dominates
        private const int RecentSpawnWindow = 8;
        private readonly List<ZoneType> _recentSpawnTypes = new List<ZoneType>(RecentSpawnWindow);

        private struct ZoneInstance
        {
            public GameObject Root;
            public EnvironmentalZoneEffect Effect;
            public ZoneVisualController Visual;
            public CircleCollider2D CircleCollider;
            public CapsuleCollider2D CapsuleCollider;
            public float BaseY;
            public float Radius;
        }

        private ZoneType[] AllowedTypes => _allowedTypes != null && _allowedTypes.Length > 0
            ? _allowedTypes
            : new[] { ZoneType.Headwind, ZoneType.Tailwind, ZoneType.Crosswind, ZoneType.Cyclone, ZoneType.Doldrums, ZoneType.WaveChop };

        private void Start()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                Debug.LogError("[EnvironmentalZoneSpawner] Camera.main is null!");
                return;
            }

            if (_courseMarkers == null)
                _courseMarkers = FindAnyObjectByType<CourseMarkers>();
            if (_courseConfig == null)
            {
                var cm = FindAnyObjectByType<CourseManager>();
                if (cm != null)
                    _courseConfig = cm.CourseConfig;
            }

            if (_courseConfig != null)
            {
                _spawnInterval = _courseConfig.ZoneSpawnInterval;
                _spawnChance = _courseConfig.ZoneSpawnChance;
                _minZonesPerRace = _courseConfig.MinZonesPerRace;
                _minZoneSpacing = _courseConfig.MinZoneSpacing;
                _spawnRange = _courseConfig.ZoneSpawnRange;
                _gracePeriodY = _courseConfig.ZoneGracePeriodY;
            }

            _zonesRoot = new GameObject("--- ENVIRONMENTAL_ZONES ---").transform;
            _zonesRoot.SetParent(transform);

            float camY = _cam.transform.position.y;
            _nextSpawnY = camY - _despawnBehindDistance;

            for (int i = 0; i < _maxZones; i++)
                _pool.Enqueue(CreateZone());

            if (_debugSpawnOneOfEach)
                SpawnDebugZones();

            VerifyZoneCollisionLayers();
        }

        // Debug order: Headwind → Tailwind → Crosswind → Cyclone → Doldrums → WaveChop → Waves → Calm
        private static readonly ZoneType[] DebugZoneOrder =
        {
            ZoneType.Headwind, ZoneType.Tailwind, ZoneType.Crosswind, ZoneType.Cyclone,
            ZoneType.Doldrums, ZoneType.WaveChop, ZoneType.Waves, ZoneType.Calm
        };

        private void SpawnDebugZones()
        {
            if (_courseMarkers == null)
            {
                Debug.LogWarning("[EnvironmentalZoneSpawner] SpawnDebugZones: _courseMarkers is null — no zones spawned");
                return;
            }

            float courseLength = _courseMarkers.CourseLength;
            float y = _debugStartY;
            float radius = (_minRadius + _maxRadius) * 0.5f;

            for (int i = 0; i < DebugZoneOrder.Length; i++)
            {
                if (y > courseLength - 5f) break;
                if (_pool.Count == 0) break;

                float x = _courseMarkers.CurveX(y);

                var zone = _pool.Dequeue();
                zone.Root.transform.position = new Vector3(x, y, 0f);
                zone.Effect.SetZoneType(DebugZoneOrder[i]);
                zone.Effect.SetStrength(1f);
                zone.Effect.SetRadius(radius);
                zone.Effect.SetShowPopupOnEnter(true);
                ApplyZoneShape(zone, DebugZoneOrder[i], radius);
                if (zone.Visual != null)
                    zone.Visual.RefreshFromZone();

                zone.BaseY = y;
                zone.Radius = radius;
                zone.Root.name = $"EnvZone_Debug_{DebugZoneOrder[i]}";
                zone.Root.SetActive(true);
                _activeZones.Add(zone);

                y += _debugZoneSpacing;
            }

            Debug.Log($"[EnvironmentalZoneSpawner] Debug: spawned {_activeZones.Count} zones. Order: Headwind → Tailwind → Crosswind → Cyclone → Doldrums → WaveChop → Waves → Calm");
        }

        private static void VerifyZoneCollisionLayers()
        {
            int defaultLayer = 0;
            int playerLayer = LayerMask.NameToLayer("Player");
            int aiLayer = LayerMask.NameToLayer("AI");
            if (playerLayer >= 0 && Physics2D.GetIgnoreLayerCollision(defaultLayer, playerLayer))
                Debug.LogWarning("[EnvironmentalZoneSpawner] Default and Player layers don't collide. Zones won't affect player. Fix in Physics 2D > Layer Collision Matrix.");
            if (aiLayer >= 0 && Physics2D.GetIgnoreLayerCollision(defaultLayer, aiLayer))
                Debug.LogWarning("[EnvironmentalZoneSpawner] Default and AI layers don't collide. Zones won't affect AI boats. Fix in Physics 2D > Layer Collision Matrix.");
        }

        private void Update()
        {
            if (_cam == null || _courseConfig == null) return;
            if (_debugSpawnOneOfEach || !_courseConfig.SpawnEnvironmentalZones) return;

            float camY = _cam.transform.position.y;
            float spawnLimit = camY + _spawnAheadDistance;
            float despawnLimit = camY - _despawnBehindDistance;

            while (_nextSpawnY < spawnLimit)
            {
                TrySpawnZone(_nextSpawnY);
                _nextSpawnY += _spawnInterval;
            }

            RecycleZonesBehind(despawnLimit);
        }

        private void TrySpawnZone(float y)
        {
            if (_pool.Count == 0) return;
            if (y < _gracePeriodY) return;

            bool forceSpawn = _activeZones.Count < _minZonesPerRace;
            if (!forceSpawn && UnityEngine.Random.value > _spawnChance) return;

            if (!_ignoreCourseBounds && _courseMarkers != null)
            {
                float courseLength = _courseMarkers.CourseLength;
                float normY = y / Mathf.Max(1f, courseLength);
                if (normY < _spawnRange.x || normY > _spawnRange.y) return;
            }

            if (TooCloseToExistingZone(y)) return;

            ZoneType type = PickFairZoneType();
            float x = PickSpawnX(y, type);
            float baseRadius = UnityEngine.Random.Range(_minRadius, _maxRadius);
            float scale = UnityEngine.Random.Range(1f - _radiusVariance, 1f + _radiusVariance);
            float radius = Mathf.Clamp(baseRadius * scale, _minRadius * 0.9f, _maxRadius * 1.1f);
            float strength = UnityEngine.Random.Range(0.8f, 1f);

            var zone = _pool.Dequeue();
            zone.Root.transform.position = new Vector3(x, y, 0f);
            zone.Effect.SetZoneType(type);
            zone.Effect.SetStrength(strength);
            zone.Effect.SetRadius(radius);
            zone.Effect.SetShowPopupOnEnter(true);
            ApplyZoneShape(zone, type, radius);
            if (zone.Visual != null)
                zone.Visual.RefreshFromZone();

            zone.BaseY = y;
            zone.Radius = radius;
            zone.Root.SetActive(true);
            _activeZones.Add(zone);

            RecordSpawnType(type);
        }

        // Weighted random — types that appeared less recently get higher chance.
        private ZoneType PickFairZoneType()
        {
            var allowed = AllowedTypes;
            if (allowed.Length == 0) return ZoneType.Headwind;

            float[] weights = new float[allowed.Length];
            float total = 0f;
            for (int i = 0; i < allowed.Length; i++)
            {
                int recentCount = 0;
                foreach (var t in _recentSpawnTypes)
                    if (t == allowed[i]) recentCount++;
                weights[i] = 1f / (recentCount + 1f);
                total += weights[i];
            }

            float r = UnityEngine.Random.Range(0f, total);
            for (int i = 0; i < allowed.Length; i++)
            {
                r -= weights[i];
                if (r <= 0f) return allowed[i];
            }
            return allowed[allowed.Length - 1];
        }

        private void RecordSpawnType(ZoneType type)
        {
            _recentSpawnTypes.Add(type);
            if (_recentSpawnTypes.Count > RecentSpawnWindow)
                _recentSpawnTypes.RemoveAt(0);
        }

        // Mix of random X and on-course path. Crosswinds stay within buoy boundaries.
        private float PickSpawnX(float y, ZoneType type)
        {
            if (_courseMarkers == null)
                return UnityEngine.Random.Range(_xRange.x, _xRange.y);

            float courseX = _courseMarkers.CurveX(y);
            float laneHalfWidth = _courseMarkers.LaneHalfWidth;

            if (type == ZoneType.Crosswind)
            {
                float margin = laneHalfWidth * 0.15f;
                float minX = courseX - laneHalfWidth + margin;
                float maxX = courseX + laneHalfWidth - margin;
                return UnityEngine.Random.Range(minX, maxX);
            }

            if (UnityEngine.Random.value > _onCoursePathWeight)
                return UnityEngine.Random.Range(_xRange.x, _xRange.y);

            float jitter = UnityEngine.Random.Range(-3f, 3f);
            return Mathf.Clamp(courseX + jitter, _xRange.x, _xRange.y);
        }

        private void ApplyZoneShape(ZoneInstance zone, ZoneType type, float radius)
        {
            if (type == ZoneType.Crosswind)
            {
                // Horizontal oval: transform scale stretches visuals; capsule scales with it
                zone.Root.transform.localScale = new Vector3(_crosswindOvalX, _crosswindOvalY, 1f);
                zone.CircleCollider.enabled = false;
                zone.CapsuleCollider.enabled = true;
                zone.CapsuleCollider.size = new Vector2(radius * 2f, radius * 2f);
            }
            else
            {
                zone.Root.transform.localScale = Vector3.one;
                zone.CircleCollider.enabled = true;
                zone.CircleCollider.radius = radius;
                zone.CapsuleCollider.enabled = false;
            }
            zone.Effect.RefreshCollider();
        }

        private bool TooCloseToExistingZone(float y)
        {
            foreach (var z in _activeZones)
            {
                float dy = Mathf.Abs(z.BaseY - y);
                if (dy < _minZoneSpacing) return true;
            }
            return false;
        }

        private void RecycleZonesBehind(float despawnY)
        {
            for (int i = _activeZones.Count - 1; i >= 0; i--)
            {
                if (_activeZones[i].BaseY < despawnY)
                {
                    var z = _activeZones[i];
                    z.Root.SetActive(false);
                    _pool.Enqueue(z);
                    _activeZones.RemoveAt(i);
                }
            }
        }

        private ZoneInstance CreateZone()
        {
            var go = new GameObject("EnvZone");
            go.transform.SetParent(_zonesRoot);
            go.SetActive(false);

            var circleCol = go.AddComponent<CircleCollider2D>();
            circleCol.isTrigger = true;
            circleCol.radius = 4f;

            var capsuleCol = go.AddComponent<CapsuleCollider2D>();
            capsuleCol.isTrigger = true;
            capsuleCol.direction = CapsuleDirection2D.Horizontal;
            capsuleCol.size = new Vector2(4f, 2f);
            capsuleCol.enabled = false;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            var effect = go.AddComponent<EnvironmentalZoneEffect>();
            var visual = go.AddComponent<ZoneVisualController>();

            return new ZoneInstance
            {
                Root = go,
                Effect = effect,
                Visual = visual,
                CircleCollider = circleCol,
                CapsuleCollider = capsuleCol,
                BaseY = 0f,
                Radius = 4f
            };
        }
    }
}
