using System;
using System.Drawing;
using System.Windows.Forms;

namespace WikiFunctions.Theming
{
    /// <summary>
    /// Draws the outline of text boxes, lists and drop-downs in dark mode. Windows draws these
    /// borders itself in light system colours, which shows as a bright white frame on a dark
    /// window. In light mode the native border is left alone.
    /// </summary>
    internal sealed class InputBorderPainter : NativeWindow
    {
        private readonly Control mControl;

        private InputBorderPainter(Control control)
        {
            mControl = control;
        }

        /// <summary>
        /// Hooks the control's window; also follows handle re-creation
        /// </summary>
        internal static void Attach(Control control)
        {
            var painter = new InputBorderPainter(control);
            control.HandleCreated += (sender, e) => painter.Hook();
            // the outline takes the accent colour while the control has focus
            control.GotFocus += (sender, e) => painter.Redraw();
            control.LostFocus += (sender, e) => painter.Redraw();
            if (control.IsHandleCreated)
                painter.Hook();
        }

        private void Hook()
        {
            // NativeWindow lets go of the handle itself when the window is destroyed
            if (Handle != mControl.Handle)
            {
                if (Handle != IntPtr.Zero)
                    ReleaseHandle();
                AssignHandle(mControl.Handle);
            }
        }

        internal void Redraw()
        {
            if (Theme.IsDark && mControl.IsHandleCreated)
                ThemeNative.RedrawFrame(mControl.Handle);
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);

            if (!Theme.IsDark)
                return;

            try
            {
                if (m.Msg == ThemeNative.WM_NCPAINT && !(mControl is ComboBox))
                    PaintNonClientBorder();
                else if (m.Msg == ThemeNative.WM_PAINT && mControl is ComboBox)
                    PaintComboBorder();
            }
            catch (Exception)
            {
                // never let a border break the control
            }
        }

        /// <summary>
        /// Text boxes and lists: the border lies outside the client area
        /// </summary>
        private void PaintNonClientBorder()
        {
            bool single = ThemeNative.HasStyle(Handle, ThemeNative.WS_BORDER);
            bool sunken = ThemeNative.HasExStyle(Handle, ThemeNative.WS_EX_CLIENTEDGE);
            if (!single && !sunken)
                return;

            DrawFrame(sunken ? 2 : 1);
        }

        /// <summary>
        /// Drop-downs draw their frame inside the client area; draw over its outer pixel
        /// </summary>
        private void PaintComboBorder()
        {
            DrawFrame(1);
        }

        private void DrawFrame(int thickness)
        {
            IntPtr dc = ThemeNative.GetWindowDC(Handle);
            if (dc == IntPtr.Zero)
                return;

            try
            {
                using (Graphics g = Graphics.FromHdc(dc))
                {
                    ThemePalette p = Theme.Current;
                    Color outer = mControl.Focused || mControl.ContainsFocus ? p.Accent : p.InputBorder;
                    var r = new Rectangle(0, 0, mControl.Width - 1, mControl.Height - 1);
                    using (var pen = new Pen(outer))
                        g.DrawRectangle(pen, r);

                    // a sunken (3D) border is two pixels: fill the inner one with the background
                    if (thickness > 1)
                        using (var pen = new Pen(mControl.BackColor.A == 255 ? mControl.BackColor : p.Input))
                            g.DrawRectangle(pen, Rectangle.Inflate(r, -1, -1));
                }
            }
            finally
            {
                ThemeNative.ReleaseDC(Handle, dc);
            }
        }
    }
}
