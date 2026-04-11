using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Breathe.Input;

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
        #region PlayerPrefs Keys
        const string KeyMasterVol  = "Breathe_MasterVolume";
        const string KeyMusicVol   = "Breathe_MusicVolume";
        const string KeySfxVol     = "Breathe_SfxVolume";
        const string KeyFullscreen = "Breathe_Fullscreen";
        const string KeyResIdx     = "Breathe_ResolutionIndex";
        #endregion

        #region Palette
        static readonly Color ColHeader    = new(0.82f, 0.90f, 1.00f);
        static readonly Color ColLabel     = new(0.72f, 0.78f, 0.86f);
        static readonly Color ColValue     = Color.white;
        static readonly Color ColDivider   = new(0.35f, 0.40f, 0.52f);
        static readonly Color ColSliderBg  = new(0.22f, 0.27f, 0.36f);
        static readonly Color ColSliderFill= new(0.30f, 0.62f, 0.78f);
        static readonly Color ColHandle    = new(0.90f, 0.93f, 0.97f);
        static readonly Color ColBtn       = new(0.28f, 0.35f, 0.48f);
        static readonly Color ColBtnHi     = new(0.38f, 0.48f, 0.62f);
        static readonly Color ColOverlayBg = new(0.12f, 0.15f, 0.22f, 1.00f);
        #endregion

        #region Runtime State
        float _masterVol = 1f;
        float _musicVol  = 0.8f;
        float _sfxVol    = 0.8f;
        Resolution[] _resolutions;
        int _resIdx;
        #endregion

        #region UI Refs
        RectTransform _content;
        TextMeshProUGUI _inputModeLabel;
        Slider _masterSlider, _musicSlider, _sfxSlider;
        TextMeshProUGUI _masterPct, _musicPct, _sfxPct;
        Toggle _fullscreenToggle;
        TextMeshProUGUI _resLabel;
        GameObject _howToPlayOverlay;
        Button _backBtn;
        float _nextY;
        #endregion

        #region Static Volume API
        public static float MasterVolume => PlayerPrefs.GetFloat(KeyMasterVol, 1f);
        public static float MusicVolume  => PlayerPrefs.GetFloat(KeyMusicVol, 0.8f);
        public static float SfxVolume    => PlayerPrefs.GetFloat(KeySfxVol, 0.8f);
        #endregion

        // ================================================================
        //  Lifecycle
        // ================================================================

        void Awake()
        {
            FindBackButton();
            DestroyAllExceptBack();
            CacheResolutions();
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
            AddDivider();
            AddHeader("Audio");
            (_masterSlider, _masterPct) = AddSliderRow("Master", _masterVol, OnMasterVol);
            (_musicSlider,  _musicPct)  = AddSliderRow("Music",  _musicVol,  OnMusicVol);
            (_sfxSlider,    _sfxPct)    = AddSliderRow("SFX",    _sfxVol,    OnSfxVol);
            AddDivider();
            AddHeader("Display");
            BuildFullscreenRow();
            BuildResolutionRow();
            AddSpacer(4);
            BuildHowToPlayBtn();

            PositionBack();
            BuildHowToPlayOverlay();
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
            rt.offsetMin = new Vector2(24, 44);
            rt.offsetMax = new Vector2(-24, -46);
            return rt;
        }

        void PositionBack()
        {
            if (_backBtn == null) return;
            var rt = _backBtn.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.30f, 0);
            rt.anchorMax = new Vector2(0.70f, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.anchoredPosition = new Vector2(0, 6);
            rt.sizeDelta = new Vector2(0, 32);
            _backBtn.transform.SetAsLastSibling();
        }

        // ================================================================
        //  Row builders — each appends a row at _nextY inside _content,
        //  using top-anchored rects. Children are anchor-positioned.
        //  Label region: left 0% to 32%.  Control region: 34% to 100%.
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
            _inputModeLabel = AddCycleBtn(row, "", CycleInputMode);
        }

        void CycleInputMode()
        {
            var mgr = BreathInputManager.Instance;
            if (mgr == null) return;
            InputMode next = mgr.CurrentMode switch
            {
                InputMode.Simulated  => InputMode.Microphone,
                InputMode.Microphone => InputMode.Fan,
                _                    => InputMode.Simulated
            };
            mgr.SetInputMode(next);
            _inputModeLabel.text = Cyc(next.ToString());
            PlayerPrefs.SetInt(BreathInputManager.PrefKeyInputMode, (int)next);
            PlayerPrefs.Save();
        }

        // ----- Audio -----

        void OnMasterVol(float v)
        {
            _masterVol = v; _masterPct.text = Pct(v);
            AudioListener.volume = v; Save(KeyMasterVol, v);
        }
        void OnMusicVol(float v)
        {
            _musicVol = v; _musicPct.text = Pct(v); Save(KeyMusicVol, v);
        }
        void OnSfxVol(float v)
        {
            _sfxVol = v; _sfxPct.text = Pct(v); Save(KeySfxVol, v);
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

        void BuildResolutionRow()
        {
            var row = PlaceRow(RowH);
            AddLabel(row, "Resolution");
            _resLabel = AddCycleBtn(row, CurrentResText(), CycleResolution);
        }

        void CycleResolution()
        {
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

            StyleBtn(btnGo);
            btnGo.GetComponent<Button>().onClick.AddListener(ShowHTP);

            var tmp = AddTMP(btnGo.GetComponent<RectTransform>(), "Label",
                Vector2.zero, Vector2.one);
            tmp.text = "How to Play";
            tmp.fontSize = 12;
            tmp.color = ColValue;
            tmp.alignment = TextAlignmentOptions.Center;

            btnGo.AddComponent<CardHoverEffect>().SetInteractable(true);
        }

        void BuildHowToPlayOverlay()
        {
            _howToPlayOverlay = new GameObject("HowToPlayPanel",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _howToPlayOverlay.transform.SetParent(transform, false);
            Anchor(_howToPlayOverlay, new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.95f));
            _howToPlayOverlay.GetComponent<Image>().color = ColOverlayBg;

            var titleTmp = AddTMP(_howToPlayOverlay.GetComponent<RectTransform>(),
                "HTP_Title", new Vector2(0, 0.89f), new Vector2(1, 0.97f));
            titleTmp.text = "H O W   T O   P L A Y";
            titleTmp.fontSize = 20;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.color = ColHeader;
            titleTmp.alignment = TextAlignmentOptions.Center;

            var bodyGo = new GameObject("HTP_Body", typeof(RectTransform));
            bodyGo.transform.SetParent(_howToPlayOverlay.transform, false);
            var bodyRT = bodyGo.GetComponent<RectTransform>();
            bodyRT.anchorMin = new Vector2(0, 0.10f);
            bodyRT.anchorMax = new Vector2(1, 0.87f);
            bodyRT.offsetMin = new Vector2(24, 0);
            bodyRT.offsetMax = new Vector2(-24, 0);
            var bodyTmp = bodyGo.AddComponent<TextMeshProUGUI>();
            bodyTmp.text = HowToPlayText();
            bodyTmp.fontSize = 13;
            bodyTmp.color = ColLabel;
            bodyTmp.alignment = TextAlignmentOptions.TopLeft;
            bodyTmp.textWrappingMode = TextWrappingModes.Normal;
            bodyTmp.overflowMode = TextOverflowModes.Truncate;
            bodyTmp.raycastTarget = false;

            var closeGo = new GameObject("BTN_Close",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            closeGo.transform.SetParent(_howToPlayOverlay.transform, false);
            Anchor(closeGo, new Vector2(0.34f, 0.01f), new Vector2(0.66f, 0.08f));
            StyleBtn(closeGo);
            closeGo.GetComponent<Button>().onClick.AddListener(HideHTP);
            var closeTmp = AddTMP(closeGo.GetComponent<RectTransform>(), "Label",
                Vector2.zero, Vector2.one);
            closeTmp.text = "C L O S E";
            closeTmp.fontSize = 13;
            closeTmp.color = ColValue;
            closeTmp.alignment = TextAlignmentOptions.Center;
            closeGo.AddComponent<CardHoverEffect>().SetInteractable(true);

            _howToPlayOverlay.SetActive(false);
        }

        static string HowToPlayText() =>
@"<b>BREATHE</b> is a collection of breath-powered minigames
designed to make breathing exercises fun and engaging.

<b>HOW IT WORKS</b>
Use your breath to control everything in gameplay.
Blow into the fan controller, speak into a microphone,
or press <b>Space</b> to simulate breath input.

<b>THE GAMES</b>
Each minigame uses your breath in a different way --
from gentle sustained blows to quick puffs. Select any
game from Level Select to jump in.

<b>CONTROLS</b>
  - Menus: Mouse click to navigate
  - Gameplay: Breath is your only input
  - Blow steadily for sustained power
  - Blow in short bursts for pulsed actions

<b>TIPS</b>
  - Start with gentle breaths and build up
  - Take breaks between games
  - Have fun -- there's no way to lose!";

        void ShowHTP() { if (_howToPlayOverlay != null) _howToPlayOverlay.SetActive(true); }
        void HideHTP() { if (_howToPlayOverlay != null) _howToPlayOverlay.SetActive(false); }

        // ================================================================
        //  Persistence
        // ================================================================

        void LoadFromPrefs()
        {
            _masterVol = PlayerPrefs.GetFloat(KeyMasterVol, 1f);
            _musicVol  = PlayerPrefs.GetFloat(KeyMusicVol, 0.8f);
            _sfxVol    = PlayerPrefs.GetFloat(KeySfxVol, 0.8f);
            AudioListener.volume = _masterVol;
        }

        void RefreshAll()
        {
            if (_inputModeLabel != null)
            {
                string mode = BreathInputManager.Instance != null
                    ? BreathInputManager.Instance.CurrentMode.ToString() : "Simulated";
                _inputModeLabel.text = Cyc(mode);
            }
            RefreshSlider(_masterSlider, _masterPct, _masterVol);
            RefreshSlider(_musicSlider,  _musicPct,  _musicVol);
            RefreshSlider(_sfxSlider,    _sfxPct,    _sfxVol);
            if (_fullscreenToggle != null)
                _fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
            if (_resLabel != null)
                _resLabel.text = Cyc(CurrentResText());
        }

        static void RefreshSlider(Slider s, TextMeshProUGUI pct, float v)
        {
            if (s != null) s.SetValueWithoutNotify(v);
            if (pct != null) pct.text = Pct(v);
        }

        static void Save(string key, float v)
        {
            PlayerPrefs.SetFloat(key, v);
            PlayerPrefs.Save();
        }

        // ================================================================
        //  Row-level widget factories (anchor-based, no HLG)
        //  Label:   right-aligned  0.06 – 0.28
        //  Control: centered       0.30 – 0.72
        //  Pct:     right edge     0.74 – 0.86
        // ================================================================

        const float LblL = 0.06f, LblR = 0.28f;
        const float CtlL = 0.30f, CtlR = 0.72f;
        const float PctL = 0.74f, PctR = 0.86f;

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

            StyleBtn(go);
            go.GetComponent<Button>().onClick.AddListener(onClick);

            var tmp = AddTMP(go.GetComponent<RectTransform>(), "Value",
                Vector2.zero, Vector2.one);
            tmp.text = Cyc(initial);
            tmp.fontSize = 13;
            tmp.color = ColValue;
            tmp.alignment = TextAlignmentOptions.Center;

            go.AddComponent<CardHoverEffect>().SetInteractable(true);
            return tmp;
        }

        (Slider, TextMeshProUGUI) AddSliderRow(string label, float initial,
            UnityEngine.Events.UnityAction<float> onChange)
        {
            var row = PlaceRow(RowH);
            AddLabel(row, label);

            var sliderGo = new GameObject("Slider", typeof(RectTransform));
            sliderGo.transform.SetParent(row, false);
            Anchor(sliderGo, new Vector2(CtlL, 0.18f), new Vector2(CtlR, 0.82f));

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

            var slider = sliderGo.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = hRT;
            slider.targetGraphic = handleGo.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0;
            slider.maxValue = 1;
            slider.value = initial;
            slider.onValueChanged.AddListener(onChange);

            var pct = AddTMP(row, "Pct", new Vector2(PctL, 0), new Vector2(PctR, 1));
            pct.text = Pct(initial);
            pct.fontSize = 12;
            pct.color = ColValue;
            pct.alignment = TextAlignmentOptions.MidlineLeft;

            return (slider, pct);
        }

        Toggle AddToggle(RectTransform row, bool initial,
            UnityEngine.Events.UnityAction<bool> onChange)
        {
            var bgGo = MakeImg(row, "ToggleBg",
                new Vector2(CtlL, 0.15f), new Vector2(CtlL, 0.85f), ColSliderBg);
            var bgRT = bgGo.GetComponent<RectTransform>();
            bgRT.sizeDelta = new Vector2(20, 0);
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

            return toggle;
        }

        // ================================================================
        //  Primitives
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

        static void StyleBtn(GameObject go)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.color = ColBtn;
            var btn = go.GetComponent<Button>();
            if (btn == null) return;
            var c = btn.colors;
            c.normalColor = ColBtn;
            c.highlightedColor = ColBtnHi;
            c.pressedColor = ColBtnHi;
            c.selectedColor = ColBtn;
            btn.colors = c;
        }

        static string Cyc(string v) => $"<  {v}  >";
        static string Pct(float v) => $"{Mathf.RoundToInt(v * 100)}%";
    }
}
