using System;
using UnityEngine;

namespace Breathe.Gameplay
{
    // Shared static event bus for zone popup text.
    // Decoupled from any specific zone implementation so both EnvironmentalZoneEffect
    // and any future zone system can raise popups through one channel.
    public static class ZoneEvents
    {
        public static event Action<string> OnZonePopup;

        public static void RaiseZonePopup(string text)
        {
            int subs = OnZonePopup?.GetInvocationList()?.Length ?? 0;
            Debug.Log($"[ZoneEvents] Popup: \"{text}\" — subscribers: {subs}");
            OnZonePopup?.Invoke(text);
        }
    }
}
