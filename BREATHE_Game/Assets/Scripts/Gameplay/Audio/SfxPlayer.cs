using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Breathe.Data;
using Breathe.Gameplay;

namespace Breathe.Audio
{
    // Plays global SFX from SfxLibrary and optional MinigameSfxProfile on MinigameDefinition.
    // Add one instance to the first scene (e.g. Main Menu) with SfxLibrary assigned; mark DontDestroyOnLoad.
    public sealed class SfxPlayer : MonoBehaviour
    {
        /// <summary>Must match <c>SettingsManager</c> SFX slider key so menu blips and all one-shots follow the SFX bus.</summary>
        const string PrefKeySfxVolume = "Breathe_SfxVolume";

        public static SfxPlayer Instance { get; private set; }

        [SerializeField] private SfxLibrary _library;
        [SerializeField, Tooltip("2D one-shots (spatialBlend 0).")]
        private AudioSource _oneShot;
        [SerializeField, Tooltip("Optional loop channel for ambience from MinigameSfxProfile.")]
        private AudioSource _loop;

        [SerializeField, Range(0f, 1f)] private float _uiVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float _countdownVolume = 0.9f;
        [SerializeField, Range(0f, 1f)] private float _resultVolume = 0.9f;
        [SerializeField, Range(0f, 1f), Tooltip("Scales the final GO cue (TutorialPopupOpen clip slot).")]
        private float _countdownGoClipVolumeScale = 0.6f;

        private GameStateManager _gameStateSubscribed;
        private bool _resultSubscribed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (_oneShot == null)
            {
                _oneShot = gameObject.AddComponent<AudioSource>();
                _oneShot.playOnAwake = false;
                _oneShot.spatialBlend = 0f;
            }

            if (_loop == null)
            {
                var loopGo = new GameObject("AmbienceLoop");
                loopGo.transform.SetParent(transform, false);
                _loop = loopGo.AddComponent<AudioSource>();
                _loop.playOnAwake = false;
                _loop.loop = true;
                _loop.spatialBlend = 0f;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            StartCoroutine(WaitForGameStateAndSubscribe());
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnsubscribeGameState();
            UnsubscribeResultOverlay();
            if (Instance == this) Instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnsubscribeGameState();
            StartCoroutine(WaitForGameStateAndSubscribe());
            // UI may build the same frame as load; register menu clicks next frame so dynamic buttons are included.
            StartCoroutine(RegisterMenuClicksAfterSceneLoad());
        }

        private IEnumerator RegisterMenuClicksAfterSceneLoad()
        {
            yield return null;
            MenuClickSoundHook.RegisterAllButtonsInScene();
        }

