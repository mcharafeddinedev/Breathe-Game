using UnityEngine;
using Breathe.Input;

namespace Breathe.Gameplay
{
    // Turns breath input into a smoothed wind power value (0-1).
    // Sails and the wind meter always show raw breath effort — zones only affect speed.
    public class WindSystem : MonoBehaviour
    {
        [SerializeField, Tooltip("EMA alpha — higher = smoother but laggier.")]
        private float _smoothingFactor = 0.4f;

        [SerializeField, Tooltip("Response curve exponent. <1 means light breath still feels strong.")]
        private float _responseCurveExponent = 0.55f;

        [SerializeField, Tooltip("Log a message when wind power crosses these thresholds.")]
        private float[] _logThresholds = { 0.2f, 0.4f, 0.6f, 0.8f };

        private float _smoothedWindPower;
        private int _previousThresholdIndex = -1;

        public float WindPower { get; private set; }
        public float CurrentWindPower => WindPower;

        // No-op now. Zones modify speed directly in SailboatController,
        // so sail visuals still reflect real breath effort even in doldrums.
        public void SetEnvironmentalMultiplier(float mult) { }

        private void Update()
        {
            float rawBreath = 0f;
            if (BreathInputManager.Instance != null)
                rawBreath = BreathInputManager.Instance.GetBreathIntensity();

            float effective = Mathf.Clamp01(rawBreath);

            // EMA smoothing — frame-rate independent via Pow
            _smoothedWindPower = Mathf.Lerp(_smoothedWindPower, effective,
                1f - Mathf.Pow(_smoothingFactor, Time.deltaTime * 60f));

            // Response curve — makes low breath feel more impactful
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
                Debug.Log($"[Wind] Power: {WindPower:F2} (band {currentIndex + 1}/{_logThresholds.Length})");
            }
        }
    }
}
