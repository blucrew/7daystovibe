using UnityEngine;

namespace HapticsPlugin
{
    /// <summary>
    /// Central visual theme for the in-game IMGUI panel.
    ///
    /// Palette = "Dark Mode (OLED)" design system:
    ///   ink    #020617  background / deepest layer
    ///   panel  #0F172A  primary surface (window body)
    ///   panel2 #1E293B  secondary surface (rows, buttons, inputs)
    ///   line   #1F2C43  borders / dividers
    ///   fg     #F8FAFC  primary text
    ///   muted  #94A3B8  secondary text
    ///   accent #22C55E  positive / CTA / active state
    ///   warn   #F59E0B  warnings
    ///   danger #F43F5E  errors / unsupported
    ///
    /// IMGUI has no styling sheet, so we theme by:
    ///   1. baking 1×1 solid-colour textures for each palette slot, and
    ///   2. assigning them as the background of a cloned GUISkin's control styles.
    /// Textures are created once and kept alive with HideAndDontSave.
    /// </summary>
    internal static class HapticsTheme
    {
        // ── Palette (sRGB → linear-ish float, good enough for flat UI) ──────────
        public static readonly Color Ink     = Hex(0x02, 0x06, 0x17);
        public static readonly Color Panel   = Hex(0x0F, 0x17, 0x2A);
        public static readonly Color Panel2  = Hex(0x1E, 0x29, 0x3B);
        public static readonly Color Line    = Hex(0x1F, 0x2C, 0x43);
        public static readonly Color Fg       = Hex(0xF8, 0xFA, 0xFC);
        public static readonly Color Muted    = Hex(0x94, 0xA3, 0xB8);
        public static readonly Color Accent   = Hex(0x22, 0xC5, 0x5E);
        public static readonly Color AccentLo = Hex(0x16, 0x83, 0x3F);
        public static readonly Color Warn     = Hex(0xF5, 0x9E, 0x0B);
        public static readonly Color Danger   = Hex(0xF4, 0x3F, 0x5E);

        private static Color Hex(int r, int g, int b, float a = 1f)
            => new Color(r / 255f, g / 255f, b / 255f, a);

        // ── Solid 1×1 textures (lazily built, kept alive) ──────────────────────
        private static Texture2D _texPanel, _texPanel2, _texLine, _texAccent, _texAccentLo, _texInk;

        public static Texture2D TexPanel    => _texPanel    ??= Solid(Panel);
        public static Texture2D TexPanel2   => _texPanel2   ??= Solid(Panel2);
        public static Texture2D TexLine     => _texLine     ??= Solid(Line);
        public static Texture2D TexAccent   => _texAccent   ??= Solid(Accent);
        public static Texture2D TexAccentLo => _texAccentLo ??= Solid(AccentLo);
        public static Texture2D TexInk      => _texInk      ??= Solid(Ink);

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode  = TextureWrapMode.Repeat,
                filterMode = FilterMode.Point,
            };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        // ── Skin ────────────────────────────────────────────────────────────────
        private static GUISkin _skin;

        /// <summary>Build (once) and return the themed skin cloned from the supplied base.</summary>
        public static GUISkin GetSkin(GUISkin baseSkin)
        {
            if (_skin != null) return _skin;

            var s = Object.Instantiate(baseSkin);
            s.hideFlags = HideFlags.HideAndDontSave;

            // Window: panel body, accent title text.
            Paint(s.window.normal, TexPanel, Fg);
            Paint(s.window.onNormal, TexPanel, Fg);
            s.window.border  = new RectOffset(8, 8, 22, 8);
            s.window.padding = new RectOffset(0, 0, 0, 0);
            s.window.normal.textColor = s.window.onNormal.textColor = Accent;
            s.window.fontStyle = FontStyle.Bold;
            s.window.alignment = TextAnchor.UpperLeft;
            s.window.contentOffset = new Vector2(6f, 2f);

            // Box / panels.
            Paint(s.box.normal, TexPanel2, Fg);
            s.box.border = new RectOffset(2, 2, 2, 2);

            // Buttons: panel2 idle, line hover, accent text when active/pressed.
            Paint(s.button.normal,   TexPanel2,   Muted);
            Paint(s.button.hover,    TexLine,     Fg);
            Paint(s.button.active,   TexAccentLo, Fg);
            Paint(s.button.onNormal, TexAccentLo, Fg);   // selected (SelectionGrid)
            Paint(s.button.onHover,  TexAccent,   Ink);
            Paint(s.button.onActive, TexAccent,   Ink);
            s.button.border  = new RectOffset(3, 3, 3, 3);
            s.button.margin  = new RectOffset(2, 2, 2, 2);
            s.button.padding = new RectOffset(4, 4, 3, 3);

            // Labels.
            s.label.normal.textColor = Fg;
            s.label.padding = new RectOffset(2, 2, 1, 1);

            // Text fields.
            Paint(s.textField.normal, TexInk, Fg);
            Paint(s.textField.focused, TexInk, Fg);
            s.textField.border = new RectOffset(3, 3, 3, 3);

            // Toggle: solid square — panel2 when off, accent when on. Colour = state.
            Paint(s.toggle.normal,    TexPanel2, Fg);
            Paint(s.toggle.hover,     TexLine,   Fg);
            Paint(s.toggle.onNormal,  TexAccent, Ink);
            Paint(s.toggle.onHover,   TexAccent, Ink);
            Paint(s.toggle.onActive,  TexAccent, Ink);
            s.toggle.border  = new RectOffset(2, 2, 2, 2);
            s.toggle.padding = new RectOffset(2, 2, 2, 2);

            // Sliders: panel2 track, accent thumb.
            Paint(s.horizontalSlider.normal,      TexPanel2, Fg);
            s.horizontalSlider.border       = new RectOffset(2, 2, 2, 2);
            s.horizontalSlider.fixedHeight  = 6f;
            Paint(s.horizontalSliderThumb.normal, TexAccent, Ink);
            Paint(s.horizontalSliderThumb.hover,  TexAccent, Ink);
            Paint(s.horizontalSliderThumb.active, TexAccent, Ink);
            s.horizontalSliderThumb.border      = new RectOffset(3, 3, 3, 3);
            s.horizontalSliderThumb.fixedHeight = 14f;
            s.horizontalSliderThumb.fixedWidth  = 14f;

            // Scrollbars.
            Paint(s.verticalScrollbar.normal,            TexInk,    Fg);
            Paint(s.verticalScrollbarThumb.normal,       TexPanel2, Fg);
            Paint(s.verticalScrollbarThumb.hover,        TexLine,   Fg);
            s.verticalScrollbarThumb.border = new RectOffset(3, 3, 3, 3);

            _skin = s;
            return _skin;
        }

        private static void Paint(GUIStyleState state, Texture2D bg, Color text)
        {
            state.background = bg;
            state.textColor  = text;
        }
    }
}
