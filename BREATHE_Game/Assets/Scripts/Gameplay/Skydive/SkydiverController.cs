using UnityEngine;

namespace Breathe.Gameplay
{
    // Manages a single skydiver's fall, wind forces, breath opposition, landing,
    // and all visual elements for the Skydive minigame scene.
    public class SkydiverController : MonoBehaviour
    {
        [Header("World Bounds")]
        [SerializeField] private float _groundY = -3.5f;
        [SerializeField] private float _spawnY = 5.5f;
        [SerializeField] private float _xMin = -7f;
        [SerializeField] private float _xMax = 7f;

        [Header("Fall")]
        [SerializeField] private float _fallSpeed = 1.8f;

        [Header("Wind")]
        [SerializeField] private float _windCyclePeriodMin = 2f;
        [SerializeField] private float _windCyclePeriodMax = 4f;
        [SerializeField] private float _windMaxForce = 3.5f;

        [Header("Breath Opposition")]
        [SerializeField] private float _breathForceMultiplier = 5f;

        [Header("Landing")]
        [SerializeField] private float _perfectRadius = 0.5f;
        [SerializeField] private float _goodRadius = 1.2f;
        [SerializeField] private float _nearRadius = 2.0f;

        public enum LandingQuality { Perfect, Good, Near, OffTarget }
        public enum DiverState { Idle, Falling, Landed }

        private DiverState _state = DiverState.Idle;
        private Vector2 _diverPos;
        private float _targetX;
        private float _windPhase;
        private float _windCyclePeriod;
        private float _currentWindForce;
        private float _displayWindForce;

        // Visual objects
        private GameObject _diverObj;
        private LineRenderer[] _diverLines;
        private SpriteRenderer _diverHead;
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
        public LandingQuality LastLandingQuality => _lastLandingQuality;
        public float LastLandingDistance => _lastLandingDistance;

        public void Initialize()
        {
            EnsureMaterial();
            _circleTex = GenerateCircleTexture(64);

            BuildBackground();
            BuildGround();
            BuildTarget();
            BuildDiver();
            BuildWindStreaks();
            BuildLandingFeedback();
        }

