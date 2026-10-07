using System;
using System.Runtime.InteropServices;

namespace WikiFunctions.Theming
{
    /// <summary>
    /// Win32 calls used by the theme. All of them fail silently on systems that lack the feature.
    /// </summary>
    internal static class ThemeNative
    {
        internal const int EM_SETBKGNDCOLOR = 0x0443;
        internal const int PBM_SETBARCOLOR = 0x0409;
        internal const int PBM_SETBKCOLOR = 0x2001;
        internal const int LVM_GETHEADER = 0x101F;

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWCP_ROUNDSMALL = 3;
        private const int DWMWCP_ROUND = 2;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

        internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, int dwThreadId);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll")]
        internal static extern int GetCurrentThreadId();

        internal static void SetTheme(IntPtr handle, string theme)
        {
            try
            {
                SetWindowTheme(handle, theme, null);
            }
            catch (Exception)
            {
                // uxtheme unavailable (e.g. classic mode)
            }
        }

        /// <summary>
        /// Removes visual styles so classic colour messages (e.g. PBM_SETBARCOLOR) take effect
        /// </summary>
        internal static void RemoveTheme(IntPtr handle)
        {
            try
            {
                SetWindowTheme(handle, "", "");
            }
            catch (Exception)
            {
            }
        }

        internal static void SetDarkTitleBar(IntPtr handle, bool dark)
        {
            try
            {
                int value = dark ? 1 : 0;
                if (DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref value, sizeof(int)) != 0)
                    DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref value, sizeof(int));
            }
            catch (Exception)
            {
                // dwmapi missing or attribute unsupported
            }
        }

        /// <summary>
        /// Windows 11 rounded corners for popups. Ignored on Windows 10.
        /// </summary>
        internal static void SetRoundedCorners(IntPtr handle, bool small)
        {
            try
            {
                int value = small ? DWMWCP_ROUNDSMALL : DWMWCP_ROUND;
                DwmSetWindowAttribute(handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref value, sizeof(int));
            }
            catch (Exception)
            {
            }
        }
    }
}
