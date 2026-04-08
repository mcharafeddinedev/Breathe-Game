using UnityEngine;
using Breathe.Input;

namespace Breathe.Gameplay
{
    // Turns breath input into a smoothed power value (0-1).
    // This is the universal breath-to-game-power pipeline shared by all minigames.
    // Each minigame reads BreathPower and maps it to game-specific behavior.
    public class BreathPowerSystem : MonoBehaviour
    {
        private static BreathPowerSystem _instance;
        public static BreathPowerSystem Instance => _instance;

        [SerializeField, Tooltip("EMA alpha — higher = smoother but laggier.")]
        private float _smoothingFactor = 0.4f;

        [SerializeField, Tooltip("Response curve exponent. <1 means light breath still feels strong.")]
        private float _responseCurveExponent = 0.55f;

        [SerializeField, Tooltip("Log a message when breath power crosses these thresholds.")]
        private float[] _logThresholds = { 0.2f, 0.4f, 0.6f, 0.8f };

        private float _smoothedBreathPower;
        private int _previousThresholdIndex = -1;

        // Spin-down detection: snaps power to 0 when the fan is clearly winding
        // down (sustained decline of >= threshold within window). While suppressed,
        // tracks the lowest raw intensity (the trough). Resumes when raw rises
        // resumeDelta above the trough — meaning the user is actively blowing
        // again, even if the fan hasn't fully stopped.
        //
        // Defaults work well for Sailboat. Other minigames can call
        // ConfigureSpinDown() with different values from their definition assets.
        public const float DefaultSpinDownThreshold = 0.12f;
        public const float DefaultSpinDownWindow = 1.0f;
        public const float DefaultResumeRiseDelta = 0.06f;

        private float _spinDownThreshold = DefaultSpinDownThreshold;
        private float _spinDownWindow = DefaultSpinDownWindow;
        private float _resumeRiseDelta = DefaultResumeRiseDelta;
        private float _decayBaseline;
        private float _declineTimer;
        private bool _inputSuppressed;
        private float _suppressedRawTrough;
        private float _lastComputedPower;
        private float _lastRawIntensity;

        public float BreathPower { get; private set; }
        public float CurrentBreathPower => BreathPower;

        /// <summary>
        /// Per-minigame spin-down tuning. Lower threshold = more aggressive snap-to-zero.
        /// Set threshold to a very high value (e.g. 999) to effectively disable spin-down.
        /// </summary>
        public void ConfigureSpinDown(float threshold, float window, float resumeDelta)
        {
            _spinDownThreshold = threshold;
            _spinDownWindow = window;
            _resumeRiseDelta = resumeDelta;
            Debug.Log($"[BreathPower] Spin-down configured: threshold={threshold:F2}, window={window:F1}s, resume={resumeDelta:F2}");
        }

        public void ResetSpinDownDefaults()
        {
            _spinDownThreshold = DefaultSpinDownThreshold;
            _spinDownWindow = DefaultSpinDownWindow;
            _resumeRiseDelta = DefaultResumeRiseDelta;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            float rawBreath = 0f;
            if (BreathInputManager.Instance != null)
                rawBreath = BreathInputManager.Instance.GetBreathIntensity();

            float effective = Mathf.Clamp01(rawBreath);

            _smoothedBreathPower = Mathf.Lerp(_smoothedBreathPower, effective,
                1f - Mathf.Pow(_smoothingFactor, Time.deltaTime * 60f));

            float curved = Mathf.Pow(Mathf.Clamp01(_smoothedBreathPower), _responseCurveExponent);
            float computedPower = Mathf.Clamp01(curved);

            if (!_inputSuppressed)
            {
                float drop = _lastComputedPower - computedPower;
                if (drop > 0.001f)
                {
                    if (_declineTimer <= 0f)
                        _decayBaseline = _lastComputedPower;
                    _declineTimer += Time.deltaTime;

                    if (_declineTimer <= _spinDownWindow &&
                        (_decayBaseline - computedPower) >= _spinDownThreshold)
                    {
                        _inputSuppressed = true;
                        _suppressedRawTrough = effective;
                        _smoothedBreathPower = 0f;
                        Debug.Log($"[BreathPower] Spin-down detected (baseline {_decayBaseline:F2} → {computedPower:F2}). Suppressing to 0.");
                    }
                }
                else
                {
                    _declineTimer = 0f;
                }
            }
            else
            {
                _smoothedBreathPower = 0f;
                _suppressedRawTrough = Mathf.Min(_suppressedRawTrough, effective);

                if (effective > _suppressedRawTrough + _resumeRiseDelta)
                {
                    _inputSuppressed = false;
                    _declineTimer = 0f;
                    Debug.Log($"[BreathPower] Input resumed (raw {effective:F3}, trough was {_suppressedRawTrough:F3}).");
                }
            }

            _lastComputedPower = computedPower;
            _lastRawIntensity = effective;
            BreathPower = _inputSuppressed ? 0f : computedPower;

            LogThresholdCrossings();
        }

        private void LogThresholdCrossings()
        {
            if (_logThresholds == null || _logThresholds.Length == 0) return;

            int currentIndex = -1;
            for (int i = _logThresholds.Length - 1; i >= 0; i--)
            {
                if (BreathPower >= _logThresholds[i])
                {
                    currentIndex = i;
                    break;
                }
            }

            if (currentIndex != _previousThresholdIndex)
            {
                _previousThresholdIndex = currentIndex;
                Debug.Log($"[BreathPower] Power: {BreathPower:F2} (band {currentIndex + 1}/{_logThresholds.Length})");
            }
        }
    }
}