        public void SpawnDiver()
        {
            _targetX = Random.Range(_xMin * 0.7f, _xMax * 0.7f);
            float spawnX = Random.Range(_xMin * 0.6f, _xMax * 0.6f);

            // Ensure target and spawn aren't too close (forces the player to use breath)
            while (Mathf.Abs(spawnX - _targetX) < 2f)
                spawnX = Random.Range(_xMin * 0.6f, _xMax * 0.6f);

            _diverPos = new Vector2(spawnX, _spawnY);
            _state = DiverState.Falling;

            _windPhase = Random.Range(0f, Mathf.PI * 2f);
            _windCyclePeriod = Random.Range(_windCyclePeriodMin, _windCyclePeriodMax);

            _diverObj.SetActive(true);
            UpdateTargetPosition();
            _landingFeedbackObj.SetActive(false);
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

            // Wind: gradual sine-wave oscillation
            _windPhase += dt * (Mathf.PI * 2f / _windCyclePeriod);
            _currentWindForce = Mathf.Sin(_windPhase) * _windMaxForce;

            // Smooth display wind for telegraph (lags slightly behind actual)
            _displayWindForce = Mathf.Lerp(_displayWindForce, _currentWindForce, dt * 3f);

            // Breath opposes wind direction
            float breathForce = 0f;
            if (Mathf.Abs(_currentWindForce) > 0.1f)
            {
                float oppositionDir = -Mathf.Sign(_currentWindForce);
                breathForce = oppositionDir * breathPower * _breathForceMultiplier;
            }

            // Apply forces
            float totalXForce = _currentWindForce + breathForce;
            _diverPos.x += totalXForce * dt;
            _diverPos.x = Mathf.Clamp(_diverPos.x, _xMin, _xMax);
            _diverPos.y -= _fallSpeed * dt;

            // Update visuals
            UpdateDiverVisual();
            UpdateWindStreakVisuals();

            // Landing check
            if (_diverPos.y <= _groundY)
            {
                _diverPos.y = _groundY;
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

            // Keep animating wind streaks for continuity
            UpdateWindStreakVisuals();
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
            _landingFeedbackObj.transform.position = new Vector3(_diverPos.x, _groundY + 0.8f, -2f);
            _landingFeedbackTimer = 1.5f;

            Color feedbackColor = _lastLandingQuality switch
            {
                LandingQuality.Perfect => new Color(0.2f, 1f, 0.4f),
                LandingQuality.Good => new Color(0.5f, 0.9f, 0.3f),
                LandingQuality.Near => new Color(1f, 0.8f, 0.2f),
                _ => new Color(1f, 0.3f, 0.2f)
            };
            _landingFeedbackRenderer.color = feedbackColor;
        }

        private void UpdateDiverVisual()
        {
            _diverObj.transform.position = new Vector3(_diverPos.x, _diverPos.y, -1f);

            // Tilt based on net horizontal force (wind + breath)
            float tilt = Mathf.Clamp(_currentWindForce * 5f, -30f, 30f);
            _diverObj.transform.rotation = Quaternion.Euler(0f, 0f, -tilt);
        }

        private void UpdateWindStreakVisuals()
        {
            if (_windStreaks == null) return;

            float windDir = Mathf.Sign(_displayWindForce);
            float windStrength = Mathf.Abs(_displayWindForce) / _windMaxForce;

            for (int i = 0; i < _windStreaks.Length; i++)
            {
                float streakY = _groundY + 2f + i * 1.8f;
                float phase = Time.time * 3f + i * 1.5f;
                float startX = windDir > 0
                    ? _xMin + Mathf.Repeat(phase * windStrength * 2f, _xMax - _xMin)
                    : _xMax - Mathf.Repeat(phase * windStrength * 2f, _xMax - _xMin);
                float length = 1.5f + windStrength * 2.5f;

                float alpha = windStrength * 0.4f;
                Color streakColor = new Color(0.8f, 0.85f, 0.95f, alpha);
                _windStreaks[i].startColor = streakColor;
                _windStreaks[i].endColor = new Color(streakColor.r, streakColor.g, streakColor.b, 0f);

                _windStreaks[i].SetPosition(0, new Vector3(startX, streakY, 1f));
                _windStreaks[i].SetPosition(1, new Vector3(startX + windDir * length, streakY, 1f));
            }
        }

        private void UpdateTargetPosition()
        {
            if (_targetObj == null) return;
            _targetObj.transform.position = new Vector3(_targetX, _groundY + 0.01f, 0.5f);
        }

        private void BuildBackground()
        {
            var cam = Camera.main;
            if (cam != null)
                cam.backgroundColor = new Color(0.45f, 0.68f, 0.92f);

            _skyObj = new GameObject("Sky");
            _skyObj.transform.SetParent(transform);
            var skyRend = _skyObj.AddComponent<SpriteRenderer>();
            var skyTex = GenerateGradientTexture(4, 64,
                new Color(0.55f, 0.75f, 0.90f),
                new Color(0.35f, 0.55f, 0.85f));
            skyRend.sprite = Sprite.Create(skyTex, new Rect(0, 0, skyTex.width, skyTex.height),
                new Vector2(0.5f, 0.5f), 8f);
            skyRend.sortingOrder = -30;
            _skyObj.transform.position = new Vector3(0f, 2f, 10f);
            _skyObj.transform.localScale = new Vector3(30f, 18f, 1f);
        }

        private void BuildGround()
        {
            _groundObj = new GameObject("Ground");
            _groundObj.transform.SetParent(transform);

            // Grass
            var grassRend = _groundObj.AddComponent<SpriteRenderer>();
            var grassTex = GenerateGradientTexture(4, 16,
                new Color(0.30f, 0.45f, 0.20f),
                new Color(0.40f, 0.65f, 0.30f));
            grassRend.sprite = Sprite.Create(grassTex, new Rect(0, 0, grassTex.width, grassTex.height),
                new Vector2(0.5f, 1f), 4f);
            grassRend.sortingOrder = -15;
            _groundObj.transform.position = new Vector3(0f, _groundY, 5f);
            _groundObj.transform.localScale = new Vector3(25f, 4f, 1f);

            // Ground line
            var lineObj = new GameObject("GroundLine");
            lineObj.transform.SetParent(transform);
            var lr = lineObj.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.material = _spriteMat;
            lr.startColor = new Color(0.35f, 0.55f, 0.25f);
            lr.endColor = new Color(0.35f, 0.55f, 0.25f);
            lr.widthMultiplier = 0.06f;
            lr.sortingOrder = -10;
            lr.SetPosition(0, new Vector3(_xMin - 2f, _groundY, 0f));
            lr.SetPosition(1, new Vector3(_xMax + 2f, _groundY, 0f));
        }

        private void BuildTarget()
        {
            _targetObj = new GameObject("Target");
            _targetObj.transform.SetParent(transform);

            // Concentric rings for bullseye
            _targetRings = new SpriteRenderer[4];
            Color[] ringColors = {
                new Color(1f, 0.2f, 0.2f, 0.9f),
                new Color(1f, 1f, 1f, 0.9f),
                new Color(1f, 0.2f, 0.2f, 0.9f),
                new Color(1f, 1f, 1f, 0.9f)
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
                _targetRings[i].sortingOrder = -9 + i;
                ringObj.transform.localPosition = Vector3.zero;
                ringObj.transform.localScale = new Vector3(ringSizes[i], ringSizes[i] * 0.3f, 1f);
            }
        }

        private void BuildDiver()
        {
            _diverObj = new GameObject("Skydiver");
            _diverObj.transform.SetParent(transform);

            // Stick figure
            _diverLines = new LineRenderer[5];
            string[] names = { "Torso", "LeftArm", "RightArm", "LeftLeg", "RightLeg" };
            Vector3[][] poses = {
                new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0.8f, 0f) },
                new[] { new Vector3(0f, 0.7f, 0f), new Vector3(-0.5f, 0.4f, 0f) },
                new[] { new Vector3(0f, 0.7f, 0f), new Vector3(0.5f, 0.4f, 0f) },
                new[] { new Vector3(0f, 0f, 0f), new Vector3(-0.3f, -0.5f, 0f) },
                new[] { new Vector3(0f, 0f, 0f), new Vector3(0.3f, -0.5f, 0f) }
            };

