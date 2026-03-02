using UnityEngine;

namespace Breathe.Gameplay
{
    /// <summary>
    /// Smooth 2D camera that follows a target transform using <see cref="Vector3.Lerp"/>
    /// in LateUpdate. Maintains a fixed Z offset for orthographic rendering.
    /// </summary>
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField, Tooltip("Transform to follow (typically the player boat).")]
        private Transform _target;

        [Header("Follow Settings")]
        [SerializeField, Tooltip("Smoothing speed. Higher = snappier tracking.")]
        private float _smoothSpeed = 5f;

        [SerializeField, Tooltip("Offset from the target position. Z should be -10 for 2D.")]
        private Vector3 _offset = new Vector3(0f, 0f, -10f);

        private Vector3? _targetOffset;
        private float _offsetLerpSpeed;
        private bool _hasSnappedToTarget;

        /// <summary>
        /// Smoothly transitions the camera offset to <paramref name="newOffset"/>
        /// over time at the given lerp <paramref name="speed"/>.
        /// </summary>
        public void TransitionOffset(Vector3 newOffset, float speed = 1f)
        {
            _targetOffset = newOffset;
            _offsetLerpSpeed = speed;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            // Snap to target on first frame so camera is centered from the start (respects course layout)
            if (!_hasSnappedToTarget)
            {
                _hasSnappedToTarget = true;
                Vector3 desired = _target.position + _offset;
                desired.z = _offset.z;
                transform.position = desired;
                return;
            }

            if (_targetOffset.HasValue)
            {
                _offset = Vector3.Lerp(_offset, _targetOffset.Value, _offsetLerpSpeed * Time.deltaTime);
                if (Vector3.Distance(_offset, _targetOffset.Value) < 0.01f)
                {
                    _offset = _targetOffset.Value;
                    _targetOffset = null;
                }
            }

            Vector3 desiredPosition = _target.position + _offset;
            desiredPosition.z = _offset.z;

            Vector3 smoothed = Vector3.Lerp(transform.position, desiredPosition,
                _smoothSpeed * Time.deltaTime);

            smoothed.z = _offset.z;
            transform.position = smoothed;
        }
    }
}
