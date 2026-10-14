using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Serialization;

[assembly: System.Reflection.AssemblyTitle("Click Goblin")]
[assembly: System.Reflection.AssemblyProduct("Click Goblin")]
[assembly: System.Reflection.AssemblyVersion("3.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.0.0.0")]

namespace ClickGoblin
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            bool first;
            using (Mutex mutex = new Mutex(true, "Local\\LightClick.Desktop.1", out first))
            {
                if (!first) { MessageBox.Show("Click Goblin (or an older LightClick) is already open.", "Click Goblin"); return 0; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new GoblinForm());
            }
            return 0;
        }
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT { public uint type; public MOUSEINPUT mouse; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct MOUSEINPUT
        {
            public int dx, dy;
            public uint mouseData, flags, time;
            public UIntPtr extraInfo;
        }
        [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
        [DllImport("user32.dll", SetLastError = true)] internal static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr window, int id);
        [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint period);
        [DllImport("dwmapi.dll")] internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        internal static INPUT[] Event(int button, bool down)
        {
            uint flag = button == 1 ? 8u : button == 2 ? 32u : button >= 3 ? 128u : 2u;
            return new INPUT[] { new INPUT { mouse = new MOUSEINPUT {
                flags = down ? flag : flag << 1, mouseData = button == 3 ? 1u : button == 4 ? 2u : 0u
            } } };
        }
        internal static Func<bool, bool> Sender(int button)
        {
            INPUT[] down = Event(button, true), up = Event(button, false);
            return delegate(bool pressed) { return SendInput(1, pressed ? down : up, Marshal.SizeOf(typeof(INPUT))) == 1; };
        }
        internal static Func<bool> AppGate()
        {
            uint target = 0, self = (uint)Process.GetCurrentProcess().Id;
            return delegate {
                uint foreground;
                GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                if (foreground == 0 || foreground == self) return false;
                if (target == 0) target = foreground;
                return foreground == target;
            };
        }
    }

    internal enum EndReason { Stopped, Limit, Duration, Blocked }

    // Wait handles and monotonic deadlines avoid spinning and keep input off the UI thread.
    internal sealed class ClickEngine : IDisposable
    {
        private readonly ManualResetEvent stop = new ManualResetEvent(false);
        private readonly AutoResetEvent changed = new AutoResetEvent(false);
        private readonly Func<bool, bool> send;
        private readonly Func<bool> gate;
        private readonly Action<EndReason> finished;
        private readonly int holdMs, delayMs, maxClicks, durationMs;
        private long intervalBits, count;
        private Thread worker;
        private volatile bool paused;
        internal long Count { get { return Interlocked.Read(ref count); } }
        internal bool Paused { get { return paused; } }
        internal double Interval { get { return BitConverter.Int64BitsToDouble(Interlocked.Read(ref intervalBits)); } }

        internal ClickEngine(double interval, int hold, int delay, int limit, int duration,
            Func<bool, bool> sender, Func<bool> allow, Action<EndReason> onFinished)
        {
            intervalBits = BitConverter.DoubleToInt64Bits(Math.Max(2, Math.Min(3600000, interval)));
            holdMs = hold; delayMs = delay; maxClicks = limit; durationMs = duration;
            send = sender; gate = allow; finished = onFinished;
        }
        internal void SetInterval(double milliseconds)
        {
            Interlocked.Exchange(ref intervalBits, BitConverter.DoubleToInt64Bits(Math.Max(2, Math.Min(3600000, milliseconds))));
            changed.Set();
        }
        internal void Start()
        {
            worker = new Thread(Run) { IsBackground = true, Name = "Click Goblin input" };
            worker.Start();
        }
        private void Run()
        {
            bool held = false, precision = false;
            EndReason reason = EndReason.Stopped;
            WaitHandle[] waits = { stop, changed };
            try
            {
                if (stop.WaitOne(delayMs)) return;
                Stopwatch clock = Stopwatch.StartNew();
                double next = Interval;
                while (!stop.WaitOne(0))
                {
                    double now = clock.Elapsed.TotalMilliseconds;
                    double remaining = durationMs > 0 ? durationMs - now : double.MaxValue;
                    if (remaining <= 0) { reason = EndReason.Duration; break; }
                    if (Interval <= 50 && !precision) precision = Native.timeBeginPeriod(1) == 0;
                    else if (Interval > 50 && precision) { Native.timeEndPeriod(1); precision = false; }
                    if (gate != null && !gate())
                    {
                        paused = true;
                        if (WaitHandle.WaitAny(waits, (int)Math.Ceiling(Math.Min(100, remaining))) == 0) break;
                        continue;
                    }
                    if (paused) { paused = false; next = now; }
                    double wait = Math.Min(next - now, remaining);
                    if (wait > 0)
                    {
                        int result = WaitHandle.WaitAny(waits, (int)Math.Ceiling(wait));
                        if (result == 0) break;
                        if (result == 1) next = clock.Elapsed.TotalMilliseconds + Interval;
                        continue;
                    }
                    // Foreground can change while waiting for a long interval.
                    if (gate != null && !gate()) { paused = true; continue; }
                    if (durationMs > 0 && clock.Elapsed.TotalMilliseconds >= durationMs) { reason = EndReason.Duration; break; }
                    if (!send(true)) { reason = EndReason.Blocked; break; }
                    held = true;
                    double pressedAt = clock.Elapsed.TotalMilliseconds;
                    int hold = Math.Max(1, Math.Min(holdMs, (int)(Interval / 2)));
                    if (durationMs > 0) hold = Math.Min(hold, Math.Max(1, (int)(durationMs - pressedAt)));
                    if (stop.WaitOne(hold)) break;
                    if (!send(false)) { reason = EndReason.Blocked; break; }
                    held = false;
                    long total = Interlocked.Increment(ref count);
                    if (maxClicks > 0 && total >= maxClicks) { reason = EndReason.Limit; break; }
                    next = pressedAt + Interval;
                    // Never emit catch-up bursts after the OS stalls.
                    if (next < clock.Elapsed.TotalMilliseconds) next = clock.Elapsed.TotalMilliseconds + 1;
                }
            }
            catch { reason = EndReason.Blocked; }
            finally
            {
                if (held) { try { send(false); } catch { } }
                if (precision) Native.timeEndPeriod(1);
                if (finished != null) finished(reason);
            }
        }
        public void Dispose()
        {
            stop.Set();
            if (worker != null) worker.Join();
            stop.Dispose(); changed.Dispose();
        }
    }

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

    internal static class HotkeyRules
    {
        internal static bool Valid(int data)
        {
            Keys key = (Keys)data & Keys.KeyCode;
            bool modified = ((Keys)data & (Keys.Control | Keys.Alt)) != 0;
            return (key >= Keys.F1 && key <= Keys.F24) ||
                (modified && ((key >= Keys.A && key <= Keys.Z) || (key >= Keys.D0 && key <= Keys.D9) || key == Keys.Up || key == Keys.Down || key == Keys.Oemplus || key == Keys.OemMinus));
        }
        internal static string Text(int data) { return new KeysConverter().ConvertToString((Keys)data); }
        internal static uint Modifiers(int data)
        {
            return 0x4000u | (((Keys)data & Keys.Control) != 0 ? 2u : 0u) |
                (((Keys)data & Keys.Alt) != 0 ? 1u : 0u) | (((Keys)data & Keys.Shift) != 0 ? 4u : 0u);
        }
    }
}