            for (int i = 0; i < 5; i++)
            {
                var partObj = new GameObject(names[i]);
                partObj.transform.SetParent(_diverObj.transform);
                _diverLines[i] = partObj.AddComponent<LineRenderer>();
                _diverLines[i].useWorldSpace = false;
                _diverLines[i].positionCount = 2;
                _diverLines[i].material = _spriteMat;
                _diverLines[i].startColor = new Color(0.9f, 0.3f, 0.1f);
                _diverLines[i].endColor = new Color(0.9f, 0.3f, 0.1f);
                _diverLines[i].widthMultiplier = 0.1f;
                _diverLines[i].sortingOrder = 10;
                _diverLines[i].SetPositions(poses[i]);
            }

            // Head
            var headObj = new GameObject("Head");
            headObj.transform.SetParent(_diverObj.transform);
            _diverHead = headObj.AddComponent<SpriteRenderer>();
            _diverHead.sprite = Sprite.Create(_circleTex,
                new Rect(0, 0, _circleTex.width, _circleTex.height),
                new Vector2(0.5f, 0.5f), 64f);
            _diverHead.color = new Color(0.92f, 0.78f, 0.65f);
            _diverHead.sortingOrder = 11;
            headObj.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            headObj.transform.localScale = Vector3.one * 0.5f;

            // Helmet/goggles
            var helmetObj = new GameObject("Helmet");
            helmetObj.transform.SetParent(_diverObj.transform);
            var helmetRend = helmetObj.AddComponent<SpriteRenderer>();
            helmetRend.sprite = Sprite.Create(_circleTex,
                new Rect(0, 0, _circleTex.width, _circleTex.height),
                new Vector2(0.5f, 0.5f), 64f);
            helmetRend.color = new Color(0.2f, 0.2f, 0.3f, 0.6f);
            helmetRend.sortingOrder = 12;
            helmetObj.transform.localPosition = new Vector3(0f, 1.15f, -0.01f);
            helmetObj.transform.localScale = new Vector3(0.55f, 0.35f, 1f);

            _diverObj.SetActive(false);
        }

        private void BuildWindStreaks()
        {
            _windStreaks = new LineRenderer[5];
            var streaksParent = new GameObject("WindStreaks");
            streaksParent.transform.SetParent(transform);

            for (int i = 0; i < _windStreaks.Length; i++)
            {
                var obj = new GameObject($"Streak_{i}");
                obj.transform.SetParent(streaksParent.transform);
                _windStreaks[i] = obj.AddComponent<LineRenderer>();
                _windStreaks[i].useWorldSpace = true;
                _windStreaks[i].positionCount = 2;
                _windStreaks[i].material = _spriteMat;
                _windStreaks[i].startWidth = 0.06f;
                _windStreaks[i].endWidth = 0.01f;
                _windStreaks[i].sortingOrder = -5;
                _windStreaks[i].SetPosition(0, Vector3.zero);
                _windStreaks[i].SetPosition(1, Vector3.zero);
            }
        }

        private void BuildLandingFeedback()
        {
            _landingFeedbackObj = new GameObject("LandingFeedback");
            _landingFeedbackObj.transform.SetParent(transform);
            _landingFeedbackRenderer = _landingFeedbackObj.AddComponent<SpriteRenderer>();
            _landingFeedbackRenderer.sprite = Sprite.Create(_circleTex,
                new Rect(0, 0, _circleTex.width, _circleTex.height),
                new Vector2(0.5f, 0.5f), 32f);
            _landingFeedbackRenderer.sortingOrder = 15;
            _landingFeedbackObj.transform.localScale = new Vector3(1.5f, 1.5f, 1f);
            _landingFeedbackObj.SetActive(false);
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
