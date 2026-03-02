using UnityEngine;
using Breathe.Data;
using Breathe.Input;

namespace Breathe.Gameplay
{
    /// <summary>
    /// Player sailboat that auto-navigates a waypoint path.
    /// Forward speed is base speed plus a breath-driven bonus from <see cref="WindSystem"/>.
    /// Sail visuals (scale and lean) react to current wind power.
    /// Implements <see cref="IBoatEnvironmentalTarget"/> for environmental zone effects.
    /// </summary>
    public class SailboatController : MonoBehaviour, IBoatEnvironmentalTarget
    {
        [Header("References")]
        [SerializeField, Tooltip("Wind system providing breath-driven wind power.")]
        private WindSystem _windSystem;

        [SerializeField, Tooltip("Course tuning data.")]
        private CourseConfig _courseConfig;

        [Header("Waypoints")]
        [SerializeField, Tooltip("Ordered waypoints defining the course path.")]
        private Transform[] _waypoints;

        [Header("Navigation")]
        [SerializeField, Tooltip("Distance to a waypoint before switching to the next one.")]
        private float _waypointReachThreshold = 1.5f;

        [Header("Sail Visuals")]
        [SerializeField, Tooltip("Transform of the sail sprite to animate.")]
        private Transform _sailTransform;

        [SerializeField, Tooltip("X-scale when empty (wide, squished triangle ~1.35). Same as AI boats.")]
        private float _sailMinWidth = 1.35f;

        [SerializeField, Tooltip("X-scale when full (sail fills a little wider). Same as AI boats.")]
        private float _sailMaxWidth = 1.4f;

        [SerializeField, Tooltip("Y-scale when empty (very squished ~0.18). Same as AI boats.")]
        private float _sailMinScale = 0.18f;

        [SerializeField, Tooltip("Y-scale when full (billowed sail). Same as AI boats.")]
        private float _sailMaxScale = 1.25f;

        [SerializeField, Tooltip("Max Z-rotation (degrees) the sail leans at full wind. 0° at rest.")]
        private float _sailMaxLean = 18f;

        [SerializeField, Tooltip("How quickly sail scale eases toward target (higher = snappier). ~4–6 = gradual fill.")]
        private float _sailSmoothSpeed = 5f;

        private int _currentWaypointIndex;
        private float _sailDisplayX;
        private float _sailDisplayY;
        private float _sailDisplayLean;
        private bool _sailDisplayInitialized;
        private Vector3 _lastMoveDir = Vector3.up;
        private bool _finishedCourse;
        private BoatWindEffect _windEffect;
        private BoatSplashEffect _splashEffect;
        private BoatWakeTrailEffect _wakeTrailEffect;
        private Vector3? _sailAwayDirection;

        // Environmental zone effects (player can be pushed off course, then recovers)
        private Vector2 _environmentalLateralDelta;
        private Vector2 _environmentalPullDelta;
        private float _environmentalRotationDelta;
        private float _courseRecoveryTimer;
        private float _environmentalSpeedMultiplier = 1f;
        private bool _inZoneSpinMode;

        /// <summary>Total instantaneous speed (base + breath bonus) in world units per second.</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>Whether the boat has passed the final waypoint.</summary>
        public bool FinishedCourse => _finishedCourse;

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

        void IBoatEnvironmentalTarget.ApplyLateralForce(Vector2 force)
        {
            _environmentalLateralDelta += force;
        }

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
            if (_windSystem != null)
                _windSystem.SetEnvironmentalMultiplier(mult);
        }

        void IBoatEnvironmentalTarget.ClearEnvironmentalSpeedMultiplier()
        {
            _environmentalSpeedMultiplier = 1f;
            _inZoneSpinMode = false;
            if (_windSystem != null)
                _windSystem.SetEnvironmentalMultiplier(1f);
            _courseRecoveryTimer = 3.2f; // Smooth remagnet to course — gradual, as it would occur in real sailing
        }

        void IBoatEnvironmentalTarget.SetInZoneSpinMode(bool enable) => _inZoneSpinMode = enable;

        WindSystem IBoatEnvironmentalTarget.GetWindSystem() => _windSystem;

        /// <summary>
        /// Replaces the waypoint array at runtime (used by <see cref="CourseManager"/>
        /// to inject procedurally generated waypoints that follow the course curve).
        /// Re-aligns the boat to face the initial course direction.
        /// </summary>
        public void SetWaypoints(Transform[] waypoints)
        {
            _waypoints = waypoints;
            _currentWaypointIndex = 0;
            _finishedCourse = false;
            _sailAwayDirection = null;
            
            // Re-align to the new waypoints
            AlignToFirstWaypoint();
        }

        /// <summary>
        /// Overrides coast direction after the race ends, causing the boat
        /// to gradually steer toward <paramref name="direction"/>.
        /// </summary>
        public void SetSailAwayDirection(Vector3 direction)
        {
            _sailAwayDirection = direction.normalized;
            _finishedCourse = true;
        }

