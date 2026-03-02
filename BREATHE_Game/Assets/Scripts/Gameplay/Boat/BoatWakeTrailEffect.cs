using UnityEngine;

namespace Breathe.Gameplay
{
    /// <summary>
    /// Water trail / wake effect that appears behind boats when they reach max speed.
    /// Spawns elongated trail sprites behind the boat that persist in world space,
    /// fade out, and return to a pool. Complements <see cref="BoatSplashEffect"/> and
    /// <see cref="BoatWindEffect"/> by adding a visible wake at high speed.
    /// </summary>
    public class BoatWakeTrailEffect : MonoBehaviour
    {
        [Header("Sprite")]
        [SerializeField, Tooltip("Trail segment sprite. Leave empty for procedural placeholder.")]
        private Sprite _trailSprite;

        [Header("Pool")]
        [SerializeField, Tooltip("Maximum simultaneous trail segments. More segments for thinner streaks.")]
        private int _poolSize = 20;

        [Header("Activation")]
        [SerializeField, Tooltip("Speed at or above which the wake trail appears. Player max ~8, AI varies.")]
        private float _maxSpeedThreshold = 7f;

        [SerializeField, Tooltip("Hysteresis: speed must drop below this to deactivate (avoids flicker).")]
        private float _deactivateThreshold = 6.5f;

        [Header("Behaviour")]
        [SerializeField, Tooltip("How far behind the boat center trail segments spawn (world units).")]
        private float _spawnOffsetBehind = 0.6f;

        [SerializeField, Tooltip("Random lateral spread when spawning. Kept narrow for streak look.")]
        private float _spawnSpreadX = 0.25f;

        [SerializeField, Tooltip("Base interval between trail spawns. Synced with wind/splash cadence.")]
        private float _spawnInterval = 0.1f;

        [SerializeField, Tooltip("Lifetime of a trail segment before it fades out.")]
        private float _segmentLifetime = 0.7f;

        [Header("Visual")]
        [SerializeField, Tooltip("Trail streak width (X scale). Thin for streak look, matches wind effect.")]
        private float _segmentWidth = 0.16f;

        [SerializeField, Tooltip("Trail streak length (Y scale) - elongated along wake direction.")]
        private float _segmentLength = 1.1f;

        [SerializeField, Tooltip("Sorting order for trail sprites.")]
        private int _sortingOrder = 3;

        private TrailSegment[] _pool;
        private Transform _poolParent;
        private float _spawnTimer;
        private float _speed;
        private bool _wasAtMaxSpeed;

        private struct TrailSegment
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public float Age;
            public float MaxAge;
            public bool Active;
        }

        /// <summary>
        /// Sets current boat speed. Called by the boat controller each frame.
        /// </summary>
        public void SetSpeed(float speed)
        {
            _speed = Mathf.Max(0f, speed);
        }

        private void Start()
        {
            EnsurePlaceholderSprite();
            BuildPool();
        }

        private void Update()
        {
            if (_speed >= _maxSpeedThreshold)
                _wasAtMaxSpeed = true;
            else if (_speed < _deactivateThreshold)
                _wasAtMaxSpeed = false;

            bool shouldSpawn = _speed >= _maxSpeedThreshold ||
                (_wasAtMaxSpeed && _speed >= _deactivateThreshold);

            if (shouldSpawn)
                UpdateSpawning();

            UpdateSegments();
        }

        private void UpdateSpawning()
        {
            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                SpawnSegment();
                _spawnTimer = _spawnInterval;
            }
        }

        private void SpawnSegment()
        {
            int idx = FindInactive();
            if (idx < 0) return;

            ref TrailSegment s = ref _pool[idx];

            // Spawn behind the boat (boat faces transform.up, behind = -up)
            Vector3 behind = -transform.up;
            float xOffset = Random.Range(-_spawnSpreadX, _spawnSpreadX);
            Vector3 spawnPos = transform.position + behind * _spawnOffsetBehind +
                transform.right * xOffset;

            s.Go.transform.SetParent(null);
            s.Go.transform.position = spawnPos;
            s.Go.transform.rotation = transform.rotation;
            s.Go.transform.localScale = new Vector3(_segmentWidth * Random.Range(0.95f, 1.05f),
                _segmentLength * Random.Range(0.95f, 1.05f), 1f);

            s.Age = 0f;
            s.MaxAge = _segmentLifetime * Random.Range(0.85f, 1.15f);
            s.Active = true;
            s.Go.SetActive(true);
            s.Sr.color = new Color(1f, 1f, 1f, 0f);
        }

        private void UpdateSegments()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                ref TrailSegment s = ref _pool[i];
                if (!s.Active) continue;

                s.Age += Time.deltaTime;
                float t = s.Age / s.MaxAge;

                float alpha;
                if (t < 0.15f)
                    alpha = (t / 0.15f) * 0.5f;
                else if (t > 0.5f)
                    alpha = Mathf.Lerp(0.5f, 0f, (t - 0.5f) / 0.5f);
                else
                    alpha = 0.5f;

                s.Sr.color = new Color(1f, 1f, 1f, alpha);

                if (t >= 1f)
                {
                    s.Active = false;
                    s.Go.SetActive(false);
                    s.Go.transform.SetParent(_poolParent);
                }
            }
        }

        private int FindInactive()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) return i;
            }
            return -1;
        }

        private void BuildPool()
        {
            Sprite sprite = _trailSprite != null ? _trailSprite : _placeholderSprite;

            _poolParent = new GameObject("WakeTrailPool").transform;
            _poolParent.SetParent(transform);
            _poolParent.localPosition = Vector3.zero;
            _poolParent.localRotation = Quaternion.identity;

            _pool = new TrailSegment[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"WakeSegment_{i}");
                go.transform.SetParent(_poolParent);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(1f, 1f, 1f, 0f);
                sr.sortingOrder = _sortingOrder;
                sr.material = new Material(Shader.Find("Sprites/Default"));

                go.SetActive(false);

                _pool[i] = new TrailSegment
                {
                    Go = go,
                    Sr = sr,
                    Active = false
                };
            }
        }

        private static Sprite _placeholderSprite;

        private static void EnsurePlaceholderSprite()
        {
            if (_placeholderSprite != null) return;

            // Thin streak: narrow width, elongated length (matches wind effect proportions)
            int w = 10, h = 48;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float cx = w / 2f, cy = h / 2f;
            float rx = Mathf.Max(0.5f, w / 2f - 1f);
            float ry = Mathf.Max(0.5f, h / 2f - 1f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / rx;
                    float dy = (y - cy) / ry;
                    float d = dx * dx + dy * dy;

                    if (d <= 1f)
                    {
                        float alpha = d > 0.3f ? Mathf.Lerp(1f, 0f, (d - 0.3f) / 0.7f) : 1f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * 0.65f));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
