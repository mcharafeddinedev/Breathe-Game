using Breathe.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace Breathe.UI
{
    /// <summary>
    /// Level-select card hover: bath-style animated caustics (same field as the menu) plus vertical scan strips.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LevelSelectCardScanlineVfx : MonoBehaviour
    {
        RawImage _caustics;
        Texture2D _causticsTex;
        Image[] _lines;
        RectTransform _rt;
        CardHoverEffect _hover;
        float _alpha;
        float _noiseSeed;
        float _causticsPhase;
        float _causticsTimer;

        // Match line strip fade; caustics layer uses normalized alpha (see Update) so the water read stays strong
        const float MasterTargetAlpha = 0.12f;
        const float LineAlphaMul = 0.75f;
        const float FadeSpeed = 7f;

        const int LineCount = 6;
        const int CausticsSize = 88;
        /// <summary>Menu bath is ~1.4; this is much stronger for a single UI layer.</summary>
        const float CardCausticsIntensity = 3.85f;
        const float CardCausticsPhaseScale = 1.9f;
        const float CausticsStepSeconds = 0.022f;

        static readonly Color CausticsHighlight = new(0.78f, 0.96f, 1f);

        const float LineWidthBase = 2.1f;

        public void Configure(Sprite whiteSprite)
        {
            _rt = GetComponent<RectTransform>();
            _noiseSeed = Random.Range(3f, 400f);

            _causticsTex = new Texture2D(CausticsSize, CausticsSize, TextureFormat.RGBA32, false);
            _causticsTex.filterMode = FilterMode.Bilinear;
            _causticsTex.wrapMode = TextureWrapMode.Clamp;

            var cGo = new GameObject("Caustics", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            cGo.transform.SetParent(transform, false);
            var crt = cGo.GetComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            _caustics = cGo.GetComponent<RawImage>();
            _caustics.texture = _causticsTex;
            _caustics.raycastTarget = false;
            _caustics.maskable = true;
            _caustics.color = new Color(1f, 1f, 1f, 0f);

            CausticsTextureFill.Fill(_causticsTex, 0f, CardCausticsIntensity, CausticsHighlight);
            _causticsTex.Apply(false);

            // Lines on top of caustics
            _lines = new Image[LineCount];
            for (int i = 0; i < LineCount; i++)
            {
                var go = new GameObject($"VScan{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(transform, false);
                var lrt = go.GetComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0f, 0f);
                lrt.anchorMax = new Vector2(0f, 1f);
                lrt.pivot = new Vector2(0f, 0.5f);
                lrt.sizeDelta = new Vector2(LineWidthBase, 0f);
                lrt.anchoredPosition = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.sprite = whiteSprite;
                img.type = Image.Type.Simple;
                img.raycastTarget = false;
                img.maskable = true;
                img.color = new Color(0.9f, 0.95f, 1f, 0f);
                _lines[i] = img;
            }
        }

        void OnDestroy()
        {
            if (_causticsTex != null)
            {
                Destroy(_causticsTex);
            }
        }

        void Start()
        {
            _hover = GetComponentInParent<CardHoverEffect>();
        }

        void Update()
        {
            if (_rt == null) return;
            bool on = _hover != null && _hover.IsHovering;
            _alpha = Mathf.Lerp(_alpha, on ? MasterTargetAlpha : 0f, Time.unscaledDeltaTime * FadeSpeed);
            if (_alpha < 0.0005f && !on) return;

            float w = _rt.rect.width;
            float h = _rt.rect.height;
            if (w < 2f || h < 2f) return;
            float t = Time.unscaledTime;

            if (_caustics != null)
            {
                // Normalize: _alpha and lines share the same small master target; caustics needs full 0..1 on the image to read
                var cc = _caustics.color;
                cc.r = cc.g = cc.b = 1f;
                cc.a = Mathf.Clamp01(_alpha * (1f / MasterTargetAlpha));
                _caustics.color = cc;
            }

            if (_causticsTex != null && on && _alpha > 0.01f)
            {
                _causticsTimer += Time.unscaledDeltaTime;
                if (_causticsTimer >= CausticsStepSeconds)
                {
                    _causticsTimer -= CausticsStepSeconds;
                    _causticsPhase += 0.018f * CardCausticsPhaseScale;
                    CausticsTextureFill.Fill(_causticsTex, _causticsPhase, CardCausticsIntensity, CausticsHighlight);
                    _causticsTex.Apply(false);
                }
            }

            if (_lines != null)
            {
                for (int i = 0; i < _lines.Length; i++)
                {
                    var lrt = _lines[i].rectTransform;
                    float speed = 22f + i * 3.2f;
                    float span = w + 18f;
                    float sweepX = Mathf.Repeat(t * speed + i * 23.1f + _noiseSeed * 0.1f, span) - 4f;
                    float nx = _noiseSeed * 0.02f + t * 0.42f + i * 0.33f;
                    float wobbleX = (Mathf.PerlinNoise(nx, i * 0.74f) - 0.5f) * (w * 0.05f + 3f);
                    float wobbleY = (Mathf.PerlinNoise(nx * 0.6f, i * 0.25f) - 0.5f) * (h * 0.04f);
                    lrt.anchoredPosition = new Vector2(sweepX + wobbleX, wobbleY);
                    float wMul = 0.55f + 0.55f * Mathf.PerlinNoise(t * 1.1f + i * 0.2f, _noiseSeed * 0.01f);
                    lrt.sizeDelta = new Vector2(Mathf.Max(1f, LineWidthBase * wMul), 0f);
                    float gain = 0.28f + 0.72f * Mathf.PerlinNoise(t * 0.85f, i * 0.48f + _noiseSeed);
                    var c = _lines[i].color;
                    c.r = 0.9f;
                    c.g = 0.95f;
                    c.b = 1f;
                    c.a = _alpha * LineAlphaMul * gain * 0.95f;
                    _lines[i].color = c;
                }
            }
        }
    }
}
