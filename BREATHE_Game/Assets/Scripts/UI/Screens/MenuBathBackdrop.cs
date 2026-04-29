using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.Video;
using Breathe.Gameplay;
using Breathe.Utility;

namespace Breathe.UI
{
    /// <summary>
    /// Same bath + caustics stack as the Bubbles minigame (menu preset: calmer caustics, no ambient floaters).
    /// Optional: loop an MP4 as the deep-water “plate” with caustics still drawn on top (sorting above the video).
    /// Place on a scene object or let <see cref="MainMenuController"/> create one at runtime.
    /// </summary>
    public sealed class MenuBathBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera _camera;

        [Header("Optional looping video background")]
        [SerializeField, Tooltip("Drag in a VideoClip (e.g. imported .mp4). Not used in WebGL builds—duplicate the file to StreamingAssets and set the name below.")]
        private VideoClip _menuBackgroundVideo;

        [SerializeField, Tooltip("File name in Assets/StreamingAssets/, e.g. breathe_videoloop.mp4. Required for WebGL; optional on PC/Mac if you prefer URL over VideoClip.")]
        private string _menuBackgroundVideoStreamingFile = "breathe_videoloop.mp4";

        [SerializeField, Tooltip("RenderTexture size (higher = sharper, more GPU memory).")]
        private int _videoRenderWidth = 1920;

        [SerializeField, Tooltip("RenderTexture size (higher = sharper, more GPU memory).")]
        private int _videoRenderHeight = 1080;

        [SerializeField, Tooltip("Hide tub rim, blobs, and sheen when a video is playing so the clip reads as the main plate.")]
        private bool _hideBathDecoWhenVideo = true;

        [SerializeField, Tooltip("No audio from the menu background clip (recommended).")]
        private bool _muteMenuVideo = true;

        [SerializeField, Range(0.2f, 1f), Tooltip("Scales the fitted 16:9 plate down so the clip looks less zoomed in (1 = use full water AABB). Smaller = more bath visible around the plate.")]
        private float _menuVideoFittedScale = 0.38f;

        [SerializeField, Tooltip("World X/Y in the bath plane after fitting the 16:9 rect. Slight +X nudges the plate right (helps when the source frame feels shifted left on screen).")]
        private Vector2 _menuVideoWorldOffset = new(0.18f, 0f);

        [Header("Menu video border")]
        [SerializeField, Range(0.01f, 0.10f), Tooltip("Extra size on each side as a fraction of the video width (keeps 16:9; larger = thicker visible frame).")]
        private float _menuVideoBorderFraction = 0.028f;

        [SerializeField, Tooltip("Override border color; when alpha is 0, use MenuVisualTheme.MenuVideoPlateBorder.")]
        private Color _menuVideoBorderColor = new(0f, 0f, 0f, 0f);

        [Header("Menu video look")]
        [SerializeField, Tooltip("Multiplies the looped clip; use RGB below 1 for dimmer wash behind UI. Slightly lowered so menu reads calmer vs UI chrome.")]
        private Color _menuVideoColorTint = new(0.62f, 0.645f, 0.68f, 1f);

        /// <summary>Well behind all bath art + caustics; higher 2D order = drawn on top.</summary>
        const int VideoFrameSortingOrder = -61;
        const int VideoMeshSortingOrder = -60;
        const string WebGlDefaultVideoFile = "breathe_videoloop.mp4";

        RenderTexture _menuVideoRt;
        Material _menuVideoMaterial;
        Mesh _menuVideoMesh;
        Material _menuVideoFrameMaterial;
        Mesh _menuVideoFrameMesh;
        VideoPlayer _menuVideoPlayer;

        VideoClip _runtimeClip;
        string _runtimeStreamingFile;
        /// <summary>Re-enabled if the video path fails or never delivers a first frame; avoids a blank void under UI.</summary>
        SpriteRenderer _menuDeepWaterSprite;

        // WebGL video RenderTexture (uses world-space mesh with Sprites/Default shader)
        RenderTexture _webglVideoRt;

        /// <summary>
        /// Call immediately after <c>AddComponent&lt;MenuBathBackdrop&gt;()</c> (before <see cref="Start"/>) to assign video when the component is created from code.
        /// </summary>
        public void SetRuntimeVideo(VideoClip clip, string streamingAssetsFileName = null)
        {
            _runtimeClip = clip;
            _runtimeStreamingFile = streamingAssetsFileName ?? "";
        }

