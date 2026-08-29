using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace BlackBrowser
{
    public class GitHubReleaseInfo
    {
        public string TagName { get; set; }
        public string Name { get; set; }
        public string Body { get; set; }
        public string HtmlUrl { get; set; }
        public string DownloadUrl { get; set; }
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public bool IsNewer { get; set; }
    }

    public static class UpdateManager
    {
        public const string CurrentVersion = "v9.0.0";
        public const string GitHubRepo = "shivamkumarmehta64-sketch/Black-Firefox";
        private static readonly string ApiUrl = "https://api.github.com/repos/" + GitHubRepo + "/releases/latest";

        public static Task<GitHubReleaseInfo> CheckForUpdateAsync()
        {
            return Task.Factory.StartNew<GitHubReleaseInfo>(() =>
            {
                try
                {
                    ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072; // TLS 1.2
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(ApiUrl);
                    request.UserAgent = "Black-Firefox-Browser/" + CurrentVersion;
                    request.Timeout = 8000;
                    request.Accept = "application/vnd.github.v3+json";

                    using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                    using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                    {
                        string json = reader.ReadToEnd();
                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        Dictionary<string, object> dict = serializer.Deserialize<Dictionary<string, object>>(json);

                        if (dict != null)
                        {
                            string tag = dict.ContainsKey("tag_name") && dict["tag_name"] != null ? dict["tag_name"].ToString() : "";
                            string name = dict.ContainsKey("name") && dict["name"] != null ? dict["name"].ToString() : tag;
                            string body = dict.ContainsKey("body") && dict["body"] != null ? dict["body"].ToString() : "";
                            string htmlUrl = dict.ContainsKey("html_url") && dict["html_url"] != null ? dict["html_url"].ToString() : "https://github.com/" + GitHubRepo + "/releases";

                            string downloadUrl = "";
                            string fileName = "Black.exe";
                            long fileSize = 0;

                            if (dict.ContainsKey("assets") && dict["assets"] is object[])
                            {
                                object[] assets = (object[])dict["assets"];
                                foreach (object assetObj in assets)
                                {
                                    Dictionary<string, object> asset = assetObj as Dictionary<string, object>;
                                    if (asset != null)
                                    {
                                        string aName = asset.ContainsKey("name") && asset["name"] != null ? asset["name"].ToString() : "";
                                        if (aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || aName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                        {
                                            downloadUrl = asset.ContainsKey("browser_download_url") && asset["browser_download_url"] != null ? asset["browser_download_url"].ToString() : "";
                                            fileName = aName;
                                            if (asset.ContainsKey("size") && asset["size"] != null)
                                            {
                                                long.TryParse(asset["size"].ToString(), out fileSize);
                                            }
                                            break;
                                        }
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(downloadUrl))
                            {
                                downloadUrl = htmlUrl;
                            }

                            bool isNewer = CompareVersions(tag, CurrentVersion) > 0;

                            GitHubReleaseInfo info = new GitHubReleaseInfo();
                            info.TagName = tag;
                            info.Name = name;
                            info.Body = body;
                            info.HtmlUrl = htmlUrl;
                            info.DownloadUrl = downloadUrl;
                            info.FileName = fileName;
                            info.FileSize = fileSize;
                            info.IsNewer = isNewer;
                            return info;
                        }
                    }
                }
                catch
                {
                    // Fallback on network/API failure
                }

                return null;
            });
        }

        private static int CompareVersions(string newTag, string curTag)
        {
            try
            {
                Version vNew = ParseVersion(newTag);
                Version vCur = ParseVersion(curTag);
                return vNew.CompareTo(vCur);
            }
            catch
            {
                return string.Compare(newTag, curTag, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static Version ParseVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return new Version(0, 0, 0);
            string clean = Regex.Replace(tag, @"[^\d\.]", "");
            string[] parts = clean.Split('.');
            if (parts.Length == 1) clean += ".0.0";
            else if (parts.Length == 2) clean += ".0";
            Version v;
            if (Version.TryParse(clean, out v)) return v;
            return new Version(0, 0, 0);
        }

        public static void StartUpdateDownload(IWin32Window owner, GitHubReleaseInfo release, Action<string> onStatus)
        {
            if (release == null || string.IsNullOrEmpty(release.DownloadUrl)) return;

            // Security check: Must be an HTTPS URL
            if (!release.DownloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (onStatus != null) onStatus("⚠️ Insecure update URL rejected.");
                return;
            }

            if (!release.DownloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                try { Process.Start(new ProcessStartInfo(release.DownloadUrl) { UseShellExecute = true }); } catch { }
                return;
            }

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "BlackFirefoxUpdate");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                // Prevent directory traversal by strictly extracting the file name
                string safeFileName = Path.GetFileName(release.FileName ?? "Black-Setup.exe");
                if (string.IsNullOrEmpty(safeFileName) || !safeFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    safeFileName = "Black-Setup.exe";
                }

                string destFile = Path.Combine(tempDir, safeFileName);

                if (onStatus != null) onStatus("⬇️ Downloading Black Firefox update (" + release.TagName + ")...");

                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072;
                WebClient client = new WebClient();
                client.Headers.Add("User-Agent", "Black-Firefox-Browser/" + CurrentVersion);

                client.DownloadProgressChanged += (s, e) =>
                {
                    if (onStatus != null) onStatus("⬇️ Downloading update: " + e.ProgressPercentage + "%");
                };

                client.DownloadFileCompleted += (s, e) =>
                {
                    if (e.Error != null)
                    {
                        if (onStatus != null) onStatus("⚠️ Update download failed. Opening release page...");
                        try { Process.Start(new ProcessStartInfo(release.HtmlUrl) { UseShellExecute = true }); } catch { }
                        return;
                    }

                    if (onStatus != null) onStatus("✅ Update downloaded! Launching installer...");

                    try
                    {
                        if (File.Exists(destFile))
                        {
                            Process.Start(new ProcessStartInfo(destFile) { UseShellExecute = true });
                            Application.Exit();
                        }
                    }
                    catch
                    {
                        try { Process.Start(new ProcessStartInfo(tempDir) { UseShellExecute = true }); } catch { }
                    }
                };

                client.DownloadFileAsync(new Uri(release.DownloadUrl), destFile);
            }
            catch (Exception ex)
            {
                if (onStatus != null) onStatus("⚠️ Update error: " + ex.Message);
                try { Process.Start(new ProcessStartInfo(release.HtmlUrl) { UseShellExecute = true }); } catch { }
            }
        }
    }
}
