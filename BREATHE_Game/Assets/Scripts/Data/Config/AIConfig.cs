using UnityEngine;

namespace Breathe.Data
{
    // Tuning data for AI opponent boats.
    // Create: Assets > Create > Breathe > AI Config
    [CreateAssetMenu(fileName = "AIConfig", menuName = "Breathe/AI Config")]
    public class AIConfig : ScriptableObject
    {
        [Header("Speed Variation")]
        [SerializeField, Tooltip("Amplitude of the sine wave speed oscillation.")]
        private float _speedVariationAmplitude = 0.3f;
        [SerializeField, Tooltip("Frequency (Hz) of the speed oscillation.")]
        private float _speedVariationFrequency = 0.5f;

        [Header("Rubber-Banding")]
        [SerializeField, Range(0f, 1f), Tooltip("How hard the AI corrects pace relative to the player.")]
        private float _rubberBandingStrength = 0.5f;

        [Header("Stun")]
        [SerializeField, Tooltip("How long the AI is stunned after hitting an obstacle.")]
        private float _stunDuration = 2.0f;

        [Header("Finish Behaviour")]
        [SerializeField] private float _finishSlowdownDistance = 10f;
        [SerializeField] private float _finishSlowdownRate = 0.5f;

        [Header("Competitive Win")]
        [SerializeField, Tooltip("Let AI potentially win if the player barely participates.")]
        private bool _allowCompetitiveWin = true;
        [SerializeField, Range(0f, 1f)] private float _underperformThreshold = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _competitiveWinChance = 0.25f;
        [SerializeField, Range(0.5f, 0.95f)] private float _competitiveWinProgressThreshold = 0.85f;
        [SerializeField] private float _minLeadForCompetitiveWin = 3f;

        [Header("Visuals")]
        [SerializeField] private Color[] _aiBoatColors = { Color.red, Color.blue };

        public float SpeedVariationAmplitude => _speedVariationAmplitude;
        public float SpeedVariationFrequency => _speedVariationFrequency;
        public float RubberBandingStrength => _rubberBandingStrength;
        public float StunDuration => _stunDuration;
        public float FinishSlowdownDistance => _finishSlowdownDistance;
        public float FinishSlowdownRate => _finishSlowdownRate;
        public bool AllowCompetitiveWin => _allowCompetitiveWin;
        public float UnderperformThreshold => _underperformThreshold;
        public float CompetitiveWinChance => _competitiveWinChance;
        public float CompetitiveWinProgressThreshold => _competitiveWinProgressThreshold;
        public float MinLeadForCompetitiveWin => _minLeadForCompetitiveWin;
        public Color[] AIBoatColors => _aiBoatColors;
    }
}
