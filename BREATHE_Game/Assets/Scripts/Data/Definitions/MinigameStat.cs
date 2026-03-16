namespace Breathe.Data
{
    // Label/value pair shown on the celebration screen after a race.
    [System.Serializable]
    public struct MinigameStat
    {
        public string Label;
        public string Value;
        public bool IsPersonalBest;

        public MinigameStat(string label, string value, bool isPersonalBest = false)
        {
            Label = label;
            Value = value;
            IsPersonalBest = isPersonalBest;
        }
    }
}
