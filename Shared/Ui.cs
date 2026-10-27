using UnityEngine;

namespace PeakShared
{
    // Small immediate-mode widget kit: dark flat theme, rounded corners, accent colour.
    internal static class Ui
    {
        internal static readonly Color Accent = new Color32(0xF5, 0xA5, 0x24, 255);
        internal static readonly Color Good = new Color32(0x3D, 0xDC, 0x97, 255);
        internal static readonly Color Bad = new Color32(0xF0, 0x5D, 0x5E, 255);
        internal static readonly Color Text = new Color32(0xE8, 0xEA, 0xED, 255);
        internal static readonly Color Muted = new Color32(0x8B, 0x92, 0x9C, 255);
        private static readonly Color WindowBg = new Color32(0x14, 0x17, 0x1C, 245);
        private static readonly Color CardBg = new Color32(0x1E, 0x23, 0x2B, 255);
        private static readonly Color Control = new Color32(0x2A, 0x31, 0x3B, 255);
        private static readonly Color ControlHover = new Color32(0x37, 0x40, 0x4D, 255);
        private static readonly Color Track = new Color32(0x33, 0x3A, 0x45, 255);

        private static bool _ready;
        private static GUIStyle _window, _card, _round, _label, _muted, _title, _btnText, _field;
        private static GUIStyle _btnDraw;

        internal static GUIStyle WindowStyle { get { Init(); return _window; } }
        internal static GUIStyle Title { get { Init(); return _title; } }
        internal static GUIStyle Muted_ { get { Init(); return _muted; } }
        internal static GUIStyle Field { get { Init(); return _field; } }

