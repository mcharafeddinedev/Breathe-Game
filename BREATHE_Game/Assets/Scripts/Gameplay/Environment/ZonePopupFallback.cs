using UnityEngine;

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
            ObstacleZone.OnZonePopup += HandlePopup;
        }

        private void OnDisable()
        {
            ObstacleZone.OnZonePopup -= HandlePopup;
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
                _style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = 22,
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    wordWrap = true,
                    normal = { textColor = Color.white },
                    padding = new RectOffset(24, 24, 16, 16)
                };
            }

            float w = 520f;
            float h = 90f;
            float topOffset = 100f;
            Rect r = new Rect((Screen.width - w) / 2f, topOffset, w, h);

            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Box(r, "");
            GUI.color = Color.white;
            GUI.Label(r, _currentText, _style);
        }
    }
}
