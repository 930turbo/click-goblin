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
        internal static Color Background = Color.FromArgb(17, 19, 28);
        internal static Color Ink = Color.FromArgb(248, 249, 253);
        internal static Color Muted = Color.FromArgb(195, 204, 222);
        internal static Color Accent = Color.FromArgb(189, 165, 255);
        internal static Color Surface = Color.FromArgb(27, 31, 43);
        internal static Color Field = Color.FromArgb(42, 48, 64);
        internal static Color Border = Color.FromArgb(78, 88, 112);
        internal static Font Font(float size, bool bold) { return AppFont.Create(size, bold); }
        internal static void Palette(int accent, int background)
        {
            Color[] accents = { Color.FromArgb(189, 165, 255), Color.FromArgb(119, 229, 191), Color.FromArgb(255, 168, 178), Color.FromArgb(130, 202, 255), Color.FromArgb(255, 210, 125),
                Color.FromArgb(255, 177, 137), Color.FromArgb(157, 227, 255), Color.FromArgb(219, 178, 255), Color.FromArgb(216, 239, 145), Color.FromArgb(255, 169, 210), Color.FromArgb(137, 235, 223), Color.FromArgb(226, 231, 242) };
            Color[] backgrounds = { Color.FromArgb(17, 19, 28), Color.FromArgb(23, 24, 27), Color.FromArgb(12, 23, 37), Color.FromArgb(25, 18, 31), Color.FromArgb(12, 27, 24), Color.FromArgb(30, 23, 20) };
            Accent = accents[Math.Max(0, Math.Min(11, accent))];
            Background = backgrounds[Math.Max(0, Math.Min(5, background))];
            Surface = Color.FromArgb(Background.R + 11, Background.G + 12, Background.B + 14);
            Field = Color.FromArgb(Surface.R + 14, Surface.G + 15, Surface.B + 18);
        }
        internal static void Refresh(Control root)
        {
            root.BackColor = root is InputFrame || root is CheckBox ? Surface : root is NumberField || root is TextBox || root is ChoiceField || root is HotkeyBox ? Field : Background;
            root.ForeColor = root.Tag as string == "muted" ? Muted : root.Tag as string == "accent" ? Accent : Ink;
            if (root is Label) root.BackColor = Color.Transparent;
            LinkLabel link = root as LinkLabel;
            if (link != null) { link.LinkColor = Muted; link.ActiveLinkColor = Accent; }
            foreach (Control child in root.Controls) Refresh(child);
            root.Invalidate();
        }
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
                Font = Font(size, bold), ForeColor = color,
                BackColor = Color.Transparent, AutoEllipsis = true };
            parent.Controls.Add(label);
            label.Tag = color == Muted ? "muted" : color == Accent ? "accent" : "primary";
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
            Font = Theme.Font(11, true);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Background);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = !Enabled ? Theme.Border : Active ? Color.FromArgb(159, 54, 73) : Color.FromArgb(Theme.Accent.R / 2, Theme.Accent.G / 2, Theme.Accent.B / 2);
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

    internal sealed class HotkeyBox : Control
    {
        internal int Binding;
        internal event EventHandler BindingChanged;
        internal HotkeyBox()
        {
            SetStyle(ControlStyles.Selectable | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            TabStop = true; Cursor = Cursors.Hand; Font = Theme.Font(11, false);
        }
        internal void SetBinding(int value) { Binding = value; Text = HotkeyRules.Text(value); Invalidate(); }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Focus(); }
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
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Field);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Enabled ? Theme.Ink : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
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
            control.SetBounds(12, 8, width - 24, 28);
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
    internal sealed class TabButton : Button
    {
        internal bool Selected;
        private bool hover;
        internal TabButton()
        {
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand;
            Font = Theme.Font(10, true);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Background); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath shape = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 2, Height - 2), 9))
            using (SolidBrush fill = new SolidBrush(Selected ? Theme.Field : hover ? Theme.Surface : Theme.Background))
            using (Pen outline = new Pen(Selected ? Theme.Accent : hover ? Theme.Border : Theme.Background))
            { e.Graphics.FillPath(fill, shape); e.Graphics.DrawPath(outline, shape); }
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, Selected ? Theme.Ink : Theme.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(7, 7, Width - 15, Height - 15));
        }
    }
}
