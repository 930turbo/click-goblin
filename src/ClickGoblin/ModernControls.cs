using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace ClickGoblin
{
    internal sealed class ToggleSwitch : CheckBox
    {
        internal ToggleSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand; AutoSize = false;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Surface); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int y = (Height - 18) / 2;
            using (GraphicsPath shape = Theme.Round(new RectangleF(0, y, 30, 18), 9))
            using (SolidBrush fill = new SolidBrush(Checked ? (Enabled ? Theme.Accent : Theme.Border) : Theme.Field))
            using (Pen border = new Pen(Checked && Enabled ? Theme.Accent : Theme.Border))
            { e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(border, shape); }
            using (SolidBrush thumb = new SolidBrush(Checked && Enabled ? Theme.Background : Theme.Muted))
                e.Graphics.FillEllipse(thumb, Checked ? 15 : 3, y + 3, 12, 12);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(39, 0, Width - 39, Height), Enabled ? Theme.Ink : Theme.Muted,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(36, 2, Width - 39, Height - 4));
        }
    }

    internal sealed class NumberField : UserControl
    {
        private readonly TextBox edit = new TextBox();
        private decimal value, minimum, maximum = 100, increment = 1;
        private int places;
        internal event EventHandler ValueChanged;
        internal decimal Minimum { get { return minimum; } set { minimum = value; Value = this.value; } }
        internal decimal Maximum { get { return maximum; } set { maximum = value; Value = this.value; } }
        internal decimal Increment { get { return increment; } set { increment = value; } }
        internal int DecimalPlaces { get { return places; } set { places = value; UpdateText(); } }
        internal bool ThousandsSeparator { get; set; }
        internal decimal Value
        {
            get { return value; }
            set
            {
                decimal next = Math.Max(minimum, Math.Min(maximum, value));
                if (this.value != next) { this.value = next; UpdateText(); if (ValueChanged != null) ValueChanged(this, EventArgs.Empty); }
                else UpdateText();
            }
        }
        internal NumberField()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            edit.BorderStyle = BorderStyle.None; edit.BackColor = Theme.Field; edit.ForeColor = Theme.Ink;
            edit.Validating += delegate { Commit(); };
            edit.KeyDown += delegate(object sender, KeyEventArgs e) {
                if (e.KeyCode == Keys.Enter) { Commit(); e.SuppressKeyPress = true; }
                if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down) { Change(e.KeyCode == Keys.Up ? 1 : -1); e.SuppressKeyPress = true; }
            };
            edit.Enter += delegate { Invalidate(); };
            Controls.Add(edit); Height = 28;
        }
        private string Display { get { return value.ToString(places == 0 ? "0" : "0." + new string('#', places), CultureInfo.CurrentCulture); } }
        private void UpdateText() { if (edit != null) edit.Text = Display; Invalidate(); }
        internal void Commit()
        {
            decimal parsed;
            if (decimal.TryParse(edit.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed)) Value = parsed;
            else UpdateText();
        }
        private void Change(int direction) { Commit(); Value += increment * direction; }
        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            if (edit != null) edit.SetBounds(0, Math.Max(0, (Height - edit.PreferredHeight) / 2), Math.Max(12, Width - 60), edit.PreferredHeight);
        }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (edit != null) { edit.Font = Font; PerformLayout(); } }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); if (edit != null) edit.Visible = Enabled; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled) return;
            edit.Focus();
            if (e.X >= Width - 26) Change(1); else if (e.X >= Width - 56) Change(-1);
        }
        protected override void OnMouseMove(MouseEventArgs e) { Cursor = e.X >= Width - 56 ? Cursors.Hand : Cursors.IBeam; base.OnMouseMove(e); }
        protected override void OnMouseWheel(MouseEventArgs e) { if (ContainsFocus && Enabled) Change(Math.Sign(e.Delta)); base.OnMouseWheel(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Field); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            if (!Enabled) TextRenderer.DrawText(e.Graphics, Display, Font, new Rectangle(0, 0, Math.Max(1, Width - 60), Height), Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            for (int i = 0; i < 2; i++)
            {
                Rectangle box = new Rectangle(Width - 56 + i * 30, (Height - 24) / 2, 24, 24);
                using (GraphicsPath p = Theme.Round(box, 6))
                using (SolidBrush fill = new SolidBrush(Theme.Surface)) e.Graphics.FillPath(fill, p);
                using (Pen pen = new Pen(Enabled ? Theme.Accent : Theme.Muted, 1.7f))
                {
                    int x = box.X + 12, y = box.Y + 12;
                    e.Graphics.DrawLine(pen, x - 4, y, x + 4, y);
                    if (i == 1) e.Graphics.DrawLine(pen, x, y - 4, x, y + 4);
                }
            }
        }
    }

    internal sealed class ChoiceField : Control
    {
        internal readonly List<object> Items = new List<object>();
        private int selected = -1;
        private ToolStripDropDown popup;
        internal event EventHandler SelectedIndexChanged;
        internal int SelectedIndex
        {
            get { return selected; }
            set
            {
                if (value < -1 || value >= Items.Count) throw new ArgumentOutOfRangeException("value");
                if (selected == value) return;
                selected = value; Text = value >= 0 ? Items[value].ToString() : ""; Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }
        internal ChoiceField()
        {
            SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = true; Cursor = Cursors.Hand; AccessibleRole = AccessibleRole.ComboBox;
        }
        internal void OpenMenu()
        {
            if (!Enabled || Items.Count == 0) return;
            if (popup != null) popup.Dispose();
            popup = new ToolStripDropDown { Padding = new Padding(0), BackColor = Theme.Surface, AutoClose = true };
            int width = Parent == null ? Width : Parent.Width;
            ChoiceList list = new ChoiceList(this) { Size = new Size(width, Items.Count * 36 + 12), Font = Font };
            ToolStripControlHost host = new ToolStripControlHost(list) { Padding = Padding.Empty, Margin = Padding.Empty, AutoSize = false, Size = list.Size };
            popup.Items.Add(host); popup.Show(this, new Point(-12, Height + 10)); list.Focus();
        }
        protected override void OnClick(EventArgs e) { base.OnClick(e); OpenMenu(); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OpenMenu(); e.Handled = true; }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            { SelectedIndex = Math.Max(0, Math.Min(Items.Count - 1, selected + (e.KeyCode == Keys.Down ? 1 : -1))); e.Handled = true; }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Field);
            if (selected >= 0) TextRenderer.DrawText(e.Graphics, Items[selected].ToString(), Font, new Rectangle(0, 0, Width - 28, Height), Enabled ? Theme.Ink : Theme.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Pen p = new Pen(Enabled ? Theme.Accent : Theme.Muted, 1.8f))
                e.Graphics.DrawLines(p, new Point[] { new Point(Width - 19, Height / 2 - 2), new Point(Width - 14, Height / 2 + 3), new Point(Width - 9, Height / 2 - 2) });
        }
        protected override void Dispose(bool disposing) { if (disposing && popup != null) popup.Dispose(); base.Dispose(disposing); }
        private sealed class ChoiceList : Control
        {
            private readonly ChoiceField owner;
            private int hover;
            internal ChoiceList(ChoiceField field)
            {
                owner = field; hover = field.SelectedIndex;
                SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                TabStop = true; Cursor = Cursors.Hand;
            }
            protected override void OnMouseMove(MouseEventArgs e) { hover = Math.Max(0, Math.Min(owner.Items.Count - 1, (e.Y - 6) / 36)); Invalidate(); }
            protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { hover = Math.Max(0, Math.Min(owner.Items.Count - 1, (e.Y - 6) / 36)); Pick(); } }
            private void Pick() { owner.SelectedIndex = hover; owner.popup.Close(); owner.Focus(); }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Escape) owner.popup.Close();
                else if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) Pick();
                else if (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up) { hover = Math.Max(0, Math.Min(owner.Items.Count - 1, hover + (e.KeyCode == Keys.Down ? 1 : -1))); Invalidate(); }
                e.Handled = true; base.OnKeyDown(e);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Theme.Surface); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                for (int i = 0; i < owner.Items.Count; i++)
                {
                    Rectangle row = new Rectangle(5, 6 + i * 36, Width - 10, 34);
                    if (i == hover || i == owner.selected)
                        using (GraphicsPath p = Theme.Round(row, 6)) using (SolidBrush b = new SolidBrush(Theme.Field)) e.Graphics.FillPath(b, p);
                    TextRenderer.DrawText(e.Graphics, owner.Items[i].ToString(), Font, new Rectangle(15, row.Y, Width - 48, row.Height), Theme.Ink, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    if (i == owner.selected) using (Pen p = new Pen(Theme.Accent, 2))
                        e.Graphics.DrawLines(p, new Point[] { new Point(Width - 30, row.Y + 17), new Point(Width - 26, row.Y + 21), new Point(Width - 19, row.Y + 13) });
                }
                using (Pen p = new Pen(Theme.Border)) e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
