using System.Collections.Generic;
using UnityEngine;

namespace Breathe.Gameplay
{
    // Spawns decorative rocky islands outside the race course boundaries.
    // Islands are procedurally shaped, pooled, and recycled as the camera scrolls.
    public class ScenerySpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CourseMarkers _courseMarkers;

        [Header("Spawn Region")]
        [SerializeField] private float _minDistanceFromCourse = 4f;
        [SerializeField] private float _maxDistanceFromCourse = 25f;
        [SerializeField] private float _spawnAheadDistance = 30f;
        [SerializeField] private float _despawnBehindDistance = 15f;

        [Header("Density")]
        [SerializeField] private float _spawnInterval = 12f;
        [SerializeField, Range(0f, 1f), Tooltip("Lower = sparser island distribution.")]
        private float _spawnChance = 0.4f;
        [SerializeField] private int _maxIslands = 20;

        [Header("Island Appearance")]
        [SerializeField] private float _minIslandSize = 1.5f;
        [SerializeField] private float _maxIslandSize = 4f;
        [SerializeField, Tooltip("Leave empty to use procedural placeholder.")]
        private Sprite[] _islandSprites;
        [SerializeField] private Color _islandTint = Color.white;
        [SerializeField, Tooltip("Sorting order (-8 = in front of water waves, behind buoys).")]
        private int _sortingOrder = -8;

        [Header("Animation")]
        [SerializeField] private float _bobAmplitude = 0.08f;
        [SerializeField] private float _bobFrequency = 0.8f;

        private Camera _cam;
        private Transform _islandsRoot;
        private float _nextSpawnY;
        private float _laneHalfWidth = 12f;

        private static Sprite _placeholderSprite;

        private readonly List<Island> _activeIslands = new List<Island>();
        private readonly Queue<Island> _pool = new Queue<Island>();

        private struct Island
        {
            public GameObject Root;
            public SpriteRenderer MainSprite;
            public CircleCollider2D Collider;
            public Vector3 BasePosition;
            public float Phase;
            public float Size;
        }

        private bool UseCustomSprites => _islandSprites != null && _islandSprites.Length > 0;

        private void Start()
        {
            _cam = Camera.main;

            if (_cam == null)
            {
                Debug.LogError("[ScenerySpawner] Camera.main is null! Islands won't spawn.");
                return;
            }

            if (_courseMarkers == null)
                _courseMarkers = FindAnyObjectByType<CourseMarkers>();

            if (_courseMarkers == null)
                Debug.LogWarning("[ScenerySpawner] CourseMarkers not found. Islands will spawn at X=0.");

            if (!UseCustomSprites)
                EnsurePlaceholderSprite();

            _islandsRoot = new GameObject("--- SCENERY_ISLANDS ---").transform;
            _islandsRoot.SetParent(transform);

            float camY = _cam.transform.position.y;
            _nextSpawnY = camY - _despawnBehindDistance;

            for (int i = 0; i < _maxIslands; i++)
            {
                _pool.Enqueue(CreateIsland());
            }

            Debug.Log($"[ScenerySpawner] Initialized. Camera Y: {camY}, NextSpawnY: {_nextSpawnY}, Pool: {_maxIslands}, CourseMarkers: {(_courseMarkers != null ? "Found" : "NULL")}");
        }

