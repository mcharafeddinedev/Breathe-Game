using UnityEngine;

namespace Breathe.Gameplay
{
    // Smooth 2D camera follow. Lerps toward target each LateUpdate,
    // keeps Z at -10 for orthographic rendering.
    public class CameraFollow : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _smoothSpeed = 5f;
        [SerializeField] private Vector3 _offset = new Vector3(0f, 0f, -10f);

        private Vector3? _targetOffset;
        private float _offsetLerpSpeed;
        private bool _hasSnappedToTarget;

        // Smoothly shift the camera offset over time (used for post-race pan, etc.)
        public void TransitionOffset(Vector3 newOffset, float speed = 1f)
        {
            _targetOffset = newOffset;
            _offsetLerpSpeed = speed;
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            // Snap immediately on the first frame so the camera starts centered
            if (!_hasSnappedToTarget)
            {
                _hasSnappedToTarget = true;
                Vector3 desired = _target.position + _offset;
                desired.z = _offset.z;
                transform.position = desired;
                return;
            }

            // Animate offset transitions if one is active
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

            Vector3 smoothed = Vector3.Lerp(transform.position, desiredPosition, _smoothSpeed * Time.deltaTime);
            smoothed.z = _offset.z;
            transform.position = smoothed;
        }
    }
}
