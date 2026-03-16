using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // AI companion boat that races alongside the player.
    // Uses Perlin-noise speed variation, rubber-banding, finish-line slowdown,
    // and an obstacle stun mechanic. Implements IBoatEnvironmentalTarget for zone effects.
    public class AICompanionController : MonoBehaviour, IBoatEnvironmentalTarget
    {
        [Header("Data")]
        [SerializeField] private CourseConfig _courseConfig;
        [SerializeField] private AIConfig _aiConfig;

        [Header("Player Reference")]
        [SerializeField] private SailboatController _playerBoat;

        [Header("Waypoints")]
        [SerializeField] private Transform[] _waypoints;
        [SerializeField] private float _waypointReachThreshold = 1.5f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField, Tooltip("Index into AIConfig.AIBoatColors for this boat's tint.")]
        private int _colorIndex;

        [Header("Sail Visuals")]
        [SerializeField] private Transform _sailTransform;
        [SerializeField] private float _sailMinWidth = 1.35f;
        [SerializeField] private float _sailMaxWidth = 1.4f;
        [SerializeField] private float _sailMinScale = 0.18f;
        [SerializeField] private float _sailMaxScale = 1.25f;
        [SerializeField] private float _sailMaxLean = 18f;
        [SerializeField] private float _sailSmoothSpeed = 5f;

        [Header("Stun")]
        [SerializeField] private float _stunSpinSpeed = 480f;
        [SerializeField] private float _laneWidth = 4f;

        private int _currentWaypointIndex;
        private float _noiseOffset;
        private float _stunTimer;
        private float _stunDurationThisTime;
        private float _stunStartRotation;
        private bool _isStunned;
        private Vector3 _laneOffset;
        private Vector3 _lastMoveDir = Vector3.up;
        private float _personalitySpeedMult = 1f;
        private float _currentSpeed;
        private float _surgeNoiseOffset;
        private float _breathPulse;
        private float _sailDisplayX, _sailDisplayY, _sailDisplayLean;
        private bool _sailDisplayInitialized;
        private Vector3? _sailAwayDirection;
        private float? _coastSpeedOverride;

        // Per-boat personality (randomized on Start)
        private float _reactionDelay;
        private float _turnRate;
        private float _hesitationTimer;
        private float _hesitationCooldown;
        private float _personalWaypointThreshold;
        private float _breathRate;
        private float _breathPhaseOffset;

        private BoatWindEffect _windEffect;
        private BoatSplashEffect _splashEffect;
        private BoatWakeTrailEffect _wakeTrailEffect;

        // Competitive win — only the lead AI can be granted a win
        private bool _competitiveWinEvaluated;
        private bool _competitiveWinGranted;
        private BreathAnalytics _breathAnalytics;
        private static AICompanionController _leadAI;

        // Environmental zone state
        private Vector2 _environmentalLateralDelta;
        private Vector2 _environmentalPullDelta;
        private float _environmentalRotationDelta;
        private float _environmentalSpeedMultiplier = 1f;
        private bool _inZoneSpinMode;

        // Replaces waypoints at runtime (CourseManager injects procedural waypoints)
        public void SetWaypoints(Transform[] waypoints)
        {
            _waypoints = waypoints;
            _currentWaypointIndex = 0;
            _sailAwayDirection = null;
            _coastSpeedOverride = null;
            AlignToFirstWaypoint();
        }

        // After the race, steer toward this direction and exit screen
        public void SetSailAwayDirection(Vector3 direction)
        {
            _sailAwayDirection = direction.normalized;
            _isStunned = false;
            if (_waypoints != null) _currentWaypointIndex = _waypoints.Length;
        }

        // Override coast speed (e.g. for catch-up after race). Pass null to revert.
        public void SetCoastSpeedOverride(float? speed)
        {
            _coastSpeedOverride = speed;
            if (speed.HasValue)
            {
                _isStunned = false;
                if (_waypoints != null) _currentWaypointIndex = _waypoints.Length;
            }
        }

        // 0..1 based on how many waypoints have been passed
        public float CourseProgress
        {
            get
            {
                if (_waypoints == null || _waypoints.Length <= 1) return 0f;
                return Mathf.Clamp01((float)_currentWaypointIndex / (_waypoints.Length - 1));
            }
        }

        public bool IsStunned => _isStunned;
        public bool HasCompetitiveWin => _competitiveWinGranted;

        // IBoatEnvironmentalTarget implementation
        bool IBoatEnvironmentalTarget.IsPlayer => false;
        Vector3 IBoatEnvironmentalTarget.Position => transform.position;

        float IBoatEnvironmentalTarget.GetEscapeEffort() => _breathPulse;

        Vector3 IBoatEnvironmentalTarget.GetCourseForwardDirection()
        {
            if (_waypoints == null || _currentWaypointIndex >= _waypoints.Length) return Vector3.zero;
            Transform target = _waypoints[_currentWaypointIndex];
            if (target == null) return Vector3.zero;
            Vector3 dir = ((target.position + _laneOffset) - transform.position).normalized;
            return dir.sqrMagnitude > 0.001f ? dir : Vector3.zero;
        }

        void IBoatEnvironmentalTarget.ApplyLateralForce(Vector2 force)
            => _environmentalLateralDelta += force;

        void IBoatEnvironmentalTarget.ApplyPullToward(Vector3 center, float strength)
        {
            Vector2 toCenter = (Vector2)(center - transform.position);
            if (toCenter.sqrMagnitude > 0.01f)
                _environmentalPullDelta += toCenter.normalized * strength * Time.deltaTime;
        }

        void IBoatEnvironmentalTarget.ApplyRotation(float torqueDegreesPerSecond)
            => _environmentalRotationDelta += torqueDegreesPerSecond * Time.deltaTime;

        void IBoatEnvironmentalTarget.SetEnvironmentalSpeedMultiplier(float mult)
            => _environmentalSpeedMultiplier = mult;

        void IBoatEnvironmentalTarget.ClearEnvironmentalSpeedMultiplier()
        {
            _environmentalSpeedMultiplier = 1f;
            _inZoneSpinMode = false;
        }

        void IBoatEnvironmentalTarget.SetInZoneSpinMode(bool enable) => _inZoneSpinMode = enable;
        WindSystem IBoatEnvironmentalTarget.GetWindSystem() => null;

        // Call when restarting a race
        public static void ResetCompetitiveWinState()
        {
            _leadAI = null;
            var allAIs = FindObjectsByType<AICompanionController>(FindObjectsSortMode.None);
            foreach (var ai in allAIs)
            {
                ai._competitiveWinEvaluated = false;
                ai._competitiveWinGranted = false;
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

            // Lane offset computed in Awake so it's ready when SetWaypoints runs (before Start)
            float sideSign = _colorIndex % 2 == 0 ? -1f : 1f;
            float laneJitter = UnityEngine.Random.Range(-0.6f, 0.6f);
            _laneOffset = new Vector3(sideSign * (_colorIndex / 2 + 1) * _laneWidth + laneJitter, 0f, 0f);
        }

        private void Start()
        {
            _noiseOffset = UnityEngine.Random.Range(0f, 1000f);
            _surgeNoiseOffset = UnityEngine.Random.Range(100f, 900f);
            _personalitySpeedMult = Random.Range(0.88f, 1.06f);
            _reactionDelay = Random.Range(0.15f, 0.55f);
            _turnRate = Random.Range(3.5f, 6.5f);
            _personalWaypointThreshold = _waypointReachThreshold * Random.Range(0.8f, 1.3f);
            _breathRate = Random.Range(0.22f, 0.38f);
            _breathPhaseOffset = Random.Range(0f, 1f);
            _hesitationCooldown = Random.Range(3f, 8f);

            if (_waypoints == null || _waypoints.Length == 0)
                Debug.LogWarning($"[AI {_colorIndex}] No waypoints assigned.", this);
            if (_courseConfig == null)
                Debug.LogWarning($"[AI {_colorIndex}] CourseConfig missing.", this);
            if (_aiConfig == null)
                Debug.LogWarning($"[AI {_colorIndex}] AIConfig missing.", this);
            if (_playerBoat == null)
                Debug.LogWarning($"[AI {_colorIndex}] PlayerBoat ref missing — rubber-banding off.", this);

            _breathAnalytics = FindAnyObjectByType<BreathAnalytics>();

            _windEffect = GetComponent<BoatWindEffect>() ?? gameObject.AddComponent<BoatWindEffect>();
            _splashEffect = GetComponent<BoatSplashEffect>() ?? gameObject.AddComponent<BoatSplashEffect>();
            _wakeTrailEffect = GetComponent<BoatWakeTrailEffect>() ?? gameObject.AddComponent<BoatWakeTrailEffect>();

            if (GetComponent<AIStunEffect>() == null)
                gameObject.AddComponent<AIStunEffect>();

            AlignToFirstWaypoint();
        }

        private void AlignToFirstWaypoint()
        {
            if (_waypoints == null || _waypoints.Length < 2) return;

            if (_waypoints[0] != null)
                transform.position = _waypoints[0].position + _laneOffset;

            Vector3 wp0 = _waypoints[0] != null ? _waypoints[0].position + _laneOffset : transform.position;
            if (Vector3.Distance(transform.position, wp0) <= _waypointReachThreshold * 2f)
                _currentWaypointIndex = 1;

            int lookAhead = Mathf.Min(3, _waypoints.Length - 1);
            Vector3 start = _waypoints[0] != null ? _waypoints[0].position + _laneOffset : transform.position;
            Vector3 ahead = _waypoints[lookAhead] != null ? _waypoints[lookAhead].position + _laneOffset : start + Vector3.up;
            Vector3 dir = (ahead - start).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.up;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            _lastMoveDir = dir;
        }

        private void Update()
        {
            // Update sail visuals even during countdown so AI sails match player (thin at rest)
            if (!_isStunned)
            {
                float visualPulse = IsRaceActive() ? _breathPulse : 0f;
                UpdateSailVisuals(visualPulse);
                if (_windEffect != null) _windEffect.SetWindIntensity(visualPulse);
            }

            if (_waypoints == null || _waypoints.Length == 0) return;
            if (_courseConfig == null || _aiConfig == null) return;

            if (_reactionDelay > 0f) { _reactionDelay -= Time.deltaTime; return; }

            if (_isStunned) { UpdateStun(); return; }

            UpdateHesitation();

            // Zero speed during countdown — ocean animates but boats stay still
            float speed = IsRaceActive() ? CalculateSpeed() : 0f;
            if (_hesitationTimer > 0f) speed *= 0.15f;

            _currentSpeed = speed;
            MoveAlongWaypoints(speed);

            if (_splashEffect != null) _splashEffect.SetSpeed(_currentSpeed);
            if (_wakeTrailEffect != null) _wakeTrailEffect.SetSpeed(_currentSpeed);
        }

        private void UpdateHesitation()
        {
            if (_hesitationTimer > 0f) { _hesitationTimer -= Time.deltaTime; return; }

            _hesitationCooldown -= Time.deltaTime;
            if (_hesitationCooldown <= 0f)
            {
                _hesitationTimer = Random.Range(0.2f, 0.6f);
                _hesitationCooldown = Random.Range(4f, 12f);
            }
        }

        // Stuns the AI for a randomized duration, zeroing speed and playing wobble
        public void TriggerStun()
        {
            _isStunned = true;
            _stunDurationThisTime = Random.Range(1.5f, 3f);
            _stunTimer = _stunDurationThisTime;
            _stunStartRotation = transform.eulerAngles.z;
        }

        private float CalculateSpeed()
        {
            float baseSpeed = _courseConfig.BaseSpeed * _personalitySpeedMult;

            // Per-boat wave — simulates different water currents per boat
            float waveNoise = Mathf.PerlinNoise(Time.time * 0.25f, _noiseOffset + 500f);
            float speed = baseSpeed + (waveNoise - 0.5f) * 4f;

            // Fake breath cycle: longer rest (55%) so sail looks thin at base speed
            float breathPhase = (Time.time * _breathRate + _breathPhaseOffset) % 1f;
            if (breathPhase < 0.55f) _breathPulse = 0f;
            else if (breathPhase < 0.68f) _breathPulse = (breathPhase - 0.55f) / 0.13f;
            else if (breathPhase < 0.75f) _breathPulse = 1f;
            else _breathPulse = 1f - (breathPhase - 0.75f) / 0.25f;
            speed += _breathPulse * baseSpeed * 0.7f;

            if (_playerBoat == null)
                return Mathf.Max(speed, baseSpeed * 0.3f);

            float playerY = _playerBoat.transform.position.y;
            float myY = transform.position.y;
            float yGap = playerY - myY; // positive = player ahead

            speed += yGap * _aiConfig.RubberBandingStrength * 0.3f;

            // Surge: periodic dramatic push via Perlin noise
            float surge = Mathf.PerlinNoise(Time.time * 0.12f, _surgeNoiseOffset);
            if (surge > 0.6f)
                speed += ((surge - 0.6f) / 0.4f) * baseSpeed * 0.9f;

            // -- Competitive win system --
            // Allows rare AI victory when player severely underperforms
            float progress = _playerBoat.CourseProgress;
            if (!_competitiveWinEvaluated &&
                _aiConfig.AllowCompetitiveWin &&
                progress >= _aiConfig.CompetitiveWinProgressThreshold)
            {
                EvaluateCompetitiveWin(yGap, baseSpeed);
            }

            // If competitive win was granted, skip safety nets
            if (_competitiveWinGranted)
            {
                speed *= _environmentalSpeedMultiplier;
                return Mathf.Max(speed, baseSpeed * 0.6f);
            }

            // -- Standard safety nets (player wins most of the time) --

            // Slow down if too far ahead
            if (yGap < -2f)
                speed -= Mathf.Abs(yGap + 2f) * baseSpeed * 0.5f;

            // Progressive slowdown near finish when close to player
            if (progress > 0.75f && yGap < 6f)
            {
                float pressure = (progress - 0.75f) / 0.25f;
                speed -= Mathf.Max(0f, 6f - yGap) * pressure * baseSpeed * 0.35f;
            }

            // Post-finish slowdown (player already crossed)
            if (_playerBoat.FinishedCourse)
            {
                float distToFinish = EstimateDistanceToFinish();
                if (distToFinish < _aiConfig.FinishSlowdownDistance && distToFinish > 0f)
                    speed *= Mathf.Pow(distToFinish / _aiConfig.FinishSlowdownDistance, _aiConfig.FinishSlowdownRate);
            }

            speed *= _environmentalSpeedMultiplier;
            return Mathf.Max(speed, baseSpeed * 0.3f);
        }

        // Only one AI (the leader) can win, so the player is at worst 2nd place
        private void EvaluateCompetitiveWin(float yGap, float baseSpeed)
        {
            _competitiveWinEvaluated = true;
            _competitiveWinGranted = false;

            if (yGap >= -_aiConfig.MinLeadForCompetitiveWin) return;
            if (!IsLeadAI()) return;

            float activityRatio = _breathAnalytics != null ? _breathAnalytics.ActivityRatio : 1f;
            if (activityRatio >= _aiConfig.UnderperformThreshold) return;

            // Worse performance = higher chance
            float performanceMultiplier = 1f - (activityRatio / _aiConfig.UnderperformThreshold);
            float adjustedChance = _aiConfig.CompetitiveWinChance * performanceMultiplier;

            float roll = Random.value;
            if (roll < adjustedChance)
            {
                _competitiveWinGranted = true;
                _leadAI = this;
                Debug.Log($"[AI {_colorIndex}] Competitive win granted! Activity: {activityRatio:P0}, Chance: {adjustedChance:P0}");
            }
        }

        private bool IsLeadAI()
        {
            if (_leadAI != null && _leadAI != this && _leadAI._competitiveWinGranted) return false;

            var allAIs = FindObjectsByType<AICompanionController>(FindObjectsSortMode.None);
            float myY = transform.position.y;
            foreach (var ai in allAIs)
            {
                if (ai != this && ai.transform.position.y > myY) return false;
            }
            return true;
        }

        private float EstimateDistanceToFinish()
        {
            if (_waypoints == null || _waypoints.Length == 0) return float.MaxValue;
            float dist = 0f;
            Vector3 prevPos = transform.position;
            for (int i = _currentWaypointIndex; i < _waypoints.Length; i++)
            {
                if (_waypoints[i] == null) continue;
                dist += Vector3.Distance(prevPos, _waypoints[i].position);
                prevPos = _waypoints[i].position;
            }
            return dist;
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

        private void MoveAlongWaypoints(float speed)
        {
            if (_currentWaypointIndex >= _waypoints.Length)
            {
                CoastForward(_coastSpeedOverride ?? (_courseConfig != null ? _courseConfig.BaseSpeed : 3f));
                return;
            }

            Transform target = _waypoints[_currentWaypointIndex];
            if (target == null) return;

            Vector3 targetPos = target.position + _laneOffset;
            Vector3 direction = targetPos - transform.position;
            float distanceToTarget = direction.magnitude;

            if (distanceToTarget <= _personalWaypointThreshold)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= _waypoints.Length)
                { CoastForward(_coastSpeedOverride ?? speed); return; }

                target = _waypoints[_currentWaypointIndex];
                if (target == null) return;
                targetPos = target.position + _laneOffset;
                direction = targetPos - transform.position;
                distanceToTarget = direction.magnitude;
            }

            if (distanceToTarget < 0.001f) return;

            Vector3 moveDir = direction / distanceToTarget;
            _lastMoveDir = moveDir;

            float step = speed * Time.deltaTime;
            transform.position += moveDir * Mathf.Min(step, distanceToTarget);
            ApplyEnvironmentalOffsets();

            // In a zone (but not cyclone spin mode): face upward. Otherwise: face move direction.
            if (_environmentalSpeedMultiplier != 1f && !_inZoneSpinMode)
                transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.identity, Time.deltaTime * 8f);
            else if (_environmentalSpeedMultiplier == 1f)
            {
                float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Lerp(transform.rotation,
                    Quaternion.Euler(0f, 0f, angle), Time.deltaTime * _turnRate);
            }
        }

        private void ApplyEnvironmentalOffsets()
        {
            if (!IsRaceActive())
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
        }

        // Sail inflate/lean driven by simulated breath pulse — mirrors player's sail behavior
        private void UpdateSailVisuals(float visualPulse)
        {
            if (_sailTransform == null) return;

            float targetX = Mathf.Lerp(_sailMinWidth, _sailMaxWidth, visualPulse);
            float targetY = Mathf.Lerp(_sailMinScale, _sailMaxScale, visualPulse);
            float targetLean = visualPulse * _sailMaxLean;

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

        private static bool IsRaceActive()
        {
            var cm = FindAnyObjectByType<CourseManager>();
            return cm != null && cm.IsRaceActive;
        }

        private void UpdateStun()
        {
            _stunTimer -= Time.deltaTime;
            if (_stunTimer <= 0f) { _isStunned = false; transform.rotation = Quaternion.identity; return; }

            float elapsed = _stunDurationThisTime - _stunTimer;
            transform.rotation = Quaternion.Euler(0f, 0f, _stunStartRotation + elapsed * _stunSpinSpeed);
        }
    }
}
