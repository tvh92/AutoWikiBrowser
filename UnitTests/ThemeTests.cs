using System;
using System.Drawing;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using WikiFunctions.Theming;

namespace UnitTests
{
    /// <summary>
    /// Tests the non-UI parts of the theme engine: colour maths and palette contrast
    /// </summary>
    [TestFixture]
    public class ThemeTests
    {
        private static readonly Color Accent = ThemePalette.DefaultAccent;

        /// <summary>WCAG 2.x contrast ratio, 1..21</summary>
        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }

        private static double Luminance(Color c)
        {
            return 0.2126 * Linear(c.R) + 0.7152 * Linear(c.G) + 0.0722 * Linear(c.B);
        }

        private static double Linear(int channel)
        {
            double v = channel / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        private static void AssertContrast(Color foreground, Color background, double minimum, string what)
        {
            double ratio = Contrast(foreground, background);
            Assert.That(ratio, Is.GreaterThanOrEqualTo(minimum), what + ": contrast " + ratio.ToString("0.00"));
        }

        [Test]
        public void BlendEndpoints()
        {
            Color c = Color.FromArgb(10, 100, 200);
            Color w = Color.FromArgb(250, 50, 0);

            ClassicAssert.AreEqual(c.ToArgb(), ThemePalette.Blend(c, w, 0).ToArgb());
            ClassicAssert.AreEqual(w.ToArgb(), ThemePalette.Blend(c, w, 1).ToArgb());
        }

        [Test]
        public void BlendMidpoint()
        {
            Color mid = ThemePalette.Blend(Color.Black, Color.White, 0.5);
            // 127.5 rounds (to even) to 128
            ClassicAssert.AreEqual(Color.FromArgb(128, 128, 128).ToArgb(), mid.ToArgb());

            mid = ThemePalette.Blend(Color.FromArgb(0, 100, 200), Color.FromArgb(100, 200, 0), 0.5);
            ClassicAssert.AreEqual(Color.FromArgb(50, 150, 100).ToArgb(), mid.ToArgb());
        }

        [Test]
        public void BlendIsOpaque()
        {
            Color translucent = Color.FromArgb(10, 20, 30, 40);
            ClassicAssert.AreEqual(255, ThemePalette.Blend(translucent, Color.White, 0.3).A);
            ClassicAssert.AreEqual(255, ThemePalette.Blend(Color.Black, Color.White, 0).A);
            ClassicAssert.AreEqual(255, ThemePalette.Blend(Color.Black, Color.White, 1).A);
        }

        [Test]
        public void ReadableTextOnLightBackgrounds()
        {
            foreach (Color bg in new[] { Color.White, Color.Yellow, Color.FromArgb(243, 243, 243) })
            {
                Color text = ThemePalette.ReadableTextOn(bg);
                ClassicAssert.Less(Luminance(text), 0.1, "dark text expected on " + bg);
                AssertContrast(text, bg, 4.5, "text on " + bg);
            }
        }

        [Test]
        public void ReadableTextOnDarkBackgrounds()
        {
            foreach (Color bg in new[] { Color.Black, Color.DarkBlue, Color.FromArgb(32, 32, 32) })
            {
                ClassicAssert.AreEqual(Color.White.ToArgb(), ThemePalette.ReadableTextOn(bg).ToArgb(), "white text expected on " + bg);
            }
        }

        [Test]
        public void IsUsableAccent()
        {
            ClassicAssert.IsTrue(ThemePalette.IsUsableAccent(ThemePalette.DefaultAccent));
            ClassicAssert.IsTrue(ThemePalette.IsUsableAccent(Color.FromArgb(0, 120, 215)));
            ClassicAssert.IsTrue(ThemePalette.IsUsableAccent(Color.FromArgb(232, 17, 35)));

            ClassicAssert.IsFalse(ThemePalette.IsUsableAccent(Color.FromArgb(128, 128, 128)), "grey");
            ClassicAssert.IsFalse(ThemePalette.IsUsableAccent(Color.FromArgb(118, 118, 118)), "grey");
            ClassicAssert.IsFalse(ThemePalette.IsUsableAccent(Color.FromArgb(250, 250, 255)), "near white");
            ClassicAssert.IsFalse(ThemePalette.IsUsableAccent(Color.White), "white");
            ClassicAssert.IsFalse(ThemePalette.IsUsableAccent(Color.FromArgb(5, 5, 15)), "near black");
            ClassicAssert.IsFalse(ThemePalette.IsUsableAccent(Color.Black), "black");
        }

        [Test]
        public void PaletteDarkFlags()
        {
            ClassicAssert.IsFalse(ThemePalette.CreateLight(Accent).IsDark);
            ClassicAssert.IsTrue(ThemePalette.CreateDark(Accent).IsDark);
            ClassicAssert.IsFalse(ThemePalette.CreateClassic(Accent).IsDark);
        }

        [Test]
        public void LightPaletteTextContrast()
        {
            AssertTextContrast(ThemePalette.CreateLight(Accent), 4.5);
        }

        [Test]
        public void DarkPaletteTextContrast()
        {
            AssertTextContrast(ThemePalette.CreateDark(Accent), 4.5);
        }

        private static void AssertTextContrast(ThemePalette p, double onAccentMinimum)
        {
            AssertContrast(p.Text, p.Window, 4.5, "Text on Window");
            AssertContrast(p.Text, p.Surface, 4.5, "Text on Surface");
            AssertContrast(p.Text, p.Input, 4.5, "Text on Input");
            AssertContrast(p.Text, p.Button, 4.5, "Text on Button");
            AssertContrast(p.OnAccent, p.Accent, onAccentMinimum, "OnAccent on Accent");
        }

        [Test]
        public void DarkPaletteSecondaryContrast()
        {
            ThemePalette p = ThemePalette.CreateDark(Accent);
            // disabled text boxes draw MutedText on Input
            AssertContrast(p.MutedText, p.Input, 3, "MutedText on Input");
            AssertContrast(p.InputBorder, p.Input, 1.5, "InputBorder on Input");
            ClassicAssert.AreNotEqual(p.Input.ToArgb(), p.InputBorder.ToArgb());
            AssertContrast(p.Link, p.Input, 4.5, "Link on Input");
        }

        [Test]
        public void ClassicPaletteKeepsOriginalColours()
        {
            ThemePalette p = ThemePalette.CreateClassic(Accent);

            ClassicAssert.AreEqual(Color.Red.ToArgb(), p.Danger.ToArgb());
            ClassicAssert.AreEqual(Color.Green.ToArgb(), p.Success.ToArgb());
            ClassicAssert.AreEqual(Color.Tomato.ToArgb(), p.Warning.ToArgb());
            ClassicAssert.AreEqual(Color.Yellow.ToArgb(), p.Highlight.ToArgb());
            ClassicAssert.AreEqual(Color.Tomato.ToArgb(), p.ErrorHighlight.ToArgb());
            ClassicAssert.AreEqual(Color.GreenYellow.ToArgb(), p.Processed.ToArgb());
            ClassicAssert.AreEqual(Color.Blue.ToArgb(), p.SyntaxLink.ToArgb());
            ClassicAssert.AreEqual(Color.PaleGoldenrod.ToArgb(), p.SyntaxComment.ToArgb());
            ClassicAssert.AreEqual(Color.White.ToArgb(), p.Input.ToArgb());
            ClassicAssert.AreEqual(Color.Black.ToArgb(), p.Text.ToArgb());
        }

        [Test]
        public void ThemeBeforeInitialize()
        {
            // the theme is never initialised in unit tests
            ClassicAssert.IsFalse(Theme.Enabled);
            ClassicAssert.IsFalse(Theme.IsDark);
            ClassicAssert.AreEqual("", Theme.HtmlPageStyles);
            ClassicAssert.AreEqual(Color.Red.ToArgb(), Theme.Current.Danger.ToArgb());
        }
    }
}
