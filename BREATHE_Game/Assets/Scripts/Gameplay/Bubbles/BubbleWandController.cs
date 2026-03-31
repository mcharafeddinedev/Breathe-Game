using System.Collections.Generic;
using UnityEngine;

namespace Breathe.Gameplay
{
    // Manages the bubble wand, bubble spawning/lifecycle, and visual feedback
    // for the three intensity zones (too gentle, sweet spot, too aggressive).
    public class BubbleWandController : MonoBehaviour
    {
        [Header("Sweet Spot")]
        [SerializeField] private float _sweetSpotMin = 0.30f;
        [SerializeField] private float _sweetSpotMax = 0.60f;
        [SerializeField] private float _gentleMin = 0.05f;

        [Header("Bubble Spawning")]
        [SerializeField] private float _sweetSpotSpawnRate = 4f;
        [SerializeField] private float _gentleSpawnRate = 1.5f;
        [SerializeField] private float _splashCooldown = 0.3f;
        [SerializeField] private int _maxActiveBubbles = 80;

        [Header("Bubble Physics")]
        [SerializeField] private float _riseSpeed = 1.8f;
        [SerializeField] private float _wobbleAmplitude = 0.4f;
        [SerializeField] private float _wobbleFrequency = 2.5f;
        [SerializeField] private float _sweetSpotLifetime = 8f;
        [SerializeField] private float _gentleBubbleLifetime = 1.5f;

        [Header("Visuals")]
        [SerializeField] private Color[] _bubbleColors = {
            new Color(0.6f, 0.85f, 1f, 0.7f),
            new Color(0.85f, 0.6f, 1f, 0.7f),
            new Color(0.6f, 1f, 0.75f, 0.7f),
            new Color(1f, 0.8f, 0.6f, 0.7f),
            new Color(1f, 0.6f, 0.8f, 0.7f)
        };

        public enum BreathZone { None, TooGentle, SweetSpot, TooAggressive }

        private struct Bubble
        {
            public GameObject Obj;
            public SpriteRenderer Renderer;
            public float Lifetime;
            public float MaxLifetime;
            public float WobblePhase;
            public float BaseX;
            public float Size;
            public bool IsSweetSpot;
        }

        private List<Bubble> _activeBubbles = new();
        private float _spawnTimer;
        private float _splashTimer;
        private BreathZone _currentZone;
        private bool _active;

        // Scene objects
        private GameObject _wandObj;
        private LineRenderer _wandRing;
        private LineRenderer _wandHandle;
        private List<GameObject> _splashParticles = new();
        private float _splashAnimTimer;
        private GameObject _backgroundObj;

        private static Material _spriteMat;
        private Texture2D _bubbleTex;

        // Tracking
        private int _sweetSpotBubblesProduced;
        private float _sweetSpotStreakTime;
        private float _longestStreak;
        private int _currentStreakBubbles;
        private int _longestStreakBubbles;

        public BreathZone CurrentZone => _currentZone;
        public int SweetSpotBubblesProduced => _sweetSpotBubblesProduced;
        public float LongestStreak => _longestStreak;
        public int LongestStreakBubbles => _longestStreakBubbles;
        public int CurrentStreakBubbles => _currentStreakBubbles;

        public void Initialize()
        {
            EnsureMaterial();
            _bubbleTex = GenerateBubbleTexture(64);

            BuildBackground();
            BuildWand();
        }

        public void Activate() => _active = true;