        private void Awake()
        {
            var cam = _camera != null ? _camera : Camera.main;
            if (cam != null)
                cam.backgroundColor = MenuVisualTheme.MenuCameraClear;
        }

        private void Start()
        {
            var cam = _camera != null ? _camera : Camera.main;
            BathBackdropBuilder.Build(cam, out GameObject root, BathBackdropBuilder.MenuDefault);
            if (root == null) return;
            root.transform.SetParent(transform, true);

            var clip = _runtimeClip != null ? _runtimeClip : _menuBackgroundVideo;
            var file = !string.IsNullOrWhiteSpace(_runtimeStreamingFile) ? _runtimeStreamingFile : _menuBackgroundVideoStreamingFile;
            if (string.IsNullOrWhiteSpace(file)) file = "";

            bool web = Application.platform == RuntimePlatform.WebGLPlayer;

            // WebGL: Use UI-based video with URL playback (avoids URP Unlit shader stripping).
            // VideoClip is not supported on WebGL — must use VideoPlayer.url with StreamingAssets.
            if (web)
            {
                if (string.IsNullOrWhiteSpace(file))
                {
                    Debug.Log("MenuBathBackdrop: No StreamingAssets video file configured for WebGL. Bath backdrop will display instead.");
                    return;
                }
                TryStartMenuVideoWebGL(root, file.Trim());
                return;
            }

            if (clip == null && string.IsNullOrEmpty(file)) return;

            TryStartMenuVideo(root, clip, file.Trim());
        }

