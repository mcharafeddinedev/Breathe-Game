using UnityEngine;

namespace Breathe.Gameplay
{
    // Full-screen countdown overlay (3, 2, 1, GO!) shown when entering the Playing state.
    // Uses OnGUI for immediate rendering without scene setup.
    public class CountdownOverlay : MonoBehaviour
    {
        [SerializeField, Tooltip("How long each number stays on screen.")]
        private float _displayDuration = 0.8f;

        private string _currentText;
        private float _displayTimer;
        private float _animProgress;
        private GUIStyle _countdownStyle;

        private void Start()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnCountdownTick += HandleCountdownTick;
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnCountdownTick -= HandleCountdownTick;
        }

        private void HandleCountdownTick(int remaining)
        {
            _currentText = remaining > 0 ? remaining.ToString() : "GO!";
            _displayTimer = _displayDuration;
            _animProgress = 0f;
        }

        private void Update()
        {
            if (_displayTimer > 0f)
            {
                _displayTimer -= Time.unscaledDeltaTime;
                _animProgress = 1f - (_displayTimer / _displayDuration);
            }
            else
            {
                _currentText = null;
            }
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(_currentText)) return;

            if (_countdownStyle == null)
            {
                _countdownStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 120,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _countdownStyle.normal.textColor = Color.white;
            }

            float popScale = EaseOutBack(_animProgress);
            float alpha = _animProgress < 0.7f ? 1f : Mathf.Lerp(1f, 0f, (_animProgress - 0.7f) / 0.3f);

            int baseSize = 120;
            int animatedSize = Mathf.RoundToInt(baseSize * popScale);
            _countdownStyle.fontSize = Mathf.Max(10, animatedSize);

            Color textColor = _currentText == "GO!" 
                ? new Color(0.2f, 1f, 0.4f, alpha)
                : new Color(1f, 1f, 1f, alpha);
            _countdownStyle.normal.textColor = textColor;

            Color shadowColor = new Color(0f, 0f, 0f, alpha * 0.6f);

            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;
            float boxWidth = 400f;
            float boxHeight = 200f;
            Rect rect = new Rect(centerX - boxWidth / 2f, centerY - boxHeight / 2f, boxWidth, boxHeight);

            GUIStyle shadowStyle = new GUIStyle(_countdownStyle);
            shadowStyle.normal.textColor = shadowColor;
            Rect shadowRect = new Rect(rect.x + 4, rect.y + 4, rect.width, rect.height);
            GUI.Label(shadowRect, _currentText, shadowStyle);

            GUI.Label(rect, _currentText, _countdownStyle);
        }

        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
