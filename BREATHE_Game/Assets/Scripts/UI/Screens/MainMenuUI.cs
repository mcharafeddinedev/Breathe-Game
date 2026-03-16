using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Breathe.Input;
using Breathe.Gameplay;

namespace Breathe.UI
{
    // Main menu: Quick Play, Level Select, and input mode selection (Simulated / Mic / Fan).
    // Mic and Fan modes route through calibration before play begins.
    public sealed class MainMenuUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Navigation")]
        [SerializeField] private Button quickPlayButton;
        [SerializeField] private Button levelSelectButton;

        [Header("Input Mode Selection")]
        [SerializeField, Tooltip("Three buttons for Simulated, Microphone, and Fan (in order).")]
        private Button[] inputModeButtons = new Button[3];

        [SerializeField] private TextMeshProUGUI inputModeLabel;

        [Header("Visual Feedback")]
        [SerializeField] private Color normalButtonColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        [SerializeField] private Color selectedButtonColor = new Color(0.3f, 0.75f, 1f, 1f);

        private InputMode _selectedMode = InputMode.Simulated;
        private static readonly string[] ModeLabels = { "Simulated", "Microphone", "Fan" };

        private void Start()
        {
            if (titleText != null)
                titleText.text = "BREATHE";

            if (quickPlayButton != null)
                quickPlayButton.onClick.AddListener(OnQuickPlay);

            if (levelSelectButton != null)
                levelSelectButton.onClick.AddListener(OnLevelSelect);

            for (int i = 0; i < inputModeButtons.Length; i++)
            {
                if (inputModeButtons[i] == null) continue;
                int capturedIndex = i;
                inputModeButtons[i].onClick.AddListener(() => OnSelectInputMode(capturedIndex));
            }

            SetSelectedMode(InputMode.Simulated);
        }

        private void OnDestroy()
        {
            if (quickPlayButton != null)
                quickPlayButton.onClick.RemoveAllListeners();

            if (levelSelectButton != null)
                levelSelectButton.onClick.RemoveAllListeners();

            for (int i = 0; i < inputModeButtons.Length; i++)
            {
                if (inputModeButtons[i] != null)
                    inputModeButtons[i].onClick.RemoveAllListeners();
            }
        }

        private void OnSelectInputMode(int index)
        {
            if (index < 0 || index >= 3) return;
            SetSelectedMode((InputMode)index);
        }

        private void SetSelectedMode(InputMode mode)
        {
            _selectedMode = mode;

            if (inputModeLabel != null)
                inputModeLabel.text = ModeLabels[(int)mode];

            UpdateButtonVisuals();
        }

        private void UpdateButtonVisuals()
        {
            int selectedIndex = (int)_selectedMode;
            for (int i = 0; i < inputModeButtons.Length; i++)
            {
                if (inputModeButtons[i] == null) continue;

                ColorBlock cb = inputModeButtons[i].colors;
                cb.normalColor = (i == selectedIndex) ? selectedButtonColor : normalButtonColor;
                cb.selectedColor = cb.normalColor;
                inputModeButtons[i].colors = cb;
            }
        }

        // Applies input mode, then routes to Calibration or Playing.
        private void OnQuickPlay()
        {
            ApplyInputMode();

            GameStateManager gsm = GameStateManager.Instance;
            if (gsm == null) return;

            bool requiresCalibration = _selectedMode == InputMode.Microphone
                                    || _selectedMode == InputMode.Fan;

            gsm.TransitionTo(requiresCalibration ? GameState.Calibration : GameState.Playing);
        }

        private void OnLevelSelect()
        {
            ApplyInputMode();

            GameStateManager gsm = GameStateManager.Instance;
            if (gsm != null)
                gsm.TransitionTo(GameState.LevelSelect);
        }

        private void ApplyInputMode()
        {
            if (BreathInputManager.Instance != null)
                BreathInputManager.Instance.SetInputMode(_selectedMode);
        }
    }
}
