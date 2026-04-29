using UnityEngine;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    /// <summary>Shared underwater bath + caustics stack (Bubbles minigame, main menu, etc.).</summary>
    public static class BathBackdropBuilder
    {
        public struct Options
        {
            /// <summary>Extra motion in the Bubbles minigame: second caustics layer + ambient floaters.</summary>
            public bool AmbientActivity;
            /// <summary>Second caustics layer (parallax) without fish/bubbles — e.g. main menu.</summary>
            public bool DualCausticsOnly;
            /// <summary>Multiplies caustics phase speed (1 = menu-like, &gt;1 = snappier ripples).</summary>
            public float CausticsPhaseScale;
            /// <summary>Multiplies caustics highlight strength (alpha cap in <see cref="BathCausticsAnimator"/>).</summary>
            public float CausticsIntensity;
            /// <summary>Dual-caustics only: sprite alpha tint on second (parallax) layer (typically ~0.48 gameplay, higher on main menu).</summary>
            public float CausticsParallaxTintAlpha;
            /// <summary>Main menu: greener/teal ocean water + caustics (gameplay keeps classic blue-cyan bath).</summary>
            public bool VibrantTealOcean;
            /// <summary>Main menu only: soft mother-of-pearl highlights + dual-layer chromatic shift instead of lime-teal caustics.</summary>
            public bool PearlescentCaustics;
        }

        public static readonly Options MenuDefault = new()
        {
            AmbientActivity = false,
            DualCausticsOnly = true,
            CausticsPhaseScale = 1.48f,
            CausticsIntensity = 1.58f,
            CausticsParallaxTintAlpha = 0.66f,
            VibrantTealOcean = true,
            PearlescentCaustics = true
        };

        public static readonly Options GameplayDefault = new()
        {
            AmbientActivity = true,
            DualCausticsOnly = false,
            CausticsPhaseScale = 1.45f,
            CausticsIntensity = 1f,
            CausticsParallaxTintAlpha = 0.48f,
            VibrantTealOcean = false,
            PearlescentCaustics = false
        };

        /// <summary>Builds the full backdrop hierarchy; sets <paramref name="root"/> to the parent object.</summary>
        public static void Build(Camera cam, out GameObject root, Options options)
        {
            float bgZ = cam != null ? cam.transform.position.z + 18f : 10f;
            Vector3 center = cam != null
                ? new Vector3(cam.transform.position.x, cam.transform.position.y + 0.5f, bgZ)
                : new Vector3(0f, 1f, bgZ);

            root = new GameObject("BathBackdrop");
            root.transform.position = center;

            // --- Deep water ---
            Color deepBottom = options.VibrantTealOcean
                ? new Color(0.025f, 0.14f, 0.155f)
                : new Color(0.04f, 0.14f, 0.22f);
            Color deepTop = options.VibrantTealOcean
                ? new Color(0.11f, 0.52f, 0.47f)
                : new Color(0.12f, 0.48f, 0.58f);
            Color causticsHighlight;
            if (options.PearlescentCaustics)
            {
                // Mother-of-pearl shimmer: warm shell + faint rose/lavender (not lime/teal underwater spotlight).
                causticsHighlight = new Color(0.91f, 0.885f, 0.942f);
            }
            else if (options.VibrantTealOcean)
                causticsHighlight = new Color(0.52f, 0.90f, 0.76f);
            else
                causticsHighlight = new Color(0.78f, 0.96f, 1f);

            var deepGo = new GameObject("DeepWater");
            deepGo.transform.SetParent(root.transform, false);
            var deepSr = deepGo.AddComponent<SpriteRenderer>();
            var deepTex = GenerateVerticalGradientTexture(12, 160, deepBottom, deepTop);
            deepSr.sprite = Sprite.Create(deepTex, new Rect(0, 0, deepTex.width, deepTex.height),
                new Vector2(0.5f, 0.5f), 8f);
            deepSr.sortingOrder = -32;
            deepGo.transform.localScale = new Vector3(36f, 26f, 1f);

            // --- Tub rim ---
            var rimGo = new GameObject("TubRim");
            rimGo.transform.SetParent(root.transform, false);
            var rimSr = rimGo.AddComponent<SpriteRenderer>();
            var rimTex = GenerateBottomRimTexture(64, 48);
            rimSr.sprite = Sprite.Create(rimTex, new Rect(0, 0, rimTex.width, rimTex.height),
                new Vector2(0.5f, 0f), 8f);
            rimSr.color = options.VibrantTealOcean
                ? new Color(0.025f, 0.12f, 0.11f, 0.85f)
                : new Color(0.03f, 0.12f, 0.16f, 0.85f);
            rimSr.sortingOrder = -30;
            rimGo.transform.localPosition = new Vector3(0f, -6.5f, 0f);
            rimGo.transform.localScale = new Vector3(38f, 10f, 1f);

            // --- Mid blobs ---
            var blobTex = GenerateSoftEllipseTexture(48, 40, 0.35f);

            var blobGo = new GameObject("BathMidBlob");
            blobGo.transform.SetParent(root.transform, false);
            var blobSr = blobGo.AddComponent<SpriteRenderer>();
            blobSr.sprite = Sprite.Create(blobTex, new Rect(0, 0, blobTex.width, blobTex.height),
                new Vector2(0.5f, 0.5f), 8f);
            blobSr.color = options.VibrantTealOcean
                ? new Color(0.055f, 0.30f, 0.26f, 0.45f)
                : new Color(0.08f, 0.22f, 0.28f, 0.45f);
            blobSr.sortingOrder = -28;
            blobGo.transform.localPosition = new Vector3(-5.5f, 2f, 0f);
            blobGo.transform.localScale = new Vector3(14f, 11f, 1f);

            var blob2Go = new GameObject("BathMidBlob2");
            blob2Go.transform.SetParent(root.transform, false);
            var blob2Sr = blob2Go.AddComponent<SpriteRenderer>();
            blob2Sr.sprite = Sprite.Create(blobTex, new Rect(0, 0, blobTex.width, blobTex.height),
                new Vector2(0.5f, 0.5f), 8f);
            blob2Sr.color = options.VibrantTealOcean
                ? new Color(0.045f, 0.26f, 0.22f, 0.35f)
                : new Color(0.07f, 0.20f, 0.26f, 0.35f);
            blob2Sr.sortingOrder = -28;
            blob2Go.transform.localPosition = new Vector3(6f, -1.5f, 0f);
            blob2Go.transform.localScale = new Vector3(10f, 8f, 1f);

            // --- Surface sheen ---
            var sheenGo = new GameObject("SurfaceSheen");
            sheenGo.transform.SetParent(root.transform, false);
            var sheenSr = sheenGo.AddComponent<SpriteRenderer>();
            var sheenTex = GenerateHorizontalSheenTexture(64, 32);
            sheenSr.sprite = Sprite.Create(sheenTex, new Rect(0, 0, sheenTex.width, sheenTex.height),
                new Vector2(0.5f, 0f), 8f);
            sheenSr.color = options.VibrantTealOcean
                ? new Color(0.40f, 0.80f, 0.72f, 0.24f)
                : new Color(0.45f, 0.75f, 0.82f, 0.22f);
            sheenSr.sortingOrder = -26;
            sheenGo.transform.localPosition = new Vector3(0f, 9f, 0f);
            sheenGo.transform.localScale = new Vector3(40f, 8f, 1f);

            if (options.AmbientActivity)
                root.AddComponent<BathAmbientFloatBubbles>().Init(cam);

            // --- Caustics ---
            var causticsGo = new GameObject("CausticsOverlay");
            causticsGo.transform.SetParent(root.transform, false);
            var causticsSr = causticsGo.AddComponent<SpriteRenderer>();
            causticsSr.sortingOrder = -22;
            causticsSr.color = options.PearlescentCaustics
                ? new Color(0.97f, 0.965f, 0.995f, 1f)
                : Color.white;
            causticsGo.transform.localScale = new Vector3(34f, 24f, 1f);
            var caustics = causticsGo.AddComponent<BathCausticsAnimator>();
            bool dualCaustics = options.AmbientActivity || options.DualCausticsOnly;
            caustics.Init(causticsSr, options.CausticsPhaseScale, dualCaustics, options.CausticsIntensity,
                causticsHighlight, options.CausticsParallaxTintAlpha, options.PearlescentCaustics);

            // Fish after caustics in hierarchy; sorting order must be > caustics (-21) or ripples hide them.
            if (options.AmbientActivity)
                root.AddComponent<BathSilhouetteFish>().Init(cam);
        }

        internal static Texture2D GenerateVerticalGradientTexture(int w, int h, Color bottom, Color top)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(bottom, top, (float)y / h);
                for (int x = 0; x < w; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        internal static Texture2D GenerateBottomRimTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)h;
                float alpha = Mathf.SmoothStep(1f, 0f, t);
                for (int x = 0; x < w; x++)
                {
                    float nx = x / (float)w;
                    float curve = Mathf.Sin(nx * Mathf.PI) * 0.15f + 0.85f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha * curve));
                }
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        internal static Texture2D GenerateSoftEllipseTexture(int w, int h, float edgeSoftness)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            float cx = w * 0.5f;
            float cy = h * 0.5f;
            float rx = cx * 0.9f;
            float ry = cy * 0.85f;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = 1f - Mathf.SmoothStep(1f - edgeSoftness, 1f, d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }

        internal static Texture2D GenerateHorizontalSheenTexture(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)h;
                float a = Mathf.SmoothStep(0f, 1f, t);
                a *= 1f - t * 0.4f;
                for (int x = 0; x < w; x++)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return tex;
        }
    }

    /// <summary>Soft animated underwater caustics; one or two texture layers.</summary>
    public sealed class BathCausticsAnimator : MonoBehaviour
    {
        private SpriteRenderer _srA;
        private SpriteRenderer _srB;
        private Texture2D _texA;
        private Texture2D _texB;
        private Sprite _spriteA;
        private Sprite _spriteB;
        private float _phase;
        private float _frameTimer;
        private float _phaseScale = 1f;
        private float _intensity = 1f;
        private bool _dual;
        private float _hiR = 0.78f, _hiG = 0.96f, _hiB = 1f;
        /// <summary>Secondary layer tint (pearlescent: cooler cyan shift vs primary pearl).</summary>
        private float _hiBR = 0.78f, _hiBG = 0.96f, _hiBB = 1f;

        private const int Size = 96;

        public void Init(SpriteRenderer primary, float phaseScale, bool dualLayer, float intensity = 1f,
            Color causticsHighlight = default, float parallaxTintAlpha = 0.48f, bool pearlescentParallax = false)
        {
            _phaseScale = phaseScale;
            _dual = dualLayer;
            _intensity = Mathf.Max(0.1f, intensity);
            if (causticsHighlight == default)
                causticsHighlight = new Color(0.78f, 0.96f, 1f);
            _hiR = causticsHighlight.r;
            _hiG = causticsHighlight.g;
            _hiB = causticsHighlight.b;
            if (pearlescentParallax)
            {
                // Slight spectral split: primary = warm pearl, texture B = cooler opal edge.
                _hiBR = Mathf.Clamp01(_hiR * 0.86f + 0.06f);
                _hiBG = Mathf.Clamp01(_hiG * 0.94f + 0.04f);
                _hiBB = Mathf.Clamp01(_hiB * 1.04f + 0.02f);
            }
            else
            {
                _hiBR = _hiR;
                _hiBG = _hiG;
                _hiBB = _hiB;
            }

            _srA = primary;
            _texA = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            _texA.wrapMode = TextureWrapMode.Clamp;
            _texA.filterMode = FilterMode.Bilinear;
            _spriteA = Sprite.Create(_texA, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 32f);
            _srA.sprite = _spriteA;

            if (!dualLayer) return;

            float bAlpha = Mathf.Clamp(parallaxTintAlpha, 0.15f, 0.92f);

            var goB = new GameObject("CausticsOverlayB");
            goB.transform.SetParent(transform, false);
            _srB = goB.AddComponent<SpriteRenderer>();
            _srB.sortingOrder = primary.sortingOrder + 1;
            if (pearlescentParallax)
            {
                // Sprite tint complements texture B for a lilac→aqua veil over the pearl base.
                _srB.color = new Color(0.80f, 0.87f, 0.94f, bAlpha);
            }
            else
            {
                _srB.color = new Color(
                    Mathf.Clamp01(_hiR * 1.04f),
                    Mathf.Clamp01(_hiG * 1f),
                    Mathf.Clamp01(_hiB * 1.06f),
                    bAlpha);
            }
            goB.transform.localScale = new Vector3(1.09f, 1.06f, 1f);
            goB.transform.localPosition = new Vector3(0.4f, -0.35f, 0f);
            _texB = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            _texB.wrapMode = TextureWrapMode.Clamp;
            _texB.filterMode = FilterMode.Bilinear;
            _spriteB = Sprite.Create(_texB, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 32f);
            _srB.sprite = _spriteB;
        }

        private void Update()
        {
            float interval = _dual ? 0.032f : 0.042f;
            _frameTimer += Time.deltaTime;
            if (_frameTimer < interval) return;
            _frameTimer = 0f;
            _phase += 0.018f * _phaseScale;
            CausticsTextureFill.Fill(_texA, _phase, _intensity, new Color(_hiR, _hiG, _hiB));
            _texA.Apply(false);
            if (_dual && _texB != null)
            {
                CausticsTextureFill.Fill(_texB, _phase * 1.15f + 2.1f, _intensity, new Color(_hiBR, _hiBG, _hiBB));
                _texB.Apply(false);
            }
        }

        private void OnDestroy()
        {
            if (_spriteA != null) Destroy(_spriteA);
            if (_spriteB != null) Destroy(_spriteB);
            if (_texA != null) Destroy(_texA);
            if (_texB != null) Destroy(_texB);
        }
    }

    /// <summary>Slow rising micro-bubbles for extra life in the Bubbles minigame only.</summary>
    public sealed class BathAmbientFloatBubbles : MonoBehaviour
    {
        private static Material _spriteMat;
        private static Texture2D _bubbleTex;

        private struct Floater
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public float Speed;
            public float Wobble;
            public float Phase;
            public float BaseX;
            public float BaseY;
        }

        private Floater[] _floaters;
        private Camera _cam;
        private float _halfH;
        private float _halfW;

        public void Init(Camera cam)
        {
            _cam = cam != null ? cam : Camera.main;
            if (_cam != null && _cam.orthographic)
            {
                _halfH = _cam.orthographicSize;
                _halfW = _halfH * _cam.aspect;
            }
            else
            {
                _halfH = 10f;
                _halfW = 18f;
            }

            EnsureBubbleTex();
            EnsureMaterial();

            const int count = 18;
            _floaters = new Floater[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"AmbientBubble_{i}");
                go.transform.SetParent(transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = _spriteMat;
                sr.sprite = Sprite.Create(_bubbleTex,
                    new Rect(0, 0, _bubbleTex.width, _bubbleTex.height),
                    new Vector2(0.5f, 0.5f), 64f);
                float s = UnityEngine.Random.Range(0.12f, 0.38f);
                go.transform.localScale = new Vector3(s, s, 1f);
                sr.sortingOrder = -25;
                sr.color = new Color(0.75f, 0.92f, 1f, UnityEngine.Random.Range(0.12f, 0.28f));

                float bx = UnityEngine.Random.Range(-_halfW * 0.85f, _halfW * 0.85f);
                float by = UnityEngine.Random.Range(-_halfH * 0.75f, _halfH * 0.85f);
                go.transform.localPosition = new Vector3(bx, by, 0f);

                _floaters[i] = new Floater
                {
                    Tr = go.transform,
                    Sr = sr,
                    Speed = UnityEngine.Random.Range(0.35f, 1.1f),
                    Wobble = UnityEngine.Random.Range(1.2f, 2.8f),
                    Phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                    BaseX = bx,
                    BaseY = by
                };
            }
        }

        private void Update()
        {
            if (_floaters == null) return;
            float dt = Time.deltaTime;
            for (int i = 0; i < _floaters.Length; i++)
            {
                ref var f = ref _floaters[i];
                if (f.Tr == null) continue;

                var p = f.Tr.localPosition;
                p.y += f.Speed * dt;
                f.Phase += dt * f.Wobble;
                p.x = f.BaseX + Mathf.Sin(f.Phase) * 0.35f;

                if (p.y > _halfH * 1.05f)
                {
                    p.y = -_halfH * 0.95f;
                    f.BaseX = UnityEngine.Random.Range(-_halfW * 0.85f, _halfW * 0.85f);
                    p.x = f.BaseX;
                    f.BaseY = p.y;
                    if (f.Sr != null)
                        f.Sr.color = new Color(0.75f, 0.92f, 1f, UnityEngine.Random.Range(0.1f, 0.26f));
                }

                f.Tr.localPosition = p;
            }
        }

        private static void EnsureMaterial()
        {
            if (_spriteMat != null) return;
            _spriteMat = new Material(Shader.Find("Sprites/Default"));
        }

        private static void EnsureBubbleTex()
        {
            if (_bubbleTex != null) return;
            int size = 32;
            _bubbleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c = size * 0.5f;
            float r = c - 1f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = Mathf.Clamp01((r - d) * 0.5f);
                _bubbleTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            _bubbleTex.Apply();
            _bubbleTex.filterMode = FilterMode.Bilinear;
        }
    }

    /// <summary>
    /// Faint background fish silhouettes (Bubbles minigame only): varied scale/speed for depth, horizontal swim + wrap.
    /// </summary>
    public sealed class BathSilhouetteFish : MonoBehaviour
    {
        private struct Silhouette
        {
            public Transform Tr;
            public SpriteRenderer Sr;
            public float Speed;
            public float Dir;
            public float BaseY;
            public float WobblePhase;
            public float WobbleAmp;
        }

        private Silhouette[] _silhouettes;
        private float _halfH;
        private float _halfW;

        private static Material _spriteMat;
        private static Texture2D _fishTex;
        private const int FishTexGeneration = 2;
        private static int _fishTexGen;

        public void Init(Camera cam)
        {
            Camera c = cam != null ? cam : Camera.main;
            if (c != null && c.orthographic)
            {
                _halfH = c.orthographicSize;
                _halfW = _halfH * c.aspect;
            }
            else
            {
                _halfH = 10f;
                _halfW = 18f;
            }

            EnsureFishTex();
            EnsureMaterial();

            const int count = 7;
            _silhouettes = new Silhouette[count];
            float margin = _halfW * 1.35f;

            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"SilhouetteFish_{i}");
                go.transform.SetParent(transform, false);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sharedMaterial = _spriteMat;
                sr.sprite = Sprite.Create(_fishTex,
                    new Rect(0, 0, _fishTex.width, _fishTex.height),
                    new Vector2(0.5f, 0.5f), 64f);
                // Larger = nearer (slightly bolder + faster); smaller = distant haze.
                float depth = UnityEngine.Random.Range(0.2f, 1f);
                float baseScale = Mathf.Lerp(0.28f, 0.82f, depth);
                float dir = UnityEngine.Random.value > 0.5f ? 1f : -1f;
                go.transform.localScale = new Vector3(baseScale * dir, baseScale, 1f);

                // Must sort above dual caustics (-22 / -21) or animated highlights fully obscure dark fish.
                // Stay below wand (3) and gameplay bubbles (5).
                float alpha = Mathf.Lerp(0.16f, 0.4f, depth);
                sr.color = new Color(0.08f, 0.16f, 0.22f, alpha);
                sr.sortingOrder = 0;

                float y = UnityEngine.Random.Range(-_halfH * 0.72f, _halfH * 0.78f);
                float x = UnityEngine.Random.Range(-margin, margin);
                go.transform.localPosition = new Vector3(x, y, 0f);

                _silhouettes[i] = new Silhouette
                {
                    Tr = go.transform,
                    Sr = sr,
                    Speed = Mathf.Lerp(0.16f, 0.52f, depth),
                    Dir = dir,
                    BaseY = y,
                    WobblePhase = UnityEngine.Random.Range(0f, Mathf.PI * 2f),
                    WobbleAmp = UnityEngine.Random.Range(0.08f, 0.28f)
                };
            }
        }

        private void Update()
        {
            if (_silhouettes == null) return;
            float dt = Time.deltaTime;
            float t = Time.time;
            float wrap = _halfW * 1.4f;

            for (int i = 0; i < _silhouettes.Length; i++)
            {
                ref var s = ref _silhouettes[i];
                if (s.Tr == null) continue;

                var p = s.Tr.localPosition;
                p.x += s.Speed * s.Dir * dt;
                p.y = s.BaseY + Mathf.Sin(t * 0.55f + s.WobblePhase) * s.WobbleAmp;

                if (p.x > wrap) p.x = -wrap;
                else if (p.x < -wrap) p.x = wrap;

                s.Tr.localPosition = p;
            }
        }

        private static void EnsureMaterial()
        {
            if (_spriteMat != null) return;
            _spriteMat = new Material(Shader.Find("Sprites/Default"));
        }

        /// <summary>Single connected side-view silhouette: tapered capsule (tail→head) + overlapping dorsal bump.</summary>
        private static void EnsureFishTex()
        {
            if (_fishTex != null && _fishTexGen == FishTexGeneration) return;
            if (_fishTex != null)
            {
                Object.Destroy(_fishTex);
                _fishTex = null;
            }

            int w = 56;
            int h = 28;
            _fishTex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            _fishTexGen = FishTexGeneration;

            // Tail (left) → head (right); radius grows along body so tail is one piece with body.
            Vector2 tail = new Vector2(6.5f, h * 0.5f);
            Vector2 head = new Vector2(w - 6f, h * 0.5f);
            Vector2 ab = head - tail;
            float abLenSq = Vector2.Dot(ab, ab);

            // Dorsal fin — small ellipse overlapping upper mid-body so it reads attached.
            float finCx = w * 0.52f;
            float finCy = h * 0.34f;
            float finRx = w * 0.1f;
            float finRy = h * 0.14f;

            for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                Vector2 p = new Vector2(px, py);
                float u = abLenSq > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - tail, ab) / abLenSq) : 0f;
                Vector2 closest = tail + ab * u;
                float rAlong = Mathf.Lerp(3.1f, 10.2f, u);
                float dCapsule = Vector2.Distance(p, closest) - rAlong;
                // Signed distance: dCapsule < 0 inside; soft edge outward.
                float aCapsule = 1f - Mathf.SmoothStep(-1.6f, 2f, dCapsule);

                float fx = (px - finCx) / finRx;
                float fy = (py - finCy) / finRy;
                float finD = fx * fx + fy * fy;
                float aFin = finD <= 1f ? Mathf.SmoothStep(1f, 0.35f, Mathf.Sqrt(finD)) * 0.88f : 0f;

                float a = Mathf.Clamp01(Mathf.Max(aCapsule, aFin));
                a = Mathf.Min(1f, Mathf.Pow(a, 0.9f) * 1.05f);
                _fishTex.SetPixel(px, py, new Color(1f, 1f, 1f, a));
            }

            _fishTex.Apply();
            _fishTex.filterMode = FilterMode.Bilinear;
            _fishTex.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
