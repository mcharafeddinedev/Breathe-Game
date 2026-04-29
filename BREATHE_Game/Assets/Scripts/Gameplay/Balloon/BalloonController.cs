using UnityEngine;
using Breathe.Audio;

namespace Breathe.Gameplay
{
    // Handles the visual balloon object: inflation scaling, color, nozzle,
    // tie-off animation, and cycling to the next balloon.
    // Driven by BalloonMinigame which controls session timing and scoring.
    public class BalloonController : MonoBehaviour
    {
        [Header("Procedural Audio")]
        [SerializeField, Tooltip("If true, spawns a ProceduralBalloonAudio component for real-time inflation SFX.")]
        bool _enableProceduralAudio = true;

        ProceduralBalloonAudio _proceduralAudio;
        [Header("Inflation")]
        [SerializeField] private float _inflationSpeed = 0.6f;
        [SerializeField] private float _maxScreenFill = 0.68f;

        [Header("Grab Animation")]
        [SerializeField] private float _grabDuration = 0.65f;
        [SerializeField] private float _squishAmount = 0.18f;
        [SerializeField] private float _departSlideSpeed = 7.5f;
        [SerializeField] private float _departLifetime = 2.0f;

        [Header("Visuals")]
        [SerializeField] private Color[] _balloonColors = {
            new Color(0.92f, 0.18f, 0.18f),
            new Color(0.16f, 0.52f, 0.92f),
            new Color(0.92f, 0.72f, 0.08f),
            new Color(0.22f, 0.80f, 0.35f),
            new Color(0.72f, 0.25f, 0.85f),
            new Color(0.92f, 0.45f, 0.15f)
        };

        private const float PPU = 256f;
        private const float HEIGHT_RATIO = 1.2f;

        private enum Phase { Inflating, Grabbing, Frozen }

        private Phase _phase = Phase.Inflating;
        private float _inflationProgress;
        private float _grabTimer;
        private int _colorIndex;
        private bool _active;

        private float _spriteWorldSize;
        private float _minScale;
        private float _maxScale;
        private float _nozzleY;
        private float _grabDirection;
        private float _popInTimer;
        private const float POP_IN_DURATION = 0.25f;

        // Active balloon being inflated
        private GameObject _balloonBody;
        private SpriteRenderer _balloonRenderer;
        private LineRenderer _nozzleLine;
        private LineRenderer _stringLine;
        private LineRenderer _knotLine;

        // Grab hand (shared between grab phase and departing animation)
        private GameObject _handObj;
        private SpriteRenderer _handRenderer;

        // Departing balloon — slides away while the new one is already inflatable
        private GameObject _departObj;
        private SpriteRenderer _departRenderer;
        private bool _departActive;
        private float _departTimer;
        private float _departDir;
        private Vector3 _departStartPos;
        private Vector3 _departStartScale;
        private LineRenderer _departStringLine;

        private Camera _cam;
        private static Material _spriteMat;
        private static Sprite _whiteSprite;
        private Texture2D _balloonTexture;
        private Texture2D _deflatedTexture;
        private Texture2D _handTexture;
        private Texture2D _fistTexture;
        private Sprite _balloonSprite;
        private Sprite _deflatedSprite;
        private Sprite _handSprite;
        private Sprite _fistSprite;
        private LineRenderer _grabHandString;
        private float _deflatedTilt;
        private float _deflatedFlip;
        private const float DEFLATED_THRESHOLD = 0.4f;

        // Background animation state
        private struct BgBalloonData
        {
            public Transform root;
            public LineRenderer stringLine;
            public float speed;
            public float swayPhase;
            public float stringLen;
        }

        private struct TiedBalloonRef
        {
            public Transform root;
            public LineRenderer stringLine;
            public Vector3 basePos;
            public float weightX;
            public float weightY;
            public float phase;
        }

        private readonly System.Collections.Generic.List<BgBalloonData> _bgBalloons
            = new System.Collections.Generic.List<BgBalloonData>();
        private readonly System.Collections.Generic.List<TiedBalloonRef> _tiedBalloons
            = new System.Collections.Generic.List<TiedBalloonRef>();
        private readonly System.Collections.Generic.List<Transform> _flameTransforms
            = new System.Collections.Generic.List<Transform>();
        private readonly System.Collections.Generic.List<Vector3> _flameBasePositions
            = new System.Collections.Generic.List<Vector3>();
        private struct ConfettiData
        {
            public Transform root;
            public float fallSpeed;
            public float swayPhase;
            public float tumbleSpeed;
            public Color color;
        }

        private readonly System.Collections.Generic.List<ConfettiData> _confetti
            = new System.Collections.Generic.List<ConfettiData>();
        private float _groundTopY;
        private float _grassHeight;
        private Color[] _confettiColors;
        private readonly System.Collections.Generic.List<Transform> _bannerParts
            = new System.Collections.Generic.List<Transform>();
        private readonly System.Collections.Generic.List<Vector3> _bannerBasePositions
            = new System.Collections.Generic.List<Vector3>();
        private float _bgCamH;
        private float _bgCamW;

        // Snapshot of the balloon state at grab time (for squish n grab animation)
        private Vector3 _grabBalloonPos;
        private Vector3 _grabBalloonScale;

        public float InflationProgress => _inflationProgress;
        public bool IsFullyInflated => _inflationProgress >= 1f;
        public bool IsBusy => _phase != Phase.Inflating;

        public void Initialize()
        {
            _cam = Camera.main;
            EnsureMaterial();
            _balloonTexture = GenerateBalloonTexture(512);
            _deflatedTexture = GenerateDeflatedBalloonTexture(128, 160);
            _handTexture = GenerateHandTexture(256, 320);
            _fistTexture = GenerateFistTexture(256);

            _spriteWorldSize = _balloonTexture.width / PPU;

            float camH = _cam != null ? _cam.orthographicSize * 2f : 10f;
            _maxScale = (camH * _maxScreenFill) / (_spriteWorldSize * HEIGHT_RATIO);
            _minScale = _maxScale * 0.06f;
            _nozzleY = -camH * 0.32f;

            BuildBackground();
            BuildNozzle();
            BuildBalloon();
            BuildDepartingBalloon();
            BuildString();
            BuildHand();
            InitializeProceduralAudio();

            _colorIndex = 0;
            ApplyBalloonColor();
            ResetInflation();
        }

        void InitializeProceduralAudio()
        {
            if (!_enableProceduralAudio) return;

            _proceduralAudio = GetComponent<ProceduralBalloonAudio>();
            if (_proceduralAudio == null)
                _proceduralAudio = gameObject.AddComponent<ProceduralBalloonAudio>();
        }

        public void Activate() => _active = true;

        public void Freeze()
        {
            _active = false;
            _phase = Phase.Frozen;

            // Stop procedural audio
            if (_proceduralAudio != null)
                _proceduralAudio.UpdateAudio(0f, _inflationProgress, false);
        }

        private void Update()
        {
            AnimateBackgroundBalloons();
            AnimateConfetti();
            AnimateFlames();
            AnimateWindSway();
        }

        private void AnimateBackgroundBalloons()
        {
            if (_bgBalloons.Count == 0) return;
            float halfH = _bgCamH * 0.5f;
            float halfW = _bgCamW * 0.5f;
            float time = Time.time;

            for (int i = 0; i < _bgBalloons.Count; i++)
            {
                var b = _bgBalloons[i];
                if (b.root == null) continue;

                Vector3 pos = b.root.position;
                pos.y += b.speed * Time.deltaTime;
                pos.x += Mathf.Cos(time * 0.8f + b.swayPhase) * b.speed * 0.15f * Time.deltaTime;

                if (pos.y > halfH + 2f)
                {
                    pos.y = -halfH - Random.Range(1f, 3f);
                    pos.x = Random.Range(-halfW * 0.85f, halfW * 0.85f);
                }

                b.root.position = pos;

                if (b.stringLine != null)
                {
                    float bBottom = pos.y - b.root.localScale.y * (_balloonTexture.width / PPU) * 0.5f;
                    float midX = pos.x + Mathf.Sin(time * 1.7f + b.swayPhase) * 0.06f;
                    float endX = pos.x + Mathf.Sin(time * 1.2f + b.swayPhase) * 0.03f;
                    b.stringLine.SetPosition(0, new Vector3(pos.x, bBottom, pos.z));
                    b.stringLine.SetPosition(1, new Vector3(midX, bBottom - b.stringLen * 0.5f, pos.z));
                    b.stringLine.SetPosition(2, new Vector3(endX, bBottom - b.stringLen, pos.z));
                }
            }
        }

