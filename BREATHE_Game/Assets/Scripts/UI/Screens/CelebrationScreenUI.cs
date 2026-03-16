using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Breathe.Data;
using Breathe.Gameplay;
using Breathe.Utility;

namespace Breathe.UI
{
    // End-of-minigame celebration panel.
    // Generic path: receives MinigameStat[] from the active IMinigame.
    // Sailboat legacy path: reads ScoreManager directly when no IMinigame is registered.
    // All elements animate in with a staggered bouncy pop-in.
    public sealed class CelebrationScreenUI : MonoBehaviour
    {
        [Header("Generic Stat Display")]
        [SerializeField, Tooltip("Parent for dynamically spawned stat rows.")]
        private Transform _statRowParent;

        [SerializeField, Tooltip("Prefab with two TMP children: Label and Value.")]
        private GameObject _statRowPrefab;

        [SerializeField] private TextMeshProUGUI _celebrationTitle;
        [SerializeField] private TextMeshProUGUI _personalBestText;

        [Header("Legacy SAILBOAT Labels (used when no IMinigame is active)")]
        [SerializeField] private TextMeshProUGUI sailTimeText;
        [SerializeField] private TextMeshProUGUI strongestGustText;
        [SerializeField] private TextMeshProUGUI longestWindText;
        [SerializeField] private TextMeshProUGUI courseTimeText;
        [SerializeField] private TextMeshProUGUI windZonesText;

        [Header("Navigation")]
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button levelSelectButton;
        [SerializeField] private Button mainMenuButton;

        [Header("Effects")]
        [SerializeField] private ParticleSystem confettiParticles;

        [Header("Pop Animation")]
        [SerializeField] private float _popInDuration = 0.4f;
        [SerializeField] private float _staggerDelay = 0.1f;

        [SerializeField, Tooltip("Bounce overshoot intensity.")]
        private float _overshoot = 1.70158f;

        private ScoreManager _scoreManager;

        private void Start()
        {
            _scoreManager = ScoreManager.Instance;

            if (playAgainButton != null)
                playAgainButton.onClick.AddListener(OnPlayAgain);
            if (levelSelectButton != null)
                levelSelectButton.onClick.AddListener(OnLevelSelect);
            if (mainMenuButton != null)
                mainMenuButton.onClick.AddListener(OnMainMenu);

            Hide();
        }

        private void OnDestroy()
        {
            if (playAgainButton != null)
                playAgainButton.onClick.RemoveListener(OnPlayAgain);
            if (levelSelectButton != null)
                levelSelectButton.onClick.RemoveListener(OnLevelSelect);
            if (mainMenuButton != null)
                mainMenuButton.onClick.RemoveListener(OnMainMenu);
        }

        // Checks for an active IMinigame (generic path), falls back to ScoreManager (legacy).
        public void Show()
        {
            _scoreManager = ScoreManager.Instance;
            var mgr = MinigameManager.Instance;

            if (mgr != null && mgr.ActiveMinigame != null)
                ShowGeneric(mgr.ActiveMinigame);
            else if (_scoreManager != null)
                ShowSailboatLegacy();

            if (confettiParticles != null)
                confettiParticles.Play();

            gameObject.SetActive(true);
            StartCoroutine(StaggeredRevealRoutine());
        }

        private void ShowGeneric(IMinigame minigame)
        {
            if (_celebrationTitle != null)
                _celebrationTitle.text = minigame.GetCelebrationTitle();
            if (_personalBestText != null)
                _personalBestText.text = minigame.GetPersonalBestMessage();

            SetLegacyLabelsActive(false);

            MinigameStat[] stats = minigame.GetEndStats();
            if (stats == null) return;

            ClearDynamicRows();
            foreach (var stat in stats)
                SpawnStatRow(stat);
        }

        private void ShowSailboatLegacy()
        {
            if (_celebrationTitle != null)
                _celebrationTitle.text = "GREAT SAILING!";

            SetLegacyLabelsActive(true);

            if (sailTimeText != null)
                sailTimeText.text = $"Sail Time: {_scoreManager.TotalBreathTime:F1}s!";
            if (strongestGustText != null)
                strongestGustText.text = $"Strongest Gust: {GetWindScaleLabel(_scoreManager.PeakBreathIntensity)}";
            if (longestWindText != null)
                longestWindText.text = $"Longest Steady Wind: {_scoreManager.LongestSustainedBlow:F1}s!";
            if (courseTimeText != null)
                courseTimeText.text = $"Course Time: {_scoreManager.CourseTime:F1}s!";
            if (windZonesText != null)
                windZonesText.text = $"Wind Zones Conquered: {_scoreManager.WindZonesConquered}!";
            if (_personalBestText != null)
                _personalBestText.text = BuildSailboatPBText();
        }

