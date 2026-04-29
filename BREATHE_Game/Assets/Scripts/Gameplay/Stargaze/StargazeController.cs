using System.Collections.Generic;
using UnityEngine;

namespace Breathe.Gameplay
{
    // Renders the night sky, manages cloud groups, handles star reveal,
    // constellation line fade-in, and the educational caption sequence.
    public class StargazeController : MonoBehaviour
    {
        [Header("World")]
        [SerializeField] private float _worldSize = 12f;

        [Header("Clouds")]
        [SerializeField] private int _cloudGroupCount = 12;

        private const float BaseCloudMoveSpeed = 1.7f;
        private const float BaseCloudReturnSpeed = 0.8f;
        private const float BaseBreathThreshold = 0.10f;

        private float _cloudMoveSpeed;
        private float _cloudReturnSpeed;
        private float _breathThreshold;

        [Header("Reveal")]
        private const float _revealThreshold = 0.95f;
        private const float _autoRevealDuration = 0.6f;

        [Header("Animation")]
        private const float _linesFadeInDuration = 0.5f;
        private const float _figureFadeInDuration = 0.4f;
        [SerializeField] private float _captionSecondsPerWord = 0.28f;
        [SerializeField] private float _captionMinDuration = 10f;
        [SerializeField] private float _captionMaxDuration = 18f;
        private float _captionDisplayDuration;
        private const float ZoomMaxMultiplier = 3f;
        private const float HeaderBarPixels = 80f;
        private const float CaptionBarPixels = 270f;
        private const float StarPadding = 1.0f;

        public enum Phase { Converging, Clearing, AutoClearing, LinesAppearing, FigureAppearing, CaptionShowing, RoundDone }

        [Header("Converge")]
        [SerializeField] private float _convergeDuration = 3f;

        private Phase _currentPhase;
        private float _phaseTimer;
        private float _revealPercent;

        // Cloud data
        private struct CloudGroup
        {
            public GameObject[] Objs;
            public SpriteRenderer[] Renderers;
            public Vector2 HomePosition;
            public Vector2 EdgeTarget;
            public float Progress; // 0 = home, 1 = edge
        }

        private CloudGroup[] _clouds;
        private ConstellationDatabase.ConstellationData _currentConstellation;

        // Star objects
        private GameObject[] _starObjs;
        private SpriteRenderer[] _starRenderers;
        private float[] _starTwinklePhase;
        private float[] _starBaseScales;
        private Color[] _starColors;

        // Constellation lines
        private LineRenderer[] _constellationLines;
        private float _linesAlpha;

        // Figure silhouette (simple overlay)
        private SpriteRenderer _figureRenderer;
        private float _figureAlpha;

        // Caption
        private string _captionName;
        private string _captionScience;
        private string _captionCharacter;

        // Scene parents (for cleanup)
        private Transform _starsParent;
        private Transform _conLinesParent;
        private Transform _figureParent;

        // Camera zoom
        private float _baseCamSize;
        private float _zoomCamTarget;
        private Vector3 _baseCamPos;
        private Vector3 _zoomCamPos;

        private static Material _spriteMat;
        private Texture2D _starTex;
        private Texture2D _cloudTex;
        private Texture2D _glowTex;

        // Background ambient starfield (persists across rounds)
        private GameObject[] _bgStarObjs;
        private SpriteRenderer[] _bgStarRenderers;
        private float[] _bgStarPhases;
        private float[] _bgStarBaseScales;
        private float[] _bgStarBaseAlphas;

        public Phase CurrentPhase => _currentPhase;
        public float RevealPercent => _revealPercent;
        public string CaptionName => _captionName;
        public string CaptionScience => _captionScience;
        public string CaptionCharacter => _captionCharacter;
        public bool IsRoundDone => _currentPhase == Phase.RoundDone;
        public float CaptionPhaseTime => _currentPhase == Phase.CaptionShowing ? _phaseTimer : 0f;

        public float ClearingBreathGate => _breathThreshold;

        public void AdvanceToRoundDone()
        {
            if (_currentPhase == Phase.CaptionShowing)
                _currentPhase = Phase.RoundDone;
        }
        public float FigureAlpha => _figureAlpha;

