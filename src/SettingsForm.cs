using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Web.WebView2.Core;

namespace BlackBrowser
{
    public class SettingsForm : Form
    {
        // Windows 11 Fluent Design color palette
        private static readonly Color _bgForm = Color.FromArgb(20, 22, 28);
        private static readonly Color _cardBg = Color.FromArgb(28, 32, 40);
        private static readonly Color _textPrimary = Color.FromArgb(240, 242, 246);
        private static readonly Color _textSecondary = Color.FromArgb(140, 145, 155);
        private static readonly Color _accent = Color.FromArgb(0, 120, 215);
        private static readonly Color _accentHover = Color.FromArgb(0, 108, 198);

        private Action<int> _onThemeChanged;
        private Action<int> _onEyeCareChanged;
        private Action<string> _onNavigate;
        private Action _onPreferencesChanged;
        private string _notesPath;
        private ComboBox _themeCombo;
        private ComboBox _eyeCareCombo;
        private TextBox _notesTextBox;
        private bool _notesPlaceholderActive = false;
        private bool _loadingNotes = false;
        private readonly List<Font> _ownedFonts = new List<Font>();

        private Font TrackFont(string family, float size, FontStyle style = FontStyle.Regular)
        {
            Font f = new Font(family, size, style);
            _ownedFonts.Add(f);
            return f;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(this.Handle, 20, ref dark, 4); // DWMWA_USE_IMMERSIVE_DARK_MODE
                int round = 2;
                DwmSetWindowAttribute(this.Handle, 33, ref round, 4); // DWMWA_WINDOW_CORNER_PREFERENCE
            }
            catch { }
        }

        public SettingsForm(int currentTheme, int currentEyeCare, Action<int> themeCallback, Action<int> eyeCareCallback, int initialTab = 0, Action<string> navCallback = null, Action preferencesChangedCallback = null)
        {
            _onThemeChanged = themeCallback;
            _onEyeCareChanged = eyeCareCallback;
            _onNavigate = navCallback;
            _onPreferencesChanged = preferencesChangedCallback;
            _notesPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "black-webview2", "dark_notes.txt");

            this.Text = "Black Firefox — Settings";
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimizeBox = false;
            this.MaximizeBox = false;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.BackColor = _bgForm;
            this.ForeColor = _textPrimary;
            this.ClientSize = new Size(720, 480);
            this.MinimumSize = this.Size;