        void TryStartMenuVideo(GameObject root, VideoClip clip, string streamingFile)
        {
            var deep = root.transform.Find("DeepWater");
            if (deep == null) return;
            var deepSr = deep.GetComponent<SpriteRenderer>();
            if (deepSr == null || deepSr.sprite == null) return;
            _menuDeepWaterSprite = deepSr;

            int rw = Mathf.Max(256, _videoRenderWidth);
            int rh = Mathf.Max(256, _videoRenderHeight);
            _menuVideoRt = new RenderTexture(rw, rh, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false
            };
            if (!_menuVideoRt.Create())
            {
                Debug.LogWarning("MenuBathBackdrop: could not create RenderTexture for menu video.");
                return;
            }

            // URP 2D sprite materials still sample the sprite’s texture — not our RenderTexture. Use a mesh + URP
            // Unlit so the only albedo is the video.
            // 16:9 video was stretched to the deep-water’s tall (12:160) aspect. Fit a 16:9 quad inside the water AABB
            // in world space (parent = bath root) so the RT maps 1:1 in UVs with no non-uniform scale.
            var vGo = new GameObject("MenuVideoBackground");
            vGo.transform.SetParent(root.transform, false);
            vGo.transform.localRotation = Quaternion.identity;
            vGo.transform.localScale = Vector3.one;
            vGo.layer = deep.gameObject.layer;

            Bounds waterWorld = deepSr.bounds;
            GetLargest16By9InAabb(waterWorld, out float quadW, out float quadH);
            float s = Mathf.Clamp01(_menuVideoFittedScale);
            quadW *= s;
            quadH *= s;
            Vector3 centerWorld = waterWorld.center;
            vGo.transform.position = new Vector3(
                centerWorld.x + _menuVideoWorldOffset.x,
                centerWorld.y + _menuVideoWorldOffset.y,
                centerWorld.z);
            vGo.transform.Translate(0f, 0f, 0.02f, Space.Self);

            float b = Mathf.Max(0.004f, _menuVideoBorderFraction);
            float frameScale = 1f + 2f * b;
            float frameW = quadW * frameScale;
            float frameH = quadH * frameScale;

            Color borderCol = _menuVideoBorderColor.a > 0.01f
                ? _menuVideoBorderColor
                : MenuVisualTheme.MenuVideoPlateBorder;
            _menuVideoFrameMesh = CreateFitted16By9QuadMesh(frameW, frameH);
            var frameGo = new GameObject("MenuVideoFrame");
            frameGo.transform.SetParent(vGo.transform, false);
            frameGo.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            frameGo.AddComponent<MeshFilter>().sharedMesh = _menuVideoFrameMesh;
            _menuVideoFrameMaterial = CreateUrpUnlitSolidColorMaterial(borderCol);
            var frameMr = frameGo.AddComponent<MeshRenderer>();
            frameMr.sharedMaterial = _menuVideoFrameMaterial;
            frameMr.sortingLayerID = deepSr.sortingLayerID;
            frameMr.sortingOrder = VideoFrameSortingOrder;
            frameMr.shadowCastingMode = ShadowCastingMode.Off;
            frameMr.receiveShadows = false;
            frameMr.lightProbeUsage = LightProbeUsage.Off;
            frameGo.layer = vGo.layer;

            var surfaceGo = new GameObject("MenuVideoSurface");
            surfaceGo.transform.SetParent(vGo.transform, false);
            surfaceGo.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            surfaceGo.layer = vGo.layer;

            _menuVideoMesh = CreateFitted16By9QuadMesh(quadW, quadH);
            surfaceGo.AddComponent<MeshFilter>().sharedMesh = _menuVideoMesh;
            _menuVideoMaterial = CreateUrpUnlitVideoMaterial(_menuVideoRt, _menuVideoColorTint);
            var mr = surfaceGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _menuVideoMaterial;
            mr.sortingLayerID = deepSr.sortingLayerID;
            mr.sortingOrder = VideoMeshSortingOrder;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            EnsureMenuCausticsDrawAboveMenuVideoIfNeeded(root, VideoMeshSortingOrder);

            var vp = surfaceGo.AddComponent<VideoPlayer>();
            _menuVideoPlayer = vp;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = _menuVideoRt;
            vp.isLooping = true;
            vp.playOnAwake = false;
            vp.waitForFirstFrame = true;
            if (_muteMenuVideo)
                vp.audioOutputMode = VideoAudioOutputMode.None;

            if (Application.platform == RuntimePlatform.WebGLPlayer)
            {
                if (string.IsNullOrEmpty(streamingFile)) streamingFile = WebGlDefaultVideoFile;
                // WebGL URLs must use forward slashes; Path.Combine on Windows uses backslashes.
                string path = Application.streamingAssetsPath + "/" + streamingFile;
                vp.source = VideoSource.Url;
                vp.url = path;
            }
            else if (clip != null)
            {
                vp.source = VideoSource.VideoClip;
                vp.clip = clip;
            }
            else
            {
                string path = Path.Combine(Application.streamingAssetsPath, streamingFile);
#if !UNITY_WEBGL
                if (!File.Exists(path))
                {
                    Debug.LogWarning(
                        "MenuBathBackdrop: StreamingAssets video not found: " + path +
                        ". Add the .mp4 under Assets/StreamingAssets/.");
                }
#endif

                vp.source = VideoSource.Url;
                vp.url = path;
            }

            if (_hideBathDecoWhenVideo)
            {
                foreach (var n in new[] { "TubRim", "BathMidBlob", "BathMidBlob2", "SurfaceSheen" })
                {
                    var t = root.transform.Find(n);
                    if (t != null) t.gameObject.SetActive(false);
                }
            }

            // Do not disable DeepWater here — URP 2D + MeshRenderer video can fail to draw; you would only see camera
            // clear + UI. We hide the gradient plate only after Prepare succeeds (OnVideoPrepareCompleted).

            vp.prepareCompleted += OnVideoPrepareCompleted;
            vp.errorReceived += OnVideoError;
            vp.Prepare();
        }

