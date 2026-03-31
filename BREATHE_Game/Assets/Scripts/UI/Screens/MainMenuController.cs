using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Breathe.Data;
using Breathe.Gameplay;
using Breathe.Utility;

namespace Breathe.UI
{
    // Drives the Main Menu scene. Manages four nav buttons and three content panels.
    // Level Select builds a scrollable grid of game cards from MinigameManager at runtime.
    public sealed class MainMenuController : MonoBehaviour
    {
        [Header("Title")]
        [SerializeField] private TextMeshProUGUI _titleText;

        [Header("Navigation Buttons")]
        [SerializeField] private Button _levelSelectButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _creditsButton;
        [SerializeField] private Button _quitButton;

        [Header("Panels")]
        [SerializeField] private GameObject _levelSelectPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _creditsPanel;

        [Header("Back Buttons (one per panel)")]
        [SerializeField] private Button _levelSelectBackButton;
        [SerializeField] private Button _settingsBackButton;
        [SerializeField] private Button _creditsBackButton;

        [Header("Level Select — Game Grid")]
        [SerializeField, Tooltip("Parent transform for spawned game cards (use a GridLayoutGroup).")]
        private Transform _gameGridParent;

        private GameObject _activePanel;

        private void Start()
        {
            _levelSelectButton?.onClick.AddListener(() => ShowPanel(_levelSelectPanel));
            _settingsButton?.onClick.AddListener(() => ShowPanel(_settingsPanel));
            _creditsButton?.onClick.AddListener(() => ShowPanel(_creditsPanel));
            _quitButton?.onClick.AddListener(OnQuit);

            _levelSelectBackButton?.onClick.AddListener(HideActivePanel);
            _settingsBackButton?.onClick.AddListener(HideActivePanel);
            _creditsBackButton?.onClick.AddListener(HideActivePanel);

            HideAllPanels();
            PopulateLevelSelect();
            AttachHoverEffects();
        }

        private void AttachHoverEffects()
        {
            Button[] buttons = {
                _levelSelectButton, _settingsButton, _creditsButton, _quitButton,
                _levelSelectBackButton, _settingsBackButton, _creditsBackButton
            };

            foreach (var btn in buttons)
            {
                if (btn != null && btn.GetComponent<CardHoverEffect>() == null)
                {
                    var hover = btn.gameObject.AddComponent<CardHoverEffect>();
                    hover.SetInteractable(btn.interactable);
                }
            }
        }

        private void OnDestroy()
        {
            _levelSelectButton?.onClick.RemoveAllListeners();
            _settingsButton?.onClick.RemoveAllListeners();
            _creditsButton?.onClick.RemoveAllListeners();
            _quitButton?.onClick.RemoveAllListeners();

            _levelSelectBackButton?.onClick.RemoveAllListeners();
            _settingsBackButton?.onClick.RemoveAllListeners();
            _creditsBackButton?.onClick.RemoveAllListeners();
        }

        private void ShowPanel(GameObject panel)
        {
            if (panel == null) return;

            HideAllPanels();
            panel.SetActive(true);
            _activePanel = panel;
            SetNavButtonsInteractable(false);
        }

        private void HideActivePanel()
        {
            if (_activePanel != null)
                _activePanel.SetActive(false);
            _activePanel = null;
            SetNavButtonsInteractable(true);
        }

        private void HideAllPanels()
        {
            if (_levelSelectPanel != null) _levelSelectPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_creditsPanel != null) _creditsPanel.SetActive(false);
            _activePanel = null;
        }

        private void SetNavButtonsInteractable(bool interactable)
        {
            if (_levelSelectButton != null) _levelSelectButton.interactable = interactable;
            if (_settingsButton != null) _settingsButton.interactable = interactable;
            if (_creditsButton != null) _creditsButton.interactable = interactable;
            if (_quitButton != null) _quitButton.interactable = interactable;
        }