        private void AnimateConfetti()
        {
            if (_confetti.Count == 0) return;
            float halfH = _bgCamH * 0.5f;
            float halfW = _bgCamW * 0.5f;
            float time = Time.time;

            for (int i = 0; i < _confetti.Count; i++)
            {
                var c = _confetti[i];
                if (c.root == null) continue;

                Vector3 pos = c.root.position;
                pos.y -= c.fallSpeed * Time.deltaTime;
                pos.x += Mathf.Sin(time * 1.2f + c.swayPhase) * c.fallSpeed * 0.4f * Time.deltaTime;

                if (pos.y <= _groundTopY)
                {
                    // Land on the grass visibly — flatten and stick
                    float landY = _groundTopY - Random.Range(0f, _grassHeight * 0.4f);
                    c.root.position = new Vector3(pos.x, landY, 6.5f);
                    c.root.rotation = Quaternion.Euler(0f, 0f, Random.Range(-45f, 45f));
                    c.root.localScale = new Vector3(
                        Random.Range(0.05f, 0.11f), Random.Range(0.03f, 0.06f), 1f);
                    var landedRend = c.root.GetComponent<SpriteRenderer>();
                    if (landedRend != null)
                    {
                        Color lc = c.color;
                        lc.a = Random.Range(0.45f, 0.65f);
                        landedRend.color = lc;
                        landedRend.sortingOrder = -15;
                    }

                    // Spawn a replacement from the top
                    var newObj = new GameObject("FallingConfetti");
                    newObj.transform.SetParent(transform);
                    var newRend = newObj.AddComponent<SpriteRenderer>();
                    newRend.sprite = _whiteSprite;
                    Color nc = _confettiColors[Random.Range(0, _confettiColors.Length)];
                    nc.a = Random.Range(0.2f, 0.4f);
                    newRend.color = nc;
                    newRend.sortingOrder = -15;
                    float nx = Random.Range(-halfW * 0.85f, halfW * 0.85f);
                    float ny = halfH + Random.Range(0.5f, 3f);
                    newObj.transform.position = new Vector3(nx, ny, 6.8f);
                    newObj.transform.localScale = new Vector3(
                        Random.Range(0.04f, 0.09f), Random.Range(0.025f, 0.055f), 1f);

                    _confetti[i] = new ConfettiData
                    {
                        root = newObj.transform,
                        fallSpeed = Random.Range(0.3f, 0.9f),
                        swayPhase = Random.Range(0f, Mathf.PI * 2f),
                        tumbleSpeed = Random.Range(30f, 120f),
                        color = nc
                    };
                    continue;
                }

                c.root.position = pos;
                c.root.rotation = Quaternion.Euler(0f, 0f, time * c.tumbleSpeed + c.swayPhase * 57f);
            }
        }

        private void AnimateFlames()
        {
            float time = Time.time;
            for (int i = 0; i < _flameTransforms.Count; i++)
            {
                var ft = _flameTransforms[i];
                if (ft == null) continue;
                Vector3 bp = _flameBasePositions[i];
                float flicker = Mathf.Sin(time * 8f + i * 2.1f) * 0.02f;
                float sway = Mathf.Sin(time * 3f + i * 1.7f) * 0.015f;
                ft.position = new Vector3(bp.x + sway, bp.y + flicker, bp.z);
                float pulse = 1f + Mathf.Sin(time * 6f + i * 1.3f) * 0.15f;
                ft.localScale = new Vector3(0.35f * pulse, 0.50f * pulse, 1f);
            }
        }

        private void AnimateWindSway()
        {
            float time = Time.time;
            float windX = Mathf.Sin(time * 0.7f) * 0.12f + Mathf.Sin(time * 1.3f) * 0.06f;

            for (int i = 0; i < _tiedBalloons.Count; i++)
            {
                var tb = _tiedBalloons[i];
                if (tb.root == null) continue;
                float personalSway = Mathf.Sin(time * 1.1f + tb.phase) * 0.08f;
                float dx = windX + personalSway;
                float dy = Mathf.Sin(time * 1.5f + tb.phase * 0.7f) * 0.04f;
                tb.root.position = tb.basePos + new Vector3(dx, dy, 0f);

                if (tb.stringLine != null)
                {
                    float bBottom = tb.root.position.y
                        - tb.root.localScale.y * (_balloonTexture.width / PPU) * 0.5f;
                    float mx = (tb.root.position.x + tb.weightX) * 0.5f
                        + Mathf.Sin(time * 1.4f + tb.phase) * 0.04f;
                    float my = (bBottom + tb.weightY) * 0.5f;
                    tb.stringLine.SetPosition(0, new Vector3(tb.root.position.x, bBottom, tb.root.position.z));
                    tb.stringLine.SetPosition(1, new Vector3(mx, my, tb.root.position.z));
                    tb.stringLine.SetPosition(2, new Vector3(tb.weightX, tb.weightY + 0.06f, tb.root.position.z));
                }
            }

            // Banner stays fixed to its posts (no sway — it's attached)
        }

        public void Inflate(float breathPower)
        {
            if (!_active || _phase != Phase.Inflating) return;

            _inflationProgress += breathPower * _inflationSpeed * Time.deltaTime;
            _inflationProgress = Mathf.Clamp01(_inflationProgress);
            UpdateBalloonVisual();

            // Update procedural audio with current breath/inflation state
            if (_proceduralAudio != null)
                _proceduralAudio.UpdateAudio(breathPower, _inflationProgress, _active);

            if (_inflationProgress >= 1f)
                BeginGrab();
        }

        // Returns true the frame a balloon is completed and scored.
        public bool UpdateCycle()
        {
            if (!_active) return false;

            bool scored = false;

            // Always tick the departing balloon (runs in parallel)
            if (_departActive)
                UpdateDeparting();

            if (_phase == Phase.Grabbing)
            {
                _grabTimer += Time.deltaTime;
                float t = Mathf.Clamp01(_grabTimer / _grabDuration);

                // Hand rushes in from the side with overshoot
                float handTargetX = 0.25f * _grabDirection;
                float handX = Mathf.Lerp(8f * _grabDirection, handTargetX, EaseOutBack(t));
                float handY = _grabBalloonPos.y - _grabBalloonScale.y * _spriteWorldSize * 0.4f;
                _handObj.transform.position = new Vector3(handX, handY, -0.5f);
                _handObj.SetActive(true);
                bool isFist = t >= 0.45f;
                _handRenderer.sprite = isFist ? _fistSprite : _handSprite;
                float handAlpha = Mathf.Clamp01(t * 4f);
                _handRenderer.color = isFist
                    ? new Color(1f, 1f, 1f, handAlpha)
                    : new Color(0.92f, 0.78f, 0.65f, handAlpha);

                // Perspective: hand enters large (near camera) and shrinks as it reaches the balloon
                float perspective = Mathf.Lerp(2.8f, 1f, Mathf.Clamp01(t * 1.4f));
                // Clench: uniform shrink with oscillating squeeze (skinny ↔ wide, settling smaller)
                float clenchT = Mathf.Clamp01((t - 0.3f) / 0.7f);
                float uniform = Mathf.Lerp(1f, 0.55f, clenchT);
                float squeezeAmp = 0.14f * (1f - clenchT * clenchT);
                float squeeze = Mathf.Sin(clenchT * 2.5f * Mathf.PI * 2f) * squeezeAmp;
                float clenchX = uniform * (1f - squeeze);
                float clenchY = uniform * (1f + squeeze);
                float baseScale = 0.9f * perspective;
                float handScaleX = baseScale * clenchX;
                float handScaleY = baseScale * clenchY;
                float flipX = -_grabDirection;
                _handObj.transform.localScale = new Vector3(handScaleX * flipX, handScaleY, 1f);

                // Rotate from vertical (fingers up) to horizontal fist as it grabs
                float rotT = Mathf.Clamp01((t - 0.15f) / 0.65f);
                float handRotation = Mathf.Lerp(0f, -90f * _grabDirection, EaseOutBack(rotT));
                _handObj.transform.rotation = Quaternion.Euler(0f, 0f, handRotation);

                // String trails from the hand dangling down (separate from depart string)
                if (_grabHandString != null)
                {
                    _grabHandString.gameObject.SetActive(true);
                    float sTop = handY - 0.05f;
                    float sLen = 4.5f;
                    float sway = Mathf.Sin(Time.time * 2.5f) * 0.06f;
                    float drag = _grabDirection * (1f - t) * 0.4f;
                    _grabHandString.SetPosition(0, new Vector3(handX, sTop, -0.6f));
                    _grabHandString.SetPosition(1, new Vector3(handX + drag * 0.6f + sway, sTop - sLen * 0.25f, -0.6f));
                    _grabHandString.SetPosition(2, new Vector3(handX + drag * 0.3f + sway * 1.3f, sTop - sLen * 0.5f, -0.6f));
                    _grabHandString.SetPosition(3, new Vector3(handX + drag * 0.1f + sway * 0.8f, sTop - sLen * 0.75f, -0.6f));
                    _grabHandString.SetPosition(4, new Vector3(handX + sway * 0.4f, sTop - sLen, -0.6f));
                }

                // Squish the balloon comically — compress X, stretch Y
                float squishT = Mathf.Sin(t * Mathf.PI);
                float sx = _grabBalloonScale.x * (1f - _squishAmount * squishT);
                float sy = _grabBalloonScale.y * (1f + _squishAmount * 0.6f * squishT);
                _balloonBody.transform.localScale = new Vector3(sx, sy, 1f);

                // Knot forms as an X between hand bottom and string, contracting to a tie
                if (t > 0.65f)
                {
                    _knotLine.gameObject.SetActive(true);
                    float knotT = Mathf.Clamp01((t - 0.65f) / 0.35f);
                    float handBottom = handY - handScaleY * (_fistTexture.height / 80f) * 0.32f;
                    float knotBottom = handBottom - 0.10f;
                    float pinch = Mathf.Lerp(0.16f, 0.015f, knotT);
                    _knotLine.SetPosition(0, new Vector3(handX - pinch, handBottom, -0.1f));
                    _knotLine.SetPosition(1, new Vector3(handX + pinch, knotBottom, -0.1f));
                    _knotLine.SetPosition(2, new Vector3(handX - pinch, knotBottom, -0.1f));
                    _knotLine.SetPosition(3, new Vector3(handX + pinch, handBottom, -0.1f));
                }

                if (t >= 1f)
                {
                    // Transfer to departing system
                    LaunchDeparting();

                    // Immediately reset for the next balloon
                    CycleColor();
                    ResetInflation();
                    _phase = Phase.Inflating;
                    scored = true;
                }
            }

            return scored;
        }

