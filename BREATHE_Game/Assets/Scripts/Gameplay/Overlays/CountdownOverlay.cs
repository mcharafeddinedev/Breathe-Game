using UnityEngine;
using Breathe.Data;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // Full-screen countdown overlay (3, 2, 1, GO!) shown when entering the Playing state.
    // The final "GO" text is configurable per minigame via MinigameDefinition.CountdownGoText.
    // Uses OnGUI for immediate rendering without scene setup.
    public class CountdownOverlay : MonoBehaviour
    {
        [SerializeField, Tooltip("How long each number stays on screen.")]
        private float _displayDuration = 0.8f;

        [SerializeField, Tooltip("Extra time the GO text lingers while floating up.")]
        private float _goFloatDuration = 1.1f;

        private string _currentText;
        private float _displayTimer;
        private float _animProgress;
        private bool _isGoTick;
        private GUIStyle _countdownStyle;
        private string _goText = "GO";

        private void Start()
        {
            ResolveGoText();

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnCountdownTick += HandleCountdownTick;
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnCountdownTick -= HandleCountdownTick;
        }

        private void ResolveGoText()
        {
            if (MinigameManager.Instance == null) return;
            MinigameDefinition def = MinigameManager.Instance.SelectedDefinition;
            if (def != null && !string.IsNullOrEmpty(def.CountdownGoText))
                _goText = def.CountdownGoText;
        }

        private void HandleCountdownTick(int remaining)
        {
            _isGoTick = remaining <= 0;
            _currentText = _isGoTick ? _goText : remaining.ToString();
            _displayTimer = _isGoTick ? _goFloatDuration : _displayDuration;
            _animProgress = 0f;
        }

        private void Update()
        {
            if (_displayTimer > 0f)
            {
                float dur = _isGoTick ? _goFloatDuration : _displayDuration;
                _displayTimer -= Time.unscaledDeltaTime;
                _animProgress = 1f - (_displayTimer / dur);
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
                    fontSize = 200,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
                _countdownStyle.normal.textColor = Color.white;
                Font f = GameFont.Get();
                if (f != null) _countdownStyle.font = f;
            }

            float popScale = EaseOutBack(_animProgress);
            bool fading = _animProgress >= 0.6f;
            float alpha = fading ? Mathf.Lerp(1f, 0f, (_animProgress - 0.6f) / 0.4f) : 1f;

            bool isGoText = _currentText == _goText;
            float baseSize = isGoText ? GoTextBaseSize(_goText) : 400f;
            float sizeScale = fading ? popScale + (1f - alpha) * 0.3f : popScale;
            int animatedSize = Mathf.RoundToInt(baseSize * sizeScale);
            _countdownStyle.fontSize = Mathf.Max(10, animatedSize);

            Color baseColor = isGoText
                ? new Color(0.2f, 1f, 0.4f, alpha)
                : new Color(1f, 1f, 1f, alpha);
            _countdownStyle.normal.textColor = baseColor;
            _countdownStyle.hover.textColor = baseColor;

            float centerX = Screen.width / 2f;
            float centerY = Screen.height / 2f;
            float boxWidth = Screen.width * 0.9f;
            float boxHeight = 560f;

            float yOffset = 0f;
            if (_isGoTick && fading)
            {
                float fadeT = (_animProgress - 0.6f) / 0.4f;
                float eased = fadeT * fadeT;
                yOffset = -eased * (centerY - boxHeight * 0.2f);
            }

            Rect rect = new Rect(centerX - boxWidth / 2f,
                                  centerY - boxHeight / 2f + yOffset,
                                  boxWidth, boxHeight);

            if (fading)
            {
                GUI.Label(rect, _currentText, _countdownStyle);
            }
            else
            {
                GameFont.OutlinedLabel(rect, _currentText, _countdownStyle, 2);
            }
        }

        private static float GoTextBaseSize(string text)
        {
            int len = text.Length;
            if (len <= 3) return 120f;
            if (len <= 6) return 100f;
            if (len <= 10) return 80f;
            return 64f;
        }

        private float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
