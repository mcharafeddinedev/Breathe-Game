namespace Breathe.Utility
{
    /// <summary>
    /// Default linear volumes (0–1) applied on first launch and when a PlayerPrefs key is missing.
    /// Product defaults: Master 90% on a 0–150% slider, Music 50%, SFX 75%.
    /// <see cref="AudioVolumeBootstrap"/> persists these on first install; <see cref="AudioPrefsKeys"/>.
    /// </summary>
    public static class AudioMixDefaults
    {
        public const float MasterLinear = 0.9f;
        public const float MusicLinear = 0.5f;
        public const float SfxLinear = 0.75f;
    }
}
