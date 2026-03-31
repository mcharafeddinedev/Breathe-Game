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
        [SerializeField] private int _cloudGroupCount = 8;
        [SerializeField] private float _cloudMoveSpeed = 2.5f;
        [SerializeField] private float _cloudReturnSpeed = 1.2f;
        [SerializeField] private float _breathThreshold = 0.30f;

        [Header("Reveal")]
        [SerializeField] private float _revealThreshold = 0.87f;
        [SerializeField] private float _autoRevealDuration = 3f;

        [Header("Animation")]
        [SerializeField] private float _linesFadeInDuration = 4f;
        [SerializeField] private float _figureFadeInDuration = 3f;
        [SerializeField] private float _captionDisplayDuration = 6f;

        public enum Phase { Clearing, AutoClearing, LinesAppearing, FigureAppearing, CaptionShowing, RoundDone }

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

        private static Material _spriteMat;
        private Texture2D _starTex;
        private Texture2D _cloudTex;
        private Texture2D _glowTex;

        public Phase CurrentPhase => _currentPhase;
        public float RevealPercent => _revealPercent;
        public string CaptionName => _captionName;
        public string CaptionScience => _captionScience;
        public string CaptionCharacter => _captionCharacter;
        public bool IsRoundDone => _currentPhase == Phase.RoundDone;

        public void Initialize()
        {
            EnsureMaterial();
            _starTex = GenerateStarTexture(32);
            _cloudTex = GenerateCloudTexture(64);
            _glowTex = GenerateGlowTexture(64);

            var cam = Camera.main;
            if (cam != null)
                cam.backgroundColor = new Color(0.02f, 0.02f, 0.08f);
        }

        public void SetupConstellation(ConstellationDatabase.ConstellationData constellation)
        {
            ClearCurrentConstellation();
            _currentConstellation = constellation;
            _currentPhase = Phase.Clearing;
            _phaseTimer = 0f;
            _linesAlpha = 0f;
            _figureAlpha = 0f;
            _revealPercent = 0f;

            _captionName = constellation.Name;
            _captionScience = constellation.ScientificDescription;
            _captionCharacter = constellation.CharacterDescription;

            BuildStars();
            BuildConstellationLines();
            BuildClouds();
            BuildFigure();
        }

        public void ClearCurrentConstellation()
        {
            DestroyChildren("Stars");
            DestroyChildren("ConLines");
            DestroyChildren("Clouds");
            DestroyChildren("Figure");

            _starObjs = null;
            _starRenderers = null;
            _constellationLines = null;
            _clouds = null;
            _figureRenderer = null;
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
                case Phase.AutoClearing:
                    _phaseTimer += dt;

                    // Smoothly push all clouds to edges
                    if (_clouds != null)
                    {
                        for (int i = 0; i < _clouds.Length; i++)
                        {
                            ref CloudGroup cloud = ref _clouds[i];
                            cloud.Progress = Mathf.MoveTowards(cloud.Progress, 1f, dt / _autoRevealDuration);
                            UpdateCloudGroupPosition(ref cloud);
                        }
                        _revealPercent = 1f;
                    }

                    UpdateStarVisibility();

                    if (_phaseTimer >= _autoRevealDuration)
                    {
                        _currentPhase = Phase.LinesAppearing;
                        _phaseTimer = 0f;
                        // Fully reveal all stars
                        RevealAllStars();
                    }
                    break;

                case Phase.LinesAppearing:
                    _phaseTimer += dt;
                    _linesAlpha = Mathf.Clamp01(_phaseTimer / _linesFadeInDuration);
                    UpdateLineAlpha();

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

                    if (_phaseTimer >= _figureFadeInDuration)
                    {
                        _currentPhase = Phase.CaptionShowing;
                        _phaseTimer = 0f;
                    }
                    break;

                case Phase.CaptionShowing:
                    _phaseTimer += dt;
                    if (_phaseTimer >= _captionDisplayDuration)
                    {
                        _currentPhase = Phase.RoundDone;
                    }
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
                float twinkle = 0.8f + 0.2f * Mathf.Sin(Time.time * 2f + _starTwinklePhase[i]);
                float baseAlpha = _starRenderers[i].color.a;

                // Scale slightly for a glow pulse
                float scaleBase = 0.3f;
                float scalePulse = scaleBase * (0.95f + 0.05f * Mathf.Sin(Time.time * 1.5f + _starTwinklePhase[i]));
                _starObjs[i].transform.localScale = Vector3.one * scalePulse;
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

        private void BuildStars()
        {
            var parent = new GameObject("Stars");
            parent.transform.SetParent(transform);

            int count = _currentConstellation.Stars.Length;
            _starObjs = new GameObject[count];
            _starRenderers = new SpriteRenderer[count];
            _starTwinklePhase = new float[count];

            for (int i = 0; i < count; i++)
            {
                Vector2 pos = _currentConstellation.Stars[i];
                float worldX = (pos.x - 0.5f) * _worldSize;
                float worldY = (pos.y - 0.5f) * _worldSize;

                var starObj = new GameObject($"Star_{i}");
                starObj.transform.SetParent(parent.transform);
                starObj.transform.position = new Vector3(worldX, worldY, -1f);
                starObj.transform.localScale = Vector3.one * 0.3f;

                var sr = starObj.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(_glowTex,
                    new Rect(0, 0, _glowTex.width, _glowTex.height),
                    new Vector2(0.5f, 0.5f), 32f);
                sr.color = new Color(1f, 0.95f, 0.8f, 0f); // Start hidden
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
                Vector2 edge = edgeDir * (_worldSize * 0.8f);

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

                    float gray = Random.Range(0.7f, 0.9f);
                    sr.color = new Color(gray, gray, gray + 0.05f, 0.85f);
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
                    Progress = 0f
                };

                UpdateCloudGroupPosition(ref _clouds[i]);
            }
        }

        private void BuildFigure()
        {
            var parent = new GameObject("Figure");
            parent.transform.SetParent(transform);

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
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float brightness = Mathf.Clamp01(1f - dist / center);
                    brightness = brightness * brightness * brightness;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, brightness));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GenerateCloudTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float radius = center - 2f;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    float edge = Mathf.Clamp01((radius - dist) / (radius * 0.4f));
                    edge = edge * edge;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, edge));
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
