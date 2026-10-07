using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace WikiFunctions.Theming
{
    /// <summary>
    /// Paint handlers that draw buttons, check boxes, radio buttons and group boxes in the
    /// flat Windows 11-like style. Attached once per control; they read state at paint time.
    /// </summary>
    internal static class ButtonPainter
    {
        private const int Radius = 4;

        private static ThemePalette P => Theme.Current;

        private static Color DisabledBorder => ThemePalette.Blend(P.StrongBorder, P.Surface, 0.35);

        internal static void PaintButton(object sender, PaintEventArgs e)
        {
            if (!Theme.Enabled)
                return;

            var button = (ButtonBase)sender;
            Graphics g = e.Graphics;
            g.Clear(ControlStyler.EffectiveBackColor(button.Parent));

            bool hot = button.Enabled && button.ClientRectangle.Contains(button.PointToClient(Control.MousePosition));
            bool pressed = hot && (Control.MouseButtons & MouseButtons.Left) != 0;
            bool isDefault = button is IButtonControl control && button.FindForm()?.AcceptButton == control;
            bool isChecked = (button as CheckBox)?.Checked == true || (button as RadioButton)?.Checked == true;

            Color fill, border, text;
            if (!button.Enabled)
            {
                fill = P.Window;
                border = P.Border;
                text = P.MutedText;
            }
            else if (isDefault || isChecked)
            {
                fill = pressed ? P.Accent : hot ? P.AccentHover : P.Accent;
                border = fill;
                text = P.OnAccent;
            }
            else if (ControlStyler.IsCustom(button.BackColor))
            {
                // AWB uses a coloured button to signal state (e.g. Find highlighting a match)
                fill = button.BackColor;
                border = ThemePalette.Blend(fill, Color.Black, 0.2);
                text = ThemePalette.ReadableTextOn(fill);
            }
            else
            {
                fill = pressed ? P.Pressed : hot ? P.Hover : P.Button;
                border = P.Border;
                text = P.Text;
            }

            var r = new Rectangle(0, 0, button.Width - 1, button.Height - 1);
            ModernToolStripRenderer.FillRounded(g, r, fill, Radius);
            ModernToolStripRenderer.DrawRounded(g, r, border, Radius);

            if (button.Focused && ShowsFocusCues(button))
                ModernToolStripRenderer.DrawRounded(g, Rectangle.Inflate(r, -2, -2), isDefault ? P.OnAccent : P.Accent, Radius - 1);

            Rectangle content = Rectangle.Inflate(button.ClientRectangle, -3, -1);
            Image image = button.Image ?? (button.ImageList != null && button.ImageIndex >= 0 &&
                                           button.ImageIndex < button.ImageList.Images.Count
                ? button.ImageList.Images[button.ImageIndex]
                : null);

            if (image != null)
            {
                if (string.IsNullOrEmpty(button.Text))
                {
                    DrawImage(g, image, Center(content, image.Size), button.Enabled);
                    return;
                }

                // image on the left, text in the remaining space
                var imageBounds = new Rectangle(content.X, content.Y + (content.Height - image.Height) / 2,
                    image.Width, image.Height);
                DrawImage(g, image, imageBounds, button.Enabled);
                content = new Rectangle(imageBounds.Right + 2, content.Y,
                    Math.Max(0, content.Right - imageBounds.Right - 2), content.Height);
            }

            TextFormatFlags flags = TextFlags(button.TextAlign, button) | TextFormatFlags.WordBreak |
                                    TextFormatFlags.NoPadding;
            Font font = FittingFont(g, button.Text, button.Font, content.Size, flags);
            Size needed = TextRenderer.MeasureText(g, button.Text, font, new Size(content.Width, int.MaxValue), flags);
            if (needed.Height > font.Height + 2)
            {
                // several lines: VerticalCenter is ignored for multi-line text, so centre manually
                flags &= ~(TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                content = new Rectangle(content.X, content.Y + Math.Max(0, (content.Height - needed.Height) / 2),
                    content.Width, Math.Min(content.Height, needed.Height));
            }
            TextRenderer.DrawText(g, button.Text, font, content, text, flags);
        }

        private static readonly System.Collections.Generic.Dictionary<Font, Font> SmallerFonts =
            new System.Collections.Generic.Dictionary<Font, Font>();

        /// <summary>
        /// Segoe UI is wider than the MS Sans Serif buttons were laid out for; if a label no
        /// longer fits, use a slightly smaller size rather than cutting it off
        /// </summary>
        private static Font FittingFont(Graphics g, string text, Font font, Size area, TextFormatFlags flags)
        {
            Size needed = TextRenderer.MeasureText(g, text, font, new Size(area.Width, int.MaxValue), flags);
            if (needed.Height <= area.Height && needed.Width <= area.Width)
                return font;

            if (!SmallerFonts.TryGetValue(font, out Font smaller))
            {
                smaller = new Font(font.FontFamily, font.Size * 0.88f, font.Style, font.Unit);
                SmallerFonts[font] = smaller;
            }
            return smaller;
        }

        internal static void PaintCheckBox(object sender, PaintEventArgs e)
        {
            if (!Theme.Enabled)
                return;

            var checkBox = (CheckBox)sender;
            Rectangle glyph = PaintToggleFrame(checkBox, e.Graphics, out Rectangle textBounds);
            bool on = checkBox.CheckState != CheckState.Unchecked;

            if (on)
            {
                ModernToolStripRenderer.FillRounded(e.Graphics, glyph, checkBox.Enabled ? P.Accent : P.StrongBorder, 3);
                if (checkBox.CheckState == CheckState.Checked)
                    ModernToolStripRenderer.DrawCheckMark(e.Graphics, glyph, P.OnAccent);
                else
                    using (var brush = new SolidBrush(P.OnAccent))
                        e.Graphics.FillRectangle(brush, glyph.X + 3, glyph.Y + glyph.Height / 2 - 1, glyph.Width - 6, 2);
            }
            else
            {
                ModernToolStripRenderer.FillRounded(e.Graphics, glyph, P.Input, 3);
                ModernToolStripRenderer.DrawRounded(e.Graphics, glyph, checkBox.Enabled ? P.StrongBorder : DisabledBorder, 3);
            }

            PaintToggleText(checkBox, e.Graphics, textBounds);
        }

        internal static void PaintRadioButton(object sender, PaintEventArgs e)
        {
            if (!Theme.Enabled)
                return;

            var radio = (RadioButton)sender;
            Graphics g = e.Graphics;
            Rectangle glyph = PaintToggleFrame(radio, g, out Rectangle textBounds);

            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (radio.Checked)
            {
                using (var brush = new SolidBrush(radio.Enabled ? P.Accent : P.StrongBorder))
                    g.FillEllipse(brush, glyph);
                Rectangle dot = Rectangle.Inflate(glyph, -glyph.Width / 4 - 1, -glyph.Height / 4 - 1);
                using (var brush = new SolidBrush(P.OnAccent))
                    g.FillEllipse(brush, dot);
            }
            else
            {
                using (var brush = new SolidBrush(P.Input))
                    g.FillEllipse(brush, glyph);
                using (var pen = new Pen(radio.Enabled ? P.StrongBorder : DisabledBorder))
                    g.DrawEllipse(pen, glyph);
            }
            g.SmoothingMode = old;

            PaintToggleText(radio, g, textBounds);
        }

        /// <summary>
        /// Clears a check box / radio button and returns where its glyph and text go
        /// </summary>
        private static Rectangle PaintToggleFrame(ButtonBase control, Graphics g, out Rectangle textBounds)
        {
            g.Clear(ControlStyler.EffectiveBackColor(control.Parent));

            int size = Math.Min(14, Math.Max(10, control.Height - 4));
            bool rtl = IsRightToLeft(control);
            bool top = control is CheckBox cb ? IsTop(cb.CheckAlign) : IsTop(((RadioButton)control).CheckAlign);
            int y = top ? 2 : (control.Height - size) / 2;
            int x = rtl ? control.Width - size - 2 : 1;

            var glyph = new Rectangle(x, y, size - 1, size - 1);
            textBounds = rtl
                ? new Rectangle(0, 0, Math.Max(0, glyph.Left - 5), control.Height)
                : new Rectangle(glyph.Right + 5, 0, Math.Max(0, control.Width - glyph.Right - 5), control.Height);
            return glyph;
        }

        private static void PaintToggleText(ButtonBase control, Graphics g, Rectangle bounds)
        {
            TextFormatFlags flags = TextFlags(control.TextAlign, control);
            Size needed = TextRenderer.MeasureText(g, control.Text, control.Font, new Size(bounds.Width, int.MaxValue),
                flags | TextFormatFlags.WordBreak);
            if (needed.Height > control.Font.Height + 2)
            {
                // several lines: VerticalCenter is ignored for multi-line text, so centre manually
                flags = (flags & ~TextFormatFlags.VerticalCenter) | TextFormatFlags.WordBreak;
                bounds = new Rectangle(bounds.X, bounds.Y + Math.Max(0, (bounds.Height - needed.Height) / 2),
                    bounds.Width, Math.Min(bounds.Height, needed.Height));
            }

            Color text = control.Enabled
                ? (ControlStyler.IsCustom(control.ForeColor) ? control.ForeColor : P.Text)
                : P.MutedText;
            TextRenderer.DrawText(g, control.Text, control.Font, bounds, text, flags);

            if (control.Focused && ShowsFocusCues(control))
                ModernToolStripRenderer.DrawRounded(g, Rectangle.Inflate(bounds, 1, -1), P.StrongBorder, 2);
        }

        internal static void PaintGroupBox(object sender, PaintEventArgs e)
        {
            if (!Theme.Enabled)
                return;

            var box = (GroupBox)sender;
            Graphics g = e.Graphics;
            Color back = ControlStyler.EffectiveBackColor(box.Parent);
            g.Clear(back);

            Size title = string.IsNullOrEmpty(box.Text)
                ? Size.Empty
                : TextRenderer.MeasureText(g, box.Text, box.Font, Size.Empty, TextFormatFlags.HidePrefix);
            int top = title.Height / 2;
            var card = new Rectangle(0, top, box.Width - 1, box.Height - top - 1);
            ModernToolStripRenderer.FillRounded(g, card, box.BackColor.A == 255 ? box.BackColor : back, 5);
            ModernToolStripRenderer.DrawRounded(g, card, P.Border, 5);

            if (title.IsEmpty)
                return;

            bool rtl = IsRightToLeft(box);
            int x = rtl ? box.Width - 8 - title.Width : 8;
            var titleBounds = new Rectangle(x, 0, title.Width + 2, title.Height);
            using (var brush = new SolidBrush(box.BackColor.A == 255 ? box.BackColor : back))
                g.FillRectangle(brush, Rectangle.Inflate(titleBounds, 2, 0));
            TextRenderer.DrawText(g, box.Text, box.Font, titleBounds,
                box.Enabled ? P.Text : P.MutedText, TextFormatFlags.HidePrefix | TextFormatFlags.NoPadding |
                                                     (rtl ? TextFormatFlags.RightToLeft : 0));
        }

        /// <summary>
        /// Native disabled labels use an etched light grey that is unreadable on dark backgrounds
        /// </summary>
        internal static void PaintDisabledLabel(object sender, PaintEventArgs e)
        {
            var label = (Label)sender;
            if (label.Enabled || !Theme.IsDark || label is LinkLabel)
                return;

            e.Graphics.Clear(ControlStyler.EffectiveBackColor(label));
            TextRenderer.DrawText(e.Graphics, label.Text, label.Font, label.ClientRectangle, P.MutedText,
                TextFlags(label.TextAlign, label) | (label.AutoSize ? 0 : TextFormatFlags.WordBreak));
        }

        private static void DrawImage(Graphics g, Image image, Rectangle bounds, bool enabled)
        {
            if (enabled)
                g.DrawImage(image, bounds);
            else
                ControlPaint.DrawImageDisabled(g, image, bounds.X, bounds.Y, Color.Transparent);
        }

        private static Rectangle Center(Rectangle area, Size size)
        {
            return new Rectangle(area.X + (area.Width - size.Width) / 2, area.Y + (area.Height - size.Height) / 2,
                size.Width, size.Height);
        }

        private static readonly System.Reflection.PropertyInfo ShowFocusCuesProperty =
            typeof(Control).GetProperty("ShowFocusCues", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        /// <summary>
        /// Focus rectangles are hidden until the keyboard is used, as in native controls
        /// </summary>
        private static bool ShowsFocusCues(Control control)
        {
            return ShowFocusCuesProperty == null || (bool)ShowFocusCuesProperty.GetValue(control, null);
        }

        private static bool IsTop(ContentAlignment align)
        {
            return align == ContentAlignment.TopLeft || align == ContentAlignment.TopCenter ||
                   align == ContentAlignment.TopRight;
        }

        private static bool IsRightToLeft(Control control)
        {
            return control.RightToLeft == RightToLeft.Yes;
        }

        internal static TextFormatFlags TextFlags(ContentAlignment align, Control control)
        {
            TextFormatFlags flags = TextFormatFlags.EndEllipsis | TextFormatFlags.HidePrefix;

            switch (align)
            {
                case ContentAlignment.TopLeft:
                case ContentAlignment.TopCenter:
                case ContentAlignment.TopRight:
                    flags |= TextFormatFlags.Top;
                    break;
                case ContentAlignment.BottomLeft:
                case ContentAlignment.BottomCenter:
                case ContentAlignment.BottomRight:
                    flags |= TextFormatFlags.Bottom;
                    break;
                default:
                    flags |= TextFormatFlags.VerticalCenter;
                    break;
            }

            switch (align)
            {
                case ContentAlignment.TopCenter:
                case ContentAlignment.MiddleCenter:
                case ContentAlignment.BottomCenter:
                    flags |= TextFormatFlags.HorizontalCenter;
                    break;
                case ContentAlignment.TopRight:
                case ContentAlignment.MiddleRight:
                case ContentAlignment.BottomRight:
                    flags |= TextFormatFlags.Right;
                    break;
            }

            if (IsRightToLeft(control))
                flags |= TextFormatFlags.RightToLeft;
            return flags;
        }
    }
}