        private void BeginGrab()
        {
            _phase = Phase.Grabbing;
            _grabTimer = 0f;
            _grabDirection = (_colorIndex % 2 == 0) ? 1f : -1f;
            _handRenderer.sprite = _handSprite;
            _handObj.transform.rotation = Quaternion.identity;
            _grabBalloonPos = _balloonBody.transform.position;
            _grabBalloonScale = _balloonBody.transform.localScale;

            // Stop inflation audio (no secondary beeps — air rush only via ProceduralBalloonAudio)
            if (_proceduralAudio != null)
                _proceduralAudio.UpdateAudio(0f, _inflationProgress, false);
        }

        private void LaunchDeparting()
        {
            _departActive = true;
            _departTimer = 0f;
            _departDir = _grabDirection;
            _departStartPos = _balloonBody.transform.position;
            _departStartScale = _balloonBody.transform.localScale;

            if (_grabHandString != null)
                _grabHandString.gameObject.SetActive(false);
            if (_departStringLine != null)
                _departStringLine.gameObject.SetActive(true);

            _departObj.SetActive(true);
            _departObj.transform.position = _departStartPos;
            _departObj.transform.localScale = _departStartScale;
            _departObj.transform.rotation = Quaternion.identity;
            _departRenderer.color = _balloonRenderer.color;
        }

        private void UpdateDeparting()
        {
            _departTimer += Time.deltaTime;

            float x = _departStartPos.x + _departDir * _departSlideSpeed * _departTimer;
            float y = _departStartPos.y + _departTimer * 2f;
            _departObj.transform.position = new Vector3(x, y, 0.2f);
            _departObj.transform.rotation = Quaternion.identity;

            // Hand follows departing balloon as a horizontal fist
            float halfH = _departStartScale.y * _spriteWorldSize * 0.5f;
            _handObj.transform.position = new Vector3(x + 0.2f * _departDir, y - halfH - 0.15f, -0.5f);
            _handRenderer.sprite = _fistSprite;
            _handRenderer.color = Color.white;
            float departClenched = 0.9f * 0.55f;
            _handObj.transform.localScale = new Vector3(departClenched * -_departDir, departClenched, 1f);
            _handObj.transform.rotation = Quaternion.Euler(0f, 0f, -90f * _departDir);

            // Knot stays attached to departing balloon bottom (contracted X)
            float bBot = y - _departStartScale.y * _spriteWorldSize * 0.5f;
            float knotTip = bBot - 0.14f;
            _knotLine.SetPosition(0, new Vector3(x - 0.015f, bBot, -0.1f));
            _knotLine.SetPosition(1, new Vector3(x + 0.015f, knotTip, -0.1f));
            _knotLine.SetPosition(2, new Vector3(x - 0.015f, knotTip, -0.1f));
            _knotLine.SetPosition(3, new Vector3(x + 0.015f, bBot, -0.1f));

            // Long string dangles from the knot
            if (_departStringLine != null)
            {
                float sLen = 5.5f;
                float time = Time.time;
                float sw1 = Mathf.Sin(time * 1.7f + _departDir) * 0.1f;
                float sw2 = Mathf.Sin(time * 2.3f + _departDir * 0.7f) * 0.06f;
                _departStringLine.SetPosition(0, new Vector3(x, knotTip, -0.6f));
                _departStringLine.SetPosition(1, new Vector3(x + sw1 * 0.3f, knotTip - sLen * 0.25f, -0.6f));
                _departStringLine.SetPosition(2, new Vector3(x + sw1 + sw2, knotTip - sLen * 0.5f, -0.6f));
                _departStringLine.SetPosition(3, new Vector3(x + sw1 * 0.7f + sw2 * 0.5f, knotTip - sLen * 0.75f, -0.6f));
                _departStringLine.SetPosition(4, new Vector3(x + sw2 * 0.3f, knotTip - sLen, -0.6f));
            }

            float halfW = _bgCamW > 0f ? _bgCamW * 0.5f : 12f;
            float halfH2 = _bgCamH > 0f ? _bgCamH * 0.5f : 7f;
            bool offScreen = Mathf.Abs(x) > halfW + 2f || y > halfH2 + 2f;

            if (offScreen || _departTimer >= _departLifetime)
            {
                _departActive = false;
                _departObj.SetActive(false);
                _handObj.SetActive(false);
                _knotLine.gameObject.SetActive(false);
                if (_departStringLine != null)
                    _departStringLine.gameObject.SetActive(false);
            }
        }

        private void ResetInflation()
        {
            _inflationProgress = 0f;
            _popInTimer = POP_IN_DURATION;
            _deflatedTilt = Random.Range(-35f, 35f);
            _deflatedFlip = Random.value > 0.5f ? 1f : -1f;
            _balloonRenderer.sprite = _deflatedSprite;
            _knotLine.gameObject.SetActive(false);
            if (_grabHandString != null)
                _grabHandString.gameObject.SetActive(false);

            UpdateBalloonVisual();
        }

        private void UpdateBalloonVisual()
        {
            Color baseColor = _balloonColors[_colorIndex % _balloonColors.Length];

            // Deflated state: limp rubber draped at the nozzle mouth, scales up to match balloon at threshold
            if (_inflationProgress < DEFLATED_THRESHOLD)
            {
                if (_balloonRenderer.sprite != _deflatedSprite)
                    _balloonRenderer.sprite = _deflatedSprite;

                _balloonRenderer.sortingOrder = 3;

                float thresholdScale = GetScaleForProgress(DEFLATED_THRESHOLD);
                float targetScaleX = thresholdScale;
                float targetScaleY = thresholdScale * HEIGHT_RATIO;

                float growT = Mathf.Clamp01(_inflationProgress / DEFLATED_THRESHOLD);
                float minFrac = 1.0f;
                float maxFrac = 2.0f;
                float eased = growT * growT;
                float frac = Mathf.Lerp(minFrac, maxFrac, eased);
                _balloonBody.transform.localScale = new Vector3(
                    targetScaleX * frac * _deflatedFlip, -targetScaleY * frac, 1f);

                float nozzleTop = _nozzleY + 0.55f;
                _balloonBody.transform.position = new Vector3(0f, nozzleTop, -0.05f);
                float tiltAmount = _deflatedTilt * 0.3f;
                _balloonBody.transform.rotation = Quaternion.Euler(0f, 0f, tiltAmount);

                Color muted = baseColor * 0.7f;
                muted.a = 1f;
                _balloonRenderer.color = muted;

                UpdateNozzleTipColor(baseColor);
                UpdateNozzleVisual();
                _stringLine.gameObject.SetActive(false);
                return;
            }

            // Transition: swap to inflating balloon sprite
            if (_balloonRenderer.sprite != _balloonSprite)
            {
                _balloonRenderer.sprite = _balloonSprite;
                _balloonRenderer.sortingOrder = 1;
                _balloonBody.transform.rotation = Quaternion.identity;
            }
            _stringLine.gameObject.SetActive(true);

            float scale = GetScaleForProgress(_inflationProgress);

            float popMult = 1f;
            if (_popInTimer > 0f)
            {
                _popInTimer -= Time.deltaTime;
                float pt = 1f - Mathf.Clamp01(_popInTimer / POP_IN_DURATION);
                popMult = 1f + Mathf.Sin(pt * Mathf.PI) * 0.3f;
            }

            _balloonBody.transform.localScale = new Vector3(
                scale * popMult, scale * HEIGHT_RATIO * popMult, 1f);

            float halfH = scale * HEIGHT_RATIO * _spriteWorldSize * 0.5f;
            float balloonCenterY = _nozzleY + halfH + 0.15f;
            _balloonBody.transform.position = new Vector3(0f, balloonCenterY, 0f);

            if (_phase == Phase.Inflating && _inflationProgress > 0.05f)
            {
                float sway = Mathf.Sin(Time.time * 2.5f) * 0.015f * _inflationProgress;
                _balloonBody.transform.position += new Vector3(sway, 0f, 0f);
            }

            UpdateNozzleTipColor(baseColor);
            UpdateNozzleVisual();
            UpdateStringForPosition(_balloonBody.transform.position);

            float sat = Mathf.Lerp(0.55f, 1f, _inflationProgress);
            Color.RGBToHSV(baseColor, out float h, out float s, out float v);
            _balloonRenderer.color = Color.HSVToRGB(h, s * sat, v);
        }

        private void UpdateNozzleVisual()
        {
            float nozzleTop = _nozzleY + 0.55f;
            _nozzleLine.SetPosition(0, new Vector3(0f, _nozzleY - 2.5f, 0f));
            _nozzleLine.SetPosition(1, new Vector3(0f, _nozzleY, 0f));
            _nozzleLine.SetPosition(2, new Vector3(0f, nozzleTop, 0f));
        }

        private void UpdateNozzleTipColor(Color balloonColor)
        {
            Color tipColor = balloonColor * 0.85f;
            tipColor.a = 1f;

            Color grayBot = new Color(0.35f, 0.35f, 0.40f);
            Color graySheen = new Color(0.62f, 0.62f, 0.68f);
            Color grayMid = new Color(0.48f, 0.48f, 0.54f);
            Color grayTop = new Color(0.55f, 0.55f, 0.60f);

            _nozzleLine.colorGradient = new Gradient
            {
                colorKeys = new[] {
                    new GradientColorKey(grayBot, 0f),
                    new GradientColorKey(graySheen, 0.30f),
                    new GradientColorKey(grayMid, 0.55f),
                    new GradientColorKey(grayTop, 0.895f),
                    new GradientColorKey(tipColor, 0.90f),
                    new GradientColorKey(tipColor, 1f)
                },
                alphaKeys = new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            };
        }

