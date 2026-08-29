using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;

namespace BlackBrowser
{
    public class QrCodeForm : Form
    {
        private readonly PictureBox qrBox;
        private readonly TextBox urlBox;

        public QrCodeForm(string url)
        {
            this.Text = "QR Code — Black Firefox";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.FromArgb(32, 33, 36);
            this.ForeColor = Color.White;
            this.ClientSize = new Size(340, 430);
            this.Font = new Font("Segoe UI", 10f);
            this.ShowInTaskbar = false;

            Label title = new Label
            {
                Text = "📱 Scan to open on your phone",
                ForeColor = Color.FromArgb(232, 234, 237),
                Location = new Point(20, 12),
                Size = new Size(300, 24),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10.5f, FontStyle.Bold)
            };

            qrBox = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(20, 44),
                Size = new Size(300, 300),
                BackColor = Color.White
            };

            urlBox = new TextBox
            {
                ReadOnly = true,
                Location = new Point(20, 352),
                Size = new Size(200, 28),
                BackColor = Color.FromArgb(44, 44, 54),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Text = url
            };

            Button copyBtn = new Button
            {
                Text = "📋 Copy URL",
                Location = new Point(230, 351),
                Size = new Size(90, 30),
                BackColor = Color.FromArgb(0, 96, 223),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            copyBtn.Click += (s, e) => { try { Clipboard.SetText(urlBox.Text); } catch { } };

            this.Controls.Add(title);
            this.Controls.Add(qrBox);
            this.Controls.Add(urlBox);
            this.Controls.Add(copyBtn);

            string api = "https://api.qrserver.com/v1/create-qr-code/?size=300x300&data=" + Uri.EscapeDataString(url);
            try
            {
                WebClient wc = new WebClient();
                wc.DownloadDataCompleted += (s, e) =>
                {
                    if (e.Error == null && e.Result != null && e.Result.Length > 0)
                    {
                        try
                        {
                            using (MemoryStream ms = new MemoryStream(e.Result))
                                qrBox.Image = Image.FromStream(ms);
                        }
                        catch { }
                    }
                };
                wc.DownloadDataAsync(new Uri(api));
            }
            catch { }
        }
    }
}