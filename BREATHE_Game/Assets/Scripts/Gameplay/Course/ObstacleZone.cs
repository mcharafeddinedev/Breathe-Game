using System;
using UnityEngine;
using UnityEngine.Events;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Trigger-based environmental zone that modifies WindSystem effectiveness
    // while the player boat is inside. Also fires a popup text event for UI.
    [RequireComponent(typeof(Collider2D))]
    public class ObstacleZone : MonoBehaviour
    {
        [Header("Zone Settings")]
        [SerializeField] private ZoneType _zoneType = ZoneType.Calm;
        [SerializeField, Tooltip("Wind multiplier inside zone (headwind ~0.5, tailwind ~1.5, calm = 1.0).")]
        private float _effectMultiplier = 1.0f;

        [Header("UI Feedback")]
        [SerializeField] private string _zonePopupText = "Zone!";
        [SerializeField] private UnityEvent<string> _onZoneEntered;

        // Static event any listener can use for zone-popup text
        public static event Action<string> OnZonePopup;

        public static void RaiseZonePopup(string text)
        {
            int subs = OnZonePopup?.GetInvocationList()?.Length ?? 0;
            Debug.Log($"[ObstacleZone.RaiseZonePopup] \"{text}\" — subscribers: {subs}");
            OnZonePopup?.Invoke(text);
        }

        public ZoneType ZoneType => _zoneType;
        public float EffectMultiplier => _effectMultiplier;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            WindSystem wind = other.GetComponentInParent<WindSystem>();
            if (wind == null) wind = FindAnyObjectByType<WindSystem>();
            if (wind != null) wind.SetEnvironmentalMultiplier(_effectMultiplier);

            _onZoneEntered?.Invoke(_zonePopupText);
            OnZonePopup?.Invoke(_zonePopupText);
            Debug.Log($"[ObstacleZone] Entered {_zoneType} — multiplier {_effectMultiplier:F2}.");
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            WindSystem wind = other.GetComponentInParent<WindSystem>();
            if (wind == null) wind = FindAnyObjectByType<WindSystem>();
            if (wind != null) wind.SetEnvironmentalMultiplier(1f);

            Debug.Log($"[ObstacleZone] Exited {_zoneType} — multiplier reset.");
        }
    }
}