        private void UpdateStringForPosition(Vector3 balloonPos)
        {
            if (_stringLine == null) return;
            float bottomY = balloonPos.y - _balloonBody.transform.localScale.y * _spriteWorldSize * 0.5f;
            float stringLen = 0.5f + _inflationProgress * 0.25f;

            float midX = Mathf.Sin(Time.time * 1.8f) * 0.05f;
            _stringLine.SetPosition(0, new Vector3(balloonPos.x, bottomY, 0.1f));
            _stringLine.SetPosition(1, new Vector3(balloonPos.x + midX, bottomY - stringLen * 0.5f, 0.1f));
            _stringLine.SetPosition(2, new Vector3(balloonPos.x, bottomY - stringLen, 0.1f));
        }

        private void UpdateKnotVisual(float t)
        {
            float bottomY = GetBalloonBottomY();
            float topY = bottomY + 0.06f;
            float tipY = topY - 0.16f;
            float pinch = Mathf.Lerp(0.18f, 0.015f, t);
            _knotLine.SetPosition(0, new Vector3(-pinch, topY, -0.1f));
            _knotLine.SetPosition(1, new Vector3(pinch, tipY, -0.1f));
            _knotLine.SetPosition(2, new Vector3(-pinch, tipY, -0.1f));
            _knotLine.SetPosition(3, new Vector3(pinch, topY, -0.1f));
        }

        private float GetBalloonBottomY()
        {
            float centerY = _balloonBody.transform.position.y;
            return centerY - _balloonBody.transform.localScale.y * _spriteWorldSize * 0.5f;
        }

        private float GetScaleForProgress(float progress)
        {
            return Mathf.Lerp(_minScale, _maxScale, progress);
        }

        private void CycleColor()
        {
            _colorIndex = (_colorIndex + 1) % _balloonColors.Length;
            ApplyBalloonColor();
        }

        private void ApplyBalloonColor()
        {
            if (_balloonRenderer != null)
                _balloonRenderer.color = _balloonColors[_colorIndex % _balloonColors.Length];
        }

        // ─── Build methods ───────────────────────────────────────

        private void BuildBackground()
        {
            _bgCamH = _cam != null ? _cam.orthographicSize * 2f : 14f;
            _bgCamW = _cam != null ? _bgCamH * _cam.aspect : 20f;

            if (_cam != null)
                _cam.backgroundColor = new Color(0.42f, 0.72f, 0.90f);

            EnsureWhiteSprite();
            BuildSkyGradient(_bgCamW, _bgCamH);
            BuildSun(_bgCamW, _bgCamH);
            BuildClouds(_bgCamW, _bgCamH);
            BuildGrass(_bgCamW, _bgCamH);
            BuildFloatingBalloons(_bgCamW, _bgCamH);
            BuildPartyScene(_bgCamW, _bgCamH);
        }

        private void BuildSkyGradient(float camW, float camH)
        {
            var bgObj = new GameObject("BalloonBackground");
            bgObj.transform.SetParent(transform);
            var bgRenderer = bgObj.AddComponent<SpriteRenderer>();

            var bgTex = GenerateGradientTexture(4, 256,
                new Color(0.82f, 0.90f, 0.96f),
                new Color(0.38f, 0.65f, 0.90f));
            bgRenderer.sprite = Sprite.Create(bgTex, new Rect(0, 0, bgTex.width, bgTex.height),
                new Vector2(0.5f, 0.5f), 8f);
            bgRenderer.sortingOrder = -20;

            float spriteW = bgTex.width / 8f;
            float spriteH = bgTex.height / 8f;
            bgObj.transform.localScale = new Vector3(camW / spriteW + 0.5f, camH / spriteH + 0.5f, 1f);
            bgObj.transform.position = new Vector3(0f, 0f, 10f);
        }

        private void BuildSun(float camW, float camH)
        {
            var sunTex = GenerateSoftCircleTexture(64);
            var sunSprite = Sprite.Create(sunTex, new Rect(0, 0, 64, 64),
                new Vector2(0.5f, 0.5f), 16f);

            var sunObj = new GameObject("Sun");
            sunObj.transform.SetParent(transform);
            var sunRend = sunObj.AddComponent<SpriteRenderer>();
            sunRend.sprite = sunSprite;
            sunRend.color = new Color(1f, 0.95f, 0.5f, 0.3f);
            sunRend.sortingOrder = -19;
            sunObj.transform.position = new Vector3(camW * 0.32f, camH * 0.32f, 9.5f);
            sunObj.transform.localScale = new Vector3(5f, 5f, 1f);

            var glowObj = new GameObject("SunGlow");
            glowObj.transform.SetParent(transform);
            var glowRend = glowObj.AddComponent<SpriteRenderer>();
            glowRend.sprite = sunSprite;
            glowRend.color = new Color(1f, 0.92f, 0.6f, 0.1f);
            glowRend.sortingOrder = -19;
            glowObj.transform.position = new Vector3(camW * 0.32f, camH * 0.32f, 9.5f);
            glowObj.transform.localScale = new Vector3(8f, 8f, 1f);
        }

        private void BuildClouds(float camW, float camH)
        {
            var cloudTex = GenerateSoftCircleTexture(64);
            var cloudSprite = Sprite.Create(cloudTex, new Rect(0, 0, 64, 64),
                new Vector2(0.5f, 0.5f), 16f);

            for (int i = 0; i < 10; i++)
            {
                var cloudObj = new GameObject($"BgCloud_{i}");
                cloudObj.transform.SetParent(transform);
                var cloudRend = cloudObj.AddComponent<SpriteRenderer>();
                cloudRend.sprite = cloudSprite;
                cloudRend.color = new Color(1f, 1f, 1f, Random.Range(0.06f, 0.16f));
                cloudRend.sortingOrder = -19;

                float cx = Random.Range(-camW * 0.45f, camW * 0.45f);
                float cy = Random.Range(camH * 0.02f, camH * 0.40f);
                float cs = Random.Range(2f, 6f);
                cloudObj.transform.position = new Vector3(cx, cy, 9f);
                cloudObj.transform.localScale = new Vector3(cs, cs * 0.5f, 1f);
            }
        }

        private void BuildGrass(float camW, float camH)
        {
            var grassTex = GenerateGradientTexture(4, 64,
                new Color(0.22f, 0.50f, 0.18f),
                new Color(0.35f, 0.68f, 0.28f));
            var grassSprite = Sprite.Create(grassTex, new Rect(0, 0, grassTex.width, grassTex.height),
                new Vector2(0.5f, 0.5f), 8f);

            float grassH = camH * 0.18f;
            _grassHeight = grassH;
            _groundTopY = -camH * 0.5f + grassH;

            var grassObj = new GameObject("Grass");
            grassObj.transform.SetParent(transform);
            var grassRend = grassObj.AddComponent<SpriteRenderer>();
            grassRend.sprite = grassSprite;
            grassRend.sortingOrder = -16;

            float spriteW = grassTex.width / 8f;
            float spriteH = grassTex.height / 8f;
            grassObj.transform.localScale = new Vector3((camW + 1f) / spriteW, grassH / spriteH, 1f);
            grassObj.transform.position = new Vector3(0f, -camH * 0.5f + grassH * 0.5f, 8f);
        }

        private void BuildFloatingBalloons(float camW, float camH)
        {
            var bgBalloonSprite = Sprite.Create(_balloonTexture,
                new Rect(0, 0, _balloonTexture.width, _balloonTexture.height),
                new Vector2(0.5f, 0.5f), PPU);

            float halfH = camH * 0.5f;
            float halfW = camW * 0.5f;

            for (int i = 0; i < 10; i++)
            {
                float bs = Random.Range(0.10f, 0.22f);
                Color col = _balloonColors[i % _balloonColors.Length];
                col.a = Random.Range(0.45f, 0.65f);
                float startX = Random.Range(-halfW * 0.85f, halfW * 0.85f);
                float startY = Random.Range(-halfH - 2f, halfH + 1f);

                var obj = new GameObject($"FloatingBalloon_{i}");
                obj.transform.SetParent(transform);
                var rend = obj.AddComponent<SpriteRenderer>();
                rend.sprite = bgBalloonSprite;
                rend.color = col;
                rend.sortingOrder = -17;
                obj.transform.position = new Vector3(startX, startY, 8.5f);
                obj.transform.localScale = new Vector3(bs, bs * HEIGHT_RATIO, 1f);

                float sLen = 0.3f + bs * 2.5f;
                var sObj = new GameObject($"FloatingString_{i}");
                sObj.transform.SetParent(transform);
                var lr = sObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 3;
                lr.material = _spriteMat;
                lr.startWidth = 0.015f;
                lr.endWidth = 0.008f;
                lr.startColor = new Color(0.5f, 0.5f, 0.55f, col.a * 0.7f);
                lr.endColor = new Color(0.5f, 0.5f, 0.55f, col.a * 0.5f);
                lr.sortingOrder = -18;
                lr.numCornerVertices = 2;

                _bgBalloons.Add(new BgBalloonData
                {
                    root = obj.transform,
                    stringLine = lr,
                    speed = Random.Range(0.3f, 0.8f),
                    swayPhase = Random.Range(0f, Mathf.PI * 2f),
                    stringLen = sLen
                });
            }
        }

