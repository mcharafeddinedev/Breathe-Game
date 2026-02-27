namespace Breathe.Utility
{
    /// <summary>
    /// Converts game-unit speed values to realistic sailing speed units.
    /// Calibrated for small sailboats (dinghies / Sunfish):
    ///   BaseSpeed ~3 u/s  →  ~3 knots at idle
    ///   Max       ~8 u/s  →  ~8 knots at full breath
    /// Scale factor is exposed for tuning without touching game physics.
    /// </summary>
    public static class WindSpeedConverter
    {
        /// <summary>
        /// Scale factor from game units/sec to knots.
        /// 1:1 by default — game speeds map directly to realistic knot values
        /// for small recreational sailboats.
        /// </summary>
        public const float GameUnitsToKnots = 1f;

        /// <summary>Standard nautical conversion: 1 knot = 1.15078 statute mph.</summary>
        public const float KnotsToMph = 1.15078f;

        public static float ToKnots(float gameSpeed) => gameSpeed * GameUnitsToKnots;
        public static float ToMph(float gameSpeed) => gameSpeed * GameUnitsToKnots * KnotsToMph;

        /// <summary>Format speed as "X.X kn / X.X mph".</summary>
        public static string Format(float gameSpeed)
        {
            return $"{ToKnots(gameSpeed):F1} kn  /  {ToMph(gameSpeed):F1} mph";
        }
    }
}
