using UnityEngine;
using Breathe.Input;

namespace Breathe.Gameplay
{
    /// <summary>
    /// Reads breath intensity from <see cref="BreathInputManager"/> and produces a
    /// smoothed wind-power value that drives sail visuals and speed.
    /// Sail inflation and wind meter always reflect raw input — environmental zones
    /// only affect movement speed (via SailboatController), not visual feedback.
    /// </summary>
    public class WindSystem : MonoBehaviour
    {
        [Header("Smoothing")]
        [SerializeField, Tooltip("EMA alpha for wind power smoothing. Higher = smoother but laggier.")]
        private float _smoothingFactor = 0.4f;

        [Header("Response Curve")]
        [SerializeField, Tooltip("Exponent for the wind power response curve. <1 = front-loaded (low breath feels stronger), >1 = back-loaded. 0.55 is a good default.")]
        private float _responseCurveExponent = 0.55f;

        [Header("Threshold Logging")]
        [SerializeField, Tooltip("Wind-power thresholds that trigger a log when crossed (ascending).")]
        private float[] _logThresholds = { 0.2f, 0.4f, 0.6f, 0.8f };

        private float _smoothedWindPower;
        private int _previousThresholdIndex = -1;

        /// <summary>
        /// Wind power from breath input (0–1). Used for sail visuals and speed.
        /// Always reflects raw input — environmental zones affect speed only, not this value.
        /// </summary>
        public float WindPower { get; private set; }

        /// <summary>Current raw (unsmoothed) wind power before clamping.</summary>
        public float CurrentWindPower => WindPower;

        /// <summary>
        /// No longer used. Environmental zones modify speed in SailboatController;
        /// sail visuals always show raw breath effort so players see their input in doldrums.
        /// </summary>
        public void SetEnvironmentalMultiplier(float mult)
        {
            // Intentionally no-op: sail inflation and wind meter reflect raw input.
            // Speed is scaled by _environmentalSpeedMultiplier in SailboatController.
        }

        private void Update()
        {
            float rawBreath = 0f;

            if (BreathInputManager.Instance != null)
                rawBreath = BreathInputManager.Instance.GetBreathIntensity();

            float effective = Mathf.Clamp01(rawBreath);

            _smoothedWindPower = Mathf.Lerp(_smoothedWindPower, effective,
                1f - Mathf.Pow(_smoothingFactor, Time.deltaTime * 60f));

            float curved = Mathf.Pow(Mathf.Clamp01(_smoothedWindPower), _responseCurveExponent);
            WindPower = Mathf.Clamp01(curved);

            LogThresholdCrossings();
        }

        private void LogThresholdCrossings()
        {
            if (_logThresholds == null || _logThresholds.Length == 0) return;

            int currentIndex = -1;
            for (int i = _logThresholds.Length - 1; i >= 0; i--)
            {
                if (WindPower >= _logThresholds[i])
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex != _previousThresholdIndex)
            {
                _previousThresholdIndex = currentIndex;
                Debug.Log($"[Wind] Power: {WindPower:F2} (threshold band {currentIndex + 1}/{_logThresholds.Length})");
            }
        }
    }
}
