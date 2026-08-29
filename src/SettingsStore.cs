using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BlackBrowser
{
    public static class SettingsStore
    {
        private static string settingsFilePath;
        private static string searchEngine = "google";
        private static bool forceDarkMode = false;
        private static bool httpsFirstMode = false;
        private static string accent = "#0C63E8";
        private static bool animationsEnabled = true;
        private static bool showHomeButton = true;
        private static string homePage = "black://home";
        private static double defaultZoom = 1.0;
        private static FormWindowState windowState = FormWindowState.Normal;
        private static int windowLeft = 0;
        private static int windowTop = 0;
        private static int windowWidth = 1280;
        private static int windowHeight = 820;

        private static string speedDialBg = "aurora";
        private static bool showClock = true;
        private static bool showNotes = true;
        private static bool showTelemetry = true;
        private static bool allowDeveloperTools = false;
        private static int cornerRadius = 12;

        static SettingsStore()
        {
            settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "black-webview2", "settings.json");
            Load();
        }

        private static void Load()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    var serializer = new JavaScriptSerializer();
                    var data = serializer.Deserialize<SettingsData>(File.ReadAllText(settingsFilePath));
                    if (data != null)
                    {
                        if (!string.IsNullOrEmpty(data.SearchEngine))
                            searchEngine = data.SearchEngine;
                        forceDarkMode = data.ForceDarkMode;
                        httpsFirstMode = data.HttpsFirstMode;
                        if (!string.IsNullOrEmpty(data.Accent))
                            accent = data.Accent;
                        animationsEnabled = data.AnimationsEnabled;
                        showHomeButton = data.ShowHomeButton;
                        homePage = data.HomePage;
                        if (data.DefaultZoom > 0) defaultZoom = data.DefaultZoom;
                        windowState = data.WindowState;
                        windowLeft = data.WindowLeft;
                        windowTop = data.WindowTop;
                        windowWidth = data.WindowWidth > 0 ? data.WindowWidth : 1280;
                        windowHeight = data.WindowHeight > 0 ? data.WindowHeight : 820;

                        if (!string.IsNullOrEmpty(data.SpeedDialBg))
                            speedDialBg = data.SpeedDialBg;
                        showClock = data.ShowClock;
                        showNotes = data.ShowNotes;
                        showTelemetry = data.ShowTelemetry;
                        allowDeveloperTools = data.AllowDeveloperTools;
                        if (data.CornerRadius > 0)
                            cornerRadius = data.CornerRadius;
                    }
                }
            }
            catch { }
        }

        public static FormWindowState WindowState
        {
            get { return windowState; }
            set { windowState = value; Save(); }
        }

        public static int WindowLeft
        {
            get { return windowLeft; }
            set { windowLeft = value; Save(); }
        }

        public static int WindowTop
        {
            get { return windowTop; }
            set { windowTop = value; Save(); }
        }

        public static int WindowWidth
        {
            get { return windowWidth; }
            set { windowWidth = value; Save(); }
        }

        public static int WindowHeight
        {
            get { return windowHeight; }
            set { windowHeight = value; Save(); }
        }

        public static string SearchEngine
        {
            get { return searchEngine; }
            set
            {
                searchEngine = value;
                Save();
            }
        }

        public static bool ForceDarkMode
        {
            get { return forceDarkMode; }
            set { forceDarkMode = value; Save(); }
        }

        public static bool HttpsFirstMode
        {
            get { return httpsFirstMode; }
            set { httpsFirstMode = value; Save(); }
        }

        public static string Accent
        {
            get { return accent; }
            set { accent = value; Save(); }
        }

        public static bool AnimationsEnabled
        {
            get { return animationsEnabled; }
            set { animationsEnabled = value; Save(); }
        }

        public static bool ShowHomeButton
        {
            get { return showHomeButton; }
            set { showHomeButton = value; Save(); }
        }

        public static string HomePage
        {
            get { return homePage; }
            set { homePage = value; Save(); }
        }

        public static double DefaultZoom
        {
            get { return defaultZoom; }
            set { defaultZoom = value; Save(); }
        }

        public static string SpeedDialBg
        {
            get { return speedDialBg; }
            set { speedDialBg = value; Save(); }
        }

        public static bool ShowClock
        {
            get { return showClock; }
            set { showClock = value; Save(); }
        }

        public static bool ShowNotes
        {
            get { return showNotes; }
            set { showNotes = value; Save(); }
        }

        public static bool ShowTelemetry
        {
            get { return showTelemetry; }
            set { showTelemetry = value; Save(); }
        }

        public static bool AllowDeveloperTools
        {
            get { return allowDeveloperTools; }
            set { allowDeveloperTools = value; Save(); }
        }

        public static int CornerRadius
        {
            get { return cornerRadius; }
            set { cornerRadius = value; Save(); }
        }

        public static string GetSearchUrl(string query)
        {
            string q = Uri.EscapeDataString(query ?? "");
            switch ((searchEngine ?? "google").ToLowerInvariant())
            {
                case "duckduckgo": return "https://duckduckgo.com/?q=" + q;
                case "bing": return "https://www.bing.com/search?q=" + q;
                case "youtube": return "https://www.youtube.com/results?search_query=" + q;
                case "github": return "https://github.com/search?q=" + q;
                case "wikipedia": return "https://en.wikipedia.org/wiki/Special:Search?search=" + q;
                case "brave": return "https://search.brave.com/search?q=" + q;
                case "yahoo": return "https://search.yahoo.com/search?p=" + q;
                case "ecosia": return "https://www.ecosia.org/search?q=" + q;
                case "startpage": return "https://www.startpage.com/sp/search?query=" + q;
                default: return "https://www.google.com/search?q=" + q;
            }
        }

        private static string ProtectString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plainText);
                byte[] protectedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(protectedData);
            }
            catch { return plainText; }
        }

        private static string UnprotectString(string protectedText)
        {
            if (string.IsNullOrEmpty(protectedText)) return protectedText;
            try
            {
                byte[] data = Convert.FromBase64String(protectedText);
                byte[] plain = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(plain);
            }
            catch { return null; }
        }

        private static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(settingsFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(new SettingsData
                {
                    SearchEngine = searchEngine,
                    ForceDarkMode = forceDarkMode,
                    HttpsFirstMode = httpsFirstMode,
                    Accent = accent,
                    AnimationsEnabled = animationsEnabled,
                    ShowHomeButton = showHomeButton,
                    HomePage = homePage,
                    DefaultZoom = defaultZoom,
                    WindowState = windowState,
                    WindowLeft = windowLeft,
                    WindowTop = windowTop,
                    WindowWidth = windowWidth,
                    WindowHeight = windowHeight,
                    SpeedDialBg = speedDialBg,
                    ShowClock = showClock,
                    ShowNotes = showNotes,
                    ShowTelemetry = showTelemetry,
                    AllowDeveloperTools = allowDeveloperTools,
                    CornerRadius = cornerRadius
                });
                File.WriteAllText(settingsFilePath, ProtectString(json), Encoding.UTF8);
            }
            catch { }
        }

        private class SettingsData
        {
            public string SearchEngine { get; set; }
            public bool ForceDarkMode { get; set; }
            public bool HttpsFirstMode { get; set; }
            public string Accent { get; set; }
            public bool AnimationsEnabled { get; set; }
            public bool ShowHomeButton { get; set; }
            public string HomePage { get; set; }
            public double DefaultZoom { get; set; }
            public FormWindowState WindowState { get; set; }
            public int WindowLeft { get; set; }
            public int WindowTop { get; set; }
            public int WindowWidth { get; set; }
            public int WindowHeight { get; set; }
            public string SpeedDialBg { get; set; }
            public bool ShowClock { get; set; }
            public bool ShowNotes { get; set; }
            public bool ShowTelemetry { get; set; }
            public bool AllowDeveloperTools { get; set; }
            public int CornerRadius { get; set; }
        }
    }
}
