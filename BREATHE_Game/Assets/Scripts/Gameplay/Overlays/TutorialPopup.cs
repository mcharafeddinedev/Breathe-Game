using System;
using UnityEngine;
using Breathe.Data;
using Breathe.Input;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // Displays a tutorial popup at the start of a minigame explaining how to play.
    // Pauses the game until Continue is pressed, then transitions to Playing (triggering countdown).
    // Tutorial text is pulled from the active MinigameDefinition asset, with serialized fallbacks.
    // Also shows input-source toggles below the panel so players can pick Simulated / Mic / Fan.
    public class TutorialPopup : MonoBehaviour
    {
        [Header("Fallback Content (used if no MinigameDefinition)")]
        [SerializeField] private string _fallbackTitle = "HOW  TO  PLAY";
        [SerializeField, TextArea(2, 4)] private string _fallbackInstruction = "BLOW  STEADILY  INTO  THE  DEVICE  TO  CONTROL  THE  GAME";
        [SerializeField, TextArea(1, 2)] private string _fallbackTip = "LONGER  STEADY  BREATHS  WORK  BEST";

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

        // Which input source is selected (index into InputMode enum: 0=Simulated, 1=Mic, 2=Fan)
        private int _selectedInput;

        private bool _stylesReady;
        private GUIStyle _overlayBg;
        private GUIStyle _panelBg;
        private GUIStyle _titleStyle;
        private GUIStyle _instructionStyle;
        private GUIStyle _tipStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _buttonHoverStyle;
        private GUIStyle _buttonTextStyle;
        private GUIStyle _buttonTextHoverStyle;
        private GUIStyle _inputHeaderStyle;
        private GUIStyle _checkboxLabelStyle;
        private GUIStyle _checkboxBoxStyle;
        private GUIStyle _checkboxBoxCheckedStyle;

        private Rect _buttonRect;
        private bool _buttonHovered;

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

        public void ShowTutorial()
        {
            LoadTutorialContent();
            SyncInputTogglesFromManager();
            _phase = Phase.FadingIn;
            _timer = 0f;
            _alpha = 0f;
        }

        private void SyncInputTogglesFromManager()
        {
            var bim = BreathInputManager.Instance;
            _selectedInput = bim != null ? (int)bim.CurrentMode : 0;
        }

        private void LoadTutorialContent()
        {
            var mgr = MinigameManager.Instance;
            MinigameDefinition def = mgr != null ? mgr.SelectedDefinition : null;

            // If no definition was selected (e.g. scene launched directly), try to
            // match by current scene name so the asset data is still used.
            if (def == null && mgr != null)
            {
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                foreach (var candidate in mgr.AvailableMinigames)
                {
                    if (candidate != null && candidate.SceneName == scene)
                    {
                        def = candidate;
                        mgr.SelectMinigame(def);
                        break;
                    }
                }
            }

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

            float pw = Mathf.Min(Screen.width * 0.85f, 780f);
            float ph = Mathf.Min(Screen.height * 0.90f, 650f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", _panelBg);

            float pad = 35f;
            float contentW = pw - pad * 2f;

            float titleH = 65f;
            float btnHeight = 55f;
            float btnWidth = 220f;
            float inputH = 75f;
            float margin = 30f;

            // Title — top of panel
            float titleY = py + margin;
            GameFont.OutlinedLabel(new Rect(px, titleY, pw, titleH), _title, _titleStyle);

            // Continue button — pinned to bottom
            float btnY = py + ph - btnHeight - margin;
            float btnX = px + (pw - btnWidth) * 0.5f;
            _buttonRect = new Rect(btnX, btnY, btnWidth, btnHeight);

            // Calculate instruction and tip heights
            float instrHeight = _instructionStyle.CalcHeight(new GUIContent(_instruction), contentW) + 8f;
            instrHeight = Mathf.Min(instrHeight, 180f);

            bool hasTip = !string.IsNullOrEmpty(_tip);
            float tipHeight = 0f;
            if (hasTip)
            {
                tipHeight = _tipStyle.CalcHeight(new GUIContent(_tip), contentW);
                tipHeight = Mathf.Min(tipHeight, 80f);
            }

            // Zone between title bottom and button top
            float zoneTop = titleY + titleH;
            float zoneBottom = btnY;
            float zoneH = zoneBottom - zoneTop;

            // 4 sections fill the zone evenly: instruction, tip, input options, (gaps between all)
            float totalContent = instrHeight + tipHeight + inputH;
            float totalGap = zoneH - totalContent;
            float gap = totalGap / (hasTip ? 4f : 3f);

            float instrY = zoneTop + gap;
            GameFont.OutlinedLabel(new Rect(px + pad, instrY, contentW, instrHeight), _instruction, _instructionStyle);

            float tipY = instrY + instrHeight + gap;
            if (hasTip)
            {
                GameFont.OutlinedLabel(new Rect(px + pad, tipY, contentW, tipHeight), _tip, _tipStyle);
                tipY += tipHeight + gap;
            }

            DrawInputOptions(px, tipY, pw);

            Vector2 mousePos = Event.current.mousePosition;
            _buttonHovered = _buttonRect.Contains(mousePos) && _phase == Phase.Visible;

            GUIStyle btnStyle = _buttonHovered ? _buttonHoverStyle : _buttonStyle;
            GUIStyle btnTextStyle = _buttonHovered ? _buttonTextHoverStyle : _buttonTextStyle;

            GUI.Box(_buttonRect, "", btnStyle);
            GameFont.OutlinedLabel(_buttonRect, "CONTINUE", btnTextStyle);
            if (Event.current.type == EventType.MouseDown && _buttonRect.Contains(Event.current.mousePosition))
            {
                OnContinueClicked();
                Event.current.Use();
            }

            GUI.color = prevColor;
        }

        private void DrawInputOptions(float panelX, float topY, float panelWidth)
        {
            GameFont.OutlinedLabel(new Rect(panelX, topY, panelWidth, 36f), "INPUT  OPTIONS", _inputHeaderStyle);
            float sectionTop = topY + 42f;

            float radioSize = 28f;
            float spacing = 8f;
            float gapBetweenItems = 50f;
            string[] labels = { "SIMULATED", "MICROPHONE", "FAN" };

            // Pre-calculate total width for proper centering
            float[] labelWidths = new float[labels.Length];
            float totalWidth = 0f;
            for (int i = 0; i < labels.Length; i++)
            {
                labelWidths[i] = _checkboxLabelStyle.CalcSize(new GUIContent(labels[i])).x;
                totalWidth += radioSize + spacing + labelWidths[i];
                if (i < labels.Length - 1) totalWidth += gapBetweenItems;
            }

            float cx = panelX + (panelWidth - totalWidth) * 0.5f;

            for (int i = 0; i < labels.Length; i++)
            {
                bool isSelected = (i == _selectedInput);
                GUIStyle boxStyle = isSelected ? _checkboxBoxCheckedStyle : _checkboxBoxStyle;
                string marker = isSelected ? "\u2022" : "";

                Rect boxRect = new Rect(cx, sectionTop, radioSize, radioSize);
                if (GUI.Button(boxRect, marker, boxStyle))
                    SelectInput(i);

                Rect labelRect = new Rect(cx + radioSize + spacing, sectionTop - 2f, labelWidths[i], 34f);
                GameFont.OutlinedLabel(labelRect, labels[i], _checkboxLabelStyle);

                if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
                {
                    SelectInput(i);
                    Event.current.Use();
                }

                cx += radioSize + spacing + labelWidths[i] + gapBetweenItems;
            }
        }

        private void SelectInput(int index)
        {
            if (index == _selectedInput) return;

            _selectedInput = index;
            var bim = BreathInputManager.Instance;
            if (bim != null)
                bim.SetInputMode((InputMode)index);
        }

        // ─── Style building ─────────────────────────────────────────────
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
                fontSize = 50,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = new Color(0.4f, 0.85f, 1f);
            FlattenStyle(_titleStyle);

            _instructionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                padding = new RectOffset(10, 10, 0, 0)
            };
            _instructionStyle.normal.textColor = Color.white;
            FlattenStyle(_instructionStyle);

            _tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true
            };
            _tipStyle.normal.textColor = new Color(0.65f, 0.85f, 0.65f);
            FlattenStyle(_tipStyle);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 30,
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
            _buttonHoverStyle.fontSize = 32;

            _buttonTextStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = _buttonStyle.fontSize,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _buttonTextStyle.normal.textColor = Color.white;
            FlattenStyle(_buttonTextStyle);

            _buttonTextHoverStyle = new GUIStyle(_buttonTextStyle)
            {
                fontSize = _buttonHoverStyle.fontSize
            };
            FlattenStyle(_buttonTextHoverStyle);

            _inputHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _inputHeaderStyle.normal.textColor = new Color(0.7f, 0.85f, 1f);
            FlattenStyle(_inputHeaderStyle);

            _checkboxLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                alignment = TextAnchor.MiddleLeft
            };
            _checkboxLabelStyle.normal.textColor = new Color(0.9f, 0.95f, 1f);
            FlattenStyle(_checkboxLabelStyle);

            _checkboxBoxStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 2)
            };
            _checkboxBoxStyle.normal.background = MakeTex(new Color(0.15f, 0.18f, 0.25f, 1f));
            _checkboxBoxStyle.normal.textColor = Color.white;
            _checkboxBoxStyle.hover.background = MakeTex(new Color(0.25f, 0.3f, 0.4f, 1f));
            _checkboxBoxStyle.active.background = MakeTex(new Color(0.1f, 0.14f, 0.2f, 1f));

            _checkboxBoxCheckedStyle = new GUIStyle(_checkboxBoxStyle);
            _checkboxBoxCheckedStyle.normal.background = MakeTex(new Color(0.15f, 0.5f, 0.9f, 1f));
            _checkboxBoxCheckedStyle.normal.textColor = Color.white;
            _checkboxBoxCheckedStyle.hover.background = MakeTex(new Color(0.2f, 0.55f, 0.95f, 1f));
            _checkboxBoxCheckedStyle.active.background = MakeTex(new Color(0.1f, 0.4f, 0.8f, 1f));

            Font f = GameFont.Get();
            if (f != null)
            {
                GUIStyle[] all = {
                    _titleStyle, _instructionStyle, _tipStyle, _buttonStyle,
                    _buttonHoverStyle, _buttonTextStyle, _buttonTextHoverStyle,
                    _inputHeaderStyle, _checkboxLabelStyle,
                    _checkboxBoxStyle, _checkboxBoxCheckedStyle
                };
                foreach (var s in all)
                    if (s != null) s.font = f;
            }
        }

        private static void FlattenStyle(GUIStyle s)
        {
            s.hover = s.normal;
            s.active = s.normal;
            s.focused = s.normal;
            s.onNormal = s.normal;
            s.onHover = s.normal;
            s.onActive = s.normal;
            s.onFocused = s.normal;
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
