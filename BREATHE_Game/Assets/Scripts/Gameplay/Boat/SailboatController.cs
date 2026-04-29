using UnityEngine;
using Breathe.Audio;
using Breathe.Data;
using Breathe.Input;

namespace Breathe.Gameplay
{
    // Player sailboat — auto-navigates waypoints, speed driven by breath input.
    // Sail visuals (scale + lean) react to wind power. Implements IBoatEnvironmentalTarget
    // so environmental zones can push it around.
    public class SailboatController : MonoBehaviour, IBoatEnvironmentalTarget
    {
        [Header("References")]
        [UnityEngine.Serialization.FormerlySerializedAs("_windSystem")]
        [SerializeField] private BreathPowerSystem _breathPowerSystem;
        [SerializeField] private CourseConfig _courseConfig;

        [Header("Procedural Audio")]
        [SerializeField] private bool _enableProceduralHullAudio = true;

        private ProceduralSailboatWindAudio _procHullAudio;
        private CourseManager _cachedCourseManager;

        [Header("Waypoints")]
        [SerializeField] private Transform[] _waypoints;
        [SerializeField, Tooltip("Distance before snapping to the next waypoint.")]
        private float _waypointReachThreshold = 1.5f;

        [Header("Sail Visuals")]
        [SerializeField] private Transform _sailTransform;
        [SerializeField] private float _sailMinWidth = 1.35f;
        [SerializeField] private float _sailMaxWidth = 1.4f;
        [SerializeField] private float _sailMinScale = 0.18f;  // Y at no wind (squished)
        [SerializeField] private float _sailMaxScale = 1.25f;  // Y at full wind (billowed)
        [SerializeField] private float _sailMaxLean = 18f;      // Z rotation degrees at full wind
        [SerializeField] private float _sailSmoothSpeed = 5f;

        private int _currentWaypointIndex;
        private float _sailDisplayX, _sailDisplayY, _sailDisplayLean;
        private bool _sailDisplayInitialized;
        private Vector3 _lastMoveDir = Vector3.up;
        private bool _finishedCourse;
        private BoatWindEffect _windEffect;
        private BoatSplashEffect _splashEffect;
        private BoatWakeTrailEffect _wakeTrailEffect;
        private Vector3? _sailAwayDirection;

        // Zone forces accumulate per frame, get applied, then reset
        private Vector2 _environmentalLateralDelta;
        private Vector2 _environmentalPullDelta;
        private float _environmentalRotationDelta;
        private float _courseRecoveryTimer;
        private float _environmentalSpeedMultiplier = 1f;
        private bool _inZoneSpinMode;

        private float _speedLogTimer;
        private const float SpeedLogInterval = 2f;

        public float CurrentSpeed { get; private set; }
        public bool FinishedCourse => _finishedCourse;

        // === IBoatEnvironmentalTarget ===

        bool IBoatEnvironmentalTarget.IsPlayer => true;
        Vector3 IBoatEnvironmentalTarget.Position => transform.position;

        float IBoatEnvironmentalTarget.GetEscapeEffort()
        {
            return BreathInputManager.Instance != null ? BreathInputManager.Instance.GetBreathIntensity() : 0f;
        }

        Vector3 IBoatEnvironmentalTarget.GetCourseForwardDirection()
        {
            if (_waypoints == null || _currentWaypointIndex >= _waypoints.Length) return Vector3.zero;
            Transform target = _waypoints[_currentWaypointIndex];
            if (target == null) return Vector3.zero;
            Vector3 dir = (target.position - transform.position).normalized;
            return dir.sqrMagnitude > 0.001f ? dir : Vector3.zero;
        }

        void IBoatEnvironmentalTarget.ApplyLateralForce(Vector2 force) => _environmentalLateralDelta += force;

        void IBoatEnvironmentalTarget.ApplyPullToward(Vector3 center, float strength)
        {
            Vector2 toCenter = (Vector2)(center - transform.position);
            if (toCenter.sqrMagnitude > 0.01f)
                _environmentalPullDelta += toCenter.normalized * strength * Time.deltaTime;
        }