        public int ConstellationStarCount => _starObjs != null ? _starObjs.Length : 0;

        public Vector3 GetStarWorldPosition(int index)
        {
            if (_starObjs == null || index < 0 || index >= _starObjs.Length) return Vector3.zero;
            return _starObjs[index].transform.position;
        }

        public string GetStarName(int index)
        {
            var names = _currentConstellation.StarNames;
            if (names == null || index < 0 || index >= names.Length) return null;
            return names[index];
        }

        /// <summary>
        /// Scale cloud difficulty. multiplier=1 is baseline, higher = harder.
        /// Clouds push slower, return faster, and need stronger breath.
        /// </summary>
        public void SetDifficulty(float multiplier)
        {
            _cloudMoveSpeed = BaseCloudMoveSpeed / multiplier;
            _cloudReturnSpeed = BaseCloudReturnSpeed * multiplier;
            _breathThreshold = Mathf.Min(BaseBreathThreshold * multiplier, 0.5f);
        }

        public void Initialize()
        {
            EnsureMaterial();
            _starTex = GenerateStarTexture(64);
            _cloudTex = GenerateCloudTexture(64);
            _glowTex = GenerateGlowTexture(64);
            SetDifficulty(1f);

            var cam = Camera.main;
            if (cam != null)
            {
                cam.backgroundColor = new Color(0.06f, 0.06f, 0.14f);
                _baseCamSize = cam.orthographicSize;
                _baseCamPos = cam.transform.position;
            }

            BuildBackgroundStars();
        }

        private void Update()
        {
            AnimateBackgroundStars();
        }

        public void SetupConstellation(ConstellationDatabase.ConstellationData constellation)
        {
            ClearCurrentConstellation();
            _currentConstellation = constellation;
            _currentPhase = Phase.Converging;
            _phaseTimer = 0f;
            _linesAlpha = 0f;
            _figureAlpha = 0f;
            _revealPercent = 0f;

            _captionName = constellation.Name;
            _captionScience = constellation.ScientificDescription;
            _captionCharacter = constellation.CharacterDescription;

            int wordCount = CountWords(_captionScience) + CountWords(_captionCharacter);
            _captionDisplayDuration = Mathf.Clamp(
                wordCount * _captionSecondsPerWord,
                _captionMinDuration, _captionMaxDuration);

            BuildStars();
            BuildConstellationLines();
            BuildClouds();
            BuildFigure();

            Vector2 centroid = Vector2.zero;
            float sMinX = float.MaxValue, sMaxX = float.MinValue;
            float sMinY = float.MaxValue, sMaxY = float.MinValue;
            foreach (var star in _currentConstellation.Stars)
            {
                centroid += star;
                float wx = (star.x - 0.5f) * _worldSize;
                float wy = (star.y - 0.5f) * _worldSize;
                sMinX = Mathf.Min(sMinX, wx); sMaxX = Mathf.Max(sMaxX, wx);
                sMinY = Mathf.Min(sMinY, wy); sMaxY = Mathf.Max(sMaxY, wy);
            }
            centroid /= _currentConstellation.Stars.Length;
            float cx = (centroid.x - 0.5f) * _worldSize;
            float cy = (centroid.y - 0.5f) * _worldSize;
            _zoomCamPos = new Vector3(cx, cy, _baseCamPos.z);

            sMinX -= StarPadding; sMaxX += StarPadding;
            sMinY -= StarPadding; sMaxY += StarPadding;

            var cam = Camera.main;
            float screenH = Screen.height > 0 ? Screen.height : 600f;
            float headerFrac = HeaderBarPixels / screenH;
            float captionFrac = CaptionBarPixels / screenH;
            float safeTop = 1f - 2f * headerFrac;
            float safeBot = 1f - 2f * captionFrac;
            safeTop = Mathf.Max(safeTop, 0.2f);
            safeBot = Mathf.Max(safeBot, 0.2f);

            float needUp = sMaxY - cy;
            float needDown = cy - sMinY;
            float sFromTop = needUp / safeTop;
            float sFromBot = needDown / safeBot;
            float aspect = cam != null ? cam.aspect : 1.78f;
            float sFromX = Mathf.Max(sMaxX - cx, cx - sMinX) / aspect;

            float fitSize = Mathf.Max(sFromTop, Mathf.Max(sFromBot, sFromX));
            float maxZoomSize = _baseCamSize / ZoomMaxMultiplier;
            _zoomCamTarget = Mathf.Max(fitSize, maxZoomSize);

            if (cam != null)
            {
                cam.orthographicSize = _baseCamSize;
                cam.transform.position = _baseCamPos;
            }
        }