        public void UpdateBreath(float breathPower)
        {
            if (!_active) return;

            // Classify breath zone
            BreathZone prevZone = _currentZone;
            if (breathPower < _gentleMin)
                _currentZone = BreathZone.None;
            else if (breathPower < _sweetSpotMin)
                _currentZone = BreathZone.TooGentle;
            else if (breathPower <= _sweetSpotMax)
                _currentZone = BreathZone.SweetSpot;
            else
                _currentZone = BreathZone.TooAggressive;

            // Track sweet-spot streaks
            if (_currentZone == BreathZone.SweetSpot)
            {
                _sweetSpotStreakTime += Time.deltaTime;
                if (_sweetSpotStreakTime > _longestStreak)
                    _longestStreak = _sweetSpotStreakTime;
            }
            else
            {
                if (_sweetSpotStreakTime > 0f)
                {
                    if (_currentStreakBubbles > _longestStreakBubbles)
                        _longestStreakBubbles = _currentStreakBubbles;
                    _currentStreakBubbles = 0;
                }
                _sweetSpotStreakTime = 0f;
            }

            // Wand visual feedback
            UpdateWandFeedback(breathPower);

            // Spawn bubbles based on zone
            _spawnTimer += Time.deltaTime;
            _splashTimer += Time.deltaTime;

            switch (_currentZone)
            {
                case BreathZone.SweetSpot:
                    float spawnInterval = 1f / _sweetSpotSpawnRate;
                    while (_spawnTimer >= spawnInterval)
                    {
                        _spawnTimer -= spawnInterval;
                        SpawnBubble(true, breathPower);
                    }
                    break;

                case BreathZone.TooGentle:
                    float gentleInterval = 1f / _gentleSpawnRate;
                    while (_spawnTimer >= gentleInterval)
                    {
                        _spawnTimer -= gentleInterval;
                        SpawnBubble(false, breathPower);
                    }
                    break;

                case BreathZone.TooAggressive:
                    _spawnTimer = 0f;
                    if (_splashTimer >= _splashCooldown)
                    {
                        _splashTimer = 0f;
                        SpawnSplash(breathPower);
                    }
                    break;

                default:
                    _spawnTimer = 0f;
                    break;
            }

            UpdateBubbles();
            UpdateSplashParticles();
        }

        public void ResetTracking()
        {
            _sweetSpotBubblesProduced = 0;
            _sweetSpotStreakTime = 0f;
            _longestStreak = 0f;
            _currentStreakBubbles = 0;
            _longestStreakBubbles = 0;
        }

        private void SpawnBubble(bool isSweetSpot, float power)
        {
            if (_activeBubbles.Count >= _maxActiveBubbles) return;

            var obj = new GameObject("Bubble");
            obj.transform.SetParent(transform);
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(_bubbleTex,
                new Rect(0, 0, _bubbleTex.width, _bubbleTex.height),
                new Vector2(0.5f, 0.5f), 32f);
            sr.sortingOrder = 5;

            float xSpread = isSweetSpot ? 1.2f : 0.5f;
            float baseX = Random.Range(-xSpread, xSpread);
            float wandY = -3f;

            float size;
            float lifetime;
            Color color;

            if (isSweetSpot)
            {
                size = Random.Range(0.5f, 1.0f);
                lifetime = _sweetSpotLifetime * Random.Range(0.8f, 1.2f);
                color = _bubbleColors[Random.Range(0, _bubbleColors.Length)];
                _sweetSpotBubblesProduced++;
                _currentStreakBubbles++;
            }
            else
            {
                size = Random.Range(0.15f, 0.3f);
                lifetime = _gentleBubbleLifetime * Random.Range(0.7f, 1.3f);
                color = new Color(0.7f, 0.75f, 0.8f, 0.3f);
            }

            sr.color = color;
            obj.transform.position = new Vector3(baseX, wandY + 0.5f, 0f);
            obj.transform.localScale = Vector3.one * size;

            _activeBubbles.Add(new Bubble
            {
                Obj = obj,
                Renderer = sr,
                Lifetime = 0f,
                MaxLifetime = lifetime,
                WobblePhase = Random.Range(0f, Mathf.PI * 2f),
                BaseX = baseX,
                Size = size,
                IsSweetSpot = isSweetSpot
            });
        }

        private void UpdateBubbles()
        {
            for (int i = _activeBubbles.Count - 1; i >= 0; i--)
            {
                var b = _activeBubbles[i];
                b.Lifetime += Time.deltaTime;
                _activeBubbles[i] = b;

                float t = b.Lifetime / b.MaxLifetime;

                if (t >= 1f)
                {
                    // Pop: quick scale-up then destroy
                    Destroy(b.Obj);
                    _activeBubbles.RemoveAt(i);
                    continue;
                }

                // Rise upward
                float y = -3f + 0.5f + b.Lifetime * _riseSpeed;

                // Wobble side to side
                float wobbleX = Mathf.Sin(b.Lifetime * _wobbleFrequency + b.WobblePhase) * _wobbleAmplitude;
                float x = b.BaseX + wobbleX;

                b.Obj.transform.position = new Vector3(x, y, 0f);

                // Sweet-spot bubbles grow slightly as they rise
                float sizeScale = b.IsSweetSpot ? (1f + t * 0.3f) : (1f - t * 0.5f);
                b.Obj.transform.localScale = Vector3.one * b.Size * sizeScale;

                // Fade near end of life
                float alpha = t > 0.7f ? Mathf.Lerp(1f, 0f, (t - 0.7f) / 0.3f) : 1f;
                Color c = b.Renderer.color;
                c.a = (b.IsSweetSpot ? 0.7f : 0.3f) * alpha;
                b.Renderer.color = c;

                // Iridescent shimmer for sweet-spot bubbles
                if (b.IsSweetSpot)
                {
                    float hueShift = Mathf.Sin(b.Lifetime * 1.5f + b.WobblePhase) * 0.05f;
                    Color.RGBToHSV(b.Renderer.color, out float h, out float s, out float v);
                    Color shimmer = Color.HSVToRGB(Mathf.Repeat(h + hueShift, 1f), s, v);
                    shimmer.a = c.a;
                    b.Renderer.color = shimmer;
                }
            }
        }

