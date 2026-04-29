using UnityEngine;

namespace Breathe.Audio
{
    // Global one-shots and short loops. Assign clips from Assets/Audio/SFX/Global/<CueName>/.
    [CreateAssetMenu(fileName = "SfxLibrary", menuName = "Breathe/Audio/SFX Library")]
    public sealed class SfxLibrary : ScriptableObject
    {
        [Header("UI — Main menu & settings")]
        [SerializeField, Tooltip("Button press / confirm — played on any Button click via MenuClickSoundHook (global).")]
        private AudioClip _uiButtonConfirm;
        [SerializeField] private AudioClip _uiButtonCancel;
        [SerializeField, Tooltip("Button hover — played on pointer enter via MenuClickSoundHook (global).")]
        private AudioClip _uiButtonHover;
        [SerializeField] private AudioClip _uiPanelOpen;
        [SerializeField] private AudioClip _uiPanelClose;

        [Header("Countdown (race / session start)")]
        [SerializeField] private AudioClip _countdownTickNumber;
        [SerializeField, Tooltip("Library slot: played when HOW TO PLAY (tutorial + input selection) opens. Final countdown \"GO\" uses Tutorial Popup Open slot — see SfxPlayer.")]
        private AudioClip _countdownGo;

        [Header("Tutorial & calibration")]
        [SerializeField, Tooltip("Library slot: played on final countdown \"GO\". Tutorial open uses Countdown Go slot — see SfxPlayer.")]
        private AudioClip _tutorialPopupOpen;
        [SerializeField] private AudioClip _tutorialPopupContinue;
        [SerializeField] private AudioClip _calibrationStart;
        [SerializeField] private AudioClip _calibrationComplete;

        [Header("Results & celebration")]
        [SerializeField] private AudioClip _resultScreenAppear;
        [SerializeField] private AudioClip _resultPersonalBest;
        [SerializeField, Tooltip("Win stinger — one clip for every minigame for now.")]
        private AudioClip _celebrationStinger;
        [SerializeField] private AudioClip _resultContinue;

        public AudioClip UiButtonConfirm => _uiButtonConfirm;
        public AudioClip UiButtonCancel => _uiButtonCancel;
        public AudioClip UiButtonHover => _uiButtonHover;
        public AudioClip UiPanelOpen => _uiPanelOpen;
        public AudioClip UiPanelClose => _uiPanelClose;

        public AudioClip CountdownTickNumber => _countdownTickNumber;
        public AudioClip CountdownGo => _countdownGo;

        public AudioClip TutorialPopupOpen => _tutorialPopupOpen;
        public AudioClip TutorialPopupContinue => _tutorialPopupContinue;
        public AudioClip CalibrationStart => _calibrationStart;
        public AudioClip CalibrationComplete => _calibrationComplete;

        public AudioClip ResultScreenAppear => _resultScreenAppear;
        public AudioClip ResultPersonalBest => _resultPersonalBest;
        public AudioClip CelebrationStinger => _celebrationStinger;
        public AudioClip ResultContinue => _resultContinue;
    }
}
