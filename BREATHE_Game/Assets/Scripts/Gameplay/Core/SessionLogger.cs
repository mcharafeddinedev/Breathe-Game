using System;
using System.IO;
using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Saves per-session play data to JSON files for longitudinal tracking.
    // Each completed round appends a record to a daily NDJSON file at:
    //   Application.persistentDataPath/sessions/YYYY-MM-DD.jsonl
    // NDJSON (one JSON per line) is append-friendly, crash-safe, and easy to parse.
    public class SessionLogger : MonoBehaviour
    {
        private static SessionLogger _instance;
        public static SessionLogger Instance => _instance;

        private string _sessionDir;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            
            // DontDestroyOnLoad requires root GameObject - detach if parented
            if (transform.parent != null)
                transform.SetParent(null);
            DontDestroyOnLoad(gameObject);

            _sessionDir = Path.Combine(Application.persistentDataPath, "sessions");
            if (!Directory.Exists(_sessionDir))
                Directory.CreateDirectory(_sessionDir);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // Log a completed round. Call from GameStateManager when entering Celebration.
        public void LogRound(string minigameId, MinigameStat[] stats, BreathAnalytics analytics)
        {
            var record = new SessionRecord
            {
                timestamp = DateTime.Now.ToString("o"),
                minigameId = minigameId,
                stats = new StatEntry[stats != null ? stats.Length : 0],
                breathPattern = analytics != null ? analytics.BreathPatternLabel : "N/A",
                averageIntensity = analytics != null ? analytics.AverageIntensity : 0f,
                totalActiveTime = analytics != null ? analytics.TotalActiveTime : 0f,
                totalSessionTime = analytics != null ? analytics.TotalSessionTime : 0f,
                activityRatio = analytics != null ? analytics.ActivityRatio : 0f,
                adjustedActivityRatio = analytics != null ? analytics.AdjustedActivityRatio : 0f,
                sustainedSegments = analytics != null ? analytics.SustainedSegmentCount : 0,
                avgSustainedDuration = analytics != null ? analytics.AverageSustainedDuration : 0f,
                burstCount = analytics != null ? analytics.BurstCount : 0,
                zoneResponseRate = analytics != null ? analytics.ZoneResponseRate : 0f
            };

            if (stats != null)
            {
                for (int i = 0; i < stats.Length; i++)
                {
                    record.stats[i] = new StatEntry
                    {
                        label = stats[i].Label,
                        value = stats[i].Value,
                        isPersonalBest = stats[i].IsPersonalBest
                    };
                }
            }

            string json = JsonUtility.ToJson(record);
            string filePath = Path.Combine(_sessionDir, DateTime.Now.ToString("yyyy-MM-dd") + ".jsonl");

            try
            {
                File.AppendAllText(filePath, json + "\n");
                Debug.Log($"[SessionLogger] Round logged to {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SessionLogger] Failed to write: {ex.Message}");
            }
        }

        public void LogRound(string minigameId, MinigameStat[] stats) => LogRound(minigameId, stats, null);
        public string GetSessionDirectory() => _sessionDir;

        [Serializable]
        private struct SessionRecord
        {
            public string timestamp;
            public string minigameId;
            public StatEntry[] stats;
            public string breathPattern;
            public float averageIntensity;
            public float totalActiveTime;
            public float totalSessionTime;
            public float activityRatio;
            public float adjustedActivityRatio;
            public int sustainedSegments;
            public float avgSustainedDuration;
            public int burstCount;
            public float zoneResponseRate;
        }

        [Serializable]
        private struct StatEntry
        {
            public string label;
            public string value;
            public bool isPersonalBest;
        }
    }
}
