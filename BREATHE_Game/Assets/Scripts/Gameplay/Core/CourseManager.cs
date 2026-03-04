using System;
using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Manages the race lifecycle: start, timing, finish-line detection, soft time-cap.
    // If CourseMarkers is assigned, waypoints are generated from the sine-curve spline
    // so boats always path between the buoy lanes.
    public class CourseManager : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CourseConfig _courseConfig;

        [Header("Course Layout")]
        [SerializeField, Tooltip("If set, waypoints come from the course curve. Otherwise uses manual waypoints below.")]
        private CourseMarkers _courseMarkers;
        [SerializeField] private float _waypointInterval = 3f;
        [SerializeField] private Transform[] _waypoints;
        [SerializeField, Tooltip("Leave empty when using CourseMarkers — one is created at runtime.")]
        private Transform _finishLine;
        private Transform _runtimeFinishLine;

        [Header("Boats")]
        [SerializeField] private SailboatController _playerBoat;
        [SerializeField] private AICompanionController[] _aiBoats;

        [Header("Analytics")]
        [SerializeField] private BreathAnalytics _breathAnalytics;

        [Header("Finish Detection")]
        [SerializeField] private float _finishThreshold = 1f;

        [Header("Auto Start")]
        [SerializeField, Tooltip("Auto-start race on scene load (testing shortcut).")]
        private bool _autoStart = true;

        [Header("Testing")]
        [SerializeField, Tooltip("Spawn a guaranteed obstacle in AI lanes so stun can be verified.")]
        private bool _forceStunForTesting = true;
        [SerializeField, Tooltip("Spawn one of each environmental zone along the course.")]
        private bool _debugEnvironmentalZones = true;
        [SerializeField] private float _forceStunObstacleDistance = 22f;

        private float _raceTimer;
        private bool _raceActive;
        private bool _playerFinished;

        public float RaceTime => _raceTimer;
        public bool IsRaceActive => _raceActive;
        public Transform[] Waypoints => _waypoints;
        public bool PlayerFinished => _playerFinished;
        public Transform FinishLine => _finishLine != null ? _finishLine : _runtimeFinishLine;
        public float FinishLineHalfWidth => _courseMarkers != null ? _courseMarkers.FinishLineHalfWidth : 13f;
        public CourseConfig CourseConfig => _courseConfig;

        // Fired when player crosses the finish line — payload is race time
        public event Action<float> OnPlayerFinished;

        private void Start()
        {
            if (_breathAnalytics == null)
                _breathAnalytics = FindAnyObjectByType<BreathAnalytics>();

            if (_courseMarkers != null)
                GenerateWaypointsFromCurve();

            InjectWaypointsIntoBoats();
            EnsureOverlays();

            if (_autoStart)
            {
                Debug.Log("[CourseManager] Auto-starting race (testing mode).");
                if (GameStateManager.Instance != null)
                    GameStateManager.Instance.TransitionTo(GameState.Tutorial);
            }
        }

        private void EnsureOverlays()
        {
            if (FindAnyObjectByType<ZonePopupFallback>() == null)
            {
                var go = new GameObject("ZonePopupFallback");
                go.AddComponent<ZonePopupFallback>();
            }
            if (FindAnyObjectByType<CountdownOverlay>() == null)
            {
                var go = new GameObject("CountdownOverlay");
                go.AddComponent<CountdownOverlay>();
            }
            if (FindAnyObjectByType<TutorialPopup>() == null)
            {
                var go = new GameObject("TutorialPopup");
                go.AddComponent<TutorialPopup>();
            }
            if (FindAnyObjectByType<PostRaceSailAway>() == null)
            {
                var go = new GameObject("PostRaceSailAway");
                go.AddComponent<PostRaceSailAway>();
            }
            if (FindAnyObjectByType<CourseObstacleSpawner>() == null)
            {
                var go = new GameObject("CourseObstacleSpawner");
                go.AddComponent<CourseObstacleSpawner>();
            }

            var zoneSpawner = FindAnyObjectByType<EnvironmentalZoneSpawner>();
            if (zoneSpawner == null)
            {
                var go = new GameObject("EnvironmentalZoneSpawner");
                zoneSpawner = go.AddComponent<EnvironmentalZoneSpawner>();
            }
            if (zoneSpawner != null && _debugEnvironmentalZones)
                zoneSpawner.SetDebugSpawnOneOfEach(true);

            if (FindAnyObjectByType<FinishLineCelebrationTrigger>() == null)
            {
                var go = new GameObject("FinishLineCelebration");
                go.AddComponent<CelebrationConfettiEffect>();
                go.AddComponent<FinishLineCelebrationTrigger>();
            }
        }

        private void GenerateWaypointsFromCurve()
        {
            float length = _courseMarkers.CourseLength;
            int count = Mathf.CeilToInt(length / _waypointInterval) + 1;

            var parent = new GameObject("--- GENERATED_WAYPOINTS ---");
            parent.transform.SetParent(transform);
            _waypoints = new Transform[count];

            for (int i = 0; i < count; i++)
            {
                float y = i * _waypointInterval;
                var wp = new GameObject($"WP_{i}");
                wp.transform.SetParent(parent.transform);
                wp.transform.position = new Vector3(_courseMarkers.CurveX(y), y, 0f);
                _waypoints[i] = wp.transform;
            }

            // Place finish line at end of course
            float finishX = _courseMarkers.CurveX(length);
            Vector3 finishPos = new Vector3(finishX, length, 0f);
            if (_finishLine != null)
            {
                _finishLine.position = new Vector3(finishX, length, _finishLine.position.z);
            }
            else
            {
                if (_runtimeFinishLine == null)
                {
                    var go = new GameObject("FinishLine_Runtime");
                    go.transform.SetParent(transform);
                    _runtimeFinishLine = go.transform;
                }
                _runtimeFinishLine.position = finishPos;
            }

            string wpInfo = "";
            for (int i = 0; i < Mathf.Min(5, count); i++)
            {
                Vector3 p = _waypoints[i].position;
                wpInfo += $"  WP{i}: ({p.x:F1}, {p.y:F1})";
            }
            Debug.Log($"[CourseManager] Generated {count} waypoints (interval={_waypointInterval}, length={length}).{wpInfo}");
        }

        private void InjectWaypointsIntoBoats()
        {
            if (_waypoints == null || _waypoints.Length == 0) return;
            if (_playerBoat != null) _playerBoat.SetWaypoints(_waypoints);
            if (_aiBoats != null)
                foreach (var ai in _aiBoats)
                    if (ai != null) ai.SetWaypoints(_waypoints);
        }

        // Call after the countdown completes
        public void StartRace()
        {
            _raceTimer = 0f;
            _raceActive = true;
            _playerFinished = false;

            SpawnGuaranteedTestObstaclesIfEnabled();
            AICompanionController.ResetCompetitiveWinState();
            if (_breathAnalytics != null) _breathAnalytics.StartTracking();
            Debug.Log("[CourseManager] Race started.");
        }

        public void StopRace()
        {
            _raceActive = false;
            Debug.Log($"[CourseManager] Race stopped at {_raceTimer:F2}s.");
        }

        // Index 0 = player, 1+ = AI boats
        public float[] GetAllBoatProgress()
        {
            int count = 1 + (_aiBoats != null ? _aiBoats.Length : 0);
            float[] progress = new float[count];
            progress[0] = _playerBoat != null ? _playerBoat.CourseProgress : 0f;
            if (_aiBoats != null)
                for (int i = 0; i < _aiBoats.Length; i++)
                    progress[i + 1] = _aiBoats[i] != null ? _aiBoats[i].CourseProgress : 0f;
            return progress;
        }

        private void Update()
        {
            if (!_raceActive) return;
            _raceTimer += Time.deltaTime;
            CheckSoftTimeCap();
            CheckPlayerFinish();
        }

        private void SpawnGuaranteedTestObstaclesIfEnabled()
        {
            if (!_forceStunForTesting) return;

            float spawnY = _playerBoat != null
                ? _playerBoat.transform.position.y + _forceStunObstacleDistance
                : _forceStunObstacleDistance;

            var spawner = FindAnyObjectByType<CourseObstacleSpawner>();
            if (spawner != null)
            {
                spawner.SpawnGuaranteedTestObstacle(spawnY, true);
                spawner.SpawnGuaranteedTestObstacle(spawnY, false);
                Debug.Log($"[CourseManager] Test obstacles (both lanes) at Y={spawnY:F1}.");
            }
        }

        // If the player runs out of time, slide the finish line in front of them
        private void CheckSoftTimeCap()
        {
            Transform finish = FinishLine;
            if (_courseConfig == null || finish == null || _playerBoat == null) return;
            if (_playerFinished) return;

            if (_raceTimer >= _courseConfig.SoftTimeCap && _playerBoat.CourseProgress < 1f)
            {
                Vector3 playerPos = _playerBoat.transform.position;
                finish.position = playerPos + _playerBoat.transform.up * (_finishThreshold * 3f);
                Debug.Log("[CourseManager] Soft time-cap reached — finish line moved ahead of player.");
            }
        }

        // Finish is a horizontal strip — player crosses when Y passes and X is within bounds
        private void CheckPlayerFinish()
        {
            if (_playerFinished) return;
            Transform finish = FinishLine;
            if (_playerBoat == null || finish == null) return;

            Vector3 playerPos = _playerBoat.transform.position;
            Vector3 finishPos = finish.position;
            float halfWidth = FinishLineHalfWidth;

            bool passedY = playerPos.y >= finishPos.y - _finishThreshold;
            bool withinX = Mathf.Abs(playerPos.x - finishPos.x) <= halfWidth + _finishThreshold;
            if (!passedY || !withinX) return;

            _playerFinished = true;
            _raceActive = false;
            Debug.Log($"[CourseManager] Player crossed finish line at {_raceTimer:F2}s!");
            OnPlayerFinished?.Invoke(_raceTimer);

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.TransitionTo(GameState.Celebration);
        }
    }
}
