using UnityEngine;

namespace Breathe.Gameplay
{
    // Triggers a confetti burst at both ends of the finish line when the player crosses it.
    // Delegates to CelebrationConfettiEffect so the same burst can be reused elsewhere.
    public class FinishLineCelebrationTrigger : MonoBehaviour
    {
        [SerializeField, Tooltip("Auto-found on this GameObject if not set.")]
        private CelebrationConfettiEffect _confettiEffect;

        [SerializeField, Tooltip("Auto-found if not set.")]
        private CourseManager _courseManager;

        private void Awake()
        {
            if (_confettiEffect == null)
                _confettiEffect = GetComponent<CelebrationConfettiEffect>();
            if (_courseManager == null)
                _courseManager = FindAnyObjectByType<CourseManager>();
        }

        private void OnEnable()
        {
            if (_courseManager != null)
                _courseManager.OnPlayerFinished += HandlePlayerFinished;
        }

        private void OnDisable()
        {
            if (_courseManager != null)
                _courseManager.OnPlayerFinished -= HandlePlayerFinished;
        }

        private void HandlePlayerFinished(float raceTime)
        {
            if (_confettiEffect == null) return;

            Vector3 center = _courseManager.FinishLine != null
                ? _courseManager.FinishLine.position
                : _courseManager.transform.position;

            float halfWidth = _courseManager.FinishLineHalfWidth;
            Vector3 leftEnd = center + Vector3.left * halfWidth;
            Vector3 rightEnd = center + Vector3.right * halfWidth;

            _confettiEffect.Play(leftEnd, singlePoint: true);
            _confettiEffect.Play(rightEnd, singlePoint: true);
        }
    }
}
