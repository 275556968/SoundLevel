using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace SoundLevel
{
    internal static class Theme
    {
        public static float Scale = 1f;

        public static readonly Color Bg = Color.FromArgb(0x15, 0x16, 0x1B);
        public static readonly Color Card = Color.FromArgb(0x1D, 0x1F, 0x27);
        public static readonly Color CardHi = Color.FromArgb(0x24, 0x27, 0x31);
        public static readonly Color Border = Color.FromArgb(0x2A, 0x2D, 0x38);
        public static readonly Color Text = Color.FromArgb(0xE9, 0xEB, 0xF2);
        public static readonly Color Dim = Color.FromArgb(0x8B, 0x91, 0xA3);
        public static readonly Color Faint = Color.FromArgb(0x5A, 0x60, 0x70);
        public static readonly Color Accent = Color.FromArgb(0x5B, 0x8C, 0xFF);
        public static readonly Color AccentHover = Color.FromArgb(0x78, 0xA1, 0xFF);
        public static readonly Color Warn = Color.FromArgb(0xFF, 0xB0, 0x20);
        public static readonly Color Good = Color.FromArgb(0x3D, 0xDC, 0x97);
        public static readonly Color TrackBg = Color.FromArgb(0x2B, 0x2E, 0x3A);

        public static readonly Font FTitle = new Font("Segoe UI", 12.5f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font FBody = new Font("Segoe UI", 9.25f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FName = new Font("Segoe UI", 9.75f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FSmall = new Font("Segoe UI", 8.25f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FBig = new Font("Segoe UI", 21f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font FButton = new Font("Segoe UI", 9.75f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font FApp = new Font("Segoe UI", 10f, FontStyle.Bold, GraphicsUnit.Point);

        public static int S(float v)
        {
            return (int)Math.Round(v * Scale);
        }

        public static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            GraphicsPath p = new GraphicsPath();
            int d = radius * 2;
            if (d <= 0 || d > r.Width || d > r.Height)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color c)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (SolidBrush b = new SolidBrush(c))
                g.FillPath(b, p);
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color c, float w)
        {
            using (GraphicsPath p = RoundRect(r, radius))
            using (Pen pen = new Pen(c, w))
                g.DrawPath(pen, p);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Alpha(Color c, int alpha)
        {
            return Color.FromArgb(alpha, c.R, c.G, c.B);
        }

        public static void DrawText(Graphics g, string s, Font f, Color c, int x, int y)
        {
            TextRenderer.DrawText(g, s, f, new Point(x, y), c,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        public static void TextRight(Graphics g, string s, Font f, Color c, int right, int y)
        {
            Size sz = TextRenderer.MeasureText(g, s, f, new Size(int.MaxValue, int.MaxValue),
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, s, f, new Point(right - sz.Width, y), c,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }

        public static void TextCenter(Graphics g, string s, Font f, Color c, Rectangle r)
        {
            TextRenderer.DrawText(g, s, f, r, c,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding);
        }
    }
}
