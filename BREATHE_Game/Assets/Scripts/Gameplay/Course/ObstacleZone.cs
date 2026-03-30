using UnityEngine;
using UnityEngine.Events;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Trigger-based environmental zone that fires popup text events for UI
    // when the player boat enters/exits.
    [RequireComponent(typeof(Collider2D))]
    public class ObstacleZone : MonoBehaviour
    {
        [Header("Zone Settings")]
        [SerializeField] private ZoneType _zoneType = ZoneType.Calm;
        [SerializeField, Tooltip("Wind multiplier inside zone (headwind ~0.5, tailwind ~1.5, calm = 1.0).")]
        private float _effectMultiplier = 1.0f;

        [Header("UI Feedback")]
        [SerializeField] private string _zonePopupText = "ZONE";
        [SerializeField] private UnityEvent<string> _onZoneEntered;

        public ZoneType ZoneType => _zoneType;
        public float EffectMultiplier => _effectMultiplier;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            _onZoneEntered?.Invoke(_zonePopupText);
            ZoneEvents.RaiseZonePopup(_zonePopupText);
            Debug.Log($"[ObstacleZone] Entered {_zoneType}.");
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            Debug.Log($"[ObstacleZone] Exited {_zoneType} — multiplier reset.");
        }
    }
}
