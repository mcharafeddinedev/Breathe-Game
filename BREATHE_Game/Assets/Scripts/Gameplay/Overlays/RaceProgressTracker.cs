using System;
using UnityEngine;

namespace Breathe.Gameplay
{
    // Reads course progress from every boat each frame, fires finish events,
    // and provides a normalized progress API for the race-progress UI bar.
    public class RaceProgressTracker : MonoBehaviour
    {
        [Header("Boats")]
        [SerializeField] private SailboatController _playerBoat;
        [SerializeField] private AICompanionController[] _aiBoats;

        [Header("Finish Detection")]
        [SerializeField, Tooltip("Progress threshold (0-1) at which a boat is considered finished.")]
        private float _finishThreshold = 0.99f;

        private float[] _progressCache;
        private bool[] _finishedFlags;

        // Payload is the boat index (0 = player, 1+ = AI).
        public event Action<int> OnBoatFinished;

        // Normalized player progress — shortcut for GetProgress(0).
        public float Progress => GetProgress(0);

        // Total boats being tracked (player + AI).
        public int GetBoatCount()
        {
            return 1 + (_aiBoats != null ? _aiBoats.Length : 0);
        }

        // Returns normalized progress [0, 1] for the given boat index (0 = player, 1+ = AI).
        public float GetProgress(int boatIndex)
        {
            if (_progressCache == null || boatIndex < 0 || boatIndex >= _progressCache.Length)
                return 0f;
            return _progressCache[boatIndex];
        }

        private void Start()
        {
            int count = GetBoatCount();
            _progressCache = new float[count];
            _finishedFlags = new bool[count];
        }

        private void Update()
        {
            if (_progressCache == null) return;
            UpdateProgressCache();
            CheckFinishes();
        }

        private void UpdateProgressCache()
        {
            _progressCache[0] = _playerBoat != null ? _playerBoat.CourseProgress : 0f;

            if (_aiBoats != null)
            {
                for (int i = 0; i < _aiBoats.Length; i++)
                    _progressCache[i + 1] = _aiBoats[i] != null ? _aiBoats[i].CourseProgress : 0f;
            }
        }

        private void CheckFinishes()
        {
            for (int i = 0; i < _progressCache.Length; i++)
            {
                if (_finishedFlags[i]) continue;
                if (_progressCache[i] >= _finishThreshold)
                {
                    _finishedFlags[i] = true;
                    Debug.Log($"[RaceProgress] Boat {i} finished (progress {_progressCache[i]:F3}).");
                    OnBoatFinished?.Invoke(i);
                }
            }
        }

        // Resets all finish flags — call when starting a new race.
        public void ResetFinishFlags()
        {
            if (_finishedFlags != null)
                Array.Clear(_finishedFlags, 0, _finishedFlags.Length);
        }
    }
}
