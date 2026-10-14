using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace ClickGoblin
{
    public sealed class Preferences
    {
        public int Button = 0;
        public decimal Seconds = 0.5m;
        public int HoldMs = 50, DelaySeconds = 0, MaxClicks = 0, DurationSeconds = 0;
        public bool AlwaysOnTop = false, TargetOnly = false;
        public decimal SpeedStep = 5;
        public int ToggleKey = (int)Keys.F6, FasterKey = (int)Keys.F8, SlowerKey = (int)Keys.F7, StopKey = (int)Keys.F10;
        internal static string PathName { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClickGoblin", "settings.xml"); } }
        internal static Preferences Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    using (FileStream stream = File.OpenRead(path))
                    {
                        Preferences s = (Preferences)new XmlSerializer(typeof(Preferences)).Deserialize(stream);
                        s.Normalize(); return s;
                    }
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            catch (InvalidOperationException) { }
            return new Preferences();
        }
        internal static Preferences LoadWithMigration()
        {
            if (File.Exists(PathName)) return Load(PathName);
            Preferences s = new Preferences();
            try
            {
                string[] lines = File.ReadAllLines(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LightClick", "settings.txt"));
                int n; decimal d;
                if (lines.Length > 0 && int.TryParse(lines[0], out n)) s.Button = n;
                if (lines.Length > 1 && decimal.TryParse(lines[1], NumberStyles.Number, CultureInfo.InvariantCulture, out d)) s.Seconds = d;
                if (lines.Length > 2 && int.TryParse(lines[2], out n)) s.HoldMs = n;
                if (lines.Length > 3 && int.TryParse(lines[3], out n)) s.DelaySeconds = n;
                if (lines.Length > 4 && int.TryParse(lines[4], out n)) s.MaxClicks = n;
                // Keep the new independent hotkeys free of duplicate legacy bindings.
                s.AlwaysOnTop = lines.Length > 6 && lines[6] == "1";
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            s.Normalize(); return s;
        }
        internal void Normalize()
        {
            Button = Math.Max(0, Math.Min(4, Button));
            Seconds = Math.Max(0.002m, Math.Min(3600, Seconds));
            HoldMs = Math.Max(1, Math.Min(1000, HoldMs));
            DelaySeconds = Math.Max(0, Math.Min(60, DelaySeconds));
            MaxClicks = Math.Max(0, Math.Min(1000000, MaxClicks));
            DurationSeconds = Math.Max(0, Math.Min(86400, DurationSeconds));
            SpeedStep = Math.Max(0.1m, Math.Min(100, SpeedStep));
            if (!HotkeyRules.Valid(ToggleKey)) ToggleKey = (int)Keys.F6;
            if (!HotkeyRules.Valid(FasterKey)) FasterKey = (int)Keys.F8;
            if (!HotkeyRules.Valid(SlowerKey)) SlowerKey = (int)Keys.F7;
            if (!HotkeyRules.Valid(StopKey)) StopKey = (int)Keys.F10;
        }
        internal bool Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (FileStream stream = File.Create(path + ".tmp")) new XmlSerializer(typeof(Preferences)).Serialize(stream, this);
                if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                else File.Move(path + ".tmp", path);
                return true;
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }

}
