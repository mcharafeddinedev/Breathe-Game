using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Interface for any boat that can be affected by environmental zones.
    // Both SailboatController (player) and AICompanionController implement this.
    public interface IBoatEnvironmentalTarget
    {
        bool IsPlayer { get; }
        Vector3 Position { get; }
        float GetEscapeEffort();            // 0-1, breath intensity for player, simulated for AI
        Vector3 GetCourseForwardDirection(); // toward next waypoint, zero if none
        void ApplyLateralForce(Vector2 force);
        void ApplyPullToward(Vector3 center, float strength);
        void ApplyRotation(float torqueDegreesPerSecond);
        void SetEnvironmentalSpeedMultiplier(float mult);
        void ClearEnvironmentalSpeedMultiplier();
        void SetInZoneSpinMode(bool enable); // cyclone spin — don't force face-up
        BreathPowerSystem GetBreathPowerSystem(); // only player has one, AI returns null
    }
}
