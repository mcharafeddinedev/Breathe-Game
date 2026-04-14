using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Breathe.Audio;
using Breathe.Input;
using Breathe.Gameplay;

namespace Breathe.UI
{
    // Two-phase mic/fan calibration wizard.
    // Phase 1 captures the ambient noise baseline (3 s of normal breathing).
    // Phase 2 captures max blow strength (2 s of peak effort).
    public sealed class CalibrationUI : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI promptText;
        [SerializeField] private TextMeshProUGUI countdownText;

        [SerializeField, Tooltip("Filled image that visualises countdown progress (0 → 1).")]
        private Image progressBar;

        [SerializeField] private Button skipButton;

        [Header("Timing")]
        [SerializeField] private float baselineDuration = 3f;
        [SerializeField] private float maxDuration = 2f;
        [SerializeField] private float phasePause = 0.75f;

        private MicBreathInput _micInput;
        private Coroutine _calibrationRoutine;

        private void Start()
        {
            if (skipButton != null)
                skipButton.onClick.AddListener(OnSkip);

            ResetVisuals();
        }

        private void OnDestroy()
        {
            if (skipButton != null)
                skipButton.onClick.RemoveListener(OnSkip);
        }

        // Locates the active MicBreathInput and runs the capture coroutine.
        public void StartCalibration()
        {
            _micInput = FindAnyObjectByType<MicBreathInput>();

            if (_micInput == null)
            {
                Debug.LogWarning("[CalibrationUI] MicBreathInput not found — skipping to play.");
                TransitionToPlaying();
                return;
            }

            if (_calibrationRoutine != null)
                StopCoroutine(_calibrationRoutine);

            _calibrationRoutine = StartCoroutine(CalibrationSequence());
        }

        private IEnumerator CalibrationSequence()
        {
            // Phase 1 — baseline capture
            SetPrompt("Breathe normally for a few seconds...");
            yield return CountdownPhase(baselineDuration);
            _micInput.SetBaseline();

            SetPrompt("Baseline captured!");
            yield return new WaitForSeconds(phasePause);

            // Phase 2 — maximum blow capture
            SetPrompt("Now blow as HARD as you can!");
            yield return CountdownPhase(maxDuration);
            _micInput.SetMax();

            SetPrompt("Calibration complete!");
            yield return new WaitForSeconds(phasePause);

            TransitionToPlaying();
        }

        // Runs a visual countdown, filling the progress bar and updating text each frame.
        private IEnumerator CountdownPhase(float duration)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float remaining = Mathf.Max(0f, duration - elapsed);

                if (progressBar != null)
                    progressBar.fillAmount = t;

                if (countdownText != null)
                    countdownText.text = Mathf.CeilToInt(remaining).ToString();

                yield return null;
            }

            if (progressBar != null)
                progressBar.fillAmount = 1f;

            if (countdownText != null)
                countdownText.text = "0";
        }

        private void OnSkip()
        {
            SfxPlayer.Instance?.PlayUiMenuClick();
            if (_calibrationRoutine != null)
            {
                StopCoroutine(_calibrationRoutine);
                _calibrationRoutine = null;
            }

            TransitionToPlaying();
        }

        private void TransitionToPlaying()
        {
            SfxPlayer.Instance?.PlayCalibrationComplete();
            GameStateManager gsm = GameStateManager.Instance;
            if (gsm != null)
                gsm.TransitionTo(GameState.Playing);
        }

        private void SetPrompt(string message)
        {
            if (promptText != null)
                promptText.text = message;
        }

        private void ResetVisuals()
        {
            if (promptText != null)
                promptText.text = string.Empty;

            if (countdownText != null)
                countdownText.text = string.Empty;

            if (progressBar != null)
                progressBar.fillAmount = 0f;
        }
    }
}
