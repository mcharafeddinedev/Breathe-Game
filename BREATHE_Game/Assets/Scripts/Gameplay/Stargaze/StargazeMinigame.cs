using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // Stargaze minigame: blow to push clouds aside and reveal constellations.
    // No traditional scoring — this is a relaxation/education game.
    // 3 rounds of different constellations per session. Breath measurements still tracked.
    public class StargazeMinigame : MinigameBase
    {
        [Header("Stargaze References")]
        [SerializeField] private StargazeController _controller;

        [Header("Session")]
        [SerializeField] private int _totalRounds = 3;
        [SerializeField] private float _transitionDelay = 2f;

        private enum SessionPhase { WaitingForStart, Playing, Transitioning, Complete }

        private SessionPhase _sessionPhase;
        private bool _gameplayActive;
        private bool _countdownDone;
        private float _postCountdownTimer = -1f;
        private float _transitionTimer;

        private int _currentRound;
        private ConstellationDatabase.ConstellationData[] _sessionConstellations;
        private float _sessionTimer;

        // Per-round timing
        private float[] _roundClearTimes;
        private float _roundStartTime;

        public override string MinigameId => "stargaze";
        public override bool IsComplete => _sessionPhase == SessionPhase.Complete;

        private GUIStyle _roundStyle;
        private GUIStyle _captionNameStyle;
        private GUIStyle _captionBodyStyle;
        private GUIStyle _hintStyle;

        protected override void Awake()
        {
            base.Awake();
            _roundClearTimes = new float[_totalRounds];
        }

        private void Start()
        {
            if (_controller == null)
                _controller = FindAnyObjectByType<StargazeController>();

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
            _sessionTimer = 0f;
            _gameplayActive = false;
            _countdownDone = false;
            _postCountdownTimer = -1f;
            _sessionPhase = SessionPhase.WaitingForStart;

            // Pick 3 random non-repeating constellations
            _sessionConstellations = ConstellationDatabase.GetRandomSet(_totalRounds);

            for (int i = 0; i < _roundClearTimes.Length; i++)
                _roundClearTimes[i] = 0f;
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
                    if (_breathAnalytics != null) _breathAnalytics.StartTracking();
                    BeginRound();
                }
                return;
            }

            if (!_gameplayActive || _controller == null) return;

            _sessionTimer += Time.deltaTime;

            switch (_sessionPhase)
            {
                case SessionPhase.Playing:
                    if (_controller.CurrentPhase == StargazeController.Phase.Clearing)
                    {
                        float breathPower = BreathPowerSystem.Instance != null
                            ? BreathPowerSystem.Instance.CurrentBreathPower : 0f;
                        _controller.UpdateBreathing(breathPower);
                    }
                    else if (_controller.CurrentPhase == StargazeController.Phase.RoundDone)
                    {
                        _roundClearTimes[_currentRound] = _sessionTimer - _roundStartTime;
                        _currentRound++;

                        if (_currentRound >= _totalRounds)
                        {
                            _sessionPhase = SessionPhase.Complete;
                            if (GameStateManager.Instance != null)
                                GameStateManager.Instance.TransitionTo(GameState.Celebration);
                        }
                        else
                        {
                            _sessionPhase = SessionPhase.Transitioning;
                            _transitionTimer = _transitionDelay;
                        }
                    }
                    else
                    {
                        // Auto-clearing, lines appearing, figure appearing, caption showing
                        _controller.UpdateAnimation();
                    }
                    break;

                case SessionPhase.Transitioning:
                    _transitionTimer -= Time.deltaTime;
                    if (_transitionTimer <= 0f)
                        BeginRound();
                    break;
            }
        }

        private void BeginRound()
        {
            if (_currentRound < _sessionConstellations.Length)
            {
                _controller.SetupConstellation(_sessionConstellations[_currentRound]);
                _sessionPhase = SessionPhase.Playing;
                _roundStartTime = _sessionTimer;
            }
        }

        public override string GetResultTitle() => "CONSTELLATIONS  REVEALED";

        public override string GetCelebrationTitle()
        {
            float activity = _breathAnalytics != null ? _breathAnalytics.AdjustedActivityRatio : 0f;
            if (activity >= 0.6f) return "STARGAZER!";
            if (activity >= 0.3f) return "SKY  WATCHER!";
            return "PEACEFUL  BREATHING";
        }

        public override string GetPersonalBestMessage()
        {
            // No competitive PBs for stargaze — return encouraging text
            return "Beautiful night sky exploration!";
        }

        public override MinigameStat[] GetEndStats()
        {
            float activity = _breathAnalytics != null ? _breathAnalytics.AdjustedActivityRatio : 0f;
            string pattern = _breathAnalytics != null ? _breathAnalytics.BreathPatternLabel : "N/A";
            float avgIntensity = _breathAnalytics != null ? _breathAnalytics.AverageIntensity : 0f;
            int sustained = _breathAnalytics != null ? _breathAnalytics.SustainedSegmentCount : 0;
            float avgDur = _breathAnalytics != null ? _breathAnalytics.AverageSustainedDuration : 0f;

            var stats = new List<MinigameStat>
            {
                new MinigameStat("Session Time", $"{_sessionTimer:F1}s", false, StatTier.Hero),
                new MinigameStat("Constellations", $"{_currentRound}", false, StatTier.Hero)
            };

            // Per-round clear times
            for (int i = 0; i < _currentRound && i < _totalRounds; i++)
            {
                string name = i < _sessionConstellations.Length
                    ? _sessionConstellations[i].Name : $"Round {i + 1}";
                stats.Add(new MinigameStat(name, $"{_roundClearTimes[i]:F1}s",
                    false, StatTier.Primary));
            }

            stats.Add(new MinigameStat("Avg Intensity", $"{avgIntensity * 100f:F0}%", false, StatTier.Secondary));
            stats.Add(new MinigameStat("Breaths", $"{sustained}", false, StatTier.Secondary));
            stats.Add(new MinigameStat("Avg Breath", $"{avgDur:F1}s", false, StatTier.Secondary));
            stats.Add(new MinigameStat("Pattern", pattern, false, StatTier.Secondary));
            stats.Add(new MinigameStat("Activity", FormatActivityGrade(activity), false, StatTier.Secondary));

            return stats.ToArray();
        }

        public override Dictionary<string, string> GetDebugInfo()
        {
            string conName = _currentRound < _sessionConstellations?.Length
                ? _sessionConstellations[_currentRound].Name : "—";
            float reveal = _controller != null ? _controller.RevealPercent : 0f;

            return new Dictionary<string, string>
            {
                ["Round"] = $"{_currentRound + 1}/{_totalRounds}",
                ["Constellation"] = conName,
                ["Reveal"] = $"{reveal:P0}",
                ["Phase"] = _controller != null ? _controller.CurrentPhase.ToString() : "N/A",
                ["Time"] = $"{_sessionTimer:F1}s"
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
            string roundText = $"CONSTELLATION  {displayRound}  /  {_totalRounds}";
            Rect roundRect = new Rect(0f, 20f, Screen.width, 36f);
            GameFont.OutlinedLabel(roundRect, roundText, _roundStyle, 2);

            // Breath hint during clearing phase
            if (_controller != null && _controller.CurrentPhase == StargazeController.Phase.Clearing)
            {
                float reveal = _controller.RevealPercent;
                string hint = reveal < 0.1f ? "BLOW  TO  CLEAR  THE  CLOUDS"
                    : reveal < 0.5f ? "KEEP  BLOWING..."
                    : reveal < 0.85f ? "ALMOST  THERE!"
                    : "";

                if (!string.IsNullOrEmpty(hint))
                {
                    float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 2f);
                    _hintStyle.normal.textColor = new Color(0.8f, 0.85f, 1f, pulse);
                    Rect hintRect = new Rect(0f, Screen.height - 80f, Screen.width, 36f);
                    GameFont.OutlinedLabel(hintRect, hint, _hintStyle);
                }
            }

            // Caption display during caption phase
            if (_controller != null &&
                (_controller.CurrentPhase == StargazeController.Phase.CaptionShowing ||
                 _controller.CurrentPhase == StargazeController.Phase.FigureAppearing))
            {
                float captionAlpha = _controller.CurrentPhase == StargazeController.Phase.CaptionShowing
                    ? 1f : Mathf.Clamp01(_controller.RevealPercent);

                _captionNameStyle.normal.textColor = new Color(1f, 0.95f, 0.7f, captionAlpha);
                _captionBodyStyle.normal.textColor = new Color(0.8f, 0.82f, 0.9f, captionAlpha * 0.9f);

                float bottomY = Screen.height - 200f;

                string nameText = _controller.CaptionName?.ToUpper().Replace(" ", "  ") ?? "";
                Rect nameRect = new Rect(40f, bottomY, Screen.width - 80f, 40f);
                GameFont.OutlinedLabel(nameRect, nameText, _captionNameStyle, 2);

                string sciText = _controller.CaptionScience ?? "";
                Rect sciRect = new Rect(40f, bottomY + 42f, Screen.width - 80f, 60f);
                GUI.Label(sciRect, sciText, _captionBodyStyle);

                string charText = _controller.CaptionCharacter ?? "";
                Rect charRect = new Rect(40f, bottomY + 104f, Screen.width - 80f, 60f);
                GUI.Label(charRect, charText, _captionBodyStyle);
            }
        }

        private void BuildHUDStyles()
        {
            if (_roundStyle != null) return;
            Font f = GameFont.Get();

            _roundStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _roundStyle.normal.textColor = new Color(0.7f, 0.75f, 0.9f);
            if (f != null) _roundStyle.font = f;

            _captionNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _captionNameStyle.normal.textColor = Color.white;
            if (f != null) _captionNameStyle.font = f;

            _captionBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            _captionBodyStyle.normal.textColor = new Color(0.8f, 0.82f, 0.9f);

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _hintStyle.normal.textColor = new Color(0.8f, 0.85f, 1f);
            if (f != null) _hintStyle.font = f;
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
