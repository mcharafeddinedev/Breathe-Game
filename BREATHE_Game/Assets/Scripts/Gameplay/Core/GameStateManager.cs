using System;
using System.Collections;
using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Game flow: MainMenu > LevelSelect > Calibration > Tutorial > Playing > Celebration
    // Quick-play skips LevelSelect, simulated input skips Calibration.
    public enum GameState
    {
        MainMenu,
        LevelSelect,
        Calibration,
        Tutorial,
        Playing,
        Celebration
    }

    // Singleton state machine for high-level game flow.
    // Handles transitions and the pre-race countdown.
    public class GameStateManager : MonoBehaviour
    {
        private static GameStateManager _instance;
        public static GameStateManager Instance { get => _instance; private set => _instance = value; }

        [Header("References")]
        [SerializeField] private CourseManager _courseManager;
        [SerializeField] private ScoreManager _scoreManager;

        [Header("Countdown")]
        [SerializeField] private float _countdownStepDuration = 1f;

        private GameState _currentState = GameState.MainMenu;
        private Coroutine _countdownCoroutine;

        public GameState CurrentState => _currentState;

        public event Action<GameState> OnStateChanged;
        public event Action<int> OnCountdownTick;   // 3, 2, 1, then 0 = "Go!"
        public event Action OnTutorialStarted;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public void TransitionTo(GameState state)
        {
            if (_currentState == state) return;
            Debug.Log($"[GameState] {_currentState} → {state}");

            ExitState(_currentState);
            _currentState = state;
            EnterState(_currentState);
            OnStateChanged?.Invoke(_currentState);
        }

        private void EnterState(GameState state)
        {
            switch (state)
            {
                case GameState.MainMenu:
                case GameState.LevelSelect:
                case GameState.Calibration:
                    Time.timeScale = 1f;
                    break;

                case GameState.Tutorial:
                    Time.timeScale = 0f; // paused until player dismisses tutorial
                    OnTutorialStarted?.Invoke();
                    break;

                case GameState.Playing:
                    Time.timeScale = 1f;
                    if (_scoreManager != null) _scoreManager.ResetStats();
                    var mgr = MinigameManager.Instance;
                    if (mgr != null && mgr.ActiveMinigame != null) mgr.ActiveMinigame.OnMinigameStart();
                    _countdownCoroutine = StartCoroutine(PlayCountdownRoutine());
                    break;

                case GameState.Celebration:
                    var mgr2 = MinigameManager.Instance;
                    if (mgr2 != null && mgr2.ActiveMinigame != null) mgr2.ActiveMinigame.OnMinigameEnd();
                    if (_courseManager != null) _courseManager.StopRace();
                    if (_scoreManager != null) _scoreManager.SavePersonalBests();
                    break;
            }
        }

        private void ExitState(GameState state)
        {
            if (state == GameState.Playing && _countdownCoroutine != null)
            {
                StopCoroutine(_countdownCoroutine);
                _countdownCoroutine = null;
            }
        }

        private IEnumerator PlayCountdownRoutine()
        {
            for (int i = 3; i >= 1; i--)
            {
                OnCountdownTick?.Invoke(i);
                yield return new WaitForSecondsRealtime(_countdownStepDuration);
            }
            OnCountdownTick?.Invoke(0);

            float buffer = 0f;
            if (MinigameManager.Instance != null)
            {
                MinigameDefinition def = MinigameManager.Instance.SelectedDefinition;
                if (def != null) buffer = def.PostCountdownBuffer;
            }
            if (buffer > 0f)
                yield return new WaitForSecondsRealtime(buffer);

            if (_courseManager != null) _courseManager.StartRace();
            _countdownCoroutine = null;
        }
    }
}
