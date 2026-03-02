namespace Breathe.Utility
{
    // Converts game-unit speed to real sailing units (knots, mph).
    // Calibrated so game speeds roughly match small dinghy speeds:
    //   ~3 u/s idle = ~3 knots, ~8 u/s max = ~8 knots
    public static class WindSpeedConverter
    {
        public const float GameUnitsToKnots = 1f; // 1:1 by design
        public const float KnotsToMph = 1.15078f;

        public static float ToKnots(float gameSpeed) => gameSpeed * GameUnitsToKnots;
        public static float ToMph(float gameSpeed) => gameSpeed * GameUnitsToKnots * KnotsToMph;

        public static string Format(float gameSpeed)
        {
            return $"{ToKnots(gameSpeed):F1} kn  /  {ToMph(gameSpeed):F1} mph";
        }
    }
}
