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

        [Header("Mix")]
        [SerializeField, Range(0f, 2f), Tooltip("Multiplies SfxPlayer ambience bed volume for this minigame (default 1). Set below 1 to duck loud loops.")]
        private float _ambienceLoopVolumeScale = 1f;
        [SerializeField, Range(0.85f, 1.35f), Tooltip("AudioSource playback pitch for the ambience loop (Unity: 1 = original pitch). Tweaks beds that share one clip.")]
        private float _ambienceLoopPitch = 1f;

        public AudioClip GameplayStart => _gameplayStart;
        public AudioClip GameplayAmbienceLoop => _gameplayAmbienceLoop;
        public AudioClip TimeWarning => _timeWarning;
        public AudioClip Success => _success;
        public AudioClip GoalComplete => _goalComplete;

        public AudioClip PrimaryAction => _primaryAction;
        public AudioClip SecondaryAction => _secondaryAction;
        public AudioClip TertiaryAction => _tertiaryAction;
        public AudioClip SpecialEvent => _specialEvent;

        /// <summary>Scales gameplay ambience loop gain in <see cref="Breathe.Audio.SfxPlayer.SetAmbienceLoop"/>.</summary>
        public float AmbienceLoopVolumeScale => Mathf.Clamp(_ambienceLoopVolumeScale, 0f, 2f);

        /// <summary>Playback pitch for the ambience loop (see <see cref="AudioSource.pitch"/>).</summary>
        public float AmbienceLoopPitch => Mathf.Clamp(_ambienceLoopPitch, 0.85f, 1.35f);
    }
}
