using UnityEngine;

namespace Breathe.Utility
{
    // Shared pool of encouraging quotes for minigame end screens.
    // Supports general, breath-focused, winner, personal-best, and keep-trying pools.
    public static class EncouragingQuotes
    {
        private static readonly string[] GeneralQuotes =
        {
            "NICE  JOB",
            "GREAT  WORK",
            "WELL  DONE",
            "AWESOME  EFFORT",
            "YOU  DID  IT",
            "FANTASTIC",
            "WAY  TO  GO",
            "KEEP  IT  UP",
            "AMAZING",
            "SUPER",
            "BRILLIANT",
            "WONDERFUL",
            "EXCELLENT",
            "IMPRESSIVE",
            "GOOD  PLAYING",
            "THAT  WAS  FUN",
            "NICE  ONE",
            "YOU  ROCK",
            "STELLAR  PERFORMANCE",
            "BRAVO",
            "OUTSTANDING",
            "TERRIFIC",
            "YOU  NAILED  IT",
            "SPECTACULAR"
        };

        private static readonly string[] BreathQuotes =
        {
            "GREAT  BREATHING",
            "STEADY  BREATHS",
            "NICE  LUNG  POWER",
            "BREATHE  EASY",
            "STRONG  EXHALES",
            "PERFECT  PACING",
            "SMOOTH  SAILING",
            "DEEP  BREATHS  PAY  OFF",
            "BREATH  OF  FRESH  AIR",
            "POWERFUL  LUNGS",
            "BREATHTAKING",
            "IN  THE  ZONE",
            "RHYTHM  MASTER",
            "FLOW  STATE  ACHIEVED"
        };

        private static readonly string[] WinnerQuotes =
        {
            "CHAMPION",
            "VICTORY",
            "FIRST  PLACE",
            "GOLD  MEDAL",
            "TOP  OF  THE  PODIUM",
            "UNBEATABLE",
            "THE  BEST",
            "WINNER  WINNER",
            "SUPREME",
            "LEGENDARY",
            "FLAWLESS  VICTORY",
            "CRUSHED  IT",
            "DOMINATED",
            "UNSTOPPABLE"
        };

        private static readonly string[] PersonalBestQuotes =
        {
            "NEW  RECORD",
            "PERSONAL  BEST",
            "YOU  BEAT  YOURSELF",
            "NEW  HIGH  SCORE",
            "RECORD  BREAKER",
            "BEST  YET",
            "LEVELING  UP",
            "PROGRESS",
            "IMPROVEMENT",
            "GETTING  STRONGER",
            "BETTER  EVERY  TIME",
            "BREAKING  BARRIERS",
            "SURPASSING  LIMITS"
        };

        private static readonly string[] KeepTryingQuotes =
        {
            "GOOD  EFFORT",
            "NICE  TRY",
            "KEEP  PRACTICING",
            "ALMOST",
            "SO  CLOSE",
            "NEXT  TIME",
            "PROGRESS  TAKES  TIME",
            "EVERY  ATTEMPT  COUNTS",
            "BUILDING  SKILLS",
            "GETTING  BETTER",
            "STAY  DETERMINED",
            "PERSISTENCE  WINS"
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
            if (activityRatio >= 0.6f && Random.value > 0.4f)
                return GetBreathQuote();

            return GetContextualQuote(placement, isPersonalBest);
        }

        // For non-race minigames that have no placement concept.
        public static string GetMinigameQuote(bool isPersonalBest, float activityRatio)
        {
            if (isPersonalBest)
                return GetPersonalBestQuote();
            if (activityRatio >= 0.6f && Random.value > 0.4f)
                return GetBreathQuote();
            return GetRandomQuote();
        }
    }
}
