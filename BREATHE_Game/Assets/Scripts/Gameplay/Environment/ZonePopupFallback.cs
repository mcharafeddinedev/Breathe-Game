using UnityEngine;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // Fallback zone popup using OnGUI when HUDController (TextMeshPro popup) is absent.
    // Created by CourseManager.EnsureOverlays when missing.
    public class ZonePopupFallback : MonoBehaviour
    {
        private string _currentText;
        private float _showUntil;
        private GUIStyle _style;

        private void OnEnable()
        {
            ZoneEvents.OnZonePopup += HandlePopup;
        }

        private void OnDisable()
        {
            ZoneEvents.OnZonePopup -= HandlePopup;
        }

        private void HandlePopup(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _currentText = text;
            _showUntil = Time.time + 1.5f;
            Debug.Log($"[ZonePopupFallback] Showing: \"{text}\"");
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_currentText) || Time.time > _showUntil) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 34,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    wordWrap = false,
                    normal = { textColor = Color.white }
                };
                // Flatten all states so text doesn't highlight on hover
                _style.hover = _style.normal;
                _style.active = _style.normal;
                _style.focused = _style.normal;
                Font f = GameFont.Get();
                if (f != null) _style.font = f;
            }

            float w = Screen.width * 0.8f;
            float h = 60f;
            float topOffset = 100f;
            Rect r = new Rect((Screen.width - w) / 2f, topOffset, w, h);

            GameFont.OutlinedLabel(r, _currentText, _style);
        }
    }
}