        private static Texture2D Rounded(Color c, int radius = 6)
        {
            int size = radius * 2 + 4;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x, radius, size - 1 - radius);
                    float cy = Mathf.Clamp(y, radius, size - 1 - radius);
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    t.SetPixel(x, y, new Color(c.r, c.g, c.b, c.a * a));
                }
            t.Apply();
            return t;
        }

        private static GUIStyle Box(Color c, int radius, int pad)
        {
            var s = new GUIStyle
            {
                border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1),
                padding = new RectOffset(pad, pad, pad, pad),
                margin = new RectOffset(0, 0, 3, 3),
            };
            s.normal.background = Rounded(c, radius);
            return s;
        }

        private static void Init()
        {
            if (_ready && _window.normal.background != null) return;
            _window = Box(WindowBg, 10, 12);
            _window.margin = new RectOffset(0, 0, 0, 0);
            _card = Box(CardBg, 8, 10);
            _round = Box(Color.white, 6, 0);
            _field = Box(Track, 5, 6);
            _field.normal.textColor = Text;
            _field.focused.textColor = Text;
            _field.fontSize = 13;
            _field.alignment = TextAnchor.MiddleLeft;
            _field.clipping = TextClipping.Clip;

            _label = new GUIStyle { fontSize = 13, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            _label.normal.textColor = Text;
            _muted = new GUIStyle(_label) { fontSize = 12 };
            _muted.normal.textColor = Muted;
            _title = new GUIStyle(_label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _btnText = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Clip };
            _btnDraw = new GUIStyle(_btnText);
            _ready = true;
        }

        private static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = prev * c;
            _round.Draw(r, false, false, false, false);
            GUI.color = prev;
        }

        private static bool Hover(Rect r) => r.Contains(Event.current.mousePosition);

        // ---- layout containers ---------------------------------------------------------------

        internal static void BeginCard() { Init(); GUILayout.BeginVertical(_card); }
        internal static void EndCard() => GUILayout.EndVertical();

        internal static void Label(string text) { Init(); GUILayout.Label(text, _label); }
        internal static void Hint(string text) { Init(); GUILayout.Label(text, _muted); }

        internal static void Heading(string text)
        {
            Init();
            GUILayout.Space(4);
            var s = new GUIStyle(_muted) { fontStyle = FontStyle.Bold };
            s.normal.textColor = Accent;
            GUILayout.Label(text.ToUpperInvariant(), s);
        }

        // ---- widgets -------------------------------------------------------------------------

        internal static bool Tab(string text, bool selected)
        {
            Init();
            var r = GUILayoutUtility.GetRect(new GUIContent(text), _btnText, GUILayout.Height(28), GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                Fill(r, selected ? Accent : Hover(r) ? ControlHover : Control);
                var st = new GUIStyle(_btnText) { fontStyle = selected ? FontStyle.Bold : FontStyle.Normal };
                st.normal.textColor = selected ? new Color32(0x14, 0x17, 0x1C, 255) : Text;
                st.Draw(r, text, false, false, false, false);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        internal enum Kind { Normal, Accent, Danger }

        internal static bool Button(string text, Kind kind = Kind.Normal, bool enabled = true, float width = 0f)
        {
            Init();
            var opts = width > 0 ? new[] { GUILayout.Height(28), GUILayout.Width(width) } : new[] { GUILayout.Height(28), GUILayout.ExpandWidth(true) };
            var r = GUILayoutUtility.GetRect(new GUIContent(text), _btnText, opts);
            if (Event.current.type == EventType.Repaint)
            {
                Color bg = kind == Kind.Accent ? Accent : kind == Kind.Danger ? Bad : Control;
                if (Hover(r) && enabled) bg = Color.Lerp(bg, Color.white, 0.12f);
                if (!enabled) bg = Color.Lerp(bg, WindowBg, 0.55f);
                Fill(r, bg);
                var st = _btnDraw;
                st.normal.textColor = !enabled ? Muted : kind == Kind.Normal ? Text : new Color32(0x14, 0x17, 0x1C, 255);
                st.Draw(r, text, false, false, false, false);
            }
            return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        internal static bool Switch(string text, bool value)
        {
            Init();
            var r = GUILayoutUtility.GetRect(1, 26, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                var track = new Rect(r.x, r.y + 4, 36, 18);
                Fill(track, value ? Accent : Track);
                var knob = new Rect(track.x + (value ? 20 : 2), track.y + 2, 14, 14);
                Fill(knob, Color.white);
                _label.Draw(new Rect(r.x + 46, r.y, r.width - 46, r.height), text, false, false, false, false);
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none) ? !value : value;
        }

        internal static float Slider(string text, float value, float min, float max, string fmt = "0.#")
        {
            Init();
            GUILayout.BeginHorizontal();
            GUILayout.Label(text, _label);
            GUILayout.FlexibleSpace();
            var right = new GUIStyle(_label) { alignment = TextAnchor.MiddleRight, fixedWidth = 60 };
            right.normal.textColor = Accent;
            GUILayout.Label(value.ToString(fmt), right);
            GUILayout.EndHorizontal();

            var r = GUILayoutUtility.GetRect(1, 20, GUILayout.ExpandWidth(true));
            int id = GUIUtility.GetControlID(FocusType.Passive);
            var e = Event.current;
            switch (e.type)
            {
                case EventType.MouseDown when r.Contains(e.mousePosition):
                    GUIUtility.hotControl = id; value = Pick(r, e.mousePosition.x, min, max); e.Use(); break;
                case EventType.MouseDrag when GUIUtility.hotControl == id:
                    value = Pick(r, e.mousePosition.x, min, max); e.Use(); break;
                case EventType.MouseUp when GUIUtility.hotControl == id:
                    GUIUtility.hotControl = 0; e.Use(); break;
                case EventType.Repaint:
                    var bar = new Rect(r.x, r.y + 8, r.width, 4);
                    Fill(bar, Track);
                    float t = Mathf.InverseLerp(min, max, value);
                    Fill(new Rect(bar.x, bar.y, Mathf.Max(4, bar.width * t), 4), Accent);
                    Fill(new Rect(r.x + (r.width - 14) * t, r.y + 3, 14, 14), Color.white);
                    break;
            }
            return value;
        }

        private static float Pick(Rect r, float x, float min, float max) =>
            Mathf.Lerp(min, max, Mathf.Clamp01((x - r.x - 7) / (r.width - 14)));

        // Thin progress bar, value 0..1.
        internal static void Bar(float value, Color color, string label = null)
        {
            Init();
            GUILayout.BeginHorizontal();
            if (label != null) GUILayout.Label(label, _muted, GUILayout.Width(72));
            var r = GUILayoutUtility.GetRect(1, 8, GUILayout.ExpandWidth(true));
            r.y += 5;
            r.height = 8;
            if (Event.current.type == EventType.Repaint)
            {
                Fill(r, Track);
                Fill(new Rect(r.x, r.y, Mathf.Max(6, r.width * Mathf.Clamp01(value)), r.height), color);
            }
            GUILayout.EndHorizontal();
        }

        internal static void Pill(string text, Color color)
        {
            Init();
            var st = new GUIStyle(_btnText) { fontSize = 11, fontStyle = FontStyle.Bold };
            var r = GUILayoutUtility.GetRect(new GUIContent(text), st, GUILayout.Height(20), GUILayout.Width(st.CalcSize(new GUIContent(text)).x + 18));
            if (Event.current.type == EventType.Repaint)
            {
                Fill(r, new Color(color.r, color.g, color.b, 0.22f));
                st.normal.textColor = color;
                st.Draw(r, text, false, false, false, false);
            }
        }
    }
}
