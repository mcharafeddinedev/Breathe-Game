using System;
using UnityEngine;

namespace Breathe.Data
{
    // One sine wave that gets summed with others to shape the course's horizontal curve.
    [Serializable]
    public struct CurveHarmonic
    {
        [Tooltip("Horizontal swing distance in world units.")]
        public float amplitude;
        [Tooltip("Full sine cycles over the course length.")]
        public float frequency;
        public float phase; // offset in radians

        public CurveHarmonic(float amplitude, float frequency, float phase = 0f)
        {
            this.amplitude = amplitude;
            this.frequency = frequency;
            this.phase = phase;
        }
    }

    // A course shape built from layered sine harmonics.
    // CourseMarkers picks one at random each race so the path feels different every time.
    [CreateAssetMenu(fileName = "NewCourseLayout", menuName = "Breathe/Course Layout")]
    public class CourseLayout : ScriptableObject
    {
        [SerializeField] private string _layoutName = "Unnamed";
        [SerializeField, Tooltip("Sine harmonics that get summed to define the curve.")]
        private CurveHarmonic[] _harmonics = { new CurveHarmonic(5f, 2f, 0f) };

        public string LayoutName => _layoutName;
        public CurveHarmonic[] Harmonics => _harmonics;

        // Get the horizontal offset at a given Y position along the course
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

        // --- Built-in presets (fallback when no custom layouts are assigned) ---

        // Hand-tuned shapes ranging from gentle drifts to tight slaloms.
        // Used as defaults if nothing is dragged into CourseMarkers in the inspector.
        public static CourseLayout[] CreateBuiltInPresets()
        {
            return new[]
            {
                // Gentle, flowing
                MakePreset("Gentle Drift",
                    new CurveHarmonic(6f, 1.5f, 0f),
                    new CurveHarmonic(2f, 0.5f, 1.5f)),
                MakePreset("Lazy River",
                    new CurveHarmonic(8f, 1f, 0.3f),
                    new CurveHarmonic(3f, 2.5f, 2f)),

                // Medium complexity
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

                // Tighter turns
                MakePreset("Chicane",
                    new CurveHarmonic(6f, 2.5f, 0f),
                    new CurveHarmonic(3f, 5f, 1.2f)),
                MakePreset("Slalom Gates",
                    new CurveHarmonic(4f, 4f, 0f),
                    new CurveHarmonic(2f, 8f, 1.5f)),

                // Big sweeping
                MakePreset("Grand Sweep",
                    new CurveHarmonic(10f, 0.7f, 0f),
                    new CurveHarmonic(4f, 2.2f, 2.5f)),
                MakePreset("Ocean Swell",
                    new CurveHarmonic(7f, 1.3f, 0f),
                    new CurveHarmonic(5f, 3f, 1.2f),
                    new CurveHarmonic(2f, 6f, 0.5f)),

                // Complex multi-harmonic
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
