using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.UI;
using TMPro;
using Breathe.Audio;
using Breathe.Gameplay;
using Breathe.Input;
using Breathe.Utility;

namespace Breathe.UI
{
    /// <summary>
    /// Populates the Settings panel with functional controls and persists
    /// all values via PlayerPrefs. Add this component to the existing
    /// SettingsPanel GameObject — it auto-discovers the back button,
    /// clears all placeholder content, and builds the full UI from scratch.
    /// </summary>
    public sealed class SettingsManager : MonoBehaviour
    {
        #region PlayerPrefs Keys (display; volume keys: <see cref="AudioPrefsKeys"/>)
        const string KeyFullscreen = "Breathe_Fullscreen";
        const string KeyResIdx = "Breathe_ResolutionIndex";
        const string KeyWebGlResMigration = "Breathe_WebGLResIdxCleared";
        #endregion

        static bool IsWebGlPlayer => Application.platform == RuntimePlatform.WebGLPlayer;

        #region Palette
        static readonly Color ColHeader = MenuVisualTheme.ChromeHeader;
        static readonly Color ColLabel = MenuVisualTheme.ChromeLabel;
        static readonly Color ColValue = MenuVisualTheme.ChromeHeader;
        static readonly Color ColDivider = MenuVisualTheme.ChromeDivider;
        static readonly Color ColSliderBg = MenuVisualTheme.SliderTrack;
        static readonly Color ColSliderFill = MenuVisualTheme.SliderFill;
        static readonly Color ColHandle = MenuVisualTheme.SliderHandle;
        static readonly Color ColBtn = MenuVisualTheme.ButtonBase;
        static readonly Color ColBtnHi = MenuVisualTheme.ButtonHighlight;
        static readonly Color ColBorder = MenuVisualTheme.PanelBorder;
        static readonly Color ColHowToPlayPanelBg = MenuVisualTheme.HowToPlayFill;
        static readonly Color ColSettingsContentFill = MenuVisualTheme.SettingsContentFill;

        const float HtpPanelBorderPx = 3f;
        const float SliderTrackBorderPx = 2f;
        #endregion

        /// <summary>How to Play: this pixel font reads tight — use positive tracking + extra inter-word spaces in the string.</summary>
        const float HtpCharacterSpacing = 12f;
        const float HtpWordSpacing = 8f;
        const float HtpLineSpacing = 9f;

        #region Runtime State
        float _masterVol = AudioMixDefaults.MasterLinear;
        float _musicVol  = AudioMixDefaults.MusicLinear;
        float _sfxVol    = AudioMixDefaults.SfxLinear;
        bool _debugOverlayEnabled = false;
        Resolution[] _resolutions;
        int _resIdx;
        string[] _availableComPorts = Array.Empty<string>();
        int _comPortIdx;
        ComPortMode _comPortMode = ComPortMode.Auto;
        #endregion

        #region UI Refs
        RectTransform _content;
        TextMeshProUGUI _inputModeLabel;
        TextMeshProUGUI _comPortLabel;
        TextMeshProUGUI _comPortStatusLabel;
        Slider _masterSlider, _musicSlider, _sfxSlider;
        TextMeshProUGUI _masterPct, _musicPct, _sfxPct;
        Toggle _fullscreenToggle;
        Toggle _debugOverlayToggle;
        TextMeshProUGUI _resLabel;
        GameObject _howToPlayOverlay;
        Button _backBtn;
        float _nextY;
        #endregion

        #region Static Volume API
        /// <summary>Raw persisted Master slider (0 … <see cref="MasterVolume.SliderNormalizedMax"/>).</summary>
        public static float MasterGainLinear => PlayerPrefs.GetFloat(AudioPrefsKeys.MasterVolume, AudioMixDefaults.MasterLinear);
        public static float MusicVolume  => PlayerPrefs.GetFloat(AudioPrefsKeys.MusicVolume, AudioMixDefaults.MusicLinear);
        public static float SfxVolume    => PlayerPrefs.GetFloat(AudioPrefsKeys.SfxVolume, AudioMixDefaults.SfxLinear);
        #endregion

        // ================================================================
        //  Lifecycle
        // ================================================================

        void Awake()
        {
            FindBackButton();
            DestroyAllExceptBack();
            CacheResolutions();
            _debugOverlayEnabled = PlayerPrefs.GetInt(DebugOverlay.PlayerPrefsKey, 0) != 0;
            BuildUI();
        }