        private void BuildPartyScene(float camW, float camH)
        {
            float groundTop = -camH * 0.5f + camH * 0.18f;
            float leftCenter = -camW * 0.28f;
            float rightCenter = camW * 0.28f;

            // === FOREGROUND LEFT: Table with cake + gift pile (close to camera, larger) ===
            float fg = 1.4f;
            float tableW = 1.6f * fg;
            float tableH = 0.12f * fg;
            float tableY = groundTop + 0.30f * fg;
            float cakeX = leftCenter + 0.2f;
            MakeBgRect("TableTop", cakeX, tableY, tableW, tableH,
                new Color(0.55f, 0.35f, 0.20f), -7, 5f);
            MakeBgRect("TableLegL", cakeX - tableW * 0.4f, tableY - 0.28f * fg, 0.10f * fg, 0.45f * fg,
                new Color(0.50f, 0.30f, 0.18f), -7, 5f);
            MakeBgRect("TableLegR", cakeX + tableW * 0.4f, tableY - 0.28f * fg, 0.10f * fg, 0.45f * fg,
                new Color(0.50f, 0.30f, 0.18f), -7, 5f);

            float cakeBase = tableY + tableH * 0.5f;
            MakeBgRect("CakeLayer1", cakeX, cakeBase + 0.18f * fg, 1.1f * fg, 0.35f * fg,
                new Color(0.85f, 0.55f, 0.65f, 1f), -6, 5f);
            MakeBgRect("CakeLayer2", cakeX, cakeBase + 0.42f * fg, 0.85f * fg, 0.28f * fg,
                new Color(0.92f, 0.72f, 0.76f, 1f), -6, 5f);
            MakeBgRect("CakeLayer3", cakeX, cakeBase + 0.62f * fg, 0.60f * fg, 0.22f * fg,
                new Color(0.96f, 0.82f, 0.84f, 1f), -6, 5f);
            MakeBgRect("Frosting1", cakeX, cakeBase + 0.35f * fg, 1.05f * fg, 0.05f * fg,
                new Color(1f, 1f, 1f, 0.8f), -5, 5f);
            MakeBgRect("Frosting2", cakeX, cakeBase + 0.56f * fg, 0.80f * fg, 0.04f * fg,
                new Color(1f, 1f, 1f, 0.7f), -5, 5f);

            var flameTex = GenerateSoftCircleTexture(32);
            var flameSprite = Sprite.Create(flameTex, new Rect(0, 0, 32, 32),
                new Vector2(0.5f, 0.5f), 32f);
            float[] candleDx = { -0.22f * fg, -0.07f * fg, 0.07f * fg, 0.22f * fg };
            Color[] candleC = {
                new Color(1f, 0.35f, 0.35f, 1f),
                new Color(0.35f, 0.65f, 1f, 1f),
                new Color(1f, 0.85f, 0.25f, 1f),
                new Color(0.45f, 0.85f, 0.45f, 1f)
            };
            for (int i = 0; i < 4; i++)
            {
                float cx = cakeX + candleDx[i];
                float candleTop = cakeBase + 0.82f * fg;
                MakeBgRect($"Candle_{i}", cx, candleTop, 0.06f * fg, 0.26f * fg, candleC[i], -5, 5f);

                Vector3 flamePos = new Vector3(cx, candleTop + 0.18f * fg, 5f);
                var fObj = new GameObject($"Flame_{i}");
                fObj.transform.SetParent(transform);
                var fRend = fObj.AddComponent<SpriteRenderer>();
                fRend.sprite = flameSprite;
                fRend.color = new Color(1f, 0.85f, 0.2f, 0.8f);
                fRend.sortingOrder = -4;
                fObj.transform.position = flamePos;
                fObj.transform.localScale = new Vector3(0.4f, 0.55f, 1f);
                _flameTransforms.Add(fObj.transform);
                _flameBasePositions.Add(flamePos);
            }

            // Gift pile — clearly to the LEFT of the table, sitting on the ground
            float giftCenterX = cakeX - tableW * 0.5f - 0.7f;
            float gy = groundTop;
            // Bottom row (3 gifts side by side on the ground)
            MakeBgRect("Gift1", giftCenterX - 0.32f, gy + 0.20f, 0.50f, 0.40f,
                new Color(0.3f, 0.55f, 0.90f, 1f), -7, 5f);
            MakeBgRect("Gift1Ribbon", giftCenterX - 0.32f, gy + 0.20f, 0.06f, 0.40f,
                new Color(1f, 0.85f, 0.2f, 1f), -6, 5f);
            MakeBgRect("Gift2", giftCenterX + 0.18f, gy + 0.16f, 0.42f, 0.32f,
                new Color(0.90f, 0.35f, 0.50f, 1f), -7, 5f);
            MakeBgRect("Gift2Ribbon", giftCenterX + 0.18f, gy + 0.16f, 0.05f, 0.32f,
                new Color(0.95f, 0.95f, 0.95f, 1f), -6, 5f);
            MakeBgRect("Gift3", giftCenterX + 0.55f, gy + 0.22f, 0.38f, 0.44f,
                new Color(0.70f, 0.30f, 0.80f, 1f), -7, 5f);
            MakeBgRect("Gift3Ribbon", giftCenterX + 0.55f, gy + 0.22f, 0.05f, 0.44f,
                new Color(0.95f, 0.75f, 0.25f, 1f), -6, 5f);
            // Top row (2 gifts stacked on the bottom row)
            MakeBgRect("Gift4", giftCenterX - 0.08f, gy + 0.52f, 0.44f, 0.34f,
                new Color(0.20f, 0.75f, 0.40f, 1f), -6, 5f);
            MakeBgRect("Gift4Ribbon", giftCenterX - 0.08f, gy + 0.52f, 0.05f, 0.34f,
                new Color(0.90f, 0.30f, 0.30f, 1f), -5, 5f);
            MakeBgRect("Gift5", giftCenterX + 0.36f, gy + 0.48f, 0.36f, 0.28f,
                new Color(0.80f, 0.65f, 0.20f, 1f), -6, 5f);
            MakeBgRect("Gift5Ribbon", giftCenterX + 0.36f, gy + 0.48f, 0.05f, 0.28f,
                new Color(0.40f, 0.20f, 0.60f, 1f), -5, 5f);
            // Top-top (1 small gift on top)
            MakeBgRect("Gift6", giftCenterX + 0.12f, gy + 0.74f, 0.32f, 0.24f,
                new Color(0.95f, 0.55f, 0.15f, 1f), -5, 5f);
            MakeBgRect("Gift6Ribbon", giftCenterX + 0.12f, gy + 0.74f, 0.04f, 0.24f,
                new Color(0.20f, 0.55f, 0.90f, 1f), -4, 5f);

            // === BACKGROUND RIGHT: Banner on posts + tied balloon cluster ===
            BuildBannerAndTiedBalloons(camW, camH, rightCenter, groundTop);

            // === MID-GROUND: Pillar frames + string lights ===
            BuildPillarsAndStringLights(camW, camH, groundTop);

            // === Extra party details ===
            BuildExtraDecor(camW, camH, groundTop);
        }

        private void BuildBannerAndTiedBalloons(float camW, float camH, float centerX, float groundTop)
        {
            float postH = 1.4f;
            float bannerY = groundTop + postH * 0.75f;
            MakeBgRect("PostL", centerX - 0.6f, groundTop + postH * 0.5f, 0.06f, postH,
                new Color(0.55f, 0.35f, 0.20f), -14);
            MakeBgRect("PostR", centerX + 0.6f, groundTop + postH * 0.5f, 0.06f, postH,
                new Color(0.55f, 0.35f, 0.20f), -14);

            float bannerW = 1.26f;
            Transform bannerMain = MakeBgRectReturn("Banner", centerX, bannerY, bannerW, 0.30f,
                new Color(0.90f, 0.25f, 0.30f, 0.85f), -13);
            _bannerParts.Add(bannerMain);
            _bannerBasePositions.Add(bannerMain.position);
            Transform stripeT = MakeBgRectReturn("BannerStripeT", centerX, bannerY + 0.08f, bannerW - 0.04f, 0.04f,
                new Color(1f, 0.85f, 0.2f, 0.85f), -12);
            _bannerParts.Add(stripeT);
            _bannerBasePositions.Add(stripeT.position);
            Transform stripeB = MakeBgRectReturn("BannerStripeB", centerX, bannerY - 0.08f, bannerW - 0.04f, 0.04f,
                new Color(1f, 0.85f, 0.2f, 0.85f), -12);
            _bannerParts.Add(stripeB);
            _bannerBasePositions.Add(stripeB.position);

            var bgBalloonSprite = Sprite.Create(_balloonTexture,
                new Rect(0, 0, _balloonTexture.width, _balloonTexture.height),
                new Vector2(0.5f, 0.5f), PPU);

            float weightX = centerX;
            float weightY = groundTop + 0.08f;
            MakeBgRect("BalloonWeight", weightX, weightY, 0.18f, 0.14f,
                new Color(0.4f, 0.4f, 0.45f, 0.9f), -14);

            float[] tiedDx    = { -0.35f, -0.12f, 0.10f, 0.32f, -0.22f, 0.22f };
            float[] tiedDy    = {  1.8f,   2.2f,  2.0f,  1.7f,  2.4f,   2.5f  };
            float[] tiedScale = {  0.18f,  0.15f, 0.17f, 0.20f, 0.13f,  0.14f };
            Color[] tiedColors = {
                new Color(0.92f, 0.18f, 0.18f, 0.85f),
                new Color(0.22f, 0.80f, 0.35f, 0.85f),
                new Color(0.92f, 0.72f, 0.08f, 0.85f),
                new Color(0.30f, 0.50f, 0.95f, 0.85f),
                new Color(0.90f, 0.40f, 0.70f, 0.85f),
                new Color(0.55f, 0.20f, 0.85f, 0.85f)
            };

            for (int i = 0; i < tiedDx.Length; i++)
            {
                float bx = weightX + tiedDx[i];
                float by = weightY + tiedDy[i];
                float bs = tiedScale[i];

                var obj = new GameObject($"TiedBalloon_{i}");
                obj.transform.SetParent(transform);
                var rend = obj.AddComponent<SpriteRenderer>();
                rend.sprite = bgBalloonSprite;
                rend.color = tiedColors[i];
                rend.sortingOrder = -15;
                Vector3 bPos = new Vector3(bx, by, 7.5f);
                obj.transform.position = bPos;
                obj.transform.localScale = new Vector3(bs, bs * HEIGHT_RATIO, 1f);

                var sObj = new GameObject($"TiedString_{i}");
                sObj.transform.SetParent(transform);
                var lr = sObj.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 3;
                lr.material = _spriteMat;
                lr.startWidth = 0.02f;
                lr.endWidth = 0.015f;
                lr.startColor = new Color(0.5f, 0.5f, 0.55f, 0.7f);
                lr.endColor = new Color(0.5f, 0.5f, 0.55f, 0.5f);
                lr.sortingOrder = -16;
                lr.numCornerVertices = 2;

                _tiedBalloons.Add(new TiedBalloonRef
                {
                    root = obj.transform,
                    stringLine = lr,
                    basePos = bPos,
                    weightX = weightX,
                    weightY = weightY,
                    phase = i * 1.3f
                });
            }
        }

