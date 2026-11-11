using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
                        string raw = File.ReadAllText(historyFilePath, Encoding.UTF8);
                        string json = raw;
                        string decrypted = TryUnprotect(raw);
                        if (!string.IsNullOrEmpty(decrypted)) json = decrypted;
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

        public static List<HistoryItem> GetHistoryItems()
        {
            lock (fileLock)
            {
                return new List<HistoryItem>(historyList);
            }
        }

        public static bool IsSafeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url) || url == "about:blank") return false;
            string u = url.Trim();
            if (u.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
                u.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                u.StartsWith("vbscript:", StringComparison.OrdinalIgnoreCase) ||
                u.StartsWith("black://", StringComparison.OrdinalIgnoreCase) ||
                u.StartsWith("https://black.internal", StringComparison.OrdinalIgnoreCase))
                return false;
            return u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   u.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   u.StartsWith("file://", StringComparison.OrdinalIgnoreCase);
        }

        public static void AddVisit(string title, string url)
        {
            if (!IsSafeUrl(url))
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

        public static List<HistoryItem> Search(string query, int maxResults)
        {
            List<HistoryItem> results = new List<HistoryItem>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            string q = query.ToLowerInvariant();
            lock (fileLock)
            {
                // Exact prefix matches first, then substring matches
                HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // Pass 1: URL starts with query or title starts with query
                foreach (var item in historyList)
                {
                    if (results.Count >= maxResults) break;
                    string titleLow = (item.Title ?? "").ToLowerInvariant();
                    string urlLow = (item.Url ?? "").ToLowerInvariant();
                    // Strip protocol for prefix matching
                    string urlNoProto = urlLow.Replace("https://", "").Replace("http://", "").Replace("www.", "");
                    if (titleLow.StartsWith(q) || urlNoProto.StartsWith(q))
                    {
                        if (seen.Add(item.Url))
                            results.Add(item);
                    }
                }

                // Pass 2: Substring matches
                foreach (var item in historyList)
                {
                    if (results.Count >= maxResults) break;
                    string titleLow = (item.Title ?? "").ToLowerInvariant();
                    string urlLow = (item.Url ?? "").ToLowerInvariant();
                    if ((titleLow.Contains(q) || urlLow.Contains(q)) && seen.Add(item.Url))
                    {
                        results.Add(item);
                    }
                }
            }
            return results;
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

        private static string TryUnprotect(string protectedText)
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

        private static void SaveHistory()
        {
            try
            {
                string dir = Path.GetDirectoryName(historyFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(historyList);
                File.WriteAllText(historyFilePath, ProtectString(json), Encoding.UTF8);
            }
            catch { }
        }

        public static bool RemoveHistoryItem(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            lock (fileLock)
            {
                int count = historyList.RemoveAll(h => h.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
                if (count > 0)
                {
                    SaveHistory();
                    return true;
                }
                return false;
            }
        }

        public static string GetHistoryHtml(bool isDarkMode)
        {
            string bg = isDarkMode ? "#0b0d1b" : "#f5f6fa";
            string textColor = isDarkMode ? "#ffffff" : "#1a1d2e";
            string cardBg = isDarkMode ? "rgba(255, 255, 255, 0.05)" : "rgba(0, 0, 0, 0.04)";
            string border = isDarkMode ? "rgba(255, 255, 255, 0.1)" : "rgba(0, 0, 0, 0.12)";
            string muted = isDarkMode ? "#a2a6cc" : "#5b6078";
            string inputBg = isDarkMode ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.06)";

            StringBuilder sb = new StringBuilder();
            sb.Append(@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<title>Local Browsing History — Black Firefox</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:'Segoe UI Variable Display','Segoe UI',system-ui,-apple-system,sans-serif;background:" + bg + @";color:" + textColor + @";padding:36px 24px;max-width:880px;margin:0 auto;-webkit-font-smoothing:antialiased}
.header{display:flex;align-items:center;justify-content:space-between;margin-bottom:24px;padding-bottom:16px;border-bottom:1px solid " + border + @"}
.title{font-size:26px;font-weight:700;background:linear-gradient(135deg, #06b6d4, #9333ea);-webkit-background-clip:text;-webkit-text-fill-color:transparent}
.sub{font-size:13.5px;color:" + muted + @";margin-top:6px}
.btn-clear{padding:8px 16px;border-radius:10px;border:1px solid rgba(239,68,68,0.4);background:rgba(239,68,68,0.1);color:#f87171;font-size:12.5px;font-weight:600;cursor:pointer;transition:all .2s ease}
.btn-clear:hover{background:rgba(239,68,68,0.22);color:#fff}
.search-box{width:100%;padding:12px 18px;border-radius:12px;border:1px solid " + border + @";background:" + inputBg + @";color:" + textColor + @";font-size:14px;margin-bottom:24px;outline:none;transition:border-color .2s}
.search-box:focus{border-color:#06b6d4}
.item{display:flex;align-items:center;justify-content:space-between;padding:16px 20px;background:" + cardBg + @";border:1px solid " + border + @";border-radius:16px;margin-bottom:14px;text-decoration:none;color:inherit;backdrop-filter:blur(16px);transition:all .2s ease}
.item:hover{transform:translateY(-2px);border-color:#06b6d4;box-shadow:0 8px 24px rgba(6,182,212,0.2)}
.item-info{flex:1;overflow:hidden;padding-right:16px}
.item-title{font-size:15.5px;font-weight:600;margin-bottom:4px;color:" + textColor + @";white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.item-url{font-size:13px;color:#06b6d4;word-break:break-all}
.item-side{display:flex;align-items:center;gap:12px;white-space:nowrap}
.item-time{font-size:12px;color:" + muted + @";font-weight:500}
.btn-del{padding:6px 12px;border-radius:8px;border:1px solid rgba(239,68,68,0.3);background:rgba(239,68,68,0.1);color:#f87171;font-size:12px;font-weight:600;cursor:pointer;transition:all .2s}
.btn-del:hover{background:rgba(239,68,68,0.25);color:#fff}
.empty{text-align:center;padding:70px 0;color:" + muted + @";font-size:16px}
</style>
</head>
<body>

<script>
function filterList(q){
  q = (q||'').toLowerCase();
  var items = document.querySelectorAll('.item');
  items.forEach(function(el){
    var title = el.getAttribute('data-title')||'';
    var url = el.getAttribute('data-url')||'';
    el.style.display = (title.indexOf(q) !== -1 || url.indexOf(q) !== -1) ? 'flex' : 'none';
  });
}
function removeHist(btn){
  var item = btn.closest('.item');
  if(!item) return;
  var url = item.getAttribute('data-rawurl') || '';
  if(confirm('Remove this entry from history?')){
    location.href = 'black://remove-history?url=' + encodeURIComponent(url);
  }
}
function clearAllHist(){
  if(confirm('Clear all browsing history?')){
    location.href = 'black://clear-history';
  }
}
</script>

<div class='header'>
  <div>
    <div class='title'>📜 Local Browsing History</div>
    <div class='sub'>✨ Anime Cyberpunk Theme • 100% Private • Stored Locally on Device</div>
  </div>
  <div><button type='button' class='btn-clear' onclick='clearAllHist()'>🗑️ Clear All</button></div>
</div>

<input type='text' class='search-box' placeholder='🔍 Search browsing history by title or URL...' oninput='filterList(this.value)'>

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
                        string rawUrl = IsSafeUrl(item.Url) ? item.Url : "#";
                        string safeTitle = System.Web.HttpUtility.HtmlEncode(item.Title ?? "");
                        string safeUrl = System.Web.HttpUtility.HtmlEncode(rawUrl);
                        string safeTime = System.Web.HttpUtility.HtmlEncode(item.Timestamp ?? "");

                        sb.Append(@"
<div class='item' data-title='" + safeTitle.ToLowerInvariant() + @"' data-url='" + safeUrl.ToLowerInvariant() + @"' data-rawurl='" + safeUrl + @"'>
  <a class='item-info' href='" + safeUrl + @"' style='text-decoration:none;'>
    <div class='item-title'>📜 " + safeTitle + @"</div>
    <div class='item-url'>" + safeUrl + @"</div>
  </a>
  <div class='item-side'>
    <div class='item-time'>" + safeTime + @"</div>
    <button type='button' class='btn-del' onclick='removeHist(this)'>🗑️ Remove</button>
  </div>
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
