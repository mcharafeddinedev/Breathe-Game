using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // Skydive minigame: guide skydivers onto bullseye targets by opposing wind with breath.
    // Wind pushes the skydiver left/right on a sine-wave cycle; breath pushes the opposite direction.
    // Session ends at 10 on-target landings or 3 off-target landings.
    public class SkydiveMinigame : MinigameBase
    {
        [Header("Skydive References")]
        [SerializeField] private SkydiverController _controller;

        [Header("Session")]
        [SerializeField] private int _successTarget = 10;
        [SerializeField] private int _missLimit = 3;
        [SerializeField] private float _delayBetweenDivers = 1.2f;

        [Header("Scoring")]
        [SerializeField] private int _perfectPoints = 300;
        [SerializeField] private int _goodPoints = 200;
        [SerializeField] private int _nearPoints = 100;

        private enum Phase { WaitingForStart, DiverFalling, DiverLanded, DiverDelay, Complete }

        private Phase _phase;
        private bool _gameplayActive;
        private bool _countdownDone;
        private float _postCountdownTimer = -1f;
        private float _delayTimer;

        private int _totalScore;
        private int _onTargetCount;
        private int _offTargetCount;
        private int _totalDivers;
        private int _perfectCount;
        private float _totalPrecision;

        // Personal bests
        private int _pbScore;
        private int _pbPerfects;
        private bool _newPBScore;
        private bool _newPBPerfects;

        private string PBScoreKey => $"{MinigameId}_PB_Score";
        private string PBPerfectsKey => $"{MinigameId}_PB_Perfects";

        public override string MinigameId => "skydive";
        public override bool IsComplete => _phase == Phase.Complete;

        private GUIStyle _hudStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _windStyle;
        private GUIStyle _feedbackStyle;

        private string _landingFeedbackText;
        private float _landingFeedbackTimer;
        private Color _landingFeedbackColor;

        private float _windChangeTimer;
        private GUIStyle _windChangeStyle;

        protected override void Awake()
        {
            base.Awake();
            LoadPersonalBests();
        }

        private void Start()
        {
            if (_controller == null)
                _controller = FindAnyObjectByType<SkydiverController>();

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
            _totalScore = 0;
            _onTargetCount = 0;
            _offTargetCount = 0;
            _totalDivers = 0;
            _perfectCount = 0;
            _totalPrecision = 0f;
            _gameplayActive = false;
            _countdownDone = false;
            _postCountdownTimer = -1f;
            _newPBScore = false;
            _newPBPerfects = false;
            _phase = Phase.WaitingForStart;
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
                    SpawnNextDiver();
                }
                return;
            }

            if (!_gameplayActive || _controller == null) return;

            float breathPower = BreathPowerSystem.Instance != null
                ? BreathPowerSystem.Instance.CurrentBreathPower : 0f;

            // Landing feedback fade
            if (_landingFeedbackTimer > 0f)
                _landingFeedbackTimer -= Time.deltaTime;

            switch (_phase)
            {
                case Phase.DiverFalling:
                    _controller.UpdateDiver(breathPower);

                    if (_controller.WindChangedThisFrame)
                        _windChangeTimer = 2.5f;
                    if (_windChangeTimer > 0f)
                        _windChangeTimer -= Time.deltaTime;

                    if (_controller.State == SkydiverController.DiverState.Landed)
                    {
                        _phase = Phase.DiverLanded;
                        ProcessLanding();
                    }
                    break;

                case Phase.DiverLanded:
                    _controller.UpdatePostLanding();
                    _delayTimer = _delayBetweenDivers;
                    _phase = Phase.DiverDelay;
                    break;

                case Phase.DiverDelay:
                    _controller.UpdatePostLanding();
                    _delayTimer -= Time.deltaTime;

                    if (_delayTimer <= 0f)
                    {
                        if (_onTargetCount >= _successTarget || _offTargetCount >= _missLimit)
                        {
                            _phase = Phase.Complete;
                            EvaluatePersonalBests();
                            if (GameStateManager.Instance != null)
                                GameStateManager.Instance.TransitionTo(GameState.Celebration);
                        }
                        else
                        {
                            SpawnNextDiver();
                        }
                    }
                    break;
            }
        }

        private void SpawnNextDiver()
        {
            _controller.SpawnDiver();
            _phase = Phase.DiverFalling;
            _totalDivers++;
        }

        private void ProcessLanding()
        {
            var quality = _controller.LastLandingQuality;
            float distance = _controller.LastLandingDistance;

            int points = quality switch
            {
                SkydiverController.LandingQuality.Perfect => _perfectPoints,
                SkydiverController.LandingQuality.Good => _goodPoints,
                SkydiverController.LandingQuality.Near => _nearPoints,
                _ => 0
            };

            _totalScore += points;

            // Precision: 1.0 for perfect center, decaying with distance
            float precision = Mathf.Clamp01(1f - distance / 3f);
            _totalPrecision += precision;

            if (quality == SkydiverController.LandingQuality.OffTarget)
            {
                _offTargetCount++;
                _landingFeedbackText = "OFF  TARGET";
                _landingFeedbackColor = new Color(1f, 0.4f, 0.3f);
            }
            else
            {
                _onTargetCount++;
                if (quality == SkydiverController.LandingQuality.Perfect)
                    _perfectCount++;

                _landingFeedbackText = quality switch
                {
                    SkydiverController.LandingQuality.Perfect => $"PERFECT!  +{points}",
                    SkydiverController.LandingQuality.Good => $"GOOD!  +{points}",
                    _ => $"CLOSE!  +{points}"
                };
                _landingFeedbackColor = quality switch
                {
                    SkydiverController.LandingQuality.Perfect => new Color(0.2f, 1f, 0.4f),
                    SkydiverController.LandingQuality.Good => new Color(0.5f, 0.9f, 0.3f),
                    _ => new Color(1f, 0.8f, 0.2f)
                };
            }

            _landingFeedbackTimer = 1.5f;
        }

        private void LoadPersonalBests()
        {
            _pbScore = PlayerPrefs.GetInt(PBScoreKey, 0);
            _pbPerfects = PlayerPrefs.GetInt(PBPerfectsKey, 0);
        }

        private void EvaluatePersonalBests()
        {
            if (_totalScore > _pbScore)
            {
                _newPBScore = true;
                _pbScore = _totalScore;
                PlayerPrefs.SetInt(PBScoreKey, _pbScore);
            }
            if (_perfectCount > _pbPerfects)
            {
                _newPBPerfects = true;
                _pbPerfects = _perfectCount;
                PlayerPrefs.SetInt(PBPerfectsKey, _pbPerfects);
            }
            PlayerPrefs.Save();
        }

        public override string GetResultTitle() => "SESSION  COMPLETE";

        public override string GetCelebrationTitle()
        {
            float accuracy = _totalDivers > 0 ? _totalPrecision / _totalDivers : 0f;
            if (_perfectCount >= 5) return "SKYDIVE  ACE!";
            if (accuracy >= 0.7f) return "GREAT  CONTROL!";
            if (_onTargetCount >= 8) return "NICE  LANDINGS!";
            return "GOOD  EFFORT!";
        }

        public override string GetPersonalBestMessage()
        {
            if (_newPBScore && _newPBPerfects)
                return "New personal bests for score AND perfect landings!";
            if (_newPBScore)
                return "New highest score!";
            if (_newPBPerfects)
                return "New most perfect landings!";
            return "Great session! Aim for more bullseyes!";
        }

        public override MinigameStat[] GetEndStats()
        {
            float activity = _breathAnalytics != null ? _breathAnalytics.AdjustedActivityRatio : 0f;
            string pattern = _breathAnalytics != null ? _breathAnalytics.BreathPatternLabel : "N/A";
            float avgIntensity = _breathAnalytics != null ? _breathAnalytics.AverageIntensity : 0f;
            float accuracy = _totalDivers > 0 ? _totalPrecision / _totalDivers * 100f : 0f;

            return new[]
            {
                new MinigameStat("Score", $"{_totalScore}", _newPBScore, StatTier.Hero),
                new MinigameStat("On Target", $"{_onTargetCount}  /  {_totalDivers}",
                    false, StatTier.Hero),
                new MinigameStat("Perfect", $"{_perfectCount}", _newPBPerfects, StatTier.Primary),
                new MinigameStat("Accuracy", $"{accuracy:F0}%", false, StatTier.Primary),
                new MinigameStat("Missed", $"{_offTargetCount}", false, StatTier.Primary),
                new MinigameStat("Avg Power", $"{avgIntensity * 100f:F0}%", false, StatTier.Secondary),
                new MinigameStat("Pattern", pattern, false, StatTier.Secondary),
                new MinigameStat("Activity", FormatActivityGrade(activity), false, StatTier.Secondary)
            };
        }

        public override Dictionary<string, string> GetDebugInfo()
        {
            float wind = _controller != null ? _controller.CurrentWindForce : 0f;
            return new Dictionary<string, string>
            {
                ["Score"] = $"{_totalScore}",
                ["On/Off"] = $"{_onTargetCount}/{_offTargetCount}",
                ["Perfects"] = $"{_perfectCount}",
                ["Wind"] = $"{wind:F2}",
                ["Phase"] = _phase.ToString()
            };
        }

        private void OnGUI()
        {
            if (!_gameplayActive) return;
            if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
                return;

            BuildHUDStyles();

            // Score — top left
            Rect scoreRect = new Rect(40f, 20f, 160f, 40f);
            GameFont.OutlinedLabel(scoreRect, $"{_totalScore}", _hudStyle, 2);
            Rect scoreLabelRect = new Rect(40f, 58f, 160f, 24f);
            GameFont.OutlinedLabel(scoreLabelRect, "SCORE", _labelStyle);

            // On-target counter — top center
            string targetText = $"{_onTargetCount}  /  {_successTarget}";
            Rect targetRect = new Rect(Screen.width * 0.5f - 80f, 20f, 160f, 40f);
            GameFont.OutlinedLabel(targetRect, targetText, _hudStyle, 2);
            Rect targetLabelRect = new Rect(Screen.width * 0.5f - 80f, 58f, 160f, 24f);
            GameFont.OutlinedLabel(targetLabelRect, "LANDINGS", _labelStyle);

            // Misses — top right
            string missText = $"{_offTargetCount}  /  {_missLimit}";
            Color missColor = _offTargetCount >= _missLimit - 1
                ? Color.Lerp(Color.red, Color.white, Mathf.PingPong(Time.time * 3f, 1f))
                : Color.white;
            _hudStyle.normal.textColor = missColor;
            Rect missRect = new Rect(Screen.width - 200f, 20f, 160f, 40f);
            GameFont.OutlinedLabel(missRect, missText, _hudStyle, 2);
            _hudStyle.normal.textColor = Color.white;
            Rect missLabelRect = new Rect(Screen.width - 200f, 58f, 160f, 24f);
            GameFont.OutlinedLabel(missLabelRect, "MISSES", _labelStyle);

            // Wind direction indicator
            if (_controller != null)
            {
                float wind = _controller.DisplayWindForce;
                string windDir = wind > 0.3f ? ">>>" : wind < -0.3f ? "<<<" : "CALM";
                float windAlpha = Mathf.Clamp01(Mathf.Abs(wind) / 2f);
                _windStyle.normal.textColor = new Color(0.8f, 0.85f, 1f, 0.4f + windAlpha * 0.6f);
                Rect windRect = new Rect(0f, Screen.height - 60f, Screen.width, 36f);
                GameFont.OutlinedLabel(windRect, $"WIND  {windDir}", _windStyle);
            }

            // Wind change popup
            if (_windChangeTimer > 0f)
            {
                if (_windChangeStyle == null)
                {
                    Font wf = GameFont.Get();
                    _windChangeStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 26,
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                    if (wf != null) _windChangeStyle.font = wf;
                }
                float wAlpha = Mathf.Clamp01(_windChangeTimer / 1.2f);
                _windChangeStyle.normal.textColor = new Color(1f, 0.9f, 0.5f, wAlpha);
                Rect wcRect = new Rect(0f, Screen.height * 0.55f, Screen.width, 40f);
                GameFont.OutlinedLabel(wcRect, "WIND  SHIFT!  BLOW  TO  GUIDE!", _windChangeStyle, 2);
            }

            // Landing feedback text
            if (_landingFeedbackTimer > 0f && !string.IsNullOrEmpty(_landingFeedbackText))
            {
                float alpha = Mathf.Clamp01(_landingFeedbackTimer / 0.8f);
                _feedbackStyle.normal.textColor = new Color(
                    _landingFeedbackColor.r, _landingFeedbackColor.g, _landingFeedbackColor.b, alpha);
                Rect feedbackRect = new Rect(0f, Screen.height * 0.35f, Screen.width, 50f);
                GameFont.OutlinedLabel(feedbackRect, _landingFeedbackText, _feedbackStyle, 2);
            }
        }

        private void BuildHUDStyles()
        {
            if (_hudStyle != null) return;
            Font f = GameFont.Get();

            _hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _hudStyle.normal.textColor = Color.white;
            if (f != null) _hudStyle.font = f;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
            if (f != null) _labelStyle.font = f;

            _windStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _windStyle.normal.textColor = Color.white;
            if (f != null) _windStyle.font = f;

            _feedbackStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _feedbackStyle.normal.textColor = Color.white;
            if (f != null) _feedbackStyle.font = f;
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
