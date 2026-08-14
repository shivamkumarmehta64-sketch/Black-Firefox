using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace BlackBrowser
{
    public static class SpeedDialPage
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

        private static string GetFaviconUrl(string url)
        {
            try
            {
                Uri u = new Uri(url.StartsWith("http") ? url : "https://" + url);
                string host = u.Host;
                if (host.StartsWith("www.")) host = host.Substring(4);
                return "https://www.google.com/s2/favicons?domain=" + host + "&sz=128";
            }
            catch { return ""; }
        }

        public static string GetSpeedDialFilePath(bool isDarkMode)
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "black-webview2");

            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

            string filePath = Path.Combine(folder, "speeddial.html");

            StringBuilder dialsSb = new StringBuilder();
            try
            {
                var dials = CustomDialsManager.GetDials();
                if (dials != null && dials.Count > 0)
                {
                    foreach (var d in dials)
                    {
                        string safeTitle = SimpleHtmlEncode(d.Title ?? "App");
                        string safeUrl = SimpleHtmlEncode(d.Url ?? "https://google.com");
                        string safeIcon = SimpleHtmlEncode(d.IconText ?? "G");
                        string safeBg = SimpleHtmlEncode(d.BgColor ?? "#1a73e8");
                        string favicon = GetFaviconUrl(d.Url ?? "");

                        string iconBlock = "";
                        if (favicon != "")
                        {
                            iconBlock = @"
    <div class='dial-icon' style='background:" + safeBg + @"'>
      <span class='dial-fallback'>" + safeIcon + @"</span>
      <img class='dial-img' src='" + favicon + @"' alt='' onerror='this.style.display=&quot;none&quot;'>
    </div>";
                        }
                        else
                        {
                            iconBlock = @"
    <div class='dial-icon' style='background:" + safeBg + @"'>
      <span class='dial-fallback'>" + safeIcon + @"</span>
    </div>";
                        }

                        dialsSb.Append(@"
  <a class='dial' href='" + safeUrl + @"' title='" + safeTitle + @"'>" + iconBlock + @"
    <div class='dial-label'>" + safeTitle + @"</div>
    <span class='dial-remove' onclick='event.preventDefault();event.stopPropagation();location.href=&quot;black://removedial?url=&quot;+encodeURIComponent('" + SimpleHtmlEncode(d.Url) + @"');'>✕</span>
  </a>");
                    }
                }
            }
            catch { }

            if (dialsSb.Length == 0)
            {
                dialsSb.Append(@"
  <a class='dial' href='https://www.google.com' title='Google'><div class='dial-icon' style='background:#e8f0fe'><span class='dial-fallback'>G</span><img class='dial-img' src='https://www.google.com/s2/favicons?domain=google.com&sz=128' alt='' onerror='this.style.display=&quot;none&quot;'></div><div class='dial-label'>Google</div></a>
  <a class='dial' href='https://gemini.google.com' title='Gemini'><div class='dial-icon' style='background:#e8f0fe'><span class='dial-fallback'>AI</span><img class='dial-img' src='https://www.google.com/s2/favicons?domain=gemini.google.com&sz=128' alt='' onerror='this.style.display=&quot;none&quot;'></div><div class='dial-label'>Gemini</div></a>
  <a class='dial' href='https://www.youtube.com' title='YouTube'><div class='dial-icon' style='background:#fce8e6'><span class='dial-fallback'>Y</span><img class='dial-img' src='https://www.google.com/s2/favicons?domain=youtube.com&sz=128' alt='' onerror='this.style.display=&quot;none&quot;'></div><div class='dial-label'>YouTube</div></a>
  <a class='dial' href='https://music.youtube.com' title='YT Music'><div class='dial-icon' style='background:#fef7e0'><span class='dial-fallback'>M</span><img class='dial-img' src='https://www.google.com/s2/favicons?domain=music.youtube.com&sz=128' alt='' onerror='this.style.display=&quot;none&quot;'></div><div class='dial-label'>YT Music</div></a>
  <a class='dial' href='https://github.com' title='GitHub'><div class='dial-icon' style='background:#e8eaed'><span class='dial-fallback'>GH</span><img class='dial-img' src='https://www.google.com/s2/favicons?domain=github.com&sz=128' alt='' onerror='this.style.display=&quot;none&quot;'></div><div class='dial-label'>GitHub</div></a>
  <a class='dial' href='https://chatgpt.com' title='ChatGPT'><div class='dial-icon' style='background:#e6f4ea'><span class='dial-fallback'>AI</span><img class='dial-img' src='https://www.google.com/s2/favicons?domain=chatgpt.com&sz=128' alt='' onerror='this.style.display=&quot;none&quot;'></div><div class='dial-label'>ChatGPT</div></a>
  <a class='dial' href='https://mail.google.com' title='Gmail'><div class='dial-icon' style='background:#fce8e6'><span class='dial-fallback'>@</span><img class='dial-img' src='https://www.google.com/s2/favicons?domain=mail.google.com&sz=128' alt='' onerror='this.style.display=&quot;none&quot;'></div><div class='dial-label'>Gmail</div></a>
  <a class='dial' href='https://www.google.com/maps' title='Maps'><div class='dial-icon' style='background:#e6f4ea'><span class='dial-fallback'>MAP</span><img class='dial-img' src='https://www.google.com/s2/favicons?domain=google.com/maps&sz=128' alt='' onerror='this.style.display=&quot;none&quot;'></div><div class='dial-label'>Maps</div></a>");
            }

            // Always append the "+ Add shortcut" tile
            dialsSb.Append(@"
  <a class='dial dial-add' href='black://adddial' title='Add your own shortcut'>
    <div class='dial-icon dial-add-icon'><span class='dial-fallback'>+</span></div>
    <div class='dial-label'>Add Shortcut</div>
  </a>");

            string html = @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<link rel='preconnect' href='https://fonts.googleapis.com'>
<link rel='preconnect' href='https://fonts.gstatic.com' crossorigin>
<link href='https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@300;400;500;600;700&family=Outfit:wght@300;400;500;600;700&family=Inter:wght@400;500;600&display=swap' rel='stylesheet'>
<style>
*{margin:0;padding:0;box-sizing:border-box}
html,body{height:100%}
body{background:linear-gradient(160deg, #0b0d14 0%, #11151f 45%, #0d1020 100%);background-attachment:fixed;font-family:'Inter','Segoe UI Variable Display','Plus Jakarta Sans',sans-serif;color:#ffffff;display:flex;flex-direction:column;align-items:center;min-height:100vh;padding:40px 24px 48px;overflow-x:hidden;-webkit-font-smoothing:antialiased;position:relative}

/* Aurora glow layers */
body::before{content:'';position:fixed;inset:0;z-index:-1;pointer-events:none;background:
 radial-gradient(1100px 700px at 12% 8%, rgba(0,96,223,0.30), transparent 62%),
 radial-gradient(900px 650px at 88% 16%, rgba(124,77,255,0.24), transparent 60%),
 radial-gradient(1000px 900px at 50% 108%, rgba(0,180,216,0.16), transparent 62%);
 filter:blur(2px)}
body::after{content:'';position:fixed;inset:0;z-index:-1;pointer-events:none;background:radial-gradient(90% 90% at 50% 40%, transparent 55%, rgba(6,8,14,0.6) 100%)}

.ntp-header{text-align:center;margin-bottom:26px;animation:fadeIn 0.5s ease}
.ntp-clock{font-size:34px;font-weight:300;letter-spacing:-1px;background:linear-gradient(135deg, #6cb4ff 0%, #0060df 55%, #a06bff 100%);-webkit-background-clip:text;-webkit-text-fill-color:transparent;user-select:none;line-height:1.05;filter:drop-shadow(0 6px 20px rgba(0,96,223,0.3))}
.ntp-date{font-size:13px;font-weight:500;margin-top:5px;color:#8a93b0;letter-spacing:0.5px;text-transform:uppercase}
.ntp-greeting{font-size:16px;font-weight:600;margin-top:9px;color:#c7cede;letter-spacing:-0.2px}

.ntp-logo{display:flex;flex-direction:column;align-items:center;gap:12px;margin-bottom:26px;animation:fadeIn 0.6s ease}
.ntp-logo-mark{width:74px;height:74px;border-radius:24px;background:linear-gradient(135deg, #0a0e1a 0%, #121a30 100%);border:1.5px solid rgba(0,96,223,0.55);display:flex;align-items:center;justify-content:center;font-size:40px;box-shadow:0 12px 40px rgba(0,96,223,0.35), inset 0 0 30px rgba(0,96,223,0.12);animation:fadeIn 0.6s ease}
.ntp-logo-name{font-size:26px;font-weight:600;letter-spacing:-0.4px;color:#e6ebf7}
.ntp-logo-name span{background:linear-gradient(135deg, #4da3ff 0%, #a06bff 100%);-webkit-background-clip:text;-webkit-text-fill-color:transparent}

.search-container{width:100%;max-width:760px;margin-bottom:44px;animation:fadeIn 0.7s ease}
.search-box{display:flex;align-items:center;width:100%;height:62px;padding:0 8px 0 26px;border-radius:31px;background:rgba(18,22,36,0.66);border:1.5px solid rgba(0,96,223,0.45);box-shadow:0 10px 40px rgba(0,0,0,0.4);backdrop-filter:blur(26px) saturate(160%);transition:all .25s cubic-bezier(0.4,0,0.2,1)}
.search-box:hover,.search-box:focus-within{box-shadow:0 14px 52px rgba(0,96,223,0.42);border-color:#4da3ff;transform:translateY(-1px)}
.search-icon{color:#4da3ff;font-size:20px;margin-right:14px}
.search-box input{flex:1;background:transparent;border:none;outline:none;color:#ffffff;font-size:17px;font-weight:400;font-family:'Inter',sans-serif}
.search-box input::placeholder{color:#7d85a0}
.search-box button{background:linear-gradient(135deg, #0060df 0%, #4da3ff 100%);border:none;color:#ffffff;font-weight:700;font-size:15px;cursor:pointer;padding:0 30px;border-radius:24px;height:46px;box-shadow:0 4px 18px rgba(0,96,223,0.45);transition:all .15s ease}
.search-box button:hover{transform:scale(1.04);box-shadow:0 6px 26px rgba(0,96,223,0.6)}
.engine-picker{margin-right:10px}
.engine-picker select{background:rgba(18,22,38,0.8);color:#8ab6ff;border:1px solid rgba(0,96,223,0.35);border-radius:16px;padding:8px 12px;font-size:13px;font-weight:600;cursor:pointer;outline:none;font-family:'Inter',sans-serif;transition:all .2s ease}
.engine-picker select:hover{border-color:#4da3ff;color:#cfe2ff}
.engine-picker select option{background:#121624;color:#dfe5f2}

.dials-heading{width:100%;max-width:960px;text-align:center;font-size:12px;font-weight:700;letter-spacing:2px;text-transform:uppercase;color:#7f88a6;margin-bottom:20px;animation:fadeIn 0.8s ease}
.dials-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(104px,1fr));gap:16px;width:100%;max-width:960px;animation:fadeIn 0.9s ease}
.dial{display:flex;flex-direction:column;align-items:center;gap:10px;padding:18px 8px 14px;border-radius:20px;background:rgba(18,22,38,0.5);border:1px solid rgba(0,96,223,0.16);backdrop-filter:blur(22px) saturate(150%);cursor:pointer;transition:all .22s cubic-bezier(0.4,0,0.2,1);text-decoration:none;color:#ffffff;box-shadow:0 4px 18px rgba(0,0,0,0.22)}
.dial:hover{transform:translateY(-6px) scale(1.04);border-color:#0060df;background:rgba(22,28,48,0.62);box-shadow:0 18px 44px rgba(0,96,223,0.35)}
.dial-icon{width:52px;height:52px;border-radius:16px;display:flex;align-items:center;justify-content:center;font-size:20px;font-weight:700;box-shadow:0 6px 18px rgba(0,0,0,0.3);transition:transform .22s ease;position:relative;overflow:hidden}
.dial:hover .dial-icon{transform:scale(1.1) rotate(-2deg)}
.dial-fallback{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;color:#1a73e8;font-weight:800;text-shadow:0 1px 2px rgba(0,0,0,0.15)}
.dial-img{position:absolute;inset:0;margin:auto;width:36px;height:36px;object-fit:contain;z-index:1;filter:drop-shadow(0 2px 6px rgba(0,0,0,0.35))}
.dial-label{font-size:12px;font-weight:600;letter-spacing:-0.1px;text-align:center;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;max-width:104px;color:#dde3f0}
.dial{position:relative}
.dial-remove{position:absolute;top:6px;right:8px;width:22px;height:22px;border-radius:50%;background:rgba(0,0,0,0.55);color:#fff;font-size:12px;line-height:22px;text-align:center;opacity:.92;transition:all .18s ease;cursor:pointer;z-index:2;border:1px solid rgba(255,255,255,0.25);box-shadow:0 2px 6px rgba(0,0,0,0.3)}
.dial:hover .dial-remove,.dial-remove:hover{opacity:1}
.dial-remove:hover{background:#d93025;transform:scale(1.12)}
.dial-add-icon{background:linear-gradient(135deg, rgba(0,96,223,0.5), rgba(124,77,255,0.5));border:1.5px dashed rgba(0,96,223,0.7)}
.dial-add-icon .dial-fallback{color:#fff;font-size:34px;font-weight:300}

.features-bar{display:flex;align-items:center;justify-content:center;gap:12px;width:100%;max-width:900px;margin-top:34px;animation:fadeIn 1.1s ease;flex-wrap:wrap}
.feature-pill{display:inline-flex;align-items:center;gap:8px;padding:10px 18px;border-radius:22px;background:rgba(18,22,38,0.5);border:1px solid rgba(0,96,223,0.18);backdrop-filter:blur(20px) saturate(150%);color:#dfe5f2;font-size:12.5px;font-weight:600;cursor:pointer;transition:all .2s ease;text-decoration:none;box-shadow:0 2px 12px rgba(0,0,0,0.18)}
.feature-pill:hover{transform:translateY(-2px);border-color:#0060df;box-shadow:0 8px 28px rgba(0,96,223,0.35);color:#8ab6ff}

.footer-note{margin-top:38px;font-size:12px;color:#a6adc8;display:flex;align-items:center;gap:16px;background:rgba(18,22,38,0.42);padding:11px 24px;border-radius:22px;backdrop-filter:blur(16px);border:1px solid rgba(0,96,223,0.16)}

@keyframes fadeIn{from{opacity:0;transform:translateY(12px)}to{opacity:1;transform:translateY(0)}}
</style>
</head>
<body>

<div class='ntp-header'>
  <div class='ntp-clock' id='clock'>12:00 PM</div>
  <div class='ntp-date' id='date'>January 1, 2026</div>
  <div class='ntp-greeting' id='greeting'>Welcome to Black Browser</div>
</div>

<div class='ntp-logo'>
  <div class='ntp-logo-mark'>⚫</div>
  <div class='ntp-logo-name'>Black <span>Browser</span></div>
</div>

<form class='search-container' id='homeSearch' onsubmit='return homeSearchSubmit(event)'>
  <div class='search-box'>
    <span class='search-icon'>🔍</span>
    <input id='searchInput' type='text' placeholder='Search the web or type a URL...' autofocus autocomplete='off'>
    <div class='engine-picker'>
      <select id='engineSelect'>
        <option value='google'" + (SettingsStore.SearchEngine == "google" ? " selected" : "") + @">Google</option>
        <option value='duckduckgo'" + (SettingsStore.SearchEngine == "duckduckgo" ? " selected" : "") + @">DuckDuckGo</option>
        <option value='bing'" + (SettingsStore.SearchEngine == "bing" ? " selected" : "") + @">Bing</option>
        <option value='youtube'" + (SettingsStore.SearchEngine == "youtube" ? " selected" : "") + @">YouTube</option>
      </select>
    </div>
    <button type='submit'>Search</button>
  </div>
</form>

<div class='dials-heading'>★ Quick Access</div>
<div class='dials-grid'>
" + dialsSb.ToString() + @"
</div>

<div class='features-bar'>
  <a class='feature-pill' href='black://history'>📜 History</a>
  <a class='feature-pill' href='black://bookmarks'>⭐ Bookmarks</a>
  <a class='feature-pill' href='black://downloads'>📥 Downloads</a>
  <a class='feature-pill' href='https://gemini.google.com'>✨ Gemini AI</a>
</div>

<div class='footer-note'>
  <span>🔒 3-Layer AdShield</span>
  <span>•</span>
  <span>🕵️ Private Mode</span>
  <span>•</span>
  <span>⚡ ~38MB RAM</span>
</div>

<script>
var ENGINES = {
  google: 'https://www.google.com/search?q=',
  duckduckgo: 'https://duckduckgo.com/?q=',
  bing: 'https://www.bing.com/search?q=',
  youtube: 'https://www.youtube.com/results?search_query='
};
function homeSearchSubmit(ev) {
  var q = document.getElementById('searchInput').value.trim();
  if (!q) { ev.preventDefault(); return false; }
  var eng = document.getElementById('engineSelect').value;
  if (q.indexOf('.') >= 0 && q.indexOf(' ') < 0 && !q.indexOf('http') === 0) {
    location.href = 'https://' + q;
  } else {
    location.href = (ENGINES[eng] || ENGINES.google) + encodeURIComponent(q);
  }
  return false;
}
document.getElementById('engineSelect').addEventListener('change', function() {
  location.href = 'black://setsearch?engine=' + this.value;
});

function pad(n){return n<10?'0'+n:n}
function updateClock() {
  var now = new Date();
  var h = now.getHours();
  var m = now.getMinutes();
  var ampm = h >= 12 ? 'PM' : 'AM';

  var greet = 'Welcome to Black Firefox';
  var userName = '" + System.Security.Principal.WindowsIdentity.GetCurrent().Name.Split('\\')[1] + @"';
  if (h < 12) greet = 'Good Morning, ' + userName + ' — Black Firefox';
  else if (h < 18) greet = 'Good Afternoon, ' + userName + ' — Black Firefox';
  else greet = 'Good Evening, ' + userName + ' — Black Firefox';

  h = h % 12; h = h ? h : 12;

  var months = ['January','February','March','April','May','June','July','August','September','October','November','December'];
  var dateStr = months[now.getMonth()] + ' ' + now.getDate() + ', ' + now.getFullYear();

  document.getElementById('clock').innerText = h + ':' + pad(m) + ' ' + ampm;
  document.getElementById('date').innerText = dateStr;
  document.getElementById('greeting').innerText = greet;
}
updateClock();
setInterval(updateClock, 1000);
</script>

</body>
</html>";

            File.WriteAllText(filePath, html, Encoding.UTF8);
            return "file:///" + filePath.Replace("\\", "/");
        }

        public static string GetHtml(bool isDarkMode)
        {
            string path = GetSpeedDialFilePath(isDarkMode);
            return File.ReadAllText(path.Replace("file:///", ""), Encoding.UTF8);
        }
    }
}
