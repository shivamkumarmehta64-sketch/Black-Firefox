using System;
using System.Collections.Generic;
using System.Text;
using System.Web.Script.Serialization;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BlackBrowser
{
    public static class CookieManager
    {
        public static string GetCookiesHtml(bool isDarkMode, IReadOnlyList<CoreWebView2Cookie> cookies = null)
        {
            string bg = isDarkMode ? "#0b0d1b" : "#f5f6fa";
            string textColor = isDarkMode ? "#ffffff" : "#1a1d2e";
            string cardBg = isDarkMode ? "rgba(255, 255, 255, 0.05)" : "rgba(0, 0, 0, 0.04)";
            string border = isDarkMode ? "rgba(255, 255, 255, 0.1)" : "rgba(0, 0, 0, 0.12)";
            string muted = isDarkMode ? "#a2a6cc" : "#5b6078";
            string inputBg = isDarkMode ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.06)";

            StringBuilder rows = new StringBuilder();
            int count = cookies != null ? cookies.Count : 0;

            if (cookies != null && cookies.Count > 0)
            {
                foreach (var c in cookies)
                {
                    string rawName = c.Name ?? "";
                    string rawValue = c.Value ?? "";
                    if (rawValue.Length > 40) rawValue = rawValue.Substring(0, 40) + "…";
                    string expiry = (c.Expires.Year < 2000) ? "Session" : c.Expires.ToLocalTime().ToString("yyyy-MM-dd");
                    string rawFlags = (c.IsHttpOnly ? "HttpOnly " : "") + (c.IsSecure ? "Secure" : "");
                    string key = Convert.ToBase64String(Encoding.UTF8.GetBytes(rawName + "|" + (c.Domain ?? "") + "|" + (c.Path ?? "")));

                    string name = System.Web.HttpUtility.HtmlEncode(rawName);
                    string domain = System.Web.HttpUtility.HtmlEncode((c.Domain ?? "") + (c.Path ?? ""));
                    string flags = System.Web.HttpUtility.HtmlEncode(rawFlags);

                    rows.Append(@"
<div class='item' data-name='" + name.ToLowerInvariant() + @"' data-dom='" + domain.ToLowerInvariant() + @"'>
  <div class='item-info'>
    <div class='item-name'>🍪 " + name + @"</div>
    <div class='item-url'>" + domain + @"</div>
  </div>
  <div class='item-actions'>
    <div class='item-meta'>" + expiry + @"</div>
    " + (flags.Length > 0 ? "<div class='item-flags'>" + flags + "</div>" : "") + @"
    <button class='btn' onclick='delCookie(""" + key + @""")'>🗑️ Delete</button>
  </div>
</div>");
                }
            }
            else
            {
                rows.Append("<div class='empty'>No cookies stored. Browsing with a clean slate!</div>");
            }

            return @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<title>Local Cookies — Black Firefox</title>
<link href='https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;600;700&family=Inter:wght@400;500;600&display=swap' rel='stylesheet'>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:'Segoe UI Variable Display','Plus Jakarta Sans','Inter',sans-serif;background:" + bg + @";color:" + textColor + @";padding:36px 24px;max-width:880px;margin:0 auto;-webkit-font-smoothing:antialiased}
.header{display:flex;align-items:center;justify-content:space-between;margin-bottom:20px;padding-bottom:16px;border-bottom:1px solid " + border + @"}
.title{font-size:26px;font-weight:700;background:linear-gradient(135deg, #f59e0b, #ef4444);-webkit-background-clip:text;-webkit-text-fill-color:transparent}
.sub{font-size:13.5px;color:" + muted + @";margin-top:6px}
.search-box{width:100%;padding:12px 18px;border-radius:12px;border:1px solid " + border + @";background:" + inputBg + @";color:" + textColor + @";font-size:14px;margin-bottom:20px;outline:none;transition:border-color .2s}
.search-box:focus{border-color:#f59e0b}
.item{display:flex;align-items:center;justify-content:space-between;padding:16px 20px;background:" + cardBg + @";border:1px solid " + border + @";border-radius:14px;margin-bottom:12px;backdrop-filter:blur(16px);transition:all .2s ease}
.item:hover{border-color:#f59e0b;box-shadow:0 6px 20px rgba(245,158,11,0.15);transform:translateY(-1px)}
.item-info{flex:1}
.item-name{font-size:15px;font-weight:600;color:" + textColor + @";word-break:break-all}
.item-url{font-size:12.5px;color:" + muted + @";word-break:break-all;margin-top:4px}
.item-actions{display:flex;align-items:center;gap:12px;margin-left:14px;white-space:nowrap}
.item-meta{font-size:12px;color:#f59e0b;font-weight:600}
.item-flags{font-size:11px;color:" + muted + @";background:rgba(255,255,255,0.06);padding:2px 6px;border-radius:6px}
.btn{padding:7px 14px;border-radius:9px;border:1px solid rgba(239,68,68,0.3);background:rgba(239,68,68,0.1);color:#f87171;font-family:inherit;font-size:12px;font-weight:600;cursor:pointer;transition:all .2s ease}
.btn:hover{background:rgba(239,68,68,0.22);color:#fff}
.btn-clear{padding:9px 18px;border-radius:10px;border:1px solid rgba(239,68,68,0.4);background:rgba(239,68,68,0.1);color:#f87171;font-family:inherit;font-size:13px;font-weight:600;cursor:pointer;transition:all .2s ease}
.btn-clear:hover{background:rgba(239,68,68,0.22)}
.empty{text-align:center;padding:70px 0;color:" + muted + @";font-size:16px}
.count{font-size:13px;color:" + muted + @";margin-bottom:16px}
</style>
</head>
<body>

<script>
function clearAll(){ if(window.chrome && chrome.webview){ chrome.webview.postMessage('cookies:clear'); } }
function delCookie(k){ if(window.chrome && chrome.webview){ chrome.webview.postMessage('cookies:del:'+k); } }
function filterList(q){
  q = (q||'').toLowerCase();
  var items = document.querySelectorAll('.item');
  items.forEach(function(el){
    var name = el.getAttribute('data-name')||'';
    var dom = el.getAttribute('data-dom')||'';
    el.style.display = (name.indexOf(q) !== -1 || dom.indexOf(q) !== -1) ? 'flex' : 'none';
  });
}
</script>

<div class='header'>
  <div>
    <div class='title'>🍪 Local Cookie Manager</div>
    <div class='sub'>✨ Stored Locally on Device • Delete individual cookies or clear all session data</div>
  </div>
  <div><button class='btn-clear' onclick='clearAll()'>🗑️ Clear All Cookies</button></div>
</div>

<input type='text' id='search' class='search-box' placeholder='🔍 Search cookies by name or domain...' oninput='filterList(this.value)'>
<div id='count' class='count'>" + (count > 0 ? count + " cookie(s) stored" : "0 cookies stored") + @"</div>
<div id='rows'>
" + rows.ToString() + @"
</div>

</body>
</html>";
        }

        public static async void Render(WebView2 wv, bool isDarkMode = true)
        {
            try
            {
                if (wv == null || wv.CoreWebView2 == null) return;
                IReadOnlyList<CoreWebView2Cookie> cookies = await wv.CoreWebView2.CookieManager.GetCookiesAsync(null);
                wv.CoreWebView2.NavigateToString(GetCookiesHtml(isDarkMode, cookies));
            }
            catch { }
        }
    }
}