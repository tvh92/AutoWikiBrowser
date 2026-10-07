using System;
using System.Drawing;
using Microsoft.Win32;

namespace WikiFunctions.Theming
{
    /// <summary>
    /// Colours used by the AWB theme. One instance per light/dark variant.
    /// </summary>
    public sealed class ThemePalette
    {
        public bool IsDark { get; private set; }

        /// <summary>Form background, menus and status bar</summary>
        public Color Window { get; private set; }
        /// <summary>Tab pages and other raised areas</summary>
        public Color Surface { get; private set; }
        /// <summary>Drop-down menus and popups</summary>
        public Color Popup { get; private set; }
        /// <summary>Text boxes, lists and the edit box</summary>
        public Color Input { get; private set; }
        public Color Button { get; private set; }
        public Color Hover { get; private set; }
        public Color Pressed { get; private set; }
        public Color Text { get; private set; }
        public Color MutedText { get; private set; }
        public Color Border { get; private set; }
        public Color StrongBorder { get; private set; }
        public Color Accent { get; private set; }
        public Color AccentHover { get; private set; }
        public Color OnAccent { get; private set; }
        public Color Selection { get; private set; }
        public Color Link { get; private set; }
        public Color VisitedLink { get; private set; }

        public Color Success { get; private set; }
        public Color Danger { get; private set; }
        public Color Warning { get; private set; }
        /// <summary>Background for "found" text and other highlights</summary>
        public Color Highlight { get; private set; }
        /// <summary>Background for errors highlighted inside the edit box</summary>
        public Color ErrorHighlight { get; private set; }
        /// <summary>Background of pre-processed rows in the page list</summary>
        public Color Processed { get; private set; }

        // Wikitext syntax highlighting in the edit box
        public Color SyntaxBlock { get; private set; }
        public Color SyntaxTemplateName { get; private set; }
        public Color SyntaxLink { get; private set; }
        public Color SyntaxFile { get; private set; }
        public Color SyntaxInterwiki { get; private set; }
        public Color SyntaxComment { get; private set; }

        internal static ThemePalette CreateLight(Color accent)
        {
            return new ThemePalette
            {
                IsDark = false,
                Window = Color.FromArgb(243, 243, 243),
                Surface = Color.FromArgb(250, 250, 250),
                Popup = Color.FromArgb(249, 249, 249),
                Input = Color.White,
                Button = Color.FromArgb(253, 253, 253),
                Hover = Color.FromArgb(234, 234, 234),
                Pressed = Color.FromArgb(222, 222, 222),
                Text = Color.FromArgb(27, 27, 27),
                MutedText = Color.FromArgb(110, 110, 110),
                Border = Color.FromArgb(219, 219, 219),
                StrongBorder = Color.FromArgb(160, 160, 160),
                Accent = accent,
                AccentHover = Blend(accent, Color.White, 0.12),
                OnAccent = ReadableTextOn(accent),
                Selection = Blend(accent, Color.White, 0.80),
                Link = Color.FromArgb(0, 90, 158),
                VisitedLink = Color.FromArgb(104, 62, 168),
                Success = Color.FromArgb(15, 123, 15),
                Danger = Color.FromArgb(196, 43, 28),
                Warning = Color.FromArgb(157, 93, 0),
                Highlight = Color.FromArgb(255, 231, 128),
                ErrorHighlight = Color.FromArgb(255, 176, 160),
                Processed = Color.FromArgb(214, 245, 196),
                SyntaxBlock = Color.FromArgb(234, 236, 240),
                SyntaxTemplateName = Color.FromArgb(0, 51, 128),
                SyntaxLink = Color.FromArgb(0, 70, 200),
                SyntaxFile = Color.FromArgb(214, 245, 196),
                SyntaxInterwiki = Color.FromArgb(200, 200, 200),
                SyntaxComment = Color.FromArgb(245, 238, 190)
            };
        }

        internal static ThemePalette CreateDark(Color accent)
        {
            Color darkAccent = Blend(accent, Color.White, 0.35);
            return new ThemePalette
            {
                IsDark = true,
                Window = Color.FromArgb(32, 32, 32),
                Surface = Color.FromArgb(39, 39, 39),
                Popup = Color.FromArgb(44, 44, 44),
                Input = Color.FromArgb(28, 28, 28),
                Button = Color.FromArgb(52, 52, 52),
                Hover = Color.FromArgb(61, 61, 61),
                Pressed = Color.FromArgb(70, 70, 70),
                Text = Color.FromArgb(232, 232, 232),
                MutedText = Color.FromArgb(150, 150, 150),
                Border = Color.FromArgb(60, 60, 60),
                StrongBorder = Color.FromArgb(110, 110, 110),
                Accent = darkAccent,
                AccentHover = Blend(darkAccent, Color.White, 0.12),
                OnAccent = ReadableTextOn(darkAccent),
                Selection = Blend(accent, Color.FromArgb(28, 28, 28), 0.55),
                Link = Color.FromArgb(120, 180, 255),
                VisitedLink = Color.FromArgb(190, 150, 255),
                Success = Color.FromArgb(46, 140, 64),
                Danger = Color.FromArgb(200, 60, 50),
                Warning = Color.FromArgb(190, 120, 20),
                Highlight = Color.FromArgb(120, 98, 20),
                ErrorHighlight = Color.FromArgb(130, 45, 35),
                Processed = Color.FromArgb(38, 74, 38),
                SyntaxBlock = Color.FromArgb(44, 46, 52),
                SyntaxTemplateName = Color.FromArgb(150, 190, 255),
                SyntaxLink = Color.FromArgb(110, 175, 255),
                SyntaxFile = Color.FromArgb(34, 64, 40),
                SyntaxInterwiki = Color.FromArgb(70, 70, 76),
                SyntaxComment = Color.FromArgb(70, 64, 34)
            };
        }

        /// <summary>
        /// Windows accent colour, or a neutral blue when it cannot be read
        /// </summary>
        internal static Color ReadAccentColor()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\DWM"))
                {
                    if (key?.GetValue("AccentColor") is int abgr)
                    {
                        Color color = Color.FromArgb(abgr & 0xFF, (abgr >> 8) & 0xFF, (abgr >> 16) & 0xFF);
                        // very light or very dark accents make poor button fills
                        float brightness = color.GetBrightness();
                        if (brightness > 0.15f && brightness < 0.85f)
                            return color;
                    }
                }
            }
            catch (Exception)
            {
                // no access to the registry: fall through to the default
            }

            return Color.FromArgb(0, 103, 192);
        }

        /// <summary>
        /// Mixes <paramref name="amount"/> (0..1) of <paramref name="with"/> into <paramref name="color"/>
        /// </summary>
        public static Color Blend(Color color, Color with, double amount)
        {
            return Color.FromArgb(
                (int)Math.Round(color.R + (with.R - color.R) * amount),
                (int)Math.Round(color.G + (with.G - color.G) * amount),
                (int)Math.Round(color.B + (with.B - color.B) * amount));
        }

        /// <summary>
        /// Black or white, whichever reads better on the given background
        /// </summary>
        public static Color ReadableTextOn(Color background)
        {
            double luminance = (0.299 * background.R + 0.587 * background.G + 0.114 * background.B) / 255;
            return luminance > 0.6 ? Color.FromArgb(20, 20, 20) : Color.White;
        }
    }
}
