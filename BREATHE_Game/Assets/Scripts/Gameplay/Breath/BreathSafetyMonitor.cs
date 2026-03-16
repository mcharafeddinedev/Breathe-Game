using System;
using UnityEngine;
using Breathe.Input;

namespace Breathe.Gameplay
{
    // Monitors prolonged high-effort breathing and suggests rest breaks.
    // Based on clinical literature (Joo et al. 2015) about preventing
    // dizziness during breath-based therapy games. Fires events for UI.
    public class BreathSafetyMonitor : MonoBehaviour
    {
        [Header("Thresholds")]
        [SerializeField, Tooltip("Intensity above this = 'high effort'.")]
        private float _highEffortThreshold = 0.6f;
        [SerializeField] private float _sustainedEffortWarningTime = 15f;
        [SerializeField] private float _cumulativeEffortWarningTime = 45f;
        [SerializeField, Tooltip("Seconds of low effort needed to reset cumulative timer.")]
        private float _recoveryDuration = 5f;

        [Header("Cooldown")]
        [SerializeField] private float _suggestionCooldown = 30f;

        private float _continuousHighEffortTime;
        private float _cumulativeHighEffortTime;
        private float _lowEffortTime;
        private float _lastSuggestionTime = -999f;
        private bool _breakSuggested;

        // UI should show a gentle rest suggestion when this fires
        public event Action OnBreakSuggested;
        // UI can dismiss the rest overlay when this fires
        public event Action OnRecovered;

        public bool IsBreakSuggested => _breakSuggested;
        public float ContinuousHighEffortTime => _continuousHighEffortTime;

        private void Update()
        {
            if (BreathInputManager.Instance == null) return;

            float intensity = BreathInputManager.Instance.GetBreathIntensity();
            float dt = Time.deltaTime;

            if (intensity >= _highEffortThreshold)
            {
                _continuousHighEffortTime += dt;
                _cumulativeHighEffortTime += dt;
                _lowEffortTime = 0f;
            }
            else
            {
                _continuousHighEffortTime = 0f;
                _lowEffortTime += dt;

                if (_lowEffortTime >= _recoveryDuration)
                {
                    _cumulativeHighEffortTime = 0f;
                    if (_breakSuggested)
                    {
                        _breakSuggested = false;
                        OnRecovered?.Invoke();
                    }
                }
            }

            if (_breakSuggested) return;

            bool shouldWarn = _continuousHighEffortTime >= _sustainedEffortWarningTime
                           || _cumulativeHighEffortTime >= _cumulativeEffortWarningTime;

            if (shouldWarn && Time.time - _lastSuggestionTime >= _suggestionCooldown)
            {
                _breakSuggested = true;
                _lastSuggestionTime = Time.time;
                Debug.Log("[BreathSafety] Break suggested — prolonged high-effort breathing detected.");
                OnBreakSuggested?.Invoke();
            }
        }
    }
}
