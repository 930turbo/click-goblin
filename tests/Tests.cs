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
        static double Luma(Color color)
        {
            double[] c = { color.R / 255.0, color.G / 255.0, color.B / 255.0 };
            for (int i = 0; i < 3; i++) c[i] = c[i] <= 0.04045 ? c[i] / 12.92 : Math.Pow((c[i] + 0.055) / 1.055, 2.4);
            return c[0] * 0.2126 + c[1] * 0.7152 + c[2] * 0.0722;
        }
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
                HotkeyMatcher matcher = new HotkeyMatcher(new int[] { (int)Keys.F6, (int)(Keys.Control | Keys.Up) });
                int action;
                Check(matcher.Process((int)Keys.F6, true, 0, out action) && action == 0, "Shortcut consumed and dispatched");
                Check(matcher.Process((int)Keys.F6, true, 0, out action) && action == -1, "Held shortcut does not repeat");
                Check(matcher.Process((int)Keys.F6, false, 0, out action), "Shortcut release consumed");
                Check(!matcher.Process((int)Keys.A, true, 0, out action), "Unrelated key passes through");
                Check(!matcher.Process((int)Keys.Up, true, 0, out action), "Wrong modifiers pass through");
                Check(matcher.Process((int)Keys.Up, true, 2, out action) && action == 1, "Modified shortcut consumed");
                Check(matcher.Process((int)Keys.Up, false, 0, out action), "Release consumed after modifier lifted");
                Check(!new Preferences().ToggleSound && new Preferences().PriorityHotkeys, "Sound off and shortcut priority on by default");
                for (int a = 0; a < 12; a++) for (int bg = 0; bg < 6; bg++)
                {
                    Theme.Palette(a, bg);
                    Check((Luma(Theme.Muted) + 0.05) / (Luma(Theme.Field) + 0.05) >= 4.5, "Readable muted and disabled text in every palette");
                }
                Theme.Palette(0, 0);
                using (System.IO.Stream fontLicense = typeof(MainForm).Assembly.GetManifestResourceStream("Outfit-OFL.txt")) Check(fontLicense != null, "Font license embedded");
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
                using (ManualResetEvent done = new ManualResetEvent(false))
                {
                    int[] presses = new int[3], releases = new int[3];
                    long[] starts = new long[3]; Stopwatch time = Stopwatch.StartNew();
                    using (ClickEngine e = new ClickEngine(150, 10, 0, 1, 0, delegate(bool d) { throw new Exception("Wrong sender"); }, null,
                        delegate(EndReason r) { done.Set(); }, 3, delegate(int channel, bool downEvent) {
                            if (downEvent) { presses[channel]++; starts[channel] = time.ElapsedMilliseconds; } else releases[channel]++;
                            return true;
                        }))
                    {
                        e.Start(); Check(done.WaitOne(3000), "Staggered group completes");
                        for (int i = 0; i < 3; i++) Check(presses[i] == 1 && releases[i] == 1, "Each staggered button released");
                        Check(starts[1] - starts[0] >= 30 && starts[2] - starts[1] >= 30, "Staggered phase spacing");
                        Check(e.Count == 1, "Group counts as one cycle");
                    }
                }
                using (ManualResetEvent pressed = new ManualResetEvent(false))
                {
                    int releases = 0;
                    using (ClickEngine e = new ClickEngine(300, 50, 0, 0, 0, null, null, null, 2,
                        delegate(int channel, bool d) { if (d) pressed.Set(); else releases++; return true; }))
                    { e.Start(); Check(pressed.WaitOne(3000), "Multi-button press"); }
                    Check(releases == 1, "Multi-button stop releases held channel");
                }
                using (ManualResetEvent done = new ManualResetEvent(false))
                {
                    int downs = 0, ups = 0;
                    using (ClickEngine e = new ClickEngine(10, 1, 0, 2, 0,
                        delegate(bool d) { if (d) downs += 3; else ups += 3; return true; }, null, delegate(EndReason r) { done.Set(); }))
                    { e.Start(); Check(done.WaitOne(3000) && downs == 6 && ups == 6 && e.Count == 2, "Together group cycle limit"); }
                }
                using (ManualResetEvent secondHeld = new ManualResetEvent(false))
                {
                    int[] ups = new int[2];
                    using (ClickEngine e = new ClickEngine(100, 20, 0, 0, 0, null, null, null, 2,
                        delegate(int channel, bool d) { if (d && channel == 1) secondHeld.Set(); if (!d) ups[channel]++; return true; }))
                    { e.Start(); Check(secondHeld.WaitOne(3000), "Second staggered channel held"); }
                    Check(ups[0] == 1 && ups[1] == 1, "Stopping second channel releases exact held button");
                }
                using (ManualResetEvent firstDone = new ManualResetEvent(false))
                using (ManualResetEvent finished = new ManualResetEvent(false))
                {
                    using (ClickEngine e = new ClickEngine(60000, 1, 0, 1, 0, null, null, delegate(EndReason r) { finished.Set(); }, 2,
                        delegate(int channel, bool d) { if (!d && channel == 0) firstDone.Set(); return true; }))
                    {
                        e.Start(); e.SetInterval(20); Check(firstDone.WaitOne(3000), "Staggered first channel");
                        e.SetInterval(10); Check(finished.WaitOne(3000), "Staggered live speed wakes phase wait");
                    }
                }
                string path = Path.Combine(root, "settings.xml");
                Preferences p = new Preferences { Seconds = 0.002m, DurationSeconds = 120, TargetOnly = true, SpeedStep = 10, FasterKey = (int)(Keys.Control | Keys.Up),
                    MultiEnabled = true, Staggered = true, ButtonMask = 21, AccentIndex = 11, BackgroundIndex = 5, MinimizeToTray = true, ToggleSound = true, PriorityHotkeys = false };
                Check(p.Save(path), "Settings saved");
                Preferences saved = Preferences.Load(path);
                Check(saved.Seconds == 0.002m && saved.DurationSeconds == 120 && saved.TargetOnly && saved.SpeedStep == 10 && saved.FasterKey == (int)(Keys.Control | Keys.Up), "Settings round trip");
                Check(saved.MultiEnabled && saved.Staggered && saved.ButtonMask == 21 && saved.AccentIndex == 11 && saved.BackgroundIndex == 5 && saved.MinimizeToTray && saved.ToggleSound && !saved.PriorityHotkeys, "Multi-button, feedback and appearance settings persist");
                File.WriteAllText(path, "bad xml"); Check(Preferences.Load(path).Seconds == 0.5m, "Corrupt preferences fallback");
                if (!skipUi)
                {
                string uiPath = Path.Combine(root, "ui.xml");
                using (MainForm f = new MainForm(uiPath))
                {
                    f.Show(); Check((bool)Field(f, "bound"), "Default hotkeys bind");
                    Check(((GlobalHotkeys)Field(f, "hotkeyFilter")).Ready, "Shortcut consumption hook installed");
                    Shot(f, Path.Combine(args[0], "clicking.png"));
                    Call(f, "ShowPage", true); Shot(f, Path.Combine(args[0], "hotkeys.png"));
                    Call(f, "ShowPage", false);
                    Call(f, "SelectPage", 1);
                    ((ToggleSwitch)Field(f, "multi")).Checked = true;
                    Shot(f, Path.Combine(args[0], "gang.png"));
                    Check(!((ChoiceField)Field(f, "buttonChoice")).Enabled, "Multi mode replaces single selector");
                    ToggleSwitch[] gangButtons = (ToggleSwitch[])Field(f, "multiButtons");
                    foreach (ToggleSwitch box in gangButtons) box.Checked = false;
                    Check(!((Button)Field(f, "toggle")).Enabled, "Empty group cannot start");
                    gangButtons[0].Checked = gangButtons[1].Checked = true;
                    ((ToggleSwitch)Field(f, "multi")).Checked = false;
                    Shot(f, Path.Combine(args[0], "gang-disabled.png"));
                    Call(f, "SelectPage", 3);
                    Shot(f, Path.Combine(args[0], "settings.png"));
                    ((ChoiceField)Field(f, "accentChoice")).SelectedIndex = 1;
                    Check(Theme.Accent.G > Theme.Accent.R, "Accent changes live");
                    ((ChoiceField)Field(f, "backgroundChoice")).SelectedIndex = 2;
                    Check(Theme.Background.B > Theme.Background.R, "Background changes live");
                    ((ChoiceField)Field(f, "accentChoice")).SelectedIndex = 0;
                    ((ChoiceField)Field(f, "backgroundChoice")).SelectedIndex = 0;
                    ((ToggleSwitch)Field(f, "toTray")).Checked = true;
                    f.WindowState = FormWindowState.Minimized;
                    Check(!f.Visible && ((NotifyIcon)Field(f, "tray")).Visible, "Minimize to tray");
                    Call(f, "RestoreWindow"); Check(f.Visible && !((NotifyIcon)Field(f, "tray")).Visible, "Restore from tray");
                    f.Close(); Check(!f.IsDisposed && !f.Visible && ((NotifyIcon)Field(f, "tray")).Visible, "X closes to tray when enabled");
                    Call(f, "RestoreWindow");
                    ((ToggleSwitch)Field(f, "toTray")).Checked = false;
                    Call(f, "ShowPage", false);
                    ChoiceField picker = (ChoiceField)Field(f, "buttonChoice");
                    picker.OpenMenu(); ToolStripDropDown menu = (ToolStripDropDown)Field(picker, "popup");
                    Check(menu.Visible, "Custom picker opens");
                    Control list = ((ToolStripControlHost)menu.Items[0]).Control;
                    using (Bitmap image = new Bitmap(menu.Width, menu.Height)) { menu.DrawToBitmap(image, new Rectangle(0, 0, menu.Width, menu.Height)); image.Save(Path.Combine(args[0], "dropdown.png")); }
                    Call(list, "OnKeyDown", new KeyEventArgs(Keys.Down)); Call(list, "OnKeyDown", new KeyEventArgs(Keys.Enter));
                    Check(picker.SelectedIndex == 1 && !menu.Visible, "Custom picker keyboard selection"); picker.SelectedIndex = 0;
                    NumberField rate = (NumberField)Field(f, "cps");
                    NumberField interval = (NumberField)Field(f, "interval");
                    ((TextBox)Field(rate, "edit")).Text = "invalid"; rate.Commit(); Check(rate.Value == 2, "Invalid numeric entry restores current value");
                    rate.Value = 100; Check(interval.Value == 0.01m, "CPS to interval");
                    interval.Value = 0.5m; Check(rate.Value == 2, "Interval to CPS");
                    interval.Value = 3600; Check(rate.Value < 0.001m, "Long interval displays matching fractional CPS");
                    interval.Value = 0.5m;
                    HotkeyBox[] boxes = (HotkeyBox[])Field(f, "keys");
                    boxes[1].SetBinding(boxes[0].Binding); Call(f, "Bind");
                    Check(!(bool)Field(f, "bound"), "Duplicate hotkeys prevent start");
                    boxes[1].SetBinding((int)(Keys.Control | Keys.Up)); Call(f, "Bind");
                    Check((bool)Field(f, "bound"), "Custom combination binds");
                    ((NumberField)Field(f, "delay")).Value = 60;
                    Call(f, "Toggle"); Check(Field(f, "engine") != null, "Start");
                    Check(rate.Enabled && !boxes[0].Enabled, "Live rate enabled; hotkey editing locked");
                    Call(f, "AdjustSpeed", 1); Check(rate.Value == 7, "Live faster hotkey step");
                    Call(f, "AdjustSpeed", -1); Check(rate.Value == 2, "Live slower hotkey step");
                    rate.Value = 500; Call(f, "AdjustSpeed", 1); Check(rate.Value == 500, "Maximum CPS clamp");
                    rate.Value = 0.01m; Call(f, "AdjustSpeed", -1); Check(rate.Value == 0.01m, "Minimum adjustment clamp");
                    Call(f, "Stop"); Check(Field(f, "engine") == null, "Stop");
                    ((ToggleSwitch)Field(f, "toTray")).Checked = true;
                    Call(f, "Toggle"); f.Close(); Check(Field(f, "engine") != null && !f.Visible, "Close-to-tray preserves active countdown");
                    Call(f, "QuitApplication"); Check(Field(f, "engine") == null && f.IsDisposed, "Quit stops input and exits even in tray mode");
                }
                }
                Console.WriteLine("PASS: native input, mouse buttons, single and grouped cycles, staggered phases, release on stop, app gate, limits, live speed and preferences. No real clicks generated.");
                Console.WriteLine(skipUi ? "UI checks skipped (CI mode)." : "PASS: UI, hotkey conflicts, CPS sync and close.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        }
    }
}
