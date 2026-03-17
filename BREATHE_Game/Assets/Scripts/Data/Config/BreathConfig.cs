using UnityEngine;

namespace Breathe.Data
{
    // Tuning values for the breath input processing pipeline.
    // Create new ones in the editor: Assets > Create > Breathe > Breath Config
    [CreateAssetMenu(fileName = "BreathConfig", menuName = "Breathe/Breath Config")]
    public class BreathConfig : ScriptableObject
    {
        [Header("Smoothing")]
        [SerializeField, Tooltip("EMA alpha. Higher = smoother but laggier.")]
        private float _smoothingFactor = 0.85f;

        [SerializeField, Tooltip("Values below this get clamped to zero.")]
        private float _deadZoneThreshold = 0.05f;

        [Header("Power Levels")]
        [SerializeField, Tooltip("Five ascending thresholds that divide intensity into power levels 0-5.")]
        private float[] _powerLevelThresholds = { 0.05f, 0.15f, 0.30f, 0.50f, 0.70f };

        [Header("Fan Hardware")]
        [SerializeField, Tooltip("Max useful deviation from Arduino (which now sends 0 at rest). " +
            "This is how much deviation = full power before the response curve. " +
            "Lower = more sensitive. Check Fan diagnostic 'clean' values in logs.")]
        private float _maxExpectedRPM = 250f;

        [Header("Simulated Input")]
        [SerializeField] private float _rampUpSpeed = 0.5f;
        [SerializeField] private float _decaySpeed = 2.0f;

        [Header("Microphone Calibration")]
        [SerializeField, Tooltip("Ambient noise floor (RMS amplitude). Values below this are treated as silence.")]
        private float _calibrationBaseline = 0.01f;
        [SerializeField, Tooltip("Expected RMS amplitude of a strong breath into the mic. " +
            "Typical breath is 0.05-0.20. Lower = more sensitive.")]
        private float _calibrationMax = 0.15f;

        public float SmoothingFactor => _smoothingFactor;
        public float DeadZoneThreshold => _deadZoneThreshold;
        public float[] PowerLevelThresholds => _powerLevelThresholds;
        public float MaxExpectedRPM => _maxExpectedRPM;
        public float RampUpSpeed => _rampUpSpeed;
        public float DecaySpeed => _decaySpeed;
        public float CalibrationBaseline => _calibrationBaseline;
        public float CalibrationMax => _calibrationMax;
    }
}
