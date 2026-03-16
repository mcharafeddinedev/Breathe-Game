using UnityEngine;

namespace Breathe.Gameplay
{
    // Procedural cartoon-ocean background using LineRenderers.
    // Three sine-wave layers (primary, depth, foam) scroll with parallax
    // for a stylized Wind Waker-inspired ocean surface.
    public class ScrollingOceanBackground : MonoBehaviour
    {
        [SerializeField]
        private Color _oceanColor = new Color(0.04f, 0.14f, 0.32f, 1f);

        [Header("Primary Waves")]
        [SerializeField] private int _lineCount = 70;
        [SerializeField] private float _lineSpacing = 1.8f;
        [SerializeField] private Color _lineColor = new Color(0.18f, 0.38f, 0.62f, 0.35f);
        [SerializeField] private float _lineWidth = 0.09f;

        [Header("Depth Waves (parallax)")]
        [SerializeField] private int _depthCount = 24;
        [SerializeField] private float _depthSpacing = 2.4f;
        [SerializeField, Range(0f, 1f), Tooltip("0 = static, 1 = same scroll rate as camera.")]
        private float _depthParallaxRate = 0.5f;
        [SerializeField] private Color _depthColor = new Color(0.1f, 0.28f, 0.52f, 0.45f);
        [SerializeField] private float _depthWidth = 0.22f;

        [Header("Foam Highlights")]
        [SerializeField] private int _foamCount = 12;
        [SerializeField] private float _foamSpacing = 3.5f;
        [SerializeField] private Color _foamColor = new Color(0.75f, 0.88f, 1f, 0.28f);
        [SerializeField] private float _foamWidth = 0.05f;
        [SerializeField, Tooltip("Ahead buffer multiplier for foam — prevents pop-in at high boat speed.")]
        private float _foamAheadMultiplier = 2.2f;

        [Header("Wave Shape")]
        [SerializeField] private int _segments = 120;
        [SerializeField, Tooltip("Extra world-units the lines extend past screen edges.")]
        private float _viewPadding = 6f;
        [SerializeField, Tooltip("Extra vertical units rendered ahead of camera. Must cover full course at max speed.")]
        private float _verticalAheadBuffer = 280f;
        [SerializeField, Tooltip("Extra vertical units rendered behind camera.")]
        private float _verticalBehindBuffer = 80f;
        [SerializeField] private float _amplitude = 0.38f;
        [SerializeField] private float _frequency = 0.70f;
        [SerializeField] private float _flowSpeed = 2.2f;

        [SerializeField, Range(0.3f, 1.5f), Tooltip("Primary wave animation speed. Baseline = 1.")]
        private float _primarySpeedMultiplier = 1f;
        [SerializeField, Range(0.2f, 1f), Tooltip("Depth layer speed — slower reads as deeper water.")]
        private float _depthSpeedMultiplier = 0.55f;
        [SerializeField, Range(0.8f, 1.8f), Tooltip("Foam layer speed — faster reads as surface detail.")]
        private float _foamSpeedMultiplier = 1.25f;

        [SerializeField, Tooltip("Amplitude of diagonal cross-wave so layers criss-cross and weave.")]
        private float _crossWaveAmplitude = 0.22f;
        [SerializeField] private float _crossWaveFrequency = 0.42f;
        [SerializeField, Tooltip("Phase lag for depth layer (radians) — reads as shadow beneath primary.")]
        private float _depthPhaseLag = 0.4f;
        [SerializeField, Tooltip("Y-offset so depth sits slightly below primary crests.")]
        private float _depthShadowOffset = -0.12f;

        private Camera _cam;
        private float _halfViewHeight;
        private WaveLine[] _primaryPool;
        private WaveLine[] _depthPool;
        private WaveLine[] _foamPool;
        private static Material _sharedMat;

        // Pre-allocated per-line data to avoid runtime allocations.
        private struct WaveLine
        {
            public LineRenderer Renderer;
            public Vector3[] Positions;
            public float Z;
            public float PhaseOffset;
            public float AmpScale;
            public float FreqScale;
            public float SpeedScale;      // Per-line flow speed variation
            public float SecondaryPhase;  // Offset for secondary harmonics (breaks sync)
            public float TertiaryPhase;   // Offset for tertiary harmonic
        }

