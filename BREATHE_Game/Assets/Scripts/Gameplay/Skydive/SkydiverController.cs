using UnityEngine;

namespace Breathe.Gameplay
{
    // Manages a single skydiver's fall, wind forces, breath opposition, landing,
    // and all visual elements for the Skydive minigame scene.
    public class SkydiverController : MonoBehaviour
    {
        [Header("World Bounds")]
        [SerializeField] private float _groundY = -6f;
        [SerializeField] private float _spawnY = 8.5f;
        [SerializeField] private float _xMin = -7f;
        [SerializeField] private float _xMax = 7f;

        [Header("Fall")]
        [SerializeField] private float _fallSpeed = 0.42f;

        [Header("Wind — fixed per diver, scales with difficulty")]
        [SerializeField] private float _windMinForce = 1.0f;
        [SerializeField] private float _windMaxForce = 4.0f;
        [SerializeField] private float _windForcePerDiver = 0.3f;

        [Header("Breath Opposition")]
        [SerializeField] private float _breathForceMultiplier = 4.5f;

        [Header("Target Drift")]
        [SerializeField] private float _targetDriftForce = 0.4f;

        [Header("Landing")]
        [SerializeField] private float _perfectRadius = 0.5f;
        [SerializeField] private float _goodRadius = 1.2f;
        [SerializeField] private float _nearRadius = 2.0f;

        public enum LandingQuality { Perfect, Good, Near, OffTarget }
        public enum DiverState { Idle, Falling, Landed }

        private DiverState _state = DiverState.Idle;
        private Vector2 _diverPos;
        private float _targetX;
        private float _currentWindForce;
        private float _displayWindForce;
        private int _diverNumber;

        // Visual objects
        private GameObject _diverObj;
        private LineRenderer[] _diverLines;
        private SpriteRenderer _diverHead;
        private SpriteRenderer _canopyRenderer;
        private LineRenderer[] _chuteStrings;
        private GameObject _targetObj;
        private SpriteRenderer[] _targetRings;
        private LineRenderer[] _windStreaks;
        private GameObject _groundObj;
        private GameObject _skyObj;
        private GameObject _landingFeedbackObj;
        private SpriteRenderer _landingFeedbackRenderer;
        private float _landingFeedbackTimer;

        private static Material _spriteMat;
        private Texture2D _circleTex;

        // Public state
        public DiverState State => _state;
        public float CurrentWindForce => _currentWindForce;
        public float DisplayWindForce => _displayWindForce;
        public float DiverX => _diverPos.x;
        public float TargetX => _targetX;

        private LandingQuality _lastLandingQuality;
        private float _lastLandingDistance;
        private float _prevWindSign;
        public LandingQuality LastLandingQuality => _lastLandingQuality;
        public float LastLandingDistance => _lastLandingDistance;
        public bool WindChangedThisFrame { get; private set; }

        private GameObject _windArrowObj;
        private SpriteRenderer _windArrowRenderer;
        private float _arrowFadeTimer;

        private float[] _streakSpeedJitter;
        private float[] _streakPhaseOffset;
        private float[] _streakYJitter;

        private SpriteRenderer[] _skyClouds;
        private float[] _cloudSpeedX;
        private float[] _cloudBaseX;

        private SpriteRenderer _jetpackRenderer;
        private LineRenderer[] _flameLines;
        private float _lastBreathPower;
        private float _smoothBreathPower;

        // Parallax scrolling
        private float _scrollOffset;
        private const float SCROLL_SPEED = 1.2f;
        private const float TREE_RATE = 0.30f;
        private const float TARGET_RATE = 0.35f;
        private const float CITY_RATE = 0.10f;

        private struct ParallaxTree
        {
            public Transform tf;
            public float origX;
            public float rate;
        }
        private ParallaxTree[] _parallaxTrees;
        private float _treeWrapWidth;

        private Transform _cityTransform;
        private float _cityOrigX;
        private float _cityWorldWidth;

        // Boost transition between divers (exit old target left, enter new target right)
        private enum BoostStage { None, Exit, Entry }
        private BoostStage _boostStage = BoostStage.None;
        private bool _boostActive;
        private float _boostTimer;
        private float _boostScrollStart;
        private float _boostScrollEnd;
        private float _pendingTargetX;
        private const float BOOST_DURATION = 1.0f;

        public void Initialize()
        {
            EnsureMaterial();
            _circleTex = GenerateCircleTexture(64);

            BuildBackground();
            BuildGround();
            BuildTarget();
            BuildDiver();
            BuildWindStreaks();
            BuildWindArrow();
            BuildSkyClouds();
            BuildBreathTrail();
            BuildLandingFeedback();

            _targetX = Random.Range(_xMin * 0.7f, _xMax * 0.7f);
            UpdateTargetPosition();
        }

        public void SpawnDiver()
        {
            _diverNumber++;

            float windStrength = Mathf.Clamp(
                _windMinForce + (_diverNumber - 1) * _windForcePerDiver,
                _windMinForce, _windMaxForce);
            float prevDir = Mathf.Sign(_currentWindForce);
            if (prevDir == 0f) prevDir = 1f;
            float windDir = (Random.value < 0.55f) ? -prevDir : prevDir;
            _currentWindForce = windDir * windStrength;
            _displayWindForce = 0f;
            _arrowFadeTimer = 1.5f;

            float spawnX = _targetX + windDir * Random.Range(1f, 2.5f);
            spawnX = Mathf.Clamp(spawnX, _xMin * 0.8f, _xMax * 0.8f);

            _diverPos = new Vector2(spawnX, _spawnY);
            _state = DiverState.Falling;
            _prevWindSign = windDir;

            _diverObj.SetActive(true);
            _landingFeedbackObj.SetActive(false);

            WindChangedThisFrame = true;
            Debug.Log($"[Skydive] Diver #{_diverNumber} — wind: {_currentWindForce:F1} " +
                $"(strength {windStrength:F1}), target X: {_targetX:F1}, spawn X: {spawnX:F1}");
        }

        public void HideDiver()
        {
            _state = DiverState.Idle;
            _diverObj.SetActive(false);
        }

