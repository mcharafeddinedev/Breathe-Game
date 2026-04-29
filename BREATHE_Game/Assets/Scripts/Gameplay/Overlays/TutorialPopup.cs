using System;
using System.Collections.Generic;
using UnityEngine;
using Breathe.Data;
using Breathe.Input;
using Breathe.Utility;
using Breathe.Audio;

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
        [SerializeField, TextArea(2, 4)] private string _fallbackInstruction = "Follow the on-screen goal for this activity.\n\nBreathe into or blow onto the device (match a steady, controlled breath to what the game asks) to play the game!";
        [SerializeField, TextArea(1, 2)] private string _fallbackTip = "Use the pattern in parentheses as your breath exercise for this round.";

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

        // UI index: WebGL 0=Sim only; desktop 0=Sim,1=Mic,2=Fan (or Sim+Fan if mic stripped).
        private int _selectedInput;

        private bool _stylesReady;
        private int _stylesBuiltForScreenH;
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
            _selectedInput = bim != null ? ModeToUiIndex(bim.CurrentMode) : 0;
        }

        private static string[] GetInputOptionLabels()
        {
            string sim = BreathInputManager.TutorialSimulatedOptionLabel;
            if (!BreathInputManager.InputModeCyclingSupported)
                return new[] { sim };
            if (!BreathInputManager.MicrophoneSupported)
                return new[] { sim, "FAN" };
            return new[] { sim, "MICROPHONE", "FAN" };
        }

        private static int ModeToUiIndex(InputMode mode)
        {
            if (!BreathInputManager.InputModeCyclingSupported)
                return 0;
            if (!BreathInputManager.MicrophoneSupported)
                return mode == InputMode.Fan ? 1 : 0;
            return (int)mode;
        }

        private static InputMode UiIndexToInputMode(int uiIndex)
        {
            if (!BreathInputManager.InputModeCyclingSupported)
                return InputMode.Simulated;
            if (!BreathInputManager.MicrophoneSupported)
                return uiIndex == 1 ? InputMode.Fan : InputMode.Simulated;
            return (InputMode)uiIndex;
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

                case Phase.Visible:
                    if (BreathPowerSystem.Instance != null &&
                        BreathPowerSystem.Instance.CurrentBreathPower >= 0.08f)
                        OnContinueClicked();
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
            SfxPlayer.Instance?.PlayUiMenuClick();
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

            float sc = UiScale();
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _overlayBg);

            // Near-fullscreen card so gameplay is mostly hidden until Continue (suspense + readability).
            float pw = Mathf.Min(Screen.width * 0.96f, 1600f);
            float ph = Mathf.Min(Screen.height * 0.94f, 1100f);
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", _panelBg);

            float pad = 32f * sc;
            float contentW = pw - pad * 2f;

            float titleH = 72f * sc;
            float btnHeight = 58f * sc;
            float btnWidth = Mathf.Min(280f * sc, pw * 0.45f);
            float inputH = 140f * sc;
            float margin = 22f * sc;

            // Title — top of panel
            float titleY = py + margin;
            GameFont.OutlinedLabel(new Rect(px, titleY, pw, titleH), _title, _titleStyle);

            // Continue button — pinned to bottom
            float btnY = py + ph - btnHeight - margin;
            float btnX = px + (pw - btnWidth) * 0.5f;
            _buttonRect = new Rect(btnX, btnY, btnWidth, btnHeight);

            // Calculate instruction and tip heights
            float lineGap = 1.34f;
            float paraGap = 20f * sc;
            float instrHeight = MeasureWrappedBlockHeight(_instruction, contentW, _instructionStyle, lineGap, paraGap) + 6f * sc;

            bool hasTip = !string.IsNullOrEmpty(_tip);
            float tipHeight = 0f;
            if (hasTip)
                tipHeight = MeasureWrappedBlockHeight(_tip, contentW, _tipStyle, lineGap * 0.98f, paraGap * 0.9f) + 4f * sc;

            // Zone between title bottom and button top
            float zoneTop = titleY + titleH;
            float zoneBottom = btnY;
            float zoneH = zoneBottom - zoneTop;

            // 4 sections fill the zone evenly: instruction, tip, input options, (gaps between all)
            float totalContent = instrHeight + tipHeight + inputH;
            float totalGap = zoneH - totalContent;
            int gapDiv = hasTip ? 4 : 3;
            float gap = gapDiv > 0 ? Mathf.Max(6f * sc, totalGap / gapDiv) : 6f * sc;
            if (totalGap < 0)
                gap = 4f * sc;

            float instrY = zoneTop + gap;
            DrawOutlinedWrappedBlock(new Rect(px + pad, instrY, contentW, instrHeight), _instruction, _instructionStyle, lineGap, paraGap);

            float tipY = instrY + instrHeight + gap;
            if (hasTip)
            {
                DrawOutlinedWrappedBlock(new Rect(px + pad, tipY, contentW, tipHeight), _tip, _tipStyle, lineGap * 0.98f, paraGap * 0.9f);
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
            if (Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                OnContinueClicked();
                Event.current.Use();
            }

            GUI.color = prevColor;
        }

        private void DrawInputOptions(float panelX, float topY, float panelWidth)
        {
            float sc = UiScale();
            GameFont.OutlinedLabel(new Rect(panelX, topY, panelWidth, 40f * sc), "INPUT  OPTIONS", _inputHeaderStyle);
            float sectionTop = topY + 44f * sc;

            float radioSize = 30f * sc;
            float spacing = 12f * sc;
            float gapBetweenItems = 40f * sc;
            string[] labels = GetInputOptionLabels();

            float marginX = 48f * sc;
            float maxRowW = Mathf.Max(100f, panelWidth - marginX * 2f);
            float minLabelW = 220f * sc;

            var labelWidths = new float[labels.Length];
            float sumCells = 0f;
            for (int i = 0; i < labels.Length; i++)
            {
                float w = _checkboxLabelStyle.CalcSize(new GUIContent(labels[i])).x;
                labelWidths[i] = Mathf.Max(w + 28f * sc, minLabelW);
                sumCells += radioSize + spacing + labelWidths[i];
            }

            float totalWidth = sumCells + gapBetweenItems * (labels.Length - 1);
            if (totalWidth > maxRowW)
            {
                float slack = maxRowW - sumCells;
                if (slack > 0 && labels.Length > 1)
                    gapBetweenItems = Mathf.Max(10f * sc, slack / (labels.Length - 1));
                else
                    gapBetweenItems = 10f * sc;
                totalWidth = sumCells + gapBetweenItems * (labels.Length - 1);
            }

            if (totalWidth > maxRowW)
            {
                DrawInputOptionsVertical(panelX, sectionTop, panelWidth, labels, radioSize, spacing, labelWidths, sc);
                return;
            }

            float cx = panelX + (panelWidth - totalWidth) * 0.5f;
            float rowH = Mathf.Max(40f * sc, 44f * sc);

            for (int i = 0; i < labels.Length; i++)
            {
                bool isSelected = (i == _selectedInput);
                GUIStyle boxStyle = isSelected ? _checkboxBoxCheckedStyle : _checkboxBoxStyle;
                string marker = isSelected ? "\u2022" : "";

                Rect boxRect = new Rect(cx, sectionTop, radioSize, radioSize);
                if (GUI.Button(boxRect, marker, boxStyle))
                    SelectInput(i);

                Rect labelRect = new Rect(cx + radioSize + spacing, sectionTop - 2f, labelWidths[i], rowH);
                GameFont.OutlinedLabel(labelRect, labels[i], _checkboxLabelStyle);

                if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
                {
                    SelectInput(i);
                    Event.current.Use();
                }

                cx += radioSize + spacing + labelWidths[i] + gapBetweenItems;
            }
        }

        private void DrawInputOptionsVertical(
            float panelX, float sectionTop, float panelWidth,
            string[] labels, float radioSize, float spacing, float[] labelWidths, float sc)
        {
            float rowH = Mathf.Max(40f * sc, 44f * sc);
            float rowGap = 10f * sc;
            float maxLabelW = labelWidths[0];
            for (int i = 1; i < labels.Length; i++)
                maxLabelW = Mathf.Max(maxLabelW, labelWidths[i]);
            float rowW = radioSize + spacing + maxLabelW;
            float cx = panelX + (panelWidth - rowW) * 0.5f;
            float cy = sectionTop;

            for (int i = 0; i < labels.Length; i++)
            {
                bool isSelected = (i == _selectedInput);
                GUIStyle boxStyle = isSelected ? _checkboxBoxCheckedStyle : _checkboxBoxStyle;
                string marker = isSelected ? "\u2022" : "";

                Rect boxRect = new Rect(cx, cy, radioSize, radioSize);
                if (GUI.Button(boxRect, marker, boxStyle))
                    SelectInput(i);

                Rect labelRect = new Rect(cx + radioSize + spacing, cy - 2f, labelWidths[i], rowH);
                GameFont.OutlinedLabel(labelRect, labels[i], _checkboxLabelStyle);

                if (Event.current.type == EventType.MouseDown && labelRect.Contains(Event.current.mousePosition))
                {
                    SelectInput(i);
                    Event.current.Use();
                }

                cy += rowH + rowGap;
            }
        }

        private void SelectInput(int index)
        {
            if (index == _selectedInput) return;

            SfxPlayer.Instance?.PlayUiMenuClick();
            _selectedInput = index;
            var bim = BreathInputManager.Instance;
            if (bim != null)
                bim.SetInputMode(UiIndexToInputMode(index));
        }

        static float UiScale()
        {
            return Mathf.Clamp(Screen.height / 900f, 0.95f, 1.5f);
        }

        static int ScaledFont(int basePx)
        {
            return Mathf.Max(10, Mathf.RoundToInt(basePx * UiScale()));
        }

        /// <summary>
        /// IMGUI has no reliable line-spacing control; we wrap manually and add vertical rhythm + paragraph gaps.
        /// </summary>
        static List<string> BuildWrappedLines(string text, float maxWidth, GUIStyle style)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.None);
            for (int p = 0; p < paragraphs.Length; p++)
            {
                if (p > 0) result.Add(null);
                string para = paragraphs[p].Replace("\r", "");
                if (string.IsNullOrEmpty(para.Trim())) continue;

                // Single newlines inside a paragraph become hard line breaks before word-wrap.
                var hardLines = para.Split(new[] { '\n' }, StringSplitOptions.None);
                foreach (var rawLine in hardLines)
                {
                    string hl = rawLine.Trim();
                    if (string.IsNullOrEmpty(hl)) continue;
                    WrapWordsToWidth(hl, maxWidth, style, result);
                }
            }
            return result;
        }

        static void WrapWordsToWidth(string paragraph, float maxWidth, GUIStyle style, List<string> result)
        {
            var words = paragraph.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string line = "";
            foreach (var word in words)
            {
                // Slightly wider word spacing than a single space for readability.
                string test = string.IsNullOrEmpty(line) ? word : line + "  " + word;
                if (style.CalcSize(new GUIContent(test)).x <= maxWidth)
                    line = test;
                else
                {
                    if (!string.IsNullOrEmpty(line)) result.Add(line);
                    line = word;
                }
            }
            if (!string.IsNullOrEmpty(line)) result.Add(line);
        }

        static float MeasureWrappedBlockHeight(string text, float areaWidth, GUIStyle style, float lineSpacingMult, float paragraphGap)
        {
            float innerW = Mathf.Max(1f, areaWidth - style.padding.horizontal);
            var lines = BuildWrappedLines(text, innerW, style);
            float lineH = Mathf.Max(style.fontSize * 1.22f, style.CalcHeight(new GUIContent("Ag"), innerW)) * lineSpacingMult;
            float h = style.padding.vertical;
            foreach (var line in lines)
            {
                if (line == null) h += paragraphGap;
                else h += lineH;
            }
            return h;
        }

        static void DrawOutlinedWrappedBlock(Rect area, string text, GUIStyle style, float lineSpacingMult, float paragraphGap)
        {
            float innerW = Mathf.Max(1f, area.width - style.padding.horizontal);
            var lines = BuildWrappedLines(text, innerW, style);
            float lineH = Mathf.Max(style.fontSize * 1.22f, style.CalcHeight(new GUIContent("Ag"), innerW)) * lineSpacingMult;
            float x = area.x + style.padding.left;
            float y = area.y + style.padding.top;
            foreach (var line in lines)
            {
                if (line == null)
                {
                    y += paragraphGap;
                    continue;
                }
                GameFont.OutlinedLabel(new Rect(x, y, innerW, lineH), line, style, 2);
                y += lineH;
            }
        }

        // ─── Style building ─────────────────────────────────────────────
        private void BuildStyles()
        {
            if (_stylesReady && _stylesBuiltForScreenH == Screen.height) return;
            _stylesReady = true;
            _stylesBuiltForScreenH = Screen.height;

            _overlayBg = new GUIStyle();
            // Strong dimmer so the running scene reads as "behind" the briefing until Continue.
            _overlayBg.normal.background = MakeTex(MenuVisualTheme.TutorialOverlayDim);

            _panelBg = new GUIStyle();
            _panelBg.normal.background = MakeTex(MenuVisualTheme.TutorialPanelBg);

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(52),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = MenuVisualTheme.TutorialTitle;
            FlattenStyle(_titleStyle);

            _instructionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(32),
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.UpperCenter,
                wordWrap = false,
                padding = new RectOffset(14, 14, 4, 4)
            };
            _instructionStyle.normal.textColor = Color.white;
            FlattenStyle(_instructionStyle);

            _tipStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(28),
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.UpperCenter,
                wordWrap = false,
                padding = new RectOffset(14, 14, 2, 2)
            };
            _tipStyle.normal.textColor = MenuVisualTheme.TutorialTip;
            FlattenStyle(_tipStyle);

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = ScaledFont(32),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(20, 20, 10, 10)
            };
            _buttonStyle.normal.background = MakeTex(MenuVisualTheme.ResultButtonPrimary);
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.background = MakeTex(MenuVisualTheme.ResultButtonPrimaryHover);
            _buttonStyle.hover.textColor = Color.white;
            _buttonStyle.active.background = MakeTex(MenuVisualTheme.TutorialCheckboxCheckedActive);
            _buttonStyle.active.textColor = Color.white;

            _buttonHoverStyle = new GUIStyle(_buttonStyle);
            _buttonHoverStyle.normal.background = MakeTex(MenuVisualTheme.ResultButtonPrimaryHover);
            _buttonHoverStyle.fontSize = ScaledFont(34);

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
                fontSize = ScaledFont(34),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _inputHeaderStyle.normal.textColor = MenuVisualTheme.TutorialInputHeader;
            FlattenStyle(_inputHeaderStyle);

            _checkboxLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = ScaledFont(28),
                alignment = TextAnchor.MiddleLeft
            };
            _checkboxLabelStyle.normal.textColor = MenuVisualTheme.TutorialCheckboxLabel;
            FlattenStyle(_checkboxLabelStyle);

            _checkboxBoxStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = ScaledFont(22),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 2)
            };
            _checkboxBoxStyle.normal.background = MakeTex(MenuVisualTheme.TutorialCheckboxBg);
            _checkboxBoxStyle.normal.textColor = Color.white;
            _checkboxBoxStyle.hover.background = MakeTex(MenuVisualTheme.TutorialCheckboxBgHover);
            _checkboxBoxStyle.active.background = MakeTex(MenuVisualTheme.TutorialCheckboxBgActive);

            _checkboxBoxCheckedStyle = new GUIStyle(_checkboxBoxStyle);
            _checkboxBoxCheckedStyle.normal.background = MakeTex(MenuVisualTheme.TutorialCheckboxChecked);
            _checkboxBoxCheckedStyle.normal.textColor = Color.white;
            _checkboxBoxCheckedStyle.hover.background = MakeTex(MenuVisualTheme.TutorialCheckboxCheckedHover);
            _checkboxBoxCheckedStyle.active.background = MakeTex(MenuVisualTheme.TutorialCheckboxCheckedActive);

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