        public void ClearCurrentConstellation()
        {
            DestroyChildren("Stars");
            DestroyChildren("ConLines");
            DestroyChildren("Clouds");
            DestroyChildren("Figure");

            _starObjs = null;
            _starRenderers = null;
            _starBaseScales = null;
            _starColors = null;
            _constellationLines = null;
            _clouds = null;
            _figureRenderer = null;
            _starsParent = null;
            _conLinesParent = null;
            _figureParent = null;

            var cam = Camera.main;
            if (cam != null && _baseCamSize > 0f)
            {
                cam.orthographicSize = _baseCamSize;
                cam.transform.position = _baseCamPos;
            }
        }

        public void UpdateBreathing(float breathPower)
        {
            if (_currentPhase != Phase.Clearing) return;

            bool blowing = breathPower >= _breathThreshold;

            // Move clouds based on breath
            float cloudsMoved = 0f;
            if (_clouds != null)
            {
                for (int i = 0; i < _clouds.Length; i++)
                {
                    ref CloudGroup cloud = ref _clouds[i];

                    if (blowing)
                    {
                        float force = (breathPower - _breathThreshold) / (1f - _breathThreshold);
                        cloud.Progress += force * _cloudMoveSpeed * Time.deltaTime / _clouds.Length;
                    }
                    else
                    {
                        cloud.Progress -= _cloudReturnSpeed * Time.deltaTime / _clouds.Length;
                    }
                    cloud.Progress = Mathf.Clamp01(cloud.Progress);
                    cloudsMoved += cloud.Progress;

                    UpdateCloudGroupPosition(ref cloud);
                }

                _revealPercent = cloudsMoved / _clouds.Length;
            }

            UpdateStarVisibility();

            if (_revealPercent >= _revealThreshold)
            {
                _currentPhase = Phase.AutoClearing;
                _phaseTimer = 0f;
            }
        }

        public void UpdateAnimation()
        {
            float dt = Time.deltaTime;

            switch (_currentPhase)
            {
                case Phase.Converging:
                    _phaseTimer += dt;
                    if (_clouds != null)
                    {
                        for (int i = 0; i < _clouds.Length; i++)
                        {
                            ref CloudGroup cloud = ref _clouds[i];
                            cloud.Progress = Mathf.MoveTowards(cloud.Progress, 0f, dt / _convergeDuration);
                            UpdateCloudGroupPosition(ref cloud);
                        }
                    }
                    if (_phaseTimer >= _convergeDuration)
                    {
                        _currentPhase = Phase.Clearing;
                        _phaseTimer = 0f;
                    }
                    break;

                case Phase.AutoClearing:
                    _phaseTimer += dt;

                    if (_clouds != null)
                    {
                        for (int i = 0; i < _clouds.Length; i++)
                        {
                            ref CloudGroup cloud = ref _clouds[i];
                            cloud.Progress = Mathf.MoveTowards(cloud.Progress, 0.85f, dt / _autoRevealDuration);
                            UpdateCloudGroupPosition(ref cloud);
                        }
                        _revealPercent = 1f;
                    }

                    UpdateStarVisibility();

                    if (_phaseTimer >= _autoRevealDuration)
                    {
                        _currentPhase = Phase.LinesAppearing;
                        _phaseTimer = 0f;
                        RevealAllStars();
                    }
                    break;

                case Phase.LinesAppearing:
                    _phaseTimer += dt;
                    _linesAlpha = Mathf.Clamp01(_phaseTimer / _linesFadeInDuration);
                    UpdateLineAlpha();
                    RevealAllStars();

                    if (_phaseTimer >= _linesFadeInDuration)
                    {
                        _currentPhase = Phase.FigureAppearing;
                        _phaseTimer = 0f;
                    }
                    break;

                case Phase.FigureAppearing:
                    _phaseTimer += dt;
                    _figureAlpha = Mathf.Clamp01(_phaseTimer / _figureFadeInDuration);
                    if (_figureRenderer != null)
                    {
                        Color c = _figureRenderer.color;
                        c.a = _figureAlpha * 0.3f;
                        _figureRenderer.color = c;
                    }
                    RevealAllStars();
                    ApplyCameraZoom(dt);

                    if (_phaseTimer >= _figureFadeInDuration)
                    {
                        _currentPhase = Phase.CaptionShowing;
                        _phaseTimer = 0f;
                    }
                    break;

                case Phase.CaptionShowing:
                    _phaseTimer += dt;
                    RevealAllStars();
                    ApplyCameraZoom(dt);
                    break;
            }

            AnimateStarTwinkle();
        }

