using System;
using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // Data-driven result overlay rendered via OnGUI with bouncy pop-in/pop-out.
    // Reads all display data from the active IMinigame — zero game-specific knowledge.
    // Stats are laid out by StatTier: Hero (large), Primary (medium 3-col), Secondary (small 3-col).
    // Header bar color is themed from MinigameDefinition.CardColor.
    public class RaceResultOverlay : MonoBehaviour
    {
        [Header("Timing")]
        [SerializeField, Tooltip("Delay before the overlay appears after the game ends. 0 = immediate pop-in.")]
        private float _showDelay = 0f;

        [Header("Pop Animation")]
        [SerializeField] private float _popInDuration = 0.4f;
        [SerializeField] private float _popOutDuration = 0.3f;
        [SerializeField, Tooltip("Higher = bouncier overshoot.")]
        private float _popOvershoot = 1.70158f;
        [SerializeField] private float _postPopDelay = 0.7f;

        // Fires when the overlay begins appearing (after show delay).
        // Scene objects (e.g., CourseMarkers) can subscribe to trigger fade-outs.
        public static event Action OnResultOverlayShowing;

        private enum Phase { Inactive, Waiting, PoppingIn, Shown, PoppingOut, PostDelay }
        private Phase _phase = Phase.Inactive;
        private float _timer;
        private float _scale;
        private float _alpha;
        private Action _pendingNav;

        // Frozen data from IMinigame (captured once on celebration entry)
        private string _resultTitle;
        private string _celebrationTitle;
        private string _personalBestMessage;
        private string _encouragingQuote;
        private Color _accentColor;

        private List<MinigameStat> _heroStats = new();
        private List<MinigameStat> _primaryStats = new();
        private List<MinigameStat> _secondaryStats = new();

        // Styles (lazy-init)
        private bool _stylesReady;
        private GUIStyle _panelBg, _headerBar, _divider;
        private GUIStyle _titleStyle, _heroStatValue, _heroSubStyle;
        private GUIStyle _primaryStatValue, _primaryStatLabel;
        private GUIStyle _secondaryValue, _secondaryLabel;
        private GUIStyle _feedbackStyle, _quoteStyle;
        private GUIStyle _pbBadge, _pbBadgeSmall;
        private GUIStyle _btnPrimary, _btnSecondary;

        private bool _subscribed;

        private void Start()
        {
            EnsureSubscribed();
        }

        private void OnEnable()
        {
            EnsureSubscribed();
        }

        private void OnDisable()
        {
            if (_subscribed && GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnStateChanged -= HandleStateChange;
                _subscribed = false;
            }
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            if (GameStateManager.Instance == null) return;
            GameStateManager.Instance.OnStateChanged += HandleStateChange;
            _subscribed = true;
        }

        private void HandleStateChange(GameState state)
        {
            if (state != GameState.Celebration) return;
            Debug.Log("[ResultOverlay] Celebration state detected — capturing stats.");
            CaptureMinigameData();
            _phase = Phase.Waiting;
            _timer = 0f;
        }

        private void CaptureMinigameData()
        {
            var mgr = MinigameManager.Instance;
            IMinigame minigame = mgr != null ? mgr.ActiveMinigame : null;
            MinigameDefinition def = mgr != null ? mgr.SelectedDefinition : null;

            _resultTitle = minigame?.GetResultTitle() ?? "COMPLETE";
            _celebrationTitle = minigame?.GetCelebrationTitle() ?? "WELL  DONE!";
            _personalBestMessage = minigame?.GetPersonalBestMessage() ?? "";
            _accentColor = def != null ? def.CardColor : new Color(0.12f, 0.45f, 0.75f, 1f);

            _heroStats.Clear();
            _primaryStats.Clear();
            _secondaryStats.Clear();

            MinigameStat[] stats = minigame?.GetEndStats();
            if (stats != null)
            {
                bool anyPB = false;
                float activityRatio = 0f;

                foreach (var s in stats)
                {
                    if (s.IsPersonalBest) anyPB = true;
                    if (s.Label == "Activity")
                    {
                        activityRatio = s.Value switch
                        {
                            "Excellent" => 0.8f,
                            "Good" => 0.6f,
                            "Fair" => 0.4f,
                            _ => 0.2f
                        };
                    }

                    switch (s.Tier)
                    {
                        case StatTier.Hero: _heroStats.Add(s); break;
                        case StatTier.Primary: _primaryStats.Add(s); break;
                        default: _secondaryStats.Add(s); break;
                    }
                }

                _encouragingQuote = EncouragingQuotes.GetMinigameQuote(anyPB, activityRatio);
            }
            else
            {
                _encouragingQuote = EncouragingQuotes.GetRandomQuote();
            }
        }

        private void Update()
        {
            switch (_phase)
            {
                case Phase.Inactive: return;

                case Phase.Waiting:
                    _timer += Time.deltaTime;
                    if (_timer >= _showDelay)
                    {
                        _phase = Phase.PoppingIn;
                        _timer = 0f;
                        OnResultOverlayShowing?.Invoke();
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

        private void OnGUI()
        {
            if (_phase < Phase.PoppingIn || _phase == Phase.PostDelay) return;

            BuildStyles();

            if (_scale < 0.001f) return;

            Color prevColor = GUI.color;
            Matrix4x4 prevMatrix = GUI.matrix;

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
            float pw = Mathf.Min(Screen.width * 0.85f, 720f);
            float ph = Mathf.Min(Screen.height * 0.94f, 820f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;
            float pad = 28f;
            float contentW = pw - pad * 2f;

            GUI.Box(new Rect(px, py, pw, ph), "", _panelBg);

            // Header bar (themed by minigame accent color)
            float hdrH = 50f;
            var headerStyle = BoxStyle(_accentColor);
            GUI.Box(new Rect(px, py, pw, hdrH), "", headerStyle);
            GameFont.OutlinedLabel(new Rect(px, py + 10f, pw, 34f),
                _resultTitle.ToUpper().Replace(" ", "  "), _titleStyle);

            // Buttons pinned at bottom
            float bh = 55f;
            float bw = 210f;
            float btnGap = 24f;
            float btnMargin = 28f;
            float btnY = py + ph - bh - btnMargin;
            float bx = px + (pw - bw * 2f - btnGap) * 0.5f;

            float zoneTop = py + hdrH;
            float zoneBottom = btnY - 12f;
            float zoneH = zoneBottom - zoneTop;

            // Compute layout heights based on content
            float heroH = _heroStats.Count > 0 ? 55f : 0f;
            float heroSubH = _heroStats.Count > 1 ? 40f : 0f;
            float celebH = 40f;
            float quoteH = 30f;
            float primaryH = _primaryStats.Count > 0 ? 60f : 0f;
            float feedbackH = !string.IsNullOrEmpty(_personalBestMessage) ? 50f : 0f;
            int secondaryRows = Mathf.CeilToInt(_secondaryStats.Count / 3f);
            float secondaryH = secondaryRows * 48f;

            float totalH = heroH + heroSubH + celebH + quoteH + primaryH + feedbackH + secondaryH;
            float gap = Mathf.Max(8f, (zoneH - totalH) / 8f);

            float cy = zoneTop + gap;

            // Hero stats (displayed very large)
            if (_heroStats.Count > 0)
            {
                var hero = _heroStats[0];
                Color heroColor = hero.IsPersonalBest
                    ? new Color(1f, 0.84f, 0f)
                    : Color.white;
                var heroStyle = new GUIStyle(_heroStatValue) { normal = { textColor = heroColor } };
                string heroText = hero.Value.ToUpper().Replace(" ", "  ");
                GameFont.OutlinedLabel(new Rect(px, cy, pw, heroH), heroText, heroStyle);

                if (hero.IsPersonalBest)
                {
                    float valW = _heroStatValue.CalcSize(new GUIContent(heroText)).x;
                    float badgeX = (Screen.width + valW) * 0.5f + 8f;
                    GameFont.OutlinedLabel(new Rect(badgeX, cy + 6f, 80f, 24f), "NEW  PB", _pbBadge);
                }
                cy += heroH + gap * 0.3f;
            }

            // Hero sub-stat (second hero if present, e.g., time under placement)
            if (_heroStats.Count > 1)
            {
                var sub = _heroStats[1];
                string subText = sub.Value.ToUpper().Replace(" ", "  ");
                GameFont.OutlinedLabel(new Rect(px, cy, pw, heroSubH), subText, _heroSubStyle);
                if (sub.IsPersonalBest)
                {
                    float valW = _heroSubStyle.CalcSize(new GUIContent(subText)).x;
                    float badgeX = (Screen.width + valW) * 0.5f + 8f;
                    GameFont.OutlinedLabel(new Rect(badgeX, cy + 6f, 80f, 24f), "NEW  PB", _pbBadge);
                }
                cy += heroSubH + gap * 0.5f;
            }

            // Celebration title
            string celebText = _celebrationTitle.ToUpper().Replace(" ", "  ");
            GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, celebH),
                $"\"{celebText}\"", _quoteStyle);
            cy += celebH + gap * 0.5f;

            // Encouraging quote
            string quoteText = _encouragingQuote.ToUpper().Replace(" ", "  ");
            var quoteSmall = new GUIStyle(_quoteStyle) { fontSize = 20 };
            GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, quoteH), quoteText, quoteSmall);
            cy += quoteH + gap;

            // Primary stats (3-column layout)
            if (_primaryStats.Count > 0)
            {
                DrawDivider(px + pad, cy, contentW);
                cy += gap * 0.5f;

                int cols = Mathf.Min(_primaryStats.Count, 3);
                float colW = contentW / cols;
                for (int i = 0; i < _primaryStats.Count && i < 3; i++)
                {
                    var stat = _primaryStats[i];
                    string val = stat.Value.ToUpper().Replace(" ", "  ");
                    string lbl = stat.Label.ToUpper().Replace(" ", "  ");
                    DrawCenteredStat(px + pad + colW * i, cy, colW, val, lbl, stat.IsPersonalBest);
                }
                cy += primaryH + gap;

                // Overflow primary stats into a second row
                if (_primaryStats.Count > 3)
                {
                    int extra = _primaryStats.Count - 3;
                    int extraCols = Mathf.Min(extra, 3);
                    float extraColW = contentW / extraCols;
                    for (int i = 3; i < _primaryStats.Count && i < 6; i++)
                    {
                        var stat = _primaryStats[i];
                        string val = stat.Value.ToUpper().Replace(" ", "  ");
                        string lbl = stat.Label.ToUpper().Replace(" ", "  ");
                        DrawCenteredStat(px + pad + extraColW * (i - 3), cy, extraColW,
                            val, lbl, stat.IsPersonalBest);
                    }
                    cy += primaryH + gap;
                }
            }

            // Feedback / personal best message
            if (!string.IsNullOrEmpty(_personalBestMessage))
            {
                DrawDivider(px + pad, cy, contentW);
                cy += gap * 0.5f;

                string fbText = _personalBestMessage.ToUpper().Replace(" ", "  ");
                var fbStyle = new GUIStyle(_feedbackStyle) { wordWrap = true };
                GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, feedbackH), fbText, fbStyle);
                cy += feedbackH + gap;
            }

            // Secondary stats (3-column rows)
            if (_secondaryStats.Count > 0)
            {
                DrawDivider(px + pad, cy, contentW);
                cy += gap * 0.5f;

                float detailColW = contentW / 3f;
                for (int i = 0; i < _secondaryStats.Count; i++)
                {
                    int col = i % 3;
                    if (col == 0 && i > 0) cy += 48f + gap * 0.3f;

                    var stat = _secondaryStats[i];
                    string val = stat.Value.ToUpper().Replace(" ", "  ");
                    string lbl = stat.Label.ToUpper().Replace(" ", "  ");
                    DrawSmallStat(px + pad + detailColW * col, cy, detailColW, val, lbl);
                }
            }

            // Buttons
            bool interactive = _phase == Phase.Shown;

            if (interactive && Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                BeginPopOut(() => SceneLoader.ReloadCurrentScene());
                Event.current.Use();
            }

            if (GUI.Button(new Rect(bx, btnY, bw, bh), "PLAY  AGAIN", _btnPrimary) && interactive)
                BeginPopOut(() => SceneLoader.ReloadCurrentScene());

            if (GUI.Button(new Rect(bx + bw + btnGap, btnY, bw, bh), "MAIN  MENU", _btnSecondary) && interactive)
                BeginPopOut(() => SceneLoader.LoadMainMenu());
        }

        private void DrawCenteredStat(float x, float y, float w, string value, string label, bool isPB)
        {
            GameFont.OutlinedLabel(new Rect(x, y, w, 36f), value, _primaryStatValue);
            GameFont.OutlinedLabel(new Rect(x, y + 34f, w, 22f), label, _primaryStatLabel);

            if (isPB)
            {
                float valW = _primaryStatValue.CalcSize(new GUIContent(value)).x;
                float badgeX = x + (w + valW) * 0.5f + 4f;
                GameFont.OutlinedLabel(new Rect(badgeX, y + 4f, 50f, 16f), "PB", _pbBadgeSmall);
            }
        }

        private void DrawSmallStat(float x, float y, float w, string value, string label)
        {
            GameFont.OutlinedLabel(new Rect(x, y, w, 26f), value, _secondaryValue);
            GameFont.OutlinedLabel(new Rect(x, y + 24f, w, 20f), label, _secondaryLabel);
        }

        private void DrawDivider(float x, float y, float w)
        {
            GUI.Box(new Rect(x, y, w, 1f), "", _divider);
        }

        private void BuildStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _panelBg = BoxStyle(new Color(0.02f, 0.04f, 0.10f, 0.97f));
            _headerBar = BoxStyle(new Color(0.12f, 0.45f, 0.75f, 1f));
            _divider = new GUIStyle { normal = { background = Tex(new Color(1f, 1f, 1f, 0.1f)) } };

            _titleStyle = Lbl(36, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _heroStatValue = Lbl(58, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _heroSubStyle = Lbl(40, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.9f, 0.95f, 1f));

            _primaryStatValue = Lbl(34, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _primaryStatLabel = Lbl(18, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.55f, 0.6f, 0.7f));

            _secondaryValue = Lbl(22, FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.75f, 0.78f, 0.85f));
            _secondaryValue.clipping = TextClipping.Overflow;
            _secondaryLabel = Lbl(16, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.50f, 0.53f, 0.60f));
            _secondaryLabel.clipping = TextClipping.Overflow;

            _feedbackStyle = Lbl(24, FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.8f, 0.83f, 0.9f));
            _quoteStyle = Lbl(24, FontStyle.Italic, TextAnchor.MiddleCenter,
                new Color(0.5f, 0.78f, 0.62f));

            _pbBadge = Lbl(18, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(1f, 0.85f, 0.2f));
            _pbBadgeSmall = Lbl(16, FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(1f, 0.85f, 0.2f));

            _btnPrimary = BtnStyle(Tex(new Color(0.18f, 0.5f, 0.85f, 1f)),
                Tex(new Color(0.22f, 0.58f, 0.95f, 1f)), Color.white);
            _btnSecondary = BtnStyle(Tex(new Color(0.15f, 0.15f, 0.22f, 1f)),
                Tex(new Color(0.22f, 0.22f, 0.32f, 1f)),
                new Color(0.75f, 0.75f, 0.82f));

            Font f = GameFont.Get();
            if (f != null)
            {
                GUIStyle[] all = {
                    _titleStyle, _heroStatValue, _heroSubStyle,
                    _primaryStatValue, _primaryStatLabel,
                    _secondaryValue, _secondaryLabel, _feedbackStyle,
                    _pbBadge, _pbBadgeSmall, _quoteStyle,
                    _btnPrimary, _btnSecondary
                };
                foreach (var s in all)
                    if (s != null) s.font = f;
            }
        }

        private static GUIStyle BoxStyle(Color col)
        {
            return new GUIStyle(GUI.skin.box) { normal = { background = Tex(col) } };
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
                fontSize = 28,
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