        public void UpdateDiver(float breathPower)
        {
            if (_state != DiverState.Falling) return;

            float dt = Time.deltaTime;
            WindChangedThisFrame = false;

            _displayWindForce = Mathf.Lerp(_displayWindForce, _currentWindForce, dt * 4f);

            float oppositionDir = -Mathf.Sign(_currentWindForce);
            float breathForce = oppositionDir * breathPower * _breathForceMultiplier;

            float targetDiff = _targetX - _diverPos.x;
            float driftForce = Mathf.Sign(targetDiff)
                * Mathf.Min(Mathf.Abs(targetDiff) * 0.15f, _targetDriftForce);

            float totalXForce = _currentWindForce + breathForce + driftForce;
            _diverPos.x += totalXForce * dt;
            _diverPos.x = Mathf.Clamp(_diverPos.x, _xMin, _xMax);
            _diverPos.y -= _fallSpeed * dt;

            _lastBreathPower = breathPower;
            _smoothBreathPower = Mathf.Lerp(_smoothBreathPower, breathPower, dt * 8f);

            UpdateDiverVisual(totalXForce);
            UpdateBreathTrail();
            UpdateWindStreakVisuals();
            UpdateSkyClouds();
            UpdateParallaxScroll();

            float landY = _groundY - 1.6f;
            if (_diverPos.y <= landY)
            {
                _diverPos.y = landY;
                _state = DiverState.Landed;
                EvaluateLanding();
                ShowLandingFeedback();
            }
        }

        public void UpdatePostLanding()
        {
            if (_landingFeedbackTimer > 0f)
            {
                _landingFeedbackTimer -= Time.deltaTime;
                float alpha = Mathf.Clamp01(_landingFeedbackTimer / 1.5f);
                if (_landingFeedbackRenderer != null)
                {
                    Color c = _landingFeedbackRenderer.color;
                    c.a = alpha;
                    _landingFeedbackRenderer.color = c;
                }
            }


            UpdateWindStreakVisuals();
            UpdateSkyClouds();
            UpdateParallaxScroll();
        }

        private void EvaluateLanding()
        {
            _lastLandingDistance = Mathf.Abs(_diverPos.x - _targetX);

            if (_lastLandingDistance <= _perfectRadius)
                _lastLandingQuality = LandingQuality.Perfect;
            else if (_lastLandingDistance <= _goodRadius)
                _lastLandingQuality = LandingQuality.Good;
            else if (_lastLandingDistance <= _nearRadius)
                _lastLandingQuality = LandingQuality.Near;
            else
                _lastLandingQuality = LandingQuality.OffTarget;
        }

        private void ShowLandingFeedback()
        {
            _landingFeedbackObj.SetActive(true);
            float landY = _groundY - 1.6f;
            _landingFeedbackObj.transform.position = new Vector3(_diverPos.x, landY + 0.6f, -2f);
            _landingFeedbackTimer = 1.5f;

            Color feedbackColor = _lastLandingQuality switch
            {
                LandingQuality.Perfect => new Color(0.2f, 1f, 0.4f, 0.25f),
                LandingQuality.Good => new Color(0.5f, 0.9f, 0.3f, 0.2f),
                LandingQuality.Near => new Color(1f, 0.8f, 0.2f, 0.2f),
                _ => new Color(1f, 0.3f, 0.2f, 0.2f)
            };
            _landingFeedbackRenderer.color = feedbackColor;
        }

        private void UpdateDiverVisual(float netForce = 0f)
        {
            _diverObj.transform.position = new Vector3(_diverPos.x, _diverPos.y, -1f);

            float tilt = Mathf.Clamp(netForce * 8f, -35f, 35f);
            _diverObj.transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        }

        private void UpdateWindStreakVisuals()
        {
            if (_windStreaks == null) return;

            float windDir = Mathf.Sign(_displayWindForce);
            float windStrength = Mathf.Abs(_displayWindForce) / _windMaxForce;
            float range = _xMax - _xMin;
            float baseSpeed = 2f + windStrength * 4f;

            for (int i = 0; i < _windStreaks.Length; i++)
            {
                float streakY = _groundY + 0.8f + i * 0.7f + _streakYJitter[i];
                float speed = baseSpeed * _streakSpeedJitter[i];
                float phase = Mathf.Repeat(Time.time * speed + _streakPhaseOffset[i], range);
                float startX = windDir > 0
                    ? _xMin + phase
                    : _xMax - phase;
                float length = 1.2f + windStrength * 2.5f;

                float alpha = 0.10f + windStrength * 0.45f;
                Color streakColor = new Color(0.85f, 0.9f, 1f, alpha);
                _windStreaks[i].startColor = new Color(streakColor.r, streakColor.g, streakColor.b, 0f);
                _windStreaks[i].endColor = streakColor;

                _windStreaks[i].SetPosition(0, new Vector3(startX, streakY, 1f));
                _windStreaks[i].SetPosition(1, new Vector3(startX + windDir * length, streakY, 1f));
            }

            UpdateWindArrow(windDir, windStrength);
        }

        private void UpdateWindArrow(float windDir, float windStrength)
        {
            if (_windArrowObj == null) return;

            if (_arrowFadeTimer > 0f)
                _arrowFadeTimer -= Time.deltaTime;

            float fadeFactor = Mathf.Clamp01(_arrowFadeTimer / 0.5f);
            float arrowAlpha = Mathf.Clamp01(windStrength * 0.6f) * fadeFactor;
            Color arrowColor = windStrength > 0.5f
                ? Color.Lerp(new Color(1f, 0.9f, 0.5f), new Color(1f, 0.4f, 0.3f), (windStrength - 0.5f) * 2f)
                : new Color(0.8f, 0.9f, 1f);
            arrowColor.a = arrowAlpha;
            _windArrowRenderer.color = arrowColor;

            float scaleX = Mathf.Abs(_windArrowObj.transform.localScale.x);
            _windArrowObj.transform.localScale = new Vector3(
                windDir >= 0 ? scaleX : -scaleX,
                _windArrowObj.transform.localScale.y, 1f);

            float bob = Mathf.Sin(Time.time * 2f) * 0.1f;
            _windArrowObj.transform.position = new Vector3(
                windDir * 2f, _groundY + 3f + bob, -1f);
        }

        private void UpdateTargetPosition()
        {
            if (_targetObj == null) return;
            _targetObj.transform.position = new Vector3(_targetX, _groundY - 1.6f, -0.5f);
        }

        private void BuildBackground()
        {
            var cam = Camera.main;
            if (cam != null)
                cam.backgroundColor = new Color(0.45f, 0.68f, 0.92f);

            _skyObj = new GameObject("Sky");
            _skyObj.transform.SetParent(transform);
            var skyRend = _skyObj.AddComponent<SpriteRenderer>();
            var skyTex = GenerateGradientTexture(8, 64,
                new Color(0.55f, 0.75f, 0.90f),
                new Color(0.35f, 0.55f, 0.85f));
            skyRend.sprite = Sprite.Create(skyTex, new Rect(0, 0, skyTex.width, skyTex.height),
                new Vector2(0.5f, 0.5f), 4f);
            skyRend.sortingOrder = -30;
            _skyObj.transform.position = new Vector3(0f, 2f, 10f);
            _skyObj.transform.localScale = new Vector3(18f, 10f, 1f);

            BuildCitySkyline();
            BuildTreeline();
        }