        private void SpawnSplash(float power)
        {
            int particleCount = Random.Range(4, 8);
            for (int i = 0; i < particleCount; i++)
            {
                var obj = new GameObject("SplashDrop");
                obj.transform.SetParent(transform);
                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(_bubbleTex,
                    new Rect(0, 0, _bubbleTex.width, _bubbleTex.height),
                    new Vector2(0.5f, 0.5f), 64f);
                sr.color = new Color(0.7f, 0.8f, 0.95f, 0.8f);
                sr.sortingOrder = 6;

                float angle = Random.Range(-70f, 70f) * Mathf.Deg2Rad;
                float speed = Random.Range(2f, 5f) * power;
                obj.transform.position = new Vector3(
                    Random.Range(-0.5f, 0.5f), -2.5f, -0.2f);
                obj.transform.localScale = Vector3.one * Random.Range(0.1f, 0.25f);

                // Store velocity in a quick component
                var mover = obj.AddComponent<SplashDropMover>();
                mover.Velocity = new Vector2(Mathf.Sin(angle) * speed, Mathf.Abs(Mathf.Cos(angle)) * speed);
                mover.Lifetime = 0.8f;

                _splashParticles.Add(obj);
            }
        }

        private void UpdateSplashParticles()
        {
            for (int i = _splashParticles.Count - 1; i >= 0; i--)
            {
                if (_splashParticles[i] == null)
                {
                    _splashParticles.RemoveAt(i);
                }
            }
        }

        private void UpdateWandFeedback(float breathPower)
        {
            if (_wandRing == null) return;

            Color ringColor = _currentZone switch
            {
                BreathZone.SweetSpot => Color.Lerp(
                    new Color(0.3f, 1f, 0.5f, 0.8f),
                    new Color(0.5f, 1f, 0.7f, 0.9f),
                    Mathf.PingPong(Time.time * 2f, 1f)),
                BreathZone.TooGentle => new Color(0.6f, 0.6f, 0.7f, 0.5f),
                BreathZone.TooAggressive => new Color(1f, 0.3f, 0.2f, 0.8f),
                _ => new Color(0.5f, 0.5f, 0.55f, 0.4f)
            };

            _wandRing.startColor = ringColor;
            _wandRing.endColor = ringColor;

            // Shake the wand when too aggressive
            float shake = _currentZone == BreathZone.TooAggressive
                ? Random.Range(-0.08f, 0.08f) : 0f;
            _wandObj.transform.position = new Vector3(shake, -3f, -0.5f);
        }

        private void BuildBackground()
        {
            var cam = Camera.main;
            if (cam != null)
                cam.backgroundColor = new Color(0.65f, 0.85f, 0.70f);

            _backgroundObj = new GameObject("BubblesBackground");
            _backgroundObj.transform.SetParent(transform);
            var sr = _backgroundObj.AddComponent<SpriteRenderer>();
            var bgTex = GenerateGradientTexture(4, 64,
                new Color(0.55f, 0.80f, 0.60f),
                new Color(0.45f, 0.70f, 0.85f));
            sr.sprite = Sprite.Create(bgTex, new Rect(0, 0, bgTex.width, bgTex.height),
                new Vector2(0.5f, 0.5f), 8f);
            sr.sortingOrder = -20;
            _backgroundObj.transform.position = new Vector3(0f, 1f, 10f);
            _backgroundObj.transform.localScale = new Vector3(30f, 20f, 1f);
        }

