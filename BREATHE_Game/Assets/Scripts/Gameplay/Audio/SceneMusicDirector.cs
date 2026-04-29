using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Breathe.Utility;

namespace Breathe.Audio
{
    [DefaultExecutionOrder(-200)]
    public sealed class SceneMusicDirector : MonoBehaviour
    {
        public static SceneMusicDirector Instance { get; private set; }

        [Header("Music loops")]
        [SerializeField] AudioClip _mainMenuA;
        [SerializeField] AudioClip _mainMenuB;
        [SerializeField] AudioClip _sailboat;
        [SerializeField] AudioClip _balloon;
        [SerializeField] AudioClip _bubbles;
        [SerializeField] AudioClip _skydive;
        [SerializeField] AudioClip _stargaze;

        [SerializeField, Tooltip("Multiply Settings music (0-1). Default 0.25 = about -75% headroom vs full gain.")]
        float _prefsGainMultiplier = 0.25f;

        [SerializeField]
        float _unloadFadeFallbackSeconds = 0.42f;

        AudioSource _src;
        Transform _listenerFollow;
        Coroutine _musicRoutine;
        float _fade01;
        bool _pairedExitFade;
        bool _retainGameplayMusicAfterFade;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSource();
            EnsurePersistentListener();
            SceneManager.sceneLoaded += OnSceneLoaded;
            ScreenFadeCoordinator.FadeOutBeforeSceneLoadStarted += OnExitFadeBegins;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            ScreenFadeCoordinator.FadeOutBeforeSceneLoadStarted -= OnExitFadeBegins;
            if (Instance == this) Instance = null;
        }

        void LateUpdate()
        {
            SyncListenerToCamera();
        }

        void EnsurePersistentListener()
        {
            if (_listenerFollow != null) return;

            var go = new GameObject("PersistentAudioListener");
            go.transform.SetParent(transform, false);
            _listenerFollow = go.transform;
            go.AddComponent<AudioListener>();

            SyncListenerToCamera();
            var keep = _listenerFollow.GetComponent<AudioListener>();
            DisableDuplicateListenersInLoadedScene(keep);
        }

        void SyncListenerToCamera()
        {
            Camera c = Camera.main;
            if (_listenerFollow != null && c != null)
                _listenerFollow.SetPositionAndRotation(c.transform.position, c.transform.rotation);
        }

        void DisableDuplicateListenersInLoadedScene(AudioListener keep)
        {
            if (keep == null) return;

            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid()) return;