        void IBoatEnvironmentalTarget.ApplyRotation(float torqueDegreesPerSecond)
        {
            _environmentalRotationDelta += torqueDegreesPerSecond * Time.deltaTime;
        }

        void IBoatEnvironmentalTarget.SetEnvironmentalSpeedMultiplier(float mult)
        {
            _environmentalSpeedMultiplier = mult;
        }

        void IBoatEnvironmentalTarget.ClearEnvironmentalSpeedMultiplier()
        {
            _environmentalSpeedMultiplier = 1f;
            _inZoneSpinMode = false;
            _courseRecoveryTimer = 3.2f;
        }

        void IBoatEnvironmentalTarget.SetInZoneSpinMode(bool enable) => _inZoneSpinMode = enable;
        BreathPowerSystem IBoatEnvironmentalTarget.GetBreathPowerSystem() => _breathPowerSystem;

        // Called by CourseManager to inject procedurally generated waypoints
        public void SetWaypoints(Transform[] waypoints)
        {
            _waypoints = waypoints;
            _currentWaypointIndex = 0;
            _finishedCourse = false;
            _sailAwayDirection = null;
            AlignToFirstWaypoint();
        }

        // After the race, steer the boat in a specific direction for the sail-away animation
        public void SetSailAwayDirection(Vector3 direction)
        {
            _sailAwayDirection = direction.normalized;
            _finishedCourse = true;
        }

        // 0-1 progress along the course, interpolated between waypoints
        public float CourseProgress
        {
            get
            {
                if (_waypoints == null || _waypoints.Length <= 1) return 0f;
                if (_currentWaypointIndex >= _waypoints.Length) return 1f;

                float baseProgress = (float)_currentWaypointIndex / _waypoints.Length;

                Transform target = _waypoints[_currentWaypointIndex];
                if (target == null) return baseProgress;

                Vector3 prevPos = _currentWaypointIndex > 0 && _waypoints[_currentWaypointIndex - 1] != null
                    ? _waypoints[_currentWaypointIndex - 1].position
                    : (_waypoints[0] != null ? _waypoints[0].position - Vector3.up * 5f : Vector3.zero);

                float segmentLength = Vector3.Distance(prevPos, target.position);
                if (segmentLength < 0.001f) return baseProgress;

                float distanceFromPrev = Vector3.Distance(transform.position, prevPos);
                float segmentProgress = Mathf.Clamp01(distanceFromPrev / segmentLength);
                return Mathf.Clamp01(baseProgress + segmentProgress / _waypoints.Length);
            }
        }

        private void Awake()
        {
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
            }
        }

        private void Start()
        {
            _currentWaypointIndex = 0;
            gameObject.tag = "Player";

            // Auto-add visual effects if they're not already on the object
            _windEffect = GetComponent<BoatWindEffect>() ?? gameObject.AddComponent<BoatWindEffect>();
            _splashEffect = GetComponent<BoatSplashEffect>() ?? gameObject.AddComponent<BoatSplashEffect>();
            _wakeTrailEffect = GetComponent<BoatWakeTrailEffect>() ?? gameObject.AddComponent<BoatWakeTrailEffect>();

            _cachedCourseManager = FindAnyObjectByType<CourseManager>();
            EnsureProceduralHullAudio();

            AlignToFirstWaypoint();
        }

        private void EnsureProceduralHullAudio()
        {
            if (!_enableProceduralHullAudio) return;

            _procHullAudio = GetComponent<ProceduralSailboatWindAudio>();
            if (_procHullAudio == null)
                _procHullAudio = gameObject.AddComponent<ProceduralSailboatWindAudio>();
        }

        private void AlignToFirstWaypoint()
        {
            if (_waypoints == null || _waypoints.Length < 2) return;

            if (_waypoints[0] != null)
            {
                transform.position = _waypoints[0].position;
                if (Vector3.Distance(transform.position, _waypoints[0].position) <= _waypointReachThreshold * 2f)
                    _currentWaypointIndex = 1;
            }

            // Look a few waypoints ahead for initial heading — pointing straight at
            // the first waypoint can look sideways on curvy courses
            int lookAhead = Mathf.Min(3, _waypoints.Length - 1);
            Vector3 start = _waypoints[0] != null ? _waypoints[0].position : transform.position;
            Vector3 ahead = _waypoints[lookAhead] != null ? _waypoints[lookAhead].position : start + Vector3.up;

            Vector3 dir = (ahead - start).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _lastMoveDir = dir;
        }

