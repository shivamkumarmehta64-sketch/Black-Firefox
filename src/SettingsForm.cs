using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BlackBrowser
{
    public class SettingsForm : Form
    {
        private TabControl tabControl;
        private TabPage generalPage;
        private TabPage eyeCarePage;
        private TabPage notesPage;
        private TabPage deviceInfoPage;

        private ComboBox themeCombo;
        private ComboBox eyeCareCombo;
        private TextBox notesTextBox;
        private Label sysInfoLabel;

        private Action<int> onThemeChanged;
        private Action<int> onEyeCareChanged;
        private string notesPath;

        public SettingsForm(int currentTheme, int currentEyeCare, Action<int> themeCallback, Action<int> eyeCareCallback, int initialTab = 0)
        {
            this.onThemeChanged = themeCallback;
            this.onEyeCareChanged = eyeCareCallback;

            notesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "black-webview2", "dark_notes.txt");

            this.Text = "Black Firefox — Settings & Dark Notes (Anime Theme)";
            this.Width = 640;
            this.Height = 500;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(14, 16, 28);
            this.ForeColor = Color.White;

            InitializeComponents(currentTheme, currentEyeCare);
            if (initialTab >= 0 && initialTab < tabControl.TabPages.Count)
            {
                tabControl.SelectedIndex = initialTab;
            }
            LoadNotes();
        }

        private void InitializeComponents(int currentTheme, int currentEyeCare)
        {
            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.Font = new Font("Segoe UI Variable Display", 9.5f);
            tabControl.Padding = new Point(14, 6);

            generalPage = new TabPage("⚙️ General & Theme");
            eyeCarePage = new TabPage("👁️ Eye Care & Screen");
            notesPage = new TabPage("📝 Dark Notes");
            deviceInfoPage = new TabPage("💻 Device & Hardware");

            generalPage.BackColor = Color.FromArgb(18, 20, 34);
            eyeCarePage.BackColor = Color.FromArgb(18, 20, 34);
            notesPage.BackColor = Color.FromArgb(12, 14, 24);
            deviceInfoPage.BackColor = Color.FromArgb(18, 20, 34);

            // ─── General & Theme Page ──────────────────────────────────────────────
            Label themeLbl = new Label();
            themeLbl.Text = "Browser Theme / Visual Style:";
            themeLbl.Location = new Point(24, 28);
            themeLbl.AutoSize = true;
            themeLbl.Font = new Font("Segoe UI Variable Display", 10f, FontStyle.Bold);
            themeLbl.ForeColor = Color.FromArgb(6, 182, 212);

            themeCombo = new ComboBox();
            themeCombo.Location = new Point(24, 58);
            themeCombo.Width = 340;
            themeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            themeCombo.Font = new Font("Segoe UI Variable Display", 10f);
            themeCombo.Items.Add("🌸 Anime Cyberpunk Neon Theme (Default)");
            themeCombo.Items.Add("🌙 Obsidian Dark Mode");
            themeCombo.SelectedIndex = currentTheme;

            themeCombo.SelectedIndexChanged += (s, e) =>
            {
                if (onThemeChanged != null) onThemeChanged(themeCombo.SelectedIndex);
            };

            generalPage.Controls.Add(themeLbl);
            generalPage.Controls.Add(themeCombo);

            // ─── Eye Care Page ─────────────────────────────────────────────────────
            Label eyeLbl = new Label();
            eyeLbl.Text = "Eye Care Overlay Filter:";
            eyeLbl.Location = new Point(24, 28);
            eyeLbl.AutoSize = true;
            eyeLbl.Font = new Font("Segoe UI Variable Display", 10f, FontStyle.Bold);
            eyeLbl.ForeColor = Color.FromArgb(147, 51, 234);

            eyeCareCombo = new ComboBox();
            eyeCareCombo.Location = new Point(24, 58);
            eyeCareCombo.Width = 340;
            eyeCareCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            eyeCareCombo.Font = new Font("Segoe UI Variable Display", 10f);
            eyeCareCombo.Items.Add("Disabled");
            eyeCareCombo.Items.Add("👁️ Warm Amber (Night Light Filter - 18%)");
            eyeCareCombo.Items.Add("🌙 Night Dimmer (Dark Screen Filter - 35%)");
            eyeCareCombo.SelectedIndex = currentEyeCare;

            eyeCareCombo.SelectedIndexChanged += (s, e) =>
            {
                if (onEyeCareChanged != null) onEyeCareChanged(eyeCareCombo.SelectedIndex);
            };

            eyeCarePage.Controls.Add(eyeLbl);
            eyeCarePage.Controls.Add(eyeCareCombo);

            // ─── Dark Notes Page ───────────────────────────────────────────────────
            Label notesHeader = new Label();
            notesHeader.Text = "📝 Quick Dark Notes (Auto-Saved Locally)";
            notesHeader.Dock = DockStyle.Top;
            notesHeader.Height = 32;
            notesHeader.Font = new Font("Segoe UI Variable Display", 10f, FontStyle.Bold);
            notesHeader.ForeColor = Color.FromArgb(6, 182, 212);
            notesHeader.Padding = new Padding(10, 6, 0, 0);

            notesTextBox = new TextBox();
            notesTextBox.Dock = DockStyle.Fill;
            notesTextBox.Multiline = true;
            notesTextBox.ScrollBars = ScrollBars.Vertical;
            notesTextBox.BackColor = Color.FromArgb(10, 12, 20);
            notesTextBox.ForeColor = Color.FromArgb(230, 235, 245);
            notesTextBox.Font = new Font("Consolas", 10.5f);
            notesTextBox.BorderStyle = BorderStyle.None;

            notesTextBox.TextChanged += (s, e) => SaveNotes();

            notesPage.Controls.Add(notesTextBox);
            notesPage.Controls.Add(notesHeader);

            // ─── Device Info Page ──────────────────────────────────────────────────
            sysInfoLabel = new Label();
            sysInfoLabel.Location = new Point(24, 28);
            sysInfoLabel.Size = new Size(560, 340);
            sysInfoLabel.Font = new Font("Consolas", 9.5f);
            sysInfoLabel.ForeColor = Color.FromArgb(200, 205, 225);

            UpdateDeviceInfo();

            deviceInfoPage.Controls.Add(sysInfoLabel);

            tabControl.TabPages.Add(generalPage);
            tabControl.TabPages.Add(eyeCarePage);
            tabControl.TabPages.Add(notesPage);
            tabControl.TabPages.Add(deviceInfoPage);

            this.Controls.Add(tabControl);
        }

        private void UpdateDeviceInfo()
        {
            Process proc = Process.GetCurrentProcess();
            double ramMB = Math.Round(proc.WorkingSet64 / (1024.0 * 1024.0), 2);

            sysInfoLabel.Text =
                "==========================================================\n" +
                "               Black Firefox DEVICE DIAGNOSTICS          \n" +
                "==========================================================\n\n" +
                " OS Version          : " + Environment.OSVersion.ToString() + "\n" +
                " 64-Bit OS           : " + (Environment.Is64BitOperatingSystem ? "Yes (x64)" : "No (x86)") + "\n" +
                " CPU Cores           : " + Environment.ProcessorCount.ToString() + " Logical Cores\n" +
                " Device Machine Name : " + Environment.MachineName + "\n" +
                " Current User        : " + Environment.UserName + "\n\n" +
                " Process ID          : " + proc.Id.ToString() + "\n" +
                " RAM Usage (Working) : " + ramMB.ToString() + " MB\n" +
                " Framework Runtime   : .NET Framework " + Environment.Version.ToString() + "\n" +
                " Rendering Engine    : Microsoft WebView2 (Chromium 128)\n" +
                " Visual Theme        : Anime Cyberpunk Neon Edition\n" +
                " 3-Layer Ad Shield   : Active\n" +
                "==========================================================";
        }

        private void LoadNotes()
        {
            try
            {
                if (File.Exists(notesPath))
                    notesTextBox.Text = File.ReadAllText(notesPath);
            }
            catch { }
        }

        private void SaveNotes()
        {
            try
            {
                string dir = Path.GetDirectoryName(notesPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(notesPath, notesTextBox.Text);
            }
            catch { }
        }
    }
}
