using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace WikiFunctions.Theming
{
    /// <summary>
    /// Applies <see cref="Theme.Current"/> to WinForms controls. Styling a control is idempotent;
    /// event hooks and owner-draw handlers are attached only on the first pass, and painting reads
    /// the palette at paint time so a theme switch only needs one restyle pass.
    /// </summary>
    internal static class ControlStyler
    {
        private static readonly ConditionalWeakTable<Control, object> Hooked =
            new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<Control, object> OriginalBorder =
            new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<TextBoxBase, Label> DisabledTextOverlays =
            new ConditionalWeakTable<TextBoxBase, Label>();
        private static readonly ConditionalWeakTable<TabControl, StrongBox<int>> HoveredTab =
            new ConditionalWeakTable<TabControl, StrongBox<int>>();
        private static readonly object OverlayTag = new object();
        private static readonly Dictionary<string, Font> UiFonts = new Dictionary<string, Font>();
        private static readonly MethodInfo SetStyleMethod =
            typeof(Control).GetMethod("SetStyle", BindingFlags.Instance | BindingFlags.NonPublic);
        private static ModernToolStripRenderer mRenderer;

        private static ThemePalette P => Theme.Current;
        private static ModernToolStripRenderer Renderer => mRenderer ?? (mRenderer = new ModernToolStripRenderer());

        internal static bool IsStyled(Control control)
        {
            return Hooked.TryGetValue(control, out _);
        }

        /// <param name="force">Restyle even if already styled (after a theme or text size change)</param>
        internal static void Style(Control root, bool force)
        {
            if (root.IsDisposed || (!force && IsStyled(root)))
                return;

            var form = root as Form;
            SizeF oldDims = SizeF.Empty;
            Size oldSize = Size.Empty;
            Point oldCenter = Point.Empty;
            List<SplitterState> splitters = null;

            if (form != null)
            {
                PrepareAutoScale(form);
                oldDims = form.CurrentAutoScaleDimensions;
                oldSize = form.Size;
                oldCenter = new Point(form.Left + form.Width / 2, form.Top + form.Height / 2);
                splitters = new List<SplitterState>();
                CollectSplitters(form, splitters);

                // The form's font must change while layout is NOT suspended: WinForms skips
                // font-based autoscaling of a suspended form
                StyleFont(form);
            }

            root.SuspendLayout();
            try
            {
                StyleTree(root);
            }
            finally
            {
                root.ResumeLayout(true);
            }

            if (form != null)
                FinishAutoScale(form, oldDims, oldSize, oldCenter, splitters);

            root.Invalidate(true);
        }

        private static void StyleTree(Control control)
        {
            if (control.IsDisposed || ReferenceEquals(control.Tag, OverlayTag))
                return;

            bool firstPass = !Hooked.TryGetValue(control, out _);
            if (firstPass)
            {
                Hooked.Add(control, new object());
                control.ControlAdded += (sender, e) =>
                {
                    if (Theme.Enabled)
                        SafeStyleTree(e.Control);
                };
            }

            try
            {
                StyleFont(control);
                StyleOne(control, firstPass);
            }
            catch (Exception)
            {
                // a control we cannot style keeps its default look
            }

            if (control.ContextMenuStrip != null)
                StyleToolStrip(control.ContextMenuStrip, !IsStyled(control.ContextMenuStrip));

            foreach (Control child in control.Controls)
                StyleTree(child);
        }

        private static void SafeStyleTree(Control control)
        {
            try
            {
                StyleTree(control);
            }
            catch (Exception)
            {
            }
        }

        #region Text size

        private static readonly ConditionalWeakTable<Control, Font> BaseFonts =
            new ConditionalWeakTable<Control, Font>();
        private static readonly ConditionalWeakTable<Form, StrongBox<SizeF>> LayoutScales =
            new ConditionalWeakTable<Form, StrongBox<SizeF>>();
        private static readonly ConditionalWeakTable<Form, object> KeepSizeForms =
            new ConditionalWeakTable<Form, object>();
        private static readonly ConditionalWeakTable<ToolStrip, object> BaseImageSizes =
            new ConditionalWeakTable<ToolStrip, object>();

        private sealed class SplitterState
        {
            internal SplitContainer Split;
            internal int Panel1;
            internal int Panel2;
            internal int Min1;
            internal int Min2;
        }

        internal static void KeepSize(Form form)
        {
            if (!KeepSizeForms.TryGetValue(form, out _))
                KeepSizeForms.Add(form, new object());
        }

        internal static SizeF LayoutScale(Control control)
        {
            Form form = control as Form ?? control?.FindForm();
            return form != null && LayoutScales.TryGetValue(form, out StrongBox<SizeF> scale)
                ? scale.Value
                : new SizeF(1, 1);
        }

        /// <summary>
        /// Lets WinForms scale the window's layout from its font: changing the font then resizes
        /// and moves every control in proportion, as on a high-DPI screen. Nested containers are
        /// set to inherit so they are scaled once, by the form, rather than twice.
        /// </summary>
        private static void PrepareAutoScale(Form form)
        {
            SetChildContainersInherit(form);
            if (form.AutoScaleMode != AutoScaleMode.Font)
                form.AutoScaleMode = AutoScaleMode.Font;
            form.AutoScaleDimensions = form.CurrentAutoScaleDimensions;
        }

        private static void SetChildContainersInherit(Control parent)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is ContainerControl container && !(child is Form) &&
                    container.AutoScaleMode != AutoScaleMode.Inherit)
                    container.AutoScaleMode = AutoScaleMode.Inherit;
                SetChildContainersInherit(child);
            }
        }

        private static void CollectSplitters(Control parent, List<SplitterState> splitters)
        {
            foreach (Control child in parent.Controls)
            {
                if (child is SplitContainer split)
                {
                    splitters.Add(new SplitterState
                    {
                        Split = split,
                        Panel1 = split.SplitterDistance,
                        Panel2 = Length(split) - split.SplitterDistance - split.SplitterWidth,
                        Min1 = split.Panel1MinSize,
                        Min2 = split.Panel2MinSize
                    });
                }
                CollectSplitters(child, splitters);
            }
        }

        private static int Length(SplitContainer split)
        {
            return split.Orientation == Orientation.Vertical ? split.Width : split.Height;
        }

        private static void FinishAutoScale(Form form, SizeF oldDims, Size oldSize, Point oldCenter,
            List<SplitterState> splitters)
        {
            SizeF newDims = form.CurrentAutoScaleDimensions;
            if (oldDims.Width <= 0 || oldDims.Height <= 0 || newDims == oldDims)
            {
                if (!LayoutScales.TryGetValue(form, out _))
                    LayoutScales.Add(form, new StrongBox<SizeF>(new SizeF(1, 1)));
                return;
            }

            var ratio = new SizeF(newDims.Width / oldDims.Width, newDims.Height / oldDims.Height);
            if (LayoutScales.TryGetValue(form, out StrongBox<SizeF> total))
                total.Value = new SizeF(total.Value.Width * ratio.Width, total.Value.Height * ratio.Height);
            else
                LayoutScales.Add(form, new StrongBox<SizeF>(ratio));

            // SplitContainers keep their splitter position in pixels; scale it like everything else
            foreach (SplitterState state in splitters)
            {
                SplitContainer split = state.Split;
                float r = split.Orientation == Orientation.Vertical ? ratio.Width : ratio.Height;
                try
                {
                    split.Panel1MinSize = (int)(state.Min1 * r);
                    split.Panel2MinSize = (int)(state.Min2 * r);
                    int distance = split.FixedPanel == FixedPanel.Panel2
                        ? Length(split) - split.SplitterWidth - (int)(state.Panel2 * r)
                        : (int)(state.Panel1 * r);
                    if (distance > 0 && distance < Length(split))
                        split.SplitterDistance = distance;
                }
                catch (Exception)
                {
                    // panel too small for the scaled minimum sizes: keep the current position
                }
            }

            if (KeepSizeForms.TryGetValue(form, out _))
            {
                if (form.WindowState == FormWindowState.Normal)
                    form.Size = oldSize;
                return;
            }

            if (form.Visible && form.WindowState == FormWindowState.Normal && form.Size != oldSize)
            {
                // grow around the old centre, staying on screen
                Rectangle area = Screen.FromPoint(oldCenter).WorkingArea;
                int width = Math.Min(form.Width, area.Width);
                int height = Math.Min(form.Height, area.Height);
                int x = Math.Max(area.Left, Math.Min(oldCenter.X - width / 2, area.Right - width));
                int y = Math.Max(area.Top, Math.Min(oldCenter.Y - height / 2, area.Bottom - height));
                form.Bounds = new Rectangle(x, y, width, height);
            }
        }

        /// <summary>
        /// Gives the control Segoe UI (instead of MS Sans Serif) at the chosen text size.
        /// Only controls with their own font are changed; the rest inherit from their parent.
        /// </summary>
        private static void StyleFont(Control control)
        {
            if (!BaseFonts.TryGetValue(control, out Font baseFont))
            {
                bool ownFont = control is Form || control is ToolStrip || control.Parent == null ||
                               !ReferenceEquals(control.Font, control.Parent.Font);
                if (!ownFont)
                    return;

                baseFont = control.Font;
                BaseFonts.Add(control, baseFont);
            }

            Font target = ScaledFont(baseFont);
            if (!control.Font.Equals(target))
                control.Font = target;
        }

        private static Font ScaledFont(Font baseFont)
        {
            bool replaceFamily = baseFont.Name == "Microsoft Sans Serif" && Theme.UiFont.Name != baseFont.Name;
            float size = (float)Math.Round(baseFont.Size * Theme.Scale * 4) / 4;
            if (!replaceFamily && Math.Abs(size - baseFont.Size) < 0.01f)
                return baseFont;

            string family = replaceFamily ? Theme.UiFont.Name : baseFont.Name;
            string key = family + "|" + size + "|" + (int)baseFont.Style + "|" + (int)baseFont.Unit;
            if (!UiFonts.TryGetValue(key, out Font font))
            {
                font = replaceFamily
                    ? new Font(Theme.UiFont.FontFamily, size, baseFont.Style, baseFont.Unit)
                    : new Font(baseFont.FontFamily, size, baseFont.Style, baseFont.Unit);
                UiFonts[key] = font;
            }
            return font;
        }

        /// <summary>
        /// Toolbar icons grow with the text size (16 px at 100 %)
        /// </summary>
        private static void StyleToolStripImages(ToolStrip toolStrip)
        {
            if (!BaseImageSizes.TryGetValue(toolStrip, out object boxed))
            {
                boxed = toolStrip.ImageScalingSize;
                BaseImageSizes.Add(toolStrip, boxed);
            }

            var baseSize = (Size)boxed;
            var target = new Size((int)Math.Round(baseSize.Width * Theme.Scale),
                (int)Math.Round(baseSize.Height * Theme.Scale));
            if (toolStrip.ImageScalingSize != target)
                toolStrip.ImageScalingSize = target;
        }

        #endregion

        private static void StyleOne(Control control, bool firstPass)
        {
            bool dark = P.IsDark;
            SetForeColor(control, P.Text);

            switch (control)
            {
                case Form form:
                    form.BackColor = P.Window;
                    WhenHandleReady(form, firstPass, () => ThemeNative.SetDarkTitleBar(form.Handle, P.IsDark));
                    break;

                case TextBoxBase textBox:
                    textBox.BackColor = P.Input;
                    SetDarkBorder(textBox, dark);
                    WhenHandleReady(textBox, firstPass, () => ApplyNativeTheme(textBox));
                    if (textBox is RichTextBox && textBox.IsHandleCreated)
                        ThemeNative.SendMessage(textBox.Handle, ThemeNative.EM_SETBKGNDCOLOR, IntPtr.Zero,
                            (IntPtr)ColorTranslator.ToWin32(P.Input));
                    if (firstPass)
                        QueueDisabledTextOverlay(textBox);
                    UpdateDisabledTextOverlay(textBox);
                    break;

                case ListBox listBox:
                    listBox.BackColor = P.Input;
                    SetDarkBorder(listBox, dark);
                    WhenHandleReady(listBox, firstPass, () => ApplyNativeTheme(listBox));
                    break;

                case TreeView treeView:
                    treeView.BackColor = P.Input;
                    treeView.LineColor = P.StrongBorder;
                    SetDarkBorder(treeView, dark);
                    WhenHandleReady(treeView, firstPass, () => ApplyNativeTheme(treeView));
                    break;

                case ListView listView:
                    listView.BackColor = P.Input;
                    SetDarkBorder(listView, dark);
                    WhenHandleReady(listView, firstPass, () => ApplyListViewTheme(listView));
                    break;

                case DataGridView grid:
                    StyleGrid(grid);
                    WhenHandleReady(grid, firstPass, () => ApplyNativeTheme(grid));
                    break;

                case ComboBox comboBox:
                    comboBox.BackColor = P.Input;
                    if (comboBox.FlatStyle == FlatStyle.Flat || comboBox.FlatStyle == FlatStyle.Popup)
                        comboBox.FlatStyle = FlatStyle.Standard;
                    if (firstPass && comboBox.DrawMode == DrawMode.Normal &&
                        comboBox.DropDownStyle == ComboBoxStyle.DropDownList)
                    {
                        comboBox.DrawMode = DrawMode.OwnerDrawFixed;
                        comboBox.DrawItem += DrawComboBoxItem;
                    }
                    WhenHandleReady(comboBox, firstPass, () =>
                        ThemeNative.SetTheme(comboBox.Handle, P.IsDark ? "DarkMode_CFD" : null));
                    break;

                case NumericUpDown numeric:
                    numeric.BackColor = P.Input;
                    SetDarkBorder(numeric, dark);
                    WhenHandleReady(numeric, firstPass, () =>
                    {
                        ApplyNativeTheme(numeric);
                        foreach (Control part in numeric.Controls)
                            ApplyNativeTheme(part);
                    });
                    return; // the inner edit/buttons are handled above

                case ProgressBar progressBar:
                    WhenHandleReady(progressBar, firstPass, () => ApplyProgressBarTheme(progressBar));
                    break;

                case ButtonBase button when button is Button ||
                                            (button is CheckBox cb && cb.Appearance == Appearance.Button) ||
                                            (button is RadioButton rb && rb.Appearance == Appearance.Button):
                    if (!IsCustom(button.BackColor))
                        button.BackColor = P.Button;
                    button.UseVisualStyleBackColor = false;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderSize = 0;
                    button.FlatAppearance.MouseOverBackColor = P.Hover;
                    button.FlatAppearance.MouseDownBackColor = P.Pressed;
                    if (firstPass)
                        button.Paint += ButtonPainter.PaintButton;
                    break;

                case CheckBox checkBox:
                    MatchParentBack(checkBox);
                    if (firstPass)
                        checkBox.Paint += ButtonPainter.PaintCheckBox;
                    break;

                case RadioButton radioButton:
                    MatchParentBack(radioButton);
                    if (firstPass)
                        radioButton.Paint += ButtonPainter.PaintRadioButton;
                    break;

                case ToolStrip toolStrip:
                    StyleToolStrip(toolStrip, firstPass);
                    break;

                case TabControl tabs:
                    StyleTabControl(tabs, firstPass);
                    break;

                case TabPage page:
                    page.UseVisualStyleBackColor = false;
                    if (!IsCustom(page.BackColor))
                        page.BackColor = P.Surface;
                    break;

                case GroupBox groupBox:
                    MatchParentBack(groupBox);
                    groupBox.FlatStyle = FlatStyle.Standard;
                    if (firstPass)
                        groupBox.Paint += ButtonPainter.PaintGroupBox;
                    break;

                case WebBrowser browser:
                    if (firstPass)
                        browser.DocumentCompleted += (sender, e) => StyleBrowserDocument(browser);
                    StyleBrowserDocument(browser);
                    break;

                case Label label:
                    if (!IsCustom(label.BackColor))
                        label.BackColor = Color.Transparent;
                    if (label is LinkLabel link)
                    {
                        link.LinkColor = P.Link;
                        link.ActiveLinkColor = P.AccentHover;
                        link.VisitedLinkColor = P.VisitedLink;
                        link.DisabledLinkColor = P.MutedText;
                    }
                    if (firstPass)
                        label.Paint += ButtonPainter.PaintDisabledLabel;
                    break;

                case PropertyGrid grid:
                    grid.ViewBackColor = P.Input;
                    grid.ViewForeColor = P.Text;
                    grid.LineColor = P.Surface;
                    grid.HelpBackColor = P.Surface;
                    grid.HelpForeColor = P.Text;
                    grid.CategoryForeColor = P.Text;
                    break;

                case SplitContainer _:
                case Panel _:
                case UserControl _:
                    MatchParentBack(control);
                    break;

                default:
                    if (!IsCustom(control.BackColor) && !(control is PictureBox))
                        MatchParentBack(control);
                    break;
            }
        }

        private static void SetForeColor(Control control, Color color)
        {
            if (!IsCustom(control.ForeColor))
                control.ForeColor = color;
        }

        /// <summary>
        /// Gives a container the background of its parent, so the form/tab-page colour flows down
        /// </summary>
        private static void MatchParentBack(Control control)
        {
            if (IsCustom(control.BackColor) && control.BackColor.A == 255)
                return;

            Color back = EffectiveBackColor(control.Parent);
            if (control is CheckBox || control is RadioButton || control is Label)
                back = Color.Transparent;
            control.BackColor = back;
        }

        internal static Color EffectiveBackColor(Control control)
        {
            while (control != null)
            {
                if (control.BackColor.A == 255)
                    return control.BackColor;
                control = control.Parent;
            }
            return P.Window;
        }

        /// <summary>
        /// True for colours AWB or its designer set deliberately (not system colours, not ours)
        /// </summary>
        internal static bool IsCustom(Color color)
        {
            return ModernToolStripRenderer.IsCustomColor(color)
                   && color.ToArgb() != Color.White.ToArgb() && color.ToArgb() != Color.Black.ToArgb();
        }

        private static void WhenHandleReady(Control control, bool firstPass, MethodInvoker action)
        {
            if (firstPass)
                control.HandleCreated += (sender, e) =>
                {
                    if (Theme.Enabled)
                        action();
                };
            if (control.IsHandleCreated)
                action();
        }

        private static void ApplyNativeTheme(Control control)
        {
            ThemeNative.SetTheme(control.Handle, P.IsDark ? "DarkMode_Explorer" : "Explorer");
        }

        private static void ApplyListViewTheme(ListView listView)
        {
            ThemeNative.SetTheme(listView.Handle, P.IsDark ? "DarkMode_Explorer" : "Explorer");
            IntPtr header = ThemeNative.SendMessage(listView.Handle, ThemeNative.LVM_GETHEADER, IntPtr.Zero, IntPtr.Zero);
            if (header != IntPtr.Zero)
                ThemeNative.SetTheme(header, P.IsDark ? "DarkMode_ItemsView" : "ItemsView");
        }

        private static void ApplyProgressBarTheme(ProgressBar progressBar)
        {
            if (!P.IsDark)
            {
                ThemeNative.SetTheme(progressBar.Handle, null);
                return;
            }

            // classic colour messages only work without visual styles
            ThemeNative.RemoveTheme(progressBar.Handle);
            ThemeNative.SendMessage(progressBar.Handle, ThemeNative.PBM_SETBKCOLOR, IntPtr.Zero,
                (IntPtr)ColorTranslator.ToWin32(P.Input));
            ThemeNative.SendMessage(progressBar.Handle, ThemeNative.PBM_SETBARCOLOR, IntPtr.Zero,
                (IntPtr)ColorTranslator.ToWin32(P.Accent));
        }

        /// <summary>
        /// 3D borders draw light edges in dark mode, so use a flat border there and restore the
        /// original border in light mode
        /// </summary>
        private static void SetDarkBorder(Control control, bool dark)
        {
            PropertyInfo prop = control.GetType().GetProperty("BorderStyle", typeof(BorderStyle));
            if (prop == null || !prop.CanWrite)
                return;

            var current = (BorderStyle)prop.GetValue(control, null);
            if (!OriginalBorder.TryGetValue(control, out object original))
            {
                original = current;
                OriginalBorder.Add(control, original);
            }

            var wanted = dark && (BorderStyle)original == BorderStyle.Fixed3D ? BorderStyle.FixedSingle : (BorderStyle)original;
            if (wanted != current)
                prop.SetValue(control, wanted, null);
        }

        private static void StyleGrid(DataGridView grid)
        {
            grid.BackgroundColor = P.Window;
            grid.GridColor = P.Border;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.EnableHeadersVisualStyles = false;
            grid.DefaultCellStyle.BackColor = P.Input;
            grid.DefaultCellStyle.ForeColor = P.Text;
            grid.DefaultCellStyle.SelectionBackColor = P.Selection;
            grid.DefaultCellStyle.SelectionForeColor = P.Text;
            grid.ColumnHeadersDefaultCellStyle.BackColor = P.Surface;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = P.Text;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = P.Surface;
            grid.RowHeadersDefaultCellStyle.BackColor = P.Surface;
            grid.RowHeadersDefaultCellStyle.ForeColor = P.Text;
            grid.RowHeadersDefaultCellStyle.SelectionBackColor = P.Selection;
        }

        private static void StyleToolStrip(ToolStrip toolStrip, bool firstPass)
        {
            if (firstPass && !Hooked.TryGetValue(toolStrip, out _))
                Hooked.Add(toolStrip, new object());

            if (!ReferenceEquals(toolStrip.Renderer, Renderer))
                toolStrip.Renderer = Renderer;
            toolStrip.BackColor = toolStrip is ToolStripDropDown ? P.Popup : P.Window;
            SetForeColor(toolStrip, P.Text);
            StyleFont(toolStrip);
            StyleToolStripImages(toolStrip);

            if (firstPass)
                toolStrip.ItemAdded += (sender, e) =>
                {
                    if (Theme.Enabled)
                        StyleToolStripItem(e.Item, true);
                };

            foreach (ToolStripItem item in toolStrip.Items)
                StyleToolStripItem(item, firstPass);
        }

        private static void StyleToolStripItem(ToolStripItem item, bool firstPass)
        {
            if (!ModernToolStripRenderer.IsCustomColor(item.ForeColor))
                item.ForeColor = P.Text;

            if (item is ToolStripControlHost host)
                SafeStyleTree(host.Control);

            if (item is ToolStripDropDownItem dropDownItem && dropDownItem.HasDropDownItems)
                StyleToolStrip(dropDownItem.DropDown, !IsStyled(dropDownItem.DropDown));
            else if (item is ToolStripDropDownItem lazy && firstPass)
            {
                // menus filled on demand (e.g. recent files) are styled when they open
                lazy.DropDownOpening += (sender, e) =>
                {
                    if (Theme.Enabled)
                        StyleToolStrip(lazy.DropDown, !IsStyled(lazy.DropDown));
                };
            }
        }

        private static void StyleTabControl(TabControl tabs, bool firstPass)
        {
            if (!firstPass || tabs.Alignment != TabAlignment.Top || SetStyleMethod == null)
                return;

            // Paint the whole control ourselves: native tabs draw light 3D frames that cannot be recoloured
            SetStyleMethod.Invoke(tabs, new object[]
            {
                ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true
            });
            HoveredTab.Add(tabs, new StrongBox<int>(-1));
            WhenHandleReady(tabs, true, () => FitTabs(tabs));
            tabs.SizeChanged += (sender, e) => FitTabs(tabs);
            tabs.Paint += PaintTabControl;
            tabs.MouseMove += (sender, e) => SetHoveredTab(tabs, TabIndexAt(tabs, e.Location));
            tabs.MouseLeave += (sender, e) => SetHoveredTab(tabs, -1);
        }

        /// <summary>
        /// Segoe UI is slightly wider than MS Sans Serif; tab strips designed to fit exactly would
        /// otherwise overflow into scroll arrows. Tightens the tab padding until they fit again.
        /// </summary>
        private static void FitTabs(TabControl tabs)
        {
            if (tabs.Multiline || tabs.TabCount == 0 || !tabs.IsHandleCreated)
                return;

            while (tabs.GetTabRect(tabs.TabCount - 1).Right > tabs.Width - 2 && tabs.Padding.X > 2)
                tabs.Padding = new Point(tabs.Padding.X - 1, tabs.Padding.Y);
        }

        private static int TabIndexAt(TabControl tabs, Point point)
        {
            for (int i = 0; i < tabs.TabCount; i++)
            {
                if (tabs.GetTabRect(i).Contains(point))
                    return i;
            }
            return -1;
        }

        private static void SetHoveredTab(TabControl tabs, int index)
        {
            if (HoveredTab.TryGetValue(tabs, out StrongBox<int> hovered) && hovered.Value != index)
            {
                hovered.Value = index;
                tabs.Invalidate(new Rectangle(0, 0, tabs.Width, tabs.DisplayRectangle.Top));
            }
        }

        private static void PaintTabControl(object sender, PaintEventArgs e)
        {
            var tabs = (TabControl)sender;
            Graphics g = e.Graphics;
            g.Clear(EffectiveBackColor(tabs.Parent));

            int stripBottom = 0;
            for (int i = 0; i < tabs.TabCount; i++)
                stripBottom = Math.Max(stripBottom, tabs.GetTabRect(i).Bottom);

            // page area: a subtle bordered card behind the tab page
            Rectangle page = tabs.DisplayRectangle;
            var card = new Rectangle(0, stripBottom, tabs.Width - 1, tabs.Height - stripBottom - 1);
            using (var brush = new SolidBrush(tabs.SelectedTab?.BackColor ?? P.Surface))
                g.FillRectangle(brush, card);
            using (var pen = new Pen(P.Border))
                g.DrawRectangle(pen, card);

            HoveredTab.TryGetValue(tabs, out StrongBox<int> hovered);
            for (int i = 0; i < tabs.TabCount; i++)
            {
                Rectangle r = tabs.GetTabRect(i);
                bool selected = i == tabs.SelectedIndex;
                bool hot = hovered != null && hovered.Value == i;

                if (selected)
                {
                    var tab = new Rectangle(r.X, r.Y, r.Width, stripBottom - r.Y + 1);
                    using (var brush = new SolidBrush(tabs.TabPages[i].BackColor))
                        g.FillRectangle(brush, tab);
                    using (var pen = new Pen(P.Border))
                    {
                        g.DrawLine(pen, tab.Left, tab.Top, tab.Left, tab.Bottom);
                        g.DrawLine(pen, tab.Right, tab.Top, tab.Right, tab.Bottom);
                    }
                    ModernToolStripRenderer.FillRounded(g, new Rectangle(r.X, r.Y, r.Width + 1, 3), P.Accent, 1);
                }
                else if (hot)
                {
                    ModernToolStripRenderer.FillRounded(g, Rectangle.Inflate(r, -1, -2), P.Hover, 4);
                }

                TextRenderer.DrawText(g, tabs.TabPages[i].Text, tabs.Font, r,
                    selected || hot ? P.Text : P.MutedText,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.EndEllipsis | TextFormatFlags.HidePrefix | TextFormatFlags.NoPadding);
            }

            if (tabs.Focused && tabs.SelectedIndex >= 0)
            {
                Rectangle r = Rectangle.Inflate(tabs.GetTabRect(tabs.SelectedIndex), -3, -3);
                ModernToolStripRenderer.DrawRounded(g, r, P.StrongBorder, 3);
            }
        }

        private static void DrawComboBoxItem(object sender, DrawItemEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;

            using (var brush = new SolidBrush(selected ? P.Selection : P.Input))
                e.Graphics.FillRectangle(brush, e.Bounds);

            string text = e.Index >= 0 && e.Index < comboBox.Items.Count
                ? comboBox.GetItemText(comboBox.Items[e.Index])
                : comboBox.Text;
            TextRenderer.DrawText(e.Graphics, text, comboBox.Font, e.Bounds,
                comboBox.Enabled ? P.Text : P.MutedText,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix);
        }

        private static void StyleBrowserDocument(WebBrowser browser)
        {
            try
            {
                if (!P.IsDark || browser.Document?.Body == null)
                    return;

                // pages other than diffs (which carry their own dark CSS) get a plain dark body
                if (string.IsNullOrEmpty(browser.Document.Body.Style))
                    browser.Document.Body.Style = "background:" + ColorTranslator.ToHtml(P.Input) +
                                                  ";color:" + ColorTranslator.ToHtml(P.Text) + ";" + DarkScrollbars;
            }
            catch (Exception)
            {
                // a navigating browser can briefly have no accessible document
            }
        }

        /// <summary>
        /// IE-only CSS that colours the embedded browser's scroll bars
        /// </summary>
        internal static string DarkScrollbars =>
            "scrollbar-base-color:#2b2b2b;scrollbar-face-color:#3a3a3a;scrollbar-track-color:#1c1c1c;" +
            "scrollbar-arrow-color:#9a9a9a;scrollbar-shadow-color:#2b2b2b;scrollbar-highlight-color:#3a3a3a;" +
            "scrollbar-3dlight-color:#2b2b2b;scrollbar-darkshadow-color:#1c1c1c;";

        #region Disabled text boxes in dark mode

        // Windows paints disabled (and empty read-only) edit controls with the light system colour,
        // ignoring BackColor. In dark mode a label is shown on top instead; the edit control itself
        // is never enabled or otherwise changed.

        private static void QueueDisabledTextOverlay(TextBoxBase textBox)
        {
            if (textBox.Parent is ToolStrip)
                return;

            textBox.ParentChanged += (sender, e) => EnsureOverlay(textBox);
            textBox.EnabledChanged += (sender, e) => UpdateDisabledTextOverlay(textBox);
            textBox.VisibleChanged += (sender, e) => UpdateDisabledTextOverlay(textBox);
            textBox.TextChanged += (sender, e) => UpdateDisabledTextOverlay(textBox);
            textBox.ReadOnlyChanged += (sender, e) => UpdateDisabledTextOverlay(textBox);
            textBox.LocationChanged += (sender, e) => UpdateDisabledTextOverlay(textBox);
            textBox.SizeChanged += (sender, e) => UpdateDisabledTextOverlay(textBox);
        }

        private static bool NeedsOverlay(TextBoxBase textBox)
        {
            return Theme.IsDark && textBox.Visible &&
                   (!textBox.Enabled || (textBox.ReadOnly && textBox.TextLength == 0));
        }

        private static void EnsureOverlay(TextBoxBase textBox)
        {
            if (textBox.IsDisposed || textBox.Parent == null || textBox.Parent is ToolStrip)
                return;

            if (DisabledTextOverlays.TryGetValue(textBox, out Label overlay))
            {
                if (overlay.Parent != textBox.Parent && !overlay.IsDisposed)
                    textBox.Parent.Controls.Add(overlay);
                return;
            }

            overlay = new Label
            {
                AutoSize = false,
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(1, 2, 1, 1),
                TextAlign = ContentAlignment.TopLeft,
                UseMnemonic = false,
                Tag = OverlayTag,
                Visible = false
            };
            DisabledTextOverlays.Add(textBox, overlay);
            textBox.Parent.Controls.Add(overlay);
        }

        private static void UpdateDisabledTextOverlay(TextBoxBase textBox)
        {
            if (!DisabledTextOverlays.TryGetValue(textBox, out Label overlay))
            {
                if (!NeedsOverlay(textBox))
                    return;
                EnsureOverlay(textBox);
                if (!DisabledTextOverlays.TryGetValue(textBox, out overlay))
                    return;
            }
            if (overlay.IsDisposed)
                return;

            bool show = NeedsOverlay(textBox);
            if (show)
            {
                overlay.Bounds = textBox.Bounds;
                overlay.Anchor = textBox.Anchor;
                overlay.Font = textBox.Font;
                overlay.BackColor = P.Input;
                overlay.ForeColor = P.MutedText;
                overlay.Text = textBox is TextBox plain && plain.UseSystemPasswordChar
                    ? new string('●', plain.TextLength)
                    : textBox.Text;
            }
            overlay.Visible = show;
            if (show)
                overlay.BringToFront();
        }

        #endregion
    }
}
