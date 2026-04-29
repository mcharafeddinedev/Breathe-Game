using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Breathe.Audio;
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

        private enum SessionPhase { WaitingForStart, Playing, Complete }

        private SessionPhase _sessionPhase;
        private bool _gameplayActive;
        private bool _countdownDone;
        private float _postCountdownTimer = -1f;
        private int _currentRound;
        private ConstellationDatabase.ConstellationData[] _sessionConstellations;
        private float _sessionTimer;

        private float _roundSplashTimer;
        private static readonly float RoundSplashDuration = 2.5f;
        /// <summary>Cloud/breath tuning; same every round (ARCADECLASSIC has no reliable "/" or "|" for UI).</summary>
        private const float RoundDifficultyMultiplier = 1f;

        // Per-round timing
        private float[] _roundClearTimes;
        private float _roundStartTime;

        public override string MinigameId => "stargaze";
        public override bool IsComplete => _sessionPhase == SessionPhase.Complete;

        private GUIStyle _roundStyle;
        private GUIStyle _captionNameStyle;
        private GUIStyle _captionBodyStyle;
        private GUIStyle _hintStyle;
        private GUIStyle _starLabelStyle;
        private GUIStyle _discoveryStyle;
        private Texture2D _captionBgTex;
        private Texture2D _headerBarTex;

        private float _discoveryTimer;
        private string _discoveryText;
        private StargazeController.Phase _lastPhase;
        private static readonly string[] EncouragingPhrases = {
            "NICE  JOB!", "WELL  DONE!", "BEAUTIFUL!", "AMAZING!", "GREAT  WORK!"
        };

        // Continue button state — appears after ContinueDelay seconds in CaptionShowing
        private const float ContinueDelay = 6f;
        private const float AutoAdvanceTime = 20f;
        private bool _continueReady;
        private GUIStyle _continueStyle;
        private GUIStyle _countdownStyle;

        private Rect _continueRect;

        [Header("Procedural Audio")]
        [SerializeField]
        bool _enableStargazeWind = true;

        [SerializeField]
        bool _enableRevealChime = true;

        ProceduralStargazeWindAudio _procWind;

        ProceduralConstellationChimeBurst _procRevealChime;

        protected override void Awake()
        {
            base.Awake();
            _roundClearTimes = new float[_totalRounds];
        }

        protected override void Start()
        {
            base.Start();
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
            if (remaining == 3 && _controller != null && _sessionConstellations != null)
            {
                _controller.SetDifficulty(RoundDifficultyMultiplier);
                _controller.SetupConstellation(_sessionConstellations[_currentRound]);
                _roundSplashTimer = RoundSplashDuration;
            }
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
            // Tick cloud convergence during countdown so clouds animate in
            if (!_gameplayActive && _controller != null)
            {
                if (_controller.CurrentPhase == StargazeController.Phase.Converging)
                    _controller.UpdateAnimation();
                else if (_controller.CurrentPhase == StargazeController.Phase.Clearing)
                    _controller.UpdateBreathing(0f);
                TickStargazeWindAudio();
            }

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
                    _sessionPhase = SessionPhase.Playing;
                    _roundStartTime = _sessionTimer;
                }
                TickStargazeWindAudio();
                return;
            }

            if (!_gameplayActive || _controller == null) return;

            _sessionTimer += Time.deltaTime;

            if (_roundSplashTimer > 0f) _roundSplashTimer -= Time.deltaTime;
            if (_discoveryTimer > 0f) _discoveryTimer -= Time.deltaTime;

            if (_controller != null)
            {
                var curPhase = _controller.CurrentPhase;
                if (curPhase == StargazeController.Phase.AutoClearing && _lastPhase == StargazeController.Phase.Clearing)
                {
                    string phrase = EncouragingPhrases[Random.Range(0, EncouragingPhrases.Length)];
                    string conName = _controller.CaptionName?.ToUpper().Replace(" ", "  ") ?? "";
                    _discoveryText = $"{phrase}\nYOU  DISCOVERED  {conName}!";
                    _discoveryTimer = 3f;
                    PlayMinigameSpecialEventSfx();
                    if (_enableRevealChime)
                    {
                        EnsureRevealChime();
                        _procRevealChime?.Trigger();
                    }
                }
                _lastPhase = curPhase;
            }

            switch (_sessionPhase)
            {
                case SessionPhase.Playing:
                    if (_controller.CurrentPhase == StargazeController.Phase.Clearing)
                    {
                        float breathPower = BreathPowerSystem.Instance != null
                            ? BreathPowerSystem.Instance.CurrentBreathPower : 0f;
                        _controller.UpdateBreathing(breathPower);
                    }
                    else if (_controller.CurrentPhase == StargazeController.Phase.CaptionShowing)
                    {
                        _controller.UpdateAnimation();

                        float captionTime = _controller.CaptionPhaseTime;
                        _continueReady = captionTime >= ContinueDelay;

                        if (captionTime >= AutoAdvanceTime || (_continueReady && ShouldContinue()))
                            _controller.AdvanceToRoundDone();
                    }
                    else if (_controller.CurrentPhase == StargazeController.Phase.RoundDone)
                    {
                        _continueReady = false;
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
                            BeginRound();
                            _roundSplashTimer = RoundSplashDuration;
                        }
                    }
                    else
                    {
                        _controller.UpdateAnimation();
                    }
                    break;

            }

            TickStargazeWindAudio();

        }

        private void BeginRound()
        {
            if (_currentRound < _sessionConstellations.Length)
            {
                _controller.SetDifficulty(RoundDifficultyMultiplier);
                _controller.SetupConstellation(_sessionConstellations[_currentRound]);
                _sessionPhase = SessionPhase.Playing;
                _roundStartTime = _sessionTimer;
                if (_roundSplashTimer <= 0f) _roundSplashTimer = RoundSplashDuration;
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
                new MinigameStat("Session Time", GameFont.FormatHudSecondsWhole(_sessionTimer), false, StatTier.Hero),
                new MinigameStat("Constellations", $"{_currentRound}", false, StatTier.Hero)
            };

            // Per-round clear times
            for (int i = 0; i < _currentRound && i < _totalRounds; i++)
            {
                string name = i < _sessionConstellations.Length
                    ? _sessionConstellations[i].Name : $"Round {i + 1}";
                stats.Add(new MinigameStat(name, GameFont.FormatHudSecondsWhole(_roundClearTimes[i]),
                    false, StatTier.Primary));
            }

            stats.Add(new MinigameStat("Avg Intensity", $"{avgIntensity * 100f:F0}%", false, StatTier.Secondary));
            stats.Add(new MinigameStat("Breaths", $"{sustained}", false, StatTier.Secondary));
            stats.Add(new MinigameStat("Avg Breath", GameFont.FormatHudSecondsWhole(avgDur), false, StatTier.Secondary));
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
                ["Round"] = $"{_currentRound + 1}  OF  {_totalRounds}",
                ["Constellation"] = conName,
                ["Reveal"] = $"{reveal:P0}",
                ["Phase"] = _controller != null ? _controller.CurrentPhase.ToString() : "N/A",
                ["Time"] = GameFont.FormatHudSecondsWhole(_sessionTimer)
            };
        }

        private void OnGUI()
        {
            if (Time.timeScale == 0f) return; // Don't draw HUD when paused
            if (!_gameplayActive) return;
            if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
                return;

            BuildHUDStyles();
            float uiSc = Mathf.Clamp(Screen.height / 900f, 0.9f, 1.55f);

            // Header bar background
            if (_headerBarTex != null)
                GUI.DrawTexture(new Rect(0f, 0f, Screen.width, 70f), _headerBarTex, ScaleMode.StretchToFill);

            // Round indicator — top center
            int displayRound = Mathf.Min(_currentRound + 1, _totalRounds);
            string roundText = $"ROUND  {displayRound}  OF  {_totalRounds}";
            Rect roundRect = new Rect(0f, 14f, Screen.width, 52f);
            GameFont.OutlinedLabel(roundRect, roundText, _roundStyle, 2);

            // Auto-advance countdown — top right, pulsing
            if (_controller != null && _controller.CurrentPhase == StargazeController.Phase.CaptionShowing)
            {
                float remaining = Mathf.Max(0f, AutoAdvanceTime - _controller.CaptionPhaseTime);
                int secs = Mathf.CeilToInt(remaining);
                if (secs > 0 && secs <= AutoAdvanceTime)
                {
                    if (_countdownStyle == null)
                    {
                        Font fCd = GameFont.Get();
                        _countdownStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 32,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleRight
                        };
                        if (fCd != null) _countdownStyle.font = fCd;
                        FlattenLabelStyle(_countdownStyle);
                    }

                    float cdPulse = 0.55f + 0.45f * Mathf.Sin(Time.time * 2.5f);
                    Color cdColor = new Color(0.68f, 0.75f, 1f, cdPulse);
                    SetStyleColor(_countdownStyle, cdColor);

                    string cdText = _currentRound < _totalRounds - 1
                        ? $"NEXT  ROUND  IN  {secs} S"
                        : $"RESULTS  IN  {secs} S";
                    Rect cdRect = new Rect(0f, 20f, Screen.width - 30f, 46f);
                    GameFont.OutlinedLabel(cdRect, cdText, _countdownStyle, 2);
                }
            }

            // Discovery popup — between top HUD and constellation (tuned so it doesn’t crowd the figure)
            if (_discoveryTimer > 0f && !string.IsNullOrEmpty(_discoveryText))
            {
                float dAlpha = Mathf.Clamp01(_discoveryTimer / 0.5f);
                SetStyleColor(_discoveryStyle, new Color(0.4f, 1f, 0.6f, dAlpha));
                float discH = 60f * uiSc;
                float bandBottom = 102f * Mathf.Max(1f, uiSc);
                float skyAnchor = Screen.height * 0.22f;
                float midpointY = bandBottom + (skyAnchor - bandBottom) * 0.24f;
                float discY = Mathf.Clamp(midpointY - discH * 0.04f, bandBottom + 10f * uiSc, Screen.height * 0.195f);
                Rect discRect = new Rect(0f, discY, Screen.width, discH);
                GameFont.OutlinedLabel(discRect, _discoveryText, _discoveryStyle, 3);
            }

            // "ROUND X" splash during converge transition
            if (_roundSplashTimer > 0f)
            {
                float splashAlpha = Mathf.Clamp01(_roundSplashTimer / 0.5f);
                SetStyleColor(_captionNameStyle, new Color(0.85f, 0.92f, 1f, splashAlpha));
                string splashText = $"ROUND  {displayRound}";
                Rect splashRect = new Rect(0f, Screen.height * 0.36f, Screen.width, 72f);
                GameFont.OutlinedLabel(splashRect, splashText, _captionNameStyle, 3);
            }

            // Phase hints — large, vertically centered (not bottom-docked)
            float hintH = 112f * uiSc;
            float hintY = Screen.height * 0.48f - hintH * 0.5f;
            var hintRect = new Rect(0f, hintY, Screen.width, hintH);

            // Hint during converging phase: "GET READY..."
            if (_controller != null && _controller.CurrentPhase == StargazeController.Phase.Converging)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
                SetStyleColor(_hintStyle, new Color(0.82f, 0.88f, 1f, pulse));
                GameFont.OutlinedLabel(hintRect, "GET  READY...", _hintStyle, 3);
            }

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
                    SetStyleColor(_hintStyle, new Color(0.82f, 0.88f, 1f, pulse));
                    GameFont.OutlinedLabel(hintRect, hint, _hintStyle, 3);
                }
            }

            // Star name labels — fade in during figure/caption reveal
            if (_controller != null &&
                (_controller.CurrentPhase == StargazeController.Phase.FigureAppearing ||
                 _controller.CurrentPhase == StargazeController.Phase.CaptionShowing))
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    float labelAlpha = _controller.FigureAlpha;
                    SetStyleColor(_starLabelStyle, new Color(0.85f, 0.9f, 1f, labelAlpha));

                    var placed = new System.Collections.Generic.List<Rect>();
                    for (int i = 0; i < _controller.ConstellationStarCount; i++)
                    {
                        string starName = _controller.GetStarName(i);
                        if (string.IsNullOrEmpty(starName)) continue;
                        starName = starName.ToUpper().Replace(" ", "  ");

                        Vector3 screenPos = cam.WorldToScreenPoint(_controller.GetStarWorldPosition(i));
                        float guiY = Screen.height - screenPos.y;
                        float labelW = Mathf.Min(starName.Length * 16f + 40f, 320f);
                        Rect rect = new Rect(screenPos.x - labelW * 0.5f, guiY - 36f, labelW, 32f);

                        float[] offsets = { 0f, 60f, -60f, 120f, -120f };
                        foreach (float off in offsets)
                        {
                            Rect test = new Rect(rect.x, rect.y + off, rect.width, rect.height);
                            bool collision = false;
                            for (int j = 0; j < placed.Count; j++)
                            {
                                if (test.Overlaps(placed[j])) { collision = true; break; }
                            }
                            if (!collision) { rect = test; break; }
                        }

                        placed.Add(rect);
                        GameFont.OutlinedLabel(rect, starName, _starLabelStyle, 2);
                    }
                }
            }

            // Caption display during caption phase
            if (_controller != null &&
                (_controller.CurrentPhase == StargazeController.Phase.CaptionShowing ||
                 _controller.CurrentPhase == StargazeController.Phase.FigureAppearing))
            {
                float captionAlpha = _controller.CurrentPhase == StargazeController.Phase.CaptionShowing
                    ? 1f : Mathf.Clamp01(_controller.RevealPercent);

                SetStyleColor(_captionNameStyle, new Color(0.85f, 0.9f, 1f, captionAlpha));
                SetStyleColor(_captionBodyStyle, new Color(0.78f, 0.82f, 0.92f, captionAlpha * 0.9f));

                float margin = 16f;
                float innerPad = 40f;

                float nameH = 50f;
                float sciH = 70f;          // Fits 2 lines at font 30 (longest ~100 chars may wrap)
                float charH = 50f;         // Fits 1-2 lines (character descriptions are shorter)
                float gapAfterName = 18f;  // Clear space between title and first sentence
                float gapAfterSci = 4f;    // Minimal gap between sentences
                float gapAfterChar = 10f;
                float btnH = _continueReady ? 42f : 0f;
                float totalContent = nameH + gapAfterName + sciH + gapAfterSci + charH + gapAfterChar + btnH;
                float bgPadV = 18f;
                float bgH = totalContent + bgPadV * 2f;
                float bottomY = Screen.height - bgH - 4f;

                if (_captionBgTex != null)
                {
                    Color prevCol = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, captionAlpha);
                    GUI.DrawTexture(new Rect(margin, bottomY, Screen.width - margin * 2f, bgH), _captionBgTex, ScaleMode.StretchToFill);
                    GUI.color = prevCol;
                }

                float y = bottomY + bgPadV;

                string nameText = _controller.CaptionName?.ToUpper().Replace(" ", "  ") ?? "";
                GameFont.OutlinedLabel(new Rect(innerPad, y, Screen.width - innerPad * 2f, nameH), nameText, _captionNameStyle, 2);
                y += nameH + gapAfterName;

                string sciText = GameFont.ExpandPronunciationHintsForPixelFont(_controller.CaptionScience ?? "")
                    .ToUpperInvariant()
                    .Replace(" ", "  ");
                GUI.Label(new Rect(innerPad, y, Screen.width - innerPad * 2f, sciH), sciText, _captionBodyStyle);
                y += sciH + gapAfterSci;

                string charText = GameFont.ExpandPronunciationHintsForPixelFont(_controller.CaptionCharacter ?? "")
                    .ToUpperInvariant()
                    .Replace(" ", "  ");
                GUI.Label(new Rect(innerPad, y, Screen.width - innerPad * 2f, charH), charText, _captionBodyStyle);
                y += charH + gapAfterChar;

                if (_continueReady)
                {
                    if (_continueStyle == null)
                    {
                        Font fCont = GameFont.Get();
                        _continueStyle = new GUIStyle(GUI.skin.label)
                        {
                            fontSize = 30,
                            fontStyle = FontStyle.Bold,
                            alignment = TextAnchor.MiddleCenter
                        };
                        if (fCont != null) _continueStyle.font = fCont;
                    }

                    // Pulsing warm amber to grab attention
                    float pulse = 0.7f + 0.3f * Mathf.Sin(Time.time * 3.5f);
                    _continueStyle.normal.textColor = new Color(1f, 0.82f, 0.4f, pulse);

                    float btnW = Screen.width - innerPad * 2f;
                    float btnX = innerPad;
                    _continueRect = new Rect(btnX, y, btnW, btnH);
                    GameFont.OutlinedLabel(_continueRect, "CLICK  OR  PRESS  ENTER  TO  CONTINUE", _continueStyle, 2);
                }
                else
                {
                    _continueRect = Rect.zero;
                }
            }
        }

        private void BuildHUDStyles()
        {
            if (_roundStyle != null) return;
            Font f = GameFont.Get();

            _roundStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _roundStyle.normal.textColor = new Color(0.68f, 0.75f, 1f);
            if (f != null) _roundStyle.font = f;

            _captionNameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _captionNameStyle.normal.textColor = new Color(0.85f, 0.9f, 1f);
            if (f != null) _captionNameStyle.font = f;

            _captionBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            _captionBodyStyle.normal.textColor = new Color(0.78f, 0.82f, 0.92f);
            if (f != null) _captionBodyStyle.font = f;

            _starLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };
            _starLabelStyle.normal.textColor = new Color(0.85f, 0.9f, 1f);
            if (f != null) _starLabelStyle.font = f;

            _discoveryStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _discoveryStyle.normal.textColor = new Color(0.4f, 1f, 0.6f);
            if (f != null) _discoveryStyle.font = f;

            _hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 54,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _hintStyle.normal.textColor = new Color(0.82f, 0.88f, 1f);
            if (f != null) _hintStyle.font = f;

            // Flatten hover states so non-interactive text doesn't highlight
            FlattenLabelStyle(_roundStyle);
            FlattenLabelStyle(_captionNameStyle);
            FlattenLabelStyle(_captionBodyStyle);
            FlattenLabelStyle(_starLabelStyle);
            FlattenLabelStyle(_discoveryStyle);
            FlattenLabelStyle(_hintStyle);

            _captionBgTex = new Texture2D(1, 1);
            _captionBgTex.SetPixel(0, 0, new Color(0.05f, 0.06f, 0.14f, 0.9f));
            _captionBgTex.Apply();

            _headerBarTex = new Texture2D(1, 1);
            _headerBarTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.5f));
            _headerBarTex.Apply();
        }

        private void EnsureProcWind()
        {
            if (_procWind != null || !_enableStargazeWind) return;

            GameObject windGo = new GameObject("StargazeWindAudio");
            windGo.transform.SetParent(transform, false);
            windGo.transform.localPosition = Vector3.zero;

            AudioSource src = windGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;

            _procWind = windGo.AddComponent<ProceduralStargazeWindAudio>();
        }

        private void EnsureRevealChime()
        {
            if (_procRevealChime != null || !_enableRevealChime) return;

            GameObject chimeGo = new GameObject("StargazeRevealChime");
            chimeGo.transform.SetParent(transform, false);
            chimeGo.transform.localPosition = Vector3.zero;

            AudioSource src = chimeGo.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;

            _procRevealChime = chimeGo.AddComponent<ProceduralConstellationChimeBurst>();
        }

        private void TickStargazeWindAudio()
        {
            if (!_enableStargazeWind || _controller == null) return;

            if (_sessionPhase == SessionPhase.Complete)
            {
                EnsureProcWind();
                _procWind?.Tick(0f, 0f);
                return;
            }

            EnsureProcWind();

            float gust = 0f;
            float mist = 0f;
            float breath = BreathPowerSystem.Instance != null ? BreathPowerSystem.Instance.CurrentBreathPower : 0f;
            var cp = _controller.CurrentPhase;

            switch (cp)
            {
                case StargazeController.Phase.Converging:
                    mist = 0.13f;
                    gust = 0.035f;
                    break;

                case StargazeController.Phase.Clearing:
                    {
                        float gate = Mathf.Max(0.02f, _controller.ClearingBreathGate);

                        if (breath >= gate)
                        {
                            float norm = Mathf.Clamp01((breath - gate) / Mathf.Max(0.0001f, 1f - gate));
                            float reveal = _controller.RevealPercent;

                            gust = Mathf.Clamp01(norm * Mathf.Lerp(0.55f, 1f, reveal));
                            mist = Mathf.Clamp01((1f - norm) * 0.16f + (1f - reveal) * 0.05f);
                        }
                        else
                        {
                            float low01 = Mathf.Clamp01(breath / Mathf.Max(gate, 1e-4f));
                            gust = 0f;
                            mist = Mathf.Lerp(0.36f, 0.14f, low01);
                        }
                        break;
                    }

                case StargazeController.Phase.AutoClearing:
                    gust = 0.2f;
                    mist = 0.07f;
                    break;

                case StargazeController.Phase.LinesAppearing:
                case StargazeController.Phase.FigureAppearing:
                    gust = 0.11f;
                    mist = 0.065f;
                    break;

                case StargazeController.Phase.CaptionShowing:
                    gust = 0.065f;
                    mist = 0.05f;
                    break;

                case StargazeController.Phase.RoundDone:
                default:
                    gust = mist = 0.02f;
                    break;
            }

            _procWind.Tick(gust, mist);
        }

        private bool ShouldContinue()
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                return true;

            // Click anywhere to continue
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                return true;

            return false;
        }

        private static string FormatActivityGrade(float ratio)
        {
            if (ratio >= 0.7f) return "Excellent";
            if (ratio >= 0.5f) return "Good";
            if (ratio >= 0.3f) return "Fair";
            return "Low";
        }

        /// <summary>Flatten hover/active/focused states so text doesn't highlight on mouseover.</summary>
        private static void FlattenLabelStyle(GUIStyle s)
        {
            if (s == null) return;
            s.hover = s.normal;
            s.active = s.normal;
            s.focused = s.normal;
        }

        /// <summary>Set text color on all states so dynamically-colored text doesn't highlight on hover.</summary>
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
