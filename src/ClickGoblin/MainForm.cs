using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClickGoblin
{
    internal sealed class MainForm : Form
    {
        private readonly ComboBox buttonChoice = new ComboBox();
        private readonly NumericUpDown cps = new RateControl(), interval = new NumericUpDown(), hold = new NumericUpDown();
        private readonly NumericUpDown delay = new NumericUpDown(), limit = new NumericUpDown(), duration = new NumericUpDown(), step = new NumericUpDown();
        private readonly CheckBox target = new CheckBox(), top = new CheckBox();
        private readonly HotkeyBox[] keys = { new HotkeyBox(), new HotkeyBox(), new HotkeyBox(), new HotkeyBox() };
        private readonly Panel clicking = new Panel(), shortcuts = new Panel();
        private readonly Button clickTab = new Button(), keysTab = new Button();
        private readonly ActionButton toggle = new ActionButton();
        private readonly LinkLabel reset = new LinkLabel();
        private readonly Label status, detail, counter, elapsed, timingHint, badge, bindingHint;
        private readonly System.Windows.Forms.Timer refresh = new System.Windows.Forms.Timer();
        private readonly ToolTip tips = new ToolTip();
        private ClickEngine engine;
        private bool running, bound, loading, syncing, editingKeys;
        private decimal seconds = 0.5m;
        private DateTime started;
        private long lastCount;
        private string testSettingsPath;
        private string SettingsPath { get { return testSettingsPath ?? Preferences.PathName; } }

        internal MainForm() : this(null) { }
        // Isolated preferences path makes UI tests non-destructive.
        internal MainForm(string preferencesPath)
        {
            testSettingsPath = preferencesPath;
            Text = "Click Goblin - Stopped";
            ClientSize = new Size(580, 866);
            AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.Sizable; MinimumSize = new Size(600, 520);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Background; ForeColor = Theme.Ink;
            Font = new Font("Segoe UI", 9.5f); DoubleBuffered = true;
            Icon = Icon.ExtractAssociatedIcon(typeof(MainForm).Assembly.Location);
            Controls.Add(new BrandMark { Location = new Point(22, 19), Size = new Size(40, 40) });
            Theme.Label(this, "Click Goblin", 74, 14, 315, 23, true, Theme.Ink).Height = 42;
            badge = Theme.Label(this, "READY", 450, 28, 108, 9, true, Theme.Accent);
            badge.TextAlign = ContentAlignment.MiddleRight;
            Nav(clickTab, "Clicking", 20, true); Nav(keysTab, "Hotkeys", 160, false);
            clickTab.Click += delegate { ShowPage(false); };
            keysTab.Click += delegate { ShowPage(true); };
            clicking.SetBounds(0, 128, 580, 540); shortcuts.Bounds = clicking.Bounds;
            Controls.Add(clicking); Controls.Add(shortcuts);

            Card timing = MakeCard(clicking, 0, 260, "Click settings");
            Theme.Label(timing, "Mouse button", 24, 73, 210, 10, false, Theme.Ink);
            buttonChoice.Items.AddRange(new object[] { "Left", "Right", "Middle", "Side / X1", "Side / X2" });
            buttonChoice.DropDownStyle = ComboBoxStyle.DropDownList; buttonChoice.FlatStyle = FlatStyle.Flat;
            buttonChoice.DrawMode = DrawMode.OwnerDrawFixed;
            buttonChoice.DrawItem += delegate(object sender, DrawItemEventArgs e) {
                using (SolidBrush brush = new SolidBrush(Theme.Field)) e.Graphics.FillRectangle(brush, e.Bounds);
                if (e.Index >= 0) TextRenderer.DrawText(e.Graphics, buttonChoice.Items[e.Index].ToString(), e.Font, e.Bounds, Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                e.DrawFocusRectangle();
            };
            buttonChoice.BackColor = Theme.Field; buttonChoice.ForeColor = Theme.Ink;
            timing.Controls.Add(new InputFrame(buttonChoice, 282, 62, 234));
            Theme.Label(timing, "Clicks per second", 24, 119, 234, 9, true, Theme.Muted);
            Theme.Label(timing, "Interval / seconds", 282, 119, 234, 9, true, Theme.Muted);
            Number(timing, cps, 24, 148, 234, 0.000001m, 500, 6, 1, "Clicks per second");
            Number(timing, interval, 282, 148, 234, 0.002m, 3600, 3, 0.005m, "Interval in seconds");
            Theme.Label(timing, "Hold / ms", 24, 216, 100, 9, true, Theme.Muted);
            Number(timing, hold, 130, 204, 128, 1, 1000, 0, 5, "Button hold time in milliseconds");
            timingHint = Theme.Label(timing, "", 282, 216, 234, 9, false, Theme.Muted);

            Card session = MakeCard(clicking, 276, 264, "Session options");
            Theme.Label(session, "Start delay / seconds", 24, 60, 234, 9, true, Theme.Muted);
            Theme.Label(session, "Click limit (0 = unlimited)", 282, 60, 234, 9, true, Theme.Muted);
            Number(session, delay, 24, 88, 234, 0, 60, 0, 1, "Start delay in seconds");
            Number(session, limit, 282, 88, 234, 0, 1000000, 0, 10, "Click limit, zero for unlimited");
            Theme.Label(session, "Time limit / seconds (0 = unlimited)", 24, 148, 252, 9, true, Theme.Muted);
            Number(session, duration, 24, 176, 234, 0, 86400, 0, 10, "Session duration, zero for unlimited");
            top.Text = "Keep on top"; top.SetBounds(282, 183, 234, 30); top.BackColor = Theme.Surface;
            session.Controls.Add(top);
            target.Text = "Lock to first app (pause when I Alt-Tab)";
            target.SetBounds(24, 231, 492, 27); target.BackColor = Theme.Surface; session.Controls.Add(target);

            Card bindings = MakeCard(shortcuts, 0, 384, "Hotkeys");
            string[] names = { "Start / stop", "Faster", "Slower", "Emergency stop" };
            for (int i = 0; i < 4; i++)
            {
                Theme.Label(bindings, names[i], 24, 74 + i * 65, 234, 10, false, Theme.Ink);
                bindings.Controls.Add(new InputFrame(keys[i], 282, 62 + i * 65, 234));
                keys[i].AccessibleName = names[i] + " hotkey";
                keys[i].Enter += delegate { editingKeys = true; Unbind(); UpdateButton(); };
                keys[i].Leave += delegate { editingKeys = false; Bind(); Save(); };
            }
            Theme.Label(bindings, "Click a box and press an F-key or Ctrl / Alt + a key.", 24, 324, 492, 9, false, Theme.Muted);
            bindingHint = Theme.Label(bindings, "", 24, 354, 492, 9, false, Theme.Accent);
            Card tuning = MakeCard(shortcuts, 400, 140, "Speed adjustment");
            Theme.Label(tuning, "CPS per hotkey press", 24, 74, 234, 10, false, Theme.Ink);
            Number(tuning, step, 282, 62, 234, 0.1m, 100, 1, 1, "CPS change per hotkey press");
            Theme.Label(tuning, "Faster / slower works without leaving your game.", 24, 110, 492, 8.5f, false, Theme.Muted);

            status = Theme.Label(this, "Ready", 24, 694, 354, 12, true, Theme.Ink);
            detail = Theme.Label(this, "", 24, 724, 354, 9, false, Theme.Muted);
            counter = Theme.Label(this, "0 clicks", 410, 694, 146, 12, true, Theme.Ink);
            counter.TextAlign = ContentAlignment.TopRight;
            elapsed = Theme.Label(this, "00:00 elapsed", 410, 724, 146, 9, false, Theme.Muted);
            elapsed.TextAlign = ContentAlignment.TopRight;
            toggle.SetBounds(20, 764, 540, 52); toggle.Click += delegate { Toggle(); }; Controls.Add(toggle);
            reset.Text = "Reset defaults"; reset.SetBounds(398, 835, 162, 23); reset.TextAlign = ContentAlignment.MiddleRight;
            reset.LinkColor = Theme.Muted; reset.ActiveLinkColor = Theme.Accent; reset.LinkBehavior = LinkBehavior.HoverUnderline;
            reset.LinkClicked += delegate { Apply(new Preferences()); Bind(); Save(); }; Controls.Add(reset);

            Apply(preferencesPath == null ? Preferences.LoadWithMigration() : Preferences.Load(preferencesPath));
            cps.ValueChanged += delegate { if (!syncing && !loading) ChangeRate(1m / cps.Value, true); };
            interval.ValueChanged += delegate { if (!syncing && !loading) ChangeRate(interval.Value, false); };
            hold.ValueChanged += delegate { UpdateTiming(); };
            top.CheckedChanged += delegate { TopMost = top.Checked; };
            refresh.Interval = 150; refresh.Tick += delegate { UpdateSession(); };
            tips.SetToolTip(cps, "Target rate, up to 500 CPS. Windows and the target app determine actual throughput.");
            tips.SetToolTip(hold, "50 ms for games; effective hold is capped at half the interval for fast clicking.");
            tips.SetToolTip(target, "Captures the first foreground app after starting, excluding Click Goblin. Pauses in other apps and resumes on return.");
            tips.SetToolTip(duration, "Wall-clock limit after countdown, including time paused by the app lock. 0 disables.");
            ShowPage(false); UpdateButton();
            Panel viewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Background };
            Panel content = new Panel { Size = new Size(580, 866), BackColor = Theme.Background };
            Control[] children = new Control[Controls.Count]; Controls.CopyTo(children, 0);
            foreach (Control child in children) content.Controls.Add(child);
            viewport.Controls.Add(content); Controls.Add(viewport);
            viewport.Resize += delegate { content.Left = Math.Max(0, (viewport.ClientSize.Width - content.Width) / 2); };
            Rectangle area = Screen.FromControl(this).WorkingArea;
            ClientSize = new Size(600, Math.Min(866, Math.Max(480, area.Height - 100)));

        }
        private void Nav(Button b, string text, int x, bool selected)
        {
            b.Text = text; b.SetBounds(x, 78, 128, 36); b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0;
            b.ForeColor = Theme.Ink; b.BackColor = selected ? Theme.Field : Theme.Background;
            b.Cursor = Cursors.Hand; Controls.Add(b);
        }
        private void ShowPage(bool hotkeys)
        {
            clicking.Visible = !hotkeys; shortcuts.Visible = hotkeys;
            clickTab.BackColor = hotkeys ? Theme.Background : Theme.Field;
            keysTab.BackColor = hotkeys ? Theme.Field : Theme.Background;
            if (!hotkeys && editingKeys) { editingKeys = false; Bind(); }
        }
        private Card MakeCard(Control parent, int y, int height, string title)
        {
            Card card = new Card { Location = new Point(20, y), Size = new Size(540, height) }; parent.Controls.Add(card);
            Theme.Label(card, title, 24, 20, 492, 11, true, Theme.Ink); return card;
        }
        private static void Number(Control parent, NumericUpDown n, int x, int y, int width, decimal min, decimal max, int places, decimal increment, string name)
        {
            n.Minimum = min; n.Maximum = max; n.DecimalPlaces = places; n.Increment = increment;
            n.Font = new Font("Segoe UI", 12); n.ForeColor = Theme.Ink; n.BackColor = Theme.Field;
            n.BorderStyle = BorderStyle.None; n.ThousandsSeparator = true; n.AccessibleName = name; parent.Controls.Add(new InputFrame(n, x, y, width));
        }
        private void Apply(Preferences p)
        {
            loading = true;
            buttonChoice.SelectedIndex = p.Button; seconds = p.Seconds;
            interval.Value = seconds; cps.Value = Math.Max(cps.Minimum, Math.Min(cps.Maximum, 1m / seconds));
            hold.Value = p.HoldMs; delay.Value = p.DelaySeconds; limit.Value = p.MaxClicks; duration.Value = p.DurationSeconds;
            top.Checked = TopMost = p.AlwaysOnTop; target.Checked = p.TargetOnly; step.Value = p.SpeedStep;
            int[] data = { p.ToggleKey, p.FasterKey, p.SlowerKey, p.StopKey };
            for (int i = 0; i < 4; i++) keys[i].SetBinding(data[i]);
            loading = false; UpdateTiming();
        }
        private void ChangeRate(decimal value, bool fromCps)
        {
            seconds = Math.Max(0.002m, Math.Min(3600m, value));
            syncing = true;
            interval.Value = seconds;
            if (!fromCps) cps.Value = Math.Max(cps.Minimum, Math.Min(cps.Maximum, 1m / seconds));
            syncing = false;
            if (engine != null) engine.SetInterval((double)seconds * 1000);
            UpdateTiming();
        }
        private void AdjustSpeed(int direction)
        {
            decimal rate = decimal.Round(Math.Max(0.01m, Math.Min(500, 1m / seconds + direction * step.Value)), 2, MidpointRounding.AwayFromZero);
            syncing = true; cps.Value = rate; syncing = false;
            ChangeRate(1m / rate, true); Save();
        }
        private void UpdateTiming()
        {
            if (timingHint != null) timingHint.Text = Math.Max(1, Math.Min((int)hold.Value, (int)(seconds * 500))) + " ms effective hold";
        }
        private void Unbind()
        {
            if (!IsHandleCreated) return;
            for (int i = 0; i < 4; i++) Native.UnregisterHotKey(Handle, 407 + i);
            bound = false;
        }
        private void Bind()
        {
            if (loading || !IsHandleCreated || editingKeys) return;
            Unbind(); bound = true;
            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < i; j++) if (keys[i].Binding == keys[j].Binding) bound = false;
                if (!HotkeyRules.Valid(keys[i].Binding) || !Native.RegisterHotKey(Handle, 407 + i, HotkeyRules.Modifiers(keys[i].Binding), (uint)(keys[i].Binding & (int)Keys.KeyCode))) bound = false;
            }
            if (!bound)
            {
                Unbind();
                SetStatus("Hotkey conflict", "Choose four different, available hotkeys.", "IN USE");
                bindingHint.Text = "A key is duplicated or already used by another app.";
            }
            else
            {
                SetStatus("Ready to click", "Switch to your app, then press " + HotkeyRules.Text(keys[0].Binding) + ".", "READY");
                bindingHint.Text = "All four hotkeys are ready.";
            }
            UpdateButton();
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int dark = 1;
            try { Native.DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); } catch (DllNotFoundException) { }
            if (status != null) Bind();
        }
        protected override void OnHandleDestroyed(EventArgs e) { Unbind(); base.OnHandleDestroyed(e); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && !editingKeys)
            {
                int id = m.WParam.ToInt32();
                if (id == 407) Toggle();
                else if (id == 408) AdjustSpeed(1);
                else if (id == 409) AdjustSpeed(-1);
                else if (id == 410) Stop();
            }
            base.WndProc(ref m);
        }
        private void Toggle()
        {
            if (running) { Stop(); return; }
            if (!bound) return;
            ValidateChildren(); Save();
            int startDelay = (int)delay.Value * 1000;
            if (target.Checked && Native.GetForegroundWindow() == Handle) startDelay = Math.Max(3000, startDelay);
            ClickEngine next = null;
            next = new ClickEngine((double)seconds * 1000, (int)hold.Value, startDelay, (int)limit.Value, (int)duration.Value * 1000,
                Native.Sender(buttonChoice.SelectedIndex), target.Checked ? Native.AppGate() : null,
                delegate(EndReason reason) { Finish(next, reason); });
            engine = next; running = true; lastCount = 0; started = DateTime.UtcNow;
            activeDelayMs = startDelay;
            LockInputs(true); Text = "Click Goblin - Running"; UpdateButton(); UpdateSession();
            engine.Start(); refresh.Start();
        }
        private int activeDelayMs;
        private void Finish(ClickEngine source, EndReason reason)
        {
            try { BeginInvoke((Action)delegate {
                if (engine != source) return;
                Stop();
                if (reason == EndReason.Blocked) SetStatus("Input blocked", "Try Run as administrator for an elevated game.", "BLOCKED");
                else if (reason == EndReason.Limit) SetStatus("Session complete", "Click limit reached.", "DONE");
                else if (reason == EndReason.Duration) SetStatus("Session complete", "Time limit reached.", "DONE");
            }); } catch (InvalidOperationException) { }
        }
        private void Stop()
        {
            refresh.Stop();
            if (running) UpdateSession();
            if (engine != null) { engine.Dispose(); lastCount = engine.Count; engine = null; }
            running = false; ShowCount(); LockInputs(false); Text = "Click Goblin - Stopped";
            SetStatus("Stopped", "Press " + HotkeyRules.Text(keys[0].Binding) + " whenever you're ready.", "READY"); UpdateButton();
        }
        private void LockInputs(bool active)
        {
            buttonChoice.Enabled = hold.Enabled = delay.Enabled = limit.Enabled = duration.Enabled = target.Enabled = reset.Enabled = !active;
            for (int i = 0; i < 4; i++) keys[i].Enabled = !active;
        }
        private void UpdateButton()
        {
            toggle.Text = (running ? "Stop clicking    /    " : "Start clicking    /    ") + HotkeyRules.Text(keys[0].Binding);
            toggle.Active = running; toggle.Enabled = bound; toggle.Invalidate();
        }
        private void ShowCount() { counter.Text = lastCount.ToString("N0") + (lastCount == 1 ? " click" : " clicks"); }
        private void UpdateSession()
        {
            if (engine == null) return;
            lastCount = engine.Count; ShowCount();
            TimeSpan time = DateTime.UtcNow - started;
            elapsed.Text = ((int)time.TotalMinutes).ToString("00") + ":" + time.Seconds.ToString("00") + " elapsed";
            double waiting = activeDelayMs / 1000.0 - time.TotalSeconds;
            if (waiting > 0) SetStatus("Starting in " + Math.Ceiling(waiting).ToString("0") + "...", "Switch to your app. Hotkey again to cancel.", "WAITING");
            else if (engine.Paused) SetStatus("Waiting for your app", "Return to the locked app to resume.", "PAUSED");
            else SetStatus("Clicking at " + (1m / seconds).ToString("0.##") + " CPS", "Emergency stop: " + HotkeyRules.Text(keys[3].Binding), "RUNNING");
        }
        private void SetStatus(string title, string description, string state) { status.Text = title; detail.Text = description; badge.Text = state; }
        private void Save()
        {
            if (!new Preferences { Button = buttonChoice.SelectedIndex, Seconds = seconds, HoldMs = (int)hold.Value,
                DelaySeconds = (int)delay.Value, MaxClicks = (int)limit.Value, DurationSeconds = (int)duration.Value,
                AlwaysOnTop = top.Checked, TargetOnly = target.Checked, SpeedStep = step.Value,
                ToggleKey = keys[0].Binding, FasterKey = keys[1].Binding, SlowerKey = keys[2].Binding, StopKey = keys[3].Binding }.Save(SettingsPath))
                detail.Text = "Preferences could not be saved to disk.";
        }
        protected override void OnFormClosing(FormClosingEventArgs e) { Stop(); Save(); base.OnFormClosing(e); }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refresh.Dispose(); tips.Dispose();
                if (engine != null) { engine.Dispose(); engine = null; }
                if (Icon != null) { Icon.Dispose(); Icon = null; }
            }
            base.Dispose(disposing);
        }
    }
}

