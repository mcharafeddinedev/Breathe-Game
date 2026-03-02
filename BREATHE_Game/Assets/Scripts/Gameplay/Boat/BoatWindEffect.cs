using UnityEngine;

namespace Breathe.Gameplay
{
    /// <summary>
    /// Sprite-based wind gust visual effect that spawns behind a boat and drifts
    /// forward past the sail. Driven by <see cref="WindSystem.WindPower"/> for the
    /// player or a manual intensity setter for AI boats.
    ///
    /// Streaks are pooled to avoid runtime allocation. The number of active streaks,
    /// their speed, opacity, and scale all scale continuously with wind power.
    /// A "gust burst" fires extra streaks on the rising edge (idle to active transition).
    ///
    /// The placeholder sprite is a procedural white ellipse. Drop a Wind Waker-style
    /// swoosh sprite into <see cref="_windStreakSprite"/> to replace it.
    /// </summary>
    public class BoatWindEffect : MonoBehaviour
    {
        [Header("Sprite")]
        [SerializeField, Tooltip("Wind streak sprite. Leave empty for procedural placeholder.")]
        private Sprite _windStreakSprite;

        [Header("Pool")]
        [SerializeField, Tooltip("Maximum simultaneous wind streaks.")]
        private int _poolSize = 8;

        [Header("Behaviour")]
        [SerializeField, Tooltip("Wind power below this value produces no streaks.")]
        private float _activationThreshold = 0.15f;

        [SerializeField, Tooltip("How far behind the boat streaks spawn (local Y, negative = behind).")]
        private float _spawnOffsetY = -1.2f;

        [SerializeField, Tooltip("Random lateral spread when spawning streaks.")]
        private float _spawnSpreadX = 0.8f;

        [SerializeField, Tooltip("How far forward streaks travel before fading out (local Y).")]
        private float _travelDistance = 2.5f;

        [SerializeField, Tooltip("Base drift speed of streaks at full wind power.")]
        private float _driftSpeed = 4f;

        [SerializeField, Tooltip("Base interval between streak spawns at full wind power.")]
        private float _spawnInterval = 0.18f;

        [SerializeField, Tooltip("Duration of the fade-in phase.")]
        private float _fadeInDuration = 0.1f;

        [SerializeField, Tooltip("Duration of the fade-out phase.")]
        private float _fadeOutDuration = 0.2f;

        [Header("Gust Burst")]
        [SerializeField, Tooltip("Extra streaks spawned on the rising edge (idle to active).")]
        private int _gustBurstCount = 3;

        [Header("Visual Scaling")]
        [SerializeField, Tooltip("Streak X scale at minimum wind.")]
        private float _minStreakScaleX = 0.3f;

        [SerializeField, Tooltip("Streak X scale at full wind.")]
        private float _maxStreakScaleX = 0.7f;

        [SerializeField, Tooltip("Streak Y scale (length). Stretched along drift direction.")]
        private float _streakScaleY = 0.15f;

        [SerializeField, Tooltip("Sorting order for streak sprites.")]
        private int _sortingOrder = 5;

        private Streak[] _pool;
        private float _spawnTimer;
        private float _windIntensity;
        private bool _wasActive;
        private WindSystem _windSystem;
        private bool _useExternalIntensity;

        private static Sprite _placeholderSprite;

        private struct Streak
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public float LocalStartY;
            public float Progress;
            public float Lifetime;
            public float SpeedMult;
            public bool Active;
        }

        /// <summary>
        /// Sets wind intensity manually (used by AI boats).
        /// When called, the component stops reading from WindSystem.
        /// </summary>
        public void SetWindIntensity(float intensity)
        {
            _useExternalIntensity = true;
            _windIntensity = Mathf.Clamp01(intensity);
        }

        private void Start()
        {
            if (!_useExternalIntensity)
                _windSystem = FindAnyObjectByType<WindSystem>();

            EnsurePlaceholderSprite();
            BuildPool();
        }

        private void Update()
        {
            if (!_useExternalIntensity && _windSystem != null)
                _windIntensity = _windSystem.WindPower;

            bool isActive = _windIntensity >= _activationThreshold;

            if (isActive && !_wasActive)
                SpawnGustBurst();

            _wasActive = isActive;

            if (isActive)
                UpdateSpawning();

            UpdateStreaks();
        }

        private void UpdateSpawning()
        {
            float power = NormalizedPower();
            float interval = Mathf.Lerp(_spawnInterval * 3f, _spawnInterval, power);

            _spawnTimer -= Time.deltaTime;
            if (_spawnTimer <= 0f)
            {
                SpawnStreak();
                _spawnTimer = interval;
            }
        }

