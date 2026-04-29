using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;
using Breathe.Utility;
using Breathe.Audio;

namespace Breathe.Gameplay
{
    // Skydive minigame: guide skydivers onto bullseye targets by opposing wind with breath.
    // Wind pushes the skydiver left/right on a sine-wave cycle; breath pushes the opposite direction.
    // Session ends at N on-target landings or 3 off-target landings.
    public class SkydiveMinigame : MinigameBase
    {
        [Header("Skydive References")]
        [SerializeField] private SkydiverController _controller;

        [Header("Landing audio")]
        [SerializeField, Tooltip("Constellation-success chime on any on-target landing; descending miss sting on off-target.")]
        private bool _enableLandingStingers = true;

        [Header("Session")]
        [SerializeField] private int _successTarget = 5;
        [SerializeField] private int _missLimit = 3;

        [Header("Scoring")]
        [SerializeField] private int _perfectPoints = 300;
        [SerializeField] private int _goodPoints = 200;
        [SerializeField] private int _nearPoints = 100;

        private enum Phase { WaitingForStart, DiverFalling, DiverLanded, DiverBoost, Complete }

        private Phase _phase;
        private bool _gameplayActive;
        private bool _countdownDone;
        private float _postCountdownTimer = -1f;

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
        private Texture2D _topHudBandTex;

        private readonly ScorePopupPresenter _scorePopups = new ScorePopupPresenter();

        private ProceduralConstellationChimeBurst _procLandingSuccessChime;
        private ProceduralSkydiveMissStingerBurst _procLandingMiss;

        private float _windChangeTimer;
        private GUIStyle _windChangeStyle;
        private int _hudStylesBuiltForScreenH;

        protected override void Awake()
        {
            base.Awake();
            LoadPersonalBests();
        }

        protected override void Start()
        {
            base.Start();
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
            _scorePopups.Clear();
            _controller?.SetThrusterGameplayAllowed(false);
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
                    _controller?.SetThrusterGameplayAllowed(true);
                    SpawnNextDiver();
                }
                return;
            }

            if (!_gameplayActive || _controller == null) return;

            _scorePopups.Tick(Time.deltaTime);

            float breathPower = BreathPowerSystem.Instance != null
                ? BreathPowerSystem.Instance.CurrentBreathPower : 0f;

            switch (_phase)
            {
                case Phase.DiverFalling:
                    _controller.UpdateDiver(breathPower);

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
                    float camW = Camera.main.orthographicSize * Camera.main.aspect;
                    float nextTargetX = Random.Range(-camW * 0.6f, camW * 0.6f);
                    _controller.StartTransition(nextTargetX);
                    _phase = Phase.DiverBoost;
                    break;

                case Phase.DiverBoost:
                    _controller.UpdatePostLanding();

                    if (_controller.IsTransitionComplete)
                    {
                        if (_onTargetCount >= _successTarget || _offTargetCount >= _missLimit)
                        {
                            _phase = Phase.Complete;
                            _controller.EnterAmbientMode();
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

                case Phase.Complete:
                    _controller.UpdateAmbient();
                    break;
            }
        }

        private void SpawnNextDiver()
        {
            _controller.SpawnDiver();
            _phase = Phase.DiverFalling;
            _totalDivers++;

            _windChangeTimer = _controller.WindDirectionFlippedVersusPriorSpawn ? 1.8f : 0f;
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
                _scorePopups.Push("OFF  TARGET", new Color(1f, 0.4f, 0.3f));
                PlayLandingStinger(hitTarget: false);
            }
            else
            {
                _onTargetCount++;
                if (quality == SkydiverController.LandingQuality.Perfect)
                    _perfectCount++;

                string feedback = quality switch
                {
                    SkydiverController.LandingQuality.Perfect => $"PERFECT!  {points}  PTS",
                    SkydiverController.LandingQuality.Good => $"GOOD!  {points}  PTS",
                    _ => $"CLOSE!  {points}  PTS"
                };
                Color feedbackColor = quality switch
                {
                    SkydiverController.LandingQuality.Perfect => new Color(0.2f, 1f, 0.4f),
                    SkydiverController.LandingQuality.Good => new Color(0.5f, 0.9f, 0.3f),
                    _ => new Color(1f, 0.8f, 0.2f)
                };
                _scorePopups.Push(feedback, feedbackColor);
                PlayLandingStinger(hitTarget: true);
            }
        }

