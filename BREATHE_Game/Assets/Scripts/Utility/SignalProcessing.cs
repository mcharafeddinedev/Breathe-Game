using UnityEngine;

namespace Breathe.Utility
{
    // Common signal processing helpers used by the breath input pipeline
    public static class SignalProcessing
    {
        // Exponential moving average — higher alpha = smoother but slower response
        public static float ExponentialMovingAverage(float current, float raw, float alpha)
        {
            return current * alpha + raw * (1f - alpha);
        }

        // Cuts values below the threshold to zero (noise gate)
        public static float DeadZone(float value, float threshold)
        {
            return value < threshold ? 0f : value;
        }

        // Remaps a value from one range to another, clamped to output bounds
        public static float MapRange(float value, float inMin, float inMax, float outMin, float outMax)
        {
            if (Mathf.Approximately(inMax, inMin))
                return outMin;

            float t = Mathf.Clamp01((value - inMin) / (inMax - inMin));
            return Mathf.Lerp(outMin, outMax, t);
        }

        // Returns a power level 0-5 based on where intensity falls in the threshold array.
        // Thresholds should be 5 ascending floats (boundaries between each level).
        public static int GetPowerLevel(float intensity, float[] thresholds)
        {
            if (thresholds == null || thresholds.Length < 5)
            {
                Debug.LogWarning("[SignalProcessing] GetPowerLevel needs exactly 5 thresholds.");
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