        private IEnumerator WaitForGameStateAndSubscribe()
        {
            float waited = 0f;
            const float timeout = 30f;
            while (waited < timeout)
            {
                if (GameStateManager.Instance != null)
                {
                    SubscribeGameState(GameStateManager.Instance);
                    yield break;
                }

                waited += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void SubscribeGameState(GameStateManager gsm)
        {
            if (gsm == null || _gameStateSubscribed == gsm) return;
            UnsubscribeGameState();

            gsm.OnCountdownTick += HandleCountdownTick;
            gsm.OnStateChanged += HandleStateChanged;
            _gameStateSubscribed = gsm;

            if (!_resultSubscribed)
            {
                RaceResultOverlay.OnResultOverlayShowing += HandleResultOverlayShowing;
                _resultSubscribed = true;
            }
        }

        private void UnsubscribeGameState()
        {
            if (_gameStateSubscribed != null)
            {
                _gameStateSubscribed.OnCountdownTick -= HandleCountdownTick;
                _gameStateSubscribed.OnStateChanged -= HandleStateChanged;
                _gameStateSubscribed = null;
            }
        }

        private void UnsubscribeResultOverlay()
        {
            if (!_resultSubscribed) return;
            RaceResultOverlay.OnResultOverlayShowing -= HandleResultOverlayShowing;
            _resultSubscribed = false;
        }

        private void HandleCountdownTick(int remaining)
        {
            if (_library == null) return;
            if (remaining > 0)
                PlayOneShot(_library.CountdownTickNumber, _countdownVolume);
            else
                // Intentionally the "tutorial open" library slot — swapped with Tutorial state (see below).
                PlayOneShot(_library.TutorialPopupOpen, _countdownVolume * _countdownGoClipVolumeScale);
        }

        private void HandleStateChanged(GameState state)
        {
            if (_library == null) return;

            switch (state)
            {
                case GameState.Calibration:
                    PlayOneShot(_library.CalibrationStart, _uiVolume);
                    break;
                case GameState.Tutorial:
                    // Intentionally the "countdown GO" library slot — swapped with final countdown cue (see HandleCountdownTick).
                    PlayOneShot(_library.CountdownGo, _uiVolume);
                    break;
                case GameState.Celebration:
                    PlayCelebrationStinger();
                    break;
            }
        }

        /// <summary>Uses MinigameSfxProfile GoalComplete or Success when assigned; otherwise global CelebrationStinger.</summary>
        private void PlayCelebrationStinger()
        {
            if (_library == null) return;
            var prof = MinigameManager.Instance?.SelectedDefinition?.MinigameSfxProfile;
            if (prof != null && prof.GoalComplete != null)
                PlayOneShot(prof.GoalComplete, _resultVolume);
            else if (prof != null && prof.Success != null)
                PlayOneShot(prof.Success, _resultVolume);
            else
                PlayOneShot(_library.CelebrationStinger, _resultVolume);
        }

        /// <summary>
        /// Adds <see cref="MenuClickSoundHook"/> to every Button under <paramref name="root"/> (skips if already hooked).
        /// Call after dynamic UI is built (e.g. settings panel, level cards).
        /// </summary>
        public void RegisterMenuClickSoundForHierarchy(Transform root) =>
            MenuClickSoundHook.RegisterHierarchy(root);

        private void HandleResultOverlayShowing()
        {
            if (_library == null) return;

            PlayOneShot(_library.ResultScreenAppear, _resultVolume);

            var mgr = MinigameManager.Instance;
            string pb = mgr?.ActiveMinigame?.GetPersonalBestMessage() ?? "";
            if (!string.IsNullOrEmpty(pb))
                PlayOneShot(_library.ResultPersonalBest, _resultVolume * 0.95f);
        }

        /// <summary>Universal hook — minigames can call for profile clips.</summary>
        public void PlayClip(AudioClip clip, float volumeScale = 1f)
        {
            PlayOneShot(clip, volumeScale);
        }

        /// <summary>Default sound for all main-menu / settings / UI buttons (assign one clip on SfxLibrary → Ui Button Confirm).</summary>
        public void PlayUiMenuClick() => PlayUiConfirm();

        public void PlayUiConfirm() => PlayOneShot(_library != null ? _library.UiButtonConfirm : null, _uiVolume);
        public void PlayUiCancel() => PlayOneShot(_library != null ? _library.UiButtonCancel : null, _uiVolume);
        public void PlayUiHover() => PlayOneShot(_library != null ? _library.UiButtonHover : null, _uiVolume * 0.7f);
        public void PlayTutorialContinue() =>
            PlayOneShot(_library != null ? _library.TutorialPopupContinue : null, _uiVolume);

        public void PlayCalibrationComplete() =>
            PlayOneShot(_library != null ? _library.CalibrationComplete : null, _uiVolume);

        public void PlayMinigameGameplayStart(MinigameSfxProfile profile)
        {
            if (profile == null) return;
            PlayOneShot(profile.GameplayStart, 1f);
        }

        public void SetAmbienceLoop(MinigameSfxProfile profile)
        {
            if (_loop == null) return;
            AudioClip loop = profile != null ? profile.GameplayAmbienceLoop : null;
            if (loop == null)
            {
                _loop.Stop();
                _loop.clip = null;
                return;
            }

            if (_loop.clip == loop && _loop.isPlaying) return;
            _loop.clip = loop;
            _loop.volume = 0.55f * SfxBusVolume;
            _loop.Play();
        }

        public void StopAmbienceLoop()
        {
            if (_loop == null) return;
            _loop.Stop();
            _loop.clip = null;
        }

        /// <summary>0–1 from Settings; multiplied with <see cref="AudioListener.volume"/> (master).</summary>
        static float SfxBusVolume => PlayerPrefs.GetFloat(PrefKeySfxVolume, 0.8f);

        private void PlayOneShot(AudioClip clip, float volumeScale)
        {
            if (clip == null || _oneShot == null) return;
            float v = Mathf.Clamp01(volumeScale * SfxBusVolume);
            _oneShot.PlayOneShot(clip, v);
        }
    }
}