        // -------------------------------------------------------------------
        // Level Select card generation
        // -------------------------------------------------------------------

        private void PopulateLevelSelect()
        {
            if (_gameGridParent == null || MinigameManager.Instance == null) return;

            MinigameDefinition[] defs = MinigameManager.Instance.AvailableMinigames;
            if (defs == null || defs.Length == 0) return;

            foreach (var def in defs)
            {
                if (def == null) continue;
                BuildCard(def);
            }
        }

        private void BuildCard(MinigameDefinition def)
        {
            bool available = def.IsUnlocked && SceneLoader.IsSceneInBuildSettings(def.SceneName);

            // Card root — the colored thumbnail IS the card background
            var cardGO = new GameObject($"Card_{def.MinigameId}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            cardGO.transform.SetParent(_gameGridParent, false);

            var cardImage = cardGO.GetComponent<Image>();
            if (def.Thumbnail != null)
            {
                cardImage.sprite = def.Thumbnail;
                cardImage.color = available ? Color.white : new Color(0.3f, 0.3f, 0.3f);
            }
            else
            {
                cardImage.color = available
                    ? def.CardColor
                    : new Color(def.CardColor.r * 0.3f, def.CardColor.g * 0.3f,
                        def.CardColor.b * 0.3f);
            }

            // Dark overlay so text is readable on top of the color
            var overlayGO = new GameObject("Overlay",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            overlayGO.transform.SetParent(cardGO.transform, false);
            var overlayRT = overlayGO.GetComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            var overlayImage = overlayGO.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, available ? 0.35f : 0.65f);
            overlayImage.raycastTarget = false;

            Color titleColor = available ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            Color descColor = available ? new Color(0.9f, 0.9f, 0.9f) : new Color(0.4f, 0.4f, 0.4f);
            Color patternColor = available ? new Color(0.6f, 0.9f, 1f) : new Color(0.3f, 0.4f, 0.45f);

            // Title — shifted down slightly to make room for pattern at bottom
            AddAnchoredLabel(cardGO.transform, def.DisplayName.ToUpper(), 22, FontStyles.Bold, titleColor,
                new Vector2(0, 0.70f), new Vector2(1, 0.93f));

            // Description — centered in the middle
            AddAnchoredLabel(cardGO.transform, def.Description, 13, FontStyles.Bold, descColor,
                new Vector2(0, 0.24f), new Vector2(1, 0.70f));

            // Breath pattern — bottom of card, hidden until hover
            var patternTMP = AddAnchoredLabel(cardGO.transform, def.BreathPattern, 14,
                FontStyles.Bold | FontStyles.Italic, patternColor,
                new Vector2(0, 0.03f), new Vector2(1, 0.22f));

            if (!available)
            {
                AddAnchoredLabel(cardGO.transform, "COMING  SOON", 14,
                    FontStyles.Bold | FontStyles.Italic, new Color(1f, 0.85f, 0.3f),
                    new Vector2(0, 0.03f), new Vector2(1, 0.22f));
            }

            // Wire button
            var button = cardGO.GetComponent<Button>();
            button.interactable = available;
            button.transition = Selectable.Transition.None;

            var captured = def;
            button.onClick.AddListener(() => OnGameSelected(captured));

            var hover = cardGO.AddComponent<CardHoverEffect>();
            hover.SetInteractable(available);
            hover.SetHoverLabel(patternTMP);
        }

        private static TextMeshProUGUI AddAnchoredLabel(Transform parent, string text, float fontSize,
            FontStyles style, Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(6, 0);
            rt.offsetMax = new Vector2(-6, 0);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.raycastTarget = false;
            return tmp;
        }

        private void OnGameSelected(MinigameDefinition def)
        {
            if (def == null || !def.IsUnlocked) return;

            MinigameManager.Instance?.SelectMinigame(def);
            SceneLoader.LoadMinigame(def.SceneName);
        }

        private static void OnQuit()
        {
            Debug.Log("[MainMenu] Quit requested.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