        private void BuildCitySkyline()
        {
            var cityTex = GenerateCityTexture(512, 80);
            var cityObj = new GameObject("CitySkyline");
            cityObj.transform.SetParent(transform);
            var sr = cityObj.AddComponent<SpriteRenderer>();
            sr.sprite = Sprite.Create(cityTex,
                new Rect(0, 0, cityTex.width, cityTex.height),
                new Vector2(0.5f, 0f), 16f);
            sr.sortingOrder = -28;
            sr.color = new Color(0.75f, 0.78f, 0.85f, 0.45f);
            cityObj.transform.position = new Vector3(0f, _groundY - 0.15f, 8f);
            cityObj.transform.localScale = new Vector3(2.6f, 1.4f, 1f);
            _cityTransform = cityObj.transform;
            _cityOrigX = 0f;
            _cityWorldWidth = (cityTex.width / 16f) * 2.6f;
        }

        private void BuildTreeline()
        {
            var parent = new GameObject("Trees");
            parent.transform.SetParent(transform);

            const int variants = 16;
            var variantRng = new System.Random(55);
            Sprite[] treeSprites = new Sprite[variants];
            for (int v = 0; v < variants; v++)
            {
                byte gR = (byte)variantRng.Next(25, 55);
                byte gG = (byte)variantRng.Next(75, 135);
                byte gB = (byte)variantRng.Next(15, 45);
                var tex = GenerateTreeTexture(32, 30, v + 10, gR, gG, gB);
                treeSprites[v] = Sprite.Create(tex,
                    new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0f), 16f);
            }

            int treeCount = 260;
            var rng = new System.Random(77);
            float xSpread = (_xMax - _xMin) * 3.0f;
            float xStart = _xMin * 3.0f;
            _treeWrapWidth = xSpread;

            float[] treeXPositions = new float[treeCount];
            for (int i = 0; i < treeCount; i++)
                treeXPositions[i] = xStart + xSpread * ((float)i / treeCount)
                    + (float)(rng.NextDouble() * 0.4 - 0.2);
            System.Array.Sort(treeXPositions);

            const float MIN_TRUNK_GAP = 0.18f;
            for (int i = 1; i < treeCount; i++)
            {
                float gap = treeXPositions[i] - treeXPositions[i - 1];
                if (gap < MIN_TRUNK_GAP)
                    treeXPositions[i] = treeXPositions[i - 1] + MIN_TRUNK_GAP;
            }

            _parallaxTrees = new ParallaxTree[treeCount];

            for (int i = 0; i < treeCount; i++)
            {
                float tx = treeXPositions[i];

                int depthLayer = rng.Next(0, 10);
                float baseY = _groundY - 0.1f - (float)rng.NextDouble() * 0.6f;
                int sortOrder = -14 + depthLayer;

                float depthT = depthLayer / 9f;
                float brightness = 0.75f + depthT * 0.25f;

                float sizeBase = 0.55f + depthT * 0.5f;
                float sizeRand = 0.85f + (float)rng.NextDouble() * 0.35f;
                float scaleX = sizeBase * sizeRand * (0.95f + (float)rng.NextDouble() * 0.1f);
                float scaleY = sizeBase * sizeRand * (0.95f + (float)rng.NextDouble() * 0.1f);
                bool flipX = rng.NextDouble() > 0.5;

                var treeObj = new GameObject($"Tree_{i}");
                treeObj.transform.SetParent(parent.transform);
                var sr = treeObj.AddComponent<SpriteRenderer>();
                sr.sprite = treeSprites[rng.Next(0, variants)];
                sr.color = new Color(brightness, brightness, brightness, 1f);
                sr.sortingOrder = sortOrder;
                sr.flipX = flipX;
                treeObj.transform.position = new Vector3(tx, baseY, 2f);
                treeObj.transform.localScale = new Vector3(scaleX, scaleY, 1f);

                _parallaxTrees[i] = new ParallaxTree
                {
                    tf = treeObj.transform,
                    origX = tx,
                    rate = 0.30f
                };
            }
        }

        private static Texture2D GenerateTreeTexture(int w, int h, int seed,
            byte leafR, byte leafG, byte leafB)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            var rng = new System.Random(seed);

            Color32 trunkLight = new Color32(100, 72, 45, 255);
            Color32 trunkDark = new Color32(65, 45, 30, 255);
            Color32 leafBright = new Color32(leafR, leafG, leafB, 255);
            Color32 leafMid = new Color32(
                (byte)(leafR * 0.7f), (byte)(leafG * 0.7f), (byte)(leafB * 0.7f), 255);
            Color32 leafDeep = new Color32(
                (byte)(leafR * 0.45f), (byte)(leafG * 0.45f), (byte)(leafB * 0.45f), 255);

            int trunkW = 2 + rng.Next(0, 2);
            int crownBot = h * 40 / 100;
            int trunkLeft = w / 2 - trunkW / 2;

            for (int y = 0; y < crownBot + 2; y++)
            for (int x = trunkLeft; x < trunkLeft + trunkW; x++)
            {
                if (x < 0 || x >= w) continue;
                bool edge = x == trunkLeft || x == trunkLeft + trunkW - 1;
                pixels[y * w + x] = edge ? trunkDark : trunkLight;
            }
            float cx = w * 0.5f;
            float cy = crownBot + (h - crownBot) * 0.5f;
            float rx = w * 0.46f + rng.Next(-1, 2);
            float ry = (h - crownBot) * 0.46f + rng.Next(-1, 2);

            for (int y = crownBot; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float d = dx * dx + dy * dy;
                if (d > 1f) continue;

                float edgeDist = 1f - d;
                bool outline = edgeDist < 0.15f;
                float rowT = (float)(y - crownBot) / (h - crownBot);

                Color32 col;
                if (outline)
                    col = leafDeep;
                else if (edgeDist < 0.35f || rowT < 0.3f)
                    col = leafMid;
                else
                    col = rng.Next(0, 4) == 0 ? leafBright : leafMid;

                if (!outline && rowT > 0.5f && edgeDist > 0.3f && rng.Next(0, 3) != 0)
                    col = leafBright;

                pixels[y * w + x] = col;
            }

