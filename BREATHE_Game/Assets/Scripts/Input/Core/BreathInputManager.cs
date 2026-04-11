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
        /// <summary>PlayerPrefs key shared with SettingsManager for input mode persistence.</summary>
        public const string PrefKeyInputMode = "Breathe_InputMode";

        [Header("Input Mode")]
        [SerializeField, Tooltip("Fallback if no saved preference exists in PlayerPrefs.")]
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
        private bool _menuMode;

        public static BreathInputManager Instance { get; private set; }

        public IBreathInput ActiveInput => _activeInput;
        public InputMode CurrentMode => currentMode;
        public BreathConfig BreathConfig => breathConfig;
        public string InputSourceName => currentMode.ToString();
        public bool IsMenuMode => _menuMode;

        public float GetBreathIntensity()
        {
            if (_menuMode)
            {
                float sim = simulatedInput != null ? simulatedInput.GetBreathIntensity() : 0f;
                float fan = fanInput != null ? fanInput.GetBreathIntensity() : 0f;
                return Mathf.Max(sim, fan);
            }
            return _activeInput?.GetBreathIntensity() ?? 0f;
        }

        public int GetBreathLevel()
        {
            if (_menuMode)
            {
                int simLvl = simulatedInput?.GetBreathLevel() ?? 0;
                int fanLvl = fanInput?.GetBreathLevel() ?? 0;
                return Mathf.Max(simLvl, fanLvl);
            }
            return _activeInput?.GetBreathLevel() ?? 0;
        }

        public bool IsBreathing()
        {
            if (_menuMode)
                return (simulatedInput?.IsBreathing() ?? false) || (fanInput?.IsBreathing() ?? false);
            return _activeInput?.IsBreathing() ?? false;
        }

        /// Activates both Simulated and Fan input simultaneously while blocking Mic.
        /// Used in the main menu so spacebar AND fan device both drive breath navigation.
        public void EnableMenuMode()
        {
            if (_menuMode) return;
            _menuMode = true;

            if (currentMode == InputMode.Microphone)
                micInput?.Shutdown();

            if (currentMode != InputMode.Simulated)
                simulatedInput?.Initialize();
            if (currentMode != InputMode.Fan)
                fanInput?.Initialize();

            Debug.Log("[BreathInputManager] Menu mode ENABLED — Simulated + Fan active, Mic blocked");
        }

        /// Shuts down menu mode and restores single-source operation.
        public void DisableMenuMode(InputMode restoreMode)
        {
            if (!_menuMode) return;
            _menuMode = false;

            simulatedInput?.Shutdown();
            fanInput?.Shutdown();
            micInput?.Shutdown();

            currentMode = restoreMode;
            _activeInput = ResolveInput(restoreMode);
            _activeInput?.Initialize();

            _wasBreathing = false;
            _breathLogTimer = 0f;

            Debug.Log($"[BreathInputManager] Menu mode DISABLED — restored to {restoreMode}");
        }

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
            DontDestroyOnLoad(gameObject);

            if (PlayerPrefs.HasKey(PrefKeyInputMode))
            {
                var saved = (InputMode)PlayerPrefs.GetInt(PrefKeyInputMode);
                if (saved != currentMode)
                {
                    Debug.Log($"[BreathInputManager] Restoring saved input mode: {saved} (was {currentMode})");
                    currentMode = saved;
                }
            }

            _activeInput = ResolveInput(currentMode);
            _activeInput?.Initialize();

            Debug.Log($"[BreathInputManager] Initialized with {currentMode} input on startup");
        }

        private void Update()
        {
            if (!_menuMode && _activeInput == null) return;

            bool breathing = IsBreathing();
            float intensity = GetBreathIntensity();

            string modeLabel = _menuMode ? "MenuMode(Sim+Fan)" : currentMode.ToString();

            if (breathing && !_wasBreathing)
                Debug.Log($"[BreathInput] Breath DETECTED on {modeLabel} — intensity: {intensity:F3}");
            else if (!breathing && _wasBreathing)
                Debug.Log($"[BreathInput] Breath STOPPED on {modeLabel}");

            _wasBreathing = breathing;

            _breathLogTimer += Time.deltaTime;
            if (_breathLogTimer >= BreathLogInterval)
            {
                _breathLogTimer = 0f;
                Debug.Log($"[BreathInput] Status — mode: {modeLabel}, intensity: {intensity:F3}, " +
                    $"level: {GetBreathLevel()}, breathing: {breathing}");
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
