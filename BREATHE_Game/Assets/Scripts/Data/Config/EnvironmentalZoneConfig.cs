using UnityEngine;

namespace Breathe.Data
{
    // Per-zone-type tuning for environmental hazards.
    // Create: Assets > Create > Breathe > Environmental Zone Config
    [CreateAssetMenu(fileName = "ZoneConfig", menuName = "Breathe/Environmental Zone Config")]
    public class EnvironmentalZoneConfig : ScriptableObject
    {
        [System.Serializable]
        public struct ZoneParameters
        {
            public ZoneType type;
            [Tooltip("Speed multiplier (0.4-0.6 headwind, 1.3-1.8 tailwind, ~0.1 doldrums).")]
            public float speedMultiplier;
            public float lateralForce;    // crosswind push per second
            public float pullForce;       // pull toward center (cyclone)
            public float rotationalTorque;
            public float baseEscapeTime;  // seconds to escape naturally
            [Tooltip("How much breath shortens escape time.")]
            public float breathEscapeMultiplier;
            public float lateralDirection; // -1 left, 1 right, 0 = random
            public Color visualColor;
            public string popupText;
        }

        [SerializeField] private ZoneParameters[] _zoneParameters = new ZoneParameters[0];

        public ZoneParameters GetParameters(ZoneType type)
        {
            if (_zoneParameters == null) return default;
            foreach (var p in _zoneParameters)
                if (p.type == type) return p;
            return GetDefaultParameters(type);
        }

        // Fallback defaults when no config asset is assigned
        public static ZoneParameters GetDefaultParameters(ZoneType type)
        {
            return type switch
            {
                ZoneType.Headwind => new ZoneParameters
                {
                    type = ZoneType.Headwind,
                    speedMultiplier = 0.5f, lateralForce = 3f, pullForce = 0f,
                    rotationalTorque = 28f, baseEscapeTime = 3.5f, breathEscapeMultiplier = 0.9f,
                    lateralDirection = 0f,
                    visualColor = new Color(0.75f, 0.78f, 0.85f, 0.65f),
                    popupText = "Headwind! Push harder to get past!"
                },
                ZoneType.Tailwind => new ZoneParameters
                {
                    type = ZoneType.Tailwind,
                    speedMultiplier = 1.9f, lateralForce = 0f, pullForce = 0f,
                    rotationalTorque = 0f, baseEscapeTime = 0f, breathEscapeMultiplier = 0f,
                    lateralDirection = 0f,
                    visualColor = new Color(0.5f, 0.95f, 0.65f, 0.6f),
                    popupText = "Tailwind! Free boost!"
                },
                ZoneType.Crosswind => new ZoneParameters
                {
                    type = ZoneType.Crosswind,
                    speedMultiplier = 0.85f, lateralForce = 8f, pullForce = 0f,
                    rotationalTorque = 0f, baseEscapeTime = 2.8f, breathEscapeMultiplier = 0.7f,
                    lateralDirection = 0f,
                    visualColor = new Color(0.65f, 0.68f, 0.75f, 0.6f),
                    popupText = "Crosswind! Push through it!"
                },
                ZoneType.Cyclone => new ZoneParameters
                {
                    type = ZoneType.Cyclone,
                    speedMultiplier = 0.55f, lateralForce = 0f, pullForce = 10f,
                    rotationalTorque = 100f, baseEscapeTime = 3f, breathEscapeMultiplier = 0.8f,
                    lateralDirection = 0f,
                    visualColor = new Color(0.35f, 0.45f, 0.6f, 0.7f),
                    popupText = "Vortex! Push hard to escape quicker!"
                },
                ZoneType.Doldrums => new ZoneParameters
                {
                    type = ZoneType.Doldrums,
                    speedMultiplier = 0f, lateralForce = 1f, pullForce = 0f,
                    rotationalTorque = 22f, baseEscapeTime = 3.5f, breathEscapeMultiplier = 0.9f,
                    lateralDirection = 0f,
                    visualColor = new Color(0.72f, 0.74f, 0.78f, 0.4f),
                    popupText = "Doldrums — don't stall out!"
                },
                ZoneType.WaveChop => new ZoneParameters
                {
                    type = ZoneType.WaveChop,
                    speedMultiplier = 0.7f, lateralForce = 4.5f, pullForce = 0f,
                    rotationalTorque = 45f, baseEscapeTime = 2.2f, breathEscapeMultiplier = 0.6f,
                    lateralDirection = 0f,
                    visualColor = new Color(0.45f, 0.7f, 0.95f, 0.6f),
                    popupText = "Choppy waters! Keep a steady rhythm."
                },
                ZoneType.Waves => new ZoneParameters
                {
                    type = ZoneType.Waves,
                    speedMultiplier = 0.75f, lateralForce = 1.5f, pullForce = 0f,
                    rotationalTorque = 30f, baseEscapeTime = 2.2f, breathEscapeMultiplier = 0.6f,
                    lateralDirection = 0f,
                    visualColor = new Color(0.45f, 0.7f, 0.95f, 0.55f),
                    popupText = "Rolling waves! Steady breathing always helps."
                },
                ZoneType.Calm => new ZoneParameters
                {
                    type = ZoneType.Calm,
                    speedMultiplier = 0.1f, lateralForce = 0f, pullForce = 0f,
                    rotationalTorque = 0f, baseEscapeTime = 0f, breathEscapeMultiplier = 0f,
                    lateralDirection = 0f,
                    visualColor = new Color(0.7f, 0.73f, 0.78f, 0.3f),
                    popupText = "Calm waters here — smooth sailing."
                },
                _ => default
            };
        }
    }
}
