using UnityEngine;

namespace Breathe.Utility
{
    /// <summary>
    /// Same sin-based caustics field as <see cref="Breathe.Gameplay.BathBackdropBuilder.BathCausticsAnimator"/>, for UI RawImage or world sprites.
    /// </summary>
    public static class CausticsTextureFill
    {
        /// <param name="intensity">Multiplies the per-pixel alpha cap (menu bath ~1f–1.5f; card hover can use 3f+).</param>
        /// <param name="verticalFadeMin">Bottom of card can be slightly dimmer (0.35f matches bath).</param>
        public static void Fill(Texture2D tex, float phase, float intensity, Color highlight,
            float verticalFadeMin = 0.35f)
        {
            float aCap = 0.22f * Mathf.Max(0.05f, intensity);
            int w = tex.width;
            int h = tex.height;
            float hr = highlight.r;
            float hg = highlight.g;
            float hb = highlight.b;
            for (int y = 0; y < h; y++)
            {
                float ny = y / (float)h;
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    float c1 = Mathf.Sin(nx * 8f + phase) * Mathf.Sin(ny * 6f + phase * 1.2f);
                    float c2 = Mathf.Sin((nx + ny) * 5f + phase * 0.8f);
                    float c3 = Mathf.Sin(nx * 14f - ny * 10f + phase * 1.5f);
                    float c4 = Mathf.Sin(nx * 3.2f + ny * 4.1f + phase * 0.55f);
                    float v = Mathf.Clamp01(c1 * 0.30f + c2 * 0.30f + c3 * 0.28f + c4 * 0.12f + 0.5f);
                    float a = Mathf.Lerp(0f, aCap, v);
                    a *= Mathf.Lerp(verticalFadeMin, 1f, ny);
                    tex.SetPixel(x, y, new Color(hr, hg, hb, a));
                }
            }
        }
    }
}
