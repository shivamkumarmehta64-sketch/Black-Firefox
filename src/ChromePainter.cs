using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace BlackBrowser
{
    // Deterministic, time-animated aurora background shared by the tab strip,
    // omnibox and glass buttons so every surface stays visually continuous.
    // Driven by a single 30fps timer; blob positions derive from Time so all
    // surfaces agree on where the glow sits.
    public static class ChromePainter
    {
        public static float Time = 0f;
        public static Color Accent = Color.FromArgb(12, 99, 232);
        public static bool IsDark = true;
        public static bool Enabled = true;

        private static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Lighten(Color c, float amt)
        {
            return Color.FromArgb(c.A,
                Math.Min(255, (int)(c.R + (255 - c.R) * amt)),
                Math.Min(255, (int)(c.G + (255 - c.G) * amt)),
                Math.Min(255, (int)(c.B + (255 - c.B) * amt)));
        }

        // Vertical base gradient for the chrome surface (dark mode = near-black
        // tinted with the accent, light mode = white tinted with the accent).
        public static LinearGradientBrush BaseBrush(Rectangle r)
        {
            Color top, bottom;
            if (IsDark)
            {
                top = Mix(Color.FromArgb(24, 24, 30), Mix(Color.FromArgb(24, 24, 30), Accent, 0.18f), 0f);
                bottom = Mix(Color.FromArgb(17, 17, 21), Mix(Color.FromArgb(17, 17, 21), Accent, 0.12f), 0.6f);
            }
            else
            {
                top = Mix(Color.FromArgb(247, 247, 248), Mix(Color.White, Accent, 0.06f), 0.4f);
                bottom = Color.FromArgb(237, 238, 240);
            }
            return new LinearGradientBrush(r, top, bottom, LinearGradientMode.Vertical);
        }

        // Paint the full aurora surface over rect r. Blob positions are
        // absolute across the whole form width so adjacent surfaces line up.
        public static void PaintBackground(Graphics g, Rectangle r, int formWidth)
        {
            using (LinearGradientBrush baseBrush = BaseBrush(r))
            {
                g.FillRectangle(baseBrush, r);
            }

            if (!Enabled) return;

            Color c1 = Lighten(Accent, 0.18f);
            Color c2 = IsDark ? Lighten(Accent, 0.42f) : Lighten(Accent, 0.25f);

            // Aurora blob 1 — slow horizontal sweep (absolute across form width).
            float cx1 = (float)(Math.Sin(Time * 0.5f) * 0.5f + 0.5f) * formWidth;
            float cy1 = r.Y + (float)Math.Sin(Time * 0.35f) * 26f + 14f;
            PaintBlob(g, cx1, cy1, 190f, Color.FromArgb(38, c1), r);

            // Aurora blob 2 — counter-phase drift (absolute across form width).
            float cx2 = (float)(Math.Cos(Time * 0.42f) * 0.5f + 0.5f) * formWidth;
            float cy2 = r.Y + (float)Math.Cos(Time * 0.28f) * 20f + 22f;
            PaintBlob(g, cx2, cy2, 150f, Color.FromArgb(30, c2), r);

            // Aurora blob 3 — small accent spark (absolute).
            float cx3 = 40f + (float)Math.Sin(Time * 0.8f) * 30f;
            PaintBlob(g, cx3, r.Y + 18f, 70f, Color.FromArgb(26, Lighten(Accent, 0.3f)), r);
        }

        private static void PaintBlob(Graphics g, float cx, float cy, float radius, Color color, Rectangle clip)
        {
            RectangleF bounds = new RectangleF(cx - radius, cy - radius, radius * 2f, radius * 2f);
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddEllipse(bounds);
                using (PathGradientBrush pg = new PathGradientBrush(path))
                {
                    pg.CenterColor = color;
                    pg.SurroundColors = new Color[] { Color.Transparent };
                    pg.CenterPoint = new PointF(cx, cy);
                    using (Region reg = new Region(clip))
                    {
                        Region old = g.Clip;
                        g.SetClip(reg, CombineMode.Replace);
                        g.FillPath(pg, path);
                        g.Clip = old;
                    }
                }
            }
        }

        // Solid colour for small children that cannot host custom paint (e.g.
        // the favicon host) so they never show a flat patch over the aurora.
        public static Color Sample()
        {
            if (IsDark) return Mix(Color.FromArgb(24, 24, 30), Accent, 0.05f);
            return Mix(Color.FromArgb(244, 244, 245), Accent, 0.02f);
        }
    }
}