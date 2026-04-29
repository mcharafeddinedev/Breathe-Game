using System.Text.RegularExpressions;
using UnityEngine;

namespace Breathe.Utility
{
    // Loads the project's custom IMGUI font from Resources at runtime.
    // All OnGUI-based overlays call GameFont.Get() to stay in sync.
    // Place the TTF in Assets/Resources/ and set the name below.
    public static class GameFont
    {
        private const string FontResourceName = "ARCADECLASSIC";
        private static Font _cached;
        private static bool _attempted;

        public static Font Get()
        {
            if (_cached != null) return _cached;
            if (_attempted) return null;

            _attempted = true;
            _cached = Resources.Load<Font>(FontResourceName);

            if (_cached == null)
                Debug.LogWarning($"[GameFont] Could not load font '{FontResourceName}' from Resources.");

            return _cached;
        }

        /// <summary>
        /// ARCADECLASSIC / WebGL dynamic fonts often omit glyphs for U+002B PLUS, U+002F SOLIDUS,
        /// and U+007C VERTICAL LINE. Substitute so HUD and popups stay readable in browser builds.
        /// </summary>
        public static string SanitizeForPixelFont(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace("+", "PLUS ");
            // Do not break TMP/HTML closing tags (e.g. </b>, </i>) — '/' was turning </b> into "< b>".
            text = Regex.Replace(text, @"(?<!<)\/", " ");
            return text.Replace('|', ' ');
        }

        /// <summary>
        /// Whole seconds for HUD and results. Fractional glyphs (U+002E, U+0027, etc.) often fail on WebGL with ARCADECLASSIC.
        /// </summary>
        public static string FormatHudSecondsWhole(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            return $"{Mathf.FloorToInt(seconds + 0.0001f)} S";
        }

        /// <summary>
        /// Countdown HUD: whole seconds remaining (ceiling), e.g. 0.2s left shows as 1 S.
        /// </summary>
        public static string FormatHudCountdownSecondsWhole(float secondsRemaining)
        {
            if (secondsRemaining < 0f) secondsRemaining = 0f;
            return $"{Mathf.CeilToInt(secondsRemaining)} S";
        }

        /// <summary>
        /// HUD counters — wide gaps around OF (Skydive / Stone Skip / Bubbles in-round HUD).
        /// </summary>
        public static string FormatHudCountOfTotal(int current, int total)
        {
            return $"{current}    OF    {total}";
        }

        /// <summary>
        /// End-of-run results overlays — compact "n OF total" so columns stay readable.
        /// </summary>
        public static string FormatResultsCountOfTotal(int current, int total)
        {
            return $"{current} OF {total}";
        }

        static readonly Regex ParenQuotedPronunciation = new Regex(@"\(\s*""([^""]+)""\s*\)", RegexOptions.Compiled);
        static readonly Regex StraightDoubleQuotedPhrase = new Regex(@"""([^""]+)""", RegexOptions.Compiled);

        /// <summary>
        /// ARCADECLASSIC often has no glyphs for parentheses or ASCII quotes. Rewrites ("PHON") and remaining
        /// "PHON" phrases into  [  PHON  ]  — square brackets usually rasterize on WebGL where () do not.
        /// </summary>
        public static string ExpandPronunciationHintsForPixelFont(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace('\u201c', '"').Replace('\u201d', '"');
            text = text.Replace('\u2014', ' ').Replace('\u2013', ' ');
            text = ParenQuotedPronunciation.Replace(text, "  [  $1  ]  ");
            text = StraightDoubleQuotedPhrase.Replace(text, "  [  $1  ]  ");
            text = text.Replace('(', ' ').Replace(')', ' ');
            return SanitizeForPixelFont(text);
        }

        // Draws an IMGUI label with a black outline for readability.
        // Call this instead of GUI.Label for any text that needs contrast.
        public static void OutlinedLabel(Rect rect, string text, GUIStyle style, int thickness = 1)
        {
            text = SanitizeForPixelFont(text);
            Color origNormal = style.normal.textColor;
            Color origHover = style.hover.textColor;
            Color origActive = style.active.textColor;
            Color origFocused = style.focused.textColor;

            Color outline = new Color(0f, 0f, 0f, origNormal.a);
            style.normal.textColor = outline;
            style.hover.textColor = outline;
            style.active.textColor = outline;
            style.focused.textColor = outline;

            for (int x = -thickness; x <= thickness; x++)
                for (int y = -thickness; y <= thickness; y++)
                    if (x != 0 || y != 0)
                        GUI.Label(new Rect(rect.x + x, rect.y + y, rect.width, rect.height), text, style);

            style.normal.textColor = origNormal;
            style.hover.textColor = origHover;
            style.active.textColor = origActive;
            style.focused.textColor = origFocused;
            GUI.Label(rect, text, style);
        }
    }
}
