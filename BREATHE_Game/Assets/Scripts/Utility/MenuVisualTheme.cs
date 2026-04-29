using UnityEngine;

namespace Breathe.Utility
{
    /// <summary>
    /// Menu / level-select / overlays: palettes route through here so chrome matches Settings (forest teal + cream + sage accents).
    /// </summary>
    public static class MenuVisualTheme
    {
        // --- Safe area margins (prevents UI from feeling zoomed in / fullscreen) ---
        /// <summary>Horizontal safe margin as fraction of screen width (0.03 = 3%).</summary>
        public const float SafeMarginHorizontal = 0.025f;
        /// <summary>Vertical safe margin as fraction of screen height (0.03 = 3%).</summary>
        public const float SafeMarginVertical = 0.025f;

        /// <summary>Optional extra inward anchor squeeze for Level Select vs other submenu panels (~1.2% each axis).</summary>
        public const float LevelSelectScreenEdgeExtra = 0.012f;

        // --- Full-screen canvas tint (Canvas_MainMenuUI): brighter teal wash over video vs near-black ---
        public static readonly Color MainMenuCanvasBackdrop = new(0.064f, 0.112f, 0.099f, 0.46f);

        // --- Camera clear behind bath+caustics (MenuBathBackdrop) ---
        public static readonly Color MenuCameraClear = new(0.045f, 0.095f, 0.11f, 1f);

        /// <summary>Settings sheet + pause submenu root — lifted teal slate (readable, not soot-black).</summary>
        public static readonly Color SubmenuPanelBackdrop = new(0.092f, 0.146f, 0.134f, 0.904f);

        /// <summary>Level select outer sheet — near-solid teal so bath art doesn’t bleed through.</summary>
        public static readonly Color LevelSelectPanelBackdrop = new(0.086f, 0.148f, 0.136f, 0.982f);

        /// <summary>Credits full-height panel (same tonal family; slightly more opaque vs bath bleed-through).</summary>
        public static readonly Color CreditsSubPanelTint = new(0.075f, 0.129f, 0.114f, 1f);

        // --- Home screen (TMP in scene); nav rows use SETTINGS-style bordered buttons ---
        public static readonly Color HomeTitle = new(1f, 0.94f, 0.84f, 1f);
        /// <summary>Line under the main wordmark (e.g. tagline) — slightly softer than the title for hierarchy.</summary>
        public static readonly Color HomeTitleSubtitle = new(0.92f, 0.88f, 0.80f, 1f);
        /// <remarks>Prefer <see cref="ChromeHeader"/> for nav/button labels matching settings.</remarks>
        public static readonly Color HomeNavLabel = new(0.97f, 0.92f, 0.85f, 1f);
        /// <summary>Plate behind wordmark — rich sea-green, fully opaque for clean readability over video.</summary>
        public static readonly Color HomeTitleTextBackdrop = new(0.08f, 0.22f, 0.18f, 1f);
        /// <summary>Plate behind the nav column — deep sea-green, fully opaque for separation from title band.</summary>
        public static readonly Color HomeNavStackBackdrop = new(0.065f, 0.19f, 0.155f, 1f);
        /// <summary>Home main-menu nav button fill inside cream outline — black vs green plates.</summary>
        public static readonly Color HomeNavButtonIdleFill = new(0.03f, 0.032f, 0.036f, 1f);

        /// <summary>Matte frame around menu video plane — teal rim keyed to boosted menu contrast.</summary>
        public static readonly Color MenuVideoPlateBorder = new(0.36f, 0.54f, 0.49f, 1f);

        // --- Level select cards (chrome matches Settings rims; art still themed per MinigameDefinition) ---
        public static readonly Color CardTitle = new(0.98f, 0.96f, 0.93f, 1f);
        public static readonly Color CardDescription = new(0.90f, 0.86f, 0.80f, 1f);
        /// <summary>Inset rim — muted teal, same family as <see cref="ChromeDivider"/>.</summary>
        public static readonly Color CardBorder = new(0.34f, 0.43f, 0.385f, 0.82f);
        /// <summary>Opaque strip behind stroked card title/description (readable on busy thumbs).</summary>
        public static readonly Color LevelSelectCardTextBand = new(0.05f, 0.065f, 0.058f, 0.96f);
        /// <summary>Full-card vignette tint (multiply with overlay alpha — not RGB black).</summary>
        public static readonly Color LevelSelectCardDimBase = new(0.048f, 0.074f, 0.065f);
        /// <summary>Breath pattern line — fades in on hover; high contrast on dim overlay.</summary>
        public static readonly Color BreathPatternHover = new(0.99f, 0.74f, 0.28f, 1f);
        public static readonly Color BreathPatternLocked = new(0.52f, 0.46f, 0.43f, 1f);
        public static readonly Color CardLockedBanner = new(0.96f, 0.82f, 0.38f, 1f);