            foreach (GameObject root in active.GetRootGameObjects())
            {
                foreach (AudioListener al in root.GetComponentsInChildren<AudioListener>(true))
                {
                    if (al == null || al == keep)
                        continue;
                    if (!al.gameObject.scene.Equals(active))
                        continue;

                    al.enabled = false;
                }
            }
        }

        void EnsureSource()
        {
            if (_src != null) return;
            var child = new GameObject("BackgroundMusic");
            child.transform.SetParent(transform, false);
            _src = child.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.loop = true;
            _src.spatialBlend = 0f;
            _fade01 = 0f;
            _src.volume = 0f;
            _src.ignoreListenerPause = true;
        }

        public void RefreshFromMusicSlider() => ApplyVolumes();

        /// <summary>Global trim on scene BGM (all loops). Includes −25% vs prior 0.85 calibration.</summary>
        const float BackgroundLoopGainScale = 0.85f * 0.75f;

        float PeakLinear()
        {
            float m = Mathf.Clamp01(PlayerPrefs.GetFloat(AudioPrefsKeys.MusicVolume, AudioMixDefaults.MusicLinear));
            return Mathf.Clamp01(m * _prefsGainMultiplier);
        }

        void ApplyVolumes()
        {
            EnsureSource();
            float pk = Mathf.Max(PeakLinear(), 1e-5f);
            _src.volume = BackgroundLoopGainScale * pk * Mathf.Clamp01(_fade01);
        }

        static bool IsMainMenu(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            return string.Equals(n.Trim(), SceneLoader.MainMenuScene.Trim(),
                System.StringComparison.OrdinalIgnoreCase);
        }

        float RevealFadeInSeconds(string sceneName)
        {
            var c = ScreenFadeCoordinator.Instance;
            if (c != null)
                return IsMainMenu(sceneName) ? c.MainMenuRevealDuration : c.GameplayRevealDuration;
            return IsMainMenu(sceneName) ? 3f : 1f;
        }

        AudioClip[] MainMenuAlts()
        {
            if (_mainMenuA != null && _mainMenuB != null)
                return new[] { _mainMenuA, _mainMenuB };
            if (_mainMenuA != null) return new[] { _mainMenuA };
            if (_mainMenuB != null) return new[] { _mainMenuB };
            return System.Array.Empty<AudioClip>();
        }

        AudioClip ResolveClip(string sceneName)
        {
            string n = sceneName?.Trim();
            if (IsMainMenu(n))
            {
                var alt = MainMenuAlts();
                if (alt.Length == 0) return null;
                return alt[UnityEngine.Random.Range(0, alt.Length)];
            }

            switch (n)
            {
                case "SAILBOAT": return _sailboat;
                case "BALLOON": return _balloon;
                case "BUBBLES": return _bubbles;
                case "SKYDIVE": return _skydive;
                case "STARGAZE": return _stargaze;
                default: return null;
            }
        }

        void OnExitFadeBegins(float blackoutSeconds, string targetSceneName)
        {
            blackoutSeconds = Mathf.Max(0.04f, blackoutSeconds);
            _pairedExitFade = true;

            string active = SceneManager.GetActiveScene().name.Trim();
            string target = (targetSceneName ?? string.Empty).Trim();
            bool sameGameplayReload = target.Length > 0
                && string.Equals(target, active, StringComparison.OrdinalIgnoreCase)
                && !IsMainMenu(target);

            _retainGameplayMusicAfterFade = sameGameplayReload;
            if (sameGameplayReload)
                return;

            RunCoroutine(ExitFadeOut(blackoutSeconds));
        }

        IEnumerator ExitFadeOut(float duration)
        {
            EnsureSource();
            float begin = Mathf.Clamp01(_fade01);
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / duration);
                _fade01 = Mathf.Lerp(begin, 0f, u);
                ApplyVolumes();
                yield return null;
            }

            _fade01 = 0f;
            ApplyVolumes();
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!isActiveAndEnabled || !scene.IsValid()) return;

            AudioListener keeper = null;
            if (_listenerFollow != null)
                keeper = _listenerFollow.GetComponent<AudioListener>();
            if (keeper != null && keeper.enabled == false)
                keeper.enabled = true;
            DisableDuplicateListenersInLoadedScene(keeper);

            string name = scene.name;
            AudioClip next = ResolveClip(name);

            bool retainIntent = _retainGameplayMusicAfterFade;
            _retainGameplayMusicAfterFade = false;

            bool paired = _pairedExitFade;
            _pairedExitFade = false;

            // Play Again / Try Again → same gameplay scene reload: keep looping track (no duck, no restart).
            if (retainIntent && ShouldKeepGameplayTrackPlaying(name, next))
            {
                AbortMusicRoutineIfAny();
                _fade01 = 1f;
                ApplyVolumes();
                return;
            }

            // Same-scene reload without paired exit fade (e.g. SceneLoader.ReloadCurrentScene fallback path).
            if (!paired && ShouldKeepGameplayTrackPlaying(name, next))
            {
                AbortMusicRoutineIfAny();
                _fade01 = 1f;
                ApplyVolumes();
                return;
            }

            if (next == null)
            {
                RunCoroutine(StopAllMusic(paired));
                return;
            }

            RunCoroutine(EnterMusic(name, next, paired));
        }

        bool ShouldKeepGameplayTrackPlaying(string incomingSceneName, AudioClip incomingClip)
        {
            if (incomingClip == null) return false;
            if (IsMainMenu(incomingSceneName)) return false;
            EnsureSource();
            if (!_src.isPlaying || _src.clip == null) return false;
            return ReferenceEquals(_src.clip, incomingClip);
        }

        void AbortMusicRoutineIfAny()
        {
            if (_musicRoutine == null) return;
            StopCoroutine(_musicRoutine);
            _musicRoutine = null;
        }

        void RunCoroutine(IEnumerator e)
        {
            if (_musicRoutine != null)
            {
                StopCoroutine(_musicRoutine);
                _musicRoutine = null;
            }

            _musicRoutine = StartCoroutine(Wrap(e));
        }

        IEnumerator Wrap(IEnumerator e)
        {
            yield return e;
            _musicRoutine = null;
        }

        IEnumerator StopAllMusic(bool silentAlready)
        {
            if (!silentAlready && _fade01 > 0.01f)
                yield return ExitFadeOut(Mathf.Clamp(_unloadFadeFallbackSeconds, 0.1f, 2f));
            EnsureSource();
            _src.Stop();
            _src.clip = null;
            _fade01 = 0f;
            ApplyVolumes();
        }

        IEnumerator EnterMusic(string sceneName, AudioClip clip, bool silentAlready)
        {
            EnsureSource();
            if (!silentAlready && _fade01 > 0.01f)
                yield return ExitFadeOut(Mathf.Clamp(_unloadFadeFallbackSeconds, 0.1f, 2f));
            _fade01 = 0f;
            ApplyVolumes();
            _src.Stop();
            _src.clip = clip;
            _src.time = 0f;
            _src.Play();

            // Main menu: lock BGM to ScreenFadeCoordinator overlay (hold-black + same eased reveal as visuals).
            if (IsMainMenu(sceneName) && ScreenFadeCoordinator.Instance != null)
            {
                const float maxWait = 25f;
                float waited = 0f;
                while (waited < maxWait)
                {
                    float b = ScreenFadeCoordinator.Instance.MenuRevealBrightness01;
                    _fade01 = Mathf.Clamp01(b);
                    ApplyVolumes();
                    if (_fade01 >= 0.999f)
                        break;
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                float fadeIn = Mathf.Max(0.04f, RevealFadeInSeconds(sceneName));
                float t = 0f;
                while (t < fadeIn)
                {
                    t += Time.unscaledDeltaTime;
                    _fade01 = Mathf.Clamp01(t / fadeIn);
                    ApplyVolumes();
                    yield return null;
                }
            }

            _fade01 = 1f;
            ApplyVolumes();
        }
    }
}
