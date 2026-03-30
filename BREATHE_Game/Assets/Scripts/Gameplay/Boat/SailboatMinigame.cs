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
                new MinigameStat("Course Time", $"{_scoreManager.CourseTime:F1}s",
                    _scoreManager.IsNewPersonalBest("CourseTime"), StatTier.Hero),
                new MinigameStat("Breath Time", $"{_scoreManager.TotalBreathTime:F1}s",
                    _scoreManager.IsNewPersonalBest("BreathTime"), StatTier.Primary),
                new MinigameStat("Longest Blow", $"{_scoreManager.LongestSustainedBlow:F1}s",
                    _scoreManager.IsNewPersonalBest("LongestBlow"), StatTier.Primary),
                new MinigameStat("Peak", $"{_scoreManager.PeakBreathIntensity * 100f:F0}%",
                    _scoreManager.IsNewPersonalBest("PeakIntensity"), StatTier.Primary),
                new MinigameStat("Speed", $"{_frozenKnots:F1}  kts",
                    false, StatTier.Secondary),
                new MinigameStat("Pattern", pattern, false, StatTier.Secondary),
                new MinigameStat("Activity", FormatActivityGrade(activity), false, StatTier.Secondary),
                new MinigameStat("Breaths", $"{sustained}", false, StatTier.Secondary),
                new MinigameStat("Avg Length", $"{avgDur:F1}s", false, StatTier.Secondary),
                new MinigameStat("Course", _frozenCourseName ?? "UNKNOWN", false, StatTier.Secondary)
            };
        }

        public override Dictionary<string, string> GetDebugInfo()
        {
            var info = new Dictionary<string, string>();
            if (_courseManager != null)
            {
                info["Race"] = _courseManager.IsRaceActive ? "Active" : "Idle";
                info["Race Time"] = $"{_courseManager.RaceTime:F1}s";
            }
            if (_scoreManager != null)
            {
                info["Breath Time"] = $"{_scoreManager.TotalBreathTime:F1}s";
                info["Peak"] = $"{_scoreManager.PeakBreathIntensity:P0}";
                info["Zones"] = $"{_scoreManager.WindZonesConquered}";
            }
            return info;
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
