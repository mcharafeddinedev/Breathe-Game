using System;
using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Applies physics forces and speed modifiers to boats inside the zone.
    // Supports headwind, tailwind, crosswind, cyclone, doldrums, wave chop.
    // Requires a trigger Collider2D. Runs early so forces apply before boat movement.
    [RequireComponent(typeof(Collider2D))]
    [DefaultExecutionOrder(-100)]
    public class EnvironmentalZoneEffect : MonoBehaviour
    {
        [Header("Zone Type")]
        [SerializeField] private ZoneType _zoneType = ZoneType.Headwind;

        [SerializeField, Range(0f, 1f), Tooltip("Strength 0–1 scales all effects.")]
        private float _strength = 1f;

        [Header("Config")]
        [SerializeField, Tooltip("Per-type parameters. Auto-found if not assigned.")]
        private EnvironmentalZoneConfig _zoneConfig;

        [SerializeField] private bool _showPopupOnEnter = true;

        [Header("Escape")]
        [SerializeField, Tooltip("Max seconds before effects fully release. Breathing helps escape faster.")]
        private float _maxEscapeTime = 2.5f;

        // Fired when a zone popup should display. Payload: (zoneType, popupText).
        public static event Action<ZoneType, string> OnZonePopup;

        // Current zone any boat is inside, for debug display. Null if none.
        public static EnvironmentalZoneEffect CurrentZoneForDebug { get; private set; }

        private Collider2D _collider;
        private readonly HashSet<IBoatEnvironmentalTarget> _boatsInside = new HashSet<IBoatEnvironmentalTarget>();
        private readonly Dictionary<IBoatEnvironmentalTarget, float> _escapeProgress = new Dictionary<IBoatEnvironmentalTarget, float>();
        private float _lateralDirection; // -1 or 1, set at start
        private Data.EnvironmentalZoneConfig.ZoneParameters _params;

        public ZoneType ZoneType => _zoneType;

        // Crosswind lateral direction: -1 left, 1 right. Used for visual alignment.
        public float LateralDirection => _lateralDirection;

        // Called by the spawner to configure zone type at runtime.
        public void SetZoneType(ZoneType type)
        {
            _zoneType = type;
            if (_zoneConfig != null)
                _params = _zoneConfig.GetParameters(_zoneType);
            else
                _params = Breathe.Data.EnvironmentalZoneConfig.GetDefaultParameters(_zoneType);
        }

        public void SetStrength(float strength) => _strength = Mathf.Clamp01(strength);

        // Sets collider radius. For crosswind, the spawner handles capsule sizing via ApplyZoneShape.
        public void SetRadius(float radius)
        {
            if (_collider is CircleCollider2D circle)
                circle.radius = radius;
            else if (_collider is CapsuleCollider2D cap)
                cap.size = new Vector2(radius * 2f, radius * 2f);
        }

        public void SetShowPopupOnEnter(bool show) => _showPopupOnEnter = show;

        private void Awake()
        {
            _collider = GetComponent<Collider2D>();
            if (_collider != null)
                _collider.isTrigger = true;

            _lateralDirection = UnityEngine.Random.value > 0.5f ? 1f : -1f;
        }

        // Re-fetch the active collider after the spawner enables/disables them.
        public void RefreshCollider()
        {
            foreach (var col in GetComponents<Collider2D>())
            {
                if (col.enabled)
                {
                    _collider = col;
                    _collider.isTrigger = true;
                    return;
                }
            }
            _collider = GetComponent<Collider2D>();
        }

        private void Start()
        {
            if (_zoneConfig == null)
            {
                var cm = FindAnyObjectByType<CourseManager>();
                if (cm != null && cm.CourseConfig != null)
                    _zoneConfig = cm.CourseConfig.ZoneConfig;
                _zoneConfig ??= Resources.Load<Breathe.Data.EnvironmentalZoneConfig>("DefaultZoneConfig");
            }

            if (_zoneConfig == null)
            {
                Debug.LogWarning("[EnvironmentalZoneEffect] No zone config — using defaults.");
            }

            _params = _zoneConfig != null
                ? _zoneConfig.GetParameters(_zoneType)
                : Breathe.Data.EnvironmentalZoneConfig.GetDefaultParameters(_zoneType);

            if (_params.lateralDirection != 0f)
                _lateralDirection = Mathf.Sign(_params.lateralDirection);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            var boat = ResolveBoat(other);
            if (boat == null)
            {
                Debug.Log($"[EnvironmentalZoneEffect] OnTriggerEnter2D: {_zoneType} — collider from {other.gameObject.name} (layer {other.gameObject.layer}), ResolveBoat=null");
                return;
            }

            Debug.Log($"[EnvironmentalZoneEffect] {_zoneType} zone entered by {boat.GetType().Name}");
            _boatsInside.Add(boat);
            _escapeProgress[boat] = 0f;

            // Only show popup for the player boat, not AI — one popup at a time.
            if (boat.IsPlayer && _showPopupOnEnter && !string.IsNullOrEmpty(_params.popupText))
            {
                ZoneEvents.RaiseZonePopup(_params.popupText);
                OnZonePopup?.Invoke(_zoneType, _params.popupText);
            }

            CurrentZoneForDebug = this;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            var boat = ResolveBoat(other);
            if (boat == null) return;

            _boatsInside.Remove(boat);
            _escapeProgress.Remove(boat);

            boat.ClearEnvironmentalSpeedMultiplier();
            boat.SetInZoneSpinMode(false);

            if (_boatsInside.Count == 0)
                CurrentZoneForDebug = null;
        }

        private void Update()
        {
            foreach (var boat in _boatsInside)
            {
                if (boat == null) continue;
                ApplyForcesToBoat(boat);
            }
        }

        private void ApplyForcesToBoat(IBoatEnvironmentalTarget boat)
        {
            float dt = Time.deltaTime;
            float str = _strength;
            float breathEffort = boat.GetEscapeEffort();
            Vector3 courseDir = boat.GetCourseForwardDirection();
            float baseMult = _params.speedMultiplier * (0.5f + 0.5f * str);

            // Low-frequency sine for ocean-like motion
            float t = Time.time * 0.6f;
            float wave = Mathf.Sin(t) * 0.5f + Mathf.Sin(t * 0.37f) * 0.3f;

            // All zones except Tailwind/Calm use escape. Input helps; guaranteed release within _maxEscapeTime.
            bool useEscape = _zoneType != ZoneType.Tailwind && _zoneType != ZoneType.Calm;
            float escape = 0f;
            if (useEscape)
            {
                escape = _escapeProgress.TryGetValue(boat, out float ep) ? ep : 0f;
                float naturalRate = (1f / Mathf.Max(0.5f, _params.baseEscapeTime)) * dt;
                float breathBonus = breathEffort * _params.breathEscapeMultiplier * dt;
                float minRate = dt / Mathf.Max(0.5f, _maxEscapeTime);
                float escapeRate = Mathf.Max(naturalRate + breathBonus, minRate);
                escape = Mathf.Clamp01(escape + escapeRate);
                _escapeProgress[boat] = escape;
            }

            // Crosswind keeps full speed; others lerp toward 1.0 as escape progresses.
            float effectiveMult = _zoneType == ZoneType.Crosswind
                ? 1f
                : (useEscape ? Mathf.Lerp(baseMult, 1f, escape) : baseMult);
            boat.SetEnvironmentalSpeedMultiplier(effectiveMult);

            // Breathing pushes the boat toward the next waypoint
            if (breathEffort > 0.2f && courseDir.sqrMagnitude > 0.01f)
            {
                float escapeStr = 4.5f * breathEffort * str * dt;
                boat.ApplyLateralForce((Vector2)courseDir * escapeStr);
            }

            boat.SetInZoneSpinMode(false);

            if (_zoneType == ZoneType.Headwind && _params.lateralForce > 0f)
            {
                float resist = (1f - escape) * _params.lateralForce * str * dt * 0.8f;
                boat.ApplyLateralForce(-Vector2.up * resist);
                if (escape >= 0.6f && courseDir.sqrMagnitude > 0.01f)
                {
                    float exitStr = (escape - 0.6f) / 0.4f * 5f * str * dt;
                    boat.ApplyLateralForce((Vector2)courseDir * exitStr);
                }
            }
            else if (_zoneType == ZoneType.Doldrums && _params.lateralForce > 0f)
            {
                float driftStr = (1f - escape) * wave * _params.lateralForce * str * dt * 0.6f;
                boat.ApplyLateralForce(new Vector2(driftStr, 0f));
                if (escape >= 0.6f && courseDir.sqrMagnitude > 0.01f)
                {
                    float exitStr = (escape - 0.6f) / 0.4f * 5f * str * dt;
                    boat.ApplyLateralForce((Vector2)courseDir * exitStr);
                }
            }
            else if (_zoneType == ZoneType.Cyclone && _params.pullForce > 0f)
            {
                float pullStr = _params.pullForce * str * (1f - escape * 0.9f) * 1.2f;
                Vector3 center = transform.position;
                boat.ApplyPullToward(center, pullStr);

                float radius = _collider switch
                {
                    CircleCollider2D c => c.radius,
                    CapsuleCollider2D cap => cap.size.x * 0.5f,
                    _ => 4f
                };
                float distToCenter = Vector3.Distance(boat.Position, center);
                bool nearCenter = distToCenter < radius * 0.35f;

                if (nearCenter && breathEffort < 0.2f && _params.rotationalTorque > 0f)
                {
                    boat.SetInZoneSpinMode(true);
                    boat.ApplyRotation(_params.rotationalTorque * str);
                }

                if (escape >= 0.7f && courseDir.sqrMagnitude > 0.01f)
                {
                    float exitStr = (escape - 0.7f) / 0.3f * 5f * str * dt;
                    boat.ApplyLateralForce((Vector2)courseDir * exitStr);
                }
            }
            else if (_zoneType == ZoneType.Crosswind)
            {
                // Lateral push tapers with escape — released within _maxEscapeTime
                float lateral = 0f;
                var cm = FindAnyObjectByType<CourseManager>();
                if (cm != null && cm.CourseConfig != null && cm.CourseConfig.CrosswindLateralForce > 0f)
                    lateral = cm.CourseConfig.CrosswindLateralForce;
                else
                    lateral = _params.lateralForce;
                if (lateral > 0f)
                {
                    float crossStr = lateral * str * (1f - escape) * dt * 0.9f;
                    boat.ApplyLateralForce(new Vector2(_lateralDirection * crossStr, 0f));
                }
            }
            else if (_zoneType == ZoneType.WaveChop || _zoneType == ZoneType.Waves)
            {
                // Horizontal wobble until escape. WaveChop = strong, Waves = moderate.
                float wobbleFreq = _zoneType == ZoneType.WaveChop ? 2.5f : 1.8f;
                float wobbleStrength = _zoneType == ZoneType.WaveChop ? 2.8f : 1.4f;
                float lateralStr = (1f - escape * 0.7f) * Mathf.Sin(Time.time * wobbleFreq) * _params.lateralForce * str * dt * wobbleStrength;
                boat.ApplyLateralForce(new Vector2(lateralStr, 0f));
                if (escape >= 0.65f && courseDir.sqrMagnitude > 0.01f)
                {
                    float exitStr = (escape - 0.65f) / 0.35f * 3f * str * dt;
                    boat.ApplyLateralForce((Vector2)courseDir * exitStr);
                }
            }
        }

        private static IBoatEnvironmentalTarget ResolveBoat(Collider2D other)
        {
            var player = other.GetComponentInParent<SailboatController>();
            if (player != null) return player;

            var ai = other.GetComponentInParent<AICompanionController>();
            if (ai != null) return ai;

            return null;
        }
    }
}
