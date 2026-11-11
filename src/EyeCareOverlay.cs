using System;
using System.Drawing;
using System.Windows.Forms;

namespace BlackBrowser
{
    public class EyeCareOverlayForm : Form
    {
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_LAYERED = 0x80000;

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_LAYERED;
                return cp;
            }
        }

        private int _mode = 0;

        public EyeCareOverlayForm()
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;
            this.Bounds = Screen.PrimaryScreen.Bounds;
            this.BackColor = Color.Black;
            this.Opacity = 0.25;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        public void SyncToOwner(Form owner)
        {
            if (this.IsDisposed || owner == null || owner.IsDisposed) return;
            if (_mode == 0 || !owner.Visible || owner.WindowState == FormWindowState.Minimized)
            {
                if (this.Visible) this.Hide();
                return;
            }
            Rectangle targetBounds = owner.Bounds;
            if (targetBounds.Width > 0 && targetBounds.Height > 0 && this.Bounds != targetBounds)
            {
                this.Bounds = targetBounds;
            }
            if (!this.Visible) this.Show();
        }

        public void SetMode(int mode, Form owner = null)
        {
            _mode = mode;
            if (mode == 1)
            {
                this.BackColor = Color.FromArgb(255, 170, 0);
                this.Opacity = 0.18;
                if (owner != null) SyncToOwner(owner);
                else this.Show();
            }
            else if (mode == 2)
            {
                this.BackColor = Color.Black;
                this.Opacity = 0.35;
                if (owner != null) SyncToOwner(owner);
                else this.Show();
            }
            else
            {
                this.Hide();
            }
        }
    }
}
