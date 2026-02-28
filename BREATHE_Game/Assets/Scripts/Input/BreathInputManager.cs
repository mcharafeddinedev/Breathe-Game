using UnityEngine;
using Breathe.Data;

namespace Breathe.Input
{
    /// <summary>
    /// Selects which physical (or simulated) breath source is active.
    /// </summary>
    public enum InputMode
    {
        Simulated,
        Microphone,
        Fan
    }

    /// <summary>
    /// Scene-scoped singleton that owns the three breath-input implementations
    /// and exposes a unified API for the rest of the game. Switch between
    /// <see cref="InputMode"/> at runtime via <see cref="SetInputMode"/>.
    /// </summary>
    public sealed class BreathInputManager : MonoBehaviour
    {

        [Header("Input Mode")]
        [SerializeField, Tooltip("Which breath input source to activate on Awake.")]
        private InputMode currentMode = InputMode.Simulated;

        [Header("Input Sources")]
        [SerializeField, Tooltip("Keyboard / gamepad simulated breath input.")]
        private SimulatedBreathInput simulatedInput;

        [SerializeField, Tooltip("Microphone-based breath input.")]
        private MicBreathInput micInput;

        [SerializeField, Tooltip("Arduino fan anemometer breath input.")]
        private FanBreathInput fanInput;

        [Header("Configuration")]
        [SerializeField, Tooltip("Shared breath-processing parameters.")]
        private BreathConfig breathConfig;

        private IBreathInput _activeInput;

        // ------------------------------------------------------------------ Singleton

        /// <summary>
        /// Scene-scoped singleton instance. <c>null</c> if no manager exists in the
        /// current scene. Does NOT persist across scene loads.
        /// </summary>
        public static BreathInputManager Instance { get; private set; }

        // ------------------------------------------------------------------ Public API

        /// <summary>
        /// The currently active <see cref="IBreathInput"/> implementation.
        /// </summary>
        public IBreathInput ActiveInput => _activeInput;

        /// <summary>The currently selected input mode enum value.</summary>
        public InputMode CurrentMode => currentMode;

        /// <summary>
        /// Display-friendly name of the current input source (enum value).
        /// </summary>
        public string InputSourceName => currentMode.ToString();

        /// <summary>
        /// The shared <see cref="Data.BreathConfig"/> asset used by all inputs.
        /// </summary>
        public BreathConfig BreathConfig => breathConfig;

        /// <summary>
        /// Convenience passthrough: returns the active input's breath intensity in [0, 1].
        /// </summary>
        public float GetBreathIntensity()
        {
            return _activeInput?.GetBreathIntensity() ?? 0f;
        }

        /// <summary>
        /// Convenience passthrough: returns the active input's discrete power level [0, 5].
        /// </summary>
        public int GetBreathLevel()
        {
            return _activeInput?.GetBreathLevel() ?? 0;
        }

        /// <summary>
        /// Convenience passthrough: true if the active input detects breath above threshold.
        /// </summary>
        public bool IsBreathing()
        {
            return _activeInput?.IsBreathing() ?? false;
        }

        /// <summary>
        /// Shuts down the current input source, switches to <paramref name="mode"/>,
        /// and initializes the new source.
        /// </summary>
        public void SetInputMode(InputMode mode)
        {
            _activeInput?.Shutdown();
            currentMode = mode;
            _activeInput = ResolveInput(mode);
            _activeInput?.Initialize();
            Debug.Log($"[BreathInputManager] Switched to {mode}");
        }

        // ------------------------------------------------------------------ Unity lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            _activeInput = ResolveInput(currentMode);
            _activeInput?.Initialize();
        }

        private void OnDestroy()
        {
            _activeInput?.Shutdown();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        // ------------------------------------------------------------------ Internal
        private IBreathInput ResolveInput(InputMode mode)
        {
            switch (mode)
            {
                case InputMode.Simulated:  return simulatedInput;
                case InputMode.Microphone: return micInput;
                case InputMode.Fan:        return fanInput;
                default:
                    Debug.LogWarning($"[BreathInputManager] Unknown input mode: {mode}");
                    return null;
            }
        }
    }
}
