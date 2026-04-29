using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using Breathe.Audio;
using Breathe.Data;
using Breathe.Gameplay;
using Breathe.Utility;

namespace Breathe.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        static Sprite _uiWhiteSprite;
        static Sprite _homePanelRounded9SliceSprite;
        const int HomePanelRoundTexSize = 64;
        const int HomePanelRoundCornerRadiusPx = 12;

        static Sprite UiWhiteSprite()
        {
            if (_uiWhiteSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                _uiWhiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
            }
            return _uiWhiteSprite;
        }

        /// <summary>9-slice rounded square for home title / nav backdrops (sizing stays layout-driven; corners stay round).</summary>
        static Sprite HomePanelRoundedSprite()
        {
            if (_homePanelRounded9SliceSprite != null) return _homePanelRounded9SliceSprite;
            return BuildHomePanelRoundedSprite();
        }

        static Sprite BuildHomePanelRoundedSprite()
        {
            int w = HomePanelRoundTexSize;
            int h = HomePanelRoundTexSize;
            float r = HomePanelRoundCornerRadiusPx;
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f - 1f);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                bool inside = RoundedRectContainsPixel(px, py, w, h, r);
                tex.SetPixel(px, py, inside ? Color.white : Color.clear);
            }
            tex.Apply();
            // Bilinear blurs the 1px white/clear boundary when 9-slice scales up; point keeps corners as crisp as the source art.
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            int br = Mathf.RoundToInt(r);
            var border = new Vector4(br, br, br, br);
            _homePanelRounded9SliceSprite = Sprite.Create(
                tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            return _homePanelRounded9SliceSprite;
        }

        static bool RoundedRectContainsPixel(int px, int py, int W, int H, float r)
        {
            if (px < 0 || py < 0 || px >= W || py >= H) return false;
            if (px >= r && px < W - r && py >= r && py < H - r) return true;
            if (px >= r && px < W - r && (py < r || py >= H - r)) return true;
            if (py >= r && py < H - r && (px < r || px >= W - r)) return true;
            float r2 = r * r;
            if (px < r && py < r)
            {
                float dx = (px + 0.5f) - r, dy = (py + 0.5f) - r;
                return dx * dx + dy * dy <= r2;
            }
            if (px >= W - r && py < r)
            {
                float dx = (px + 0.5f) - (W - r), dy = (py + 0.5f) - r;
                return dx * dx + dy * dy <= r2;
            }
            if (px < r && py >= H - r)
            {
                float dx = (px + 0.5f) - r, dy = (py + 0.5f) - (H - r);
                return dx * dx + dy * dy <= r2;
            }
            if (px >= W - r && py >= H - r)
            {
                float dx = (px + 0.5f) - (W - r), dy = (py + 0.5f) - (H - r);
                return dx * dx + dy * dy <= r2;
            }
            return false;
        }

        /// <summary>Alpha-blended overlay using <see cref="MinigameDefinition.CardColor"/>; screenshot stays visible underneath.</summary>
        static Color LevelSelectCardTintColor(MinigameDefinition def, bool available)
        {
            if (def == null) return new Color(0.5f, 0.5f, 0.5f, 0.22f);
            Color c = def.CardColor;
            // rgb from asset; a = wash strength (higher = each mode’s palette reads clearly on thumbs)
            const float aUnlocked = 0.36f;
            const float aLocked = 0.15f;
            c.a = available ? aUnlocked : aLocked;
            if (!available)
            {
                c.r *= 0.5f;
                c.g *= 0.5f;
                c.b *= 0.5f;
            }
            return c;
        }

        /// <summary>Biases the full-card dim toward each mode’s <see cref="MinigameDefinition.CardColor"/> so thumbnails don’t all read as the same forest wash.</summary>
        static Color LevelSelectCardOverlayTintedRgb(MinigameDefinition def, bool available, bool hasThumb)
        {
            Color dim = MenuVisualTheme.LevelSelectCardDimBase;
            if (def == null)
                return dim;
            float w = available
                ? (hasThumb ? 0.24f : 0.16f)
                : 0.1f;
            Color key = def.CardColor;
            key.a = 1f;
            return Color.Lerp(dim, key, w);
        }

        [Header("Title")]
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField, Tooltip("Optional. If unassigned, a tagline is created at runtime under the title (same style as the wordmark).")]
        private TextMeshProUGUI _titleSubtitleText;
        [Header("Home layout — prominence")]
        [SerializeField, Range(0.78f, 1.35f), Tooltip("Scales the Home group (title, tagline, nav). Below 1 shrinks the whole home block.")]
        private float _homeMenuProminenceScale = 1f;
        [SerializeField, Tooltip("Moves the title plate + nav column together (positive = upward, reference-resolution pixels). Small values (~12–28) avoid crowding the top; 0 = scene layout only.")]
        private float _homeBlockVerticalShiftPx = 18f;
        [SerializeField, Tooltip("Tagline (runtime text if _titleSubtitleText is not assigned in the scene).")]
        private string _homeMenuTagline = "PUT YOUR BREATH TO THE TEST!";

        TextMeshProUGUI _titleSubtitle;

        [Header("Navigation Buttons")]
        [SerializeField] private Button _levelSelectButton;
        [SerializeField] private Button _howToPlayButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _creditsButton;
        [SerializeField] private Button _quitButton;

        [Header("Panels")]
        [SerializeField] private GameObject _levelSelectPanel;
        [SerializeField] private GameObject _howToPlayPanel;
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private GameObject _creditsPanel;

        [Header("Back Buttons (one per panel)")]
        [SerializeField] private Button _levelSelectBackButton;
        [SerializeField] private Button _howToPlayBackButton;
        [SerializeField] private Button _settingsBackButton;
        [SerializeField] private Button _creditsBackButton;

        [Header("Level Select — Game Grid")]
        [SerializeField, Tooltip("Parent RectTransform for spawned cards (plain stretch; layout is built in code — no LayoutGroup on this object).")]
        private Transform _gameGridParent;

        [Header("Background")]
        [SerializeField, Tooltip("If true and no MenuBathBackdrop exists in the scene, spawn one (bath + caustics, same as Bubbles menu).")]
        private bool _spawnBathCausticsBackdrop = true;

        [SerializeField, Tooltip("When a MenuBathBackdrop is spawned at runtime, optional looping MP4 (overrides null). Scene-placed backdrops: assign on the MenuBathBackdrop instead.")]
        private VideoClip _mainMenuBackgroundVideo;

        [SerializeField, Tooltip("When VideoClip is null: file name in Assets/StreamingAssets/ (e.g. MainMenuLoop.mp4). Only used for runtime-spawned backdrops; call MenuBathBackdrop.SetRuntimeVideo or use inspector on scene object.")]
        private string _mainMenuBackgroundVideoStreaming = "";

        [SerializeField, Tooltip("Parent of the main menu title + nav buttons (not the sub-panels). Hidden while Level Select / Settings / Credits is open so semi-transparent panels show caustics instead of this UI behind them.")]
        private GameObject _homeScreenRoot;

        [Header("Credits (shown on Credits panel)")]
        [SerializeField, Tooltip("Your name, shown in DEVELOPER (use with Game studio for one line, or alone).")]
        private string _creditsDeveloperName = "";
        [SerializeField, Tooltip("Studio or label (e.g. Gold Leaf Interactive). Shown on one line with Your name, as \"Name  -  Studio\" (| is not used; pixel font strips it).")]
        private string _creditsStudio = "";
        [SerializeField, TextArea(3, 8), Tooltip("Solo or team roles. Comma = one item per line. Use a newline to keep a long sentence as a single block, e.g. first line: Solo developer.\\n second line: scope.")]
        private string _creditsRoles = "";
        [SerializeField, TextArea(2, 6), Tooltip("Software and assets to acknowledge. Empty = a sensible default.")]
        private string _creditsToolsAck = "";
        [SerializeField, TextArea(3, 8), Tooltip("Build / process blurb. Empty = default (Unity, platforms, process).")]
        private string _creditsBuildBlurb = "";

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
                var mb = go.AddComponent<MenuBathBackdrop>();
                if (_mainMenuBackgroundVideo != null || !string.IsNullOrWhiteSpace(_mainMenuBackgroundVideoStreaming))
                    mb.SetRuntimeVideo(_mainMenuBackgroundVideo, _mainMenuBackgroundVideoStreaming);
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
            // Apply safe margins so UI doesn't feel zoomed in
            homeRT.anchorMin = new Vector2(MenuVisualTheme.SafeMarginHorizontal, MenuVisualTheme.SafeMarginVertical);
            homeRT.anchorMax = new Vector2(1f - MenuVisualTheme.SafeMarginHorizontal, 1f - MenuVisualTheme.SafeMarginVertical);
            homeRT.offsetMin = Vector2.zero;
            homeRT.offsetMax = Vector2.zero;
            homeRT.SetAsFirstSibling();

            if (titleTr != null) titleTr.SetParent(home.transform, true);
            if (navTr != null) navTr.SetParent(home.transform, true);

            _homeScreenRoot = home;
        }

        private void Start()
        {
            ApplyMenuBackdropColorsFromTheme();
            ApplySafeAreaMargins();
            ApplyHomeScreenProminence();
            if (_titleText != null)
            {
                _titleText.ForceMeshUpdate(true);
                Canvas.ForceUpdateCanvases();
            }
            EnsureHomeTitleSubtitle();
            ApplyHomeBlockVerticalShift();
            ApplyHomeScreenTextColors();
            SetupHomeScreenTextBackdrops();
            MenuTextLegibility.TryApplyOverlayOutlineToTmp(_titleText, largeTitle: true);
            if (_titleSubtitle != null)
                MenuTextLegibility.TryApplyOverlayOutlineToTmp(_titleSubtitle, largeTitle: false);

            // Create How To Play button/panel dynamically if not assigned
            EnsureHowToPlayButtonAndPanel();

            _levelSelectButton?.onClick.AddListener(() => ShowPanel(_levelSelectPanel));
            _howToPlayButton?.onClick.AddListener(() => ShowPanel(_howToPlayPanel));
            _settingsButton?.onClick.AddListener(() => ShowPanel(_settingsPanel));
            _creditsButton?.onClick.AddListener(() => ShowPanel(_creditsPanel));
            _quitButton?.onClick.AddListener(OnQuit);

            _levelSelectBackButton?.onClick.AddListener(HideActivePanel);
            _howToPlayBackButton?.onClick.AddListener(HideActivePanel);
            _settingsBackButton?.onClick.AddListener(HideActivePanel);
            _creditsBackButton?.onClick.AddListener(HideActivePanel);

            // Level select layout reads GameGrid.rect.width — must run while LevelSelectPanel is still active
            // (HideAllPanels deactivates it; wrong width caused asymmetric “nudged right” margins).
            PopulateLevelSelect();
            SetupCreditsContent();
            if (_creditsPanel != null)
                MenuTextLegibility.TryApplyToPanelNonButtonText(_creditsPanel.transform);
            if (_levelSelectPanel != null)
                MenuTextLegibility.TryApplyToPanelNonButtonText(_levelSelectPanel.transform);

            HideAllPanels();
            if (_homeScreenRoot != null)
                _homeScreenRoot.SetActive(true);
            AttachHoverEffects();
            // Do not gate on SfxPlayer.Instance — script order can leave it null this frame; hooks only need SfxPlayer at click time.
            MenuClickSoundHook.RegisterHierarchy(transform.root);
            StartCoroutine(RegisterMenuClicksNextFrame());
            StartCoroutine(DeferredApplyMenuChromeFromThemeNextFrame());
        }

        private System.Collections.IEnumerator DeferredApplyMenuChromeFromThemeNextFrame()
        {
            yield return null;
            ApplyMenuBackdropColorsFromTheme();
            EnsureLevelSelectInsetChrome();
            if (_levelSelectPanel != null)
            {
                var rtl = _levelSelectPanel.GetComponent<RectTransform>();
                if (rtl != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(rtl);
            }
            ApplyHomeBackdropPlatesFromTheme();
            yield return null;
            ApplyHomeBackdropPlatesFromTheme();
            ApplyMenuBackdropColorsFromTheme();
        }

        private System.Collections.IEnumerator RegisterMenuClicksNextFrame()
        {
            yield return null;
            MenuClickSoundHook.RegisterHierarchy(transform.root);
        }

        private void OnDestroy()
        {
            _levelSelectButton?.onClick.RemoveAllListeners();
            _howToPlayButton?.onClick.RemoveAllListeners();
            _settingsButton?.onClick.RemoveAllListeners();
            _creditsButton?.onClick.RemoveAllListeners();
            _quitButton?.onClick.RemoveAllListeners();

            _levelSelectBackButton?.onClick.RemoveAllListeners();
            _howToPlayBackButton?.onClick.RemoveAllListeners();
            _settingsBackButton?.onClick.RemoveAllListeners();
            _creditsBackButton?.onClick.RemoveAllListeners();
        }

        // -------------------------------------------------------------------
        // Hover effects
        // -------------------------------------------------------------------

        private void AttachHoverEffects()
        {
            Button[] buttons = {
                _levelSelectButton, _howToPlayButton, _settingsButton, _creditsButton, _quitButton,
                _levelSelectBackButton, _howToPlayBackButton, _settingsBackButton, _creditsBackButton
            };

            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                MenuUiChrome.AttachStandardButtonHover(btn.gameObject, btn.interactable);
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
            ApplyMenuBackdropColorsFromTheme();
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
            ApplyHomeBackdropPlatesFromTheme();
        }

        /// <summary>Fullscreen canvas tint + submenu panel roots (<see cref="MenuVisualTheme"/>). Re-run after canvas rebuilds/opening panels so Graphic/canvas batches don’t drift from authored colors.</summary>
        void ApplyMenuBackdropColorsFromTheme()
        {
            ApplyMainMenuCanvasBackdrop();
            TryTintPanelRoot(_levelSelectPanel, MenuVisualTheme.LevelSelectPanelBackdrop);
            TryTintPanelRoot(_howToPlayPanel, MenuVisualTheme.SubmenuPanelBackdrop);
            TryTintPanelRoot(_settingsPanel, MenuVisualTheme.SubmenuPanelBackdrop);
            TryTintPanelRoot(_creditsPanel, MenuVisualTheme.CreditsSubPanelTint);
            // Keeps runtime-placed Home* backdrop Images aligned with theme (canvas batch/sync can drift to stale/black).
            ApplyHomeBackdropPlatesFromTheme();
        }

        /// <summary>Apply safe area margins to all sub-panels so UI doesn't feel zoomed in or touch edges.</summary>
        void ApplySafeAreaMargins()
        {
            ApplySafeMarginToPanel(_levelSelectPanel);
            ApplySafeMarginToPanel(_howToPlayPanel);
            ApplySafeMarginToPanel(_settingsPanel);
            ApplySafeMarginToPanel(_creditsPanel);
        }

        void ApplySafeMarginToPanel(GameObject panel)
        {
            if (panel == null) return;
            var rt = panel.GetComponent<RectTransform>();
            if (rt == null) return;
            float h = MenuVisualTheme.SafeMarginHorizontal;
            float v = MenuVisualTheme.SafeMarginVertical;
            if (ReferenceEquals(panel, _levelSelectPanel))
            {
                // Breathing room from screen edges vs home/settings (level grid + featured strip need width).
                h = Mathf.Min(0.05f, h + MenuVisualTheme.LevelSelectScreenEdgeExtra);
                v = Mathf.Min(0.05f, v + MenuVisualTheme.LevelSelectScreenEdgeExtra);
            }

            rt.anchorMin = new Vector2(h, v);
            rt.anchorMax = new Vector2(1f - h, 1f - v);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>Canvas root Image on Canvas_MainMenuUI — scene color is legacy; drive from <see cref="MenuVisualTheme"/>.</summary>
        void ApplyMainMenuCanvasBackdrop()
        {
            ApplyImageColorFromTheme(FindMainMenuCanvasBackdropImage(), MenuVisualTheme.MainMenuCanvasBackdrop);
        }

        /// <summary>Resolves the full-screen tint on the menu canvas (same GO as <see cref="Canvas"/> in our scenes).</summary>
        Image FindMainMenuCanvasBackdropImage()
        {
            Canvas canvas = null;
            if (_titleText != null && _titleText.canvas != null)
                canvas = _titleText.canvas.rootCanvas;
            if (canvas == null && _levelSelectButton != null)
            {
                var c = _levelSelectButton.GetComponentInParent<Canvas>();
                if (c != null)
                    canvas = c.rootCanvas;
            }

            if (canvas == null)
            {
                var go = GameObject.Find("Canvas_MainMenuUI");
                if (go != null)
                    canvas = go.GetComponent<Canvas>();
            }

            if (canvas == null)
                return null;

            var img = canvas.GetComponent<Image>();
            return img;
        }

        void TryTintPanelRoot(GameObject panel, Color tint) =>
            ApplyImageColorFromTheme(panel != null ? panel.GetComponent<Image>() : null, tint);

        /// <summary>Sets <paramref name="img"/> tint on both <see cref="Graphic.color"/> and <see cref="CanvasRenderer"/> (UI batch/sync can otherwise show stale/deserialized hues).</summary>
        static void ApplyImageColorFromTheme(Image img, Color tint)
        {
            if (img == null) return;
            img.material = null;
            img.canvasRenderer.SetColor(tint);
            img.color = tint;
        }

        /// <summary>Warm typography on home title + nav/back labels (scene defaults are generic white / legacy blue).</summary>
        void ApplyHomeScreenTextColors()
        {
            if (_titleText != null)
                _titleText.color = MenuVisualTheme.HomeTitle;
            if (_titleSubtitle != null)
                _titleSubtitle.color = MenuVisualTheme.HomeTitleSubtitle;

            void NavLabel(Button btn, Color? label = null)
            {
                if (btn == null) return;
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.color = label ?? MenuVisualTheme.ChromeHeader;
            }

            void HomeMainNavFace(Button btn)
            {
                if (btn == null) return;
                MenuUiChrome.StyleButtonLikeSettings(btn.gameObject, MenuVisualTheme.HomeNavButtonIdleFill);
            }

            HomeMainNavFace(_levelSelectButton);
            HomeMainNavFace(_howToPlayButton);
            HomeMainNavFace(_settingsButton);
            HomeMainNavFace(_creditsButton);
            HomeMainNavFace(_quitButton);

            NavLabel(_levelSelectButton);
            NavLabel(_howToPlayButton);
            NavLabel(_settingsButton);
            NavLabel(_creditsButton);
            NavLabel(_quitButton);

            ApplySubmenuBackButtonChrome();
        }

        /// <summary>LEVEL SELECT / SETTINGS / CREDITS BACK: SETTINGS-style bordered fills + chrome labels.</summary>
        void ApplySubmenuBackButtonChrome()
        {
            void Style(Button btn)
            {
                if (btn == null) return;
                MenuUiChrome.StyleButtonLikeSettings(btn.gameObject);
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.color = MenuVisualTheme.ChromeHeader;
            }

            Style(_levelSelectBackButton);
            Style(_howToPlayBackButton);
            Style(_settingsBackButton);
            Style(_creditsBackButton);
        }

        /// <summary>Creates How To Play button and panel dynamically if not assigned in Inspector.</summary>
        void EnsureHowToPlayButtonAndPanel()
        {
            // Find the NavButtons parent
            RectTransform navStack = FindHomeNavStackRect();
            if (navStack == null) return;

            // Create button if not assigned
            if (_howToPlayButton == null && _settingsButton != null)
            {
                var btnGo = Instantiate(_settingsButton.gameObject, navStack);
                btnGo.name = "BTN_HowToPlay";

                // Position after Level Select (index 1, since Level Select is 0)
                int targetIdx = 1;
                if (_levelSelectButton != null)
                    targetIdx = _levelSelectButton.transform.GetSiblingIndex() + 1;
                btnGo.transform.SetSiblingIndex(targetIdx);

                _howToPlayButton = btnGo.GetComponent<Button>();
                _howToPlayButton.onClick.RemoveAllListeners();

                var tmp = btnGo.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                    tmp.text = "HOW  TO  PLAY";
            }

            // Create panel if not assigned
            if (_howToPlayPanel == null && _settingsPanel != null)
            {
                var panelGo = Instantiate(_settingsPanel, _settingsPanel.transform.parent);
                panelGo.name = "HowToPlayPanel";
                _howToPlayPanel = panelGo;

                // Remove the SettingsManager component - we'll build custom content
                var settingsMgr = panelGo.GetComponent<SettingsManager>();
                if (settingsMgr != null)
                    Destroy(settingsMgr);

                // Find and repurpose the back button
                foreach (var btn in panelGo.GetComponentsInChildren<Button>(true))
                {
                    var label = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label != null && label.text.ToUpperInvariant().Contains("BACK"))
                    {
                        _howToPlayBackButton = btn;
                        _howToPlayBackButton.onClick.RemoveAllListeners();
                        break;
                    }
                }

                // Clear existing content and build How To Play content
                BuildHowToPlayPanelContent(panelGo);
            }
        }

        void BuildHowToPlayPanelContent(GameObject panel)
        {
            // Find or create content area
            var rt = panel.GetComponent<RectTransform>();
            if (rt == null) return;

            // Destroy all children except back button
            var toDestroy = new List<GameObject>();
            foreach (Transform child in panel.transform)
            {
                if (_howToPlayBackButton != null && child.gameObject == _howToPlayBackButton.gameObject)
                    continue;
                // Keep panel background images
                if (child.GetComponent<UnityEngine.UI.Image>() != null && child.GetComponent<Button>() == null 
                    && child.childCount == 0)
                    continue;
                toDestroy.Add(child.gameObject);
            }
            foreach (var go in toDestroy)
                Destroy(go);

            // Add border frame + opaque backdrop
            var borderGo = new GameObject("HTP_Border", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            borderGo.transform.SetParent(panel.transform, false);
            var borderRt = borderGo.GetComponent<RectTransform>();
            borderRt.anchorMin = new Vector2(0.02f, 0.02f);
            borderRt.anchorMax = new Vector2(0.98f, 0.98f);
            borderRt.offsetMin = Vector2.zero;
            borderRt.offsetMax = Vector2.zero;
            var borderImg = borderGo.GetComponent<Image>();
            borderImg.color = MenuVisualTheme.PanelBorder; // Cream/beige border
            borderImg.raycastTarget = false;

            var backdropGo = new GameObject("HTP_Backdrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            backdropGo.transform.SetParent(borderGo.transform, false);
            var backdropRt = backdropGo.GetComponent<RectTransform>();
            backdropRt.anchorMin = Vector2.zero;
            backdropRt.anchorMax = Vector2.one;
            const float borderPx = 3f;
            backdropRt.offsetMin = new Vector2(borderPx, borderPx);
            backdropRt.offsetMax = new Vector2(-borderPx, -borderPx);
            var backdropImg = backdropGo.GetComponent<Image>();
            backdropImg.color = new Color(0.06f, 0.12f, 0.10f, 0.98f); // Dark sea-green, very opaque
            backdropImg.raycastTarget = false;

            borderGo.transform.SetAsFirstSibling();

            // Create title
            var titleGo = new GameObject("HTP_Title", typeof(RectTransform));
            titleGo.transform.SetParent(panel.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1);
            titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.anchoredPosition = new Vector2(0, -14);
            titleRt.sizeDelta = new Vector2(0, 52);

            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "HOW  TO  PLAY";
            titleTmp.fontSize = 44;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = MenuVisualTheme.ChromeHeader;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.font = Resources.Load<TMP_FontAsset>("ARCADECLASSIC SDF");

            // Create scrollable body content
            var scrollGo = new GameObject("HTP_Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.035f, 0.10f);
            scrollRt.anchorMax = new Vector2(0.965f, 0.86f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            var scrollImg = scrollGo.GetComponent<Image>();
            scrollImg.color = new Color(0f, 0f, 0f, 0.01f);
            scrollImg.raycastTarget = true;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var vpRt = viewportGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = new Vector2(-14f, 0f); // Room for scrollbar
            var vpImg = viewportGo.GetComponent<Image>();
            vpImg.color = Color.clear;
            vpImg.raycastTarget = false;

            var bodyGo = new GameObject("HTP_Body", typeof(RectTransform), typeof(ContentSizeFitter));
            bodyGo.transform.SetParent(viewportGo.transform, false);
            var bodyRt = bodyGo.GetComponent<RectTransform>();
            bodyRt.anchorMin = new Vector2(0f, 1f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.pivot = new Vector2(0.5f, 1f);
            bodyRt.sizeDelta = new Vector2(0f, 0f);
            var bodyCsf = bodyGo.GetComponent<ContentSizeFitter>();
            bodyCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            bodyCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var bodyTmp = bodyGo.AddComponent<TextMeshProUGUI>();
            bodyTmp.text = GetHowToPlayText();
            bodyTmp.fontSize = 26;
            bodyTmp.lineSpacing = 6f;
            bodyTmp.color = MenuVisualTheme.ChromeHeader;
            bodyTmp.alignment = TextAlignmentOptions.Top;
            bodyTmp.font = Resources.Load<TMP_FontAsset>("ARCADECLASSIC SDF");
            bodyTmp.textWrappingMode = TextWrappingModes.Normal;
            bodyTmp.overflowMode = TextOverflowModes.Overflow;

            // Scrollbar
            var scrollbarGo = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            scrollbarGo.transform.SetParent(scrollGo.transform, false);
            var sbRt = scrollbarGo.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1f, 0f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.sizeDelta = new Vector2(10f, 0f);
            sbRt.anchoredPosition = Vector2.zero;
            var sbImg = scrollbarGo.GetComponent<Image>();
            sbImg.color = new Color(0.15f, 0.2f, 0.18f, 0.6f);

            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(scrollbarGo.transform, false);
            var handleRt = handleGo.GetComponent<RectTransform>();
            handleRt.anchorMin = Vector2.zero;
            handleRt.anchorMax = Vector2.one;
            handleRt.offsetMin = new Vector2(2f, 2f);
            handleRt.offsetMax = new Vector2(-2f, -2f);
            var handleImg = handleGo.GetComponent<Image>();
            handleImg.color = MenuVisualTheme.SliderFill;

            var scrollbar = scrollbarGo.GetComponent<Scrollbar>();
            scrollbar.handleRect = handleRt;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = handleImg;

            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            scroll.viewport = vpRt;
            scroll.content = bodyRt;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            // Create back button if missing
            if (_howToPlayBackButton == null)
            {
                var backGo = new GameObject("BTN_HowToPlayBack", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                backGo.transform.SetParent(panel.transform, false);
                var backRt = backGo.GetComponent<RectTransform>();
                backRt.anchorMin = new Vector2(0.5f, 0f);
                backRt.anchorMax = new Vector2(0.5f, 0f);
                backRt.pivot = new Vector2(0.5f, 0f);
                backRt.sizeDelta = new Vector2(248f, 36f);
                backRt.anchoredPosition = new Vector2(0f, 12f);

                MenuUiChrome.StyleButtonLikeSettings(backGo);
                _howToPlayBackButton = backGo.GetComponent<Button>();
                _howToPlayBackButton.onClick.AddListener(HideActivePanel);

                var backTextGo = new GameObject("Text", typeof(RectTransform));
                backTextGo.transform.SetParent(backGo.transform, false);
                var backTextRt = backTextGo.GetComponent<RectTransform>();
                backTextRt.anchorMin = Vector2.zero;
                backTextRt.anchorMax = Vector2.one;
                backTextRt.offsetMin = Vector2.zero;
                backTextRt.offsetMax = Vector2.zero;

                var backTmp = backTextGo.AddComponent<TextMeshProUGUI>();
                backTmp.text = "BACK";
                backTmp.font = Resources.Load<TMP_FontAsset>("ARCADECLASSIC SDF");
                backTmp.fontSize = 20f;
                backTmp.alignment = TextAlignmentOptions.Center;
                backTmp.color = MenuVisualTheme.ChromeHeader;

                MenuUiChrome.AttachStandardButtonHover(backGo);
            }

            // Make sure back button is on top
            if (_howToPlayBackButton != null)
                _howToPlayBackButton.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Returns How To Play text. Edit the shared source in SettingsManager.HowToPlayTextRaw().
        /// </summary>
        static string GetHowToPlayText() => SettingsManager.HowToPlayTextRaw();

        void ApplyHomeScreenProminence()
        {
            if (_homeScreenRoot != null)
            {
                float s = Mathf.Clamp(_homeMenuProminenceScale, 0.78f, 1.35f);
                _homeScreenRoot.transform.localScale = new Vector3(s, s, 1f);
            }
            if (_titleText != null)
            {
                const float minTitle = 62f;
                const float maxTitle = 88f;
                _titleText.fontSize = Mathf.Clamp(_titleText.fontSize * 1.15f, minTitle, maxTitle);
            }
            RectTransform nav = FindHomeNavStackRect();
            if (nav != null)
            {
                var vlg = nav.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                    vlg.spacing = Mathf.Clamp(Mathf.Max(vlg.spacing, 16f) * 1.1f, 16f, 28f);
                var sd = nav.sizeDelta;
                nav.sizeDelta = new Vector2(
                    Mathf.Max(sd.x * 1.12f, 260f),
                    Mathf.Max(sd.y * 1.15f, 310f));
                foreach (Transform child in nav)
                {
                    var label = child.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (label == null) continue;
                    label.fontSize = Mathf.Clamp(label.fontSize * 1.25f, 24f, 38f);
                }
            }
        }

        void EnsureHomeTitleSubtitle()
        {
            if (_titleText == null) return;
            var parent = _titleText.transform.parent as RectTransform;
            if (parent == null) return;

            _titleSubtitle = _titleSubtitleText;
            if (_titleSubtitle == null)
            {
                Transform existing = parent.Find("TitleSubtitle - TMP");
                if (existing != null)
                    _titleSubtitle = existing.GetComponent<TextMeshProUGUI>();
            }
            if (_titleSubtitle == null)
            {
                var go = new GameObject("TitleSubtitle - TMP", typeof(RectTransform), typeof(TextMeshProUGUI));
                go.transform.SetParent(parent, false);
                _titleSubtitle = go.GetComponent<TextMeshProUGUI>();
                _titleSubtitle.raycastTarget = false;
            }

            var trt = _titleText.rectTransform;
            // One-line wordmark: strip embedded newlines from scene / inspector text; no auto-wrap to a second line.
            {
                string wordmark = _titleText.text;
                if (!string.IsNullOrEmpty(wordmark))
                    _titleText.text = wordmark.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ");
                _titleText.textWrappingMode = TextWrappingModes.NoWrap;
            }
            var subRt = _titleSubtitle.rectTransform;
            subRt.localScale = Vector3.one;
            subRt.anchorMin = new Vector2(0.5f, 0.5f);
            subRt.anchorMax = new Vector2(0.5f, 0.5f);
            subRt.pivot = new Vector2(0.5f, 0.5f);
            _titleSubtitle.font = _titleText.font;
            if (_titleText.fontSharedMaterial != null)
                _titleSubtitle.fontSharedMaterial = _titleText.fontSharedMaterial;
            _titleSubtitle.text = !string.IsNullOrWhiteSpace(_homeMenuTagline) ? _homeMenuTagline.Trim() : "PUT  YOUR  BREATH  TO  THE  TEST!";
            _titleSubtitle.textWrappingMode = TextWrappingModes.Normal;
            _titleSubtitle.alignment = TextAlignmentOptions.Center;
            _titleSubtitle.fontStyle = FontStyles.Normal;
            _titleSubtitle.overflowMode = TextOverflowModes.Overflow;
            // Clearly subordinate to the wordmark; also keeps tagline from stretching as wide as the old 720px box.
            _titleSubtitle.fontSize = Mathf.Clamp(_titleText.fontSize * 0.22f, 14f, 22f);
            // Wider gaps between words (pixel font otherwise reads as one block).
            _titleSubtitle.wordSpacing = 10f;
            _titleSubtitle.characterSpacing = 0.5f;

            _titleText.ForceMeshUpdate(true);
            Vector2 titlePref = _titleText.GetPreferredValues(_titleText.text, 1600f, 900f);
            const float titlePadX = 10f;
            const float titlePadY = 8f;
            trt.sizeDelta = new Vector2(
                Mathf.Max(titlePref.x + titlePadX, 48f),
                Mathf.Max(titlePref.y + titlePadY, 32f));

            _titleSubtitle.ForceMeshUpdate(true);
            float subMaxW = Mathf.Min(360f, Mathf.Max(200f, titlePref.x * 0.68f));
            Vector2 subPref = _titleSubtitle.GetPreferredValues(_titleSubtitle.text, subMaxW, 220f);
            subRt.sizeDelta = new Vector2(subMaxW, Mathf.Max(subPref.y + 10f, 32f));

            _titleText.ForceMeshUpdate(true);
            _titleSubtitle.ForceMeshUpdate(true);
            Canvas.ForceUpdateCanvases();

            const float titleSubtitleGap = 5f;
            float hTitle = trt.rect.height;
            float hSub = subRt.rect.height;
            float xTitle = trt.anchoredPosition.x;
            float yTitle = trt.anchoredPosition.y;
            subRt.anchoredPosition = new Vector2(xTitle, yTitle - hTitle * 0.5f - titleSubtitleGap - hSub * 0.5f);
            subRt.SetSiblingIndex(trt.GetSiblingIndex() + 1);
        }

        /// <summary>Moves wordmark + tagline + <c>NavButtons</c> by <see cref="_homeBlockVerticalShiftPx"/> before backdrops.</summary>
        void ApplyHomeBlockVerticalShift()
        {
            float dy = _homeBlockVerticalShiftPx;
            if (Mathf.Approximately(dy, 0f)) return;

            void NudgeY(RectTransform rt, float deltaY)
            {
                if (rt == null) return;
                var ap = rt.anchoredPosition;
                rt.anchoredPosition = new Vector2(ap.x, ap.y + deltaY);
            }

            if (_titleText != null)
                NudgeY(_titleText.rectTransform, dy);
            if (_titleSubtitle != null)
                NudgeY(_titleSubtitle.rectTransform, dy);

            RectTransform nav = FindHomeNavStackRect();
            NudgeY(nav, dy);
        }

        /// <summary>Dark cards behind the title and nav column so they read on top of the live video plate.</summary>
        void SetupHomeScreenTextBackdrops()
        {
            if (_titleText == null) return;
            if (_homeScreenRoot != null)
            {
                var hrt = _homeScreenRoot.GetComponent<RectTransform>();
                if (hrt != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(hrt);
            }
            Canvas.ForceUpdateCanvases();

            RectTransform navStack = FindHomeNavStackRect();
            // Pushes the nav column down from its scene Y so the title card and nav card never read as one merged plate.
            const float titleNavGroupSeparationPx = 55f;
            if (navStack != null)
            {
                var ap = navStack.anchoredPosition;
                navStack.anchoredPosition = new Vector2(ap.x, ap.y - titleNavGroupSeparationPx);
            }

            if (_homeScreenRoot != null)
            {
                var hrt = _homeScreenRoot.GetComponent<RectTransform>();
                if (hrt != null)
                    LayoutRebuilder.ForceRebuildLayoutImmediate(hrt);
            }
            if (navStack != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(navStack);
            Canvas.ForceUpdateCanvases();

            // Tight insets: plates hug the text / button stack, not a wide banner.
            const float homeBackdropPadH = 10f;
            const float homeBackdropPadV = 10f;
            if (_titleText != null && _titleText.transform.parent != null)
                DestroyIfExists(_titleText.transform.parent, "HomeTitleTextBackdrop");
            TryInsertHomeTitleBlockBackdrop(
                _titleText, _titleSubtitle, "HomeTitleTextBackdrop", MenuVisualTheme.HomeTitleTextBackdrop, homeBackdropPadH, homeBackdropPadV);
            if (navStack != null)
            {
                if (navStack.parent != null)
                    DestroyIfExists(navStack.parent, "HomeNavStackBackdrop");
                TryInsertRectBackdropBehind(navStack, "HomeNavStackBackdrop", MenuVisualTheme.HomeNavStackBackdrop, homeBackdropPadH);
            }

            ApplyHomeBackdropPlatesFromTheme();
        }

        /// <summary>Re-applies home plate <see cref="Image"/> colors from theme (layout/TMP outline can desync vertex colors).</summary>
        void ApplyHomeBackdropPlatesFromTheme()
        {
            Transform root = null;
            if (_homeScreenRoot != null)
                root = _homeScreenRoot.transform;
            else if (_titleText != null)
                root = _titleText.transform.root;

            ApplyPlateImageColorRecursive(root, "HomeTitleTextBackdrop", MenuVisualTheme.HomeTitleTextBackdrop);
            ApplyPlateImageColorRecursive(root, "HomeNavStackBackdrop", MenuVisualTheme.HomeNavStackBackdrop);
        }

        static void ApplyPlateImageColorRecursive(Transform searchRoot, string goName, Color c)
        {
            if (searchRoot == null) return;
            foreach (Image img in searchRoot.GetComponentsInChildren<Image>(true))
            {
                if (img == null || img.gameObject.name != goName) continue;
                img.material = null;
                img.canvasRenderer.SetColor(c);
                img.color = c;
            }
        }

        RectTransform FindHomeNavStackRect()
        {
            if (_levelSelectButton != null && _levelSelectButton.transform.parent != null)
            {
                var p = _levelSelectButton.transform.parent;
                if (p.name == "NavButtons")
                    return p as RectTransform;
            }
            var roots = new[] { _titleText != null ? _titleText.transform.root : null, transform.root };
            for (int r = 0; r < roots.Length; r++)
            {
                var t = roots[r];
                if (t == null) continue;
                foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
                {
                    if (c != null && c.name == "NavButtons")
                        return c as RectTransform;
                }
            }
            return null;
        }

        static void GetAxisAlignedBoundsInParent(
            RectTransform a, RectTransform b, RectTransform parent, out Vector2 center, out Vector2 size)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            void Acc(RectTransform rt)
            {
                if (rt == null) return;
                var w = new Vector3[4];
                rt.GetWorldCorners(w);
                for (int i = 0; i < 4; i++)
                {
                    var lp = (Vector2)parent.InverseTransformPoint(w[i]);
                    min = Vector2.Min(min, lp);
                    max = Vector2.Max(max, lp);
                }
            }
            Acc(a);
            Acc(b);
            size = max - min;
            center = (min + max) * 0.5f;
        }

        static void TryInsertHomeTitleBlockBackdrop(
            TextMeshProUGUI titleTmp, TextMeshProUGUI subtitleTmp, string goName, Color c, float padH, float padV)
        {
            if (titleTmp == null) return;
            var target = titleTmp.rectTransform;
            var parent = target.parent as RectTransform;
            if (parent == null) return;

            int idx = target.GetSiblingIndex();
            var go = new GameObject(goName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var brt = go.GetComponent<RectTransform>();
            var img = go.GetComponent<Image>();
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(idx);

            brt.localScale = target.localScale;
            brt.localRotation = target.localRotation;
            brt.anchorMin = target.anchorMin;
            brt.anchorMax = target.anchorMax;
            brt.pivot = new Vector2(0.5f, 0.5f);

            titleTmp.ForceMeshUpdate(true);
            if (subtitleTmp != null) subtitleTmp.ForceMeshUpdate(true);

            if (subtitleTmp != null)
            {
                var subrt = subtitleTmp.rectTransform;
                GetAxisAlignedBoundsInParent(target, subrt, parent, out Vector2 center, out Vector2 sz);
                brt.anchoredPosition = new Vector2(center.x, center.y);
                brt.sizeDelta = new Vector2(sz.x + 2f * padH, sz.y + 2f * padV);
            }
            else
            {
                brt.pivot = target.pivot;
                brt.anchoredPosition3D = target.anchoredPosition3D;
                float pw = titleTmp.preferredWidth;
                float ph = titleTmp.preferredHeight;
                bool stretchH = !Mathf.Approximately(target.anchorMin.x, target.anchorMax.x);
                bool stretchV = !Mathf.Approximately(target.anchorMin.y, target.anchorMax.y);
                if (stretchH || stretchV || pw < 1f || ph < 1f)
                {
                    brt.sizeDelta = target.sizeDelta;
                    brt.offsetMin = target.offsetMin;
                    brt.offsetMax = target.offsetMax;
                    InflateRectForBackdropPad(brt, Mathf.Min(padH, 10f));
                }
                else
                    brt.sizeDelta = new Vector2(pw + 2f * padH, ph + 2f * padV);
            }

            img.sprite = HomePanelRoundedSprite();
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 1f;
            img.material = null;
            img.canvasRenderer.SetColor(c);
            img.color = c;
            img.raycastTarget = false;
        }

        static void TryInsertRectBackdropBehind(RectTransform target, string goName, Color c, float pad)
        {
            if (target == null) return;
            var parent = target.parent as RectTransform;
            if (parent == null) return;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name != goName) continue;
                var existingImg = parent.GetChild(i).GetComponent<Image>();
                if (existingImg != null)
                {
                    existingImg.material = null;
                    existingImg.canvasRenderer.SetColor(c);
                    existingImg.color = c;
                }
                return;
            }

            int idx = target.GetSiblingIndex();
            var go = new GameObject(goName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var brt = go.GetComponent<RectTransform>();
            var newBackdropImg = go.GetComponent<Image>();
            go.transform.SetParent(parent, false);
            go.transform.SetSiblingIndex(idx);

            brt.localScale = target.localScale;
            brt.localRotation = target.localRotation;
            brt.anchorMin = target.anchorMin;
            brt.anchorMax = target.anchorMax;
            brt.pivot = target.pivot;
            brt.anchoredPosition3D = target.anchoredPosition3D;
            brt.sizeDelta = target.sizeDelta;
            brt.offsetMin = target.offsetMin;
            brt.offsetMax = target.offsetMax;
            InflateRectForBackdropPad(brt, pad);

            newBackdropImg.sprite = HomePanelRoundedSprite();
            newBackdropImg.type = Image.Type.Sliced;
            newBackdropImg.pixelsPerUnitMultiplier = 1f;
            newBackdropImg.material = null;
            newBackdropImg.canvasRenderer.SetColor(c);
            newBackdropImg.color = c;
            newBackdropImg.raycastTarget = false;
        }

        static void InflateRectForBackdropPad(RectTransform rt, float pad)
        {
            bool stretchH = !Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x);
            bool stretchV = !Mathf.Approximately(rt.anchorMin.y, rt.anchorMax.y);
            if (stretchH || stretchV)
            {
                var oMin = rt.offsetMin;
                var oMax = rt.offsetMax;
                float px = stretchH ? pad : 0f;
                float py = stretchV ? pad : 0f;
                rt.offsetMin = new Vector2(oMin.x - px, oMin.y - py);
                rt.offsetMax = new Vector2(oMax.x + px, oMax.y + py);
            }
            else
            {
                var sd = rt.sizeDelta;
                rt.sizeDelta = new Vector2(sd.x + 2f * pad, sd.y + 2f * pad);
            }
        }

        private void HideAllPanels()
        {
            if (_levelSelectPanel != null) _levelSelectPanel.SetActive(false);
            if (_howToPlayPanel != null) _howToPlayPanel.SetActive(false);
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
            if (_creditsPanel != null) _creditsPanel.SetActive(false);
            _activePanel = null;
        }

        private void SetNavButtonsInteractable(bool interactable)
        {
            Button[] nav = {
                _levelSelectButton, _howToPlayButton, _settingsButton, _creditsButton, _quitButton
            };
            foreach (var b in nav)
            {
                if (b == null) continue;
                b.interactable = interactable;
                var h = b.gameObject.GetComponent<CardHoverEffect>();
                if (h != null) h.SetInteractable(interactable);
            }
        }

        // -------------------------------------------------------------------
        // Level Select card generation
        //
        /// <summary>Padding from panel edges — panel root tint only (no inset frame; avoids inner cream box clipping).</summary>
        const float LevelSelectCardAreaPadH = 18f;

        /// <summary>Bottom inset so the grid clears the BACK pill (pixels from panel bottom).</summary>
        const float LevelSelectLayoutBottomPadding = 64f;

        /// <summary>Top inset below header / safe-area (pixels).</summary>
        const float LevelSelectLayoutTopPadding = 20f;

        /// <summary>Sailboat column wider than legacy square (~grid height); reserve so horizontal padding stays honest.</summary>
        const float LevelSelectFeaturedWidthScale = 0.95f;

        /// <summary>Sailboat taller than grid block ratio (clamped to row height).</summary>
        const float LevelSelectFeaturedHeightScale = 0.92f;

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
            // and breaks the scripted 50/50 split — featured card disappears and the 2×2 grid won't stay left.
            DisableConflictingLayoutOnGridParent();

            for (int i = _gameGridParent.childCount - 1; i >= 0; i--)
                Destroy(_gameGridParent.GetChild(i).gameObject);

            _cardObjects.Clear();
            _cardDefs.Clear();

            MinigameDefinition[] defs = MinigameManager.Instance.MinigamesForLevelSelect;
            if (defs == null || defs.Length == 0)
            {
                PositionLevelSelectBackButton();
                EnsureLevelSelectInsetChrome();
                return;
            }

            MinigameDefinition featuredDef = null;
            var others = new List<MinigameDefinition>();
            foreach (var d in defs)
            {
                if (d == null) continue;
                if (IsSailboatFeaturedDefinition(d))
                    featuredDef = d;
                else
                    others.Add(d);
            }

            // Full-area root: one horizontal row with symmetric side insets; gap between 2×2 and flagship = grid spacing.
            var rootGo = new GameObject("LevelSelectLayoutRoot", typeof(RectTransform));
            rootGo.transform.SetParent(_gameGridParent, false);

            var rootRT = rootGo.GetComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = new Vector2(LevelSelectCardAreaPadH, LevelSelectLayoutBottomPadding);
            rootRT.offsetMax = new Vector2(-LevelSelectCardAreaPadH, -LevelSelectLayoutTopPadding);

            var gridParentRT = _gameGridParent as RectTransform;
            if (gridParentRT == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(gridParentRT);
            LayoutRebuilder.ForceRebuildLayoutImmediate(rootRT);

            const float rowBottomInset = 14f;
            const float rowTopInset = 8f;

            // 2×2: narrower cells, taller cells; compactH may shrink if row doesn’t fit Sailboat/feature height.
            float compactW = 280f;
            float compactH = 275f;
            const float cardGap = 14f;
            var gridPadding = new RectOffset(8, 8, 10, 10);

            float tentativeBlockH = gridPadding.top + 2f * compactH + cardGap + gridPadding.bottom;
            float availRowH = Mathf.Max(0f, rootRT.rect.height - rowBottomInset - rowTopInset);
            if (availRowH > 2f && tentativeBlockH > availRowH)
            {
                float scale = Mathf.Max(0.72f, (availRowH - 8f) / tentativeBlockH);
                compactH = Mathf.Round(Mathf.Clamp(compactH * scale, 140f, 300f));
            }

            float gridBlockWidth = gridPadding.left + 2f * compactW + cardGap + gridPadding.right;
            float gridBlockHeight = gridPadding.top + 2f * compactH + cardGap + gridPadding.bottom;
            const float featuredOverGridScale = 1.0f; // Match 2x2 grid height exactly
            float featuredSquareSize = Mathf.Round(gridBlockHeight * featuredOverGridScale);

            const float rootHorizontalInset = LevelSelectCardAreaPadH * 2f;
            float availW = rootRT.rect.width;
            if (availW < 2f)
                availW = Mathf.Max(2f, gridParentRT.rect.width - rootHorizontalInset);

            const float minHorizontalSide = 12f;

            float featuredReserveWidth = featuredDef != null
                ? Mathf.Round(Mathf.Min(featuredSquareSize * LevelSelectFeaturedWidthScale, availW * 0.52f))
                : 0f;
            float contentWidth = featuredDef != null
                ? gridBlockWidth + cardGap + featuredReserveWidth
                : gridBlockWidth;
            float horizontalPad = (availW - contentWidth) * 0.5f;
            horizontalPad = Mathf.Max(minHorizontalSide, horizontalPad);
            // If the panel is narrow, shrink the square so the block never exceeds the row (avoids clipping past the purple frame).
            float rowInnerW = availW - 2f * horizontalPad;
            if (featuredDef != null && contentWidth > rowInnerW + 0.5f)
            {
                float maxSide = Mathf.Max(96f, rowInnerW - gridBlockWidth - cardGap);
                featuredSquareSize = Mathf.Min(featuredSquareSize, maxSide);
                contentWidth = gridBlockWidth + cardGap + featuredSquareSize;
                horizontalPad = (availW - contentWidth) * 0.5f;
                horizontalPad = Mathf.Max(minHorizontalSide, horizontalPad);
            }
            rowInnerW = availW - 2f * horizontalPad;

            // No HorizontalLayoutGroup — explicit rects keep the 2×2 + flagship width-exact inside the row.
            var rowGo = new GameObject("LevelSelectRow", typeof(RectTransform));
            rowGo.transform.SetParent(rootGo.transform, false);
            var rowRT = rowGo.GetComponent<RectTransform>();
            rowRT.anchorMin = Vector2.zero;
            rowRT.anchorMax = Vector2.one;
            rowRT.offsetMin = new Vector2(horizontalPad, rowBottomInset);
            rowRT.offsetMax = new Vector2(-horizontalPad, -rowTopInset);

            var leftGo = new GameObject("LeftColumn", typeof(RectTransform), typeof(GridLayoutGroup));
            leftGo.transform.SetParent(rowGo.transform, false);
            var leftRT = leftGo.GetComponent<RectTransform>();
            if (featuredDef != null)
            {
                // Fixed size matching grid block, centered vertically in the row
                leftRT.anchorMin = new Vector2(0f, 0.5f);
                leftRT.anchorMax = new Vector2(0f, 0.5f);
                leftRT.pivot = new Vector2(0f, 0.5f);
                leftRT.sizeDelta = new Vector2(gridBlockWidth, gridBlockHeight);
                leftRT.anchoredPosition = Vector2.zero;
            }
            else
            {
                leftRT.anchorMin = new Vector2(0.5f, 0.5f);
                leftRT.anchorMax = new Vector2(0.5f, 0.5f);
                leftRT.pivot = new Vector2(0.5f, 0.5f);
                leftRT.sizeDelta = new Vector2(gridBlockWidth, gridBlockHeight);
                leftRT.anchoredPosition = Vector2.zero;
            }

            var grid = leftGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(compactW, compactH);
            grid.spacing = new Vector2(cardGap, cardGap);
            grid.padding = gridPadding;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;

            foreach (var def in others)
                BuildCard(def, leftGo.transform, CardSizeMode.Compact, compactW, compactH);

            if (featuredDef != null)
            {
                float maxFeaturedColumnW = Mathf.Max(96f, rowInnerW - gridBlockWidth - cardGap);
                float featuredWidth = Mathf.Round(
                    Mathf.Min(featuredSquareSize * LevelSelectFeaturedWidthScale, maxFeaturedColumnW));
                float featuredHeight = Mathf.Round(
                    Mathf.Min(gridBlockHeight * LevelSelectFeaturedHeightScale, availRowH - 4f));
                featuredHeight = Mathf.Max(featuredHeight, gridBlockHeight * 0.98f);
                GameObject feGo = BuildCard(featuredDef, rowGo.transform, CardSizeMode.Featured, compactW, compactH,
                    featuredWidth, featuredHeight);
                var feRT = feGo.GetComponent<RectTransform>();
                float x0 = gridBlockWidth + cardGap;
                feRT.anchorMin = new Vector2(0f, 0.5f);
                feRT.anchorMax = new Vector2(0f, 0.5f);
                feRT.pivot = new Vector2(0f, 0.5f);
                feRT.sizeDelta = new Vector2(featuredWidth, featuredHeight);
                feRT.anchoredPosition = new Vector2(x0, 0f);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(gridParentRT);

            PositionLevelSelectBackButton();
            EnsureLevelSelectInsetChrome();
        }

        /// <summary>Ensures deprecated inset chrome is gone (was cream-bordered inner box clipping cards); panel root tint unchanged.</summary>
        void EnsureLevelSelectInsetChrome()
        {
            if (_levelSelectPanel == null || _gameGridParent == null) return;

            Transform existing = _levelSelectPanel.transform.Find("LevelSelectInsetFrame");
            if (existing != null)
                Destroy(existing.gameObject);

            _gameGridParent.transform.SetAsLastSibling();
            PositionLevelSelectBackButton();
        }

        const string DefaultCreditsSoloRolesLine =
            "Programming, design, art direction, audio\n"
            + "A personal project, built with care";

        const string DefaultCreditsBuildBlurb =
            "Developed in Unity 6 (C#)\n"
            + "Self-contained, data-driven minigames\n"
            + "Windows build with full input options\n"
            + "WebGL with simulated breath input in the browser";

        const string DefaultCreditsToolsLine =
            "Unity Editor, C# & version control\n"
            + "Movavi (video and editing)\n"
            + "Cursor (tooling)\n"
            + "Freesound.org (SFX)\n"
            + "Suno (music)";

        /// <summary>Turns a paragraph into list lines: newlines first, else semicolons, else ". " sentence breaks.</summary>
        static string[] SplitCreditsListItems(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
            raw = raw.Trim().Replace("\r\n", "\n");
            if (raw.Contains('\n'))
            {
                var parts = raw.Split('\n');
                var list = new List<string>(parts.Length);
                foreach (var p in parts)
                {
                    var t = p.Trim();
                    if (t.Length > 0) list.Add(t);
                }
                return list.Count > 0 ? list.ToArray() : new[] { raw };
            }
            if (raw.Contains(';'))
            {
                var a = raw.Split(';');
                var list = new List<string>(a.Length);
                foreach (var p in a)
                {
                    var t = p.Trim();
                    if (t.Length > 0) list.Add(t);
                }
                if (list.Count > 1) return list.ToArray();
            }
            if (raw.Contains(". "))
            {
                var chunks = raw.Split(new[] { ". " }, StringSplitOptions.RemoveEmptyEntries);
                if (chunks.Length > 1)
                {
                    for (int i = 0; i < chunks.Length - 1; i++)
                        chunks[i] = chunks[i].TrimEnd() + ".";
                    for (int i = 0; i < chunks.Length; i++)
                        chunks[i] = chunks[i].Trim();
                    return chunks;
                }
            }
            return new[] { raw };
        }

        static string FormatCreditsListLinesForDisplay(string[] items)
        {
            if (items == null || items.Length == 0) return "";
            var b = new StringBuilder();
            for (int i = 0; i < items.Length; i++)
            {
                if (i > 0) b.Append('\n');
                b.Append("-  ");
                b.Append(items[i].Trim());
            }
            return b.ToString();
        }

        /// <summary>Doubles normal spaces so pixel / awkward fonts don’t look crushed (applied after <see cref="GameFont.SanitizeForPixelFont"/>).</summary>
        static string CreditsWidenInterWordSpaces(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return s.Replace(" ", "  ");
        }

        static string CreditsNormalizeRolesForDisplay(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            var t = raw.Trim().Replace("\r\n", "\n");
            // Drop legacy awkward "All …" line openings (solo cred reads cleaner without it).
            if (t.Contains('\n'))
            {
                var lines = t.Split('\n');
                var sb = new StringBuilder();
                foreach (var line in lines)
                {
                    var L = line.Trim();
                    if (L.StartsWith("All ", StringComparison.OrdinalIgnoreCase))
                        L = L.Length > 4 ? L.Substring(4).TrimStart() : "";
                    if (L.Length == 0) continue;
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(L);
                }
                return sb.ToString();
            }
            else if (t.StartsWith("All ", StringComparison.OrdinalIgnoreCase))
                t = t.Substring(4).TrimStart();

            // Comma-separated → one role per line for readability
            var parts = t.Split(',');
            if (parts.Length <= 1) return t;
            var commaSb = new StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                var p = parts[i].Trim();
                if (p.Length == 0) continue;
                if (commaSb.Length > 0) commaSb.Append('\n');
                commaSb.Append(p);
            }
            return commaSb.Length > 0 ? commaSb.ToString() : t;
        }

        /// <summary>Pixel-font TMP: no kerning, generous tracking on body lines.</summary>
        static void CreditsConfigureTmp(
            TextMeshProUGUI tmp, float charSpacing, float wordSpacing, float lineSpacing)
        {
            tmp.fontFeatures = new List<OTL_FeatureTag>();
            tmp.characterSpacing = charSpacing;
            tmp.wordSpacing = wordSpacing;
            tmp.lineSpacing = lineSpacing;
        }

        void DestroyIfExists(Transform parent, string childName)
        {
            var t = parent.Find(childName);
            if (t != null) Destroy(t.gameObject);
        }

        /// <summary>
        /// Centered, sectioned credits (DEVELOPER, BUILD, TOOLS, ENGINE) with clear type hierarchy and spaced body text.
        /// </summary>
        private void SetupCreditsContent()
        {
            if (_creditsPanel == null) return;

            Transform headerTr = _creditsPanel.transform.Find("CreditsText - TMP");
            if (headerTr == null) return;

            var titleTmp = headerTr.GetComponent<TextMeshProUGUI>();
            if (titleTmp == null) return;

            const float titleSize = 62f;
            const float sectionHeaderSize = 32f;
            // Identity line (name / studio) slightly larger than bulleted list body.
            const float bodySizePrimary = 26f;
            const float bodySizeList = 22f;
            const float titleChar = 2f;
            const float headChar = 4f;
            const float headWord = 2f;
            const float bodyCharPrimary = 6f;
            const float bodyWordPrimary = 4f;
            const float bodyLinePrimary = 14f;
            const float bodyCharList = 5f;
            const float bodyWordList = 3f;
            const float bodyLineList = 12f;
            const float vlgSpacing = 6f;

            DestroyIfExists(_creditsPanel.transform, "CreditsBody");
            DestroyIfExists(_creditsPanel.transform, "CreditsScroll");
            DestroyIfExists(_creditsPanel.transform, "CreditsSections");

            var hrt = headerTr.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.5f, 1f);
            hrt.anchorMax = new Vector2(0.5f, 1f);
            hrt.pivot = new Vector2(0.5f, 1f);
            // Slight -X: pixel font + titleChar reads optically right of true center; a bit more -Y: sit higher in the header band.
            hrt.anchoredPosition = new Vector2(-6f, -2f);
            hrt.sizeDelta = new Vector2(720f, 80f);
            titleTmp.text = "CREDITS";
            titleTmp.fontSize = titleSize;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.color = MenuVisualTheme.ChromeHeader;
            titleTmp.raycastTarget = false;
            titleTmp.textWrappingMode = TextWrappingModes.Normal;
            titleTmp.lineSpacing = 0f;
            CreditsConfigureTmp(titleTmp, titleChar, 0f, 0f);

            // Scrollable region so long credits never overlap section headers; scrollbar when content > viewport.
            var scrollRoot = new GameObject("CreditsScroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollRoot.transform.SetParent(_creditsPanel.transform, false);
            var scrollRt = scrollRoot.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0.04f, 0.2f);
            scrollRt.anchorMax = new Vector2(0.96f, 0.86f);
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = Vector2.zero;
            scrollRt.pivot = new Vector2(0.5f, 0.5f);
            var scrollRootImg = scrollRoot.GetComponent<Image>();
            scrollRootImg.sprite = UiWhiteSprite();
            scrollRootImg.color = new Color(0f, 0f, 0f, 0.02f);
            scrollRootImg.raycastTarget = true;
            var scroll = scrollRoot.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            scroll.inertia = true;

            const float scrollBarW = 14f;
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            viewportGo.transform.SetParent(scrollRoot.transform, false);
            var vpRt = viewportGo.GetComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero;
            vpRt.anchorMax = Vector2.one;
            vpRt.pivot = new Vector2(0.5f, 0.5f);
            vpRt.offsetMin = Vector2.zero;
            vpRt.offsetMax = new Vector2(-scrollBarW, 0f);
            var vpImg = viewportGo.GetComponent<Image>();
            vpImg.sprite = UiWhiteSprite();
            vpImg.color = new Color(0f, 0f, 0f, 0.02f);
            vpImg.raycastTarget = false;

            var sectionsGo = new GameObject("CreditsSections", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            sectionsGo.transform.SetParent(viewportGo.transform, false);
            var srt = sectionsGo.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(0f, 1f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0.5f, 1f);
            srt.anchoredPosition = Vector2.zero;
            srt.sizeDelta = Vector2.zero;
            var contentFitter = sectionsGo.GetComponent<ContentSizeFitter>();
            contentFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var vlg = sectionsGo.GetComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = vlgSpacing;
            vlg.padding = new RectOffset(12, 12, 8, 10);

            var sbRoot = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            sbRoot.transform.SetParent(scrollRoot.transform, false);
            var sbRt = sbRoot.GetComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1f, 0f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.sizeDelta = new Vector2(scrollBarW, 0f);
            sbRt.anchoredPosition = Vector2.zero;
            var trackImg = sbRoot.GetComponent<Image>();
            trackImg.sprite = UiWhiteSprite();
            trackImg.type = Image.Type.Simple;
            trackImg.color = new Color(MenuVisualTheme.SliderTrack.r, MenuVisualTheme.SliderTrack.g,
                MenuVisualTheme.SliderTrack.b, 0.55f);
            var slidingGo = new GameObject("SlidingArea", typeof(RectTransform));
            slidingGo.transform.SetParent(sbRoot.transform, false);
            var sldRt = slidingGo.GetComponent<RectTransform>();
            sldRt.anchorMin = Vector2.zero;
            sldRt.anchorMax = Vector2.one;
            sldRt.pivot = new Vector2(0.5f, 0.5f);
            sldRt.sizeDelta = Vector2.zero;
            sldRt.anchoredPosition = Vector2.zero;
            sldRt.offsetMin = new Vector2(2f, 3f);
            sldRt.offsetMax = new Vector2(-2f, -3f);
            var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleGo.transform.SetParent(slidingGo.transform, false);
            var hRt = handleGo.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(0f, 0f);
            hRt.anchorMin = new Vector2(0f, 0f);
            hRt.anchorMax = new Vector2(1f, 0f);
            hRt.pivot = new Vector2(0.5f, 0.5f);
            var hImg = handleGo.GetComponent<Image>();
            hImg.sprite = UiWhiteSprite();
            hImg.type = Image.Type.Simple;
            hImg.color = MenuVisualTheme.SliderFill;
            var scrollbar = sbRoot.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.targetGraphic = hImg;
            scrollbar.handleRect = hRt;
            scroll.viewport = vpRt;
            scroll.content = srt;
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            scroll.verticalScrollbarSpacing = 0f;

            TMP_FontAsset font = titleTmp.font;
            Material mat = titleTmp.fontSharedMaterial;
            Color headCol = MenuVisualTheme.ChromeHeader;
            Color bodyCol = MenuVisualTheme.ChromeLabel;

            void AddSpacer(float h)
            {
                var sp = new GameObject("Spacer", typeof(RectTransform), typeof(LayoutElement));
                sp.transform.SetParent(sectionsGo.transform, false);
                var srt2 = sp.GetComponent<RectTransform>();
                srt2.sizeDelta = Vector2.zero;
                sp.GetComponent<LayoutElement>().minHeight = h;
            }

            void AddSectionHeader(string s)
            {
                var go = new GameObject("Hdr_" + s, typeof(RectTransform), typeof(TextMeshProUGUI),
                    typeof(ContentSizeFitter), typeof(LayoutElement));
                go.transform.SetParent(sectionsGo.transform, false);
                {
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.sizeDelta = Vector2.zero;
                }
                var tmp = go.GetComponent<TextMeshProUGUI>();
                tmp.font = font;
                if (mat != null) tmp.fontSharedMaterial = mat;
                tmp.text = GameFont.SanitizeForPixelFont(s);
                tmp.fontSize = sectionHeaderSize;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = headCol;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.raycastTarget = false;
                CreditsConfigureTmp(tmp, headChar, headWord, 0f);
                var csf = go.GetComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var le = go.GetComponent<LayoutElement>();
                le.flexibleWidth = 1f;
            }

            void AddBody(string s, bool primaryLine = false)
            {
                if (string.IsNullOrWhiteSpace(s)) return;
                var go = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI),
                    typeof(ContentSizeFitter), typeof(LayoutElement));
                go.transform.SetParent(sectionsGo.transform, false);
                {
                    var rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.sizeDelta = Vector2.zero;
                }
                var tmp = go.GetComponent<TextMeshProUGUI>();
                tmp.font = font;
                if (mat != null) tmp.fontSharedMaterial = mat;
                tmp.text = CreditsWidenInterWordSpaces(GameFont.SanitizeForPixelFont(s.Trim()));
                if (primaryLine)
                {
                    tmp.fontSize = bodySizePrimary;
                    CreditsConfigureTmp(tmp, bodyCharPrimary, bodyWordPrimary, bodyLinePrimary);
                }
                else
                {
                    tmp.fontSize = bodySizeList;
                    CreditsConfigureTmp(tmp, bodyCharList, bodyWordList, bodyLineList);
                }
                tmp.fontStyle = FontStyles.Normal;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.margin = Vector4.zero;
                tmp.color = bodyCol;
                tmp.textWrappingMode = TextWrappingModes.Normal;
                tmp.overflowMode = TextOverflowModes.Overflow;
                tmp.raycastTarget = false;
                var csf = go.GetComponent<ContentSizeFitter>();
                csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                var le = go.GetComponent<LayoutElement>();
                le.flexibleWidth = 1f;
            }

            // --- DEVELOPER
            AddSectionHeader("DEVELOPER");
            AddSpacer(6f);
            {
                string name = _creditsDeveloperName != null ? _creditsDeveloperName.Trim() : "";
                string studio = _creditsStudio != null ? _creditsStudio.Trim() : "";
                string line1;
                if (name.Length > 0 && studio.Length > 0)
                    line1 = name + "  -  " + studio;
                else if (name.Length > 0)
                    line1 = name;
                else if (studio.Length > 0)
                    line1 = studio;
                else
                    line1 = "";
                if (line1.Length > 0)
                    AddBody(line1, true);
                string roles = CreditsNormalizeRolesForDisplay(_creditsRoles);
                if (string.IsNullOrWhiteSpace(roles)) roles = DefaultCreditsSoloRolesLine;
                string listBlock = FormatCreditsListLinesForDisplay(SplitCreditsListItems(roles));
                if (listBlock.Length > 0)
                {
                    AddSpacer(8f);
                    AddBody(listBlock, false);
                }
            }
            AddSpacer(16f);

            // --- BUILD
            AddSectionHeader("BUILD");
            AddSpacer(6f);
            {
                string blurb = string.IsNullOrWhiteSpace(_creditsBuildBlurb) ? DefaultCreditsBuildBlurb : _creditsBuildBlurb.Trim();
                string listBlock = FormatCreditsListLinesForDisplay(SplitCreditsListItems(blurb));
                if (listBlock.Length > 0)
                    AddBody(listBlock, false);
            }
            AddSpacer(16f);

            // --- TOOLS
            AddSectionHeader("TOOLS");
            AddSpacer(6f);
            {
                string tools = string.IsNullOrWhiteSpace(_creditsToolsAck) ? DefaultCreditsToolsLine : _creditsToolsAck.Trim();
                string listBlock = FormatCreditsListLinesForDisplay(SplitCreditsListItems(tools));
                if (listBlock.Length > 0)
                    AddBody(listBlock, false);
            }
            AddSpacer(16f);

            // --- ENGINE — no hyphen bullets here (avoid “-— URP” reading as artifact); spell out Render Pipeline.
            AddSectionHeader("ENGINE");
            AddSpacer(6f);
            AddBody("Unity 6 WITH C#", false);

            headerTr.SetAsFirstSibling();
            scrollRoot.transform.SetAsFirstSibling();
            headerTr.SetAsFirstSibling();
            PositionCreditsBackButton();

            LayoutRebuilder.ForceRebuildLayoutImmediate(srt);
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRt);
            Canvas.ForceUpdateCanvases();
            scroll.verticalNormalizedPosition = 1f;
        }

        /// <summary>BACK pinned to the bottom of the credits panel so it never covers ENGINE / last lines.</summary>
        void PositionCreditsBackButton()
        {
            if (_creditsBackButton == null) return;
            var backRT = _creditsBackButton.GetComponent<RectTransform>();
            if (_creditsPanel != null && backRT.parent != _creditsPanel.transform)
                backRT.SetParent(_creditsPanel.transform, false);

            backRT.anchorMin = new Vector2(0.5f, 0f);
            backRT.anchorMax = new Vector2(0.5f, 0f);
            backRT.pivot = new Vector2(0.5f, 0f);
            backRT.sizeDelta = new Vector2(200f, 36f);
            backRT.anchoredPosition = new Vector2(0f, 10f);
            backRT.SetAsLastSibling();
        }

        /// <summary>
        /// BACK centered on the level-select panel. Kept under LevelSelectPanel so it survives grid rebuilds.
        /// </summary>
        private void PositionLevelSelectBackButton()
        {
            if (_levelSelectBackButton == null) return;
            var backRT = _levelSelectBackButton.GetComponent<RectTransform>();
            if (_levelSelectPanel != null && backRT.parent != _levelSelectPanel.transform)
                backRT.SetParent(_levelSelectPanel.transform, false);

            backRT.anchorMin = new Vector2(0.5f, 0f);
            backRT.anchorMax = new Vector2(0.5f, 0f);
            backRT.pivot = new Vector2(0.5f, 0f);
            backRT.sizeDelta = new Vector2(180f, 34f);
            backRT.anchoredPosition = new Vector2(0f, 14f);
            backRT.SetAsLastSibling();
        }

        /// <summary>Flagship card on the right — Sailboat; match by id or scene if assets differ.</summary>
        private static bool IsSailboatFeaturedDefinition(MinigameDefinition d)
        {
            if (string.Equals(d.MinigameId, "sailboat", StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(d.SceneName) &&
                string.Equals(d.SceneName.Trim(), "SAILBOAT", StringComparison.OrdinalIgnoreCase))
                return true;
            return false;
        }

        private enum CardSizeMode
        {
            Compact,
            Featured
        }

        private GameObject BuildCard(MinigameDefinition def, Transform parent, CardSizeMode sizeMode,
            float compactCellWidth, float compactCellHeight, float featuredWidth = -1f, float featuredHeight = -1f)
        {
            bool available = def.IsUnlocked && SceneLoader.IsSceneInBuildSettings(def.SceneName);
            bool featured = sizeMode == CardSizeMode.Featured;
            Sprite thumbSprite = LevelSelectBackdrop.ResolveThumbnail(def);
            bool hasThumb = thumbSprite != null;

            var cardGO = new GameObject($"Card_{def.MinigameId}",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            cardGO.transform.SetParent(parent, false);

            var cardRT = cardGO.GetComponent<RectTransform>();
            if (featured)
            {
                // Featured card: may have different width and height (not necessarily square)
                float fw = featuredWidth > 1f ? featuredWidth : Mathf.Round((2f * compactCellHeight + 16f + 20f) * 1.07f);
                float fh = featuredHeight > 1f ? featuredHeight : fw; // Default to square if no height specified
                cardRT.anchorMin = new Vector2(0f, 0.5f);
                cardRT.anchorMax = new Vector2(0f, 0.5f);
                cardRT.pivot = new Vector2(0f, 0.5f);
                cardRT.sizeDelta = new Vector2(fw, fh);
                cardRT.anchoredPosition = Vector2.zero;
            }
            else
            {
                var layoutEl = cardGO.AddComponent<LayoutElement>();
                layoutEl.preferredWidth = compactCellWidth;
                layoutEl.preferredHeight = compactCellHeight;
                layoutEl.minWidth = compactCellWidth * 0.85f;
                layoutEl.minHeight = compactCellHeight * 0.85f;
            }

            if (cardGO.GetComponent<RectMask2D>() == null)
                cardGO.AddComponent<RectMask2D>();

            var cardImage = cardGO.GetComponent<Image>();
            cardImage.raycastTarget = true;
            cardImage.maskable = true;

            if (hasThumb)
            {
                // Opaque hit target only; art is on child Backdrop (cover crop, no letterboxing).
                cardImage.sprite = null;
                cardImage.color = new Color(0f, 0f, 0f, 0f);
                cardImage.preserveAspect = false;

                var backdropGO = new GameObject("Backdrop",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                backdropGO.transform.SetParent(cardGO.transform, false);
                backdropGO.transform.SetAsFirstSibling();

                var bdRT = backdropGO.GetComponent<RectTransform>();
                bdRT.anchorMin = Vector2.zero;
                bdRT.anchorMax = Vector2.one;
                bdRT.pivot = new Vector2(0.5f, 0.5f);
                bdRT.offsetMin = Vector2.zero;
                bdRT.offsetMax = Vector2.zero;

                var bdImg = backdropGO.GetComponent<Image>();
                bdImg.sprite = thumbSprite;
                bdImg.type = Image.Type.Simple;
                bdImg.preserveAspect = false;
                bdImg.color = available ? Color.white : new Color(0.3f, 0.3f, 0.3f);
                bdImg.raycastTarget = false;
                bdImg.maskable = true;

                var arf = backdropGO.AddComponent<AspectRatioFitter>();
                arf.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                arf.aspectRatio = LevelSelectBackdrop.SpriteAspect(thumbSprite);

                // Per-minigame tint from ScriptableObject CardColor (under dim overlay, over screenshot).
                var tintGo = new GameObject("CardTint",
                    typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                tintGo.transform.SetParent(cardGO.transform, false);
                tintGo.transform.SetSiblingIndex(backdropGO.transform.GetSiblingIndex() + 1);
                var tintRT = tintGo.GetComponent<RectTransform>();
                tintRT.anchorMin = Vector2.zero;
                tintRT.anchorMax = Vector2.one;
                tintRT.offsetMin = Vector2.zero;
                tintRT.offsetMax = Vector2.zero;
                var tintImg = tintGo.GetComponent<Image>();
                tintImg.sprite = UiWhiteSprite();
                tintImg.type = Image.Type.Simple;
                tintImg.color = LevelSelectCardTintColor(def, available);
                tintImg.raycastTarget = false;
                tintImg.maskable = true;
            }
            else
            {
                cardImage.preserveAspect = false;
                cardImage.sprite = null;
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
            // Full-card vignette — base dim leans toward each minigame’s CardColor so modes read as different “codes.”
            float overlayAlpha = hasThumb ? 0.56f : 0.4f;
            Color dimTinted = LevelSelectCardOverlayTintedRgb(def, available, hasThumb);
            float a = available ? overlayAlpha : 0.74f;
            overlayImage.color = new Color(dimTinted.r, dimTinted.g, dimTinted.b, a);
            overlayImage.raycastTarget = false;

            var scanGo = new GameObject("ScanlineVfx", typeof(RectTransform), typeof(LevelSelectCardScanlineVfx));
            scanGo.transform.SetParent(cardGO.transform, false);
            scanGo.transform.SetSiblingIndex(overlayGO.transform.GetSiblingIndex() + 1);
            var scanRT = scanGo.GetComponent<RectTransform>();
            scanRT.anchorMin = Vector2.zero;
            scanRT.anchorMax = Vector2.one;
            scanRT.offsetMin = Vector2.zero;
            scanRT.offsetMax = Vector2.zero;
            scanGo.GetComponent<LevelSelectCardScanlineVfx>().Configure(UiWhiteSprite());

            AddLevelSelectCardBorder(cardGO.transform, scanGo.transform);

            Color titleColor = available ? MenuVisualTheme.CardTitle : new Color(0.5f, 0.5f, 0.5f);
            Color descColor = available ? MenuVisualTheme.CardDescription : new Color(0.4f, 0.4f, 0.4f);
            // Breath pattern: no band — high-contrast amber on hover (see CardHoverEffect).
            Color patternColor = available
                ? MenuVisualTheme.BreathPatternHover
                : MenuVisualTheme.BreathPatternLocked;

            float titleSize = featured ? 31f : 23.5f;
            float descSize = featured ? 17.5f : 13.5f;
            // Breath pattern: top strip, smaller type, fades in on hover (see CardHoverEffect).
            float patternHoverSize = featured ? 15f : 12f;

            // Title + description shifted down for a more centered block; top of card stays clear for art.
            AddStrokedLabelWithBand(cardGO.transform, def.DisplayName.ToUpper(), titleSize, FontStyles.Bold,
                titleColor, new Vector2(0, 0.64f), new Vector2(1, 0.81f), strokeStrong: true, featured,
                autoSize: true);

            AddStrokedLabelWithBand(cardGO.transform, def.Description, descSize, FontStyles.Bold, descColor,
                new Vector2(0, 0.24f), new Vector2(1, 0.62f), strokeStrong: true, featured,
                autoSize: true);

            TextMeshProUGUI patternTMP = null;
            if (available)
            {
                patternTMP = AddStrokedLabelNoBand(cardGO.transform, def.BreathPattern, patternHoverSize,
                    FontStyles.Bold | FontStyles.Italic, patternColor,
                    new Vector2(0, 0.875f), new Vector2(1, 0.99f), featured);
            }
            else
            {
                // Locked: top strip always visible (not wired to hover fade).
                AddStrokedLabelNoBand(cardGO.transform, "COMING  SOON", 14f,
                    FontStyles.Bold | FontStyles.Italic, MenuVisualTheme.CardLockedBanner,
                    new Vector2(0, 0.875f), new Vector2(1, 0.99f), featured);
            }

            var button = cardGO.GetComponent<Button>();
            button.interactable = available;
            button.transition = Selectable.Transition.None;

            var captured = def;
            button.onClick.AddListener(() => OnGameSelected(captured));

            var hover = cardGO.AddComponent<CardHoverEffect>();
            hover.DisableFillTintFromButton();
            hover.SetInteractable(available);
            hover.SetHoverLabel(available ? patternTMP : null);

            _cardObjects.Add(cardGO);
            _cardDefs.Add(def);
            return cardGO;
        }

        /// <summary>Accent frame above the dim layer, below text (clipped with card).</summary>
        private static void AddLevelSelectCardBorder(Transform cardRoot, Transform insertAfter)
        {
            const float th = 2.5f;
            var rim = MenuVisualTheme.CardBorder;

            var holder = new GameObject("CardBorder", typeof(RectTransform));
            holder.transform.SetParent(cardRoot, false);
            holder.transform.SetSiblingIndex(insertAfter.GetSiblingIndex() + 1);
            var hrt = holder.GetComponent<RectTransform>();
            hrt.anchorMin = Vector2.zero;
            hrt.anchorMax = Vector2.one;
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;

            void Bar(string name, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(holder.transform, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = aMin;
                rt.anchorMax = aMax;
                rt.offsetMin = oMin;
                rt.offsetMax = oMax;
                var img = go.GetComponent<Image>();
                img.sprite = UiWhiteSprite();
                img.color = rim;
                img.raycastTarget = false;
            }

            Bar("EdgeT", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -th), Vector2.zero);
            Bar("EdgeB", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, th));
            // Side rails inset vertically so corners are owned by top/bottom bars (no double-bright corners).
            Bar("EdgeL", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, th), new Vector2(th, -th));
            Bar("EdgeR", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-th, th), Vector2.zero);
        }

        /// <summary>
        /// Opaque strip behind text only (title/description). Full-card dim is a separate overlay.
        /// </summary>
        private static TextMeshProUGUI AddStrokedLabelWithBand(Transform parent, string text, float fontSize,
            FontStyles style, Color color, Vector2 anchorMin, Vector2 anchorMax, bool strokeStrong, bool featured,
            bool autoSize)
        {
            var row = new GameObject("TextRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin = anchorMin;
            rowRT.anchorMax = anchorMax;
            float edge = featured ? 8f : 6f;
            rowRT.offsetMin = new Vector2(edge, 1f);
            rowRT.offsetMax = new Vector2(-edge, -1f);

            var bandGo = new GameObject("LineBand", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bandGo.transform.SetParent(row.transform, false);
            var bandRT = bandGo.GetComponent<RectTransform>();
            bandRT.anchorMin = Vector2.zero;
            bandRT.anchorMax = Vector2.one;
            bandRT.offsetMin = Vector2.zero;
            bandRT.offsetMax = Vector2.zero;
            var bandImg = bandGo.GetComponent<Image>();
            bandImg.sprite = UiWhiteSprite();
            bandImg.color = MenuVisualTheme.LevelSelectCardTextBand;
            bandImg.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            labelGo.transform.SetParent(row.transform, false);
            var rt = labelGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            float lx = featured ? 8f : 6f;
            float ly = featured ? 4f : 3f;
            rt.offsetMin = new Vector2(lx, ly);
            rt.offsetMax = new Vector2(-lx, -ly);

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = GameFont.SanitizeForPixelFont(text);
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            if (autoSize)
            {
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = featured ? 11.5f : 9.5f;
                tmp.fontSizeMax = fontSize;
            }

            float outline = strokeStrong ? 0.35f : 0.22f;
            var ol = labelGo.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.98f);
            ol.useGraphicAlpha = true;
            ol.effectDistance = new Vector2(outline, -outline);

            var sh = labelGo.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.78f);
            sh.useGraphicAlpha = true;
            sh.effectDistance = new Vector2(2.2f, -2.2f);

            return tmp;
        }

        /// <summary>
        /// Breath pattern / footer line: sits on the global card dim only — no extra band behind it.
        /// </summary>
        private static TextMeshProUGUI AddStrokedLabelNoBand(Transform parent, string text, float fontSize,
            FontStyles style, Color color, Vector2 anchorMin, Vector2 anchorMax, bool featured)
        {
            var row = new GameObject("PatternRow", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rowRT = row.GetComponent<RectTransform>();
            rowRT.anchorMin = anchorMin;
            rowRT.anchorMax = anchorMax;
            float edge = featured ? 8f : 6f;
            rowRT.offsetMin = new Vector2(edge, 0f);
            rowRT.offsetMax = new Vector2(-edge, 0f);

            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            labelGo.transform.SetParent(row.transform, false);
            var rt = labelGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(4f, 2f);
            rt.offsetMax = new Vector2(-4f, -2f);

            var tmp = labelGo.AddComponent<TextMeshProUGUI>();
            tmp.text = GameFont.SanitizeForPixelFont(text);
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            tmp.enableAutoSizing = true;
            float minAuto = featured ? 11.5f : 9.5f;
            if (fontSize < 12f)
                minAuto = featured ? 9f : 7.5f;
            tmp.fontSizeMin = minAuto;
            tmp.fontSizeMax = fontSize;

            var ol = labelGo.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.98f);
            ol.useGraphicAlpha = true;
            ol.effectDistance = new Vector2(0.38f, -0.38f);

            var sh = labelGo.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.76f);
            sh.useGraphicAlpha = true;
            sh.effectDistance = new Vector2(2f, -2f);

            return tmp;
        }

        // -------------------------------------------------------------------
        // Actions
        // -------------------------------------------------------------------

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void BreatheWebGL_ReloadPage();
#endif

        private void OnGameSelected(MinigameDefinition def)
        {
            if (def == null || !def.IsUnlocked) return;

            Debug.Log($"[MainMenu] Game selected: {def.name} (scene: {def.SceneName})");
            MinigameManager.Instance?.SelectMinigame(def);
            if (ScreenFadeCoordinator.Instance != null)
                ScreenFadeCoordinator.Instance.FadeToBlackThenLoadScene(def.SceneName);
            else
                SceneLoader.LoadMinigame(def.SceneName);
        }

        private static void OnQuit()
        {
            Debug.Log("[MainMenu] Quit requested.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            // Application.Quit() tears down the WebGL player in a way that often leaves the tab frozen.
            // Full page reload restores a clean canvas and audio context (itch.io embeds included).
            BreatheWebGL_ReloadPage();
#else
            Application.Quit();
#endif
        }
    }
}
