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

        // Draws an IMGUI label with a black outline for readability.
        // Call this instead of GUI.Label for any text that needs contrast.
        public static void OutlinedLabel(Rect rect, string text, GUIStyle style, int thickness = 1)
        {
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
