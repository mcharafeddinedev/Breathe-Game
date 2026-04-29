namespace Breathe.Input
{
    // Interface for any breath-sensing device (mic, fan, keyboard sim, etc.)
    public interface IBreathInput
    {
        float GetBreathIntensity();  // continuous 0-1
        int GetBreathLevel();        // discrete 0-5 based on BreathConfig thresholds
        bool IsBreathing();          // true if above the dead zone
        bool IsActive { get; }       // true if currently initialized and running

        void Initialize(); // open devices, allocate buffers, etc.
        void Shutdown();   // clean up everything from Initialize
    }
}
