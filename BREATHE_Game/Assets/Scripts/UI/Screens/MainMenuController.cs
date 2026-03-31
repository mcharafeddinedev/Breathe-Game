using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Breathe.Data;
using Breathe.Gameplay;
using Breathe.Input;
using Breathe.Utility;

namespace Breathe.UI
{
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

        // Nav button tracking
        private readonly List<Button> _navButtons = new();
        private readonly List<Action> _navActions = new();
        private readonly List<Color> _navButtonBaseColors = new();

        // Card tracking
        private readonly List<GameObject> _cardObjects = new();
        private readonly List<MinigameDefinition> _cardDefs = new();

        // Breath navigation constants
        private const float BreathScrollThreshold = 0.03f;
        private const float ScrollSpeed = 2.5f;
        private const float NavDwellTime = 8f;
        private const float CardDwellTime = 8f;
        private const float SettleDelay = 1f;

        // Scroll / dwell state
        private int _focusedNavIndex;
        private int _focusedCardIndex = -1;
        private float _scrollAccumulator;
        private float _dwellTimer;
        private bool _isScrolling;
        private float _settleTimer;

        // Floating UI (reparented to focused element each frame)
        private TextMeshProUGUI _dwellLabel;

        // Controls side panels (hidden when a panel is open)
        private GameObject _controlsLeft;
        private GameObject _controlsRight;

        // -------------------------------------------------------------------
        // Breath input — works from either system
        // -------------------------------------------------------------------

        private static float GetBreathIntensity()
        {
            if (BreathPowerSystem.Instance != null)
                return BreathPowerSystem.Instance.CurrentBreathPower;
            if (BreathInputManager.Instance != null)
                return BreathInputManager.Instance.GetBreathIntensity();
            return 0f;
        }

        // -------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------

        private void Start()
        {
            RegisterNavButton(_levelSelectButton, () => {
                ShowPanel(_levelSelectPanel);
                if (_cardObjects.Count > 0 && _focusedCardIndex < 0) _focusedCardIndex = 0;
            });
            RegisterNavButton(_settingsButton, () => ShowPanel(_settingsPanel));
            RegisterNavButton(_creditsButton, () => ShowPanel(_creditsPanel));
            RegisterNavButton(_quitButton, OnQuit);

            _levelSelectBackButton?.onClick.AddListener(() => { HideActivePanel(); ResetNavFocus(0); });
            _settingsBackButton?.onClick.AddListener(() => { HideActivePanel(); ResetNavFocus(1); });
            _creditsBackButton?.onClick.AddListener(() => { HideActivePanel(); ResetNavFocus(2); });

            HideAllPanels();
            PopulateLevelSelect();
            AttachHoverEffects();
            BuildControlsSection();
            BuildFloatingUI();

            _settleTimer = SettleDelay;
            _focusedNavIndex = 0;
        }

        private void RegisterNavButton(Button btn, Action action)
        {
            if (btn == null) return;
            int idx = _navButtons.Count;
            _navButtons.Add(btn);
            _navActions.Add(action);

            var img = btn.GetComponent<Image>();
            _navButtonBaseColors.Add(img != null ? img.color : Color.white);

            btn.onClick.AddListener(() => {
                _focusedNavIndex = idx;
                _dwellTimer = 0f;
                action?.Invoke();
            });
        }

        private void ResetNavFocus(int index)
        {
            _focusedNavIndex = index;
            _dwellTimer = 0f;
            _scrollAccumulator = 0f;
            _isScrolling = false;
        }

        private void Update()
        {
            if (_settleTimer > 0f)
            {
                _settleTimer -= Time.unscaledDeltaTime;
                return;
            }

            float breath = GetBreathIntensity();
            bool breathActive = breath >= BreathScrollThreshold;
            bool enterPressed = Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.numpadEnterKey.wasPressedThisFrame);

