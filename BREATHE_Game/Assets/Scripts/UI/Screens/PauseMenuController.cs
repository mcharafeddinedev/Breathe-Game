using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using TMPro;
using Breathe.Gameplay;
using Breathe.Utility;

namespace Breathe.UI
{
    /// <summary>
    /// In-game pause menu triggered by ESC or TAB during gameplay.
    /// Shows RESUME, SETTINGS, MAIN MENU options. Works in any minigame scene.
    /// </summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        [Header("Scene Config")]
        [SerializeField] private string _mainMenuSceneName = "MainMenu";

        [Header("Button Sizing")]
        [SerializeField] private float _buttonWidth = 280f;
        [SerializeField] private float _buttonHeight = 56f;
        [SerializeField] private float _buttonSpacing = 16f;

        private GameObject _pausePanel;
        private GameObject _settingsPanel;
        private bool _isPaused;
        private bool _settingsOpen;
        private float _savedTimeScale = 1f;

        private Canvas _canvas;
        private static PauseMenuController _instance;

        /// <summary>Returns the singleton instance, creating it if needed.</summary>
        public static PauseMenuController Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("PauseMenuController");
                    _instance = go.AddComponent<PauseMenuController>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        /// <summary>Call to ensure pause menu is available.</summary>
        public static void EnsureExists() => _ = Instance;

        /// <summary>True when the pause menu is showing (pause panel or settings panel).</summary>
        public static bool IsPaused => _instance != null && (_instance._isPaused || _instance._settingsOpen);