        private void SpawnGustBurst()
        {
            for (int i = 0; i < _gustBurstCount; i++)
                SpawnStreak();

            _spawnTimer = _spawnInterval * 0.5f;
        }

        private void SpawnStreak()
        {
            int idx = FindInactiveStreak();
            if (idx < 0) return;

            ref Streak s = ref _pool[idx];
            float power = NormalizedPower();

            float xOffset = Random.Range(-_spawnSpreadX, _spawnSpreadX) * (0.5f + power * 0.5f);
            float yJitter = Random.Range(-0.2f, 0.2f);
            s.Go.transform.localPosition = new Vector3(xOffset, _spawnOffsetY + yJitter, 0f);
            s.Go.transform.localRotation = Quaternion.identity;

            float scaleX = Mathf.Lerp(_minStreakScaleX, _maxStreakScaleX, power);
            scaleX *= Random.Range(0.8f, 1.2f);
            s.Go.transform.localScale = new Vector3(scaleX, _streakScaleY * Random.Range(0.8f, 1.2f), 1f);

            s.LocalStartY = s.Go.transform.localPosition.y;
            s.Progress = 0f;
            s.Lifetime = 0f;
            s.SpeedMult = Random.Range(0.7f, 1.3f);
            s.Active = true;
            s.Go.SetActive(true);
            s.Sr.color = new Color(1f, 1f, 1f, 0f);
        }

        private void UpdateStreaks()
        {
            float power = NormalizedPower();
            float speed = _driftSpeed * (0.4f + power * 0.6f);
            float maxAlpha = Mathf.Lerp(0.25f, 0.75f, power);

            for (int i = 0; i < _pool.Length; i++)
            {
                ref Streak s = ref _pool[i];
                if (!s.Active) continue;

                s.Lifetime += Time.deltaTime;
                float dy = speed * s.SpeedMult * Time.deltaTime;
                Vector3 pos = s.Go.transform.localPosition;
                pos.y += dy;
                s.Go.transform.localPosition = pos;

                float traveled = pos.y - s.LocalStartY;
                s.Progress = Mathf.Clamp01(traveled / _travelDistance);

                float fadeOutFrac = Mathf.Clamp01(
                    (_fadeOutDuration * speed * s.SpeedMult) / _travelDistance);
                float fadeOutStart = 1f - fadeOutFrac;

                float alpha;
                if (s.Lifetime < _fadeInDuration)
                    alpha = (s.Lifetime / _fadeInDuration) * maxAlpha;
                else if (s.Progress > fadeOutStart && fadeOutFrac > 0f)
                    alpha = Mathf.Lerp(maxAlpha, 0f, (s.Progress - fadeOutStart) / fadeOutFrac);
                else
                    alpha = maxAlpha;

                s.Sr.color = new Color(1f, 1f, 1f, alpha);

                if (s.Progress >= 1f)
                {
                    s.Active = false;
                    s.Go.SetActive(false);
                }
            }
        }

        private float NormalizedPower()
        {
            if (_windIntensity < _activationThreshold) return 0f;
            return Mathf.Clamp01((_windIntensity - _activationThreshold) / (1f - _activationThreshold));
        }

        private int FindInactiveStreak()
        {
            for (int i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) return i;
            }
            return -1;
        }

        private void BuildPool()
        {
            Sprite sprite = _windStreakSprite != null ? _windStreakSprite : _placeholderSprite;

            var parent = new GameObject("WindStreaks");
            parent.transform.SetParent(transform, false);
            parent.transform.localPosition = Vector3.zero;
            parent.transform.localRotation = Quaternion.identity;

            _pool = new Streak[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"Streak_{i}");
                go.transform.SetParent(parent.transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(1f, 1f, 1f, 0f);
                sr.sortingOrder = _sortingOrder;
                sr.material = new Material(Shader.Find("Sprites/Default"));

                go.SetActive(false);

                _pool[i] = new Streak
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

            int w = 32, h = 12;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float cx = w / 2f, cy = h / 2f;
            float rx = w / 2f - 1f, ry = h / 2f - 1f;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / rx;
                    float dy = (y - cy) / ry;
                    float d = dx * dx + dy * dy;

                    if (d <= 1f)
                    {
                        float alpha = d > 0.6f ? Mathf.Lerp(1f, 0f, (d - 0.6f) / 0.4f) : 1f;
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
            _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 16f);
        }
    }
}
