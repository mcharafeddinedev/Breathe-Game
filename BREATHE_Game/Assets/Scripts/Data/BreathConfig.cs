using UnityEngine;

namespace Breathe.Data
{
    /// <summary>
    /// Tuning data for the breath-input processing pipeline.
    /// Create instances via <c>Assets → Create → Breathe → Breath Config</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "BreathConfig", menuName = "Breathe/Breath Config")]
    public class BreathConfig : ScriptableObject
    {
        [Header("Smoothing")]
        [SerializeField, Tooltip("Alpha for exponential moving average. Higher = smoother but slower response.")]
        private float _smoothingFactor = 0.85f;

        [SerializeField, Tooltip("Values below this threshold are clamped to zero.")]
        private float _deadZoneThreshold = 0.05f;

        [Header("Power Levels")]
        [SerializeField, Tooltip("Five ascending thresholds defining the boundaries between power levels 0→1, 1→2, 2→3, 3→4, 4→5.")]
        private float[] _powerLevelThresholds = { 0.05f, 0.15f, 0.30f, 0.50f, 0.70f };

        [Header("Microphone")]
        [SerializeField, Tooltip("Maximum expected microphone RPM/amplitude for normalization.")]
        private float _maxExpectedRPM = 1500f;

        [Header("Simulated Input")]
        [SerializeField, Tooltip("Simulated input ramp-up speed per second.")]
        private float _rampUpSpeed = 0.5f;

        [SerializeField, Tooltip("Simulated input decay speed per second.")]
        private float _decaySpeed = 2.0f;

        [Header("Calibration")]
        [SerializeField, Tooltip("Baseline calibration value (ambient noise floor).")]
        private float _calibrationBaseline = 0.02f;

        [SerializeField, Tooltip("Maximum calibration value (strongest expected breath).")]
        private float _calibrationMax = 0.8f;

        /// <summary>Alpha for exponential moving average. Higher = smoother but slower response.</summary>
        public float SmoothingFactor => _smoothingFactor;

        /// <summary>Values below this threshold are clamped to zero.</summary>
        public float DeadZoneThreshold => _deadZoneThreshold;

        /// <summary>Five ascending thresholds defining power-level boundaries (0→1 through 4→5).</summary>
        public float[] PowerLevelThresholds => _powerLevelThresholds;

        /// <summary>Maximum expected microphone RPM/amplitude for normalization.</summary>
        public float MaxExpectedRPM => _maxExpectedRPM;

        /// <summary>Simulated input ramp-up speed per second.</summary>
        public float RampUpSpeed => _rampUpSpeed;

        /// <summary>Simulated input decay speed per second.</summary>
        public float DecaySpeed => _decaySpeed;

        /// <summary>Baseline calibration value (ambient noise floor).</summary>
        public float CalibrationBaseline => _calibrationBaseline;

        /// <summary>Maximum calibration value (strongest expected breath).</summary>
        public float CalibrationMax => _calibrationMax;
    }
}