        /// <summary>Auto-bootstrap when game starts (after scene load).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => EnsureExists();

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
            {
                if (_settingsOpen)
                {
                    CloseSettings();
                }
                else if (_isPaused)
                {
                    Resume();
                }
                else if (CanPause())
                {
                    Pause();
                }
            }
        }

        private bool CanPause()
        {
            if (GameStateManager.Instance == null) return false;
            var state = GameStateManager.Instance.CurrentState;
            return state == GameState.Playing || state == GameState.Tutorial;
        }

        private void Pause()
        {
            if (_isPaused) return;
            _isPaused = true;
            _savedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            ShowPausePanel();
        }

        private void Resume()
        {
            if (!_isPaused) return;
            _isPaused = false;
            Time.timeScale = _savedTimeScale;
            HidePausePanel();
        }

        private void ShowPausePanel()
        {
            EnsureCanvas();
            if (_pausePanel != null)
            {
                _pausePanel.SetActive(true);
                return;
            }

            BuildPausePanel();
        }

        private void HidePausePanel()
        {
            if (_pausePanel != null)
                _pausePanel.SetActive(false);
        }

        private void EnsureCanvas()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("PauseMenuCanvas");
            canvasGo.transform.SetParent(transform, false);
            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists for UI interaction
            EnsureEventSystem();
        }

        private static GameObject _createdEventSystem;

        private void EnsureEventSystem()
        {
            // Check if we already created one and it still exists
            if (_createdEventSystem != null) return;

            // Check if any EventSystem already exists (scene-based or otherwise)
            var existing = FindAnyObjectByType<EventSystem>();
            if (existing != null) return;

            // No EventSystem found - create one
            var esGo = new GameObject("PauseMenuEventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            DontDestroyOnLoad(esGo);
            _createdEventSystem = esGo;
        }

        private void BuildPausePanel()
        {
            _pausePanel = new GameObject("PausePanel", typeof(RectTransform), typeof(Image));
            _pausePanel.transform.SetParent(_canvas.transform, false);

            var panelRT = _pausePanel.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            var bgImg = _pausePanel.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.92f);
            bgImg.raycastTarget = true;

            // Panel container for buttons
            var containerGo = new GameObject("ButtonContainer", typeof(RectTransform), typeof(Image));
            containerGo.transform.SetParent(_pausePanel.transform, false);
            var containerRT = containerGo.GetComponent<RectTransform>();
            containerRT.anchorMin = new Vector2(0.5f, 0.5f);
            containerRT.anchorMax = new Vector2(0.5f, 0.5f);
            containerRT.pivot = new Vector2(0.5f, 0.5f);

            float containerW = _buttonWidth + 48f;
            float containerH = 3 * _buttonHeight + 2 * _buttonSpacing + 100f;
            containerRT.sizeDelta = new Vector2(containerW, containerH);

            var containerImg = containerGo.GetComponent<Image>();
            containerImg.color = MenuVisualTheme.SubmenuPanelBackdrop;
            containerImg.raycastTarget = true;

            // Title
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(containerGo.transform, false);
            var titleRT = titleGo.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.5f, 1f);
            titleRT.anchorMax = new Vector2(0.5f, 1f);
            titleRT.pivot = new Vector2(0.5f, 1f);
            titleRT.anchoredPosition = new Vector2(0f, -20f);
            titleRT.sizeDelta = new Vector2(containerW - 20f, 40f);

            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "PAUSED";
            titleTmp.font = Resources.Load<TMP_FontAsset>("ARCADECLASSIC SDF");
            titleTmp.fontSize = 32f;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = MenuVisualTheme.HomeTitle;
            titleTmp.raycastTarget = false;

            // Buttons: RESUME, SETTINGS, MAIN MENU
            float startY = -70f;
            CreateButton(containerGo.transform, "RESUME", startY, Resume);
            CreateButton(containerGo.transform, "SETTINGS", startY - (_buttonHeight + _buttonSpacing), OpenSettings);
            CreateButton(containerGo.transform, "MAIN  MENU", startY - 2 * (_buttonHeight + _buttonSpacing), GoToMainMenu);
        }

        private void CreateButton(Transform parent, string label, float yPos, System.Action onClick)
        {
            var btnGo = new GameObject(label + "Button", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);

            var btnRT = btnGo.GetComponent<RectTransform>();
            btnRT.anchorMin = new Vector2(0.5f, 1f);
            btnRT.anchorMax = new Vector2(0.5f, 1f);
            btnRT.pivot = new Vector2(0.5f, 1f);
            btnRT.anchoredPosition = new Vector2(0f, yPos);
            btnRT.sizeDelta = new Vector2(_buttonWidth, _buttonHeight);

            var btnImg = btnGo.GetComponent<Image>();
            btnImg.color = MenuVisualTheme.ButtonBase;
            btnImg.raycastTarget = true;

            var btn = btnGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.ColorTint;
            btn.targetGraphic = btnImg;
            var colors = btn.colors;
            colors.normalColor = MenuVisualTheme.ButtonBase;
            colors.highlightedColor = MenuVisualTheme.ButtonHighlight;
            colors.pressedColor = MenuVisualTheme.ButtonHighlight * 0.9f;
            colors.selectedColor = MenuVisualTheme.ButtonHighlight;
            colors.fadeDuration = 0.1f;
            btn.colors = colors;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(btnGo.transform, false);
            var textRT = textGo.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var textTmp = textGo.GetComponent<TextMeshProUGUI>();
            textTmp.text = label;
            textTmp.font = Resources.Load<TMP_FontAsset>("ARCADECLASSIC SDF");
            textTmp.fontSize = 22f;
            textTmp.alignment = TextAlignmentOptions.Center;
            textTmp.color = MenuVisualTheme.HomeTitle;
            textTmp.raycastTarget = false; // Let clicks pass through to button

            MenuUiChrome.AttachStandardButtonHover(btnGo, btn.interactable);
        }

        private void OpenSettings()
        {
            _settingsOpen = true;
            if (_pausePanel != null)
                _pausePanel.SetActive(false);

            BuildSettingsPanel();
        }

        private void CloseSettings()
        {
            _settingsOpen = false;
            if (_settingsPanel != null)
                _settingsPanel.SetActive(false);

            if (_pausePanel != null)
                _pausePanel.SetActive(true);
        }

        private void BuildSettingsPanel()
        {
            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(true);
                return;
            }

            // Dim background
            _settingsPanel = new GameObject("PauseSettingsPanel", typeof(RectTransform), typeof(Image));
            _settingsPanel.transform.SetParent(_canvas.transform, false);

            var panelRT = _settingsPanel.GetComponent<RectTransform>();
            panelRT.anchorMin = Vector2.zero;
            panelRT.anchorMax = Vector2.one;
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;

            var bgImg = _settingsPanel.GetComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.92f);
            bgImg.raycastTarget = true;

            // Settings container - centered and appropriately sized
            var containerGo = new GameObject("SettingsContainer", typeof(RectTransform), typeof(Image));
            containerGo.transform.SetParent(_settingsPanel.transform, false);
            var containerRT = containerGo.GetComponent<RectTransform>();
            // Wider/taller than legacy 70×80% so rows (esp. audio %) aren’t cramped vs main menu.
            containerRT.anchorMin = new Vector2(0.08f, 0.05f);
            containerRT.anchorMax = new Vector2(0.92f, 0.95f);
            containerRT.offsetMin = Vector2.zero;
            containerRT.offsetMax = Vector2.zero;

            var containerImg = containerGo.GetComponent<Image>();
            containerImg.color = MenuVisualTheme.SubmenuPanelBackdrop;

            // Create a Back button that SettingsManager can find
            var backBtnGo = new GameObject("BackButton", typeof(RectTransform), typeof(Image), typeof(Button));
            backBtnGo.transform.SetParent(containerGo.transform, false);
            var backRT = backBtnGo.GetComponent<RectTransform>();
            backRT.anchorMin = new Vector2(0.5f, 0f);
            backRT.anchorMax = new Vector2(0.5f, 0f);
            backRT.pivot = new Vector2(0.5f, 0f);
            backRT.sizeDelta = new Vector2(200f, 40f);
            backRT.anchoredPosition = new Vector2(0f, 10f);

            var backImg = backBtnGo.GetComponent<Image>();
            backImg.color = MenuVisualTheme.ButtonBase;

            var backBtn = backBtnGo.GetComponent<Button>();
            backBtn.onClick.AddListener(CloseSettings);

            var backTextGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            backTextGo.transform.SetParent(backBtnGo.transform, false);
            var backTextRT = backTextGo.GetComponent<RectTransform>();
            backTextRT.anchorMin = Vector2.zero;
            backTextRT.anchorMax = Vector2.one;
            backTextRT.offsetMin = Vector2.zero;
            backTextRT.offsetMax = Vector2.zero;

            var backTmp = backTextGo.GetComponent<TextMeshProUGUI>();
            backTmp.text = "BACK";
            backTmp.font = Resources.Load<TMP_FontAsset>("ARCADECLASSIC SDF");
            backTmp.fontSize = 20f;
            backTmp.alignment = TextAlignmentOptions.Center;
            backTmp.color = MenuVisualTheme.HomeTitle;
            backTmp.raycastTarget = false;

            MenuUiChrome.AttachStandardButtonHover(backBtnGo);

            // Add SettingsManager component - it will build the settings UI and find/use our Back button
            containerGo.AddComponent<SettingsManager>();
        }

        private void GoToMainMenu()
        {
            Time.timeScale = 1f;
            _isPaused = false;
            _settingsOpen = false;

            // Hide and destroy pause UI before scene load
            HidePausePanel();
            if (_settingsPanel != null)
            {
                Destroy(_settingsPanel);
                _settingsPanel = null;
            }
            if (_pausePanel != null)
            {
                Destroy(_pausePanel);
                _pausePanel = null;
            }
            if (_canvas != null)
            {
                Destroy(_canvas.gameObject);
                _canvas = null;
            }

            // Destroy our EventSystem - main menu scene has its own
            if (_createdEventSystem != null)
            {
                Destroy(_createdEventSystem);
                _createdEventSystem = null;
            }

            SceneManager.LoadScene(_mainMenuSceneName);
        }

        /// <summary>Called when scene changes - cleanup duplicate EventSystems.</summary>
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Reset pause state on scene load
            _isPaused = false;
            _settingsOpen = false;

            // Cleanup duplicate EventSystems - prefer scene's native EventSystem over our created one
            var allEventSystems = FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            if (allEventSystems.Length > 1 && _createdEventSystem != null)
            {
                // Scene has its own EventSystem, destroy ours
                Destroy(_createdEventSystem);
                _createdEventSystem = null;
            }
        }
    }
}
