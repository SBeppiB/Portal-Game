using System;
using UnityEngine;

namespace Portals.Menus
{
    /// <summary>
    /// Shared IMGUI presentation for the main menu and pause menu: atmospheric
    /// gradient background, styled buttons, centred layout, transient status
    /// messages and a modal confirmation dialog. Everything runs on unscaled
    /// time (OnGUI is unaffected by Time.timeScale), so menus stay responsive
    /// while the game is paused.
    /// </summary>
    public static class MenuGUI
    {
        private static Texture2D _background;
        private static Texture2D _panelTex;
        private static Texture2D _buttonTex;
        private static Texture2D _buttonHoverTex;
        private static Texture2D _buttonDisabledTex;
        private static bool _stylesReady;

        private static GUIStyle _titleStyle;
        private static GUIStyle _subtitleStyle;
        private static GUIStyle _buttonStyle;
        private static GUIStyle _buttonDisabledStyle;
        private static GUIStyle _labelStyle;
        private static GUIStyle _smallStyle;
        private static GUIStyle _boxStyle;
        private static GUIStyle _headerStyle;

        private static string _status;
        private static float _statusUntil;
        private const float StatusDuration = 4f;

        private static Texture2D _dimTex;
        private static AudioClip _clickClip;
        private static AudioSource _clickSource;

        private static readonly Color Accent = new Color(1.00f, 0.55f, 0.10f);
        private static readonly Color PanelColor = new Color(0.03f, 0.05f, 0.08f, 0.92f);

        public static void EnsureStyles()
        {
            if (_stylesReady) return;
            _stylesReady = true;

            _background = BuildGradient(new Color(0.035f, 0.055f, 0.085f), new Color(0.008f, 0.010f, 0.016f));
            _panelTex = BuildSolid(PanelColor);
            _buttonTex = BuildSolid(new Color(0.06f, 0.10f, 0.15f, 0.95f));
            _buttonHoverTex = BuildSolid(new Color(0.95f, 0.45f, 0.08f, 0.95f));
            _buttonDisabledTex = BuildSolid(new Color(0.10f, 0.11f, 0.13f, 0.80f));

            _titleStyle = new GUIStyle
            {
                fontSize = 76,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _titleStyle.normal.textColor = Accent;

            _subtitleStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter
            };
            _subtitleStyle.normal.textColor = new Color(0.75f, 0.82f, 0.90f);

            _headerStyle = new GUIStyle
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _headerStyle.normal.textColor = Accent;

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                fixedHeight = 46,
                margin = new RectOffset(6, 6, 6, 6)
            };
            _buttonStyle.normal.background = _buttonTex;
            _buttonStyle.normal.textColor = Color.white;
            _buttonStyle.hover.background = _buttonHoverTex;
            _buttonStyle.hover.textColor = Color.black;
            _buttonStyle.active.background = _buttonHoverTex;
            _buttonStyle.active.textColor = Color.black;
            _buttonStyle.focused.background = _buttonHoverTex;
            _buttonStyle.focused.textColor = Color.black;

            _buttonDisabledStyle = new GUIStyle(_buttonStyle)
            {
                fontSize = 22
            };
            _buttonDisabledStyle.normal.background = _buttonDisabledTex;
            _buttonDisabledStyle.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
            _buttonDisabledStyle.hover.background = _buttonDisabledTex;
            _buttonDisabledStyle.hover.textColor = new Color(0.5f, 0.5f, 0.5f);
            _buttonDisabledStyle.active.background = _buttonDisabledTex;
            _buttonDisabledStyle.active.textColor = new Color(0.5f, 0.5f, 0.5f);
            _buttonDisabledStyle.focused.background = _buttonDisabledTex;
            _buttonDisabledStyle.focused.textColor = new Color(0.5f, 0.5f, 0.5f);

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft
            };
            _labelStyle.normal.textColor = new Color(0.85f, 0.88f, 0.92f);

