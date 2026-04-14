using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Breathe.Input;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    // On-screen debug HUD rendered via OnGUI. Shared base sections (breath input, FPS,
    // session time) are always present. Per-minigame sections are read from
    // IMinigame.GetDebugInfo(). Sailboat-specific telemetry (boats, AI, course) is
    // discovered automatically if those objects exist in the scene.
    // Toggle with backtick (`), Tab cycles display modes. Default visibility is also controlled from
    // Settings (PlayerPrefs) so players can disable the overlay without using the keyboard.
    public class DebugOverlay : MonoBehaviour
    {
        public const string PlayerPrefsKey = "Breathe_DebugOverlay";

        [Header("Display Settings")]
        [SerializeField] private bool _visible = true;
        [SerializeField, Tooltip("Margin from top-left corner in pixels.")]
        private float _margin = 12f;

        private enum DisplayMode { Compact, Expanded, Minimal }
        private DisplayMode _displayMode = DisplayMode.Compact;

        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _valueHighlight;

        private Vector2 _scrollPosition;

        // Shared references (always available)
        private BreathPowerSystem _breathPowerSystem;
        private GameStateManager _gameStateManager;

        // Sailboat-specific references (null in non-sailboat scenes)
        private SailboatController _playerBoat;
        private AICompanionController[] _aiBoats;
        private RaceProgressTracker _raceProgressTracker;
        private CourseManager _courseManager;
        private CourseMarkers _courseMarkers;

        public bool Visible => _visible;

        /// <summary>Applies preference, persists it, and updates any active DebugOverlay in loaded scenes.</summary>
        public static void SetEnabledAndSave(bool enabled)
        {
            PlayerPrefs.SetInt(PlayerPrefsKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
            foreach (var o in Object.FindObjectsByType<DebugOverlay>(FindObjectsSortMode.None))
                o.SetVisible(enabled);
        }

        void SetVisible(bool value) => _visible = value;

        private void Start()
        {
            _visible = PlayerPrefs.GetInt(PlayerPrefsKey, 1) != 0;

            _breathPowerSystem = FindAnyObjectByType<BreathPowerSystem>();
            _gameStateManager = FindAnyObjectByType<GameStateManager>();

            _playerBoat = FindAnyObjectByType<SailboatController>();
            _aiBoats = FindObjectsByType<AICompanionController>(FindObjectsSortMode.None);
            _raceProgressTracker = FindAnyObjectByType<RaceProgressTracker>();
            _courseManager = FindAnyObjectByType<CourseManager>();
            _courseMarkers = FindAnyObjectByType<CourseMarkers>();
        }

        private void Update()
        {
            if (Keyboard.current == null) return;

            if (Keyboard.current.backquoteKey.wasPressedThisFrame)
            {
                _visible = !_visible;
                PlayerPrefs.SetInt(PlayerPrefsKey, _visible ? 1 : 0);
                PlayerPrefs.Save();
            }

            if (Keyboard.current.tabKey.wasPressedThisFrame && _visible)
                _displayMode = (DisplayMode)(((int)_displayMode + 1) % 3);
        }

        private void OnGUI()
        {
            if (!_visible) return;

            EnsureStyles();

            float panelWidth = _displayMode == DisplayMode.Expanded ? 340f : 280f;
            float maxHeight = Screen.height - _margin * 2f;

            float panelHeight = _displayMode switch
            {
                DisplayMode.Minimal => 80f,
                DisplayMode.Compact => Mathf.Min(280f, maxHeight),
                DisplayMode.Expanded => Mathf.Min(maxHeight, 700f),
                _ => 280f
            };

            Rect panelRect = new Rect(_margin, _margin, panelWidth, panelHeight);
            GUI.Box(panelRect, GUIContent.none, _boxStyle);

            float innerPad = 8f;
            Rect contentRect = new Rect(
                panelRect.x + innerPad,
                panelRect.y + innerPad,
                panelRect.width - innerPad * 2f,
                panelRect.height - innerPad * 2f
            );

            GUILayout.BeginArea(contentRect);

            string minigameId = MinigameManager.Instance?.ActiveMinigame?.MinigameId ?? "—";
            GUILayout.Label($"<b>BREATHE</b>  <size=10><color=#888>Debug  [{minigameId}]</color></size>",
                _headerStyle);
            GUILayout.Label($"<size=9><color=#555>[`] toggle  [Tab] {_displayMode}</color></size>",
                _labelStyle);
            GUILayout.Space(6f);

            if (_displayMode == DisplayMode.Expanded)
            {
                float scrollHeight = contentRect.height - 38f;
                _scrollPosition = GUILayout.BeginScrollView(
                    _scrollPosition,
                    false, false,
                    GUIStyle.none, GUI.skin.verticalScrollbar,
                    GUILayout.Height(scrollHeight)
                );
                DrawExpanded();
                GUILayout.EndScrollView();
            }
            else
            {
                switch (_displayMode)
                {
                    case DisplayMode.Minimal:
                        DrawMinimal();
                        break;
                    case DisplayMode.Compact:
                        DrawCompact();
                        break;
                }
            }

            GUILayout.EndArea();
        }

        private void DrawMinimal()
        {
            var bim = BreathInputManager.Instance;
            string intensity = bim != null ? $"{bim.GetBreathIntensity():F2}" : "--";
            string fps = $"{1f / Time.smoothDeltaTime:F0}";
            GUILayout.Label($"Breath: <color=#4AF>{intensity}</color>  |  FPS: {fps}", _labelStyle);
        }

        private void DrawCompact()
        {
            DrawSection("BREATH INPUT");
            DrawBreathInfo();

            GUILayout.Space(10f);
            DrawSection("GAME STATE");
            DrawGameStateInfo();

            DrawMinigameDebugSection();

            // Sailboat-specific sections (only if those objects exist)
            if (_playerBoat != null)
            {
                GUILayout.Space(10f);
                DrawSection("PLAYER BOAT");
                DrawPlayerBoatInfo();
            }
        }

        private void DrawExpanded()
        {
            DrawSection("BREATH INPUT");
            DrawBreathInfo();

            GUILayout.Space(10f);
            DrawSection("GAME STATE");
            DrawGameStateInfo();

            DrawMinigameDebugSection();

            // Sailboat-specific sections (auto-discovered, only if present)
            if (_courseMarkers != null)
            {
                GUILayout.Space(10f);
                DrawSection("COURSE");
                DrawCourseInfo();
            }

            if (_playerBoat != null)
            {
                GUILayout.Space(10f);
                DrawSection("PLAYER BOAT");
                DrawPlayerBoatInfo();
                DrawPlacementInfo();
            }

            if (_aiBoats != null && _aiBoats.Length > 0)
            {
                GUILayout.Space(10f);
                DrawSection("AI BOATS");
                DrawAIBoatsInfo();
            }

            GUILayout.Space(10f);
            DrawSection("SYSTEMS");
            DrawSystemsInfo();

            GUILayout.Space(12f);
        }

        // Renders key-value pairs from the active IMinigame.GetDebugInfo()
        private void DrawMinigameDebugSection()
        {
            var minigame = MinigameManager.Instance?.ActiveMinigame;
            if (minigame == null) return;

            Dictionary<string, string> debugInfo = minigame.GetDebugInfo();
            if (debugInfo == null || debugInfo.Count == 0) return;

            GUILayout.Space(10f);
            DrawSection($"MINIGAME ({minigame.MinigameId.ToUpper()})");
            foreach (var kvp in debugInfo)
                DrawRow(kvp.Key, kvp.Value);
        }

        private void DrawGameStateInfo()
        {
            string state = _gameStateManager != null
                ? _gameStateManager.CurrentState.ToString()
                : "N/A";
            DrawRow("State", state);

            if (_courseManager != null)
            {
                string time = FormatTime(_courseManager.RaceTime);
                DrawRow("Race Time", time);
            }

            if (_raceProgressTracker != null)
            {
                string progress = _raceProgressTracker.Progress.ToString("P1");
                DrawRow("Progress", $"<color=#4CF>{progress}</color>");
            }
        }

        private void DrawCourseInfo()
        {
            if (_courseMarkers != null)
            {
                DrawRow("Layout", $"<color=#FFA>{_courseMarkers.ActiveLayoutName}</color>");
                DrawRow("Length", $"{_courseMarkers.CourseLength:F0} units");
            }
        }

        private void DrawBreathInfo()
        {
            var bim = BreathInputManager.Instance;
            if (bim == null)
            {
                DrawRow("Status", "<color=#F66>No BreathInputManager</color>");
                return;
            }

            DrawRow("Source", bim.InputSourceName);
            DrawRow("Intensity", $"{bim.GetBreathIntensity():F3}");
            DrawRow("Level", bim.GetBreathLevel().ToString());

            if (_breathPowerSystem != null)
                DrawRow("Breath Power", $"{_breathPowerSystem.BreathPower:F2}");
        }

        private void DrawPlayerBoatInfo()
        {
            if (_playerBoat == null) return;

            var zone = EnvironmentalZoneEffect.CurrentZoneForDebug;
            if (zone != null)
                DrawRow("Zone", $"<color=#8CF>{zone.ZoneType}</color>");

            float speedRaw = _playerBoat.CurrentSpeed;
            float knots = WindSpeedConverter.ToKnots(speedRaw);
            float mph = WindSpeedConverter.ToMph(speedRaw);

            DrawRow("Speed", $"{speedRaw:F2} u/s");
            DrawRow("", $"<size=11><color=#AAA>{knots:F1} knots  |  {mph:F1} mph</color></size>");

            Vector3 pos = _playerBoat.transform.position;
            DrawRow("Position", $"({pos.x:F1}, {pos.y:F1})");
            DrawRow("Finished", _playerBoat.FinishedCourse ? "<color=#4F4>Yes</color>" : "No");
        }

        private void DrawPlacementInfo()
        {
            if (_playerBoat == null || _aiBoats == null || _aiBoats.Length == 0) return;

            float playerY = _playerBoat.transform.position.y;
            int place = 1;
            foreach (var ai in _aiBoats)
                if (ai != null && ai.transform.position.y > playerY) place++;

            string ordinal = place switch
            {
                1 => "<color=#FFD700>1st</color>",
                2 => "<color=#C0C0C0>2nd</color>",
                3 => "<color=#CD7F32>3rd</color>",
                _ => $"{place}th"
            };
            DrawRow("Position", $"{ordinal} of {_aiBoats.Length + 1}");
        }

        private void DrawAIBoatsInfo()
        {
            if (_aiBoats == null || _aiBoats.Length == 0)
            {
                DrawRow("AI Boats", "None");
                return;
            }

            for (int i = 0; i < _aiBoats.Length; i++)
            {
                var ai = _aiBoats[i];
                if (ai == null) continue;

                string stunned = ai.IsStunned ? " <color=#F88>[STUNNED]</color>" : "";
                float progress = ai.CourseProgress;
                DrawRow($"AI {i}", $"{progress:P0} @ Y={ai.transform.position.y:F0}{stunned}");
            }
        }

        private void DrawSystemsInfo()
        {
            DrawRow("Frame Rate", $"{1f / Time.smoothDeltaTime:F0} FPS");
            DrawRow("Time Scale", $"{Time.timeScale:F2}x");
            DrawRow("Play Time", FormatTime(Time.time));
        }

        private static string FormatTime(float seconds)
        {
            int min = Mathf.FloorToInt(seconds / 60f);
            float sec = seconds % 60f;
            return $"{min}:{sec:00.0}";
        }

        private void DrawSection(string title)
        {
            GUILayout.Label($"<color=#888>── {title} ──</color>", _sectionStyle);
        }

        private void DrawRow(string label, string value)
        {
            if (string.IsNullOrEmpty(label))
                GUILayout.Label(value, _labelStyle);
            else
                GUILayout.Label($"<color=#AAA>{label}:</color>  {value}", _labelStyle);
        }

        private void EnsureStyles()
        {
            if (_boxStyle != null) return;

            _boxStyle = new GUIStyle(GUI.skin.box);
            Texture2D bgTex = new Texture2D(1, 1);
            bgTex.SetPixel(0, 0, new Color(0.02f, 0.02f, 0.06f, 0.92f));
            bgTex.Apply();
            _boxStyle.normal.background = bgTex;

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 12,
                normal = { textColor = Color.white },
                padding = new RectOffset(0, 0, 2, 2),
                margin = new RectOffset(0, 0, 0, 0)
            };

            _headerStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 14,
                padding = new RectOffset(0, 0, 0, 4)
            };

            _sectionStyle = new GUIStyle(_labelStyle)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(0, 0, 4, 2)
            };

            _valueHighlight = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold
            };
        }
    }
}