        private void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;

            if (_cam != null)
            {
                _cam.backgroundColor = _oceanColor;
                _halfViewHeight = _cam.orthographicSize;
            }
            else
            {
                _halfViewHeight = 10f;
            }

            // Override buffers at runtime so waves cover full course at max speed (tailwind + breath).
            float courseLength = 120f;
            var markers = FindAnyObjectByType<CourseMarkers>();
            if (markers != null)
                courseLength = markers.CourseLength;
            float minAhead = courseLength * 2.5f;
            if (_verticalAheadBuffer < minAhead)
                _verticalAheadBuffer = minAhead;
            float minBehind = courseLength * 0.5f;
            if (_verticalBehindBuffer < minBehind)
                _verticalBehindBuffer = minBehind;

            // Band height must cover view range or lines won't fill the buffers.
            float primaryAhead = _verticalAheadBuffer * 1.5f;
            float minBandHeight = primaryAhead + _verticalBehindBuffer + _halfViewHeight * 2f + 80f;
            int minLines = Mathf.CeilToInt(minBandHeight / _lineSpacing);
            if (_lineCount < minLines)
                _lineCount = minLines;
            int minDepth = Mathf.CeilToInt(minBandHeight / _depthSpacing);
            if (_depthCount < minDepth)
                _depthCount = minDepth;

            // Foam needs extended band for its larger ahead buffer
            float foamAhead = _verticalAheadBuffer * _foamAheadMultiplier;
            float minFoamBandHeight = foamAhead + _verticalBehindBuffer + _halfViewHeight * 2f + 80f;
            int minFoamLines = Mathf.CeilToInt(minFoamBandHeight / _foamSpacing);
            if (_foamCount < minFoamLines)
                _foamCount = minFoamLines;

            EnsureMaterial();

            // Order: primary (back) -11, depth (middle) -10, foam (front) -9
            _primaryPool = BuildPool("Waves_Primary", _lineCount,
                _lineColor, _lineWidth, -11);
            _depthPool = BuildPool("Waves_Depth", _depthCount,
                _depthColor, _depthWidth, -10);
            _foamPool = BuildPool("Waves_Foam", _foamCount,
                _foamColor, _foamWidth, -9);
        }

        private void LateUpdate()
        {
            float camX = transform.position.x;
            float camY = transform.position.y;
            float t = Time.time;

            float halfW = (_cam != null
                ? _cam.orthographicSize * _cam.aspect
                : 18f) + _viewPadding;

            float primaryPhase = t * _flowSpeed * _primarySpeedMultiplier;
            float depthPhase = t * _flowSpeed * _depthSpeedMultiplier - _depthPhaseLag;
            float foamPhase = t * _flowSpeed * _foamSpeedMultiplier;

            AnimateLayer(_primaryPool, _lineSpacing,
                camX, camY, t, halfW, _primarySpeedMultiplier, primaryPhase, useSharedPhase: false, aheadMultiplier: 1.5f);

            AnimateLayer(_depthPool, _depthSpacing,
                camX, camY * _depthParallaxRate, t, halfW, _depthSpeedMultiplier, depthPhase,
                useSharedPhase: true, shadowOffset: _depthShadowOffset);

            AnimateLayer(_foamPool, _foamSpacing,
                camX, camY * 0.85f, t, halfW, _foamSpeedMultiplier, foamPhase, useSharedPhase: true, aheadMultiplier: _foamAheadMultiplier);
        }

