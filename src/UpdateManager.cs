using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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
        public string ChecksumUrl { get; set; }
        public bool IsInstaller { get; set; }
        public bool IsZipArchive { get; set; }
        public bool IsNewer { get; set; }
    }

    public static class UpdateManager
    {
        public const string CurrentVersion = "v9.0.1";
        public const string GitHubRepo = "shivamkumarmehta64-sketch/Black-Firefox";
        private static readonly string ApiUrl = "https://api.github.com/repos/" + GitHubRepo + "/releases/latest";

        private class ReleaseAsset
        {
            public string Name;
            public string Url;
            public long Size;
        }

        private static bool IsInstalledViaSetup()
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                return File.Exists(Path.Combine(baseDir, "unins000.exe"));
            }
            catch
            {
                return false;
            }
        }

        private static bool IsDirectoryWritable(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
                string probe = Path.Combine(dir, ".bf_write_test_" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "1");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

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

                            ReleaseAsset installerAsset = null;
                            ReleaseAsset zipAsset = null;
                            string checksumUrl = "";

                            if (dict.ContainsKey("assets") && dict["assets"] is System.Collections.IEnumerable && !(dict["assets"] is string))
                            {
                                foreach (object assetObj in (System.Collections.IEnumerable)dict["assets"])
                                {
                                    Dictionary<string, object> asset = assetObj as Dictionary<string, object>;
                                    if (asset == null) continue;

                                    string aName = asset.ContainsKey("name") && asset["name"] != null ? asset["name"].ToString() : "";
                                    string aUrl = asset.ContainsKey("browser_download_url") && asset["browser_download_url"] != null ? asset["browser_download_url"].ToString() : "";
                                    long aSize = 0;
                                    if (asset.ContainsKey("size") && asset["size"] != null)
                                    {
                                        long.TryParse(asset["size"].ToString(), out aSize);
                                    }

                                    if (string.IsNullOrEmpty(aName) || string.IsNullOrEmpty(aUrl)) continue;

                                    if (aName.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase) ||
                                        aName.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase))
                                    {
                                        checksumUrl = aUrl;
                                    }
                                    else if (aName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                                             (aName.IndexOf("setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                              aName.IndexOf("installer", StringComparison.OrdinalIgnoreCase) >= 0))
                                    {
                                        if (installerAsset == null)
                                        {
                                            installerAsset = new ReleaseAsset { Name = aName, Url = aUrl, Size = aSize };
                                        }
                                    }
                                    else if (aName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                    {
                                        if (zipAsset == null)
                                        {
                                            zipAsset = new ReleaseAsset { Name = aName, Url = aUrl, Size = aSize };
                                        }
                                    }
                                }
                            }

                            ReleaseAsset chosen = null;
                            bool isInstaller = false;
                            bool isZip = false;

                            bool setupInstalled = IsInstalledViaSetup();
                            bool canWriteAppDir = IsDirectoryWritable(AppDomain.CurrentDomain.BaseDirectory);

                            if (setupInstalled && installerAsset != null)
                            {
                                chosen = installerAsset;
                                isInstaller = true;
                            }
                            else if (!setupInstalled && canWriteAppDir && zipAsset != null)
                            {
                                chosen = zipAsset;
                                isZip = true;
                            }
                            else if (installerAsset != null)
                            {
                                chosen = installerAsset;
                                isInstaller = true;
                            }
                            else if (canWriteAppDir && zipAsset != null)
                            {
                                chosen = zipAsset;
                                isZip = true;
                            }

                            string downloadUrl = chosen != null ? chosen.Url : htmlUrl;
                            string fileName = chosen != null ? chosen.Name : "BlackFirefox-Setup.exe";
                            long fileSize = chosen != null ? chosen.Size : 0;

                            bool isNewer = CompareVersions(tag, CurrentVersion) > 0;

                            GitHubReleaseInfo info = new GitHubReleaseInfo();
                            info.TagName = tag;
                            info.Name = name;
                            info.Body = body;
                            info.HtmlUrl = htmlUrl;
                            info.DownloadUrl = downloadUrl;
                            info.FileName = fileName;
                            info.FileSize = fileSize;
                            info.ChecksumUrl = checksumUrl;
                            info.IsInstaller = isInstaller;
                            info.IsZipArchive = isZip;
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

        private static bool VerifyFileChecksum(string filePath, string fileName, string checksumUrl)
        {
            if (string.IsNullOrEmpty(checksumUrl)) return true;
            if (!checksumUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                ServicePointManager.SecurityProtocol = ServicePointManager.SecurityProtocol | (SecurityProtocolType)3072;
                string sumsText;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers.Add("User-Agent", "Black-Firefox-Browser/" + CurrentVersion);
                    sumsText = wc.DownloadString(checksumUrl);
                }
                if (string.IsNullOrWhiteSpace(sumsText)) return true;

                string expectedHash = null;
                string[] lines = sumsText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    Match m = Regex.Match(line.Trim(), @"^([a-fA-F0-9]{64})\s+\*?(.+)$");
                    if (m.Success)
                    {
                        string listedName = Path.GetFileName(m.Groups[2].Value.Trim());
                        if (string.Equals(listedName, fileName, StringComparison.OrdinalIgnoreCase))
                        {
                            expectedHash = m.Groups[1].Value.ToLowerInvariant();
                            break;
                        }
                    }
                }

                if (string.IsNullOrEmpty(expectedHash)) return true;

                using (FileStream fs = File.OpenRead(filePath))
                using (SHA256 sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(fs);
                    string actualHash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                    return string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        private static string FindPackageRootWithExe(string extractDir)
        {
            if (File.Exists(Path.Combine(extractDir, "Black.exe"))) return extractDir;
            try
            {
                foreach (string sub in Directory.GetDirectories(extractDir))
                {
                    if (File.Exists(Path.Combine(sub, "Black.exe"))) return sub;
                }
            }
            catch { }
            return null;
        }

        public static void StartUpdateDownload(IWin32Window owner, GitHubReleaseInfo release, Action<string> onStatus)
        {
            if (release == null || string.IsNullOrEmpty(release.DownloadUrl)) return;

            if (!release.DownloadUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                if (onStatus != null) onStatus("⚠️ Insecure update URL rejected.");
                return;
            }

            if (!release.IsInstaller && !release.IsZipArchive)
            {
                try { Process.Start(new ProcessStartInfo(release.DownloadUrl) { UseShellExecute = true }); } catch { }
                return;
            }

            try
            {
                string tempDir = Path.Combine(Path.GetTempPath(), "BlackFirefoxUpdate");
                if (!Directory.Exists(tempDir)) Directory.CreateDirectory(tempDir);

                string defaultExt = release.IsZipArchive ? ".zip" : ".exe";
                string fallbackName = release.IsZipArchive ? "BlackFirefox-Update.zip" : "BlackFirefox-Setup.exe";
                string safeFileName = Path.GetFileName(release.FileName ?? fallbackName);
                if (string.IsNullOrEmpty(safeFileName) || !safeFileName.EndsWith(defaultExt, StringComparison.OrdinalIgnoreCase))
                {
                    safeFileName = fallbackName;
                }

                string destFile = Path.Combine(tempDir, safeFileName);
                try { if (File.Exists(destFile)) File.Delete(destFile); } catch { }

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
                    client.Dispose();
                    if (e.Error != null || !File.Exists(destFile))
                    {
                        if (onStatus != null) onStatus("⚠️ Update download failed. Opening release page...");
                        try { Process.Start(new ProcessStartInfo(release.HtmlUrl) { UseShellExecute = true }); } catch { }
                        return;
                    }

                    if (release.FileSize > 0 && new FileInfo(destFile).Length != release.FileSize)
                    {
                        try { File.Delete(destFile); } catch { }
                        if (onStatus != null) onStatus("⚠️ Downloaded package was incomplete. Opening release page...");
                        try { Process.Start(new ProcessStartInfo(release.HtmlUrl) { UseShellExecute = true }); } catch { }
                        return;
                    }

                    if (!VerifyFileChecksum(destFile, safeFileName, release.ChecksumUrl))
                    {
                        try { File.Delete(destFile); } catch { }
                        if (onStatus != null) onStatus("⚠️ Checksum verification failed! Update aborted.");
                        return;
                    }

                    if (release.IsInstaller)
                    {
                        if (onStatus != null) onStatus("✅ Installer verified! Launching update setup...");
                        try
                        {
                            Process.Start(new ProcessStartInfo(destFile) { UseShellExecute = true });
                            Application.Exit();
                        }
                        catch
                        {
                            try { Process.Start(new ProcessStartInfo(tempDir) { UseShellExecute = true }); } catch { }
                        }
                        return;
                    }

                    // Portable ZIP in-place replacement flow
                    try
                    {
                        string stagedDir = Path.Combine(tempDir, "staged");
                        if (Directory.Exists(stagedDir)) Directory.Delete(stagedDir, true);
                        ZipFile.ExtractToDirectory(destFile, stagedDir);

                        string sourceRoot = FindPackageRootWithExe(stagedDir);
                        if (string.IsNullOrEmpty(sourceRoot))
                        {
                            if (onStatus != null) onStatus("⚠️ Invalid update archive structure. Opening release page...");
                            try { Process.Start(new ProcessStartInfo(release.HtmlUrl) { UseShellExecute = true }); } catch { }
                            return;
                        }

                        string installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');
                        string exePath = Application.ExecutablePath;
                        int pid = Process.GetCurrentProcess().Id;
                        string cmdPath = Path.Combine(tempDir, "apply_update.cmd");

                        string script =
                            "@echo off\r\n" +
                            "timeout /t 1 /nobreak >nul\r\n" +
                            "for /L %%i in (1,1,15) do (\r\n" +
                            "  tasklist /FI \"PID eq " + pid + "\" 2>NUL | find /I \"" + pid + "\" >NUL\r\n" +
                            "  if errorlevel 1 goto :copy_files\r\n" +
                            "  timeout /t 1 /nobreak >nul\r\n" +
                            ")\r\n" +
                            ":copy_files\r\n" +
                            "xcopy /s /e /y /q \"" + sourceRoot + "\\*\" \"" + installDir + "\\\" >nul\r\n" +
                            "start \"\" \"" + exePath + "\"\r\n" +
                            "exit\r\n";

                        File.WriteAllText(cmdPath, script, Encoding.ASCII);

                        if (onStatus != null) onStatus("✅ Update ready! Restarting Black Firefox...");
                        ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + cmdPath + "\"");
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                        psi.WindowStyle = ProcessWindowStyle.Hidden;
                        Process.Start(psi);
                        Application.Exit();
                    }
                    catch
                    {
                        if (onStatus != null) onStatus("⚠️ Failed to apply ZIP update. Opening release page...");
                        try { Process.Start(new ProcessStartInfo(release.HtmlUrl) { UseShellExecute = true }); } catch { }
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