        private void UpdateCloudGroupPosition(ref CloudGroup cloud)
        {
            Vector2 pos = Vector2.Lerp(cloud.HomePosition, cloud.EdgeTarget, cloud.Progress);

            float alpha = 1f - cloud.Progress * 0.6f;

            for (int j = 0; j < cloud.Objs.Length; j++)
            {
                cloud.Objs[j].transform.position = new Vector3(
                    pos.x + (j - 1) * 0.8f,
                    pos.y + (j % 2) * 0.4f,
                    -2f + j * 0.1f);

                Color c = cloud.Renderers[j].color;
                c.a = alpha * 0.85f;
                cloud.Renderers[j].color = c;
            }
        }

        private void UpdateStarVisibility()
        {
            if (_starRenderers == null || _clouds == null) return;

            for (int i = 0; i < _starRenderers.Length; i++)
            {
                Vector2 starPos = _currentConstellation.Stars[i];
                float worldX = (starPos.x - 0.5f) * _worldSize;
                float worldY = (starPos.y - 0.5f) * _worldSize;

                // Star is visible if no cloud group is close to its home position covering it
                float occlusion = 0f;
                for (int c = 0; c < _clouds.Length; c++)
                {
                    Vector2 cloudPos = Vector2.Lerp(_clouds[c].HomePosition, _clouds[c].EdgeTarget, _clouds[c].Progress);
                    float dist = Vector2.Distance(new Vector2(worldX, worldY), cloudPos);
                    float cloudRadius = 2.5f;
                    if (dist < cloudRadius)
                        occlusion = Mathf.Max(occlusion, 1f - _clouds[c].Progress);
                }

                float visibility = 1f - occlusion;
                Color col = _starRenderers[i].color;
                col.a = visibility * 0.9f;
                _starRenderers[i].color = col;
            }
        }

        private void RevealAllStars()
        {
            if (_starRenderers == null) return;
            for (int i = 0; i < _starRenderers.Length; i++)
            {
                Color c = _starRenderers[i].color;
                c.a = 0.95f;
                _starRenderers[i].color = c;
            }
        }

        private void AnimateStarTwinkle()
        {
            if (_starRenderers == null || _starTwinklePhase == null) return;

            for (int i = 0; i < _starRenderers.Length; i++)
            {
                float phase = _starTwinklePhase[i];
                float p1 = Mathf.Sin(Time.time * 1.8f + phase);
                float p2 = Mathf.Sin(Time.time * 2.7f + phase * 1.3f);
                float scaleMod = 1f + 0.12f * p1 + 0.06f * p2;
                float baseScale = (_starBaseScales != null && i < _starBaseScales.Length)
                    ? _starBaseScales[i] : 0.35f;
                _starObjs[i].transform.localScale = Vector3.one * baseScale * scaleMod;
            }
        }

