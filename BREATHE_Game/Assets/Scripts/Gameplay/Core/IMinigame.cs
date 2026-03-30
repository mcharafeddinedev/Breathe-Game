using System.Collections.Generic;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Contract every minigame controller implements.
    // GameStateManager, MinigameManager, the result overlay, and the debug overlay
    // drive any minigame through this interface without knowing its specifics.
    public interface IMinigame
    {
        string MinigameId { get; }
        bool IsComplete { get; }

        void OnMinigameStart();
        void OnMinigameEnd();

        // Tiered label/value pairs for the data-driven result screen
        MinigameStat[] GetEndStats();

        // Dynamic celebration heading (e.g. "AMAZING SAILING!", "BALLOON MASTER!")
        string GetCelebrationTitle();

        // Personal-best message (or empty string if none)
        string GetPersonalBestMessage();

        // Static header bar text for the result screen (e.g. "RACE COMPLETE", "TIME'S UP!")
        string GetResultTitle();

        // Game-specific key-value pairs rendered in the debug overlay's custom section.
        // Return null or empty to show only the shared base section.
        Dictionary<string, string> GetDebugInfo();
    }
}
