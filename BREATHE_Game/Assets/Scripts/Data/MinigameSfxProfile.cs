using UnityEngine;

namespace Breathe.Data
{
    // Per-minigame cues. Create one asset per mode (e.g. BalloonSfx) and assign on MinigameDefinition.
    // Drop .wav/.ogg into matching folders under Assets/Audio/SFX/Minigames/<GameName>/.
    [CreateAssetMenu(fileName = "MinigameSfx", menuName = "Breathe/Audio/Minigame SFX Profile")]
    public sealed class MinigameSfxProfile : ScriptableObject
    {
        [Header("Core")]
        [SerializeField] private AudioClip _gameplayStart;
        [SerializeField] private AudioClip _gameplayAmbienceLoop;
        [SerializeField] private AudioClip _timeWarning;
        [SerializeField] private AudioClip _success;
        [SerializeField] private AudioClip _goalComplete;

        [Header("Interactions (map to your mode)")]
        [SerializeField] private AudioClip _primaryAction;
        [SerializeField] private AudioClip _secondaryAction;
        [SerializeField] private AudioClip _tertiaryAction;
        [SerializeField] private AudioClip _specialEvent;

        public AudioClip GameplayStart => _gameplayStart;
        public AudioClip GameplayAmbienceLoop => _gameplayAmbienceLoop;
        public AudioClip TimeWarning => _timeWarning;
        public AudioClip Success => _success;
        public AudioClip GoalComplete => _goalComplete;

        public AudioClip PrimaryAction => _primaryAction;
        public AudioClip SecondaryAction => _secondaryAction;
        public AudioClip TertiaryAction => _tertiaryAction;
        public AudioClip SpecialEvent => _specialEvent;
    }
}
