using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BlackBrowser
{
    public partial class BrowserForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;

        private static void EnableDarkModeForWindow(IntPtr handle)
        {
            try
            {
                int darkMode = 1;
                if (DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int)) != 0)
                {
                    DwmSetWindowAttribute(handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref darkMode, sizeof(int));
                }
            }
            catch { }
        }

        private Panel omniboxPanel;
        private FlowLayoutPanel actionsPanel;
        private Panel softBanner;
        private Label softBannerLabel;
        private DarkTabControl tabControl;

        private GlassButton backBtn;
        private GlassButton fwdBtn;
        private GlassButton reloadBtn;
        private GlassButton homeBtn;
        private Panel omniShell;
        private Panel navStrip;
        private PictureBox faviconBox;
        private TextBox urlBar;
        private GlassButton starBtn;
        private Button shieldBtn;
        private GlassButton downloadsBtn;
        private Button updateBtn;
        private GlassButton menuBtn;
        private GlassButton tabNewBtn;
        private GlassButton readerBtn;
        private GitHubReleaseInfo latestUpdateInfo;

        private Panel findPanel;
        private TextBox findBox;
        private Button findPrevBtn;
        private Button findNextBtn;
        private Button findCloseBtn;
        private Label findStatus;
        private WebView2 findWebView;

        private ContextMenuStrip mainMenu;
        private ContextMenuStrip tabContextMenu;
        private TabPage rightClickedTab;

        private NotifyIcon trayIcon;
        private Timer gcTimer;
        private Timer bannerTimer;

        private EyeCareOverlayForm eyeCareOverlay;
        private int eyeCareMode = 0;
        private bool isDarkMode = true;

        private CoreWebView2Environment webViewEnv;
        private int totalBlockedAds = 0;
        private string logPath;
        private Stack<Tuple<string, string>> closedTabStack = new Stack<Tuple<string, string>>();
        private string initialStartupUrl = "black://home";

        // Smart Address Bar autocomplete with Computer Science Trie
        private PrefixTrie omniTrie = new PrefixTrie();
        private LruTabManager lruTabManager = new LruTabManager(4);
        private Panel acDropdown;
        private int acSelectedIndex = -1;
        private List<Tuple<string, string, string>> acItems = new List<Tuple<string, string, string>>(); // type, title, url
        private bool acNavigating = false;
        private Timer acDebounceTimer;
        private ToolTip appToolTip;

        // Tab drag-and-drop
        private int dragTabIndex = -1;
        private Point dragStartPoint = Point.Empty;
        private int dragInsertionIndex = -1;
        private int dragMarkerX = -1;
        private int hoverTabIndex = -1;

        // Tab pinning
        private HashSet<TabPage> pinnedTabs = new HashSet<TabPage>();

        // Fullscreen notification
        private Panel fullscreenNotice;
        private Timer fullscreenNoticeTimer;
        private bool fullscreenAutoRevealEnabled = false;

        // PDF viewing: hosts that serve PDFs as application/octet-stream make
        // WebView2 download instead of display. We download those to a temp
        // folder and navigate the tab to the local file, so the built-in PDF
        // viewer renders them. pdfViewerOrigins maps temp path -> original URL.
        private static readonly string PdfTempDir = Path.Combine(Path.GetTempPath(), "BlackBrowserPdf");
        private readonly Dictionary<string, string> pdfViewerOrigins = new Dictionary<string, string>();
        private Image dismissIcon;

        // Downloads shelf
        private Panel downloadsShelf;
        private Timer shelfHideTimer;
        private FlowLayoutPanel shelfFlow;
        private Dictionary<CoreWebView2DownloadOperation, ShelfRow> shelfRows = new Dictionary<CoreWebView2DownloadOperation, ShelfRow>();

        private class ShelfRow
        {
            public Panel Panel;
            public Label Name;
            public Panel Progress;
            public Label Speed;
            public Button Cancel;
            public long LastBytes;
            public DateTime LastTick;
        }

        // Sidebar
        private Panel sidebarPanel;
        private Button sidebarBookmarksBtn;
        private Button sidebarHistoryBtn;
        private TreeView sidebarTree;
        private ListView sidebarList;
        private bool sidebarVisible = false;

        // Reader mode & per-tab crash / internal page state
        private class TabMetadata
        {
            public bool ReaderActive = false;
            public string ReaderOriginalUrl = "";
            public string ReaderHtml = "";
            public DateTime LastProcessCrash = DateTime.MinValue;
            public HashSet<string> BlockedDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public string ContainerName = "";
        }
        private Dictionary<TabPage, TabMetadata> tabMetadata = new Dictionary<TabPage, TabMetadata>();

        // Session-scoped permission decisions: key = "origin|PermissionKind"
        private readonly Dictionary<string, CoreWebView2PermissionState> permissionDecisions =
            new Dictionary<string, CoreWebView2PermissionState>(StringComparer.OrdinalIgnoreCase);

        private TabMetadata GetTabMetadata(TabPage page)
        {
            if (page == null) return null;
            TabMetadata meta;
            if (!tabMetadata.TryGetValue(page, out meta))
            {
                meta = new TabMetadata();
                tabMetadata[page] = meta;
            }
            return meta;
        }

        private void UpdateReaderButtonState()
        {
            if (readerBtn == null) return;
            TabPage page = tabControl.SelectedTab;
            TabMetadata meta = GetTabMetadata(page);
            bool readerActive = meta != null && meta.ReaderActive;

            if (readerActive)
            {
                readerBtn.BackColor = isDarkMode ? Color.FromArgb(70, 6, 182, 212) : Color.FromArgb(40, 6, 182, 212);
                readerBtn.ForeColor = Color.FromArgb(6, 182, 212);
            }
            else
            {
                readerBtn.ForeColor = isDarkMode ? Color.FromArgb(200, 205, 220) : Color.FromArgb(95, 99, 104);
                readerBtn.HoverTint = Color.Empty;
            }
        }

        // Password save & autofill prompt
        private Panel pwPrompt;
        private Label pwPromptLabel;
        private Button pwSaveBtn;
        private Button pwSkipBtn;
        private Tuple<WebView2, string, string, string> pwCandidate;
        private bool pwPromptIsAutofill = false;

        private static Color ParseAccentColor(string hex)
        {
            try
            {
                if (!string.IsNullOrEmpty(hex))
                {
                    hex = hex.Trim().TrimStart('#');
                    if (hex.Length == 6)
                    {
                        return Color.FromArgb(
                            Convert.ToInt32(hex.Substring(0, 2), 16),
                            Convert.ToInt32(hex.Substring(2, 2), 16),
                            Convert.ToInt32(hex.Substring(4, 2), 16));
                    }
                }
            }
            catch { }
            return Color.FromArgb(12, 99, 232);
        }

        private string ParseLaunchTarget(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string arg = raw.Trim();
            if (arg.Equals("--newwindow", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("__SHOW__", StringComparison.Ordinal))
                return null;
            if (arg.StartsWith("--app=", StringComparison.OrdinalIgnoreCase))
            {
                string appUrl = arg.Substring("--app=".Length).Trim().Trim('"');
                if (appUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    appUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    return appUrl;
                return null;
            }
            if (arg.IndexOf("music.youtube.com", StringComparison.OrdinalIgnoreCase) >= 0 || arg.Equals("--ytmusic", StringComparison.OrdinalIgnoreCase))
                return "https://music.youtube.com";
            if (arg.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0 || arg.Equals("--youtube", StringComparison.OrdinalIgnoreCase))
                return "https://www.youtube.com";
            if (arg.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("black://", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
                return arg;
            try
            {
                if (File.Exists(arg))
                    return new Uri(Path.GetFullPath(arg)).AbsoluteUri;
            }
            catch { }
            return null;
        }

        public void HandleExternalLaunchArg(string arg)
        {
            try
            {
                ShowMainWindow();
                string target = ParseLaunchTarget(arg);
                if (!string.IsNullOrEmpty(target))
                {
                    AddNewTab("Loading...", target);
                }
            }
            catch { }
        }

        private bool hasExplicitLaunchArg = false;
        private bool restoringSession = false;

        public BrowserForm(string[] args = null)
        {
            logPath = Program.GetLogFilePath();
            CleanupStalePdfTemp();
            Log("=== Black Browser starting (Black Browser Glassmorphic Edition v9.1.0) ===");

            string home = string.IsNullOrWhiteSpace(SettingsStore.HomePage) ? "black://home" : SettingsStore.HomePage;
            initialStartupUrl = home;

            if (args != null && args.Length > 0)
            {
                foreach (string raw in args)
                {
                    if (!string.IsNullOrEmpty(raw) && raw.Trim().StartsWith("--app=", StringComparison.OrdinalIgnoreCase))
                    {
                        isAppWindowMode = true;
                    }
                    string parsed = ParseLaunchTarget(raw);
                    if (!string.IsNullOrEmpty(parsed))
                    {
                        hasExplicitLaunchArg = true;
                        initialStartupUrl = parsed;
                        if (parsed == "https://music.youtube.com")
                            this.Text = "🎵 YouTube Music Desktop — Black Browser (Ad-Free)";
                        else if (parsed == "https://www.youtube.com")
                            this.Text = "▶ YouTube Desktop — Black Browser (Ad-Free)";
                        else if (isAppWindowMode)
                            this.Text = "Black Browser App";
                        break;
                    }
                }
            }

            ChromePainter.Enabled = SettingsStore.AnimationsEnabled;
            ChromePainter.Accent = ParseAccentColor(SettingsStore.Accent);

            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.DoubleBuffered = true;

            if (string.IsNullOrEmpty(this.Text) || this.Text == "Black Browser" || this.Text == "Black Firefox")
                this.Text = "Black Browser";

            this.Width = 1280;
            this.Height = 820;
            this.BackColor = Color.FromArgb(16, 16, 20);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            EnableDarkModeForWindow(this.Handle);

            // Restore window state from settings
            try
            {
                if (SettingsStore.WindowWidth >= 900 && SettingsStore.WindowHeight >= 600)
                {
                    this.Width = SettingsStore.WindowWidth;
                    this.Height = SettingsStore.WindowHeight;
                    if (SettingsStore.WindowLeft > 0 && SettingsStore.WindowTop > 0)
                    {
                        Rectangle desired = new Rectangle(SettingsStore.WindowLeft, SettingsStore.WindowTop, SettingsStore.WindowWidth, SettingsStore.WindowHeight);
                        if (Screen.FromRectangle(desired).WorkingArea.IntersectsWith(desired))
                        {
                            this.StartPosition = FormStartPosition.Manual;
                            this.Left = SettingsStore.WindowLeft;
                            this.Top = SettingsStore.WindowTop;
                        }
                    }
                }
                if (SettingsStore.WindowState == FormWindowState.Maximized)
                {
                    this.WindowState = FormWindowState.Maximized;
                }
            }
            catch { }

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
            SetupFileDragDrop();
            PopulateOmniTrie();

            lruTabManager.SetMaxActiveTabs(SettingsStore.MaxActiveTabs);
            lruTabManager.OnSuspensionChanged = (tp) =>
            {
                try
                {
                    if (!this.IsDisposed && !tabControl.IsDisposed)
                        tabControl.Invalidate();
                }
                catch { }
            };

            if (isAppWindowMode)
            {
                SetOmniboxVisible(false);
            }

            this.Move += (s, e) => { if (eyeCareOverlay != null) eyeCareOverlay.SyncToOwner(this); };
            this.VisibleChanged += (s, e) => { if (eyeCareOverlay != null) eyeCareOverlay.SyncToOwner(this); };

            this.Show();
            this.BringToFront();
            this.Activate();

            InitializeBrowserEnv();
        }

        private void SetupFileDragDrop()
        {
            try
            {
                this.AllowDrop = true;
                this.DragEnter += (s, e) =>
                {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy;
                };
                this.DragDrop += (s, e) =>
                {
                    if (e.Data.GetDataPresent(DataFormats.FileDrop))
                    {
                        string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                        if (files != null && files.Length > 0)
                        {
                            foreach (string f in files)
                            {
                                if (File.Exists(f))
                                {
                                    string u = "file:///" + f.Replace("\\", "/");
                                    AddNewTab(Path.GetFileName(f), u);
                                }
                            }
                        }
                    }
                };
            }
            catch { }
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
            Program.AppendLog("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + msg);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private void SetupGCTimer()
        {
            gcTimer = new Timer();
            gcTimer.Interval = 120000;
            gcTimer.Tick += (s, e) =>
            {
                // Trim memory gently only when minimized or when multiple tabs are open
                if (this.WindowState == FormWindowState.Minimized || !this.Visible || tabControl.TabPages.Count > 4)
                {
                    MemoryTrimmer.TrimProcessMemory();
                }
            };
            gcTimer.Start();
        }

        private void SetupRAMTimer()
        {
            // Consolidated into SetupGCTimer to avoid redundant Gen-2 GC and page-fault churn
        }

        private void InitializeUI()
        {
            softBanner = new Panel();
            softBanner.Dock = DockStyle.Bottom;
            softBanner.Height = 26;
            softBanner.BackColor = Color.FromArgb(23, 23, 28);
            softBanner.Visible = false;

            softBannerLabel = new Label();
            softBannerLabel.Dock = DockStyle.Fill;
            softBannerLabel.ForeColor = Color.FromArgb(200, 205, 220);
            softBannerLabel.Font = new Font("Segoe UI Variable Display", 8.5f);
            softBannerLabel.TextAlign = ContentAlignment.MiddleCenter;

            softBanner.Controls.Add(softBannerLabel);

            bannerTimer = new Timer();
            bannerTimer.Interval = 4000;
            bannerTimer.Tick += (s, e) =>
            {
                softBanner.Visible = false;
                bannerTimer.Stop();
            };

            omniboxPanel = new Panel();
            omniboxPanel.Dock = DockStyle.None;
            omniboxPanel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            omniboxPanel.Height = 72;
            omniboxPanel.BackColor = Color.FromArgb(22, 22, 28);
            omniboxPanel.Padding = new Padding(6, 0, 6, 0);

            backBtn = new GlassButton { IconType = VectorIcon.Back, Width = 38, Height = 38, Location = new Point(6, 17), CornerRadius = 8 };
            fwdBtn = new GlassButton { IconType = VectorIcon.Forward, Width = 38, Height = 38, Location = new Point(48, 17), CornerRadius = 8 };
            reloadBtn = new GlassButton { IconType = VectorIcon.Reload, Width = 38, Height = 38, Location = new Point(90, 17), CornerRadius = 8 };
            homeBtn = new GlassButton { IconType = VectorIcon.Home, Width = 38, Height = 38, Location = new Point(132, 17), CornerRadius = 8 };

            backBtn.Click += (s, e) => { WebView2 wv = GetCurrentWebView(); if (wv != null && wv.CanGoBack) wv.GoBack(); };
            fwdBtn.Click += (s, e) => { WebView2 wv = GetCurrentWebView(); if (wv != null && wv.CanGoForward) wv.GoForward(); };
            reloadBtn.Click += (s, e) => ReloadCurrentTab();
            homeBtn.Click += (s, e) => NavigateCurrentTab(string.IsNullOrWhiteSpace(SettingsStore.HomePage) ? "black://home" : SettingsStore.HomePage);
            homeBtn.Visible = SettingsStore.ShowHomeButton;

            actionsPanel = new FlowLayoutPanel();
            actionsPanel.Dock = DockStyle.Right;
            actionsPanel.Height = 72;
            actionsPanel.AutoSize = true;
            actionsPanel.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            actionsPanel.FlowDirection = FlowDirection.RightToLeft;
            actionsPanel.WrapContents = false;
            actionsPanel.Padding = new Padding(0, 14, 8, 14);
            actionsPanel.BackColor = Color.Transparent;
            actionsPanel.Resize += (s, e) => PositionOmnibox();

            menuBtn = new GlassButton { IconType = VectorIcon.Menu, Width = 40, Height = 40, CornerRadius = 8, Margin = new Padding(3, 2, 3, 2) };
            menuBtn.Click += (s, e) =>
            {
                int mw = mainMenu.PreferredSize.Width;
                mainMenu.Show(menuBtn, new Point(menuBtn.Width - mw, menuBtn.Height + 2));
            };

            downloadsBtn = new GlassButton { IconType = VectorIcon.Downloads, Width = 40, Height = 40, CornerRadius = 8, Margin = new Padding(3, 2, 3, 2) };
            downloadsBtn.Click += (s, e) => NavigateCurrentTab("black://downloads");

            shieldBtn = CreateActionBtn("shield", Color.FromArgb(36, 38, 48), Color.FromArgb(220, 225, 240), 80, "0", 40);
            shieldBtn.Margin = new Padding(3, 2, 3, 2);
            shieldBtn.Click += (s, e) => ShowPerSiteShieldDialog();

            updateBtn = CreateActionBtn("arrow_clockwise", Color.FromArgb(220, 80, 20), Color.FromArgb(255, 255, 255), 96, "Update", 40);
            updateBtn.Visible = false;
            updateBtn.Margin = new Padding(3, 2, 3, 2);
            updateBtn.Click += (s, e) => PromptApplyUpdate();

            tabNewBtn = new GlassButton { IconType = VectorIcon.Plus, Width = 34, Height = 34, CornerRadius = 8, ForeColor = Color.FromArgb(190, 195, 215) };
            tabNewBtn.Click += (s, e) => AddNewTab("New Tab", "black://home");

            actionsPanel.Controls.Add(menuBtn);
            actionsPanel.Controls.Add(downloadsBtn);
            actionsPanel.Controls.Add(shieldBtn);
            actionsPanel.Controls.Add(updateBtn);

            navStrip = new Panel();
            navStrip.Dock = DockStyle.Left;
            navStrip.Width = SettingsStore.ShowHomeButton ? 196 : 150;
            navStrip.Height = 72;
            navStrip.BackColor = Color.Transparent;
            navStrip.Controls.Add(backBtn);
            navStrip.Controls.Add(fwdBtn);
            navStrip.Controls.Add(reloadBtn);
            navStrip.Controls.Add(homeBtn);
            backBtn.Location = new Point(6, 13);
            fwdBtn.Location = new Point(52, 13);
            reloadBtn.Location = new Point(98, 13);
            homeBtn.Location = new Point(144, 13);
            backBtn.Width = 44;
            fwdBtn.Width = 44;
            reloadBtn.Width = 44;
            homeBtn.Width = 44;
            backBtn.Height = 44;
            fwdBtn.Height = 44;
            reloadBtn.Height = 44;
            homeBtn.Height = 44;
            backBtn.Region = new Region(GetRoundedPath(new Rectangle(0, 0, 44, 44), 12));
            fwdBtn.Region = new Region(GetRoundedPath(new Rectangle(0, 0, 44, 44), 12));
            reloadBtn.Region = new Region(GetRoundedPath(new Rectangle(0, 0, 44, 44), 12));
            homeBtn.Region = new Region(GetRoundedPath(new Rectangle(0, 0, 44, 44), 12));

            omniShell = new Panel();
            omniShell.Location = new Point(202, 12);
            omniShell.Height = 48;
            omniShell.Anchor = AnchorStyles.Left | AnchorStyles.Top;
            omniShell.BackColor = Color.FromArgb(32, 34, 44);
            omniShell.Padding = new Padding(10, 6, 6, 6);
            omniShell.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (Pen p = new Pen(Color.FromArgb(56, 60, 78), 1.2f))
                using (GraphicsPath gp = GetRoundedPath(new Rectangle(0, 0, omniShell.Width - 1, omniShell.Height - 1), 10))
                {
                    e.Graphics.DrawPath(p, gp);
                }
            };

            faviconBox = new PictureBox();
            faviconBox.Dock = DockStyle.Left;
            faviconBox.Width = 24;
            faviconBox.SizeMode = PictureBoxSizeMode.Zoom;
            faviconBox.BackColor = Color.Transparent;
            faviconBox.Cursor = Cursors.Hand;
            faviconBox.Click += (s, e) => ShowSitePermissionsDialog();
            faviconBox.Visible = false;

            urlBar = new TextBox();
            urlBar.Dock = DockStyle.Fill;
            urlBar.BorderStyle = BorderStyle.None;
            urlBar.BackColor = Color.FromArgb(32, 34, 44);
            urlBar.ForeColor = Color.FromArgb(242, 244, 250);
            urlBar.Font = new Font("Segoe UI Variable Display", 11.5f);

            urlBar.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    string selectedUrl = (acSelectedIndex >= 0 && acSelectedIndex < acItems.Count) ? acItems[acSelectedIndex].Item3 : null;
                    HideAutocomplete();
                    if (!string.IsNullOrEmpty(selectedUrl))
                    {
                        acNavigating = true;
                        if (!selectedUrl.StartsWith("math:", StringComparison.Ordinal) &&
                            !selectedUrl.StartsWith("black://switch-tab", StringComparison.OrdinalIgnoreCase))
                        {
                            urlBar.Text = selectedUrl;
                        }
                        NavigateCurrentTab(selectedUrl);
                        acNavigating = false;
                    }
                    else if (e.Control)
                    {
                        string text = urlBar.Text.Trim();
                        if (!string.IsNullOrWhiteSpace(text))
                            NavigateCurrentTab("https://" + text.Replace("http://", "").Replace("https://", "") + ".com");
                        else
                            NavigateCurrentTab(urlBar.Text.Trim());
                    }
                    else
                    {
                        NavigateCurrentTab(urlBar.Text.Trim());
                    }
                }
                else if (e.KeyCode == Keys.Down)
                {
                    e.SuppressKeyPress = true;
                    if (acDropdown.Visible && acItems.Count > 0)
                    {
                        acSelectedIndex = Math.Min(acSelectedIndex + 1, acItems.Count - 1);
                        PaintAutocompleteItems();
                    }
                }
                else if (e.KeyCode == Keys.Up)
                {
                    e.SuppressKeyPress = true;
                    if (acDropdown.Visible && acItems.Count > 0)
                    {
                        acSelectedIndex = Math.Max(acSelectedIndex - 1, -1);
                        PaintAutocompleteItems();
                    }
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    e.SuppressKeyPress = true;
                    if (acDropdown.Visible) HideAutocomplete();
                    else
                    {
                        WebView2 escWv = GetCurrentWebView();
                        if (escWv != null && escWv.CoreWebView2 != null)
                        {
                            try { escWv.CoreWebView2.Stop(); } catch { }
                        }
                    }
                }
            };
            urlBar.Click += (s, e) => { urlBar.SelectAll(); ShowAutocomplete(urlBar.Text); };
            urlBar.GotFocus += (s, e) => { urlBar.SelectAll(); ShowAutocomplete(urlBar.Text); };
            urlBar.LostFocus += (s, e) =>
            {
                // Delay hiding so click on dropdown items works
                Timer hideTm = new Timer();
                hideTm.Interval = 200;
                hideTm.Tick += (hs, he) => { hideTm.Stop(); hideTm.Dispose(); if (!urlBar.Focused) HideAutocomplete(); };
                hideTm.Start();
            };
            urlBar.TextChanged += (s, e) =>
            {
                if (acNavigating) return;
                if (acDebounceTimer != null) { acDebounceTimer.Stop(); acDebounceTimer.Dispose(); }
                acDebounceTimer = new Timer();
                acDebounceTimer.Interval = 150;
                acDebounceTimer.Tick += (ds, de) => { acDebounceTimer.Stop(); ShowAutocomplete(urlBar.Text); };
                acDebounceTimer.Start();
            };

            starBtn = new GlassButton { IconType = VectorIcon.Star, Width = 36, Height = 36, Dock = DockStyle.Right, CornerRadius = 6, Margin = new Padding(2) };
            starBtn.Click += (s, e) => ToggleCurrentTabBookmark();

            readerBtn = new GlassButton { IconType = VectorIcon.Reader, Width = 36, Height = 36, Dock = DockStyle.Right, CornerRadius = 6, Margin = new Padding(2) };
            readerBtn.Click += (s, e) => ToggleReaderMode();

            omniShell.Controls.Add(urlBar);
            omniShell.Controls.Add(starBtn);
            omniShell.Controls.Add(readerBtn);
            omniShell.Controls.Add(faviconBox);

            omniboxPanel.Controls.Add(actionsPanel);
            omniboxPanel.Controls.Add(omniShell);
            omniboxPanel.Controls.Add(navStrip);

            // Autocomplete dropdown
            acDropdown = new Panel();
            acDropdown.Visible = false;
            acDropdown.BackColor = Color.FromArgb(30, 30, 38);
            acDropdown.Width = 600;
            acDropdown.Height = 0;
            acDropdown.AutoScroll = false;
            acDropdown.Padding = new Padding(0);
            acDropdown.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(60, 60, 72), 1))
                {
                    e.Graphics.DrawRectangle(pen, 0, 0, acDropdown.Width - 1, acDropdown.Height - 1);
                }
            };

            tabControl = new DarkTabControl();
            tabControl.Dock = DockStyle.Fill;
            tabControl.Padding = new Point(14, 6);
            tabControl.Font = new Font("Segoe UI Variable Display", 10f);
            tabControl.SizeMode = TabSizeMode.Fixed;
            tabControl.ItemSize = new Size(185, 42);
            tabControl.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl.DrawItem += OnDrawTabItem;
            tabControl.MouseDown += OnTabMouseDown;
            tabControl.MouseMove += OnTabMouseMove;
            tabControl.MouseUp += OnTabMouseUp;
            tabControl.MouseLeave += (s, e) =>
            {
                if (hoverTabIndex != -1)
                {
                    hoverTabIndex = -1;
                    tabControl.Invalidate();
                }
            };
            tabControl.SelectedIndexChanged += OnTabChanged;

            // Fullscreen notification overlay
            fullscreenNotice = new Panel();
            fullscreenNotice.Size = new Size(340, 44);
            fullscreenNotice.BackColor = Color.FromArgb(220, 28, 28, 34);
            fullscreenNotice.Visible = false;
            Label fsLabel = new Label();
            fsLabel.Text = "Press F11 to exit full screen";
            fsLabel.ForeColor = Color.FromArgb(220, 225, 240);
            fsLabel.Font = new Font("Segoe UI Variable Display", 11f, FontStyle.Bold);
            fsLabel.Dock = DockStyle.Fill;
            fsLabel.TextAlign = ContentAlignment.MiddleCenter;
            fullscreenNotice.Controls.Add(fsLabel);
            fullscreenNoticeTimer = new Timer();
            fullscreenNoticeTimer.Interval = 3000;
            fullscreenNoticeTimer.Tick += (s, e) => { fullscreenNotice.Visible = false; fullscreenNoticeTimer.Stop(); };

            this.Controls.Add(tabControl);
            this.Controls.Add(tabNewBtn);
            this.Controls.Add(omniboxPanel);
            this.Controls.Add(softBanner);
            this.Controls.Add(acDropdown);
            this.Controls.Add(fullscreenNotice);
            tabControl.SendToBack();
            omniboxPanel.BringToFront();
            tabNewBtn.BringToFront();
            softBanner.BringToFront();
            acDropdown.BringToFront();
            fullscreenNotice.BringToFront();

            BuildFindBar();
            BuildDownloadsShelf();
            BuildSidebar();
            BuildPasswordPrompt();

            // Configure comprehensive tooltips for all controls
            appToolTip = new ToolTip();
            appToolTip.AutoPopDelay = 5000;
            appToolTip.InitialDelay = 350;
            appToolTip.ReshowDelay = 150;
            appToolTip.ShowAlways = true;

            appToolTip.SetToolTip(backBtn, "Back (Alt + Left Arrow)");
            appToolTip.SetToolTip(fwdBtn, "Forward (Alt + Right Arrow)");
            appToolTip.SetToolTip(reloadBtn, "Reload page (F5 / Ctrl + R)");
            appToolTip.SetToolTip(homeBtn, "Speed Dial Home (Alt + Home)");
            appToolTip.SetToolTip(tabNewBtn, "Open new tab (Ctrl + T)");
            appToolTip.SetToolTip(starBtn, "Bookmark this page (Ctrl + D)");
            appToolTip.SetToolTip(readerBtn, "Toggle Reader Mode — Distraction-free article view (Ctrl + Shift + R)");
            appToolTip.SetToolTip(shieldBtn, "AdShield Protection — Click to view blocked ads & trackers");
            appToolTip.SetToolTip(downloadsBtn, "Downloads Manager (Ctrl + J)");
            appToolTip.SetToolTip(updateBtn, "Update Available — Click to install");
            appToolTip.SetToolTip(menuBtn, "Main Menu & Settings (Alt + F)");
            appToolTip.SetToolTip(urlBar, "Search or enter web address (Ctrl + L / Alt + D)");

            this.KeyPreview = true;
            this.KeyDown += OnFormKeyDown;

            this.Resize += (s, e) =>
            {
                PositionOmnibox();
                if (acDropdown != null && acDropdown.Visible) PositionAutocomplete();
                if (eyeCareOverlay != null) eyeCareOverlay.SyncToOwner(this);

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

            // Fullscreen auto-reveal: show toolbar when mouse is at top edge
            this.MouseMove += (s, e) =>
            {
                if (isFullscreen && e.Y <= 5)
                {
                    SetOmniboxVisible(true);
                    fullscreenAutoRevealEnabled = true;
                }
                else if (fullscreenAutoRevealEnabled && isFullscreen && e.Y > omniboxPanel.Height + 60)
                {
                    SetOmniboxVisible(false);
                    fullscreenAutoRevealEnabled = false;
                }
            };
            UpdateUICornerRadius();
        }

        private void BuildDownloadsShelf()
        {
            downloadsShelf = new Panel();
            downloadsShelf.Dock = DockStyle.Bottom;
            downloadsShelf.Height = 48;
            downloadsShelf.BackColor = Color.FromArgb(26, 26, 34);
            downloadsShelf.Visible = false;
            downloadsShelf.Padding = new Padding(6);

            shelfFlow = new FlowLayoutPanel();
            shelfFlow.Dock = DockStyle.Fill;
            shelfFlow.FlowDirection = FlowDirection.LeftToRight;
            shelfFlow.WrapContents = true;
            shelfFlow.AutoScroll = true;
            shelfFlow.BackColor = Color.Transparent;
            downloadsShelf.Controls.Add(shelfFlow);

            shelfHideTimer = new Timer();
            shelfHideTimer.Interval = 6000;
            shelfHideTimer.Tick += (s, e) => { shelfHideTimer.Stop(); if (downloadsShelf != null) downloadsShelf.Visible = false; };

            this.Controls.Add(downloadsShelf);
            downloadsShelf.SendToBack();
            if (softBanner != null) softBanner.BringToFront();
        }

        private void ShowShelf()
        {
            if (downloadsShelf == null) return;
            downloadsShelf.Visible = true;
            downloadsShelf.BringToFront();
            if (shelfHideTimer != null) { shelfHideTimer.Stop(); shelfHideTimer.Start(); }
        }

        private ShelfRow CreateShelfRow(string name, CoreWebView2DownloadOperation op)
        {
            ShelfRow row = new ShelfRow();
            row.Panel = new Panel();
            row.Panel.Width = 300;
            row.Panel.Height = 38;
            row.Panel.Margin = new Padding(0, 2, 8, 2);
            row.Panel.BackColor = Color.FromArgb(36, 36, 46);

            row.Name = new Label();
            row.Name.Text = name;
            row.Name.Font = new Font("Segoe UI Variable Display", 9f);
            row.Name.ForeColor = Color.FromArgb(230, 233, 240);
            row.Name.Location = new Point(10, 4);
            row.Name.AutoSize = true;
            row.Panel.Controls.Add(row.Name);

            row.Progress = new Panel();
            row.Progress.BackColor = Color.FromArgb(60, 60, 74);
            row.Progress.Location = new Point(10, 22);
            row.Progress.Height = 4;
            row.Progress.Width = 200;
            row.Panel.Controls.Add(row.Progress);

            row.Speed = new Label();
            row.Speed.Text = "";
            row.Speed.Font = new Font("Segoe UI Variable Display", 8.5f);
            row.Speed.ForeColor = Color.FromArgb(140, 145, 160);
            row.Speed.Location = new Point(10, 27);
            row.Speed.AutoSize = true;
            row.Panel.Controls.Add(row.Speed);

            row.Cancel = new Button();
            row.Cancel.Text = "✕";
            row.Cancel.FlatStyle = FlatStyle.Flat;
            row.Cancel.FlatAppearance.BorderSize = 0;
            row.Cancel.BackColor = Color.Transparent;
            row.Cancel.ForeColor = Color.FromArgb(150, 155, 170);
            row.Cancel.Size = new Size(22, 22);
            row.Cancel.Location = new Point(272, 8);
            row.Cancel.Click += (s, e) => { try { if (op != null) op.Cancel(); } catch { } };
            row.Panel.Controls.Add(row.Cancel);

            row.LastBytes = 0;
            row.LastTick = DateTime.Now;
            if (shelfFlow != null) shelfFlow.Controls.Add(row.Panel);
            return row;
        }

        private void UpdateShelfRow(ShelfRow row, CoreWebView2DownloadOperation op)
        {
            try
            {
                if (row == null || op == null || row.Panel == null || row.Panel.IsDisposed) return;
                long total = (long)op.TotalBytesToReceive;
                long recv = (long)op.BytesReceived;
                if (total > 0)
                {
                    int pct = (int)(recv * 100 / total);
                    row.Progress.Width = Math.Max(4, (int)(200.0 * recv / total));
                    row.Speed.Text = pct + "%";
                }
                else
                {
                    row.Speed.Text = (recv / (1024 * 1024)).ToString() + " MB";
                }
                DateTime now = DateTime.Now;
                double secs = (now - row.LastTick).TotalSeconds;
                if (secs > 0.3)
                {
                    long delta = recv - row.LastBytes;
                    if (delta > 0)
                    {
                        double spd = delta / secs;
                        string spdStr = spd > 1024 * 1024 ? (spd / (1024 * 1024)).ToString("0.0") + " MB/s"
                                     : spd > 1024 ? (spd / 1024).ToString("0") + " KB/s"
                                     : spd.ToString("0") + " B/s";
                        row.Speed.Text = row.Speed.Text + " · " + spdStr;
                    }
                    row.LastBytes = recv;
                    row.LastTick = now;
                }
                if (shelfHideTimer != null) { shelfHideTimer.Stop(); shelfHideTimer.Start(); }
            }
            catch { }
        }

        private void RemoveShelfRow(CoreWebView2DownloadOperation op, bool completed)
        {
            try
            {
                ShelfRow row = null;
                if (shelfRows != null && shelfRows.TryGetValue(op, out row))
                {
                    if (completed && row.Speed != null) row.Speed.Text = "✓ Done";
                    if (shelfFlow != null && row.Panel != null) shelfFlow.Controls.Remove(row.Panel);
                    shelfRows.Remove(op);
                    if (row.Panel != null) { try { row.Panel.Dispose(); } catch { } }
                    if (shelfFlow != null && shelfFlow.Controls.Count == 0 && downloadsShelf != null)
                        downloadsShelf.Visible = false;
                }
            }
            catch { }
        }

        private void BuildSidebar()
        {
            sidebarPanel = new Panel();
            sidebarPanel.Width = 260;
            sidebarPanel.Dock = DockStyle.Left;
            sidebarPanel.BackColor = Color.FromArgb(24, 24, 32);
            sidebarPanel.Visible = false;
            int topOff = (tabControl != null ? tabControl.DisplayRectangle.Y : 46) + (omniboxPanel != null ? omniboxPanel.Height : 72) + 8;
            sidebarPanel.Padding = new Padding(8, topOff, 8, 8);

            Panel headerRow = new Panel();
            headerRow.Dock = DockStyle.Top;
            headerRow.Height = 30;
            headerRow.BackColor = Color.Transparent;

            Label title = new Label();
            title.Text = "🗂 Library";
            title.Font = new Font("Segoe UI Variable Display", 10f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(230, 233, 240);
            title.Dock = DockStyle.Fill;
            title.TextAlign = ContentAlignment.MiddleLeft;
            headerRow.Controls.Add(title);

            Button closeBtn = new Button();
            closeBtn.Text = "✕";
            closeBtn.FlatStyle = FlatStyle.Flat;
            closeBtn.FlatAppearance.BorderSize = 0;
            closeBtn.BackColor = Color.Transparent;
            closeBtn.ForeColor = Color.FromArgb(150, 155, 170);
            closeBtn.Size = new Size(26, 26);
            closeBtn.Dock = DockStyle.Right;
            closeBtn.Cursor = Cursors.Hand;
            closeBtn.Click += (s, e) => ToggleSidebar();
            headerRow.Controls.Add(closeBtn);

            sidebarBookmarksBtn = new Button();
            sidebarBookmarksBtn.Text = "⭐ Bookmarks";
            sidebarBookmarksBtn.FlatStyle = FlatStyle.Flat;
            sidebarBookmarksBtn.FlatAppearance.BorderSize = 0;
            sidebarBookmarksBtn.BackColor = Color.FromArgb(44, 44, 58);
            sidebarBookmarksBtn.ForeColor = Color.FromArgb(230, 233, 240);
            sidebarBookmarksBtn.Dock = DockStyle.Top;
            sidebarBookmarksBtn.Height = 30;
            sidebarBookmarksBtn.Click += (s, e) => ShowSidebarTab(true);

            sidebarHistoryBtn = new Button();
            sidebarHistoryBtn.Text = "🕐 History";
            sidebarHistoryBtn.FlatStyle = FlatStyle.Flat;
            sidebarHistoryBtn.FlatAppearance.BorderSize = 0;
            sidebarHistoryBtn.BackColor = Color.FromArgb(32, 32, 42);
            sidebarHistoryBtn.ForeColor = Color.FromArgb(230, 233, 240);
            sidebarHistoryBtn.Dock = DockStyle.Top;
            sidebarHistoryBtn.Height = 30;
            sidebarHistoryBtn.Click += (s, e) => ShowSidebarTab(false);

            sidebarList = new ListView();
            sidebarList.Dock = DockStyle.Fill;
            sidebarList.View = View.Details;
            sidebarList.FullRowSelect = true;
            sidebarList.BackColor = Color.FromArgb(28, 28, 38);
            sidebarList.ForeColor = Color.FromArgb(220, 225, 238);
            sidebarList.BorderStyle = BorderStyle.None;
            sidebarList.Columns.Add("Site", 150);
            sidebarList.Columns.Add("Time", 100);
            sidebarList.DoubleClick += (s, e) =>
            {
                if (sidebarList.SelectedItems.Count > 0)
                {
                    string u = sidebarList.SelectedItems[0].Tag as string;
                    if (!string.IsNullOrEmpty(u)) NavigateCurrentTab(u);
                }
            };
            sidebarList.Visible = false;
            sidebarPanel.Controls.Add(sidebarList);

            sidebarTree = new TreeView();
            sidebarTree.Dock = DockStyle.Fill;
            sidebarTree.BackColor = Color.FromArgb(28, 28, 38);
            sidebarTree.ForeColor = Color.FromArgb(220, 225, 238);
            sidebarTree.BorderStyle = BorderStyle.None;
            sidebarTree.NodeMouseDoubleClick += (s, e) =>
            {
                if (e.Node != null && e.Node.Tag != null)
                {
                    string u = e.Node.Tag as string;
                    if (!string.IsNullOrEmpty(u)) NavigateCurrentTab(u);
                }
            };
            sidebarPanel.Controls.Add(sidebarTree);
            sidebarPanel.Controls.Add(sidebarHistoryBtn);
            sidebarPanel.Controls.Add(sidebarBookmarksBtn);
            sidebarPanel.Controls.Add(headerRow);

            this.Controls.Add(sidebarPanel);
        }

        private void ToggleSidebar()
        {
            sidebarVisible = !sidebarVisible;
            sidebarPanel.Visible = sidebarVisible;
            if (sidebarVisible)
            {
                PopulateSidebarTree();
                PopulateSidebarList();
            }
            if (omniboxPanel != null) omniboxPanel.BringToFront();
        }

        private void PopulateSidebarTree()
        {
            if (sidebarTree == null) return;
            sidebarTree.Nodes.Clear();
            try
            {
                var bms = BookmarksManager.GetBookmarks();
                if (bms != null)
                {
                    foreach (var b in bms)
                    {
                        TreeNode n = new TreeNode(b.Title);
                        n.Tag = b.Url;
                        sidebarTree.Nodes.Add(n);
                    }
                }
            }
            catch { }
        }

        private void PopulateSidebarList()
        {
            if (sidebarList == null) return;
            sidebarList.Items.Clear();
            try
            {
                var hs = HistoryManager.GetHistoryItems();
                if (hs != null)
                {
                    int limit = Math.Min(hs.Count, 200);
                    for (int i = 0; i < limit; i++)
                    {
                        var h = hs[i];
                        ListViewItem it = new ListViewItem(h.Title);
                        it.SubItems.Add(h.Timestamp);
                        it.Tag = h.Url;
                        sidebarList.Items.Add(it);
                    }
                }
            }
            catch { }
        }

        private void ShowSidebarTab(bool bookmarks)
        {
            if (sidebarTree == null || sidebarList == null) return;
            sidebarTree.Visible = bookmarks;
            sidebarList.Visible = !bookmarks;
            if (sidebarBookmarksBtn != null)
                sidebarBookmarksBtn.BackColor = bookmarks ? Color.FromArgb(44, 44, 58) : Color.FromArgb(32, 32, 42);
            if (sidebarHistoryBtn != null)
                sidebarHistoryBtn.BackColor = bookmarks ? Color.FromArgb(32, 32, 42) : Color.FromArgb(44, 44, 58);
            if (bookmarks) sidebarTree.BringToFront(); else sidebarList.BringToFront();
        }

        private void BuildPasswordPrompt()
        {
            pwPrompt = new Panel();
            pwPrompt.Dock = DockStyle.Bottom;
            pwPrompt.Height = 40;
            pwPrompt.BackColor = Color.FromArgb(34, 34, 46);
            pwPrompt.Visible = false;

            pwPromptLabel = new Label();
            pwPromptLabel.Text = "Save password for this site?";
            pwPromptLabel.Font = new Font("Segoe UI Variable Display", 9.5f);
            pwPromptLabel.ForeColor = Color.FromArgb(220, 225, 238);
            pwPromptLabel.Dock = DockStyle.Fill;
            pwPromptLabel.TextAlign = ContentAlignment.MiddleLeft;
            pwPromptLabel.Padding = new Padding(12, 0, 0, 0);
            pwPrompt.Controls.Add(pwPromptLabel);

            pwSkipBtn = new Button();
            pwSkipBtn.Text = "Not now";
            pwSkipBtn.FlatStyle = FlatStyle.Flat;
            pwSkipBtn.FlatAppearance.BorderSize = 0;
            pwSkipBtn.BackColor = Color.Transparent;
            pwSkipBtn.ForeColor = Color.FromArgb(160, 165, 180);
            pwSkipBtn.Size = new Size(80, 28);
            pwSkipBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pwSkipBtn.Click += (s, e) =>
            {
                pwPrompt.Visible = false;
                pwCandidate = null;
                pwPromptIsAutofill = false;
                pwAutofillMatches.Clear();
                if (pwAccountCombo != null) pwAccountCombo.Visible = false;
            };
            pwPrompt.Controls.Add(pwSkipBtn);

            pwAccountCombo = new ComboBox();
            pwAccountCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            pwAccountCombo.FlatStyle = FlatStyle.Flat;
            pwAccountCombo.BackColor = Color.FromArgb(46, 48, 64);
            pwAccountCombo.ForeColor = Color.White;
            pwAccountCombo.Font = new Font("Segoe UI", 9f);
            pwAccountCombo.Size = new Size(190, 24);
            pwAccountCombo.Visible = false;
            pwAccountCombo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pwAccountCombo.SelectedIndexChanged += (s, e) =>
            {
                if (pwPromptIsAutofill && pwCandidate != null && pwAccountCombo.SelectedIndex >= 0 && pwAccountCombo.SelectedIndex < pwAutofillMatches.Count)
                {
                    PasswordItem sel = pwAutofillMatches[pwAccountCombo.SelectedIndex];
                    pwCandidate = Tuple.Create(pwCandidate.Item1, pwCandidate.Item2, sel.Username ?? "", sel.Password ?? "");
                }
            };
            pwPrompt.Controls.Add(pwAccountCombo);

            pwSaveBtn = new Button();
            pwSaveBtn.Text = "Save";
            pwSaveBtn.FlatStyle = FlatStyle.Flat;
            pwSaveBtn.FlatAppearance.BorderSize = 0;
            pwSaveBtn.BackColor = Color.FromArgb(0, 96, 223);
            pwSaveBtn.ForeColor = Color.White;
            pwSaveBtn.Size = new Size(70, 28);
            pwSaveBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            pwSaveBtn.Click += (s, e) =>
            {
                try
                {
                    if (pwCandidate != null && !string.IsNullOrEmpty(pwCandidate.Item4))
                    {
                        if (pwPromptIsAutofill)
                        {
                            WebView2 targetWv = pwCandidate.Item1;
                            if (targetWv != null && !targetWv.IsDisposed && targetWv.CoreWebView2 != null)
                            {
                                var ser = new System.Web.Script.Serialization.JavaScriptSerializer();
                                string uJson = ser.Serialize(pwCandidate.Item3 ?? "");
                                string pJson = ser.Serialize(pwCandidate.Item4 ?? "");
                                string fillJs = "(function(u,p){"
                                    + "var pw=document.querySelector('input[type=password]');"
                                    + "if(!pw)return;"
                                    + "var form=pw.form||document;"
                                    + "var uf=form.querySelector('input[type=email],input[name*=user],input[name*=email],input[name*=login],input[type=text]');"
                                    + "function setVal(el,val){if(!el||!val)return;"
                                    + "var d=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value');"
                                    + "if(d&&d.set)d.set.call(el,val);else el.value=val;"
                                    + "el.dispatchEvent(new Event('input',{bubbles:true}));"
                                    + "el.dispatchEvent(new Event('change',{bubbles:true}));}"
                                    + "setVal(uf,u);setVal(pw,p);"
                                    + "})(" + uJson + "," + pJson + ");";
                                targetWv.CoreWebView2.ExecuteScriptAsync(fillJs);
                            }
                        }
                        else
                        {
                            PasswordManager.SavePassword(pwCandidate.Item2, pwCandidate.Item3, pwCandidate.Item4);
                            ShowSoftCommunication("🔐 Password saved");
                        }
                    }
                }
                catch { }
                pwPrompt.Visible = false;
                pwCandidate = null;
                pwPromptIsAutofill = false;
                pwAutofillMatches.Clear();
                if (pwAccountCombo != null) pwAccountCombo.Visible = false;
            };
            pwPrompt.Controls.Add(pwSaveBtn);

            pwPrompt.Resize += (s, e) =>
            {
                if (pwSaveBtn != null) pwSaveBtn.Location = new Point(pwPrompt.ClientSize.Width - 82, 6);
                if (pwSkipBtn != null) pwSkipBtn.Location = new Point(pwPrompt.ClientSize.Width - 170, 6);
                if (pwAccountCombo != null) pwAccountCombo.Location = new Point(Math.Max(12, pwPrompt.ClientSize.Width - 372), 8);
            };

            this.Controls.Add(pwPrompt);
            pwPrompt.SendToBack();
        }

        private async void ToggleReaderMode()
        {
            WebView2 wv = GetCurrentWebView();
            if (wv == null || wv.CoreWebView2 == null) return;
            TabPage page = tabControl.SelectedTab;
            TabMetadata meta = GetTabMetadata(page);
            if (meta == null) return;

            try
            {
                if (meta.ReaderActive)
                {
                    meta.ReaderActive = false;
                    meta.ReaderHtml = "";
                    UpdateReaderButtonState();
                    if (!string.IsNullOrEmpty(meta.ReaderOriginalUrl)) wv.CoreWebView2.Navigate(meta.ReaderOriginalUrl);
                    return;
                }
                string js = "(function(){var c=document.querySelector('article')||document.querySelector('main')||document.body;"
                    + "var t=c?c.innerText:'';var h=document.title||location.hostname;"
                    + "return encodeURIComponent(h)+'|'+encodeURIComponent(t);})()";
                string res = await wv.CoreWebView2.ExecuteScriptAsync(js);
                string title = "";
                string text = "";
                try
                {
                    string dec = Uri.UnescapeDataString(res).Trim('"');
                    int bar = dec.IndexOf('|');
                    if (bar >= 0) { title = dec.Substring(0, bar); text = dec.Substring(bar + 1); }
                }
                catch { }
                if (string.IsNullOrWhiteSpace(text) || text.Length < 80)
                {
                    ShowSoftCommunication("📖 No readable article found on this page");
                    return;
                }
                meta.ReaderActive = true;
                meta.ReaderOriginalUrl = wv.CoreWebView2.Source;
                meta.ReaderHtml = BuildReaderHtml(title, text, meta.ReaderOriginalUrl);
                UpdateReaderButtonState();
                wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/reader");
            }
            catch (Exception ex) { Log("Reader: " + ex.Message); }
        }

        private string BuildReaderHtml(string title, string text, string originalUrl)
        {
            string[] words = (text ?? "").Split(new char[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int readMins = Math.Max(1, (int)Math.Ceiling(words.Length / 220.0));
            StringBuilder sb = new StringBuilder();
            sb.Append("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.Append("<style>"
                + "body{background:#141419;color:#dfe3ea;font-family:Georgia,'Segoe UI',serif;max-width:760px;margin:0 auto;padding:24px 24px 80px;line-height:1.75;transition:background .2s,color .2s}"
                + "body.sepia{background:#f4ecd8;color:#3c2f1f}body.light{background:#fafbfc;color:#1f2328}"
                + ".bar{display:flex;flex-wrap:wrap;gap:8px;align-items:center;padding:12px 0 22px;border-bottom:1px solid rgba(128,136,155,.22);margin-bottom:26px}"
                + "h1{font-size:30px;line-height:1.3;margin:0 0 8px}.meta{color:#8a93a5;font-size:13px;margin-bottom:28px}"
                + "p{margin:0 0 18px;font-size:18px}"
                + "button{background:rgba(128,136,160,.16);color:inherit;border:1px solid rgba(128,136,160,.3);border-radius:7px;padding:6px 12px;font-size:13px;cursor:pointer}"
                + "button:hover{background:rgba(128,136,160,.28)}.sp{flex:1}"
                + "</style></head><body>");
            sb.Append("<div class='bar'>"
                + "<button onclick='stopSpeech();window.chrome.webview.postMessage(\"readerback\")'>← Exit Reader</button>"
                + "<button onclick='setTheme(\"dark\")'>🌙 Dark</button>"
                + "<button onclick='setTheme(\"sepia\")'>📜 Sepia</button>"
                + "<button onclick='setTheme(\"light\")'>☀️ Light</button>"
                + "<button onclick='chFont(-2)'>A-</button>"
                + "<button onclick='chFont(2)'>A+</button>"
                + "<button id='ttsBtn' onclick='toggleSpeech()'>🔊 Read Aloud</button>"
                + "<span class='sp'></span>"
                + "<span style='font-size:12px;opacity:.75'>" + words.Length + " words • " + readMins + " min read</span>"
                + "</div>");
            sb.Append("<h1>" + SimpleHtmlEscape(title) + "</h1>");
            sb.Append("<div class='meta'>" + SimpleHtmlEscape(originalUrl) + "</div>");
            sb.Append("<article id='art'>");
            string[] paras = (text ?? "").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string para in paras)
                sb.Append("<p>" + SimpleHtmlEscape(para.Trim()) + "</p>");
            sb.Append("</article>");
            sb.Append("<script>"
                + "var curSz=parseInt(localStorage.getItem('black_reader_sz')||'18',10);"
                + "function applySz(){var ps=document.querySelectorAll('#art p');for(var i=0;i<ps.length;i++)ps[i].style.fontSize=curSz+'px';}"
                + "function chFont(d){curSz=Math.max(13,Math.min(28,curSz+d));localStorage.setItem('black_reader_sz',curSz);applySz();}"
                + "function setTheme(t){document.body.className=(t==='dark'?'':t);localStorage.setItem('black_reader_th',t);}"
                + "(function(){var th=localStorage.getItem('black_reader_th')||'dark';setTheme(th);applySz();})();"
                + "var speaking=false;"
                + "function stopSpeech(){if(window.speechSynthesis)window.speechSynthesis.cancel();speaking=false;var b=document.getElementById('ttsBtn');if(b)b.textContent='🔊 Read Aloud';}"
                + "function toggleSpeech(){if(!window.speechSynthesis)return;if(speaking){stopSpeech();return;}"
                + "var u=new SpeechSynthesisUtterance(document.getElementById('art').innerText);"
                + "u.onend=function(){stopSpeech();};u.onerror=function(){stopSpeech();};"
                + "speaking=true;document.getElementById('ttsBtn').textContent='⏹ Stop';window.speechSynthesis.speak(u);}"
                + "window.addEventListener('beforeunload',stopSpeech);"
                + "</script></body></html>");
            return sb.ToString();
        }

        private static string SimpleHtmlEscape(string s)
        {
            if (s == null) return "";
            return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
        }

        private void BuildFindBar()
        {
            findPanel = new Panel();
            findPanel.Height = 42;
            findPanel.Dock = DockStyle.Bottom;
            findPanel.BackColor = Color.FromArgb(32, 33, 36);
            findPanel.Visible = false;

            findStatus = new Label();
            findStatus.Text = "";
            findStatus.ForeColor = Color.FromArgb(154, 160, 166);
            findStatus.Font = new Font("Segoe UI Variable Display", 9f);
            findStatus.AutoSize = true;
            findStatus.Location = new Point(10, 13);

            findBox = new TextBox();
            findBox.Width = 220;
            findBox.Location = new Point(90, 8);
            findBox.BorderStyle = BorderStyle.FixedSingle;
            findBox.Font = new Font("Segoe UI Variable Display", 10f);
            findBox.BackColor = Color.FromArgb(18, 18, 22);
            findBox.ForeColor = Color.White;

            findPrevBtn = new Button();
            findPrevBtn.Text = "▲";
            findPrevBtn.Size = new Size(30, 27);
            findPrevBtn.Location = new Point(318, 7);
            findPrevBtn.FlatStyle = FlatStyle.Flat;
            findPrevBtn.FlatAppearance.BorderSize = 0;
            findPrevBtn.BackColor = Color.FromArgb(18, 18, 22);
            findPrevBtn.ForeColor = Color.White;

            findNextBtn = new Button();
            findNextBtn.Text = "▼";
            findNextBtn.Size = new Size(30, 27);
            findNextBtn.Location = new Point(352, 7);
            findNextBtn.FlatStyle = FlatStyle.Flat;
            findNextBtn.FlatAppearance.BorderSize = 0;
            findNextBtn.BackColor = Color.FromArgb(18, 18, 22);
            findNextBtn.ForeColor = Color.White;

            findCloseBtn = new Button();
            findCloseBtn.Text = "✕";
            findCloseBtn.Size = new Size(30, 27);
            findCloseBtn.Location = new Point(396, 7);
            findCloseBtn.FlatStyle = FlatStyle.Flat;
            findCloseBtn.FlatAppearance.BorderSize = 0;
            findCloseBtn.BackColor = Color.FromArgb(18, 18, 22);
            findCloseBtn.ForeColor = Color.White;

            findBox.TextChanged += (s, e) => RunFind(false);
            findBox.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && e.Shift) { RunFind(true); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) { RunFind(false); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { HideFindBar(); e.SuppressKeyPress = true; }
            };
            findPrevBtn.Click += (s, e) => RunFind(true);
            findNextBtn.Click += (s, e) => RunFind(false);
            findCloseBtn.Click += (s, e) => HideFindBar();

            findPanel.Controls.Add(findCloseBtn);
            findPanel.Controls.Add(findNextBtn);
            findPanel.Controls.Add(findPrevBtn);
            findPanel.Controls.Add(findBox);
            findPanel.Controls.Add(findStatus);

            this.Controls.Add(findPanel);
            findPanel.BringToFront();
        }

        private void ShowFindBar(WebView2 wv)
        {
            findWebView = wv;
            findPanel.Visible = true;
            findBox.Focus();
            if (!string.IsNullOrEmpty(findBox.Text)) RunFind(false);
        }

        private void HideFindBar()
        {
            findPanel.Visible = false;
            findWebView = null;
            findBox.Clear();
            findStatus.Text = "";
        }

        private void RunFind(bool backwards)
        {
            if (findWebView == null || findWebView.CoreWebView2 == null) { findStatus.Text = ""; return; }
            string q = findBox.Text;
            if (string.IsNullOrEmpty(q)) { findStatus.Text = ""; return; }
            string jsQ = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(q);
            string js = "window.find(" + jsQ + ", false, " + (backwards ? "true" : "false") + ", true);";
            findWebView.CoreWebView2.ExecuteScriptAsync(js).ContinueWith(t =>
            {
                bool found = t.IsCompleted && t.Result != null && t.Result.Trim() == "true";
                this.BeginInvoke((Action)(() => findStatus.Text = found ? "Match found" : "No more matches"));
            });
        }

        public void ShowSoftCommunication(string msg)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((Action)(() => ShowSoftCommunication(msg)));
                return;
            }

            softBannerLabel.Text = msg;
            softBanner.Visible = true;
            bannerTimer.Stop();
            bannerTimer.Start();
        }

        // ===== SMART ADDRESS BAR AUTOCOMPLETE (TRIE DATA STRUCTURE) =====

        private void PopulateOmniTrie()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string[] topSites = new[] {
                        "google.com", "youtube.com", "github.com", "reddit.com", "wikipedia.org",
                        "twitter.com", "x.com", "chatgpt.com", "amazon.com", "netflix.com",
                        "linkedin.com", "instagram.com", "stackoverflow.com", "news.ycombinator.com"
                    };
                    foreach (var s in topSites)
                        omniTrie.Insert(s, s, "https://" + s, "🌐", 10);

                    var bookmarks = BookmarksManager.GetBookmarks();
                    if (bookmarks != null)
                    {
                        foreach (var b in bookmarks)
                        {
                            omniTrie.Insert(b.Title, b.Title, b.Url, "⭐", 25);
                            omniTrie.Insert(b.Url, b.Title, b.Url, "⭐", 20);
                        }
                    }

                    var history = HistoryManager.GetHistoryItems();
                    if (history != null)
                    {
                        foreach (var h in history)
                        {
                            omniTrie.Insert(h.Title, h.Title, h.Url, "🕒", 5);
                            omniTrie.Insert(h.Url, h.Title, h.Url, "🕒", 5);
                        }
                    }
                }
                catch { }
            });
        }

        private void ShowAutocomplete(string query)
        {
            if (acDropdown == null || !urlBar.Focused) return;
            if (string.IsNullOrWhiteSpace(query) || query.Length < 1)
            {
                HideAutocomplete();
                return;
            }

            // Don't show autocomplete for internal URLs
            if (query.StartsWith("black://") || query == "about:blank") { HideAutocomplete(); return; }

            acItems.Clear();
            acSelectedIndex = -1;

            // Instant Omnibox Math (=expression)
            string mathResult;
            if (TryEvaluateQuickMath(query, out mathResult))
            {
                acItems.Add(Tuple.Create("🧮", "= " + mathResult + "  (Press Enter to copy)", "math:" + mathResult));
            }

            // Switch-to-Open-Tab Suggestions (Chrome/Firefox style)
            int tabMatches = 0;
            for (int ti = 0; ti < tabControl.TabPages.Count && tabMatches < 3; ti++)
            {
                TabPage tp = tabControl.TabPages[ti];
                if (tp == null) continue;
                string tTitle = tp.Text ?? "";
                WebView2 twv = GetWebView(tp);
                string tUrl = (twv != null && twv.Source != null) ? twv.Source.ToString() : "";
                if (tTitle.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    tUrl.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    acItems.Add(Tuple.Create("📑", "Switch to tab: " + tTitle, "black://switch-tab?index=" + ti));
                    tabMatches++;
                }
            }

            // O(L) Trie Prefix Search
            var trieSuggestions = omniTrie.SearchPrefix(query, 6);
            foreach (var s in trieSuggestions)
            {
                acItems.Add(Tuple.Create(s.Category, s.Title, s.Url));
            }

            if (acItems.Count > 8) acItems.RemoveRange(8, acItems.Count - 8);

            // Always add configured search engine suggestion at the end
            string searchLabel = "🔍 Search " + (SettingsStore.SearchEngine ?? "google") + " for \"" +
                (query.Length > 40 ? query.Substring(0, 37) + "..." : query) + "\"";
            acItems.Add(Tuple.Create("🔍", searchLabel, SettingsStore.GetSearchUrl(query)));

            if (acItems.Count == 0) { HideAutocomplete(); return; }

            PaintAutocompleteItems();
            PositionAutocomplete();
            acDropdown.Visible = true;
            acDropdown.BringToFront();
        }

        private void HideAutocomplete()
        {
            if (acDropdown != null) acDropdown.Visible = false;
            acSelectedIndex = -1;
            acItems.Clear();
        }

        private void PositionAutocomplete()
        {
            if (acDropdown == null || omniShell == null) return;
            Point omniLoc = omniShell.PointToScreen(Point.Empty);
            Point formLoc = this.PointToClient(omniLoc);
            acDropdown.Location = new Point(formLoc.X, formLoc.Y + omniShell.Height + 2);
            acDropdown.Width = omniShell.Width;
        }

        private void PaintAutocompleteItems()
        {
            acDropdown.SuspendLayout();
            acDropdown.Controls.Clear();

            int itemHeight = 38;
            int y = 4;

            acDropdown.BackColor = isDarkMode ? Color.FromArgb(30, 30, 38) : Color.FromArgb(255, 255, 255);

            for (int i = 0; i < acItems.Count; i++)
            {
                var item = acItems[i];
                Panel row = new Panel();
                row.Location = new Point(4, y);
                row.Size = new Size(acDropdown.Width - 8, itemHeight);
                row.BackColor = isDarkMode
                    ? ((i == acSelectedIndex) ? Color.FromArgb(50, 50, 62) : Color.FromArgb(30, 30, 38))
                    : ((i == acSelectedIndex) ? Color.FromArgb(232, 238, 250) : Color.FromArgb(255, 255, 255));
                row.Cursor = Cursors.Hand;
                int idx = i;
                Action selectItem = () =>
                {
                    acNavigating = true;
                    string pickedUrl = acItems[idx].Item3;
                    if (!pickedUrl.StartsWith("math:", StringComparison.Ordinal) &&
                        !pickedUrl.StartsWith("black://switch-tab", StringComparison.OrdinalIgnoreCase))
                    {
                        urlBar.Text = pickedUrl;
                    }
                    HideAutocomplete();
                    NavigateCurrentTab(pickedUrl);
                    acNavigating = false;
                };

                row.Click += (s, e) => selectItem();
                row.MouseEnter += (s, e) =>
                {
                    acSelectedIndex = idx;
                    PaintAutocompleteItems();
                };

                // Type icon
                Label iconLbl = new Label();
                iconLbl.Text = item.Item1;
                iconLbl.Location = new Point(8, 2);
                iconLbl.Size = new Size(24, itemHeight - 4);
                iconLbl.ForeColor = isDarkMode ? Color.FromArgb(180, 185, 200) : Color.FromArgb(90, 95, 105);
                iconLbl.Font = new Font("Segoe UI", 11f);
                iconLbl.TextAlign = ContentAlignment.MiddleCenter;
                iconLbl.BackColor = Color.Transparent;
                iconLbl.Click += (s, e) => selectItem();
                row.Controls.Add(iconLbl);

                // Title
                string title = item.Item2 ?? "";
                if (title.Length > 55) title = title.Substring(0, 52) + "...";
                Label titleLbl = new Label();
                titleLbl.Text = title;
                titleLbl.Location = new Point(36, 3);
                titleLbl.Size = new Size(row.Width - 44, 18);
                titleLbl.ForeColor = isDarkMode ? Color.FromArgb(230, 232, 240) : Color.FromArgb(32, 33, 36);
                titleLbl.Font = new Font("Segoe UI Variable Display", 9.5f, FontStyle.Regular);
                titleLbl.BackColor = Color.Transparent;
                titleLbl.Click += (s, e) => selectItem();
                row.Controls.Add(titleLbl);

                // URL (don't show for search suggestion since it's verbose)
                if (item.Item1 != "🔍")
                {
                    string url = item.Item3 ?? "";
                    if (url.StartsWith("math:", StringComparison.Ordinal))
                        url = "Instant Calculator • Click or Enter to copy result";
                    else if (url.StartsWith("black://switch-tab", StringComparison.OrdinalIgnoreCase))
                        url = "Jump to open tab";
                    if (url.Length > 70) url = url.Substring(0, 67) + "...";
                    Label urlLbl = new Label();
                    urlLbl.Text = url;
                    urlLbl.Location = new Point(36, 20);
                    urlLbl.Size = new Size(row.Width - 44, 15);
                    urlLbl.ForeColor = isDarkMode ? Color.FromArgb(120, 130, 160) : Color.FromArgb(100, 105, 115);
                    urlLbl.Font = new Font("Segoe UI Variable Display", 8f);
                    urlLbl.BackColor = Color.Transparent;
                    urlLbl.Click += (s, e) => selectItem();
                    row.Controls.Add(urlLbl);
                }

                acDropdown.Controls.Add(row);
                y += itemHeight + 2;
            }

            acDropdown.Height = y + 4;
            acDropdown.ResumeLayout(true);
        }

        private void PositionOmnibox()
        {
            if (tabControl == null || omniboxPanel == null) return;

            int tabStripHeight = tabControl.DisplayRectangle.Y;
            if (tabStripHeight <= 0) tabStripHeight = 46;
            omniboxPanel.Location = new Point(0, tabStripHeight);
            omniboxPanel.Width = this.ClientSize.Width;
            omniboxPanel.BringToFront();

            UpdateNewTabButtonPosition();

            if (actionsPanel != null)
            {
                actionsPanel.BringToFront();
            }

            if (omniShell != null && actionsPanel != null)
            {
                int navW = navStrip != null ? navStrip.Width : 176;
                int actW = actionsPanel.Width > 0 ? actionsPanel.Width : 160;
                omniShell.Location = new Point(navW + 8, 12);
                omniShell.Width = Math.Max(180, omniboxPanel.Width - navW - actW - 20);
            }
        }

        private void UpdateNewTabButtonPosition()
        {
            if (tabNewBtn == null || tabControl == null) return;

            int tabStripHeight = tabControl.DisplayRectangle.Y;
            if (tabStripHeight <= 0) tabStripHeight = 44;

            int x = 12;
            if (tabControl.TabCount > 0)
            {
                try
                {
                    Rectangle lastTab = tabControl.GetTabRect(tabControl.TabCount - 1);
                    x = lastTab.Right + 8;
                }
                catch { }
            }
            int maxX = this.ClientSize.Width - tabNewBtn.Width - 8;
            if (x > maxX) x = maxX;
            if (x < 12) x = 12;
            tabNewBtn.Location = new Point(x, Math.Max(2, (tabStripHeight - tabNewBtn.Height) / 2));
            tabNewBtn.BringToFront();
        }

        private void SetOmniboxVisible(bool visible)
        {
            omniboxPanel.Visible = visible;
            if (tabNewBtn != null) tabNewBtn.Visible = visible;
            if (!visible)
            {
                if (downloadsShelf != null) downloadsShelf.Visible = false;
                if (sidebarPanel != null) sidebarPanel.Visible = false;
                if (pwPrompt != null) pwPrompt.Visible = false;
            }

            int stripH = tabControl.DisplayRectangle.Y;

            if (visible)
            {
                tabControl.Dock = DockStyle.Fill;
                tabControl.Location = new Point(0, 0);
                tabControl.Width = this.ClientSize.Width;
                tabControl.Height = this.ClientSize.Height;
                foreach (TabPage p in tabControl.TabPages)
                {
                    p.Padding = new Padding(0, 72, 0, 0);
                }
            }
            else
            {
                tabControl.Dock = DockStyle.None;
                tabControl.Location = new Point(0, -stripH);
                tabControl.Width = this.ClientSize.Width;
                tabControl.Height = this.ClientSize.Height + stripH;
                foreach (TabPage p in tabControl.TabPages)
                {
                    p.Padding = new Padding(0, 0, 0, 0);
                }
            }

            PositionOmnibox();
            this.PerformLayout();
        }

        private void ToggleCurrentTabBookmark()
        {
            WebView2 wv = GetCurrentWebView();
            if (wv != null && wv.CoreWebView2 != null)
            {
                string url = wv.Source != null ? wv.Source.ToString() : "";
                if (string.IsNullOrWhiteSpace(url) || url == "about:blank" || url.StartsWith("black://") || InternalUrlRouter.IsInternalOrigin(url))
                {
                    NavigateCurrentTab("black://bookmarks");
                    return;
                }
                string title = wv.CoreWebView2.DocumentTitle;
                bool isBkd = BookmarksManager.ToggleBookmark(title, url);

                if (isBkd)
                {
                    starBtn.IconType = VectorIcon.StarFilled;
                    starBtn.ForeColor = Color.FromArgb(245, 158, 11);
                    ShowSoftCommunication("⭐ Bookmark Added Locally to Device!");
                }
                else
                {
                    starBtn.IconType = VectorIcon.Star;
                    starBtn.ForeColor = isDarkMode ? Color.FromArgb(200, 205, 220) : Color.FromArgb(95, 99, 104);
                    ShowSoftCommunication("🗑️ Bookmark Removed");
                }
            }
        }

        private void ShowAdShieldStatus()
        {
            ShowPerSiteShieldDialog();
        }

        private void OpenSettingsDialog(int initialTab)
        {
            using (SettingsForm sf = new SettingsForm(
                isDarkMode ? 1 : 0,
                eyeCareMode,
                (themeIndex) => SetTheme(themeIndex == 1),
                (eyeCareIndex) => SetEyeCareMode(eyeCareIndex),
                initialTab,
                (u) => NavigateCurrentTab(u),
                ApplyUserPreferences))
            {
                sf.ShowDialog(this);
            }

            ApplyUserPreferences();
            ReloadAllSpeedDials();
        }

        private void ApplyUserPreferences()
        {
            try
            {
                ChromePainter.Accent = ParseAccentColor(SettingsStore.Accent);
                ChromePainter.Enabled = SettingsStore.AnimationsEnabled;
                lruTabManager.SetMaxActiveTabs(SettingsStore.MaxActiveTabs);
                if (homeBtn != null) homeBtn.Visible = SettingsStore.ShowHomeButton;
                if (navStrip != null) navStrip.Width = SettingsStore.ShowHomeButton ? 196 : 150;
                UpdateUICornerRadius();
                PositionOmnibox();
                foreach (TabPage page in tabControl.TabPages)
                {
                    WebView2 wv = GetWebView(page);
                    if (wv != null && !wv.IsDisposed && wv.CoreWebView2 != null)
                    {
                        try
                        {
                            wv.CoreWebView2.Profile.PreferredColorScheme = SettingsStore.ForceDarkMode
                                ? CoreWebView2PreferredColorScheme.Dark
                                : CoreWebView2PreferredColorScheme.Auto;
                            wv.CoreWebView2.Profile.PreferredTrackingPreventionLevel = SettingsStore.StrictTrackingPrevention
                                ? CoreWebView2TrackingPreventionLevel.Strict
                                : CoreWebView2TrackingPreventionLevel.Balanced;
                        }
                        catch { }
                    }
                }
                this.Invalidate(true);
            }
            catch { }
        }

        private void ReloadAllSpeedDials()
        {
            try
            {
                foreach (TabPage page in tabControl.TabPages)
                {
                    WebView2 wv = GetWebView(page);
                    if (wv != null && wv.CoreWebView2 != null && wv.Source != null)
                    {
                        string src = wv.Source.ToString();
                        if (InternalUrlRouter.IsInternalOrigin(src) || src.StartsWith("black://", StringComparison.OrdinalIgnoreCase))
                        {
                            wv.Reload();
                        }
                    }
                }
            }
            catch { }
        }

        private void UpdateUICornerRadius()
        {
            int radius = SettingsStore.CornerRadius;
            int actionRadius = Math.Max(0, radius - 2);

            try
            {
                tabNewBtn.CornerRadius = radius;
                backBtn.CornerRadius = radius;
                fwdBtn.CornerRadius = radius;
                reloadBtn.CornerRadius = radius;
                homeBtn.CornerRadius = radius;
                menuBtn.CornerRadius = radius;
                downloadsBtn.CornerRadius = radius;

                starBtn.CornerRadius = actionRadius;
                readerBtn.CornerRadius = actionRadius;
            }
            catch { }
        }

        private void UpdateShieldButtonText()
        {
            if (shieldBtn == null) return;
            try
            {
                if (this.InvokeRequired)
                {
                    this.BeginInvoke((Action)UpdateShieldButtonText);
                    return;
                }
                string countStr = totalBlockedAds.ToString();
                if (shieldBtn.Image != null)
                {
                    shieldBtn.Text = " " + countStr;
                }
                else
                {
                    shieldBtn.Text = "🛡 " + countStr;
                }

                using (Graphics g = shieldBtn.CreateGraphics())
                {
                    SizeF sz = g.MeasureString(shieldBtn.Text, shieldBtn.Font);
                    int iconW = shieldBtn.Image != null ? 28 : 0;
                    int neededW = Math.Max(76, (int)sz.Width + iconW + 24);
                    if (shieldBtn.Width != neededW)
                    {
                        shieldBtn.Width = neededW;
                        Region oldReg = shieldBtn.Region;
                        shieldBtn.Region = new Region(GetRoundedPath(new Rectangle(0, 0, shieldBtn.Width, shieldBtn.Height), 10));
                        if (oldReg != null) oldReg.Dispose();
                    }
                }
            }
            catch { }
        }

        private static Dictionary<string, Image> iconCache = new Dictionary<string, Image>();

        private Image LoadIcon(string iconName, int size = 20)
        {
            string cacheKey = iconName + "_" + size;
            if (iconCache.ContainsKey(cacheKey)) return iconCache[cacheKey];

            try
            {
                string[] potentialBases = new[]
                {
                    Path.Combine(Application.StartupPath, "assets", "icons"),
                    Path.Combine(Application.StartupPath, "..", "..", "assets", "icons"),
                    Path.Combine(Application.StartupPath, "..", "assets", "icons"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assets", "icons"),
                    Path.Combine(Directory.GetCurrentDirectory(), "assets", "icons"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "assets", "icons")
                };

                foreach (var basePath in potentialBases)
                {
                    if (!Directory.Exists(basePath)) continue;

                    string whitePath = Path.Combine(basePath, iconName + "_20_regular_white.png");
                    string regularPath = Path.Combine(basePath, iconName + "_20_regular.png");
                    string imagePath = File.Exists(whitePath) ? whitePath : (File.Exists(regularPath) ? regularPath : "");

                    if (File.Exists(imagePath))
                    {
                        byte[] bytes = File.ReadAllBytes(imagePath);
                        using (var ms = new MemoryStream(bytes))
                        using (var src = Image.FromStream(ms))
                        {
                            Bitmap bmp = new Bitmap(src, new Size(size, size));
                            iconCache[cacheKey] = bmp;
                            return bmp;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        private Button CreateBtn(string iconName, int left)
        {
            Button b = new Button();
            b.Location = new Point(left + 2, 3);
            b.Width = 44;
            b.Height = 44;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 60, 76);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(74, 74, 92);
            b.BackColor = Color.Transparent;
            b.ForeColor = Color.FromArgb(210, 215, 230);
            b.Font = new Font("Segoe UI Variable Display", 14f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.TabStop = false;
            b.Region = new Region(GetRoundedPath(new Rectangle(0, 0, 44, 44), 12));
            b.Text = "";

            var img = LoadIcon(iconName, 24);
            if (img != null)
            {
                b.Image = img;
                b.ImageAlign = ContentAlignment.MiddleCenter;
                b.TextImageRelation = TextImageRelation.ImageBeforeText;
            }
            else
            {
                string fallback = iconName == "arrow_left" ? "←" :
                                 iconName == "arrow_right" ? "→" :
                                 iconName == "arrow_clockwise" ? "↻" :
                                 iconName == "home" ? "🏠" : "";
                b.Text = fallback;
                b.Font = new Font("Segoe UI Variable Display", 16f, FontStyle.Bold);
            }
            return b;
        }

        private Button CreateActionBtn(string iconName, Color bg, Color fg, int width, string fallbackText = null, int height = 40)
        {
            Button b = new Button();
            b.Width = width;
            b.Height = height;
            b.Margin = new Padding(3, 2, 3, 2);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(72, 72, 88);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(86, 86, 104);
            b.BackColor = bg;
            b.ForeColor = fg;
            b.Font = new Font("Segoe UI Variable Display", 10f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.TabStop = false;
            int radius = 10;
            b.Region = new Region(GetRoundedPath(new Rectangle(0, 0, width, height), radius));
            b.Text = "";

            var img = LoadIcon(iconName, 20);
            if (img != null)
            {
                b.Image = img;
                b.ImageAlign = ContentAlignment.MiddleCenter;
                b.TextImageRelation = TextImageRelation.ImageBeforeText;
                if (!string.IsNullOrEmpty(fallbackText))
                {
                    b.Text = " " + fallbackText;
                }
            }
            else
            {
                b.Text = fallbackText ?? iconName;
            }

            b.Resize += (s, e) =>
            {
                if (b.Width > 0 && b.Height > 0)
                {
                    Region oldReg = b.Region;
                    b.Region = new Region(GetRoundedPath(new Rectangle(0, 0, b.Width, b.Height), radius));
                    if (oldReg != null) oldReg.Dispose();
                }
            };

            return b;
        }

        private GraphicsPath GetRoundedPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0 || r.Width <= 0 || r.Height <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private void OnDrawTabItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= tabControl.TabPages.Count) return;

            TabPage page = tabControl.TabPages[e.Index];
            Rectangle rect = tabControl.GetTabRect(e.Index);
            bool selected = (tabControl.SelectedIndex == e.Index);
            bool isPrivate = page.Tag != null && (bool)page.Tag == true;
            bool isPinned = pinnedTabs.Contains(page);

            Color backColor;
            if (isDarkMode)
            {
                backColor = isPrivate
                    ? (selected ? Color.FromArgb(32, 34, 46) : Color.FromArgb(20, 20, 26))
                    : isPinned
                        ? (selected ? Color.FromArgb(28, 42, 48) : Color.FromArgb(20, 28, 32))
                        : (selected ? Color.FromArgb(32, 34, 44) : Color.FromArgb(20, 20, 26));

                if (e.Index == hoverTabIndex && !selected)
                {
                    backColor = isPrivate ? Color.FromArgb(40, 40, 54) : Color.FromArgb(28, 30, 38);
                }
            }
            else
            {
                backColor = isPrivate
                    ? (selected ? Color.FromArgb(240, 235, 255) : Color.FromArgb(230, 225, 245))
                    : isPinned
                        ? (selected ? Color.FromArgb(230, 245, 242) : Color.FromArgb(220, 235, 232))
                        : (selected ? Color.FromArgb(255, 255, 255) : Color.FromArgb(228, 230, 235));

                if (e.Index == hoverTabIndex && !selected)
                {
                    backColor = isPrivate ? Color.FromArgb(235, 230, 250) : Color.FromArgb(238, 240, 244);
                }
            }

            Rectangle tabRect = new Rectangle(rect.X + 2, rect.Y + 3, rect.Width - 4, rect.Height - 3);
            using (GraphicsPath p = GetRoundedPath(tabRect, 8))
            using (SolidBrush b = new SolidBrush(backColor))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(b, p);
            }

            TabMetadata meta = GetTabMetadata(page);
            string containerName = meta != null ? meta.ContainerName : "";
            bool hasContainer = !string.IsNullOrEmpty(containerName);
            bool isSuspended = lruTabManager.IsTabSuspended(page);
            bool isPlayingAudio = false;
            bool isMuted = false;
            WebView2 tabWv = GetWebView(page);
            if (tabWv != null && !tabWv.IsDisposed && tabWv.CoreWebView2 != null)
            {
                try { isPlayingAudio = tabWv.CoreWebView2.IsDocumentPlayingAudio; } catch { }
                try { isMuted = tabWv.CoreWebView2.IsMuted; } catch { }
            }

            if (selected || hasContainer)
            {
                Color barColor = isPrivate ? Color.FromArgb(160, 90, 240)
                    : hasContainer ? GetContainerAccentColor(containerName)
                    : isPinned ? Color.FromArgb(0, 180, 160)
                    : Color.FromArgb(12, 99, 232);
                using (SolidBrush bar = new SolidBrush(barColor))
                {
                    int barH = selected ? 3 : 2;
                    Rectangle topBar = new Rectangle(tabRect.X + 8, tabRect.Y + 1, tabRect.Width - 16, barH);
                    using (GraphicsPath bp = GetRoundedPath(topBar, 2))
                    {
                        e.Graphics.FillPath(bar, bp);
                    }
                }
            }

            Color textColor = isPrivate
                ? (isDarkMode ? Color.FromArgb(210, 195, 255) : Color.FromArgb(100, 60, 180))
                : (isDarkMode
                    ? (selected ? Color.FromArgb(245, 245, 250) : Color.FromArgb(160, 165, 180))
                    : (selected ? Color.FromArgb(32, 33, 36) : Color.FromArgb(95, 99, 104)));

            string displayText = (isPinned ? "📌 " : "")
                + (isSuspended ? "💤 " : "")
                + (isMuted ? "🔇 " : (isPlayingAudio ? "🔊 " : ""))
                + page.Text;

            TextRenderer.DrawText(e.Graphics, displayText, tabControl.Font,
                new Rectangle(rect.X + 12, rect.Y + 4, rect.Width - 44, rect.Height - 4),
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            if (!isPinned)
            {
                Rectangle closeRect = new Rectangle(rect.Right - 26, rect.Y + (rect.Height - 16) / 2, 16, 16);
                float cx = closeRect.X + closeRect.Width / 2f;
                float cy = closeRect.Y + closeRect.Height / 2f;

                Color xColor = isDarkMode
                    ? (selected ? Color.FromArgb(200, 205, 225) : Color.FromArgb(140, 145, 160))
                    : (selected ? Color.FromArgb(60, 64, 67) : Color.FromArgb(128, 134, 139));
                using (Pen xPen = new Pen(xColor, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    e.Graphics.DrawLine(xPen, cx - 3.5f, cy - 3.5f, cx + 3.5f, cy + 3.5f);
                    e.Graphics.DrawLine(xPen, cx + 3.5f, cy - 3.5f, cx - 3.5f, cy + 3.5f);
                }
            }

            if (dragMarkerX >= 0 && e.Index != dragTabIndex)
            {
                Rectangle r = tabControl.GetTabRect(e.Index);
                if (dragMarkerX >= r.Left && dragMarkerX <= r.Right)
                {
                    using (SolidBrush mb = new SolidBrush(Color.FromArgb(0, 140, 255)))
                    {
                        e.Graphics.FillRectangle(mb, dragMarkerX - 1, r.Y + 6, 2, r.Height - 12);
                    }
                }
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

                    Rectangle closeRect = new Rectangle(rect.Right - 28, rect.Y + (rect.Height - 18) / 2, 22, 22);
                    if (e.Button == MouseButtons.Left && !pinnedTabs.Contains(tabControl.TabPages[i]) && closeRect.Contains(e.Location))
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

            // Start drag detection
            if (e.Button == MouseButtons.Left)
            {
                dragStartPoint = e.Location;
                dragTabIndex = -1;
                dragInsertionIndex = -1;
                dragMarkerX = -1;
                // Check if click is on a tab to initiate drag
                for (int i = 0; i < tabControl.TabPages.Count; i++)
                {
                    Rectangle rect = tabControl.GetTabRect(i);
                    if (rect.Contains(e.Location))
                    {
                        // Will start drag if mouse moves enough pixels outside tab
                        dragTabIndex = i;
                        break;
                    }
                }
            }
        }

        private void OnTabMouseMove(object sender, MouseEventArgs e)
        {
            if (dragTabIndex >= 0 && e.Button == MouseButtons.Left)
            {
                // 16px horizontal threshold before drag is considered
                int dx = Math.Abs(e.X - dragStartPoint.X);
                if (dx >= 16)
                {
                    // Compute insertion index by comparing mouse X against each tab's center
                    int count = tabControl.TabPages.Count;
                    int target = count;
                    for (int i = 0; i < count; i++)
                    {
                        if (i == dragTabIndex) continue;
                        Rectangle r = tabControl.GetTabRect(i);
                        if (e.X < r.Left + r.Width / 2)
                        {
                            target = i;
                            break;
                        }
                    }

                    // Map target to a marker X position in the current (pre-removal) layout
                    int markerX;
                    if (target >= count) markerX = tabControl.GetTabRect(count - 1).Right;
                    else markerX = tabControl.GetTabRect(target).Left;

                    if (target != dragInsertionIndex || markerX != dragMarkerX)
                    {
                        dragInsertionIndex = target;
                        dragMarkerX = markerX;
                        tabControl.Invalidate();
                    }
                }
            }
            else
            {
                // Track hover tab when not dragging
                int newHoverIndex = -1;
                for (int i = 0; i < tabControl.TabPages.Count; i++)
                {
                    Rectangle rect = tabControl.GetTabRect(i);
                    if (rect.Contains(e.Location))
                    {
                        newHoverIndex = i;
                        break;
                    }
                }

                if (newHoverIndex != hoverTabIndex)
                {
                    hoverTabIndex = newHoverIndex;
                    tabControl.Invalidate();
                }
            }
        }

        private void OnTabMouseUp(object sender, MouseEventArgs e)
        {
            if (dragTabIndex >= 0 && dragInsertionIndex >= 0)
            {
                // Perform the reorder: remove dragged tab, insert at the target slot
                int count = tabControl.TabPages.Count;
                int target = dragInsertionIndex;
                var tmp = tabControl.TabPages[dragTabIndex];
                tabControl.TabPages.RemoveAt(dragTabIndex);
                int insertAt = (target > dragTabIndex) ? target - 1 : target;
                if (insertAt > count - 2) insertAt = count - 1;
                tabControl.TabPages.Insert(insertAt, tmp);
            }

            dragTabIndex = -1;
            dragStartPoint = Point.Empty;
            dragInsertionIndex = -1;
            dragMarkerX = -1;
            tabControl.Invalidate();
        }

        private void InitializeTabContextMenu()
        {
            tabContextMenu = new ContextMenuStrip();
            tabContextMenu.Font = new Font("Segoe UI Variable Display", 9.5f);
            tabContextMenu.ShowImageMargin = false;
            tabContextMenu.BackColor = Color.FromArgb(32, 33, 36);
            tabContextMenu.ForeColor = Color.FromArgb(232, 234, 237);
            tabContextMenu.Renderer = new ChromeMenuRenderer();

            tabContextMenu.Items.Add("📌 Pin / Unpin Tab", null, (s, e) =>
            {
                if (rightClickedTab != null)
                {
                    if (pinnedTabs.Contains(rightClickedTab))
                    {
                        pinnedTabs.Remove(rightClickedTab);
                        ShowSoftCommunication("📌 Tab Unpinned");
                    }
                    else
                    {
                        pinnedTabs.Add(rightClickedTab);
                        ShowSoftCommunication("📌 Tab Pinned");
                    }
                    tabControl.Invalidate();
                }
            });

            tabContextMenu.Items.Add("➕ Duplicate Tab (Ctrl+Shift+K)", null, (s, e) => DuplicateCurrentTab(rightClickedTab));

            tabContextMenu.Items.Add("↩️ Re-open Closed Tab (Ctrl+Shift+T)", null, (s, e) => ReopenLastClosedTab());

            ToolStripMenuItem containerTabMenu = new ToolStripMenuItem("🗂️ Open in Container Tab");
            string[] ctxContainers = new string[] { "Work", "Personal", "Shopping", "Dev" };
            foreach (string cname in ctxContainers)
            {
                string capturedContainer = cname;
                containerTabMenu.DropDownItems.Add(capturedContainer + " Container", null, (s, e) =>
                {
                    AddNewTab("[" + capturedContainer + "] Tab", "black://home", false, null, null, capturedContainer);
                });
            }
            tabContextMenu.Items.Add(containerTabMenu);

            tabContextMenu.Items.Add("🔊 Mute / Unmute Tab", null, (s, e) =>
            {
                if (rightClickedTab != null)
                {
                    WebView2 wv = GetWebView(rightClickedTab);
                    if (wv != null && wv.CoreWebView2 != null)
                    {
                        wv.CoreWebView2.IsMuted = !wv.CoreWebView2.IsMuted;
                        tabControl.Invalidate();
                        ShowSoftCommunication(wv.CoreWebView2.IsMuted ? "🔇 Tab Muted" : "🔊 Tab Unmuted");
                    }
                }
            });

            tabContextMenu.Items.Add("📺 Picture-in-Picture", null, (s, e) => TriggerPictureInPictureOnTab(rightClickedTab));
            tabContextMenu.Items.Add("🎵 Audio & Media Hub", null, (s, e) => ShowMediaHubDialog());
            tabContextMenu.Items.Add("🔒 Site Info & Permissions", null, (s, e) => ShowSitePermissionsDialog());

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
                string disp = InternalUrlRouter.GetDisplayUrl(u);
                if (!string.IsNullOrEmpty(disp) && disp != "black://home")
                {
                    closedTabStack.Push(Tuple.Create(page.Text, disp));
                    while (closedTabStack.Count > 10) closedTabStack.Pop();
                }
            }

            pinnedTabs.Remove(page);

            if (tabControl.TabPages.Count > 1)
            {
                if (wv != null)
                {
                    try { if (wv.CoreWebView2 != null) wv.CoreWebView2.Stop(); } catch { }
                    wv.Dispose();
                }
                lruTabManager.RemoveTab(page);
                tabMetadata.Remove(page);
                tabControl.TabPages.Remove(page);
                page.Dispose();
            }
            else
            {
                TabMetadata meta = GetTabMetadata(page);
                if (meta != null) { meta.ReaderActive = false; meta.ReaderOriginalUrl = ""; meta.ReaderHtml = ""; }
                UpdateReaderButtonState();
                if (wv != null && wv.CoreWebView2 != null)
                {
                    wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/home");
                    urlBar.Text = "";
                    tabControl.SelectedTab.Text = "New Tab";
                }
            }

            PositionOmnibox();
            this.BeginInvoke((Action)(() => UpdateNewTabButtonPosition()));
            SaveCurrentSession();
        }

        private void DuplicateCurrentTab(TabPage targetTab = null)
        {
            TabPage target = targetTab ?? tabControl.SelectedTab;
            if (target != null)
            {
                WebView2 wv = GetWebView(target);
                string url = "black://home";
                if (wv != null)
                {
                    string raw = "";
                    if (wv.CoreWebView2 != null && !string.IsNullOrEmpty(wv.CoreWebView2.Source))
                        raw = wv.CoreWebView2.Source;
                    else if (wv.Source != null)
                        raw = wv.Source.ToString();
                    string disp = InternalUrlRouter.GetDisplayUrl(raw);
                    url = !string.IsNullOrEmpty(disp) ? disp : (InternalUrlRouter.IsInternalOrigin(raw) || raw == "about:blank" ? "black://home" : raw);
                }

                bool isPriv = target.Tag != null && (bool)target.Tag == true;
                AddNewTab(target.Text, url, isPrivate: isPriv);
                ShowSoftCommunication("📑 Tab Duplicated");
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
            mainMenu.ShowImageMargin = false;
            mainMenu.BackColor = Color.FromArgb(32, 33, 36);
            mainMenu.ForeColor = Color.FromArgb(232, 234, 237);
            mainMenu.Renderer = new ChromeMenuRenderer();

            mainMenu.Items.Add("New tab (Ctrl+T)", null, (s, e) => AddNewTab("New Tab", "black://home"));
            mainMenu.Items.Add("New private tab (Ctrl+Shift+P)", null, (s, e) => AddNewTab("Private Tab", "black://home", isPrivate: true));
            ToolStripMenuItem newContainerMenu = new ToolStripMenuItem("🗂️ New container tab");
            string[] menuContainers = new string[] { "Work", "Personal", "Shopping", "Dev" };
            foreach (string cname in menuContainers)
            {
                string capturedContainer = cname;
                newContainerMenu.DropDownItems.Add(capturedContainer + " Container", null, (s, e) =>
                {
                    AddNewTab("[" + capturedContainer + "] Tab", "black://home", false, null, null, capturedContainer);
                });
            }
            mainMenu.Items.Add(newContainerMenu);
            mainMenu.Items.Add("Duplicate tab (Ctrl+Shift+K)", null, (s, e) => DuplicateCurrentTab());
            mainMenu.Items.Add("Reopen closed tab (Ctrl+Shift+T)", null, (s, e) => ReopenLastClosedTab());
            mainMenu.Items.Add(new ToolStripSeparator());

            mainMenu.Items.Add("History (Ctrl+H)", null, (s, e) => NavigateCurrentTab("black://history"));
            mainMenu.Items.Add("Downloads (Ctrl+J)", null, (s, e) => NavigateCurrentTab("black://downloads"));
            mainMenu.Items.Add("Bookmarks (Ctrl+D)", null, (s, e) => NavigateCurrentTab("black://bookmarks"));
            mainMenu.Items.Add("🧩 Extensions (Ctrl+Shift+X)", null, (s, e) => NavigateCurrentTab("black://extensions"));
            mainMenu.Items.Add("🔐 Passwords", null, (s, e) => NavigateCurrentTab("black://passwords"));
            mainMenu.Items.Add(new ToolStripSeparator());

            mainMenu.Items.Add("Zoom in (Ctrl +)", null, (s, e) => AdjustZoom(0.1f));
            mainMenu.Items.Add("Zoom out (Ctrl -)", null, (s, e) => AdjustZoom(-0.1f));
            mainMenu.Items.Add("Reset zoom (Ctrl 0)", null, (s, e) => AdjustZoom(0f, true));
            mainMenu.Items.Add(new ToolStripSeparator());

            mainMenu.Items.Add("⚡ Optimize memory", null, (s, e) =>
            {
                MemoryTrimmer.TrimProcessMemory();
                long ramMB = MemoryTrimmer.GetWorkingSetMB();
                ShowSoftCommunication("⚡ Memory Optimization: Working set purged (" + ramMB + "MB active)");
            });
            mainMenu.Items.Add("👁 Eye care filter (Ctrl+Shift+E)", null, (s, e) => CycleEyeCareMode());
            mainMenu.Items.Add("🌙 Dark / light theme (Ctrl+Shift+D)", null, (s, e) => ToggleTheme());
            mainMenu.Items.Add("📝 Dark notes (Ctrl+Shift+N)", null, (s, e) => OpenSettingsDialog(3));
            mainMenu.Items.Add("📱 QR code for page", null, (s, e) => ShowCurrentPageQrCode());
            mainMenu.Items.Add("🎵 Audio & Media Hub", null, (s, e) => ShowMediaHubDialog());
            mainMenu.Items.Add("📺 Picture-in-Picture", null, (s, e) => TriggerPictureInPictureOnTab(null));
            mainMenu.Items.Add("🔑 Generate Strong Password", null, (s, e) => PromptGenerateStrongPassword());
            mainMenu.Items.Add("🔒 Site Info & Permissions", null, (s, e) => ShowSitePermissionsDialog());
            mainMenu.Items.Add("📲 Install Site as Desktop App", null, (s, e) => InstallCurrentSiteAsDesktopApp());
            mainMenu.Items.Add("📸 Take screenshot", null, (s, e) => TakeScreenshot());
            mainMenu.Items.Add("Print page (Ctrl+P)", null, (s, e) =>
            {
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.CoreWebView2 != null)
                {
                    try { wv.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser); } catch { }
                }
            });

            ToolStripMenuItem devToolsItem = new ToolStripMenuItem("Developer tools (F12)");
            devToolsItem.Enabled = SettingsStore.AllowDeveloperTools;
            devToolsItem.Click += (s, e) =>
            {
                if (!SettingsStore.AllowDeveloperTools)
                {
                    ShowSoftCommunication("🔒 Developer tools are disabled in this build.");
                    return;
                }
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.CoreWebView2 != null)
                {
                    try { wv.CoreWebView2.OpenDevToolsWindow(); } catch { }
                }
            };
            mainMenu.Items.Add(devToolsItem);
            mainMenu.Items.Add(new ToolStripSeparator());

            mainMenu.Items.Add("🌐 Set as default browser", null, (s, e) => SetAsDefaultBrowser());
            mainMenu.Items.Add("🚀 Check for updates...", null, (s, e) => CheckForUpdatesUserManual());
            mainMenu.Items.Add("⚙️ Settings", null, (s, e) => OpenSettingsDialog(0));
            mainMenu.Items.Add("ℹ️ About & Legal", null, (s, e) => NavigateCurrentTab("black://about"));
            mainMenu.Items.Add(new ToolStripSeparator());

            mainMenu.Items.Add("Exit", null, (s, e) => ExitApp());
        }

        private void ShowCurrentPageQrCode()
        {
            WebView2 wv = GetCurrentWebView();
            string url = "";
            if (wv != null && wv.Source != null)
            {
                url = InternalUrlRouter.GetDisplayUrl(wv.Source.ToString());
                if (string.IsNullOrEmpty(url)) url = wv.Source.ToString();
            }
            if (string.IsNullOrWhiteSpace(url) || InternalUrlRouter.IsInternalOrigin(url) || url.StartsWith("black://", StringComparison.OrdinalIgnoreCase))
            {
                ShowSoftCommunication("📱 Open a webpage first to generate a QR code");
                return;
            }
            using (QrCodeForm qr = new QrCodeForm(url))
            {
                qr.ShowDialog(this);
            }
        }

        private void SetAsDefaultBrowser()
        {
            string err;
            if (DefaultBrowserManager.RegisterAsDefaultBrowser(out err))
            {
                ShowSoftCommunication("🌐 Windows Default Apps opened — Set Black Browser as Default Browser!");
            }
            else
            {
                ShowSoftCommunication("⚠️ " + err);
            }
        }

        private async void TakeScreenshot()
        {
            try
            {
                WebView2 wv = GetCurrentWebView();
                if (wv == null || wv.CoreWebView2 == null) { ShowSoftCommunication("⚠️ No page to capture"); return; }
                using (SaveFileDialog sfd = new SaveFileDialog())
                {
                    sfd.Filter = "PNG image|*.png";
                    sfd.FileName = "Black-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png";
                    if (sfd.ShowDialog(this) != DialogResult.OK) return;
                    using (FileStream fs = new FileStream(sfd.FileName, FileMode.Create))
                    {
                        await wv.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, fs);
                    }
                    ShowSoftCommunication("📸 Screenshot saved: " + Path.GetFileName(sfd.FileName));
                }
            }
            catch (Exception ex)
            {
                Log("Screenshot failed: " + ex.Message);
                ShowSoftCommunication("⚠️ Screenshot failed");
            }
        }

        private void AdjustZoom(float delta, bool reset = false)
        {
            WebView2 wv = GetCurrentWebView();
            if (wv == null) return;
            if (reset)
            {
                wv.ZoomFactor = 1.0;
                ShowSoftCommunication("🔍 Zoom: 100%");
            }
            else
            {
                wv.ZoomFactor = Math.Max(0.25, Math.Min(5.0, wv.ZoomFactor + delta));
                ShowSoftCommunication("🔍 Zoom: " + (int)(wv.ZoomFactor * 100) + "%");
            }
        }

        private void SetTheme(bool dark)
        {
            isDarkMode = dark;
            ChromePainter.IsDark = dark;

            Color bg = dark ? Color.FromArgb(18, 18, 22) : Color.FromArgb(243, 243, 243);
            Color omniBg = dark ? Color.FromArgb(28, 28, 34) : Color.FromArgb(255, 255, 255);
            Color inputBg = dark ? Color.FromArgb(44, 44, 54) : Color.FromArgb(241, 243, 244);
            Color inputFg = dark ? Color.FromArgb(240, 240, 245) : Color.FromArgb(32, 33, 36);
            Color btnBg = dark ? Color.FromArgb(38, 38, 46) : Color.FromArgb(255, 255, 255);
            Color btnFg = dark ? Color.FromArgb(200, 205, 220) : Color.FromArgb(95, 99, 104);
            Color btnBgAlt = dark ? Color.FromArgb(44, 44, 54) : Color.FromArgb(241, 243, 244);
            Color accentBg = dark ? Color.FromArgb(0, 96, 223) : Color.FromArgb(232, 240, 254);
            Color accentFg = dark ? Color.FromArgb(255, 255, 255) : Color.FromArgb(0, 103, 192);

            this.BackColor = bg;
            omniboxPanel.BackColor = omniBg;
            omniShell.BackColor = inputBg;
            urlBar.BackColor = inputBg;
            urlBar.ForeColor = inputFg;

            foreach (Button b in new Button[] { backBtn, fwdBtn, reloadBtn, homeBtn })
            {
                b.BackColor = Color.Transparent;
                b.ForeColor = btnFg;
            }
            UpdateNavButtons();

            menuBtn.BackColor = Color.Transparent;
            menuBtn.ForeColor = btnFg;
            downloadsBtn.BackColor = Color.Transparent;
            downloadsBtn.ForeColor = btnFg;
            tabNewBtn.ForeColor = dark ? Color.FromArgb(180, 185, 200) : Color.FromArgb(95, 99, 104);
            tabNewBtn.FlatAppearance.MouseOverBackColor = dark ? Color.FromArgb(58, 58, 68) : Color.FromArgb(218, 220, 224);
            Color shieldBg = dark ? Color.FromArgb(36, 38, 48) : Color.FromArgb(235, 238, 245);
            Color shieldFg = dark ? Color.FromArgb(220, 225, 240) : Color.FromArgb(40, 44, 52);
            shieldBtn.BackColor = shieldBg;
            shieldBtn.ForeColor = shieldFg;
            updateBtn.BackColor = Color.FromArgb(220, 80, 20);
            updateBtn.ForeColor = Color.White;

            WebView2 wvCurrent = GetCurrentWebView();
            if (wvCurrent != null && wvCurrent.Source != null)
            {
                string uriStr = wvCurrent.Source.ToString();
                bool isBkd = BookmarksManager.IsBookmarked(uriStr);
                if (isBkd)
                {
                    starBtn.IconType = VectorIcon.StarFilled;
                    starBtn.ForeColor = Color.FromArgb(245, 158, 11);
                }
                else
                {
                    starBtn.IconType = VectorIcon.Star;
                    starBtn.ForeColor = dark ? Color.FromArgb(200, 205, 220) : Color.FromArgb(95, 99, 104);
                }
            }
            else
            {
                starBtn.IconType = VectorIcon.Star;
                starBtn.ForeColor = dark ? Color.FromArgb(200, 205, 220) : Color.FromArgb(95, 99, 104);
            }

            readerBtn.BackColor = Color.Transparent;
            readerBtn.ForeColor = btnFg;
            UpdateReaderButtonState();

            foreach (TabPage p in tabControl.TabPages)
            {
                bool isPriv = p.Tag != null && (bool)p.Tag == true;
                p.BackColor = isPriv ? Color.FromArgb(18, 18, 24) : bg;

                WebView2 wv = GetWebView(p);
                if (wv != null && wv.CoreWebView2 != null && wv.Source != null)
                {
                    string uriStr = wv.Source.ToString();
                    if (InternalUrlRouter.IsInternalOrigin(uriStr))
                    {
                        wv.Reload();
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
            eyeCareOverlay.SetMode(mode, this);

            if (eyeCareMode == 1)
            {
                ShowSoftCommunication("👁️ Eye Care Filter: Warm Blue-Light Tint (25%)");
            }
            else if (eyeCareMode == 2)
            {
                ShowSoftCommunication("👁️ Eye Care Filter: Night Dimmer (35%)");
            }
            else
            {
                ShowSoftCommunication("👁️ Eye Care Filter: Disabled");
            }
        }

        private void CycleEyeCareMode()
        {
            SetEyeCareMode((eyeCareMode + 1) % 3);
        }

        private void CleanupStalePdfTemp()
        {
            try
            {
                if (!Directory.Exists(PdfTempDir)) return;
                foreach (string f in Directory.GetFiles(PdfTempDir, "*.pdf"))
                {
                    try
                    {
                        if (File.GetLastWriteTime(f) < DateTime.Now.AddDays(-1)) File.Delete(f);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private static bool IsInternalOrigin(string source)
        {
            return InternalUrlRouter.IsInternalOrigin(source);
        }

        private static string GetQueryParam(string uri, string key)
        {
            if (string.IsNullOrEmpty(uri) || string.IsNullOrEmpty(key)) return "";
            int qIdx = uri.IndexOf('?');
            if (qIdx < 0 || qIdx + 1 >= uri.Length) return "";
            string query = uri.Substring(qIdx + 1);
            string[] pairs = query.Split('&');
            foreach (string pair in pairs)
            {
                int eq = pair.IndexOf('=');
                string k = eq >= 0 ? Uri.UnescapeDataString(pair.Substring(0, eq)) : Uri.UnescapeDataString(pair);
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    return eq >= 0 ? Uri.UnescapeDataString(pair.Substring(eq + 1)) : "";
                }
            }
            return "";
        }

        private static bool IsPdfUrl(string uriStr)
        {
            if (string.IsNullOrEmpty(uriStr)) return false;
            Uri u;
            if (Uri.TryCreate(uriStr, UriKind.Absolute, out u))
                return (u.AbsolutePath ?? "").EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
            return uriStr.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        private void ShowAutofillPromptIfAvailable(WebView2 wv, TabPage page, string sourceUri)
        {
            try
            {
                if (wv == null || wv.IsDisposed || wv.CoreWebView2 == null || page == null || page.IsDisposed) return;
                if (page.Tag != null && (bool)page.Tag == true) return; // Do not auto-prompt in private tabs
                if (tabControl.SelectedTab != page) return;
                Uri u;
                if (!Uri.TryCreate(sourceUri, UriKind.Absolute, out u)) return;
                bool isLocalHttp = u.Scheme == Uri.UriSchemeHttp &&
                    (u.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || u.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase));
                if (u.Scheme != Uri.UriSchemeHttps && !isLocalHttp) return;

                var creds = PasswordManager.GetCredentialsForHost(u.Host);
                if (creds == null || creds.Count == 0) return;

                PasswordItem first = creds[0];
                wv.CoreWebView2.ExecuteScriptAsync(
                    "(function(){var p=document.querySelector('input[type=password]');return p&&!p.value&&p.offsetParent!==null?'1':'0';})()"
                ).ContinueWith(t =>
                {
                    try
                    {
                        if (t.IsFaulted || t.IsCanceled || string.IsNullOrEmpty(t.Result)) return;
                        if (t.Result.Trim('"') != "1") return;
                        if (this.IsDisposed || wv.IsDisposed || tabControl.SelectedTab != page) return;
                        this.BeginInvoke((Action)(() =>
                        {
                            if (pwPrompt == null || pwPromptLabel == null || pwSaveBtn == null) return;
                            pwAutofillMatches.Clear();
                            foreach (PasswordItem cred in creds) pwAutofillMatches.Add(cred);
                            pwCandidate = Tuple.Create(wv, u.Host, first.Username ?? "", first.Password ?? "");
                            pwPromptIsAutofill = true;
                            if (creds.Count > 1 && pwAccountCombo != null)
                            {
                                pwAccountCombo.Items.Clear();
                                foreach (PasswordItem cred in creds)
                                {
                                    pwAccountCombo.Items.Add(string.IsNullOrEmpty(cred.Username) ? "(no username)" : cred.Username);
                                }
                                pwAccountCombo.SelectedIndex = 0;
                                pwAccountCombo.Visible = true;
                                pwPromptLabel.Text = "🔑 Autofill (" + creds.Count + " accounts) for " + u.Host + ":";
                            }
                            else
                            {
                                if (pwAccountCombo != null) pwAccountCombo.Visible = false;
                                string userLabel = string.IsNullOrEmpty(first.Username) ? u.Host : (u.Host + " (" + first.Username + ")");
                                pwPromptLabel.Text = "🔑 Autofill saved password for " + userLabel + "?";
                            }
                            pwSaveBtn.Text = "Fill";
                            pwPrompt.Visible = true;
                        }));
                    }
                    catch { }
                });
            }
            catch { }
        }

        private async void InitializeBrowserEnv()
        {
            string userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "black-webview2");

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                bool success = false;
                try
                {
                    CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions();
                    options.AdditionalBrowserArguments = ExtensionManager.GetExtensionArguments();
                    options.AreBrowserExtensionsEnabled = true;

                    webViewEnv = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
                    Log("Environment created successfully with standard WebView2 environment settings");
                    success = true;
                }
                catch (Exception ex)
                {
                    Log("Env attempt " + attempt + " failed: " + ex.ToString());
                    ShowSoftCommunication("⚠️ WebView2 init attempt " + attempt + " failed.");
                }

                if (success && webViewEnv != null)
                {
                    bool restoredSession = false;
                    if (!hasExplicitLaunchArg && SettingsStore.RestoreSession)
                    {
                        restoringSession = true;
                        try
                        {
                            SessionStateData session = SessionManager.LoadSession();
                            if (session != null && session.Tabs != null && session.Tabs.Count > 0)
                            {
                                foreach (SessionTabEntry entry in session.Tabs)
                                {
                                    if (entry == null || string.IsNullOrWhiteSpace(entry.Url)) continue;
                                    string tUrl = entry.Url;
                                    string tTitle = string.IsNullOrWhiteSpace(entry.Title) ? "Tab" : entry.Title;
                                    AddNewTab(tTitle, tUrl);
                                    if (entry.Pinned && tabControl.TabPages.Count > 0)
                                    {
                                        pinnedTabs.Add(tabControl.TabPages[tabControl.TabPages.Count - 1]);
                                    }
                                    restoredSession = true;
                                }
                                if (restoredSession && session.ActiveIndex >= 0 && session.ActiveIndex < tabControl.TabPages.Count)
                                {
                                    tabControl.SelectedIndex = session.ActiveIndex;
                                }
                            }
                        }
                        catch { }
                        finally
                        {
                            restoringSession = false;
                        }
                    }

                    if (!restoredSession)
                    {
                        AddNewTab(initialStartupUrl.Contains("music.youtube.com") ? "YT Music" : (initialStartupUrl.Contains("youtube.com") ? "YouTube" : "New Tab"), initialStartupUrl);
                    }
                    CheckForUpdatesBackground();
                    return;
                }

                if (attempt < 3)
                {
                    await System.Threading.Tasks.Task.Delay(1500);
                }
            }

            Log("FATAL Env: Failed to create WebView2 environment after 3 attempts");
            if (string.IsNullOrEmpty(Program.GetInstalledWebView2Version()))
            {
                ShowSoftCommunication("⚠️ WebView2 Runtime is missing. Prompting installation...");
                this.BeginInvoke((Action)(() =>
                {
                    try
                    {
                        if (Program.EnsureWebView2RuntimeAvailable())
                        {
                            InitializeBrowserEnv();
                        }
                    }
                    catch { }
                }));
            }
            else
            {
                ShowSoftCommunication("⚠️ Failed to initialize WebView2. Some features may not work.");
            }
        }

        public async void AddNewTab(string title, string url, bool isPrivate = false, CoreWebView2NewWindowRequestedEventArgs newWindowArgs = null, CoreWebView2Deferral deferral = null, string containerName = null)
        {
            bool deferralCompleted = false;
            try
            {
                if (webViewEnv == null) return;

                string tabTitle = isPrivate ? "🕵️ Private Tab" : TruncateTitle(title);
                TabPage page = new TabPage(tabTitle);
                page.Tag = isPrivate;
                page.Padding = new Padding(0, (isAppWindowMode || isFullscreen) ? 0 : 72, 0, 0);

                TabMetadata initialMeta = GetTabMetadata(page);
                if (initialMeta != null && !string.IsNullOrEmpty(containerName))
                {
                    initialMeta.ContainerName = containerName.Trim();
                }

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
                lruTabManager.RegisterTab(page, wv);

                tabControl.Invalidate();
                this.PerformLayout();
                PositionOmnibox();
                this.BeginInvoke((Action)(() => UpdateNewTabButtonPosition()));

                CoreWebView2ControllerOptions ctrlOptions = null;
                if (isPrivate || !string.IsNullOrEmpty(containerName))
                {
                    try
                    {
                        ctrlOptions = webViewEnv.CreateCoreWebView2ControllerOptions();
                        if (ctrlOptions != null)
                        {
                            if (isPrivate) ctrlOptions.IsInPrivateModeEnabled = true;
                            if (!string.IsNullOrEmpty(containerName))
                            {
                                string cleanContainer = containerName.Trim().ToLowerInvariant();
                                ctrlOptions.ProfileName = "container_" + cleanContainer;
                            }
                        }
                    }
                    catch { }
                }

                if (ctrlOptions != null)
                {
                    await wv.EnsureCoreWebView2Async(webViewEnv, ctrlOptions);
                }
                else
                {
                    await wv.EnsureCoreWebView2Async(webViewEnv);
                }

                // Harden WebView2 settings (keep authentic runtime User-Agent & Client Hints)
                try { wv.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false; } catch { }
                try { wv.CoreWebView2.Settings.IsGeneralAutofillEnabled = !isPrivate; } catch { }
                try { wv.CoreWebView2.Settings.IsStatusBarEnabled = true; } catch { }
                try { wv.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = true; } catch { }
                try { wv.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = true; } catch { }
                try { wv.CoreWebView2.Settings.IsBuiltInErrorPageEnabled = true; } catch { }
                try { wv.CoreWebView2.Settings.AreDevToolsEnabled = SettingsStore.AllowDeveloperTools; } catch { }
                try { wv.CoreWebView2.MemoryUsageTargetLevel = CoreWebView2MemoryUsageTargetLevel.Low; } catch { }
                try
                {
                    if (SettingsStore.DefaultZoom >= 0.25 && SettingsStore.DefaultZoom <= 5.0 && Math.Abs(SettingsStore.DefaultZoom - 1.0) > 0.01)
                        wv.ZoomFactor = SettingsStore.DefaultZoom;
                }
                catch { }
                try
                {
                    wv.CoreWebView2.Profile.PreferredColorScheme = SettingsStore.ForceDarkMode
                        ? CoreWebView2PreferredColorScheme.Dark
                        : CoreWebView2PreferredColorScheme.Auto;
                    wv.CoreWebView2.Profile.PreferredTrackingPreventionLevel = SettingsStore.StrictTrackingPrevention
                        ? CoreWebView2TrackingPreventionLevel.Strict
                        : CoreWebView2TrackingPreventionLevel.Balanced;
                }
                catch { }

                wv.CoreWebView2.IsDocumentPlayingAudioChanged += (s, e) =>
                {
                    try
                    {
                        if (!this.IsDisposed && !tabControl.IsDisposed)
                            this.BeginInvoke((Action)(() => tabControl.Invalidate()));
                    }
                    catch { }
                };
                wv.CoreWebView2.IsMutedChanged += (s, e) =>
                {
                    try
                    {
                        if (!this.IsDisposed && !tabControl.IsDisposed)
                            this.BeginInvoke((Action)(() => tabControl.Invalidate()));
                    }
                    catch { }
                };

                // Serve internal pages from isolated https://black.internal virtual origin
                wv.CoreWebView2.AddWebResourceRequestedFilter(InternalUrlRouter.VirtualOrigin + "/*", CoreWebView2WebResourceContext.All);
                wv.CoreWebView2.WebResourceRequested += async (ws, we) =>
                {
                    try
                    {
                        await InternalUrlRouter.TryServeVirtualRequest(
                            wv, we,
                            () => isDarkMode,
                            () =>
                            {
                                TabMetadata m = GetTabMetadata(page);
                                return m != null ? m.ReaderHtml : "";
                            });
                    }
                    catch { }
                };

                // Password-save detection and WebMessage security handling
                wv.CoreWebView2.WebMessageReceived += async (ws, we) =>
                {
                    try
                    {
                        if (we == null || string.IsNullOrEmpty(we.WebMessageAsJson)) return;
                        string msgSource = we.Source ?? "";
                        bool isInternal = IsInternalOrigin(msgSource);

                        string raw = we.WebMessageAsJson;
                        string msg = raw.Trim().Trim('"');

                        // Privileged internal commands strictly require https://black.internal origin
                        if (isInternal)
                        {
                            string internalMsg = msg;
                            try
                            {
                                string jsDecoded = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<string>(raw);
                                if (jsDecoded != null) internalMsg = jsDecoded;
                            }
                            catch { }

                            if (internalMsg.StartsWith("notes:save:", StringComparison.Ordinal))
                            {
                                string noteBody = internalMsg.Substring("notes:save:".Length);
                                SettingsStore.SaveQuickNotes(noteBody);
                                return;
                            }
                            if (msg.StartsWith("open:", StringComparison.Ordinal))
                            {
                                int di;
                                if (int.TryParse(msg.Substring(5), out di))
                                {
                                    string path = DownloadsManager.GetDownloadPath(di);
                                    if (!string.IsNullOrEmpty(path) && (File.Exists(path) || Directory.Exists(path)))
                                    {
                                        this.BeginInvoke((Action)(() =>
                                        {
                                            try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path.Replace("\"", "") + "\"") { UseShellExecute = true }); } catch { }
                                        }));
                                    }
                                }
                                return;
                            }
                            if (msg == "clear")
                            {
                                DownloadsManager.ClearDownloads();
                                this.BeginInvoke((Action)(() =>
                                {
                                    try
                                    {
                                        if (!wv.IsDisposed && wv.CoreWebView2 != null)
                                            wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/downloads");
                                    }
                                    catch { }
                                }));
                                return;
                            }
                            if (msg.StartsWith("cookies:clear", StringComparison.Ordinal))
                            {
                                try { wv.CoreWebView2.CookieManager.DeleteAllCookies(); } catch { }
                                this.BeginInvoke((Action)(() =>
                                {
                                    try { if (!wv.IsDisposed && wv.CoreWebView2 != null) wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/cookies"); } catch { }
                                }));
                                return;
                            }
                            if (msg.StartsWith("cookies:del:", StringComparison.Ordinal))
                            {
                                string key = msg.Substring("cookies:del:".Length);
                                try
                                {
                                    byte[] kb = System.Convert.FromBase64String(key);
                                    string parts = System.Text.Encoding.UTF8.GetString(kb);
                                    int p1 = parts.IndexOf('|');
                                    int p2 = parts.LastIndexOf('|');
                                    if (p1 > 0 && p2 > p1)
                                    {
                                        string cname = parts.Substring(0, p1);
                                        string cdom = parts.Substring(p1 + 1, p2 - p1 - 1);
                                        string cpath = parts.Substring(p2 + 1);
                                        var cookies = await wv.CoreWebView2.CookieManager.GetCookiesAsync(null);
                                        foreach (var c in cookies)
                                        {
                                            if (c.Name == cname && (c.Domain == cdom || c.Domain == "." + cdom) && c.Path == cpath)
                                                wv.CoreWebView2.CookieManager.DeleteCookie(c);
                                        }
                                    }
                                }
                                catch { }
                                this.BeginInvoke((Action)(() =>
                                {
                                    try { if (!wv.IsDisposed && wv.CoreWebView2 != null) wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/cookies"); } catch { }
                                }));
                                return;
                            }
                            if (msg == "readerback")
                            {
                                this.BeginInvoke((Action)(() =>
                                {
                                    try
                                    {
                                        TabMetadata meta = GetTabMetadata(page);
                                        if (meta != null)
                                        {
                                            if (!string.IsNullOrEmpty(meta.ReaderOriginalUrl))
                                            {
                                                wv.CoreWebView2.Navigate(meta.ReaderOriginalUrl);
                                            }
                                            meta.ReaderActive = false;
                                            meta.ReaderOriginalUrl = null;
                                            meta.ReaderHtml = "";
                                        }
                                        UpdateReaderButtonState();
                                    }
                                    catch { }
                                }));
                                return;
                            }
                        }

                        // Password capture: parse properly & strictly verify origin
                        string decodedMsg = null;
                        try { decodedMsg = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<string>(raw); } catch { }
                        if (string.IsNullOrEmpty(decodedMsg)) decodedMsg = msg;
                        int idx = decodedMsg.IndexOf("BF_PW|", StringComparison.Ordinal);
                        if (idx != 0) return;

                        string json = decodedMsg.Substring(6);
                        var pwDict = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                        if (pwDict == null) return;
                        string site = pwDict.ContainsKey("s") ? Convert.ToString(pwDict["s"]) : "";
                        string user = pwDict.ContainsKey("u") ? Convert.ToString(pwDict["u"]) : "";
                        string pass = pwDict.ContainsKey("p") ? Convert.ToString(pwDict["p"]) : "";
                        if (string.IsNullOrEmpty(pass) || string.IsNullOrEmpty(site)) return;

                        // Origin validation: disallow empty/internal origins and require exact host match
                        Uri srcUri;
                        if (isInternal || string.IsNullOrEmpty(msgSource) || !Uri.TryCreate(msgSource, UriKind.Absolute, out srcUri))
                            return;
                        if (!srcUri.Host.Equals(site, StringComparison.OrdinalIgnoreCase))
                            return;

                        this.BeginInvoke((Action)(() =>
                        {
                            pwAutofillMatches.Clear();
                            if (pwAccountCombo != null) pwAccountCombo.Visible = false;
                            pwCandidate = Tuple.Create(wv, site, user, pass);
                            pwPromptIsAutofill = false;
                            if (pwSaveBtn != null) pwSaveBtn.Text = "Save";
                            if (pwPromptLabel != null)
                                pwPromptLabel.Text = "🔐 Save password for " + site + "?";
                            if (pwPrompt != null) pwPrompt.Visible = true;
                        }));
                    }
                    catch { }
                };

                wv.CoreWebView2.NavigationCompleted += (ws, we) =>
                {
                    try
                    {
                        if (wv.IsDisposed || page.IsDisposed) return;
                        string srcNow = wv.Source != null ? wv.Source.ToString() : "";
                        if (!InternalUrlRouter.IsInternalOrigin(srcNow))
                        {
                            wv.CoreWebView2.ExecuteScriptAsync(
                                "if(!window.__bfDet){window.__bfDet=1;document.addEventListener('submit',function(ev){"
                                + "var f=ev.target||{};var p=f.querySelector?f.querySelector('input[type=password]'):null;"
                                + "if(p&&p.value){var u=f.querySelector?f.querySelector('input[type=email],input[name*=user],input[name*=email],input[name*=login],input[type=text]'):null;"
                                + "window.chrome.webview.postMessage('BF_PW|'+JSON.stringify({s:location.hostname,u:u?u.value:'',p:p.value}));}},true);}");
                            if (we != null && we.IsSuccess)
                                ShowAutofillPromptIfAvailable(wv, page, srcNow);
                        }
                    }
                    catch { }
                };

                wv.CoreWebView2.ProcessFailed += (s, e) =>
                {
                    Log("ProcessFailed: " + e.ProcessFailedKind.ToString());
                    this.BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.BrowserProcessExited)
                            {
                                ShowSoftCommunication("⚠️ Browser engine process exited unexpectedly.");
                                return;
                            }
                            if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
                            {
                                ShowSoftCommunication("⚠️ Page became unresponsive.");
                                return;
                            }
                            TabMetadata meta = GetTabMetadata(page);
                            DateTime now = DateTime.UtcNow;
                            if (meta != null && (now - meta.LastProcessCrash).TotalSeconds < 15)
                            {
                                // Prevent infinite crash-reload loop
                                if (!wv.IsDisposed && wv.CoreWebView2 != null)
                                {
                                    wv.CoreWebView2.NavigateToString(
                                        "<!DOCTYPE html><html><body style='background:#12131a;color:#e2e8f0;font-family:Segoe UI,sans-serif;text-align:center;padding:80px 24px'>"
                                        + "<h2>⚠️ This tab crashed repeatedly</h2>"
                                        + "<p style='color:#94a3b8;margin:12px 0 24px'>Auto-reload paused to prevent a crash loop (" + SimpleHtmlEscape(e.ProcessFailedKind.ToString()) + ").</p>"
                                        + "<button onclick='history.back()' style='padding:10px 18px;border-radius:8px;border:none;background:#3b82f6;color:#fff;cursor:pointer;font-weight:600'>Go Back</button>"
                                        + "</body></html>");
                                }
                                return;
                            }
                            if (meta != null) meta.LastProcessCrash = now;
                            if (!wv.IsDisposed && wv.CoreWebView2 != null) wv.Reload();
                        }
                        catch { }
                    }));
                };

                wv.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;
                    string targetUri = e.Uri ?? "";
                    bool safeScheme = string.IsNullOrEmpty(targetUri) ||
                        targetUri.Equals("about:blank", StringComparison.OrdinalIgnoreCase) ||
                        targetUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        targetUri.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                        targetUri.StartsWith("black://", StringComparison.OrdinalIgnoreCase);
                    if (!safeScheme)
                    {
                        Log("Blocked unsafe NewWindowRequested scheme: " + targetUri);
                        return;
                    }
                    var winDeferral = e.GetDeferral();
                    this.BeginInvoke((Action)(() =>
                    {
                        AddNewTab("Loading...", targetUri, isPrivate, e, winDeferral, containerName);
                    }));
                };

                try
                {
                    var ctrlField = typeof(WebView2).GetField("_coreWebView2Controller", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var controller = ctrlField != null ? (ctrlField.GetValue(wv) as CoreWebView2Controller) : null;
                    if (controller != null)
                    {
                        controller.AcceleratorKeyPressed += (s, e) =>
                        {
                            if (e.KeyEventKind == CoreWebView2KeyEventKind.KeyDown || e.KeyEventKind == CoreWebView2KeyEventKind.SystemKeyDown)
                            {
                                Keys key = (Keys)(e.VirtualKey & 0xFF);
                                bool ctrl = (GetKeyState(0x11) & 0x8000) != 0;
                                bool shift = (GetKeyState(0x10) & 0x8000) != 0;
                                bool alt = (GetKeyState(0x12) & 0x8000) != 0;
                                if (IsBrowserShortcut(ctrl, shift, alt, key, true))
                                {
                                    e.Handled = true;
                                    this.BeginInvoke((Action)(() => ExecuteBrowserShortcut(ctrl, shift, alt, key)));
                                }
                            }
                        };
                    }
                }
                catch { }

                wv.CoreWebView2.PermissionRequested += (s, e) =>
                {
                    try
                    {
                        string permHost = "";
                        Uri permUri;
                        if (Uri.TryCreate(e.Uri, UriKind.Absolute, out permUri)) permHost = permUri.GetLeftPart(UriPartial.Authority);
                        if (InternalUrlRouter.IsInternalOrigin(e.Uri))
                        {
                            e.State = CoreWebView2PermissionState.Allow;
                            return;
                        }
                        string cacheKey = permHost + "|" + e.PermissionKind.ToString();
                        CoreWebView2PermissionState cached;
                        if (permissionDecisions.TryGetValue(cacheKey, out cached))
                        {
                            e.State = cached;
                            return;
                        }
                        var permDeferral = e.GetDeferral();
                        this.BeginInvoke((Action)(() =>
                        {
                            try
                            {
                                string label = string.IsNullOrEmpty(permHost) ? (e.Uri ?? "this website") : permHost;
                                DialogResult dr = MessageBox.Show(
                                    this,
                                    label + " wants permission to use: " + e.PermissionKind.ToString() + ".\n\nAllow this request?",
                                    "Permission Request — Black Browser",
                                    MessageBoxButtons.YesNo,
                                    MessageBoxIcon.Question);
                                CoreWebView2PermissionState decision = dr == DialogResult.Yes
                                    ? CoreWebView2PermissionState.Allow
                                    : CoreWebView2PermissionState.Deny;
                                permissionDecisions[cacheKey] = decision;
                                e.State = decision;
                                if (!isPrivate && !string.IsNullOrEmpty(permHost) && !wv.IsDisposed && wv.CoreWebView2 != null)
                                {
                                    var unusedPermSave = wv.CoreWebView2.Profile.SetPermissionStateAsync(e.PermissionKind, permHost, decision);
                                }
                            }
                            catch
                            {
                                e.State = CoreWebView2PermissionState.Deny;
                            }
                            finally
                            {
                                permDeferral.Complete();
                            }
                        }));
                    }
                    catch { }
                };

                wv.CoreWebView2.ContextMenuRequested += (s, e) =>
                {
                    try
                    {
                        string selText = e.ContextMenuTarget.SelectionText;
                        if (!string.IsNullOrWhiteSpace(selText))
                        {
                            string engineName = SettingsStore.SearchEngine ?? "Google";
                            var searchItem = wv.CoreWebView2.Environment.CreateContextMenuItem(
                                "🔍 Search " + engineName + " for \"" + (selText.Length > 18 ? selText.Substring(0, 15) + "..." : selText) + "\"",
                                null, CoreWebView2ContextMenuItemKind.Command);

                            searchItem.CustomItemSelected += (cs, ce) =>
                            {
                                this.BeginInvoke((Action)(() =>
                                {
                                    AddNewTab("Search", SettingsStore.GetSearchUrl(selText), isPrivate);
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

                        if (SettingsStore.AllowDeveloperTools)
                        {
                            var inspectItem = wv.CoreWebView2.Environment.CreateContextMenuItem(
                                "🛠️ Inspect Element (F12)", null, CoreWebView2ContextMenuItemKind.Command);

                            inspectItem.CustomItemSelected += (cs, ce) =>
                            {
                                try { wv.CoreWebView2.OpenDevToolsWindow(); } catch { }
                            };
                            e.MenuItems.Add(inspectItem);
                        }
                    }
                    catch { }
                };

                // Set by NavigationStarting when the main frame tries to open a
                // .pdf URL. If that navigation collapses into a download, it means
                // the server served the PDF as octet-stream — reroute to the viewer.
                bool pdfNavSeen = false;

                wv.CoreWebView2.DownloadStarting += (s, e) =>
                {
                    try
                    {
                        bool wasPdfNav = pdfNavSeen;
                        pdfNavSeen = false;
                        string downloadUrl = e.DownloadOperation != null ? (e.DownloadOperation.Uri ?? "") : "";
                        string path = e.ResultFilePath;
                        string name = !string.IsNullOrEmpty(path) ? Path.GetFileName(path) : "Download";

                        bool isPdf = IsPdfUrl(downloadUrl) || name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

                        // PDF that couldn't be displayed in-tab (octet-stream/unknown
                        // MIME) → pull it to a temp file and let the native viewer show it.
                        if (isPdf && wasPdfNav && !string.IsNullOrEmpty(downloadUrl))
                        {
                            Log("PDF: rerouting download to viewer: " + downloadUrl);
                            e.Handled = true;
                            if (e.DownloadOperation != null) { try { e.DownloadOperation.Cancel(); } catch { } }
                            ShowSoftCommunication("📄 Opening PDF…");
                            try
                            {
                                string dest = Path.Combine(PdfTempDir, DateTime.Now.Ticks + ".pdf");
                                Directory.CreateDirectory(PdfTempDir);
                                pdfViewerOrigins[dest] = downloadUrl;
                                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                                WebClient wc = new WebClient();
                                wc.DownloadFileCompleted += (ds, de) =>
                                {
                                    try
                                    {
                                        if (de.Error != null)
                                        {
                                            Log("PDF: download failed: " + de.Error.Message);
                                            ShowSoftCommunication("⚠️ PDF download failed");
                                            pdfViewerOrigins.Remove(dest);
                                            return;
                                        }
                                        Log("PDF: download ok, navigating to " + dest);
                                        this.BeginInvoke((Action)(() =>
                                        {
                                            try { if (!wv.IsDisposed && wv.CoreWebView2 != null) wv.CoreWebView2.Navigate(dest); } catch (Exception ex2) { Log("PDF: navigate failed: " + ex2.Message); }
                                        }));
                                    }
                                    finally
                                    {
                                        try { wc.Dispose(); } catch { }
                                    }
                                };
                                wc.DownloadFileAsync(new Uri(downloadUrl), dest);
                                return;
                            }
                            catch (Exception ex)
                            {
                                Log("PDF open failed: " + ex.Message);
                                ShowSoftCommunication("⚠️ PDF open failed");
                            }
                        }

                        e.Handled = false;
                        CoreWebView2DownloadOperation dlOp = e.DownloadOperation;
                        long initialSize = (dlOp != null && dlOp.TotalBytesToReceive.HasValue) ? (long)dlOp.TotalBytesToReceive.Value : 0;
                        DownloadsManager.AddDownload(name, path ?? "", initialSize);
                        ShowSoftCommunication("📥 Download Started: " + name);

                        ShelfRow row = CreateShelfRow(name, dlOp);
                        if (dlOp != null && row != null)
                        {
                            shelfRows[dlOp] = row;
                            ShowShelf();
                        }

                        if (dlOp != null)
                        {
                            dlOp.BytesReceivedChanged += (ds, de) =>
                            {
                                try { UpdateShelfRow(row, dlOp); } catch { }
                            };
                            dlOp.StateChanged += (ds, de) =>
                            {
                                try
                                {
                                    UpdateShelfRow(row, dlOp);
                                    if (dlOp.State == CoreWebView2DownloadState.Completed)
                                    {
                                        long finalBytes = dlOp.BytesReceived > 0 ? dlOp.BytesReceived : initialSize;
                                        if (finalBytes <= 0 && !string.IsNullOrEmpty(path) && File.Exists(path))
                                        {
                                            try { finalBytes = new FileInfo(path).Length; } catch { }
                                        }
                                        DownloadsManager.UpdateDownloadSize(path, finalBytes);
                                        ShowSoftCommunication("✅ Download Complete: " + name);
                                        RemoveShelfRow(dlOp, true);
                                    }
                                    else if (dlOp.State == CoreWebView2DownloadState.Interrupted)
                                    {
                                        Log("Download Interrupted: " + name + " Reason: " + dlOp.InterruptReason.ToString());
                                        ShowSoftCommunication("⚠️ Download Interrupted: " + name + " (" + dlOp.InterruptReason.ToString() + ")");
                                        RemoveShelfRow(dlOp, false);
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
                    try { this.BeginInvoke((Action)(() => HandleElementFullscreenChanged(wv))); }
                    catch { }
                };

                var unusedShieldTask = AdShieldEngine.AttachAdShield(wv, (blockedHost) =>
                {
                    totalBlockedAds++;
                    RecordTabBlockedDomain(page, blockedHost);
                    UpdateShieldButtonText();
                });

                wv.CoreWebView2.NavigationStarting += (s, e) =>
                {
                    if (wv.IsDisposed || page.IsDisposed) return;

                    // HTTPS-First: upgrade plain http (except local hosts) when enabled
                    if (SettingsStore.HttpsFirstMode &&
                        e.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !e.Uri.StartsWith("http://localhost", StringComparison.OrdinalIgnoreCase) &&
                        !e.Uri.StartsWith("http://127.0.0.1", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        wv.CoreWebView2.Navigate("https://" + e.Uri.Substring("http://".Length));
                        return;
                    }

                    pdfNavSeen = IsPdfUrl(e.Uri);

                    string virtualTarget = InternalUrlRouter.ResolveToVirtualUrl(e.Uri);
                    if (!string.IsNullOrEmpty(virtualTarget))
                    {
                        e.Cancel = true;
                        wv.CoreWebView2.Navigate(virtualTarget);
                        return;
                    }

                    string curSrc = wv.Source != null ? wv.Source.ToString() : "";
                    bool isInternalSource = IsInternalOrigin(curSrc);

                    if (e.Uri.StartsWith("black://load-extension", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(async () =>
                        {
                            using (var fbd = new FolderBrowserDialog())
                            {
                                fbd.Description = "Select unpacked extension directory containing manifest.json";
                                if (fbd.ShowDialog(this) == DialogResult.OK)
                                {
                                    string err;
                                    if (ExtensionManager.AddUnpackedExtension(fbd.SelectedPath, out err))
                                    {
                                        bool hotLoaded = await TryHotLoadExtensionAsync(wv, fbd.SelectedPath);
                                        ShowSoftCommunication(hotLoaded
                                            ? "🧩 Extension Loaded & Hot-Activated!"
                                            : "🧩 Extension Loaded Successfully! Restart browser to activate hooks.");
                                    }
                                    else
                                    {
                                        ShowSoftCommunication("⚠️ Failed to load extension: " + err);
                                    }
                                }
                            }
                            if (!wv.IsDisposed && wv.CoreWebView2 != null)
                                wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/extensions");
                        }));
                        return;
                    }

                    if (e.Uri.StartsWith("black://toggle-extension", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        string id = GetQueryParam(e.Uri, "id");
                        bool val = GetQueryParam(e.Uri, "val").Equals("true", StringComparison.OrdinalIgnoreCase);
                        if (!string.IsNullOrEmpty(id))
                        {
                            ExtensionManager.ToggleExtension(id, val);
                            ExtensionItem toggledItem = ExtensionManager.GetInstalledExtensions().Find(x => x.Id == id);
                            if (toggledItem != null)
                            {
                                var unusedExtSync = SyncRuntimeExtensionAsync(wv, toggledItem, false);
                            }
                        }
                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/extensions");
                        return;
                    }

                    if (e.Uri.StartsWith("black://remove-extension", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        string id = GetQueryParam(e.Uri, "id");
                        if (!string.IsNullOrEmpty(id))
                        {
                            ExtensionItem removedItem = ExtensionManager.GetInstalledExtensions().Find(x => x.Id == id);
                            ExtensionManager.RemoveExtension(id);
                            if (removedItem != null)
                            {
                                var unusedExtRm = SyncRuntimeExtensionAsync(wv, removedItem, true);
                            }
                        }
                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/extensions");
                        return;
                    }

                    if (e.Uri.StartsWith("black://remove-password", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        string site = GetQueryParam(e.Uri, "site");
                        string user = GetQueryParam(e.Uri, "user");
                        if (!string.IsNullOrEmpty(site))
                        {
                            if (e.Uri.IndexOf("user=", StringComparison.OrdinalIgnoreCase) >= 0)
                                PasswordManager.RemovePassword(site, user);
                            else
                                PasswordManager.RemovePassword(site);
                        }
                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/passwords");
                        return;
                    }

                    if (e.Uri.StartsWith("black://remove-bookmark", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        string bmUrl = GetQueryParam(e.Uri, "url");
                        if (!string.IsNullOrEmpty(bmUrl)) BookmarksManager.RemoveBookmark(bmUrl);
                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/bookmarks");
                        return;
                    }

                    if (e.Uri.Equals("black://export-bookmarks", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(() =>
                        {
                            using (var sfd = new SaveFileDialog())
                            {
                                sfd.Filter = "HTML Files (*.html)|*.html|All Files (*.*)|*.*";
                                sfd.FileName = "black_browser_bookmarks.html";
                                if (sfd.ShowDialog(this) == DialogResult.OK)
                                {
                                    BookmarksManager.ExportHtml(sfd.FileName);
                                    ShowSoftCommunication("📤 Bookmarks exported successfully to " + Path.GetFileName(sfd.FileName));
                                }
                            }
                        }));
                        return;
                    }

                    if (e.Uri.Equals("black://import-bookmarks", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(() =>
                        {
                            using (var ofd = new OpenFileDialog())
                            {
                                ofd.Filter = "HTML Files (*.html)|*.html|All Files (*.*)|*.*";
                                if (ofd.ShowDialog(this) == DialogResult.OK)
                                {
                                    int imported = BookmarksManager.ImportHtml(ofd.FileName);
                                    ShowSoftCommunication("📥 Imported " + imported + " bookmarks successfully!");
                                    if (!wv.IsDisposed && wv.CoreWebView2 != null)
                                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/bookmarks");
                                }
                            }
                        }));
                        return;
                    }

                    if (e.Uri.StartsWith("black://remove-history", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        string histUrl = GetQueryParam(e.Uri, "url");
                        if (!string.IsNullOrEmpty(histUrl)) HistoryManager.RemoveHistoryItem(histUrl);
                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/history");
                        return;
                    }

                    if (e.Uri.Equals("black://clear-history", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        HistoryManager.ClearHistory();
                        ShowSoftCommunication("🗑️ Browsing history cleared.");
                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/history");
                        return;
                    }

                    if (e.Uri.Equals("black://default-browser", StringComparison.OrdinalIgnoreCase) || e.Uri.Equals("black://set-default", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        SetAsDefaultBrowser();
                        return;
                    }

                    if (e.Uri.Equals("black://optimize-memory", StringComparison.OrdinalIgnoreCase) || e.Uri.Equals("black://purge-ram", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        MemoryTrimmer.TrimProcessMemory();
                        long ramMB = MemoryTrimmer.GetWorkingSetMB();
                        ShowSoftCommunication("⚡ Memory Purged: " + ramMB + "MB active working set");
                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/home");
                        return;
                    }

                    if (e.Uri.StartsWith("black://adddial", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        PromptAddDial(wv, isDarkMode);
                        return;
                    }

                    if (e.Uri.StartsWith("black://setsearch", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        string engine = GetQueryParam(e.Uri, "engine").Trim().ToLowerInvariant();
                        if (!string.IsNullOrEmpty(engine))
                            SettingsStore.SearchEngine = engine;
                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/home");
                        return;
                    }

                    if (e.Uri.StartsWith("black://removedial", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        string dialUrl = GetQueryParam(e.Uri, "url");
                        if (!string.IsNullOrEmpty(dialUrl))
                            CustomDialsManager.RemoveCustomDial(dialUrl);
                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/home");
                        return;
                    }

                    if (e.Uri.Equals("black://new-private", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(() => AddNewTab("Private Tab", "black://home", isPrivate: true)));
                        return;
                    }

                    if (e.Uri.Equals("black://reopen-tab", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(() => ReopenLastClosedTab()));
                        return;
                    }

                    if (e.Uri.Equals("black://toggle-sidebar", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(() => ToggleSidebar()));
                        return;
                    }

                    if (e.Uri.Equals("black://toggle-theme", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(() => ToggleTheme()));
                        return;
                    }

                    if (e.Uri.Equals("black://cycle-eyecare", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(() => CycleEyeCareMode()));
                        return;
                    }

                    if (e.Uri.Equals("black://toggle-adshield", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        SettingsStore.AdShieldEnabled = !SettingsStore.AdShieldEnabled;
                        ShowSoftCommunication(SettingsStore.AdShieldEnabled
                            ? "🛡️ AdShield Global Protection: Enabled"
                            : "⏸️ AdShield Global Protection: Paused");
                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/home");
                        return;
                    }

                    if (e.Uri.Equals("black://toggle-https", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        SettingsStore.HttpsFirstMode = !SettingsStore.HttpsFirstMode;
                        ShowSoftCommunication(SettingsStore.HttpsFirstMode
                            ? "🔒 HTTPS-First Upgrade: Enabled"
                            : "🔓 HTTPS-First Upgrade: Off");
                        wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/home");
                        return;
                    }

                    if (e.Uri.StartsWith("black://setbg", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        string bgVal = (GetQueryParam(e.Uri, "bg") ?? "").Trim().ToLowerInvariant();
                        if (bgVal == "aurora" || bgVal == "cyberpunk" || bgVal == "nebula" || bgVal == "obsidian" || bgVal == "light")
                        {
                            SettingsStore.SpeedDialBg = bgVal;
                            ShowSoftCommunication("🎨 New Tab Theme: " + bgVal);
                            ReloadAllSpeedDials();
                        }
                        return;
                    }

                    if (e.Uri.Equals("black://qr", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(() => ShowCurrentPageQrCode()));
                        return;
                    }

                    if (e.Uri.Equals("black://screenshot", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(() => TakeScreenshot()));
                        return;
                    }

                    if (e.Uri.Equals("black://settings", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(() => OpenSettingsDialog(0)));
                        return;
                    }

                    if (e.Uri.Equals("black://check-updates", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        if (!isInternalSource) return;
                        this.BeginInvoke((Action)(() => CheckForUpdatesUserManual()));
                        return;
                    }

                    if (SettingsStore.StripTrackingParams &&
                        (e.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                         e.Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
                        !InternalUrlRouter.IsInternalOrigin(e.Uri))
                    {
                        string cleaned = AdShieldEngine.StripTrackingParameters(e.Uri);
                        if (!string.IsNullOrEmpty(cleaned) && !string.Equals(cleaned, e.Uri, StringComparison.Ordinal))
                        {
                            e.Cancel = true;
                            wv.CoreWebView2.Navigate(cleaned);
                            return;
                        }
                    }

                    if (tabControl.SelectedTab == page)
                    {
                        urlBar.Text = InternalUrlRouter.GetDisplayUrl(e.Uri);
                    }
                };

                wv.CoreWebView2.FaviconChanged += (s, e) =>
                {
                    try
                    {
                        if (wv.IsDisposed || page.IsDisposed) return;
                        if (tabControl.SelectedTab == page)
                        {
                            this.BeginInvoke((Action)(() => FetchAndSetTabFavicon(wv, page)));
                        }
                    }
                    catch { }
                };

                wv.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    if (wv.IsDisposed || page.IsDisposed) return;
                    string uriStr = wv.Source != null ? wv.Source.ToString() : "";
                    string originalUrl = null;
                    string internalDefaultTitle = InternalUrlRouter.GetDefaultTitle(uriStr);

                    if (tabControl.SelectedTab == page)
                    {
                        if (pdfViewerOrigins.TryGetValue(uriStr, out originalUrl))
                        {
                            urlBar.Text = originalUrl;
                            page.Text = TruncateTitle(Path.GetFileName(new Uri(originalUrl).AbsolutePath) ?? "PDF");
                        }
                        else
                        {
                            urlBar.Text = InternalUrlRouter.GetDisplayUrl(uriStr);
                            string docTitle = wv.CoreWebView2.DocumentTitle;
                            string pageName = !string.IsNullOrEmpty(internalDefaultTitle)
                                ? internalDefaultTitle
                                : (string.IsNullOrEmpty(docTitle) ? "New Tab" : TruncateTitle(docTitle));
                            page.Text = isPrivate ? "🕵️ " + pageName : pageName;
                        }
                        tabControl.Invalidate();
                        UpdateNavButtons();
                    }
                    else
                    {
                        string docTitle = wv.CoreWebView2.DocumentTitle;
                        string pageName = !string.IsNullOrEmpty(internalDefaultTitle)
                            ? internalDefaultTitle
                            : (string.IsNullOrEmpty(docTitle) ? "New Tab" : TruncateTitle(docTitle));
                        page.Text = isPrivate ? "🕵️ " + pageName : pageName;
                        tabControl.Invalidate();
                    }

                    if (!isPrivate && uriStr.Length > 0 && !InternalUrlRouter.IsInternalOrigin(uriStr) && !pdfViewerOrigins.ContainsKey(uriStr))
                    {
                        HistoryManager.AddVisit(wv.CoreWebView2.DocumentTitle, uriStr);
                    }
                    if (!isPrivate) SaveCurrentSession();
                };

                wv.CoreWebView2.SourceChanged += (s, e) =>
                {
                    if (wv.IsDisposed || page.IsDisposed) return;
                    if (tabControl.SelectedTab == page)
                    {
                        string uriStr = wv.Source != null ? wv.Source.ToString() : "";
                        string originalUrl;
                        if (pdfViewerOrigins.TryGetValue(uriStr, out originalUrl))
                        {
                            urlBar.Text = originalUrl;
                        }
                        else
                        {
                            urlBar.Text = InternalUrlRouter.GetDisplayUrl(uriStr);
                        }
                    }
                };

                if (newWindowArgs != null)
                {
                    newWindowArgs.NewWindow = wv.CoreWebView2;
                    if (deferral != null) { deferral.Complete(); deferralCompleted = true; }
                    return;
                }

                string initialVirtual = InternalUrlRouter.ResolveToVirtualUrl(url);
                if (!string.IsNullOrEmpty(initialVirtual))
                {
                    wv.CoreWebView2.Navigate(initialVirtual);
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
            finally
            {
                if (deferral != null && !deferralCompleted)
                {
                    try { deferral.Complete(); } catch { }
                }
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
            HideFindBar();
            if (pwPrompt != null) { pwPrompt.Visible = false; pwCandidate = null; pwPromptIsAutofill = false; }
            pwAutofillMatches.Clear();
            if (pwAccountCombo != null) pwAccountCombo.Visible = false;

            TabPage activePage = tabControl.SelectedTab;
            WebView2 wv = GetCurrentWebView();
            if (wv != null && wv.CoreWebView2 != null && wv.Source != null)
            {
                string uriStr = wv.Source.ToString();
                urlBar.Text = InternalUrlRouter.GetDisplayUrl(uriStr);
                UpdateNavButtons();

                bool isBkd = BookmarksManager.IsBookmarked(uriStr);
                if (isBkd)
                {
                    starBtn.IconType = VectorIcon.StarFilled;
                    starBtn.ForeColor = Color.FromArgb(245, 158, 11);
                }
                else
                {
                    starBtn.IconType = VectorIcon.Star;
                    starBtn.ForeColor = isDarkMode ? Color.FromArgb(200, 205, 220) : Color.FromArgb(95, 99, 104);
                }

                FetchAndSetTabFavicon(wv, activePage);
            }
            else
            {
                ClearFavicon();
                UpdateNavButtons();
            }

            UpdateReaderButtonState();
            lruTabManager.Touch(activePage);
            PositionOmnibox();
            this.BeginInvoke((Action)(() => UpdateNewTabButtonPosition()));
            SaveCurrentSession();
        }

        private void ClearFavicon()
        {
            if (faviconBox == null) return;
            faviconBox.Visible = false;
            if (faviconBox.Image != null)
            {
                faviconBox.Image.Dispose();
                faviconBox.Image = null;
            }
        }

        private async void FetchAndSetTabFavicon(WebView2 wv, TabPage page)
        {
            if (faviconBox == null) return;
            try
            {
                if (wv == null || wv.IsDisposed || wv.CoreWebView2 == null || page == null || page.IsDisposed || tabControl.SelectedTab != page)
                    return;

                string uriStr = wv.Source != null ? wv.Source.ToString() : "";
                if (string.IsNullOrEmpty(uriStr) || InternalUrlRouter.IsInternalOrigin(uriStr))
                {
                    ClearFavicon();
                    return;
                }

                using (Stream st = await wv.CoreWebView2.GetFaviconAsync(CoreWebView2FaviconImageFormat.Png))
                {
                    if (this.IsDisposed || faviconBox == null || tabControl.SelectedTab != page) return;
                    if (st != null && st.Length > 0)
                    {
                        Image img = Image.FromStream(st);
                        Image old = faviconBox.Image;
                        faviconBox.Image = img;
                        if (old != null) old.Dispose();
                        faviconBox.Visible = true;
                    }
                    else
                    {
                        ClearFavicon();
                    }
                }
            }
            catch
            {
                if (tabControl.SelectedTab == page) ClearFavicon();
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
            reloadBtn.Enabled = true;
            homeBtn.Enabled = true;
        }

        private void NavigateCurrentTab(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return;

            if (input.StartsWith("math:", StringComparison.Ordinal))
            {
                string val = input.Substring("math:".Length);
                try { Clipboard.SetText(val); } catch { }
                urlBar.Text = "= " + val;
                ShowSoftCommunication("🧮 Copied calculation result: " + val);
                return;
            }

            string quickMath;
            if (TryEvaluateQuickMath(input, out quickMath))
            {
                try { Clipboard.SetText(quickMath); } catch { }
                urlBar.Text = "= " + quickMath;
                ShowSoftCommunication("🧮 Copied calculation result: " + quickMath);
                return;
            }

            if (input.StartsWith("black://switch-tab", StringComparison.OrdinalIgnoreCase))
            {
                string idxStr = GetQueryParam(input, "index");
                int tabIdx;
                if (int.TryParse(idxStr, out tabIdx) && tabIdx >= 0 && tabIdx < tabControl.TabPages.Count)
                {
                    tabControl.SelectedIndex = tabIdx;
                }
                return;
            }

            WebView2 wv = GetCurrentWebView();
            if (wv != null && wv.CoreWebView2 != null)
            {
                if (string.Equals(input, "black://default-browser", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(input, "black://set-default", StringComparison.OrdinalIgnoreCase))
                {
                    SetAsDefaultBrowser();
                    return;
                }

                string virtualTarget = InternalUrlRouter.ResolveToVirtualUrl(input);
                if (!string.IsNullOrEmpty(virtualTarget))
                {
                    wv.CoreWebView2.Navigate(virtualTarget);
                    urlBar.Text = InternalUrlRouter.GetDisplayUrl(virtualTarget);
                    string defTitle = InternalUrlRouter.GetDefaultTitle(virtualTarget);
                    if (tabControl.SelectedTab != null && !string.IsNullOrEmpty(defTitle))
                        tabControl.SelectedTab.Text = defTitle;
                    return;
                }

                string target = FormatUrl(input);
                wv.CoreWebView2.Navigate(target);
            }
        }

        private void PromptAddDial(WebView2 wv, bool dark)
        {
            using (var dlg = new Form())
            {
                dlg.Text = "Add Shortcut";
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ClientSize = new System.Drawing.Size(380, 150);
                dlg.BackColor = dark ? System.Drawing.Color.FromArgb(32, 33, 36) : System.Drawing.Color.White;

                var lblTitle = new Label { Text = "Shortcut name:", Left = 15, Top = 18, Width = 90, ForeColor = dark ? System.Drawing.Color.FromArgb(221, 227, 240) : System.Drawing.Color.FromArgb(32, 33, 36), BackColor = System.Drawing.Color.Transparent };
                var txtTitle = new TextBox { Left = 110, Top = 15, Width = 250 };
                var lblUrl = new Label { Text = "Website URL:", Left = 15, Top = 58, Width = 90, ForeColor = dark ? System.Drawing.Color.FromArgb(221, 227, 240) : System.Drawing.Color.FromArgb(32, 33, 36), BackColor = System.Drawing.Color.Transparent };
                var txtUrl = new TextBox { Left = 110, Top = 55, Width = 250 };

                var btnOk = new Button { Text = "Add", DialogResult = DialogResult.OK, Left = 195, Top = 102, Width = 80 };
                var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 280, Top = 102, Width = 80 };

                dlg.Controls.Add(lblTitle);
                dlg.Controls.Add(txtTitle);
                dlg.Controls.Add(lblUrl);
                dlg.Controls.Add(txtUrl);
                dlg.Controls.Add(btnOk);
                dlg.Controls.Add(btnCancel);
                dlg.AcceptButton = btnOk;
                dlg.CancelButton = btnCancel;
                dlg.Shown += (s, ev) => txtTitle.Focus();

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string title = txtTitle.Text.Trim();
                    string url = txtUrl.Text.Trim();
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        CustomDialsManager.AddCustomDial(title, url);
                        if (wv != null && wv.CoreWebView2 != null)
                            wv.CoreWebView2.Navigate(InternalUrlRouter.VirtualOrigin + "/home");
                    }
                }
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

            return SettingsStore.GetSearchUrl(input);
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
                    trayIcon.Text = "Black Browser (Glassmorphic Edition)";
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
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = (SettingsStore.WindowState == FormWindowState.Maximized)
                    ? FormWindowState.Maximized
                    : FormWindowState.Normal;
            }
            this.BringToFront();
            this.Activate();
            Program.ForceForegroundWindow(this.Handle);
            ResumeActiveWebView();
            if (eyeCareOverlay != null) eyeCareOverlay.SyncToOwner(this);
        }

        private void SaveCurrentSession()
        {
            if (restoringSession) return;
            try
            {
                if (tabControl == null || tabControl.IsDisposed) return;
                List<SessionTabEntry> entries = new List<SessionTabEntry>();
                int activeIdx = 0;
                for (int i = 0; i < tabControl.TabPages.Count; i++)
                {
                    TabPage page = tabControl.TabPages[i];
                    if (page.Tag != null && (bool)page.Tag == true) continue; // never persist private tabs
                    WebView2 wv = GetWebView(page);
                    string raw = (wv != null && wv.Source != null) ? wv.Source.ToString() : "";
                    string disp = InternalUrlRouter.GetDisplayUrl(raw);
                    string urlToSave = !string.IsNullOrEmpty(disp)
                        ? disp
                        : (InternalUrlRouter.IsInternalOrigin(raw) || string.IsNullOrEmpty(raw) || raw == "about:blank" ? "black://home" : raw);
                    if (page == tabControl.SelectedTab) activeIdx = entries.Count;
                    entries.Add(new SessionTabEntry
                    {
                        Title = page.Text,
                        Url = urlToSave,
                        Pinned = pinnedTabs.Contains(page)
                    });
                }
                SessionManager.SaveSession(entries, activeIdx);
            }
            catch { }
        }

        private void ExitApp()
        {
            SaveCurrentSession();
            if (trayIcon != null) trayIcon.Visible = false;
            Application.Exit();
        }

        private bool IsBrowserShortcut(bool ctrl, bool shift, bool alt, Keys keyCode, bool fromWebView)
        {
            if (ctrl && shift)
            {
                if (keyCode == Keys.T || keyCode == Keys.K || keyCode == Keys.A || keyCode == Keys.P ||
                    keyCode == Keys.I || keyCode == Keys.N || keyCode == Keys.E || keyCode == Keys.D ||
                    keyCode == Keys.Delete || keyCode == Keys.B || keyCode == Keys.R || keyCode == Keys.X ||
                    keyCode == Keys.Tab)
                    return true;
            }
            if (ctrl && !alt)
            {
                if (keyCode == Keys.T || keyCode == Keys.W || keyCode == Keys.Oemplus || keyCode == Keys.Add ||
                    keyCode == Keys.OemMinus || keyCode == Keys.Subtract || keyCode == Keys.D0 || keyCode == Keys.NumPad0 ||
                    keyCode == Keys.U || keyCode == Keys.P || keyCode == Keys.F || keyCode == Keys.G ||
                    keyCode == Keys.Tab || (keyCode >= Keys.D1 && keyCode <= Keys.D9) ||
                    keyCode == Keys.L || keyCode == Keys.E || keyCode == Keys.K || keyCode == Keys.D ||
                    keyCode == Keys.N || keyCode == Keys.Oemcomma || keyCode == Keys.R || keyCode == Keys.H || keyCode == Keys.J)
                    return true;
            }
            if (alt && !ctrl)
            {
                if (keyCode == Keys.D || keyCode == Keys.Home || keyCode == Keys.Left || keyCode == Keys.Right)
                    return true;
            }
            if (!ctrl && !alt)
            {
                if (keyCode == Keys.F5 || keyCode == Keys.F6 || keyCode == Keys.F11 || keyCode == Keys.F12)
                    return true;
                if (keyCode == Keys.Escape && (!fromWebView || isFullscreen))
                    return true;
            }
            return false;
        }

        private void OnFormKeyDown(object sender, KeyEventArgs e)
        {
            if (IsBrowserShortcut(e.Control, e.Shift, e.Alt, e.KeyCode, false))
            {
                e.SuppressKeyPress = true;
                ExecuteBrowserShortcut(e.Control, e.Shift, e.Alt, e.KeyCode);
            }
        }

        private void ExecuteBrowserShortcut(bool ctrl, bool shift, bool alt, Keys keyCode)
        {
            // === Ctrl+Shift combos (check these FIRST to avoid conflicts) ===
            if (ctrl && shift && keyCode == Keys.T)
            {
                ReopenLastClosedTab();
            }
            else if (ctrl && shift && keyCode == Keys.K)
            {
                DuplicateCurrentTab();
            }
            else if (ctrl && shift && keyCode == Keys.A)
            {
                ShowTabSearchPopup();
            }
            else if (ctrl && shift && keyCode == Keys.P)
            {
                AddNewTab("Private Tab", "black://home", isPrivate: true);
            }
            else if (ctrl && shift && keyCode == Keys.I)
            {
                if (!SettingsStore.AllowDeveloperTools)
                {
                    ShowSoftCommunication("🔒 Developer tools are disabled in this build.");
                    return;
                }
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.CoreWebView2 != null)
                {
                    try { wv.CoreWebView2.OpenDevToolsWindow(); } catch { }
                }
            }
            else if (ctrl && shift && keyCode == Keys.N)
            {
                OpenSettingsDialog(3);
            }
            else if (ctrl && shift && keyCode == Keys.E)
            {
                CycleEyeCareMode();
            }
            else if (ctrl && shift && keyCode == Keys.D)
            {
                ToggleTheme();
            }
            else if (ctrl && shift && keyCode == Keys.Delete)
            {
                ShowClearBrowsingDataDialog();
            }
            else if (ctrl && shift && keyCode == Keys.B)
            {
                ToggleSidebar();
            }
            else if (ctrl && shift && keyCode == Keys.R)
            {
                ToggleReaderMode();
            }
            else if (ctrl && shift && keyCode == Keys.X)
            {
                NavigateCurrentTab("black://extensions");
            }
            // === Ctrl combos ===
            else if (ctrl && keyCode == Keys.T)
            {
                AddNewTab("New Tab", "black://home");
            }
            else if (ctrl && keyCode == Keys.W)
            {
                CloseCurrentTab();
            }
            else if (ctrl && (keyCode == Keys.Oemplus || keyCode == Keys.Add))
            {
                AdjustZoom(0.1f);
            }
            else if (ctrl && (keyCode == Keys.OemMinus || keyCode == Keys.Subtract))
            {
                AdjustZoom(-0.1f);
            }
            else if (ctrl && (keyCode == Keys.D0 || keyCode == Keys.NumPad0))
            {
                AdjustZoom(0f, true);
            }
            else if (ctrl && keyCode == Keys.U)
            {
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.Source != null)
                {
                    string currUrl = wv.Source.ToString();
                    if (!string.IsNullOrEmpty(currUrl) && !currUrl.StartsWith("view-source:"))
                        AddNewTab("Source", "view-source:" + currUrl);
                }
            }
            else if (ctrl && keyCode == Keys.P)
            {
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.CoreWebView2 != null)
                {
                    try { wv.CoreWebView2.ShowPrintUI(CoreWebView2PrintDialogKind.Browser); } catch { }
                }
            }
            else if (ctrl && keyCode == Keys.F)
            {
                WebView2 wv = GetCurrentWebView();
                if (wv != null) ShowFindBar(wv);
            }
            else if (ctrl && keyCode == Keys.G)
            {
                RunFind(false);
            }
            else if (ctrl && keyCode == Keys.Tab)
            {
                if (tabControl.TabPages.Count > 1)
                {
                    int nextIdx = shift
                        ? (tabControl.SelectedIndex - 1 + tabControl.TabPages.Count) % tabControl.TabPages.Count
                        : (tabControl.SelectedIndex + 1) % tabControl.TabPages.Count;
                    tabControl.SelectedIndex = nextIdx;
                }
            }
            else if (ctrl && keyCode >= Keys.D1 && keyCode <= Keys.D8)
            {
                int targetIdx = keyCode - Keys.D1;
                if (targetIdx < tabControl.TabPages.Count)
                    tabControl.SelectedIndex = targetIdx;
            }
            else if (ctrl && keyCode == Keys.D9)
            {
                if (tabControl.TabPages.Count > 0)
                    tabControl.SelectedIndex = tabControl.TabPages.Count - 1;
            }
            else if (ctrl && keyCode == Keys.L)
            {
                urlBar.Focus();
                urlBar.SelectAll();
            }
            else if (ctrl && (keyCode == Keys.E || keyCode == Keys.K))
            {
                urlBar.Focus();
                urlBar.Clear();
            }
            else if (ctrl && keyCode == Keys.D)
            {
                ToggleCurrentTabBookmark();
            }
            else if (ctrl && keyCode == Keys.N)
            {
                try { Process.Start(Application.ExecutablePath, "--newwindow"); } catch { }
            }
            else if (ctrl && keyCode == Keys.Oemcomma)
            {
                OpenSettingsDialog(0);
            }
            else if (ctrl && keyCode == Keys.R || keyCode == Keys.F5)
            {
                ReloadCurrentTab();
            }
            else if (ctrl && keyCode == Keys.H)
            {
                NavigateCurrentTab("black://history");
            }
            else if (ctrl && keyCode == Keys.J)
            {
                NavigateCurrentTab("black://downloads");
            }
            // === Alt combos ===
            else if (alt && keyCode == Keys.D)
            {
                urlBar.Focus();
                urlBar.SelectAll();
            }
            else if (alt && keyCode == Keys.Home)
            {
                NavigateCurrentTab(string.IsNullOrWhiteSpace(SettingsStore.HomePage) ? "black://home" : SettingsStore.HomePage);
            }
            else if (alt && keyCode == Keys.Left)
            {
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.CanGoBack) wv.GoBack();
            }
            else if (alt && keyCode == Keys.Right)
            {
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.CanGoForward) wv.GoForward();
            }
            // === Function keys ===
            else if (keyCode == Keys.F6)
            {
                urlBar.Focus();
                urlBar.SelectAll();
            }
            else if (keyCode == Keys.F12)
            {
                if (!SettingsStore.AllowDeveloperTools)
                {
                    ShowSoftCommunication("🔒 Developer tools are disabled in this build.");
                    return;
                }
                WebView2 wv = GetCurrentWebView();
                if (wv != null && wv.CoreWebView2 != null)
                {
                    try { wv.CoreWebView2.OpenDevToolsWindow(); } catch { }
                }
            }
            else if (keyCode == Keys.F11)
            {
                ToggleFullscreen();
            }
            // === Escape ===
            else if (keyCode == Keys.Escape)
            {
                if (isFullscreen)
                {
                    ToggleFullscreen();
                }
                else
                {
                    HideAutocomplete();
                    WebView2 escWv = GetCurrentWebView();
                    if (escWv != null && escWv.CoreWebView2 != null)
                    {
                        try { escWv.CoreWebView2.Stop(); } catch { }
                    }
                }
            }
        }

        // ===== TAB SEARCH POPUP (Ctrl+Shift+A) =====

        private void ShowTabSearchPopup()
        {
            using (Form tabSearchForm = new Form())
            {
                tabSearchForm.Text = "Search Tabs";
                tabSearchForm.StartPosition = FormStartPosition.CenterParent;
                tabSearchForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                tabSearchForm.MaximizeBox = false;
                tabSearchForm.MinimizeBox = false;
                tabSearchForm.ClientSize = new Size(480, 400);
                tabSearchForm.BackColor = Color.FromArgb(24, 24, 30);

                Label header = new Label();
                header.Text = "🔍 Search Open Tabs (" + tabControl.TabPages.Count + " tabs)";
                header.ForeColor = Color.FromArgb(220, 225, 240);
                header.Font = new Font("Segoe UI Variable Display", 12f, FontStyle.Bold);
                header.Location = new Point(16, 12);
                header.AutoSize = true;
                tabSearchForm.Controls.Add(header);

                TextBox searchBox = new TextBox();
                searchBox.Location = new Point(16, 44);
                searchBox.Width = 448;
                searchBox.Height = 30;
                searchBox.Font = new Font("Segoe UI Variable Display", 11f);
                searchBox.BackColor = Color.FromArgb(40, 40, 50);
                searchBox.ForeColor = Color.FromArgb(240, 240, 245);
                searchBox.BorderStyle = BorderStyle.FixedSingle;
                tabSearchForm.Controls.Add(searchBox);

                ListBox tabList = new ListBox();
                tabList.Location = new Point(16, 82);
                tabList.Size = new Size(448, 300);
                tabList.Font = new Font("Segoe UI Variable Display", 10f);
                tabList.BackColor = Color.FromArgb(32, 32, 40);
                tabList.ForeColor = Color.FromArgb(230, 232, 240);
                tabList.BorderStyle = BorderStyle.None;
                tabSearchForm.Controls.Add(tabList);

                // Populate tab list
                Action<string> refreshList = (query) =>
                {
                    tabList.Items.Clear();
                    for (int i = 0; i < tabControl.TabPages.Count; i++)
                    {
                        TabPage tp = tabControl.TabPages[i];
                        string tabTitle = tp.Text;
                        WebView2 wv = GetWebView(tp);
                        string rawUrl = (wv != null && wv.Source != null) ? wv.Source.ToString() : "";
                        string tabUrl = InternalUrlRouter.IsInternalOrigin(rawUrl)
                            ? (string.IsNullOrEmpty(InternalUrlRouter.GetDisplayUrl(rawUrl)) ? "black://home" : InternalUrlRouter.GetDisplayUrl(rawUrl))
                            : rawUrl;
                        string display = (i + 1) + ". " + tabTitle + " — " + tabUrl;

                        if (string.IsNullOrWhiteSpace(query) ||
                            tabTitle.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                            tabUrl.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            tabList.Items.Add(display);
                        }
                    }
                    if (tabList.Items.Count > 0) tabList.SelectedIndex = 0;
                };

                refreshList("");

                searchBox.TextChanged += (s, ev) => refreshList(searchBox.Text);

                // Enter or double-click to switch to tab
                Action switchToSelected = () =>
                {
                    if (tabList.SelectedIndex >= 0)
                    {
                        string selected = tabList.SelectedItem.ToString();
                        int dotIdx = selected.IndexOf('.');
                        if (dotIdx > 0)
                        {
                            int tabIdx;
                            if (int.TryParse(selected.Substring(0, dotIdx), out tabIdx))
                            {
                                tabIdx--; // 1-indexed display
                                if (tabIdx >= 0 && tabIdx < tabControl.TabPages.Count)
                                {
                                    tabControl.SelectedIndex = tabIdx;
                                    tabSearchForm.Close();
                                }
                            }
                        }
                    }
                };

                tabList.DoubleClick += (s, ev) => switchToSelected();
                searchBox.KeyDown += (s, ev) =>
                {
                    if (ev.KeyCode == Keys.Enter) { ev.SuppressKeyPress = true; switchToSelected(); }
                    else if (ev.KeyCode == Keys.Escape) { ev.SuppressKeyPress = true; tabSearchForm.Close(); }
                    else if (ev.KeyCode == Keys.Down && tabList.Items.Count > 0)
                    {
                        ev.SuppressKeyPress = true;
                        tabList.SelectedIndex = Math.Min(tabList.SelectedIndex + 1, tabList.Items.Count - 1);
                    }
                    else if (ev.KeyCode == Keys.Up && tabList.Items.Count > 0)
                    {
                        ev.SuppressKeyPress = true;
                        tabList.SelectedIndex = Math.Max(tabList.SelectedIndex - 1, 0);
                    }
                };

                tabSearchForm.Shown += (s, ev) => searchBox.Focus();
                tabSearchForm.ShowDialog(this);
            }
        }

        // ===== CLEAR BROWSING DATA DIALOG (Ctrl+Shift+Delete) =====

        private void ShowClearBrowsingDataDialog()
        {
            using (Form dlg = new Form())
            {
                dlg.Text = "Clear Browsing Data";
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
                dlg.MaximizeBox = false;
                dlg.MinimizeBox = false;
                dlg.ClientSize = new Size(380, 310);
                dlg.BackColor = Color.FromArgb(24, 24, 30);

                Label title = new Label();
                title.Text = "🗑️ Clear Browsing Data";
                title.ForeColor = Color.FromArgb(220, 225, 240);
                title.Font = new Font("Segoe UI Variable Display", 13f, FontStyle.Bold);
                title.Location = new Point(16, 14);
                title.AutoSize = true;
                dlg.Controls.Add(title);

                CheckBox chkHistory = new CheckBox();
                chkHistory.Text = "Browsing History";
                chkHistory.Checked = true;
                chkHistory.ForeColor = Color.FromArgb(210, 215, 230);
                chkHistory.Font = new Font("Segoe UI Variable Display", 10f);
                chkHistory.Location = new Point(24, 56);
                chkHistory.AutoSize = true;
                dlg.Controls.Add(chkHistory);

                CheckBox chkDownloads = new CheckBox();
                chkDownloads.Text = "Download History";
                chkDownloads.Checked = true;
                chkDownloads.ForeColor = Color.FromArgb(210, 215, 230);
                chkDownloads.Font = new Font("Segoe UI Variable Display", 10f);
                chkDownloads.Location = new Point(24, 88);
                chkDownloads.AutoSize = true;
                dlg.Controls.Add(chkDownloads);

                CheckBox chkCookies = new CheckBox();
                chkCookies.Text = "Cookies & Active Sessions";
                chkCookies.Checked = false;
                chkCookies.ForeColor = Color.FromArgb(210, 215, 230);
                chkCookies.Font = new Font("Segoe UI Variable Display", 10f);
                chkCookies.Location = new Point(24, 120);
                chkCookies.AutoSize = true;
                dlg.Controls.Add(chkCookies);

                CheckBox chkCache = new CheckBox();
                chkCache.Text = "Cached Images & Files (Disk Cache)";
                chkCache.Checked = true;
                chkCache.ForeColor = Color.FromArgb(210, 215, 230);
                chkCache.Font = new Font("Segoe UI Variable Display", 10f);
                chkCache.Location = new Point(24, 152);
                chkCache.AutoSize = true;
                dlg.Controls.Add(chkCache);

                CheckBox chkSiteData = new CheckBox();
                chkSiteData.Text = "Site Storage (DOM, IndexedDB, Service Workers)";
                chkSiteData.Checked = false;
                chkSiteData.ForeColor = Color.FromArgb(210, 215, 230);
                chkSiteData.Font = new Font("Segoe UI Variable Display", 10f);
                chkSiteData.Location = new Point(24, 184);
                chkSiteData.AutoSize = true;
                dlg.Controls.Add(chkSiteData);

                Button btnClear = new Button();
                btnClear.Text = "Clear Data";
                btnClear.Size = new Size(120, 36);
                btnClear.Location = new Point(245, 254);
                btnClear.FlatStyle = FlatStyle.Flat;
                btnClear.FlatAppearance.BorderSize = 0;
                btnClear.BackColor = Color.FromArgb(200, 40, 40);
                btnClear.ForeColor = Color.White;
                btnClear.Font = new Font("Segoe UI Variable Display", 10f, FontStyle.Bold);
                btnClear.Cursor = Cursors.Hand;
                dlg.Controls.Add(btnClear);

                Button btnCancel = new Button();
                btnCancel.Text = "Cancel";
                btnCancel.Size = new Size(90, 36);
                btnCancel.Location = new Point(145, 254);
                btnCancel.FlatStyle = FlatStyle.Flat;
                btnCancel.FlatAppearance.BorderSize = 1;
                btnCancel.FlatAppearance.BorderColor = Color.FromArgb(60, 60, 72);
                btnCancel.BackColor = Color.FromArgb(40, 40, 50);
                btnCancel.ForeColor = Color.FromArgb(200, 205, 220);
                btnCancel.Font = new Font("Segoe UI Variable Display", 10f);
                btnCancel.Cursor = Cursors.Hand;
                btnCancel.DialogResult = DialogResult.Cancel;
                dlg.Controls.Add(btnCancel);
                dlg.CancelButton = btnCancel;

                btnClear.Click += async (s, ev) =>
                {
                    int cleared = 0;
                    CoreWebView2BrowsingDataKinds kinds = (CoreWebView2BrowsingDataKinds)0;
                    if (chkHistory.Checked)
                    {
                        HistoryManager.ClearHistory();
                        kinds |= CoreWebView2BrowsingDataKinds.BrowsingHistory;
                        cleared++;
                    }
                    if (chkDownloads.Checked)
                    {
                        DownloadsManager.ClearDownloads();
                        kinds |= CoreWebView2BrowsingDataKinds.DownloadHistory;
                        cleared++;
                    }
                    if (chkCookies.Checked)
                    {
                        kinds |= CoreWebView2BrowsingDataKinds.Cookies;
                        cleared++;
                    }
                    if (chkCache.Checked)
                    {
                        kinds |= CoreWebView2BrowsingDataKinds.DiskCache;
                        cleared++;
                    }
                    if (chkSiteData.Checked)
                    {
                        kinds |= CoreWebView2BrowsingDataKinds.AllDomStorage
                            | CoreWebView2BrowsingDataKinds.IndexedDb
                            | CoreWebView2BrowsingDataKinds.LocalStorage
                            | CoreWebView2BrowsingDataKinds.CacheStorage
                            | CoreWebView2BrowsingDataKinds.ServiceWorkers;
                        cleared++;
                    }
                    if ((int)kinds != 0)
                    {
                        WebView2 wv = GetCurrentWebView();
                        if (wv != null && !wv.IsDisposed && wv.CoreWebView2 != null)
                        {
                            try
                            {
                                await wv.CoreWebView2.Profile.ClearBrowsingDataAsync(kinds);
                            }
                            catch { }
                        }
                    }
                    dlg.Close();
                    ShowSoftCommunication("🗑️ Cleared " + cleared + " data categories");
                };

                dlg.ShowDialog(this);
            }
        }

        private bool isFullscreen = false;
        private FormWindowState prevWindowState;
        private FormBorderStyle prevBorderStyle;

        private void HandleElementFullscreenChanged(WebView2 wv)
        {
            try
            {
                if (this.IsDisposed || wv == null || wv.CoreWebView2 == null) return;

                if (wv.CoreWebView2.ContainsFullScreenElement)
                {
                    if (!isFullscreen)
                    {
                        prevWindowState = this.WindowState;
                        prevBorderStyle = this.FormBorderStyle;
                    }
                    this.FormBorderStyle = FormBorderStyle.None;
                    this.WindowState = FormWindowState.Maximized;
                    SetOmniboxVisible(false);
                }
                else
                {
                    if (isFullscreen)
                    {
                        this.FormBorderStyle = FormBorderStyle.None;
                        this.WindowState = FormWindowState.Maximized;
                        SetOmniboxVisible(false);
                    }
                    else
                    {
                        this.FormBorderStyle = prevBorderStyle;
                        this.WindowState = prevWindowState;
                        SetOmniboxVisible(!isAppWindowMode);
                    }
                }
                this.PerformLayout();
            }
            catch { }
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                prevWindowState = this.WindowState;
                prevBorderStyle = this.FormBorderStyle;
                this.FormBorderStyle = FormBorderStyle.None;
                this.WindowState = FormWindowState.Maximized;
                SetOmniboxVisible(false);
                isFullscreen = true;

                // Show "Press F11 to exit" notification centered
                if (fullscreenNotice != null)
                {
                    fullscreenNotice.Location = new Point(
                        (this.ClientSize.Width - fullscreenNotice.Width) / 2,
                        (this.ClientSize.Height - fullscreenNotice.Height) / 2);
                    fullscreenNotice.Visible = true;
                    fullscreenNotice.BringToFront();
                    fullscreenNoticeTimer.Stop();
                    fullscreenNoticeTimer.Start();
                }
            }
            else
            {
                this.FormBorderStyle = prevBorderStyle;
                this.WindowState = prevWindowState;
                SetOmniboxVisible(!isAppWindowMode);
                isFullscreen = false;
                fullscreenAutoRevealEnabled = false;
                if (fullscreenNotice != null) fullscreenNotice.Visible = false;
            }
        }

        // ===== GITHUB AUTO-UPDATER =====

        private void CheckForUpdatesBackground()
        {
            Timer updateDelayTimer = new Timer();
            updateDelayTimer.Interval = 4000;
            updateDelayTimer.Tick += (s, e) =>
            {
                updateDelayTimer.Stop();
                updateDelayTimer.Dispose();

                UpdateManager.CheckForUpdateAsync().ContinueWith(t =>
                {
                    if (t.IsCompleted && t.Result != null && t.Result.IsNewer)
                    {
                        latestUpdateInfo = t.Result;
                        this.BeginInvoke((Action)(() =>
                        {
                            if (updateBtn != null)
                            {
                                updateBtn.Text = "🚀 " + latestUpdateInfo.TagName;
                                updateBtn.Visible = true;
                            }
                            ShowSoftCommunication("🚀 Update Available: Black Browser " + latestUpdateInfo.TagName + " is ready! Click menu or 🚀 button to install.");
                        }));
                    }
                });
            };
            updateDelayTimer.Start();
        }

        private void CheckForUpdatesUserManual()
        {
            ShowSoftCommunication("🔍 Checking GitHub for updates...");
            UpdateManager.CheckForUpdateAsync().ContinueWith(t =>
            {
                if (t.IsCompleted && t.Result != null)
                {
                    latestUpdateInfo = t.Result;
                    this.BeginInvoke((Action)(() =>
                    {
                        if (latestUpdateInfo.IsNewer)
                        {
                            if (updateBtn != null)
                            {
                                updateBtn.Text = "🚀 " + latestUpdateInfo.TagName;
                                updateBtn.Visible = true;
                            }
                            PromptApplyUpdate();
                        }
                        else
                        {
                            MessageBox.Show(this,
                                "You're running the latest version of Black Browser (" + UpdateManager.CurrentVersion + ").\n\nNo new updates available on GitHub.",
                                "Black Browser — Up to date",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Information);
                        }
                    }));
                }
                else
                {
                    this.BeginInvoke((Action)(() =>
                    {
                        ShowSoftCommunication("⚠️ Could not check for updates. Please verify your internet connection.");
                    }));
                }
            });
        }

        private void PromptApplyUpdate()
        {
            if (latestUpdateInfo == null)
            {
                CheckForUpdatesUserManual();
                return;
            }

            string releaseNotes = !string.IsNullOrEmpty(latestUpdateInfo.Body)
                ? (latestUpdateInfo.Body.Length > 280 ? latestUpdateInfo.Body.Substring(0, 277) + "..." : latestUpdateInfo.Body)
                : "Performance improvements, UI refinements, and security patches.";

            string message = "A new version of Black Browser is available!\n\n"
                + "• Current Version: " + UpdateManager.CurrentVersion + "\n"
                + "• Latest Version: " + latestUpdateInfo.TagName + "\n\n"
                + "What's new:\n" + releaseNotes + "\n\n"
                + "Would you like to download and install this update now?";

            DialogResult res = MessageBox.Show(this, message, "Update Black Browser (" + latestUpdateInfo.TagName + ")",
                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                UpdateManager.StartUpdateDownload(this, latestUpdateInfo, (status) =>
                {
                    ShowSoftCommunication(status);
                });
            }
            else if (res == DialogResult.No)
            {
                try { Process.Start(new ProcessStartInfo(latestUpdateInfo.HtmlUrl) { UseShellExecute = true }); } catch { }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Save window state & open tab session before hiding/closing
            Rectangle bounds = (this.WindowState == FormWindowState.Normal) ? this.Bounds : this.RestoreBounds;
            SettingsStore.SaveWindowBounds(this.WindowState, bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            SaveCurrentSession();

            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                if (eyeCareOverlay != null) eyeCareOverlay.SyncToOwner(this);
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
                if (appToolTip      != null) { appToolTip.Dispose(); appToolTip = null; }
                if (gcTimer         != null) gcTimer.Dispose();
                if (bannerTimer     != null) bannerTimer.Dispose();
                if (fullscreenNoticeTimer != null) fullscreenNoticeTimer.Dispose();
                if (acDebounceTimer != null) acDebounceTimer.Dispose();
                if (shelfHideTimer != null) shelfHideTimer.Dispose();
                if (dismissIcon    != null) { dismissIcon.Dispose(); dismissIcon = null; }
                foreach (var kv in iconCache)
                {
                    try { if (kv.Value != null) kv.Value.Dispose(); } catch { }
                }
                iconCache.Clear();
                if (webViewEnv      != null) { try { ((System.IDisposable)webViewEnv).Dispose(); } catch { } }
                webViewEnv = null;
            }
            base.Dispose(disposing);
        }
    }
}