            if (_activePanel == null && _navButtons.Count > 0)
            {
                UpdateNavMode(breathActive, breath, enterPressed);
            }
            else if (_activePanel == _levelSelectPanel && _cardObjects.Count > 0)
            {
                UpdateCardMode(breathActive, breath, enterPressed);
            }
            else
            {
                HideFloatingUI();
            }
        }

        // -------------------------------------------------------------------
        // Main nav breath scroll
        // -------------------------------------------------------------------

        private void UpdateNavMode(bool breathActive, float breath, bool enterPressed)
        {
            TickBreathScroll(breathActive, breath, _navButtons.Count, ref _focusedNavIndex);

            if (enterPressed && _focusedNavIndex >= 0 && _focusedNavIndex < _navActions.Count)
            {
                _dwellTimer = 0f;
                _navActions[_focusedNavIndex]?.Invoke();
                return;
            }

            if (!_isScrolling && _focusedNavIndex >= 0)
            {
                _dwellTimer += Time.unscaledDeltaTime;
                if (_dwellTimer >= NavDwellTime && _focusedNavIndex < _navActions.Count)
                {
                    _dwellTimer = 0f;
                    _navActions[_focusedNavIndex]?.Invoke();
                    return;
                }
            }

            // Visuals — scale + darken focused nav button, restore others
            for (int i = 0; i < _navButtons.Count; i++)
            {
                if (_navButtons[i] == null) continue;
                bool focused = (i == _focusedNavIndex);
                _navButtons[i].transform.localScale = focused ? Vector3.one * 1.06f : Vector3.one;

                var img = _navButtons[i].GetComponent<Image>();
                if (img != null && i < _navButtonBaseColors.Count)
                {
                    Color baseCol = _navButtonBaseColors[i];
                    img.color = focused
                        ? new Color(baseCol.r * 0.78f, baseCol.g * 0.78f, baseCol.b * 0.78f, baseCol.a)
                        : baseCol;
                }
            }

            if (_focusedNavIndex >= 0 && _focusedNavIndex < _navButtons.Count)
                ShowFloatingUI(_navButtons[_focusedNavIndex].gameObject, NavDwellTime, false);
            else
                HideFloatingUI();
        }

        // -------------------------------------------------------------------
        // Level Select card breath scroll
        // -------------------------------------------------------------------

        private void UpdateCardMode(bool breathActive, float breath, bool enterPressed)
        {
            if (_focusedCardIndex < 0) _focusedCardIndex = 0;

            TickBreathScroll(breathActive, breath, _cardObjects.Count, ref _focusedCardIndex);

            if (enterPressed && _focusedCardIndex >= 0 && _focusedCardIndex < _cardDefs.Count)
            {
                OnGameSelected(_cardDefs[_focusedCardIndex]);
                return;
            }

            if (!_isScrolling && _focusedCardIndex >= 0)
            {
                _dwellTimer += Time.unscaledDeltaTime;
                if (_dwellTimer >= CardDwellTime && _focusedCardIndex < _cardDefs.Count)
                {
                    _dwellTimer = 0f;
                    OnGameSelected(_cardDefs[_focusedCardIndex]);
                    return;
                }
            }

            // Visuals
            for (int i = 0; i < _cardObjects.Count; i++)
            {
                if (_cardObjects[i] == null) continue;
                _cardObjects[i].transform.localScale = (i == _focusedCardIndex)
                    ? Vector3.one * 1.08f : Vector3.one;
            }

            if (_focusedCardIndex >= 0 && _focusedCardIndex < _cardObjects.Count)
            {
                ShowFloatingUI(_cardObjects[_focusedCardIndex], CardDwellTime, true);
                ScrollCardIntoView(_cardObjects[_focusedCardIndex]);
            }
            else
            {
                HideFloatingUI();
            }
        }

        // -------------------------------------------------------------------
        // Generic breath scroll tick
        // -------------------------------------------------------------------

        private void TickBreathScroll(bool breathActive, float breath, int count, ref int focusedIndex)
        {
            if (count == 0) return;
            if (focusedIndex < 0) focusedIndex = 0;

            if (breathActive)
            {
                _scrollAccumulator += breath * ScrollSpeed * Time.unscaledDeltaTime;

                if (!_isScrolling)
                {
                    _isScrolling = true;
                    _dwellTimer = 0f;
                }

                if (_scrollAccumulator >= 1f)
                {
                    _scrollAccumulator -= 1f;
                    focusedIndex = (focusedIndex + 1) % count;
                    _dwellTimer = 0f;
                }
            }
            else if (_isScrolling)
            {
                _scrollAccumulator = 0f;
                _isScrolling = false;
                _dwellTimer = 0f;
            }
        }

        // -------------------------------------------------------------------
        // Floating UI (border + dwell countdown)
        // -------------------------------------------------------------------

        private void BuildFloatingUI()
        {
            // Dwell countdown label
            var labelGO = new GameObject("DwellCountdown",
                typeof(RectTransform), typeof(CanvasRenderer));
            _dwellLabel = labelGO.AddComponent<TextMeshProUGUI>();
            _dwellLabel.fontSize = 14;
            _dwellLabel.fontStyle = FontStyles.Bold;
            _dwellLabel.color = new Color(0.6f, 0.9f, 1f, 0.9f);
            _dwellLabel.alignment = TextAlignmentOptions.Center;
            _dwellLabel.overflowMode = TextOverflowModes.Overflow;
            _dwellLabel.raycastTarget = false;
            labelGO.SetActive(false);
        }

        private void ShowFloatingUI(GameObject target, float dwellTime, bool insideTarget)
        {
            if (target == null) { HideFloatingUI(); return; }

            _dwellLabel.transform.SetParent(target.transform, false);
            _dwellLabel.transform.SetAsLastSibling();
            var lrt = _dwellLabel.GetComponent<RectTransform>();

            if (insideTarget)
            {
                lrt.anchorMin = new Vector2(0, 0);
                lrt.anchorMax = new Vector2(1, 0.18f);
                lrt.offsetMin = new Vector2(4, 2);
                lrt.offsetMax = new Vector2(-4, 0);
            }
            else
            {
                lrt.anchorMin = new Vector2(0, 0);
                lrt.anchorMax = new Vector2(1, 0);
                lrt.pivot = new Vector2(0.5f, 1f);
                lrt.anchoredPosition = new Vector2(0, -4);
                lrt.sizeDelta = new Vector2(0, 24);
            }

            if (!_isScrolling && _dwellTimer > 0f)
            {
                _dwellLabel.gameObject.SetActive(true);
                int sec = Mathf.CeilToInt(Mathf.Max(0f, dwellTime - _dwellTimer));
                _dwellLabel.text = $"SELECTING  IN  {sec}s";
                float lp = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 3f);
                _dwellLabel.alpha = lp;
            }
            else
            {
                _dwellLabel.gameObject.SetActive(false);
            }
        }

        private void HideFloatingUI()
        {
            if (_dwellLabel != null) _dwellLabel.gameObject.SetActive(false);
        }

        // -------------------------------------------------------------------
        // Controls section (below nav buttons)
        // -------------------------------------------------------------------

        private void BuildControlsSection()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            Color textColor = new(0.75f, 0.75f, 0.75f, 1f);

            string leftText =
                "---  CONTROLS  ---\n\n" +
                "DEVICE\n" +
                "BREATHE  TO  SCROLL\n" +
                "HOLD  POSITION  TO  SELECT\n\n" +
                "KEYBOARD\n" +
                "ENTER  TO  CONFIRM";

            _controlsLeft = BuildSidePanel(canvas.transform, "ControlsLeft", leftText, textColor,
                new Vector2(0.01f, 0.22f), new Vector2(0.28f, 0.78f));

            string rightText =
                "---  LEVEL  SELECT  ---\n\n" +
                "BREATHE  TO  BROWSE  GAMES\n" +
                "STAY  ON  A  GAME  FOR\n" +
                "8  SECONDS  TO  START  IT\n\n" +
                "MOUSE\n" +
                "CLICK  TO  SELECT";

            _controlsRight = BuildSidePanel(canvas.transform, "ControlsRight", rightText, textColor,
                new Vector2(0.72f, 0.22f), new Vector2(0.99f, 0.78f));
        }

        private static GameObject BuildSidePanel(Transform parent, string name, string text,
            Color color, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Normal;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return go;
        }

        // -------------------------------------------------------------------
        // Hover effects
        // -------------------------------------------------------------------

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

        // -------------------------------------------------------------------
        // Panel management
        // -------------------------------------------------------------------

        private void ShowPanel(GameObject panel)
        {
            if (panel == null) return;

            HideAllPanels();
            panel.SetActive(true);
            _activePanel = panel;
            SetNavButtonsInteractable(false);
            SetControlsVisible(false);

            _scrollAccumulator = 0f;
            _isScrolling = false;
            _dwellTimer = 0f;
        }

        private void HideActivePanel()
        {
            if (_activePanel != null)
                _activePanel.SetActive(false);
            _activePanel = null;
            SetNavButtonsInteractable(true);
            SetControlsVisible(true);

            _scrollAccumulator = 0f;
            _isScrolling = false;
            _dwellTimer = 0f;
            HideFloatingUI();
        }

        private void SetControlsVisible(bool visible)
        {
            if (_controlsLeft != null) _controlsLeft.SetActive(visible);
            if (_controlsRight != null) _controlsRight.SetActive(visible);
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

            AddAnchoredLabel(cardGO.transform, def.DisplayName.ToUpper(), 22, FontStyles.Bold, titleColor,
                new Vector2(0, 0.70f), new Vector2(1, 0.93f));

            AddAnchoredLabel(cardGO.transform, def.Description, 13, FontStyles.Bold, descColor,
                new Vector2(0, 0.24f), new Vector2(1, 0.70f));

            var patternTMP = AddAnchoredLabel(cardGO.transform, def.BreathPattern, 14,
                FontStyles.Bold | FontStyles.Italic, patternColor,
                new Vector2(0, 0.03f), new Vector2(1, 0.22f));

            if (!available)
            {
                AddAnchoredLabel(cardGO.transform, "COMING  SOON", 14,
                    FontStyles.Bold | FontStyles.Italic, new Color(1f, 0.85f, 0.3f),
                    new Vector2(0, 0.03f), new Vector2(1, 0.22f));
            }

            var button = cardGO.GetComponent<Button>();
            button.interactable = available;
            button.transition = Selectable.Transition.None;

            var captured = def;
            button.onClick.AddListener(() => OnGameSelected(captured));

            var hover = cardGO.AddComponent<CardHoverEffect>();
            hover.SetInteractable(available);
            hover.SetHoverLabel(patternTMP);

            _cardObjects.Add(cardGO);
            _cardDefs.Add(def);
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

        // -------------------------------------------------------------------
        // Scroll helper
        // -------------------------------------------------------------------

        private void ScrollCardIntoView(GameObject card)
        {
            if (_gameGridParent == null) return;
            var scrollRect = _gameGridParent.GetComponentInParent<ScrollRect>();
            if (scrollRect == null) return;

            Canvas.ForceUpdateCanvases();
            var cardRT = card.GetComponent<RectTransform>();
            var contentRT = scrollRect.content;
            if (cardRT == null || contentRT == null) return;

            Vector2 viewportLocalPos = scrollRect.viewport.InverseTransformPoint(cardRT.position);
            float viewH = scrollRect.viewport.rect.height;
            if (viewportLocalPos.y > viewH * 0.4f || viewportLocalPos.y < -viewH * 0.4f)
            {
                Vector2 contentLocalPos = contentRT.InverseTransformPoint(cardRT.position);
                float targetY = -contentLocalPos.y - viewH * 0.5f;
                contentRT.anchoredPosition = new Vector2(contentRT.anchoredPosition.x,
                    Mathf.Clamp(targetY, 0f, contentRT.rect.height - viewH));
            }
        }

        // -------------------------------------------------------------------
        // Actions
        // -------------------------------------------------------------------

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