        private void PlayLandingStinger(bool hitTarget)
        {
            if (!_enableLandingStingers) return;
            EnsureLandingStingers();
            if (hitTarget)
                _procLandingSuccessChime?.Trigger();
            else
                _procLandingMiss?.Trigger();
        }

        private void EnsureLandingStingers()
        {
            if (!_enableLandingStingers) return;
            if (_procLandingSuccessChime != null && _procLandingMiss != null)
                return;

            if (_procLandingSuccessChime == null)
            {
                var goSucc = new GameObject("SkydiveLandingSuccessChime");
                goSucc.transform.SetParent(transform, false);
                goSucc.transform.localPosition = Vector3.zero;
                var srcS = goSucc.AddComponent<AudioSource>();
                srcS.playOnAwake = false;
                srcS.loop = false;
                srcS.spatialBlend = 0f;
                _procLandingSuccessChime = goSucc.AddComponent<ProceduralConstellationChimeBurst>();
            }

            if (_procLandingMiss == null)
            {
                var goMiss = new GameObject("SkydiveLandingMissStinger");
                goMiss.transform.SetParent(transform, false);
                goMiss.transform.localPosition = Vector3.zero;
                var srcM = goMiss.AddComponent<AudioSource>();
                srcM.playOnAwake = false;
                srcM.loop = false;
                srcM.spatialBlend = 0f;
                _procLandingMiss = goMiss.AddComponent<ProceduralSkydiveMissStingerBurst>();
            }
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
                new MinigameStat("On Target", GameFont.FormatResultsCountOfTotal(_onTargetCount, _totalDivers),
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
                ["On/Off"] = $"{_onTargetCount}  ON  {_offTargetCount}  OFF",
                ["Perfects"] = $"{_perfectCount}",
                ["Wind"] = $"{wind:F2}",
                ["Phase"] = _phase.ToString()
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
            float margin = 18f * sc;
            float numW = 240f * sc;
            float numH = 58f * sc;
            float lblH = 34f * sc;
            float rowGap = 6f * sc;
            float yNum = margin;
            float yLbl = yNum + numH + rowGap;

            _scorePopups.FontScale = Mathf.Clamp(sc * 1.18f, 1.1f, 1.75f);
            _scorePopups.DefaultYFraction = 0.72f; // Lower on screen, near the tree line

            EnsureTopHudBandTex();
            float topBandH = yLbl + lblH + margin;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, topBandH), _topHudBandTex, ScaleMode.StretchToFill);

            // Score — top left
            Rect scoreRect = new Rect(margin, yNum, numW, numH);
            GameFont.OutlinedLabel(scoreRect, $"{_totalScore}", _hudStyle, 2);
            Rect scoreLabelRect = new Rect(margin, yLbl, numW, lblH);
            GameFont.OutlinedLabel(scoreLabelRect, "SCORE", _labelStyle);

            // On-target counter — top center
            string targetText = GameFont.FormatHudCountOfTotal(_onTargetCount, _successTarget);
            float cx = (Screen.width - numW) * 0.5f;
            Rect targetRect = new Rect(cx, yNum, numW, numH);
            GameFont.OutlinedLabel(targetRect, targetText, _hudStyle, 2);
            Rect targetLabelRect = new Rect(cx, yLbl, numW, lblH);
            GameFont.OutlinedLabel(targetLabelRect, "LANDINGS", _labelStyle);

