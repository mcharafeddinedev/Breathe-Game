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
        [SerializeField, TextArea(2, 4)] private string _description = "Blow steadily to fill your sails and race across the finish line!";
        [SerializeField] private Sprite _thumbnail;
        [SerializeField] private string _breathPattern = "Sustained Blow";
        [SerializeField] private bool _isUnlocked = true;

        [Header("Tutorial")]
        [SerializeField] private string _tutorialTitle = "HOW TO PLAY";
        [SerializeField, TextArea(3, 6)] private string _tutorialInstruction = "Blow steadily into the device to fill your sails and move your boat forward!\n\nLonger, steady breaths are more effective than quick puffs.";
        [SerializeField, TextArea(1, 3)] private string _tutorialTip = "Keep breathing to maintain your speed!";

        public string MinigameId => _minigameId;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Thumbnail => _thumbnail;
        public string BreathPattern => _breathPattern;
        public bool IsUnlocked => _isUnlocked;
        public string TutorialTitle => _tutorialTitle;
        public string TutorialInstruction => _tutorialInstruction;
        public string TutorialTip => _tutorialTip;
    }
}
