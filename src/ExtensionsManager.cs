using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace BlackBrowser
{
    public static class ExtensionsManager
    {
        private static string SimpleHtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c == '<') sb.Append("&lt;");
                else if (c == '>') sb.Append("&gt;");
                else if (c == '&') sb.Append("&amp;");
                else if (c == '"') sb.Append("&quot;");
                else if (c == '\'') sb.Append("&#39;");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        public static string GetExtensionsHtml(bool isDarkMode)
        {
            string bg = isDarkMode ? "#121216" : "#f3f5fa";
            string cardBg = isDarkMode ? "#1e1e24" : "#ffffff";
            string cardBorder = isDarkMode ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.08)";
            string textColor = isDarkMode ? "#ffffff" : "#1d1d21";
            string subTextColor = isDarkMode ? "#9a9ab0" : "#6e6e82";
            string devBarBg = isDarkMode ? "#18181f" : "#e9ecf5";

            StringBuilder listSb = new StringBuilder();
            int totalExts = 0;

            try
            {
                string extBaseDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "black-webview2", "Extensions");

                if (Directory.Exists(extBaseDir))
                {
                    string[] dirs = Directory.GetDirectories(extBaseDir);
                    totalExts = dirs.Length;

                    foreach (string dir in dirs)
                    {
                        string folderName = Path.GetFileName(dir);
                        string manifestPath = Path.Combine(dir, "manifest.json");

                        string name = folderName;
                        string version = "1.0";
                        string description = "Unpacked Chrome / Edge Extension";

                        if (File.Exists(manifestPath))
                        {
                            string json = File.ReadAllText(manifestPath);
                            var nameMatch = Regex.Match(json, @"""name""\s*:\s*""([^""]+)""");
                            if (nameMatch.Success) name = nameMatch.Groups[1].Value;

                            var verMatch = Regex.Match(json, @"""version""\s*:\s*""([^""]+)""");
                            if (verMatch.Success) version = verMatch.Groups[1].Value;

                            var descMatch = Regex.Match(json, @"""description""\s*:\s*""([^""]+)""");
                            if (descMatch.Success) description = descMatch.Groups[1].Value;
                        }

                        string safeName = SimpleHtmlEncode(name);
                        string safeVer = SimpleHtmlEncode(version);
                        string safeDesc = SimpleHtmlEncode(description);
                        string safeId = SimpleHtmlEncode(folderName);

                        listSb.Append(@"
  <div class='brave-card' data-name='" + safeName.ToLower() + @"'>
    <div class='card-header'>
      <div class='ext-icon'>🧩</div>
      <div class='ext-meta'>
        <div class='ext-name'>" + safeName + @" <span class='ext-ver'>v" + safeVer + @"</span></div>
        <div class='ext-desc'>" + safeDesc + @"</div>
        <div class='ext-id'>ID: " + safeId + @"</div>
      </div>
    </div>
    <div class='card-footer'>
      <a class='btn-remove' href='black://extensions?action=remove&id=" + Uri.EscapeDataString(folderName) + @"'>Remove</a>
      <div class='toggle-wrapper'>
        <span class='toggle-label'>Enabled</span>
        <label class='switch'>
          <input type='checkbox' checked onchange='location.href=""black://extensions?action=toggle&id=" + Uri.EscapeDataString(folderName) + @"""'>
          <span class='slider round'></span>
        </label>
      </div>
    </div>
  </div>");
                    }
                }
            }
            catch { }

            if (totalExts == 0)
            {
                listSb.Append(@"
  <div class='empty-state'>
    <div class='empty-icon'>🧩</div>
    <div class='empty-title'>No Extensions Installed Yet</div>
    <div class='empty-desc'>Install extensions automatically from web stores, or load unpacked extension folders from your computer using Developer Mode.</div>
    <div class='store-btns'>
      <a class='store-btn primary' href='black://extensions?action=load_unpacked'>📁 Load Unpacked Extension</a>
      <a class='store-btn' href='https://chromewebstore.google.com'>🛒 Chrome Web Store</a>
      <a class='store-btn' href='https://microsoftedge.microsoft.com/addons'>🧩 Edge Add-ons Store</a>
    </div>
  </div>");
            }

            return @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<link href='https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700&family=Inter:wght@400;500;600&display=swap' rel='stylesheet'>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:'Segoe UI Variable Display','Plus Jakarta Sans','Inter',sans-serif;background:" + bg + @";color:" + textColor + @";padding:28px 36px;max-width:1200px;margin:0 auto;-webkit-font-smoothing:antialiased}

.top-bar{display:flex;align-items:center;justify-content:space-between;margin-bottom:20px;padding-bottom:16px;border-bottom:1px solid " + cardBorder + @"}
.title-group{display:flex;align-items:center;gap:12px}
.page-title{font-size:26px;font-weight:700;letter-spacing:-0.5px}
.badge-count{background:rgba(0,103,192,0.15);color:#0067c0;padding:3px 10px;border-radius:12px;font-size:13px;font-weight:600}

.dev-mode-group{display:flex;align-items:center;gap:10px;background:" + devBarBg + @";padding:8px 18px;border-radius:20px;border:1px solid " + cardBorder + @"}
.dev-mode-label{font-size:13.5px;font-weight:600;color:" + textColor + @"}

.install-panel{display:flex;flex-direction:column;gap:10px;margin-bottom:20px;background:" + devBarBg + @";padding:16px 20px;border-radius:16px;border:1px solid " + cardBorder + @"}
.install-title{font-size:15px;font-weight:700;color:" + textColor + @";display:flex;align-items:center;gap:8px}
.install-row{display:flex;gap:10px;flex-wrap:wrap}
.install-row input{flex:1;min-width:260px;height:40px;padding:0 16px;border-radius:20px;border:1px solid " + cardBorder + @";background:" + cardBg + @";color:" + textColor + @";font-size:13.5px;outline:none}
.install-row input:focus{border-color:#0067c0}
.install-row button{height:40px;padding:0 24px;border-radius:20px;background:linear-gradient(135deg,#0067c0,#1a73e8);color:#fff;border:none;font-size:13.5px;font-weight:700;cursor:pointer;transition:all .15s ease;box-shadow:0 4px 12px rgba(0,103,192,0.3)}
.install-row button:hover{transform:translateY(-1px);box-shadow:0 6px 18px rgba(0,103,192,0.4)}
.install-hint{font-size:12px;color:" + subTextColor + @";line-height:1.5}

.dev-toolbar{display:flex;align-items:center;gap:12px;margin-bottom:24px;background:" + devBarBg + @";padding:12px 20px;border-radius:16px;border:1px solid " + cardBorder + @";flex-wrap:wrap}
.action-btn{padding:8px 18px;border-radius:18px;background:#0067c0;color:#fff;text-decoration:none;font-size:13.5px;font-weight:600;display:inline-flex;align-items:center;gap:6px;transition:all .15s ease;box-shadow:0 4px 12px rgba(0,103,192,0.25)}
.action-btn:hover{transform:translateY(-1px);box-shadow:0 6px 18px rgba(0,103,192,0.35)}
.action-btn.secondary{background:transparent;color:" + textColor + @";border:1px solid " + cardBorder + @";box-shadow:none}
.action-btn.secondary:hover{border-color:#0067c0;color:#0067c0}

.search-box{flex:1;min-width:240px;max-width:360px;position:relative}
.search-box input{width:100%;height:38px;padding:0 16px 0 38px;border-radius:19px;border:1px solid " + cardBorder + @";background:" + cardBg + @";color:" + textColor + @";font-size:13.5px;outline:none}
.search-box input:focus{border-color:#0067c0}
.search-icon{position:absolute;left:14px;top:10px;color:" + subTextColor + @";font-size:14px}

.dials-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(420px,1fr));gap:20px}
.brave-card{background:" + cardBg + @";border:1px solid " + cardBorder + @";border-radius:18px;padding:20px;display:flex;flex-direction:column;justify-content:space-between;box-shadow:0 4px 20px rgba(0,0,0,0.03);transition:all .2s ease}
.brave-card:hover{transform:translateY(-2px);border-color:#0067c0;box-shadow:0 8px 30px rgba(0,103,192,0.12)}
.card-header{display:flex;gap:16px;align-items:flex-start}
.ext-icon{width:48px;height:48px;border-radius:14px;background:rgba(0,103,192,0.12);color:#0067c0;display:flex;align-items:center;justify-content:center;font-size:24px;flex-shrink:0}
.ext-meta{flex:1;min-width:0}
.ext-name{font-size:16px;font-weight:600;color:" + textColor + @";display:flex;align-items:center;gap:8px}
.ext-ver{font-size:12px;color:" + subTextColor + @";font-weight:400}
.ext-desc{font-size:13px;color:" + subTextColor + @";margin-top:4px;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;line-height:1.4}
.ext-id{font-size:11px;color:" + subTextColor + @";opacity:0.6;margin-top:6px;font-family:monospace}

.card-footer{display:flex;align-items:center;justify-content:space-between;margin-top:20px;padding-top:14px;border-top:1px solid " + cardBorder + @"}
.btn-remove{padding:6px 14px;border-radius:12px;background:rgba(220,53,69,0.1);color:#dc3545;text-decoration:none;font-size:12.5px;font-weight:600;transition:all .15s}
.btn-remove:hover{background:#dc3545;color:#fff}
.toggle-wrapper{display:flex;align-items:center;gap:8px}
.toggle-label{font-size:12.5px;color:" + subTextColor + @";font-weight:600}

.switch{position:relative;display:inline-block;width:44px;height:24px}
.switch input{opacity:0;width:0;height:0}
.slider{position:absolute;cursor:pointer;top:0;left:0;right:0;bottom:0;background-color:#ccc;transition:.3s;border-radius:24px}
.slider:before{position:absolute;content:'';height:18px;width:18px;left:3px;bottom:3px;background-color:white;transition:.3s;border-radius:50%}
input:checked + .slider{background-color:#0067c0}
input:checked + .slider:before{transform:translateX(20px)}

.empty-state{text-align:center;padding:70px 20px;background:" + cardBg + @";border-radius:24px;border:1px solid " + cardBorder + @";grid-column:1/-1}
.empty-icon{font-size:60px;margin-bottom:16px}
.empty-title{font-size:22px;font-weight:600;margin-bottom:8px}
.empty-desc{font-size:14.5px;color:" + subTextColor + @";max-width:520px;margin:0 auto 26px;line-height:1.5}
.store-btns{display:flex;justify-content:center;gap:14px;flex-wrap:wrap}
.store-btn{padding:11px 24px;border-radius:20px;background:rgba(0,103,192,0.1);color:#0067c0;text-decoration:none;font-size:14px;font-weight:600;transition:all .15s}
.store-btn.primary{background:#0067c0;color:#fff;box-shadow:0 4px 14px rgba(0,103,192,0.3)}
.store-btn:hover{transform:scale(1.04)}
</style>
</head>
<body>

<div class='top-bar'>
  <div class='title-group'>
    <div class='page-title'>🧩 Extensions</div>
    <span class='badge-count'>" + totalExts + @" Installed</span>
  </div>
  <div class='dev-mode-group'>
    <span class='dev-mode-label'>Developer mode</span>
    <label class='switch'>
      <input type='checkbox' id='devToggle' checked onchange='toggleDevBar(this.checked)'>
      <span class='slider round'></span>
    </label>
  </div>
</div>

<div class='install-panel' id='installPanel'>
  <div class='install-title'>🔗 Install Extension from Store Link</div>
  <div class='install-row'>
    <input type='text' id='installUrl' placeholder='Paste Chrome Web Store / Edge Add-ons link, or extension ID...'>
    <button onclick='installFromLink()'>⚡ Install</button>
  </div>
  <div class='install-hint'>Works with links like <b>chromewebstore.google.com/detail/.../cjpalhdlnbpafiamejdnhcphjbkeiagm</b> or <b>microsoftedge.microsoft.com/addons/detail/.../elhekieabhbkpmcefcoobjddigjcaadp</b></div>
</div>

<div class='dev-toolbar' id='devToolbar'>
  <a class='action-btn' href='black://extensions?action=load_unpacked'>📁 Load unpacked</a>
  <a class='action-btn secondary' href='black://extensions'>🔄 Update</a>
  <div class='search-box'>
    <span class='search-icon'>🔍</span>
    <input type='text' id='searchInput' placeholder='Search extensions...' oninput='filterExts(this.value)'>
  </div>
</div>

<div class='dials-grid' id='extGrid'>
" + listSb.ToString() + @"
</div>

<script>
function toggleDevBar(show) {
  document.getElementById('devToolbar').style.display = show ? 'flex' : 'none';
}

function filterExts(query) {
  var q = query.toLowerCase().trim();
  var cards = document.querySelectorAll('.brave-card');
  cards.forEach(function(card) {
    var name = card.getAttribute('data-name') || '';
    if (name.includes(q)) {
      card.style.display = 'flex';
    } else {
      card.style.display = 'none';
    }
  });
}

function installFromLink() {
  var val = (document.getElementById('installUrl').value || '').trim();
  if (!val) { alert('Please paste a Chrome Web Store or Edge Add-ons link, or an extension ID.'); return; }
  location.href = 'black://extensions?action=install&src=' + encodeURIComponent(val);
}
</script>

</body>
</html>";
        }
    }
}
