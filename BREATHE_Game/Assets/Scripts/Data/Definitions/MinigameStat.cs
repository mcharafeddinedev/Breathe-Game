namespace Breathe.Data
{
    // Controls how a stat is displayed on the result screen.
    // Hero = large centerpiece (e.g. placement, main score).
    // Primary = medium row with PB badge potential.
    // Secondary = small detail row.
    public enum StatTier { Hero, Primary, Secondary }

    // Label/value pair shown on the result screen after a minigame.
    [System.Serializable]
    public struct MinigameStat
    {
        public string Label;
        public string Value;
        public bool IsPersonalBest;
        public StatTier Tier;

        public MinigameStat(string label, string value, bool isPersonalBest = false,
            StatTier tier = StatTier.Secondary)
        {
            Label = label;
            Value = value;
            IsPersonalBest = isPersonalBest;
            Tier = tier;
        }
    }
}