        private void BuildPillarsAndStringLights(float camW, float camH, float groundTop)
        {
            float halfW = camW * 0.5f;
            float halfH = camH * 0.5f;
            float pillarW = 0.55f;
            float screenBot = -halfH;
            float grassMid = (groundTop + screenBot) * 0.5f;
            float pillarTop = halfH + 0.2f;
            float pillarH = pillarTop - grassMid;
            float pillarY = grassMid + pillarH * 0.5f;
            float pillarZ = 6.5f;
            Color pillarBase = new Color(0.48f, 0.30f, 0.16f);
            Color pillarDark = new Color(0.38f, 0.22f, 0.12f);
            Color pillarHighlight = new Color(0.58f, 0.38f, 0.22f);

            // Left pillar — rooted in the grass, extends to top of screen
            float lpx = -halfW + pillarW * 0.35f;
            MakeBgRect("PillarL", lpx, pillarY, pillarW, pillarH, pillarBase, -9, pillarZ);
            MakeBgRect("PillarLShadeL", lpx - pillarW * 0.35f, pillarY, pillarW * 0.15f, pillarH,
                pillarDark, -8, pillarZ);
            MakeBgRect("PillarLHighR", lpx + pillarW * 0.15f, pillarY, pillarW * 0.12f, pillarH,
                pillarHighlight, -8, pillarZ);
            MakeBgRect("PillarLCap", lpx, pillarTop + 0.06f, pillarW * 1.2f, 0.14f,
                pillarDark, -8, pillarZ);
            MakeBgRect("PillarLBase", lpx, grassMid + 0.06f, pillarW * 1.3f, 0.16f,
                pillarDark, -8, pillarZ);

            // Right pillar
            float rpx = halfW - pillarW * 0.35f;
            MakeBgRect("PillarR", rpx, pillarY, pillarW, pillarH, pillarBase, -9, pillarZ);
            MakeBgRect("PillarRShadeR", rpx + pillarW * 0.35f, pillarY, pillarW * 0.15f, pillarH,
                pillarDark, -8, pillarZ);
            MakeBgRect("PillarRHighL", rpx - pillarW * 0.15f, pillarY, pillarW * 0.12f, pillarH,
                pillarHighlight, -8, pillarZ);
            MakeBgRect("PillarRCap", rpx, pillarTop + 0.06f, pillarW * 1.2f, 0.14f,
                pillarDark, -8, pillarZ);
            MakeBgRect("PillarRBase", rpx, grassMid + 0.06f, pillarW * 1.3f, 0.16f,
                pillarDark, -8, pillarZ);

            // String lights hang between pillar caps, extending past screen edges
            float stringStartX = lpx - 0.5f;
            float stringEndX = rpx + 0.5f;
            float topY = pillarTop - 0.1f;

            var stringObj = new GameObject("PartyString");
            stringObj.transform.SetParent(transform);
            var stringLR = stringObj.AddComponent<LineRenderer>();
            stringLR.useWorldSpace = true;
            int pts = 16;
            stringLR.positionCount = pts;
            stringLR.material = _spriteMat;
            stringLR.startWidth = 0.03f;
            stringLR.endWidth = 0.03f;
            stringLR.startColor = new Color(0.35f, 0.35f, 0.40f, 0.6f);
            stringLR.endColor = new Color(0.35f, 0.35f, 0.40f, 0.6f);
            stringLR.sortingOrder = -9;
            stringLR.numCornerVertices = 3;

            var lightTex = GenerateSoftCircleTexture(32);
            var lightSprite = Sprite.Create(lightTex, new Rect(0, 0, 32, 32),
                new Vector2(0.5f, 0.5f), 32f);
            Color[] lightColors = {
                new Color(1f, 0.3f, 0.3f, 0.7f),
                new Color(0.3f, 0.8f, 0.3f, 0.7f),
                new Color(0.3f, 0.5f, 1f, 0.7f),
                new Color(1f, 0.85f, 0.2f, 0.7f),
                new Color(0.8f, 0.3f, 0.9f, 0.7f),
                new Color(1f, 0.5f, 0.2f, 0.7f)
            };

            for (int i = 0; i < pts; i++)
            {
                float t = (float)i / (pts - 1);
                float x = Mathf.Lerp(stringStartX, stringEndX, t);
                float sag = 4f * t * (1f - t) * 0.8f;
                float y = topY - sag;
                stringLR.SetPosition(i, new Vector3(x, y, pillarZ));

                var lightObj = new GameObject($"StringLight_{i}");
                lightObj.transform.SetParent(transform);
                var lightRend = lightObj.AddComponent<SpriteRenderer>();
                lightRend.sprite = lightSprite;
                lightRend.color = lightColors[i % lightColors.Length];
                lightRend.sortingOrder = -8;
                lightObj.transform.position = new Vector3(x, y - 0.10f, pillarZ);
                lightObj.transform.localScale = new Vector3(0.28f, 0.34f, 1f);
            }
        }

        private void BuildExtraDecor(float camW, float camH, float groundTop)
        {
            _confettiColors = new Color[] {
                new Color(1f, 0.3f, 0.3f),
                new Color(0.3f, 0.8f, 0.3f),
                new Color(0.3f, 0.5f, 1f),
                new Color(1f, 0.85f, 0.2f),
                new Color(0.9f, 0.4f, 0.7f),
                new Color(0.5f, 0.3f, 0.9f),
                new Color(1f, 0.6f, 0.2f),
                new Color(0.2f, 0.8f, 0.8f)
            };
            float halfW = camW * 0.5f;
            float halfH = camH * 0.5f;

            // Static confetti on the ground
            for (int i = 0; i < 30; i++)
            {
                float cx = Random.Range(-halfW * 0.75f, halfW * 0.75f);
                float cy = groundTop + Random.Range(-0.12f, 0.04f);
                float cw = Random.Range(0.05f, 0.10f);
                float ch = Random.Range(0.03f, 0.05f);
                Color cc = _confettiColors[i % _confettiColors.Length];
                cc.a = Random.Range(0.35f, 0.55f);
                var cObj = new GameObject($"GroundConfetti_{i}");
                cObj.transform.SetParent(transform);
                var cRend = cObj.AddComponent<SpriteRenderer>();
                cRend.sprite = _whiteSprite;
                cRend.color = cc;
                cRend.sortingOrder = -15;
                cObj.transform.position = new Vector3(cx, cy, 7f);
                cObj.transform.localScale = new Vector3(cw, ch, 1f);
                cObj.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-40f, 40f));
            }