        private void Update()
        {
            if (_waypoints == null || _waypoints.Length == 0 || _courseConfig == null) return;

            float windPower = _breathPowerSystem != null ? _breathPowerSystem.BreathPower : 0f;

            // No movement during countdown — boats sit still until the race starts.
            // Once finished, keep coasting so the boat sails through the finish line.
            var cm = _cachedCourseManager != null ? _cachedCourseManager : FindAnyObjectByType<CourseManager>();
            if (cm != null) _cachedCourseManager = cm;

            if (cm != null && !cm.IsRaceActive && !cm.PlayerFinished)
            {
                CurrentSpeed = 0f;
            }
            else
            {
                float breathBonus = windPower * _courseConfig.BreathBonusMultiplier;
                float baseSpeed = _courseConfig.BaseSpeed + breathBonus;
                // Skip zone speed modifier when coasting to finish (so player can actually cross the line)
                CurrentSpeed = _finishedCourse ? baseSpeed : baseSpeed * _environmentalSpeedMultiplier;
            }

            MoveAlongWaypoints();
            ApplyEnvironmentalOffsets();
            UpdateSailVisuals(windPower);

            if (_splashEffect != null)
            {
                _splashEffect.SetSpeed(CurrentSpeed);
                _splashEffect.SetSplashWindDrive(windPower);
                _splashEffect.TickSplashFrameAfterWind();
            }
            if (_wakeTrailEffect != null) _wakeTrailEffect.SetSpeed(CurrentSpeed);

            if (_procHullAudio != null && _courseConfig != null)
            {
                float speedCap = Mathf.Max(0.1f, _courseConfig.BaseSpeed + _courseConfig.BreathBonusMultiplier);
                float speed01 = Mathf.Clamp01(CurrentSpeed / speedCap);
                bool raceAudibleShell = cm != null && (cm.IsRaceActive || cm.PlayerFinished);
                _procHullAudio.Tick(windPower, speed01, raceAudibleShell);
            }

            _speedLogTimer += Time.deltaTime;
            if (_speedLogTimer >= SpeedLogInterval)
            {
                _speedLogTimer = 0f;
                float breathRaw = BreathInputManager.Instance != null
                    ? BreathInputManager.Instance.GetBreathIntensity() : 0f;
                Debug.Log($"[Boat] Speed: {CurrentSpeed:F2}, wind power: {windPower:F3}, " +
                    $"breath intensity: {breathRaw:F3}, zone mult: {_environmentalSpeedMultiplier:F2}");
            }
        }

