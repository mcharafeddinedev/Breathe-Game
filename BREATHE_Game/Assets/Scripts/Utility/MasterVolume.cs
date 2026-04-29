using UnityEngine;

namespace Breathe.Utility
{
    /// <summary>
    /// Master is stored separately from Music/SFX: the slider persists 0 … <see cref="SliderNormalizedMax"/>
    /// (e.g. 1.5 = "150%") so players who want louder output can push past the former 100% ceiling.
    /// Mapped directly to <see cref="AudioListener.volume"/> — typical range is treated as linear gain.
    /// </summary>
    public static class MasterVolume
    {
        /// <summary>Right edge of the Master slider (persisted pref value).</summary>
        public const float SliderNormalizedMax = 1.5f;

        /// <summary>Hard clamp on what we send to the listener multiplier (protects sliders if prefs are corrupted).</summary>
        const float ListenerVolumeAbsoluteMax = 2f;

        public static float ClampStoredPreference(float raw) =>
            Mathf.Clamp(raw, 0f, SliderNormalizedMax);

        public static float PreferenceToListenerVolume(float storedClamped)
        {
            storedClamped = ClampStoredPreference(storedClamped);
            return Mathf.Clamp(storedClamped, 0f, ListenerVolumeAbsoluteMax);
        }

        public static string FormatPercent(float storedClamped) =>
            $"{Mathf.RoundToInt(ClampStoredPreference(storedClamped) * 100f)}%";

        public static void ApplyListenerFromStoredPreference(float rawFromPrefsOrDefault)
        {
            AudioListener.volume = PreferenceToListenerVolume(rawFromPrefsOrDefault);
        }
    }
}
