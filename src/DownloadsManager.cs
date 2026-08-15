using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

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

        public static void AddDownload(string fileName, string filePath, long totalBytes)
        {
            lock (fileLock)
            {
                double mb = Math.Round(totalBytes / (1024.0 * 1024.0), 2);
                string sizeStr = mb > 0 ? mb.ToString() + " MB" : "Unknown Size";

                downloadsList.Insert(0, new DownloadItem
                {
                    FileName = fileName,
                    FilePath = filePath,
                    TotalBytes = sizeStr,
                    DateAdded = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                });
            }
        }

        public static string GetDownloadsHtml(bool isDarkMode)
        {
            string bg = "#0b0d1b";
            string textColor = "#ffffff";
            string cardBg = "rgba(255, 255, 255, 0.05)";
            string border = "rgba(255, 255, 255, 0.1)";

            StringBuilder sb = new StringBuilder();
            sb.Append(@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<title>Local Downloads — Black Firefox</title>
<link href='https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;600;700&family=Inter:wght@400;500;600&display=swap' rel='stylesheet'>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:'Segoe UI Variable Display','Plus Jakarta Sans','Inter',sans-serif;background:" + bg + @";color:" + textColor + @";padding:36px 24px;max-width:860px;margin:0 auto;-webkit-font-smoothing:antialiased}
.header{display:flex;align-items:center;justify-content:space-between;margin-bottom:32px;padding-bottom:16px;border-bottom:1px solid " + border + @"}
.title{font-size:26px;font-weight:700;background:linear-gradient(135deg, #10b981, #06b6d4);-webkit-background-clip:text;-webkit-text-fill-color:transparent}
.sub{font-size:13.5px;color:#a2a6cc;margin-top:6px}
.item{display:flex;align-items:center;justify-content:space-between;padding:16px 20px;background:" + cardBg + @";border:1px solid " + border + @";border-radius:16px;margin-bottom:14px;backdrop-filter:blur(16px);transition:all .2s ease}
.item:hover{transform:translateY(-2px);border-color:#10b981;box-shadow:0 8px 24px rgba(16,185,129,0.2)}
.item-name{font-size:15.5px;font-weight:600;margin-bottom:4px;color:" + textColor + @"}
.item-path{font-size:13px;color:#a2a6cc;word-break:break-all}
.item-size{font-size:13px;font-weight:600;color:#10b981;white-space:nowrap;margin-left:20px}
.empty{text-align:center;padding:70px 0;color:#a2a6cc;font-size:16px}
</style>
</head>
<body>

<div class='header'>
  <div>
    <div class='title'>📥 Local Downloads Manager</div>
    <div class='sub'>✨ Anime Cyberpunk Theme • Stored Locally on Device</div>
  </div>
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
  <div class='item-size'>" + safeSize + @"</div>
</div>");
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
