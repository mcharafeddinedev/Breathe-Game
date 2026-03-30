using UnityEngine;
using Breathe.Input;

namespace Breathe.Gameplay
{
    // Accumulates per-race breathing stats and persists personal bests via PlayerPrefs.
    // PB keys are scoped by minigame ID to prevent cross-game collisions.
    public class ScoreManager : MonoBehaviour
    {
        private const string KEY_BREATH_TIME = "PB_BreathTime";
        private const string KEY_PEAK_INTENSITY = "PB_PeakIntensity";
        private const string KEY_LONGEST_BLOW = "PB_LongestBlow";
        private const string KEY_COURSE_TIME = "PB_CourseTime";

        [Header("Scoping")]
        [SerializeField, Tooltip("Minigame ID prefix for PlayerPrefs keys (e.g. 'sailboat').")]
        private string _minigameScope = "sailboat";

        private string ScopedKey(string key) => $"{_minigameScope}_{key}";

        private static ScoreManager _instance;
        public static ScoreManager Instance { get => _instance; private set => _instance = value; }

        [Header("Race Reference")]
        [SerializeField] private CourseManager _courseManager;

        private float _totalBreathTime;
        private float _peakBreathIntensity;
        private float _longestSustainedBlow;
        private float _courseTime;
        private int _windZonesConquered;
        private float _currentBlowDuration;
        private bool _wasBreathing;

        private float _pbBreathTime;
        private float _pbPeakIntensity;
        private float _pbLongestBlow;
        private float _pbCourseTime;

        public float PBBreathTime => _pbBreathTime;
        public float PBPeakIntensity => _pbPeakIntensity;
        public float PBLongestBlow => _pbLongestBlow;
        public float PBCourseTime => _pbCourseTime;
        public float TotalBreathTime => _totalBreathTime;
        public float PeakBreathIntensity => _peakBreathIntensity;
        public float LongestSustainedBlow => _longestSustainedBlow;
        public float CourseTime => _courseTime;
        public int WindZonesConquered => _windZonesConquered;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            LoadPersonalBests();
        }

        private void OnDestroy() { if (_instance == this) _instance = null; }
        private void OnEnable() => ZoneEvents.OnZonePopup += HandleZonePopup;
        private void OnDisable() => ZoneEvents.OnZonePopup -= HandleZonePopup;

        private void Update()
        {
            if (_courseManager == null || !_courseManager.IsRaceActive) return;

            float dt = Time.deltaTime;
            _courseTime += dt;
            if (BreathInputManager.Instance == null) return;

            float intensity = BreathInputManager.Instance.GetBreathIntensity();
            bool breathing = BreathInputManager.Instance.IsBreathing();

            if (breathing)
            {
                _totalBreathTime += dt;
                _currentBlowDuration += dt;
            }
            else
            {
                if (_wasBreathing && _currentBlowDuration > _longestSustainedBlow)
                    _longestSustainedBlow = _currentBlowDuration;
                _currentBlowDuration = 0f;
            }
            _wasBreathing = breathing;

            if (intensity > _peakBreathIntensity)
                _peakBreathIntensity = intensity;
        }

        public void ResetStats()
        {
            _totalBreathTime = 0f;
            _peakBreathIntensity = 0f;
            _longestSustainedBlow = 0f;
            _courseTime = 0f;
            _windZonesConquered = 0;
            _currentBlowDuration = 0f;
            _wasBreathing = false;
        }

        public void SavePersonalBests()
        {
            if (_totalBreathTime > _pbBreathTime)
            { _pbBreathTime = _totalBreathTime; PlayerPrefs.SetFloat(ScopedKey(KEY_BREATH_TIME), _pbBreathTime); }
            if (_peakBreathIntensity > _pbPeakIntensity)
            { _pbPeakIntensity = _peakBreathIntensity; PlayerPrefs.SetFloat(ScopedKey(KEY_PEAK_INTENSITY), _pbPeakIntensity); }
            if (_longestSustainedBlow > _pbLongestBlow)
            { _pbLongestBlow = _longestSustainedBlow; PlayerPrefs.SetFloat(ScopedKey(KEY_LONGEST_BLOW), _pbLongestBlow); }

            bool courseTimeBetter = _pbCourseTime <= 0f || _courseTime < _pbCourseTime;
            if (courseTimeBetter && _courseTime > 0f)
            { _pbCourseTime = _courseTime; PlayerPrefs.SetFloat(ScopedKey(KEY_COURSE_TIME), _pbCourseTime); }

            PlayerPrefs.Save();
            Debug.Log($"[ScoreManager] Personal bests saved (scope: {_minigameScope}).");
        }

        public void LoadPersonalBests()
        {
            _pbBreathTime = PlayerPrefs.GetFloat(ScopedKey(KEY_BREATH_TIME), 0f);
            _pbPeakIntensity = PlayerPrefs.GetFloat(ScopedKey(KEY_PEAK_INTENSITY), 0f);
            _pbLongestBlow = PlayerPrefs.GetFloat(ScopedKey(KEY_LONGEST_BLOW), 0f);
            _pbCourseTime = PlayerPrefs.GetFloat(ScopedKey(KEY_COURSE_TIME), 0f);
        }

        // Check if this race's stat beats the stored personal best
        public bool IsNewPersonalBest(string statName)
        {
            return statName switch
            {
                "BreathTime"    => _totalBreathTime > _pbBreathTime,
                "PeakIntensity" => _peakBreathIntensity > _pbPeakIntensity,
                "LongestBlow"   => _longestSustainedBlow > _pbLongestBlow,
                "CourseTime"    => _pbCourseTime <= 0f || (_courseTime > 0f && _courseTime < _pbCourseTime),
                _               => false
            };
        }

        public void RecordZoneConquered() => _windZonesConquered++;

        private void HandleZonePopup(string zoneText)
        {
            if (BreathInputManager.Instance != null &&
                BreathInputManager.Instance.GetBreathIntensity() > 0.5f)
                RecordZoneConquered();
        }
    }
}
