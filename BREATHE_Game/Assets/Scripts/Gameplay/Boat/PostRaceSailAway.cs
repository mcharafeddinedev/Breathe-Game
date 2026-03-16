using System.Collections;
using UnityEngine;

namespace Breathe.Gameplay
{
    // After the race, AI boats catch up, all three cruise together briefly,
    // then AIs peel off to opposite edges while the player sails straight ahead.
    public class PostRaceSailAway : MonoBehaviour
    {
        [Header("References (auto-found if empty)")]
        [SerializeField] private SailboatController _playerBoat;
        [SerializeField] private AICompanionController[] _aiBoats;
        [SerializeField] private CameraFollow _cameraFollow;

        [Header("Catch-Up")]
        [SerializeField, Tooltip("Slightly faster than player coast speed.")]
        private float _catchUpSpeed = 5f;

        [SerializeField, Tooltip("Y-distance threshold — AI is 'caught up' when within this of the player.")]
        private float _catchUpThreshold = 1.5f;

        [SerializeField] private float _catchUpTimeout = 10f;

        [SerializeField] private float _cruiseDuration = 2f;

        [Header("Sail Away")]
        [SerializeField, Tooltip("Degrees off straight-ahead the AI boats diverge. One left, one right.")]
        private float _spreadAngle = 55f;

        [SerializeField] private Vector3 _cameraEndOffset = new Vector3(0f, -3.5f, -10f);
        [SerializeField] private float _cameraTransitionSpeed = 1.2f;

        private void Awake()
        {
            if (_playerBoat == null)
                _playerBoat = FindAnyObjectByType<SailboatController>();
            if (_aiBoats == null || _aiBoats.Length == 0)
                _aiBoats = FindObjectsByType<AICompanionController>(FindObjectsSortMode.None);
            if (_cameraFollow == null)
                _cameraFollow = FindAnyObjectByType<CameraFollow>();
        }

        private void OnEnable()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState state)
        {
            if (state == GameState.Celebration)
                StartCoroutine(SailAwayRoutine());
        }

        private IEnumerator SailAwayRoutine()
        {
            // Brief settling pause so the finish moment lands
            yield return new WaitForSeconds(0.3f);

            // Phase 1: AI boats catch up to the player
            if (_aiBoats != null && _playerBoat != null)
            {
                foreach (var ai in _aiBoats)
                {
                    if (ai != null)
                        ai.SetCoastSpeedOverride(_catchUpSpeed);
                }

                float elapsed = 0f;
                while (elapsed < _catchUpTimeout && !AllAIsCaughtUp())
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                foreach (var ai in _aiBoats)
                {
                    if (ai != null)
                        ai.SetCoastSpeedOverride(null);
                }
            }

            // Phase 2: Cruise together side-by-side
            yield return new WaitForSeconds(_cruiseDuration);

            // Phase 3: Player goes straight, AIs diverge to opposite edges
            if (_playerBoat != null)
                _playerBoat.SetSailAwayDirection(Vector3.up);

            if (_aiBoats != null)
            {
                for (int i = 0; i < _aiBoats.Length; i++)
                {
                    if (_aiBoats[i] == null) continue;
                    float sign = (i == 0) ? -1f : 1f;
                    Vector3 dir = Quaternion.Euler(0f, 0f, sign * _spreadAngle) * Vector3.up;
                    _aiBoats[i].SetSailAwayDirection(dir);
                }
            }

            // Shift camera so the player appears in the upper portion of the screen
            if (_cameraFollow != null)
                _cameraFollow.TransitionOffset(_cameraEndOffset, _cameraTransitionSpeed);
        }

        private bool AllAIsCaughtUp()
        {
            if (_playerBoat == null || _aiBoats == null) return true;
            float playerY = _playerBoat.transform.position.y;
            foreach (var ai in _aiBoats)
            {
                if (ai == null) continue;
                if (ai.transform.position.y < playerY - _catchUpThreshold)
                    return false;
            }
            return true;
        }
    }
}
