using System;
using UnityEngine;
using Breathe.Data;

namespace Breathe.Gameplay
{
    // Displays a tutorial popup at the start of a minigame explaining how to play.
    // Pauses the game until Continue is pressed, then transitions to Playing (triggering countdown).
    // Tutorial text is pulled from the active MinigameDefinition asset, with serialized fallbacks.
    public class TutorialPopup : MonoBehaviour
    {
        [Header("Fallback Content (used if no MinigameDefinition)")]
        [SerializeField] private string _fallbackTitle = "HOW TO PLAY";
        [SerializeField, TextArea(2, 4)] private string _fallbackInstruction = "Blow steadily into the device to control the game!";
        [SerializeField, TextArea(1, 2)] private string _fallbackTip = "Longer, steady breaths work best.";

        [Header("Animation")]
        [SerializeField] private float _fadeInDuration = 0.3f;
        [SerializeField] private float _fadeOutDuration = 0.2f;

        private enum Phase { Hidden, FadingIn, Visible, FadingOut }
        private Phase _phase = Phase.Hidden;
        private float _timer;
        private float _alpha;

        private string _title;
        private string _instruction;
        private string _tip;

        private bool _stylesReady;
        private GUIStyle _overlayBg;
        private GUIStyle _panelBg;
        private GUIStyle _titleStyle;
        private GUIStyle _instructionStyle;
        private GUIStyle _tipStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _buttonHoverStyle;

        private Rect _buttonRect;
        private bool _buttonHovered;

        // Fired when the Continue button is pressed.
        public event Action OnContinuePressed;

        private void Awake()
        {
            _phase = Phase.Hidden;

            if (GameStateManager.Instance != null)
            {
                GameStateManager.Instance.OnTutorialStarted += ShowTutorial;

                if (GameStateManager.Instance.CurrentState == GameState.Tutorial)
                    ShowTutorial();
            }
        }

        private void OnDestroy()
        {
            if (GameStateManager.Instance != null)
                GameStateManager.Instance.OnTutorialStarted -= ShowTutorial;
        }

        // Called automatically when entering Tutorial state.
        public void ShowTutorial()
        {
            LoadTutorialContent();
            _phase = Phase.FadingIn;
            _timer = 0f;
            _alpha = 0f;
        }

        private void LoadTutorialContent()
        {
            var mgr = MinigameManager.Instance;
            MinigameDefinition def = mgr != null ? mgr.SelectedDefinition : null;

            if (def != null)
            {
                _title = !string.IsNullOrEmpty(def.TutorialTitle) ? def.TutorialTitle : _fallbackTitle;
                _instruction = !string.IsNullOrEmpty(def.TutorialInstruction) ? def.TutorialInstruction : _fallbackInstruction;
                _tip = !string.IsNullOrEmpty(def.TutorialTip) ? def.TutorialTip : _fallbackTip;
            }
            else
            {
                _title = _fallbackTitle;
                _instruction = _fallbackInstruction;
                _tip = _fallbackTip;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            switch (_phase)
            {
                case Phase.FadingIn:
                    _timer += dt;
                    _alpha = Mathf.Clamp01(_timer / _fadeInDuration);
                    if (_timer >= _fadeInDuration)
                    {
                        _phase = Phase.Visible;
                        _alpha = 1f;
                    }
                    break;

                case Phase.FadingOut:
                    _timer += dt;
                    _alpha = 1f - Mathf.Clamp01(_timer / _fadeOutDuration);
                    if (_timer >= _fadeOutDuration)
                    {
                        _phase = Phase.Hidden;
                        _alpha = 0f;

                        OnContinuePressed?.Invoke();

                        if (GameStateManager.Instance != null)
                            GameStateManager.Instance.TransitionTo(GameState.Playing);
                    }
                    break;
            }
        }

        private void OnContinueClicked()
        {
            if (_phase != Phase.Visible) return;
            _phase = Phase.FadingOut;
            _timer = 0f;
        }

        private void OnGUI()
        {
            if (_phase == Phase.Hidden) return;
            if (_alpha < 0.01f) return;

            BuildStyles();

            Color prevColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, _alpha);

            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _overlayBg);

            float pw = Mathf.Min(Screen.width * 0.75f, 650f);
            float ph = 380f;
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", _panelBg);

            float pad = 35f;
            float cy = py + pad;

            GUI.Label(new Rect(px, cy, pw, 45f), _title, _titleStyle);
            cy += 60f;

            float instrHeight = _instructionStyle.CalcHeight(new GUIContent(_instruction), pw - pad * 2f) + 8f;
            instrHeight = Mathf.Min(instrHeight, 140f);
            GUI.Label(new Rect(px + pad, cy, pw - pad * 2f, instrHeight), _instruction, _instructionStyle);
            cy += instrHeight + 20f;

            if (!string.IsNullOrEmpty(_tip))
            {
                float tipHeight = _tipStyle.CalcHeight(new GUIContent(_tip), pw - pad * 2f);
                tipHeight = Mathf.Min(tipHeight, 60f);
                GUI.Label(new Rect(px + pad, cy, pw - pad * 2f, tipHeight), _tip, _tipStyle);
                cy += tipHeight + 25f;
            }
            else
            {
                cy += 25f;
            }

            float btnWidth = 180f;
            float btnHeight = 50f;
            float btnX = px + (pw - btnWidth) * 0.5f;
            float btnY = py + ph - btnHeight - pad;
            _buttonRect = new Rect(btnX, btnY, btnWidth, btnHeight);

            Vector2 mousePos = Event.current.mousePosition;
            _buttonHovered = _buttonRect.Contains(mousePos) && _phase == Phase.Visible;

            GUIStyle btnStyle = _buttonHovered ? _buttonHoverStyle : _buttonStyle;

            if (GUI.Button(_buttonRect, "CONTINUE", btnStyle))
            {
                OnContinueClicked();
            }

            GUI.color = prevColor;
        }

        private void BuildStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _overlayBg = new GUIStyle();
            _overlayBg.normal.background = MakeTex(new Color(0f, 0f, 0f, 0.75f));

            _panelBg = new GUIStyle();
            _panelBg.normal.background = MakeTex(new Color(0.04f, 0.10f, 0.22f, 0.97f));

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.4f, 0.85f, 1f);

            _instructionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                padding = new RectOffset(10, 10, 0, 0)
            };
            _instructionStyle.normal.textColor = Color.white;

            _tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            _tipStyle.normal.textColor = new Color(0.65f, 0.85f, 0.65f);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(20, 20, 10, 10)
            };
            _buttonStyle.normal.background = MakeTex(new Color(0.15f, 0.5f, 0.9f, 1f));
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.background = MakeTex(new Color(0.2f, 0.6f, 1f, 1f));
            _buttonStyle.hover.textColor = Color.white;
            _buttonStyle.active.background = MakeTex(new Color(0.1f, 0.4f, 0.8f, 1f));
            _buttonStyle.active.textColor = Color.white;

            _buttonHoverStyle = new GUIStyle(_buttonStyle);
            _buttonHoverStyle.normal.background = MakeTex(new Color(0.2f, 0.6f, 1f, 1f));
            _buttonHoverStyle.fontSize = 23;
        }

        private static Texture2D MakeTex(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