        private void BuildWand()
        {
            _wandObj = new GameObject("BubbleWand");
            _wandObj.transform.SetParent(transform);
            _wandObj.transform.position = new Vector3(0f, -3f, -0.5f);

            // Wand ring
            var ringObj = new GameObject("WandRing");
            ringObj.transform.SetParent(_wandObj.transform);
            _wandRing = ringObj.AddComponent<LineRenderer>();
            _wandRing.useWorldSpace = false;
            _wandRing.loop = true;
            _wandRing.material = _spriteMat;
            _wandRing.startColor = new Color(0.5f, 0.5f, 0.55f, 0.6f);
            _wandRing.endColor = new Color(0.5f, 0.5f, 0.55f, 0.6f);
            _wandRing.widthMultiplier = 0.08f;
            _wandRing.sortingOrder = 3;
            _wandRing.numCornerVertices = 6;

            int ringSegs = 32;
            _wandRing.positionCount = ringSegs;
            float ringRadius = 0.7f;
            for (int i = 0; i < ringSegs; i++)
            {
                float angle = (float)i / ringSegs * Mathf.PI * 2f;
                _wandRing.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    Mathf.Sin(angle) * ringRadius + 0.8f, 0f));
            }

            // Film inside ring (subtle)
            var filmObj = new GameObject("WandFilm");
            filmObj.transform.SetParent(_wandObj.transform);
            var filmRend = filmObj.AddComponent<SpriteRenderer>();
            filmRend.sprite = Sprite.Create(_bubbleTex,
                new Rect(0, 0, _bubbleTex.width, _bubbleTex.height),
                new Vector2(0.5f, 0.5f), 32f);
            filmRend.color = new Color(0.7f, 0.8f, 0.95f, 0.15f);
            filmRend.sortingOrder = 2;
            filmObj.transform.localPosition = new Vector3(0f, 0.8f, 0.01f);
            filmObj.transform.localScale = new Vector3(1.3f, 1.3f, 1f);

            // Handle
            var handleObj = new GameObject("WandHandle");
            handleObj.transform.SetParent(_wandObj.transform);
            _wandHandle = handleObj.AddComponent<LineRenderer>();
            _wandHandle.useWorldSpace = false;
            _wandHandle.positionCount = 2;
            _wandHandle.material = _spriteMat;
            _wandHandle.startColor = new Color(0.55f, 0.40f, 0.25f);
            _wandHandle.endColor = new Color(0.50f, 0.35f, 0.20f);
            _wandHandle.startWidth = 0.12f;
            _wandHandle.endWidth = 0.10f;
            _wandHandle.sortingOrder = 1;
            _wandHandle.SetPosition(0, new Vector3(0f, 0.1f, 0f));
            _wandHandle.SetPosition(1, new Vector3(0f, -2f, 0f));
        }

        private static void EnsureMaterial()
        {
            if (_spriteMat != null) return;
            _spriteMat = new Material(Shader.Find("Sprites/Default"));
        }

        private static Texture2D GenerateBubbleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float radius = center - 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float edge = Mathf.Clamp01((radius - dist) * 1.5f);
                    // Hollow look: brighter at edge, transparent in center
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(dist - radius * 0.85f) / (radius * 0.2f));
                    float fill = Mathf.Clamp01(edge * 0.3f + ring * 0.7f);
                    // Highlight in upper-left
                    float hlDist = Vector2.Distance(new Vector2(x, y),
                        new Vector2(center * 0.7f, center * 1.3f));
                    float hl = Mathf.Clamp01(1f - hlDist / (radius * 0.4f)) * edge;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, fill + hl * 0.5f));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GenerateGradientTexture(int w, int h, Color bottom, Color top)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, (float)y / h);
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }

    // Lightweight mover for splash droplets — avoids a separate pooling system.
    public class SplashDropMover : MonoBehaviour
    {
        public Vector2 Velocity;
        public float Lifetime;
        private float _timer;

        private void Update()
        {
            _timer += Time.deltaTime;
            Velocity.y -= 12f * Time.deltaTime;
            transform.position += (Vector3)Velocity * Time.deltaTime;

            float alpha = Mathf.Clamp01(1f - _timer / Lifetime);
            var sr = GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = 0.8f * alpha;
                sr.color = c;
            }

            if (_timer >= Lifetime) Destroy(gameObject);
        }
    }
}
