using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    /// <summary>
    /// Interface for boats that can be affected by environmental zones.
    /// Implemented by <see cref="SailboatController"/> and <see cref="AICompanionController"/>.
    /// </summary>
    public interface IBoatEnvironmentalTarget
    {
        /// <summary>Whether this is the player boat (affects course recovery behavior).</summary>
        bool IsPlayer { get; }

        /// <summary>Current escape effort [0,1] — breath intensity for player, simulated for AI.</summary>
        float GetEscapeEffort();

        /// <summary>Normalized direction toward next waypoint (for breath-assisted escape). Zero if none.</summary>
        Vector3 GetCourseForwardDirection();

        /// <summary>Apply lateral force (crosswind). Called per-frame while in zone.</summary>
        void ApplyLateralForce(Vector2 force);

        /// <summary>Apply pull toward a center point (cyclone). Called per-frame.</summary>
        void ApplyPullToward(Vector3 center, float strength);

        /// <summary>Apply rotational torque (degrees per second).</summary>
        void ApplyRotation(float torqueDegreesPerSecond);

        /// <summary>Set speed multiplier from zone (headwind, tailwind, doldrums). Called each frame while in zone.</summary>
        void SetEnvironmentalSpeedMultiplier(float mult);

        /// <summary>Reset speed multiplier when leaving zone.</summary>
        void ClearEnvironmentalSpeedMultiplier();

        /// <summary>When true, boat applies rotation (e.g. cyclone center spin) and does not force face-up.</summary>
        void SetInZoneSpinMode(bool enable);

        /// <summary>Get WindSystem for player (to set environmental multiplier). Null for AI.</summary>
        WindSystem GetWindSystem();

        /// <summary>World position for force calculations (e.g. cyclone away-direction).</summary>
        Vector3 Position { get; }
    }
}
