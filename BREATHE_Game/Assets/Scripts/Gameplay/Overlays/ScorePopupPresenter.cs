using System.Collections.Generic;
using UnityEngine;
using Breathe.Utility;

namespace Breathe.Gameplay
{
    /// <summary>
    /// OnGUI floating score lines: scale pop-in, subtle pulse, drift upward, fade out.
    /// Optional <see cref="PushFollowing"/> keeps text centered on a world transform (e.g. a bubble).
    /// Host minigame calls <see cref="Tick"/> from Update and <see cref="DrawOnGUI"/> after its HUD.
    /// </summary>
    public sealed class ScorePopupPresenter
    {
        sealed class Entry
        {
            public float Time;
            public Vector2 Center;
            public string Text;
            public Color Color;
            public float Phase;
            public Transform FollowTransform;
            public Camera FollowCam;
        }

        readonly List<Entry> _entries = new List<Entry>();

        const float Duration = 1.45f;
        const float DriftPxPerSec = 42f;
        const int MaxEntries = 14;

        GUIStyle _style;
        bool _styleReady;
        float _fontScale = 1f;

        /// <summary>Multiplies base popup font size (44). Default 1. Skydive uses ~1.1–1.35 for readability.</summary>
        public float FontScale
        {
            get => _fontScale;
            set
            {
                float v = Mathf.Max(0.5f, value);
                if (Mathf.Approximately(_fontScale, v)) return;
                _fontScale = v;
                _styleReady = false;
            }
        }

        public void Clear() => _entries.Clear();

        /// <param name="screenCenter">Optional anchor in screen space (y=0 top). Random jitter if null.</param>
        public void Push(string text, Color color, Vector2? screenCenter = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            float cx = screenCenter?.x ?? Screen.width * 0.5f + Random.Range(-110, 111);
            float cy = screenCenter?.y ?? Screen.height * 0.36f + Random.Range(-36, 37);
            if (_entries.Count >= MaxEntries)
                _entries.RemoveAt(0);
            _entries.Add(new Entry
            {
                Time = 0f,
                Center = new Vector2(cx, cy),
                Text = text,
                Color = color,
                Phase = Random.value * 6.283185f,
                FollowTransform = null,
                FollowCam = null
            });
        }

        /// <summary>Popup stays centered on <paramref name="follow"/> in screen space; fade timing matches <see cref="Push"/>.</summary>
        public void PushFollowing(string text, Color color, Transform follow, Camera cam)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (follow == null || cam == null)
            {
                Push(text, color, null);
                return;
            }

            Vector3 sp = cam.WorldToScreenPoint(follow.position);
            float cx = sp.x;
            float cy = Screen.height - sp.y;
            if (sp.z <= 0f)
            {
                Push(text, color, null);
                return;
            }

            if (_entries.Count >= MaxEntries)
                _entries.RemoveAt(0);
            _entries.Add(new Entry
            {
                Time = 0f,
                Center = new Vector2(cx, cy),
                Text = text,
                Color = color,
                Phase = Random.value * 6.283185f,
                FollowTransform = follow,
                FollowCam = cam
            });
        }

        public void Tick(float deltaTime)
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry e = _entries[i];
                e.Time += deltaTime;

                if (e.FollowTransform != null && e.FollowCam != null)
                {
                    Transform tr = e.FollowTransform;
                    if (tr != null)
                    {
                        Vector3 sp = e.FollowCam.WorldToScreenPoint(tr.position);
                        if (sp.z > 0f)
                            e.Center = new Vector2(sp.x, Screen.height - sp.y);
                    }
                    else
                    {
                        e.FollowTransform = null;
                        e.FollowCam = null;
                    }
                }
                else
                {
                    Vector2 c = e.Center;
                    c.y -= DriftPxPerSec * deltaTime;
                    e.Center = c;
                }

                if (e.Time >= Duration)
                    _entries.RemoveAt(i);
            }
        }

        public void DrawOnGUI()
        {
            if (_entries.Count == 0) return;
            EnsureStyle();

            Color prevGui = GUI.color;
            Matrix4x4 prevMat = GUI.matrix;

            for (int i = 0; i < _entries.Count; i++)
            {
                Entry e = _entries[i];
                float u = e.Time / Duration;
                float alpha = u < 0.14f
                    ? u / 0.14f
                    : (u > 0.58f ? 1f - (u - 0.58f) / 0.42f : 1f);

                float pop = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e.Time / 0.2f));
                float overshoot = 1f + 0.12f * Mathf.Sin(pop * Mathf.PI);
                float pulse = 1f + 0.065f * Mathf.Sin(Time.unscaledTime * 9f + e.Phase);
                float s = Mathf.Clamp(pop * overshoot * pulse, 0.35f, 1.65f);

                float w = Mathf.Min(760f, Screen.width * 0.92f);
                float h = Mathf.Max(86f, _style.fontSize * 1.45f);
                Rect r = new Rect(e.Center.x - w * 0.5f, e.Center.y - h * 0.5f, w, h);

                float a = alpha * (e.Color.a > 0.01f ? e.Color.a : 1f);
                GUI.color = new Color(e.Color.r, e.Color.g, e.Color.b, a);

                Vector3 pivot = new Vector3(e.Center.x, e.Center.y, 0f);
                GUI.matrix = Matrix4x4.TRS(pivot, Quaternion.identity, new Vector3(s, s, 1f))
                    * Matrix4x4.TRS(-pivot, Quaternion.identity, Vector3.one);

                GameFont.OutlinedLabel(r, e.Text, _style, 2);
                GUI.matrix = prevMat;
            }

            GUI.color = prevGui;
        }

        void EnsureStyle()
        {
            if (_styleReady) return;
            _styleReady = true;
            Font f = GameFont.Get();
            int fs = Mathf.Max(12, Mathf.RoundToInt(44f * _fontScale));
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = fs,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _style.normal.textColor = Color.white;
            if (f != null) _style.font = f;
        }
    }
}
