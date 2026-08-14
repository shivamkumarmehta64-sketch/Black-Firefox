using System;
using System.IO;
using System.Web.Script.Serialization;

namespace BlackBrowser
{
    public static class SettingsStore
    {
        private static string settingsFilePath;
        private static string searchEngine = "google";

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
                    if (data != null && !string.IsNullOrEmpty(data.SearchEngine))
                        searchEngine = data.SearchEngine;
                }
            }
            catch { }
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

        public static string GetSearchUrl(string query)
        {
            string q = Uri.EscapeDataString(query ?? "");
            switch (searchEngine.ToLowerInvariant())
            {
                case "duckduckgo": return "https://duckduckgo.com/?q=" + q;
                case "bing": return "https://www.bing.com/search?q=" + q;
                case "youtube": return "https://www.youtube.com/results?search_query=" + q;
                default: return "https://www.google.com/search?q=" + q;
            }
        }

        private static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(settingsFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                var serializer = new JavaScriptSerializer();
                File.WriteAllText(settingsFilePath, serializer.Serialize(new SettingsData { SearchEngine = searchEngine }));
            }
            catch { }
        }

        private class SettingsData
        {
            public string SearchEngine { get; set; }
        }
    }
}