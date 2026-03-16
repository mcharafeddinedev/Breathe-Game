using UnityEngine;

namespace Breathe.Utility
{
    // Shared pool of encouraging quotes for minigame end screens.
    // Supports general, breath-focused, winner, personal-best, and keep-trying pools.
    public static class EncouragingQuotes
    {
        private static readonly string[] GeneralQuotes =
        {
            "Nice job!",
            "Great work!",
            "Well done!",
            "Awesome effort!",
            "You did it!",
            "Fantastic!",
            "Way to go!",
            "Keep it up!",
            "Amazing!",
            "Super!",
            "Brilliant!",
            "Wonderful!",
            "Excellent!",
            "You're doing great!",
            "Impressive!",
            "Good playing!",
            "That was fun!",
            "Nice one!",
            "You rock!",
            "Stellar performance!",
            "Bravo!",
            "Outstanding!",
            "Terrific!",
            "You nailed it!",
            "Spectacular!"
        };

        private static readonly string[] BreathQuotes =
        {
            "Great breathing!",
            "Steady breaths!",
            "Nice lung power!",
            "Breathe easy!",
            "Strong exhales!",
            "Perfect pacing!",
            "Smooth sailing!",
            "Deep breaths pay off!",
            "Breath of fresh air!",
            "You've got wind!",
            "Powerful lungs!",
            "Breathtaking!",
            "In the zone!",
            "Rhythm master!",
            "Flow state achieved!"
        };

        private static readonly string[] WinnerQuotes =
        {
            "Champion!",
            "Victory!",
            "First place!",
            "Gold medal!",
            "You're #1!",
            "Top of the podium!",
            "Unbeatable!",
            "The best!",
            "Winner winner!",
            "Supreme!",
            "Legendary!",
            "Flawless victory!",
            "Crushed it!",
            "Dominated!",
            "Unstoppable!"
        };

        private static readonly string[] PersonalBestQuotes =
        {
            "New record!",
            "Personal best!",
            "You beat yourself!",
            "New high score!",
            "Record breaker!",
            "Best yet!",
            "Leveling up!",
            "Progress!",
            "Improvement!",
            "Getting stronger!",
            "Better every time!",
            "Growth mindset!",
            "Self-improvement!",
            "Breaking barriers!",
            "Surpassing limits!"
        };

        private static readonly string[] KeepTryingQuotes =
        {
            "Good effort!",
            "Nice try!",
            "Keep practicing!",
            "You'll get there!",
            "Almost!",
            "So close!",
            "Next time!",
            "Don't give up!",
            "Progress takes time!",
            "Every attempt counts!",
            "Learning curve!",
            "Building skills!",
            "Getting better!",
            "Stay determined!",
            "Persistence wins!"
        };

        public static string GetRandomQuote()
        {
            return GeneralQuotes[Random.Range(0, GeneralQuotes.Length)];
        }

        public static string GetBreathQuote()
        {
            return BreathQuotes[Random.Range(0, BreathQuotes.Length)];
        }

        public static string GetWinnerQuote()
        {
            return WinnerQuotes[Random.Range(0, WinnerQuotes.Length)];
        }

        public static string GetPersonalBestQuote()
        {
            return PersonalBestQuotes[Random.Range(0, PersonalBestQuotes.Length)];
        }

        public static string GetKeepTryingQuote()
        {
            return KeepTryingQuotes[Random.Range(0, KeepTryingQuotes.Length)];
        }

        // Picks from the most fitting pool based on placement and personal-best status.
        public static string GetContextualQuote(int placement, bool isPersonalBest)
        {
            // Personal best takes priority for encouragement
            if (isPersonalBest)
                return GetPersonalBestQuote();

            // First place gets winner quotes
            if (placement == 1)
                return GetWinnerQuote();

            // Podium (2nd/3rd) gets general positive quotes
            if (placement <= 3)
                return GetRandomQuote();

            // Lower placements get keep-trying encouragement
            return GetKeepTryingQuote();
        }

        // Same as GetContextualQuote but mixes in breath-focused quotes when activity is high.
        public static string GetBreathGameQuote(int placement, bool isPersonalBest, float activityRatio)
        {
            // High activity ratio? Highlight the breathing
            if (activityRatio >= 0.6f && Random.value > 0.4f)
                return GetBreathQuote();

            return GetContextualQuote(placement, isPersonalBest);
        }
    }
}