            InitializeComponents(currentTheme, currentEyeCare, initialTab);
        }

        private void ApplyDarkComboStyle(ComboBox cb)
        {
            cb.FlatStyle = FlatStyle.Flat;
            cb.BackColor = Color.FromArgb(24, 28, 36);
            cb.ForeColor = _textPrimary;
            cb.DrawMode = DrawMode.OwnerDrawFixed;
            cb.ItemHeight = 26;
            cb.DrawItem += (s, e) =>
            {
                if (e.Index < 0 || e.Index >= cb.Items.Count) return;
                bool selected = (e.State & DrawItemState.Selected) != 0;
                Color bg = selected ? _accent : Color.FromArgb(24, 28, 36);
                using (var b = new SolidBrush(bg))
                    e.Graphics.FillRectangle(b, e.Bounds);
                Color fg = selected ? Color.White : _textPrimary;
                TextRenderer.DrawText(e.Graphics, cb.Items[e.Index].ToString(), cb.Font,
                    new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height),
                    fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
        }

        private void OnSettingsTabDrawItem(object sender, DrawItemEventArgs e)
        {
            TabControl tc = sender as TabControl;
            if (tc == null || e.Index < 0 || e.Index >= tc.TabPages.Count) return;
            bool selected = (e.Index == tc.SelectedIndex);
            Color tabBg = selected ? _cardBg : Color.FromArgb(18, 20, 26);
            using (var b = new SolidBrush(tabBg))
                e.Graphics.FillRectangle(b, e.Bounds);

            if (selected)
            {
                using (var bar = new SolidBrush(_accent))
                    e.Graphics.FillRectangle(bar, e.Bounds.X + 8, e.Bounds.Bottom - 3, e.Bounds.Width - 16, 3);
            }

            Color fg = selected ? _textPrimary : _textSecondary;
            Font f = selected ? TrackFont("Segoe UI", 9.5f, FontStyle.Bold) : tc.Font;
            TextRenderer.DrawText(e.Graphics, tc.TabPages[e.Index].Text, f, e.Bounds, fg,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }

        private void NotifyPreferencesChanged()
        {
            if (_onPreferencesChanged != null) _onPreferencesChanged();
        }

        private void InitializeComponents(int currentTheme, int currentEyeCare, int initialTab)
        {
            this.SuspendLayout();

            var tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.Font = TrackFont("Segoe UI", 9.5f);
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.ItemSize = new Size(118, 34);
            tabControl.DrawItem += OnSettingsTabDrawItem;

            // ─── General & Theme Page ──────────────────────────────────────────────
            var generalPage = new TabPage("General");
            generalPage.BackColor = _cardBg;
            generalPage.Padding = new Padding(20);

            var themeLbl = new Label();
            themeLbl.Text = "Browser Theme / Visual Style:";
            themeLbl.Location = new Point(20, 16);
            themeLbl.AutoSize = true;
            themeLbl.Font = TrackFont("Segoe UI", 10f, FontStyle.Bold);
            themeLbl.ForeColor = _textPrimary;

            _themeCombo = new ComboBox();
            _themeCombo.Location = new Point(20, 42);
            _themeCombo.Width = 340;
            _themeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _themeCombo.Font = TrackFont("Segoe UI", 10f);
            _themeCombo.FlatStyle = FlatStyle.Flat;
            _themeCombo.BackColor = Color.FromArgb(24, 28, 36);
            _themeCombo.ForeColor = _textPrimary;
            _themeCombo.DrawMode = DrawMode.OwnerDrawVariable;
            _themeCombo.DrawItem += OnThemeDrawItem;
            _themeCombo.MeasureItem += OnThemeMeasureItem;
            _themeCombo.Items.Add("Anime Cyberpunk Neon Theme (Default)");
            _themeCombo.Items.Add("Obsidian Dark Mode");
            _themeCombo.SelectedIndex = (currentTheme >= 0 && currentTheme < _themeCombo.Items.Count) ? currentTheme : 0;
            _themeCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_onThemeChanged != null) _onThemeChanged(_themeCombo.SelectedIndex);
            };

            var searchLbl = new Label();
            searchLbl.Text = "Default Search Engine:";
            searchLbl.Location = new Point(20, 94);
            searchLbl.AutoSize = true;
            searchLbl.Font = TrackFont("Segoe UI", 9.5f, FontStyle.Bold);
            searchLbl.ForeColor = _textPrimary;

            var searchCombo = new ComboBox();
            searchCombo.Location = new Point(20, 118);
            searchCombo.Width = 220;
            searchCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            searchCombo.Font = TrackFont("Segoe UI", 9.5f);
            ApplyDarkComboStyle(searchCombo);
            string[] engines = new string[] { "google", "duckduckgo", "bing", "brave", "startpage", "ecosia", "youtube" };
            string[] engineLabels = new string[] { "Google", "DuckDuckGo", "Bing", "Brave Search", "Startpage", "Ecosia", "YouTube" };
            for (int i = 0; i < engineLabels.Length; i++) searchCombo.Items.Add(engineLabels[i]);
            int engIdx = Array.IndexOf(engines, (SettingsStore.SearchEngine ?? "google").ToLowerInvariant());
            searchCombo.SelectedIndex = engIdx >= 0 ? engIdx : 0;
            searchCombo.SelectedIndexChanged += (s, e) =>
            {
                if (searchCombo.SelectedIndex >= 0 && searchCombo.SelectedIndex < engines.Length)
                {
                    SettingsStore.SearchEngine = engines[searchCombo.SelectedIndex];
                    NotifyPreferencesChanged();
                }
            };

            var zoomLbl = new Label();
            zoomLbl.Text = "Default Zoom:";
            zoomLbl.Location = new Point(260, 94);
            zoomLbl.AutoSize = true;
            zoomLbl.Font = TrackFont("Segoe UI", 9.5f, FontStyle.Bold);
            zoomLbl.ForeColor = _textPrimary;

            var zoomCombo = new ComboBox();
            zoomCombo.Location = new Point(260, 118);
            zoomCombo.Width = 100;
            zoomCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            zoomCombo.Font = TrackFont("Segoe UI", 9.5f);
            ApplyDarkComboStyle(zoomCombo);
            double[] zoomVals = new double[] { 0.8, 0.9, 1.0, 1.1, 1.25, 1.5 };
            string[] zoomLabels = new string[] { "80%", "90%", "100%", "110%", "125%", "150%" };
            for (int i = 0; i < zoomLabels.Length; i++) zoomCombo.Items.Add(zoomLabels[i]);
            int bestZoomIdx = 2;
            for (int i = 0; i < zoomVals.Length; i++)
            {
                if (Math.Abs(SettingsStore.DefaultZoom - zoomVals[i]) < 0.05) { bestZoomIdx = i; break; }
            }
            zoomCombo.SelectedIndex = bestZoomIdx;
            zoomCombo.SelectedIndexChanged += (s, e) =>
            {
                if (zoomCombo.SelectedIndex >= 0 && zoomCombo.SelectedIndex < zoomVals.Length)
                {
                    SettingsStore.DefaultZoom = zoomVals[zoomCombo.SelectedIndex];
                    NotifyPreferencesChanged();
                }
            };

            var homeLbl = new Label();
            homeLbl.Text = "Home Page URL:";
            homeLbl.Location = new Point(20, 160);
            homeLbl.AutoSize = true;
            homeLbl.Font = TrackFont("Segoe UI", 9.5f, FontStyle.Bold);
            homeLbl.ForeColor = _textPrimary;

            var homeBox = new TextBox();
            homeBox.Location = new Point(20, 184);
            homeBox.Width = 340;
            homeBox.Font = TrackFont("Segoe UI", 9.5f);
            homeBox.BackColor = Color.FromArgb(20, 24, 32);
            homeBox.ForeColor = _textPrimary;
            homeBox.BorderStyle = BorderStyle.FixedSingle;
            homeBox.Text = string.IsNullOrEmpty(SettingsStore.HomePage) ? "black://home" : SettingsStore.HomePage;
            homeBox.Leave += (s, e) =>
            {
                string hp = (homeBox.Text ?? "").Trim();
                SettingsStore.HomePage = string.IsNullOrEmpty(hp) ? "black://home" : hp;
                NotifyPreferencesChanged();
            };

            var chkHomeBtn = new CheckBox();
            chkHomeBtn.Text = "Show Home button on toolbar";
            chkHomeBtn.Location = new Point(20, 224);
            chkHomeBtn.AutoSize = true;
            chkHomeBtn.Font = TrackFont("Segoe UI", 9.5f);
            chkHomeBtn.ForeColor = _textPrimary;
            chkHomeBtn.Checked = SettingsStore.ShowHomeButton;
            chkHomeBtn.CheckedChanged += (s, e) =>
            {
                SettingsStore.ShowHomeButton = chkHomeBtn.Checked;
                NotifyPreferencesChanged();
            };

            var chkHttps = new CheckBox();
            chkHttps.Text = "HTTPS-First Mode (auto-upgrade plain HTTP links)";
            chkHttps.Location = new Point(20, 254);
            chkHttps.AutoSize = true;
            chkHttps.Font = TrackFont("Segoe UI", 9.5f);
            chkHttps.ForeColor = _textPrimary;
            chkHttps.Checked = SettingsStore.HttpsFirstMode;
            chkHttps.CheckedChanged += (s, e) =>
            {
                SettingsStore.HttpsFirstMode = chkHttps.Checked;
                NotifyPreferencesChanged();
            };

            var chkAnimations = new CheckBox();
            chkAnimations.Text = "Enable glassmorphic UI animations";
            chkAnimations.Location = new Point(20, 284);
            chkAnimations.AutoSize = true;
            chkAnimations.Font = TrackFont("Segoe UI", 9.5f);
            chkAnimations.ForeColor = _textPrimary;
            chkAnimations.Checked = SettingsStore.AnimationsEnabled;
            chkAnimations.CheckedChanged += (s, e) =>
            {
                SettingsStore.AnimationsEnabled = chkAnimations.Checked;
                NotifyPreferencesChanged();
            };

            var chkForceDark = new CheckBox();
            chkForceDark.Text = "Prefer dark color scheme for websites";
            chkForceDark.Location = new Point(20, 314);
            chkForceDark.AutoSize = true;
            chkForceDark.Font = TrackFont("Segoe UI", 9.5f);
            chkForceDark.ForeColor = _textPrimary;
            chkForceDark.Checked = SettingsStore.ForceDarkMode;
            chkForceDark.CheckedChanged += (s, e) =>
            {
                SettingsStore.ForceDarkMode = chkForceDark.Checked;
                NotifyPreferencesChanged();
            };

            var chkRestore = new CheckBox();
            chkRestore.Text = "Restore previous open tabs on startup";
            chkRestore.Location = new Point(20, 344);
            chkRestore.AutoSize = true;
            chkRestore.Font = TrackFont("Segoe UI", 9.5f);
            chkRestore.ForeColor = _textPrimary;
            chkRestore.Checked = SettingsStore.RestoreSession;
            chkRestore.CheckedChanged += (s, e) =>
            {
                SettingsStore.RestoreSession = chkRestore.Checked;
                NotifyPreferencesChanged();
            };

            generalPage.Controls.Add(themeLbl);
            generalPage.Controls.Add(_themeCombo);
            generalPage.Controls.Add(searchLbl);
            generalPage.Controls.Add(searchCombo);
            generalPage.Controls.Add(zoomLbl);
            generalPage.Controls.Add(zoomCombo);
            generalPage.Controls.Add(homeLbl);
            generalPage.Controls.Add(homeBox);
            generalPage.Controls.Add(chkHomeBtn);
            generalPage.Controls.Add(chkHttps);
            generalPage.Controls.Add(chkAnimations);
            generalPage.Controls.Add(chkForceDark);
            generalPage.Controls.Add(chkRestore);

            // ─── Eye Care Page ─────────────────────────────────────────────────────
            var eyeCarePage = new TabPage("Eye Care");
            eyeCarePage.BackColor = _cardBg;
            eyeCarePage.Padding = new Padding(20);

            var eyeLbl = new Label();
            eyeLbl.Text = "Eye Care Overlay Filter:";
            eyeLbl.Location = new Point(0, 0);
            eyeLbl.AutoSize = true;
            eyeLbl.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            eyeLbl.ForeColor = _textPrimary;

            _eyeCareCombo = new ComboBox();
            _eyeCareCombo.Location = new Point(0, 30);
            _eyeCareCombo.Width = 360;
            _eyeCareCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _eyeCareCombo.Font = new Font("Segoe UI", 10f);
            _eyeCareCombo.FlatStyle = FlatStyle.Flat;
            _eyeCareCombo.BackColor = Color.FromArgb(24, 28, 36);
            _eyeCareCombo.ForeColor = _textPrimary;
            _eyeCareCombo.DrawMode = DrawMode.OwnerDrawVariable;
            _eyeCareCombo.DrawItem += OnEyeCareDrawItem;
            _eyeCareCombo.MeasureItem += OnEyeCareMeasureItem;
            _eyeCareCombo.Items.Add("Disabled");
            _eyeCareCombo.Items.Add("Warm Amber (Night Light Filter - 18%)");
            _eyeCareCombo.Items.Add("Night Dimmer (Dark Screen Filter - 35%)");
            _eyeCareCombo.SelectedIndex = (currentEyeCare >= 0 && currentEyeCare < _eyeCareCombo.Items.Count) ? currentEyeCare : 0;
            _eyeCareCombo.SelectedIndexChanged += (s, e) =>
            {
                if (_onEyeCareChanged != null) _onEyeCareChanged(_eyeCareCombo.SelectedIndex);
            };

            eyeCarePage.Controls.Add(eyeLbl);
            eyeCarePage.Controls.Add(_eyeCareCombo);

            // ─── Dark Notes Page ───────────────────────────────────────────────────
            var notesPage = new TabPage("Dark Notes");
            notesPage.BackColor = _cardBg;
            notesPage.Padding = new Padding(12);

            var notesHeader = new Label();
            notesHeader.Text = "Quick Dark Notes (Auto-Saved Locally)";
            notesHeader.Dock = DockStyle.Top;
            notesHeader.Height = 32;
            notesHeader.Font = TrackFont("Segoe UI", 10f, FontStyle.Bold);
            notesHeader.ForeColor = _textPrimary;

            _notesTextBox = new TextBox();
            _notesTextBox.Dock = DockStyle.Fill;
            _notesTextBox.Multiline = true;
            _notesTextBox.ScrollBars = ScrollBars.Vertical;
            _notesTextBox.BackColor = Color.FromArgb(18, 22, 30);
            _notesTextBox.ForeColor = _textSecondary;
            _notesTextBox.Font = TrackFont("Segoe UI", 10f);
            _notesTextBox.BorderStyle = BorderStyle.None;

            string existingNotes = "";
            try
            {
                if (File.Exists(_notesPath))
                    existingNotes = File.ReadAllText(_notesPath, System.Text.Encoding.UTF8);
            }
            catch { }

            _loadingNotes = true;
            if (!string.IsNullOrEmpty(existingNotes))
            {
                _notesPlaceholderActive = false;
                _notesTextBox.ForeColor = _textPrimary;
                _notesTextBox.Text = existingNotes;
            }
            else
            {
                _notesPlaceholderActive = true;
                _notesTextBox.ForeColor = _textSecondary;
                _notesTextBox.Text = "Quick note...";
            }
            _loadingNotes = false;

            _notesTextBox.TextChanged += (s, e) =>
            {
                if (!_loadingNotes && !_notesPlaceholderActive) SaveNotes();
            };

            _notesTextBox.Enter += (s, e) =>
            {
                if (_notesPlaceholderActive)
                {
                    _loadingNotes = true;
                    _notesPlaceholderActive = false;
                    _notesTextBox.Text = "";
                    _notesTextBox.ForeColor = _textPrimary;
                    _loadingNotes = false;
                }
            };
            _notesTextBox.Leave += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_notesTextBox.Text))
                {
                    _loadingNotes = true;
                    _notesPlaceholderActive = true;
                    _notesTextBox.Text = "Quick note...";
                    _notesTextBox.ForeColor = _textSecondary;
                    _loadingNotes = false;
                }
            };

            notesPage.Controls.Add(_notesTextBox);
            notesPage.Controls.Add(notesHeader);

            // ─── Device Info Page ──────────────────────────────────────────────────
            var devicePage = new TabPage("Device");
            devicePage.BackColor = _cardBg;
            devicePage.Padding = new Padding(20);

            var infoLabel = new Label();
            infoLabel.Dock = DockStyle.Fill;
            infoLabel.Font = new Font("Consolas", 9f);
            infoLabel.ForeColor = _textSecondary;
            infoLabel.Text = BuildDeviceInfoText();

            devicePage.Controls.Add(infoLabel);

            // ─── Legal & Branding Page ─────────────────────────────────────────────
            var legalPage = new TabPage("About & Legal");
            legalPage.BackColor = _cardBg;
            legalPage.Padding = new Padding(20);

            var legalTitle = new Label();
            legalTitle.Text = "Black Firefox Legal & Governance";
            legalTitle.Location = new Point(0, 0);
            legalTitle.AutoSize = true;
            legalTitle.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            legalTitle.ForeColor = _textPrimary;

            var legalDesc = new Label();
            legalDesc.Text = "Click any link below to view application governance, terms, zero-telemetry privacy policy, or trademark notices:";
            legalDesc.Location = new Point(0, 26);
            legalDesc.Size = new Size(460, 40);
            legalDesc.Font = new Font("Segoe UI", 9f);
            legalDesc.ForeColor = _textSecondary;

            var linkAbout = new LinkLabel();
            linkAbout.Text = "• black://about — Application Overview & Architecture";
            linkAbout.Location = new Point(0, 75);
            linkAbout.AutoSize = true;
            linkAbout.LinkColor = Color.FromArgb(96, 165, 250);
            linkAbout.LinkClicked += (s, e) => { if (_onNavigate != null) _onNavigate("black://about"); };

            var linkTerms = new LinkLabel();
            linkTerms.Text = "• black://terms — Terms of Service (MIT License)";
            linkTerms.Location = new Point(0, 105);
            linkTerms.AutoSize = true;
            linkTerms.LinkColor = Color.FromArgb(96, 165, 250);
            linkTerms.LinkClicked += (s, e) => { if (_onNavigate != null) _onNavigate("black://terms"); };

            var linkPrivacy = new LinkLabel();
            linkPrivacy.Text = "• black://privacy — Privacy Policy (Zero Telemetry Guarantee)";
            linkPrivacy.Location = new Point(0, 135);
            linkPrivacy.AutoSize = true;
            linkPrivacy.LinkColor = Color.FromArgb(96, 165, 250);
            linkPrivacy.LinkClicked += (s, e) => { if (_onNavigate != null) _onNavigate("black://privacy"); };

            var linkTrademark = new LinkLabel();
            linkTrademark.Text = "• black://trademark — Trademark Guidelines & Mozilla Disclaimers";
            linkTrademark.Location = new Point(0, 165);
            linkTrademark.AutoSize = true;
            linkTrademark.LinkColor = Color.FromArgb(96, 165, 250);
            linkTrademark.LinkClicked += (s, e) => { if (_onNavigate != null) _onNavigate("black://trademark"); };

            legalPage.Controls.Add(legalTitle);
            legalPage.Controls.Add(legalDesc);
            legalPage.Controls.Add(linkAbout);
            legalPage.Controls.Add(linkTerms);
            legalPage.Controls.Add(linkPrivacy);
            legalPage.Controls.Add(linkTrademark);

            // ─── Customization Page ──────────────────────────────────────────────
            var customPage = new TabPage("Theme");
            customPage.BackColor = _cardBg;
            customPage.Padding = new Padding(20);

            var customTitle = new Label();
            customTitle.Text = "Speed Dial Theme & Widget Customization";
            customTitle.Location = new Point(20, 20);
            customTitle.AutoSize = true;
            customTitle.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            customTitle.ForeColor = _textPrimary;

            var bgLabel = new Label();
            bgLabel.Text = "Speed Dial Background Preset:";
            bgLabel.Location = new Point(20, 55);
            bgLabel.AutoSize = true;
            bgLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            bgLabel.ForeColor = _textSecondary;

            var bgCombo = new ComboBox();
            bgCombo.Location = new Point(20, 80);
            bgCombo.Width = 260;
            bgCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            bgCombo.Font = new Font("Segoe UI", 9.5f);
            ApplyDarkComboStyle(bgCombo);
            bgCombo.Items.Add("Aurora Glassmorphism");
            bgCombo.Items.Add("Cyberpunk Neon Glow");
            bgCombo.Items.Add("Deep Space Nebula");
            bgCombo.Items.Add("Obsidian Minimalist");
            bgCombo.Items.Add("Light Glassmorphism");

            string currentBg = SettingsStore.SpeedDialBg.ToLowerInvariant();
            if (currentBg == "cyberpunk") bgCombo.SelectedIndex = 1;
            else if (currentBg == "nebula") bgCombo.SelectedIndex = 2;
            else if (currentBg == "obsidian") bgCombo.SelectedIndex = 3;
            else if (currentBg == "light") bgCombo.SelectedIndex = 4;
            else bgCombo.SelectedIndex = 0;

            bgCombo.SelectedIndexChanged += (s, e) =>
            {
                if (bgCombo.SelectedIndex == 1) SettingsStore.SpeedDialBg = "cyberpunk";
                else if (bgCombo.SelectedIndex == 2) SettingsStore.SpeedDialBg = "nebula";
                else if (bgCombo.SelectedIndex == 3) SettingsStore.SpeedDialBg = "obsidian";
                else if (bgCombo.SelectedIndex == 4) SettingsStore.SpeedDialBg = "light";
                else SettingsStore.SpeedDialBg = "aurora";
            };

            var widgetLabel = new Label();
            widgetLabel.Text = "Toggle Home Widgets:";
            widgetLabel.Location = new Point(320, 55);
            widgetLabel.AutoSize = true;
            widgetLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            widgetLabel.ForeColor = _textSecondary;

            var chkClock = new CheckBox();
            chkClock.Text = "Digital Clock & Date";
            chkClock.Location = new Point(320, 80);
            chkClock.AutoSize = true;
            chkClock.Font = new Font("Segoe UI", 9.5f);
            chkClock.ForeColor = _textPrimary;
            chkClock.Checked = SettingsStore.ShowClock;
            chkClock.CheckedChanged += (s, e) => SettingsStore.ShowClock = chkClock.Checked;

            var chkNotes = new CheckBox();
            chkNotes.Text = "Scratchpad Widget";
            chkNotes.Location = new Point(320, 110);
            chkNotes.AutoSize = true;
            chkNotes.Font = new Font("Segoe UI", 9.5f);
            chkNotes.ForeColor = _textPrimary;
            chkNotes.Checked = SettingsStore.ShowNotes;
            chkNotes.CheckedChanged += (s, e) => SettingsStore.ShowNotes = chkNotes.Checked;

            var chkTelemetry = new CheckBox();
            chkTelemetry.Text = "Telemetry Status Badges";
            chkTelemetry.Location = new Point(320, 140);
            chkTelemetry.AutoSize = true;
            chkTelemetry.Font = new Font("Segoe UI", 9.5f);
            chkTelemetry.ForeColor = _textPrimary;
            chkTelemetry.Checked = SettingsStore.ShowTelemetry;
            chkTelemetry.CheckedChanged += (s, e) => SettingsStore.ShowTelemetry = chkTelemetry.Checked;

            var accentLabel = new Label();
            accentLabel.Text = "UI Theme Accent Preset:";
            accentLabel.Location = new Point(20, 140);
            accentLabel.AutoSize = true;
            accentLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            accentLabel.ForeColor = _textSecondary;

            var accentCombo = new ComboBox();
            accentCombo.Location = new Point(20, 165);
            accentCombo.Width = 260;
            accentCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            accentCombo.Font = new Font("Segoe UI", 9.5f);
            ApplyDarkComboStyle(accentCombo);
            accentCombo.Items.Add("Classic Blue (#0C63E8)");
            accentCombo.Items.Add("Cyberpunk Pink (#ec4899)");
            accentCombo.Items.Add("Aurora Emerald (#10b981)");
            accentCombo.Items.Add("Amber Gold (#f59e0b)");
            accentCombo.Items.Add("Nebula Purple (#8b5cf6)");

            string currentAcc = SettingsStore.Accent.ToLowerInvariant();
            if (currentAcc == "#ec4899") accentCombo.SelectedIndex = 1;
            else if (currentAcc == "#10b981") accentCombo.SelectedIndex = 2;
            else if (currentAcc == "#f59e0b") accentCombo.SelectedIndex = 3;
            else if (currentAcc == "#8b5cf6") accentCombo.SelectedIndex = 4;
            else accentCombo.SelectedIndex = 0;

            accentCombo.SelectedIndexChanged += (s, e) =>
            {
                if (accentCombo.SelectedIndex == 1) SettingsStore.Accent = "#ec4899";
                else if (accentCombo.SelectedIndex == 2) SettingsStore.Accent = "#10b981";
                else if (accentCombo.SelectedIndex == 3) SettingsStore.Accent = "#f59e0b";
                else if (accentCombo.SelectedIndex == 4) SettingsStore.Accent = "#8b5cf6";
                else SettingsStore.Accent = "#0C63E8";
                NotifyPreferencesChanged();
            };

            var roundLabel = new Label();
            roundLabel.Text = "UI Corner Roundness Style:";
            roundLabel.Location = new Point(20, 215);
            roundLabel.AutoSize = true;
            roundLabel.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            roundLabel.ForeColor = _textSecondary;

            var roundCombo = new ComboBox();
            roundCombo.Location = new Point(20, 240);
            roundCombo.Width = 260;
            roundCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            roundCombo.Font = new Font("Segoe UI", 9.5f);
            ApplyDarkComboStyle(roundCombo);
            roundCombo.Items.Add("Sharp Modern (0px)");
            roundCombo.Items.Add("Standard Rounded (8px)");
            roundCombo.Items.Add("Pill Style (12px)");
            roundCombo.Items.Add("Fluent Extra Curved (20px)");

            int currentRad = SettingsStore.CornerRadius;
            if (currentRad == 0) roundCombo.SelectedIndex = 0;
            else if (currentRad == 8) roundCombo.SelectedIndex = 1;
            else if (currentRad == 20) roundCombo.SelectedIndex = 3;
            else roundCombo.SelectedIndex = 2;

            roundCombo.SelectedIndexChanged += (s, e) =>
            {
                if (roundCombo.SelectedIndex == 0) SettingsStore.CornerRadius = 0;
                else if (roundCombo.SelectedIndex == 1) SettingsStore.CornerRadius = 8;
                else if (roundCombo.SelectedIndex == 3) SettingsStore.CornerRadius = 20;
                else SettingsStore.CornerRadius = 12;
                NotifyPreferencesChanged();
            };

            customPage.Controls.Add(customTitle);
            customPage.Controls.Add(bgLabel);
            customPage.Controls.Add(bgCombo);
            customPage.Controls.Add(widgetLabel);
            customPage.Controls.Add(chkClock);
            customPage.Controls.Add(chkNotes);
            customPage.Controls.Add(chkTelemetry);
            customPage.Controls.Add(accentLabel);
            customPage.Controls.Add(accentCombo);
            customPage.Controls.Add(roundLabel);
            customPage.Controls.Add(roundCombo);

            tabControl.TabPages.Add(generalPage);
            tabControl.TabPages.Add(customPage);
            tabControl.TabPages.Add(eyeCarePage);
            tabControl.TabPages.Add(notesPage);
            tabControl.TabPages.Add(devicePage);
            tabControl.TabPages.Add(legalPage);
            if (initialTab >= 0 && initialTab < tabControl.TabPages.Count)
                tabControl.SelectedIndex = initialTab;

            this.Controls.Add(tabControl);
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private void OnThemeMeasureItem(object sender, MeasureItemEventArgs e)
        {
            e.ItemHeight = 40;
        }

        private void OnThemeDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _themeCombo.Items.Count) return;

            e.DrawBackground();
            var accent = (e.Index == 0) ? _accent : Color.FromArgb(200, 200, 200);
            using (var accentBrush = new SolidBrush(accent))
                e.Graphics.FillRectangle(accentBrush, e.Bounds.X, e.Bounds.Y, 4, e.Bounds.Height);

            var selected = (e.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(selected ? _textPrimary : _textSecondary))
                e.Graphics.DrawString(_themeCombo.Items[e.Index].ToString(), _themeCombo.Font,
                    brush, e.Bounds.X + 12, e.Bounds.Y + (e.Bounds.Height - _themeCombo.Font.Height) / 2);
            e.DrawFocusRectangle();
        }

        private void OnEyeCareMeasureItem(object sender, MeasureItemEventArgs e)
        {
            e.ItemHeight = 34;
        }

        private void OnEyeCareDrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _eyeCareCombo.Items.Count) return;

            e.DrawBackground();
            var selected = (e.State & DrawItemState.Selected) != 0;
            using (var brush = new SolidBrush(selected ? _textPrimary : _textSecondary))
                e.Graphics.DrawString(_eyeCareCombo.Items[e.Index].ToString(), _eyeCareCombo.Font,
                    brush, e.Bounds.X + 8, e.Bounds.Y + (e.Bounds.Height - _eyeCareCombo.Font.Height) / 2);
            e.DrawFocusRectangle();
        }

        private string BuildDeviceInfoText()
        {
            try
            {
                var proc = Process.GetCurrentProcess();
                var ramMB = Math.Round(proc.WorkingSet64 / (1024.0 * 1024.0), 2);
                string wvVer = "Microsoft WebView2";
                try
                {
                    string v = CoreWebView2Environment.GetAvailableBrowserVersionString();
                    if (!string.IsNullOrEmpty(v)) wvVer = "Microsoft WebView2 (" + v + ")";
                }
                catch { }

                return
                    "BLACK FIREFOX DEVICE DIAGNOSTICS\n\n" +
                    "OS:       " + Environment.OSVersion + "\n" +
                    "Arch:     " + (Environment.Is64BitOperatingSystem ? "x64" : "x86") + "\n" +
                    "CPU:      " + Environment.ProcessorCount + " cores\n" +
                    "Machine:  " + Environment.MachineName + "\n" +
                    "User:     " + Environment.UserName + "\n\n" +
                    "PID:      " + proc.Id + "\n" +
                    "RAM:      " + ramMB + " MB\n" +
                    "Engine:   " + wvVer + "\n" +
                    "Theme:    Anime Cyberpunk Neon Edition\n" +
                    "AdShield: 3-Layer Active";
            }
            catch
            {
                return "Unable to load device info.";
            }
        }

        private void SaveNotes()
        {
            if (_notesPlaceholderActive) return;
            try
            {
                var dir = Path.GetDirectoryName(_notesPath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(_notesPath, _notesTextBox.Text, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            SaveNotes();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Font f in _ownedFonts)
                {
                    try { if (f != null) f.Dispose(); } catch { }
                }
                _ownedFonts.Clear();
            }
            base.Dispose(disposing);
        }
    }
}