        // Animates title → stat rows → PB text → buttons with staggered bouncy pop-in.
        private IEnumerator StaggeredRevealRoutine()
        {
            float delay = 0f;

            if (_celebrationTitle != null)
            {
                StartCoroutine(UIPopAnimation.PopIn(
                    _celebrationTitle.transform, _popInDuration, delay: delay, overshoot: _overshoot));
                delay += _staggerDelay;
            }

            if (_statRowParent != null)
            {
                for (int i = 0; i < _statRowParent.childCount; i++)
                {
                    Transform row = _statRowParent.GetChild(i);
                    StartCoroutine(UIPopAnimation.PopIn(
                        row, _popInDuration, delay: delay, overshoot: _overshoot));
                    delay += _staggerDelay;
                }
            }

            TextMeshProUGUI[] legacyLabels = { sailTimeText, strongestGustText, longestWindText, courseTimeText, windZonesText };
            foreach (var label in legacyLabels)
            {
                if (label != null && label.gameObject.activeSelf)
                {
                    StartCoroutine(UIPopAnimation.PopIn(
                        label.transform, _popInDuration, delay: delay, overshoot: _overshoot));
                    delay += _staggerDelay;
                }
            }

            if (_personalBestText != null)
            {
                StartCoroutine(UIPopAnimation.PopIn(
                    _personalBestText.transform, _popInDuration, delay: delay, overshoot: _overshoot));
                delay += _staggerDelay * 2f;
            }

            Button[] navButtons = { playAgainButton, levelSelectButton, mainMenuButton };
            foreach (var btn in navButtons)
            {
                if (btn != null)
                {
                    StartCoroutine(UIPopAnimation.PopIn(
                        btn.transform, _popInDuration, delay: delay, overshoot: _overshoot * 0.6f));
                    delay += _staggerDelay;
                }
            }

            yield break;
        }

        public void Hide()
        {
            if (confettiParticles != null)
                confettiParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ClearDynamicRows();
            gameObject.SetActive(false);
        }

        private void SpawnStatRow(MinigameStat stat)
        {
            if (_statRowPrefab == null || _statRowParent == null) return;

            GameObject row = Instantiate(_statRowPrefab, _statRowParent);
            row.transform.localScale = Vector3.zero;

            var labels = row.GetComponentsInChildren<TextMeshProUGUI>();
            if (labels.Length >= 2)
            {
                labels[0].text = stat.Label;
                labels[1].text = stat.Value;
                if (stat.IsPersonalBest)
                    labels[1].color = new Color(1f, 0.84f, 0f);
            }
            else if (labels.Length == 1)
            {
                labels[0].text = $"{stat.Label}: {stat.Value}";
            }
        }

        private void ClearDynamicRows()
        {
            if (_statRowParent == null) return;
            for (int i = _statRowParent.childCount - 1; i >= 0; i--)
                Destroy(_statRowParent.GetChild(i).gameObject);
        }

        private void SetLegacyLabelsActive(bool active)
        {
            if (sailTimeText != null) sailTimeText.gameObject.SetActive(active);
            if (strongestGustText != null) strongestGustText.gameObject.SetActive(active);
            if (longestWindText != null) longestWindText.gameObject.SetActive(active);
            if (courseTimeText != null) courseTimeText.gameObject.SetActive(active);
            if (windZonesText != null) windZonesText.gameObject.SetActive(active);
        }

        private static string GetWindScaleLabel(float peakIntensity)
        {
            if (peakIntensity < 0.15f) return "Breeze";
            if (peakIntensity < 0.30f) return "Strong Wind";
            if (peakIntensity < 0.50f) return "Gale";
            return "HURRICANE!";
        }

        private string BuildSailboatPBText()
        {
            _scoreManager.LoadPersonalBests();

            if (_scoreManager.IsNewPersonalBest("CourseTime"))
            {
                float improvement = _scoreManager.PBCourseTime - _scoreManager.CourseTime;
                if (improvement > 0.1f)
                    return $"New personal best! {improvement:F1}s faster!";
                return "New personal best!";
            }

            if (_scoreManager.IsNewPersonalBest("BreathTime"))
                return "New longest sail time! Keep it up!";

            if (_scoreManager.IsNewPersonalBest("LongestBlow"))
                return "New longest sustained blow! Great lungs!";

            return "Great race! Try again to beat your personal best!";
        }

        private void OnPlayAgain()
        {
            Hide();
            GameStateManager gsm = GameStateManager.Instance;
            if (gsm != null)
                gsm.TransitionTo(GameState.Playing);
        }

        private void OnLevelSelect()
        {
            Hide();
            GameStateManager gsm = GameStateManager.Instance;
            if (gsm != null)
                gsm.TransitionTo(GameState.LevelSelect);
        }

        private void OnMainMenu()
        {
            Hide();
            GameStateManager gsm = GameStateManager.Instance;
            if (gsm != null)
                gsm.TransitionTo(GameState.MainMenu);
        }
    }
}
