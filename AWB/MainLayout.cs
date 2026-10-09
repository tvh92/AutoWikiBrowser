using System;
using System.Drawing;
using System.Windows.Forms;
using WikiFunctions.Properties;
using WikiFunctions.Theming;

namespace AutoWikiBrowser
{
    /// <summary>
    /// Resizable main window layout: the diff/preview browser above, and below it the page list,
    /// the options tabs and the edit box, all separated by draggable splitters whose positions
    /// are remembered.
    /// </summary>
    partial class MainForm
    {
        // Designed sizes of the bottom area, in unscaled (100 %) pixels
        private const int DesignListWidth = 216;
        private const int DesignOptionsWidth = 330;
        private const int DesignBottomHeight = 377;

        private SplitContainer splitMain;     // browser | bottom panels
        private SplitContainer splitList;     // page list | options + edit box
        private SplitContainer splitOptions;  // options tabs | edit box tabs
        private Control focusBeforeSplitterDrag;

        /// <summary>
        /// Moves the designer's fixed-position panels into docked split containers.
        /// Called from the constructor, right after InitializeComponent.
        /// </summary>
        private void BuildResizableLayout()
        {
            SuspendLayout();
            panel1.SuspendLayout();

            Size client = ClientSize;
            int top = MnuMain.Height;

            splitMain = CreateSplit(Orientation.Horizontal, FixedPanel.Panel2,
                new Size(client.Width, client.Height - top - StatusMain.Height));
            splitList = CreateSplit(Orientation.Vertical, FixedPanel.Panel1, new Size(client.Width, DesignBottomHeight));
            splitOptions = CreateSplit(Orientation.Vertical, FixedPanel.Panel1,
                new Size(client.Width - DesignListWidth - splitList.SplitterWidth, DesignBottomHeight));

            // page list: "Make list" caption above the list maker
            panel1.Controls.Remove(label8);
            panel1.Controls.Remove(listMaker);
            label8.Dock = DockStyle.Top;
            listMaker.Dock = DockStyle.Fill;
            splitList.Panel1.Padding = new Padding(3, 2, 0, 2);
            splitList.Panel1.Controls.Add(listMaker);
            splitList.Panel1.Controls.Add(label8);

            // options tabs and edit box tabs
            panel1.Controls.Remove(MainTab);
            panel1.Controls.Remove(EditBoxTab);
            MainTab.MinimumSize = MainTab.MaximumSize = Size.Empty;
            MainTab.Dock = DockStyle.Fill;
            EditBoxTab.Dock = DockStyle.Fill;
            splitOptions.Panel1.Controls.Add(MainTab);
            splitOptions.Panel2.Controls.Add(EditBoxTab);

            splitList.Panel2.Controls.Add(splitOptions);
            panel1.Controls.Add(splitList);
            panel1.Dock = DockStyle.Fill;

            Controls.Remove(webBrowser);
            Controls.Remove(panel1);
            webBrowser.Dock = DockStyle.Fill;
            splitMain.Panel1.Controls.Add(webBrowser);
            splitMain.Panel2.Controls.Add(panel1);
            Controls.Add(splitMain);
            splitMain.BringToFront(); // fill the space left by the docked menu, toolbar and status bar

            SetSplitter(splitMain, splitMain.Height - splitMain.SplitterWidth - DesignBottomHeight, 50, 150);
            SetSplitter(splitList, DesignListWidth, listMaker.MinimumSize.Width + 4, 300);
            SetSplitter(splitOptions, DesignOptionsWidth, 200, 200);
            splitOptions.Dock = splitList.Dock = splitMain.Dock = DockStyle.Fill;

            panel1.ResumeLayout(false);
            ResumeLayout(false);

            Load += (sender, e) => RestoreLayout();
        }

        private SplitContainer CreateSplit(Orientation orientation, FixedPanel fixedPanel, Size size)
        {
            var split = new SplitContainer
            {
                Orientation = orientation,
                FixedPanel = fixedPanel,
                Size = size,
                SplitterWidth = 5,
                TabStop = false
            };

            split.Paint += PaintSplitterGrip;
            split.MouseDown += (sender, e) => focusBeforeSplitterDrag = ActiveControl;
            split.SplitterMoved += (sender, e) =>
            {
                // dragging a splitter focuses it; give focus back to where the user was typing
                if (split.Focused && focusBeforeSplitterDrag != null && focusBeforeSplitterDrag.CanFocus)
                    focusBeforeSplitterDrag.Focus();
                split.Invalidate();
            };
            return split;
        }

        private static void SetSplitter(SplitContainer split, int distance, int min1, int min2)
        {
            try
            {
                split.Panel1MinSize = min1;
                split.Panel2MinSize = min2;
                split.SplitterDistance = distance;
            }
            catch (Exception)
            {
                // window too small for the requested sizes: keep the default position
            }
        }

