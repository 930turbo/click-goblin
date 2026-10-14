using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace ClickGoblin
{
    internal static class Tests
    {
        static void Check(bool condition, string name) { if (!condition) throw new Exception(name); }
        static object Field(object o, string name) { return o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o); }
        static object Call(object o, string name, params object[] args) { return o.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(o, args); }
        static void Shot(Form f, string path) { using (Bitmap b = new Bitmap(f.Width, f.Height)) { f.DrawToBitmap(b, new Rectangle(0, 0, f.Width, f.Height)); b.Save(path); } }
        [STAThread]
        static int Main(string[] args)
        {
            try
            {
                bool skipUi = Array.IndexOf(args, "--skip-ui") >= 0;
                if (!skipUi) { Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); }
                Check(Marshal.SizeOf(typeof(Native.INPUT)) == (IntPtr.Size == 8 ? 40 : 28), "Native input ABI");
                uint[] down = { 2, 8, 32, 128, 128 };
                for (int i = 0; i < 5; i++)
                {
                    Check(Native.Event(i, true)[0].mouse.flags == down[i] && Native.Event(i, false)[0].mouse.flags == down[i] * 2, "Mouse button pairing");
                    Check(Native.Event(i, true)[0].mouse.mouseData == (i == 3 ? 1u : i == 4 ? 2u : 0u), "Side button data");
                }
                Check(HotkeyRules.Valid((int)(Keys.Control | Keys.Shift | Keys.G)) && HotkeyRules.Valid((int)Keys.F6) && !HotkeyRules.Valid((int)Keys.G), "Hotkey validation");
                using (ManualResetEvent done = new ManualResetEvent(false))
                {
                    int presses = 0, releases = 0; EndReason reason = EndReason.Stopped;
                    using (ClickEngine e = new ClickEngine(10, 3, 0, 7, 0,
                        delegate(bool pressed) { if (pressed) presses++; else releases++; return true; }, null,
                        delegate(EndReason r) { reason = r; done.Set(); }))
                    {
                        e.Start(); Check(done.WaitOne(3000), "Finite loop completion");
                        Check(e.Count == 7 && presses == 7 && releases == 7 && reason == EndReason.Limit, "Exact click limit");
                    }
                }
                using (ManualResetEvent pressed = new ManualResetEvent(false))
                {
                    int releases = 0;
                    using (ClickEngine e = new ClickEngine(50, 1000, 0, 0, 0,
                        delegate(bool d) { if (d) pressed.Set(); else releases++; return true; }, null, null))
                    { e.Start(); Check(pressed.WaitOne(3000), "Press"); }
                    Check(releases == 1, "Stop while held releases once");
                }
                int delayed = 0;
                using (ClickEngine e = new ClickEngine(10, 1, 60000, 0, 0, delegate(bool d) { delayed++; return true; }, null, null))
                { e.Start(); }
                Check(delayed == 0, "Cancel countdown without input");
                using (ManualResetEvent done = new ManualResetEvent(false))
                {
                    EndReason reason = EndReason.Stopped;
                    using (ClickEngine e = new ClickEngine(10, 1, 0, 0, 200, delegate(bool d) { return true; }, null,
                        delegate(EndReason r) { reason = r; done.Set(); }))
                    { e.Start(); Check(done.WaitOne(3000) && reason == EndReason.Duration, "Duration limit"); }
                }
                using (ManualResetEvent done = new ManualResetEvent(false))
                {
                    EndReason reason = EndReason.Stopped;
                    using (ClickEngine e = new ClickEngine(10, 1, 0, 0, 0, delegate(bool d) { return false; }, null,
                        delegate(EndReason r) { reason = r; done.Set(); }))
                    { e.Start(); Check(done.WaitOne(3000) && reason == EndReason.Blocked, "Blocked input"); }
                }
                using (ManualResetEvent done = new ManualResetEvent(false))
                {
                    int allowed = 0;
                    using (ClickEngine e = new ClickEngine(10, 1, 0, 3, 0, delegate(bool d) { return true; },
                        delegate { return Interlocked.CompareExchange(ref allowed, 0, 0) == 1; }, delegate(EndReason r) { done.Set(); }))
                    {
                        e.Start(); Check(!done.WaitOne(140) && e.Count == 0, "App gate blocks clicks");
                        Interlocked.Exchange(ref allowed, 1);
                        Check(done.WaitOne(3000) && e.Count == 3, "App gate resumes");
                    }
                }
                using (ManualResetEvent done = new ManualResetEvent(false))
                {
                    using (ClickEngine e = new ClickEngine(60000, 1, 0, 1, 0, delegate(bool d) { return true; }, null, delegate(EndReason r) { done.Set(); }))
                    { e.Start(); e.SetInterval(5); Check(done.WaitOne(3000) && e.Count == 1, "Live speed change wakes sleeping worker"); }
                }
                using (ManualResetEvent done = new ManualResetEvent(false))
                {
                    Stopwatch timer = Stopwatch.StartNew();
                    using (ClickEngine e = new ClickEngine(2, 1, 0, 0, 500, delegate(bool d) { return true; }, null, delegate(EndReason r) { done.Set(); }))
                    {
                        e.Start(); Check(done.WaitOne(3000), "High CPS test completes");
                        Check(e.Count > 20, "High CPS makes progress");
                        Console.WriteLine("High-speed fake-input sample: " + e.Count + " clicks in " + timer.ElapsedMilliseconds + " ms (500 CPS requested).");
                    }
                }
                string root = Path.Combine(args[0], "preferences-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
                string path = Path.Combine(root, "settings.xml");
                Preferences p = new Preferences { Seconds = 0.002m, DurationSeconds = 120, TargetOnly = true, SpeedStep = 10, FasterKey = (int)(Keys.Control | Keys.Up) };
                Check(p.Save(path), "Settings saved");
                Preferences saved = Preferences.Load(path);
                Check(saved.Seconds == 0.002m && saved.DurationSeconds == 120 && saved.TargetOnly && saved.SpeedStep == 10 && saved.FasterKey == (int)(Keys.Control | Keys.Up), "Settings round trip");
                File.WriteAllText(path, "bad xml"); Check(Preferences.Load(path).Seconds == 0.5m, "Corrupt preferences fallback");
                if (!skipUi)
                {
                string uiPath = Path.Combine(root, "ui.xml");
                using (MainForm f = new MainForm(uiPath))
                {
                    f.Show(); Check((bool)Field(f, "bound"), "Default hotkeys bind");
                    Shot(f, Path.Combine(args[0], "clicking.png"));
                    Call(f, "ShowPage", true); Shot(f, Path.Combine(args[0], "hotkeys.png"));
                    Call(f, "ShowPage", false);
                    NumericUpDown rate = (NumericUpDown)Field(f, "cps");
                    NumericUpDown interval = (NumericUpDown)Field(f, "interval");
                    rate.Value = 100; Check(interval.Value == 0.01m, "CPS to interval");
                    interval.Value = 0.5m; Check(rate.Value == 2, "Interval to CPS");
                    interval.Value = 3600; Check(rate.Value < 0.001m, "Long interval displays matching fractional CPS");
                    interval.Value = 0.5m;
                    HotkeyBox[] boxes = (HotkeyBox[])Field(f, "keys");
                    boxes[1].SetBinding(boxes[0].Binding); Call(f, "Bind");
                    Check(!(bool)Field(f, "bound"), "Duplicate hotkeys prevent start");
                    boxes[1].SetBinding((int)(Keys.Control | Keys.Up)); Call(f, "Bind");
                    Check((bool)Field(f, "bound"), "Custom combination binds");
                    ((NumericUpDown)Field(f, "delay")).Value = 60;
                    Call(f, "Toggle"); Check(Field(f, "engine") != null, "Start");
                    Check(rate.Enabled && !boxes[0].Enabled, "Live rate enabled; hotkey editing locked");
                    Call(f, "AdjustSpeed", 1); Check(rate.Value == 7, "Live faster hotkey step");
                    Call(f, "AdjustSpeed", -1); Check(rate.Value == 2, "Live slower hotkey step");
                    rate.Value = 500; Call(f, "AdjustSpeed", 1); Check(rate.Value == 500, "Maximum CPS clamp");
                    rate.Value = 0.01m; Call(f, "AdjustSpeed", -1); Check(rate.Value == 0.01m, "Minimum adjustment clamp");
                    Call(f, "Stop"); Check(Field(f, "engine") == null, "Stop");
                    Call(f, "Toggle"); f.Close(); Check(Field(f, "engine") == null, "Close cancels countdown");
                }
                }
                Console.WriteLine("PASS: native input, mouse buttons, timing, click limits, cancellation/release, app gate, duration, live speed, high CPS and preferences. No real clicks generated.");
                Console.WriteLine(skipUi ? "UI checks skipped (CI mode)." : "PASS: UI, hotkey conflicts, CPS sync and close.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
