using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ClickGoblin
{
    internal static class Theme
    {
        internal static readonly Color Background = Color.FromArgb(20, 22, 30);
        internal static readonly Color Ink = Color.FromArgb(238, 240, 247);
        internal static readonly Color Muted = Color.FromArgb(157, 167, 188);
        internal static readonly Color Accent = Color.FromArgb(180, 153, 255);
        internal static readonly Color Surface = Color.FromArgb(29, 32, 43);
        internal static readonly Color Field = Color.FromArgb(38, 42, 56);
        internal static readonly Color Border = Color.FromArgb(52, 58, 75);
        internal static GraphicsPath Round(RectangleF box, float radius)
        {
            float d = radius * 2;
            GraphicsPath p = new GraphicsPath();
            p.AddArc(box.X, box.Y, d, d, 180, 90);
            p.AddArc(box.Right - d, box.Y, d, d, 270, 90);
            p.AddArc(box.Right - d, box.Bottom - d, d, d, 0, 90);
            p.AddArc(box.X, box.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
        internal static Label Label(Control parent, string text, int x, int y, int width, float size, bool bold, Color color)
        {
            Label label = new Label { Text = text, Location = new Point(x, y), Size = new Size(width, 25),
                Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color,
                BackColor = Color.Transparent, AutoEllipsis = true };
            parent.Controls.Add(label);
            return label;
        }
    }

    internal sealed class Card : Panel
    {
        internal Card() { DoubleBuffered = true; BackColor = Theme.Background; }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Background);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), 12))
            using (SolidBrush fill = new SolidBrush(Theme.Surface))
            using (Pen pen = new Pen(Theme.Border))
            { e.Graphics.FillPath(fill, path); e.Graphics.DrawPath(pen, path); }
        }
    }

    internal sealed class ActionButton : Button
    {
        private bool hover;
        internal bool Active;
        internal ActionButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font("Segoe UI", 11, FontStyle.Bold);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Background);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = !Enabled ? Theme.Muted : Active ? Color.FromArgb(159, 54, 73) : Color.FromArgb(103, 76, 179);
            if (hover && Enabled) color = ControlPaint.Dark(color, 0.12f);
            using (GraphicsPath path = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), 10))
            using (SolidBrush brush = new SolidBrush(color)) e.Graphics.FillPath(brush, path);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(6, 6, Width - 13, Height - 13), Color.White, color);
        }
    }

    internal sealed class BrandMark : Control
    {
        internal BrandMark() { DoubleBuffered = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            float scale = Width / 40f;
            e.Graphics.ScaleTransform(scale, scale);
            using (GraphicsPath path = Theme.Round(new RectangleF(0, 0, 40, 40), 12))
            using (SolidBrush brush = new SolidBrush(Theme.Accent)) e.Graphics.FillPath(brush, path);
            using (SolidBrush ink = new SolidBrush(Theme.Background))
            using (SolidBrush eye = new SolidBrush(Color.White))
            using (GraphicsPath head = Theme.Round(new RectangleF(11, 10, 18, 23), 8))
            using (Pen grin = new Pen(Theme.Accent, 1.8f))
            {
                e.Graphics.FillPolygon(ink, new PointF[] { new PointF(5, 9), new PointF(7, 23), new PointF(17, 16) });
                e.Graphics.FillPolygon(ink, new PointF[] { new PointF(35, 9), new PointF(33, 23), new PointF(23, 16) });
                e.Graphics.FillPath(ink, head);
                e.Graphics.FillEllipse(eye, 14, 18, 4, 4); e.Graphics.FillEllipse(eye, 22, 18, 4, 4);
                e.Graphics.DrawArc(grin, 16, 23, 8, 5, 0, 180);
            }
        }
    }

    internal sealed class RateControl : NumericUpDown
    {
        protected override void UpdateEditText()
        {
            // Preserve precision for long intervals without padding every rate with zeroes.
            Text = Value.ToString("0.######", CultureInfo.CurrentCulture);
        }
    }

    internal sealed class HotkeyBox : TextBox
    {
        internal int Binding;
        internal event EventHandler BindingChanged;
        internal HotkeyBox()
        {
            ReadOnly = true; ShortcutsEnabled = false;
            BackColor = Theme.Field; ForeColor = Theme.Ink;
            BorderStyle = BorderStyle.None;
            Font = new Font("Segoe UI", 11);
            TextAlign = HorizontalAlignment.Center;
        }
        internal void SetBinding(int value) { Binding = value; Text = HotkeyRules.Text(value); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Tab) return;
            e.SuppressKeyPress = true; e.Handled = true;
            if (HotkeyRules.Valid((int)e.KeyData))
            {
                SetBinding((int)e.KeyData);
                if (BindingChanged != null) BindingChanged(this, EventArgs.Empty);
            }
        }
    }

    internal sealed class InputFrame : Panel
    {
        private Control input;
        internal InputFrame(Control control, int x, int y, int width)
        {
            input = control;
            SetBounds(x, y, width, 44);
            DoubleBuffered = true;
            BackColor = Theme.Surface;
            control.Enter += delegate { Invalidate(); };
            control.Leave += delegate { Invalidate(); };
            control.SetBounds(12, 10, width - 24, 26);
            Controls.Add(control);
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Surface);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath shape = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), 7))
            using (SolidBrush fill = new SolidBrush(Theme.Field))
            using (Pen border = new Pen(input.ContainsFocus ? Theme.Accent : Theme.Border))
            { e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(border, shape); }
        }
    }
}
