using UnityEngine;
using Breathe.Data;

namespace Breathe.Input
{
    public enum InputMode
    {
        Simulated,
        Microphone,
        Fan
    }

    // Singleton that manages which breath input source is active.
    // Doesn't persist across scene loads — each scene gets its own.
    public sealed class BreathInputManager : MonoBehaviour
    {
        [Header("Input Mode")]
        [SerializeField, Tooltip("Which input source to use on startup.")]
        private InputMode currentMode = InputMode.Simulated;

        [Header("Input Sources")]
        [SerializeField] private SimulatedBreathInput simulatedInput;
        [SerializeField] private MicBreathInput micInput;
        [SerializeField] private FanBreathInput fanInput;

        [Header("Configuration")]
        [SerializeField] private BreathConfig breathConfig;

        private IBreathInput _activeInput;

        public static BreathInputManager Instance { get; private set; }

        public IBreathInput ActiveInput => _activeInput;
        public InputMode CurrentMode => currentMode;
        public string InputSourceName => currentMode.ToString();
        public BreathConfig BreathConfig => breathConfig;

        // Passthrough helpers so other scripts don't need to touch ActiveInput directly
        public float GetBreathIntensity() => _activeInput?.GetBreathIntensity() ?? 0f;
        public int GetBreathLevel() => _activeInput?.GetBreathLevel() ?? 0;
        public bool IsBreathing() => _activeInput?.IsBreathing() ?? false;

        // Switch input sources at runtime
        public void SetInputMode(InputMode mode)
        {
            _activeInput?.Shutdown();
            currentMode = mode;
            _activeInput = ResolveInput(mode);
            _activeInput?.Initialize();
            Debug.Log($"[BreathInputManager] Switched to {mode}");
        }

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
            if (Instance == this) Instance = null;
        }

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
