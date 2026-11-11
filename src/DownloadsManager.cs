using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BlackBrowser
{
    public class DownloadItem
    {
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public string TotalBytes { get; set; }
        public string DateAdded { get; set; }
    }

    public static class DownloadsManager
    {
        private static List<DownloadItem> downloadsList = new List<DownloadItem>();
        private static readonly object fileLock = new object();
        private static readonly string StoragePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "black-webview2", "downloads.json");

        static DownloadsManager()
        {
            Load();
        }

        private static void Load()
        {
            lock (fileLock)
            {
                try
                {
                    if (File.Exists(StoragePath))
                    {
                        var serializer = new JavaScriptSerializer();
                        var list = serializer.Deserialize<List<DownloadItem>>(File.ReadAllText(StoragePath, Encoding.UTF8));
                        if (list != null) downloadsList = list;
                    }
                }
                catch { }
            }
        }

        private static void Save()
        {
            lock (fileLock)
            {
                try
                {
                    string dir = Path.GetDirectoryName(StoragePath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    var serializer = new JavaScriptSerializer();
                    File.WriteAllText(StoragePath, serializer.Serialize(downloadsList), Encoding.UTF8);
                }
                catch { }
            }
        }

        public static string FormatBytes(long totalBytes)
        {
            if (totalBytes <= 0) return "Unknown Size";
            if (totalBytes < 1024) return totalBytes + " B";
            if (totalBytes < 1024 * 1024) return Math.Round(totalBytes / 1024.0, 1) + " KB";
            if (totalBytes < 1024L * 1024L * 1024L) return Math.Round(totalBytes / (1024.0 * 1024.0), 2) + " MB";
            return Math.Round(totalBytes / (1024.0 * 1024.0 * 1024.0), 2) + " GB";
        }

        public static void AddDownload(string fileName, string filePath, long totalBytes)
        {
            lock (fileLock)
            {
                downloadsList.Insert(0, new DownloadItem
                {
                    FileName = fileName,
                    FilePath = filePath,
                    TotalBytes = FormatBytes(totalBytes),
                    DateAdded = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                });
            }
            Save();
        }

        public static void UpdateDownloadSize(string filePath, long totalBytes)
        {
            if (string.IsNullOrEmpty(filePath) || totalBytes <= 0) return;
            bool updated = false;
            lock (fileLock)
            {
                foreach (var item in downloadsList)
                {
                    if (string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                    {
                        item.TotalBytes = FormatBytes(totalBytes);
                        updated = true;
                        break;
                    }
                }
            }
            if (updated) Save();
        }

        public static void ClearDownloads()
        {
            lock (fileLock) downloadsList.Clear();
            Save();
        }

        public static string GetDownloadPath(int index)
        {
            lock (fileLock)
            {
                if (index >= 0 && index < downloadsList.Count)
                    return downloadsList[index].FilePath;
            }
            return null;
        }

        public static int GetDownloadsCount()
        {
            lock (fileLock)
            {
                return downloadsList.Count;
            }
        }

        public static string GetDownloadsHtml(bool isDarkMode)
        {
            string bg = isDarkMode ? "#0b0d1b" : "#f5f6fa";
            string textColor = isDarkMode ? "#ffffff" : "#1a1d2e";
            string cardBg = isDarkMode ? "rgba(255, 255, 255, 0.05)" : "rgba(0, 0, 0, 0.04)";
            string border = isDarkMode ? "rgba(255, 255, 255, 0.1)" : "rgba(0, 0, 0, 0.12)";
            string muted = isDarkMode ? "#a2a6cc" : "#5b6078";

            StringBuilder sb = new StringBuilder();
            sb.Append(@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<title>Local Downloads — Black Browser</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:'Segoe UI Variable Display','Segoe UI',system-ui,-apple-system,sans-serif;background:" + bg + @";color:" + textColor + @";padding:36px 24px;max-width:860px;margin:0 auto;-webkit-font-smoothing:antialiased}
.header{display:flex;align-items:center;justify-content:space-between;margin-bottom:32px;padding-bottom:16px;border-bottom:1px solid " + border + @"}
.title{font-size:26px;font-weight:700;background:linear-gradient(135deg, #10b981, #06b6d4);-webkit-background-clip:text;-webkit-text-fill-color:transparent}
.sub{font-size:13.5px;color:" + muted + @";margin-top:6px}
.item{display:flex;align-items:center;justify-content:space-between;padding:16px 20px;background:" + cardBg + @";border:1px solid " + border + @";border-radius:16px;margin-bottom:14px;backdrop-filter:blur(16px);transition:all .2s ease}
.item:hover{transform:translateY(-2px);border-color:#10b981;box-shadow:0 8px 24px rgba(16,185,129,0.2)}
.item-name{font-size:15.5px;font-weight:600;margin-bottom:4px;color:" + textColor + @"}
.item-path{font-size:13px;color:" + muted + @";word-break:break-all}
.item-size{font-size:13px;font-weight:600;color:#10b981;white-space:nowrap;margin-left:20px}
.item-actions{display:flex;align-items:center;gap:8px;margin-left:16px}
.btn{padding:7px 14px;border-radius:10px;border:1px solid " + border + @";background:rgba(16,185,129,0.12);color:#10b981;font-family:inherit;font-size:12.5px;font-weight:600;cursor:pointer;transition:all .2s ease}
.btn:hover{background:rgba(16,185,129,0.25)}
.btn-clear{padding:9px 18px;border-radius:10px;border:1px solid rgba(239,68,68,0.4);background:rgba(239,68,68,0.1);color:#f87171;font-family:inherit;font-size:13px;font-weight:600;cursor:pointer;transition:all .2s ease}
.btn-clear:hover{background:rgba(239,68,68,0.22)}
.empty{text-align:center;padding:70px 0;color:" + muted + @";font-size:16px}
</style>
</head>
<body>

<script>
function openFolder(i){ if(window.chrome && chrome.webview){ chrome.webview.postMessage('open:'+i); } }
function clearAll(){ if(window.chrome && chrome.webview){ chrome.webview.postMessage('clear'); } }
</script>

<div class='header'>
  <div>
    <div class='title'>📥 Local Downloads Manager</div>
    <div class='sub'>✨ Anime Cyberpunk Theme • Stored Locally on Device</div>
  </div>
  <div><button class='btn-clear' onclick='clearAll()'>🗑️ Clear All</button></div>
</div>

<div id='list'>");

            lock (fileLock)
            {
                if (downloadsList.Count == 0)
                {
                    sb.Append("<div class='empty'>No active or recent downloads logged. Downloaded files will appear here automatically!</div>");
                }
                else
                {
                    int di = 0;
                    foreach (var item in downloadsList)
                    {
                        string safeName = System.Web.HttpUtility.HtmlEncode(item.FileName);
                        string safePath = System.Web.HttpUtility.HtmlEncode(item.FilePath);
                        string safeSize = System.Web.HttpUtility.HtmlEncode(item.TotalBytes);

                        sb.Append(@"
<div class='item'>
  <div>
    <div class='item-name'>📄 " + safeName + @"</div>
    <div class='item-path'>" + safePath + @"</div>
  </div>
  <div class='item-actions'>
    <div class='item-size'>" + safeSize + @"</div>
    <button class='btn' onclick='openFolder(" + di + @")'>📂 Open Folder</button>
  </div>
</div>");
                        di++;
                    }
                }
            }

            sb.Append(@"
</div>

</body>
</html>");
            return sb.ToString();
        }
    }
}
