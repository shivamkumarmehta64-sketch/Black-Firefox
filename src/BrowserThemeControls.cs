using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlackBrowser
{
    public class DarkTabControl : TabControl
    {
        public DarkTabControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            DoubleBuffered = true;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0014) // WM_ERASEBKGND
            {
                m.Result = (IntPtr)1;
                return;
            }

            base.WndProc(ref m);

            if (m.Msg == 0x000F) // WM_PAINT
            {
                try
                {
                    using (Graphics g = Graphics.FromHwnd(this.Handle))
                    {
                        int stripH = this.DisplayRectangle.Y > 0 ? this.DisplayRectangle.Y : 46;
                        int rightEdge = 0;
                        if (this.TabCount > 0)
                        {
                            Rectangle last = this.GetTabRect(this.TabCount - 1);
                            rightEdge = last.Right;
                        }

                        Color bg = ChromePainter.IsDark ? Color.FromArgb(16, 16, 20) : Color.FromArgb(235, 236, 240);
                        Color border = ChromePainter.IsDark ? Color.FromArgb(28, 28, 34) : Color.FromArgb(218, 220, 224);

                        using (SolidBrush darkBrush = new SolidBrush(bg))
                        {
                            if (this.Width > rightEdge)
                            {
                                g.FillRectangle(darkBrush, rightEdge, 0, this.Width - rightEdge, stripH);
                            }
                            if (this.TabCount > 0)
                            {
                                Rectangle first = this.GetTabRect(0);
                                if (first.Left > 0)
                                {
                                    g.FillRectangle(darkBrush, 0, 0, first.Left, stripH);
                                }
                            }
                            else
                            {
                                g.FillRectangle(darkBrush, 0, 0, this.Width, stripH);
                            }
                        }

                        using (Pen borderPen = new Pen(border, 1))
                        {
                            g.DrawLine(borderPen, 0, stripH - 1, this.Width, stripH - 1);
                        }
                    }
                }
                catch { }
            }
        }
    }

    public class ChromeMenuRenderer : ToolStripProfessionalRenderer
    {
        public ChromeMenuRenderer() : base(new ChromeMenuColorTable()) { }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.Selected)
            {
                Rectangle rc = new Rectangle(4, 1, e.Item.Width - 8, e.Item.Height - 2);
                using (var b = new SolidBrush(Color.FromArgb(66, 133, 244)))
                {
                    e.Graphics.FillRectangle(b, rc);
                }
                e.Item.ForeColor = Color.White;
            }
            else
            {
                using (var b = new SolidBrush(Color.FromArgb(32, 33, 36)))
                {
                    e.Graphics.FillRectangle(b, new Rectangle(Point.Empty, e.Item.Size));
                }
                e.Item.ForeColor = Color.FromArgb(232, 234, 237);
            }
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(Color.FromArgb(32, 33, 36)))
            {
                e.Graphics.FillRectangle(b, e.AffectedBounds);
            }
        }
    }

    public class ChromeMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected { get { return Color.FromArgb(66, 133, 244); } }
        public override Color MenuItemBorder { get { return Color.FromArgb(66, 133, 244); } }
        public override Color MenuBorder { get { return Color.FromArgb(60, 64, 67); } }
        public override Color SeparatorDark { get { return Color.FromArgb(60, 64, 67); } }
        public override Color SeparatorLight { get { return Color.FromArgb(60, 64, 67); } }
    }
}
