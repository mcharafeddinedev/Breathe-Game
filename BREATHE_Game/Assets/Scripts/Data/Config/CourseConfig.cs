using System;
using UnityEngine;

namespace Breathe.Data
{
    // Types of environmental zones that can appear on a course
    public enum ZoneType
    {
        Headwind,
        Tailwind,
        Waves,
        Calm,
        Crosswind,
        Cyclone,
        Doldrums,
        WaveChop
    }

    // Single zone entry placed along a course
    [Serializable]
    public struct ZoneDefinition
    {
        public ZoneType zoneType;
        [Range(0f, 1f)] public float positionAlongCourse;
        [Min(0f)] public float length;
    }

    // Tuning data for a race course — speed, zones, obstacles, etc.
    // Create new ones: Assets > Create > Breathe > Course Config
    [CreateAssetMenu(fileName = "CourseConfig", menuName = "Breathe/Course Config")]
    public class CourseConfig : ScriptableObject
    {
        [Header("Course Dimensions")]
        [SerializeField, Tooltip("Total course length in world units.")]
        private float _courseLength = 100f;

        [SerializeField, Range(0f, 1f), Tooltip("Where the finish line sits (0-1 along the course).")]
        private float _finishLinePosition = 1.0f;

        [Header("Speed")]
        [SerializeField, Tooltip("Forward speed with no breath bonus.")]
        private float _baseSpeed = 3f;

        [SerializeField, Tooltip("Multiplier on breath intensity for bonus speed.")]
        private float _breathBonusMultiplier = 5f;

        [Header("Pacing")]
        [SerializeField, Tooltip("Seconds before the finish line auto-spawns to keep races from dragging.")]
        private float _softTimeCap = 60f;

        [Header("Zones")]
        [SerializeField] private ZoneDefinition[] _zoneDefinitions = Array.Empty<ZoneDefinition>();

        [Header("Zone Spawning")]
        [SerializeField, Tooltip("Enable procedural zone spawning during the race.")]
        private bool _spawnEnvironmentalZones = true;

        [SerializeField, Tooltip("Average Y distance between spawn attempts.")]
        private float _zoneSpawnInterval = 18f;

        [SerializeField, Range(0f, 1f)]
        private float _zoneSpawnChance = 0.6f;

        [SerializeField, Min(0), Tooltip("Minimum zones guaranteed per race.")]
        private int _minZonesPerRace = 3;

        [SerializeField] private float _minZoneSpacing = 15f;

        [SerializeField, Tooltip("Normalized range (0-1) where zones can appear. Keeps start/finish clear.")]
        private Vector2 _zoneSpawnRange = new Vector2(0.08f, 0.92f);

        [SerializeField, Tooltip("No zones below this Y — gives the player a grace period at the start.")]
        private float _zoneGracePeriodY = 10f;

        [SerializeField, Tooltip("Per-type zone parameters. Auto-found at runtime if left empty.")]
        private EnvironmentalZoneConfig _zoneConfig;

        [Header("Crosswind Override")]
        [SerializeField, Range(0f, 20f), Tooltip("Lateral force for crosswind zones. 0 = use zone config defaults.")]
        private float _crosswindLateralForce = 8f;

        [Header("Obstacles")]
        [SerializeField, Tooltip("Normalized positions (0-1) for obstacle placement.")]
        private float[] _obstaclePositions = Array.Empty<float>();

        // --- Public accessors ---
        public float CourseLength => _courseLength;
        public float FinishLinePosition => _finishLinePosition;
        public float BaseSpeed => _baseSpeed;
        public float BreathBonusMultiplier => _breathBonusMultiplier;
        public float SoftTimeCap => _softTimeCap;
        public ZoneDefinition[] ZoneDefinitions => _zoneDefinitions;
        public bool SpawnEnvironmentalZones => _spawnEnvironmentalZones;
        public float ZoneSpawnInterval => _zoneSpawnInterval;
        public float ZoneSpawnChance => _zoneSpawnChance;
        public int MinZonesPerRace => _minZonesPerRace;
        public float MinZoneSpacing => _minZoneSpacing;
        public Vector2 ZoneSpawnRange => _zoneSpawnRange;
        public float ZoneGracePeriodY => _zoneGracePeriodY;
        public EnvironmentalZoneConfig ZoneConfig => _zoneConfig;
        public float CrosswindLateralForce => _crosswindLateralForce;
        public float[] ObstaclePositions => _obstaclePositions;
    }
}
