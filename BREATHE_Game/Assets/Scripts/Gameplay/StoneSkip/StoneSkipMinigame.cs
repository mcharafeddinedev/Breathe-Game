using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // Stone skip minigame: blow as hard and long as you can to wind up, then the stone
    // launches when breath drops below 10%. More power + longer blow = further throw.
    // 3 rounds per session. Score = points per skip.
    public class StoneSkipMinigame : MinigameBase
    {
        [Header("Stone Skip References")]
        [SerializeField] private StoneSkipController _controller;

        [Header("Session")]
        [SerializeField] private int _totalRounds = 3;
        [SerializeField] private int _pointsPerSkip = 200;
        [SerializeField] private float _launchThreshold = 0.10f;
        [SerializeField] private float _windUpStartThreshold = 0.08f;
        [SerializeField] private float _delayBetweenRounds = 2f;

        private enum RoundPhase { WaitingForBlow, WindingUp, StoneFlying, RoundDelay }

        private RoundPhase _roundPhase;
        private int _currentRound;
        private bool _gameplayActive;
        private bool _countdownDone;
        private float _postCountdownTimer = -1f;

        // Wind-up tracking
        private float _windUpDuration;
        private float _windUpIntensitySum;
        private int _windUpSamples;
        private bool _hasStartedBlowing;

        // Round results
        private int[] _skipsPerRound;
        private float[] _distancePerRound;
        private int[] _scorePerRound;
        private float _roundDelayTimer;

        // Totals
        private int _totalScore;
        private int _bestSkips;
        private float _bestDistance;

        // Personal bests
        private int _pbTotalScore;
        private int _pbBestSkips;
        private float _pbBestDistance;
        private bool _newPBScore;
        private bool _newPBSkips;

        private string PBScoreKey => $"{MinigameId}_PB_TotalScore";
        private string PBSkipsKey => $"{MinigameId}_PB_BestSkips";
        private string PBDistanceKey => $"{MinigameId}_PB_BestDistance";

        public override string MinigameId => "stoneskip";
        public override bool IsComplete => _currentRound >= _totalRounds &&
            (_controller == null || _controller.IsRoundDone);

        private GUIStyle _roundStyle;
        private GUIStyle _infoStyle;
        private GUIStyle _bigInfoStyle;
        private GUIStyle _promptStyle;

        private readonly ScorePopupPresenter _scorePopups = new ScorePopupPresenter();

        protected override void Awake()
        {
            base.Awake();
            _skipsPerRound = new int[_totalRounds];
            _distancePerRound = new float[_totalRounds];
            _scorePerRound = new int[_totalRounds];
            LoadPersonalBests();
        }

        private void Start()
        {
            if (_controller == null)
                _controller = FindAnyObjectByType<StoneSkipController>();

            _controller?.Initialize();

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
            _currentRound = 0;
            _totalScore = 0;
            _bestSkips = 0;
            _bestDistance = 0f;
            _gameplayActive = false;
            _countdownDone = false;
            _postCountdownTimer = -1f;
            _newPBScore = false;
            _newPBSkips = false;
            _scorePopups.Clear();
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
                    _roundPhase = RoundPhase.WaitingForBlow;
                    if (_breathAnalytics != null) _breathAnalytics.StartTracking();
                    _controller?.ResetForNewRound();
                }
                return;
            }

            if (!_gameplayActive) return;

            _scorePopups.Tick(Time.deltaTime);

            float breathPower = BreathPowerSystem.Instance != null
                ? BreathPowerSystem.Instance.CurrentBreathPower : 0f;

            switch (_roundPhase)
            {
                case RoundPhase.WaitingForBlow:
                    if (breathPower > _windUpStartThreshold)
                    {
                        _roundPhase = RoundPhase.WindingUp;
                        _windUpDuration = 0f;
                        _windUpIntensitySum = 0f;
                        _windUpSamples = 0;
                        _hasStartedBlowing = true;
                    }
                    break;

                case RoundPhase.WindingUp:
                    _windUpDuration += Time.deltaTime;
                    _windUpIntensitySum += breathPower;
                    _windUpSamples++;

                    // Visual wind-up progress (caps at 1 over ~3 seconds of max effort)
                    float visualProgress = Mathf.Clamp01(_windUpDuration * breathPower / 2f);
                    _controller?.SetWindUpProgress(visualProgress);

                    // Launch when breath drops below threshold
                    if (_hasStartedBlowing && breathPower < _launchThreshold)
                    {
                        float avgIntensity = _windUpSamples > 0
                            ? _windUpIntensitySum / _windUpSamples : 0f;
                        // throwPower = average intensity * duration, normalized to 0-1 range
                        // ~3 seconds at full power = max throw
                        float throwPower = Mathf.Clamp01(avgIntensity * _windUpDuration / 3f);
                        _controller?.LaunchStone(throwPower);
                        TryPlayMinigamePrimaryActionSfx(0f);
                        _roundPhase = RoundPhase.StoneFlying;
                    }
                    break;

                case RoundPhase.StoneFlying:
                    _controller?.UpdateStone();

                    if (_controller != null && _controller.IsRoundDone)
                    {
                        int skips = _controller.SkipCount;
                        float distance = _controller.DistanceTraveled;
                        int roundScore = skips * _pointsPerSkip;

                        int roundIdx = Mathf.Min(_currentRound, _totalRounds - 1);
                        _skipsPerRound[roundIdx] = skips;
                        _distancePerRound[roundIdx] = distance;
                        _scorePerRound[roundIdx] = roundScore;
                        _totalScore += roundScore;
                        if (roundScore > 0)
                            _scorePopups.Push($"+{roundScore}", new Color(1f, 0.88f, 0.35f));

                        if (skips > _bestSkips) _bestSkips = skips;
                        if (distance > _bestDistance) _bestDistance = distance;

                        _currentRound++;

                        if (_currentRound >= _totalRounds)
                        {
                            EvaluatePersonalBests();
                            if (GameStateManager.Instance != null)
                                GameStateManager.Instance.TransitionTo(GameState.Celebration);
                        }
                        else
                        {
                            _roundPhase = RoundPhase.RoundDelay;
                            _roundDelayTimer = _delayBetweenRounds;
                        }
                    }
                    break;

                case RoundPhase.RoundDelay:
                    _roundDelayTimer -= Time.deltaTime;
                    if (_roundDelayTimer <= 0f)
                    {
                        _controller?.ResetForNewRound();
                        _roundPhase = RoundPhase.WaitingForBlow;
                        _hasStartedBlowing = false;
                    }
                    break;
            }
        }

        private void LoadPersonalBests()
        {
            _pbTotalScore = PlayerPrefs.GetInt(PBScoreKey, 0);
            _pbBestSkips = PlayerPrefs.GetInt(PBSkipsKey, 0);
            _pbBestDistance = PlayerPrefs.GetFloat(PBDistanceKey, 0f);
        }

        private void EvaluatePersonalBests()
        {
            if (_totalScore > _pbTotalScore)
            {
                _newPBScore = true;
                _pbTotalScore = _totalScore;
                PlayerPrefs.SetInt(PBScoreKey, _pbTotalScore);
            }
            if (_bestSkips > _pbBestSkips)
            {
                _newPBSkips = true;
                _pbBestSkips = _bestSkips;
                PlayerPrefs.SetInt(PBSkipsKey, _pbBestSkips);
            }
            if (_bestDistance > _pbBestDistance)
            {
                _pbBestDistance = _bestDistance;
                PlayerPrefs.SetFloat(PBDistanceKey, _pbBestDistance);
            }
            PlayerPrefs.Save();
        }

        public override string GetResultTitle() => "STONES  SKIPPED!";

        public override string GetCelebrationTitle()
        {
            if (_bestSkips >= 8) return "LEGENDARY  SKIP!";
            if (_bestSkips >= 5) return "AMAZING  THROW!";
            if (_bestSkips >= 3) return "NICE  TOSS!";
            return "KEEP  PRACTICING!";
        }

        public override string GetPersonalBestMessage()
        {
            if (_newPBScore && _newPBSkips)
                return "New personal bests for score AND most skips!";
            if (_newPBScore)
                return "New highest total score!";
            if (_newPBSkips)
                return "New most skips in a single throw!";
            return "Great throws! Try again to skip further!";
        }

        public override MinigameStat[] GetEndStats()
        {
            float activity = _breathAnalytics != null ? _breathAnalytics.AdjustedActivityRatio : 0f;
            string pattern = _breathAnalytics != null ? _breathAnalytics.BreathPatternLabel : "N/A";
            float avgIntensity = _breathAnalytics != null ? _breathAnalytics.AverageIntensity : 0f;

            // Find best round
            int bestRoundIdx = 0;
            for (int i = 1; i < _totalRounds; i++)
                if (_skipsPerRound[i] > _skipsPerRound[bestRoundIdx]) bestRoundIdx = i;

            return new[]
            {
                new MinigameStat("Score", $"{_totalScore}", _newPBScore, StatTier.Hero),
                new MinigameStat("Best Throw", $"{_bestSkips}  skips", _newPBSkips, StatTier.Hero),
                new MinigameStat("Throw 1", $"{_skipsPerRound[0]}  skips", false, StatTier.Primary),
                new MinigameStat("Throw 2", $"{_skipsPerRound[1]}  skips", false, StatTier.Primary),
                new MinigameStat("Throw 3", $"{_skipsPerRound[2]}  skips", false, StatTier.Primary),
                new MinigameStat("Best Distance", $"{_bestDistance:F1}m", false, StatTier.Primary),
                new MinigameStat("Avg Power", $"{avgIntensity * 100f:F0}%", false, StatTier.Secondary),
                new MinigameStat("Pattern", pattern, false, StatTier.Secondary),
                new MinigameStat("Activity", FormatActivityGrade(activity), false, StatTier.Secondary)
            };
        }

        public override Dictionary<string, string> GetDebugInfo()
        {
            return new Dictionary<string, string>
            {
                ["Round"] = $"{Mathf.Min(_currentRound + 1, _totalRounds)}/{_totalRounds}",
                ["Phase"] = _roundPhase.ToString(),
                ["Score"] = $"{_totalScore}",
                ["Best Skips"] = $"{_bestSkips}",
                ["Wind-Up"] = _roundPhase == RoundPhase.WindingUp
                    ? $"{_windUpDuration:F1}s" : "—"
            };
        }

        private void OnGUI()
        {
            if (!_gameplayActive) return;
            if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
                return;

            BuildHUDStyles();

            // Round indicator — top center
            int displayRound = Mathf.Min(_currentRound + 1, _totalRounds);
            string roundText = $"THROW  {displayRound}  /  {_totalRounds}";
            Rect roundRect = new Rect(Screen.width * 0.5f - 120f, 20f, 240f, 40f);
            GameFont.OutlinedLabel(roundRect, roundText, _roundStyle, 2);

            // Score — top right
            Rect scoreRect = new Rect(Screen.width - 200f, 20f, 160f, 40f);
            GameFont.OutlinedLabel(scoreRect, $"{_totalScore}", _bigInfoStyle, 2);
            Rect scoreLabelRect = new Rect(Screen.width - 200f, 58f, 160f, 24f);
            GameFont.OutlinedLabel(scoreLabelRect, "SCORE", _infoStyle);

            // Prompt during waiting phase
            if (_roundPhase == RoundPhase.WaitingForBlow)
            {
                string prompt = "BLOW  TO  WIND  UP!";
                Rect promptRect = new Rect(0f, Screen.height * 0.3f, Screen.width, 60f);
                float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 4f);
                _promptStyle.normal.textColor = new Color(1f, 1f, 0.4f, pulse);
                GameFont.OutlinedLabel(promptRect, prompt, _promptStyle, 2);
            }

            // Wind-up power indicator
            if (_roundPhase == RoundPhase.WindingUp)
            {
                float avgIntensity = _windUpSamples > 0 ? _windUpIntensitySum / _windUpSamples : 0f;
                float power = Mathf.Clamp01(avgIntensity * _windUpDuration / 3f);
                string powerText = $"POWER  {power * 100f:F0}%";
                Rect powerRect = new Rect(0f, Screen.height * 0.3f, Screen.width, 50f);
                Color powerColor = Color.Lerp(Color.yellow, Color.green, power);
                _promptStyle.normal.textColor = powerColor;
                GameFont.OutlinedLabel(powerRect, powerText, _promptStyle, 2);
            }

            // Last round result flash
            if (_roundPhase == RoundPhase.RoundDelay && _currentRound > 0)
            {
                int lastIdx = _currentRound - 1;
                string resultText = $"{_skipsPerRound[lastIdx]}  SKIPS!  +{_scorePerRound[lastIdx]}";
                Rect resultRect = new Rect(0f, Screen.height * 0.4f, Screen.width, 50f);
                float fadeIn = Mathf.Clamp01((_delayBetweenRounds - _roundDelayTimer) / 0.5f);
                _bigInfoStyle.normal.textColor = new Color(1f, 1f, 1f, fadeIn);
                GameFont.OutlinedLabel(resultRect, resultText, _bigInfoStyle, 2);
                _bigInfoStyle.normal.textColor = Color.white;
            }

            _scorePopups.DrawOnGUI();
        }

        private void BuildHUDStyles()
        {
            if (_roundStyle != null) return;
            Font f = GameFont.Get();

            _roundStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _roundStyle.normal.textColor = Color.white;
            if (f != null) _roundStyle.font = f;

            _infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _infoStyle.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
            if (f != null) _infoStyle.font = f;

            _bigInfoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _bigInfoStyle.normal.textColor = Color.white;
            if (f != null) _bigInfoStyle.font = f;

            _promptStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _promptStyle.normal.textColor = Color.yellow;
            if (f != null) _promptStyle.font = f;
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
