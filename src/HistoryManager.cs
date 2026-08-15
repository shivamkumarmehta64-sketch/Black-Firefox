using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BlackBrowser
{
    public class HistoryItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Timestamp { get; set; }
    }

    public static class HistoryManager
    {
        private static string historyFilePath;
        private static List<HistoryItem> historyList = new List<HistoryItem>();
        private static readonly object fileLock = new object();

        static HistoryManager()
        {
            historyFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "black-webview2", "history.json");
            LoadHistory();
        }

        private static void LoadHistory()
        {
            lock (fileLock)
            {
                try
                {
                    if (File.Exists(historyFilePath))
                    {
                        string json = File.ReadAllText(historyFilePath, Encoding.UTF8);
                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        historyList = serializer.Deserialize<List<HistoryItem>>(json) ?? new List<HistoryItem>();
                    }
                }
                catch
                {
                    historyList = new List<HistoryItem>();
                }
            }
        }

        public static void AddVisit(string title, string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url == "about:blank" || url.StartsWith("black://"))
                return;

            lock (fileLock)
            {
                try
                {
                    string pageTitle = string.IsNullOrWhiteSpace(title) ? url : title;
                    string timeStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

                    if (historyList.Count > 0 && historyList[0].Url == url)
                    {
                        historyList[0].Timestamp = timeStr;
                        historyList[0].Title = pageTitle;
                    }
                    else
                    {
                        historyList.Insert(0, new HistoryItem
                        {
                            Title = pageTitle,
                            Url = url,
                            Timestamp = timeStr
                        });
                    }

                    if (historyList.Count > 1000)
                    {
                        historyList.RemoveRange(1000, historyList.Count - 1000);
                    }

                    SaveHistory();
                }
                catch { }
            }
        }

        public static void ClearHistory()
        {
            lock (fileLock)
            {
                try
                {
                    historyList.Clear();
                    SaveHistory();
                }
                catch { }
            }
        }

        private static void SaveHistory()
        {
            try
            {
                string dir = Path.GetDirectoryName(historyFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(historyList);
                File.WriteAllText(historyFilePath, json, Encoding.UTF8);
            }
            catch { }
        }

        public static string GetHistoryHtml(bool isDarkMode)
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
<title>Local Browsing History — Black Firefox</title>
<link href='https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;600;700&family=Inter:wght@400;500;600&display=swap' rel='stylesheet'>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:'Segoe UI Variable Display','Plus Jakarta Sans','Inter',sans-serif;background:" + bg + @";color:" + textColor + @";padding:36px 24px;max-width:860px;margin:0 auto;-webkit-font-smoothing:antialiased}
.header{display:flex;align-items:center;justify-content:space-between;margin-bottom:32px;padding-bottom:16px;border-bottom:1px solid " + border + @"}
.title{font-size:26px;font-weight:700;background:linear-gradient(135deg, #06b6d4, #9333ea);-webkit-background-clip:text;-webkit-text-fill-color:transparent}
.sub{font-size:13.5px;color:#a2a6cc;margin-top:6px}
.item{display:flex;align-items:center;justify-content:space-between;padding:16px 20px;background:" + cardBg + @";border:1px solid " + border + @";border-radius:16px;margin-bottom:14px;text-decoration:none;color:inherit;backdrop-filter:blur(16px);transition:all .2s ease}
.item:hover{transform:translateY(-2px);border-color:#06b6d4;box-shadow:0 8px 24px rgba(6,182,212,0.2)}
.item-title{font-size:15.5px;font-weight:600;margin-bottom:4px;color:" + textColor + @"}
.item-url{font-size:13px;color:#06b6d4;word-break:break-all}
.item-time{font-size:12px;color:#a2a6cc;white-space:nowrap;margin-left:20px;font-weight:500}
.empty{text-align:center;padding:70px 0;color:#a2a6cc;font-size:16px}
</style>
</head>
<body>

<div class='header'>
  <div>
    <div class='title'>📜 Local Browsing History</div>
    <div class='sub'>✨ Anime Cyberpunk Theme • 100% Private • Stored Locally on Device</div>
  </div>
</div>

<div id='list'>");

            lock (fileLock)
            {
                if (historyList.Count == 0)
                {
                    sb.Append("<div class='empty'>No local browsing history yet. Visit any web page to start logging!</div>");
                }
                else
                {
                    foreach (var item in historyList)
                    {
                        string safeTitle = System.Web.HttpUtility.HtmlEncode(item.Title);
                        string safeUrl = System.Web.HttpUtility.HtmlEncode(item.Url);
                        string safeTime = System.Web.HttpUtility.HtmlEncode(item.Timestamp);

                        sb.Append(@"
<a class='item' href='" + safeUrl + @"'>
  <div>
    <div class='item-title'>" + safeTitle + @"</div>
    <div class='item-url'>" + safeUrl + @"</div>
  </div>
  <div class='item-time'>" + safeTime + @"</div>
</a>");
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
