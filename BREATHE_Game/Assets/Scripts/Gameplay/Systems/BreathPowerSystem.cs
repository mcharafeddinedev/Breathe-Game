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

        public float BreathPower { get; private set; }
        public float CurrentBreathPower => BreathPower;

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
            BreathPower = Mathf.Clamp01(curved);

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