        // Plots each line along a composite 3-harmonic sine curve with cross-wave.
        // Cross-wave (sin of Y) makes layers criss-cross for depth; depth uses shared phase + lag.
        private void AnimateLayer(WaveLine[] pool, float spacing,
            float camX, float effectiveY, float time,
            float halfWidth, float intensity, float sharedPhase,
            bool useSharedPhase = false, float aheadMultiplier = 1f, float shadowOffset = 0f)
        {
            float leftX = camX - halfWidth;
            float segW = (halfWidth * 2f) / _segments;

            float bandHeight = pool.Length * spacing;
            float verticalBuffer = spacing * 3f + _viewPadding;
            float aheadBuffer = _verticalAheadBuffer * aheadMultiplier;
            float viewTop = effectiveY + _halfViewHeight + verticalBuffer + aheadBuffer;
            float viewBottom = effectiveY - _halfViewHeight - verticalBuffer - _verticalBehindBuffer;

            float crossPhase = time * _flowSpeed * 0.6f;

            for (int i = 0; i < pool.Length; i++)
            {
                ref WaveLine wave = ref pool[i];

                float lineOffsetInBand = i * spacing;
                float bandOffset = effectiveY - Mod(effectiveY - lineOffsetInBand, bandHeight);
                float relativeY = bandOffset + bandHeight * 0.5f;

                while (relativeY < viewBottom)
                    relativeY += bandHeight;
                while (relativeY > viewTop)
                    relativeY -= bandHeight;

                float speed = _flowSpeed * intensity * wave.SpeedScale;
                float phase = useSharedPhase ? sharedPhase : (wave.PhaseOffset + time * speed);
                float phase2 = useSharedPhase ? sharedPhase * 0.73f : (wave.SecondaryPhase + time * speed * 0.73f);
                float phase3 = useSharedPhase ? sharedPhase * 1.31f : (wave.TertiaryPhase + time * speed * 1.31f);

                float freq = _frequency * wave.FreqScale;
                float amp = _amplitude * wave.AmpScale * intensity;

                for (int s = 0; s <= _segments; s++)
                {
                    float x = leftX + s * segW;

                    // Primary 3-harmonic wave
                    float waveY = relativeY
                        + Mathf.Sin(x * freq + phase) * amp
                        + Mathf.Sin(x * freq * 2.17f + phase2) * amp * 0.28f
                        + Mathf.Sin(x * freq * 0.51f + phase3) * amp * 0.45f;

                    // Cross-wave: diagonal component so layers criss-cross
                    float crossY = Mathf.Sin(relativeY * _crossWaveFrequency + x * _crossWaveFrequency * 0.7f + crossPhase) * _crossWaveAmplitude;

                    float y = waveY + crossY + shadowOffset;

                    wave.Positions[s] = new Vector3(x, y, wave.Z);
                }

                wave.Renderer.SetPositions(wave.Positions);
            }
        }

        // True modulo — always returns a positive result (C# % can be negative).
        private static float Mod(float x, float m)
        {
            return ((x % m) + m) % m;
        }

        private WaveLine[] BuildPool(string groupName, int count,
            Color color, float width, int sortingOrder)
        {
            var parent = new GameObject(groupName);
            var pool = new WaveLine[count];

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Wave_{i}");
                go.transform.SetParent(parent.transform);
                go.transform.position = new Vector3(0f, 0f, 5f);

                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = _segments + 1;
                lr.widthMultiplier = width;
                lr.material = _sharedMat;
                lr.startColor = color;
                lr.endColor = color;
                lr.sortingOrder = sortingOrder;
                lr.numCornerVertices = 8;
                lr.numCapVertices = 6;
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lr.receiveShadows = false;

                pool[i] = new WaveLine
                {
                    Renderer = lr,
                    Positions = new Vector3[_segments + 1],
                    Z = 5f,
                    PhaseOffset = Random.Range(0f, Mathf.PI * 2f),
                    AmpScale = Random.Range(0.7f, 1.3f),
                    FreqScale = Random.Range(0.85f, 1.15f),
                    SpeedScale = Random.Range(0.7f, 1.4f),
                    SecondaryPhase = Random.Range(0f, Mathf.PI * 2f),
                    TertiaryPhase = Random.Range(0f, Mathf.PI * 2f)
                };
            }

            return pool;
        }

        private static void EnsureMaterial()
        {
            if (_sharedMat != null) return;
            _sharedMat = new Material(Shader.Find("Sprites/Default"));
        }
    }
}
