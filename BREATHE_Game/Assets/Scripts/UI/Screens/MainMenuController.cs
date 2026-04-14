using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Breathe.Audio;
using Breathe.Data;
using Breathe.Gameplay;
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
        [SerializeField, Tooltip("Parent RectTransform for spawned cards (plain stretch; layout is built in code — no LayoutGroup on this object).")]
        private Transform _gameGridParent;

        [Header("Background")]
        [SerializeField, Tooltip("If true and no MenuBathBackdrop exists in the scene, spawn one (bath + caustics, same as Bubbles menu).")]
        private bool _spawnBathCausticsBackdrop = true;

        [SerializeField, Tooltip("Parent of the main menu title + nav buttons (not the sub-panels). Hidden while Level Select / Settings / Credits is open so semi-transparent panels show caustics instead of this UI behind them.")]
        private GameObject _homeScreenRoot;

        [SerializeField, Tooltip("Applied to each sub-panel root Image so bath caustics read through (adjust in inspector if needed).")]
        private Color _subPanelTint = new(0.08f, 0.11f, 0.18f, 0.78f);

        private GameObject _activePanel;

        private readonly List<GameObject> _cardObjects = new();
        private readonly List<MinigameDefinition> _cardDefs = new();

        // -------------------------------------------------------------------
        // Lifecycle
        // -------------------------------------------------------------------

        private void Awake()
        {
            if (_homeScreenRoot == null)
            {
                var t = transform.Find("HomeScreen");
                if (t != null)
                    _homeScreenRoot = t.gameObject;
            }

            EnsureHomeScreenGroup();

            if (_spawnBathCausticsBackdrop && FindAnyObjectByType<MenuBathBackdrop>() == null)
            {
                var go = new GameObject("MenuBathBackdrop");
                go.AddComponent<MenuBathBackdrop>();
            }
        }

        /// <summary>
        /// Groups title + nav under one root so we can hide "home" while sub-panels are open (transparent panels would otherwise show this UI behind them).
        /// Creates <c>HomeScreen</c> under the menu Canvas if not present in the scene.
        /// </summary>
        void EnsureHomeScreenGroup()
        {
            if (_homeScreenRoot != null) return;

            Transform canvasRoot = null;
            if (_titleText != null)
                canvasRoot = _titleText.transform.parent;
            else if (_levelSelectButton != null)
            {
                Transform n = _levelSelectButton.transform;
                while (n != null && n.GetComponent<Canvas>() == null)
                    n = n.parent;
                canvasRoot = n;
            }
            if (canvasRoot == null) return;

            Transform titleTr = canvasRoot.Find("TitleText - TMP");
            Transform navTr = canvasRoot.Find("NavButtons");
            if (titleTr == null && navTr == null) return;

            var home = new GameObject("HomeScreen", typeof(RectTransform));
            var homeRT = home.GetComponent<RectTransform>();
            homeRT.SetParent(canvasRoot, false);
            homeRT.anchorMin = Vector2.zero;
            homeRT.anchorMax = Vector2.one;
            homeRT.offsetMin = Vector2.zero;
            homeRT.offsetMax = Vector2.zero;
            homeRT.SetAsFirstSibling();

            if (titleTr != null) titleTr.SetParent(home.transform, true);
            if (navTr != null) navTr.SetParent(home.transform, true);

            _homeScreenRoot = home;
        }

        private void Start()
        {
            ApplySubPanelTransparency();

            _levelSelectButton?.onClick.AddListener(() => ShowPanel(_levelSelectPanel));
            _settingsButton?.onClick.AddListener(() => ShowPanel(_settingsPanel));
            _creditsButton?.onClick.AddListener(() => ShowPanel(_creditsPanel));
            _quitButton?.onClick.AddListener(OnQuit);

            _levelSelectBackButton?.onClick.AddListener(HideActivePanel);
            _settingsBackButton?.onClick.AddListener(HideActivePanel);
            _creditsBackButton?.onClick.AddListener(HideActivePanel);

            HideAllPanels();
            if (_homeScreenRoot != null)
                _homeScreenRoot.SetActive(true);

            PopulateLevelSelect();
            AttachHoverEffects();
            // Do not gate on SfxPlayer.Instance — script order can leave it null this frame; hooks only need SfxPlayer at click time.
            MenuClickSoundHook.RegisterHierarchy(transform.root);
            StartCoroutine(RegisterMenuClicksNextFrame());
        }

        private System.Collections.IEnumerator RegisterMenuClicksNextFrame()
        {
            yield return null;
            MenuClickSoundHook.RegisterHierarchy(transform.root);
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

        // -------------------------------------------------------------------
        // Panel management
        // -------------------------------------------------------------------

        private void ShowPanel(GameObject panel)
        {
            if (panel == null) return;

            HideAllPanels();
            if (_homeScreenRoot != null)
                _homeScreenRoot.SetActive(false);
            panel.SetActive(true);
            _activePanel = panel;
            SetNavButtonsInteractable(false);
            MenuClickSoundHook.RegisterHierarchy(panel.transform);
        }

        private void HideActivePanel()
        {
            if (_activePanel != null)
                _activePanel.SetActive(false);
            _activePanel = null;
            if (_homeScreenRoot != null)
                _homeScreenRoot.SetActive(true);
            SetNavButtonsInteractable(true);
        }

        /// <summary>Sets each sub-panel root Image to a semi-transparent tint so the single world-space bath+caustics layer reads through.</summary>
        void ApplySubPanelTransparency()
        {
            TryTintPanelRoot(_levelSelectPanel);
            TryTintPanelRoot(_settingsPanel);
            TryTintPanelRoot(_creditsPanel);
        }

        void TryTintPanelRoot(GameObject panel)
        {
            if (panel == null) return;
            var img = panel.GetComponent<Image>();
            if (img == null) return;
            img.color = _subPanelTint;
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

        private void DisableConflictingLayoutOnGridParent()
        {
            var go = _gameGridParent.gameObject;
            var lg = go.GetComponent<LayoutGroup>();
            if (lg != null)
                lg.enabled = false;
            var csf = go.GetComponent<ContentSizeFitter>();
            if (csf != null)
                csf.enabled = false;
        }

        private void PopulateLevelSelect()
        {
            if (_gameGridParent == null || MinigameManager.Instance == null) return;

            // A VerticalLayoutGroup (or other LayoutGroup) on this parent overrides child anchors
            // and breaks the scripted 50/50 split — Skydive disappears and the 2×2 grid won't stay left.
            DisableConflictingLayoutOnGridParent();

            for (int i = _gameGridParent.childCount - 1; i >= 0; i--)
                Destroy(_gameGridParent.GetChild(i).gameObject);

            _cardObjects.Clear();
            _cardDefs.Clear();

            MinigameDefinition[] defs = MinigameManager.Instance.MinigamesForLevelSelect;
            if (defs == null || defs.Length == 0)
            {
                PositionLevelSelectBackButton();
                return;
            }

            MinigameDefinition skydive = null;
            var others = new List<MinigameDefinition>();
            foreach (var d in defs)
            {
                if (d == null) continue;
                if (IsSkydiveFeaturedDefinition(d))
                    skydive = d;
                else
                    others.Add(d);
            }

            // Full-area root (no HorizontalLayoutGroup — use explicit 50/50 anchors so the
            // left half is only for the small grid and the right half is only for Skydive).
            var rootGo = new GameObject("LevelSelectLayoutRoot", typeof(RectTransform));
            rootGo.transform.SetParent(_gameGridParent, false);

            var rootRT = rootGo.GetComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;

            // Left 50%: smaller 2×2 cards, centered as a block in this half.
            var leftGo = new GameObject("LeftColumn", typeof(RectTransform), typeof(GridLayoutGroup));
            leftGo.transform.SetParent(rootGo.transform, false);

            var leftRT = leftGo.GetComponent<RectTransform>();
            leftRT.anchorMin = new Vector2(0f, 0f);
            leftRT.anchorMax = new Vector2(0.5f, 1f);
            leftRT.offsetMin = new Vector2(14f, 18f);
            leftRT.offsetMax = new Vector2(-8f, -18f);

            var grid = leftGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(158f, 118f);
            grid.spacing = new Vector2(14f, 14f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            foreach (var def in others)
                BuildCard(def, leftGo.transform, CardSizeMode.Compact);

            // Right 50%: Skydive fills most of this half (≈ area of 2×2 small cards).
            if (skydive != null)
            {
                var rightGo = new GameObject("SkydiveColumn", typeof(RectTransform));
                rightGo.transform.SetParent(rootGo.transform, false);

                var rightRT = rightGo.GetComponent<RectTransform>();
                rightRT.anchorMin = new Vector2(0.5f, 0f);
                rightRT.anchorMax = new Vector2(1f, 1f);
                rightRT.offsetMin = new Vector2(8f, 18f);
                rightRT.offsetMax = new Vector2(-14f, -18f);

                BuildCard(skydive, rightGo.transform, CardSizeMode.Featured);
            }

            PositionLevelSelectBackButton();
        }

        /// <summary>
        /// BACK sits under the 2×2 grid (left column), not centered on the full panel + Skydive.
        /// Kept under LevelSelectPanel so it is not destroyed when GameGrid content is rebuilt.
        /// </summary>
        private void PositionLevelSelectBackButton()
        {
            if (_levelSelectBackButton == null) return;
            var backRT = _levelSelectBackButton.GetComponent<RectTransform>();
            if (_levelSelectPanel != null && backRT.parent != _levelSelectPanel.transform)
                backRT.SetParent(_levelSelectPanel.transform, false);

            // Horizontal center of the left 50% = 0.25 of panel width; bottom edge with small padding.
            backRT.anchorMin = new Vector2(0.25f, 0f);
            backRT.anchorMax = new Vector2(0.25f, 0f);
            backRT.pivot = new Vector2(0.5f, 0f);
            backRT.sizeDelta = new Vector2(180f, 34f);
            backRT.anchoredPosition = new Vector2(0f, 14f);
            backRT.SetAsLastSibling();
        }

        /// <summary>Skydive is featured on the right; match by id or scene name if assets differ.</summary>
        private static bool IsSkydiveFeaturedDefinition(MinigameDefinition d)
        {
            if (string.Equals(d.MinigameId, "skydive", StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(d.SceneName) &&
                string.Equals(d.SceneName.Trim(), "SKYDIVE", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private enum CardSizeMode
        {
            Compact,
            Featured
        }

        private void BuildCard(MinigameDefinition def, Transform parent, CardSizeMode sizeMode)
        {
            bool available = def.IsUnlocked && SceneLoader.IsSceneInBuildSettings(def.SceneName);
            bool featured = sizeMode == CardSizeMode.Featured;

            var cardGO = new GameObject($"Card_{def.MinigameId}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            cardGO.transform.SetParent(parent, false);

            var cardRT = cardGO.GetComponent<RectTransform>();
            if (featured)
            {
                // Fill the right-hand column (parent is SkydiveColumn) — large card, not a small grid cell.
                cardRT.anchorMin = new Vector2(0.03f, 0.03f);
                cardRT.anchorMax = new Vector2(0.97f, 0.97f);
                cardRT.pivot = new Vector2(0.5f, 0.5f);
                cardRT.offsetMin = Vector2.zero;
                cardRT.offsetMax = Vector2.zero;
            }
            else
            {
                var layoutEl = cardGO.AddComponent<LayoutElement>();
                layoutEl.preferredWidth = 158f;
                layoutEl.preferredHeight = 118f;
                layoutEl.minWidth = 140f;
                layoutEl.minHeight = 100f;
            }

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
            float overlayAlpha = def.Thumbnail != null ? 0.42f : 0.38f;
            overlayImage.color = new Color(0f, 0f, 0f, available ? overlayAlpha : 0.68f);
            overlayImage.raycastTarget = false;

            Color titleColor = available ? Color.white : new Color(0.5f, 0.5f, 0.5f);
            Color descColor = available ? new Color(0.92f, 0.92f, 0.92f) : new Color(0.4f, 0.4f, 0.4f);
            Color patternColor = available ? new Color(0.65f, 0.92f, 1f) : new Color(0.3f, 0.4f, 0.45f);

            float titleSize = featured ? 32f : 22f;
            float descSize = featured ? 17f : 12f;
            float patternSize = featured ? 18f : 13f;

            AddStrokedLabel(cardGO.transform, def.DisplayName.ToUpper(), titleSize, FontStyles.Bold, titleColor,
                new Vector2(0, 0.68f), new Vector2(1, 0.94f), strokeStrong: true);

            AddStrokedLabel(cardGO.transform, def.Description, descSize, FontStyles.Bold, descColor,
                new Vector2(0, 0.26f), new Vector2(1, 0.68f), strokeStrong: true);

            var patternTMP = AddStrokedLabel(cardGO.transform, def.BreathPattern, patternSize,
                FontStyles.Bold | FontStyles.Italic, patternColor,
                new Vector2(0, 0.04f), new Vector2(1, 0.24f), strokeStrong: false);

            if (!available)
            {
                AddStrokedLabel(cardGO.transform, "COMING  SOON", 15,
                    FontStyles.Bold | FontStyles.Italic, new Color(1f, 0.85f, 0.3f),
                    new Vector2(0, 0.04f), new Vector2(1, 0.24f), strokeStrong: true);
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

        /// <summary>
        /// TMP with dark outline + shadow so text stays readable on busy screenshot backgrounds.
        /// </summary>
        private static TextMeshProUGUI AddStrokedLabel(Transform parent, string text, float fontSize,
            FontStyles style, Color color, Vector2 anchorMin, Vector2 anchorMax, bool strokeStrong)
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

            float outline = strokeStrong ? 0.35f : 0.22f;
            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.92f);
            ol.useGraphicAlpha = true;
            ol.effectDistance = new Vector2(outline, -outline);

            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.55f);
            sh.useGraphicAlpha = true;
            sh.effectDistance = new Vector2(2.2f, -2.2f);

            return tmp;
        }

        // -------------------------------------------------------------------
        // Actions
        // -------------------------------------------------------------------

        private void OnGameSelected(MinigameDefinition def)
        {
            if (def == null || !def.IsUnlocked) return;

            Debug.Log($"[MainMenu] Game selected: {def.name} (scene: {def.SceneName})");
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