        /// <summary>
        /// A small grip in the middle of each splitter, so it is clear that it can be dragged
        /// </summary>
        private static void PaintSplitterGrip(object sender, PaintEventArgs e)
        {
            var split = (SplitContainer)sender;
            Rectangle r = split.SplitterRectangle;
            using (var brush = new SolidBrush(Theme.Enabled ? Theme.Current.StrongBorder : SystemColors.ControlDark))
            {
                for (int i = -1; i <= 1; i++)
                {
                    if (split.Orientation == Orientation.Vertical)
                        e.Graphics.FillEllipse(brush, r.X + r.Width / 2 - 1, r.Y + r.Height / 2 + i * 6 - 1, 3, 3);
                    else
                        e.Graphics.FillEllipse(brush, r.X + r.Width / 2 + i * 6 - 1, r.Y + r.Height / 2 - 1, 3, 3);
                }
            }
        }

        /// <summary>
        /// Restores the saved splitter positions (stored at 100 % size, so they follow the text size)
        /// </summary>
        private void RestoreLayout()
        {
            SizeF scale = Theme.LayoutScale(this);

            int bottom = UiSettings.GetInt("Layout.BottomHeight", 0);
            if (bottom > 0)
                SetDistance(splitMain, splitMain.Height - splitMain.SplitterWidth - (int)(bottom * scale.Height));

            int list = UiSettings.GetInt("Layout.ListWidth", 0);
            if (list > 0)
                SetDistance(splitList, (int)(list * scale.Width));

            int options = UiSettings.GetInt("Layout.OptionsWidth", 0);
            if (options > 0)
                SetDistance(splitOptions, (int)(options * scale.Width));
        }

        private static void SetDistance(SplitContainer split, int distance)
        {
            int length = split.Orientation == Orientation.Vertical ? split.Width : split.Height;
            distance = Math.Max(split.Panel1MinSize, Math.Min(distance, length - split.Panel2MinSize - split.SplitterWidth));
            try
            {
                if (distance > 0)
                    split.SplitterDistance = distance;
            }
            catch (Exception)
            {
            }
        }

        private void SaveLayout()
        {
            if (WindowState == FormWindowState.Minimized)
                return;

            SizeF scale = Theme.LayoutScale(this);
            if (!splitMain.Panel2Collapsed)
                UiSettings.SetInt("Layout.BottomHeight", (int)(splitMain.Panel2.Height / scale.Height));
            if (!splitList.Panel1Collapsed)
            {
                UiSettings.SetInt("Layout.ListWidth", (int)(splitList.SplitterDistance / scale.Width));
                UiSettings.SetInt("Layout.OptionsWidth", (int)(splitOptions.SplitterDistance / scale.Width));
            }
        }

        /// <summary>
        /// View > Show/hide panel: hides or shows the whole bottom area
        /// </summary>
        private void PanelShowHide()
        {
            bool show = splitMain.Panel2Collapsed;
            splitMain.Panel2Collapsed = !show;
            showHidePanelToolStripMenuItem.Checked = show;
        }

        /// <summary>
        /// View > Enlarge edit area: hides the page list and options so the edit box gets the full width
        /// </summary>
        private void ParametersShowHide()
        {
            bool enlarge = !splitList.Panel1Collapsed;
            enlargeEditAreaToolStripMenuItem.Checked = enlarge;
            btntsShowHideParameters.Image = enlarge ? Resources.Showhideparameters2 : Resources.Showhideparameters;

            splitList.Panel1Collapsed = enlarge;
            splitOptions.Panel1Collapsed = enlarge;
            listMaker.Visible = MainTab.Visible = label8.Visible = !enlarge;
        }

        /// <summary>
        /// Adds View > Theme and View > Text size
        /// </summary>
        private void AddAppearanceMenus()
        {
            var themeMenu = new ToolStripMenuItem("T&heme");
            foreach (ThemeMode mode in new[] { ThemeMode.System, ThemeMode.Light, ThemeMode.Dark, ThemeMode.Classic })
            {
                string text = mode == ThemeMode.System ? "Use &Windows setting" : "&" + mode;
                var item = new ToolStripMenuItem(text) { Tag = mode };
                item.Click += (sender, e) => Theme.SetMode((ThemeMode)((ToolStripItem)sender).Tag);
                themeMenu.DropDownItems.Add(item);
            }

            themeMenu.DropDownOpening += (sender, e) =>
            {
                foreach (ToolStripMenuItem item in themeMenu.DropDownItems)
                    item.Checked = (ThemeMode)item.Tag == Theme.Mode;
            };

            var sizeMenu = new ToolStripMenuItem("Text si&ze");
            foreach (int percent in Theme.ScalePercentages)
            {
                var item = new ToolStripMenuItem(percent + " %") { Tag = percent };
                item.Click += (sender, e) =>
                {
                    SaveLayout(); // keep the splitters where they are relative to the new size
                    Theme.SetScale((int)((ToolStripItem)sender).Tag);
                };
                sizeMenu.DropDownItems.Add(item);
            }

            sizeMenu.DropDownOpening += (sender, e) =>
            {
                foreach (ToolStripMenuItem item in sizeMenu.DropDownItems)
                    item.Checked = Math.Abs((int)item.Tag / 100f - Theme.Scale) < 0.001f;
            };

            viewToolStripMenuItem.DropDownItems.Add(new ToolStripSeparator());
            viewToolStripMenuItem.DropDownItems.Add(themeMenu);
            viewToolStripMenuItem.DropDownItems.Add(sizeMenu);
        }
    }
}
