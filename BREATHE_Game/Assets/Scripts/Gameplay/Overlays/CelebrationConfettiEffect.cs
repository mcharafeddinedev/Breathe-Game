using UnityEngine;

namespace Breathe.Gameplay
{
    // Reusable confetti burst effect using a sprite-pool approach.
    // Call Play() with a world position to trigger a burst on both sides.
    // Works for finish-line celebration, minigame completions, or any win moment.
    public class CelebrationConfettiEffect : MonoBehaviour
    {
        [SerializeField, Tooltip("Leave empty for procedural placeholder (soft circle).")]
        private Sprite _particleSprite;

        [Header("Burst (sides of position)")]
        [SerializeField, Tooltip("Horizontal offset from center for each side burst.")]
        private float _sideOffset = 4f;
        [SerializeField] private float _spread = 2f;
        [SerializeField] private int _particlesPerSide = 10;

        [Header("Motion")]
        [SerializeField] private float _outwardSpeed = 3f;
        [SerializeField] private float _upwardSpeed = 2f;
        [SerializeField, Tooltip("Random speed variance (0–1 multiplier).")]
        private float _speedVariance = 0.4f;
        [SerializeField] private float _gravity = 2f;

        [Header("Lifetime & Visual")]
        [SerializeField] private float _lifetime = 1.2f;
        [SerializeField, Tooltip("Min/max particle scale.")]
        private Vector2 _scaleRange = new Vector2(0.25f, 0.6f);
        [SerializeField, Tooltip("Sorting order for particles.")]
        private int _sortingOrder = 10;

        [Header("Confetti Colors")]
        [SerializeField] private Color[] _colors = new[]
        {
            new Color(1f, 0.85f, 0.2f, 0.9f),   // gold
            new Color(1f, 1f, 1f, 0.9f),        // white
            new Color(0.4f, 0.75f, 1f, 0.9f),  // light blue
            new Color(1f, 0.5f, 0.4f, 0.9f),    // coral
        };

        private struct Particle
        {
            public GameObject Go;
            public SpriteRenderer Sr;
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float MaxAge;
            public float BaseScale;
            public Color BaseColor;
            public bool Active;
        }

        private Particle[] _pool;
        private int _poolSize;
        private Transform _poolRoot;
        private static Sprite _placeholderSprite;

        // Triggers a confetti burst at the given world position.
        // If singlePoint is true, particles spawn at the position only (no left/right offset).
        public void Play(Vector3 worldPosition, bool singlePoint = false)
        {
            if (_pool == null) return;

            int sides = singlePoint ? 1 : 2;
            int perSide = singlePoint ? _particlesPerSide * 2 : _particlesPerSide;
            int spawned = 0;

            for (int side = 0; side < sides && spawned < _poolSize; side++)
            {
                float sideDir = side == 0 ? -1f : 1f;
                Vector3 sideCenter = singlePoint ? worldPosition : worldPosition + Vector3.right * (sideDir * _sideOffset);

                for (int i = 0; i < perSide && spawned < _poolSize; i++)
                {
                    int idx = FindInactive();
                    if (idx < 0) break;

                    ref Particle p = ref _pool[idx];

                    float rx = (Random.value - 0.5f) * 2f * _spread;
                    float ry = (Random.value - 0.5f) * 2f * _spread;
                    p.Position = sideCenter + new Vector3(rx, ry, 0f);

                    float outMult = _outwardSpeed * (0.8f + Random.value * _speedVariance);
                    float upMult = _upwardSpeed * (0.8f + Random.value * _speedVariance);
                    float velSide = singlePoint ? (Random.value > 0.5f ? 1f : -1f) : sideDir;
                    p.Velocity = new Vector3(velSide * outMult, upMult, 0f);

                    p.Age = 0f;
                    p.MaxAge = _lifetime * (0.85f + Random.value * 0.3f);
                    p.BaseScale = Mathf.Lerp(_scaleRange.x, _scaleRange.y, Random.value);
                    p.BaseColor = _colors[Random.Range(0, _colors.Length)];
                    p.Active = true;

                    p.Go.transform.position = p.Position;
                    p.Go.transform.localScale = Vector3.one * p.BaseScale;
                    p.Sr.color = new Color(p.BaseColor.r, p.BaseColor.g, p.BaseColor.b, 0f);
                    p.Go.SetActive(true);
                    spawned++;
                }
            }
        }

        private void Start()
        {
            EnsurePlaceholderSprite();
            _poolSize = _particlesPerSide * 2 * 2; // both sides, plus spare
            BuildPool();
        }

        private void Update()
        {
            if (_pool == null) return;

            float dt = Time.deltaTime;
            for (int i = 0; i < _pool.Length; i++)
            {
                ref Particle p = ref _pool[i];
                if (!p.Active) continue;

                p.Age += dt;
                p.Velocity.y -= _gravity * dt;
                p.Position += p.Velocity * dt;

                p.Go.transform.position = p.Position;

                float t = p.Age / p.MaxAge;
                float alpha;
                if (t < 0.15f)
                    alpha = (t / 0.15f) * 0.9f;
                else if (t > 0.55f)
                    alpha = Mathf.Lerp(0.9f, 0f, (t - 0.55f) / 0.45f);
                else
                    alpha = 0.9f;

                p.Sr.color = new Color(p.BaseColor.r, p.BaseColor.g, p.BaseColor.b, alpha);

                float scaleMult = 1f + t * 0.3f;
                p.Go.transform.localScale = Vector3.one * (p.BaseScale * scaleMult);

                if (t >= 1f)
                {
                    p.Active = false;
                    p.Go.SetActive(false);
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
            Sprite sprite = _particleSprite != null ? _particleSprite : _placeholderSprite;

            _poolRoot = new GameObject("ConfettiPool").transform;
            _poolRoot.SetParent(transform, false);
            _poolRoot.localPosition = Vector3.zero;

            _pool = new Particle[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var go = new GameObject($"Confetti_{i}");
                go.transform.SetParent(_poolRoot, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.color = new Color(1f, 1f, 1f, 0f);
                sr.sortingOrder = _sortingOrder;
                sr.material = new Material(Shader.Find("Sprites/Default"));

                go.SetActive(false);

                _pool[i] = new Particle { Go = go, Sr = sr, Active = false };
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
