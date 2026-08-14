using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BlackBrowser
{
    public class BrowserForm : Form
    {
        private Panel headerContainer;
        private Panel omniboxPanel;
        private FlowLayoutPanel actionsPanel;
        private Panel softBanner;
        private Label softBannerLabel;
        private TabControl tabControl;

        private Button backBtn;
        private Button fwdBtn;
        private Button reloadBtn;
        private Button homeBtn;
        private TextBox urlBar;
        private Button starBtn;
        private Button shieldBtn;
        private Button eyeCareBtn;
        private Button notesBtn;
        private Button settingsBtn;
        private Button extBtn;
        private Button menuBtn;
        private Button addTabBtn;
        private Button ramBtn;

        private ContextMenuStrip mainMenu;
        private ContextMenuStrip tabContextMenu;
        private TabPage rightClickedTab;

        private NotifyIcon trayIcon;
        private Timer gcTimer;
        private Timer ramTimer;
        private Timer bannerTimer;

        private EyeCareOverlayForm eyeCareOverlay;
        private int eyeCareMode = 0;
        private bool isDarkMode = true;

        private CoreWebView2Environment webViewEnv;
        private int totalBlockedAds = 0;
        private string logPath;
        private Stack<Tuple<string, string>> closedTabStack = new Stack<Tuple<string, string>>();
        private string initialStartupUrl = "black://home";

        public BrowserForm(string[] args = null)
        {
            logPath = Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "debug.log");
            Log("=== Black Browser starting (Black Firefox Glassmorphic Edition v8.6) ===");

            if (args != null && args.Length > 0)
            {
                string firstArg = args[0].Trim();
                if (firstArg.IndexOf("music.youtube.com", StringComparison.OrdinalIgnoreCase) >= 0 || firstArg.Equals("--ytmusic", StringComparison.OrdinalIgnoreCase))
                {
                    initialStartupUrl = "https://music.youtube.com";
                    this.Text = "🎵 YouTube Music Desktop — Black Browser (Ad-Free)";
                }
                else if (firstArg.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0 || firstArg.Equals("--youtube", StringComparison.OrdinalIgnoreCase))
                {
                    initialStartupUrl = "https://www.youtube.com";
                    this.Text = "▶ YouTube Desktop — Black Browser (Ad-Free)";
                }
                else if (firstArg.StartsWith("http", StringComparison.OrdinalIgnoreCase) || firstArg.StartsWith("black://", StringComparison.OrdinalIgnoreCase))
                {
                    initialStartupUrl = firstArg;
                }
            }

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.DoubleBuffered = true;

            if (string.IsNullOrEmpty(this.Text) || this.Text == "Black Browser")
                this.Text = "Black Browser";

            this.Width = 1280;
            this.Height = 820;
            this.BackColor = Color.FromArgb(18, 18, 22);
            this.MinimumSize = new Size(900, 600);
            this.StartPosition = FormStartPosition.CenterScreen;

            string iconPath = Path.Combine(Application.StartupPath, "icon.ico");
            if (File.Exists(iconPath))
                this.Icon = new Icon(iconPath);

            eyeCareOverlay = new EyeCareOverlayForm();

            InitializeUI();
            InitializeMainMenu();
            InitializeTabContextMenu();
            SetupTray();
            SetupGCTimer();
            SetupRAMTimer();

            this.Show();
            this.BringToFront();
            this.Activate();

            InitializeBrowserEnv();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Program.WM_SHOW_BLACK_BROWSER)
            {
                ShowMainWindow();
            }
            base.WndProc(ref m);
        }

        private void Log(string msg)
        {
            try { File.AppendAllText(logPath, "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg + "\n"); }
            catch { }
        }

        private void SetupGCTimer()
        {
            gcTimer = new Timer();
            gcTimer.Interval = 60000;
            gcTimer.Tick += (s, e) => MemoryTrimmer.TrimProcessMemory();
            gcTimer.Start();
        }

        private void SetupRAMTimer()
        {
            ramTimer = new Timer();
            ramTimer.Interval = 10000;
            ramTimer.Tick += (s, e) =>
            {
                if (ramBtn != null)
                {
                    long ramMB = MemoryTrimmer.GetWorkingSetMB();
                    ramBtn.Text = "⚡ " + ramMB + "MB";
                }
            };
            ramTimer.Start();
        }

        private void InitializeUI()
        {
            headerContainer = new Panel();
            headerContainer.Dock = DockStyle.Top;
            headerContainer.Height = 68;
            headerContainer.BackColor = Color.FromArgb(23, 23, 28);

            softBanner = new Panel();
            softBanner.Dock = DockStyle.Top;
            softBanner.Height = 24;
            softBanner.BackColor = Color.FromArgb(0, 96, 223);

            softBannerLabel = new Label();
            softBannerLabel.Dock = DockStyle.Fill;
            softBannerLabel.Text = "⚫ Black Firefox Theme Active — 100% Ad-Free YouTube & Zero Trackers";
            softBannerLabel.ForeColor = Color.White;
            softBannerLabel.Font = new Font("Segoe UI Variable Display", 8.5f, FontStyle.Bold);
            softBannerLabel.TextAlign = ContentAlignment.MiddleCenter;

            softBanner.Controls.Add(softBannerLabel);

            bannerTimer = new Timer();
            bannerTimer.Interval = 4000;
            bannerTimer.Tick += (s, e) =>
            {
                softBannerLabel.Text = "⚫ Black Firefox Theme Active — 100% Ad-Free YouTube & Zero Trackers";
                bannerTimer.Stop();
            };

            omniboxPanel = new Panel();
            omniboxPanel.Dock = DockStyle.Fill;
            omniboxPanel.BackColor = Color.FromArgb(28, 28, 34);
            omniboxPanel.Padding = new Padding(6, 6, 6, 6);

            backBtn = CreateBtn("←", 0);
            fwdBtn = CreateBtn("→", 32);
            reloadBtn = CreateBtn("↻", 64);
            homeBtn = CreateBtn("🏠", 96);

            backBtn.Click += (s, e) => { WebView2 wv = GetCurrentWebView(); if (wv != null && wv.CanGoBack) wv.GoBack(); };
            fwdBtn.Click += (s, e) => { WebView2 wv = GetCurrentWebView(); if (wv != null && wv.CanGoForward) wv.GoForward(); };
            reloadBtn.Click += (s, e) => ReloadCurrentTab();
            homeBtn.Click += (s, e) => NavigateCurrentTab("about:blank");

            actionsPanel = new FlowLayoutPanel();
            actionsPanel.Dock = DockStyle.Right;
            actionsPanel.Height = 32;
            actionsPanel.AutoSize = true;
            actionsPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actionsPanel.FlowDirection = FlowDirection.RightToLeft;
            actionsPanel.WrapContents = false;
            actionsPanel.Padding = new Padding(0, 2, 4, 0);

            menuBtn = CreateActionBtn("⋮", Color.FromArgb(38, 38, 46), Color.FromArgb(200, 205, 220), 32);
            menuBtn.Click += (s, e) => mainMenu.Show(menuBtn, new Point(menuBtn.Width - mainMenu.Width, menuBtn.Height));

            addTabBtn = CreateActionBtn("+ Tab", Color.FromArgb(0, 96, 223), Color.FromArgb(255, 255, 255), 56);
            addTabBtn.Click += (s, e) => AddNewTab("New Tab", "about:blank");

            extBtn = CreateActionBtn("🧩 Ext", Color.FromArgb(44, 44, 54), Color.FromArgb(200, 205, 220), 64);
            extBtn.Click += (s, e) => NavigateCurrentTab("black://extensions");

            settingsBtn = CreateActionBtn("⚙️", Color.FromArgb(44, 44, 54), Color.FromArgb(200, 205, 220), 36);
            settingsBtn.Click += (s, e) => OpenSettingsDialog(0);

            notesBtn = CreateActionBtn("📝 Notes", Color.FromArgb(44, 44, 54), Color.FromArgb(200, 205, 220), 68);
            notesBtn.Click += (s, e) => OpenSettingsDialog(2);

            eyeCareBtn = CreateActionBtn("👁 Eye", Color.FromArgb(58, 48, 38), Color.FromArgb(255, 200, 130), 64);
            eyeCareBtn.Click += (s, e) => CycleEyeCareMode();

            shieldBtn = CreateActionBtn("🛡 0", Color.FromArgb(0, 96, 223), Color.FromArgb(255, 255, 255), 62);
            shieldBtn.Click += (s, e) => ShowAdShieldStatus();

            ramBtn = CreateActionBtn("⚡ 38MB", Color.FromArgb(38, 66, 50), Color.FromArgb(130, 235, 160), 72);
            ramBtn.Click += (s, e) =>
            {
                MemoryTrimmer.TrimProcessMemory();
                long ramMB = MemoryTrimmer.GetWorkingSetMB();
                ramBtn.Text = "⚡ " + ramMB + "MB";
                ShowSoftCommunication("⚡ Memory Optimization Completed — Purged Working Set");
            };

            starBtn = CreateActionBtn("⭐", Color.FromArgb(58, 48, 38), Color.FromArgb(255, 200, 130), 32);
            starBtn.Click += (s, e) => ToggleCurrentTabBookmark();

            actionsPanel.Controls.Add(menuBtn);
            actionsPanel.Controls.Add(addTabBtn);
            actionsPanel.Controls.Add(extBtn);
            actionsPanel.Controls.Add(settingsBtn);
            actionsPanel.Controls.Add(notesBtn);
            actionsPanel.Controls.Add(eyeCareBtn);
            actionsPanel.Controls.Add(shieldBtn);
            actionsPanel.Controls.Add(ramBtn);
            actionsPanel.Controls.Add(starBtn);

            urlBar = new TextBox();
            urlBar.Location = new Point(136, 7);
            urlBar.Height = 28;
            urlBar.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            urlBar.BackColor = Color.FromArgb(44, 44, 54);
            urlBar.ForeColor = Color.FromArgb(240, 240, 245);
            urlBar.Font = new Font("Segoe UI Variable Display", 10f);
            urlBar.BorderStyle = BorderStyle.FixedSingle;

            urlBar.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    NavigateCurrentTab(urlBar.Text.Trim());
                }
            };
            urlBar.Click += (s, e) => urlBar.SelectAll();
            urlBar.GotFocus += (s, e) => urlBar.SelectAll();

            omniboxPanel.Controls.Add(actionsPanel);
            omniboxPanel.Controls.Add(backBtn);
            omniboxPanel.Controls.Add(fwdBtn);
            omniboxPanel.Controls.Add(reloadBtn);
            omniboxPanel.Controls.Add(homeBtn);
            omniboxPanel.Controls.Add(urlBar);

            headerContainer.Controls.Add(omniboxPanel);
            headerContainer.Controls.Add(softBanner);

            tabControl = new TabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.Padding = new Point(14, 4);
            tabControl.Font = new Font("Segoe UI Variable Display", 9.5f);
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.DrawItem += OnDrawTabItem;
            tabControl.MouseDown += OnTabMouseDown;
            tabControl.SelectedIndexChanged += OnTabChanged;

            this.Controls.Add(headerContainer);
            this.Controls.Add(tabControl);
            headerContainer.SendToBack();
            tabControl.BringToFront();

            this.KeyPreview = true;
            this.KeyDown += OnFormKeyDown;

            this.Resize += (s, e) =>
            {
                if (urlBar != null && actionsPanel != null)
                {
                    urlBar.Width = Math.Max(200, this.Width - actionsPanel.Width - 160);
                }

                if (this.WindowState == FormWindowState.Minimized)
                {
                    SuspendAllWebViews();
                    MemoryTrimmer.TrimProcessMemory();
                }
                else
                {
                    ResumeActiveWebView();
                }
                this.PerformLayout();
            };
        }

        public void ShowSoftCommunication(string msg)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action)(() => ShowSoftCommunication(msg)));
                return;
            }

            softBannerLabel.Text = msg;
            bannerTimer.Stop();
            bannerTimer.Start();
        }

        private void ToggleCurrentTabBookmark()
        {
            WebView2 wv = GetCurrentWebView();
            if (wv != null && wv.CoreWebView2 != null)
            {
                string url = wv.Source != null ? wv.Source.ToString() : "";
                if (string.IsNullOrWhiteSpace(url) || url == "about:blank" || url.StartsWith("black://") || url.EndsWith("speeddial.html"))
                {
                    NavigateCurrentTab("black://bookmarks");
                    return;
                }
                string title = wv.CoreWebView2.DocumentTitle;
                bool added = BookmarksManager.ToggleBookmark(title, url);

                if (added)
                {
                    starBtn.BackColor = Color.FromArgb(150, 110, 40);
                    ShowSoftCommunication("⭐ Bookmark Added Locally to Device!");
                }
                else
                {
                    starBtn.BackColor = Color.FromArgb(58, 48, 38);
                    ShowSoftCommunication("⭐ Bookmark Removed");
                }
            }
        }

        private void ShowAdShieldStatus()
        {
            ShowSoftCommunication("🛡️ AdShield Engine: " + totalBlockedAds + " Ads Blocked • Zero Trackers");
        }

        private void OpenSettingsDialog(int initialTab)
        {
            using (SettingsForm sf = new SettingsForm(
                isDarkMode ? 1 : 0,
                eyeCareMode,
                (themeIndex) => SetTheme(themeIndex == 1),
                (eyeCareIndex) => SetEyeCareMode(eyeCareIndex),
                initialTab))
            {
                sf.ShowDialog(this);
            }
        }

        private Button CreateBtn(string text, int left)
        {
            Button b = new Button();
            b.Text = text;
            b.Location = new Point(left + 4, 6);
            b.Width = 28;
            b.Height = 28;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = Color.FromArgb(38, 38, 46);
            b.ForeColor = Color.FromArgb(200, 205, 220);
            b.Font = new Font("Segoe UI Variable Display", 9.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
        }

        private Button CreateActionBtn(string text, Color bg, Color fg, int width)
        {
            Button b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = 26;
            b.Margin = new Padding(2, 4, 2, 4);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = bg;
            b.ForeColor = fg;
            b.Font = new Font("Segoe UI Variable Display", 8.5f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            return b;
        }

        private void OnDrawTabItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= tabControl.TabPages.Count) return;

            TabPage page = tabControl.TabPages[e.Index];
            Rectangle rect = tabControl.GetTabRect(e.Index);
            bool selected = (tabControl.SelectedIndex == e.Index);
            bool isPrivate = page.Tag != null && (bool)page.Tag == true;

            Color backColor = isPrivate
                ? (selected ? Color.FromArgb(32, 32, 42) : Color.FromArgb(22, 22, 28))
                : (selected ? Color.FromArgb(38, 38, 46) : Color.FromArgb(28, 28, 34));

            using (SolidBrush b = new SolidBrush(backColor))
            {
                e.Graphics.FillRectangle(b, rect);
            }

            if (selected)
            {
                Color barColor = isPrivate ? Color.FromArgb(160, 90, 240) : Color.FromArgb(0, 96, 223);
                using (Pen p = new Pen(barColor, 3))
                {
                    e.Graphics.DrawLine(p, rect.Left, rect.Bottom - 1, rect.Right, rect.Bottom - 1);
                }
            }

            Color textColor = isPrivate ? Color.FromArgb(200, 180, 255) : (selected ? Color.FromArgb(240, 240, 245) : Color.FromArgb(160, 165, 180));

            TextRenderer.DrawText(e.Graphics, page.Text, tabControl.Font,
                new Rectangle(rect.X + 8, rect.Y + 4, rect.Width - 26, rect.Height - 4),
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            Rectangle closeRect = new Rectangle(rect.Right - 20, rect.Y + (rect.Height - 14) / 2, 14, 14);
            using (Font f = new Font("Segoe UI", 8.5f, FontStyle.Bold))
            {
                TextRenderer.DrawText(e.Graphics, "✕", f, closeRect,
                    Color.FromArgb(120, 120, 120), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private void OnTabMouseDown(object sender, MouseEventArgs e)
        {
            for (int i = 0; i < tabControl.TabPages.Count; i++)
            {
                Rectangle rect = tabControl.GetTabRect(i);
                if (rect.Contains(e.Location))
                {
                    rightClickedTab = tabControl.TabPages[i];

                    Rectangle closeRect = new Rectangle(rect.Right - 20, rect.Y + (rect.Height - 14) / 2, 14, 14);
                    if (closeRect.Contains(e.Location))
                    {
                        CloseTabAtIndex(i);
                        return;
                    }

                    if (e.Button == MouseButtons.Middle)
                    {
                        CloseTabAtIndex(i);
                        return;
                    }

                    if (e.Button == MouseButtons.Right)
                    {
                        tabContextMenu.Show(tabControl, e.Location);
                        return;
                    }
                }
            }
        }

        private void InitializeTabContextMenu()
        {
            tabContextMenu = new ContextMenuStrip();
            tabContextMenu.Font = new Font("Segoe UI Variable Display", 9.5f);

            tabContextMenu.Items.Add("➕ Duplicate Tab", null, (s, e) =>
            {
                if (rightClickedTab != null)
                {
                    WebView2 wv = GetWebView(rightClickedTab);
                    string url = (wv != null && wv.Source != null) ? wv.Source.ToString() : "about:blank";
                    AddNewTab(rightClickedTab.Text, url);
                }
            });

            tabContextMenu.Items.Add("↩️ Re-open Closed Tab (Ctrl+Shift+T)", null, (s, e) => ReopenLastClosedTab());

            tabContextMenu.Items.Add("🔊 Mute / Unmute Tab", null, (s, e) =>
            {
                if (rightClickedTab != null)
                {
                    WebView2 wv = GetWebView(rightClickedTab);
                    if (wv != null && wv.CoreWebView2 != null)
                    {
                        wv.CoreWebView2.IsMuted = !wv.CoreWebView2.IsMuted;
                        ShowSoftCommunication(wv.CoreWebView2.IsMuted ? "🔇 Tab Muted" : "🔊 Tab Unmuted");
                    }
                }
            });

            tabContextMenu.Items.Add("↻ Reload Tab", null, (s, e) =>
            {
                if (rightClickedTab != null)
                {
                    WebView2 wv = GetWebView(rightClickedTab);
                    if (wv != null && wv.CoreWebView2 != null) wv.Reload();
                }
            });

            tabContextMenu.Items.Add(new ToolStripSeparator());

            tabContextMenu.Items.Add("✕ Close Tab", null, (s, e) =>
            {
                if (rightClickedTab != null)
                {
                    int idx = tabControl.TabPages.IndexOf(rightClickedTab);
                    if (idx >= 0) CloseTabAtIndex(idx);
                }
            });

            tabContextMenu.Items.Add("🚫 Close Other Tabs", null, (s, e) =>
            {
                if (rightClickedTab != null)
                {
                    int keepIdx = tabControl.TabPages.IndexOf(rightClickedTab);
                    for (int i = tabControl.TabPages.Count - 1; i >= 0; i--)
                    {
                        if (i != keepIdx) CloseTabAtIndex(i);
                    }
                }
            });

            tabContextMenu.Items.Add("➡️ Close Tabs to Right", null, (s, e) =>
            {
                if (rightClickedTab != null)
                {
                    int keepIdx = tabControl.TabPages.IndexOf(rightClickedTab);
                    for (int i = tabControl.TabPages.Count - 1; i > keepIdx; i--)
                    {
                        CloseTabAtIndex(i);
                    }
                }
            });
        }

        private void CloseTabAtIndex(int index)
        {
            if (index < 0 || index >= tabControl.TabPages.Count) return;

            TabPage page = tabControl.TabPages[index];
            WebView2 wv = GetWebView(page);
            if (wv != null && wv.Source != null)
            {
                string u = wv.Source.ToString();
                if (!string.IsNullOrEmpty(u) && u != "about:blank" && !u.EndsWith("speeddial.html"))
                {
                    closedTabStack.Push(Tuple.Create(page.Text, u));
                }
            }

            if (tabControl.TabPages.Count > 1)
            {
                if (wv != null)
                {
                    try { if (wv.CoreWebView2 != null) wv.CoreWebView2.Stop(); } catch { }
                    wv.Dispose();
                }
                tabControl.TabPages.Remove(page);
                page.Dispose();
            }
            else
            {
                if (wv != null && wv.CoreWebView2 != null)
                {
                    wv.CoreWebView2.Navigate(SpeedDialPage.GetSpeedDialFilePath(isDarkMode));
                    urlBar.Text = "";
                    tabControl.SelectedTab.Text = "New Tab";
                }
            }
        }

        private void ReopenLastClosedTab()
        {
            if (closedTabStack.Count > 0)
            {
                var last = closedTabStack.Pop();
                AddNewTab(last.Item1, last.Item2);
                ShowSoftCommunication("↩️ Restored Closed Tab: " + last.Item1);
            }
            else
            {
                ShowSoftCommunication("⚠️ No closed tabs to restore");
            }
        }

        private void InitializeMainMenu()
        {
            mainMenu = new ContextMenuStrip();
            mainMenu.Font = new Font("Segoe UI Variable Display", 9.5f);

            mainMenu.Items.Add("➕ New Tab (Ctrl+T)", null, (s, e) => AddNewTab("New Tab", "black://home"));
            mainMenu.Items.Add("🕵️ New Private Tab (Ctrl+Shift+P)", null, (s, e) => AddNewTab("Private Tab", "black://home", isPrivate: true));
            mainMenu.Items.Add("🏠 Go to Speed Dial Home", null, (s, e) => NavigateCurrentTab("black://home"));
            mainMenu.Items.Add("⭐ Local Bookmarks", null, (s, e) => NavigateCurrentTab("black://bookmarks"));
            mainMenu.Items.Add("📜 Local History (Ctrl+H)", null, (s, e) => NavigateCurrentTab("black://history"));
            mainMenu.Items.Add("📥 Local Downloads (Ctrl+J)", null, (s, e) => NavigateCurrentTab("black://downloads"));
            mainMenu.Items.Add("🧩 Extensions Manager", null, (s, e) => NavigateCurrentTab("black://extensions"));
            mainMenu.Items.Add("🛒 Chrome Web Store", null, (s, e) => AddNewTab("Chrome Store", "https://chromewebstore.google.com"));
            mainMenu.Items.Add("🧩 Edge Add-ons Store", null, (s, e) => AddNewTab("Edge Add-ons", "https://microsoftedge.microsoft.com/addons"));
            mainMenu.Items.Add(new ToolStripSeparator());
            mainMenu.Items.Add("↩️ Re-open Closed Tab (Ctrl+Shift+T)", null, (s, e) => ReopenLastClosedTab());
            mainMenu.Items.Add("⚡ Optimize Memory Now", null, (s, e) =>
            {
                MemoryTrimmer.TrimProcessMemory();
                long ramMB = MemoryTrimmer.GetWorkingSetMB();
                if (ramBtn != null) ramBtn.Text = "⚡ " + ramMB + "MB";
                ShowSoftCommunication("⚡ Memory Optimization Completed — Purged Working Set");
            });
            mainMenu.Items.Add("⚙️ Settings & Device Info (Ctrl+,)", null, (s, e) => OpenSettingsDialog(0));
            mainMenu.Items.Add("📝 Dark Notes (Ctrl+Shift+N)", null, (s, e) => OpenSettingsDialog(2));
            mainMenu.Items.Add("👁️ Cycle Eye Care Filter (Ctrl+Shift+E)", null, (s, e) => CycleEyeCareMode());
            mainMenu.Items.Add("🌓 Toggle Dark / Light Theme (Ctrl+Shift+D)", null, (s, e) => ToggleTheme());
            mainMenu.Items.Add(new ToolStripSeparator());
            mainMenu.Items.Add("✕ Close Active Tab (Ctrl+W)", null, (s, e) => CloseCurrentTab());
            mainMenu.Items.Add("🚪 Exit Browser", null, (s, e) => ExitApp());
        }

        private void SetTheme(bool dark)
        {
            isDarkMode = dark;

            Color bg = dark ? Color.FromArgb(18, 18, 22) : Color.FromArgb(243, 243, 243);
            Color headerBg = dark ? Color.FromArgb(23, 23, 28) : Color.FromArgb(235, 235, 235);
            Color omniBg = dark ? Color.FromArgb(28, 28, 34) : Color.FromArgb(255, 255, 255);
            Color inputBg = dark ? Color.FromArgb(44, 44, 54) : Color.FromArgb(241, 243, 244);
            Color inputFg = dark ? Color.FromArgb(240, 240, 245) : Color.FromArgb(32, 33, 36);
            Color btnBg = dark ? Color.FromArgb(38, 38, 46) : Color.FromArgb(255, 255, 255);
            Color btnFg = dark ? Color.FromArgb(200, 205, 220) : Color.FromArgb(95, 99, 104);
            Color btnBgAlt = dark ? Color.FromArgb(44, 44, 54) : Color.FromArgb(241, 243, 244);
            Color accentBg = dark ? Color.FromArgb(0, 96, 223) : Color.FromArgb(232, 240, 254);
            Color accentFg = dark ? Color.FromArgb(255, 255, 255) : Color.FromArgb(0, 103, 192);

            this.BackColor = bg;
            headerContainer.BackColor = headerBg;
            omniboxPanel.BackColor = omniBg;
            urlBar.BackColor = inputBg;
            urlBar.ForeColor = inputFg;

            foreach (Button b in new Button[] { backBtn, fwdBtn, reloadBtn, homeBtn })
            {
                b.BackColor = btnBg;
                b.ForeColor = btnFg;
            }

            menuBtn.BackColor = btnBgAlt;
            menuBtn.ForeColor = btnFg;
            extBtn.BackColor = btnBgAlt;
            extBtn.ForeColor = btnFg;
            settingsBtn.BackColor = btnBgAlt;
            settingsBtn.ForeColor = btnFg;
            notesBtn.BackColor = btnBgAlt;
            notesBtn.ForeColor = btnFg;
            addTabBtn.BackColor = accentBg;
            addTabBtn.ForeColor = accentFg;
            shieldBtn.BackColor = accentBg;
            shieldBtn.ForeColor = accentFg;
            ramBtn.BackColor = dark ? Color.FromArgb(38, 66, 50) : Color.FromArgb(230, 245, 235);
            ramBtn.ForeColor = dark ? Color.FromArgb(130, 235, 160) : Color.FromArgb(15, 120, 50);
            eyeCareBtn.BackColor = dark ? Color.FromArgb(58, 48, 38) : Color.FromArgb(254, 247, 224);
            eyeCareBtn.ForeColor = dark ? Color.FromArgb(255, 200, 130) : Color.FromArgb(180, 100, 0);
            starBtn.BackColor = dark ? Color.FromArgb(58, 48, 38) : Color.FromArgb(254, 247, 224);
            starBtn.ForeColor = dark ? Color.FromArgb(255, 200, 130) : Color.FromArgb(180, 100, 0);

            foreach (TabPage p in tabControl.TabPages)
            {
                bool isPriv = p.Tag != null && (bool)p.Tag == true;
                p.BackColor = isPriv ? Color.FromArgb(18, 18, 24) : bg;

                WebView2 wv = GetWebView(p);
                if (wv != null && wv.CoreWebView2 != null && wv.Source != null)
                {
                    string uriStr = wv.Source.ToString();
                    if (uriStr.EndsWith("speeddial.html"))
                    {
                        wv.CoreWebView2.Navigate(SpeedDialPage.GetSpeedDialFilePath(dark));
                    }
                }
            }
            tabControl.Invalidate();
        }

        private void ToggleTheme()
        {
            SetTheme(!isDarkMode);
            ShowSoftCommunication(isDarkMode ? "🌙 Dark Theme Enabled" : "☀️ Light Theme Enabled");
        }

        private void SetEyeCareMode(int mode)
        {
            eyeCareMode = mode;
            eyeCareOverlay.SetMode(mode);

            if (eyeCareMode == 1)
            {
                eyeCareBtn.Text = "👁 Warm";
                eyeCareBtn.BackColor = Color.FromArgb(70, 56, 32);
                ShowSoftCommunication("👁️ Eye Care Filter: Warm Blue-Light Tint (25%)");
            }
            else if (eyeCareMode == 2)
            {
                eyeCareBtn.Text = "👁 Dimmed";
                eyeCareBtn.BackColor = Color.FromArgb(48, 48, 56);
                ShowSoftCommunication("👁️ Eye Care Filter: Night Dimmer (35%)");
            }
            else
            {
                eyeCareBtn.Text = "👁 Eye";
                eyeCareBtn.BackColor = Color.FromArgb(58, 48, 38);
                ShowSoftCommunication("👁️ Eye Care Filter: Disabled");
            }
        }

        private void CycleEyeCareMode()
        {
            SetEyeCareMode((eyeCareMode + 1) % 3);
        }

        private async void InitializeBrowserEnv()
        {
            try
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "black-webview2");

                CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions();
                options.AdditionalBrowserArguments = "--enable-features=msWebView2Extensions --allow-file-access-from-files";
                try { options.AreBrowserExtensionsEnabled = true; } catch { }

                webViewEnv = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                Log("Environment created successfully with standard WebView2 environment settings");

                // Launch Home or initial startup page in the initial tab
                AddNewTab(initialStartupUrl.Contains("music.youtube.com") ? "YT Music" : (initialStartupUrl.Contains("youtube.com") ? "YouTube" : "New Tab"), initialStartupUrl);
            }
            catch (Exception ex)
            {
                Log("FATAL Env: " + ex.ToString());
            }
        }

        public async void AddNewTab(string title, string url, bool isPrivate = false)
        {
            try
            {
                if (webViewEnv == null) return;

                string tabTitle = isPrivate ? "🕵️ Private Tab" : TruncateTitle(title);
                TabPage page = new TabPage(tabTitle);
                page.Tag = isPrivate;

                Color defaultBg = isPrivate
                    ? Color.FromArgb(18, 18, 24)
                    : (isDarkMode ? Color.FromArgb(18, 18, 22) : Color.FromArgb(243, 243, 243));

                page.BackColor = defaultBg;

                WebView2 wv = new WebView2();
                wv.Dock = DockStyle.Fill;
                wv.DefaultBackgroundColor = defaultBg;
                page.Controls.Add(wv);
                tabControl.TabPages.Add(page);
                tabControl.SelectedTab = page;

                tabControl.Invalidate();
                this.PerformLayout();

                await wv.EnsureCoreWebView2Async(webViewEnv);

                wv.CoreWebView2.ProcessFailed += (s, e) =>
                {
                    Log("ProcessFailed: " + e.ProcessFailedKind.ToString());
                    try { wv.Reload(); } catch { }
                };

                wv.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;
                    string targetUri = e.Uri;
                    this.BeginInvoke((Action)(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(targetUri) && targetUri != "about:blank")
                        {
                            AddNewTab("Loading...", targetUri, isPrivate);
                        }
                        else
                        {
                            AddNewTab("New Tab", "black://home", isPrivate);
                        }
                    }));
                };

                wv.CoreWebView2.PermissionRequested += (s, e) =>
                {
                    if (e.PermissionKind == CoreWebView2PermissionKind.Notifications)
                        e.State = CoreWebView2PermissionState.Allow;
                };

                try { wv.CoreWebView2.Settings.AreDevToolsEnabled = true; } catch { }

                wv.CoreWebView2.ContextMenuRequested += (s, e) =>
                {
                    try
                    {
                        string selText = e.ContextMenuTarget.SelectionText;
                        if (!string.IsNullOrWhiteSpace(selText))
                        {
                            var searchItem = wv.CoreWebView2.Environment.CreateContextMenuItem(
                                "🔍 Search Google for \"" + (selText.Length > 18 ? selText.Substring(0, 15) + "..." : selText) + "\"",
                                null, CoreWebView2ContextMenuItemKind.Command);

                            searchItem.CustomItemSelected += (cs, ce) =>
                            {
                                this.BeginInvoke((Action)(() =>
                                {
                                    AddNewTab("Google Search", "https://www.google.com/search?q=" + Uri.EscapeDataString(selText), isPrivate);
                                }));
                            };
                            e.MenuItems.Insert(0, searchItem);
                        }

                        var sourceItem = wv.CoreWebView2.Environment.CreateContextMenuItem(
                            "📜 View Page Source (Ctrl+U)", null, CoreWebView2ContextMenuItemKind.Command);

                        sourceItem.CustomItemSelected += (cs, ce) =>
                        {
                            string currUrl = wv.Source != null ? wv.Source.ToString() : "";
                            if (!string.IsNullOrEmpty(currUrl) && !currUrl.StartsWith("view-source:"))
                            {
                                this.BeginInvoke((Action)(() =>
                                {
                                    AddNewTab("Source", "view-source:" + currUrl, isPrivate);
                                }));
                            }
                        };
                        e.MenuItems.Add(sourceItem);

                        var inspectItem = wv.CoreWebView2.Environment.CreateContextMenuItem(
                            "🛠️ Inspect Element (F12)", null, CoreWebView2ContextMenuItemKind.Command);

                        inspectItem.CustomItemSelected += (cs, ce) =>
                        {
                            try { wv.CoreWebView2.OpenDevToolsWindow(); } catch { }
                        };
                        e.MenuItems.Add(inspectItem);
                    }
                    catch { }
                };

                wv.CoreWebView2.DownloadStarting += (s, e) =>
                {
                    try
                    {
                        e.Handled = false;
                        string path = e.ResultFilePath;
                        string name = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : "Download";
                        DownloadsManager.AddDownload(name, path ?? "", 0);
                        ShowSoftCommunication("📥 Download Started: " + name);

                        if (e.DownloadOperation != null)
                        {
                            e.DownloadOperation.StateChanged += (ds, de) =>
                            {
                                try
                                {
                                    if (e.DownloadOperation.State == CoreWebView2DownloadState.Completed)
                                    {
                                        ShowSoftCommunication("✅ Download Complete: " + name);
                                        if (name.EndsWith(".crx", StringComparison.OrdinalIgnoreCase) || (path != null && path.EndsWith(".crx", StringComparison.OrdinalIgnoreCase)))
                                        {
                                            string unpackedDir = ExtensionInstaller.UnpackCrx(path);
                                            if (unpackedDir != null)
                                            {
                                                try
                                                {
                                                    wv.CoreWebView2.Profile.AddBrowserExtensionAsync(unpackedDir);
                                                }
                                                catch { }
                                                ShowSoftCommunication("🧩 Extension Installed & Enabled: " + name);
                                            }
                                        }
                                    }
                                    else if (e.DownloadOperation.State == CoreWebView2DownloadState.Interrupted)
                                    {
                                        Log("Download Interrupted: " + name + " Reason: " + e.DownloadOperation.InterruptReason.ToString());
                                        ShowSoftCommunication("⚠️ Download Interrupted: " + name + " (" + e.DownloadOperation.InterruptReason.ToString() + ")");
                                    }
                                }
                                catch { }
                            };
                        }
                    }
                    catch { }
                };

                wv.CoreWebView2.ContainsFullScreenElementChanged += (s, e) =>
                {
                    this.Invoke((Action)(() =>
                    {
                        if (wv.CoreWebView2.ContainsFullScreenElement)
                        {
                            headerContainer.Visible = false;
                            this.FormBorderStyle = FormBorderStyle.None;
                            this.WindowState = FormWindowState.Maximized;
                        }
                        else
                        {
                            headerContainer.Visible = true;
                            this.FormBorderStyle = FormBorderStyle.Sizable;
                            this.WindowState = FormWindowState.Normal;
                        }
                        this.PerformLayout();
                    }));
                };

                AdShieldEngine.AttachAdShield(wv, () =>
                {
                    totalBlockedAds++;
                    try { this.Invoke((Action)(() => shieldBtn.Text = "🛡 " + totalBlockedAds)); } catch { }
                });

                wv.CoreWebView2.NavigationStarting += (s, e) =>
                {
                    if (e.Uri.Equals("black://history", StringComparison.OrdinalIgnoreCase) ||
                        e.Uri.Equals("about:history", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        wv.CoreWebView2.NavigateToString(HistoryManager.GetHistoryHtml(isDarkMode));
                        if (tabControl.SelectedTab == page) { urlBar.Text = "black://history"; page.Text = "Local History"; }
                        return;
                    }

                    if (e.Uri.Equals("black://bookmarks", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        wv.CoreWebView2.NavigateToString(BookmarksManager.GetBookmarksHtml(isDarkMode));
                        if (tabControl.SelectedTab == page) { urlBar.Text = "black://bookmarks"; page.Text = "Local Bookmarks"; }
                        return;
                    }

                    if (e.Uri.Equals("black://downloads", StringComparison.OrdinalIgnoreCase) ||
                        e.Uri.Equals("about:downloads", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        wv.CoreWebView2.NavigateToString(DownloadsManager.GetDownloadsHtml(isDarkMode));
                        if (tabControl.SelectedTab == page) { urlBar.Text = "black://downloads"; page.Text = "Local Downloads"; }
                        return;
                    }

                    if (e.Uri.StartsWith("black://extensions", StringComparison.OrdinalIgnoreCase) ||
                        e.Uri.StartsWith("about:extensions", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (e.Uri.Contains("?action="))
                        {
                            HandleExtensionAction(e.Uri);
                        }
                        wv.CoreWebView2.NavigateToString(ExtensionsManager.GetExtensionsHtml(isDarkMode));
                        if (tabControl.SelectedTab == page) { urlBar.Text = "black://extensions"; page.Text = "Extensions"; }
                        return;
                    }

                    if (tabControl.SelectedTab == page)
                    {
                        string uriStr = e.Uri;
                        urlBar.Text = (uriStr == "about:blank" || uriStr.EndsWith("speeddial.html")) ? "" : uriStr;
                    }
                };

                wv.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    if (tabControl.SelectedTab == page)
                    {
                        string uriStr = wv.Source != null ? wv.Source.ToString() : "";
                        urlBar.Text = (uriStr == "about:blank" || uriStr.EndsWith("speeddial.html")) ? "" : uriStr;

                        string pageName = string.IsNullOrEmpty(wv.CoreWebView2.DocumentTitle) || wv.CoreWebView2.DocumentTitle == "speeddial.html"
                            ? "New Tab" : TruncateTitle(wv.CoreWebView2.DocumentTitle);

                        page.Text = isPrivate ? "🕵️ " + pageName : pageName;
                        tabControl.Invalidate();
                        UpdateNavButtons();
                    }

                    if (!isPrivate && wv.Source != null && !wv.Source.ToString().EndsWith("speeddial.html"))
                    {
                        HistoryManager.AddVisit(wv.CoreWebView2.DocumentTitle, wv.Source.ToString());
                    }
                };

                wv.CoreWebView2.SourceChanged += (s, e) =>
                {
                    if (tabControl.SelectedTab == page)
                    {
                        string uriStr = wv.Source != null ? wv.Source.ToString() : "";
                        urlBar.Text = (uriStr == "about:blank" || uriStr.EndsWith("speeddial.html")) ? "" : uriStr;
                    }
                };

                if (url == "about:blank" || string.IsNullOrEmpty(url) || url == "black://home")
                {
                    wv.CoreWebView2.Navigate(SpeedDialPage.GetSpeedDialFilePath(isDarkMode));
                }
                else if (url == "black://history" || url == "about:history")
                {
                    wv.CoreWebView2.NavigateToString(HistoryManager.GetHistoryHtml(isDarkMode));
                }
                else if (url == "black://bookmarks")
                {
                    wv.CoreWebView2.NavigateToString(BookmarksManager.GetBookmarksHtml(isDarkMode));
                }
                else if (url == "black://downloads" || url == "about:downloads")
                {
                    wv.CoreWebView2.NavigateToString(DownloadsManager.GetDownloadsHtml(isDarkMode));
                }
                else if (url == "black://extensions" || url == "about:extensions")
                {
                    wv.CoreWebView2.NavigateToString(ExtensionsManager.GetExtensionsHtml(isDarkMode));
                }
                else
                {
                    wv.CoreWebView2.Navigate(FormatUrl(url));
                }
            }
            catch (Exception ex)
            {
                Log("AddNewTab ERROR: " + ex.ToString());
            }
        }

        private string TruncateTitle(string title)
        {
            if (string.IsNullOrEmpty(title)) return "Tab";
            if (title.Length > 18) return title.Substring(0, 15) + "...";
            return title;
        }

        private void CloseCurrentTab()
        {
            if (tabControl.SelectedIndex >= 0)
                CloseTabAtIndex(tabControl.SelectedIndex);
        }

        private WebView2 GetCurrentWebView()
        {
            if (tabControl.SelectedTab != null && tabControl.SelectedTab.Controls.Count > 0)
            {
                return tabControl.SelectedTab.Controls[0] as WebView2;
            }
            return null;
        }

        private WebView2 GetWebView(TabPage page)
        {
            if (page != null && page.Controls.Count > 0)
                return page.Controls[0] as WebView2;
            return null;
        }

        private void OnTabChanged(object sender, EventArgs e)
        {
            WebView2 wv = GetCurrentWebView();
            if (wv != null && wv.CoreWebView2 != null && wv.Source != null)
            {
                string uriStr = wv.Source.ToString();
                urlBar.Text = (uriStr == "about:blank" || uriStr.EndsWith("speeddial.html")) ? "" : uriStr;
                UpdateNavButtons();

                starBtn.BackColor = BookmarksManager.IsBookmarked(uriStr)
                    ? Color.FromArgb(254, 235, 180)
                    : Color.FromArgb(254, 247, 224);
            }
            else
            {
                UpdateNavButtons();
            }
        }

        private void UpdateNavButtons()
        {
            WebView2 wv = GetCurrentWebView();
            if (wv != null && wv.CoreWebView2 != null)
            {
                backBtn.Enabled = wv.CanGoBack;
                fwdBtn.Enabled = wv.CanGoForward;
            }
            else
            {
                backBtn.Enabled = false;
                fwdBtn.Enabled = false;
            }
        }

        private void NavigateCurrentTab(string input)
        {
            WebView2 wv = GetCurrentWebView();
            if (wv != null && wv.CoreWebView2 != null)
            {
                if (string.IsNullOrWhiteSpace(input) || input == "about:blank" || input.Equals("black://home", StringComparison.OrdinalIgnoreCase))
                {
                    wv.CoreWebView2.Navigate(SpeedDialPage.GetSpeedDialFilePath(isDarkMode));
                    urlBar.Text = "";
                    if (tabControl.SelectedTab != null) tabControl.SelectedTab.Text = "New Tab";
                    return;
                }

                if (input.Equals("black://history", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("about:history", StringComparison.OrdinalIgnoreCase))
                {
                    wv.CoreWebView2.NavigateToString(HistoryManager.GetHistoryHtml(isDarkMode));
                    urlBar.Text = "black://history";
                    if (tabControl.SelectedTab != null) tabControl.SelectedTab.Text = "Local History";
                    return;
                }

                if (input.Equals("black://bookmarks", StringComparison.OrdinalIgnoreCase))
                {
                    wv.CoreWebView2.NavigateToString(BookmarksManager.GetBookmarksHtml(isDarkMode));
                    urlBar.Text = "black://bookmarks";
                    if (tabControl.SelectedTab != null) tabControl.SelectedTab.Text = "Local Bookmarks";
                    return;
                }

                if (input.Equals("black://downloads", StringComparison.OrdinalIgnoreCase) ||
                    input.Equals("about:downloads", StringComparison.OrdinalIgnoreCase))
                {
                    wv.CoreWebView2.NavigateToString(DownloadsManager.GetDownloadsHtml(isDarkMode));
                    urlBar.Text = "black://downloads";
                    if (tabControl.SelectedTab != null) tabControl.SelectedTab.Text = "Local Downloads";
                    return;
                }

                if (input.StartsWith("black://extensions", StringComparison.OrdinalIgnoreCase) ||
                    input.StartsWith("about:extensions", StringComparison.OrdinalIgnoreCase))
                {
                    if (input.Contains("?action="))
                    {
                        HandleExtensionAction(input);
                    }
                    wv.CoreWebView2.NavigateToString(ExtensionsManager.GetExtensionsHtml(isDarkMode));
                    urlBar.Text = "black://extensions";
                    if (tabControl.SelectedTab != null) tabControl.SelectedTab.Text = "Extensions";
                    return;
                }

                string target = FormatUrl(input);
                wv.CoreWebView2.Navigate(target);
            }
        }

        private void HandleExtensionAction(string uri)
        {
            try
            {
                if (uri.Contains("action=load_unpacked"))
                {
                    using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                    {
                        fbd.Description = "Select Unpacked Extension Folder (containing manifest.json)";
                        if (fbd.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                        {
                            string folderName = Path.GetFileName(fbd.SelectedPath);
                            string extBaseDir = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                "black-webview2", "Extensions", folderName);

                            if (Directory.Exists(extBaseDir)) Directory.Delete(extBaseDir, true);
                            Directory.CreateDirectory(extBaseDir);

                            foreach (string dirPath in Directory.GetDirectories(fbd.SelectedPath, "*", SearchOption.AllDirectories))
                            {
                                Directory.CreateDirectory(dirPath.Replace(fbd.SelectedPath, extBaseDir));
                            }
                            foreach (string filePath in Directory.GetFiles(fbd.SelectedPath, "*.*", SearchOption.AllDirectories))
                            {
                                File.Copy(filePath, filePath.Replace(fbd.SelectedPath, extBaseDir), true);
                            }

                            WebView2 wv = GetCurrentWebView();
                            if (wv != null && wv.CoreWebView2 != null)
                            {
                                try { wv.CoreWebView2.Profile.AddBrowserExtensionAsync(extBaseDir); } catch { }
                            }

                            ShowSoftCommunication("🧩 Unpacked Extension Loaded: " + folderName);
                        }
                    }
                }
                else if (uri.Contains("action=remove") && uri.Contains("id="))
                {
                    int idIdx = uri.IndexOf("id=");
                    if (idIdx != -1)
                    {
                        string id = Uri.UnescapeDataString(uri.Substring(idIdx + 3));
                        int ampIdx = id.IndexOf("&");
                        if (ampIdx != -1) id = id.Substring(0, ampIdx);

                        string extDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "black-webview2", "Extensions", id);

                        if (Directory.Exists(extDir))
                        {
                            Directory.Delete(extDir, true);
                            ShowSoftCommunication("🗑️ Extension Removed: " + id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log("ExtensionAction Error: " + ex.ToString());
            }
        }

        private void ReloadCurrentTab()
        {
            WebView2 wv = GetCurrentWebView();
            if (wv != null && wv.CoreWebView2 != null)
                wv.Reload();
        }

        private string FormatUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || input == "about:blank")
                return "about:blank";

            if (input.StartsWith("http://") || input.StartsWith("https://") || input.StartsWith("file://") || input.StartsWith("black://"))
                return input;

            if (input.Contains(".") && !input.Contains(" "))
                return "https://" + input;

            return "https://www.google.com/search?q=" + Uri.EscapeDataString(input);
        }

        private void SuspendAllWebViews()
        {
            foreach (TabPage page in tabControl.TabPages)
            {
                WebView2 wv = GetWebView(page);
                if (wv != null && wv.CoreWebView2 != null)
                {
                    try { wv.CoreWebView2.TrySuspendAsync(); } catch { }
                }
            }
        }

        private void ResumeActiveWebView()
        {
            WebView2 wv = GetCurrentWebView();
            if (wv != null && wv.CoreWebView2 != null)
            {
                try { wv.CoreWebView2.Resume(); } catch { }
            }
        }

        private void SetupTray()
        {
            try
            {
                string iconPath = Path.Combine(Application.StartupPath, "icon.ico");
                if (File.Exists(iconPath))
                {
                    trayIcon = new NotifyIcon();
                    trayIcon.Icon = new Icon(iconPath);
                    trayIcon.Text = "Black Browser (Black Firefox Glassmorphic)";
                    trayIcon.Visible = true;

                    ContextMenuStrip menu = new ContextMenuStrip();
                    menu.Items.Add("Open Black Browser", null, (s, e) => ShowMainWindow());
                    menu.Items.Add("⚡ Optimize Memory", null, (s, e) => MemoryTrimmer.TrimProcessMemory());
                    menu.Items.Add(new ToolStripSeparator());
                    menu.Items.Add("Exit", null, (s, e) => ExitApp());

                    trayIcon.ContextMenuStrip = menu;
                    trayIcon.DoubleClick += (s, e) => ShowMainWindow();
                }
            }
            catch { }
        }

        private void ShowMainWindow()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.BringToFront();
            this.Activate();
            ResumeActiveWebView();
        }

        private void ExitApp()
        {
            if (trayIcon != null) trayIcon.Visible = false;
            Application.Exit();
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.T)
            {
                e.SuppressKeyPress = true;
                ReopenLastClosedTab();
            }
            else if (e.Control && e.KeyCode == Keys.T)
            {
                e.SuppressKeyPress = true;
                AddNewTab("New Tab", "about:blank");
            }
            else if (e.Control && e.KeyCode == Keys.W)
            {
                e.SuppressKeyPress = true;
                CloseCurrentTab();
            }
            else if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.Add))
            {
                e.SuppressKeyPress = true;
                WebView2 wv = GetCurrentWebView();
                if (wv != null)
                {
                    wv.ZoomFactor = Math.Min(5.0, wv.ZoomFactor + 0.1);
                    ShowSoftCommunication("🔍 Zoom: " + (int)(wv.ZoomFactor * 100) + "%");
                }
            }
            else if (e.Control && (e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.Subtract))
            {
                e.SuppressKeyPress = true;
                WebView2 wv = GetCurrentWebView();
                if (wv != null)
                {
                    wv.ZoomFactor = Math.Max(0.25, wv.ZoomFactor - 0.1);
                    ShowSoftCommunication("🔍 Zoom: " + (int)(wv.ZoomFactor * 100) + "%");
                }
            }
            else if (e.Control && (e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0))
            {
                e.SuppressKeyPress = true;
                WebView2 wv = GetCurrentWebView();
                if (wv != null)
                {
                    wv.ZoomFactor = 1.0;
                    ShowSoftCommunication("🔍 Zoom Reset: 100%");
                }
            }
            else if (e.Control && e.KeyCode == Keys.U)
            {
                e.SuppressKeyPress = true;
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.Source != null)
                {
                    string currUrl = wv.Source.ToString();
                    if (!string.IsNullOrEmpty(currUrl) && !currUrl.StartsWith("view-source:"))
                        AddNewTab("Source", "view-source:" + currUrl);
                }
            }
            else if (e.Control && e.KeyCode == Keys.P)
            {
                e.SuppressKeyPress = true;
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.CoreWebView2 != null)
                {
                    try { wv.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser); } catch { }
                }
            }
            else if (e.KeyCode == Keys.F12)
            {
                e.SuppressKeyPress = true;
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.CoreWebView2 != null)
                {
                    try { wv.CoreWebView2.OpenDevToolsWindow(); } catch { }
                }
            }
            else if (e.Control && e.KeyCode == Keys.Tab)
            {
                e.SuppressKeyPress = true;
                if (tabControl.TabPages.Count > 1)
                {
                    int nextIdx = e.Shift
                        ? (tabControl.SelectedIndex - 1 + tabControl.TabPages.Count) % tabControl.TabPages.Count
                        : (tabControl.SelectedIndex + 1) % tabControl.TabPages.Count;
                    tabControl.SelectedIndex = nextIdx;
                }
            }
            else if (e.Control && e.KeyCode >= Keys.D1 && e.KeyCode <= Keys.D8)
            {
                e.SuppressKeyPress = true;
                int targetIdx = e.KeyCode - Keys.D1;
                if (targetIdx < tabControl.TabPages.Count)
                    tabControl.SelectedIndex = targetIdx;
            }
            else if (e.Control && e.KeyCode == Keys.D9)
            {
                e.SuppressKeyPress = true;
                if (tabControl.TabPages.Count > 0)
                    tabControl.SelectedIndex = tabControl.TabPages.Count - 1;
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.P)
            {
                e.SuppressKeyPress = true;
                AddNewTab("Private Tab", "about:blank", isPrivate: true);
            }
            else if (e.Control && e.KeyCode == Keys.L)
            {
                e.SuppressKeyPress = true;
                urlBar.Focus();
                urlBar.SelectAll();
            }
            else if (e.Control && e.KeyCode == Keys.Oemcomma)
            {
                e.SuppressKeyPress = true;
                OpenSettingsDialog(0);
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.N)
            {
                e.SuppressKeyPress = true;
                OpenSettingsDialog(2);
            }
            else if (e.Control && e.KeyCode == Keys.R || e.KeyCode == Keys.F5)
            {
                e.SuppressKeyPress = true;
                ReloadCurrentTab();
            }
            else if (e.Control && e.KeyCode == Keys.H)
            {
                e.SuppressKeyPress = true;
                NavigateCurrentTab("black://history");
            }
            else if (e.Control && e.KeyCode == Keys.J)
            {
                e.SuppressKeyPress = true;
                NavigateCurrentTab("black://downloads");
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.E)
            {
                e.SuppressKeyPress = true;
                CycleEyeCareMode();
            }
            else if (e.Control && e.Shift && e.KeyCode == Keys.D)
            {
                e.SuppressKeyPress = true;
                ToggleTheme();
            }
            else if (e.Alt && e.KeyCode == Keys.Left)
            {
                e.SuppressKeyPress = true;
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.CanGoBack) wv.GoBack();
            }
            else if (e.Alt && e.KeyCode == Keys.Right)
            {
                e.SuppressKeyPress = true;
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.CanGoForward) wv.GoForward();
            }
            else if (e.KeyCode == Keys.F11)
            {
                e.SuppressKeyPress = true;
                ToggleFullscreen();
            }
        }

        private bool isFullscreen = false;
        private FormWindowState prevWindowState;
        private FormBorderStyle prevBorderStyle;

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                prevWindowState = this.WindowState;
                prevBorderStyle = this.FormBorderStyle;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
                headerContainer.Visible = false;
                isFullscreen = true;
            }
            else
            {
                this.FormBorderStyle = prevBorderStyle;
                this.WindowState = prevWindowState;
                headerContainer.Visible = true;
                isFullscreen = false;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                SuspendAllWebViews();
                MemoryTrimmer.TrimProcessMemory();
            }
            else
            {
                base.OnFormClosing(e);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (eyeCareOverlay != null) eyeCareOverlay.Dispose();
                if (mainMenu        != null) mainMenu.Dispose();
                if (tabContextMenu  != null) tabContextMenu.Dispose();
                if (trayIcon        != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
                if (gcTimer         != null) gcTimer.Dispose();
                if (ramTimer        != null) ramTimer.Dispose();
                if (bannerTimer     != null) bannerTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
