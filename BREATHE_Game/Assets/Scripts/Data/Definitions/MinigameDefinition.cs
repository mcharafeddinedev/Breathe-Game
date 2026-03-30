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
        [SerializeField] private Sprite _thumbnail;
        [SerializeField] private string _breathPattern = "SUSTAINED  BLOW";
        [SerializeField] private bool _isUnlocked = true;

        [Header("Level Select Card")]
        [SerializeField, Tooltip("Placeholder card color shown when no thumbnail sprite is assigned.")]
        private Color _cardColor = new Color(0.56f, 0.93f, 0.56f, 1f);

        [Header("Scene")]
        [SerializeField, Tooltip("Exact name of the scene in Build Settings (e.g. \"SAILBOAT\").")]
        private string _sceneName = "SAILBOAT";

        [Header("Countdown")]
        [SerializeField, Tooltip("Text shown instead of 'GO' at end of countdown.")]
        private string _countdownGoText = "GO";
        [SerializeField, Tooltip("Seconds of grace after countdown before breath input is evaluated. " +
            "Prevents false reads from device startup lag.")]
        private float _postCountdownBuffer = 0f;

        [Header("Tutorial")]
        [SerializeField] private string _tutorialTitle = "HOW  TO  PLAY";
        [SerializeField, TextArea(3, 6)] private string _tutorialInstruction = "BLOW  STEADILY  INTO  THE  DEVICE  TO  FILL  YOUR  SAILS  AND  MOVE  YOUR  BOAT  FORWARD\n\nLONGER  STEADY  BREATHS  ARE  MORE  EFFECTIVE  THAN  QUICK  PUFFS";
        [SerializeField, TextArea(1, 3)] private string _tutorialTip = "KEEP  BREATHING  TO  MAINTAIN  YOUR  SPEED";

        public string MinigameId => _minigameId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Thumbnail => _thumbnail;
        public string BreathPattern => _breathPattern;
        public bool IsUnlocked => _isUnlocked;
        public Color CardColor => _cardColor;
        public string SceneName => _sceneName;
        public string CountdownGoText => _countdownGoText;
        public float PostCountdownBuffer => _postCountdownBuffer;
        public string TutorialTitle => _tutorialTitle;
        public string TutorialInstruction => _tutorialInstruction;
        public string TutorialTip => _tutorialTip;
    }
}