        void OnEnable()
        {
            LoadFromPrefs();
            RefreshAll();
            if (_howToPlayOverlay != null)
                _howToPlayOverlay.SetActive(false);
        }

        // ================================================================
        //  Cleanup
        // ================================================================

        void FindBackButton()
        {
            foreach (var btn in GetComponentsInChildren<Button>(true))
            {
                if (btn.gameObject.name.Contains("Back"))
                { _backBtn = btn; break; }
            }
        }

        void DestroyAllExceptBack()
        {
            var kill = new List<GameObject>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i).gameObject;
                if (_backBtn != null && child == _backBtn.gameObject) continue;
                kill.Add(child);
            }
            foreach (var go in kill)
            {
                go.SetActive(false);
                Destroy(go);
            }
        }

        void CacheResolutions()
        {
            if (IsWebGlPlayer)
            {
                _resolutions = Array.Empty<Resolution>();
                _resIdx = 0;
                if (PlayerPrefs.GetInt(KeyWebGlResMigration, 0) == 0)
                {
                    PlayerPrefs.DeleteKey(KeyResIdx);
                    PlayerPrefs.SetInt(KeyWebGlResMigration, 1);
                    PlayerPrefs.Save();
                }
                return;
            }

            var raw = Screen.resolutions;
            var unique = new List<Resolution>();
            var seen = new HashSet<string>();
            foreach (var r in raw)
            {
                string key = $"{r.width}x{r.height}";
                if (seen.Add(key)) unique.Add(r);
            }
            _resolutions = unique.ToArray();
            int saved = PlayerPrefs.GetInt(KeyResIdx, -1);
            _resIdx = (saved >= 0 && saved < _resolutions.Length) ? saved : _resolutions.Length - 1;
        }

        // ================================================================
        //  Build — uses a content area with VLG for vertical stacking,
        //  and anchor-based positioning inside each row (no HLG).
        // ================================================================

        void BuildUI()
        {
            BuildTitle();
            _content = BuildContentArea();
            _nextY = 0;

            AddHeader("Input");
            BuildInputRow();
            if (!IsWebGlPlayer)
                BuildComPortRow();
            AddDivider();
            AddHeader("Audio");
            (_masterSlider, _masterPct) = AddSliderRow("Master",
                MasterVolume.ClampStoredPreference(_masterVol), OnMasterVol, MasterVolume.SliderNormalizedMax);
            (_musicSlider,  _musicPct)  = AddSliderRow("Music",  _musicVol,  OnMusicVol);
            (_sfxSlider,    _sfxPct)    = AddSliderRow("SFX",    _sfxVol,    OnSfxVol);
            AddDivider();
            AddHeader("Display");
            BuildFullscreenRow();
            if (IsWebGlPlayer)
                BuildWebResolutionInfoRow();
            else
                BuildResolutionRow();
            BuildDebugOverlayRow();
            AddSpacer(2);
            BuildHowToPlayBtn();

            PositionBack();
            StyleBackButtonBorder();
            BuildHowToPlayOverlay();

            MenuTextLegibility.TryApplyToPanelNonButtonText(transform);
            MenuClickSoundHook.RegisterHierarchy(transform);
        }

        void BuildTitle()
        {
            var go = new GameObject("SettingsTitle", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -8);
            rt.sizeDelta = new Vector2(0, 36);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = "S E T T I N G S";
            tmp.fontSize = 22;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = ColHeader;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
        }

        RectTransform BuildContentArea()
        {
            var go = new GameObject("Content", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            // Insets leave room for title + BACK; tighter vertical inset so stacked rows fill the framed area (esp. pause menu).
            rt.offsetMin = new Vector2(22, 32);
            rt.offsetMax = new Vector2(-22, -36);
            MenuUiChrome.AddInsetPanelFrame(rt);
            return rt;
        }

        void PositionBack()
        {
            if (_backBtn == null) return;
            // Center on the panel (same axis as Content when horizontal margins match). Width is derived
            // from the content band so BACK lines up with the card, not a narrower 30–70% strip.
            var rt = _backBtn.GetComponent<RectTransform>();
            float parentW = ((RectTransform)transform).rect.width;
            float left = _content != null ? _content.offsetMin.x : 28f;
            float right = _content != null ? -_content.offsetMax.x : 28f;
            float contentW = Mathf.Max(120f, parentW - left - right);
            float btnW = Mathf.Min(320f, Mathf.Max(200f, contentW * 0.42f));

            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(btnW, 34f);
            rt.anchoredPosition = new Vector2(0f, 6f);
            _backBtn.transform.SetAsLastSibling();
        }

        void StyleBackButtonBorder()
        {
            if (_backBtn == null) return;
            MenuUiChrome.StyleButtonLikeSettings(_backBtn.gameObject);
            MenuUiChrome.AttachStandardButtonHover(_backBtn.gameObject, _backBtn.interactable);
        }

        // ================================================================
        //  Row builders — each appends a row at _nextY inside _content,
        //  using top-anchored rects. Children are anchor-positioned (see LblL/CtlL/PctL).
        // ================================================================

        const float RowH = 26f;
        const float HdrH = 22f;
        const float DivH = 1f;
        const float Gap  = 2f;

        RectTransform PlaceRow(float height)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -_nextY);
            rt.sizeDelta = new Vector2(0, height);
            _nextY += height + Gap;
            return rt;
        }

        void AddHeader(string text)
        {
            var row = PlaceRow(HdrH);
            var tmp = AddTMP(row, "Header", Vector2.zero, Vector2.one);
            tmp.text = $"--  {text}  --";
            tmp.fontSize = 13;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = ColHeader;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        void AddDivider()
        {
            var row = PlaceRow(DivH);
            var rt = row.GetComponent<RectTransform>();
            rt.offsetMin = new Vector2(10f, 0f);
            rt.offsetMax = new Vector2(-10f, 0f);
            var img = row.gameObject.AddComponent<CanvasRenderer>();
            var image = row.gameObject.AddComponent<Image>();
            image.color = ColDivider;
            _nextY += 2;
        }

        void AddSpacer(float h) { _nextY += h; }

        // ----- Input -----

        void BuildInputRow()
        {
            var row = PlaceRow(RowH);
            AddLabel(row, "Input Mode");
            if (BreathInputManager.InputModeCyclingSupported)
            {
                _inputModeLabel = AddCycleBtn(row,
                    BreathInputManager.InputModeSettingsLabel(InputMode.Simulated), CycleInputMode);
                _inputModeLabel.fontSize = 12;
                _inputModeLabel.enableAutoSizing = true;
                _inputModeLabel.fontSizeMin = 9;
                _inputModeLabel.fontSizeMax = 12;
            }
            else
            {
                _inputModeLabel = AddTMP(row, "InputModeValue", new Vector2(CtlL, 0.08f), new Vector2(CtlR, 0.92f));
                _inputModeLabel.text = BreathInputManager.InputModeSettingsLabel(InputMode.Simulated);
                _inputModeLabel.fontSize = 12;
                _inputModeLabel.enableAutoSizing = true;
                _inputModeLabel.fontSizeMin = 9;
                _inputModeLabel.fontSizeMax = 12;
                _inputModeLabel.color = ColValue;
                _inputModeLabel.alignment = TextAlignmentOptions.Center;
            }
        }

        void CycleInputMode()
        {
            if (!BreathInputManager.InputModeCyclingSupported) return;
            var mgr = BreathInputManager.Instance;
            if (mgr == null) return;
            InputMode next = BreathInputManager.GetNextCycledInputMode(mgr.CurrentMode);
            mgr.SetInputMode(next);
            _inputModeLabel.text = Cyc(BreathInputManager.InputModeSettingsLabel(next));
            PlayerPrefs.SetInt(BreathInputManager.PrefKeyInputMode, (int)next);
            PlayerPrefs.Save();
        }

        // ----- COM Port (Fan Hardware) -----

        void BuildComPortRow()
        {
            RefreshAvailableComPorts();

            var row = PlaceRow(RowH);
            AddLabel(row, "COM Port");
            _comPortLabel = AddCycleBtn(row, GetComPortDisplayText(), CycleComPort);
            _comPortLabel.fontSize = 11;
            _comPortLabel.enableAutoSizing = true;
            _comPortLabel.fontSizeMin = 8;
            _comPortLabel.fontSizeMax = 11;

            var statusRow = PlaceRow(RowH);
            var statusLabelTmp = AddTMP(statusRow, "ComStatusLabel", new Vector2(LblL, 0), new Vector2(LblR, 1));
            statusLabelTmp.text = "Status";
            statusLabelTmp.fontSize = 11;
            statusLabelTmp.fontStyle = FontStyles.Italic;
            statusLabelTmp.color = ColLabel;
            statusLabelTmp.alignment = TextAlignmentOptions.MidlineRight;

            _comPortStatusLabel = AddTMP(statusRow, "ComStatusValue", new Vector2(CtlL, 0), new Vector2(PctR, 1));
            _comPortStatusLabel.fontSize = 10;
            _comPortStatusLabel.enableAutoSizing = true;
            _comPortStatusLabel.fontSizeMin = 8;
            _comPortStatusLabel.fontSizeMax = 10;
            _comPortStatusLabel.color = ColLabel;
            _comPortStatusLabel.alignment = TextAlignmentOptions.MidlineLeft;
            RefreshComPortStatus();
        }

        void RefreshAvailableComPorts()
        {
            _availableComPorts = FanBreathInput.GetAvailablePorts(forceRefresh: true);
            _comPortMode = FanBreathInput.GetSavedPortMode();

            if (_comPortMode == ComPortMode.Manual)
            {
                string savedPort = FanBreathInput.GetSavedManualPort();
                _comPortIdx = Array.FindIndex(_availableComPorts,
                    p => string.Equals(p, savedPort, StringComparison.OrdinalIgnoreCase));
                if (_comPortIdx < 0) _comPortIdx = 0;
            }
            else
            {
                _comPortIdx = -1;
            }
        }

        string GetComPortDisplayText()
        {
            if (_comPortMode == ComPortMode.Auto)
                return "Auto";

            if (_availableComPorts.Length == 0)
                return "No ports";

            if (_comPortIdx >= 0 && _comPortIdx < _availableComPorts.Length)
                return _availableComPorts[_comPortIdx];

            return "Auto";
        }

        void CycleComPort()
        {
            RefreshAvailableComPorts();

            if (_comPortMode == ComPortMode.Auto)
            {
                if (_availableComPorts.Length > 0)
                {
                    _comPortMode = ComPortMode.Manual;
                    _comPortIdx = 0;
                    FanBreathInput.SetPortMode(ComPortMode.Manual);
                    FanBreathInput.SetManualPort(_availableComPorts[0]);
                }
            }
            else
            {
                _comPortIdx++;
                if (_comPortIdx >= _availableComPorts.Length)
                {
                    _comPortMode = ComPortMode.Auto;
                    _comPortIdx = -1;
                    FanBreathInput.SetPortMode(ComPortMode.Auto);
                }
                else
                {
                    FanBreathInput.SetManualPort(_availableComPorts[_comPortIdx]);
                }
            }

            _comPortLabel.text = Cyc(GetComPortDisplayText());

            var mgr = BreathInputManager.Instance;
            if (mgr != null && mgr.CurrentMode == InputMode.Fan)
            {
                var fanInput = mgr.ActiveInput as FanBreathInput;
                fanInput?.Reinitialize();
            }

            RefreshComPortStatus();
        }

        void RefreshComPortStatus()
        {
            if (_comPortStatusLabel == null) return;

            var status = FanBreathInput.ConnectionStatus;
            string msg = FanBreathInput.StatusMessage;

            Color statusColor = status switch
            {
                FanConnectionStatus.Connected => new Color(0.4f, 0.8f, 0.4f),
                FanConnectionStatus.Connecting => ColLabel,
                FanConnectionStatus.Failed => new Color(0.9f, 0.5f, 0.4f),
                _ => ColLabel
            };

            _comPortStatusLabel.text = msg;
            _comPortStatusLabel.color = statusColor;
        }

        // ----- Audio -----

        void OnMasterVol(float v)
        {
            v = MasterVolume.ClampStoredPreference(v);
            _masterVol = v;
            _masterPct.text = MasterVolume.FormatPercent(v);
            MasterVolume.ApplyListenerFromStoredPreference(v);
            Save(AudioPrefsKeys.MasterVolume, v);
        }
        void OnMusicVol(float v)
        {
            _musicVol = v; _musicPct.text = Pct(v); Save(AudioPrefsKeys.MusicVolume, v);

            Breathe.Audio.SceneMusicDirector.Instance?.RefreshFromMusicSlider();

        }
        void OnSfxVol(float v)
        {
            _sfxVol = v; _sfxPct.text = Pct(v); Save(AudioPrefsKeys.SfxVolume, v);
        }

        // ----- Display -----

        void BuildFullscreenRow()
        {
            var row = PlaceRow(RowH);
            AddLabel(row, "Fullscreen");
            _fullscreenToggle = AddToggle(row, Screen.fullScreen, OnFullscreen);
        }

        void OnFullscreen(bool on)
        {
            Screen.fullScreen = on;
            PlayerPrefs.SetInt(KeyFullscreen, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        void BuildDebugOverlayRow()
        {
            var row = PlaceRow(RowH);
            AddLabel(row, "Debug overlay");
            _debugOverlayToggle = AddToggle(row, _debugOverlayEnabled, OnDebugOverlay);
        }

        void OnDebugOverlay(bool enabled)
        {
            _debugOverlayEnabled = enabled;
            DebugOverlay.SetEnabledAndSave(enabled);
        }

        void BuildResolutionRow()
        {
            var row = PlaceRow(RowH);
            AddLabel(row, "Resolution");
            _resLabel = AddCycleBtn(row, CurrentResText(), CycleResolution);
        }

        void BuildWebResolutionInfoRow()
        {
            var row = PlaceRow(RowH);
            AddLabel(row, "Size");
            _resLabel = AddTMP(row, "WebResInfo", new Vector2(CtlL, 0), new Vector2(CtlR, 1));
            _resLabel.text = WebResSummaryText();
            _resLabel.fontSize = 11;
            _resLabel.fontStyle = FontStyles.Italic;
            _resLabel.color = ColLabel;
            _resLabel.alignment = TextAlignmentOptions.Center;
        }

        static string WebResSummaryText() =>
            $"{Screen.width} x {Screen.height} (browser)";

        void CycleResolution()
        {
            if (IsWebGlPlayer) return;
            if (_resolutions.Length == 0) return;
            _resIdx = (_resIdx + 1) % _resolutions.Length;
            var r = _resolutions[_resIdx];
            Screen.SetResolution(r.width, r.height, Screen.fullScreen);
            _resLabel.text = Cyc($"{r.width} x {r.height}");
            PlayerPrefs.SetInt(KeyResIdx, _resIdx);
            PlayerPrefs.Save();
        }

        string CurrentResText()
        {
            if (_resolutions.Length == 0) return "Default";
            var r = _resolutions[_resIdx];
            return $"{r.width} x {r.height}";
        }

        // ----- How to Play -----

        void BuildHowToPlayBtn()
        {
            var row = PlaceRow(RowH);
            var btnGo = new GameObject("BTN_HowToPlay",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(row, false);
            Anchor(btnGo, new Vector2(CtlL, 0.05f), new Vector2(CtlR, 0.95f));

            MenuUiChrome.StyleButtonLikeSettings(btnGo);
            btnGo.GetComponent<Button>().onClick.AddListener(ShowHTP);

            var tmp = AddTMP(btnGo.GetComponent<RectTransform>(), "Label",
                Vector2.zero, Vector2.one);
            tmp.text = "HOW  TO  PLAY";
            tmp.fontSize = 12;
            tmp.color = ColValue;
            tmp.alignment = TextAlignmentOptions.Center;
            DisableTmpKerning(tmp);
            tmp.characterSpacing = HtpCharacterSpacing * 0.4f;
            tmp.wordSpacing = 6f;

            MenuUiChrome.AttachStandardButtonHover(btnGo);
        }

        void BuildHowToPlayOverlay()
        {
            _howToPlayOverlay = new GameObject("HowToPlayPanel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _howToPlayOverlay.transform.SetParent(transform, false);
            Anchor(_howToPlayOverlay, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
            var outerImg = _howToPlayOverlay.GetComponent<Image>();
            outerImg.color = ColBorder;
            outerImg.raycastTarget = true;

            var innerGo = new GameObject("InnerFill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            innerGo.transform.SetParent(_howToPlayOverlay.transform, false);
            var innerRT = innerGo.GetComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            float b = HtpPanelBorderPx;
            innerRT.offsetMin = new Vector2(b, b);
            innerRT.offsetMax = new Vector2(-b, -b);
            var innerImg = innerGo.GetComponent<Image>();
            innerImg.color = ColHowToPlayPanelBg;
            innerImg.raycastTarget = false;
            innerGo.transform.SetAsFirstSibling();

            var titleTmp = AddTMP(innerRT,
                "HTP_Title", new Vector2(0, 0.86f), new Vector2(1, 0.98f));
            titleTmp.text = "HOW TO PLAY";
            titleTmp.fontSize = 32;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = ColHeader;
            titleTmp.alignment = TextAlignmentOptions.Center;
            DisableTmpKerning(titleTmp);
            titleTmp.characterSpacing = HtpCharacterSpacing * 0.55f;
            titleTmp.wordSpacing = HtpWordSpacing;

            // Body: centered copy, bottom kept well above Close; RectMask2D clips TMP.
            var bodyGo = new GameObject("HTP_Body", typeof(RectTransform));
            bodyGo.transform.SetParent(innerGo.transform, false);
            var bodyRT = bodyGo.GetComponent<RectTransform>();
            bodyRT.anchorMin = new Vector2(0, 0.13f);
            bodyRT.anchorMax = new Vector2(1, 0.88f);
            bodyRT.offsetMin = new Vector2(32, 0);
            bodyRT.offsetMax = new Vector2(-32, 0);
            bodyGo.AddComponent<RectMask2D>();

            var bodyTextGo = new GameObject("HTP_BodyText", typeof(RectTransform));
            bodyTextGo.transform.SetParent(bodyGo.transform, false);
            var bodyTextRt = bodyTextGo.GetComponent<RectTransform>();
            bodyTextRt.anchorMin = Vector2.zero;
            bodyTextRt.anchorMax = Vector2.one;
            bodyTextRt.offsetMin = Vector2.zero;
            bodyTextRt.offsetMax = Vector2.zero;

            var bodyTmp = bodyTextGo.AddComponent<TextMeshProUGUI>();
            bodyTmp.text = GameFont.SanitizeForPixelFont(ExpandHowToPlayWordSpacing(HowToPlayTextRaw()));
            bodyTmp.richText = true;
            bodyTmp.fontSize = 15;
            bodyTmp.color = ColLabel;
            bodyTmp.alignment = TextAlignmentOptions.Center;
            bodyTmp.textWrappingMode = TextWrappingModes.Normal;
            bodyTmp.overflowMode = TextOverflowModes.Overflow;
            bodyTmp.raycastTarget = false;
            DisableTmpKerning(bodyTmp);
            bodyTmp.characterSpacing = HtpCharacterSpacing * 0.85f;
            bodyTmp.wordSpacing = HtpWordSpacing;
            bodyTmp.lineSpacing = HtpLineSpacing;

            var closeGo = new GameObject("BTN_Close",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(innerGo.transform, false);
            Anchor(closeGo, new Vector2(0.32f, 0.03f), new Vector2(0.68f, 0.11f));
            MenuUiChrome.StyleButtonLikeSettings(closeGo);
            closeGo.GetComponent<Button>().onClick.AddListener(HideHTP);
            var closeTmp = AddTMP(closeGo.GetComponent<RectTransform>(), "Label",
                Vector2.zero, Vector2.one);
            closeTmp.text = "CLOSE";
            closeTmp.fontSize = 17;
            closeTmp.color = ColValue;
            closeTmp.alignment = TextAlignmentOptions.Center;
            DisableTmpKerning(closeTmp);
            closeTmp.characterSpacing = HtpCharacterSpacing * 0.55f;
            MenuUiChrome.AttachStandardButtonHover(closeGo);

            _howToPlayOverlay.SetActive(false);
        }

        /// <summary>Plain + rich tags; <see cref="ExpandHowToPlayWordSpacing"/> doubles gaps between words outside tags.</summary>
        static string HowToPlayTextRaw() =>
@"<b>BREATHE</b> is a breath-controlled minigame arcade: easygoing play, not a test.


<size=150%><b>INPUT</b></size>
Fan, microphone, or <b>Space</b> for simulated breath. Choose the mode in Settings.


<size=150%><b>PLAY</b></size>
Open  <b>Level Select</b> and pick a game. Steady exhales and short puffs do different things in each one.

Mouse in menus. Breath in games. Pause or rest whenever you like.";

        /// <summary>Doubles single spaces between word characters so TMP reads clearly with this font (tags preserved).</summary>
        static string ExpandHowToPlayWordSpacing(string source)
        {
            var parts = Regex.Split(source, @"(<[^>]+>)");
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length == 0) continue;
                if (part[0] == '<')
                    sb.Append(part);
                else
                    sb.Append(Regex.Replace(part, @"(?<=\S) (?=\S)", "  "));
            }
            return sb.ToString();
        }

        /// <summary>No OpenType layout features (including kerning) — better for bitmap/pixel fonts. Uses TMP fontFeatures instead of deprecated enableKerning.</summary>
        static void DisableTmpKerning(TMP_Text tmp)
        {
            tmp.fontFeatures = new List<OTL_FeatureTag>();
        }

        void ShowHTP() { if (_howToPlayOverlay != null) _howToPlayOverlay.SetActive(true); }
        void HideHTP() { if (_howToPlayOverlay != null) _howToPlayOverlay.SetActive(false); }

        // ================================================================
        //  Persistence
        // ================================================================

        void LoadFromPrefs()
        {
            _masterVol = MasterVolume.ClampStoredPreference(PlayerPrefs.GetFloat(AudioPrefsKeys.MasterVolume, AudioMixDefaults.MasterLinear));
            _musicVol  = PlayerPrefs.GetFloat(AudioPrefsKeys.MusicVolume, AudioMixDefaults.MusicLinear);
            _sfxVol    = PlayerPrefs.GetFloat(AudioPrefsKeys.SfxVolume, AudioMixDefaults.SfxLinear);
            _debugOverlayEnabled = PlayerPrefs.GetInt(DebugOverlay.PlayerPrefsKey, 0) != 0;
            MasterVolume.ApplyListenerFromStoredPreference(_masterVol);

            SceneMusicDirector.Instance?.RefreshFromMusicSlider();
        }

        void RefreshAll()
        {
            if (_inputModeLabel != null)
            {
                InputMode m = BreathInputManager.Instance != null
                    ? BreathInputManager.Instance.CurrentMode
                    : InputMode.Simulated;
                string text = BreathInputManager.InputModeSettingsLabel(m);
                _inputModeLabel.text = BreathInputManager.InputModeCyclingSupported ? Cyc(text) : text;
            }
            if (_comPortLabel != null)
            {
                RefreshAvailableComPorts();
                _comPortLabel.text = Cyc(GetComPortDisplayText());
                RefreshComPortStatus();
            }
            RefreshSlider(_masterSlider, _masterPct, _masterVol, MasterVolume.SliderNormalizedMax);
            RefreshSlider(_musicSlider,  _musicPct,  _musicVol);
            RefreshSlider(_sfxSlider,    _sfxPct,    _sfxVol);
            if (_fullscreenToggle != null)
                _fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            if (_resLabel != null)
                _resLabel.text = IsWebGlPlayer ? WebResSummaryText() : Cyc(CurrentResText());
            if (_debugOverlayToggle != null)
                _debugOverlayToggle.SetIsOnWithoutNotify(_debugOverlayEnabled);
        }

        static void RefreshSlider(Slider s, TextMeshProUGUI pct, float v, float sliderMax = 1f)
        {
            float clamped = Mathf.Clamp(v, 0f, sliderMax);
            if (s != null) s.SetValueWithoutNotify(clamped);
            if (pct != null)
                pct.text = sliderMax > 1f + 1e-4f ? MasterVolume.FormatPercent(clamped) : Pct(clamped);
        }

        static void Save(string key, float v)
        {
            PlayerPrefs.SetFloat(key, v);
            PlayerPrefs.Save();
        }

        // ================================================================
        //  Row-level widget factories (anchor-based, no HLG)
        //  Symmetric row layout (8% margin each side) so Settings content looks centered.
        //  Label:   0.08 – 0.22 (14% width, right-aligned)
        //  Control: 0.24 – 0.72 (48% width, slider/cycle btn)
        //  Pct:     0.74 – 0.92 (18% width, left-aligned number by slider end)
        // ================================================================

        const float LblL = 0.08f, LblR = 0.22f;
        const float CtlL = 0.24f, CtlR = 0.72f;
        const float PctL = 0.74f, PctR = 0.92f;

        static void AddLabel(RectTransform row, string text)
        {
            var tmp = AddTMP(row, "Label", new Vector2(LblL, 0), new Vector2(LblR, 1));
            tmp.text = text;
            tmp.fontSize = 13;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = ColValue;
            tmp.alignment = TextAlignmentOptions.MidlineRight;
        }

        TextMeshProUGUI AddCycleBtn(RectTransform row, string initial,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("CycleBtn",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(row, false);
            Anchor(go, new Vector2(CtlL, 0.08f), new Vector2(CtlR, 0.92f));

            MenuUiChrome.StyleButtonLikeSettings(go);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var tmp = AddTMP(go.GetComponent<RectTransform>(), "Value",
                Vector2.zero, Vector2.one);
            tmp.text = Cyc(initial);
            tmp.fontSize = 13;
            tmp.color = ColValue;
            tmp.alignment = TextAlignmentOptions.Center;

            MenuUiChrome.AttachStandardButtonHover(go);
            return tmp;
        }

        (Slider, TextMeshProUGUI) AddSliderRow(string label, float initial,
            UnityEngine.Events.UnityAction<float> onChange, float sliderMax = 1f)
        {
            var row = PlaceRow(RowH);
            AddLabel(row, label);

            var borderGo = new GameObject("SliderBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            borderGo.transform.SetParent(row, false);
            Anchor(borderGo, new Vector2(CtlL, 0.18f), new Vector2(CtlR, 0.82f));
            borderGo.GetComponent<Image>().color = ColBorder;
            borderGo.GetComponent<Image>().raycastTarget = false;

            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(borderGo.transform, false);
            var srt = sliderGo.GetComponent<RectTransform>();
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            float sb = SliderTrackBorderPx;
            srt.offsetMin = new Vector2(sb, sb);
            srt.offsetMax = new Vector2(-sb, -sb);

            MakeImg(sliderGo.transform, "Bg", Vector2.zero, Vector2.one, ColSliderBg);

            var fillArea = new GameObject("FillArea", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            Anchor(fillArea, Vector2.zero, Vector2.one);
            var fillGo = MakeImg(fillArea.transform, "Fill", Vector2.zero, Vector2.one, ColSliderFill);
            var fillRT = fillGo.GetComponent<RectTransform>();

            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            Anchor(handleArea, Vector2.zero, Vector2.one);
            var handleGo = MakeImg(handleArea.transform, "Handle",
                new Vector2(0, 0), new Vector2(0, 1), ColHandle);
            var hRT = handleGo.GetComponent<RectTransform>();
            hRT.sizeDelta = new Vector2(8, 0);

            var slider = sliderGo.AddComponent<MenuSlider>();
            slider.fillRect = fillRT;
            slider.handleRect = hRT;
            slider.targetGraphic = handleGo.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = sliderMax;
            slider.value = Mathf.Clamp(initial, 0f, sliderMax);
            slider.onValueChanged.AddListener(onChange);

            var pct = AddTMP(row, "Pct", new Vector2(PctL, 0), new Vector2(PctR, 1));
            pct.text = sliderMax > 1f + 1e-4f ? MasterVolume.FormatPercent(slider.value) : Pct(slider.value);
            pct.fontSize = 13;
            pct.color = ColValue;
            pct.alignment = TextAlignmentOptions.MidlineLeft;

            return (slider, pct);
        }

        Toggle AddToggle(RectTransform row, bool initial,
            UnityEngine.Events.UnityAction<bool> onChange)
        {
            var frameGo = new GameObject("ToggleFrame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frameGo.transform.SetParent(row, false);
            Anchor(frameGo, new Vector2(CtlL, 0.15f), new Vector2(CtlL, 0.85f));
            var frameRT = frameGo.GetComponent<RectTransform>();
            frameRT.sizeDelta = new Vector2(24, 0);
            frameGo.GetComponent<Image>().color = ColBorder;
            frameGo.GetComponent<Image>().raycastTarget = false;

            var bgGo = MakeImg(frameGo.transform, "ToggleBg", Vector2.zero, Vector2.one, ColSliderBg);
            var bgRT = bgGo.GetComponent<RectTransform>();
            float tb = 2f;
            bgRT.offsetMin = new Vector2(tb, tb);
            bgRT.offsetMax = new Vector2(-tb, -tb);
            var bgImg = bgGo.GetComponent<Image>();

            var chkGo = MakeImg(bgGo.transform, "Check",
                new Vector2(0.15f, 0.15f), new Vector2(0.85f, 0.85f), ColSliderFill);
            var chkImg = chkGo.GetComponent<Image>();

            var lbl = AddTMP(row, "ToggleLabel",
                new Vector2(CtlL + 0.04f, 0), new Vector2(CtlR, 1));
            lbl.text = initial ? "On" : "Off";
            lbl.fontSize = 13;
            lbl.color = ColValue;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            var toggleGo = bgGo;
            var toggle = toggleGo.AddComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = chkImg;
            toggle.isOn = initial;

            var capturedLbl = lbl;
            toggle.onValueChanged.AddListener(v =>
            {
                capturedLbl.text = v ? "On" : "Off";
                onChange(v);
            });

            MenuUiChrome.AttachStandardButtonHover(bgGo);

            return toggle;
        }

        // ================================================================
        //  Primitives (see <see cref="MenuUiChrome"/> for shared SETTINGS-style buttons + inset panels)
        // ================================================================

        static void Anchor(GameObject go, Vector2 min, Vector2 max)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static TextMeshProUGUI AddTMP(RectTransform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.raycastTarget = false;
            return tmp;
        }

        static GameObject MakeImg(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Color col)
        {
            var go = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Anchor(go, anchorMin, anchorMax);
            go.GetComponent<Image>().color = col;
            return go;
        }

        static string Cyc(string v) => $"<  {v}  >";
        static string Pct(float v) => $"{Mathf.RoundToInt(v * 100)}%";
    }
}
