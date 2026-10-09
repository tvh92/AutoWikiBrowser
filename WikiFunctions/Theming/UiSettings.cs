using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WikiFunctions.Theming
{
    /// <summary>
    /// Per-user appearance settings (theme, text size, panel layout), kept separately from AWB's
    /// settings XML so they apply to every settings file. Stored as key=value lines.
    /// </summary>
    public static class UiSettings
    {
        private static Dictionary<string, string> mValues;

        private static string FilePath => Path.Combine(AwbDirs.UserData, "Appearance.ini");

        private static Dictionary<string, string> Values
        {
            get
            {
                if (mValues != null)
                    return mValues;

                mValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    if (File.Exists(FilePath))
                    {
                        foreach (string line in File.ReadAllLines(FilePath))
                        {
                            int eq = line.IndexOf('=');
                            if (eq > 0)
                                mValues[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                        }
                    }
                }
                catch (Exception)
                {
                    // unreadable file: start from defaults
                }
                return mValues;
            }
        }

        public static string Get(string key, string fallback)
        {
            return Values.TryGetValue(key, out string value) ? value : fallback;
        }

        public static int GetInt(string key, int fallback)
        {
            return int.TryParse(Get(key, null), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                ? value
                : fallback;
        }

        public static void Set(string key, string value)
        {
            Values[key] = value;
            Save();
        }

        public static void SetInt(string key, int value)
        {
            Set(key, value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Save()
        {
            try
            {
                File.WriteAllLines(FilePath, Values.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value));
            }
            catch (Exception)
            {
                // read-only profile: settings just aren't remembered
            }
        }
    }
}
