using System.Collections;
using UnityEngine;

namespace Breathe.Utility
{
    // Drop-in component for automatic pop-in/pop-out scale animation.
    // Attach to any UI element — bounces in from zero on enable.
    // For one-off or code-driven use, see the static helpers on UIPopAnimation.
    [AddComponentMenu("Breathe/UI Pop Animator")]
    public class UIPopAnimator : MonoBehaviour
    {
        [Header("Pop In")]
        [SerializeField, Tooltip("Duration of the scale-up bounce in seconds.")]
        private float _popInDuration = 0.35f;

        [SerializeField, Tooltip("Overshoot intensity. Higher = bouncier. 0 = smooth ease only.")]
        private float _overshoot = 1.70158f;

        [SerializeField]
        private float _startDelay;

        [SerializeField]
        private bool _playOnEnable = true;

        [Header("Pop Out")]
        [SerializeField, Tooltip("Duration of the scale-down in seconds.")]
        private float _popOutDuration = 0.2f;

        [SerializeField, Tooltip("Deactivate the GameObject after pop-out completes.")]
        private bool _deactivateAfterPopOut;

        private Vector3 _originalScale = Vector3.one;
        private Coroutine _activeAnimation;

        // The resting scale this element returns to after pop-in.
        public Vector3 OriginalScale
        {
            get => _originalScale;
            set => _originalScale = value;
        }

        public float StartDelay
        {
            get => _startDelay;
            set => _startDelay = value;
        }

        private void Awake()
        {
            _originalScale = transform.localScale;
        }

        private void OnEnable()
        {
            if (_playOnEnable)
                PlayPopIn();
        }

        private void OnDisable()
        {
            KillActive();
            transform.localScale = _originalScale;
        }

        public void PlayPopIn()
        {
            KillActive();
            _activeAnimation = StartCoroutine(
                UIPopAnimation.PopIn(
                    transform,
                    _popInDuration,
                    _originalScale,
                    _overshoot,
                    _startDelay,
                    () => _activeAnimation = null));
        }

        public void PlayPopOut()
        {
            KillActive();
            _activeAnimation = StartCoroutine(
                UIPopAnimation.PopOut(
                    transform,
                    _popOutDuration,
                    deactivateOnComplete: _deactivateAfterPopOut,
                    onComplete: () => _activeAnimation = null));
        }

        // Pop-in, hold, then pop-out — good for transient notifications.
        public void PlayPopInHoldOut(float holdDuration = 1f)
        {
            KillActive();
            _activeAnimation = StartCoroutine(
                UIPopAnimation.PopInHoldOut(
                    transform,
                    _popInDuration,
                    holdDuration,
                    _popOutDuration,
                    _originalScale,
                    _overshoot,
                    _deactivateAfterPopOut,
                    () => _activeAnimation = null));
        }

        public void SnapHidden()
        {
            KillActive();
            transform.localScale = Vector3.zero;
            gameObject.SetActive(false);
        }

        public void SnapVisible()
        {
            KillActive();
            transform.localScale = _originalScale;
        }

        private void KillActive()
        {
            if (_activeAnimation != null)
            {
                StopCoroutine(_activeAnimation);
                _activeAnimation = null;
            }
        }
    }
}
