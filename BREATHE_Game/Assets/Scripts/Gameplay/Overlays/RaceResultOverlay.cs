using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // Race-result overlay rendered via OnGUI with bouncy pop-in / pop-out animation.
    // Shows placement, time, breath analytics, personal bests, and an encouraging quote.
    public class RaceResultOverlay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CourseManager _courseManager;
        [SerializeField] private SailboatController _playerBoat;
        [SerializeField] private AICompanionController[] _aiBoats;
        [SerializeField] private CourseMarkers _courseMarkers;
        [SerializeField] private BreathAnalytics _breathAnalytics;
        [SerializeField] private ScoreManager _scoreManager;

        [Header("Timing")]
        [SerializeField, Tooltip("Seconds boats coast before the popup appears. 0 = immediate.")]
        private float _coastDelay = 4.5f;

        [Header("Pop Animation")]
        [SerializeField] private float _popInDuration = 0.4f;
        [SerializeField] private float _popOutDuration = 0.3f;
        [SerializeField, Tooltip("Higher = bouncier overshoot.")]
        private float _popOvershoot = 1.70158f;
        [SerializeField] private float _postPopDelay = 0.7f;

        private enum Phase { Inactive, Coasting, PoppingIn, Shown, PoppingOut, PostDelay }
        private Phase _phase = Phase.Inactive;
        private float _timer;
        private float _scale;
        private float _alpha;
        private Action _pendingNav;

        // Frozen race stats (captured once at finish)
        private float _finishTime;
        private int _playerPlacement;
        private int _totalRacers;
        private float _frozenKnots;
        private float _frozenMph;
        private string _courseLayoutName;

        // Frozen breath stats
        private float _totalBreathTime;
        private float _longestBlow;
        private float _peakIntensity;
        private float _activityRatio;
        private string _breathPattern;
        private int _sustainedCount;
        private int _burstCount;
        private float _avgSustainedDuration;

        // Personal best flags
        private bool _isPBCourseTime;
        private bool _isPBBreathTime;
        private bool _isPBLongestBlow;
        private bool _isPBPeakIntensity;

        private string _encouragingQuote;

        // Styles (lazy-init)
        private bool _stylesReady;
        private GUIStyle _panelBg;
        private GUIStyle _headerBar;
        private GUIStyle _titleStyle;
        private GUIStyle _pbBadge;
        private GUIStyle _pbBadgeSmall;
        private GUIStyle _btnPrimary;
        private GUIStyle _btnSecondary;
        private GUIStyle _divider;
        private GUIStyle _quoteStyle;
        private GUIStyle _heroStatValue;
        private GUIStyle _heroTimeStyle;
        private GUIStyle _primaryStatValue;
        private GUIStyle _primaryStatLabel;
        private GUIStyle _secondaryLabel;
        private GUIStyle _secondaryValue;
        private GUIStyle _feedbackStyle;

        private void Start()
        {
            if (_breathAnalytics == null)
                _breathAnalytics = FindAnyObjectByType<BreathAnalytics>();
            if (_scoreManager == null)
                _scoreManager = ScoreManager.Instance;
        }

        private void OnEnable()
        {
            if (_courseManager != null)
                _courseManager.OnPlayerFinished += HandleFinish;
        }

        private void OnDisable()
        {
            if (_courseManager != null)
                _courseManager.OnPlayerFinished -= HandleFinish;
        }

        private void HandleFinish(float raceTime)
        {
            _finishTime = raceTime;
            _playerPlacement = CalculatePlacement();
            _totalRacers = 1 + (_aiBoats != null ? _aiBoats.Length : 0);

            float rawSpeed = _playerBoat != null ? _playerBoat.CurrentSpeed : 0f;
            _frozenKnots = WindSpeedConverter.ToKnots(rawSpeed);
            _frozenMph = WindSpeedConverter.ToMph(rawSpeed);

            _courseLayoutName = _courseMarkers != null ? _courseMarkers.ActiveLayoutName : "Unknown";

            if (_breathAnalytics != null)
            {
                _breathAnalytics.StopTracking();
                _activityRatio = _breathAnalytics.ActivityRatio;
                _breathPattern = _breathAnalytics.BreathPatternLabel;
                _sustainedCount = _breathAnalytics.SustainedSegmentCount;
                _burstCount = _breathAnalytics.BurstCount;
                _avgSustainedDuration = _breathAnalytics.AverageSustainedDuration;
            }

            if (_scoreManager != null)
            {
                _totalBreathTime = _scoreManager.TotalBreathTime;
                _longestBlow = _scoreManager.LongestSustainedBlow;
                _peakIntensity = _scoreManager.PeakBreathIntensity;

                _isPBCourseTime = _scoreManager.IsNewPersonalBest("CourseTime");
                _isPBBreathTime = _scoreManager.IsNewPersonalBest("BreathTime");
                _isPBLongestBlow = _scoreManager.IsNewPersonalBest("LongestBlow");
                _isPBPeakIntensity = _scoreManager.IsNewPersonalBest("PeakIntensity");

                _scoreManager.SavePersonalBests();
            }

            bool anyPB = _isPBCourseTime || _isPBBreathTime || _isPBLongestBlow || _isPBPeakIntensity;
            _encouragingQuote = EncouragingQuotes.GetBreathGameQuote(_playerPlacement, anyPB, _activityRatio);

            _phase = Phase.Coasting;
            _timer = 0f;
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Inactive:
                    return;

                case Phase.Coasting:
                    _timer += Time.deltaTime;
                    if (_timer >= _coastDelay)
                    {
                        _phase = Phase.PoppingIn;
                        _timer = 0f;
                        _courseMarkers?.BeginFadeOut();
                    }
                    break;

                case Phase.PoppingIn:
                    _timer += Time.deltaTime;
                    float tIn = Mathf.Clamp01(_timer / _popInDuration);
                    _scale = UIPopAnimation.EvaluateBackOut(tIn, _popOvershoot);
                    _alpha = Mathf.Clamp01(tIn * 1.6f);
                    if (tIn >= 1f)
                    {
                        _phase = Phase.Shown;
                        _scale = 1f;
                        _alpha = 1f;
                    }
                    break;

                case Phase.PoppingOut:
                    _timer += Time.deltaTime;
                    float tOut = Mathf.Clamp01(_timer / _popOutDuration);
                    float eased = UIPopAnimation.EvaluateBackIn(tOut, _popOvershoot * 0.4f);
                    _scale = Mathf.Max(0f, 1f - eased);
                    _alpha = 1f - Mathf.Clamp01(tOut * 1.4f);
                    if (tOut >= 1f)
                    {
                        _phase = Phase.PostDelay;
                        _timer = 0f;
                        _scale = 0f;
                        _alpha = 0f;
                    }
                    break;

                case Phase.PostDelay:
                    _timer += Time.deltaTime;
                    if (_timer >= _postPopDelay)
                        _pendingNav?.Invoke();
                    break;
            }
        }

        private void BeginPopOut(Action navigation)
        {
            _pendingNav = navigation;
            _phase = Phase.PoppingOut;
            _timer = 0f;
        }

        private int CalculatePlacement()
        {
            if (_aiBoats == null || _aiBoats.Length == 0) return 1;

            int ahead = 0;
            float playerY = _playerBoat != null ? _playerBoat.transform.position.y : 0f;
            foreach (var ai in _aiBoats)
            {
                if (ai != null && ai.transform.position.y > playerY)
                    ahead++;
            }
            return ahead + 1;
        }

        private void OnGUI()
        {
            if (_phase < Phase.PoppingIn) return;
            if (_phase == Phase.PostDelay) return;

            BuildStyles();

            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = GUI.matrix;

            if (_scale < 0.001f) return;

            Vector3 pivot = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            GUI.matrix = Matrix4x4.Translate(pivot)
                         * Matrix4x4.Scale(new Vector3(_scale, _scale, 1f))
                         * Matrix4x4.Translate(-pivot);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_alpha));

            DrawPanel();

            GUI.matrix = prevMatrix;
            GUI.color = prevColor;
        }

        private void DrawPanel()
        {
            float pw = Mathf.Min(Screen.width * 0.7f, 540f);
            float ph = Mathf.Min(Screen.height * 0.88f, 650f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;
            float pad = 28f;
            float contentW = pw - pad * 2f;

            GUI.Box(new Rect(px, py, pw, ph), "", _panelBg);

            float hdrH = 46f;
            GUI.Box(new Rect(px, py, pw, hdrH), "", _headerBar);
            GUI.Label(new Rect(px, py + 8f, pw, 30f), "RACE COMPLETE", _titleStyle);

            float cy = py + hdrH + 24f;

            // Placement hero
            string placeText = _playerPlacement switch
            {
                1 => "1ST PLACE",
                2 => "2ND PLACE",
                3 => "3RD PLACE",
                _ => $"{_playerPlacement}TH PLACE"
            };
            Color placeColor = _playerPlacement switch
            {
                1 => new Color(1f, 0.84f, 0f),
                2 => new Color(0.78f, 0.78f, 0.85f),
                3 => new Color(0.85f, 0.55f, 0.25f),
                _ => new Color(0.7f, 0.7f, 0.75f)
            };

            var placementStyle = new GUIStyle(_heroStatValue) { normal = { textColor = placeColor } };
            GUI.Label(new Rect(px, cy, pw, 44f), placeText, placementStyle);
            cy += 42f;

            string timeDisplay = FormatTime(_finishTime);
            GUI.Label(new Rect(px, cy, pw, 32f), timeDisplay, _heroTimeStyle);
            if (_isPBCourseTime)
            {
                float timeWidth = _heroTimeStyle.CalcSize(new GUIContent(timeDisplay)).x;
                float badgeX = (Screen.width + timeWidth) * 0.5f + 8f;
                GUI.Label(new Rect(badgeX, cy + 6f, 55f, 20f), "NEW PB!", _pbBadge);
            }
            cy += 38f;

            GUI.Label(new Rect(px + pad, cy, contentW, 24f), $"\"{_encouragingQuote}\"", _quoteStyle);
            cy += 32f;

            DrawDivider(px + pad, cy, contentW);
            cy += 18f;

            // Primary breath metrics
            float statBlockW = contentW / 3f;
            float statX = px + pad;

            DrawCenteredStat(statX, cy, statBlockW, $"{_totalBreathTime:F1}s", "BREATH TIME", _isPBBreathTime);
            DrawCenteredStat(statX + statBlockW, cy, statBlockW, $"{_longestBlow:F1}s", "LONGEST BLOW", _isPBLongestBlow);
            DrawCenteredStat(statX + statBlockW * 2f, cy, statBlockW, $"{_peakIntensity * 100f:F0}%", "PEAK", _isPBPeakIntensity);

            cy += 70f;

            DrawDivider(px + pad, cy, contentW);
            cy += 16f;

            // Session feedback
            string encouragement = GetEncouragementMessage();
            var feedbackStyle = new GUIStyle(_feedbackStyle) { wordWrap = true };
            GUI.Label(new Rect(px + pad, cy, contentW, 50f), encouragement, feedbackStyle);
            cy += 52f;

            DrawDivider(px + pad, cy, contentW);
            cy += 14f;

            // Secondary details
            float detailY = cy;
            float detailColW = contentW / 3f;

            DrawSmallStat(px + pad, detailY, detailColW, $"{_frozenKnots:F1} kts", "Speed");
            DrawSmallStat(px + pad + detailColW, detailY, detailColW, _breathPattern, "Pattern");

            string activityGrade = _activityRatio switch
            {
                >= 0.7f => "Excellent",
                >= 0.5f => "Good",
                >= 0.3f => "Fair",
                _ => "Low"
            };
            DrawSmallStat(px + pad + detailColW * 2f, detailY, detailColW, activityGrade, "Activity");

            cy += 44f;

            detailY = cy;
            DrawSmallStat(px + pad, detailY, detailColW, $"{_sustainedCount}", "Breaths");
            DrawSmallStat(px + pad + detailColW, detailY, detailColW, $"{_avgSustainedDuration:F1}s", "Avg Length");
            DrawSmallStat(px + pad + detailColW * 2f, detailY, detailColW, _courseLayoutName, "Course");

            // Buttons
            float btnY = py + ph - 62f;
            float bw = 150f;
            float bh = 42f;
            float gap = 20f;
            float bx = px + (pw - bw * 2f - gap) * 0.5f;

            bool interactive = _phase == Phase.Shown;

            if (GUI.Button(new Rect(bx, btnY, bw, bh), "PLAY AGAIN", _btnPrimary) && interactive)
            {
                BeginPopOut(() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
            }

            if (GUI.Button(new Rect(bx + bw + gap, btnY, bw, bh), "QUIT", _btnSecondary) && interactive)
            {
                BeginPopOut(() =>
                {
                    if (GameStateManager.Instance != null)
                        GameStateManager.Instance.TransitionTo(GameState.MainMenu);
                    else
                        SceneManager.LoadScene(0);
                });
            }
        }

        private void DrawCenteredStat(float x, float y, float w, string value, string label, bool isPB)
        {
            GUI.Label(new Rect(x, y, w, 32f), value, _primaryStatValue);
            GUI.Label(new Rect(x, y + 30f, w, 18f), label, _primaryStatLabel);

            if (isPB)
            {
                float valueWidth = _primaryStatValue.CalcSize(new GUIContent(value)).x;
                float badgeX = x + (w + valueWidth) * 0.5f + 4f;
                GUI.Label(new Rect(badgeX, y + 4f, 50f, 16f), "PB!", _pbBadgeSmall);
            }
        }

        private void DrawSmallStat(float x, float y, float w, string value, string label)
        {
            GUI.Label(new Rect(x, y, w, 18f), value, _secondaryValue);
            GUI.Label(new Rect(x, y + 18f, w, 14f), label, _secondaryLabel);
        }

        private string GetEncouragementMessage()
        {
            if (_isPBCourseTime && _isPBBreathTime)
                return "Outstanding! You set new personal bests for both speed and breath control!";
            if (_isPBCourseTime)
                return "Great racing! You beat your personal best time!";
            if (_isPBLongestBlow)
                return "Impressive breath control! Your longest sustained blow is a new record!";
            if (_activityRatio >= 0.7f)
                return "Excellent breathing consistency throughout the race!";
            if (_sustainedCount >= 5)
                return "Good sustained breathing! Keep practicing for longer breaths.";
            if (_burstCount > _sustainedCount * 2)
                return "Try taking longer, steadier breaths for better sail power.";
            return "Keep practicing! Focus on steady, sustained breathing.";
        }

        private void DrawDivider(float x, float y, float w)
        {
            GUI.Box(new Rect(x, y, w, 1f), "", _divider);
        }

        private static string FormatTime(float seconds)
        {
            int min = Mathf.FloorToInt(seconds / 60f);
            float sec = seconds % 60f;
            return min > 0 ? $"{min}:{sec:00.0}" : $"{sec:F1}s";
        }

        private void BuildStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _panelBg = BoxStyle(new Color(0.02f, 0.04f, 0.10f, 0.97f));
            _headerBar = BoxStyle(new Color(0.12f, 0.45f, 0.75f, 1f));
            _divider = new GUIStyle { normal = { background = Tex(new Color(1f, 1f, 1f, 0.1f)) } };

            _titleStyle = Lbl(22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _heroStatValue = Lbl(38, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _heroTimeStyle = Lbl(26, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.9f, 0.95f, 1f));

            _primaryStatValue = Lbl(22, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _primaryStatLabel = Lbl(10, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.55f, 0.6f, 0.7f));

            _secondaryValue = Lbl(13, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.65f, 0.68f, 0.75f));
            _secondaryLabel = Lbl(9, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.45f, 0.48f, 0.55f));

            _feedbackStyle = Lbl(13, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.8f, 0.83f, 0.9f));

            _pbBadge = Lbl(11, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(1f, 0.85f, 0.2f));
            _pbBadgeSmall = Lbl(9, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(1f, 0.85f, 0.2f));

            _quoteStyle = Lbl(13, FontStyle.Italic, TextAnchor.MiddleCenter,
                new Color(0.5f, 0.78f, 0.62f));

            _btnPrimary = BtnStyle(Tex(new Color(0.18f, 0.5f, 0.85f, 1f)),
                Tex(new Color(0.22f, 0.58f, 0.95f, 1f)), Color.white);

            _btnSecondary = BtnStyle(Tex(new Color(0.15f, 0.15f, 0.22f, 1f)),
                Tex(new Color(0.22f, 0.22f, 0.32f, 1f)),
                new Color(0.75f, 0.75f, 0.82f));
        }

        private static GUIStyle BoxStyle(Color col)
        {
            var s = new GUIStyle(GUI.skin.box) { normal = { background = Tex(col) } };
            return s;
        }

        private static GUIStyle Lbl(int size, FontStyle style, TextAnchor anchor, Color color)
        {
            var s = new GUIStyle(GUI.skin.label)
            {
                fontSize = size,
                fontStyle = style,
                alignment = anchor
            };
            s.normal.textColor = color;
            return s;
        }

        private static GUIStyle BtnStyle(Texture2D normal, Texture2D hover, Color textColor)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            s.normal.background = normal;
            s.normal.textColor = textColor;
            s.hover.background = hover;
            s.hover.textColor = Color.white;
            s.active.background = hover;
            s.active.textColor = Color.white;
            return s;
        }

        private static Texture2D Tex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
