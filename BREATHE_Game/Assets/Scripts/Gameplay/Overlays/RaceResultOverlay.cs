using System;
using System.Collections.Generic;
using UnityEngine;
using Breathe.Audio;
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

        // Styles (rebuilt when screen height changes so fonts track resolution)
        private bool _stylesReady;
        private int _stylesBuiltForScreenH;
        private GUIStyle _dimScrim;
        private GUIStyle _panelBg, _headerBar, _divider;
        private GUIStyle _titleStyle, _heroStatValue, _heroSubStyle;
        private GUIStyle _primaryStatValue, _primaryStatLabel;
        private GUIStyle _secondaryValue, _secondaryLabel;
        private GUIStyle _feedbackStyle, _quoteStyle;
        private GUIStyle _sectionHeaderStyle;
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

                case Phase.Shown:
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

        const int MaxPrimaryStatsDisplay = 9;
        const int MaxSecondaryStatsDisplay = 6;

        static float ResultUiScale()
        {
            return Mathf.Clamp(Screen.height / 900f, 0.9f, 1.55f);
        }

        private void DrawPanel()
        {
            EnsureResultStyles();
            float sc = ResultUiScale();

            float pw = Mathf.Min(Screen.width * 0.95f, 1500f);
            float ph = Mathf.Min(Screen.height * 0.93f, 1050f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;
            float pad = 26f * sc;
            float contentW = pw - pad * 2f;

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _dimScrim);
            GUI.Box(new Rect(px, py, pw, ph), "", _panelBg);

            float hdrH = 54f * sc;
            var headerStyle = BoxStyle(_accentColor);
            GUI.Box(new Rect(px, py, pw, hdrH), "", headerStyle);
            float titleBarH = 38f * sc;
            GameFont.OutlinedLabel(new Rect(px, py + (hdrH - titleBarH) * 0.5f, pw, titleBarH),
                _resultTitle.ToUpper().Replace(" ", "  "), _titleStyle);

            float bh = 58f * sc;
            float bw = Mathf.Min(240f * sc, (pw - 80f * sc) * 0.45f);
            float btnGap = 28f * sc;
            float btnMargin = 22f * sc;
            float btnY = py + ph - bh - btnMargin;
            float bx = px + (pw - bw * 2f - btnGap) * 0.5f;

            float zoneTop = py + hdrH + 10f * sc;
            float gap = 12f * sc;
            float cy = zoneTop;

            float heroH = _heroStats.Count > 0 ? 62f * sc : 0f;
            float heroSubH = _heroStats.Count > 1 ? 44f * sc : 0f;

            // --- Hero: main outcome numbers ---
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
                    float badgeX = (Screen.width + valW) * 0.5f + 8f * sc;
                    GameFont.OutlinedLabel(new Rect(badgeX, cy + 6f * sc, 90f * sc, 28f * sc), "NEW  PB", _pbBadge);
                }
                cy += heroH + gap * 0.35f;
            }

            if (_heroStats.Count > 1)
            {
                var sub = _heroStats[1];
                string subText = sub.Value.ToUpper().Replace(" ", "  ");
                GameFont.OutlinedLabel(new Rect(px, cy, pw, heroSubH), subText, _heroSubStyle);
                if (sub.IsPersonalBest)
                {
                    float valW = _heroSubStyle.CalcSize(new GUIContent(subText)).x;
                    float badgeX = (Screen.width + valW) * 0.5f + 8f * sc;
                    GameFont.OutlinedLabel(new Rect(badgeX, cy + 6f * sc, 90f * sc, 28f * sc), "NEW  PB", _pbBadge);
                }
                cy += heroSubH + gap * 0.5f;
            }

            // --- Primary: scores & key metrics (emphasis) ---
            int pShow = Mathf.Min(_primaryStats.Count, MaxPrimaryStatsDisplay);
            if (pShow > 0)
            {
                GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, 26f * sc),
                    "YOUR  RESULTS", _sectionHeaderStyle);
                cy += 28f * sc;

                float colW = contentW / 3f;
                float rowH = 72f * sc;
                int rows = Mathf.CeilToInt(pShow / 3f);
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int i = r * 3 + c;
                        if (i >= pShow) break;
                        var stat = _primaryStats[i];
                        DrawCenteredStat(px + pad + colW * c, cy, colW, stat.Value, stat.Label,
                            stat.IsPersonalBest, sc);
                    }
                    cy += rowH;
                }
                cy += gap;
            }

            // --- Personal-best / feedback (single flowing block, height from content) ---
            if (!string.IsNullOrEmpty(_personalBestMessage))
            {
                string fbText = _personalBestMessage.ToUpper().Replace(" ", "  ");
                var fbStyle = new GUIStyle(_feedbackStyle) { wordWrap = true };
                float fbH = fbStyle.CalcHeight(new GUIContent(fbText), contentW);
                fbH = Mathf.Clamp(fbH, 28f * sc, 120f * sc);
                GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, fbH), fbText, fbStyle);
                cy += fbH + gap;
            }

            // --- Secondary: session details (capped; avoid stat wall) ---
            int sShow = Mathf.Min(_secondaryStats.Count, MaxSecondaryStatsDisplay);
            if (sShow > 0)
            {
                GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, 24f * sc),
                    "SESSION  DETAILS", _sectionHeaderStyle);
                cy += 26f * sc;

                float detailColW = contentW / 3f;
                float smallRowH = 52f * sc;
                int sRows = Mathf.CeilToInt(sShow / 3f);
                for (int r = 0; r < sRows; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int i = r * 3 + c;
                        if (i >= sShow) break;
                        var stat = _secondaryStats[i];
                        DrawSmallStat(px + pad + detailColW * c, cy, detailColW,
                            stat.Value, stat.Label, sc);
                    }
                    cy += smallRowH;
                }
                cy += gap;

                int hidden = _secondaryStats.Count - MaxSecondaryStatsDisplay;
                if (hidden > 0)
                {
                    var moreStyle = new GUIStyle(_secondaryLabel)
                    {
                        fontSize = Mathf.Max(12, _secondaryLabel.fontSize),
                        fontStyle = FontStyle.Italic
                    };
                    GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, 22f * sc),
                        $"+  {hidden}  MORE  STATS", moreStyle);
                    cy += 24f * sc;
                }
                cy += gap * 0.5f;
            }

            // --- Closing flavor (celebration + quote) — one compact block; clamp to space above buttons ---
            float maxFlavorBottom = btnY - 14f * sc;
            float availForFlavor = maxFlavorBottom - cy;
            if (availForFlavor > 36f * sc)
            {
                string celebText = _celebrationTitle.ToUpper().Replace(" ", "  ");
                string quoteText = string.IsNullOrEmpty(_encouragingQuote)
                    ? ""
                    : _encouragingQuote.ToUpper().Replace(" ", "  ");
                string flavorBody = string.IsNullOrEmpty(quoteText)
                    ? $"\"{celebText}\""
                    : $"\"{celebText}\"\n{quoteText}";
                var flavorStyle = new GUIStyle(_quoteStyle)
                {
                    wordWrap = true,
                    alignment = TextAnchor.UpperCenter
                };
                float flavorH = flavorStyle.CalcHeight(new GUIContent(flavorBody), contentW);
                flavorH = Mathf.Clamp(flavorH, 32f * sc, Mathf.Min(96f * sc, availForFlavor - 4f * sc));
                GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, flavorH), flavorBody, flavorStyle);
            }

            // Buttons
            bool interactive = _phase == Phase.Shown;

            if (interactive && Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                SfxPlayer.Instance?.PlayUiMenuClick();
                BeginPopOut(() => SceneLoader.ReloadCurrentScene());
                Event.current.Use();
            }

            if (GUI.Button(new Rect(bx, btnY, bw, bh), "PLAY  AGAIN", _btnPrimary) && interactive)
            {
                SfxPlayer.Instance?.PlayUiMenuClick();
                BeginPopOut(() => SceneLoader.ReloadCurrentScene());
            }

            if (GUI.Button(new Rect(bx + bw + btnGap, btnY, bw, bh), "MAIN  MENU", _btnSecondary) && interactive)
            {
                SfxPlayer.Instance?.PlayUiMenuClick();
                BeginPopOut(() => SceneLoader.LoadMainMenu());
            }
        }

        private void DrawCenteredStat(float x, float y, float w, string value, string label, bool isPB, float sc)
        {
            string val = value.ToUpper().Replace(" ", "  ");
            string lbl = label.ToUpper().Replace(" ", "  ");
            float vh = 40f * sc;
            float lh = 24f * sc;
            GameFont.OutlinedLabel(new Rect(x, y, w, vh), val, _primaryStatValue);
            GameFont.OutlinedLabel(new Rect(x, y + vh - 6f * sc, w, lh), lbl, _primaryStatLabel);

            if (isPB)
            {
                float valW = _primaryStatValue.CalcSize(new GUIContent(val)).x;
                float badgeX = x + (w + valW) * 0.5f + 4f * sc;
                GameFont.OutlinedLabel(new Rect(badgeX, y + 4f * sc, 56f * sc, 20f * sc), "PB", _pbBadgeSmall);
            }
        }

        private void DrawSmallStat(float x, float y, float w, string value, string label, float sc)
        {
            string val = value.ToUpper().Replace(" ", "  ");
            string lbl = label.ToUpper().Replace(" ", "  ");
            float vh = 30f * sc;
            float lh = 20f * sc;
            GameFont.OutlinedLabel(new Rect(x, y, w, vh), val, _secondaryValue);
            GameFont.OutlinedLabel(new Rect(x, y + vh - 4f * sc, w, lh), lbl, _secondaryLabel);
        }

        private void EnsureResultStyles()
        {
            if (_stylesReady && _stylesBuiltForScreenH == Screen.height) return;
            _stylesReady = true;
            _stylesBuiltForScreenH = Screen.height;

            float sc = ResultUiScale();
            int S(int px) => Mathf.Max(10, Mathf.RoundToInt(px * sc));

            _dimScrim = BoxStyle(new Color(0f, 0f, 0f, 0.52f));

            _panelBg = BoxStyle(new Color(0.02f, 0.04f, 0.10f, 0.97f));
            _headerBar = BoxStyle(new Color(0.12f, 0.45f, 0.75f, 1f));
            _divider = new GUIStyle { normal = { background = Tex(new Color(1f, 1f, 1f, 0.1f)) } };

            _titleStyle = Lbl(S(38), FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _heroStatValue = Lbl(S(60), FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _heroSubStyle = Lbl(S(42), FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.9f, 0.95f, 1f));

            _primaryStatValue = Lbl(S(30), FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _primaryStatValue.clipping = TextClipping.Overflow;
            _primaryStatLabel = Lbl(S(19), FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.55f, 0.6f, 0.7f));
            _primaryStatLabel.clipping = TextClipping.Overflow;

            _secondaryValue = Lbl(S(23), FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.75f, 0.78f, 0.85f));
            _secondaryValue.clipping = TextClipping.Overflow;
            _secondaryLabel = Lbl(S(17), FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.50f, 0.53f, 0.60f));
            _secondaryLabel.clipping = TextClipping.Overflow;

            _feedbackStyle = Lbl(S(25), FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(0.8f, 0.83f, 0.9f));
            _quoteStyle = Lbl(S(22), FontStyle.Italic, TextAnchor.MiddleCenter,
                new Color(0.5f, 0.78f, 0.62f));

            _sectionHeaderStyle = Lbl(S(19), FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(0.62f, 0.68f, 0.8f));

            _pbBadge = Lbl(S(19), FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(1f, 0.85f, 0.2f));
            _pbBadgeSmall = Lbl(S(17), FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(1f, 0.85f, 0.2f));

            _btnPrimary = BtnStyle(Tex(new Color(0.18f, 0.5f, 0.85f, 1f)),
                Tex(new Color(0.22f, 0.58f, 0.95f, 1f)), Color.white, S(30));
            _btnSecondary = BtnStyle(Tex(new Color(0.15f, 0.15f, 0.22f, 1f)),
                Tex(new Color(0.22f, 0.22f, 0.32f, 1f)),
                new Color(0.75f, 0.75f, 0.82f), S(28));

            Font f = GameFont.Get();
            if (f != null)
            {
                GUIStyle[] all = {
                    _titleStyle, _heroStatValue, _heroSubStyle,
                    _primaryStatValue, _primaryStatLabel,
                    _secondaryValue, _secondaryLabel, _feedbackStyle,
                    _pbBadge, _pbBadgeSmall, _quoteStyle, _sectionHeaderStyle,
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

        private static GUIStyle BtnStyle(Texture2D normal, Texture2D hover, Color textColor, int fontSize)
        {
            var s = new GUIStyle(GUI.skin.button)
            {
                fontSize = fontSize,
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
