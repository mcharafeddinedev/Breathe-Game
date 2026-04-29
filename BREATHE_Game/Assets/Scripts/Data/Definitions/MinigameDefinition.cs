using UnityEngine;

namespace Breathe.Data
{
    // Describes a single minigame for the level-select screen and MinigameManager.
    // Create one asset per minigame (SAILBOAT, BALLOON, STARGAZE, etc.)
    [CreateAssetMenu(fileName = "NewMinigame", menuName = "Breathe/Minigame Definition")]
    public class MinigameDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Unique ID used for PlayerPrefs keys and lookups.")]
        private string _minigameId = "sailboat";
        [SerializeField] private string _displayName = "SAILBOAT";
        [SerializeField, TextArea(2, 4)] private string _description = "BLOW  STEADILY  TO  FILL  YOUR  SAILS  AND  RACE  ACROSS  THE  FINISH  LINE";
        [SerializeField, Tooltip("Level-select card art. Import a PNG/JPG capture as Sprite (Texture Type: Sprite 2D), assign here — the card uses it full-bleed with a text backdrop for readability.")]
        private Sprite _thumbnail;
        [SerializeField] private string _breathPattern = "SUSTAINED  BLOW";
        [SerializeField] private bool _isUnlocked = true;
        [SerializeField, Tooltip("If false, this minigame stays in the roster for lookups but is hidden from level select.")]
        private bool _includeInLevelSelect = true;

        [Header("Level Select Card")]
        [SerializeField, Tooltip("Placeholder card color shown when no thumbnail sprite is assigned.")]
        private Color _cardColor = new Color(0.56f, 0.93f, 0.56f, 1f);

        [Header("Scene")]
        [SerializeField, Tooltip("Exact name of the scene in Build Settings (e.g. \"SAILBOAT\").")]
        private string _sceneName = "SAILBOAT";

        [Header("Countdown")]
        [SerializeField, Tooltip("Text shown instead of 'GO' at end of countdown.")]
        private string _countdownGoText = "GO";
        [SerializeField, Tooltip("Screenspace px: shift the final phrase downward. Use for long GO text so it clears centered HUD hints.")]
        private float _countdownGoVerticalOffsetPx = 0f;
        [SerializeField, Tooltip("Seconds of grace after countdown before breath input is evaluated. " +
            "Prevents false reads from device startup lag.")]
        private float _postCountdownBuffer = 0f;

        [Header("Audio (optional)")]
        [SerializeField, Tooltip("Per-mode SFX; assign clips in Assets/Audio/SFX/Minigames/<YourGame>/.")]
        private MinigameSfxProfile _minigameSfxProfile;

        [Header("Tutorial")]
        [SerializeField] private string _tutorialTitle = "HOW  TO  PLAY";
        [SerializeField, TextArea(3, 6)] private string _tutorialInstruction = "Race to the finish by filling your sails with steady breath.\n\nBreathe into or blow onto the device (long, steady exhales\u2014not quick puffs) to play the game!";
        [SerializeField, TextArea(1, 3)] private string _tutorialTip = "KEEP  BREATHING  TO  MAINTAIN  YOUR  SPEED";

        [Header("Spin-Down Detection")]
        [SerializeField, Tooltip("How far power must drop within the window to trigger snap-to-zero. " +
            "Lower = more aggressive. Set very high (e.g. 999) to disable.")]
        private float _spinDownThreshold = 0.12f;
        [SerializeField, Tooltip("Time window (seconds) over which the drop is measured.")]
        private float _spinDownWindow = 1.0f;
        [SerializeField, Tooltip("How much raw intensity must rise above the trough to resume. " +
            "Lower = resumes faster after spin-down.")]
        private float _spinDownResumeDelta = 0.06f;

        public string MinigameId => _minigameId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Thumbnail => _thumbnail;
        public string BreathPattern => _breathPattern;
        public bool IsUnlocked => _isUnlocked;
        public bool IncludeInLevelSelect => _includeInLevelSelect;
        public Color CardColor => _cardColor;
        public string SceneName => _sceneName;
        public string CountdownGoText => _countdownGoText;
        public float CountdownGoVerticalOffsetPx => _countdownGoVerticalOffsetPx;
        public float PostCountdownBuffer => _postCountdownBuffer;
        public string TutorialTitle => _tutorialTitle;
        public string TutorialInstruction => _tutorialInstruction;
        public string TutorialTip => _tutorialTip;
        public float SpinDownThreshold => _spinDownThreshold;
        public float SpinDownWindow => _spinDownWindow;
        public float SpinDownResumeDelta => _spinDownResumeDelta;
        public MinigameSfxProfile MinigameSfxProfile => _minigameSfxProfile;
    }
}
