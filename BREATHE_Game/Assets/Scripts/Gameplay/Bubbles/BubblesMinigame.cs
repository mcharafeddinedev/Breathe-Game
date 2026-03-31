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
        [SerializeField] private int _bubbleGoal = 45;
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

        protected override void Awake()
        {
            base.Awake();
            LoadPersonalBests();
        }

        private void Start()
        {
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

            _sessionTimer += Time.deltaTime;

            float breathPower = BreathPowerSystem.Instance != null
                ? BreathPowerSystem.Instance.CurrentBreathPower : 0f;

            _wandController.UpdateBreath(breathPower);

            // Calculate score based on new bubbles produced
            int currentBubbles = _wandController.SweetSpotBubblesProduced;
            if (currentBubbles > _lastKnownBubbles)
            {
                int newBubbles = currentBubbles - _lastKnownBubbles;
                for (int i = 0; i < newBubbles; i++)
                {
                    int basePoints = _pointsPerBubble;
                    // Streak bonus when currently in sweet spot
                    if (_wandController.CurrentZone == BubbleWandController.BreathZone.SweetSpot &&
                        _wandController.CurrentStreakBubbles > 1)
                        basePoints += _streakBonusPerBubble;
                    _totalScore += basePoints;
                }
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
                new MinigameStat("Time", $"{_sessionTimer:F1}s", _newPBTime, StatTier.Hero),
                new MinigameStat("Bubbles", $"{bubbles}", false, StatTier.Primary),
                new MinigameStat("Best Streak", $"{streakBubbles} in a row", false, StatTier.Primary),
                new MinigameStat("Streak Time", $"{longestStreak:F1}s", false, StatTier.Primary),
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
                ["Time"] = $"{_sessionTimer:F1}s",
                ["Bubbles"] = $"{bubbles}/{_bubbleGoal}",
                ["Score"] = $"{_totalScore}",
                ["Zone"] = zone,
                ["Streak"] = $"{streak}"
            };
        }

        private void OnGUI()
        {
            if (!_gameplayActive) return;
            if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
                return;

            BuildHUDStyles();

            // Header bar background
            if (_headerBarTex != null)
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, 90f), _headerBarTex, ScaleMode.StretchToFill);

            int bubbles = _wandController != null ? _wandController.SweetSpotBubblesProduced : 0;

            // Bubble counter — top center
            string counterText = $"{bubbles}  /  {_bubbleGoal}";
            Rect counterRect = new Rect(Screen.width * 0.5f - 100f, 20f, 200f, 45f);
            GameFont.OutlinedLabel(counterRect, counterText, _counterStyle, 2);

            Rect counterLabel = new Rect(Screen.width * 0.5f - 100f, 62f, 200f, 24f);
            GameFont.OutlinedLabel(counterLabel, "BUBBLES", _labelStyle);

            // Timer — top left
            string timeText = $"{_sessionTimer:F1}s";
            Rect timeRect = new Rect(40f, 20f, 140f, 40f);
            GameFont.OutlinedLabel(timeRect, timeText, _timerStyle, 2);

            Rect timeLabel = new Rect(40f, 58f, 140f, 24f);
            GameFont.OutlinedLabel(timeLabel, "TIME", _labelStyle);

            // Score — top right
            Rect scoreRect = new Rect(Screen.width - 180f, 20f, 140f, 40f);
            GameFont.OutlinedLabel(scoreRect, $"{_totalScore}", _timerStyle, 2);

            Rect scoreLabel = new Rect(Screen.width - 180f, 58f, 140f, 24f);
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
                    _zoneStyle.normal.textColor = zoneColor;

                    Rect zoneRect = new Rect(0f, Screen.height - 100f, Screen.width, 40f);
                    GameFont.OutlinedLabel(zoneRect, zoneText, _zoneStyle, 2);
                }
            }
        }

        private void BuildHUDStyles()
        {
            if (_counterStyle != null) return;
            Font f = GameFont.Get();

            _counterStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _counterStyle.normal.textColor = Color.white;
            if (f != null) _counterStyle.font = f;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
            if (f != null) _labelStyle.font = f;

            _timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _timerStyle.normal.textColor = Color.white;
            if (f != null) _timerStyle.font = f;

            _zoneStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _zoneStyle.normal.textColor = Color.white;
            if (f != null) _zoneStyle.font = f;

            _headerBarTex = new Texture2D(1, 1);
            _headerBarTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.5f));
            _headerBarTex.Apply();
        }

        private static string FormatActivityGrade(float ratio)
        {
            if (ratio >= 0.7f) return "Excellent";
            if (ratio >= 0.5f) return "Good";
            if (ratio >= 0.3f) return "Fair";
            return "Low";
        }
    }
}
