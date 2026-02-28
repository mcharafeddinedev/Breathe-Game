namespace Breathe.Input
{
    /// <summary>
    /// Abstraction for any breath-sensing input device.
    /// Implementations may read from a microphone, fan anemometer,
    /// gamepad, keyboard simulation, or any future hardware.
    /// </summary>
    public interface IBreathInput
    {
        /// <summary>
        /// Returns the current breath intensity as a continuous value in [0, 1].
        /// 0 = silence / no airflow, 1 = maximum detected breath force.
        /// </summary>
        float GetBreathIntensity();

        /// <summary>
        /// Returns a discrete power level in [0, 5] derived from the continuous
        /// intensity and the threshold table defined in <see cref="Data.BreathConfig"/>.
        /// </summary>
        int GetBreathLevel();

        /// <summary>
        /// Returns <c>true</c> when the breath intensity exceeds the configured
        /// dead-zone threshold, indicating the player is actively breathing.
        /// </summary>
        bool IsBreathing();

        /// <summary>
        /// Perform any one-time setup required by this input source
        /// (open devices, allocate buffers, start background threads, etc.).
        /// </summary>
        void Initialize();

        /// <summary>
        /// Release all resources acquired during <see cref="Initialize"/>
        /// (close devices, stop threads, free buffers).
        /// </summary>
        void Shutdown();
    }
}
