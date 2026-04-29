using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // Sailboat race implementation of MinigameBase.
    // Freezes race-specific stats (placement, speed, course name) at the exact moment
    // the player crosses the finish line, then exposes them through GetEndStats().
    public class SailboatMinigame : MinigameBase
    {
        [Header("Sailboat References")]
        [SerializeField] private ScoreManager _scoreManager;
        [SerializeField] private CourseManager _courseManager;

        // Frozen at finish for accurate result display
        private int _frozenPlacement;
        private float _frozenKnots;
        private string _frozenCourseName;

        public override string MinigameId => "sailboat";
        public override bool IsComplete => _courseManager != null && _courseManager.PlayerFinished;

        private void OnEnable()
        {
            if (_courseManager != null)
                _courseManager.OnPlayerFinished += FreezeRaceStats;

            RaceResultOverlay.OnResultOverlayShowing += HandleOverlayShowing;
        }

        private void OnDisable()
        {
            if (_courseManager != null)
                _courseManager.OnPlayerFinished -= FreezeRaceStats;

            RaceResultOverlay.OnResultOverlayShowing -= HandleOverlayShowing;
        }

        private void FreezeRaceStats(float raceTime)
        {

            var playerBoat = FindAnyObjectByType<SailboatController>();
            var aiBoats = FindObjectsByType<AICompanionController>(FindObjectsSortMode.None);

            _frozenPlacement = 1;
            if (playerBoat != null && aiBoats != null)
            {
                float playerY = playerBoat.transform.position.y;
                foreach (var ai in aiBoats)
                    if (ai != null && ai.transform.position.y > playerY) _frozenPlacement++;
            }

            float rawSpeed = playerBoat != null ? playerBoat.CurrentSpeed : 0f;
            _frozenKnots = WindSpeedConverter.ToKnots(rawSpeed);

            var courseMarkers = FindAnyObjectByType<CourseMarkers>();
            string rawName = courseMarkers != null ? courseMarkers.ActiveLayoutName : "UNKNOWN";
            _frozenCourseName = rawName.ToUpper().Replace(" ", "  ");
        }

        private void HandleOverlayShowing()
        {
            var courseMarkers = FindAnyObjectByType<CourseMarkers>();
            courseMarkers?.BeginFadeOut();
        }

        public override void OnMinigameStart()
        {
            base.OnMinigameStart();
            if (_scoreManager != null) _scoreManager.ResetStats();
        }

        public override void OnMinigameEnd()
        {
            if (_scoreManager != null) _scoreManager.SavePersonalBests();
            base.OnMinigameEnd();
        }

        public override string GetResultTitle() => "RACE  COMPLETE";

        public override string GetCelebrationTitle()
        {
            float activity = _breathAnalytics != null ? _breathAnalytics.AdjustedActivityRatio : 0f;
            if (activity >= 0.7f) return "AMAZING  SAILING!";
            if (activity >= 0.4f) return "GREAT  SAILING!";
            return "RACE  COMPLETE!";
        }

        public override string GetPersonalBestMessage()
        {
            if (_scoreManager == null) return "";

            if (_scoreManager.IsNewPersonalBest("CourseTime") && _scoreManager.IsNewPersonalBest("BreathTime"))
                return "New personal bests for speed AND breath control!";
            if (_scoreManager.IsNewPersonalBest("CourseTime"))
                return "New fastest course time!";
            if (_scoreManager.IsNewPersonalBest("BreathTime"))
                return "New longest sail time!";
            if (_scoreManager.IsNewPersonalBest("LongestBlow"))
                return "New longest sustained blow!";

            return "Great race! Try again to beat your personal best!";
        }

        public override MinigameStat[] GetEndStats()
        {
            if (_scoreManager == null) return System.Array.Empty<MinigameStat>();

            float activity = _breathAnalytics != null ? _breathAnalytics.AdjustedActivityRatio : 0f;
            string pattern = _breathAnalytics != null ? _breathAnalytics.BreathPatternLabel : "N/A";
            int sustained = _breathAnalytics != null ? _breathAnalytics.SustainedSegmentCount : 0;
            float avgDur = _breathAnalytics != null ? _breathAnalytics.AverageSustainedDuration : 0f;

            string placementText = _frozenPlacement switch
            {
                1 => "1ST  PLACE",
                2 => "2ND  PLACE",
                3 => "3RD  PLACE",
                _ => $"{_frozenPlacement}TH  PLACE"
            };

            return new[]
            {
                new MinigameStat("Placement", placementText,
                    false, StatTier.Hero),
                new MinigameStat("Course Time", GameFont.FormatHudSecondsWhole(_scoreManager.CourseTime),
                    _scoreManager.IsNewPersonalBest("CourseTime"), StatTier.Hero),
                new MinigameStat("Breath Time", GameFont.FormatHudSecondsWhole(_scoreManager.TotalBreathTime),
                    _scoreManager.IsNewPersonalBest("BreathTime"), StatTier.Primary),
                new MinigameStat("Longest Blow", GameFont.FormatHudSecondsWhole(_scoreManager.LongestSustainedBlow),
                    _scoreManager.IsNewPersonalBest("LongestBlow"), StatTier.Primary),
                new MinigameStat("Peak", $"{_scoreManager.PeakBreathIntensity * 100f:F0}%",
                    _scoreManager.IsNewPersonalBest("PeakIntensity"), StatTier.Primary),
                new MinigameStat("Speed", $"{_frozenKnots:F1}  kts",
                    false, StatTier.Secondary),
                new MinigameStat("Pattern", pattern, false, StatTier.Secondary),
                new MinigameStat("Activity", FormatActivityGrade(activity), false, StatTier.Secondary),
                new MinigameStat("Breaths", $"{sustained}", false, StatTier.Secondary),
                new MinigameStat("Avg Length", GameFont.FormatHudSecondsWhole(avgDur), false, StatTier.Secondary),
                new MinigameStat("Course", _frozenCourseName ?? "UNKNOWN", false, StatTier.Secondary)
            };
        }

        public override Dictionary<string, string> GetDebugInfo()
        {
            var info = new Dictionary<string, string>();
            if (_courseManager != null)
            {
                info["Race"] = _courseManager.IsRaceActive ? "Active" : "Idle";
                info["Race Time"] = GameFont.FormatHudSecondsWhole(_courseManager.RaceTime);
            }
            if (_scoreManager != null)
            {
                info["Breath Time"] = GameFont.FormatHudSecondsWhole(_scoreManager.TotalBreathTime);
                info["Peak"] = $"{_scoreManager.PeakBreathIntensity:P0}";
                info["Zones"] = $"{_scoreManager.WindZonesConquered}";
            }
            return info;
        }

        // Wind power bar state
        private Texture2D _whiteTex;
        private GUIStyle _barLabelStyle;
        private float _smoothBreathPower;

        private void Update()
        {
            float breathPower = BreathPowerSystem.Instance != null
                ? BreathPowerSystem.Instance.CurrentBreathPower : 0f;
            _smoothBreathPower = Mathf.Lerp(_smoothBreathPower, breathPower, Time.deltaTime * 12f);
        }

        private void OnGUI()
        {
            if (Time.timeScale == 0f) return; // Don't draw HUD when paused
            if (GameStateManager.Instance == null ||
                GameStateManager.Instance.CurrentState != GameState.Playing)
                return;

            DrawWindBar();
        }

        private void DrawWindBar()
        {
            if (_whiteTex == null)
            {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }

            if (_barLabelStyle == null)
            {
                Font f = GameFont.Get();
                _barLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 24,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _barLabelStyle.normal.textColor = Color.white;
                // Flatten all states so text doesn't highlight on hover
                _barLabelStyle.hover = _barLabelStyle.normal;
                _barLabelStyle.active = _barLabelStyle.normal;
                _barLabelStyle.focused = _barLabelStyle.normal;
                if (f != null) _barLabelStyle.font = f;
            }

            float barWidth = 42f;
            float margin = 20f;
            
            // Draw on both sides
            DrawWindBarAt(margin); // Left side
            DrawWindBarAt(Screen.width - barWidth - margin); // Right side
        }

        private void DrawWindBarAt(float barX)
        {
            float barWidth = 42f;
            float padTop = 16f;
            float labelBand = 44f;
            float gapBelowBar = 8f;
            float padBottom = 10f;
            float barHeight = Mathf.Max(80f,
                Screen.height - padTop - labelBand - gapBelowBar - padBottom);
            float barY = padTop;
            float cornerInset = 5f;

            GUI.color = new Color(0.1f, 0.1f, 0.15f, 0.8f);
            GUI.DrawTexture(new Rect(barX - 3f, barY - 3f, barWidth + 6f, barHeight + 6f), _whiteTex);

            GUI.color = new Color(0.15f, 0.15f, 0.22f, 0.7f);
            GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), _whiteTex);

            float fill = Mathf.Clamp01(_smoothBreathPower);
            float fillHeight = barHeight * fill;
            float fillY = barY + barHeight - fillHeight;

            Color lowColor = new Color(0.3f, 0.7f, 0.95f);
            Color midColor = new Color(0.2f, 0.9f, 0.5f);
            Color highColor = new Color(1f, 0.85f, 0.2f);
            Color fillColor = fill < 0.5f
                ? Color.Lerp(lowColor, midColor, fill * 2f)
                : Color.Lerp(midColor, highColor, (fill - 0.5f) * 2f);
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(barX + cornerInset * 0.5f, fillY, barWidth - cornerInset, fillHeight), _whiteTex);

            if (fill > 0.02f)
            {
                GUI.color = new Color(fillColor.r, fillColor.g, fillColor.b, 0.4f);
                float glowH = Mathf.Min(8f, fillHeight);
                GUI.DrawTexture(new Rect(barX + cornerInset * 0.5f, fillY, barWidth - cornerInset, glowH), _whiteTex);
            }

            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            for (int i = 1; i <= 3; i++)
            {
                float tickY = barY + barHeight * (1f - i * 0.25f);
                GUI.DrawTexture(new Rect(barX, tickY, barWidth, 1f), _whiteTex);
            }

            GUI.color = Color.white;
            Rect labelRect = new Rect(barX - 14f, barY + barHeight + gapBelowBar, barWidth + 28f, labelBand);
            GameFont.OutlinedLabel(labelRect, "WIND", _barLabelStyle);
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
