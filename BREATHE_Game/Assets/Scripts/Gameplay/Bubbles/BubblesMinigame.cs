using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // Bubbles minigame: find the sweet spot of breath intensity to produce bubbles.
    // Too gentle = tiny dim bubbles. Too aggressive = wand splashes. Sweet spot = big colorful chains.
    // Goal-based: produce a target number of bubbles to complete. Timer tracks how long it takes.
    public class BubblesMinigame : MinigameBase
    {
        [Header("Bubbles References")]
        [SerializeField] private BubbleWandController _wandController;

        [Header("Session")]
        [SerializeField] private int _bubbleGoal = 38;
        [SerializeField] private int _pointsPerBubble = 10;
        [SerializeField] private int _streakBonusPerBubble = 5;

        private float _sessionTimer;
        private bool _gameplayActive;
        private bool _countdownDone;
        private float _postCountdownTimer = -1f;

        private int _totalScore;
        private int _lastKnownBubbles;

        // Personal bests
        private int _pbScore;
        private float _pbTime;
        private int _pbStreakBubbles;
        private bool _newPBScore;
        private bool _newPBTime;

        private string PBScoreKey => $"{MinigameId}_PB_Score";
        private string PBTimeKey => $"{MinigameId}_PB_Time";
        private string PBStreakKey => $"{MinigameId}_PB_Streak";

        public override string MinigameId => "bubbles";
        public override bool IsComplete => _gameplayActive && _wandController != null &&
            _wandController.SweetSpotBubblesProduced >= _bubbleGoal;

        private GUIStyle _counterStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _zoneStyle;
        private GUIStyle _timerStyle;
        private Texture2D _headerBarTex;
        private int _hudStylesBuiltForScreenH;

        private readonly ScorePopupPresenter _scorePopups = new ScorePopupPresenter();

        protected override void Awake()
        {
            base.Awake();
            LoadPersonalBests();
        }

        protected override void Start()
        {
            base.Start();
            if (_wandController == null)
                _wandController = FindAnyObjectByType<BubbleWandController>();

            _wandController?.Initialize();

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnCountdownTick += HandleCountdown;

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.TransitionTo(GameState.Tutorial);
        }

        protected override void OnDestroy()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnCountdownTick -= HandleCountdown;
            base.OnDestroy();
        }

        private void HandleCountdown(int remaining)
        {
            if (remaining == 0) _countdownDone = true;
        }

        public override void OnMinigameStart()
        {
            base.OnMinigameStart();
            _sessionTimer = 0f;
            _totalScore = 0;
            _lastKnownBubbles = 0;
            _gameplayActive = false;
            _countdownDone = false;
            _postCountdownTimer = -1f;
            _newPBScore = false;
            _newPBTime = false;
            _scorePopups.Clear();
            _wandController?.ResetTracking();
        }

        private void Update()
        {
            if (_countdownDone && !_gameplayActive)
            {
                if (_postCountdownTimer < 0f)
                {
                    float buffer = Definition != null ? Definition.PostCountdownBuffer : 0f;
                    _postCountdownTimer = buffer;
                }
                _postCountdownTimer -= Time.deltaTime;
                if (_postCountdownTimer <= 0f)
                {
                    _gameplayActive = true;
                    _wandController?.Activate();
                    if (_breathAnalytics != null) _breathAnalytics.StartTracking();
                }
                return;
            }

            if (!_gameplayActive || _wandController == null) return;

            _scorePopups.Tick(Time.deltaTime);

            _sessionTimer += Time.deltaTime;

            float breathPower = BreathPowerSystem.Instance != null
                ? BreathPowerSystem.Instance.CurrentBreathPower : 0f;

            _wandController.UpdateBreath(breathPower);

            // Calculate score based on new bubbles produced
            int currentBubbles = _wandController.SweetSpotBubblesProduced;
            if (currentBubbles > _lastKnownBubbles)
            {
                int newBubbles = currentBubbles - _lastKnownBubbles;
                Camera cam = Camera.main;
                for (int i = 0; i < newBubbles; i++)
                {
                    int basePoints = _pointsPerBubble;
                    // Streak bonus when currently in sweet spot
                    if (_wandController.CurrentZone == BubbleWandController.BreathZone.SweetSpot &&
                        _wandController.CurrentStreakBubbles > 1)
                        basePoints += _streakBonusPerBubble;
                    _totalScore += basePoints;

                    Transform anchor = _wandController.TryTakeSweetSpotScoreAnchor();
                    var popColor = new Color(0.45f, 1f, 0.65f);
                    string ptsPopup = $"{basePoints}  PTS";
                    if (anchor != null && cam != null)
                        _scorePopups.PushFollowing(ptsPopup, popColor, anchor, cam);
                    else
                        _scorePopups.Push(ptsPopup, popColor, null);
                }
                if (newBubbles > 0)
                    TryPlayMinigamePrimaryActionSfx(0.14f);
                _lastKnownBubbles = currentBubbles;
            }

            // Check completion
            if (currentBubbles >= _bubbleGoal)
            {
                EvaluatePersonalBests();
                if (GameStateManager.Instance != null)
                    GameStateManager.Instance.TransitionTo(GameState.Celebration);
            }
        }

        private void LoadPersonalBests()
        {
            _pbScore = PlayerPrefs.GetInt(PBScoreKey, 0);
            _pbTime = PlayerPrefs.GetFloat(PBTimeKey, float.MaxValue);
            _pbStreakBubbles = PlayerPrefs.GetInt(PBStreakKey, 0);
        }

        private void EvaluatePersonalBests()
        {
            if (_totalScore > _pbScore)
            {
                _newPBScore = true;
                _pbScore = _totalScore;
                PlayerPrefs.SetInt(PBScoreKey, _pbScore);
            }
            // Lower time is better
            if (_sessionTimer < _pbTime)
            {
                _newPBTime = true;
                _pbTime = _sessionTimer;
                PlayerPrefs.SetFloat(PBTimeKey, _pbTime);
            }
            int longestStreak = _wandController != null ? _wandController.LongestStreakBubbles : 0;
            if (longestStreak > _pbStreakBubbles)
            {
                _pbStreakBubbles = longestStreak;
                PlayerPrefs.SetInt(PBStreakKey, _pbStreakBubbles);
            }
            PlayerPrefs.Save();
        }

        public override string GetResultTitle() => "BUBBLES  COMPLETE!";

        public override string GetCelebrationTitle()
        {
            if (_wandController == null) return "WELL  DONE!";
            float avgTime = _bubbleGoal > 0 ? _sessionTimer / _bubbleGoal : 0f;
            if (avgTime < 0.8f) return "BUBBLE  MASTER!";
            if (avgTime < 1.5f) return "BEAUTIFUL  BUBBLES!";
            return "NICE  BREATHING!";
        }

        public override string GetPersonalBestMessage()
        {
            if (_newPBScore && _newPBTime)
                return "New personal bests for score AND completion time!";
            if (_newPBScore)
                return "New highest score!";
            if (_newPBTime)
                return "New fastest completion!";
            return "Great bubble work! Try for a faster time!";
        }

        public override MinigameStat[] GetEndStats()
        {
            float activity = _breathAnalytics != null ? _breathAnalytics.AdjustedActivityRatio : 0f;
            string pattern = _breathAnalytics != null ? _breathAnalytics.BreathPatternLabel : "N/A";
            float avgIntensity = _breathAnalytics != null ? _breathAnalytics.AverageIntensity : 0f;

            int bubbles = _wandController != null ? _wandController.SweetSpotBubblesProduced : 0;
            float longestStreak = _wandController != null ? _wandController.LongestStreak : 0f;
            int streakBubbles = _wandController != null ? _wandController.LongestStreakBubbles : 0;

            return new[]
            {
                new MinigameStat("Score", $"{_totalScore}", _newPBScore, StatTier.Hero),
                new MinigameStat("Time", GameFont.FormatHudSecondsWhole(_sessionTimer), _newPBTime, StatTier.Hero),
                new MinigameStat("Bubbles", GameFont.FormatResultsCountOfTotal(bubbles, _bubbleGoal), false, StatTier.Primary),
                new MinigameStat("Best Streak", $"{streakBubbles} in a row", false, StatTier.Primary),
                new MinigameStat("Streak Time", GameFont.FormatHudSecondsWhole(longestStreak), false, StatTier.Primary),
                new MinigameStat("Avg Power", $"{avgIntensity * 100f:F0}%", false, StatTier.Secondary),
                new MinigameStat("Pattern", pattern, false, StatTier.Secondary),
                new MinigameStat("Activity", FormatActivityGrade(activity), false, StatTier.Secondary)
            };
        }

        public override Dictionary<string, string> GetDebugInfo()
        {
            string zone = _wandController != null ? _wandController.CurrentZone.ToString() : "N/A";
            int bubbles = _wandController != null ? _wandController.SweetSpotBubblesProduced : 0;
            int streak = _wandController != null ? _wandController.CurrentStreakBubbles : 0;

            return new Dictionary<string, string>
            {
                ["Time"] = GameFont.FormatHudSecondsWhole(_sessionTimer),
                ["Bubbles"] = GameFont.FormatResultsCountOfTotal(bubbles, _bubbleGoal),
                ["Score"] = $"{_totalScore}",
                ["Zone"] = zone,
                ["Streak"] = $"{streak}"
            };
        }

        private void OnGUI()
        {
            if (Time.timeScale == 0f) return; // Don't draw HUD when paused
            if (!_gameplayActive) return;
            if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
                return;

            BuildHUDStyles();

            float sc = HudUiScale();
            _scorePopups.FontScale = Mathf.Clamp(sc * 1.12f, 1.05f, 1.65f);

            float margin = 16f * sc;
            float numW = 220f * sc;
            // Wider than timer/score: "NN  OF  NN" + bold pixel font must stay on one line.
            float bubbleCounterW = 300f * sc;
            // Pixel font + bold + outline needs more vertical room than fontSize alone (avoids IMGUI clipping).
            float numH = 76f * sc;
            float lblH = 32f * sc;
            float rowGap = 10f * sc;
            float yNum = 14f * sc;
            float yLbl = yNum + numH + rowGap;
            float barH = yLbl + lblH + 28f * sc;

            // Header bar background
            if (_headerBarTex != null)
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, barH), _headerBarTex, ScaleMode.StretchToFill);

            int bubbles = _wandController != null ? _wandController.SweetSpotBubblesProduced : 0;

            // Bubble counter — top center
            string counterText = GameFont.FormatHudCountOfTotal(bubbles, _bubbleGoal);
            float cx = (Screen.width - bubbleCounterW) * 0.5f;
            Rect counterRect = new Rect(cx, yNum, bubbleCounterW, numH);
            GameFont.OutlinedLabel(counterRect, counterText, _counterStyle, 2);

            Rect counterLabel = new Rect(cx, yLbl, bubbleCounterW, lblH);
            GameFont.OutlinedLabel(counterLabel, "BUBBLES", _labelStyle);

            // Timer — top left
            string timeText = GameFont.FormatHudSecondsWhole(_sessionTimer);
            Rect timeRect = new Rect(margin, yNum, numW, numH);
            GameFont.OutlinedLabel(timeRect, timeText, _timerStyle, 2);

            Rect timeLabel = new Rect(margin, yLbl, numW, lblH);
            GameFont.OutlinedLabel(timeLabel, "TIME", _labelStyle);

            // Score — top right
            float rx = Screen.width - margin - numW;
            Rect scoreRect = new Rect(rx, yNum, numW, numH);
            GameFont.OutlinedLabel(scoreRect, $"{_totalScore}", _timerStyle, 2);

            Rect scoreLabel = new Rect(rx, yLbl, numW, lblH);
            GameFont.OutlinedLabel(scoreLabel, "SCORE", _labelStyle);

            // Zone indicator — bottom center
            if (_wandController != null)
            {
                string zoneText = _wandController.CurrentZone switch
                {
                    BubbleWandController.BreathZone.SweetSpot => "PERFECT!",
                    BubbleWandController.BreathZone.TooGentle => "BLOW  HARDER",
                    BubbleWandController.BreathZone.TooAggressive => "TOO  HARD!",
                    _ => ""
                };

                if (!string.IsNullOrEmpty(zoneText))
                {
                    Color zoneColor = _wandController.CurrentZone switch
                    {
                        BubbleWandController.BreathZone.SweetSpot => new Color(0.3f, 1f, 0.5f),
                        BubbleWandController.BreathZone.TooGentle => new Color(0.7f, 0.7f, 0.8f),
                        BubbleWandController.BreathZone.TooAggressive => new Color(1f, 0.4f, 0.3f),
                        _ => Color.white
                    };
                    SetStyleColor(_zoneStyle, zoneColor);

                    float zoneH = 52f * sc;
                    Rect zoneRect = new Rect(0f, Screen.height - 28f * sc - zoneH, Screen.width, zoneH);
                    GameFont.OutlinedLabel(zoneRect, zoneText, _zoneStyle, 2);
                }
            }

            _scorePopups.DrawOnGUI();
        }

        static float HudUiScale()
        {
            return Mathf.Clamp(Screen.height / 900f, 0.95f, 1.55f);
        }

        static int HudFont(int basePx)
        {
            return Mathf.Max(10, Mathf.RoundToInt(basePx * HudUiScale()));
        }

        private void BuildHUDStyles()
        {
            if (_counterStyle != null && _hudStylesBuiltForScreenH == Screen.height) return;
            _hudStylesBuiltForScreenH = Screen.height;
            Font f = GameFont.Get();

            _counterStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = HudFont(52),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow,
                wordWrap = false
            };
            _counterStyle.normal.textColor = Color.white;
            if (f != null) _counterStyle.font = f;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = HudFont(28),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
            if (f != null) _labelStyle.font = f;

            _timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = HudFont(46),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                clipping = TextClipping.Overflow
            };
            _timerStyle.normal.textColor = Color.white;
            if (f != null) _timerStyle.font = f;

            _zoneStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = HudFont(48),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _zoneStyle.normal.textColor = Color.white;
            if (f != null) _zoneStyle.font = f;

            // Flatten all states so text doesn't highlight on hover
            FlattenHudStyle(_counterStyle);
            FlattenHudStyle(_labelStyle);
            FlattenHudStyle(_timerStyle);
            FlattenHudStyle(_zoneStyle);

            if (_headerBarTex == null)
            {
                _headerBarTex = new Texture2D(1, 1);
                _headerBarTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.5f));
                _headerBarTex.Apply();
            }
        }

        private static string FormatActivityGrade(float ratio)
        {
            if (ratio >= 0.7f) return "Excellent";
            if (ratio >= 0.5f) return "Good";
            if (ratio >= 0.3f) return "Fair";
            return "Low";
        }

        private static void FlattenHudStyle(GUIStyle s)
        {
            if (s == null) return;
            s.hover = s.normal;
            s.active = s.normal;
            s.focused = s.normal;
        }

        private static void SetStyleColor(GUIStyle s, Color c)
        {
            if (s == null) return;
            s.normal.textColor = c;
            s.hover.textColor = c;
            s.active.textColor = c;
            s.focused.textColor = c;
        }
    }
}
