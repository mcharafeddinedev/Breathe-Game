using UnityEngine;

namespace Breathe.Gameplay
{
    /// <summary>
    /// Sprite-based bow wave / wake effect. Spawns small splash puff sprites at the
    /// bow of the boat that drift backward, fade out, and return to a pool.
    /// Emission rate and sprite scale are proportional to <see cref="SetSpeed"/>.
    ///
    /// Placeholder sprite is a procedural white circle. Drop a Wind Waker-style
    /// splash sprite into <see cref="_splashSprite"/> to replace it.
    /// </summary>
    public class BoatSplashEffect : MonoBehaviour
    {
        [Header("Sprite")]
        [SerializeField, Tooltip("Splash puff sprite. Leave empty for procedural placeholder.")]
        private Sprite _splashSprite;

        [Header("Pool")]
        [SerializeField, Tooltip("Maximum simultaneous splash sprites.")]
        private int _poolSize = 6;

        [Header("Behaviour")]
        [SerializeField, Tooltip("Speed below this value produces no splashes.")]
        private float _activationSpeed = 1.5f;

        [SerializeField, Tooltip("Speed at which splash effect is at full intensity.")]
        private float _fullIntensitySpeed = 8f;

        [SerializeField, Tooltip("How far ahead of the boat center splashes spawn (local Y).")]
        private float _spawnOffsetY = 0.7f;

        [SerializeField, Tooltip("Random lateral spread when spawning splashes.")]
        private float _spawnSpreadX = 0.5f;

        [SerializeField, Tooltip("How far backward splashes drift before fading (local Y, negative = behind).")]
        private float _driftDistance = 1.8f;

        [SerializeField, Tooltip("Base drift speed of splashes.")]
        private float _driftSpeed = 2.5f;

        [SerializeField, Tooltip("Base interval between splash spawns at full speed.")]
        private float _spawnInterval = 0.25f;

        [SerializeField, Tooltip("Lifetime of a single splash puff before it recycles.")]
        private float _lifetime = 0.6f;

        [Header("Visual Scaling")]
        [SerializeField, Tooltip("Splash sprite scale at minimum speed.")]
        private float _minScale = 0.2f;

        [SerializeField, Tooltip("Splash sprite scale at full speed.")]
        private float _maxScale = 0.5f;

        [SerializeField, Tooltip("Sorting order for splash sprites.")]
        private int _sortingOrder = 4;

        private Splash[] _pool;
        private float _spawnTimer;
        private float _speed;

        private static Sprite _placeholderSprite;

        private struct Splash
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public float Age;
            public float MaxAge;
            public float DriftMult;
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
            float intensity = NormalizedIntensity();

            if (intensity > 0f)
                UpdateSpawning(intensity);

            UpdateSplashes();
        }

        private void UpdateSpawning(float intensity)
        {
            float interval = Mathf.Lerp(_spawnInterval * 2.5f, _spawnInterval, intensity);

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                SpawnSplash(intensity);
                _spawnTimer = interval;
            }
        }

        private void SpawnSplash(float intensity)
        {
            int idx = FindInactive();
            if (idx < 0) return;

            ref Splash s = ref _pool[idx];

            float xOffset = Random.Range(-_spawnSpreadX, _spawnSpreadX);
            s.Go.transform.localPosition = new Vector3(xOffset, _spawnOffsetY, 0f);
            s.Go.transform.localRotation = Quaternion.identity;

            float scale = Mathf.Lerp(_minScale, _maxScale, intensity) * Random.Range(0.8f, 1.2f);
            s.Go.transform.localScale = new Vector3(scale, scale, 1f);

            s.Age = 0f;
            float distanceLife = _driftDistance / (_driftSpeed * s.DriftMult);
            s.MaxAge = Mathf.Min(_lifetime, distanceLife) * Random.Range(0.8f, 1.2f);
            s.DriftMult = Random.Range(0.7f, 1.3f);
            s.Active = true;
            s.Go.SetActive(true);
            s.Sr.color = new Color(1f, 1f, 1f, 0f);
        }

        private void UpdateSplashes()
        {
            float intensity = NormalizedIntensity();
            float maxAlpha = Mathf.Lerp(0.2f, 0.6f, intensity);

            for (int i = 0; i < _pool.Length; i++)
            {
                ref Splash s = ref _pool[i];
                if (!s.Active) continue;

                s.Age += Time.deltaTime;
                float t = s.Age / s.MaxAge;

                float dy = -_driftSpeed * s.DriftMult * Time.deltaTime;
                Vector3 pos = s.Go.transform.localPosition;
                pos.y += dy;

                float expandScale = 1f + t * 0.4f;
                Vector3 baseScale = s.Go.transform.localScale;
                s.Go.transform.localScale = new Vector3(
                    baseScale.x * (1f + Time.deltaTime * 0.8f),
                    baseScale.y * (1f + Time.deltaTime * 0.8f),
                    1f);

                s.Go.transform.localPosition = pos;

                float alpha;
                if (t < 0.15f)
                    alpha = (t / 0.15f) * maxAlpha;
                else if (t > 0.6f)
                    alpha = Mathf.Lerp(maxAlpha, 0f, (t - 0.6f) / 0.4f);
                else
                    alpha = maxAlpha;

                s.Sr.color = new Color(1f, 1f, 1f, alpha);

                if (t >= 1f)
                {
                    s.Active = false;
                    s.Go.SetActive(false);
                }
            }
        }

        private float NormalizedIntensity()
        {
            if (_speed < _activationSpeed) return 0f;
            return Mathf.Clamp01((_speed - _activationSpeed) / (_fullIntensitySpeed - _activationSpeed));
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
            Sprite sprite = _splashSprite != null ? _splashSprite : _placeholderSprite;

            var parent = new GameObject("BowSplashes");
            parent.transform.SetParent(transform, false);
            parent.transform.localPosition = Vector3.zero;
            parent.transform.localRotation = Quaternion.identity;

            _pool = new Splash[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"Splash_{i}");
                go.transform.SetParent(parent.transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(1f, 1f, 1f, 0f);
                sr.sortingOrder = _sortingOrder;
                sr.material = new Material(Shader.Find("Sprites/Default"));

                go.SetActive(false);

                _pool[i] = new Splash
                {
                    Go = go,
                    Sr = sr,
                    Active = false
                };
            }
        }

        private static void EnsurePlaceholderSprite()
        {
            if (_placeholderSprite != null) return;

            int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = size / 2f - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) / radius;

                    if (d <= 1f)
                    {
                        float alpha = d > 0.5f ? Mathf.Lerp(1f, 0f, (d - 0.5f) / 0.5f) : 1f;
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
