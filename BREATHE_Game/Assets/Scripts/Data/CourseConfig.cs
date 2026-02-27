using System;
using UnityEngine;

namespace Breathe.Data
{
    /// <summary>Defines the types of environmental zones the player can encounter on a course.</summary>
    public enum ZoneType
    {
        Headwind,
        Tailwind,
        Waves,
        Calm
    }

    /// <summary>
    /// Describes a single environmental zone placed along the course.
    /// </summary>
    [Serializable]
    public struct ZoneDefinition
    {
        [Tooltip("Environmental zone type.")]
        public ZoneType zoneType;

        [Tooltip("Normalized position along the course (0 = start, 1 = finish)."), Range(0f, 1f)]
        public float positionAlongCourse;

        [Tooltip("Length of the zone in world units."), Min(0f)]
        public float length;
    }

    /// <summary>
    /// Tuning data for a single race course.
    /// Create instances via <c>Assets → Create → Breathe → Course Config</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "CourseConfig", menuName = "Breathe/Course Config")]
    public class CourseConfig : ScriptableObject
    {
        [Header("Course Dimensions")]
        [SerializeField, Tooltip("Total length of the course in world units.")]
        private float _courseLength = 100f;

        [SerializeField, Tooltip("Normalized position along the course where the finish line sits (0–1)."), Range(0f, 1f)]
        private float _finishLinePosition = 1.0f;

        [Header("Speed")]
        [SerializeField, Tooltip("Base forward speed when no breath bonus is applied.")]
        private float _baseSpeed = 3f;

        [SerializeField, Tooltip("Multiplier applied to breath intensity for bonus speed.")]
        private float _breathBonusMultiplier = 5f;

        [Header("Pacing")]
        [SerializeField, Tooltip("Seconds before the finish line auto-spawns ahead of the player.")]
        private float _softTimeCap = 60f;

        [Header("Zones")]
        [SerializeField, Tooltip("Environmental zones placed along the course.")]
        private ZoneDefinition[] _zoneDefinitions = Array.Empty<ZoneDefinition>();

        [Header("Obstacles")]
        [SerializeField, Tooltip("Normalized positions (0–1) of obstacles along the course.")]
        private float[] _obstaclePositions = Array.Empty<float>();

        /// <summary>Total length of the course in world units.</summary>
        public float CourseLength => _courseLength;

        /// <summary>Normalized finish-line position along the course.</summary>
        public float FinishLinePosition => _finishLinePosition;

        /// <summary>Base forward speed when no breath bonus is applied.</summary>
        public float BaseSpeed => _baseSpeed;

        /// <summary>Multiplier applied to breath intensity for bonus speed.</summary>
        public float BreathBonusMultiplier => _breathBonusMultiplier;

        /// <summary>Seconds before the finish line auto-spawns ahead of the player.</summary>
        public float SoftTimeCap => _softTimeCap;

        /// <summary>Environmental zones placed along the course.</summary>
        public ZoneDefinition[] ZoneDefinitions => _zoneDefinitions;

        /// <summary>Normalized obstacle positions along the course.</summary>
        public float[] ObstaclePositions => _obstaclePositions;
    }
}
