using Breathe.Data;

namespace Breathe.Gameplay
{
    // Contract every minigame controller implements.
    // GameStateManager, MinigameManager, and the celebration screen drive any
    // minigame through this interface without knowing its specifics.
    public interface IMinigame
    {
        string MinigameId { get; }
        bool IsComplete { get; }

        // Called when countdown finishes — reset state, start timers, enable input
        void OnMinigameStart();

        // Called when minigame ends (natural finish or quit) — freeze gameplay, finalize stats
        void OnMinigameEnd();

        // Label/value pairs for the celebration screen
        MinigameStat[] GetEndStats();

        // e.g. "GREAT SAILING!" or "BALLOON MASTER!"
        string GetCelebrationTitle();

        // Personal-best message (or empty string if none)
        string GetPersonalBestMessage();
    }
}
