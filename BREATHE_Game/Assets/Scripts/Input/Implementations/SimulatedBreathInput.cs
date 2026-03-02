using UnityEngine;
using UnityEngine.InputSystem;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Input
{
    // Keyboard/gamepad breath sim for testing and accessibility.
    // Hold Space to ramp up, release to decay. Gamepad right trigger
    // and left stick Y also work — highest value wins each frame.
    public sealed class SimulatedBreathInput : MonoBehaviour, IBreathInput
    {
        [SerializeField] private BreathConfig breathConfig;

        private float _keyboardIntensity;
        private float _smoothedIntensity;
        private int _previousLevel;
        private bool _active;

        public float GetBreathIntensity() => _smoothedIntensity;

        public int GetBreathLevel()
        {
            return SignalProcessing.GetPowerLevel(_smoothedIntensity, breathConfig.PowerLevelThresholds);
        }

        public bool IsBreathing() => _smoothedIntensity > breathConfig.DeadZoneThreshold;

        public void Initialize()
        {
            _keyboardIntensity = 0f;
            _smoothedIntensity = 0f;
            _previousLevel = 0;
            _active = true;
            enabled = true;
        }

        public void Shutdown()
        {
            _active = false;
            _keyboardIntensity = 0f;
            _smoothedIntensity = 0f;
            enabled = false;
        }

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

        private void UpdateKeyboardRamp(float dt)
        {
            bool spaceHeld = Keyboard.current != null && Keyboard.current.spaceKey.isPressed;

            if (spaceHeld)
                _keyboardIntensity += breathConfig.RampUpSpeed * dt;
            else
                _keyboardIntensity -= breathConfig.DecaySpeed * dt;

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
