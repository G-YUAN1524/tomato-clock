using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace TomatoClock
{
    internal sealed class AppSettings
    {
        public int FocusSeconds = 1500;
        public int CustomSeconds = 0;
        public bool Pinned = true;
        public double Left = double.NaN;
        public double Top = double.NaN;

        private static string SettingsFolder
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "番茄时钟");
            }
        }

        private static string SettingsPath
        {
            get { return Path.Combine(SettingsFolder, "settings.ini"); }
        }

        public static AppSettings Load()
        {
            AppSettings settings = new AppSettings();
            try
            {
                if (!File.Exists(SettingsPath)) return settings;

                string[] lines = File.ReadAllLines(SettingsPath, Encoding.UTF8);
                foreach (string raw in lines)
                {
                    if (raw == null) continue;
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;

                    int split = line.IndexOf('=');
                    if (split <= 0) continue;
                    string key = line.Substring(0, split).Trim();
                    string value = line.Substring(split + 1).Trim();

                    switch (key)
                    {
                        case "FocusSeconds":
                            settings.FocusSeconds = Clamp(ParseInt(value, 1500), 1, 10800);
                            break;
                        case "CustomSeconds":
                            settings.CustomSeconds = Clamp(ParseInt(value, 0), 0, 10800);
                            break;
                        case "Pinned":
                            settings.Pinned = string.Equals(value, "1", StringComparison.Ordinal) ||
                                              string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
                            break;
                        case "Left":
                            settings.Left = ParseDouble(value, double.NaN);
                            break;
                        case "Top":
                            settings.Top = ParseDouble(value, double.NaN);
                            break;
                    }
                }
            }
            catch
            {
                // Settings are a convenience; a damaged file must never stop the timer.
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                List<string> lines = new List<string>();
                lines.Add("# 番茄时钟设置");
                lines.Add("FocusSeconds=" + FocusSeconds.ToString(CultureInfo.InvariantCulture));
                lines.Add("CustomSeconds=" + CustomSeconds.ToString(CultureInfo.InvariantCulture));
                lines.Add("Pinned=" + (Pinned ? "1" : "0"));
                lines.Add("Left=" + Left.ToString("0.##", CultureInfo.InvariantCulture));
                lines.Add("Top=" + Top.ToString("0.##", CultureInfo.InvariantCulture));

                string temp = SettingsPath + ".tmp";
                File.WriteAllLines(temp, lines.ToArray(), new UTF8Encoding(false));
                if (File.Exists(SettingsPath))
                {
                    try
                    {
                        File.Replace(temp, SettingsPath, null);
                    }
                    catch
                    {
                        File.Delete(SettingsPath);
                        File.Move(temp, SettingsPath);
                    }
                }
                else
                {
                    File.Move(temp, SettingsPath);
                }
            }
            catch
            {
                // Ignore read-only or locked profile directories.
            }
        }

        private static int ParseInt(string value, int fallback)
        {
            int parsed;
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static double ParseDouble(string value, double fallback)
        {
            double parsed;
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
                ? parsed
                : fallback;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}
