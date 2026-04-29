using UnityEngine;
using UnityEngine.SceneManagement;
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
    /// <para><b>Session rule:</b> Each scene load starts in <see cref="InputMode.Simulated"/>.
    /// <see cref="SetInputMode"/> (settings / tutorial) may change the source live until the next load.</para>
    public sealed class BreathInputManager : MonoBehaviour
    {
        /// <summary>PlayerPrefs key — written as Simulated on each scene load; settings UI may update when cycling mode in-scene.</summary>
        public const string PrefKeyInputMode = "Breathe_InputMode";

        [Header("Input Mode")]
        [SerializeField, Tooltip("Editor default only — runtime always starts as Simulated; each loaded scene resets to Simulated.")]
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

        /// <summary>
        /// True when microphone input is available on this platform.
        /// WebGL: disabled — browser mic permissions and Web Audio API can break gameplay; Simulated only.
        /// </summary>
        public static bool MicrophoneSupported =>
#if UNITY_WEBGL && !UNITY_EDITOR
            false;
#else
            true;
#endif

        /// <summary>False on WebGL — USB serial (fan hardware) is not available in the browser.</summary>
        public static bool FanHardwareSupported =>
#if UNITY_WEBGL
            false;
#else
            true;
#endif

        /// <summary>True when settings/tutorial should offer cycling beyond Simulated.</summary>
        public static bool InputModeCyclingSupported => MicrophoneSupported || FanHardwareSupported;

        /// <summary>
        /// Settings cycle order:
        /// - Desktop: Sim → Mic → Fan → Sim...
        /// - WebGL:   Simulated only (mic/fan disabled to avoid browser API issues)
        /// </summary>
        public static InputMode GetNextCycledInputMode(InputMode current)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            // No cycling on WebGL — Simulated only
            return InputMode.Simulated;
#else
            return current switch
            {
                InputMode.Simulated => InputMode.Microphone,
                InputMode.Microphone => InputMode.Fan,
                _ => InputMode.Simulated
            };
#endif
        }

        /// <summary>Matches <see cref="SimulatedBreathInput"/> — hold Space.</summary>
        public const string SimulatedControlsShortHint = "hold Space";

        /// <summary>Settings row: Simulated includes control hint; other modes use enum name.</summary>
        public static string InputModeSettingsLabel(InputMode mode) =>
            mode == InputMode.Simulated
                ? $"Simulated — {SimulatedControlsShortHint}"
                : mode.ToString();

        /// <summary>Tutorial input toggles — uppercase + extra spaces for pixel font.</summary>
        public static string TutorialSimulatedOptionLabel => "SIMULATED  (HOLD  SPACE)";

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
            // WebGL: force Simulated — mic/fan not supported and can break gameplay.
#if UNITY_WEBGL && !UNITY_EDITOR
            if (mode != InputMode.Simulated)
            {
                Debug.LogWarning($"[BreathInputManager] {mode} not supported on WebGL — forcing Simulated");
                mode = InputMode.Simulated;
            }
#endif

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
            SceneManager.sceneLoaded += OnSceneLoaded;

            // Simulated is the default in every scene; do not restore Mic/Fan from PlayerPrefs.
            currentMode = InputMode.Simulated;
            _activeInput = ResolveInput(currentMode);
            _activeInput?.Initialize();

            Debug.Log($"[BreathInputManager] Initialized with {currentMode} input on startup");
        }

        /// <summary>Every scene load resets breath input to Simulated (prefs kept in sync for settings UI).</summary>
        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance != this) return;

            PlayerPrefs.SetInt(PrefKeyInputMode, (int)InputMode.Simulated);
            PlayerPrefs.Save();

            if (currentMode != InputMode.Simulated)
                SetInputMode(InputMode.Simulated);
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
            SceneManager.sceneLoaded -= OnSceneLoaded;
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