            // Misses — top right
            string missText = GameFont.FormatHudCountOfTotal(_offTargetCount, _missLimit);
            Color missColor = _offTargetCount >= _missLimit - 1
                ? Color.Lerp(Color.red, Color.white, Mathf.PingPong(Time.time * 3f, 1f))
                : Color.white;
            SetStyleColor(_hudStyle, missColor);
            float rx = Screen.width - margin - numW;
            Rect missRect = new Rect(rx, yNum, numW, numH);
            GameFont.OutlinedLabel(missRect, missText, _hudStyle, 2);
            SetStyleColor(_hudStyle, Color.white);
            Rect missLabelRect = new Rect(rx, yLbl, numW, lblH);
            GameFont.OutlinedLabel(missLabelRect, "MISSES", _labelStyle);

            // Wind direction indicator
            if (_controller != null)
            {
                float wind = _controller.DisplayWindForce;
                string windArrows = Mathf.Abs(wind) > 2f ? ">>>>>" :
                    Mathf.Abs(wind) > 1f ? ">>>" : ">";
                string windDir = wind > 0.3f ? windArrows : wind < -0.3f ?
                    windArrows.Replace('>', '<') : "---";
                float windAlpha = Mathf.Clamp01(Mathf.Abs(wind) / 2f);
                SetStyleColor(_windStyle, new Color(0.8f, 0.85f, 1f, 0.5f + windAlpha * 0.5f));
                float windH = 52f * sc;
                float windBottomPad = 22f * sc;
                Rect windRect = new Rect(0f, Screen.height - windBottomPad - windH, Screen.width, windH);
                GameFont.OutlinedLabel(windRect, windDir, _windStyle);
            }


            // Wind change popup
            if (_windChangeTimer > 0f)
            {
                float pulse = 0.92f + 0.08f * Mathf.Sin(Time.time * 5f);
                float fade = _windChangeTimer < 0.6f
                    ? _windChangeTimer / 0.6f
                    : 1f;
                float wAlpha = fade * pulse;
                SetStyleColor(_windChangeStyle, new Color(1f, 0.85f, 0.3f, wAlpha));
                float wcTop = topBandH + 52f * sc;
                float wcH = 80f * sc;
                Rect wcRect = new Rect(0f, wcTop, Screen.width, wcH);
                GameFont.OutlinedLabel(wcRect, "WIND  SHIFT", _windChangeStyle, 3);
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
            if (_hudStyle != null && _hudStylesBuiltForScreenH == Screen.height) return;
            _hudStylesBuiltForScreenH = Screen.height;
            Font f = GameFont.Get();

            _hudStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = HudFont(48),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _hudStyle.normal.textColor = Color.white;
            if (f != null) _hudStyle.font = f;
            FlattenHudStyle(_hudStyle);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = HudFont(26),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _labelStyle.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
            if (f != null) _labelStyle.font = f;
            FlattenHudStyle(_labelStyle);

            _windStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = HudFont(42),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _windStyle.normal.textColor = Color.white;
            if (f != null) _windStyle.font = f;
            FlattenHudStyle(_windStyle);

            _windChangeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = HudFont(62),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _windChangeStyle.normal.textColor = new Color(1f, 0.85f, 0.3f, 1f);
            if (f != null) _windChangeStyle.font = f;
            FlattenHudStyle(_windChangeStyle);
        }

        void EnsureTopHudBandTex()
        {
            if (_topHudBandTex != null) return;
            _topHudBandTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _topHudBandTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.5f));
            _topHudBandTex.Apply();
        }

        static void FlattenHudStyle(GUIStyle s)
        {
            s.hover = s.normal;
            s.active = s.normal;
            s.focused = s.normal;
        }

        static void SetStyleColor(GUIStyle s, Color c)
        {
            if (s == null) return;
            s.normal.textColor = c;
            s.hover.textColor = c;
            s.active.textColor = c;
            s.focused.textColor = c;
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
