using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace BlackBrowser
{
    public class BookmarkItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string AddedDate { get; set; }
    }

    public static class BookmarksManager
    {
        private static string bookmarksFilePath;
        private static List<BookmarkItem> bookmarksList = new List<BookmarkItem>();
        private static readonly object fileLock = new object();

        static BookmarksManager()
        {
            bookmarksFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "black-webview2", "bookmarks.json");
            LoadBookmarks();
        }

        private static void LoadBookmarks()
        {
            lock (fileLock)
            {
                try
                {
                    if (File.Exists(bookmarksFilePath))
                    {
                        string json = File.ReadAllText(bookmarksFilePath, Encoding.UTF8);
                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        bookmarksList = serializer.Deserialize<List<BookmarkItem>>(json) ?? new List<BookmarkItem>();
                    }
                    else
                    {
                        bookmarksList = new List<BookmarkItem>
                        {
                            new BookmarkItem
                            {
                                Title = "Black Browser",
                                Url = "https://shivamkumarmehta64-sketch.github.io/Black-Firefox/",
                                AddedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                            },
                            new BookmarkItem
                            {
                                Title = "Shivam's Apps",
                                Url = "https://github.com/shivamkumarmehta64-sketch",
                                AddedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                            }
                        };
                        SaveBookmarks();
                    }
                }
                catch
                {
                    bookmarksList = new List<BookmarkItem>();
                }
            }
        }

        public static bool IsBookmarked(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            lock (fileLock)
            {
                return bookmarksList.Exists(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
            }
        }

        public static List<BookmarkItem> GetBookmarks()
        {
            lock (fileLock)
            {
                return new List<BookmarkItem>(bookmarksList);
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

        public static bool ToggleBookmark(string title, string url)
        {
            if (!IsSafeUrl(url))
                return false;

            lock (fileLock)
            {
                int index = bookmarksList.FindIndex(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    bookmarksList.RemoveAt(index);
                    SaveBookmarks();
                    return false;
                }
                else
                {
                    bookmarksList.Insert(0, new BookmarkItem
                    {
                        Title = string.IsNullOrWhiteSpace(title) ? url : title,
                        Url = url,
                        AddedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                    });
                    SaveBookmarks();
                    return true;
                }
            }
        }

        public static List<BookmarkItem> Search(string query, int maxResults)
        {
            List<BookmarkItem> results = new List<BookmarkItem>();
            if (string.IsNullOrWhiteSpace(query)) return results;

            string q = query.ToLowerInvariant();
            lock (fileLock)
            {
                foreach (var item in bookmarksList)
                {
                    if (results.Count >= maxResults) break;
                    string titleLow = (item.Title ?? "").ToLowerInvariant();
                    string urlLow = (item.Url ?? "").ToLowerInvariant();
                    if (titleLow.Contains(q) || urlLow.Contains(q))
                    {
                        results.Add(item);
                    }
                }
            }
            return results;
        }

        private static void SaveBookmarks()
        {
            try
            {
                string dir = Path.GetDirectoryName(bookmarksFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                JavaScriptSerializer serializer = new JavaScriptSerializer();
                string json = serializer.Serialize(bookmarksList);
                File.WriteAllText(bookmarksFilePath, json, Encoding.UTF8);
            }
            catch { }
        }

        public static void ExportHtml(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE NETSCAPE-Bookmark-file-1>");
            sb.AppendLine("<META HTTP-EQUIV=\"Content-Type\" CONTENT=\"text/html; charset=UTF-8\">");
            sb.AppendLine("<TITLE>Bookmarks</TITLE>");
            sb.AppendLine("<H1>Bookmarks</H1>");
            sb.AppendLine("<DL><p>");
            lock (fileLock)
            {
                foreach (var b in bookmarksList)
                {
                    string t = System.Web.HttpUtility.HtmlEncode(b.Title);
                    string u = System.Web.HttpUtility.HtmlEncode(b.Url);
                    sb.AppendLine("    <DT><A HREF=\"" + u + "\">" + t + "</A>");
                }
            }
            sb.AppendLine("</DL><p>");
            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        public static int ImportHtml(string path)
        {
            if (!File.Exists(path)) return 0;
            string html = File.ReadAllText(path, Encoding.UTF8);
            var rx = new System.Text.RegularExpressions.Regex(
                "<A\\b[^>]*?\\bHREF\\s*=\\s*\"([^\"]+)\"[^>]*>([^<]*)</A>",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            int count = 0;
            lock (fileLock)
            {
                foreach (System.Text.RegularExpressions.Match m in rx.Matches(html))
                {
                    string url = System.Web.HttpUtility.HtmlDecode(m.Groups[1].Value);
                    string title = System.Web.HttpUtility.HtmlDecode(m.Groups[2].Value);
                    if (!IsSafeUrl(url))
                        continue;
                    if (bookmarksList.Exists(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    bookmarksList.Insert(0, new BookmarkItem
                    {
                        Title = string.IsNullOrWhiteSpace(title) ? url : title,
                        Url = url,
                        AddedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                    });
                    count++;
                }
                if (count > 0) SaveBookmarks();
            }
            return count;
        }

        public static bool RemoveBookmark(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            lock (fileLock)
            {
                int index = bookmarksList.FindIndex(b => b.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
                if (index >= 0)
                {
                    bookmarksList.RemoveAt(index);
                    SaveBookmarks();
                    return true;
                }
                return false;
            }
        }

        public static string GetBookmarksHtml(bool isDarkMode)
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
<title>Local Bookmarks — Black Browser</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:'Segoe UI Variable Display','Segoe UI',system-ui,-apple-system,sans-serif;background:" + bg + @";color:" + textColor + @";padding:36px 24px;max-width:880px;margin:0 auto;-webkit-font-smoothing:antialiased}
.header{display:flex;align-items:center;justify-content:space-between;margin-bottom:24px;padding-bottom:16px;border-bottom:1px solid " + border + @"}
.title{font-size:26px;font-weight:700;background:linear-gradient(135deg, #f59e0b, #ec4899);-webkit-background-clip:text;-webkit-text-fill-color:transparent}
.sub{font-size:13.5px;color:" + muted + @";margin-top:6px}
.header-actions{display:flex;gap:10px}
.btn-action{padding:8px 16px;border-radius:10px;border:1px solid " + border + @";background:rgba(245,158,11,0.12);color:#f59e0b;font-size:12.5px;font-weight:600;cursor:pointer;text-decoration:none;transition:all .2s ease;display:inline-flex;align-items:center;gap:6px}
.btn-action:hover{background:rgba(245,158,11,0.25);transform:translateY(-1px)}
.search-box{width:100%;padding:12px 18px;border-radius:12px;border:1px solid " + border + @";background:" + inputBg + @";color:" + textColor + @";font-size:14px;margin-bottom:24px;outline:none;transition:border-color .2s}
.search-box:focus{border-color:#f59e0b}
.item{display:flex;align-items:center;justify-content:space-between;padding:16px 20px;background:" + cardBg + @";border:1px solid " + border + @";border-radius:16px;margin-bottom:14px;text-decoration:none;color:inherit;backdrop-filter:blur(16px);transition:all .2s ease}
.item:hover{transform:translateY(-2px);border-color:#f59e0b;box-shadow:0 8px 24px rgba(245,158,11,0.2)}
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
function removeBm(btn){
  var item = btn.closest('.item');
  if(!item) return;
  var url = item.getAttribute('data-rawurl') || '';
  var title = item.getAttribute('data-rawtitle') || '';
  if(confirm('Remove bookmark: ' + title + '?')){
    location.href = 'black://remove-bookmark?url=' + encodeURIComponent(url);
  }
}
</script>

<div class='header'>
  <div>
    <div class='title'>⭐ Local Bookmarks & Saved Sites</div>
    <div class='sub'>✨ Stored 100% Locally on Device • Zero Cloud Telemetry</div>
  </div>
  <div class='header-actions'>
    <a class='btn-action' href='black://export-bookmarks'>📤 Export HTML</a>
    <a class='btn-action' href='black://import-bookmarks'>📥 Import HTML</a>
  </div>
</div>

<input type='text' class='search-box' placeholder='🔍 Search saved bookmarks by title or URL...' oninput='filterList(this.value)'>

<div id='list'>");

            lock (fileLock)
            {
                if (bookmarksList.Count == 0)
                {
                    sb.Append("<div class='empty'>No saved bookmarks yet. Click the ⭐ star button on any web page to bookmark it!</div>");
                }
                else
                {
                    foreach (var item in bookmarksList)
                    {
                        string rawUrl = IsSafeUrl(item.Url) ? item.Url : "#";
                        string safeTitle = System.Web.HttpUtility.HtmlEncode(item.Title ?? "");
                        string safeUrl = System.Web.HttpUtility.HtmlEncode(rawUrl);
                        string safeTime = System.Web.HttpUtility.HtmlEncode(item.AddedDate ?? "");

                        sb.Append(@"
<div class='item' data-title='" + safeTitle.ToLowerInvariant() + @"' data-url='" + safeUrl.ToLowerInvariant() + @"' data-rawtitle='" + safeTitle + @"' data-rawurl='" + safeUrl + @"'>
  <a class='item-info' href='" + safeUrl + @"' style='text-decoration:none;'>
    <div class='item-title'>⭐ " + safeTitle + @"</div>
    <div class='item-url'>" + safeUrl + @"</div>
  </a>
  <div class='item-side'>
    <div class='item-time'>" + safeTime + @"</div>
    <button type='button' class='btn-del' onclick='removeBm(this)'>🗑️ Remove</button>
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
