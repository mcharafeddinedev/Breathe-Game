using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // Balloon inflation minigame: blow to inflate balloons before time runs out.
    // Each completed balloon = 100 points. Session is timed (default 45s).
    // Extends MinigameBase for automatic registration, analytics, and session logging.
    public class BalloonMinigame : MinigameBase
    {
        [Header("Balloon References")]
        [SerializeField] private BalloonController _balloonController;

        [Header("Session")]
        [SerializeField] private float _sessionDuration = 45f;
        [SerializeField] private int _pointsPerBalloon = 100;

        private float _sessionTimer;
        private int _balloonsCompleted;
        private int _totalScore;
        private bool _gameplayActive;
        private bool _countdownDone;
        private float _postCountdownTimer;

        // Personal best tracking
        private int _pbScore;
        private int _pbBalloons;
        private bool _newPBScore;
        private bool _newPBBalloons;

        private string PBScoreKey => $"{MinigameId}_PB_Score";
        private string PBBalloonsKey => $"{MinigameId}_PB_Balloons";

        public override string MinigameId => "balloon";
        public override bool IsComplete => _sessionTimer <= 0f && _gameplayActive;

        private GUIStyle _timerStyle;
        private GUIStyle _timerLabelStyle;
        private GUIStyle _counterStyle;
        private GUIStyle _labelStyle;
        private Texture2D _hudBgTex;

        private readonly ScorePopupPresenter _scorePopups = new ScorePopupPresenter();

        protected override void Awake()
        {
            base.Awake();
            LoadPersonalBests();
        }

        private void Start()
        {
            if (_balloonController == null)
                _balloonController = FindAnyObjectByType<BalloonController>();

            if (_balloonController != null)
                _balloonController.Initialize();

            // Subscribe to countdown
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnCountdownTick += HandleCountdown;

            // Auto-start the game flow (Tutorial → Playing)
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
            if (remaining == 0)
                _countdownDone = true;
        }

        public override void OnMinigameStart()
        {
            base.OnMinigameStart();
            _balloonsCompleted = 0;
            _totalScore = 0;
            _sessionTimer = _sessionDuration;
            _gameplayActive = false;
            _countdownDone = false;
            _postCountdownTimer = -1f;
            _newPBScore = false;
            _newPBBalloons = false;
            _scorePopups.Clear();
        }

        private void Update()
        {
            // Wait for countdown to finish before enabling gameplay
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
                    _balloonController?.Activate();
                    if (_breathAnalytics != null) _breathAnalytics.StartTracking();
                }
                return;
            }

            if (!_gameplayActive) return;

            _scorePopups.Tick(Time.deltaTime);

            // Timer countdown
            _sessionTimer -= Time.deltaTime;

            if (_sessionTimer <= 0f)
            {
                _sessionTimer = 0f;
                _balloonController?.Freeze();
                EvaluatePersonalBests();

                if (GameStateManager.Instance != null)
                    GameStateManager.Instance.TransitionTo(GameState.Celebration);
                return;
            }

            // Inflate based on breath power
            float breathPower = 0f;
            if (BreathPowerSystem.Instance != null)
                breathPower = BreathPowerSystem.Instance.CurrentBreathPower;

            _balloonController?.Inflate(breathPower);

            // Handle cycle animations (tie-off, slide away, new balloon)
            if (_balloonController != null && _balloonController.UpdateCycle())
            {
                _balloonsCompleted++;
                _totalScore = _balloonsCompleted * _pointsPerBalloon;
                _scorePopups.Push($"+{_pointsPerBalloon}", new Color(1f, 0.88f, 0.35f));
                TryPlayMinigamePrimaryActionSfx(0f);
            }
        }

        private void LoadPersonalBests()
        {
            _pbScore = PlayerPrefs.GetInt(PBScoreKey, 0);
            _pbBalloons = PlayerPrefs.GetInt(PBBalloonsKey, 0);
        }

        private void EvaluatePersonalBests()
        {
            if (_totalScore > _pbScore)
            {
                _newPBScore = true;
                _pbScore = _totalScore;
                PlayerPrefs.SetInt(PBScoreKey, _pbScore);
            }
            if (_balloonsCompleted > _pbBalloons)
            {
                _newPBBalloons = true;
                _pbBalloons = _balloonsCompleted;
                PlayerPrefs.SetInt(PBBalloonsKey, _pbBalloons);
            }
            PlayerPrefs.Save();
        }

        public override string GetResultTitle() => "TIME'S  UP!";

        public override string GetCelebrationTitle()
        {
            if (_balloonsCompleted >= 8) return "BALLOON  MASTER!";
            if (_balloonsCompleted >= 5) return "GREAT  INFLATING!";
            if (_balloonsCompleted >= 3) return "NICE  WORK!";
            return "KEEP  BLOWING!";
        }

        public override string GetPersonalBestMessage()
        {
            if (_newPBScore && _newPBBalloons)
                return "New personal bests for score AND balloon count!";
            if (_newPBScore)
                return "New highest score!";
            if (_newPBBalloons)
                return "New most balloons inflated!";
            return "Great session! Try again to beat your personal best!";
        }

        public override MinigameStat[] GetEndStats()
        {
            float activity = _breathAnalytics != null ? _breathAnalytics.AdjustedActivityRatio : 0f;
            string pattern = _breathAnalytics != null ? _breathAnalytics.BreathPatternLabel : "N/A";
            int sustained = _breathAnalytics != null ? _breathAnalytics.SustainedSegmentCount : 0;
            float avgDur = _breathAnalytics != null ? _breathAnalytics.AverageSustainedDuration : 0f;
            float avgIntensity = _breathAnalytics != null ? _breathAnalytics.AverageIntensity : 0f;

            return new[]
            {
                new MinigameStat("Score", $"{_totalScore}", _newPBScore, StatTier.Hero),
                new MinigameStat("Balloons", $"{_balloonsCompleted}", _newPBBalloons, StatTier.Hero),
                new MinigameStat("Avg Intensity", $"{avgIntensity * 100f:F0}%", false, StatTier.Primary),
                new MinigameStat("Breaths", $"{sustained}", false, StatTier.Primary),
                new MinigameStat("Avg Breath", $"{avgDur:F1}s", false, StatTier.Primary),
                new MinigameStat("Pattern", pattern, false, StatTier.Secondary),
                new MinigameStat("Activity", FormatActivityGrade(activity), false, StatTier.Secondary),
                new MinigameStat("Session", $"{_sessionDuration:F0}s", false, StatTier.Secondary)
            };
        }

        public override Dictionary<string, string> GetDebugInfo()
        {
            var info = new Dictionary<string, string>
            {
                ["Timer"] = $"{_sessionTimer:F1}s",
                ["Balloons"] = $"{_balloonsCompleted}",
                ["Score"] = $"{_totalScore}",
                ["Inflation"] = _balloonController != null
                    ? $"{_balloonController.InflationProgress:P0}" : "N/A",
                ["Active"] = _gameplayActive ? "Yes" : "No"
            };
            return info;
        }

        private void OnGUI()
        {
            if (!_gameplayActive) return;
            if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
                return;

            BuildHUDStyles();

            float sw = Screen.width;
            float sh = Screen.height;
            float pad = sw * 0.03f;
            float barH = sh * 0.09f;

            // Semi-transparent top bar spanning full width
            GUI.DrawTexture(new Rect(0, 0, sw, barH), _hudBgTex);

            float cellW = sw / 3f;
            float numY = barH * 0.08f;
            float numH = barH * 0.55f;
            float lblY = numY + numH - barH * 0.05f;
            float lblH = barH * 0.35f;

            // Left cell: SCORE
            Rect scoreRect = new Rect(pad, numY, cellW - pad, numH);
            _counterStyle.alignment = TextAnchor.MiddleLeft;
            GameFont.OutlinedLabel(scoreRect, $"{_totalScore}", _counterStyle, 2);
            Rect scoreLblRect = new Rect(pad, lblY, cellW - pad, lblH);
            _labelStyle.alignment = TextAnchor.MiddleLeft;
            GameFont.OutlinedLabel(scoreLblRect, "SCORE", _labelStyle);

            // Center cell: TIMER
            string timeText = $"{Mathf.CeilToInt(_sessionTimer)}";
            Color timerColor = _sessionTimer <= 10f
                ? Color.Lerp(Color.red, Color.white, Mathf.PingPong(Time.time * 3f, 1f))
                : Color.white;
            _timerStyle.normal.textColor = timerColor;

            Rect timerRect = new Rect(cellW, numY, cellW, numH);
            GameFont.OutlinedLabel(timerRect, timeText, _timerStyle, 2);
            Rect timerLblRect = new Rect(cellW, lblY, cellW, lblH);
            _timerLabelStyle.normal.textColor = _sessionTimer <= 10f
                ? new Color(1f, 0.7f, 0.7f) : new Color(0.85f, 0.88f, 0.95f);
            GameFont.OutlinedLabel(timerLblRect, "TIME", _timerLabelStyle);

            // Right cell: BALLOONS
            Rect counterRect = new Rect(sw - cellW, numY, cellW - pad, numH);
            _counterStyle.alignment = TextAnchor.MiddleRight;
            GameFont.OutlinedLabel(counterRect, $"{_balloonsCompleted}", _counterStyle, 2);
            Rect counterLblRect = new Rect(sw - cellW, lblY, cellW - pad, lblH);
            _labelStyle.alignment = TextAnchor.MiddleRight;
            GameFont.OutlinedLabel(counterLblRect, "BALLOONS", _labelStyle);

            // Inflation progress bar at bottom of screen
            float barWidth = sw * 0.35f;
            float barHeight = sh * 0.018f;
            float barX = (sw - barWidth) * 0.5f;
            float barY = sh - barHeight - sh * 0.04f;
            float progress = _balloonController != null ? _balloonController.InflationProgress : 0f;

            GUI.DrawTexture(new Rect(barX - 2, barY - 2, barWidth + 4, barHeight + 4), _hudBgTex);
            GUI.DrawTexture(new Rect(barX, barY, barWidth * progress, barHeight), Texture2D.whiteTexture);

            _scorePopups.DrawOnGUI();
        }

        private void BuildHUDStyles()
        {
            if (_timerStyle != null) return;

            Font f = GameFont.Get();

            _hudBgTex = new Texture2D(1, 1);
            _hudBgTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.3f));
            _hudBgTex.Apply();

            _timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Screen.height * 0.05f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _timerStyle.normal.textColor = Color.white;
            if (f != null) _timerStyle.font = f;

            _timerLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Screen.height * 0.02f),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _timerLabelStyle.normal.textColor = new Color(0.85f, 0.88f, 0.95f);
            if (f != null) _timerLabelStyle.font = f;

            _counterStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Screen.height * 0.042f),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _counterStyle.normal.textColor = Color.white;
            if (f != null) _counterStyle.font = f;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Screen.height * 0.018f),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle.normal.textColor = new Color(0.82f, 0.86f, 0.92f);
            if (f != null) _labelStyle.font = f;
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
