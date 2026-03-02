using UnityEngine;

namespace Breathe.Gameplay
{
    // Bow splash effect — small puff sprites that spawn at the front and drift back.
    // Faster speed = more frequent, larger splashes. Uses object pooling.
    // Assign a real sprite to _splashSprite or it'll generate a placeholder circle.
    public class BoatSplashEffect : MonoBehaviour
    {
        [SerializeField, Tooltip("Splash sprite. Leave empty for a procedural placeholder.")]
        private Sprite _splashSprite;

        [SerializeField] private int _poolSize = 6;

        [Header("Activation")]
        [SerializeField, Tooltip("No splashes below this speed.")]
        private float _activationSpeed = 1.5f;
        [SerializeField] private float _fullIntensitySpeed = 8f;

        [Header("Spawn")]
        [SerializeField] private float _spawnOffsetY = 0.7f;
        [SerializeField] private float _spawnSpreadX = 0.5f;
        [SerializeField] private float _driftDistance = 1.8f;
        [SerializeField] private float _driftSpeed = 2.5f;
        [SerializeField] private float _spawnInterval = 0.25f;
        [SerializeField] private float _lifetime = 0.6f;

        [Header("Scale")]
        [SerializeField] private float _minScale = 0.2f;
        [SerializeField] private float _maxScale = 0.5f;
        [SerializeField] private int _sortingOrder = 4;

        private Splash[] _pool;
        private float _spawnTimer;
        private float _speed;
        private static Sprite _placeholderSprite;

        private struct Splash
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public float Age, MaxAge, DriftMult;
            public bool Active;
        }

        public void SetSpeed(float speed) => _speed = Mathf.Max(0f, speed);

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
            float maxAlpha = Mathf.Lerp(0.2f, 0.6f, NormalizedIntensity());

            for (int i = 0; i < _pool.Length; i++)
            {
                ref Splash s = ref _pool[i];
                if (!s.Active) continue;

                s.Age += Time.deltaTime;
                float t = s.Age / s.MaxAge;

                // Drift backward
                Vector3 pos = s.Go.transform.localPosition;
                pos.y += -_driftSpeed * s.DriftMult * Time.deltaTime;
                s.Go.transform.localPosition = pos;

                // Slowly expand
                Vector3 baseScale = s.Go.transform.localScale;
                s.Go.transform.localScale = new Vector3(
                    baseScale.x * (1f + Time.deltaTime * 0.8f),
                    baseScale.y * (1f + Time.deltaTime * 0.8f), 1f);

                // Fade in, hold, fade out
                float alpha;
                if (t < 0.15f) alpha = (t / 0.15f) * maxAlpha;
                else if (t > 0.6f) alpha = Mathf.Lerp(maxAlpha, 0f, (t - 0.6f) / 0.4f);
                else alpha = maxAlpha;

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
                if (!_pool[i].Active) return i;
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

                _pool[i] = new Splash { Go = go, Sr = sr, Active = false };
            }
        }

        // Generates a soft white circle sprite if no real sprite is assigned
        private static void EnsurePlaceholderSprite()
        {
            if (_placeholderSprite != null) return;

            int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size / 2f;
            float radius = size / 2f - 1f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / radius;
                if (d <= 1f)
                {
                    float alpha = d > 0.5f ? Mathf.Lerp(1f, 0f, (d - 0.5f) / 0.5f) : 1f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
                else tex.SetPixel(x, y, Color.clear);
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