            tex.SetPixels32(pixels);
            tex.Apply(false);
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        private static Texture2D GenerateCityTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];

            Color32 clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            var rng = new System.Random(42);
            int buildingCount = 45;

            int[] bHeights = new int[buildingCount];
            int[] bWidths = new int[buildingCount];
            int[] bXpos = new int[buildingCount];
            byte[] bBaseVals = new byte[buildingCount];

            for (int b = 0; b < buildingCount; b++)
            {
                bWidths[b] = rng.Next(4, 14);
                bHeights[b] = rng.Next(h / 3, h - 2);
                bXpos[b] = Mathf.Clamp(
                    (int)(((float)b / buildingCount) * (w - bWidths[b]) + rng.Next(-2, 3)),
                    0, w - bWidths[b]);
                bBaseVals[b] = (byte)rng.Next(60, 90);
            }

            int spire1 = -1, spire2 = -1;
            for (int b = 0; b < buildingCount; b++)
            {
                if (spire1 < 0 || bHeights[b] > bHeights[spire1])
                {
                    spire2 = spire1;
                    spire1 = b;
                }
                else if (spire2 < 0 || bHeights[b] > bHeights[spire2])
                    spire2 = b;
            }

            for (int b = 0; b < buildingCount; b++)
            {
                int bw = bWidths[b], bh = bHeights[b], bx = bXpos[b];
                byte baseV = bBaseVals[b];
                Color32 wallColor = new Color32(baseV, (byte)(baseV + 5), (byte)(baseV + 15), 200);
                Color32 darkEdge = new Color32(
                    (byte)(baseV * 0.65f), (byte)(baseV * 0.68f), (byte)(baseV * 0.75f), 200);

                for (int y = 0; y < bh; y++)
                for (int x = 0; x < bw; x++)
                {
                    int px = bx + x, py = y;
                    if (px < 0 || px >= w || py < 0 || py >= h) continue;

                    bool isEdge = x == 0 || x == bw - 1 || y == bh - 1;
                    Color32 col = isEdge ? darkEdge : wallColor;

                    if (!isEdge && bw >= 4 && y > 2 && y < bh - 1 && x > 0 && x < bw - 1)
                    {
                        bool winRow = (y % 3 == 1);
                        bool winCol = (x % 2 == 1);
                        if (winRow && winCol)
                        {
                            bool lit = rng.NextDouble() < 0.3;
                            col = lit
                                ? new Color32(200, 190, 130, 160)
                                : new Color32((byte)(baseV - 10), (byte)(baseV - 8), (byte)(baseV - 5), 180);
                        }
                    }

                    pixels[py * w + px] = col;
                }

                if (b == spire1 || (b == spire2 && rng.NextDouble() < 0.5))
                {
                    int spireW = Mathf.Max(1, bw / 4);
                    int spireH = rng.Next(6, 14);
                    int spireX = bx + bw / 2 - spireW / 2;
                    for (int y = bh; y < bh + spireH && y < h; y++)
                    for (int x = 0; x < spireW; x++)
                    {
                        int px = spireX + x;
                        if (px >= 0 && px < w)
                            pixels[y * w + px] = darkEdge;
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        private void BuildGround()
        {
            _groundObj = new GameObject("Ground");
            _groundObj.transform.SetParent(transform);

            var grassRend = _groundObj.AddComponent<SpriteRenderer>();
            var grassTex = GenerateGradientTexture(4, 16,
                new Color(0.18f, 0.30f, 0.10f),
                new Color(0.28f, 0.50f, 0.18f));
            grassRend.sprite = Sprite.Create(grassTex, new Rect(0, 0, grassTex.width, grassTex.height),
                new Vector2(0.5f, 1f), 4f);
            grassRend.sortingOrder = -15;
            _groundObj.transform.position = new Vector3(0f, _groundY, 5f);
            _groundObj.transform.localScale = new Vector3(120f, 2f, 1f);

            var lineObj = new GameObject("GroundLine");
            lineObj.transform.SetParent(transform);
            var lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.material = _spriteMat;
            lr.startColor = new Color(0.35f, 0.55f, 0.25f);
            lr.endColor = new Color(0.35f, 0.55f, 0.25f);
            lr.widthMultiplier = 0.06f;
            lr.sortingOrder = -16;
            lr.SetPosition(0, new Vector3(-60f, _groundY, 0f));
            lr.SetPosition(1, new Vector3(60f, _groundY, 0f));
        }

        private void BuildTarget()
        {
            _targetObj = new GameObject("Target");
            _targetObj.transform.SetParent(transform);

            _targetRings = new SpriteRenderer[4];
            Color[] ringColors = {
                new Color(1f, 0.2f, 0.2f, 0.95f),
                new Color(1f, 1f, 1f, 0.95f),
                new Color(1f, 0.2f, 0.2f, 0.95f),
                new Color(1f, 1f, 1f, 0.95f)
            };
            float[] ringSizes = { 1.6f, 1.2f, 0.8f, 0.4f };

            for (int i = 0; i < 4; i++)
            {
                var ringObj = new GameObject($"Ring_{i}");
                ringObj.transform.SetParent(_targetObj.transform);
                _targetRings[i] = ringObj.AddComponent<SpriteRenderer>();
                _targetRings[i].sprite = Sprite.Create(_circleTex,
                    new Rect(0, 0, _circleTex.width, _circleTex.height),
                    new Vector2(0.5f, 0.5f), 32f);
                _targetRings[i].color = ringColors[i];
                _targetRings[i].sortingOrder = -5 + i;
                ringObj.transform.localPosition = Vector3.zero;
                ringObj.transform.localScale = new Vector3(ringSizes[i], ringSizes[i] * 0.15f, 1f);
            }
        }

        private void BuildDiver()
        {
            _diverObj = new GameObject("Skydiver");
            _diverObj.transform.SetParent(transform);
            _diverObj.transform.localScale = Vector3.one * 1.8f;

            Color suitRed   = new Color(0.82f, 0.12f, 0.15f);
            Color suitDark  = new Color(0.12f, 0.12f, 0.14f);
            Color suitWhite = new Color(0.92f, 0.92f, 0.94f);
            Color glove     = new Color(0.08f, 0.08f, 0.10f);
            Color boot      = new Color(0.10f, 0.10f, 0.12f);

            _diverLines = new LineRenderer[5];
            string[] names = { "Torso", "LeftArm", "RightArm", "LeftLeg", "RightLeg" };
            Vector3[][] poses = {
                new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0.3f, 0f) },
                new[] { new Vector3(0f, 0.26f, 0f), new Vector3(-0.18f, 0.10f, 0f) },
                new[] { new Vector3(0f, 0.26f, 0f), new Vector3( 0.18f, 0.10f, 0f) },
                new[] { new Vector3(0f, 0f, 0f), new Vector3(-0.10f, -0.22f, 0f) },
                new[] { new Vector3(0f, 0f, 0f), new Vector3( 0.10f, -0.22f, 0f) }
            };
            Color[] startColors = { suitRed,   suitRed,   suitRed,   suitDark, suitDark };
            Color[] endColors   = { suitWhite, glove,     glove,     boot,     boot     };

            for (int i = 0; i < 5; i++)
            {
                var partObj = new GameObject(names[i]);
                partObj.transform.SetParent(_diverObj.transform);
                _diverLines[i] = partObj.AddComponent<LineRenderer>();
                _diverLines[i].useWorldSpace = false;
                _diverLines[i].positionCount = 2;
                _diverLines[i].material = _spriteMat;
                _diverLines[i].startColor = startColors[i];
                _diverLines[i].endColor = endColors[i];
                _diverLines[i].widthMultiplier = 0.05f;
                _diverLines[i].sortingOrder = 10;
                _diverLines[i].SetPositions(poses[i]);
            }

            var headObj = new GameObject("Head");
            headObj.transform.SetParent(_diverObj.transform);
            _diverHead = headObj.AddComponent<SpriteRenderer>();
            _diverHead.sprite = Sprite.Create(_circleTex,
                new Rect(0, 0, _circleTex.width, _circleTex.height),
                new Vector2(0.5f, 0.5f), 512f);
            _diverHead.color = new Color(0.92f, 0.78f, 0.65f);
            _diverHead.sortingOrder = 11;
            headObj.transform.localPosition = new Vector3(0f, 0.24f, 0f);
            headObj.transform.localScale = Vector3.one * 1.2f;

            var helmetObj = new GameObject("Helmet");
            helmetObj.transform.SetParent(_diverObj.transform);
            var helmetRend = helmetObj.AddComponent<SpriteRenderer>();
            helmetRend.sprite = Sprite.Create(_circleTex,
                new Rect(0, 0, _circleTex.width, _circleTex.height),
                new Vector2(0.5f, 0.5f), 512f);
            helmetRend.color = new Color(0.95f, 0.95f, 0.97f, 0.75f);
            helmetRend.sortingOrder = 12;
            helmetObj.transform.localPosition = new Vector3(0f, 0.26f, -0.01f);
            helmetObj.transform.localScale = new Vector3(1.3f, 0.8f, 1f);

            var visorObj = new GameObject("Visor");
            visorObj.transform.SetParent(_diverObj.transform);
            var visorRend = visorObj.AddComponent<SpriteRenderer>();
            visorRend.sprite = Sprite.Create(_circleTex,
                new Rect(0, 0, _circleTex.width, _circleTex.height),
                new Vector2(0.5f, 0.5f), 512f);
            visorRend.color = new Color(0.15f, 0.18f, 0.25f, 0.85f);
            visorRend.sortingOrder = 13;
            visorObj.transform.localPosition = new Vector3(0f, 0.25f, -0.02f);
            visorObj.transform.localScale = new Vector3(0.9f, 0.35f, 1f);

            BuildParachute();
            _diverObj.SetActive(false);
        }

        private void BuildParachute()
        {
            var canopyTex = GenerateCanopyTexture(128);
            var canopyObj = new GameObject("Canopy");
            canopyObj.transform.SetParent(_diverObj.transform);
            _canopyRenderer = canopyObj.AddComponent<SpriteRenderer>();
            _canopyRenderer.sprite = Sprite.Create(canopyTex,
                new Rect(0, 0, canopyTex.width, canopyTex.height),
                new Vector2(0.5f, 0f), 128f);
            _canopyRenderer.sortingOrder = 8;
            canopyObj.transform.localPosition = new Vector3(0f, 0.55f, 0f);
            canopyObj.transform.localScale = new Vector3(1.4f, 0.95f, 1f);

            _chuteStrings = new LineRenderer[4];

            float canopyWorldBot = canopyObj.transform.localPosition.y;
            float canopyHalfW = 0.48f * canopyObj.transform.localScale.x;

            Vector3[] bodyAnchors = {
                new Vector3(-0.08f, 0.24f, 0f),
                new Vector3(-0.03f, 0.24f, 0f),
                new Vector3( 0.03f, 0.24f, 0f),
                new Vector3( 0.08f, 0.24f, 0f)
            };
            float[] xTargets = { -canopyHalfW * 0.85f, -canopyHalfW * 0.28f, canopyHalfW * 0.28f, canopyHalfW * 0.85f };
            float[] yTargets = { canopyWorldBot + 0.12f, canopyWorldBot + 0.14f, canopyWorldBot + 0.14f, canopyWorldBot + 0.12f };

            Vector3[] canopyAnchors = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                float dx = xTargets[i] - bodyAnchors[i].x;
                float dy = yTargets[i] - bodyAnchors[i].y;
                canopyAnchors[i] = new Vector3(bodyAnchors[i].x + dx * 1.75f, bodyAnchors[i].y + dy * 1.75f, 0f);
            }

            for (int i = 0; i < 4; i++)
            {
                var stringObj = new GameObject($"ChuteString_{i}");
                stringObj.transform.SetParent(_diverObj.transform);
                _chuteStrings[i] = stringObj.AddComponent<LineRenderer>();
                _chuteStrings[i].useWorldSpace = false;
                _chuteStrings[i].positionCount = 2;
                _chuteStrings[i].material = _spriteMat;
                _chuteStrings[i].startColor = new Color(0.55f, 0.50f, 0.40f);
                _chuteStrings[i].endColor = new Color(0.55f, 0.50f, 0.40f);
                _chuteStrings[i].widthMultiplier = 0.012f;
                _chuteStrings[i].sortingOrder = 9;
                _chuteStrings[i].SetPosition(0, canopyAnchors[i]);
                _chuteStrings[i].SetPosition(1, bodyAnchors[i]);

                Debug.Log($"[Skydive] String {i}: top=({canopyAnchors[i].x:F2},{canopyAnchors[i].y:F2}) " +
                    $"bot=({bodyAnchors[i].x:F2},{bodyAnchors[i].y:F2}) canopyBot={canopyWorldBot:F2}");
            }
        }

        private static Texture2D GenerateCanopyTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float cx = size * 0.5f;
            float radius = size * 0.48f;

            Color panelA = new Color(0.95f, 0.35f, 0.2f);
            Color panelB = new Color(1f, 0.98f, 0.95f);
            int panelCount = 8;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - cx;
                    float dy = y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist > radius || y < 0)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    float angle = Mathf.Atan2(dx, dy);
                    float panelPhase = (angle / Mathf.PI + 1f) * 0.5f * panelCount;
                    int panelIdx = (int)panelPhase;
                    Color baseCol = (panelIdx % 2 == 0) ? panelA : panelB;

                    float shade = 0.85f + 0.15f * (1f - dist / radius);
                    Color final = baseCol * shade;
                    final.a = 1f;
                    tex.SetPixel(x, y, final);
                }

            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        private void BuildWindStreaks()
        {
            int count = 16;
            _windStreaks = new LineRenderer[count];
            _streakSpeedJitter = new float[count];
            _streakPhaseOffset = new float[count];
            _streakYJitter = new float[count];

            var streaksParent = new GameObject("WindStreaks");
            streaksParent.transform.SetParent(transform);

            for (int i = 0; i < count; i++)
            {
                _streakSpeedJitter[i] = Random.Range(0.6f, 1.4f);
                _streakPhaseOffset[i] = Random.Range(0f, 14f);
                _streakYJitter[i] = Random.Range(-0.3f, 0.3f);

                var obj = new GameObject($"Streak_{i}");
                obj.transform.SetParent(streaksParent.transform);
                _windStreaks[i] = obj.AddComponent<LineRenderer>();
                _windStreaks[i].useWorldSpace = true;
                _windStreaks[i].positionCount = 2;
                _windStreaks[i].material = _spriteMat;
                _windStreaks[i].startWidth = 0.02f;
                _windStreaks[i].endWidth = 0.12f;
                _windStreaks[i].sortingOrder = -5;
                _windStreaks[i].SetPosition(0, Vector3.zero);
                _windStreaks[i].SetPosition(1, Vector3.zero);
            }
        }

        private void BuildWindArrow()
        {
            _windArrowObj = new GameObject("WindArrow");
            _windArrowObj.transform.SetParent(transform);

            var arrowTex = GenerateArrowTexture(128, 48);
            _windArrowRenderer = _windArrowObj.AddComponent<SpriteRenderer>();
            _windArrowRenderer.sprite = Sprite.Create(arrowTex,
                new Rect(0, 0, arrowTex.width, arrowTex.height),
                new Vector2(0.5f, 0.5f), 16f);
            _windArrowRenderer.sortingOrder = 5;
            _windArrowRenderer.color = new Color(1f, 1f, 1f, 0f);
            _windArrowObj.transform.position = new Vector3(0f, _groundY + 3f, -1f);
            _windArrowObj.transform.localScale = new Vector3(1.2f, 0.8f, 1f);
        }

        private void BuildBreathTrail()
        {
            var packTex = GenerateJetpackTexture(20, 26);
            var packObj = new GameObject("ParachutePack");
            packObj.transform.SetParent(_diverObj.transform);
            _jetpackRenderer = packObj.AddComponent<SpriteRenderer>();
            _jetpackRenderer.sprite = Sprite.Create(packTex,
                new Rect(0, 0, packTex.width, packTex.height),
                new Vector2(0.5f, 0.5f), 64f);
            _jetpackRenderer.sortingOrder = 9;
            packObj.transform.localPosition = new Vector3(0f, 0.12f, 0.01f);
            packObj.transform.localScale = new Vector3(0.85f, 0.8f, 1f);

            _flameLines = new LineRenderer[2];
            for (int i = 0; i < 2; i++)
            {
                var obj = new GameObject(i == 0 ? "ThrusterL" : "ThrusterR");
                obj.transform.SetParent(_diverObj.transform);
                var lr = obj.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 5;
                lr.material = _spriteMat;
                lr.startColor = Color.clear;
                lr.endColor = Color.clear;
                lr.widthMultiplier = 0.03f;
                lr.sortingOrder = 8;
                lr.numCapVertices = 2;

                var wc = new AnimationCurve(
                    new Keyframe(0f, 0.5f),
                    new Keyframe(0.2f, 1f),
                    new Keyframe(0.5f, 0.65f),
                    new Keyframe(0.8f, 0.2f),
                    new Keyframe(1f, 0f));
                lr.widthCurve = wc;

                for (int p = 0; p < 5; p++)
                    lr.SetPosition(p, Vector3.zero);

                _flameLines[i] = lr;
            }
        }

        private void UpdateBreathTrail()
        {
            if (_flameLines == null) return;

            float intensity = _smoothBreathPower;
            float time = Time.time;
            float pushDir = -Mathf.Sign(_currentWindForce);

            float nozzleY = 0.02f;
            float nozzleXL = -0.07f;
            float nozzleXR =  0.07f;

            for (int i = 0; i < 2; i++)
            {
                var lr = _flameLines[i];
                float side = (i == 0) ? -1f : 1f;
                float nx = (i == 0) ? nozzleXL : nozzleXR;

                bool active = (side * pushDir < 0f);
                float power = active ? intensity : 0f;

                float flameLen = 0.14f + power * 0.50f;
                float alpha = Mathf.Clamp01(power * 2.5f);
                float flicker = 0.85f + 0.15f * Mathf.Sin(time * 22f + i * 5f);
                alpha *= flicker;

                Color coreCol = Color.Lerp(
                    new Color(1f, 0.55f, 0.08f, alpha),
                    new Color(1f, 0.92f, 0.3f, alpha),
                    power * 0.5f);
                Color tipCol = new Color(1f, 0.2f, 0.05f, alpha * 0.15f);

                lr.startColor = coreCol;
                lr.endColor = tipCol;
                lr.widthMultiplier = (0.04f + power * 0.05f) * flicker;

                for (int p = 0; p < 5; p++)
                {
                    float t = p / 4f;
                    float py = nozzleY - flameLen * t * 0.75f;
                    float px = nx + side * flameLen * t * 0.5f;
                    float wobX = Mathf.Sin(time * (24f + i * 9f) + t * 6f) * 0.015f * t * power;
                    float wobY = Mathf.Cos(time * 18f + i * 6f + t * 4f) * 0.008f * t;
                    lr.SetPosition(p, new Vector3(px + wobX, py + wobY, 0f));
                }
            }
        }

        private static Texture2D GenerateJetpackTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            Color32 clear32 = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear32;

            Color32 packBody = new Color32(25, 25, 28, 255);
            Color32 packEdge = new Color32(40, 42, 48, 255);
            Color32 strap    = new Color32(18, 18, 20, 255);
            Color32 strapBkl = new Color32(80, 75, 60, 255);
            Color32 accent   = new Color32(180, 25, 30, 255);
            Color32 nozzle   = new Color32(50, 50, 55, 255);
            Color32 nozDark  = new Color32(30, 30, 35, 255);

            int cx = w / 2;
            int pL = cx - 5;
            int pR = cx + 4;
            int pBot = 4;
            int pTop = h - 2;

            for (int y = pBot; y <= pTop; y++)
                for (int x = pL; x <= pR; x++)
                {
                    if (x < 0 || x >= w || y < 0 || y >= h) continue;
                    bool edge = (x == pL || x == pR || y == pBot || y == pTop);
                    pixels[y * w + x] = edge ? packEdge : packBody;
                }

            for (int x = pL + 1; x <= pR - 1; x++)
                for (int y = pTop - 2; y <= pTop - 1; y++)
                    if (x >= 0 && x < w && y >= 0 && y < h)
                        pixels[y * w + x] = accent;

            for (int y = h * 2 / 5; y < h * 2 / 5 + 2; y++)
                for (int x = pL - 2; x <= pR + 2; x++)
                    if (x >= 0 && x < w && y >= 0 && y < h) pixels[y * w + x] = strap;
            for (int y = h * 3 / 5; y < h * 3 / 5 + 2; y++)
                for (int x = pL - 2; x <= pR + 2; x++)
                    if (x >= 0 && x < w && y >= 0 && y < h) pixels[y * w + x] = strap;

            int bklY1 = h * 2 / 5;
            int bklY2 = h * 3 / 5;
            if (pL - 2 >= 0 && pL - 2 < w && bklY1 >= 0 && bklY1 < h)
                pixels[bklY1 * w + (pL - 2)] = strapBkl;
            if (pR + 2 >= 0 && pR + 2 < w && bklY1 >= 0 && bklY1 < h)
                pixels[bklY1 * w + (pR + 2)] = strapBkl;
            if (pL - 2 >= 0 && pL - 2 < w && bklY2 >= 0 && bklY2 < h)
                pixels[bklY2 * w + (pL - 2)] = strapBkl;
            if (pR + 2 >= 0 && pR + 2 < w && bklY2 >= 0 && bklY2 < h)
                pixels[bklY2 * w + (pR + 2)] = strapBkl;

            int nozSize = 2;
            int nozLx = cx - 3;
            int nozRx = cx + 2;
            for (int dy = 0; dy < nozSize + 1; dy++)
                for (int dx = -1; dx <= nozSize; dx++)
                {
                    int y = pBot - 1 - dy;
                    if (y < 0 || y >= h) continue;
                    int lx = nozLx + dx;
                    int rx = nozRx + dx;
                    bool dark = dy == nozSize;
                    if (lx >= 0 && lx < w) pixels[y * w + lx] = dark ? nozDark : nozzle;
                    if (rx >= 0 && rx < w) pixels[y * w + rx] = dark ? nozDark : nozzle;
                }

            tex.SetPixels32(pixels);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        private void BuildSkyClouds()
        {
            int count = 5;
            _skyClouds = new SpriteRenderer[count];
            _cloudSpeedX = new float[count];
            _cloudBaseX = new float[count];

            var cloudTex = GenerateSkyCloudTexture(64);
            var parent = new GameObject("SkyClouds");
            parent.transform.SetParent(transform);

            float camTop = Camera.main != null ? Camera.main.orthographicSize : 5f;
            float cloudMinY = camTop * 0.80f;
            float cloudMaxY = camTop * 0.95f;

            float spacing = (_xMax - _xMin) * 2.4f / Mathf.Max(1, count);

            for (int i = 0; i < count; i++)
            {
                var obj = new GameObject($"SkyCloud_{i}");
                obj.transform.SetParent(parent.transform);

                var sr = obj.AddComponent<SpriteRenderer>();
                sr.sprite = Sprite.Create(cloudTex,
                    new Rect(0, 0, cloudTex.width, cloudTex.height),
                    new Vector2(0.5f, 0.5f), 16f);
                sr.sortingOrder = 20;
                float gray = Random.Range(0.92f, 1f);
                sr.color = new Color(gray, gray, gray, Random.Range(0.35f, 0.55f));

                float y = Mathf.Lerp(cloudMinY, cloudMaxY, Random.value);
                float scaleX = Random.Range(1.4f, 2.6f);
                float scaleY = Random.Range(0.4f, 0.7f);
                obj.transform.localScale = new Vector3(scaleX, scaleY, 1f);

                _cloudBaseX[i] = _xMin * 1.2f + spacing * i + Random.Range(-1f, 1f);
                _cloudSpeedX[i] = Random.Range(0.5f, 0.9f) * (Random.value > 0.5f ? 1f : -1f);
                obj.transform.position = new Vector3(_cloudBaseX[i], y, -2f);

                _skyClouds[i] = sr;
            }
        }

        private void UpdateSkyClouds()
        {
            if (_skyClouds == null) return;

            float wrapMargin = _xMax + 6f;

            for (int i = 0; i < _skyClouds.Length; i++)
            {
                var t = _skyClouds[i].transform;
                float x = t.position.x + _cloudSpeedX[i] * Time.deltaTime;

                if (x > wrapMargin) x = -wrapMargin;
                else if (x < -wrapMargin) x = wrapMargin;

                t.position = new Vector3(x, t.position.y, t.position.z);
            }
        }

        private void UpdateParallaxScroll()
        {
            float dt = Time.deltaTime;
            float scrollDelta;

            if (_boostActive)
            {
                _boostTimer -= dt;
                float boostT = 1f - Mathf.Clamp01(_boostTimer / BOOST_DURATION);
                float smoothT = Mathf.SmoothStep(0f, 1f, boostT);
                float targetOffset = Mathf.Lerp(_boostScrollStart, _boostScrollEnd, smoothT);
                scrollDelta = targetOffset - _scrollOffset;
                _scrollOffset = targetOffset;

                if (_boostTimer <= 0f)
                {
                    _scrollOffset = _boostScrollEnd;
                    _boostActive = false;
                    _boostStage = BoostStage.None;
                    _targetX = _pendingTargetX;
                    UpdateTargetPosition();
                }
            }
            else
            {
                scrollDelta = SCROLL_SPEED * dt;
                _scrollOffset += scrollDelta;
            }

            // Drift landed diver + feedback during exit boost
            if (_boostActive && _boostStage == BoostStage.Exit && _state == DiverState.Landed)
            {
                _diverPos.x -= scrollDelta * TARGET_RATE;
                UpdateDiverVisual();
                if (_landingFeedbackObj != null && _landingFeedbackObj.activeSelf)
                {
                    float landY = _groundY - 1.6f;
                    _landingFeedbackObj.transform.position =
                        new Vector3(_diverPos.x, landY + 0.6f, -2f);
                }
            }

            // Mid-boost teleport: old target exited left, teleport to off-screen right for entry
            if (_boostActive && _boostStage == BoostStage.Exit && _targetX < _xMin - 4f)
            {
                _targetX = _xMax + 5f;
                UpdateTargetPosition();
                _boostStage = BoostStage.Entry;
                _diverObj.SetActive(false);
                _landingFeedbackObj.SetActive(false);
            }

            // Target continuous scroll (always active — boost and normal)
            _targetX -= scrollDelta * TARGET_RATE;
            UpdateTargetPosition();

            // City scroll (slowest layer) with wrapping
            if (_cityTransform != null)
            {
                float cx = _cityOrigX - _scrollOffset * CITY_RATE;
                float halfCity = _cityWorldWidth * 0.5f;
                if (cx < -halfCity - 5f)
                    _cityOrigX += _cityWorldWidth;
                else if (cx > halfCity + 5f)
                    _cityOrigX -= _cityWorldWidth;
                cx = _cityOrigX - _scrollOffset * CITY_RATE;
                var cp = _cityTransform.position;
                _cityTransform.position = new Vector3(cx, cp.y, cp.z);
            }

            // Tree scroll (mid-ground layer) with wrapping
            if (_parallaxTrees != null)
            {
                float leftEdge = _xMin - 8f;
                float rightEdge = _xMax + 8f;

                for (int i = 0; i < _parallaxTrees.Length; i++)
                {
                    float x = _parallaxTrees[i].origX - _scrollOffset * _parallaxTrees[i].rate;

                    if (x < leftEdge)
                    {
                        _parallaxTrees[i].origX += _treeWrapWidth;
                        x += _treeWrapWidth;
                    }
                    else if (x > rightEdge)
                    {
                        _parallaxTrees[i].origX -= _treeWrapWidth;
                        x -= _treeWrapWidth;
                    }

                    var pos = _parallaxTrees[i].tf.position;
                    _parallaxTrees[i].tf.position = new Vector3(x, pos.y, pos.z);
                }
            }
        }

        public void StartTransition(float newTargetX)
        {
            _pendingTargetX = newTargetX;

            float exitDist = _targetX - (_xMin - 5f);
            if (exitDist < 5f) exitDist = 5f;
            float entryDist = (_xMax + 5f) - newTargetX;

            float totalOffset = (exitDist + entryDist) / TARGET_RATE;

            _boostScrollStart = _scrollOffset;
            _boostScrollEnd = _scrollOffset + totalOffset;
            _boostActive = true;
            _boostTimer = BOOST_DURATION;
            _boostStage = BoostStage.Exit;
        }

        public bool IsTransitionComplete => !_boostActive;

        /// <summary>Hides gameplay elements (diver, target, wind, trails) and keeps only the ambient world visible.</summary>
        public void EnterAmbientMode()
        {
            if (_targetObj != null) _targetObj.SetActive(false);
            if (_diverObj != null) _diverObj.SetActive(false);
            if (_landingFeedbackObj != null) _landingFeedbackObj.SetActive(false);
            if (_windArrowObj != null) _windArrowObj.SetActive(false);
            if (_windStreaks != null)
                foreach (var lr in _windStreaks) lr.enabled = false;
            if (_flameLines != null)
                foreach (var lr in _flameLines) lr.enabled = false;
        }

        /// <summary>Ticks only ambient visuals (parallax scroll + clouds). Safe to call indefinitely.</summary>
        public void UpdateAmbient()
        {
            UpdateSkyClouds();
            UpdateParallaxScroll();
        }

        private static Texture2D GenerateSkyCloudTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f;

            Vector2[] blobs = {
                new Vector2(c, c),
                new Vector2(c * 0.6f, c * 0.9f),
                new Vector2(c * 1.4f, c * 0.85f),
                new Vector2(c * 0.85f, c * 1.2f),
                new Vector2(c * 1.15f, c * 1.1f),
            };
            float[] radii = { c * 0.6f, c * 0.42f, c * 0.4f, c * 0.35f, c * 0.38f };

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float alpha = 0f;
                    for (int b = 0; b < blobs.Length; b++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), blobs[b]);
                        float edge = Mathf.Clamp01((radii[b] - dist) / (radii[b] * 0.35f));
                        edge *= edge;
                        alpha = Mathf.Max(alpha, edge);
                    }
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GenerateArrowTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float cx = w * 0.5f;
            float cy = h * 0.5f;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float shaftMinY = cy - h * 0.18f;
                    float shaftMaxY = cy + h * 0.18f;
                    bool inShaft = x < w * 0.7f && y >= shaftMinY && y <= shaftMaxY;

                    float headX = w * 0.55f;
                    float headHalf = h * 0.5f;
                    float t = (x - headX) / (w - headX);
                    bool inHead = x >= headX && t >= 0f && t <= 1f
                        && Mathf.Abs(y - cy) <= headHalf * (1f - t);

                    if (inShaft || inHead)
                        tex.SetPixel(x, y, Color.white);
                    else
                        tex.SetPixel(x, y, Color.clear);
                }

            tex.Apply(false);
            tex.filterMode = FilterMode.Point;
            return tex;
        }

        private void BuildLandingFeedback()
        {
            _landingFeedbackObj = new GameObject("LandingFeedback");
            _landingFeedbackObj.transform.SetParent(transform);
            _landingFeedbackRenderer = _landingFeedbackObj.AddComponent<SpriteRenderer>();
            var glowTex = GenerateGlowTexture(64);
            _landingFeedbackRenderer.sprite = Sprite.Create(glowTex,
                new Rect(0, 0, glowTex.width, glowTex.height),
                new Vector2(0.5f, 0.5f), 32f);
            _landingFeedbackRenderer.sortingOrder = 15;
            _landingFeedbackObj.transform.localScale = new Vector3(1.2f, 1.2f, 1f);
            _landingFeedbackObj.SetActive(false);
        }

        private static Texture2D GenerateGlowTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float cx = size * 0.5f;
            float r = size * 0.48f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cx) * (y - cx));
                float a = Mathf.Clamp01(1f - dist / r);
                a = a * a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply(false);
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static void EnsureMaterial()
        {
            if (_spriteMat != null) return;
            _spriteMat = new Material(Shader.Find("Sprites/Default"));
        }

        private static Texture2D GenerateCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            float radius = center - 1f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((radius - dist) * 2f)));
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
}
