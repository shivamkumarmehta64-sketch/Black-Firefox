using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace BlackBrowser
{
    public class PasswordItem
    {
        public string Site { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public string DateAdded { get; set; }
    }

    public static class PasswordManager
    {
        private static readonly object fileLock = new object();
        private static List<PasswordItem> _cache = null;

        private static string GetPath()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "black-webview2");
            try { Directory.CreateDirectory(folder); } catch { }
            return Path.Combine(folder, "passwords.dat");
        }

        private static string Protect(string plain)
        {
            try
            {
                byte[] data = Encoding.UTF8.GetBytes(plain);
                byte[] enc = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                return Convert.ToBase64String(enc);
            }
            catch { return null; }
        }

        private static string Unprotect(string enc)
        {
            try
            {
                byte[] data = Convert.FromBase64String(enc);
                byte[] dec = ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(dec);
            }
            catch { return ""; }
        }

        public static List<PasswordItem> GetPasswords()
        {
            lock (fileLock)
            {
                if (_cache != null) return new List<PasswordItem>(_cache);
                _cache = new List<PasswordItem>();
                try
                {
                    string path = GetPath();
                    if (!File.Exists(path)) return new List<PasswordItem>(_cache);
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    var parsed = new JavaScriptSerializer().Deserialize<List<Dictionary<string, object>>>(json);
                    if (parsed == null) return new List<PasswordItem>(_cache);
                    foreach (var d in parsed)
                    {
                        PasswordItem it = new PasswordItem();
                        it.Site = d.ContainsKey("Site") ? Convert.ToString(d["Site"]) : "";
                        it.Username = d.ContainsKey("Username") ? Unprotect(Convert.ToString(d["Username"])) : "";
                        it.Password = d.ContainsKey("Password") ? Unprotect(Convert.ToString(d["Password"])) : "";
                        it.DateAdded = d.ContainsKey("DateAdded") ? Convert.ToString(d["DateAdded"]) : "";
                        if (it.Site.Length > 0) _cache.Add(it);
                    }
                }
                catch { }
                return new List<PasswordItem>(_cache);
            }
        }

        private static void SaveAllLocked(List<PasswordItem> items)
        {
            try
            {
                List<Dictionary<string, object>> list = new List<Dictionary<string, object>>();
                foreach (PasswordItem it in items)
                {
                    Dictionary<string, object> d = new Dictionary<string, object>();
                    d["Site"] = it.Site;
                    d["Username"] = Protect(it.Username ?? "");
                    d["Password"] = Protect(it.Password ?? "");
                    d["DateAdded"] = it.DateAdded;
                    list.Add(d);
                }
                string json = new JavaScriptSerializer().Serialize(list);
                File.WriteAllText(GetPath(), json, Encoding.UTF8);
            }
            catch { }
        }

        public static void SavePassword(string site, string username, string password)
        {
            if (string.IsNullOrEmpty(site) || string.IsNullOrEmpty(password)) return;
            string safeUser = username ?? "";
            lock (fileLock)
            {
                GetPasswords();
                _cache.RemoveAll(x =>
                    x.Site != null && x.Site.Equals(site, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.Username ?? "", safeUser, StringComparison.OrdinalIgnoreCase));
                PasswordItem it = new PasswordItem();
                it.Site = site;
                it.Username = safeUser;
                it.Password = password;
                it.DateAdded = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                _cache.Insert(0, it);
                SaveAllLocked(_cache);
            }
        }

        public static List<PasswordItem> GetCredentialsForHost(string host)
        {
            List<PasswordItem> matches = new List<PasswordItem>();
            if (string.IsNullOrEmpty(host)) return matches;
            List<PasswordItem> all = GetPasswords();
            foreach (PasswordItem p in all)
            {
                if (!string.IsNullOrEmpty(p.Site) && p.Site.Equals(host, StringComparison.OrdinalIgnoreCase))
                {
                    matches.Add(new PasswordItem
                    {
                        Site = p.Site,
                        Username = p.Username,
                        Password = p.Password,
                        DateAdded = p.DateAdded
                    });
                }
            }
            return matches;
        }

        public static void RemovePassword(string site, string username = null)
        {
            if (string.IsNullOrEmpty(site)) return;
            lock (fileLock)
            {
                GetPasswords();
                if (username == null)
                {
                    _cache.RemoveAll(x => x.Site != null && x.Site.Equals(site, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    _cache.RemoveAll(x =>
                        x.Site != null && x.Site.Equals(site, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(x.Username ?? "", username, StringComparison.OrdinalIgnoreCase));
                }
                SaveAllLocked(_cache);
            }
        }

        public static string GetPasswordsHtml(bool isDarkMode)
        {
            List<PasswordItem> items = GetPasswords();
            string bg = isDarkMode ? "#0b0d1b" : "#f5f6fa";
            string textColor = isDarkMode ? "#ffffff" : "#1a1d2e";
            string cardBg = isDarkMode ? "rgba(255, 255, 255, 0.05)" : "rgba(0, 0, 0, 0.04)";
            string border = isDarkMode ? "rgba(255, 255, 255, 0.1)" : "rgba(0, 0, 0, 0.12)";
            string muted = isDarkMode ? "#a2a6cc" : "#5b6078";
            string inputBg = isDarkMode ? "rgba(255, 255, 255, 0.08)" : "rgba(0, 0, 0, 0.06)";

            StringBuilder cards = new StringBuilder();
            if (items == null || items.Count == 0)
            {
                cards.Append("<div class='empty'>No saved passwords yet. As you log into websites, Black Browser will prompt you to securely save them!</div>");
            }
            else
            {
                for (int idx = 0; idx < items.Count; idx++)
                {
                    PasswordItem p = items[idx];
                    string safeSite = System.Web.HttpUtility.HtmlEncode(p.Site ?? "");
                    string safeUser = System.Web.HttpUtility.HtmlEncode(p.Username ?? "");
                    string safePass = System.Web.HttpUtility.HtmlEncode(p.Password ?? "");
                    string safeDate = System.Web.HttpUtility.HtmlEncode(p.DateAdded ?? "");

                    cards.Append(@"
<div class='item' data-site='" + safeSite.ToLowerInvariant() + @"' data-user='" + safeUser.ToLowerInvariant() + @"' data-rawsite='" + safeSite + @"' data-rawuser='" + safeUser + @"'>
  <div class='item-info'>
    <div class='item-header'>
      <span class='item-icon'>🔐</span>
      <span class='item-site'>" + safeSite + @"</span>
      <span class='badge'>DPAPI Encrypted</span>
    </div>
    <div class='item-user'>👤 <strong>" + safeUser + @"</strong></div>
    <div class='item-pass-row'>
      <span class='pass-mask' data-shown='0'>••••••••••••</span>
      <span class='pass-secret' style='display:none'>" + safePass + @"</span>
      <button type='button' class='btn-sm btn-toggle'>👁️ Show</button>
      <button type='button' class='btn-sm btn-copy'>📋 Copy</button>
    </div>
    <div class='item-date'>Added: " + safeDate + @"</div>
  </div>
  <div class='item-actions'>
    <button type='button' class='btn-del'>🗑️ Remove</button>
  </div>
</div>");
                }
            }

            return @"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<title>Saved Passwords — Black Browser</title>
<style>
*{margin:0;padding:0;box-sizing:border-box}
body{font-family:'Segoe UI Variable Display','Segoe UI',system-ui,-apple-system,sans-serif;background:" + bg + @";color:" + textColor + @";padding:36px 24px;max-width:880px;margin:0 auto;-webkit-font-smoothing:antialiased}
.header{display:flex;align-items:center;justify-content:space-between;margin-bottom:24px;padding-bottom:16px;border-bottom:1px solid " + border + @"}
.title{font-size:26px;font-weight:700;background:linear-gradient(135deg, #3b82f6, #8b5cf6);-webkit-background-clip:text;-webkit-text-fill-color:transparent}
.sub{font-size:13.5px;color:" + muted + @";margin-top:6px}
.search-box{width:100%;padding:12px 18px;border-radius:12px;border:1px solid " + border + @";background:" + inputBg + @";color:" + textColor + @";font-size:14px;margin-bottom:24px;outline:none;transition:border-color .2s}
.search-box:focus{border-color:#3b82f6}
.item{display:flex;align-items:center;justify-content:space-between;padding:18px 22px;background:" + cardBg + @";border:1px solid " + border + @";border-radius:16px;margin-bottom:14px;backdrop-filter:blur(16px);transition:all .2s ease}
.item:hover{transform:translateY(-2px);border-color:#3b82f6;box-shadow:0 8px 24px rgba(59,130,246,0.2)}
.item-info{display:flex;flex-direction:column;gap:6px;flex:1}
.item-header{display:flex;align-items:center;gap:10px}
.item-icon{font-size:18px}
.item-site{font-size:16px;font-weight:700;color:" + textColor + @"}
.badge{font-size:11px;padding:2px 8px;border-radius:8px;font-weight:600;background:rgba(59,130,246,0.15);color:#60a5fa;border:1px solid rgba(59,130,246,0.3)}
.item-user{font-size:13.5px;color:" + muted + @"}
.item-pass-row{display:flex;align-items:center;gap:8px;margin-top:4px}
.pass-mask{font-family:'Cascadia Code','Consolas',monospace;font-size:14px;color:#a78bfa;font-weight:600;letter-spacing:2px}
.item-date{font-size:11.5px;color:" + muted + @";margin-top:4px}
.btn-sm{padding:4px 10px;border-radius:8px;border:1px solid " + border + @";background:rgba(255,255,255,0.06);color:" + textColor + @";font-size:12px;font-weight:600;cursor:pointer;transition:all .2s}
.btn-sm:hover{background:rgba(59,130,246,0.2);border-color:#3b82f6}
.btn-del{padding:8px 16px;border-radius:10px;border:1px solid rgba(239,68,68,0.3);background:rgba(239,68,68,0.1);color:#f87171;font-size:12.5px;font-weight:600;cursor:pointer;transition:all .2s;white-space:nowrap;margin-left:16px}
.btn-del:hover{background:rgba(239,68,68,0.25);color:#fff}
.empty{text-align:center;padding:70px 0;color:" + muted + @";font-size:15px}
</style>
</head>
<body>

<div class='header'>
  <div>
    <div class='title'>🔐 Saved Passwords & Credentials</div>
    <div class='sub'>✨ Protected by Windows DPAPI • Local Storage Only • Zero Cloud Transmission</div>
  </div>
</div>

<input type='text' id='search' class='search-box' placeholder='🔍 Search saved sites and usernames...' oninput='filterList(this.value)'>

<div id='list'>
" + cards.ToString() + @"
</div>

<script>
function filterList(q){
  q = (q||'').toLowerCase();
  var items = document.querySelectorAll('.item');
  items.forEach(function(el){
    var site = el.getAttribute('data-site')||'';
    var user = el.getAttribute('data-user')||'';
    el.style.display = (site.indexOf(q) !== -1 || user.indexOf(q) !== -1) ? 'flex' : 'none';
  });
}
document.getElementById('list').addEventListener('click', function(e){
  var btn = e.target.closest('button');
  if(!btn) return;
  var card = btn.closest('.item');
  if(!card) return;
  var secretEl = card.querySelector('.pass-secret');
  var maskEl = card.querySelector('.pass-mask');
  var pass = secretEl ? secretEl.textContent : '';
  if(btn.classList.contains('btn-toggle')){
    if(maskEl.getAttribute('data-shown') === '1'){
      maskEl.textContent = '••••••••••••';
      maskEl.setAttribute('data-shown', '0');
      btn.textContent = '👁️ Show';
    } else {
      maskEl.textContent = pass;
      maskEl.setAttribute('data-shown', '1');
      btn.textContent = '🙈 Hide';
    }
  } else if(btn.classList.contains('btn-copy')){
    navigator.clipboard.writeText(pass).then(function(){
      var old = btn.textContent;
      btn.textContent = '✓ Copied!';
      setTimeout(function(){ btn.textContent = old; }, 1500);
    });
  } else if(btn.classList.contains('btn-del')){
    var site = card.getAttribute('data-rawsite') || '';
    var user = card.getAttribute('data-rawuser') || '';
    if(confirm('Remove saved password for ' + site + (user ? ' (' + user + ')' : '') + '?')){
      location.href = 'black://remove-password?site=' + encodeURIComponent(site) + '&user=' + encodeURIComponent(user);
    }
  }
});
</script>

</body>
</html>";
        }
    }
}