        /// <summary>
        /// WebGL: World-space SpriteRenderer approach using Sprites/Default shader (never stripped).
        /// Uses VideoPlayer.url with StreamingAssets path.
        /// </summary>
        void TryStartMenuVideoWebGL(GameObject root, string streamingFile)
        {
            // Find the deep water sprite to position relative to it
            var deep = root.transform.Find("DeepWater");
            if (deep == null)
            {
                Debug.LogWarning("MenuBathBackdrop: DeepWater not found in bath backdrop for WebGL video.");
                return;
            }
            var deepSr = deep.GetComponent<SpriteRenderer>();
            if (deepSr == null || deepSr.sprite == null)
            {
                Debug.LogWarning("MenuBathBackdrop: DeepWater has no SpriteRenderer for WebGL video positioning.");
                return;
            }
            _menuDeepWaterSprite = deepSr;

            // Build URL for StreamingAssets (WebGL requires forward slashes)
            string videoUrl = Application.streamingAssetsPath + "/" + streamingFile;
            Debug.Log($"MenuBathBackdrop: Starting WebGL video from URL: {videoUrl}");

            // Create RenderTexture for video
            int rw = Mathf.Max(256, _videoRenderWidth);
            int rh = Mathf.Max(256, _videoRenderHeight);
            _webglVideoRt = new RenderTexture(rw, rh, 0, RenderTextureFormat.ARGB32)
            {
                name = "WebGLMenuVideo_RT",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            _webglVideoRt.Create();

            // Create world-space quad for video using Sprites/Default (never stripped in WebGL)
            Bounds waterWorld = deepSr.bounds;
            GetLargest16By9InAabb(waterWorld, out float quadW, out float quadH);
            float s = Mathf.Clamp01(_menuVideoFittedScale);
            quadW *= s;
            quadH *= s;
            Vector3 centerWorld = waterWorld.center;

            var vGo = new GameObject("WebGLVideoBackground");
            vGo.transform.SetParent(root.transform, false);
            vGo.transform.position = new Vector3(
                centerWorld.x + _menuVideoWorldOffset.x,
                centerWorld.y + _menuVideoWorldOffset.y,
                centerWorld.z + 0.02f);
            vGo.layer = deep.gameObject.layer;

            // Border frame
            float b = Mathf.Max(0.004f, _menuVideoBorderFraction);
            float frameScale = 1f + 2f * b;
            float frameW = quadW * frameScale;
            float frameH = quadH * frameScale;

            Color borderCol = _menuVideoBorderColor.a > 0.01f
                ? _menuVideoBorderColor
                : MenuVisualTheme.MenuVideoPlateBorder;
            _menuVideoFrameMesh = CreateFitted16By9QuadMesh(frameW, frameH);
            var frameGo = new GameObject("WebGLVideoFrame");
            frameGo.transform.SetParent(vGo.transform, false);
            frameGo.transform.localPosition = new Vector3(0f, 0f, -0.04f);
            frameGo.AddComponent<MeshFilter>().sharedMesh = _menuVideoFrameMesh;
            // Use Sprites/Default which is never stripped
            _menuVideoFrameMaterial = new Material(Shader.Find("Sprites/Default"));
            _menuVideoFrameMaterial.color = borderCol;
            var frameMr = frameGo.AddComponent<MeshRenderer>();
            frameMr.sharedMaterial = _menuVideoFrameMaterial;
            frameMr.sortingLayerID = deepSr.sortingLayerID;
            frameMr.sortingOrder = VideoFrameSortingOrder;
            frameMr.shadowCastingMode = ShadowCastingMode.Off;
            frameMr.receiveShadows = false;
            frameMr.lightProbeUsage = LightProbeUsage.Off;
            frameGo.layer = vGo.layer;

            // Video surface
            var surfaceGo = new GameObject("WebGLVideoSurface");
            surfaceGo.transform.SetParent(vGo.transform, false);
            surfaceGo.transform.localPosition = new Vector3(0f, 0f, 0.02f);
            surfaceGo.layer = vGo.layer;

            _menuVideoMesh = CreateFitted16By9QuadMesh(quadW, quadH);
            surfaceGo.AddComponent<MeshFilter>().sharedMesh = _menuVideoMesh;
            // Use Sprites/Default with our RenderTexture - this shader is never stripped
            _menuVideoMaterial = new Material(Shader.Find("Sprites/Default"));
            _menuVideoMaterial.mainTexture = _webglVideoRt;
            _menuVideoMaterial.color = _menuVideoColorTint;
            var mr = surfaceGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _menuVideoMaterial;
            mr.sortingLayerID = deepSr.sortingLayerID;
            mr.sortingOrder = VideoMeshSortingOrder;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            EnsureMenuCausticsDrawAboveMenuVideoIfNeeded(root, VideoMeshSortingOrder);

            // Create VideoPlayer
            _menuVideoPlayer = surfaceGo.AddComponent<VideoPlayer>();
            _menuVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            _menuVideoPlayer.targetTexture = _webglVideoRt;
            _menuVideoPlayer.isLooping = true;
            _menuVideoPlayer.playOnAwake = false;
            _menuVideoPlayer.waitForFirstFrame = true;
            _menuVideoPlayer.skipOnDrop = true;
            if (_muteMenuVideo)
                _menuVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;

            // WebGL must use URL source
            _menuVideoPlayer.source = VideoSource.Url;
            _menuVideoPlayer.url = videoUrl;

            _menuVideoPlayer.prepareCompleted += OnWebGLVideoPrepared;
            _menuVideoPlayer.errorReceived += OnWebGLVideoError;

            if (_hideBathDecoWhenVideo)
            {
                foreach (var n in new[] { "TubRim", "BathMidBlob", "BathMidBlob2", "SurfaceSheen" })
                {
                    var t = root.transform.Find(n);
                    if (t != null) t.gameObject.SetActive(false);
                }
                StartCoroutine(HideDeepWaterOnceWebGLVideoHasFirstFrame());
            }

            _menuVideoPlayer.Prepare();
        }

        System.Collections.IEnumerator HideDeepWaterOnceWebGLVideoHasFirstFrame()
        {
            float t = 0f;
            while (_menuVideoPlayer != null && !_menuVideoPlayer.isPlaying && t < 8f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_menuVideoPlayer != null && _menuVideoPlayer.isPlaying && _menuDeepWaterSprite != null)
            {
                _menuDeepWaterSprite.enabled = false;
                Debug.Log("MenuBathBackdrop: WebGL video playing, hiding deep water sprite.");
            }
        }

        void OnWebGLVideoPrepared(VideoPlayer vp)
        {
            vp.prepareCompleted -= OnWebGLVideoPrepared;
            Debug.Log($"MenuBathBackdrop: WebGL video prepared. Resolution: {vp.width}x{vp.height}");
            vp.Play();
        }

        void OnWebGLVideoError(VideoPlayer vp, string message)
        {
            vp.errorReceived -= OnWebGLVideoError;
            Debug.LogWarning($"MenuBathBackdrop: WebGL video error: {message}. Bath backdrop will show instead.");
            // Re-enable deep water if we hid it
            if (_menuDeepWaterSprite != null)
                _menuDeepWaterSprite.enabled = true;
        }

        /// <summary>Largest axis-aligned 16:9 rect that fits in the water AABB (world units).</summary>
        static void GetLargest16By9InAabb(Bounds worldAabb, out float w, out float h)
        {
            float wb = worldAabb.size.x;
            float hb = worldAabb.size.y;
            const float arVid = 16f / 9f;
            if (wb < 1e-4f || hb < 1e-4f)
            {
                w = 1f;
                h = 1f;
                return;
            }

            float arB = wb / hb;
            if (arB > arVid)
            {
                h = hb;
                w = h * arVid;
                if (w > wb)
                {
                    w = wb;
                    h = w / arVid;
                }
            }
            else
            {
                w = wb;
                h = w / arVid;
                if (h > hb)
                {
                    h = hb;
                    w = h * arVid;
                }
            }
        }

        /// <summary>XY quad in vGo’s local space, centered, +Z normal, full 0..1 UVs (1:1 with video RT aspect).</summary>
        static Mesh CreateFitted16By9QuadMesh(float w, float h)
        {
            float hx = w * 0.5f, hy = h * 0.5f;
            float z = 0f;
            var m = new Mesh
            {
                name = "MenuVideoPlane16x9",
                vertices = new[]
                {
                    new Vector3(-hx, -hy, z),
                    new Vector3(-hx, hy, z),
                    new Vector3(hx, hy, z),
                    new Vector3(hx, -hy, z)
                },
                uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) },
                triangles = new[] { 0, 1, 2, 0, 2, 3 }
            };
            m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>
        /// Lock caustics sprites to just above the menu video mesh so they always read as a water layer on the clip
        /// (parallax + intensity do the look; order prevents any batch edge case from tucking them underneath).
        /// </summary>
        static void EnsureMenuCausticsDrawAboveMenuVideoIfNeeded(GameObject bathRoot, int videoMeshOrder)
        {
            int orderA = videoMeshOrder + 2;
            int orderB = videoMeshOrder + 3;
            foreach (var sr in bathRoot.GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr == null) continue;
                if (sr.name != "CausticsOverlay" && sr.name != "CausticsOverlayB") continue;
                sr.sortingOrder = sr.name == "CausticsOverlayB" ? orderB : orderA;
            }
        }

        static Material CreateUrpUnlitVideoMaterial(Texture videoRt, Color tint)
        {
            // URP Unlit: samples _BaseMap; Sprite shaders ignore it for 2D batching.
            // Fallback chain to handle WebGL shader stripping.
            var s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Texture");
            if (s == null) s = Shader.Find("Unlit/Transparent");
            if (s == null) s = Shader.Find("Sprites/Default"); // always included
            if (s == null)
            {
                Debug.LogError("MenuBathBackdrop: no usable shader found for video material.");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            var m = new Material(s);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", videoRt);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", videoRt);
            m.mainTexture = videoRt;
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
            if (m.HasProperty("_Cull")) m.SetInt("_Cull", (int)CullMode.Off);
            ConfigureUrpUnlitForUrp2DMenuPlane(m);
            return m;
        }

        static Material CreateUrpUnlitSolidColorMaterial(Color c)
        {
            var s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Unlit/Texture");
            if (s == null) s = Shader.Find("Sprites/Default"); // always included
            if (s == null)
            {
                Debug.LogError("MenuBathBackdrop: no Unlit shader for video frame border.");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            var m = new Material(s);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", Texture2D.whiteTexture);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            m.color = c;
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", Texture2D.whiteTexture);
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 0f);
            if (m.HasProperty("_Cull")) m.SetInt("_Cull", (int)CullMode.Off);
            ConfigureUrpUnlitForUrp2DMenuPlane(m);
            return m;
        }

        /// <summary>URP 2D (Renderer2D) is finicky with opaque MeshRenderer; send Unlit quads to the transparent queue and disable Z write.</summary>
        static void ConfigureUrpUnlitForUrp2DMenuPlane(Material m)
        {
            if (m == null) return;
            m.renderQueue = (int)RenderQueue.Transparent;
            if (m.HasProperty("_ZWrite")) m.SetInt("_ZWrite", 0);
        }

        void OnVideoPrepareCompleted(VideoPlayer vp)
        {
            if (vp == null || _menuVideoPlayer != vp) return;
            _menuVideoPlayer.prepareCompleted -= OnVideoPrepareCompleted;
            if (_menuDeepWaterSprite != null)
                _menuDeepWaterSprite.enabled = false;
            _menuVideoPlayer.Play();
        }

        void OnVideoError(VideoPlayer source, string message)
        {
            Debug.LogWarning("MenuBathBackdrop: Video error — " + message);
            if (_menuVideoPlayer != null)
                _menuVideoPlayer.prepareCompleted -= OnVideoPrepareCompleted;
            if (_menuDeepWaterSprite != null)
                _menuDeepWaterSprite.enabled = true;
        }

        void OnDestroy()
        {
            if (_menuVideoPlayer != null)
            {
                _menuVideoPlayer.prepareCompleted -= OnVideoPrepareCompleted;
                _menuVideoPlayer.prepareCompleted -= OnWebGLVideoPrepared;
                _menuVideoPlayer.errorReceived -= OnVideoError;
                _menuVideoPlayer.errorReceived -= OnWebGLVideoError;
            }

            if (_menuVideoMaterial != null)
            {
                Destroy(_menuVideoMaterial);
                _menuVideoMaterial = null;
            }

            if (_menuVideoFrameMaterial != null)
            {
                Destroy(_menuVideoFrameMaterial);
                _menuVideoFrameMaterial = null;
            }

            if (_menuVideoMesh != null)
            {
                Destroy(_menuVideoMesh);
                _menuVideoMesh = null;
            }

            if (_menuVideoFrameMesh != null)
            {
                Destroy(_menuVideoFrameMesh);
                _menuVideoFrameMesh = null;
            }

            if (_menuVideoRt != null)
            {
                _menuVideoRt.Release();
                Destroy(_menuVideoRt);
            }

            // WebGL cleanup
            if (_webglVideoRt != null)
            {
                _webglVideoRt.Release();
                Destroy(_webglVideoRt);
                _webglVideoRt = null;
            }
        }
    }
}