            // Falling confetti snow — continuously drifts down and recycles
            for (int i = 0; i < 30; i++)
            {
                float cx = Random.Range(-halfW * 0.9f, halfW * 0.9f);
                float cy = Random.Range(-halfH, halfH + 2f);
                float cw = Random.Range(0.06f, 0.12f);
                float ch = Random.Range(0.035f, 0.07f);
                Color cc = _confettiColors[i % _confettiColors.Length];
                cc.a = Random.Range(0.55f, 0.8f);

                var cObj = new GameObject($"FallingConfetti_{i}");
                cObj.transform.SetParent(transform);
                var cRend = cObj.AddComponent<SpriteRenderer>();
                cRend.sprite = _whiteSprite;
                cRend.color = cc;
                cRend.sortingOrder = -15;
                cObj.transform.position = new Vector3(cx, cy, 6.8f);
                cObj.transform.localScale = new Vector3(cw, ch, 1f);

                _confetti.Add(new ConfettiData
                {
                    root = cObj.transform,
                    fallSpeed = Random.Range(0.3f, 0.9f),
                    swayPhase = Random.Range(0f, Mathf.PI * 2f),
                    tumbleSpeed = Random.Range(30f, 120f),
                    color = cc
                });
            }
        }

        private void MakeBgRect(string name, float x, float y, float w, float h, Color color, int order, float z = 7f)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform);
            var rend = obj.AddComponent<SpriteRenderer>();
            rend.sprite = _whiteSprite;
            rend.color = color;
            rend.sortingOrder = order;
            obj.transform.position = new Vector3(x, y, z);
            obj.transform.localScale = new Vector3(w, h, 1f);
        }

        private Transform MakeBgRectReturn(string name, float x, float y, float w, float h, Color color, int order, float z = 7f)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(transform);
            var rend = obj.AddComponent<SpriteRenderer>();
            rend.sprite = _whiteSprite;
            rend.color = color;
            rend.sortingOrder = order;
            obj.transform.position = new Vector3(x, y, z);
            obj.transform.localScale = new Vector3(w, h, 1f);
            return obj.transform;
        }

        private void BuildNozzle()
        {
            var nozzleObj = new GameObject("Nozzle");
            nozzleObj.transform.SetParent(transform);
            _nozzleLine = nozzleObj.AddComponent<LineRenderer>();
            _nozzleLine.useWorldSpace = true;
            _nozzleLine.positionCount = 3;
            _nozzleLine.material = _spriteMat;

            _nozzleLine.colorGradient = new Gradient
            {
                colorKeys = new[] {
                    new GradientColorKey(new Color(0.35f, 0.35f, 0.40f), 0f),
                    new GradientColorKey(new Color(0.62f, 0.62f, 0.68f), 0.30f),
                    new GradientColorKey(new Color(0.48f, 0.48f, 0.54f), 0.55f),
                    new GradientColorKey(new Color(0.55f, 0.55f, 0.60f), 0.89f),
                    new GradientColorKey(new Color(0.55f, 0.55f, 0.60f), 0.90f)
                },
                alphaKeys = new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 1f)
                }
            };
            _nozzleLine.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.5f, 0.22f),
                new Keyframe(0.88f, 0.14f),
                new Keyframe(0.93f, 0.22f),
                new Keyframe(1f, 0.18f));
            _nozzleLine.sortingOrder = 2;
            _nozzleLine.numCornerVertices = 5;
            _nozzleLine.numCapVertices = 4;
        }

        private void BuildBalloon()
        {
            _balloonBody = new GameObject("BalloonBody");
            _balloonBody.transform.SetParent(transform);
            _balloonRenderer = _balloonBody.AddComponent<SpriteRenderer>();
            _balloonSprite = Sprite.Create(_balloonTexture,
                new Rect(0, 0, _balloonTexture.width, _balloonTexture.height),
                new Vector2(0.5f, 0.5f), PPU);
            _deflatedSprite = Sprite.Create(_deflatedTexture,
                new Rect(0, 0, _deflatedTexture.width, _deflatedTexture.height),
                new Vector2(0.5f, 0.8f), PPU);
            _balloonRenderer.sprite = _deflatedSprite;
            _balloonRenderer.sortingOrder = 1;
        }

        private void BuildDepartingBalloon()
        {
            _departObj = new GameObject("DepartingBalloon");
            _departObj.transform.SetParent(transform);
            _departRenderer = _departObj.AddComponent<SpriteRenderer>();
            _departRenderer.sprite = _balloonSprite;
            _departRenderer.sortingOrder = -1;
            _departObj.SetActive(false);

            var dStrObj = new GameObject("DepartingString");
            dStrObj.transform.SetParent(transform);
            _departStringLine = dStrObj.AddComponent<LineRenderer>();
            _departStringLine.useWorldSpace = true;
            _departStringLine.positionCount = 5;
            _departStringLine.material = _spriteMat;
            _departStringLine.startColor = new Color(0.50f, 0.48f, 0.45f);
            _departStringLine.endColor = new Color(0.45f, 0.43f, 0.40f);
            _departStringLine.startWidth = 0.04f;
            _departStringLine.endWidth = 0.025f;
            _departStringLine.sortingOrder = 3;
            _departStringLine.numCornerVertices = 4;
            dStrObj.SetActive(false);
        }

        private void BuildString()
        {
            var stringObj = new GameObject("String");
            stringObj.transform.SetParent(transform);
            _stringLine = stringObj.AddComponent<LineRenderer>();
            _stringLine.useWorldSpace = true;
            _stringLine.positionCount = 3;
            _stringLine.material = _spriteMat;
            _stringLine.startColor = new Color(0.55f, 0.55f, 0.60f);
            _stringLine.endColor = new Color(0.45f, 0.45f, 0.50f);
            _stringLine.startWidth = 0.03f;
            _stringLine.endWidth = 0.02f;
            _stringLine.sortingOrder = 0;
            _stringLine.numCornerVertices = 3;
        }

        private void BuildHand()
        {
            _handObj = new GameObject("Hand");
            _handObj.transform.SetParent(transform);
            _handRenderer = _handObj.AddComponent<SpriteRenderer>();
            _handSprite = Sprite.Create(_handTexture,
                new Rect(0, 0, _handTexture.width, _handTexture.height),
                new Vector2(0.5f, 0.5f), 80f);
            _fistSprite = Sprite.Create(_fistTexture,
                new Rect(0, 0, _fistTexture.width, _fistTexture.height),
                new Vector2(0.5f, 0.5f), 80f);
            _handRenderer.sprite = _handSprite;
            _handRenderer.sortingOrder = 5;
            _handRenderer.color = new Color(0.92f, 0.78f, 0.65f);
            _handObj.transform.localScale = new Vector3(0.9f, 0.9f, 1f);
            _handObj.SetActive(false);

            var ghsObj = new GameObject("GrabHandString");
            ghsObj.transform.SetParent(transform);
            _grabHandString = ghsObj.AddComponent<LineRenderer>();
            _grabHandString.useWorldSpace = true;
            _grabHandString.positionCount = 5;
            _grabHandString.material = _spriteMat;
            _grabHandString.startColor = new Color(0.50f, 0.48f, 0.45f);
            _grabHandString.endColor = new Color(0.45f, 0.43f, 0.40f);
            _grabHandString.startWidth = 0.04f;
            _grabHandString.endWidth = 0.025f;
            _grabHandString.sortingOrder = 3;
            _grabHandString.numCornerVertices = 4;
            ghsObj.SetActive(false);

            var knotObj = new GameObject("Knot");
            knotObj.transform.SetParent(transform);
            _knotLine = knotObj.AddComponent<LineRenderer>();
            _knotLine.useWorldSpace = true;
            _knotLine.positionCount = 4;
            _knotLine.material = _spriteMat;
            _knotLine.startColor = new Color(0.4f, 0.4f, 0.45f);
            _knotLine.endColor = new Color(0.4f, 0.4f, 0.45f);
            _knotLine.startWidth = 0.07f;
            _knotLine.endWidth = 0.07f;
            _knotLine.sortingOrder = 4;
            _knotLine.gameObject.SetActive(false);
        }

        // ─── Texture generators ──────────────────────────────────

        private static void EnsureMaterial()
        {
            if (_spriteMat != null) return;
            _spriteMat = new Material(Shader.Find("Sprites/Default"));
        }

        private static void EnsureWhiteSprite()
        {
            if (_whiteSprite != null) return;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.filterMode = FilterMode.Point;
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        // High-res round balloon with baked specular highlight and rim shading.
        private static Texture2D GenerateBalloonTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float cx = size * 0.5f;
            float cy = size * 0.5f;
            float radius = size * 0.46f;

            float hlX = cx - size * 0.15f;
            float hlY = cy + size * 0.15f;
            float hlRadius = size * 0.14f;

            float hl2X = cx - size * 0.08f;
            float hl2Y = cy + size * 0.10f;
            float hl2Radius = size * 0.30f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float normDist = dist / radius;
                    float edge = Mathf.Clamp01((radius - dist) * 2f / (size * 0.01f + 1f));

                    if (edge <= 0f)
                    {
                        tex.SetPixel(x, y, Color.clear);
                        continue;
                    }

                    float rimFactor = Mathf.Clamp01(normDist);
                    float body = 1f - rimFactor * rimFactor * 0.35f;

                    float hlDist = Vector2.Distance(new Vector2(x, y), new Vector2(hlX, hlY));
                    float highlight = Mathf.Clamp01(1f - hlDist / hlRadius);
                    highlight = highlight * highlight * highlight;

                    float hl2Dist = Vector2.Distance(new Vector2(x, y), new Vector2(hl2X, hl2Y));
                    float sheen = Mathf.Clamp01(1f - hl2Dist / hl2Radius);
                    sheen = sheen * sheen * 0.25f;

                    float luminance = Mathf.Clamp01(body + highlight * 0.55f + sheen);
                    tex.SetPixel(x, y, new Color(luminance, luminance, luminance, edge));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        // Soft deflated balloon — smooth egg/oval like the inflated balloon but
        // slightly elongated and not taut. Narrow neck at top for nozzle attachment.
        private static Texture2D GenerateDeflatedBalloonTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float w = width;
            float h = height;

            // Main body: smooth ellipse, center shifted slightly low for egg shape
            float bodyCx = w * 0.5f;
            float bodyCy = h * 0.38f;
            float bodyRx = w * 0.40f;
            float bodyRy = h * 0.34f;

            // Neck: short squarish connector between body and nozzle
            float neckCx = w * 0.5f;
            float neckCy = h * 0.72f;
            float neckRx = w * 0.09f;
            float neckRy = h * 0.11f;

            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    float bdx = (px - bodyCx) / bodyRx;
                    float bdy = (py - bodyCy) / bodyRy;
                    float bodyDist = bdx * bdx + bdy * bdy;

                    float nax = Mathf.Abs(px - neckCx) / neckRx;
                    float nay = Mathf.Abs(py - neckCy) / neckRy;
                    float neckBox = Mathf.Max(nax, nay);
                    float neckDist = neckBox * neckBox;

                    float dist = Mathf.Min(bodyDist, neckDist);

                    float alpha = Mathf.Clamp01((1f - dist) * 4f);
                    if (alpha <= 0f)
                    {
                        tex.SetPixel(px, py, Color.clear);
                        continue;
                    }

                    float shade = 0.82f - Mathf.Clamp01(dist) * 0.14f;

                    // Specular highlight upper-left (matches inflated balloon)
                    float hlDist = Mathf.Sqrt(
                        (px - w * 0.36f) * (px - w * 0.36f) +
                        (py - h * 0.50f) * (py - h * 0.50f));
                    float hl = Mathf.Clamp01(1f - hlDist / (w * 0.30f));
                    shade += hl * hl * 0.22f;

                    // Subtle wrinkles for not-taut look
                    float wrinkle = Mathf.Abs(Mathf.Sin(py * 0.10f + px * 0.04f)) * 0.03f;
                    shade -= wrinkle;

                    shade = Mathf.Clamp01(shade);
                    tex.SetPixel(px, py, new Color(shade, shade, shade, alpha));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GenerateHandTexture(int width, int height)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float w = width;
            float h = height;

            float palmCx = w * 0.48f;
            float palmCy = h * 0.36f;
            float palmRx = w * 0.36f;
            float palmRy = h * 0.28f;

            float fingerBaseY = palmCy + palmRy * 0.55f;
            float fingerRx = w * 0.09f;
            float[] fingerCxs = {
                palmCx - w * 0.20f,
                palmCx - w * 0.07f,
                palmCx + w * 0.07f,
                palmCx + w * 0.20f
            };
            float[] fingerTipY = {
                fingerBaseY + h * 0.30f,
                fingerBaseY + h * 0.35f,
                fingerBaseY + h * 0.32f,
                fingerBaseY + h * 0.24f
            };

            float thumbCx = palmCx + w * 0.34f;
            float thumbCy = palmCy + h * 0.06f;
            float thumbRx = w * 0.10f;
            float thumbRy = h * 0.16f;

            for (int py = 0; py < height; py++)
            {
                for (int px = 0; px < width; px++)
                {
                    float minDist = 999f;

                    float pdx = (px - palmCx) / palmRx;
                    float pdy = (py - palmCy) / palmRy;
                    minDist = Mathf.Min(minDist, pdx * pdx + pdy * pdy);

                    for (int f = 0; f < 4; f++)
                    {
                        float fCy = (fingerBaseY + fingerTipY[f]) * 0.5f;
                        float fRy = (fingerTipY[f] - fingerBaseY) * 0.5f;
                        float fdx = (px - fingerCxs[f]) / fingerRx;
                        float fdy = (py - fCy) / Mathf.Max(fRy, 1f);
                        minDist = Mathf.Min(minDist, fdx * fdx + fdy * fdy);
                    }

                    float tdx = (px - thumbCx) / thumbRx;
                    float tdy = (py - thumbCy) / thumbRy;
                    minDist = Mathf.Min(minDist, tdx * tdx + tdy * tdy);

                    float alpha = Mathf.Clamp01((1f - minDist) * 5f);
                    if (alpha <= 0f)
                    {
                        tex.SetPixel(px, py, Color.clear);
                        continue;
                    }

                    float shade = 1f - Mathf.Clamp01(minDist) * 0.18f;

                    for (int f = 0; f < 3; f++)
                    {
                        float sepX = (fingerCxs[f] + fingerCxs[f + 1]) * 0.5f;
                        float sepD = Mathf.Abs(px - sepX) / (w * 0.012f);
                        bool inGroove = py > fingerBaseY && py < fingerBaseY + h * 0.18f;
                        if (sepD < 1f && inGroove)
                            shade -= (1f - sepD) * 0.10f;
                    }

                    float tWebX = palmCx + palmRx * 0.65f;
                    float tWebDist = Vector2.Distance(new Vector2(px, py), new Vector2(tWebX, thumbCy));
                    float tWebD = tWebDist / (w * 0.06f);
                    if (tWebD < 1f)
                        shade -= (1f - tWebD) * 0.08f;

                    shade = Mathf.Clamp01(shade);
                    tex.SetPixel(px, py, new Color(shade, shade, shade, alpha));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GenerateFistTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float s = size;
            Color skinBase = new Color(0.92f, 0.78f, 0.65f);

            // Main fist body — compact rounded block of curled fingers
            float bodyCx = s * 0.48f;
            float bodyCy = s * 0.45f;
            float bodyRx = s * 0.35f;
            float bodyRy = s * 0.26f;

            // Knuckle bumps along the top — squarish
            float knuckleY = bodyCy + bodyRy * 0.85f;
            float knuckleRx = s * 0.10f;
            float knuckleRy = s * 0.07f;
            float[] knuckleCxs = {
                bodyCx - s * 0.21f,
                bodyCx - s * 0.07f,
                bodyCx + s * 0.07f,
                bodyCx + s * 0.21f
            };

            // Thumb wraps across the front-bottom
            float thumbCx = bodyCx;
            float thumbCy = bodyCy - bodyRy * 0.55f;
            float thumbRx = s * 0.24f;
            float thumbRy = s * 0.11f;

            // Curled finger segment below the body (the folded-under part)
            float curlCx = bodyCx;
            float curlCy = bodyCy - bodyRy * 0.15f;
            float curlRx = bodyRx * 0.92f;
            float curlRy = bodyRy * 0.55f;

            // Wrist — narrower block extending below the fist
            float wristCx = bodyCx;
            float wristCy = s * 0.15f;
            float wristRx = bodyRx * 0.52f;
            float wristRy = s * 0.16f;
            float wristCreaseY = thumbCy - thumbRy * 0.6f;

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    float minDist = 999f;

                    float bdx = Mathf.Abs(px - bodyCx) / bodyRx;
                    float bdy = Mathf.Abs(py - bodyCy) / bodyRy;
                    float bd = Mathf.Max(bdx, bdy);
                    minDist = Mathf.Min(minDist, bd * bd);

                    for (int k = 0; k < 4; k++)
                    {
                        float kdx = Mathf.Abs(px - knuckleCxs[k]) / knuckleRx;
                        float kdy = Mathf.Abs(py - knuckleY) / knuckleRy;
                        float kd = Mathf.Max(kdx, kdy);
                        minDist = Mathf.Min(minDist, kd * kd);
                    }

                    float tdx = (px - thumbCx) / thumbRx;
                    float tdy = (py - thumbCy) / thumbRy;
                    minDist = Mathf.Min(minDist, tdx * tdx + tdy * tdy);

                    float cdx = Mathf.Abs(px - curlCx) / curlRx;
                    float cdy = Mathf.Abs(py - curlCy) / curlRy;
                    float cd = Mathf.Max(cdx, cdy);
                    minDist = Mathf.Min(minDist, cd * cd);

                    // Wrist
                    float wdx = Mathf.Abs(px - wristCx) / wristRx;
                    float wdy = Mathf.Abs(py - wristCy) / wristRy;
                    float wd = Mathf.Max(wdx, wdy);
                    minDist = Mathf.Min(minDist, wd * wd);

                    float alpha = Mathf.Clamp01((1f - minDist) * 5f);
                    if (alpha <= 0f)
                    {
                        tex.SetPixel(px, py, Color.clear);
                        continue;
                    }

                    float shade = 1f - Mathf.Clamp01(minDist) * 0.18f;

                    // Finger grooves across the knuckles
                    for (int k = 0; k < 3; k++)
                    {
                        float sepX = (knuckleCxs[k] + knuckleCxs[k + 1]) * 0.5f;
                        float sepD = Mathf.Abs(px - sepX) / (s * 0.012f);
                        bool inKnuckle = py > bodyCy && py < knuckleY + knuckleRy * 0.5f;
                        if (sepD < 1f && inKnuckle)
                            shade -= (1f - sepD) * 0.10f;
                    }

                    // Thumb crease — horizontal line separating thumb from fingers
                    float thumbTopY = thumbCy + thumbRy * 0.85f;
                    float thumbCreaseD = Mathf.Abs(py - thumbTopY) / (s * 0.014f);
                    bool inThumbX = px > thumbCx - thumbRx * 0.75f && px < thumbCx + thumbRx * 0.75f;
                    if (thumbCreaseD < 1f && inThumbX)
                        shade -= (1f - thumbCreaseD) * 0.08f;

                    // Curl crease — where fingers fold under
                    float curlCreaseY = bodyCy + bodyRy * 0.15f;
                    float curlCreaseD = Mathf.Abs(py - curlCreaseY) / (s * 0.012f);
                    bool inCurlX = px > bodyCx - bodyRx * 0.7f && px < bodyCx + bodyRx * 0.7f;
                    if (curlCreaseD < 1f && inCurlX)
                        shade -= (1f - curlCreaseD) * 0.06f;

                    // Wrist crease — where the wrist meets the hand
                    float wCreaseD = Mathf.Abs(py - wristCreaseY) / (s * 0.016f);
                    bool inWristX = px > wristCx - wristRx * 0.9f && px < wristCx + wristRx * 0.9f;
                    if (wCreaseD < 1f && inWristX)
                        shade -= (1f - wCreaseD) * 0.10f;

                    shade = Mathf.Clamp01(shade);
                    tex.SetPixel(px, py, new Color(
                        skinBase.r * shade, skinBase.g * shade, skinBase.b * shade, alpha));
                }
            }

            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GenerateGradientTexture(int width, int height, Color bottom, Color top)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                Color c = Color.Lerp(bottom, top, (float)y / height);
                for (int x = 0; x < width; x++)
                    tex.SetPixel(x, y, c);
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static Texture2D GenerateSoftCircleTexture(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha = alpha * alpha;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
