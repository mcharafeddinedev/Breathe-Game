using UnityEngine;
using Breathe.Input;

namespace Breathe.Gameplay
{
    // Tracks detailed breath analytics per play session — goes beyond what
    // ScoreManager captures. Covers clinical data requirements:
    // sustained segments, burst count, average intensity, breath pattern, zone response.
    // Runs alongside ScoreManager but owns universal (not minigame-specific) metrics.
    public class BreathAnalytics : MonoBehaviour
    {
        [Header("Classification Thresholds")]
        [SerializeField, Tooltip("Min seconds of continuous breath to count as sustained.")]
        private float _sustainedSegmentMinDuration = 1.5f;
        [SerializeField, Tooltip("Max seconds for a breath event to be classified as a burst.")]
        private float _burstMaxDuration = 0.8f;
        [SerializeField] private float _activeThreshold = 0.05f;

        private bool _isActive;
        private float _currentSegmentDuration;
        private bool _wasBreathing;

        private float _totalIntensityAccumulator;
        private int _intensitySampleCount;

        private int _sustainedSegmentCount;
        private float _sustainedSegmentTotalDuration;
        private int _burstCount;
        private float _totalActiveTime;
        private float _totalSessionTime;

        private float _lastZoneMultiplier = 1f;
        private float _effortBeforeZoneChange;
        private int _zoneTransitionCount;
        private int _appropriateZoneResponseCount;

        public int SustainedSegmentCount => _sustainedSegmentCount;
        public float AverageSustainedDuration =>
            _sustainedSegmentCount > 0 ? _sustainedSegmentTotalDuration / _sustainedSegmentCount : 0f;
        public int BurstCount => _burstCount;
        public float AverageIntensity =>
            _intensitySampleCount > 0 ? _totalIntensityAccumulator / _intensitySampleCount : 0f;
        public float TotalActiveTime => _totalActiveTime;
        public float TotalSessionTime => _totalSessionTime;
        public float ActivityRatio =>
            _totalSessionTime > 0f ? _totalActiveTime / _totalSessionTime : 0f;

        // Classifies the overall breath pattern based on segment data
        public string BreathPatternLabel
        {
            get
            {
                if (_sustainedSegmentCount == 0 && _burstCount == 0) return "Minimal";
                float ratio = _burstCount > 0
                    ? (float)_sustainedSegmentCount / _burstCount
                    : float.MaxValue;

                if (ratio > 2f) return "Sustained";
                if (ratio < 0.5f) return "Burst-Dominant";
                if (_burstCount >= 5 && AverageSustainedDuration < 2f) return "Rhythmic";
                return "Mixed";
            }
        }

        // How often the player adjusted effort appropriately when entering a zone
        public float ZoneResponseRate =>
            _zoneTransitionCount > 0 ? (float)_appropriateZoneResponseCount / _zoneTransitionCount : 0f;

        public void StartTracking() { _isActive = true; ResetAll(); }
        public void StopTracking() { _isActive = false; FinalizeCurrentSegment(); }

        public void ResetAll()
        {
            _currentSegmentDuration = 0f;
            _wasBreathing = false;
            _totalIntensityAccumulator = 0f;
            _intensitySampleCount = 0;
            _sustainedSegmentCount = 0;
            _sustainedSegmentTotalDuration = 0f;
            _burstCount = 0;
            _totalActiveTime = 0f;
            _totalSessionTime = 0f;
            _lastZoneMultiplier = 1f;
            _effortBeforeZoneChange = 0f;
            _zoneTransitionCount = 0;
            _appropriateZoneResponseCount = 0;
        }

        // Call when the environmental speed multiplier changes — captures effort before/after
        public void NotifyZoneChange(float newMultiplier)
        {
            if (!_isActive) return;
            if (Mathf.Approximately(newMultiplier, _lastZoneMultiplier)) return;

            float currentIntensity = BreathInputManager.Instance != null
                ? BreathInputManager.Instance.GetBreathIntensity() : 0f;

            _effortBeforeZoneChange = currentIntensity;
            _lastZoneMultiplier = newMultiplier;
            _zoneTransitionCount++;
        }

        // Call a few seconds after zone entry to check if the player responded well
        public void EvaluateZoneResponse()
        {
            if (!_isActive || BreathInputManager.Instance == null) return;

            float currentIntensity = BreathInputManager.Instance.GetBreathIntensity();
            bool isHarderZone = _lastZoneMultiplier < 1f;
            bool isEasierZone = _lastZoneMultiplier > 1f;

            bool appropriateResponse = false;
            if (isHarderZone && currentIntensity > _effortBeforeZoneChange + 0.05f)
                appropriateResponse = true;
            else if (isEasierZone)
                appropriateResponse = true;
            else if (Mathf.Approximately(_lastZoneMultiplier, 1f))
                appropriateResponse = true;

            if (appropriateResponse) _appropriateZoneResponseCount++;
        }

        private void Update()
        {
            if (!_isActive || BreathInputManager.Instance == null) return;

            float dt = Time.deltaTime;
            float intensity = BreathInputManager.Instance.GetBreathIntensity();
            bool breathing = intensity > _activeThreshold;

            _totalSessionTime += dt;
            _totalIntensityAccumulator += intensity;
            _intensitySampleCount++;

            if (breathing)
            {
                _totalActiveTime += dt;
                _currentSegmentDuration += dt;
            }
            else
            {
                if (_wasBreathing) FinalizeCurrentSegment();
                _currentSegmentDuration = 0f;
            }

            _wasBreathing = breathing;
        }

        private void FinalizeCurrentSegment()
        {
            if (_currentSegmentDuration <= 0f) return;

            if (_currentSegmentDuration >= _sustainedSegmentMinDuration)
            {
                _sustainedSegmentCount++;
                _sustainedSegmentTotalDuration += _currentSegmentDuration;
            }
            else if (_currentSegmentDuration <= _burstMaxDuration && _currentSegmentDuration > 0.05f)
            {
                _burstCount++;
            }
        }
    }
}
