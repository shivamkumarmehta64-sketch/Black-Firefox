using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace BlackBrowser
{
    public enum VectorIcon
    {
        None,
        Back,
        Forward,
        Reload,
        Home,
        Star,
        StarFilled,
        Reader,
        Downloads,
        Menu,
        Plus
    }

    public class GlassButton : Button
    {
        public Color AccentColor { get; set; }
        public Color HoverTint { get; set; }
        public float GlowStrength { get; set; }
        public VectorIcon IconType { get; set; }

        private float _hover = 0f;
        private float _press = 0f;
        private bool _hovering = false;
        private bool _pressed = false;
        private Timer _anim;

        public bool IsCircle { get; set; }
        public int CornerRadius { get; set; }

        public GlassButton()
        {
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            DoubleBuffered = true;
            IsCircle = false;
            CornerRadius = 10;
            AccentColor = Color.Empty;
            HoverTint = Color.Empty;
            GlowStrength = 0.14f;
            IconType = VectorIcon.None;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            BackColor = Color.Transparent;
            ForeColor = Color.FromArgb(215, 220, 235);
            Cursor = Cursors.Hand;
            TabStop = false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_anim != null)
                {
                    _anim.Stop();
                    _anim.Dispose();
                    _anim = null;
                }
            }
            base.Dispose(disposing);
        }

        private GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void StartAnim()
        {
            if (_anim == null)
            {
                _anim = new Timer();
                _anim.Interval = 16;
                _anim.Tick += (s, e) => Step();
            }
            _anim.Start();
        }

        private void Step()
        {
            float targetH = _hovering ? 1f : 0f;
            float targetP = _pressed ? 1f : 0f;
            _hover += (targetH - _hover) * 0.28f;
            _press += (targetP - _press) * 0.4f;
            if (Math.Abs(_hover - targetH) < 0.01f) _hover = targetH;
            if (Math.Abs(_press - targetP) < 0.01f) _press = targetP;
            Invalidate();
            if (Math.Abs(_hover - targetH) < 0.01f && Math.Abs(_press - targetP) < 0.01f)
                _anim.Stop();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovering = true;
            if (ChromePainter.Enabled) StartAnim(); else { _hover = 1f; Invalidate(); }
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovering = false;
            _pressed = false;
            if (ChromePainter.Enabled) StartAnim(); else { _hover = 0f; Invalidate(); }
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs mevent)
        {
            _pressed = true;
            if (ChromePainter.Enabled) StartAnim(); else { _press = 1f; Invalidate(); }
            base.OnMouseDown(mevent);
        }

        protected override void OnMouseUp(MouseEventArgs mevent)
        {
            _pressed = false;
            if (ChromePainter.Enabled) StartAnim(); else { _press = 0f; Invalidate(); }
            base.OnMouseUp(mevent);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // Background is cleanly painted in OnPaint to prevent GDI+ alpha stacking
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            Graphics g = pevent.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle rect = new Rectangle(0, 0, Width, Height);

            // 1. Fill with parent background to eliminate dark alpha-multiplication overlay artifacts
            Color parentBg = (BackColor != Color.Transparent && BackColor != Color.Empty)
                ? BackColor
                : (Parent != null && Parent.BackColor != Color.Transparent && Parent.BackColor != Color.Empty)
                    ? Parent.BackColor
                    : (ChromePainter.IsDark ? Color.FromArgb(28, 28, 34) : Color.FromArgb(243, 243, 243));

            using (SolidBrush bgBrush = new SolidBrush(parentBg))
            {
                g.FillRectangle(bgBrush, rect);
            }

            // 2. Draw hover/press pill
            float h = _hover;
            if (h > 0.01f || _press > 0.01f)
            {
                Color hoverBase = HoverTint != Color.Empty
                    ? HoverTint
                    : (ChromePainter.IsDark ? Color.FromArgb(255, 255, 255) : Color.FromArgb(0, 0, 0));

                int alphaHover = ChromePainter.IsDark ? 28 : 18;
                int alphaPress = ChromePainter.IsDark ? 45 : 32;

                Color fill = Color.FromArgb((int)(alphaHover * Math.Max(h, _press)), hoverBase);
                Color pressFill = Color.FromArgb((int)(alphaPress * _press), hoverBase);

                int radius = IsCircle ? Math.Min(rect.Width, rect.Height) / 2 : CornerRadius;
                using (GraphicsPath p = RoundedRect(rect, radius))
                {
                    Color c = _press > 0.01f ? pressFill : fill;
                    using (SolidBrush b = new SolidBrush(c))
                        g.FillPath(b, p);

                    if (h > 0.01f)
                    {
                        if (AccentColor != Color.Empty)
                        {
                            using (Pen pen = new Pen(Color.FromArgb((int)(140 * h), AccentColor), 1f))
                                g.DrawPath(pen, p);
                        }
                        else
                        {
                            Color borderCol = ChromePainter.IsDark
                                ? Color.FromArgb((int)(35 * h), 255, 255, 255)
                                : Color.FromArgb((int)(25 * h), 0, 0, 0);
                            using (Pen pen = new Pen(borderCol, 1f))
                                g.DrawPath(pen, p);
                        }
                    }
                }
            }

            // 3. Draw Icon / Image / Text
            Color iconColor = Enabled ? ForeColor : (ChromePainter.IsDark ? Color.FromArgb(100, 105, 120) : Color.FromArgb(160, 165, 175));

            if (IconType != VectorIcon.None)
            {
                DrawVectorIcon(g, rect, iconColor);
            }
            else if (Image != null)
            {
                int s = Math.Min(24, Math.Max(16, Height - 14));
                int ix = (Width - s) / 2;
                int iy = (Height - s) / 2;
                g.DrawImage(Image, ix, iy, s, s);
            }
            else if (!string.IsNullOrEmpty(Text))
            {
                TextRenderer.DrawText(g, Text, Font, rect, iconColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void DrawVectorIcon(Graphics g, Rectangle rect, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height / 2f;

            using (Pen pen = new Pen(color, 2.2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;

                switch (IconType)
                {
                    case VectorIcon.Back:
                        g.DrawLine(pen, cx + 5.5f, cy, cx - 5.5f, cy);
                        g.DrawLine(pen, cx - 1.5f, cy - 4.5f, cx - 5.5f, cy);
                        g.DrawLine(pen, cx - 1.5f, cy + 4.5f, cx - 5.5f, cy);
                        break;

                    case VectorIcon.Forward:
                        g.DrawLine(pen, cx - 5.5f, cy, cx + 5.5f, cy);
                        g.DrawLine(pen, cx + 1.5f, cy - 4.5f, cx + 5.5f, cy);
                        g.DrawLine(pen, cx + 1.5f, cy + 4.5f, cx + 5.5f, cy);
                        break;

                    case VectorIcon.Reload:
                        g.DrawArc(pen, cx - 6f, cy - 6f, 12f, 12f, 45f, 270f);
                        g.DrawLine(pen, cx + 4f, cy - 5f, cx + 6.5f, cy - 2.5f);
                        g.DrawLine(pen, cx + 3.5f, cy + 0.5f, cx + 6.5f, cy - 2.5f);
                        break;

                    case VectorIcon.Home:
                        g.DrawLine(pen, cx - 7f, cy - 1f, cx, cy - 7f);
                        g.DrawLine(pen, cx, cy - 7f, cx + 7f, cy - 1f);
                        g.DrawLine(pen, cx - 5.5f, cy - 1f, cx - 5.5f, cy + 6f);
                        g.DrawLine(pen, cx + 5.5f, cy - 1f, cx + 5.5f, cy + 6f);
                        g.DrawLine(pen, cx - 5.5f, cy + 6f, cx + 5.5f, cy + 6f);
                        g.DrawLine(pen, cx - 2f, cy + 6f, cx - 2f, cy + 2.5f);
                        g.DrawLine(pen, cx - 2f, cy + 2.5f, cx + 2f, cy + 2.5f);
                        g.DrawLine(pen, cx + 2f, cy + 2.5f, cx + 2f, cy + 6f);
                        break;

                    case VectorIcon.Star:
                        DrawStar(g, cx, cy, 7.5f, 3.5f, false, color);
                        break;

                    case VectorIcon.StarFilled:
                        DrawStar(g, cx, cy, 7.5f, 3.5f, true, color);
                        break;

                    case VectorIcon.Reader:
                        g.DrawRectangle(pen, cx - 5.5f, cy - 7f, 11f, 14f);
                        g.DrawLine(pen, cx - 3f, cy - 3f, cx + 3f, cy - 3f);
                        g.DrawLine(pen, cx - 3f, cy + 1f, cx + 3f, cy + 1f);
                        break;

                    case VectorIcon.Downloads:
                        g.DrawLine(pen, cx, cy - 6f, cx, cy + 2f);
                        g.DrawLine(pen, cx - 4f, cy - 1.5f, cx, cy + 2.5f);
                        g.DrawLine(pen, cx + 4f, cy - 1.5f, cx, cy + 2.5f);
                        g.DrawLine(pen, cx - 6f, cy + 6f, cx + 6f, cy + 6f);
                        break;

                    case VectorIcon.Menu:
                        // Crisp 3-line hamburger menu icon
                        g.DrawLine(pen, cx - 6f, cy - 4.5f, cx + 6f, cy - 4.5f);
                        g.DrawLine(pen, cx - 6f, cy, cx + 6f, cy);
                        g.DrawLine(pen, cx - 6f, cy + 4.5f, cx + 6f, cy + 4.5f);
                        break;

                    case VectorIcon.Plus:
                        using (Pen plusPen = new Pen(color, 2.4f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        {
                            g.DrawLine(plusPen, cx - 5.5f, cy, cx + 5.5f, cy);
                            g.DrawLine(plusPen, cx, cy - 5.5f, cx, cy + 5.5f);
                        }
                        break;
                }
            }
        }

        private static void DrawStar(Graphics g, float cx, float cy, float rOuter, float rInner, bool filled, Color color)
        {
            PointF[] pts = new PointF[10];
            double angle = -Math.PI / 2.0;
            double dAngle = Math.PI / 5.0;

            for (int i = 0; i < 10; i++)
            {
                float r = (i % 2 == 0) ? rOuter : rInner;
                pts[i] = new PointF((float)(cx + r * Math.Cos(angle)), (float)(cy + r * Math.Sin(angle)));
                angle += dAngle;
            }

            if (filled)
            {
                using (SolidBrush b = new SolidBrush(color))
                    g.FillPolygon(b, pts);
            }
            using (Pen p = new Pen(color, 1.8f) { LineJoin = LineJoin.Round })
                g.DrawPolygon(p, pts);
        }
    }
}