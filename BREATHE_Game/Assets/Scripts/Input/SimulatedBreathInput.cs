using UnityEngine;
using UnityEngine.InputSystem;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Input
{
    /// <summary>
    /// Keyboard / gamepad breath simulation for development and accessibility.
    /// Hold Space to ramp intensity up; release to let it decay.
    /// Gamepad right trigger and left-stick Y provide analog alternatives.
    /// The highest value across all sources wins each frame.
    /// </summary>
    public sealed class SimulatedBreathInput : MonoBehaviour, IBreathInput
    {
        [Header("Configuration")]
        [SerializeField, Tooltip("Shared breath-processing parameters.")]
        private BreathConfig breathConfig;

        private float _keyboardIntensity;
        private float _smoothedIntensity;
        private int _previousLevel;
        private bool _active;

        // ------------------------------------------------------------------ IBreathInput
        /// <summary>
        /// Returns the current smoothed, dead-zoned breath intensity in [0, 1].
        /// </summary>
        public float GetBreathIntensity() => _smoothedIntensity;

        /// <summary>
        /// Returns a discrete power level in [0, 5] based on configured thresholds.
        /// </summary>
        public int GetBreathLevel()
        {
            return SignalProcessing.GetPowerLevel(_smoothedIntensity, breathConfig.PowerLevelThresholds);
        }

        /// <summary>
        /// True when the smoothed intensity exceeds the dead-zone threshold.
        /// </summary>
        public bool IsBreathing() => _smoothedIntensity > breathConfig.DeadZoneThreshold;

        /// <summary>
        /// Enables simulated input processing.
        /// </summary>
        public void Initialize()
        {
            _keyboardIntensity = 0f;
            _smoothedIntensity = 0f;
            _previousLevel = 0;
            _active = true;
            enabled = true;
        }

        /// <summary>
        /// Disables simulated input processing and resets state.
        /// </summary>
        public void Shutdown()
        {
            _active = false;
            _keyboardIntensity = 0f;
            _smoothedIntensity = 0f;
            enabled = false;
        }

        // ------------------------------------------------------------------ Unity lifecycle
        private void Update()
        {
            if (!_active || breathConfig == null) return;

            float dt = Time.deltaTime;

            UpdateKeyboardRamp(dt);
            float gamepadValue = ReadGamepadAnalog();
            float raw = Mathf.Max(_keyboardIntensity, gamepadValue);

            _smoothedIntensity = SignalProcessing.ExponentialMovingAverage(
                _smoothedIntensity, raw, breathConfig.SmoothingFactor);

            _smoothedIntensity = SignalProcessing.DeadZone(
                _smoothedIntensity, breathConfig.DeadZoneThreshold);

            _smoothedIntensity = Mathf.Clamp01(_smoothedIntensity);

            CheckLevelCrossing();
        }

        // ------------------------------------------------------------------ Internal
        private void UpdateKeyboardRamp(float dt)
        {
            bool spaceHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

            if (spaceHeld)
            {
                _keyboardIntensity += breathConfig.RampUpSpeed * dt;
            }
            else
            {
                _keyboardIntensity -= breathConfig.DecaySpeed * dt;
            }

            _keyboardIntensity = Mathf.Clamp01(_keyboardIntensity);
        }

        private float ReadGamepadAnalog()
        {
            if (Gamepad.current == null) return 0f;

            float trigger = Gamepad.current.rightTrigger.ReadValue();
            float stickY = Mathf.Clamp01(Gamepad.current.leftStick.ReadValue().y);

            return Mathf.Max(trigger, stickY);
        }

        private void CheckLevelCrossing()
        {
            int level = GetBreathLevel();
            if (level != _previousLevel)
            {
                Debug.Log($"[BreathInput] Simulated intensity level changed to {level}");
                _previousLevel = level;
            }
        }
    }
}