        private void CoastForward(float speed)
        {
            if (_sailAwayDirection.HasValue)
            {
                _lastMoveDir = Vector3.Lerp(_lastMoveDir, _sailAwayDirection.Value, Time.deltaTime * 1.5f);
                if (_environmentalSpeedMultiplier == 1f)
                {
                    float angle = Mathf.Atan2(_lastMoveDir.y, _lastMoveDir.x) * Mathf.Rad2Deg - 90f;
                    transform.rotation = Quaternion.Lerp(transform.rotation,
                        Quaternion.Euler(0f, 0f, angle), Time.deltaTime * 2f);
                }
                else if (!_inZoneSpinMode)
                    transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * 8f);
            }
            transform.position += _lastMoveDir * speed * Time.deltaTime;
        }

        private void MoveAlongWaypoints()
        {
            if (_currentWaypointIndex >= _waypoints.Length)
            {
                _finishedCourse = true;
                CoastForward(_courseConfig != null ? _courseConfig.BaseSpeed : 3f);
                return;
            }

            Transform target = _waypoints[_currentWaypointIndex];
            if (target == null) return;

            Vector3 direction = target.position - transform.position;
            float distanceToTarget = direction.magnitude;

            if (distanceToTarget <= _waypointReachThreshold)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= _waypoints.Length)
                {
                    _finishedCourse = true;
                    CoastForward(CurrentSpeed);
                    return;
                }
                target = _waypoints[_currentWaypointIndex];
                if (target == null) return;
                direction = target.position - transform.position;
                distanceToTarget = direction.magnitude;
            }

            if (distanceToTarget < 0.001f) return;

            Vector3 moveDir = direction / distanceToTarget;
            _lastMoveDir = moveDir;

            float step = CurrentSpeed * Time.deltaTime;
            transform.position += moveDir * Mathf.Min(step, distanceToTarget);

            // In a zone: face straight up unless spinning (cyclone center)
            if (_environmentalSpeedMultiplier != 1f && !_inZoneSpinMode)
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * 8f);
            else if (_environmentalSpeedMultiplier == 1f)
            {
                float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Lerp(transform.rotation,
                    Quaternion.Euler(0f, 0f, angle), Time.deltaTime * 5f);
            }
        }

        private void ApplyEnvironmentalOffsets()
        {
            // Ignore zone forces during countdown
            var cm = FindAnyObjectByType<CourseManager>();
            if (cm != null && !cm.IsRaceActive)
            {
                _environmentalLateralDelta = Vector2.zero;
                _environmentalPullDelta = Vector2.zero;
                _environmentalRotationDelta = 0f;
                return;
            }

            Vector2 totalDelta = _environmentalLateralDelta + _environmentalPullDelta;
            if (totalDelta.sqrMagnitude > 0.0001f)
                transform.position += (Vector3)totalDelta;

            if (Mathf.Abs(_environmentalRotationDelta) > 0.001f && (_environmentalSpeedMultiplier == 1f || _inZoneSpinMode))
                transform.Rotate(0f, 0f, _environmentalRotationDelta);

            _environmentalLateralDelta = Vector2.zero;
            _environmentalPullDelta = Vector2.zero;
            _environmentalRotationDelta = 0f;

            // Course recovery after leaving a zone — smoothstep so it starts gentle then firms up
            if (_courseRecoveryTimer > 0f)
            {
                _courseRecoveryTimer -= Time.deltaTime;
                if (_waypoints != null && _currentWaypointIndex < _waypoints.Length)
                {
                    Transform target = _waypoints[_currentWaypointIndex];
                    if (target != null)
                    {
                        Vector3 toPath = target.position - transform.position;
                        float dist = toPath.magnitude;
                        if (dist > 0.3f)
                        {
                            float t = 1f - _courseRecoveryTimer / 3.2f;
                            float smoothT = t * t * (3f - 2f * t); // smoothstep
                            float pull = dist * 1.1f * Time.deltaTime * (0.25f + 0.75f * smoothT);
                            pull = Mathf.Min(pull, dist * 0.35f); // cap per frame
                            transform.position += toPath.normalized * pull;
                        }
                    }
                }
            }
        }

        private void UpdateSailVisuals(float windPower)
        {
            if (_sailTransform == null) return;

            float targetX = Mathf.Lerp(_sailMinWidth, _sailMaxWidth, windPower);
            float targetY = Mathf.Lerp(_sailMinScale, _sailMaxScale, windPower);
            float targetLean = windPower * _sailMaxLean;

            if (!_sailDisplayInitialized)
            {
                _sailDisplayX = targetX;
                _sailDisplayY = targetY;
                _sailDisplayLean = targetLean;
                _sailDisplayInitialized = true;
            }
            else
            {
                // Exponential ease toward target
                float t = 1f - Mathf.Exp(-_sailSmoothSpeed * Time.deltaTime);
                _sailDisplayX = Mathf.Lerp(_sailDisplayX, targetX, t);
                _sailDisplayY = Mathf.Lerp(_sailDisplayY, targetY, t);
                _sailDisplayLean = Mathf.Lerp(_sailDisplayLean, targetLean, t);
            }

            _sailTransform.localScale = new Vector3(_sailDisplayX, _sailDisplayY, 1f);
            _sailTransform.localRotation = Quaternion.Euler(0f, 0f, _sailDisplayLean);
        }
    }
}
