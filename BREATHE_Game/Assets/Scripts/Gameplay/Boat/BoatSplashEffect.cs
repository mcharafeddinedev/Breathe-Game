using UnityEngine;
using Breathe.Audio;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Bow splash effect — small puff sprites that spawn at the front and drift back.
    // Faster speed = more frequent, larger splashes. Uses object pooling.
    // Assign a real sprite to _splashSprite or it'll generate a placeholder circle.
    // Bow splash VFX and bow splash sting SFX. Controllers must call TickSplashFrameAfterWind() immediately
    // after SetSpeed + SetSplashWindDrive so wind/splash line up each frame (see TickSplashFrameAfterWind).
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

        [Header("Splash one-shot audio (SecondaryAction clip)")]
        [SerializeField, Tooltip("Player boat: BreathPower gate (0–1). Above this: bow splash sting can audition; crossing UP past this fires a guaranteed sting once per gust (rising edge).")]
        float _splashWindThresholdPlayer = 0.8f;

        [SerializeField, Tooltip("AI companion boats: simulated gust strength must exceed this (e.g. 0.6 = strong sail only).")]
        float _splashWindThresholdEnemy = 0.6f;

        [SerializeField, Range(0f, 1f), Tooltip("How much normalized boat speed vs sailing power (Breath) shapes splash volume. Player uses max(speed, breath) so strong blow is not silenced by low hull speed.")]
        float _splashSfxSpeedWeight = 0.35f;

        [SerializeField, Range(0.15f, 0.6f), Tooltip("Player: pass chance per splash puff once over wind threshold (supplementary sting; cross-threshold sting is deterministic).")]
        private float _splashSfxProbability = 0.5f;

        [SerializeField, Tooltip("Min seconds between optional puff-triggered splash stings (player). Cross-threshold stings use a separate cooldown.")]
        private float _splashSfxMinInterval = 1.05f;

        [SerializeField, Tooltip("Min seconds between player rising-edge splash stings (debounces jitter riding the threshold).")]
        private float _playerWindCrossSplashMinInterval = 0.35f;

        [SerializeField, Range(0.8f, 1.65f), Tooltip("Extra multiplier for the guaranteed rising-edge splash at the wind threshold.")]
        private float _playerWindCrossSplashGainMul = 1.35f;

        [SerializeField, Tooltip("Seconds to suppress puff-roll splash audio after a threshold-cross sting (avoid double-hit same frame cluster).")]
        private float _suppressPuffSplashSfxAfterCrossDuration = 0.18f;

        [SerializeField, Tooltip("Min seconds between splash stings (AI).")]
        private float _splashSfxMinIntervalEnemy = 1.55f;

        [SerializeField, Range(0.02f, 1f), Tooltip("Tighter rolls for AI — keeps enemy splashes sparse vs player.")]
        private float _splashSfxProbabilityEnemy = 0.12f;

        [SerializeField, Range(0.05f, 1.5f), Tooltip("Scales clip vs SfxPlayer bus (ocean wave layered on procedural wash).")]
        private float _splashSfxGain = 1.0f;

        private Splash[] _pool;
        private float _spawnTimer;
        private float _speed;
        /// <summary>0–1 wind/streak input; player BreathPower; AI simulated gust (matches BoatWind).</summary>
        private float _splashWindDrive01;
        private float _splashWindDrivePrev01 = -1f;
        private float _nextSplashSfxAllowed;
        private float _nextPlayerWindCrossSplashAllowed;
        private float _suppressPuffSplashSfxUntilUnscaledTime;
        private static Sprite _placeholderSprite;

        private struct Splash
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public float Age, MaxAge, DriftMult;
            public bool Active;
        }

        public void SetSpeed(float speed) => _speed = Mathf.Max(0f, speed);

        /// <summary>Splash loudness aligns with BoatWind streak band; call every frame with BreathPower.</summary>
        public void SetSplashWindDrive(float breathOrSimulatedWind01) =>
            _splashWindDrive01 = Mathf.Clamp01(breathOrSimulatedWind01);

        private void Start()
        {
            EnsurePlaceholderSprite();
            BuildPool();
        }

        /// <summary>
        /// Boat controllers must call once per frame after <see cref="SetSpeed"/> / <see cref="SetSplashWindDrive"/>.
        /// </summary>
        public void TickSplashFrameAfterWind()
        {
            float wind01 = _splashWindDrive01;
            float prev01 = _splashWindDrivePrev01;

            TryPlayPlayerBowSplashWindThresholdCross(prev01, wind01);

            float intensity = NormalizedIntensity();
            if (intensity > 0f)
                UpdateSpawning(intensity);

            _splashWindDrivePrev01 = wind01;
        }

        private void Update()
        {
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

            TryPlayBowSplashSound(intensity, s.Go.transform.position);
        }

        void TryPlayPlayerBowSplashWindThresholdCross(float prevWind01, float wind01)
        {
            if (!CompareTag("Player")) return;

            float thr = _splashWindThresholdPlayer;

            float eps = Mathf.Max(5e-4f, thr * 0.01f);
            bool hadPrevWindSample = prevWind01 >= 0f;
            bool crossed = hadPrevWindSample && prevWind01 < thr - eps && wind01 >= thr;

            if (!crossed) return;
            if (Time.unscaledTime < _nextPlayerWindCrossSplashAllowed) return;

            if (!ResolveSailboatSecondaryActionClip(out AudioClip clip)) return;

            Vector3 splashWorldPosition = BowSplashEmitWorldPosition();

            SfxPlayer.EnsureInstance();
            SfxPlayer sfx = SfxPlayer.Instance;
            if (sfx == null) return;

            float windTier = Mathf.InverseLerp(thr, 1f, Mathf.Clamp01(wind01));
            float punch = Mathf.Lerp(0.78f, 1f, windTier);
            sfx.PlayClipSpatial(clip,
                _splashSfxGain * _playerWindCrossSplashGainMul * punch,
                splashWorldPosition);

            float jitterRoll = Mathf.Lerp(0.97f, 1.03f, Random.value);
            _nextPlayerWindCrossSplashAllowed = Time.unscaledTime + Mathf.Max(0.05f, _playerWindCrossSplashMinInterval);
            _suppressPuffSplashSfxUntilUnscaledTime =
                Time.unscaledTime + Mathf.Max(0f, _suppressPuffSplashSfxAfterCrossDuration) * jitterRoll;
        }

        Vector3 BowSplashEmitWorldPosition() =>
            transform.TransformPoint(new Vector3(0f, _spawnOffsetY, 0f));

        bool ResolveSailboatSecondaryActionClip(out AudioClip clip)
        {
            clip = null;

            var mm = MinigameManager.Instance;
            MinigameDefinition def = null;
            if (mm != null)
            {
                var ig = mm.ActiveMinigame;
                if (ig != null)
                    def = mm.GetDefinitionById(ig.MinigameId);

                if (def == null && mm.SelectedDefinition != null)
                    def = mm.SelectedDefinition;

                if (def == null && mm.AvailableMinigames != null)
                {
                    foreach (var d in mm.AvailableMinigames)
                    {
                        if (d != null && d.MinigameId == "sailboat") { def = d; break; }
                    }
                }
            }

            clip = def?.MinigameSfxProfile?.SecondaryAction;
            return clip != null;
        }

        void TryPlayBowSplashSound(float speedIntensity, Vector3 splashWorldPosition)
        {
            bool playerBoat = CompareTag("Player");
            float thr = playerBoat ? _splashWindThresholdPlayer : _splashWindThresholdEnemy;
            if (_splashWindDrive01 < thr) return;

            float windTier =
                Mathf.InverseLerp(thr, 1f, _splashWindDrive01);
            float speed01 = Mathf.Clamp01(speedIntensity);
            // High breath but low hull speed no longer drives "audible" to ~0 near the wind threshold.
            float motionBlend = playerBoat
                ? Mathf.Max(speed01, _splashWindDrive01)
                : speed01;
            float motionFactor = Mathf.Lerp(_splashSfxSpeedWeight, 1f, motionBlend);
            float audible = Mathf.Clamp01(windTier * motionFactor);

            if (Time.unscaledTime < _nextSplashSfxAllowed) return;
            if (playerBoat && Time.unscaledTime < _suppressPuffSplashSfxUntilUnscaledTime) return;

            if (Random.value > (playerBoat ? _splashSfxProbability : _splashSfxProbabilityEnemy)) return;

            if (!ResolveSailboatSecondaryActionClip(out AudioClip clip)) return;

            SfxPlayer.EnsureInstance();
            SfxPlayer sfx = SfxPlayer.Instance;
            if (sfx == null) return;

            float body = Mathf.Lerp(playerBoat ? 0.62f : 0.42f, 1f, audible);
            float jitter = Mathf.Lerp(0.96f, 1.04f, Random.value);
            sfx.PlayClipSpatial(clip, _splashSfxGain * body * jitter, splashWorldPosition);

            _nextSplashSfxAllowed = Time.unscaledTime +
                                    (playerBoat ? _splashSfxMinInterval : _splashSfxMinIntervalEnemy);
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
