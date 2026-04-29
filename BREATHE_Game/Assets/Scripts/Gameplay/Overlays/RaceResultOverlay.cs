using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
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
            _accentColor = def != null ? def.CardColor : MenuVisualTheme.ResultAccentFallback;

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
                    {
                        var nav = _pendingNav;
                        _pendingNav = null;
                        _phase = Phase.Inactive;
                        nav?.Invoke();
                    }

                    break;
            }
        }

        private void BeginPopOut(Action navigation)
        {
            _pendingNav = navigation;
            _phase = Phase.PoppingOut;
            _timer = 0f;
        }

        static void NavigateReloadCurrentAfterFade()
        {
            string name = SceneManager.GetActiveScene().name;
            var c = ScreenFadeCoordinator.Instance;
            if (c != null)
                c.FadeToBlackThenLoadScene(name);
            else
                SceneLoader.ReloadCurrentScene();
        }

        static void NavigateMainMenuAfterFade()
        {
            var c = ScreenFadeCoordinator.Instance;
            if (c != null)
                c.FadeToBlackThenLoadScene(SceneLoader.MainMenuScene);
            else
                SceneLoader.LoadMainMenu();
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

            float pw = Mathf.Min(Screen.width * 0.75f, 1100f);
            float ph = Mathf.Min(Screen.height * 0.84f, 960f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;
            float pad = 36f * sc;
            float contentW = pw - pad * 2f;

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _dimScrim);
            GUI.Box(new Rect(px, py, pw, ph), "", _panelBg);

            float hdrH = 54f * sc;
            var headerStyle = BoxStyle(_accentColor);
            GUI.Box(new Rect(px, py, pw, hdrH), "", headerStyle);
            float titleBarH = 38f * sc;
            GameFont.OutlinedLabel(new Rect(px, py + (hdrH - titleBarH) * 0.5f, pw, titleBarH),
                GameFont.ExpandPronunciationHintsForPixelFont(_resultTitle).ToUpperInvariant().Replace(" ", "  "),
                _titleStyle);

            float bh = 58f * sc;
            float bw = Mathf.Min(240f * sc, (pw - 80f * sc) * 0.45f);
            float btnGap = 32f * sc;
            float btnMargin = 24f * sc;
            float btnY = py + ph - bh - btnMargin;
            float bx = px + (pw - bw * 2f - btnGap) * 0.5f;
            // Stats and labels must not extend below this Y (above PLAY AGAIN / MAIN MENU).
            float contentFooterTop = btnY - 44f * sc;

            float zoneTop = py + hdrH + 14f * sc;
            float gap = 20f * sc;
            float cy = zoneTop;

            float heroH = _heroStats.Count > 0 ? 68f * sc : 0f;
            float heroSubH = _heroStats.Count > 1 ? 50f * sc : 0f;

            // --- Hero: main outcome numbers ---
            if (_heroStats.Count > 0)
            {
                var hero = _heroStats[0];
                Color heroColor = hero.IsPersonalBest
                    ? new Color(1f, 0.84f, 0f)
                    : Color.white;
                var heroStyle = new GUIStyle(_heroStatValue) { normal = { textColor = heroColor } };
                string heroText = GameFont.ExpandPronunciationHintsForPixelFont(hero.Value)
                    .ToUpperInvariant()
                    .Replace(" ", "  ");
                GameFont.OutlinedLabel(new Rect(px, cy, pw, heroH), heroText, heroStyle);

                if (hero.IsPersonalBest)
                {
                    float valW = _heroStatValue.CalcSize(new GUIContent(heroText)).x;
                    float badgeX = (Screen.width + valW) * 0.5f + 8f * sc;
                    GameFont.OutlinedLabel(new Rect(badgeX, cy + 6f * sc, 90f * sc, 28f * sc), "NEW  PB", _pbBadge);
                }
                cy += heroH + gap * 0.85f;
            }

            if (_heroStats.Count > 1)
            {
                var sub = _heroStats[1];
                string subText = GameFont.ExpandPronunciationHintsForPixelFont(sub.Value)
                    .ToUpperInvariant()
                    .Replace(" ", "  ");
                GameFont.OutlinedLabel(new Rect(px, cy, pw, heroSubH), subText, _heroSubStyle);
                if (sub.IsPersonalBest)
                {
                    float valW = _heroSubStyle.CalcSize(new GUIContent(subText)).x;
                    float badgeX = (Screen.width + valW) * 0.5f + 8f * sc;
                    GameFont.OutlinedLabel(new Rect(badgeX, cy + 6f * sc, 90f * sc, 28f * sc), "NEW  PB", _pbBadge);
                }
                cy += heroSubH + gap * 0.75f;
            }

            // --- Primary: scores & key metrics (emphasis) ---
            int pShow = Mathf.Min(_primaryStats.Count, MaxPrimaryStatsDisplay);
            if (pShow > 0)
            {
                // Thin divider line above section
                float divH = 2f * sc;
                GUI.DrawTexture(new Rect(px + pad + contentW * 0.15f, cy, contentW * 0.7f, divH), _divider.normal.background);
                cy += divH + 10f * sc;

                GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, 28f * sc),
                    "YOUR  RESULTS", _sectionHeaderStyle);
                cy += 36f * sc;

                float colGutter = 28f * sc;
                float colW = (contentW - colGutter * 2f) / 3f;
                float rowH = 96f * sc;
                int rows = Mathf.CeilToInt(pShow / 3f);
                for (int r = 0; r < rows; r++)
                {
                    for (int c = 0; c < 3; c++)
                    {
                        int i = r * 3 + c;
                        if (i >= pShow) break;
                        var stat = _primaryStats[i];
                        float colX = px + pad + c * (colW + colGutter);
                        DrawCenteredStat(colX, cy, colW, stat.Value, stat.Label,
                            stat.IsPersonalBest, sc);
                    }
                    cy += rowH + 6f * sc;
                }
                cy += gap * 0.45f;
            }

            // --- Personal-best / feedback (single flowing block, height from content) ---
            if (!string.IsNullOrEmpty(_personalBestMessage))
            {
                cy += 8f * sc;
                string fbText = GameFont.ExpandPronunciationHintsForPixelFont(_personalBestMessage)
                    .ToUpperInvariant()
                    .Replace(" ", "  ");
                var fbStyle = new GUIStyle(_feedbackStyle) { wordWrap = true };
                float fbH = fbStyle.CalcHeight(new GUIContent(fbText), contentW);
                fbH = Mathf.Clamp(fbH, 32f * sc, 110f * sc);
                GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, fbH), fbText, fbStyle);
                cy += fbH + gap * 0.55f;
            }

            // --- Secondary: session details — row height matched to DrawSmallStat; rows capped so nothing sits under buttons ---
            int eligibleSecondaries =
                Mathf.Min(_secondaryStats.Count, MaxSecondaryStatsDisplay);
            if (eligibleSecondaries > 0)
            {
                const float smallRowContentH = 42f + 7f + 22f;
                float rowStep = smallRowContentH * sc + 2f * sc;
                float sectionOverheadBeforeGrid = (2f + 10f + 34f) * sc;
                float availForSecondaryGrid =
                    Mathf.Max(0f, contentFooterTop - cy - sectionOverheadBeforeGrid);
                int rowsWanted = Mathf.CeilToInt(eligibleSecondaries / 3f);
                int maxRowsFit = Mathf.Max(0,
                    Mathf.FloorToInt(availForSecondaryGrid / Mathf.Max(rowStep, 0.001f)));
                int sRows = Mathf.Min(rowsWanted, maxRowsFit);
                int sShow = Mathf.Min(eligibleSecondaries, Mathf.Max(0, sRows * 3));

                if (sShow > 0)
                {
                    float divH2 = 2f * sc;
                    GUI.DrawTexture(new Rect(px + pad + contentW * 0.15f, cy, contentW * 0.7f, divH2),
                        _divider.normal.background);
                    cy += divH2 + 10f * sc;

                    GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, 28f * sc),
                        "SESSION  DETAILS", _sectionHeaderStyle);
                    cy += 34f * sc;

                    float detailGutter = 28f * sc;
                    float detailColW = (contentW - detailGutter * 2f) / 3f;
                    for (int r = 0; r < sRows; r++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            int i = r * 3 + c;
                            if (i >= sShow) break;
                            var stat = _secondaryStats[i];
                            float colX = px + pad + c * (detailColW + detailGutter);
                            DrawSmallStat(colX, cy, detailColW,
                                stat.Value, stat.Label, sc);
                        }
                        cy += rowStep;
                    }
                    cy += gap * 0.2f;

                    int hidden = _secondaryStats.Count - sShow;
                    if (hidden > 0)
                    {
                        var moreStyle = new GUIStyle(_secondaryLabel)
                        {
                            fontSize = Mathf.Max(12, _secondaryLabel.fontSize),
                            fontStyle = FontStyle.Italic
                        };
                        GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, 22f * sc),
                            $"+  {hidden}  MORE  STATS", moreStyle);
                        cy += 20f * sc;
                    }

                    cy += gap * 0.25f;
                }
            }

            // --- Closing flavor (celebration + quote) — clamp to space above buttons ---
            cy += 6f * sc;
            float maxFlavorBottom = contentFooterTop - 8f * sc;
            float availForFlavor = maxFlavorBottom - cy;
            if (availForFlavor > 40f * sc)
            {
                string celebText = GameFont.ExpandPronunciationHintsForPixelFont(_celebrationTitle)
                    .ToUpperInvariant()
                    .Replace(" ", "  ");
                string quoteText = string.IsNullOrEmpty(_encouragingQuote)
                    ? ""
                    : GameFont.ExpandPronunciationHintsForPixelFont(_encouragingQuote)
                        .ToUpperInvariant()
                        .Replace(" ", "  ");
                string flavorBody = string.IsNullOrEmpty(quoteText)
                    ? $"\"{celebText}\""
                    : $"\"{celebText}\"\n\n{quoteText}";
                var flavorStyle = new GUIStyle(_quoteStyle)
                {
                    wordWrap = true,
                    alignment = TextAnchor.UpperCenter,
                    richText = false
                };
                float flavorH = flavorStyle.CalcHeight(new GUIContent(flavorBody), contentW);
                flavorH = Mathf.Clamp(flavorH, 48f * sc, Mathf.Min(160f * sc, availForFlavor - 8f * sc));
                GameFont.OutlinedLabel(new Rect(px + pad, cy, contentW, flavorH), flavorBody, flavorStyle);
            }

            // Buttons
            bool interactive = _phase == Phase.Shown;

            if (interactive && Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                SfxPlayer.Instance?.PlayUiMenuClick();
                BeginPopOut(NavigateReloadCurrentAfterFade);
                Event.current.Use();
            }

            if (GUI.Button(new Rect(bx, btnY, bw, bh), "PLAY  AGAIN", _btnPrimary) && interactive)
            {
                SfxPlayer.Instance?.PlayUiMenuClick();
                BeginPopOut(NavigateReloadCurrentAfterFade);
            }

            if (GUI.Button(new Rect(bx + bw + btnGap, btnY, bw, bh), "MAIN  MENU", _btnSecondary) && interactive)
            {
                SfxPlayer.Instance?.PlayUiMenuClick();
                BeginPopOut(NavigateMainMenuAfterFade);
            }
        }

        private void DrawCenteredStat(float x, float y, float w, string value, string label, bool isPB, float sc)
        {
            string val = GameFont.ExpandPronunciationHintsForPixelFont(value).ToUpperInvariant().Replace(" ", "  ");
            string lbl = GameFont.ExpandPronunciationHintsForPixelFont(label).ToUpperInvariant().Replace(" ", "  ");
            float vh = 56f * sc;
            float lh = 26f * sc;
            GameFont.OutlinedLabel(new Rect(x, y, w, vh), val, _primaryStatValue);
            GameFont.OutlinedLabel(new Rect(x, y + vh + 8f * sc, w, lh), lbl, _primaryStatLabel);

            if (isPB)
            {
                float valW = _primaryStatValue.CalcSize(new GUIContent(val)).x;
                float badgeX = x + (w + valW) * 0.5f + 4f * sc;
                GameFont.OutlinedLabel(new Rect(badgeX, y + 4f * sc, 56f * sc, 20f * sc), "PB", _pbBadgeSmall);
            }
        }

        private void DrawSmallStat(float x, float y, float w, string value, string label, float sc)
        {
            string val = GameFont.ExpandPronunciationHintsForPixelFont(value).ToUpperInvariant().Replace(" ", "  ");
            string lbl = GameFont.ExpandPronunciationHintsForPixelFont(label).ToUpperInvariant().Replace(" ", "  ");
            float vh = 42f * sc;
            float lh = 22f * sc;
            GameFont.OutlinedLabel(new Rect(x, y, w, vh), val, _secondaryValue);
            GameFont.OutlinedLabel(new Rect(x, y + vh + 7f * sc, w, lh), lbl, _secondaryLabel);
        }

        private void EnsureResultStyles()
        {
            if (_stylesReady && _stylesBuiltForScreenH == Screen.height) return;
            _stylesReady = true;
            _stylesBuiltForScreenH = Screen.height;

            float sc = ResultUiScale();
            int S(int px) => Mathf.Max(10, Mathf.RoundToInt(px * sc));

            _dimScrim = BoxStyle(MenuVisualTheme.OverlayDim);

            _panelBg = BoxStyle(MenuVisualTheme.ResultPanelBg);
            _headerBar = BoxStyle(MenuVisualTheme.ResultAccentFallback);
            _divider = new GUIStyle { normal = { background = Tex(new Color(0.45f, 0.82f, 0.88f, 0.35f)) } };

            _titleStyle = Lbl(S(38), FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _heroStatValue = Lbl(S(60), FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _heroSubStyle = Lbl(S(42), FontStyle.Bold, TextAnchor.MiddleCenter,
                MenuVisualTheme.ResultHeroSub);

            _primaryStatValue = Lbl(S(38), FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
            _primaryStatValue.clipping = TextClipping.Overflow;
            _primaryStatLabel = Lbl(S(19), FontStyle.Bold, TextAnchor.MiddleCenter,
                MenuVisualTheme.ResultPrimaryLabel);
            _primaryStatLabel.clipping = TextClipping.Overflow;

            _secondaryValue = Lbl(S(27), FontStyle.Bold, TextAnchor.MiddleCenter,
                MenuVisualTheme.ResultSecondaryValue);
            _secondaryValue.clipping = TextClipping.Overflow;
            _secondaryLabel = Lbl(S(17), FontStyle.Bold, TextAnchor.MiddleCenter,
                MenuVisualTheme.ResultSecondaryLabel);
            _secondaryLabel.clipping = TextClipping.Overflow;

            _feedbackStyle = Lbl(S(27), FontStyle.Bold, TextAnchor.MiddleCenter,
                MenuVisualTheme.ResultFeedback);
            _quoteStyle = Lbl(S(22), FontStyle.Italic, TextAnchor.MiddleCenter,
                MenuVisualTheme.ResultQuote);

            _sectionHeaderStyle = Lbl(S(22), FontStyle.Bold, TextAnchor.MiddleCenter,
                MenuVisualTheme.ResultSectionHeader);

            _pbBadge = Lbl(S(19), FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(1f, 0.85f, 0.2f));
            _pbBadgeSmall = Lbl(S(17), FontStyle.Bold, TextAnchor.MiddleLeft,
                new Color(1f, 0.85f, 0.2f));

            _btnPrimary = BtnStyle(Tex(MenuVisualTheme.ResultButtonPrimary),
                Tex(MenuVisualTheme.ResultButtonPrimaryHover), Color.white, S(30));
            _btnSecondary = BtnStyle(Tex(MenuVisualTheme.ResultButtonSecondary),
                Tex(MenuVisualTheme.ResultButtonSecondaryHover),
                MenuVisualTheme.ResultSecondaryBtnText, S(28));

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
            // Flatten all states so non-interactive text doesn't highlight on hover
            s.hover.textColor = color;
            s.active.textColor = color;
            s.focused.textColor = color;
            s.hover.background = null;
            s.active.background = null;
            s.focused.background = null;
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