        private static void EnsurePlaceholderSprite()
        {
            if (_placeholderSprite != null) return;

            int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = size / 2f - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist <= radius)
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, 1f));
                    else
                        tex.SetPixel(x, y, Color.clear);
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private Sprite GetRandomIslandSprite()
        {
            if (UseCustomSprites)
                return _islandSprites[Random.Range(0, _islandSprites.Length)];
            return _placeholderSprite;
        }

        private void Update()
        {
            if (_cam == null) return;

            float camY = _cam.transform.position.y;
            float spawnLimit = camY + _spawnAheadDistance;
            float despawnLimit = camY - _despawnBehindDistance;

            while (_nextSpawnY < spawnLimit)
            {
                TrySpawnIsland(_nextSpawnY);
                _nextSpawnY += _spawnInterval;
            }

            RecycleIslandsBehind(despawnLimit);
            AnimateIslands();
        }

        private void TrySpawnIsland(float y)
        {
            if (_pool.Count == 0) return;

            if (Random.value < _spawnChance)
                SpawnIslandAt(y, true);

            if (Random.value < _spawnChance)
                SpawnIslandAt(y, false);
        }

        private void SpawnIslandAt(float y, bool leftSide)
        {
            if (_pool.Count == 0) return;

            float courseX = _courseMarkers != null ? _courseMarkers.CurveX(y) : 0f;
            float minX = _laneHalfWidth + _minDistanceFromCourse;
            float maxX = _maxDistanceFromCourse;

            float offsetX = Random.Range(minX, maxX);
            float x = leftSide ? courseX - offsetX : courseX + offsetX;

            float yJitter = Random.Range(-_spawnInterval * 0.4f, _spawnInterval * 0.4f);

            var island = _pool.Dequeue();
            island.BasePosition = new Vector3(x, y + yJitter, 0f);
            island.Root.transform.position = island.BasePosition;
            island.Phase = Random.Range(0f, Mathf.PI * 2f);
            island.Size = Random.Range(_minIslandSize, _maxIslandSize);

            ConfigureIsland(ref island);

            island.Root.SetActive(true);
            _activeIslands.Add(island);

            Debug.Log($"[ScenerySpawner] Spawned island at ({x:F1}, {y:F1}), size: {island.Size:F1}, active: {_activeIslands.Count}");
        }

        private void RecycleIslandsBehind(float despawnY)
        {
            for (int i = _activeIslands.Count - 1; i >= 0; i--)
            {
                if (_activeIslands[i].BasePosition.y < despawnY)
                {
                    var island = _activeIslands[i];
                    island.Root.SetActive(false);
                    _pool.Enqueue(island);
                    _activeIslands.RemoveAt(i);
                }
            }
        }

        private void AnimateIslands()
        {
            float t = Time.time;

            for (int i = 0; i < _activeIslands.Count; i++)
            {
                var island = _activeIslands[i];
                float bobY = Mathf.Sin(t * _bobFrequency + island.Phase) * _bobAmplitude;
                float bobX = Mathf.Sin(t * _bobFrequency * 0.6f + island.Phase + 1f) * _bobAmplitude * 0.3f;

                island.Root.transform.position = new Vector3(
                    island.BasePosition.x + bobX,
                    island.BasePosition.y + bobY,
                    island.BasePosition.z
                );
            }
        }

        private Island CreateIsland()
        {
            var go = new GameObject("Island");
            go.transform.SetParent(_islandsRoot);
            go.SetActive(false);
            go.tag = "Obstacle";
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer >= 0)
                go.layer = obstacleLayer;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = _sortingOrder;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            go.AddComponent<ObstacleCollision>();

            return new Island
            {
                Root = go,
                MainSprite = sr,
                Collider = col,
                BasePosition = Vector3.zero,
                Phase = 0f,
                Size = 1f
            };
        }

        private void ConfigureIsland(ref Island island)
        {
            float size = island.Size;

            island.MainSprite.sprite = GetRandomIslandSprite();
            island.MainSprite.color = new Color(_islandTint.r, _islandTint.g, _islandTint.b, 1f);
            island.MainSprite.flipX = false;

            if (UseCustomSprites)
            {
                island.Root.transform.localScale = new Vector3(size, size, 1f);
                island.Root.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-10f, 10f));
                if (Random.value > 0.5f)
                    island.MainSprite.flipX = true;
            }
            else
            {
                float scaleVariation = Random.Range(0.85f, 1.15f);
                float scale = size * scaleVariation;
                island.Root.transform.localScale = new Vector3(scale, scale * Random.Range(0.8f, 1.0f), 1f);
                island.Root.transform.localRotation = Quaternion.identity;
                island.MainSprite.color = new Color(0.15f, 0.12f, 0.10f, 1f);
            }

            // Collider at ~70% of visual size so boats clip slightly before triggering
            if (island.Collider != null)
                island.Collider.radius = 0.35f;
        }
    }
}
