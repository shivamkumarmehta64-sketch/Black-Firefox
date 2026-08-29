using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace BlackBrowser
{
    // Animated startup splash: aurora sweep behind the logo + thin progress
    // bar, fades in then out. Closed by BrowserForm once the main window is
    // shown.
    public class SplashForm : Form
    {
        public static SplashForm Current;

        private readonly Timer _timer;
        private float _t = 0f;          // seconds
        private bool _closing = false;
        private float _opacity = 0f;    // 0..1 fade

        public SplashForm()
        {
            Current = this;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(440, 280);
            DoubleBuffered = true;
            ShowInTaskbar = false;
            TopMost = true;

            using (GraphicsPath p = new GraphicsPath())
            {
                int d = 24;
                Rectangle r = new Rectangle(0, 0, Width, Height);
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                Region = new Region(p);
            }

            ChromePainter.IsDark = true;
            ChromePainter.Accent = ParseAccent(SettingsStore.Accent);

            _timer = new Timer();
            _timer.Interval = 16;
            _timer.Tick += OnTick;
            _timer.Start();

            Opacity = 0;
            Shown += (s, e) => { _opacity = 0f; };
        }

        private static Color ParseAccent(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return Color.FromArgb(12, 99, 232);
                hex = hex.TrimStart('#');
                if (hex.Length == 6)
                    return Color.FromArgb(
                        Convert.ToInt32(hex.Substring(0, 2), 16),
                        Convert.ToInt32(hex.Substring(2, 2), 16),
                        Convert.ToInt32(hex.Substring(4, 2), 16));
            }
            catch { }
            return Color.FromArgb(12, 99, 232);
        }

        private void OnTick(object sender, EventArgs e)
        {
            _t += 0.016f;
            ChromePainter.Time = _t;

            if (_closing)
            {
                _opacity -= 0.06f;
                if (_opacity <= 0f) { _timer.Stop(); Close(); return; }
                Opacity = _opacity;
            }
            else
            {
                if (_opacity < 1f)
                {
                    _opacity += 0.09f;
                    if (_opacity > 1f) _opacity = 1f;
                    Opacity = _opacity;
                }
                if (_t > 1.6f)
                {
                    _closing = true;
                }
            }
            Invalidate();
        }

        public void BeginClose()
        {
            if (!_closing) _closing = true;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (Current == this) Current = null;
            _timer.Dispose();
            base.OnFormClosed(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle bg = new Rectangle(0, 0, Width, Height);
            ChromePainter.PaintBackground(g, bg, Width);

            // Subtle inner border.
            using (Pen pen = new Pen(Color.FromArgb(60, ChromePainter.Accent), 1f))
                g.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);

            // Logo.
            int logoSize = 84;
            int logoX = (Width - logoSize) / 2;
            int logoY = 34;
            string iconPath = Path.Combine(Application.StartupPath, "icon.ico");
            if (File.Exists(iconPath))
            {
                try
                {
                    using (Icon ico = new Icon(iconPath))
                    using (Bitmap bmp = ico.ToBitmap())
                    {
                        g.DrawImage(bmp, logoX, logoY, logoSize, logoSize);
                    }
                }
                catch
                {
                    DrawLogoFallback(g, logoX, logoY, logoSize);
                }
            }
            else
            {
                DrawLogoFallback(g, logoX, logoY, logoSize);
            }

            // Title + tagline.
            using (Font title = new Font("Segoe UI Variable Display", 22f, FontStyle.Bold))
            using (Font sub = new Font("Segoe UI Variable Display", 11f))
            using (SolidBrush titleBrush = new SolidBrush(Color.FromArgb(238, 240, 246)))
            using (SolidBrush tagBrush = new SolidBrush(Color.FromArgb(150, 156, 170)))
            {
                string name = "Black Firefox";
                SizeF tSize = g.MeasureString(name, title);
                g.DrawString(name, title, titleBrush,
                    new PointF((Width - tSize.Width) / 2f, logoY + logoSize + 12));

                string tag = "Glassmorphic Edition  ·  Ad-Free";
                SizeF sSize = g.MeasureString(tag, sub);
                g.DrawString(tag, sub, tagBrush,
                    new PointF((Width - sSize.Width) / 2f, logoY + logoSize + 52));
            }

            // Progress bar with animated sweep.
            int barW = Width - 120;
            int barX = 60;
            int barY = Height - 56;
            Rectangle bar = new Rectangle(barX, barY, barW, 4);
            using (SolidBrush track = new SolidBrush(Color.FromArgb(50, ChromePainter.Accent)))
                g.FillRectangle(track, bar);

            float sweep = (_t % 1.4f) / 1.4f;
            float segW = 110f;
            float segX = barX + sweep * (barW + segW) - segW;
            using (LinearGradientBrush fill = new LinearGradientBrush(
                new RectangleF(segX, barY, segW, 4),
                Color.FromArgb(0, ChromePainter.Accent),
                Color.FromArgb(230, ChromePainter.Lighten(ChromePainter.Accent, 0.25f)),
                LinearGradientMode.Horizontal))
            {
                g.FillRectangle(fill, segX, barY, segW, 4);
            }

            base.OnPaint(e);
        }

        private void DrawLogoFallback(Graphics g, int x, int y, int s)
        {
            using (GraphicsPath p = new GraphicsPath())
            {
                Rectangle r = new Rectangle(x, y, s, s);
                int d = 22;
                p.AddArc(r.X, r.Y, d, d, 180, 90);
                p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
                p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
                p.CloseFigure();
                using (LinearGradientBrush b = new LinearGradientBrush(r,
                    ChromePainter.Lighten(ChromePainter.Accent, 0.3f),
                    ChromePainter.Accent, LinearGradientMode.ForwardDiagonal))
                    g.FillPath(b, p);
                using (Font f = new Font("Segoe MDL2 Assets", 44f))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 238, 245)))
                    g.DrawString("\uE774", f, b,
                        new RectangleF(x, y, s, s),
                        new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center });
            }
        }
    }
}