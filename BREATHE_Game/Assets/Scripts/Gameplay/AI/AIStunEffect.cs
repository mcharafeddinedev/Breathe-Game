using UnityEngine;

namespace Breathe.Gameplay
{
    // Cartoon dizzy-stars effect for AI boats. Procedurally generates star sprites
    // that orbit the boat when stunned — no external assets needed.
    [RequireComponent(typeof(AICompanionController))]
    public class AIStunEffect : MonoBehaviour
    {
        [Header("Stars")]
        [SerializeField] private int _starCount = 7;
        [SerializeField] private float _orbitRadius = 1.2f;
        [SerializeField, Tooltip("Revolutions per second.")] private float _orbitSpeed = 0.5f;
        [SerializeField] private float _wobbleAmplitude = 0.3f;
        [SerializeField] private float _wobbleFrequency = 4f;

        [Header("Visual")]
        [SerializeField] private float _starSize = 0.35f;
        [SerializeField] private Color _starColor = new Color(1f, 0.9f, 0.3f, 0.85f);
        [SerializeField] private int _sortingOrder = 6;

        private AICompanionController _ai;
        private Transform[] _starTransforms;
        private SpriteRenderer[] _starRenderers;
        private float[] _starPhases;
        private float[] _starRadiusOffsets;
        private static Sprite _starSprite;

        private void Awake() => _ai = GetComponent<AICompanionController>();

        private void Start()
        {
            EnsureStarSprite();
            BuildStars();
        }

        private void Update()
        {
            if (_ai == null || !_ai.IsStunned) { HideStars(); return; }
            ShowStars();
            UpdateOrbit();
        }

        // Procedurally generates a 5-pointed star texture (shared across all instances)
        private static void EnsureStarSprite()
        {
            if (_starSprite != null) return;

            int size = 24;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float cx = size / 2f, cy = size / 2f, radius = size / 2f - 1f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x - cx) / radius;
                float dy = (y - cy) / radius;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float starR = Mathf.Clamp01(0.5f + 0.5f * Mathf.Cos(angle * 5f));
                float alpha = Mathf.Clamp01(1f - (dist - starR) * 2.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            _starSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private void BuildStars()
        {
            _starTransforms = new Transform[_starCount];
            _starRenderers = new SpriteRenderer[_starCount];
            _starPhases = new float[_starCount];
            _starRadiusOffsets = new float[_starCount];

            for (int i = 0; i < _starCount; i++)
            {
                var go = new GameObject($"StunStar_{i}");
                go.transform.SetParent(transform);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one * _starSize;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _starSprite;
                sr.color = _starColor;
                sr.sortingOrder = _sortingOrder;

                _starTransforms[i] = go.transform;
                _starRenderers[i] = sr;
                _starPhases[i] = (float)i / _starCount * Mathf.PI * 2f;
                _starRadiusOffsets[i] = 0f;
                go.SetActive(false);
            }
        }

        private void ShowStars()
        {
            for (int i = 0; i < _starCount; i++)
                if (_starTransforms[i] != null && !_starTransforms[i].gameObject.activeSelf)
                    _starTransforms[i].gameObject.SetActive(true);
        }

        private void HideStars()
        {
            for (int i = 0; i < _starCount; i++)
                if (_starTransforms != null && _starTransforms[i] != null && _starTransforms[i].gameObject.activeSelf)
                    _starTransforms[i].gameObject.SetActive(false);
        }

        private void UpdateOrbit()
        {
            float t = Time.time;
            for (int i = 0; i < _starCount; i++)
            {
                if (_starTransforms[i] == null) continue;

                float angle = _starPhases[i] + t * _orbitSpeed * Mathf.PI * 2f;
                float wobble = 1f + Mathf.Sin(t * _wobbleFrequency * Mathf.PI * 2f) * _wobbleAmplitude;
                float r = (_orbitRadius + _starRadiusOffsets[i]) * wobble;

                _starTransforms[i].localPosition = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
                _starTransforms[i].localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                _starTransforms[i].localScale = Vector3.one * _starSize * (0.9f + 0.2f * Mathf.Sin(t * 6f + i));
            }
        }
    }
}