        private void AnimateBackgroundStars()
        {
            if (_bgStarRenderers == null) return;

            for (int i = 0; i < _bgStarRenderers.Length; i++)
            {
                if (_bgStarRenderers[i] == null) continue;
                float phase = _bgStarPhases[i];
                float pulse = Mathf.Sin(Time.time * (1.0f + phase * 0.4f) + phase);

                float scale = _bgStarBaseScales[i] * (0.8f + 0.4f * (0.5f + 0.5f * pulse));
                _bgStarObjs[i].transform.localScale = Vector3.one * scale;

                Color c = _bgStarRenderers[i].color;
                c.a = _bgStarBaseAlphas[i] * (0.7f + 0.3f * (0.5f + 0.5f * pulse));
                _bgStarRenderers[i].color = c;
            }
        }

        private void UpdateLineAlpha()
        {
            if (_constellationLines == null) return;

            Color lineColor = new Color(0.6f, 0.75f, 1f, _linesAlpha * 0.7f);
            foreach (var lr in _constellationLines)
            {
                lr.startColor = lineColor;
                lr.endColor = lineColor;
            }
        }

        private void ApplyCameraZoom(float dt)
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, _zoomCamTarget, dt * 1.5f);
            cam.transform.position = Vector3.Lerp(cam.transform.position, _zoomCamPos, dt * 1.5f);
        }

        private static int CountWords(string text)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            int count = 0;
            bool inWord = false;
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsWhiteSpace(text[i]))
                    inWord = false;
                else if (!inWord)
                {
                    inWord = true;
                    count++;
                }
            }
            return count;
        }

        private void BuildStars()
        {
            var parent = new GameObject("Stars");
            parent.transform.SetParent(transform);
            _starsParent = parent.transform;

            int count = _currentConstellation.Stars.Length;
            _starObjs = new GameObject[count];
            _starRenderers = new SpriteRenderer[count];
            _starTwinklePhase = new float[count];
            _starBaseScales = new float[count];
            _starColors = new Color[count];

            var visuals = _currentConstellation.StarVisuals;

            for (int i = 0; i < count; i++)
            {
                Vector2 pos = _currentConstellation.Stars[i];
                float worldX = (pos.x - 0.5f) * _worldSize;
                float worldY = (pos.y - 0.5f) * _worldSize;

                float brightness = (visuals != null && i < visuals.Length) ? visuals[i].Brightness : 0.45f;
                Color tint = (visuals != null && i < visuals.Length) ? visuals[i].Tint : new Color(1f, 0.95f, 0.8f);

                float baseScale = Mathf.Lerp(0.18f, 0.65f, brightness);
                _starBaseScales[i] = baseScale;
                _starColors[i] = tint;

                var starObj = new GameObject($"Star_{i}");
                starObj.transform.SetParent(parent.transform);
                starObj.transform.position = new Vector3(worldX, worldY, -1f);
                starObj.transform.localScale = Vector3.one * baseScale;

                var sr = starObj.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(_starTex,
                    new Rect(0, 0, _starTex.width, _starTex.height),
                    new Vector2(0.5f, 0.5f), 32f);
                sr.color = new Color(tint.r, tint.g, tint.b, 0f);
                sr.sortingOrder = 5;

                _starObjs[i] = starObj;
                _starRenderers[i] = sr;
                _starTwinklePhase[i] = Random.Range(0f, Mathf.PI * 2f);
            }
        }

        private void BuildConstellationLines()
        {
            var parent = new GameObject("ConLines");
            parent.transform.SetParent(transform);
            _conLinesParent = parent.transform;

            int lineCount = _currentConstellation.LineConnections.Length / 2;
            _constellationLines = new LineRenderer[lineCount];

            for (int i = 0; i < lineCount; i++)
            {
                int idxA = _currentConstellation.LineConnections[i * 2];
                int idxB = _currentConstellation.LineConnections[i * 2 + 1];

                Vector2 posA = _currentConstellation.Stars[idxA];
                Vector2 posB = _currentConstellation.Stars[idxB];

                float ax = (posA.x - 0.5f) * _worldSize;
                float ay = (posA.y - 0.5f) * _worldSize;
                float bx = (posB.x - 0.5f) * _worldSize;
                float by = (posB.y - 0.5f) * _worldSize;

                var lineObj = new GameObject($"Line_{i}");
                lineObj.transform.SetParent(parent.transform);
                var lr = lineObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.material = _spriteMat;
                lr.startColor = new Color(0.6f, 0.75f, 1f, 0f);
                lr.endColor = new Color(0.6f, 0.75f, 1f, 0f);
                lr.widthMultiplier = 0.04f;
                lr.sortingOrder = 4;
                lr.SetPosition(0, new Vector3(ax, ay, -0.5f));
                lr.SetPosition(1, new Vector3(bx, by, -0.5f));

                _constellationLines[i] = lr;
            }
        }

        private void BuildClouds()
        {
            var parent = new GameObject("Clouds");
            parent.transform.SetParent(transform);

            _clouds = new CloudGroup[_cloudGroupCount];

            for (int i = 0; i < _cloudGroupCount; i++)
            {
                // Distribute clouds across the sky
                float angle = (float)i / _cloudGroupCount * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                float radius = Random.Range(1f, _worldSize * 0.35f);
                Vector2 home = new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius);

                // Edge target: push cloud to nearest screen edge
                Vector2 edgeDir = home.normalized;
                if (edgeDir.sqrMagnitude < 0.01f)
                    edgeDir = new Vector2(Random.Range(-1f, 1f), Random.Range(-1f, 1f)).normalized;
                Vector2 edge = edgeDir * (_worldSize * 0.75f);

                int blobCount = Random.Range(2, 4);
                var objs = new GameObject[blobCount];
                var renderers = new SpriteRenderer[blobCount];

                for (int j = 0; j < blobCount; j++)
                {
                    var cloudObj = new GameObject($"Cloud_{i}_{j}");
                    cloudObj.transform.SetParent(parent.transform);

                    var sr = cloudObj.AddComponent<SpriteRenderer>();
                    sr.sprite = Sprite.Create(_cloudTex,
                        new Rect(0, 0, _cloudTex.width, _cloudTex.height),
                        new Vector2(0.5f, 0.5f), 16f);

                    float gray = Random.Range(0.40f, 0.58f);
                    sr.color = new Color(gray, gray, gray + 0.03f, 0.85f);
                    sr.sortingOrder = 10;

                    float blobScale = Random.Range(1.8f, 3.2f);
                    cloudObj.transform.localScale = new Vector3(blobScale, blobScale * 0.6f, 1f);

                    objs[j] = cloudObj;
                    renderers[j] = sr;
                }

                _clouds[i] = new CloudGroup
                {
                    Objs = objs,
                    Renderers = renderers,
                    HomePosition = home,
                    EdgeTarget = edge,
                    Progress = 1f
                };

                UpdateCloudGroupPosition(ref _clouds[i]);
            }
        }

        private void BuildFigure()
        {
            var parent = new GameObject("Figure");
            parent.transform.SetParent(transform);
            _figureParent = parent.transform;

            // Compute centroid of the constellation for figure placement
            Vector2 centroid = Vector2.zero;
            foreach (var star in _currentConstellation.Stars)
                centroid += star;
            centroid /= _currentConstellation.Stars.Length;

            float cx = (centroid.x - 0.5f) * _worldSize;
            float cy = (centroid.y - 0.5f) * _worldSize;

            var figObj = new GameObject("FigureSilhouette");
            figObj.transform.SetParent(parent.transform);
            figObj.transform.position = new Vector3(cx, cy, -0.8f);

            _figureRenderer = figObj.AddComponent<SpriteRenderer>();
            _figureRenderer.sprite = Sprite.Create(_glowTex,
                new Rect(0, 0, _glowTex.width, _glowTex.height),
                new Vector2(0.5f, 0.5f), 8f);
            _figureRenderer.color = new Color(0.5f, 0.6f, 0.9f, 0f);
            _figureRenderer.sortingOrder = 3;

            // Scale to roughly cover the constellation
            float span = 0f;
            foreach (var star in _currentConstellation.Stars)
            {
                float d = Vector2.Distance(star, centroid);
                if (d > span) span = d;
            }
            float figScale = span * _worldSize * 0.8f;
            figObj.transform.localScale = new Vector3(figScale, figScale, 1f);
        }

        private void BuildBackgroundStars()
        {
            var parent = new GameObject("BgStars");
            parent.transform.SetParent(transform);

            var cam = Camera.main;
            float depth = Mathf.Abs(cam.transform.position.z);
            float cx = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth)).x;
            float cy = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, depth)).y;
            float halfW = cam.ViewportToWorldPoint(new Vector3(1f, 0.5f, depth)).x - cx;
            float halfH = cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, depth)).y - cy;

            int count = 120;
            _bgStarObjs = new GameObject[count];
            _bgStarRenderers = new SpriteRenderer[count];
            _bgStarPhases = new float[count];
            _bgStarBaseScales = new float[count];
            _bgStarBaseAlphas = new float[count];

            for (int i = 0; i < count; i++)
            {
                var obj = new GameObject($"BgStar_{i}");
                obj.transform.SetParent(parent.transform);

                float x = cx + Random.Range(-halfW * 1.1f, halfW * 1.1f);
                float y = cy + Random.Range(-halfH * 1.1f, halfH * 1.1f);
                obj.transform.position = new Vector3(x, y, 0.5f);

                float size = Random.Range(0.04f, 0.13f);
                obj.transform.localScale = Vector3.one * size;

                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(_glowTex,
                    new Rect(0, 0, _glowTex.width, _glowTex.height),
                    new Vector2(0.5f, 0.5f), 32f);

                float warmth = Random.value;
                Color col = warmth < 0.3f
                    ? new Color(0.8f, 0.85f, 1f)
                    : warmth < 0.7f
                        ? new Color(1f, 1f, 0.95f)
                        : new Color(1f, 0.92f, 0.8f);
                col.a = Random.Range(0.2f, 0.6f);
                sr.color = col;
                sr.sortingOrder = 1;

                _bgStarObjs[i] = obj;
                _bgStarRenderers[i] = sr;
                _bgStarPhases[i] = Random.Range(0f, Mathf.PI * 2f);
                _bgStarBaseScales[i] = size;
                _bgStarBaseAlphas[i] = col.a;
            }
        }

        private void DestroyChildren(string name)
        {
            var existing = transform.Find(name);
            if (existing != null) Destroy(existing.gameObject);
        }

        private static void EnsureMaterial()
        {
            if (_spriteMat != null) return;
            _spriteMat = new Material(Shader.Find("Sprites/Default"));
        }

        private static Texture2D GenerateStarTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float spikeWidth = size * 0.04f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    float glow = Mathf.Clamp01(1f - dist / center);
                    glow = Mathf.Pow(glow, 1.6f);

                    float adx = Mathf.Abs(dx);
                    float ady = Mathf.Abs(dy);
                    float sH = Mathf.Exp(-ady / spikeWidth) * Mathf.Clamp01(1f - adx / (center * 0.9f));
                    float sV = Mathf.Exp(-adx / spikeWidth) * Mathf.Clamp01(1f - ady / (center * 0.9f));
                    float spike = Mathf.Max(sH, sV) * 0.25f;

                    float brightness = Mathf.Clamp01(glow + spike);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, brightness));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GenerateCloudTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f;

            Vector2[] blobs = {
                new Vector2(c, c),
                new Vector2(c * 0.65f, c * 0.9f),
                new Vector2(c * 1.35f, c * 0.85f),
                new Vector2(c * 0.8f, c * 1.25f),
                new Vector2(c * 1.2f, c * 1.15f),
                new Vector2(c * 0.9f, c * 0.65f),
            };
            float[] radii = { c * 0.65f, c * 0.45f, c * 0.42f, c * 0.38f, c * 0.40f, c * 0.36f };

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float alpha = 0f;
                    for (int b = 0; b < blobs.Length; b++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), blobs[b]);
                        float edge = Mathf.Clamp01((radii[b] - dist) / (radii[b] * 0.35f));
                        edge = edge * edge;
                        alpha = Mathf.Max(alpha, edge);
                    }
                    float bright = 0.82f + 0.18f * alpha;
                    tex.SetPixel(x, y, new Color(bright, bright, bright, alpha));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GenerateGlowTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float glow = Mathf.Clamp01(1f - dist / center);
                    glow = Mathf.Pow(glow, 1.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, glow));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }
}
