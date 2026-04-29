using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Breathe.Utility
{
    /// <summary>
    /// DontDestroyOnLoad full-screen black overlay. Main menu fades in from black (~3 s); gameplay (~1 s).
    /// Use <see cref="FadeToBlackThenLoadScene"/> before <see cref="SceneManager.LoadScene"/> (level pick, results → retry/menu).
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class ScreenFadeCoordinator : MonoBehaviour
    {
        public static ScreenFadeCoordinator Instance { get; private set; }

        /// <summary>Runs at the same time as the fade-to-black (before LoadScene). Args: blackout duration seconds, target scene name.</summary>
        public static event Action<float, string> FadeOutBeforeSceneLoadStarted;

        public float MainMenuRevealDuration => MainMenuFadeInDuration;
        public float GameplayRevealDuration => GameplayFadeInDuration;
        public float FadeBeforeLoadSeconds => FadeOutBeforeLoadDuration;

        /// <summary>
        /// 1 = fade overlay fully transparent (scene visible); 0 = full black.
        /// Use to sync BGM/haptics with the same curve as the main-menu reveal (including boot hold + eased ramp).
        /// </summary>
        public float MenuRevealBrightness01 =>
            _fadeImage == null ? 1f : 1f - Mathf.Clamp01(_fadeImage.color.a);

        const int CanvasSortOrder = 32500;

        /// <summary>Reveal duration after MainMenu loads (opacity ramp only; hold-black is separate).</summary>
        const float MainMenuFadeInDuration = 3f;

        /// <summary>Reveal duration after gameplay/minigame scenes load.</summary>
        const float GameplayFadeInDuration = 1f;

        /// <summary>Fade to black duration before SceneManager.LoadScene.</summary>
        const float FadeOutBeforeLoadDuration = 1f;

        /// <summary>
        /// Limits how much simulated time advances per frame only during the boot main-menu reveal.
        /// Prevents load spikes from skipping alpha; short fades elsewhere use uncapped timestep for predictable timing.</summary>
        const float MaxUnscaledFrameSecondsBootRevealOnly = 1f / 30f;

        Image _fadeImage;

        /// <summary>Scene reveal (opaque → transparent). Stopped/replaced by <see cref="OnSceneLoaded"/> only.</summary>
        Coroutine _revealRoutine;

        /// <summary>Fade-out then load — separate coroutine so <see cref="OnSceneLoaded"/> does not stop mid-LoadScene.</summary>
        Coroutine _exitFadeRoutine;

        /// <summary>Skip the first OnSceneLoaded since we handle boot fade manually.</summary>
        bool _bootFadeInProgress;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void BootstrapEarly()
        {
            if (Instance != null) return;
            var go = new GameObject("ScreenFadeCoordinator");
            DontDestroyOnLoad(go);
            go.AddComponent<ScreenFadeCoordinator>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.root == transform)
                DontDestroyOnLoad(gameObject);

            EnsureOverlayHierarchy();
            SetOpaqueBlack();

            // Subscribe to scene loads, but flag that boot fade is in progress
            _bootFadeInProgress = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            
            StopRevealRoutineOnly();
            _revealRoutine = StartCoroutine(RevealFadeForBootSceneCoroutine());
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!isActiveAndEnabled || !scene.IsValid()) return;

            // Skip if boot fade is still handling the initial reveal
            if (_bootFadeInProgress)
                return;

            StopRevealRoutineOnly();
            SetOpaqueBlack();
            float reveal = IsMainMenuScene(scene.name)
                ? MainMenuFadeInDuration
                : GameplayFadeInDuration;
            _revealRoutine = StartCoroutine(FadeAlphaRoutineCoroutine(1f, 0f, reveal));
        }

        /// <summary>Fades to black then loads the scene (<see cref="SceneLoader.MainMenuScene"/>, same scene reload, etc.).</summary>
        public void FadeToBlackThenLoadScene(string sceneName)
        {
            if (_exitFadeRoutine != null)
                return;

            bool valid =
                !string.IsNullOrEmpty(sceneName)
                && SceneLoader.IsSceneInBuildSettings(sceneName);

            _exitFadeRoutine = StartCoroutine(FadeOutThenLoadSceneCoroutine(sceneName, valid));
        }

        static bool IsMainMenuScene(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName)
                   && string.Equals(
                       sceneName.Trim(),
                       SceneLoader.MainMenuScene.Trim(),
                       StringComparison.OrdinalIgnoreCase);
        }

        IEnumerator RevealFadeForBootSceneCoroutine()
        {
            // Ensure overlay is opaque black and wait for assets to initialize
            SetOpaqueBlack();
            
            // Wait a moment for video/assets to begin loading before starting fade
            float holdBlackDuration = 1.5f;
            float elapsed = 0f;
            while (elapsed < holdBlackDuration)
            {
                SetOpaqueBlack(); // Keep forcing black in case anything else tries to change it
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            // Wait until scene is valid (might still be loading with BeforeSceneLoad)
            Scene s = SceneManager.GetActiveScene();
            float maxWait = 5f;
            float waited = 0f;
            while (!s.IsValid() && waited < maxWait)
            {
                SetOpaqueBlack();
                yield return null;
                waited += Time.unscaledDeltaTime;
                s = SceneManager.GetActiveScene();
            }
            
            if (!s.IsValid())
            {
                Debug.LogWarning("[ScreenFadeCoordinator] Scene never became valid, cannot fade.");
                _bootFadeInProgress = false;
                yield break;
            }
            
            float reveal = IsMainMenuScene(s.name)
                ? MainMenuFadeInDuration
                : GameplayFadeInDuration;
            
            // Long initial main menu reveal: eased ramp + guarded dt. All other fades stay linear.
            bool bootMainMenuEase = IsMainMenuScene(s.name);
            yield return FadeAlphaRun(1f, 0f, reveal, bootMainMenuEase);
            
            _bootFadeInProgress = false;
            _revealRoutine = null;
        }

        IEnumerator FadeOutThenLoadSceneCoroutine(string sceneName, bool validScene)
        {
            FadeOutBeforeSceneLoadStarted?.Invoke(FadeOutBeforeLoadDuration, sceneName);
            yield return FadeAlphaRun(UiAlpha(), 1f, FadeOutBeforeLoadDuration);
            SetOpaqueBlack();
            _exitFadeRoutine = null;

            if (!validScene)
            {
                Debug.LogWarning($"[ScreenFadeCoordinator] Cannot load scene \"{sceneName}\".");
                yield break;
            }

            SceneManager.LoadScene(sceneName);
        }

        /// <param name="bootMainMenuRevealEase">Only initial main-menu boot: smootherstep + per-frame dt cap. False: linear alpha, matching pre-fix behavior elsewhere.</param>
        IEnumerator FadeAlphaRun(float from, float to, float duration, bool bootMainMenuRevealEase = false)
        {
            if (duration <= 0f || _fadeImage == null)
                yield break;

            float t = 0f;
            var c = _fadeImage.color;
            while (t < duration)
            {
                float dt = bootMainMenuRevealEase
                    ? Mathf.Min(Time.unscaledDeltaTime, MaxUnscaledFrameSecondsBootRevealOnly)
                    : Time.unscaledDeltaTime;
                t += dt;
                float u = Mathf.Clamp01(t / duration);
                if (bootMainMenuRevealEase)
                    u = SmootherFade01(u);
                c.a = Mathf.Lerp(from, to, u);
                _fadeImage.color = c;
                yield return null;
            }

            c.a = to;
            _fadeImage.color = c;
        }

        /// <summary>Ken Perlin smootherstep from 0 to 1; used only for long boot main-menu reveal.</summary>
        static float SmootherFade01(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * u * (u * (6f * u - 15f) + 10f);
        }

        void StopRevealRoutineOnly()
        {
            if (_revealRoutine == null) return;
            StopCoroutine(_revealRoutine);
            _revealRoutine = null;
        }

        IEnumerator FadeAlphaRoutineCoroutine(float from, float to, float duration)
        {
            yield return FadeAlphaRun(from, to, duration);
            _revealRoutine = null;
        }

        void SetOpaqueBlack()
        {
            if (_fadeImage == null) EnsureOverlayHierarchy();
            _fadeImage.color = new Color(0f, 0f, 0f, 1f);
        }

        float UiAlpha() => _fadeImage != null ? _fadeImage.color.a : 0f;

        void EnsureOverlayHierarchy()
        {
            if (_fadeImage != null) return;

            var root = new GameObject("ScreenFadeOverlay", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);

            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasSortOrder;
            canvas.overrideSorting = true;

            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var rt = root.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var tex = Texture2D.whiteTexture;
            var sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);

            var imgGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(root.transform, false);
            var irt = imgGo.GetComponent<RectTransform>();
            irt.anchorMin = Vector2.zero;
            irt.anchorMax = Vector2.one;
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = Vector2.zero;

            _fadeImage = imgGo.GetComponent<Image>();
            _fadeImage.sprite = sprite;
            _fadeImage.color = Color.black;
            _fadeImage.raycastTarget = false;
        }
    }
}