        /// <summary>
        /// Normalized course progress in [0, 1] based on distance traveled along waypoints.
        /// Interpolates between waypoints for smooth progress updates.
        /// 0 = at start, 1 = past the final waypoint.
        /// </summary>
        public float CourseProgress
        {
            get
            {
                if (_waypoints == null || _waypoints.Length <= 1) return 0f;
                if (_currentWaypointIndex >= _waypoints.Length) return 1f;

                // Progress from completed waypoints
                float baseProgress = (float)_currentWaypointIndex / _waypoints.Length;

                // Interpolate progress toward current target waypoint
                Transform target = _waypoints[_currentWaypointIndex];
                if (target == null) return baseProgress;

                // Get the previous waypoint (or start position)
                Vector3 prevPos = _currentWaypointIndex > 0 && _waypoints[_currentWaypointIndex - 1] != null
                    ? _waypoints[_currentWaypointIndex - 1].position
                    : (_waypoints[0] != null ? _waypoints[0].position - Vector3.up * 5f : Vector3.zero);

                float segmentLength = Vector3.Distance(prevPos, target.position);
                if (segmentLength < 0.001f) return baseProgress;

                float distanceToTarget = Vector3.Distance(transform.position, target.position);
                float distanceFromPrev = Vector3.Distance(transform.position, prevPos);
                
                // How far along this segment (0 = at prev, 1 = at target)
                float segmentProgress = Mathf.Clamp01(distanceFromPrev / segmentLength);
                
                // Add fractional progress for this segment
                float fractionalProgress = segmentProgress / _waypoints.Length;

                return Mathf.Clamp01(baseProgress + fractionalProgress);
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

            _windEffect = GetComponent<BoatWindEffect>();
            if (_windEffect == null)
                _windEffect = gameObject.AddComponent<BoatWindEffect>();

            _splashEffect = GetComponent<BoatSplashEffect>();
            if (_splashEffect == null)
                _splashEffect = gameObject.AddComponent<BoatSplashEffect>();

            _wakeTrailEffect = GetComponent<BoatWakeTrailEffect>();
            if (_wakeTrailEffect == null)
                _wakeTrailEffect = gameObject.AddComponent<BoatWakeTrailEffect>();

            AlignToFirstWaypoint();
        }

        private void AlignToFirstWaypoint()
        {
            if (_waypoints == null || _waypoints.Length < 2) return;

            // Start at center of first buoy boundary (WP0 = course center at y=0)
            if (_waypoints[0] != null)
            {
                transform.position = _waypoints[0].position;
                float distToFirst = Vector3.Distance(transform.position, _waypoints[0].position);
                if (distToFirst <= _waypointReachThreshold * 2f)
                    _currentWaypointIndex = 1;
            }

            // Face along the course heading (WP0 → WP[lookAhead]) instead of
            // directly toward the nearest waypoint, which can be sideways on curves
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
            if (_waypoints == null || _waypoints.Length == 0) return;
            if (_courseConfig == null) return;

            float windPower = _windSystem != null ? _windSystem.WindPower : 0f;

            // Zero speed during countdown (before race starts) — ocean/camera animate but boats stay still.
            // When player has finished, keep moving so the boat passes through the finish line and coasts.
            var cm = FindAnyObjectByType<CourseManager>();
            if (cm != null && !cm.IsRaceActive && !cm.PlayerFinished)
            {
                CurrentSpeed = 0f;
            }
            else
            {
                float breathBonus = windPower * _courseConfig.BreathBonusMultiplier;
                float baseSpeed = _courseConfig.BaseSpeed + breathBonus;
                // When coasting to finish (passed all waypoints), bypass environmental zones so player can cross
                CurrentSpeed = _finishedCourse ? baseSpeed : baseSpeed * _environmentalSpeedMultiplier;
            }

            MoveAlongWaypoints();
            ApplyEnvironmentalOffsets();
            UpdateSailVisuals(windPower);

            if (_splashEffect != null)
                _splashEffect.SetSpeed(CurrentSpeed);
            if (_wakeTrailEffect != null)
                _wakeTrailEffect.SetSpeed(CurrentSpeed);
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
                float coastSpeed = _courseConfig != null ? _courseConfig.BaseSpeed : 3f;
                CoastForward(coastSpeed);
                return;
            }

            Transform target = _waypoints[_currentWaypointIndex];
            if (target == null) return;

            Vector3 direction = target.position - transform.position;
            float distanceToTarget = direction.magnitude;

            // Advance to next waypoint if close enough
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

            // Direct movement toward current waypoint
            Vector3 moveDir = direction / distanceToTarget;
            _lastMoveDir = moveDir;

            float step = CurrentSpeed * Time.deltaTime;
            transform.position += moveDir * Mathf.Min(step, distanceToTarget);

            // When in environmental zone: spin mode (cyclone center) = apply rotation; else face up.
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
            // During countdown, ignore environmental forces — boats stay still
            var cm = FindAnyObjectByType<CourseManager>();
            if (cm != null && !cm.IsRaceActive)
            {
                _environmentalLateralDelta = Vector2.zero;
                _environmentalPullDelta = Vector2.zero;
                _environmentalRotationDelta = 0f;
                return;
            }

            // Apply lateral and pull deltas from environmental zones
            Vector2 totalDelta = _environmentalLateralDelta + _environmentalPullDelta;
            if (totalDelta.sqrMagnitude > 0.0001f)
            {
                transform.position += (Vector3)totalDelta;
            }

            // Apply rotation: in spin mode (cyclone center) or when not in zone
            if (Mathf.Abs(_environmentalRotationDelta) > 0.001f && (_environmentalSpeedMultiplier == 1f || _inZoneSpinMode))
                transform.Rotate(0f, 0f, _environmentalRotationDelta);

            _environmentalLateralDelta = Vector2.zero;
            _environmentalPullDelta = Vector2.zero;
            _environmentalRotationDelta = 0f;

            // Course recovery: when exiting a zone, smoothly remagnet back toward waypoint path.
            // Starts gentle (inertia), ramps up naturally as the boat settles back on course.
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
                            float recoveryDuration = 3.2f;
                            float t = 1f - _courseRecoveryTimer / recoveryDuration;
                            float smoothT = t * t * (3f - 2f * t); // Smoothstep: gentle start, natural ramp
                            float pull = dist * 1.1f * Time.deltaTime * (0.25f + 0.75f * smoothT);
                            pull = Mathf.Min(pull, dist * 0.35f); // Slightly gentler per-frame cap for smoother feel
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