        // --- Settings + How to Play (runtime TMP / Image) — align with main menu: deep forest green + cream chrome ---
        public static readonly Color ChromeHeader = new(0.98f, 0.91f, 0.80f, 1f);
        public static readonly Color ChromeLabel = new(0.90f, 0.86f, 0.80f, 1f);
        public static readonly Color ChromeDivider = new(0.28f, 0.36f, 0.32f, 0.9f);
        public static readonly Color SliderTrack = new(0.12f, 0.18f, 0.16f, 1f);
        public static readonly Color SliderFill = new(0.42f, 0.58f, 0.48f, 1f);
        public static readonly Color SliderHandle = new(0.95f, 0.90f, 0.84f, 1f);
        public static readonly Color ButtonBase = new(0.14f, 0.22f, 0.19f, 1f);
        /// <summary>Hover/pressed fill for bordered menu buttons — teal-cyan “lit” lift vs <see cref="ButtonBase"/> (not muddy forest green).</summary>
        public static readonly Color ButtonHighlight = new(0.24f, 0.46f, 0.50f, 1f);
        public static readonly Color PanelBorder = new(0.90f, 0.86f, 0.78f, 1f);
        /// <summary>How to Play inner fill — teal slate, brighter than soot.</summary>
        public static readonly Color HowToPlayFill = new(0.082f, 0.128f, 0.112f, 1f);
        public static readonly Color SettingsContentFill = new(0.076f, 0.118f, 0.104f, 1f);

        // --- Race result overlay (OnGUI) ---
        public static readonly Color OverlayDim = new(0f, 0f, 0f, 0.78f);
        public static readonly Color ResultPanelBg = new(0.11f, 0.09f, 0.15f, 1f);
        public static readonly Color ResultHeroSub = new(0.94f, 0.88f, 0.80f, 1f);
        /// <summary>Primary stat labels (e.g. constellation names) - soft gold for visibility.</summary>
        public static readonly Color ResultPrimaryLabel = new(0.95f, 0.85f, 0.55f, 1f);
        /// <summary>Secondary stat values - bright enough to stand out.</summary>
        public static readonly Color ResultSecondaryValue = new(0.96f, 0.94f, 0.90f, 1f);
        /// <summary>Secondary stat labels (AVG INTENSITY, BREATHS, etc.) - warm gold for emphasis.</summary>
        public static readonly Color ResultSecondaryLabel = new(0.92f, 0.78f, 0.45f, 1f);
        /// <summary>Personal-best announcement under YOUR RESULTS (high-visibility red).</summary>
        public static readonly Color ResultFeedback = new(0.92f, 0.28f, 0.34f, 1f);
        public static readonly Color ResultQuote = new(0.66f, 0.80f, 0.66f, 1f);
        /// <summary>Section headers (YOUR RESULTS, SESSION DETAILS) - vibrant cyan-teal for visual separation.</summary>
        public static readonly Color ResultSectionHeader = new(0.45f, 0.82f, 0.88f, 1f);
        public static readonly Color ResultButtonPrimary = new(0.62f, 0.38f, 0.26f, 1f);
        public static readonly Color ResultButtonPrimaryHover = new(0.72f, 0.46f, 0.32f, 1f);
        public static readonly Color ResultButtonSecondary = new(0.16f, 0.12f, 0.13f, 1f);
        public static readonly Color ResultButtonSecondaryHover = new(0.24f, 0.19f, 0.20f, 1f);
        public static readonly Color ResultSecondaryBtnText = new(0.82f, 0.76f, 0.70f, 1f);
        public static readonly Color ResultAccentFallback = new(0.62f, 0.38f, 0.26f, 1f);

        // --- In-minigame tutorial popup (OnGUI) ---
        public static readonly Color TutorialOverlayDim = new(0f, 0f, 0f, 0.9f);
        public static readonly Color TutorialPanelBg = new(0.10f, 0.08f, 0.14f, 0.98f);
        public static readonly Color TutorialTitle = new(0.98f, 0.78f, 0.52f, 1f);
        public static readonly Color TutorialTip = new(0.66f, 0.80f, 0.68f, 1f);
        public static readonly Color TutorialInputHeader = new(0.92f, 0.80f, 0.68f, 1f);
        public static readonly Color TutorialCheckboxLabel = new(0.90f, 0.86f, 0.80f, 1f);
        public static readonly Color TutorialCheckboxBg = new(0.24f, 0.18f, 0.30f, 1f);
        public static readonly Color TutorialCheckboxBgHover = new(0.34f, 0.26f, 0.40f, 1f);
        public static readonly Color TutorialCheckboxBgActive = new(0.17f, 0.13f, 0.24f, 1f);
        public static readonly Color TutorialCheckboxChecked = new(0.62f, 0.38f, 0.26f, 1f);
        public static readonly Color TutorialCheckboxCheckedHover = new(0.72f, 0.46f, 0.32f, 1f);
        public static readonly Color TutorialCheckboxCheckedActive = new(0.52f, 0.32f, 0.22f, 1f);
    }
}
