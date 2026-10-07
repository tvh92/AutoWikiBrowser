using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace WikiFunctions.Theming
{
    /// <summary>
    /// Flat menu, toolbar and status bar renderer. Colours come from <see cref="Theme.Current"/>
    /// at paint time, so switching theme only needs a repaint.
    /// </summary>
    internal sealed class ModernToolStripRenderer : ToolStripRenderer
    {
        private static readonly ConditionalWeakTable<ToolStrip, object> RoundedDropDowns =
            new ConditionalWeakTable<ToolStrip, object>();

        private static ThemePalette P => Theme.Current;

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            if (e.ToolStrip is ToolStripDropDown)
            {
                e.Graphics.Clear(P.Popup);
                RoundDropDownCorners(e.ToolStrip);
            }
            else
                e.Graphics.Clear(P.Window);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            if (!(e.ToolStrip is ToolStripDropDown))
                return;

            Rectangle r = e.AffectedBounds;
            using (var pen = new Pen(P.Border))
                e.Graphics.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
        {
            // no separate image column: the whole menu is one flat surface
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            ToolStripItem item = e.Item;
            bool open = item is ToolStripMenuItem menuItem && menuItem.DropDown.Visible && item.IsOnDropDown == false;
            if (!item.Enabled || (!item.Selected && !open))
                return;

            Rectangle r = item.IsOnDropDown
                ? new Rectangle(4, 1, item.Width - 8, item.Height - 2)
                : new Rectangle(1, 1, item.Width - 2, item.Height - 2);
            FillRounded(e.Graphics, r, open ? P.Pressed : P.Hover, 4);
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            RenderButtonLike(e.Graphics, e.Item, e.Item is ToolStripButton button && button.Checked);
        }

        protected override void OnRenderDropDownButtonBackground(ToolStripItemRenderEventArgs e)
        {
            RenderButtonLike(e.Graphics, e.Item, false);
        }

        protected override void OnRenderSplitButtonBackground(ToolStripItemRenderEventArgs e)
        {
            RenderButtonLike(e.Graphics, e.Item, false);
            var split = (ToolStripSplitButton)e.Item;
            DrawArrow(new ToolStripArrowRenderEventArgs(e.Graphics, split, split.DropDownButtonBounds,
                P.Text, ArrowDirection.Down));
        }

        private static void RenderButtonLike(Graphics g, ToolStripItem item, bool isChecked)
        {
            var r = new Rectangle(1, 1, item.Width - 2, item.Height - 2);
            if (item.Pressed)
                FillRounded(g, r, P.Pressed, 4);
            else if (item.Selected && item.Enabled)
                FillRounded(g, r, P.Hover, 4);
            else if (isChecked)
                FillRounded(g, r, P.Selection, 4);
        }

        protected override void OnRenderLabelBackground(ToolStripItemRenderEventArgs e)
        {
            // Status labels that AWB colours (logged-in user, notifications, bot timer) become pills
            Color back = e.Item.BackColor;
            if (!(e.Item.Owner is StatusStrip) || !IsCustomColor(back))
                return;

            var r = new Rectangle(1, 2, e.Item.Width - 2, e.Item.Height - 4);
            FillRounded(e.Graphics, r, back, r.Height / 2);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            ToolStripItem item = e.Item;
            if (!item.Enabled)
                e.TextColor = P.MutedText;
            else if (item.Owner is StatusStrip && IsCustomColor(item.BackColor))
                e.TextColor = ThemePalette.ReadableTextOn(item.BackColor);
            else if (!IsCustomColor(item.ForeColor))
                e.TextColor = P.Text;

            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Rectangle r = e.ImageRectangle;
            r.Inflate(1, 1);
            FillRounded(e.Graphics, r, P.Accent, 3);

            if (e.Item.Image == null)
                DrawCheckMark(e.Graphics, r, P.OnAccent);
            else
                e.Graphics.DrawImage(e.Item.Image, e.ImageRectangle);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = e.Item != null && !e.Item.Enabled ? P.MutedText : P.Text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var pen = new Pen(P.Border))
            {
                if (e.Vertical)
                {
                    int x = e.Item.Width / 2;
                    e.Graphics.DrawLine(pen, x, 4, x, e.Item.Height - 4);
                }
                else
                {
                    int y = e.Item.Height / 2;
                    e.Graphics.DrawLine(pen, 8, y, e.Item.Width - 8, y);
                }
            }
        }

        protected override void OnRenderGrip(ToolStripGripRenderEventArgs e)
        {
            // toolbars are not movable in AWB: no grip
        }

        protected override void OnRenderStatusStripSizingGrip(ToolStripRenderEventArgs e)
        {
            // Windows 10/11 windows have no visible sizing grip
        }

        internal static void DrawCheckMark(Graphics g, Rectangle r, Color color)
        {
            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                float x = r.X, y = r.Y, w = r.Width, h = r.Height;
                g.DrawLines(pen, new[]
                {
                    new PointF(x + w * 0.25f, y + h * 0.52f),
                    new PointF(x + w * 0.43f, y + h * 0.70f),
                    new PointF(x + w * 0.76f, y + h * 0.32f)
                });
            }
            g.SmoothingMode = old;
        }

        internal static void FillRounded(Graphics g, Rectangle r, Color color, int radius)
        {
            if (r.Width <= 0 || r.Height <= 0)
                return;

            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedRect(r, radius))
            using (var brush = new SolidBrush(color))
                g.FillPath(brush, path);
            g.SmoothingMode = old;
        }

        internal static void DrawRounded(Graphics g, Rectangle r, Color color, int radius)
        {
            if (r.Width <= 0 || r.Height <= 0)
                return;

            SmoothingMode old = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = RoundedRect(r, radius))
            using (var pen = new Pen(color))
                g.DrawPath(pen, path);
            g.SmoothingMode = old;
        }

        internal static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = System.Math.Min(radius * 2, System.Math.Min(r.Width, r.Height));
            if (d <= 1)
            {
                path.AddRectangle(r);
                return path;
            }

            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        /// <summary>
        /// True for colours AWB sets deliberately (status colours), false for system and theme defaults
        /// </summary>
        internal static bool IsCustomColor(Color color)
        {
            return !color.IsEmpty && !color.IsSystemColor && color.A == 255 && !Theme.IsPaletteColor(color);
        }

        private static void RoundDropDownCorners(ToolStrip dropDown)
        {
            if (!dropDown.IsHandleCreated || RoundedDropDowns.TryGetValue(dropDown, out _))
                return;

            RoundedDropDowns.Add(dropDown, new object());
            ThemeNative.SetRoundedCorners(dropDown.Handle, small: true);
        }
    }
}
