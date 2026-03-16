using System.Collections.Generic;
using UnityEngine;

namespace Breathe.Gameplay
{
    // Spawns brown rocks in the AI lanes near buoy edges. AI boats get stunned on contact,
    // player path (center) stays clear. Pools and recycles rocks as the camera scrolls.
    public class CourseObstacleSpawner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CourseMarkers _courseMarkers;

        [Header("Spawn Region")]
        [SerializeField] private float _spawnAheadDistance = 30f;
        [SerializeField] private float _despawnBehindDistance = 15f;
        [SerializeField, Tooltip("No obstacles before this Y (grace period at start).")]
        private float _gracePeriodY = 10f;

        [Header("Placement")]
        [SerializeField] private float _minOffsetFromCenter = 5f;
        [SerializeField] private float _maxOffsetFromCenter = 10f;

        [Header("Density")]
        [SerializeField] private float _spawnInterval = 18f;
        [SerializeField, Range(0f, 1f)] private float _spawnChance = 0.3f;
        [SerializeField] private int _maxRocks = 14;

        [Header("Appearance")]
        [SerializeField] private float _minRockSize = 0.5f;
        [SerializeField] private float _maxRockSize = 1.5f;
        [SerializeField] private Sprite[] _rockSprites;
        [SerializeField] private Color _rockTint = new Color(0.4f, 0.28f, 0.2f, 1f);
        [SerializeField] private int _sortingOrder = -8;

        [Header("Test Obstacles")]
        [SerializeField] private float _testObstacleOffsetFromCenter = 4.8f;
        [SerializeField] private float _testObstacleSize = 2.2f;

        [Header("Animation")]
        [SerializeField] private float _bobAmplitude = 0.06f;
        [SerializeField] private float _bobFrequency = 0.8f;

        private Camera _cam;
        private Transform _rocksRoot;
        private float _nextSpawnY;
        private static Sprite _placeholderSprite;

        private readonly List<Rock> _activeRocks = new List<Rock>();
        private readonly Queue<Rock> _pool = new Queue<Rock>();

        private struct Rock
        {
            public GameObject Root;
            public SpriteRenderer MainSprite;
            public CircleCollider2D Collider;
            public Vector3 BasePosition;
            public float Phase;
            public float Size;
        }

        private bool UseCustomSprites => _rockSprites != null && _rockSprites.Length > 0;

        private void Start()
        {
            _cam = Camera.main;
            if (_cam == null) { Debug.LogError("[ObstacleSpawner] Camera.main is null!"); return; }

            if (_courseMarkers == null)
                _courseMarkers = FindAnyObjectByType<CourseMarkers>();

            if (!UseCustomSprites) EnsurePlaceholderSprite();

            _rocksRoot = new GameObject("--- COURSE_OBSTACLES ---").transform;
            _rocksRoot.SetParent(transform);

            _nextSpawnY = _cam.transform.position.y - _despawnBehindDistance;
            for (int i = 0; i < _maxRocks; i++) _pool.Enqueue(CreateRock());
            VerifyCollisionMatrix();
        }

        private static void VerifyCollisionMatrix()
        {
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            int aiLayer = LayerMask.NameToLayer("AI");
            if (obstacleLayer >= 0 && aiLayer >= 0 && Physics2D.GetIgnoreLayerCollision(obstacleLayer, aiLayer))
                Debug.LogWarning("[ObstacleSpawner] Obstacle-AI collision is disabled in Physics2D layer matrix.");
        }

