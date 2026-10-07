using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WikiFunctions
{
    /// <summary>
    /// Experimental application-wide dark theme for the standalone test build.
    /// It themes existing and newly opened WinForms without changing AWB settings.
    /// </summary>
    internal static class DarkModeTheme
    {
        private const int EmSetBackgroundColor = 0x0443;
        private const int PbmSetBarColor = 0x0409;
        private const int PbmSetBackgroundColor = 0x2001;
        private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
        private const int DwmwaUseImmersiveDarkMode = 20;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute,
            ref int value, int valueSize);

        private static readonly Color Window = Color.FromArgb(31, 32, 35);
        private static readonly Color Surface = Color.FromArgb(43, 45, 49);
        private static readonly Color Input = Color.FromArgb(35, 37, 41);
        private static readonly Color Foreground = Color.FromArgb(232, 234, 237);
        private static readonly Color Muted = Color.FromArgb(154, 160, 166);
        private static readonly Color Border = Color.FromArgb(95, 99, 104);
        private static readonly Color Selection = Color.FromArgb(60, 80, 112);
        private static readonly ToolStripRenderer DarkToolStripRenderer =
            new ToolStripProfessionalRenderer(new DarkColorTable());
        private static readonly ConditionalWeakTable<Control, object> Themed =
            new ConditionalWeakTable<Control, object>();
        private static readonly ConditionalWeakTable<TextBoxBase, Label> DisabledTextOverlays =
            new ConditionalWeakTable<TextBoxBase, Label>();
        private static readonly ConditionalWeakTable<ComboBox, Label> ComboArrowOverlays =
            new ConditionalWeakTable<ComboBox, Label>();
        private static readonly ConditionalWeakTable<TabControl, TabFrameOverlay> TabFrameOverlays =
            new ConditionalWeakTable<TabControl, TabFrameOverlay>();
        private static readonly object DisabledTextOverlayTag = new object();
        private static Timer ThemeTimer;

        internal static void Initialize()
        {
            if (ThemeTimer != null)
                return;

            ThemeTimer = new Timer { Interval = 500 };
            ThemeTimer.Tick += ThemeOpenForms;
            ThemeTimer.Start();
        }

        private static void ThemeOpenForms(object sender, EventArgs e)
        {
            foreach (Form form in Application.OpenForms)
                ThemeControl(form);
        }

        private static void ThemeControl(Control control)
        {
            if (control.IsDisposed || ReferenceEquals(control.Tag, DisabledTextOverlayTag))
                return;

            bool firstPass = !Themed.TryGetValue(control, out _);
            ApplyColors(control, firstPass);

            if (firstPass)
            {
                Themed.Add(control, new object());
            }

            foreach (Control child in control.Controls)
                ThemeControl(child);
        }

        private static void ApplyColors(Control control, bool firstPass)
        {
            control.ForeColor = Foreground;

            if (control is Form)
            {
                control.BackColor = Window;
                if (firstPass && control.IsHandleCreated)
                    ApplyDarkTitleBar(control.Handle);
            }
            else if (control is TextBoxBase textBox)
            {
                textBox.BackColor = Input;
                textBox.ForeColor = Foreground;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                if (firstPass)
                {
                    ApplyDarkControlThemeWhenReady(textBox);
                    QueueDisabledTextOverlay(textBox);
                }
                UpdateDisabledTextOverlay(textBox);

                if (textBox is RichTextBox && textBox.IsHandleCreated)
                    SendMessage(textBox.Handle, EmSetBackgroundColor, IntPtr.Zero,
                        (IntPtr)ColorTranslator.ToWin32(Input));

            }
            else if (control is ListBox listBox)
            {
                listBox.BackColor = Input;
                listBox.ForeColor = Foreground;
                listBox.BorderStyle = BorderStyle.FixedSingle;
                if (firstPass)
                    ApplyDarkControlThemeWhenReady(listBox);
            }
            else if (control is TreeView treeView)
            {
                treeView.BackColor = Input;
                treeView.ForeColor = Foreground;
                treeView.BorderStyle = BorderStyle.FixedSingle;
                if (firstPass)
                    ApplyDarkControlThemeWhenReady(treeView);
            }
            else if (control is ListView listView)
            {
                listView.BackColor = Input;
                listView.ForeColor = Foreground;
                listView.BorderStyle = BorderStyle.FixedSingle;
                if (firstPass)
                    ApplyDarkControlThemeWhenReady(listView);
            }
            else if (control is DataGridView grid)
            {
                grid.BackgroundColor = Window;
                grid.GridColor = Border;
                grid.DefaultCellStyle.BackColor = Input;
                grid.DefaultCellStyle.ForeColor = Foreground;
                grid.DefaultCellStyle.SelectionBackColor = Selection;
                grid.DefaultCellStyle.SelectionForeColor = Foreground;
                grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Foreground;
                grid.RowHeadersDefaultCellStyle.BackColor = Surface;
                grid.RowHeadersDefaultCellStyle.ForeColor = Foreground;
                grid.EnableHeadersVisualStyles = false;
                if (firstPass)
                    ApplyDarkControlThemeWhenReady(grid);
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.BackColor = Input;
                comboBox.ForeColor = Foreground;
                comboBox.FlatStyle = FlatStyle.Flat;
                if (firstPass)
                    ApplyDarkControlThemeWhenReady(comboBox);
                if (comboBox.DropDownStyle != ComboBoxStyle.Simple)
                    comboBox.DrawMode = DrawMode.OwnerDrawFixed;
                if (firstPass)
                {
                    comboBox.DrawItem += DrawComboBoxItem;
                    QueueComboArrowOverlay(comboBox);
                }
                UpdateComboArrowOverlay(comboBox);
            }
            else if (control is ProgressBar progressBar)
            {
                ThemeProgressBar(progressBar);
            }
            else if (control is NumericUpDown numericUpDown)
            {
                numericUpDown.BackColor = Input;
                numericUpDown.ForeColor = Foreground;
                numericUpDown.BorderStyle = BorderStyle.FixedSingle;
                if (firstPass)
                    ApplyDarkControlThemeWhenReady(numericUpDown);
            }
            else if (control is Button button)
            {
                button.BackColor = Surface;
                button.ForeColor = button.Enabled ? Foreground : Muted;
                button.UseVisualStyleBackColor = false;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 58, 64);
                button.FlatAppearance.MouseDownBackColor = Selection;
                if (firstPass && button.Image == null)
                    button.Paint += DrawDisabledButton;
            }
            else if (control is ToolStrip toolStrip)
            {
                toolStrip.BackColor = Surface;
                toolStrip.ForeColor = Foreground;
                if (!ReferenceEquals(toolStrip.Renderer, DarkToolStripRenderer))
                    toolStrip.Renderer = DarkToolStripRenderer;
                ThemeToolStripItems(toolStrip);
            }
            else if (control is TabControl tabs)
            {
                tabs.BackColor = Surface;
                tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
                tabs.Appearance = TabAppearance.FlatButtons;
                if (firstPass)
                {
                    ApplyDarkControlThemeWhenReady(tabs);
                    tabs.DrawItem += DrawTab;
                    QueueTabFrameOverlay(tabs);
                }
            }
            else if (control is WebBrowser browser)
            {
                if (firstPass)
                    browser.DocumentCompleted += ThemeBrowserDocument;
                ThemeBrowserDocument(browser, EventArgs.Empty);
            }
            else if (control is TabPage || control is Panel || control is UserControl ||
                     control is GroupBox || control is SplitContainer)
            {
                control.BackColor = Surface;
            }
            else if (control is Label label)
            {
                label.BackColor = Color.Transparent;
                if (label is LinkLabel linkLabel)
                {
                    linkLabel.LinkColor = Color.FromArgb(138, 180, 248);
                    linkLabel.ActiveLinkColor = Color.FromArgb(174, 203, 250);
                    linkLabel.VisitedLinkColor = Color.FromArgb(187, 134, 252);
                    linkLabel.DisabledLinkColor = Muted;
                }
                if (!label.Enabled)
                    label.ForeColor = Muted;
                if (firstPass)
                    label.Paint += DrawDisabledLabel;
            }
            else if (control is CheckBox checkBox)
            {
                checkBox.BackColor = Surface;
                checkBox.FlatStyle = FlatStyle.Flat;
                checkBox.FlatAppearance.BorderColor = Border;
                if (firstPass)
                    checkBox.Paint += DrawCheckBox;
            }
            else if (control is RadioButton radioButton)
            {
                radioButton.BackColor = Surface;
                radioButton.FlatStyle = FlatStyle.Flat;
                radioButton.FlatAppearance.BorderColor = Border;
                if (firstPass)
                    radioButton.Paint += DrawRadioButton;
            }
            else
            {
                control.BackColor = Surface;
            }
        }

        private static void ApplyDarkControlThemeWhenReady(Control control)
        {
            control.HandleCreated += (sender, args) =>
                SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
            if (control.IsHandleCreated)
                SetWindowTheme(control.Handle, "DarkMode_Explorer", null);
        }

        private static void BeginInvokeWhenReady(Control control, MethodInvoker action)
        {
            if (control.IsDisposed)
                return;

            if (!control.IsHandleCreated)
            {
                EventHandler handleCreated = null;
                handleCreated = (sender, args) =>
                {
                    control.HandleCreated -= handleCreated;
                    BeginInvokeWhenReady(control, action);
                };
                control.HandleCreated += handleCreated;
                return;
            }

            try
            {
                control.BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
                // The handle can be destroyed between the check and BeginInvoke.
                if (!control.IsDisposed)
                {
                    EventHandler handleCreated = null;
                    handleCreated = (sender, args) =>
                    {
                        control.HandleCreated -= handleCreated;
                        BeginInvokeWhenReady(control, action);
                    };
                    control.HandleCreated += handleCreated;
                }
            }
        }
        private static void QueueDisabledTextOverlay(TextBoxBase textBox)
        {
            if (textBox.Parent is ToolStrip || DisabledTextOverlays.TryGetValue(textBox, out _))
                return;

            BeginInvokeWhenReady(textBox, () =>
            {
                if (textBox.IsDisposed || textBox.Parent == null ||
                    DisabledTextOverlays.TryGetValue(textBox, out _))
                    return;

                var overlay = new Label
                {
                    AutoSize = false,
                    BackColor = Input,
                    ForeColor = Muted,
                    BorderStyle = BorderStyle.FixedSingle,
                    Padding = new Padding(2),
                    TextAlign = ContentAlignment.TopLeft,
                    Font = textBox.Font,
                    Tag = DisabledTextOverlayTag,
                    Bounds = textBox.Bounds,
                    Anchor = textBox.Anchor,
                    Visible = false
                };

                DisabledTextOverlays.Add(textBox, overlay);
                textBox.Parent.Controls.Add(overlay);
                textBox.EnabledChanged += (sender, args) => UpdateDisabledTextOverlay(textBox);
                textBox.VisibleChanged += (sender, args) => UpdateDisabledTextOverlay(textBox);
                textBox.TextChanged += (sender, args) => UpdateDisabledTextOverlay(textBox);
                textBox.ReadOnlyChanged += (sender, args) => UpdateDisabledTextOverlay(textBox);
                textBox.LocationChanged += (sender, args) => UpdateDisabledTextOverlay(textBox);
                textBox.SizeChanged += (sender, args) => UpdateDisabledTextOverlay(textBox);
                UpdateDisabledTextOverlay(textBox);
            });
        }

        private static void UpdateDisabledTextOverlay(TextBoxBase textBox)
        {
            if (!DisabledTextOverlays.TryGetValue(textBox, out Label overlay) || overlay.IsDisposed)
                return;

            overlay.Bounds = textBox.Bounds;
            overlay.Font = textBox.Font;
            if (textBox is TextBox plainTextBox && plainTextBox.UseSystemPasswordChar)
                overlay.Text = new string('●', plainTextBox.TextLength);
            else
                overlay.Text = textBox.Text;
            overlay.Visible = textBox.Visible &&
                (!textBox.Enabled || (textBox.ReadOnly && textBox.TextLength == 0));
            if (overlay.Visible)
                overlay.BringToFront();
        }
        private static void QueueComboArrowOverlay(ComboBox comboBox)
        {
            if (comboBox.Parent is ToolStrip || ComboArrowOverlays.TryGetValue(comboBox, out _))
                return;

            BeginInvokeWhenReady(comboBox, () =>
            {
                if (comboBox.IsDisposed || comboBox.Parent == null ||
                    ComboArrowOverlays.TryGetValue(comboBox, out _))
                    return;

                var arrow = new Label
                {
                    AutoSize = false,
                    BackColor = Surface,
                    ForeColor = Foreground,
                    Font = comboBox.Font,
                    Tag = DisabledTextOverlayTag,
                    Text = "▼",
                    TextAlign = ContentAlignment.MiddleCenter
                };
                ComboArrowOverlays.Add(comboBox, arrow);
                comboBox.Parent.Controls.Add(arrow);
                arrow.Click += (sender, args) =>
                {
                    if (comboBox.Enabled)
                    {
                        comboBox.Focus();
                        comboBox.DroppedDown = true;
                    }
                };
                comboBox.EnabledChanged += (sender, args) => UpdateComboArrowOverlay(comboBox);
                comboBox.VisibleChanged += (sender, args) => UpdateComboArrowOverlay(comboBox);
                comboBox.LocationChanged += (sender, args) => UpdateComboArrowOverlay(comboBox);
                comboBox.SizeChanged += (sender, args) => UpdateComboArrowOverlay(comboBox);
                UpdateComboArrowOverlay(comboBox);
            });
        }

        private static void UpdateComboArrowOverlay(ComboBox comboBox)
        {
            if (!ComboArrowOverlays.TryGetValue(comboBox, out Label arrow) || arrow.IsDisposed)
                return;

            int width = Math.Min(SystemInformation.VerticalScrollBarWidth, comboBox.Width);
            arrow.Bounds = new Rectangle(comboBox.Right - width, comboBox.Top + 1,
                width, Math.Max(0, comboBox.Height - 2));
            arrow.ForeColor = comboBox.Enabled ? Foreground : Muted;
            arrow.Visible = comboBox.Visible && comboBox.DropDownStyle != ComboBoxStyle.Simple;
            if (arrow.Visible)
                arrow.BringToFront();
        }

        private static void QueueTabFrameOverlay(TabControl tabs)
        {
            if (TabFrameOverlays.TryGetValue(tabs, out _))
                return;

            BeginInvokeWhenReady(tabs, () =>
            {
                if (tabs.IsDisposed || tabs.Parent == null ||
                    TabFrameOverlays.TryGetValue(tabs, out _))
                    return;

                var overlay = new TabFrameOverlay(tabs);
                TabFrameOverlays.Add(tabs, overlay);
                overlay.Update();
            });
        }
        private static void ThemeProgressBar(ProgressBar progressBar)
        {
            if (!progressBar.IsHandleCreated)
                return;

            SendMessage(progressBar.Handle, PbmSetBackgroundColor, IntPtr.Zero,
                (IntPtr)ColorTranslator.ToWin32(Input));
            SendMessage(progressBar.Handle, PbmSetBarColor, IntPtr.Zero,
                (IntPtr)ColorTranslator.ToWin32(Selection));
        }

        private static void ThemeToolStripItems(ToolStrip toolStrip)
        {
            foreach (ToolStripItem item in toolStrip.Items)
                ThemeToolStripItem(item);
        }

        private static void ThemeToolStripItem(ToolStripItem item)
        {
            item.BackColor = Surface;
            item.ForeColor = item.Enabled ? Foreground : Muted;

            if (item is ToolStripControlHost host)
                ThemeControl(host.Control);
            if (item is ToolStripDropDownItem dropDown)
            {
                foreach (ToolStripItem child in dropDown.DropDownItems)
                    ThemeToolStripItem(child);
            }
        }

        private static void DrawComboBoxItem(object sender, DrawItemEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            Color background = (e.State & DrawItemState.Selected) != 0 ? Selection : Input;
            using (var brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, e.Bounds);

            string text = e.Index >= 0 && e.Index < comboBox.Items.Count
                ? comboBox.GetItemText(comboBox.Items[e.Index])
                : comboBox.Text;
            TextRenderer.DrawText(e.Graphics, text, comboBox.Font, e.Bounds,
                comboBox.Enabled ? Foreground : Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static void DrawDisabledButton(object sender, PaintEventArgs e)
        {
            var button = (Button)sender;
            if (button.Enabled)
                return;

            e.Graphics.Clear(Surface);
            using (var pen = new Pen(Border))
                e.Graphics.DrawRectangle(pen, 0, 0, button.Width - 1, button.Height - 1);
            TextRenderer.DrawText(e.Graphics, button.Text, button.Font, button.ClientRectangle,
                Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawDisabledLabel(object sender, PaintEventArgs e)
        {
            var label = (Label)sender;
            if (label.Enabled)
                return;

            e.Graphics.Clear(label.Parent?.BackColor ?? Surface);
            TextRenderer.DrawText(e.Graphics, label.Text, label.Font, label.ClientRectangle,
                Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis);
        }

        private static void DrawCheckBox(object sender, PaintEventArgs e)
        {
            var checkBox = (CheckBox)sender;
            e.Graphics.Clear(checkBox.Parent?.BackColor ?? Surface);

            int glyphSize = Math.Min(13, Math.Max(9, checkBox.Height - 4));
            var glyph = new Rectangle(1, (checkBox.Height - glyphSize) / 2, glyphSize, glyphSize);
            using (var fill = new SolidBrush(Input))
                e.Graphics.FillRectangle(fill, glyph);
            using (var pen = new Pen(Border))
                e.Graphics.DrawRectangle(pen, glyph);

            if (checkBox.CheckState != CheckState.Unchecked)
            {
                Color markColor = checkBox.Enabled ? Foreground : Muted;
                using (var pen = new Pen(markColor, 2))
                {
                    int middle = glyph.Top + glyph.Height / 2;
                    e.Graphics.DrawLine(pen, glyph.Left + 3, middle,
                        glyph.Left + glyph.Width / 2, glyph.Bottom - 3);
                    e.Graphics.DrawLine(pen, glyph.Left + glyph.Width / 2, glyph.Bottom - 3,
                        glyph.Right - 2, glyph.Top + 3);
                }
            }

            var textBounds = new Rectangle(glyph.Right + 5, 0,
                Math.Max(0, checkBox.Width - glyph.Right - 5), checkBox.Height);
            TextRenderer.DrawText(e.Graphics, checkBox.Text, checkBox.Font, textBounds,
                checkBox.Enabled ? Foreground : Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static void DrawRadioButton(object sender, PaintEventArgs e)
        {
            var radioButton = (RadioButton)sender;
            e.Graphics.Clear(radioButton.Parent?.BackColor ?? Surface);

            int glyphSize = Math.Min(13, Math.Max(9, radioButton.Height - 4));
            var glyph = new Rectangle(1, (radioButton.Height - glyphSize) / 2, glyphSize, glyphSize);
            using (var fill = new SolidBrush(Input))
                e.Graphics.FillEllipse(fill, glyph);
            using (var pen = new Pen(Border))
                e.Graphics.DrawEllipse(pen, glyph);
            if (radioButton.Checked)
            {
                var dot = Rectangle.Inflate(glyph, -4, -4);
                using (var fill = new SolidBrush(radioButton.Enabled ? Foreground : Muted))
                    e.Graphics.FillEllipse(fill, dot);
            }

            var textBounds = new Rectangle(glyph.Right + 5, 0,
                Math.Max(0, radioButton.Width - glyph.Right - 5), radioButton.Height);
            TextRenderer.DrawText(e.Graphics, radioButton.Text, radioButton.Font, textBounds,
                radioButton.Enabled ? Foreground : Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }
        private static void ApplyDarkTitleBar(IntPtr handle)
        {
            int enabled = 1;
            if (DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode,
                    ref enabled, sizeof(int)) != 0)
                DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkModeBefore20H1,
                    ref enabled, sizeof(int));
        }

        private static void DrawTab(object sender, DrawItemEventArgs e)
        {
            var tabs = (TabControl)sender;
            if (e.Index < 0 || e.Index >= tabs.TabPages.Count)
                return;

            Rectangle bounds = e.Bounds;
            Color background = e.Index == tabs.SelectedIndex ? Selection : Surface;

            using (var brush = new SolidBrush(background))
                e.Graphics.FillRectangle(brush, bounds);
            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font,
                bounds, Foreground, TextFormatFlags.HorizontalCenter |
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static void ThemeBrowserDocument(object sender, EventArgs e)
        {
            var browser = sender as WebBrowser;
            try
            {
                if (browser?.Document?.Body == null)
                    return;

                browser.Document.Body.Style =
                    "background:#232529;color:#e8eaed;font-family:Consolas,monospace;";
            }
            catch (Exception)
            {
                // A navigating browser can briefly have no accessible document.
            }
        }

        private sealed class TabFrameOverlay
        {
            private readonly TabControl Tabs;
            private readonly Label HeaderFill;
            private readonly Label LeftEdge;
            private readonly Label RightEdge;
            private readonly Label BottomEdge;

            internal TabFrameOverlay(TabControl tabs)
            {
                Tabs = tabs;
                HeaderFill = CreateStrip(Surface);
                LeftEdge = CreateStrip(Border);
                RightEdge = CreateStrip(Border);
                BottomEdge = CreateStrip(Border);

                tabs.Parent.Controls.Add(HeaderFill);
                tabs.Parent.Controls.Add(LeftEdge);
                tabs.Parent.Controls.Add(RightEdge);
                tabs.Parent.Controls.Add(BottomEdge);

                tabs.LocationChanged += (sender, args) => Update();
                tabs.SizeChanged += (sender, args) => Update();
                tabs.VisibleChanged += (sender, args) => Update();
                tabs.SelectedIndexChanged += (sender, args) => Update();
                tabs.ControlAdded += (sender, args) => Update();
                tabs.ControlRemoved += (sender, args) => Update();
            }

            private static Label CreateStrip(Color color)
            {
                return new Label
                {
                    AutoSize = false,
                    BackColor = color,
                    Tag = DisabledTextOverlayTag
                };
            }

            internal void Update()
            {
                if (Tabs.IsDisposed || Tabs.Parent == null)
                    return;

                Rectangle bounds = Tabs.Bounds;
                Rectangle lastTab = Tabs.TabCount > 0
                    ? Tabs.GetTabRect(Tabs.TabCount - 1)
                    : Rectangle.Empty;
                int headerHeight = Math.Max(2, lastTab.Bottom + 2);
                int lastTabRight = Math.Max(0, lastTab.Right);

                HeaderFill.Bounds = new Rectangle(bounds.Left + lastTabRight, bounds.Top,
                    Math.Max(0, bounds.Width - lastTabRight - 2), headerHeight);
                LeftEdge.Bounds = new Rectangle(bounds.Left, bounds.Top + headerHeight,
                    2, Math.Max(0, bounds.Height - headerHeight));
                RightEdge.Bounds = new Rectangle(bounds.Right - 2, bounds.Top + headerHeight,
                    2, Math.Max(0, bounds.Height - headerHeight));
                BottomEdge.Bounds = new Rectangle(bounds.Left, bounds.Bottom - 2,
                    bounds.Width, 2);

                bool visible = Tabs.Visible;
                HeaderFill.Visible = visible;
                LeftEdge.Visible = visible;
                RightEdge.Visible = visible;
                BottomEdge.Visible = visible;
                if (visible)
                {
                    HeaderFill.BringToFront();
                    LeftEdge.BringToFront();
                    RightEdge.BringToFront();
                    BottomEdge.BringToFront();
                }
            }
        }
        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin => Surface;
            public override Color ToolStripGradientMiddle => Surface;
            public override Color ToolStripGradientEnd => Surface;
            public override Color MenuStripGradientBegin => Surface;
            public override Color MenuStripGradientEnd => Surface;
            public override Color ToolStripDropDownBackground => Surface;
            public override Color ImageMarginGradientBegin => Surface;
            public override Color ImageMarginGradientMiddle => Surface;
            public override Color ImageMarginGradientEnd => Surface;
            public override Color MenuItemSelected => Selection;
            public override Color MenuItemBorder => Border;
            public override Color MenuItemSelectedGradientBegin => Selection;
            public override Color MenuItemSelectedGradientEnd => Selection;
            public override Color MenuItemPressedGradientBegin => Input;
            public override Color MenuItemPressedGradientMiddle => Input;
            public override Color MenuItemPressedGradientEnd => Input;
            public override Color SeparatorDark => Border;
            public override Color SeparatorLight => Surface;
        }
    }
}