            _smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleLeft
            };
            _smallStyle.normal.textColor = new Color(0.6f, 0.65f, 0.72f);

            _boxStyle = new GUIStyle(GUI.skin.box);
            _boxStyle.normal.background = _panelTex;
        }

        public static void DrawBackground()
        {
            EnsureStyles();
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _background);
        }

        /// <summary>Translucent black veil drawn over the frozen gameplay frame.</summary>
        public static void DrawDim(float alpha)
        {
            EnsureStyles();
            if (_dimTex == null)
            {
                _dimTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                for (int i = 0; i < 4; i++) _dimTex.SetPixel(0, 0, new Color(0f, 0f, 0f, alpha));
                _dimTex.Apply();
            }
            else
            {
                // Alpha can differ per call; rebuild the single pixel when needed.
                var c = _dimTex.GetPixel(0, 0);
                if (Mathf.Abs(c.a - alpha) > 0.001f)
                {
                    for (int i = 0; i < 4; i++) _dimTex.SetPixel(0, 0, new Color(0f, 0f, 0f, alpha));
                    _dimTex.Apply();
                }
            }
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _dimTex);
        }

        /// <summary>
        /// Tiny synthesized UI blip (no audio assets exist in the project) played
        /// through the menu's own AudioSource so it works while gameplay audio is
        /// listener-swapped away during pause.
        /// </summary>
        public static void PlayClick()
        {
            try
            {
                if (_clickClip == null)
                {
                    const int rate = 22050;
                    const float freq = 760f;
                    const float dur = 0.055f;
                    int samples = (int)(rate * dur);
                    float[] data = new float[samples];
                    for (int i = 0; i < samples; i++)
                    {
                        float t = i / (float)rate;
                        float envelope = 1f - t / dur;
                        data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * 0.35f * envelope * envelope;
                    }
                    _clickClip = AudioClip.Create("ui_click", samples, 1, rate, false);
                    _clickClip.SetData(data, 0);
                }

                if (_clickSource == null)
                {
                    var host = GameObject.Find("Portal Game");
                    if (host == null) return;
                    _clickSource = host.GetComponent<AudioSource>();
                    if (_clickSource == null) _clickSource = host.AddComponent<AudioSource>();
                    _clickSource.playOnAwake = false;
                    _clickSource.spatialBlend = 0f;
                }

                _clickSource.PlayOneShot(_clickClip);
            }
            catch { /* UI sound must never break input handling */ }
        }

        public static Rect CenterArea(float width, float height)
        {
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
        }

        public static bool Button(Rect rect, string text, bool enabled = true)
        {
            EnsureStyles();
            return GUI.Button(rect, text, enabled ? _buttonStyle : _buttonDisabledStyle) && enabled;
        }

        /// <summary>Centred vertical stack of full-width buttons with fixed spacing.</summary>
        public static bool[] ButtonColumn(Rect area, string[] labels, bool[] enabled, float buttonHeight = 46f, float spacing = 8f)
        {
            EnsureStyles();
            var results = new bool[labels.Length];
            float y = area.y;
            for (int i = 0; i < labels.Length; i++)
            {
                var r = new Rect(area.x + 30f, y, area.width - 60f, buttonHeight);
                results[i] = GUI.Button(r, labels[i], enabled == null || enabled[i] ? _buttonStyle : _buttonDisabledStyle);
                if (enabled != null && !enabled[i]) results[i] = false;
                y += buttonHeight + spacing;
            }
            return results;
        }

        public static void Label(Rect rect, string text, int fontSize = 16, TextAnchor anchor = TextAnchor.MiddleLeft, Color? color = null)
        {
            EnsureStyles();
            var style = new GUIStyle(_labelStyle) { fontSize = fontSize, alignment = anchor };
            style.normal.textColor = color ?? _labelStyle.normal.textColor;
            GUI.Label(rect, text, style);
        }

        public static void Status(string message)
        {
            _status = message;
            _statusUntil = Time.unscaledTime + StatusDuration;
        }

        public static void DrawStatus(Rect area)
        {
            if (string.IsNullOrEmpty(_status) || Time.unscaledTime > _statusUntil) return;
            Label(new Rect(area.x, area.y, area.width, 24f), _status, 15, TextAnchor.MiddleCenter,
                new Color(1f, 0.75f, 0.4f));
        }

        /// <summary>
        /// Modal confirmation dialog. Returns true when the caller should proceed.
        /// The caller owns the screen state (this only draws and returns a choice).
        /// </summary>
        public static bool Confirm(string title, string message)
        {
            EnsureStyles();
            bool proceed = false;

            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _panelTex);

            Rect box = CenterArea(520f, 240f);
            GUI.Box(box, GUIContent.none);

            Label(new Rect(box.x, box.y + 24f, box.width, 36f), title, 24, TextAnchor.MiddleCenter, Accent);
            Label(new Rect(box.x + 30f, box.y + 74f, box.width - 60f, 70f), message, 16, TextAnchor.UpperCenter);

            float bw = 180f;
            float by = box.yMax - 70f;
            if (GUI.Button(new Rect(box.center.x - bw - 10f, by, bw, 46f), "Yes", _buttonStyle))
                proceed = true;
            if (GUI.Button(new Rect(box.center.x + 10f, by, bw, 46f), "No", _buttonStyle))
                proceed = false;

            // While the dialog is up, clicking outside must not fall through to
            // anything behind it (it covers the full screen anyway).
            return proceed;
        }

        private static Texture2D BuildSolid(Color color)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            for (int i = 0; i < 4; i++) tex.SetPixel(0, 0, color);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            return tex;
        }

        private static Texture2D BuildGradient(Color top, Color bottom)
        {
            const int height = 256;
            var tex = new Texture2D(8, height, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1);
                Color c = Color.Lerp(top, bottom, t);
                // Subtle centre-column glow for depth.
                for (int x = 0; x < 8; x++)
                {
                    float glow = 0.04f * (1f - Mathf.Abs(x - 3.5f) / 3.5f);
                    tex.SetPixel(x, y, new Color(c.r + glow, c.g + glow, c.b + glow, 1f));
                }
            }
            tex.Apply();
            return tex;
        }
    }
}
