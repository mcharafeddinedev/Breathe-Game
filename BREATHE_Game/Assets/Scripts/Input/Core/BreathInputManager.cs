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

    /// Singleton that manages which single breath input source is active.
    /// Only one source runs at a time — switching shuts down the old and initializes the new.
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
        private bool _wasBreathing;
        private float _breathLogTimer;
        private const float BreathLogInterval = 2f;

        public static BreathInputManager Instance { get; private set; }

        public IBreathInput ActiveInput => _activeInput;
        public InputMode CurrentMode => currentMode;
        public BreathConfig BreathConfig => breathConfig;
        public string InputSourceName => currentMode.ToString();

        public float GetBreathIntensity() => _activeInput?.GetBreathIntensity() ?? 0f;
        public int GetBreathLevel() => _activeInput?.GetBreathLevel() ?? 0;
        public bool IsBreathing() => _activeInput?.IsBreathing() ?? false;

        /// Switch to a new input source. Shuts down the previous one, initializes the new one,
        /// and logs exactly what happened.
        public void SetInputMode(InputMode mode)
        {
            InputMode previousMode = currentMode;

            if (_activeInput != null)
            {
                _activeInput.Shutdown();
                Debug.Log($"[BreathInputManager] {previousMode} input SHUT DOWN");
            }

            currentMode = mode;
            _activeInput = ResolveInput(mode);
            _activeInput?.Initialize();

            string simStatus   = mode == InputMode.Simulated  ? "ACTIVE" : "off";
            string micStatus   = mode == InputMode.Microphone ? "ACTIVE" : "off";
            string fanStatus   = mode == InputMode.Fan        ? "ACTIVE" : "off";

            Debug.Log($"[BreathInputManager] Input switched to {mode}\n" +
                $"  Simulated: {simStatus}  |  Microphone: {micStatus}  |  Fan: {fanStatus}");

            _wasBreathing = false;
            _breathLogTimer = 0f;
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

            Debug.Log($"[BreathInputManager] Initialized with {currentMode} input on startup");
        }

        private void Update()
        {
            if (_activeInput == null) return;

            bool breathing = _activeInput.IsBreathing();
            float intensity = _activeInput.GetBreathIntensity();

            if (breathing && !_wasBreathing)
                Debug.Log($"[BreathInput] Breath DETECTED on {currentMode} — intensity: {intensity:F3}");
            else if (!breathing && _wasBreathing)
                Debug.Log($"[BreathInput] Breath STOPPED on {currentMode}");

            _wasBreathing = breathing;

            _breathLogTimer += Time.deltaTime;
            if (_breathLogTimer >= BreathLogInterval)
            {
                _breathLogTimer = 0f;
                Debug.Log($"[BreathInput] Status — mode: {currentMode}, intensity: {intensity:F3}, " +
                    $"level: {_activeInput.GetBreathLevel()}, breathing: {breathing}");
            }
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
