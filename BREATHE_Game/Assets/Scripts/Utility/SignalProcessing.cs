using UnityEngine;

namespace Breathe.Utility
{
    /// <summary>
    /// Static utility class providing common signal-processing operations
    /// used throughout the breath-input pipeline.
    /// </summary>
    public static class SignalProcessing
    {
        /// <summary>
        /// Compute an exponential moving average (EMA).
        /// </summary>
        /// <param name="current">The current smoothed value.</param>
        /// <param name="raw">The latest raw sample.</param>
        /// <param name="alpha">
        /// Smoothing factor in [0, 1]. Higher values retain more of the
        /// current value (smoother but slower response).
        /// </param>
        /// <returns>The new smoothed value.</returns>
        public static float ExponentialMovingAverage(float current, float raw, float alpha)
        {
            return current * alpha + raw * (1f - alpha);
        }

        /// <summary>
        /// Apply a dead-zone filter. Values below the threshold are clamped to zero.
        /// </summary>
        /// <param name="value">Input value (assumed non-negative).</param>
        /// <param name="threshold">Dead-zone threshold.</param>
        /// <returns>Zero if <paramref name="value"/> is below the threshold; otherwise <paramref name="value"/>.</returns>
        public static float DeadZone(float value, float threshold)
        {
            return value < threshold ? 0f : value;
        }

        /// <summary>
        /// Linearly re-map a value from one range to another, clamped to the output range.
        /// </summary>
        /// <param name="value">Input value.</param>
        /// <param name="inMin">Input range minimum.</param>
        /// <param name="inMax">Input range maximum.</param>
        /// <param name="outMin">Output range minimum.</param>
        /// <param name="outMax">Output range maximum.</param>
        /// <returns>The re-mapped and clamped value.</returns>
        public static float MapRange(float value, float inMin, float inMax, float outMin, float outMax)
        {
            if (Mathf.Approximately(inMax, inMin))
                return outMin;

            float t = (value - inMin) / (inMax - inMin);
            t = Mathf.Clamp01(t);
            return Mathf.Lerp(outMin, outMax, t);
        }

        /// <summary>
        /// Determine the discrete power level (0–5) for a given intensity value
        /// based on an ascending array of five threshold boundaries.
        /// </summary>
        /// <param name="intensity">Continuous intensity value to classify.</param>
        /// <param name="thresholds">
        /// Exactly five ascending floats representing the boundaries between
        /// levels 0→1, 1→2, 2→3, 3→4, and 4→5.
        /// </param>
        /// <returns>An integer in [0, 5].</returns>
        public static int GetPowerLevel(float intensity, float[] thresholds)
        {
            if (thresholds == null || thresholds.Length < 5)
            {
                Debug.LogWarning("[SignalProcessing] GetPowerLevel requires exactly 5 thresholds.");
                return 0;
            }

            for (int i = thresholds.Length - 1; i >= 0; i--)
            {
                if (intensity >= thresholds[i])
                    return i + 1;
            }

            return 0;
        }
    }
}
