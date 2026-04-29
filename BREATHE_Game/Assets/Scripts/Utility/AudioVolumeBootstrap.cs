using UnityEngine;

namespace Breathe.Utility
{
    /// <summary>
    /// Ensures first-time installs persist <see cref="AudioMixDefaults"/> values and applies master gain
    /// before any scene/audio scripts run — avoids Unity keeping global listener volume at ~100% briefly.
    /// </summary>
    public static class AudioVolumeBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void EnsurePersistedDefaultsAndApplyMaster()
        {
            bool wrote = false;

            void EnsureKey(string key, float defaultLinear)
            {
                if (!PlayerPrefs.HasKey(key))
                {
                    float v = key == AudioPrefsKeys.MasterVolume
                        ? MasterVolume.ClampStoredPreference(defaultLinear)
                        : Mathf.Clamp01(defaultLinear);
                    PlayerPrefs.SetFloat(key, v);
                    wrote = true;
                }
            }

            EnsureKey(AudioPrefsKeys.MasterVolume, AudioMixDefaults.MasterLinear);
            EnsureKey(AudioPrefsKeys.MusicVolume, AudioMixDefaults.MusicLinear);
            EnsureKey(AudioPrefsKeys.SfxVolume, AudioMixDefaults.SfxLinear);

            if (wrote)
                PlayerPrefs.Save();

            ApplyMasterFromPrefs();
        }

        /// <summary>
        /// After <see cref="PlayerPrefs.DeleteAll"/> (or wipe), restores shipped audio sliders (Master / Music / SFX)
        /// so the next boot matches <see cref="AudioMixDefaults"/> without relying on implicit GetFloat defaults.
        /// </summary>
        public static void ReapplyShippingAudioDefaults()
        {
            PlayerPrefs.SetFloat(AudioPrefsKeys.MasterVolume, MasterVolume.ClampStoredPreference(AudioMixDefaults.MasterLinear));
            PlayerPrefs.SetFloat(AudioPrefsKeys.MusicVolume, Mathf.Clamp01(AudioMixDefaults.MusicLinear));
            PlayerPrefs.SetFloat(AudioPrefsKeys.SfxVolume, Mathf.Clamp01(AudioMixDefaults.SfxLinear));
            PlayerPrefs.Save();

            ApplyMasterFromPrefs();
        }

        static void ApplyMasterFromPrefs()
        {
            float raw = PlayerPrefs.GetFloat(AudioPrefsKeys.MasterVolume, AudioMixDefaults.MasterLinear);
            MasterVolume.ApplyListenerFromStoredPreference(raw);
        }
    }
}
