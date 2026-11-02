using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SoundLevel
{
    internal class FlatButton : Control
    {
        public bool Accent;
        private bool _hover;
        private bool _down;

        public FlatButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            Font = Theme.FButton;
            BackColor = Theme.Card;
            ForeColor = Theme.Text;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Theme.S(10);

            Color fill, text, border;
            if (Accent)
            {
                fill = _down ? Theme.Mix(Theme.Accent, Color.Black, 0.15f)
                     : _hover ? Theme.AccentHover : Theme.Accent;
                text = Color.White;
                border = Color.Transparent;
            }
            else
            {
                fill = _down ? Theme.Mix(Theme.CardHi, Color.Black, 0.25f)
                     : _hover ? Theme.Mix(Theme.CardHi, Color.White, 0.04f) : Theme.CardHi;
                text = Theme.Text;
                border = Theme.Border;
            }

            if (!Enabled)
            {
                fill = Theme.Mix(BackColor, fill, 0.35f);
                text = Theme.Mix(BackColor, text, 0.35f);
                border = Theme.Mix(BackColor, border, 0.35f);
            }

            Theme.FillRound(g, r, radius, fill);
            if (border != Color.Transparent) Theme.DrawRound(g, r, radius, border, 1f);

            Theme.TextCenter(g, Text, Font, text, r);
        }
    }

    internal class LevelSlider : Control
    {
        private double _value = 0.5;
        private bool _drag;
        private bool _hover;

        public event EventHandler ValueChanged;

        public double Value
        {
            get { return _value; }
            set
            {
                double v = value;
                if (v < 0) v = 0;
                if (v > 1) v = 1;
                if (Math.Abs(v - _value) < 0.0005) return;
                _value = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public void SetValueSilent(double v)
        {
            if (v < 0) v = 0;
            if (v > 1) v = 1;
            _value = v;
            Invalidate();
        }

        public LevelSlider()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
                     ControlStyles.Selectable, true);
            TabStop = true;
            Cursor = Cursors.Hand;
            BackColor = Theme.Card;
        }

        private int ThumbR { get { return Theme.S(8); } }

        private Rectangle TrackRect
        {
            get
            {
                int r = ThumbR;
                int h = Theme.S(6);
                int y = (Height - h) / 2;
                return new Rectangle(r, y, Math.Max(1, Width - r * 2), h);
            }
        }

        private void SetFromX(int x)
        {
            Rectangle t = TrackRect;
            double v = (double)(x - t.X) / Math.Max(1, t.Width);
            Value = v;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            _drag = true;
            SetFromX(e.X);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_drag) SetFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _drag = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Value = _value + (e.Delta > 0 ? 0.02 : -0.02);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Left || keyData == Keys.Right || keyData == Keys.Up || keyData == Keys.Down)
                return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Down) Value = _value - 0.01;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.Up) Value = _value + 0.01;
            if (e.KeyCode == Keys.PageDown) Value = _value - 0.05;
            if (e.KeyCode == Keys.PageUp) Value = _value + 0.05;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, ClientRectangle);

            Rectangle t = TrackRect;
            Theme.FillRound(g, t, t.Height / 2, Theme.TrackBg);

            int cx = t.X + (int)Math.Round(t.Width * _value);
            Rectangle filled = new Rectangle(t.X, t.Y, Math.Max(0, cx - t.X), t.Height);
            if (filled.Width > 0) Theme.FillRound(g, filled, t.Height / 2, Theme.Accent);

            int d = ThumbR * 2;
            Rectangle thumb = new Rectangle(cx - ThumbR, (Height - d) / 2, d, d);

            if (_hover || _drag)
            {
                using (SolidBrush halo = new SolidBrush(Theme.Alpha(Theme.Accent, 40)))
                    g.FillEllipse(halo, thumb.X - Theme.S(4), thumb.Y - Theme.S(4),
                        d + Theme.S(8), d + Theme.S(8));
            }

            using (SolidBrush b = new SolidBrush(Color.White))
                g.FillEllipse(b, thumb);
        }
    }

    internal class IconButton : Control
    {
        public bool IsClose;
        private bool _hover;

        public IconButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            BackColor = Theme.Bg;
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, ClientRectangle);

            if (_hover)
            {
                Color c = IsClose ? Color.FromArgb(0xC4, 0x2B, 0x1C) : Theme.CardHi;
                Theme.FillRound(g, new Rectangle(0, 0, Width - 1, Height - 1), Theme.S(7), c);
            }

            Color ink = _hover && IsClose ? Color.White : Theme.Dim;
            using (Pen p = new Pen(ink, Math.Max(1.2f, Theme.Scale * 1.2f)))
            {
                int cx = Width / 2;
                int cy = Height / 2;
                int s = Theme.S(5);
                if (IsClose)
                {
                    g.DrawLine(p, cx - s, cy - s, cx + s, cy + s);
                    g.DrawLine(p, cx + s, cy - s, cx - s, cy + s);
                }
                else
                {
                    g.DrawLine(p, cx - s, cy, cx + s, cy);
                }
            }
        }
    }

    internal class CircleButton : Control
    {
        private bool _hover;
        private bool _down;

        public bool Playing;

        public CircleButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            BackColor = Theme.Card;
            TabStop = false;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, ClientRectangle);

            int d = Math.Min(Width, Height);
            Rectangle circle = new Rectangle((Width - d) / 2, (Height - d) / 2, d - 1, d - 1);

            if (_hover)
            {
                using (SolidBrush halo = new SolidBrush(Theme.Alpha(Theme.Accent, 50)))
                    g.FillEllipse(halo, circle.X - Theme.S(3), circle.Y - Theme.S(3),
                        circle.Width + Theme.S(6), circle.Height + Theme.S(6));
            }

            Color fill = _down ? Theme.Mix(Theme.Accent, Color.Black, 0.15f)
                       : _hover ? Theme.AccentHover : Theme.Accent;
            if (!Enabled) fill = Theme.Mix(BackColor, Theme.Accent, 0.22f);
            using (SolidBrush b = new SolidBrush(fill))
                g.FillEllipse(b, circle);

            Color glyph = Enabled ? Color.White : Theme.Mix(BackColor, Color.White, 0.3f);

            if (Playing)
            {
                int s = Theme.S(13);
                Rectangle sq = new Rectangle(Width / 2 - s / 2, Height / 2 - s / 2, s, s);
                Theme.FillRound(g, sq, Theme.S(3), glyph);
            }
            else
            {
                int w = Theme.S(12);
                int h = Theme.S(15);
                Point a = new Point(Width / 2 - w / 2 + Theme.S(2), Height / 2 - h / 2);
                Point b = new Point(a.X, a.Y + h);
                Point c = new Point(a.X + w, Height / 2);
                using (SolidBrush b2 = new SolidBrush(glyph))
                    g.FillPolygon(b2, new Point[] { a, b, c });
            }
        }
    }

    internal class PillToggle : Control
    {
        private bool _checked = true;
        private bool _hover;

        public string LabelOn = "已启用";
        public string LabelOff = "已暂停";

        public event EventHandler CheckedChanged;

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public PillToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
            BackColor = Theme.Bg;
            Font = Theme.FSmall;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (SolidBrush bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, ClientRectangle);

            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Height / 2;

            Color fill = _checked
                ? Theme.Alpha(Theme.Accent, _hover ? 54 : 40)
                : (_hover ? Theme.CardHi : Theme.Card);
            Theme.FillRound(g, r, radius, fill);

            int d = Theme.S(8);
            int cy = Height / 2;
            int cx = Theme.S(14);
            using (SolidBrush dot = new SolidBrush(_checked ? Theme.Accent : Theme.Faint))
                g.FillEllipse(dot, cx - d / 2, cy - d / 2, d, d);

            string label = _checked ? LabelOn : LabelOff;
            Theme.DrawText(g, label, Font, _checked ? Theme.Text : Theme.Dim, Theme.S(24), (Height - Theme.S(11)) / 2);
        }
    }
}
