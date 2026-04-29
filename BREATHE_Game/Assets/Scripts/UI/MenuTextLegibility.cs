using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Breathe.UI
{
    /// <summary>
    /// Black outline + drop shadow for TextMeshProUGUI on variable backgrounds (e.g. menu video + caustics).
    /// Skips text under <see cref="Button"/> (labels sit on solid button fills). Idempotent: leaves labels that already have an <see cref="Outline"/>.
    /// </summary>
    public static class MenuTextLegibility
    {
        static readonly Color OutlineColor = new(0f, 0f, 0f, 0.95f);
        static readonly Color ShadowColor = new(0f, 0f, 0f, 0.78f);

        /// <param name="largeTitle">Stronger offsets for the main menu wordmark / big headers.</param>
        public static void TryApplyOverlayOutlineToTmp(TextMeshProUGUI tmp, bool largeTitle = false)
        {
            if (tmp == null) return;
            var go = tmp.gameObject;
            if (go.GetComponent<Outline>() != null) return;
            if (go.GetComponentInParent<Button>(true) != null) return;

            float d = largeTitle ? 0.5f : 0.32f;
            var ol = go.AddComponent<Outline>();
            ol.effectColor = OutlineColor;
            ol.useGraphicAlpha = true;
            ol.effectDistance = new Vector2(d, -d);

            float sd = largeTitle ? 2.6f : 2f;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = ShadowColor;
            sh.useGraphicAlpha = true;
            sh.effectDistance = new Vector2(sd, -sd);
        }

        /// <summary>Applies to all TMP in <paramref name="root"/>'s hierarchy that are not on/under a button and do not already have an outline.</summary>
        public static void TryApplyToPanelNonButtonText(Transform root)
        {
            if (root == null) return;
            var list = root.GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < list.Length; i++)
                TryApplyOverlayOutlineToTmp(list[i], largeTitle: false);
        }
    }
}
