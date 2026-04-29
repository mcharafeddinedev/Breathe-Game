using UnityEngine;
using UnityEngine.UI;
using Breathe.Utility;

namespace Breathe.UI
{
    /// <summary>
    /// Shared UI primitives so Main Menu / Level Select match <see cref="SettingsManager"/> chrome (cream border ring + inset fill panel).
    /// </summary>
    public static class MenuUiChrome
    {
        public const float PanelInsetBorderPx = 2f;

        /// <remarks>Aligned with SettingsManager prior hard-coded value.</remarks>
        const float BtnBorderPx = 2f;

        /// <summary>HOW TO PLAY / sliders / BACK: cream outline, dark teal fill matching settings.</summary>
        public static void StyleButtonLikeSettings(GameObject go) => StyleButtonLikeSettings(go, null);

        /// <param name="idleFillOverride">When set, used for normal/selected/disabled fill (e.g. main-menu nav vs settings).</param>
        public static void StyleButtonLikeSettings(GameObject go, Color? idleFillOverride)
        {
            var outer = go.GetComponent<Image>();
            if (outer == null) return;
            outer.color = MenuVisualTheme.PanelBorder;
            outer.raycastTarget = false;

            Image fillImg;
            Transform fillT = go.transform.Find("Fill");
            if (fillT == null)
            {
                var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                fillGo.transform.SetParent(go.transform, false);
                var fillRt = fillGo.GetComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = Vector2.one;
                fillRt.offsetMin = new Vector2(BtnBorderPx, BtnBorderPx);
                fillRt.offsetMax = new Vector2(-BtnBorderPx, -BtnBorderPx);
                fillImg = fillGo.GetComponent<Image>();
                fillImg.raycastTarget = true;
                fillGo.transform.SetAsFirstSibling();
            }
            else
                fillImg = fillT.GetComponent<Image>();

            Color norm = idleFillOverride ?? MenuVisualTheme.ButtonBase;
            fillImg.color = norm;

            var btn = go.GetComponent<Button>();
            if (btn == null) return;
            btn.targetGraphic = fillImg;
            btn.transition = Selectable.Transition.ColorTint;
            var c = btn.colors;
            Color hi = MenuVisualTheme.ButtonHighlight;
            c.normalColor = norm;
            c.highlightedColor = hi;
            c.pressedColor = hi;
            c.selectedColor = norm;
            c.disabledColor = norm;
            c.fadeDuration = 0.08f;
            btn.colors = c;
        }

        /// <summary>
        /// Hover lift + subtle press bounce (matches Settings cycle buttons / HOW TO PLAY). Always attach to the same
        /// <see cref="GameObject"/> as <see cref="Button"/> — after <see cref="StyleButtonLikeSettings(GameObject, Color?)"/> — never only on the inner <c>Fill</c> graphic.
        /// </summary>
        public static void AttachStandardButtonHover(GameObject buttonRoot, bool interactable = true)
        {
            if (buttonRoot == null) return;
            var hover = buttonRoot.GetComponent<CardHoverEffect>();
            if (hover == null)
                hover = buttonRoot.AddComponent<CardHoverEffect>();
            hover.SetInteractable(interactable);
            // Let Unity's built-in ColorTint handle fill color; CardHoverEffect just does scale.
        }

        /// <summary>Same construction as Settings "Content" block — beige thin frame + inward dark charcoal fill.</summary>
        public static void AddInsetPanelFrame(RectTransform parent)
        {
            AddInsetPanelFrame(parent, MenuVisualTheme.PanelBorder, MenuVisualTheme.SettingsContentFill, PanelInsetBorderPx);
        }

        /// <inheritdoc cref="AddInsetPanelFrame(RectTransform)" />
        public static void AddInsetPanelFrame(RectTransform parent, Color borderColor, Color fillColor, float borderPx)
        {
            Transform existing = parent.Find("PanelFrame");
            if (existing != null)
                return;

            var frameGo = new GameObject("PanelFrame",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameGo.transform.SetParent(parent, false);
            var fr = frameGo.GetComponent<RectTransform>();
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = Vector2.zero;
            fr.offsetMax = Vector2.zero;
            var outer = frameGo.GetComponent<Image>();
            outer.color = borderColor;
            outer.raycastTarget = false;

            var fillGo = new GameObject("InnerFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            fillGo.transform.SetParent(frameGo.transform, false);
            var fillRt = fillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(borderPx, borderPx);
            fillRt.offsetMax = new Vector2(-borderPx, -borderPx);
            fillGo.GetComponent<Image>().color = fillColor;
            fillGo.GetComponent<Image>().raycastTarget = false;

            frameGo.transform.SetAsFirstSibling();
        }
    }
}
