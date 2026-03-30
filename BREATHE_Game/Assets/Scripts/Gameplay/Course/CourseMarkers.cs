using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Spawns buoy lanes, dashed center line, distance rings, and a checkered finish line.
    // Buoys bob on the water with slight random offsets. After the race, BeginFadeOut
    // smoothly hides everything so only open waves remain.
    public class CourseMarkers : MonoBehaviour
    {
        [Header("Course Extent")]
        [SerializeField] private float _courseLength = 110f;

        [Header("Buoy Settings")]
        [SerializeField] private float _buoyInterval = 5f;
        [SerializeField] private float _laneHalfWidth = 12f;
        public float LaneHalfWidth => _laneHalfWidth;
        public float FinishLineHalfWidth => _laneHalfWidth + 1f;
        [SerializeField] private float _buoySize = 0.5f;
        [SerializeField] private Color _buoyColorA = new Color(0.88f, 0.38f, 0.12f, 0.85f);
        [SerializeField] private Color _buoyColorB = new Color(0.95f, 0.65f, 0.18f, 0.85f);

        [Header("Buoy Sprites (leave empty for placeholder)")]
        [SerializeField] private Sprite _buoyBodySprite;
        [SerializeField] private Sprite _buoyCapSprite;

        [Header("Buoy Animation")]
        [SerializeField] private float _bobAmplitude = 0.18f;
        [SerializeField] private float _bobFrequency = 1.8f;
        [SerializeField] private float _positionJitter = 0.6f;

        [Header("Course Layouts")]
        [SerializeField, Tooltip("Hand-designed course shapes. Random pick each run. Falls back to built-in presets.")]
        private CourseLayout[] _courseLayouts;

        [Header("Course Curvature (fallback)")]
        [SerializeField] private float _courseOffsetX = 0f;
        [SerializeField] private float _curveAmplitude = 5f;
        [SerializeField] private float _curveFrequency = 2f;

        [Header("Lane Dashes")]
        [SerializeField] private bool _showCenterDashes = false;
        [SerializeField] private Color _dashColor = new Color(1f, 1f, 1f, 0.12f);

        [Header("Finish Line")]
        [SerializeField] private Color _finishLineColor = new Color(1f, 1f, 1f, 0.6f);

        [Header("Distance Rings")]
        [SerializeField] private bool _showDistanceRings = true;
        [SerializeField] private float _ringInterval = 10f;
        [SerializeField] private Color _ringColor = new Color(1f, 1f, 1f, 0.06f);

        [Header("Finish Line Sprites (leave empty for placeholder)")]
        [SerializeField] private Sprite _finishLineSprite;

        [Header("Post-Race Fade")]
        [SerializeField] private float _fadeOutDuration = 2.5f;

        private static Sprite _placeholderSprite;
        private bool UseCustomBuoySprites => _buoyBodySprite != null;

        private struct BuoyRef
        {
            public Transform root;
            public Vector3 basePos;
            public float phase;
        }

        private readonly List<BuoyRef> _buoys = new List<BuoyRef>();
        private readonly List<SpriteRenderer> _allRenderers = new List<SpriteRenderer>();
        private readonly List<float> _originalAlphas = new List<float>();
        private Transform _markersRoot;

        private bool _fadingOut;
        private float _fadeProgress;
        private CourseLayout _activeLayout;

        private void Awake()
        {
            // Re-seed so each run picks a different layout (Unity keeps Random state across Play sessions)
            Random.InitState(System.Environment.TickCount);
            SelectRandomLayout();
        }

        private void Start()
        {
            EnsurePlaceholderSprite();
            SpawnAllMarkers();
        }

        // Tear down and regenerate with a new random layout
        public void RegenerateWithRandomLayout()
        {
            if (_markersRoot != null) { Destroy(_markersRoot.gameObject); _markersRoot = null; }
            _buoys.Clear();
            _allRenderers.Clear();
            _originalAlphas.Clear();
            _fadingOut = false;
            _fadeProgress = 0f;

            Random.InitState(System.Environment.TickCount);
            SelectRandomLayout();
            EnsurePlaceholderSprite();
            SpawnAllMarkers();
        }

        private void SelectRandomLayout()
        {
            CourseLayout[] pool = _courseLayouts != null && _courseLayouts.Length > 0
                ? _courseLayouts : CourseLayout.CreateBuiltInPresets();
            int index = Random.Range(0, pool.Length);
            _activeLayout = pool[index];
            Debug.Log($"[CourseMarkers] Layout: \"{_activeLayout.LayoutName}\" ({index}/{pool.Length})");
        }

        private void Update()
        {
            float t = Time.time;

            if (!_fadingOut)
            {
                for (int i = 0; i < _buoys.Count; i++)
                {
                    var b = _buoys[i];
                    float bobY = Mathf.Sin(t * _bobFrequency + b.phase) * _bobAmplitude;
                    float bobX = Mathf.Sin(t * _bobFrequency * 0.7f + b.phase + 1.3f) * _bobAmplitude * 0.3f;
                    b.root.position = new Vector3(b.basePos.x + bobX, b.basePos.y + bobY, b.basePos.z);
                }
                return;
            }

            // Fade out all markers after race
            _fadeProgress += Time.deltaTime / _fadeOutDuration;
            float alpha = 1f - Mathf.Clamp01(_fadeProgress);

            for (int i = 0; i < _allRenderers.Count; i++)
            {
                if (_allRenderers[i] == null) continue;
                Color c = _allRenderers[i].color;
                c.a = _originalAlphas[i] * alpha;
                _allRenderers[i].color = c;
            }
            for (int i = 0; i < _buoys.Count; i++)
            {
                var b = _buoys[i];
                float bobY = Mathf.Sin(t * _bobFrequency + b.phase) * _bobAmplitude * alpha;
                float bobX = Mathf.Sin(t * _bobFrequency * 0.7f + b.phase + 1.3f) * _bobAmplitude * 0.3f * alpha;
                b.root.position = new Vector3(b.basePos.x + bobX, b.basePos.y + bobY, b.basePos.z);
            }

            if (_fadeProgress >= 1f && _markersRoot != null)
                _markersRoot.gameObject.SetActive(false);
        }

        public void BeginFadeOut()
        {
            if (_fadingOut) return;
            _fadingOut = true;
            _fadeProgress = 0f;
        }

        public float CourseLength => _courseLength;
        public string ActiveLayoutName => _activeLayout != null ? _activeLayout.LayoutName : "NONE";

        // X-center of the course at a given Y along the track.
        // CourseManager uses this to generate waypoints on the same spline as the buoys.
        public float CurveX(float y)
        {
            if (_activeLayout != null)
                return _courseOffsetX + _activeLayout.EvaluateX(y, _courseLength);
            return _courseOffsetX + Mathf.Sin(y / _courseLength * _curveFrequency * Mathf.PI * 2f) * _curveAmplitude;
        }

        private void SpawnAllMarkers()
        {
            int buoyCount = Mathf.CeilToInt(_courseLength / _buoyInterval);
            var root = new GameObject("--- COURSE_MARKERS ---");
            root.transform.SetParent(transform);
            _markersRoot = root.transform;

            for (int i = 0; i <= buoyCount; i++)
            {
                float y = i * _buoyInterval;
                float cx = CurveX(y);

                float jitterSeed = y * 137.5f;
                float jLX = (Mathf.PerlinNoise(jitterSeed, 0f) - 0.5f) * 2f * _positionJitter;
                float jLY = (Mathf.PerlinNoise(jitterSeed, 10f) - 0.5f) * 2f * _positionJitter;
                float jRX = (Mathf.PerlinNoise(jitterSeed, 20f) - 0.5f) * 2f * _positionJitter;
                float jRY = (Mathf.PerlinNoise(jitterSeed, 30f) - 0.5f) * 2f * _positionJitter;

                Color gateColor = (i % 2 == 0) ? _buoyColorA : _buoyColorB;
                MakeBuoy(root.transform, new Vector3(cx - _laneHalfWidth + jLX, y + jLY, 0f), gateColor, jitterSeed);
                MakeBuoy(root.transform, new Vector3(cx + _laneHalfWidth + jRX, y + jRY, 0f), gateColor, jitterSeed + 50f);

                if (_showCenterDashes)
                    TrackRenderer(MakeSprite(root.transform, new Vector3(cx, y, 0f), new Vector3(0.12f, 0.7f, 1f), _dashColor, -6));
            }

            int ringCount = Mathf.CeilToInt(_courseLength / _ringInterval);
            for (int r = 1; r < ringCount; r++)
            {
                float y = r * _ringInterval;
                float cx = CurveX(y);
                if (_showDistanceRings)
                    TrackRenderer(MakeSprite(root.transform, new Vector3(cx, y, 0f), new Vector3(_laneHalfWidth * 2f, 0.04f, 1f), _ringColor, -7));
            }

            BuildFinishLine(root.transform);
        }

        private void MakeBuoy(Transform parent, Vector3 pos, Color bodyColor, float seed)
        {
            var buoyRoot = new GameObject("Buoy");
            buoyRoot.transform.SetParent(parent);
            buoyRoot.transform.position = pos;

            if (UseCustomBuoySprites)
            {
                var body = new GameObject("Body");
                body.transform.SetParent(buoyRoot.transform);
                body.transform.localPosition = Vector3.zero;
                body.transform.localScale = new Vector3(_buoySize, _buoySize, 1f);
                var bodySr = body.AddComponent<SpriteRenderer>();
                bodySr.sprite = _buoyBodySprite;
                bodySr.color = bodyColor;
                bodySr.sortingOrder = -5;
                TrackRenderer(bodySr);

                if (_buoyCapSprite != null)
                {
                    var cap = new GameObject("Cap");
                    cap.transform.SetParent(buoyRoot.transform);
                    cap.transform.localPosition = new Vector3(0f, _buoySize * 0.5f, 0f);
                    cap.transform.localScale = new Vector3(_buoySize * 0.8f, _buoySize * 0.8f, 1f);
                    var capSr = cap.AddComponent<SpriteRenderer>();
                    capSr.sprite = _buoyCapSprite;
                    capSr.color = Color.white;
                    capSr.sortingOrder = -4;
                    TrackRenderer(capSr);
                }
            }
            else
            {
                var body = new GameObject("Body");
                body.transform.SetParent(buoyRoot.transform);
                body.transform.localPosition = Vector3.zero;
                body.transform.localScale = new Vector3(_buoySize, _buoySize * 1.3f, 1f);
                var bodySr = body.AddComponent<SpriteRenderer>();
                bodySr.sprite = _placeholderSprite;
                bodySr.color = bodyColor;
                bodySr.sortingOrder = -5;
                TrackRenderer(bodySr);

                float capH = _buoySize * 0.35f;
                var cap = new GameObject("Cap");
                cap.transform.SetParent(buoyRoot.transform);
                cap.transform.localPosition = new Vector3(0f, _buoySize * 0.65f + capH * 0.3f, 0f);
                cap.transform.localScale = new Vector3(_buoySize * 0.85f, capH, 1f);
                var capSr = cap.AddComponent<SpriteRenderer>();
                capSr.sprite = _placeholderSprite;
                capSr.color = new Color(1f, 1f, 1f, 0.9f);
                capSr.sortingOrder = -4;
                TrackRenderer(capSr);
            }

            _buoys.Add(new BuoyRef { root = buoyRoot.transform, basePos = pos, phase = seed * 0.1f });
        }

        private void BuildFinishLine(Transform parent)
        {
            float finishY = _courseLength;
            float finishCx = CurveX(finishY);
            float finishWidth = _laneHalfWidth * 2f + 2f;

            if (_finishLineSprite != null)
            {
                TrackRenderer(MakeSprite(parent, new Vector3(finishCx, finishY, 0f),
                    new Vector3(finishWidth, finishWidth * 0.1f, 1f), _finishLineColor, -4, _finishLineSprite));
            }
            else
            {
                TrackRenderer(MakeSprite(parent, new Vector3(finishCx, finishY, 0f),
                    new Vector3(finishWidth, 0.4f, 1f), _finishLineColor, -4));

                int checks = 14;
                float segW = finishWidth / checks;
                for (int c = 0; c < checks; c += 2)
                {
                    float x = finishCx - finishWidth * 0.5f + segW * c + segW * 0.5f;
                    TrackRenderer(MakeSprite(parent, new Vector3(x, finishY, 0f),
                        new Vector3(segW, 0.4f, 1f), new Color(0f, 0f, 0f, 0.5f), -3));
                }
            }
        }

        private SpriteRenderer MakeSprite(Transform parent, Vector3 position,
            Vector3 scale, Color color, int sortingOrder, Sprite customSprite = null)
        {
            var go = new GameObject("Marker");
            go.transform.SetParent(parent);
            go.transform.position = position;
            go.transform.localScale = scale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = customSprite != null ? customSprite : _placeholderSprite;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        private void TrackRenderer(SpriteRenderer sr)
        {
            _allRenderers.Add(sr);
            _originalAlphas.Add(sr.color.a);
        }

        private static void EnsurePlaceholderSprite()
        {
            if (_placeholderSprite != null) return;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            _placeholderSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
