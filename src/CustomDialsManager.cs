using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BlackBrowser
{
    public class CustomDialItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string IconText { get; set; }
        public string BgColor { get; set; }
        public string FgColor { get; set; }
    }

    public static class CustomDialsManager
    {
        private static string customDialsFilePath;
        private static List<CustomDialItem> customList = new List<CustomDialItem>();
        private static readonly object fileLock = new object();

        static CustomDialsManager()
        {
            customDialsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "black-webview2", "custom_dials.json");
            LoadCustomDials();
        }

        private static void LoadCustomDials()
        {
            lock (fileLock)
            {
                try
                {
                    if (File.Exists(customDialsFilePath))
                    {
                        string json = File.ReadAllText(customDialsFilePath, Encoding.UTF8);
                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        customList = serializer.Deserialize<List<CustomDialItem>>(json) ?? new List<CustomDialItem>();
                    }
                    else
                    {
                        // Add default quick apps
                        customList = new List<CustomDialItem>
                        {
                            new CustomDialItem { Title = "Google", Url = "https://www.google.com", IconText = "G", BgColor = "#e8f0fe", FgColor = "#1a73e8" },
                            new CustomDialItem { Title = "Gemini", Url = "https://gemini.google.com", IconText = "AI", BgColor = "#e8f0fe", FgColor = "#1a73e8" },
                            new CustomDialItem { Title = "YouTube", Url = "https://www.youtube.com", IconText = "Y", BgColor = "#fce8e6", FgColor = "#d93025" },
                            new CustomDialItem { Title = "YT Music", Url = "https://music.youtube.com", IconText = "M", BgColor = "#fef7e0", FgColor = "#f29900" },
                            new CustomDialItem { Title = "Gmail", Url = "https://mail.google.com", IconText = "@", BgColor = "#fce8e6", FgColor = "#d93025" },
                            new CustomDialItem { Title = "Maps", Url = "https://www.google.com/maps", IconText = "MAP", BgColor = "#e6f4ea", FgColor = "#107c41" },
                            new CustomDialItem { Title = "GitHub", Url = "https://github.com", IconText = "GH", BgColor = "#e8eaed", FgColor = "#202124" },
                            new CustomDialItem { Title = "ChatGPT", Url = "https://chatgpt.com", IconText = "AI", BgColor = "#e6f4ea", FgColor = "#107c41" },
                            new CustomDialItem { Title = "Reddit", Url = "https://reddit.com", IconText = "R", BgColor = "#fce8e6", FgColor = "#d93025" },
                            new CustomDialItem { Title = "X / Twitter", Url = "https://x.com", IconText = "X", BgColor = "#e8eaed", FgColor = "#202124" },
                            new CustomDialItem { Title = "Instagram", Url = "https://www.instagram.com", IconText = "IG", BgColor = "#fce8e6", FgColor = "#d93025" },
                            new CustomDialItem { Title = "Netflix", Url = "https://www.netflix.com", IconText = "N", BgColor = "#e8eaed", FgColor = "#b1060f" }
                        };
                        SaveCustomDials();
                    }
                }
                catch
                {
                    customList = new List<CustomDialItem>();
                }
            }
        }

        public static void AddCustomDial(string title, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            string trimmedUrl = url.Trim();
            if (!trimmedUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !trimmedUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (trimmedUrl.IndexOf(':') >= 0) return; // reject javascript:, data:, etc.
                trimmedUrl = "https://" + trimmedUrl;
            }
            Uri parsed;
            if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out parsed) ||
                (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
                return;

            string safeTitle = (title ?? "").Trim();
            if (safeTitle.Length == 0) safeTitle = parsed.Host;

            lock (fileLock)
            {
                string iconText = safeTitle.Length >= 2 ? safeTitle.Substring(0, 2).ToUpperInvariant() : safeTitle.ToUpperInvariant();
                if (iconText.Length == 0) iconText = "W";

                customList.Add(new CustomDialItem
                {
                    Title = safeTitle,
                    Url = parsed.AbsoluteUri,
                    IconText = iconText,
                    BgColor = "#e8f0fe",
                    FgColor = "#1a73e8"
                });

                SaveCustomDials();
            }
        }

        public static List<CustomDialItem> GetDials()
        {
            lock (fileLock)
            {
                return new List<CustomDialItem>(customList);
            }
        }

        public static void RemoveCustomDial(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;
            lock (fileLock)
            {
                customList.RemoveAll(d => d.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
                SaveCustomDials();
            }
        }

        private static void SaveCustomDials()
        {
            try
            {
                string dir = Path.GetDirectoryName(customDialsFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(customList);
                File.WriteAllText(customDialsFilePath, json, Encoding.UTF8);
            }
            catch { }
        }
    }
}