        private static void EnsurePlaceholderSprite()
        {
            if (_placeholderSprite != null) return;
            int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f, radius = size / 2f - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x - center, dy = y - center;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = dist <= radius ? (dist > radius - 2f ? (radius - dist) / 2f : 1f) : 0f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private Sprite GetRandomRockSprite()
        {
            return UseCustomSprites ? _rockSprites[Random.Range(0, _rockSprites.Length)] : _placeholderSprite;
        }

        private void Update()
        {
            if (_cam == null) return;
            float camY = _cam.transform.position.y;

            while (_nextSpawnY < camY + _spawnAheadDistance)
            {
                TrySpawnRock(_nextSpawnY);
                _nextSpawnY += _spawnInterval;
            }
            RecycleRocksBehind(camY - _despawnBehindDistance);
            AnimateRocks();
        }

        // Spawns a guaranteed obstacle in an AI lane for testing stun collisions
        public void SpawnGuaranteedTestObstacle(float worldY, bool leftSide)
        {
            if (_pool.Count == 0) return;
            float courseX = _courseMarkers != null ? _courseMarkers.CurveX(worldY) : 0f;
            float x = leftSide ? courseX - _testObstacleOffsetFromCenter : courseX + _testObstacleOffsetFromCenter;

            var rock = _pool.Dequeue();
            rock.BasePosition = new Vector3(x, worldY, 0f);
            rock.Root.transform.position = rock.BasePosition;
            rock.Phase = Random.Range(0f, Mathf.PI * 2f);
            rock.Size = _testObstacleSize;
            ConfigureRock(ref rock);
            rock.Root.SetActive(true);
            _activeRocks.Add(rock);
        }

        private void TrySpawnRock(float y)
        {
            if (_pool.Count == 0 || y < _gracePeriodY) return;
            if (Random.value < _spawnChance) SpawnRockAt(y, true);
            if (Random.value < _spawnChance) SpawnRockAt(y, false);
        }

        private void SpawnRockAt(float y, bool leftSide)
        {
            if (_pool.Count == 0) return;
            float courseX = _courseMarkers != null ? _courseMarkers.CurveX(y) : 0f;
            float offsetX = Random.Range(_minOffsetFromCenter, _maxOffsetFromCenter);
            float x = leftSide ? courseX - offsetX : courseX + offsetX;
            float yJitter = Random.Range(-_spawnInterval * 0.3f, _spawnInterval * 0.3f);

            var rock = _pool.Dequeue();
            rock.BasePosition = new Vector3(x, y + yJitter, 0f);
            rock.Root.transform.position = rock.BasePosition;
            rock.Phase = Random.Range(0f, Mathf.PI * 2f);
            rock.Size = Random.Range(_minRockSize, _maxRockSize);
            ConfigureRock(ref rock);
            rock.Root.SetActive(true);
            _activeRocks.Add(rock);
        }

        private void RecycleRocksBehind(float despawnY)
        {
            for (int i = _activeRocks.Count - 1; i >= 0; i--)
            {
                if (_activeRocks[i].BasePosition.y < despawnY)
                {
                    var rock = _activeRocks[i];
                    rock.Root.SetActive(false);
                    _pool.Enqueue(rock);
                    _activeRocks.RemoveAt(i);
                }
            }
        }

        private void AnimateRocks()
        {
            float t = Time.time;
            for (int i = 0; i < _activeRocks.Count; i++)
            {
                var rock = _activeRocks[i];
                float bobY = Mathf.Sin(t * _bobFrequency + rock.Phase) * _bobAmplitude;
                float bobX = Mathf.Sin(t * _bobFrequency * 0.6f + rock.Phase + 1f) * _bobAmplitude * 0.3f;
                rock.Root.transform.position = new Vector3(
                    rock.BasePosition.x + bobX, rock.BasePosition.y + bobY, rock.BasePosition.z);
            }
        }

        private Rock CreateRock()
        {
            var go = new GameObject("CourseRock");
            go.transform.SetParent(_rocksRoot);
            go.SetActive(false);
            go.tag = "Obstacle";
            int obstacleLayer = LayerMask.NameToLayer("Obstacle");
            if (obstacleLayer >= 0) go.layer = obstacleLayer;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = _sortingOrder;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            go.AddComponent<ObstacleCollision>();

            return new Rock { Root = go, MainSprite = sr, Collider = col, BasePosition = Vector3.zero, Phase = 0f, Size = 1f };
        }

        private void ConfigureRock(ref Rock rock)
        {
            rock.MainSprite.sprite = GetRandomRockSprite();
            rock.MainSprite.color = _rockTint;
            rock.MainSprite.flipX = false;

            if (UseCustomSprites)
            {
                rock.Root.transform.localScale = new Vector3(rock.Size, rock.Size, 1f);
                rock.Root.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(-15f, 15f));
                if (Random.value > 0.5f) rock.MainSprite.flipX = true;
            }
            else
            {
                float scale = rock.Size * Random.Range(0.9f, 1.1f);
                rock.Root.transform.localScale = new Vector3(scale, scale * Random.Range(0.85f, 1f), 1f);
                rock.Root.transform.localRotation = Quaternion.identity;
                rock.MainSprite.color = new Color(0.35f, 0.25f, 0.18f, 0.95f);
            }

            if (rock.Collider != null) rock.Collider.radius = 0.5f;
        }
    }
}
