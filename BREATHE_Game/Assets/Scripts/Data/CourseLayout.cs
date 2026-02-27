using System;
using UnityEngine;

namespace Breathe.Data
{
    /// <summary>
    /// A single sine harmonic that contributes to the course's horizontal curve.
    /// Multiple harmonics are summed to produce complex, organic course shapes.
    /// </summary>
    [Serializable]
    public struct CurveHarmonic
    {
        [Tooltip("Horizontal swing distance in world units.")]
        public float amplitude;

        [Tooltip("Number of full sine cycles over the course length.")]
        public float frequency;

        [Tooltip("Phase offset in radians. Shifts the wave start position.")]
        public float phase;

        public CurveHarmonic(float amplitude, float frequency, float phase = 0f)
        {
            this.amplitude = amplitude;
            this.frequency = frequency;
            this.phase = phase;
        }
    }

    /// <summary>
    /// A hand-designed course shape defined by one or more sine harmonics.
    /// <see cref="Breathe.Gameplay.CourseMarkers"/> randomly selects from a pool
    /// of these at race start so each run feels different.
    /// Create custom layouts via <c>Assets → Create → Breathe → Course Layout</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCourseLayout", menuName = "Breathe/Course Layout")]
    public class CourseLayout : ScriptableObject
    {
        [SerializeField, Tooltip("Display name for debug logging.")]
        private string _layoutName = "Unnamed";

        [SerializeField, Tooltip("Sine harmonics summed to define the course curve shape.")]
        private CurveHarmonic[] _harmonics = { new CurveHarmonic(5f, 2f, 0f) };

        /// <summary>Human-readable name for this layout.</summary>
        public string LayoutName => _layoutName;

        /// <summary>Harmonics that combine to form the course curve.</summary>
        public CurveHarmonic[] Harmonics => _harmonics;

        /// <summary>
        /// Evaluates the summed harmonic curve at a given Y position along the course.
        /// </summary>
        /// <param name="y">Distance along the course in world units.</param>
        /// <param name="courseLength">Total course length (used to normalize frequency).</param>
        /// <returns>Horizontal X offset at that Y position.</returns>
        public float EvaluateX(float y, float courseLength)
        {
            float x = 0f;
            for (int i = 0; i < _harmonics.Length; i++)
            {
                ref readonly CurveHarmonic h = ref _harmonics[i];
                x += Mathf.Sin(y / courseLength * h.frequency * Mathf.PI * 2f + h.phase)
                     * h.amplitude;
            }
            return x;
        }

        // -----------------------------------------------------------------
        // Built-in presets (used when no assets are assigned in the inspector)
        // -----------------------------------------------------------------

        /// <summary>
        /// Returns a set of hand-tuned course shapes covering a range of
        /// feel — from gentle S-curves to tight slaloms. These are used as
        /// the default pool when no <see cref="CourseLayout"/> assets are
        /// dragged into <see cref="Breathe.Gameplay.CourseMarkers"/>.
        /// </summary>
        public static CourseLayout[] CreateBuiltInPresets()
        {
            return new[]
            {
                // Gentle, flowing courses (easier)
                MakePreset("Gentle Drift",
                    new CurveHarmonic(6f, 1.5f, 0f),
                    new CurveHarmonic(2f, 0.5f, 1.5f)),

                MakePreset("Lazy River",
                    new CurveHarmonic(8f, 1f, 0.3f),
                    new CurveHarmonic(3f, 2.5f, 2f)),

                // Medium complexity - interesting curves
                MakePreset("Serpentine",
                    new CurveHarmonic(5f, 3f, 0f),
                    new CurveHarmonic(2.5f, 1.2f, 1f)),

                MakePreset("Rolling Hills",
                    new CurveHarmonic(7f, 2f, 0.5f),
                    new CurveHarmonic(3f, 4f, 0f),
                    new CurveHarmonic(1.5f, 7f, 2.2f)),

                MakePreset("Winding Path",
                    new CurveHarmonic(4f, 2.5f, 0f),
                    new CurveHarmonic(3f, 1f, 1.8f),
                    new CurveHarmonic(2f, 5f, 0.7f)),

                // More challenging - tighter turns
                MakePreset("Chicane",
                    new CurveHarmonic(6f, 2.5f, 0f),
                    new CurveHarmonic(3f, 5f, 1.2f)),

                MakePreset("Slalom Gates",
                    new CurveHarmonic(4f, 4f, 0f),
                    new CurveHarmonic(2f, 8f, 1.5f)),

                // Dramatic sweeping courses
                MakePreset("Grand Sweep",
                    new CurveHarmonic(10f, 0.7f, 0f),
                    new CurveHarmonic(4f, 2.2f, 2.5f)),

                MakePreset("Ocean Swell",
                    new CurveHarmonic(7f, 1.3f, 0f),
                    new CurveHarmonic(5f, 3f, 1.2f),
                    new CurveHarmonic(2f, 6f, 0.5f)),

                // Complex multi-harmonic courses
                MakePreset("Coastal Run",
                    new CurveHarmonic(5f, 1.8f, 0.2f),
                    new CurveHarmonic(3f, 3.5f, 1.5f),
                    new CurveHarmonic(2f, 0.6f, 0f)),

                MakePreset("Reef Passage",
                    new CurveHarmonic(4f, 2f, 0f),
                    new CurveHarmonic(3f, 4.5f, 0.8f),
                    new CurveHarmonic(1.5f, 9f, 2f)),

                MakePreset("Channel Markers",
                    new CurveHarmonic(6f, 1.5f, 1f),
                    new CurveHarmonic(2.5f, 4f, 0f),
                    new CurveHarmonic(1.5f, 2f, 2.5f)),
            };
        }

        private static CourseLayout MakePreset(string name, params CurveHarmonic[] harmonics)
        {
            var layout = CreateInstance<CourseLayout>();
            layout._layoutName = name;
            layout._harmonics = harmonics;
            return layout;
        }
    }
}
