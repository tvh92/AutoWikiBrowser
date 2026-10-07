using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace WikiFunctions.Theming
{
    public enum ThemeMode
    {
        /// <summary>Follow the Windows app mode (light or dark)</summary>
        System,
        Light,
        Dark,
        /// <summary>The unmodified classic WinForms look</summary>
        Classic
    }

    /// <summary>
    /// Application-wide light/dark theme for AWB.
    /// Each window is styled once, when it is first activated; controls added later are styled
    /// as they are added. Nothing is polled or recoloured on a timer.
    /// </summary>
    public static class Theme
    {
        private const int WH_CBT = 5;
        private const int HCBT_ACTIVATE = 5;

        private static ThemeNative.HookProc CbtProc; // kept alive for the native hook
        private static IntPtr CbtHook;
        private static readonly HashSet<int> PaletteArgb = new HashSet<int>();
        private static Font mUiFont;

        /// <summary>Colours currently in use. Never null.</summary>
        public static ThemePalette Current { get; private set; } = ThemePalette.CreateLight(ThemePalette.ReadAccentColor());

        /// <summary>The mode chosen by the user</summary>
        public static ThemeMode Mode { get; private set; } = ThemeMode.System;

        /// <summary>False before <see cref="Initialize"/> and in Classic mode</summary>
        public static bool Enabled { get; private set; }

        public static bool IsDark => Enabled && Current.IsDark;

        /// <summary>Raised on the UI thread after the palette changed and open windows were restyled</summary>
        public static event EventHandler Changed;

        /// <summary>
        /// Font used for UI text, replacing the WinForms default MS Sans Serif
        /// </summary>
        public static Font UiFont
        {
            get
            {
                if (mUiFont == null)
                {
                    using (var probe = new Font("Segoe UI", 8.25F))
                        mUiFont = probe.Name == "Segoe UI" ? new Font("Segoe UI", 8.25F) : Control.DefaultFont;
                }
                return mUiFont;
            }
        }

        private static string SettingsFile => Path.Combine(AwbDirs.UserData, "Theme.txt");

        /// <summary>
        /// Loads the saved mode and starts styling windows. Call once at startup, before creating forms.
        /// </summary>
        public static void Initialize()
        {
            if (CbtProc != null || Globals.UsingMono)
                return;

            Mode = LoadMode();
            UpdatePalette();

            CbtProc = OnCbt;
            CbtHook = ThemeNative.SetWindowsHookEx(WH_CBT, CbtProc, IntPtr.Zero, ThemeNative.GetCurrentThreadId());
            Application.Idle += StyleUnstyledForms;
            ToolStripManager.Renderer = Enabled ? new ModernToolStripRenderer() : ToolStripManager.Renderer;

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        }

        /// <summary>
        /// Switches mode, saves it, and restyles all open windows
        /// </summary>
        public static void SetMode(ThemeMode mode)
        {
            if (mode == Mode)
                return;

            bool wasEnabled = Enabled;
            Mode = mode;
            SaveMode(mode);
            UpdatePalette();

            if (wasEnabled != Enabled)
            {
                // Switching to or from the classic look cannot cleanly undo owner drawing
                MessageBox.Show("The new look will be fully applied after restarting AWB.", "Theme",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            RestyleAll();
        }

        /// <summary>
        /// Styles a form (and everything on it) right away, e.g. from its constructor to avoid a
        /// flash of the classic look. Forms are otherwise styled automatically.
        /// </summary>
        public static void Apply(Control control)
        {
            if (Enabled && control != null)
                ControlStyler.Style(control, true);
        }

        internal static bool IsPaletteColor(Color color)
        {
            return PaletteArgb.Contains(color.ToArgb());
        }

        private static void UpdatePalette()
        {
            Color accent = ThemePalette.ReadAccentColor();
            ThemePalette light = ThemePalette.CreateLight(accent);
            ThemePalette dark = ThemePalette.CreateDark(accent);

            // Neutral colours count as "theme defaults" that a restyle may replace; status colours
            // (Success, Highlight...) count as deliberate and are kept and drawn as-is
            PaletteArgb.Clear();
            foreach (ThemePalette p in new[] { light, dark })
            {
                foreach (Color c in new[]
                         {
                             p.Window, p.Surface, p.Popup, p.Input, p.Button, p.Hover, p.Pressed, p.Text,
                             p.MutedText, p.Border, p.StrongBorder, p.Selection
                         })
                    PaletteArgb.Add(c.ToArgb());
            }

            Enabled = Mode != ThemeMode.Classic;
            bool dark1 = Mode == ThemeMode.Dark || (Mode == ThemeMode.System && WindowsUsesDarkApps());
            Current = dark1 ? dark : light;
        }

        private static void RestyleAll()
        {
            foreach (Form form in Application.OpenForms)
                ControlStyler.Style(form, true);

            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static IntPtr OnCbt(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code == HCBT_ACTIVATE && Enabled)
            {
                try
                {
                    if (Control.FromHandle(wParam) is Form form)
                        ControlStyler.Style(form, false);
                }
                catch (Exception)
                {
                    // never let styling break window activation
                }
            }

            return ThemeNative.CallNextHookEx(CbtHook, code, wParam, lParam);
        }

        /// <summary>
        /// Fallback for windows shown without activation. Only checks the open-forms list,
        /// which is a handful of entries, so it is cheap to run on idle.
        /// </summary>
        private static void StyleUnstyledForms(object sender, EventArgs e)
        {
            if (!Enabled)
                return;

            try
            {
                for (int i = 0; i < Application.OpenForms.Count; i++)
                {
                    Form form = Application.OpenForms[i];
                    if (!ControlStyler.IsStyled(form))
                        ControlStyler.Style(form, false);
                }
            }
            catch (Exception)
            {
                // the collection can change while a form closes
            }
        }

        private static void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category != UserPreferenceCategory.General && e.Category != UserPreferenceCategory.Color)
                return;
            if (!Enabled)
                return;

            Form owner = Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null;
            if (owner == null || owner.IsDisposed)
                return;

            MethodInvoker refresh = () =>
            {
                bool wasDark = Current.IsDark;
                Color oldAccent = Current.Accent;
                UpdatePalette();
                if (wasDark != Current.IsDark || oldAccent != Current.Accent)
                    RestyleAll();
            };

            if (owner.InvokeRequired)
                owner.BeginInvoke(refresh);
            else
                refresh();
        }

        private static bool WindowsUsesDarkApps()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(
                           @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    return key?.GetValue("AppsUseLightTheme") is int light && light == 0;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static ThemeMode LoadMode()
        {
            try
            {
                if (File.Exists(SettingsFile) &&
                    Enum.TryParse(File.ReadAllText(SettingsFile).Trim(), true, out ThemeMode mode))
                    return mode;
            }
            catch (Exception)
            {
                // unreadable settings: use the default
            }

            return ThemeMode.System;
        }

        private static void SaveMode(ThemeMode mode)
        {
            try
            {
                File.WriteAllText(SettingsFile, mode.ToString());
            }
            catch (Exception)
            {
                // read-only profile: the choice just isn't remembered
            }
        }
    }
}
