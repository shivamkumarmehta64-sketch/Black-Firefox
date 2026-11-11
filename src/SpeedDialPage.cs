using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace BlackBrowser
{
    public static class SpeedDialPage
    {
        private static string GetRgbString(string hex)
        {
            try
            {
                if (string.IsNullOrEmpty(hex)) return "99, 102, 241";
                string cleanHex = hex.Trim().Replace("#", "");
                if (cleanHex.Length == 6)
                {
                    int r = Convert.ToInt32(cleanHex.Substring(0, 2), 16);
                    int g = Convert.ToInt32(cleanHex.Substring(2, 2), 16);
                    int b = Convert.ToInt32(cleanHex.Substring(4, 2), 16);
                    return r + ", " + g + ", " + b;
                }
            }
            catch { }
            return "99, 102, 241";
        }

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

        private static string SimpleJsEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c == '\\') sb.Append("\\\\");
                else if (c == '"') sb.Append("\\\"");
                else if (c == '\'') sb.Append("\\'");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') sb.Append("\\r");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string _cachedFileKey = null;
        private static string _cachedFilePath = null;

        private static string BuildSpeedDialKey(bool isDarkMode)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(isDarkMode ? "1" : "0");
            sb.Append('|').Append(SettingsStore.SearchEngine ?? "");
            sb.Append('|').Append(SettingsStore.SpeedDialBg ?? "");
            sb.Append('|').Append(SettingsStore.ShowClock ? "1" : "0");
            sb.Append('|').Append(SettingsStore.ShowNotes ? "1" : "0");
            sb.Append('|').Append(SettingsStore.ShowTelemetry ? "1" : "0");
            sb.Append('|').Append(SettingsStore.AnimationsEnabled ? "1" : "0");
            sb.Append('|').Append(SettingsStore.CornerRadius);
            sb.Append('|').Append(SettingsStore.Accent ?? "");
            try
            {
                var dials = CustomDialsManager.GetDials();
                if (dials != null)
                {
                    sb.Append('|').Append(dials.Count);
                    foreach (var d in dials)
                    {
                        sb.Append('|').Append(d.Title ?? "");
                        sb.Append('|').Append(d.Url ?? "");
                        sb.Append('|').Append(d.IconText ?? "");
                        sb.Append('|').Append(d.BgColor ?? "");
                        sb.Append('|').Append(d.FgColor ?? "");
                    }
                }
            }
            catch { sb.Append('|'); }
            return sb.ToString();
        }

        public static string GetHtml(bool isDarkMode)
        {
            // Always render live RAM, AdShield counters, recent history, bookmarks, and synced notes cleanly.
            return BuildHtml(isDarkMode);
        }

        private static string GetHostLabel(string url)
        {
            try
            {
                Uri u;
                if (Uri.TryCreate(url, UriKind.Absolute, out u) && !string.IsNullOrEmpty(u.Host))
                {
                    string h = u.Host;
                    return h.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? h.Substring(4) : h;
                }
            }
            catch { }
            return "web";
        }

        private static string BuildHtml(bool isDarkMode)
        {
            string userName = Environment.UserName;
            if (string.IsNullOrEmpty(userName)) userName = "Explorer";

            long ramMb = MemoryTrimmer.GetWorkingSetMB();
            long blockedAds = AdShieldEngine.TotalBlockedCount;
            int disabledSitesCount = AdShieldState.DisabledSitesCount;
            bool adShieldEnabled = SettingsStore.AdShieldEnabled;
            bool httpsFirst = SettingsStore.HttpsFirstMode;
            bool restoreSession = SettingsStore.RestoreSession;

            string rawWv2Ver = Program.GetInstalledWebView2Version();
            string wv2Short = !string.IsNullOrEmpty(rawWv2Ver) ? (rawWv2Ver + " x64") : "WebView2 x64";
            if (wv2Short.Length > 20) wv2Short = rawWv2Ver.Substring(0, Math.Min(12, rawWv2Ver.Length)) + " x64";

            List<BookmarkItem> allBookmarks = BookmarksManager.GetBookmarks() ?? new List<BookmarkItem>();
            List<HistoryItem> allHistory = HistoryManager.GetHistoryItems() ?? new List<HistoryItem>();
            List<PasswordItem> allPasswords = PasswordManager.GetPasswords() ?? new List<PasswordItem>();
            List<ExtensionItem> allExtensions = ExtensionManager.GetInstalledExtensions() ?? new List<ExtensionItem>();
            int downloadsCount = DownloadsManager.GetDownloadsCount();

            string currentEngine = (SettingsStore.SearchEngine ?? "google").ToLowerInvariant();
            string searchEngineGoogle    = currentEngine == "google"     ? " active" : "";
            string searchEngineDdg       = currentEngine == "duckduckgo" ? " active" : "";
            string searchEngineBing      = currentEngine == "bing"       ? " active" : "";
            string searchEngineBrave     = currentEngine == "brave"      ? " active" : "";
            string searchEngineYoutube   = currentEngine == "youtube"    ? " active" : "";
            string searchEngineGithub    = currentEngine == "github"     ? " active" : "";
            string searchEngineWiki      = currentEngine == "wikipedia"  ? " active" : "";
            string searchEngineStartpage = currentEngine == "startpage"  ? " active" : "";

            // Recent History HTML (top 5 distinct safe external entries)
            StringBuilder recentHistSb = new StringBuilder();
            HashSet<string> seenRecent = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int recentShown = 0;
            for (int i = 0; i < allHistory.Count && recentShown < 5; i++)
            {
                HistoryItem hi = allHistory[i];
                if (hi == null || string.IsNullOrWhiteSpace(hi.Url)) continue;
                if (!HistoryManager.IsSafeUrl(hi.Url)) continue;
                if (hi.Url.StartsWith("black://", StringComparison.OrdinalIgnoreCase) || InternalUrlRouter.IsInternalOrigin(hi.Url)) continue;
                if (!seenRecent.Add(hi.Url)) continue;
                string title = string.IsNullOrWhiteSpace(hi.Title) ? GetHostLabel(hi.Url) : hi.Title.Trim();
                if (title.Length > 44) title = title.Substring(0, 41) + "...";
                string host = GetHostLabel(hi.Url);
                string firstCh = string.IsNullOrEmpty(host) ? "W" : host.Substring(0, 1).ToUpperInvariant();
                recentHistSb.Append(
                    "<a class='recent-row' href='" + SimpleHtmlEncode(hi.Url) + "'>"
                    + "<span class='recent-badge'>" + SimpleHtmlEncode(firstCh) + "</span>"
                    + "<span class='recent-meta'><span class='recent-title'>" + SimpleHtmlEncode(title) + "</span>"
                    + "<span class='recent-host'>" + SimpleHtmlEncode(host) + "</span></span>"
                    + "<span class='recent-arrow'>&#x2197;</span>"
                    + "</a>"
                );
                recentShown++;
            }
            if (recentShown == 0)
            {
                recentHistSb.Append("<div class='recent-empty'>No recent sites visited yet. Use search or Speed Dial above to start browsing.</div>");
            }

            // Pinned / Saved Bookmarks HTML (top 5 safe entries)
            StringBuilder recentBmSb = new StringBuilder();
            int bmShown = 0;
            for (int i = 0; i < allBookmarks.Count && bmShown < 5; i++)
            {
                BookmarkItem bm = allBookmarks[i];
                if (bm == null || string.IsNullOrWhiteSpace(bm.Url)) continue;
                if (!BookmarksManager.IsSafeUrl(bm.Url)) continue;
                string title = string.IsNullOrWhiteSpace(bm.Title) ? GetHostLabel(bm.Url) : bm.Title.Trim();
                if (title.Length > 44) title = title.Substring(0, 41) + "...";
                string host = GetHostLabel(bm.Url);
                recentBmSb.Append(
                    "<a class='recent-row' href='" + SimpleHtmlEncode(bm.Url) + "'>"
                    + "<span class='recent-badge bm'>&#x2B50;</span>"
                    + "<span class='recent-meta'><span class='recent-title'>" + SimpleHtmlEncode(title) + "</span>"
                    + "<span class='recent-host'>" + SimpleHtmlEncode(host) + "</span></span>"
                    + "<span class='recent-arrow'>&#x2197;</span>"
                    + "</a>"
                );
                bmShown++;
            }
            if (bmShown == 0)
            {
                recentBmSb.Append("<div class='recent-empty'>No bookmarks saved yet. Press Ctrl+D on any site to save it.</div>");
            }

            string syncedNotesText = SettingsStore.GetQuickNotes();
            int ramPct = (int)Math.Min(100, Math.Max(8, (ramMb * 100) / 256));
            int secActiveCount = (adShieldEnabled ? 1 : 0) + (httpsFirst ? 1 : 0) + (restoreSession ? 1 : 0) + 3;

            // Build shortcuts HTML from user's saved dials
            var dialsSb = new StringBuilder();
            try
            {
                var dials = CustomDialsManager.GetDials();
                if (dials != null && dials.Count > 0)
                {
                    foreach (var d in dials)
                    {
                        string rawUrl = d.Url ?? "https://google.com";
                        if (!rawUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                            !rawUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            rawUrl = "https://google.com";
                        string safeTitle = SimpleHtmlEncode(d.Title ?? "App");
                        string safeUrl   = SimpleHtmlEncode(rawUrl);
                        string safeIcon  = SimpleHtmlEncode(d.IconText ?? "?");
                        string safeBg    = SimpleHtmlEncode(d.BgColor ?? "#6366f1");
                        string safeFg    = SimpleHtmlEncode(d.FgColor ?? "#ffffff");

                        dialsSb.Append(
                            "<div class='sh-tile-wrapper' data-rawtitle='" + safeTitle + "' data-rawurl='" + safeUrl + "'>"
                            + "<a class='sh-tile' href='" + safeUrl + "' title='" + safeTitle + "'>"
                            + "<div class='sh-icon' style='background:" + safeBg + ";color:" + safeFg + "'>"
                            + "<span class='sh-letter'>" + safeIcon + "</span>"
                            + "</div>"
                            + "<div class='sh-label'>" + safeTitle + "</div>"
                            + "</a>"
                            + "<button type='button' class='sh-rm' onclick='removeDial(event,this)' title='Remove shortcut'>&#x2715;</button>"
                            + "</div>"
                        );
                    }
                }
            }
            catch { }

            // Add Tile Button
            dialsSb.Append(
                "<div class='sh-tile-wrapper'>"
                + "<a class='sh-tile sh-add' href='black://adddial' title='Add new custom shortcut'>"
                + "<div class='sh-icon sh-add-icon'><span>+</span></div>"
                + "<div class='sh-label'>Add Tile</div>"
                + "</a>"
                + "</div>"
            );

            string shortcutsSection = dialsSb.ToString();

            string selectedBg = (SettingsStore.SpeedDialBg ?? "aurora").ToLowerInvariant();
            string themeMode = isDarkMode ? "dark" : "light";
            if (selectedBg == "light") themeMode = "light";

            string bgGradient = "";
            if (selectedBg == "cyberpunk")
            {
                bgGradient = themeMode == "dark"
                    ? "radial-gradient(circle at 20% 20%, rgba(236, 72, 153, 0.2) 0%, transparent 50%), radial-gradient(circle at 80% 80%, rgba(6, 182, 212, 0.2) 0%, transparent 50%), #05050a"
                    : "radial-gradient(circle at 20% 20%, rgba(236, 72, 153, 0.1) 0%, transparent 50%), radial-gradient(circle at 80% 80%, rgba(6, 182, 212, 0.1) 0%, transparent 50%), #fafafa";
            }
            else if (selectedBg == "nebula")
            {
                bgGradient = themeMode == "dark"
                    ? "radial-gradient(circle at 50% 50%, rgba(139, 92, 246, 0.25) 0%, transparent 70%), radial-gradient(circle at 10% 10%, rgba(244, 63, 94, 0.15) 0%, transparent 40%), #030008"
                    : "radial-gradient(circle at 50% 50%, rgba(139, 92, 246, 0.1) 0%, transparent 70%), #f5f3ff";
            }
            else if (selectedBg == "obsidian")
            {
                bgGradient = themeMode == "dark" ? "#050505" : "#ffffff";
            }
            else if (selectedBg == "light")
            {
                bgGradient = "linear-gradient(135deg, #e2e8f0 0%, #cbd5e1 100%)";
            }
            else // aurora (default)
            {
                bgGradient = themeMode == "dark"
                    ? "radial-gradient(circle at 50% -10%, rgba(99, 102, 241, 0.25) 0%, transparent 60%), radial-gradient(circle at 90% 90%, rgba(168, 85, 247, 0.15) 0%, transparent 60%), radial-gradient(circle at 10% 80%, rgba(6, 182, 212, 0.12) 0%, transparent 50%), #08090e"
                    : "radial-gradient(circle at 50% -10%, rgba(99, 102, 241, 0.12) 0%, transparent 60%), radial-gradient(circle at 90% 90%, rgba(168, 85, 247, 0.08) 0%, transparent 60%), radial-gradient(circle at 10% 80%, rgba(6, 182, 212, 0.08) 0%, transparent 50%), #f8fafc";
            }

            string html = @"<!DOCTYPE html>
<html lang='en' data-theme='" + themeMode + @"'>
<head>
<meta charset='utf-8'>
<meta name='viewport' content='width=device-width, initial-scale=1'>
<title>New Tab • Black Browser</title>
<style>
:root {
  --font-main: 'Segoe UI Variable Display', 'Segoe UI', system-ui, -apple-system, Roboto, sans-serif;
  --font-display: 'Segoe UI Variable Display', 'Segoe UI', var(--font-main);
  --font-mono: 'Cascadia Code', 'Consolas', monospace;
  --accent: " + SettingsStore.Accent + @";
  --accent-light: " + SettingsStore.Accent + @"ee;
  --accent-purple: #a855f7;
  --accent-cyan: #06b6d4;
  --accent-emerald: #10b981;
  --accent-glow: rgba(" + GetRgbString(SettingsStore.Accent) + @", 0.35);
  --radius-lg: " + SettingsStore.CornerRadius + @"px;
  --radius-md: " + Math.Max(0, SettingsStore.CornerRadius - 4) + @"px;
  --radius-sm: " + Math.Max(0, SettingsStore.CornerRadius - 8) + @"px;
}

[data-theme='dark'] {
  --bg: #08090e;
  --surface: rgba(18, 20, 30, 0.65);
  --surface-hover: rgba(28, 32, 48, 0.85);
  --surface-card: rgba(22, 25, 38, 0.7);
  --border: rgba(255, 255, 255, 0.08);
  --border-focus: var(--accent);
  --text: #f8fafc;
  --text-muted: #94a3b8;
  --text-dim: #64748b;
  --glow: var(--accent-glow);
  --search-bg: rgba(18, 20, 32, 0.85);
}

[data-theme='light'] {
  --bg: #f8fafc;
  --surface: rgba(255, 255, 255, 0.8);
  --surface-hover: rgba(255, 255, 255, 0.95);
  --surface-card: rgba(255, 255, 255, 0.9);
  --border: rgba(0, 0, 0, 0.08);
  --border-focus: var(--accent);
  --text: #0f172a;
  --text-muted: #475569;
  --text-dim: #94a3b8;
  --glow: var(--accent-glow);
  --search-bg: rgba(255, 255, 255, 0.95);
}

* { margin: 0; padding: 0; box-sizing: border-box; }

body {
  min-height: 100vh;
  background: " + bgGradient + @";
  color: var(--text);
  font-family: var(--font-main);
  -webkit-font-smoothing: antialiased;
  overflow-x: hidden;
  overflow-y: auto;
  position: relative;
}

#particleCanvas {
  position: fixed;
  inset: 0;
  z-index: 1;
  pointer-events: none;
  width: 100%;
  height: 100%;
}

.container {
  position: relative;
  z-index: 2;
  max-width: 1180px;
  margin: 0 auto;
  padding: 28px 24px 60px;
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}

/* Topbar */
.topbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 10px 16px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 20px;
  backdrop-filter: blur(24px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.15);
  margin-bottom: 24px;
  animation: fadeInDown 0.6s cubic-bezier(0.16, 1, 0.3, 1) both;
}

@keyframes fadeInDown {
  from { opacity: 0; transform: translateY(-20px); }
  to { opacity: 1; transform: translateY(0); }
}

.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  text-decoration: none;
  flex-shrink: 0;
}

.brand-badge-icon {
  width: 36px;
  height: 36px;
  background: linear-gradient(135deg, #6366f1, #a855f7);
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  box-shadow: 0 4px 16px rgba(99, 102, 241, 0.4);
}

.brand-text-title {
  font-family: var(--font-display);
  font-size: 16.5px;
  font-weight: 800;
  background: linear-gradient(135deg, #818cf8, #c084fc);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  letter-spacing: -0.3px;
}

.brand-sub {
  font-size: 11px;
  color: var(--accent-emerald);
  font-weight: 600;
  display: flex;
  align-items: center;
  gap: 4px;
}

.brand-sub::before {
  content: '';
  width: 6px;
  height: 6px;
  background: var(--accent-emerald);
  border-radius: 50%;
  display: inline-block;
}

.nav-chips {
  display: flex;
  align-items: center;
  gap: 6px;
  flex-wrap: wrap;
  justify-content: flex-end;
}

.nav-chip {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 6px 10px;
  border-radius: 10px;
  color: var(--text-muted);
  font-size: 12px;
  font-weight: 600;
  white-space: nowrap;
  text-decoration: none;
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid var(--border);
  transition: all 0.2s ease;
}

.nav-chip:hover {
  background: rgba(99, 102, 241, 0.15);
  border-color: rgba(99, 102, 241, 0.4);
  color: var(--text);
  transform: translateY(-1px);
}

/* Hero Section */
.hero {
  text-align: center;
  margin-bottom: 24px;
  animation: fadeInUp 0.7s 0.1s cubic-bezier(0.16, 1, 0.3, 1) both;
}

@keyframes fadeInUp {
  from { opacity: 0; transform: translateY(20px); }
  to { opacity: 1; transform: translateY(0); }
}

.hero-clock {
  font-family: var(--font-display);
  font-size: 56px;
  font-weight: 800;
  letter-spacing: -2px;
  background: linear-gradient(135deg, #ffffff 0%, #cbd5e1 45%, #818cf8 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  line-height: 1.05;
  margin-bottom: 8px;
  filter: drop-shadow(0 0 40px var(--glow));
}

[data-theme='light'] .hero-clock {
  background: linear-gradient(135deg, #1e293b 0%, #3b82f6 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.hero-greeting {
  font-family: var(--font-display);
  font-size: 22px;
  font-weight: 600;
  color: var(--text-muted);
  margin-bottom: 12px;
}

.hero-status-pill {
  display: inline-flex;
  align-items: center;
  gap: 12px;
  padding: 6px 16px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 30px;
  font-size: 12px;
  color: var(--text-muted);
  font-weight: 500;
  backdrop-filter: blur(16px);
}

.hero-status-pill span {
  display: flex;
  align-items: center;
  gap: 5px;
}

/* Omni-Search Box */
.search-section {
  max-width: 680px;
  margin: 0 auto 28px;
  width: 100%;
  animation: fadeInUp 0.7s 0.2s cubic-bezier(0.16, 1, 0.3, 1) both;
}

.search-box {
  position: relative;
  background: var(--search-bg);
  border: 1.5px solid var(--border);
  border-radius: 24px;
  padding: 6px 8px 6px 20px;
  display: flex;
  align-items: center;
  gap: 12px;
  backdrop-filter: blur(24px);
  box-shadow: 0 12px 40px rgba(0, 0, 0, 0.25), 0 0 0 1px rgba(255, 255, 255, 0.05);
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
}

.search-box:focus-within {
  border-color: var(--accent);
  box-shadow: 0 16px 48px rgba(99, 102, 241, 0.25), 0 0 0 3px rgba(99, 102, 241, 0.2);
  transform: translateY(-2px);
}

.search-icon {
  font-size: 18px;
  color: var(--accent);
  flex-shrink: 0;
}

.search-input {
  flex: 1;
  background: transparent;
  border: none;
  outline: none;
  font-family: var(--font-main);
  font-size: 15.5px;
  color: var(--text);
  padding: 10px 0;
}

.search-input::placeholder {
  color: var(--text-dim);
}

.search-shortcut-hint {
  font-family: var(--font-mono);
  font-size: 11px;
  padding: 3px 7px;
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid var(--border);
  border-radius: 6px;
  color: var(--text-dim);
  user-select: none;
}

.search-btn {
  background: linear-gradient(135deg, #6366f1, #8b5cf6);
  border: none;
  color: #ffffff;
  padding: 10px 22px;
  border-radius: 18px;
  font-family: var(--font-main);
  font-size: 13.5px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: 0 4px 14px rgba(99, 102, 241, 0.4);
}

.search-btn:hover {
  transform: scale(1.02);
  box-shadow: 0 6px 20px rgba(99, 102, 241, 0.5);
}

.engine-chips {
  display: flex;
  justify-content: center;
  gap: 8px;
  margin-top: 14px;
  flex-wrap: wrap;
}

.engine-chip {
  padding: 5px 12px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 600;
  text-decoration: none;
  color: var(--text-muted);
  background: var(--surface);
  border: 1px solid var(--border);
  transition: all 0.2s ease;
  display: inline-flex;
  align-items: center;
  gap: 5px;
}

.engine-chip:hover {
  color: var(--text);
  border-color: var(--accent);
  background: rgba(99, 102, 241, 0.12);
}

.engine-chip.active {
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.25), rgba(168, 85, 247, 0.25));
  border-color: var(--accent);
  color: #818cf8;
  box-shadow: 0 2px 10px rgba(99, 102, 241, 0.2);
}

/* Shortcuts Grid */
.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 18px;
}

.section-title {
  font-family: var(--font-display);
  font-size: 16px;
  font-weight: 700;
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--text);
}

.sh-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(105px, 1fr));
  gap: 16px;
  margin-bottom: 38px;
  animation: fadeInUp 0.7s 0.3s cubic-bezier(0.16, 1, 0.3, 1) both;
}

.sh-tile-wrapper {
  position: relative;
}

.sh-tile {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 16px 10px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 20px;
  text-decoration: none;
  color: var(--text);
  backdrop-filter: blur(18px);
  transition: all 0.25s cubic-bezier(0.4, 0, 0.2, 1);
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.1);
  min-height: 108px;
}

.sh-tile:hover {
  transform: translateY(-5px);
  border-color: var(--accent);
  background: var(--surface-hover);
  box-shadow: 0 12px 30px rgba(99, 102, 241, 0.25);
}

.sh-icon {
  width: 50px;
  height: 50px;
  border-radius: 14px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 17px;
  letter-spacing: -0.4px;
  font-weight: 700;
  margin-bottom: 8px;
  position: relative;
  overflow: hidden;
  box-shadow: 0 4px 14px rgba(0, 0, 0, 0.2);
}

.sh-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  position: absolute;
  inset: 0;
}

.sh-label {
  font-size: 12px;
  font-weight: 600;
  color: var(--text);
  text-align: center;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 85px;
}

.sh-add-icon {
  background: rgba(99, 102, 241, 0.12) !important;
  color: #818cf8 !important;
  border: 1.5px dashed rgba(99, 102, 241, 0.4);
  font-size: 26px;
}

.sh-rm {
  position: absolute;
  top: 6px;
  right: 6px;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: rgba(239, 68, 68, 0.85);
  color: #ffffff;
  border: none;
  font-size: 11px;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  transform: scale(0.8);
  transition: all 0.2s ease;
  z-index: 10;
}

.sh-tile-wrapper:hover .sh-rm {
  opacity: 1;
  transform: scale(1);
}

.sh-rm:hover {
  background: #dc2626;
  transform: scale(1.15);
}

/* Feature Dashboard Row */
.dashboard-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
  gap: 20px;
  margin-bottom: 36px;
  animation: fadeInUp 0.7s 0.4s cubic-bezier(0.16, 1, 0.3, 1) both;
}

.telemetry-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
  margin-top: 4px;
}

.tel-item {
  padding: 11px 12px;
  background: rgba(0, 0, 0, 0.15);
  border: 1px solid var(--border);
  border-radius: 14px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  transition: all 0.2s ease;
}

.tel-item:hover {
  background: rgba(99, 102, 241, 0.1);
  border-color: rgba(99, 102, 241, 0.3);
  transform: translateY(-1px);
}

.tel-label {
  font-size: 11px;
  font-weight: 600;
  color: var(--text-muted);
  display: flex;
  align-items: center;
  gap: 5px;
}

.tel-value {
  font-family: var(--font-display);
  font-size: 15px;
  font-weight: 800;
  color: var(--text);
  letter-spacing: -0.3px;
}

.tel-sub {
  font-size: 10px;
  color: var(--text-dim);
}

.tel-bar-bg {
  width: 100%;
  height: 4px;
  background: rgba(255, 255, 255, 0.08);
  border-radius: 2px;
  overflow: hidden;
  margin-top: 2px;
}

.tel-bar-fill {
  height: 100%;
  background: linear-gradient(90deg, var(--accent), var(--accent-purple));
  border-radius: 2px;
}

.dash-card {
  background: var(--surface-card);
  border: 1px solid var(--border);
  border-radius: 22px;
  padding: 22px;
  backdrop-filter: blur(24px);
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.12);
  display: flex;
  flex-direction: column;
}

.card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 14px;
}

.card-title {
  font-family: var(--font-display);
  font-size: 15px;
  font-weight: 700;
  display: flex;
  align-items: center;
  gap: 8px;
}

.card-actions {
  display: flex;
  gap: 6px;
}

.btn-mini {
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--border);
  color: var(--text-muted);
  padding: 4px 10px;
  border-radius: 8px;
  font-size: 11.5px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
}

.btn-mini:hover {
  background: rgba(99, 102, 241, 0.2);
  border-color: var(--accent);
  color: var(--text);
}

.scratchpad-area {
  flex: 1;
  width: 100%;
  min-height: 95px;
  background: rgba(0, 0, 0, 0.15);
  border: 1px solid var(--border);
  border-radius: 14px;
  padding: 12px;
  color: var(--text);
  font-family: var(--font-mono);
  font-size: 13px;
  line-height: 1.6;
  resize: none;
  outline: none;
  transition: border-color 0.2s ease;
}

.scratchpad-area:focus {
  border-color: var(--accent);
}

.scratchpad-footer {
  display: flex;
  justify-content: space-between;
  font-size: 11px;
  color: var(--text-dim);
  margin-top: 8px;
}

.security-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
  gap: 10px;
  margin-top: 4px;
}

.sec-pill {
  padding: 11px 12px;
  background: rgba(0, 0, 0, 0.15);
  border: 1px solid var(--border);
  border-radius: 14px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  transition: all 0.2s ease;
}

.sec-pill:hover {
  background: rgba(99, 102, 241, 0.1);
  border-color: rgba(99, 102, 241, 0.3);
  transform: translateY(-1px);
}

.sec-pill-title {
  font-size: 12px;
  font-weight: 700;
  color: var(--text);
  display: flex;
  align-items: center;
  gap: 6px;
}

.sec-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: var(--accent-emerald);
  display: inline-block;
  box-shadow: 0 0 8px rgba(16, 185, 129, 0.6);
  flex-shrink: 0;
}

.sec-pill-sub {
  font-size: 10.5px;
  color: var(--text-muted);
}

.sec-footer {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-top: 12px;
  padding-top: 10px;
  border-top: 1px solid var(--border);
  font-size: 11px;
  color: var(--text-dim);
}

.sec-footer-link {
  color: var(--text-muted);
  text-decoration: none;
  transition: color 0.2s ease;
}

.sec-footer-link:hover {
  color: var(--accent);
}

/* Quick Hub Cards */
.hub-strip {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 14px;
  margin-bottom: 40px;
  animation: fadeInUp 0.7s 0.45s cubic-bezier(0.16, 1, 0.3, 1) both;
}

.hub-card {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 14px 18px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 18px;
  text-decoration: none;
  color: var(--text);
  backdrop-filter: blur(18px);
  transition: all 0.25s ease;
}

.hub-card:hover {
  transform: translateY(-3px);
  border-color: var(--accent);
  background: var(--surface-hover);
  box-shadow: 0 8px 24px rgba(99, 102, 241, 0.2);
}

.hub-icon {
  width: 40px;
  height: 40px;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 18px;
  background: rgba(99, 102, 241, 0.12);
  color: #818cf8;
}

.hub-title {
  font-size: 13.5px;
  font-weight: 700;
  color: var(--text);
}

.hub-desc {
  font-size: 11px;
  color: var(--text-muted);
  margin-top: 2px;
}

/* Footer */
.footer {
  margin-top: auto;
  display: flex;
  align-items: center;
  justify-content: space-between;
  flex-wrap: wrap;
  gap: 12px;
  padding-top: 20px;
  border-top: 1px solid var(--border);
  font-size: 12px;
  color: var(--text-muted);
}

.footer-links {
  display: flex;
  gap: 14px;
}

.footer-link {
  color: var(--text-muted);
  text-decoration: none;
  transition: color 0.2s ease;
}

.footer-link:hover {
  color: var(--text);
}

/* Shortcuts Modal */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.75);
  backdrop-filter: blur(12px);
  z-index: 100;
  display: flex;
  align-items: center;
  justify-content: center;
  opacity: 0;
  pointer-events: none;
  transition: all 0.25s ease;
}

.modal-overlay.active {
  opacity: 1;
  pointer-events: auto;
}

.modal-card {
  background: var(--surface-card);
  border: 1px solid rgba(99, 102, 241, 0.35);
  border-radius: 20px;
  padding: 24px;
  max-width: 560px;
  width: 90%;
  box-shadow: 0 24px 64px rgba(0, 0, 0, 0.45), 0 0 32px rgba(99, 102, 241, 0.2);
  transform: translateY(20px) scale(0.95);
  transition: all 0.3s cubic-bezier(0.34, 1.56, 0.64, 1);
  backdrop-filter: blur(24px);
}

.modal-overlay.active .modal-card {
  transform: translateY(0) scale(1);
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 20px;
}

.modal-title {
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 700;
  color: var(--text);
}

.modal-close {
  background: rgba(255, 255, 255, 0.06);
  border: 1px solid var(--border);
  color: var(--text-muted);
  width: 32px;
  height: 32px;
  border-radius: 50%;
  cursor: pointer;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  transition: all 0.2s ease;
}

.modal-close:hover {
  background: rgba(239, 68, 68, 0.2);
  color: #ef4444;
}

.modal-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}

.sc-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 8px 12px;
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid var(--border);
  border-radius: 10px;
}

.sc-key {
  font-family: var(--font-mono);
  font-size: 11px;
  font-weight: 700;
  color: #818cf8;
  background: rgba(99, 102, 241, 0.12);
  padding: 3px 6px;
  border-radius: 6px;
  border: 1px solid rgba(99, 102, 241, 0.25);
}

.sc-desc {
  font-size: 12px;
  color: var(--text-muted);
}

/* Command Action Strip & Jump-Back-In Widget */
.command-strip {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  gap: 8px;
  margin: -20px auto 34px;
  max-width: 960px;
  padding: 10px 14px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 16px;
  backdrop-filter: blur(20px);
  animation: fadeInUp 0.7s 0.25s cubic-bezier(0.16, 1, 0.3, 1) both;
}

.action-pill {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 11px;
  border-radius: 10px;
  font-size: 12px;
  font-weight: 600;
  text-decoration: none;
  color: var(--text-muted);
  background: rgba(255, 255, 255, 0.03);
  border: 1px solid var(--border);
  transition: all 0.2s ease;
}

.action-pill:hover {
  color: var(--text);
  border-color: var(--accent);
  background: rgba(99, 102, 241, 0.14);
  transform: translateY(-1px);
}

.action-pill.on {
  color: #10b981;
  border-color: rgba(16, 185, 129, 0.35);
  background: rgba(16, 185, 129, 0.1);
}

.strip-sep {
  width: 1px;
  height: 18px;
  background: var(--border);
  margin: 0 2px;
}

.theme-pills {
  display: inline-flex;
  align-items: center;
  gap: 5px;
}

.theme-pill {
  padding: 4px 9px;
  border-radius: 8px;
  font-size: 11px;
  font-weight: 600;
  text-decoration: none;
  color: var(--text-dim);
  border: 1px solid var(--border);
  transition: all 0.2s ease;
  text-transform: capitalize;
}

.theme-pill:hover { color: var(--text); border-color: var(--accent); }
.theme-pill.active { background: rgba(99, 102, 241, 0.22); color: #818cf8; border-color: var(--accent); }

.nav-count {
  font-size: 10px;
  font-weight: 700;
  padding: 1px 6px;
  border-radius: 99px;
  background: rgba(99, 102, 241, 0.18);
  color: #818cf8;
}

.status-link {
  color: inherit;
  text-decoration: none;
  display: inline-flex;
  align-items: center;
  gap: 5px;
  transition: color 0.2s ease;
}

.status-link:hover { color: var(--text); }

.recent-tabs {
  display: flex;
  gap: 6px;
}

.tab-btn {
  background: rgba(255, 255, 255, 0.04);
  border: 1px solid var(--border);
  color: var(--text-muted);
  padding: 4px 10px;
  border-radius: 8px;
  font-size: 11.5px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
}

.tab-btn.active {
  background: rgba(99, 102, 241, 0.22);
  border-color: var(--accent);
  color: var(--text);
}

.recent-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  flex: 1;
}

.recent-row {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 9px 11px;
  border-radius: 12px;
  background: rgba(0, 0, 0, 0.14);
  border: 1px solid var(--border);
  text-decoration: none;
  color: var(--text);
  transition: all 0.2s ease;
}

.recent-row:hover {
  background: rgba(99, 102, 241, 0.12);
  border-color: rgba(99, 102, 241, 0.35);
  transform: translateX(2px);
}

.recent-badge {
  width: 28px;
  height: 28px;
  border-radius: 8px;
  background: rgba(99, 102, 241, 0.22);
  color: #818cf8;
  font-size: 12px;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.recent-badge.bm {
  background: rgba(245, 158, 11, 0.18);
  color: #fbbf24;
}

.recent-meta {
  display: flex;
  flex-direction: column;
  min-width: 0;
  flex: 1;
}

.recent-title {
  font-size: 12.5px;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.recent-host {
  font-size: 10.5px;
  color: var(--text-dim);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.recent-arrow {
  font-size: 12px;
  color: var(--text-dim);
}

.recent-empty {
  padding: 22px 12px;
  text-align: center;
  font-size: 12px;
  color: var(--text-dim);
  background: rgba(0, 0, 0, 0.1);
  border-radius: 12px;
  border: 1px dashed var(--border);
}

.sec-dot.off {
  background: #f59e0b;
  box-shadow: 0 0 8px rgba(245, 158, 11, 0.55);
}

/* Dynamic Layout Overrides */
.topbar { border-radius: var(--radius-lg) !important; }
.brand-badge-icon { border-radius: var(--radius-md) !important; }
.nav-chip { border-radius: var(--radius-sm) !important; }
.search-wrapper { border-radius: var(--radius-lg) !important; }
.engine-chip { border-radius: var(--radius-sm) !important; }
.sh-tile, .sh-icon { border-radius: var(--radius-md) !important; }
.quick-notes, .notes-area { border-radius: var(--radius-md) !important; }
.modal-card { border-radius: var(--radius-lg) !important; }
</style>
</head>
<body>

<canvas id='particleCanvas'></canvas>

<div class='container'>

  <!-- Topbar -->
  <div class='topbar'>
    <a class='brand' href='black://check-updates' title='Click to check for updates (" + SimpleHtmlEncode(UpdateManager.CurrentVersion) + @")'>
      <div class='brand-badge-icon'>&#x1F310;</div>
      <div>
        <div class='brand-text-title'>Black Browser</div>
        <div class='brand-sub'>" + SimpleHtmlEncode(UpdateManager.CurrentVersion) + @" &bull; Private &amp; Secure</div>
      </div>
    </a>
    <div class='nav-chips'>
      <a class='nav-chip' href='black://bookmarks'>&#x2B50; Bookmarks <span class='nav-count'>" + allBookmarks.Count + @"</span></a>
      <a class='nav-chip' href='black://history'>&#x1F4DC; History <span class='nav-count'>" + allHistory.Count + @"</span></a>
      <a class='nav-chip' href='black://downloads'>&#x1F4E5; Downloads <span class='nav-count'>" + downloadsCount + @"</span></a>
      <a class='nav-chip' href='black://passwords'>&#x1F510; Passwords <span class='nav-count'>" + allPasswords.Count + @"</span></a>
      <a class='nav-chip' href='black://cookies'>&#x1F36A; Cookies</a>
      <a class='nav-chip' href='black://extensions'>&#x1F9E9; Extensions <span class='nav-count'>" + allExtensions.Count + @"</span></a>
      <a class='nav-chip' href='black://settings'>&#x2699;&#xFE0F; Settings</a>
    </div>
  </div>

  <!-- Hero Clock & Greeting -->
  <div class='hero'>
    <div class='hero-clock' id='clock' style='" + (SettingsStore.ShowClock ? "" : "display:none;") + @"'>12:00:00 PM</div>
    <div class='hero-greeting' id='greeting'>Welcome back, " + SimpleHtmlEncode(userName) + @"</div>
    <div class='hero-status-pill' style='" + (SettingsStore.ShowTelemetry ? "" : "display:none;") + @"'>
      <span id='date'>Loading date...</span>
      <span>&bull;</span>
      <a class='status-link' href='black://toggle-adshield' title='Toggle AdShield protection'>" + (adShieldEnabled ? "&#x1F6E1;&#xFE0F; AdShield Active (" + blockedAds + " blocked)" : "&#x23F8;&#xFE0F; AdShield Paused") + @"</a>
      <span>&bull;</span>
      <a class='status-link' href='black://toggle-https' title='Toggle HTTPS-First enforcement'>" + (httpsFirst ? "&#x1F512; HTTPS-First On" : "&#x1F513; HTTPS-First Off") + @"</a>
      <span>&bull;</span>
      <a class='status-link' href='black://optimize-memory' title='Click to optimize working set RAM'>&#x26A1; " + ramMb + @" MB RAM</a>
    </div>
  </div>

  <!-- Omni Search -->
  <div class='search-section'>
    <form id='homeSearch' onsubmit='return doSearch(event)'>
      <div class='search-box'>
        <span class='search-icon'>&#x1F50D;</span>
        <input class='search-input' id='searchInput' type='text' placeholder='Search with " + SimpleHtmlEncode(currentEngine) + @" or enter URL...' autofocus autocomplete='off'>
        <span class='search-shortcut-hint'>/</span>
        <button class='search-btn' type='submit'>Search</button>
      </div>
      <div class='engine-chips'>
        <a class='engine-chip" + searchEngineGoogle + @"' href='black://setsearch?engine=google'>Google</a>
        <a class='engine-chip" + searchEngineDdg + @"' href='black://setsearch?engine=duckduckgo'>DuckDuckGo</a>
        <a class='engine-chip" + searchEngineBing + @"' href='black://setsearch?engine=bing'>Bing</a>
        <a class='engine-chip" + searchEngineBrave + @"' href='black://setsearch?engine=brave'>Brave</a>
        <a class='engine-chip" + searchEngineYoutube + @"' href='black://setsearch?engine=youtube'>YouTube</a>
        <a class='engine-chip" + searchEngineGithub + @"' href='black://setsearch?engine=github'>GitHub</a>
        <a class='engine-chip" + searchEngineWiki + @"' href='black://setsearch?engine=wikipedia'>Wikipedia</a>
        <a class='engine-chip" + searchEngineStartpage + @"' href='black://setsearch?engine=startpage'>Startpage</a>
      </div>
    </form>
  </div>

  <!-- Full Browser Controls & Theme Switcher Strip -->
  <div class='command-strip'>
    <a class='action-pill' href='black://new-private' title='Open Private Tab (Ctrl+Shift+P)'>&#x1F575;&#xFE0F; Private Tab</a>
    <a class='action-pill' href='black://reopen-tab' title='Reopen Last Closed Tab (Ctrl+Shift+T)'>&#x21A9;&#xFE0F; Reopen Tab</a>
    <a class='action-pill' href='black://toggle-sidebar' title='Toggle Quick Sidebar (Ctrl+Shift+B)'>&#x1F4C1; Sidebar</a>
    <a class='action-pill' href='black://toggle-theme' title='Toggle Dark / Light Theme (Ctrl+Shift+D)'>" + (isDarkMode ? "&#x2600;&#xFE0F; Light Mode" : "&#x1F319; Dark Mode") + @"</a>
    <a class='action-pill' href='black://cycle-eyecare' title='Cycle Eye Care Blue-Light Filter (Ctrl+Shift+E)'>&#x1F441;&#xFE0F; Eye Care</a>
    <a class='action-pill" + (adShieldEnabled ? " on" : "") + @"' href='black://toggle-adshield' title='Pause or resume AdShield'>" + (adShieldEnabled ? "&#x1F6E1;&#xFE0F; AdShield ON" : "&#x23F8;&#xFE0F; AdShield OFF") + @"</a>
    <a class='action-pill' href='black://qr' title='Generate QR Code for Current Page'>&#x1F4F1; QR</a>
    <a class='action-pill' href='black://screenshot' title='Capture Viewport Screenshot'>&#x1F4F8; Capture</a>
    <a class='action-pill' href='black://set-default' title='Set Black Browser as Default Windows Browser'>&#x1F310; Set Default</a>
    <span class='strip-sep'></span>
    <div class='theme-pills' title='New Tab Wallpaper Theme'>
      <a class='theme-pill" + (selectedBg == "aurora" ? " active" : "") + @"' href='black://setbg?bg=aurora'>Aurora</a>
      <a class='theme-pill" + (selectedBg == "cyberpunk" ? " active" : "") + @"' href='black://setbg?bg=cyberpunk'>Cyberpunk</a>
      <a class='theme-pill" + (selectedBg == "nebula" ? " active" : "") + @"' href='black://setbg?bg=nebula'>Nebula</a>
      <a class='theme-pill" + (selectedBg == "obsidian" ? " active" : "") + @"' href='black://setbg?bg=obsidian'>Obsidian</a>
      <a class='theme-pill" + (selectedBg == "light" ? " active" : "") + @"' href='black://setbg?bg=light'>Light</a>
    </div>
  </div>

  <!-- Shortcuts Section -->
  <div class='section-header'>
    <div class='section-title'>&#x2B50; Speed Dial Shortcuts</div>
  </div>
  <div class='sh-grid'>
    " + shortcutsSection + @"
  </div>

  <!-- Dashboard Grid -->
  <div class='dashboard-grid'>

    <!-- Jump Back In: Recent History & Bookmarks -->
    <div class='dash-card'>
      <div class='card-header'>
        <div class='card-title'>&#x1F552; Jump Back In</div>
        <div class='recent-tabs'>
          <button type='button' class='tab-btn active' id='tabBtnRecent' onclick='switchJumpTab(&quot;recent&quot;)'>Recent</button>
          <button type='button' class='tab-btn' id='tabBtnBookmarks' onclick='switchJumpTab(&quot;bookmarks&quot;)'>Bookmarks</button>
        </div>
      </div>
      <div class='recent-list' id='jumpRecentPane'>" + recentHistSb.ToString() + @"</div>
      <div class='recent-list' id='jumpBookmarksPane' style='display:none;'>" + recentBmSb.ToString() + @"</div>
      <div class='sec-footer'>
        <a class='sec-footer-link' href='black://history'>All History (" + allHistory.Count + @")</a>
        <a class='sec-footer-link' href='black://bookmarks'>All Bookmarks (" + allBookmarks.Count + @")</a>
      </div>
    </div>

    <!-- Scratchpad Widget (Synced with Dark Notes) -->
    <div class='dash-card' style='" + (SettingsStore.ShowNotes ? "" : "display:none;") + @"'>
      <div class='card-header'>
        <div class='card-title'>&#x1F4DD; Persistent Quick Notes</div>
        <div class='card-actions'>
          <button class='btn-mini' onclick='copyScratchpad(this)'>&#x1F4CB; Copy</button>
          <button class='btn-mini' onclick='clearScratchpad()'>&#x1F5D1;&#xFE0F; Clear</button>
        </div>
      </div>
      <textarea class='scratchpad-area' id='scratchpad' placeholder='Type instant notes, prompts, or snippets (synced with Ctrl+Shift+N Dark Notes)...'>" + SimpleHtmlEncode(syncedNotesText) + @"</textarea>
      <div class='scratchpad-footer'>
        <span id='scratchCount'>" + syncedNotesText.Length + @" characters</span>
        <span>&#x1F512; Synced Local Notes (dark_notes.txt)</span>
      </div>
    </div>

    <!-- Security & Privacy Status -->
    <div class='dash-card'>
      <div class='card-header'>
        <div class='card-title'>&#x1F6E1;&#xFE0F; Security &amp; Architecture Status</div>
        <div class='card-actions'>
          <a class='btn-mini' href='black://passwords' style='text-decoration:none;'>&#x1F510; Vault</a>
          <a class='btn-mini' href='black://cookies' style='text-decoration:none;'>&#x1F36A; Cookies</a>
          <a class='btn-mini' href='black://about' style='text-decoration:none;'>&#x2139;&#xFE0F; About</a>
        </div>
      </div>
      <div class='security-grid'>
        <a class='sec-pill' href='black://toggle-adshield' style='text-decoration:none;'>
          <div class='sec-pill-title'><span class='sec-dot" + (adShieldEnabled ? "" : " off") + @"'></span>&#x1F6E1;&#xFE0F; 3-Layer AdShield</div>
          <div class='sec-pill-sub'>" + (adShieldEnabled ? ("Active (" + disabledSitesCount + " site exceptions)") : "Paused globally — click to enable") + @"</div>
        </a>
        <a class='sec-pill' href='black://passwords' style='text-decoration:none;'>
          <div class='sec-pill-title'><span class='sec-dot'></span>&#x1F510; Windows DPAPI Vault</div>
          <div class='sec-pill-sub'>" + allPasswords.Count + @" encrypted credentials</div>
        </a>
        <a class='sec-pill' href='black://toggle-https' style='text-decoration:none;'>
          <div class='sec-pill-title'><span class='sec-dot" + (httpsFirst ? "" : " off") + @"'></span>&#x1F512; HTTPS-First Engine</div>
          <div class='sec-pill-sub'>" + (httpsFirst ? "Auto-upgrading plain HTTP" : "Optional — click to enable") + @"</div>
        </a>
        <div class='sec-pill'>
          <div class='sec-pill-title'><span class='sec-dot'></span>&#x26A1; Zero Cloud Telemetry</div>
          <div class='sec-pill-sub'>100% local %LOCALAPPDATA% profile</div>
        </div>
        <div class='sec-pill'>
          <div class='sec-pill-title'><span class='sec-dot'></span>&#x1F9E0; LRU Tab Sentinel</div>
          <div class='sec-pill-sub'>Audio-aware background sleep</div>
        </div>
        <a class='sec-pill' href='black://settings' style='text-decoration:none;'>
          <div class='sec-pill-title'><span class='sec-dot" + (restoreSession ? "" : " off") + @"'></span>&#x1F36A; Session Restore</div>
          <div class='sec-pill-sub'>" + (restoreSession ? "Restores open tabs on launch" : "Fresh session on launch") + @"</div>
        </a>
      </div>
      <div class='sec-footer'>
        <span>&#x1F7E2; " + secActiveCount + @"/6 Privacy Controls Active</span>
        <a class='sec-footer-link' href='black://about'>Architecture PRD &bull; MIT License</a>
      </div>
    </div>

    <!-- Live Engine Telemetry & Performance -->
    <div class='dash-card' style='" + (SettingsStore.ShowTelemetry ? "" : "display:none;") + @"'>
      <div class='card-header'>
        <div class='card-title'>&#x26A1; Live Engine Telemetry</div>
        <div class='card-actions'>
          <a class='btn-mini' href='black://optimize-memory' style='text-decoration:none;'>&#x1F9F9; Purge RAM</a>
          <a class='btn-mini' href='black://check-updates' style='text-decoration:none;'>&#x1F680; Updates</a>
        </div>
      </div>
      <div class='telemetry-grid'>
        <div class='tel-item'>
          <div class='tel-label'>&#x1F9E0; Process Memory</div>
          <div class='tel-value'>" + ramMb + @" MB</div>
          <div class='tel-bar-bg'><div class='tel-bar-fill' style='width:" + ramPct + @"%;'></div></div>
        </div>
        <div class='tel-item'>
          <div class='tel-label'>&#x1F6E1;&#xFE0F; Session Ads Blocked</div>
          <div class='tel-value'>" + blockedAds + @"</div>
          <div class='tel-bar-bg'><div class='tel-bar-fill' style='width:" + (adShieldEnabled ? "95%" : "20%") + @";background:" + (adShieldEnabled ? "var(--accent-emerald)" : "#f59e0b") + @";'></div></div>
        </div>
        <div class='tel-item'>
          <div class='tel-label'>&#x2699;&#xFE0F; Runtime Core</div>
          <div class='tel-value'>" + SimpleHtmlEncode(wv2Short) + @"</div>
          <div class='tel-sub'>Evergreen Chromium</div>
        </div>
        <div class='tel-item'>
          <div class='tel-label'>&#x1F9E9; Local Library</div>
          <div class='tel-value'>" + allExtensions.Count + @" Ext &bull; " + downloadsCount + @" DL</div>
          <div class='tel-sub'>Unpacked &amp; Managed</div>
        </div>
      </div>
      <div class='sec-footer'>
        <span>&#x1F30C; Live Process Telemetry</span>
        <a class='sec-footer-link' href='black://set-default'>Default Browser &bull; Win 10/11</a>
      </div>
    </div>

  </div>

  <!-- Quick Hub Links -->
  <div class='hub-strip'>
    <a class='hub-card' href='black://extensions'>
      <div class='hub-icon'>&#x1F9E9;</div>
      <div>
        <div class='hub-title'>Extension Manager</div>
        <div class='hub-desc'>" + allExtensions.Count + @" installed &bull; unpacked MV2</div>
      </div>
    </a>
    <a class='hub-card' href='black://downloads'>
      <div class='hub-icon'>&#x1F4E5;</div>
      <div>
        <div class='hub-title'>Downloads Manager</div>
        <div class='hub-desc'>" + downloadsCount + @" recent files saved locally</div>
      </div>
    </a>
    <a class='hub-card' href='black://settings'>
      <div class='hub-icon'>&#x2699;&#xFE0F;</div>
      <div>
        <div class='hub-title'>Browser Preferences</div>
        <div class='hub-desc'>Appearance, privacy &amp; search</div>
      </div>
    </a>
    <a class='hub-card' href='black://doc-prd'>
      <div class='hub-icon'>&#x1F4D6;</div>
      <div>
        <div class='hub-title'>Architecture Docs</div>
        <div class='hub-desc'>PRD, App Flow &amp; UI specs</div>
      </div>
    </a>
  </div>

  <!-- Footer -->
  <div class='footer'>
    <div>&#x1F310; Black Browser Engine &bull; Private, Fast &bull; MIT License</div>
    <div class='footer-links'>
      <a class='footer-link' href='javascript:void(0)' onclick='openShortcutsModal()'>&#x2328;&#xFE0F; Shortcuts (?)</a>
      <a class='footer-link' href='black://about'>About</a>
      <a class='footer-link' href='black://terms'>Terms</a>
      <a class='footer-link' href='black://privacy'>Privacy</a>
    </div>
  </div>

</div>

<!-- Shortcuts Modal -->
<div class='modal-overlay' id='shortcutsModal' onclick='if(event.target===this)closeShortcutsModal()'>
  <div class='modal-card'>
    <div class='modal-header'>
      <div class='modal-title'>&#x2328;&#xFE0F; Keyboard Shortcuts</div>
      <button class='modal-close' onclick='closeShortcutsModal()'>&#x2715;</button>
    </div>
    <div class='modal-grid'>
      <div class='sc-item'><span class='sc-key'>Ctrl + T</span><span class='sc-desc'>New Tab</span></div>
      <div class='sc-item'><span class='sc-key'>Ctrl + W</span><span class='sc-desc'>Close Tab</span></div>
      <div class='sc-item'><span class='sc-key'>Ctrl + Shift + T</span><span class='sc-desc'>Reopen Closed Tab</span></div>
      <div class='sc-item'><span class='sc-key'>Ctrl + Shift + P</span><span class='sc-desc'>New Private Tab</span></div>
      <div class='sc-item'><span class='sc-key'>Ctrl + H</span><span class='sc-desc'>History</span></div>
      <div class='sc-item'><span class='sc-key'>Ctrl + J</span><span class='sc-desc'>Downloads</span></div>
      <div class='sc-item'><span class='sc-key'>Ctrl + Shift + B</span><span class='sc-desc'>Toggle Sidebar</span></div>
      <div class='sc-item'><span class='sc-key'>Ctrl + Shift + D</span><span class='sc-desc'>Dark / Light Theme</span></div>
      <div class='sc-item'><span class='sc-key'>Ctrl + Shift + E</span><span class='sc-desc'>Cycle Eye Care Filter</span></div>
      <div class='sc-item'><span class='sc-key'>Ctrl + Shift + R</span><span class='sc-desc'>Reader Mode</span></div>
      <div class='sc-item'><span class='sc-key'>/</span><span class='sc-desc'>Focus Search</span></div>
      <div class='sc-item'><span class='sc-key'>?</span><span class='sc-desc'>Shortcuts Guide</span></div>
      <div class='sc-item'><span class='sc-key'>F11</span><span class='sc-desc'>Fullscreen</span></div>
    </div>
  </div>
</div>

<script>
// Jump-Back-In Tab Switcher
function switchJumpTab(mode) {
  var rec = document.getElementById('jumpRecentPane');
  var bms = document.getElementById('jumpBookmarksPane');
  var bRec = document.getElementById('tabBtnRecent');
  var bBm = document.getElementById('tabBtnBookmarks');
  if (!rec || !bms) return;
  if (mode === 'bookmarks') {
    rec.style.display = 'none';
    bms.style.display = 'flex';
    if (bRec) bRec.classList.remove('active');
    if (bBm) bBm.classList.add('active');
  } else {
    rec.style.display = 'flex';
    bms.style.display = 'none';
    if (bRec) bRec.classList.add('active');
    if (bBm) bBm.classList.remove('active');
  }
}

// Live Clock & Greeting
function pad(n){ return n < 10 ? '0' + n : '' + n; }
function updateClock() {
  var now = new Date();
  var h = now.getHours(), m = now.getMinutes(), s = now.getSeconds();
  var ampm = h >= 12 ? 'PM' : 'AM';
  var h12 = h % 12; h12 = h12 ? h12 : 12;
  var clockEl = document.getElementById('clock');
  if (clockEl) clockEl.textContent = h12 + ':' + pad(m) + ':' + pad(s) + ' ' + ampm;

  var greetEl = document.getElementById('greeting');
  if (greetEl) {
    var greeting = h < 12 ? 'Good Morning' : (h < 18 ? 'Good Afternoon' : 'Good Evening');
    greetEl.textContent = greeting + ', " + SimpleJsEncode(userName) + @"';
  }

  var days = ['Sunday','Monday','Tuesday','Wednesday','Thursday','Friday','Saturday'];
  var months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
  var dateEl = document.getElementById('date');
  if (dateEl) dateEl.textContent = days[now.getDay()] + ', ' + months[now.getMonth()] + ' ' + now.getDate() + ', ' + now.getFullYear();
}
updateClock();
setInterval(updateClock, 1000);

// Search Functionality
function removeDial(ev, btn) {
  ev.preventDefault();
  ev.stopPropagation();
  var wrap = btn.closest('.sh-tile-wrapper');
  if (!wrap) return;
  var title = wrap.getAttribute('data-rawtitle') || 'shortcut';
  var url = wrap.getAttribute('data-rawurl') || '';
  if (confirm('Remove shortcut: ' + title + '?')) {
    location.href = 'black://removedial?url=' + encodeURIComponent(url);
  }
}

var currentEngine = '" + currentEngine + @"';
var ENGINES = {
  google: 'https://www.google.com/search?q=',
  duckduckgo: 'https://duckduckgo.com/?q=',
  bing: 'https://www.bing.com/search?q=',
  brave: 'https://search.brave.com/search?q=',
  youtube: 'https://www.youtube.com/results?search_query=',
  github: 'https://github.com/search?q=',
  wikipedia: 'https://en.wikipedia.org/wiki/Special:Search?search=',
  startpage: 'https://www.startpage.com/sp/search?query=',
  yahoo: 'https://search.yahoo.com/search?p=',
  ecosia: 'https://www.ecosia.org/search?q='
};

function doSearch(e) {
  e.preventDefault();
  var q = document.getElementById('searchInput').value.trim();
  if (!q) return false;
  if (q.indexOf('.') >= 0 && q.indexOf(' ') < 0 && !q.startsWith('http')) {
    location.href = 'https://' + q;
  } else {
    location.href = (ENGINES[currentEngine] || ENGINES.google) + encodeURIComponent(q);
  }
  return false;
}

// Scratchpad Synced Local Persistence (dark_notes.txt + localStorage fallback)
function safeGetStorage(k) { try { return window.localStorage ? window.localStorage.getItem(k) : null; } catch(e) { return null; } }
function safeSetStorage(k, v) { try { if (window.localStorage) window.localStorage.setItem(k, v); } catch(e) {} }
function safeRemoveStorage(k) { try { if (window.localStorage) window.localStorage.removeItem(k); } catch(e) {} }

function syncNotesToHost(val) {
  safeSetStorage('bf_quick_notes', val);
  try {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage('notes:save:' + val);
    }
  } catch(e) {}
}

var sp = document.getElementById('scratchpad');
var spCount = document.getElementById('scratchCount');
var spSyncTimer = null;
if (sp) {
  if (!sp.value) {
    var saved = safeGetStorage('bf_quick_notes');
    if (saved) {
      sp.value = saved;
      syncNotesToHost(saved);
    }
  } else {
    safeSetStorage('bf_quick_notes', sp.value);
  }
  if (spCount) spCount.textContent = sp.value.length + ' characters';
  sp.addEventListener('input', function() {
    safeSetStorage('bf_quick_notes', sp.value);
    if (spCount) spCount.textContent = sp.value.length + ' characters';
    clearTimeout(spSyncTimer);
    spSyncTimer = setTimeout(function() { syncNotesToHost(sp.value); }, 250);
  });
}

function copyScratchpad(btn) {
  if (!sp) return;
  sp.select();
  try {
    navigator.clipboard.writeText(sp.value).then(function() {
      var orig = btn.innerHTML;
      btn.innerHTML = '&#x2714; Copied!';
      setTimeout(function() { btn.innerHTML = orig; }, 1500);
    });
  } catch(e){}
}

function clearScratchpad() {
  if (!sp) return;
  if (confirm('Clear scratchpad notes?')) {
    sp.value = '';
    safeRemoveStorage('bf_quick_notes');
    syncNotesToHost('');
    if (spCount) spCount.textContent = '0 characters';
  }
}

// Modal & Keyboard Shortcuts
function openShortcutsModal() {
  var m = document.getElementById('shortcutsModal');
  if (m) m.classList.add('active');
}

function closeShortcutsModal() {
  var m = document.getElementById('shortcutsModal');
  if (m) m.classList.remove('active');
}

window.addEventListener('keydown', function(e) {
  if (e.key === 'Escape') closeShortcutsModal();
  if (e.key === '/' && document.activeElement !== sp && document.activeElement !== document.getElementById('searchInput')) {
    e.preventDefault();
    document.getElementById('searchInput').focus();
  }
  if (e.key === '?' && document.activeElement !== sp && document.activeElement !== document.getElementById('searchInput')) {
    e.preventDefault();
    openShortcutsModal();
  }
});

// Particle Canvas Animation
(function initParticles() {
  var animationsEnabled = " + (SettingsStore.AnimationsEnabled ? "true" : "false") + @";
  if (!animationsEnabled) return;
  var canvas = document.getElementById('particleCanvas');
  if (!canvas) return;
  var ctx = canvas.getContext('2d');
  var width = canvas.width = window.innerWidth;
  var height = canvas.height = window.innerHeight;

  window.addEventListener('resize', function() {
    width = canvas.width = window.innerWidth;
    height = canvas.height = window.innerHeight;
  });

  var particles = [];
  var particleCount = Math.min(width > 800 ? 45 : 20, 50);

  for (var i = 0; i < particleCount; i++) {
    particles.push({
      x: Math.random() * width,
      y: Math.random() * height,
      vx: (Math.random() - 0.5) * 0.35,
      vy: (Math.random() - 0.5) * 0.35,
      radius: Math.random() * 1.6 + 0.6,
      alpha: Math.random() * 0.5 + 0.2
    });
  }

  function draw() {
    if (document.hidden) {
      setTimeout(function() { requestAnimationFrame(draw); }, 500);
      return;
    }
    ctx.clearRect(0, 0, width, height);
    for (var i = 0; i < particles.length; i++) {
      var p = particles[i];
      p.x += p.vx;
      p.y += p.vy;
      if (p.x < 0) p.x = width;
      if (p.x > width) p.x = 0;
      if (p.y < 0) p.y = height;
      if (p.y > height) p.y = 0;

      ctx.beginPath();
      ctx.arc(p.x, p.y, p.radius, 0, Math.PI * 2);
      ctx.fillStyle = 'rgba(129, 140, 248, ' + p.alpha + ')';
      ctx.fill();

      for (var j = i + 1; j < particles.length; j++) {
        var p2 = particles[j];
        var dx = p.x - p2.x;
        var dy = p.y - p2.y;
        var dist = Math.sqrt(dx * dx + dy * dy);
        if (dist < 100) {
          ctx.beginPath();
          ctx.moveTo(p.x, p.y);
          ctx.lineTo(p2.x, p2.y);
          ctx.strokeStyle = 'rgba(99, 102, 241, ' + (0.12 * (1 - dist / 100)) + ')';
          ctx.lineWidth = 0.7;
          ctx.stroke();
        }
      }
    }
    requestAnimationFrame(draw);
  }
  draw();
})();
</script>
</body>
</html>";

            return html;
        }

        public static string GetSpeedDialFilePath(bool isDarkMode)
        {
            string key = BuildSpeedDialKey(isDarkMode);
            if (!string.IsNullOrEmpty(_cachedFilePath) && _cachedFileKey == key)
            {
                string checkPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "black-webview2", "speeddial.html");
                if (File.Exists(checkPath)) return _cachedFilePath;
            }
            string html = GetHtml(isDarkMode);
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "black-webview2");

                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

                string filePath = Path.Combine(folder, "speeddial.html");
                File.WriteAllText(filePath, html, Encoding.UTF8);
                _cachedFileKey = key;
                _cachedFilePath = "file:///" + filePath.Replace("\\", "/");
                return _cachedFilePath;
            }
            catch
            {
                return "";
            }
        }

        // === 5 Vibe Coding Document Pages ===

        public static string GetPrdHtml()
        {
            return BuildDocPage(
                "PRD - Product Requirements Document",
                "DOC 01", "&#x1F4CB;", "#6366f1", "#818cf8",
                "linear-gradient(135deg,#6366f1,#4f46e5)",
                new string[]
                {
                    "<div class='sec-t'><span class='sn'>01</span> &#x1F3AF; Problem Statement</div>"
                    + "<div class='sec-b'>"
                    + "<div class='fld'><div class='fl'>Problem</div><div class='fc ed' contenteditable='true' data-p='Describe the core problem your app solves. Be specific about the pain...'></div></div>"
                    + "<div class='fld'><div class='fl'>Current Solution &amp; Flaws</div><div class='fc ed' contenteditable='true' data-p='What do users do today? Why does it fail them?'></div></div>"
                    + "<div class='fld'><div class='fl'>Opportunity</div><div class='fc ed' contenteditable='true' data-p='Why now? What makes this the right moment to build?'></div></div>"
                    + "</div>",

                    "<div class='sec-t'><span class='sn'>02</span> &#x1F465; Target Users</div>"
                    + "<div class='sec-b'><div class='tbl'>"
                    + "<div class='th'><span>Persona</span><span>Description</span><span>Primary Need</span><span>Pain Point</span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='Primary User'></span><span class='ed' contenteditable='true' data-p='Age, job, lifestyle...'></span><span class='ed' contenteditable='true' data-p='What they want...'></span><span class='ed' contenteditable='true' data-p='What frustrates them...'></span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='Secondary User'></span><span class='ed' contenteditable='true' data-p='Age, job, lifestyle...'></span><span class='ed' contenteditable='true' data-p='What they want...'></span><span class='ed' contenteditable='true' data-p='What frustrates them...'></span></div>"
                    + "</div></div>",

                    "<div class='sec-t'><span class='sn'>03</span> &#x2728; Core Features (MVP)</div>"
                    + "<div class='sec-b'>"
                    + "<div class='feat-list' id='featureList'>"
                    + "<div class='feat'><span class='p p1'>P1</span><span class='ed' contenteditable='true' data-p='Feature name and description...'></span></div>"
                    + "<div class='feat'><span class='p p1'>P1</span><span class='ed' contenteditable='true' data-p='Feature name and description...'></span></div>"
                    + "<div class='feat'><span class='p p2'>P2</span><span class='ed' contenteditable='true' data-p='Feature name and description...'></span></div>"
                    + "<div class='feat'><span class='p p2'>P2</span><span class='ed' contenteditable='true' data-p='Feature name and description...'></span></div>"
                    + "<div class='feat'><span class='p p3'>P3</span><span class='ed' contenteditable='true' data-p='Feature name and description...'></span></div>"
                    + "</div>"
                    + "<button class='add-btn' onclick='addFeat()'>+ Add Feature</button>"
                    + "</div>",

                    "<div class='sec-t'><span class='sn'>04</span> &#x1F4CA; Success Metrics (KPIs)</div>"
                    + "<div class='sec-b'><div class='tbl'>"
                    + "<div class='th'><span>Metric</span><span>Target</span><span>Measurement</span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='DAU / MAU'></span><span class='ed' contenteditable='true' data-p='1000 DAU in 30 days'></span><span class='ed' contenteditable='true' data-p='Analytics dashboard'></span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='Retention Rate'></span><span class='ed' contenteditable='true' data-p='>40% day-7 retention'></span><span class='ed' contenteditable='true' data-p='Mixpanel cohort'></span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='NPS Score'></span><span class='ed' contenteditable='true' data-p='>50 NPS'></span><span class='ed' contenteditable='true' data-p='In-app survey'></span></div>"
                    + "</div></div>",

                    "<div class='sec-t'><span class='sn'>05</span> &#x1F6AB; Out of Scope (v1)</div>"
                    + "<div class='sec-b'><div class='fld'><div class='fc ed' contenteditable='true' data-p='List what is explicitly NOT in v1 to keep scope tight&#10;- No social/sharing features&#10;- No native mobile app (web only)&#10;- No multi-language support'></div></div></div>"
                }
            );
        }

        public static string GetAppFlowHtml()
        {
            return BuildDocPage(
                "App Flow - User Journey &amp; Navigation",
                "DOC 02", "&#x1F5FA;&#xFE0F;", "#8b5cf6", "#a78bfa",
                "linear-gradient(135deg,#8b5cf6,#7c3aed)",
                new string[]
                {
                    "<div class='sec-t'><span class='sn'>01</span> &#x1F5A5;&#xFE0F; Screens &amp; Pages</div>"
                    + "<div class='sec-b'>"
                    + "<div class='scr-grid' id='screensGrid'>"
                    + "<div class='scr'><div class='scr-n'>01</div><div class='scr-nm ed' contenteditable='true' data-p='Landing Page'></div><div class='scr-d ed' contenteditable='true' data-p='Hero section, CTA, features overview. Entry point for all new users.'></div><div class='stags'><span class='stag'>public</span><span class='stag'>no-auth</span></div></div>"
                    + "<div class='scr'><div class='scr-n'>02</div><div class='scr-nm ed' contenteditable='true' data-p='Auth / Login'></div><div class='scr-d ed' contenteditable='true' data-p='Email + password, Google OAuth. Redirects to Dashboard on success.'></div><div class='stags'><span class='stag'>auth</span><span class='stag'>redirect</span></div></div>"
                    + "<div class='scr'><div class='scr-n'>03</div><div class='scr-nm ed' contenteditable='true' data-p='Dashboard'></div><div class='scr-d ed' contenteditable='true' data-p='Main app view, personalized content, key actions.'></div><div class='stags'><span class='stag'>protected</span><span class='stag'>main</span></div></div>"
                    + "<div class='scr'><div class='scr-n'>04</div><div class='scr-nm ed' contenteditable='true' data-p='Settings'></div><div class='scr-d ed' contenteditable='true' data-p='Profile, preferences, notifications, billing.'></div><div class='stags'><span class='stag'>protected</span></div></div>"
                    + "</div>"
                    + "<button class='add-btn' onclick='addScr()'>+ Add Screen</button>"
                    + "</div>",

                    "<div class='sec-t'><span class='sn'>02</span> &#x1F504; Navigation Flow</div>"
                    + "<div class='sec-b'><div class='flow'>"
                    + "<div class='fn start'>&#x1F310; Guest visits app</div>"
                    + "<div class='fa'>&#x2193;</div>"
                    + "<div class='fn'>Landing Page</div>"
                    + "<div class='fsplit'>"
                    + "<div class='fb'><div class='fa'>&#x2193; Sign Up</div><div class='fn'>Register</div><div class='fa'>&#x2193;</div><div class='fn'>Onboarding</div></div>"
                    + "<div class='fb'><div class='fa'>&#x2193; Login</div><div class='fn'>Auth Screen</div></div>"
                    + "</div>"
                    + "<div class='fa'>&#x2193; Authenticated</div>"
                    + "<div class='fn hi'>Dashboard (Main)</div>"
                    + "<div class='fd ed' contenteditable='true' data-p='Describe additional navigation flows, modal states, and edge cases here...'></div>"
                    + "</div></div>",

                    "<div class='sec-t'><span class='sn'>03</span> &#x1F4F1; Key User Journeys</div>"
                    + "<div class='sec-b'>"
                    + "<div class='journey'><div class='jt'>&#x1F3AF; Happy Path - New User</div>"
                    + "<div class='steps'>"
                    + "<div class='step'><span class='stepn'>1</span><span class='ed' contenteditable='true' data-p='User lands on homepage'></span></div>"
                    + "<div class='step'><span class='stepn'>2</span><span class='ed' contenteditable='true' data-p='Clicks Get Started CTA'></span></div>"
                    + "<div class='step'><span class='stepn'>3</span><span class='ed' contenteditable='true' data-p='Signs up with Google'></span></div>"
                    + "<div class='step'><span class='stepn'>4</span><span class='ed' contenteditable='true' data-p='Completes onboarding (3 steps)'></span></div>"
                    + "<div class='step'><span class='stepn'>5</span><span class='ed' contenteditable='true' data-p='Reaches dashboard - sees first value'></span></div>"
                    + "</div></div>"
                    + "<div class='journey' style='margin-top:14px'><div class='jt'>&#x26A1; Core Action - Power User</div>"
                    + "<div class='steps'>"
                    + "<div class='step'><span class='stepn'>1</span><span class='ed' contenteditable='true' data-p='Opens app directly to dashboard'></span></div>"
                    + "<div class='step'><span class='stepn'>2</span><span class='ed' contenteditable='true' data-p='Performs primary action in less than 2 taps'></span></div>"
                    + "<div class='step'><span class='stepn'>3</span><span class='ed' contenteditable='true' data-p='Sees confirmation / result'></span></div>"
                    + "</div></div>"
                    + "</div>",

                    "<div class='sec-t'><span class='sn'>04</span> &#x1F514; States &amp; Error Flows</div>"
                    + "<div class='sec-b'><div class='tbl'>"
                    + "<div class='th'><span>State</span><span>Trigger</span><span>UI Behavior</span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='Loading'></span><span class='ed' contenteditable='true' data-p='API request pending'></span><span class='ed' contenteditable='true' data-p='Skeleton loader shown'></span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='Empty State'></span><span class='ed' contenteditable='true' data-p='No data yet'></span><span class='ed' contenteditable='true' data-p='Illustration + CTA to add first item'></span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='Error'></span><span class='ed' contenteditable='true' data-p='API fails / network error'></span><span class='ed' contenteditable='true' data-p='Toast notification + retry button'></span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='Success'></span><span class='ed' contenteditable='true' data-p='Action completed'></span><span class='ed' contenteditable='true' data-p='Green toast, optimistic UI update'></span></div>"
                    + "</div></div>"
                }
            );
        }

        public static string GetTechStackHtml()
        {
            return BuildDocPage(
                "Tech Stack - Architecture &amp; Technology Choices",
                "DOC 03", "&#x2699;&#xFE0F;", "#0ea5e9", "#38bdf8",
                "linear-gradient(135deg,#0ea5e9,#0284c7)",
                new string[]
                {
                    "<div class='sec-t'><span class='sn'>01</span> &#x1F5A5;&#xFE0F; Frontend</div>"
                    + "<div class='sec-b'><div class='sg'>"
                    + "<div class='si'><div class='sl'>Framework</div><div class='sv ed' contenteditable='true' data-p='Next.js 14 / React 18 / Vue 3 / SvelteKit'></div></div>"
                    + "<div class='si'><div class='sl'>Language</div><div class='sv ed' contenteditable='true' data-p='TypeScript'></div></div>"
                    + "<div class='si'><div class='sl'>Styling</div><div class='sv ed' contenteditable='true' data-p='Tailwind CSS + shadcn/ui'></div></div>"
                    + "<div class='si'><div class='sl'>State Mgmt</div><div class='sv ed' contenteditable='true' data-p='Zustand / Redux Toolkit / Jotai'></div></div>"
                    + "<div class='si'><div class='sl'>Data Fetching</div><div class='sv ed' contenteditable='true' data-p='TanStack Query / SWR / tRPC'></div></div>"
                    + "<div class='si'><div class='sl'>Forms</div><div class='sv ed' contenteditable='true' data-p='React Hook Form + Zod'></div></div>"
                    + "<div class='si'><div class='sl'>Animations</div><div class='sv ed' contenteditable='true' data-p='Framer Motion / GSAP'></div></div>"
                    + "<div class='si'><div class='sl'>Testing</div><div class='sv ed' contenteditable='true' data-p='Vitest + Playwright'></div></div>"
                    + "</div></div>",

                    "<div class='sec-t'><span class='sn'>02</span> &#x26A1; Backend</div>"
                    + "<div class='sec-b'><div class='sg'>"
                    + "<div class='si'><div class='sl'>Runtime</div><div class='sv ed' contenteditable='true' data-p='Node.js 20 / Bun / Deno'></div></div>"
                    + "<div class='si'><div class='sl'>Framework</div><div class='sv ed' contenteditable='true' data-p='Express / Fastify / Hono / NestJS'></div></div>"
                    + "<div class='si'><div class='sl'>API Style</div><div class='sv ed' contenteditable='true' data-p='REST / GraphQL / tRPC'></div></div>"
                    + "<div class='si'><div class='sl'>Validation</div><div class='sv ed' contenteditable='true' data-p='Zod / Yup / Joi'></div></div>"
                    + "<div class='si'><div class='sl'>File Upload</div><div class='sv ed' contenteditable='true' data-p='UploadThing / AWS S3 / Cloudflare R2'></div></div>"
                    + "<div class='si'><div class='sl'>Background Jobs</div><div class='sv ed' contenteditable='true' data-p='BullMQ / Inngest / Trigger.dev'></div></div>"
                    + "<div class='si'><div class='sl'>Email</div><div class='sv ed' contenteditable='true' data-p='Resend + React Email'></div></div>"
                    + "<div class='si'><div class='sl'>Payments</div><div class='sv ed' contenteditable='true' data-p='Stripe'></div></div>"
                    + "</div></div>",

                    "<div class='sec-t'><span class='sn'>03</span> &#x1F5C4;&#xFE0F; Database &amp; Storage</div>"
                    + "<div class='sec-b'><div class='sg'>"
                    + "<div class='si'><div class='sl'>Primary DB</div><div class='sv ed' contenteditable='true' data-p='PostgreSQL (Supabase / Neon / PlanetScale)'></div></div>"
                    + "<div class='si'><div class='sl'>ORM</div><div class='sv ed' contenteditable='true' data-p='Prisma / Drizzle ORM'></div></div>"
                    + "<div class='si'><div class='sl'>Cache</div><div class='sv ed' contenteditable='true' data-p='Redis (Upstash)'></div></div>"
                    + "<div class='si'><div class='sl'>Search</div><div class='sv ed' contenteditable='true' data-p='Algolia / Typesense / pgvector'></div></div>"
                    + "<div class='si'><div class='sl'>File Storage</div><div class='sv ed' contenteditable='true' data-p='Supabase Storage / Cloudflare R2 / S3'></div></div>"
                    + "<div class='si'><div class='sl'>Vector DB</div><div class='sv ed' contenteditable='true' data-p='Pinecone / Weaviate / pgvector (for AI)'></div></div>"
                    + "</div></div>",

                    "<div class='sec-t'><span class='sn'>04</span> &#x1F512; Auth &amp; Security</div>"
                    + "<div class='sec-b'><div class='sg'>"
                    + "<div class='si'><div class='sl'>Auth Provider</div><div class='sv ed' contenteditable='true' data-p='Clerk / NextAuth.js / Supabase Auth / Auth0'></div></div>"
                    + "<div class='si'><div class='sl'>OAuth Providers</div><div class='sv ed' contenteditable='true' data-p='Google, GitHub, Discord'></div></div>"
                    + "<div class='si'><div class='sl'>Session Mgmt</div><div class='sv ed' contenteditable='true' data-p='JWT / Cookie sessions'></div></div>"
                    + "<div class='si'><div class='sl'>RBAC</div><div class='sv ed' contenteditable='true' data-p='Role-based access control strategy'></div></div>"
                    + "<div class='si'><div class='sl'>Rate Limiting</div><div class='sv ed' contenteditable='true' data-p='Upstash Ratelimit / nginx'></div></div>"
                    + "</div></div>",

                    "<div class='sec-t'><span class='sn'>05</span> &#x2601;&#xFE0F; Infrastructure &amp; Deployment</div>"
                    + "<div class='sec-b'><div class='sg'>"
                    + "<div class='si'><div class='sl'>Hosting</div><div class='sv ed' contenteditable='true' data-p='Vercel / Railway / Render / Fly.io'></div></div>"
                    + "<div class='si'><div class='sl'>CDN</div><div class='sv ed' contenteditable='true' data-p='Cloudflare / Vercel Edge Network'></div></div>"
                    + "<div class='si'><div class='sl'>CI/CD</div><div class='sv ed' contenteditable='true' data-p='GitHub Actions / Vercel auto-deploy'></div></div>"
                    + "<div class='si'><div class='sl'>Monitoring</div><div class='sv ed' contenteditable='true' data-p='Sentry + PostHog / Axiom'></div></div>"
                    + "<div class='si'><div class='sl'>Domain / DNS</div><div class='sv ed' contenteditable='true' data-p='Cloudflare DNS'></div></div>"
                    + "<div class='si'><div class='sl'>Environment</div><div class='sv ed' contenteditable='true' data-p='dev / staging / production'></div></div>"
                    + "</div></div>"
                }
            );
        }

        public static string GetFrontendGuidelinesHtml()
        {
            return BuildDocPage(
                "Frontend Guidelines - Design System &amp; UI Rules",
                "DOC 04", "&#x1F3A8;", "#ec4899", "#f472b6",
                "linear-gradient(135deg,#ec4899,#db2777)",
                new string[]
                {
                    "<div class='sec-t'><span class='sn'>01</span> &#x1F3A8; Color Palette</div>"
                    + "<div class='sec-b'>"
                    + "<div class='clr-grid' id='colorGrid'>"
                    + "<div class='cc'><div class='cs' style='background:#6366f1'></div><div class='ci'><div class='cn ed' contenteditable='true' data-p='Primary'>Primary</div><div class='chx ed mono' contenteditable='true' data-p='#6366f1'>#6366f1</div><div class='cu ed' contenteditable='true' data-p='Buttons, links, active states'></div></div></div>"
                    + "<div class='cc'><div class='cs' style='background:#8b5cf6'></div><div class='ci'><div class='cn ed' contenteditable='true' data-p='Secondary'>Secondary</div><div class='chx ed mono' contenteditable='true' data-p='#8b5cf6'>#8b5cf6</div><div class='cu ed' contenteditable='true' data-p='Accents, gradients'></div></div></div>"
                    + "<div class='cc'><div class='cs' style='background:#0f172a'></div><div class='ci'><div class='cn ed' contenteditable='true' data-p='Background'>Background</div><div class='chx ed mono' contenteditable='true' data-p='#0f172a'>#0f172a</div><div class='cu ed' contenteditable='true' data-p='Page background'></div></div></div>"
                    + "<div class='cc'><div class='cs' style='background:#10b981'></div><div class='ci'><div class='cn ed' contenteditable='true' data-p='Success'>Success</div><div class='chx ed mono' contenteditable='true' data-p='#10b981'>#10b981</div><div class='cu ed' contenteditable='true' data-p='Confirmations, positive states'></div></div></div>"
                    + "<div class='cc'><div class='cs' style='background:#ef4444'></div><div class='ci'><div class='cn ed' contenteditable='true' data-p='Error'>Error</div><div class='chx ed mono' contenteditable='true' data-p='#ef4444'>#ef4444</div><div class='cu ed' contenteditable='true' data-p='Errors, destructive actions'></div></div></div>"
                    + "<div class='cc'><div class='cs' style='background:#f59e0b'></div><div class='ci'><div class='cn ed' contenteditable='true' data-p='Warning'>Warning</div><div class='chx ed mono' contenteditable='true' data-p='#f59e0b'>#f59e0b</div><div class='cu ed' contenteditable='true' data-p='Alerts, caution states'></div></div></div>"
                    + "</div>"
                    + "<button class='add-btn' onclick='addClr()'>+ Add Color</button>"
                    + "</div>",

                    "<div class='sec-t'><span class='sn'>02</span> &#x270D;&#xFE0F; Typography</div>"
                    + "<div class='sec-b'>"
                    + "<div class='fld'><div class='fl'>Primary Font</div><div class='fc ed' contenteditable='true' data-p=\"e.g. Inter (body), Outfit (headings), JetBrains Mono (code)\"></div></div>"
                    + "<div class='tbl' style='margin-top:14px'>"
                    + "<div class='th'><span>Style</span><span>Size / Weight</span><span>Usage</span></div>"
                    + "<div class='tr'><span class='ed' contenteditable='true' data-p='Display / H1'></span><span class='mono ed' contenteditable='true' data-p='48px / 800 / -1.5px'></span><span class='ed' contenteditable='true' data-p='Hero titles, landing page'></span></div>"
                    + "<div class='tr'><span class='ed' contenteditable='true' data-p='H2 Section'></span><span class='mono ed' contenteditable='true' data-p='28px / 700 / -0.5px'></span><span class='ed' contenteditable='true' data-p='Section headings'></span></div>"
                    + "<div class='tr'><span class='ed' contenteditable='true' data-p='Body'></span><span class='mono ed' contenteditable='true' data-p='16px / 400 / 1.7lh'></span><span class='ed' contenteditable='true' data-p='Paragraphs, descriptions'></span></div>"
                    + "<div class='tr'><span class='ed' contenteditable='true' data-p='Caption'></span><span class='mono ed' contenteditable='true' data-p='12px / 600 / 1.2px ls'></span><span class='ed' contenteditable='true' data-p='Labels, metadata, tags'></span></div>"
                    + "<div class='tr'><span class='ed' contenteditable='true' data-p='Code'></span><span class='mono ed' contenteditable='true' data-p='14px / JetBrains Mono'></span><span class='ed' contenteditable='true' data-p='Inline code, snippets'></span></div>"
                    + "</div>"
                    + "</div>",

                    "<div class='sec-t'><span class='sn'>03</span> &#x1F4D0; Spacing &amp; Layout</div>"
                    + "<div class='sec-b'>"
                    + "<div class='sp-scale'>"
                    + "<div class='sp-r'><span class='mono sp-t'>spacing-1</span><div class='sp-b' style='width:4px;height:18px;background:#ec4899'></div><span class='sp-v'>4px</span><span class='ed sp-u' contenteditable='true' data-p='Icon gaps'></span></div>"
                    + "<div class='sp-r'><span class='mono sp-t'>spacing-2</span><div class='sp-b' style='width:8px;height:18px;background:#ec4899'></div><span class='sp-v'>8px</span><span class='ed sp-u' contenteditable='true' data-p='Tight spacing inside components'></span></div>"
                    + "<div class='sp-r'><span class='mono sp-t'>spacing-3</span><div class='sp-b' style='width:12px;height:18px;background:#ec4899'></div><span class='sp-v'>12px</span><span class='ed sp-u' contenteditable='true' data-p='Button padding'></span></div>"
                    + "<div class='sp-r'><span class='mono sp-t'>spacing-4</span><div class='sp-b' style='width:16px;height:18px;background:#ec4899'></div><span class='sp-v'>16px</span><span class='ed sp-u' contenteditable='true' data-p='Card padding, section gaps'></span></div>"
                    + "<div class='sp-r'><span class='mono sp-t'>spacing-6</span><div class='sp-b' style='width:24px;height:18px;background:#ec4899'></div><span class='sp-v'>24px</span><span class='ed sp-u' contenteditable='true' data-p='Large card padding'></span></div>"
                    + "<div class='sp-r'><span class='mono sp-t'>spacing-8</span><div class='sp-b' style='width:32px;height:18px;background:#ec4899'></div><span class='sp-v'>32px</span><span class='ed sp-u' contenteditable='true' data-p='Section margins'></span></div>"
                    + "<div class='sp-r'><span class='mono sp-t'>spacing-16</span><div class='sp-b' style='width:64px;height:18px;background:#ec4899'></div><span class='sp-v'>64px</span><span class='ed sp-u' contenteditable='true' data-p='Page margins, hero sections'></span></div>"
                    + "</div>"
                    + "<div class='fld' style='margin-top:16px'><div class='fl'>Grid System</div><div class='fc ed' contenteditable='true' data-p='e.g. 12-column grid, 24px gutter, max-width 1280px, breakpoints: sm=640px md=768px lg=1024px xl=1280px'></div></div>"
                    + "</div>",

                    "<div class='sec-t'><span class='sn'>04</span> &#x1F539; Component Library</div>"
                    + "<div class='sec-b'>"
                    + "<div class='fld'><div class='fl'>UI Library</div><div class='fc ed' contenteditable='true' data-p='e.g. shadcn/ui (primary), Radix UI primitives, custom components on top'></div></div>"
                    + "<div class='tbl' style='margin-top:14px'>"
                    + "<div class='th'><span>Component</span><span>Variants</span><span>Usage Rule</span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='Button'></span><span class='ed' contenteditable='true' data-p='primary, secondary, ghost, destructive'></span><span class='ed' contenteditable='true' data-p='Always use primary for main CTA per view'></span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='Input'></span><span class='ed' contenteditable='true' data-p='default, error, disabled'></span><span class='ed' contenteditable='true' data-p='Always include label + error state'></span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='Card'></span><span class='ed' contenteditable='true' data-p='flat, elevated, interactive'></span><span class='ed' contenteditable='true' data-p='Use elevated for primary content areas'></span></div>"
                    + "<div class='tr'><span class='mono ed' contenteditable='true' data-p='Modal'></span><span class='ed' contenteditable='true' data-p='sm, md, lg, fullscreen'></span><span class='ed' contenteditable='true' data-p='Always include close button + ESC key'></span></div>"
                    + "</div>"
                    + "</div>",

                    "<div class='sec-t'><span class='sn'>05</span> &#x2728; Animation &amp; Motion Rules</div>"
                    + "<div class='sec-b'><div class='tbl'>"
                    + "<div class='th'><span>Animation</span><span>Duration</span><span>Easing</span><span>When to Use</span></div>"
                    + "<div class='tr'><span class='ed' contenteditable='true' data-p='Micro-interaction'></span><span class='mono ed' contenteditable='true' data-p='150ms'></span><span class='mono ed' contenteditable='true' data-p='ease-out'></span><span class='ed' contenteditable='true' data-p='Button hover, toggle'></span></div>"
                    + "<div class='tr'><span class='ed' contenteditable='true' data-p='Page transition'></span><span class='mono ed' contenteditable='true' data-p='300ms'></span><span class='mono ed' contenteditable='true' data-p='cubic-bezier(0.4,0,0.2,1)'></span><span class='ed' contenteditable='true' data-p='Route changes, panel slides'></span></div>"
                    + "<div class='tr'><span class='ed' contenteditable='true' data-p='Modal open/close'></span><span class='mono ed' contenteditable='true' data-p='250ms'></span><span class='mono ed' contenteditable='true' data-p='spring()'></span><span class='ed' contenteditable='true' data-p='Dialog, sheet, drawer'></span></div>"
                    + "</div></div>"
                }
            );
        }

        public static string GetBackendStructureHtml()
        {
            return BuildDocPage(
                "Backend Structure - API &amp; Database Schema",
                "DOC 05", "&#x1F5C4;&#xFE0F;", "#10b981", "#34d399",
                "linear-gradient(135deg,#10b981,#059669)",
                new string[]
                {
                    "<div class='sec-t'><span class='sn'>01</span> &#x1F5C2;&#xFE0F; Database Schema</div>"
                    + "<div class='sec-b'>"
                    + "<div class='schema'>"
                    + "<div class='stbl'>"
                    + "<div class='sth'><span class='mono'>users</span><span class='stag'>primary table</span></div>"
                    + "<div class='scols'>"
                    + "<div class='scol-h'><span>Column</span><span>Type</span><span>Constraints</span><span>Description</span></div>"
                    + "<div class='scol'><span class='mono'>id</span><span class='mono tt'>uuid</span><span class='mono ct'>PK, default gen_random_uuid()</span><span class='ed' contenteditable='true' data-p='Primary key'></span></div>"
                    + "<div class='scol'><span class='mono'>email</span><span class='mono tt'>varchar(255)</span><span class='mono ct'>UNIQUE, NOT NULL</span><span class='ed' contenteditable='true' data-p='User email address'></span></div>"
                    + "<div class='scol'><span class='mono'>name</span><span class='mono tt'>varchar(100)</span><span class='mono ct'>NOT NULL</span><span class='ed' contenteditable='true' data-p='Display name'></span></div>"
                    + "<div class='scol'><span class='mono'>avatar_url</span><span class='mono tt'>text</span><span class='mono ct'>nullable</span><span class='ed' contenteditable='true' data-p='Profile picture URL'></span></div>"
                    + "<div class='scol'><span class='mono'>created_at</span><span class='mono tt'>timestamptz</span><span class='mono ct'>default now()</span><span class='ed' contenteditable='true' data-p='Account creation time'></span></div>"
                    + "</div></div>"
                    + "<div class='stbl' style='margin-top:14px'>"
                    + "<div class='sth'><span class='mono ed' contenteditable='true' data-p='your_table_name'></span><span class='stag'>custom table</span></div>"
                    + "<div class='scols'>"
                    + "<div class='scol-h'><span>Column</span><span>Type</span><span>Constraints</span><span>Description</span></div>"
                    + "<div class='scol'><span class='mono ed' contenteditable='true' data-p='id'></span><span class='mono tt ed' contenteditable='true' data-p='uuid'></span><span class='mono ct ed' contenteditable='true' data-p='PK'></span><span class='ed' contenteditable='true' data-p='...'></span></div>"
                    + "<div class='scol'><span class='mono ed' contenteditable='true' data-p='user_id'></span><span class='mono tt ed' contenteditable='true' data-p='uuid'></span><span class='mono ct ed' contenteditable='true' data-p='FK users.id'></span><span class='ed' contenteditable='true' data-p='Owner reference'></span></div>"
                    + "<div class='scol'><span class='mono ed' contenteditable='true' data-p='column_name'></span><span class='mono tt ed' contenteditable='true' data-p='text'></span><span class='mono ct ed' contenteditable='true' data-p='NOT NULL'></span><span class='ed' contenteditable='true' data-p='Description...'></span></div>"
                    + "</div></div>"
                    + "</div></div>",

                    "<div class='sec-t'><span class='sn'>02</span> &#x1F6E3;&#xFE0F; API Routes</div>"
                    + "<div class='sec-b'>"
                    + "<div class='api-tbl' id='apiTbl'>"
                    + "<div class='api-h'><span>Method</span><span>Endpoint</span><span>Auth</span><span>Description</span></div>"
                    + "<div class='api-r'><span class='m get'>GET</span><span class='mono ed' contenteditable='true' data-p='/api/v1/users/me'></span><span class='ab'>JWT</span><span class='ed' contenteditable='true' data-p='Get current user profile'></span></div>"
                    + "<div class='api-r'><span class='m post'>POST</span><span class='mono ed' contenteditable='true' data-p='/api/v1/auth/login'></span><span class='ab pub'>Public</span><span class='ed' contenteditable='true' data-p='Login with email/password, returns JWT'></span></div>"
                    + "<div class='api-r'><span class='m post'>POST</span><span class='mono ed' contenteditable='true' data-p='/api/v1/auth/register'></span><span class='ab pub'>Public</span><span class='ed' contenteditable='true' data-p='Create new user account'></span></div>"
                    + "<div class='api-r'><span class='m get'>GET</span><span class='mono ed' contenteditable='true' data-p='/api/v1/items'></span><span class='ab'>JWT</span><span class='ed' contenteditable='true' data-p='List items (paginated)'></span></div>"
                    + "<div class='api-r'><span class='m post'>POST</span><span class='mono ed' contenteditable='true' data-p='/api/v1/items'></span><span class='ab'>JWT</span><span class='ed' contenteditable='true' data-p='Create a new item'></span></div>"
                    + "<div class='api-r'><span class='m patch'>PATCH</span><span class='mono ed' contenteditable='true' data-p='/api/v1/items/:id'></span><span class='ab'>JWT</span><span class='ed' contenteditable='true' data-p='Update an item'></span></div>"
                    + "<div class='api-r'><span class='m del'>DELETE</span><span class='mono ed' contenteditable='true' data-p='/api/v1/items/:id'></span><span class='ab'>JWT</span><span class='ed' contenteditable='true' data-p='Delete an item'></span></div>"
                    + "</div>"
                    + "<button class='add-btn' onclick='addRoute()'>+ Add Route</button>"
                    + "</div>",

                    "<div class='sec-t'><span class='sn'>03</span> &#x1F512; Authentication Flow</div>"
                    + "<div class='sec-b'><div class='flow'>"
                    + "<div class='fn'>User submits credentials</div>"
                    + "<div class='fa'>&#x2193;</div>"
                    + "<div class='fn'>Validate input (Zod schema)</div>"
                    + "<div class='fa'>&#x2193;</div>"
                    + "<div class='fn'>Query DB for user by email</div>"
                    + "<div class='fsplit'>"
                    + "<div class='fb'><div class='fa'>&#x2713; Found</div><div class='fn'>Compare bcrypt hash</div></div>"
                    + "<div class='fb'><div class='fa'>&#x2717; Not Found</div><div class='fn err'>Return 401</div></div>"
                    + "</div>"
                    + "<div class='fa'>&#x2193;</div>"
                    + "<div class='fn hi'>Sign JWT (access 15m + refresh 30d)</div>"
                    + "<div class='fa'>&#x2193;</div>"
                    + "<div class='fn'>Set httpOnly cookie + return user</div>"
                    + "</div>"
                    + "<div class='fld' style='margin-top:16px'><div class='fl'>Additional Auth Notes</div><div class='fc ed' contenteditable='true' data-p='OAuth provider flows, password reset flow, email verification, MFA setup...'></div></div>"
                    + "</div>",

                    "<div class='sec-t'><span class='sn'>04</span> &#x1F4C1; Folder Structure</div>"
                    + "<div class='sec-b'><div class='code-block'><pre class='mono code-pre ed' contenteditable='true'>src/\n"
                    + "├── app/                    # Next.js App Router\n"
                    + "│   ├── (auth)/\n"
                    + "│   │   ├── login/\n"
                    + "│   │   └── register/\n"
                    + "│   ├── (dashboard)/\n"
                    + "│   │   ├── layout.tsx\n"
                    + "│   │   └── page.tsx\n"
                    + "│   └── api/\n"
                    + "│       └── [...route]/\n"
                    + "├── components/\n"
                    + "│   ├── ui/                 # shadcn/ui base\n"
                    + "│   └── features/           # Feature components\n"
                    + "├── lib/\n"
                    + "│   ├── db.ts               # Prisma client\n"
                    + "│   ├── auth.ts             # Auth utilities\n"
                    + "│   └── validations.ts      # Zod schemas\n"
                    + "├── hooks/                  # Custom React hooks\n"
                    + "├── stores/                 # Zustand stores\n"
                    + "└── types/                  # TypeScript types</pre></div></div>",

                    "<div class='sec-t'><span class='sn'>05</span> &#x2699;&#xFE0F; Environment Variables</div>"
                    + "<div class='sec-b'><div class='code-block'><pre class='mono code-pre env ed' contenteditable='true'># Database\n"
                    + "DATABASE_URL=\"postgresql://user:pass@host/db\"\n"
                    + "\n"
                    + "# Auth\n"
                    + "NEXTAUTH_SECRET=\"your-secret-key\"\n"
                    + "NEXTAUTH_URL=\"http://localhost:3000\"\n"
                    + "\n"
                    + "# OAuth\n"
                    + "GOOGLE_CLIENT_ID=\"...\"\n"
                    + "GOOGLE_CLIENT_SECRET=\"...\"\n"
                    + "\n"
                    + "# Email (Resend)\n"
                    + "RESEND_API_KEY=\"re_...\"\n"
                    + "FROM_EMAIL=\"noreply@yourdomain.com\"\n"
                    + "\n"
                    + "# Payments (Stripe)\n"
                    + "STRIPE_SECRET_KEY=\"sk_live_...\"\n"
                    + "STRIPE_WEBHOOK_SECRET=\"whsec_...\"\n"
                    + "NEXT_PUBLIC_STRIPE_PUBLISHABLE_KEY=\"pk_live_...\"\n"
                    + "\n"
                    + "# Storage (S3 / Cloudflare R2)\n"
                    + "AWS_ACCESS_KEY_ID=\"...\"\n"
                    + "AWS_SECRET_ACCESS_KEY=\"...\"\n"
                    + "AWS_BUCKET_NAME=\"...\"</pre></div></div>"
                }
            );
        }

        // === Shared Doc Page Builder ===

        private static string BuildDocPage(
            string title, string docNum, string icon,
            string color, string colorLight, string gradient,
            string[] sections)
        {
            var sb = new StringBuilder();
            sb.Append("<!DOCTYPE html>\n<html lang='en'>\n<head>\n");
            sb.Append("<meta charset='utf-8'>\n");
            sb.Append("<meta name='viewport' content='width=device-width, initial-scale=1'>\n");
            sb.Append("<title>" + SimpleHtmlEncode(title) + "</title>\n");

            sb.Append(@"<style>
*{margin:0;padding:0;box-sizing:border-box}
:root{--bg:#06070f;--surf:rgba(255,255,255,0.04);--surf2:rgba(255,255,255,0.07);--brd:rgba(255,255,255,0.08);--brd2:rgba(255,255,255,0.14);--txt:#e2e8f0;--dim:#64748b;--muted:#94a3b8;--accent:" + color + @";--al:" + colorLight + @";--mono:'Cascadia Code','Consolas',monospace;--font:'Segoe UI Variable Display','Segoe UI',system-ui,sans-serif}
html,body{min-height:100%;background:var(--bg);font-family:var(--font);color:var(--txt);-webkit-font-smoothing:antialiased;overflow-x:hidden}
.bg-orbs{position:fixed;inset:0;z-index:0;pointer-events:none;overflow:hidden}
.orb{position:absolute;border-radius:50%;filter:blur(120px);opacity:0.1;animation:oF 20s ease-in-out infinite alternate}
.orb-1{width:700px;height:700px;background:radial-gradient(circle," + color + @",transparent);top:-200px;left:-200px}
.orb-2{width:500px;height:500px;background:radial-gradient(circle," + colorLight + @",transparent);bottom:-100px;right:-100px;animation-delay:-8s}
@keyframes oF{0%{transform:translate(0,0)}100%{transform:translate(30px,20px)}}
.shell{position:relative;z-index:1;max-width:960px;margin:0 auto;padding:32px 32px 80px}
.doc-nav{display:flex;align-items:center;gap:12px;margin-bottom:40px;flex-wrap:wrap}
.nav-back{display:inline-flex;align-items:center;gap:8px;padding:10px 20px;border-radius:50px;background:rgba(255,255,255,0.08);border:1px solid rgba(255,255,255,0.18);color:#f1f5f9;font-size:13.5px;font-weight:600;cursor:pointer;transition:all .2s ease;text-decoration:none;backdrop-filter:blur(16px);box-shadow:0 2px 12px rgba(0,0,0,0.3)}
.nav-back:hover{border-color:rgba(99,102,241,0.5);background:rgba(99,102,241,0.2);color:#ffffff;transform:translateY(-1px)}
.nav-bc{font-size:13.5px;color:#94a3b8;display:flex;align-items:center;gap:8px;font-weight:500}
.nav-cpy{margin-left:auto;display:inline-flex;align-items:center;gap:8px;padding:10px 22px;border-radius:50px;font-size:13.5px;font-weight:700;cursor:pointer;transition:all .2s ease;border:none;font-family:var(--font);box-shadow:0 4px 16px rgba(0,0,0,0.3)}
.nav-cpy:hover{transform:translateY(-2px);filter:brightness(1.15)}
.doc-hdr{display:flex;align-items:flex-start;gap:24px;margin-bottom:48px;padding:28px;border-radius:20px;background:var(--surf);border:1px solid var(--brd);backdrop-filter:blur(16px);box-shadow:0 4px 24px rgba(0,0,0,0.3)}
.hdr-icon{width:64px;height:64px;border-radius:18px;display:flex;align-items:center;justify-content:center;font-size:30px;flex-shrink:0;box-shadow:0 4px 20px rgba(0,0,0,0.2)}
.hdr-num{font-family:var(--mono);font-size:11px;font-weight:500;letter-spacing:2px;color:var(--dim);margin-bottom:6px}
.hdr-title{font-family:'Outfit',sans-serif;font-size:28px;font-weight:800;letter-spacing:-0.8px;color:var(--txt);margin-bottom:8px;line-height:1.2}
.hdr-sub{font-size:14px;color:var(--muted);line-height:1.7;max-width:580px}
.hdr-line{width:60px;height:3px;border-radius:2px;margin-top:14px}
.doc-sec{margin-bottom:40px;animation:sIn .4s ease both}
@keyframes sIn{from{opacity:0;transform:translateY(12px)}to{opacity:1;transform:translateY(0)}}
.sec-t{display:flex;align-items:center;gap:12px;font-family:'Outfit',sans-serif;font-size:17px;font-weight:700;color:var(--txt);margin-bottom:18px;padding-bottom:12px;border-bottom:1px solid var(--brd)}
.sn{font-family:var(--mono);font-size:11px;color:var(--al);padding:3px 9px;border-radius:50px;letter-spacing:1px}
.sec-b{background:var(--surf);border:1px solid var(--brd);border-radius:16px;padding:24px;backdrop-filter:blur(12px)}
.fld{margin-bottom:16px}
.fld:last-child{margin-bottom:0}
.fl{font-size:10.5px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;color:var(--dim);margin-bottom:8px}
.fc{background:rgba(255,255,255,0.03);border:1px solid var(--brd);border-radius:10px;padding:12px 14px;font-size:14px;color:var(--txt);line-height:1.7;min-height:80px}
.ed{outline:none;transition:all .2s ease;border-radius:6px;padding:2px 4px;margin:-2px -4px;cursor:text;white-space:pre-wrap}
.ed:empty::before{content:attr(data-p);color:var(--dim);pointer-events:none;font-style:italic}
.fc.ed:focus,.ed:focus{background:rgba(255,255,255,0.04);box-shadow:0 0 0 2px " + color + @"40}
.mono{font-family:var(--mono)!important}
.tbl{border-radius:12px;overflow:hidden;border:1px solid var(--brd)}
.th{display:grid;grid-template-columns:repeat(auto-fit,minmax(80px,1fr));background:rgba(255,255,255,0.04);border-bottom:1px solid var(--brd)}
.th span,.tr span{padding:10px 14px;font-size:12px;display:block}
.th span{font-weight:700;letter-spacing:0.8px;text-transform:uppercase;color:var(--dim);font-size:11px}
.tr{display:grid;grid-template-columns:repeat(auto-fit,minmax(80px,1fr));border-bottom:1px solid rgba(255,255,255,0.04)}
.tr:last-child{border-bottom:none}
.tr span{font-size:13px;color:var(--muted)}
.tr:hover{background:rgba(255,255,255,0.02)}
.sg{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:12px}
.si{background:rgba(255,255,255,0.03);border:1px solid var(--brd);border-radius:12px;padding:14px 16px;transition:border-color .2s}
.si:hover{border-color:var(--brd2)}
.sl{font-size:10.5px;font-weight:700;letter-spacing:1.5px;text-transform:uppercase;color:var(--dim);margin-bottom:6px}
.sv{font-size:13.5px;color:var(--txt);font-weight:500;line-height:1.5}
.scr-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:14px;margin-bottom:14px}
.scr{background:rgba(255,255,255,0.03);border:1px solid var(--brd);border-radius:14px;padding:16px;transition:all .2s}
.scr:hover{border-color:var(--brd2);background:rgba(255,255,255,0.05)}
.scr-n{font-family:var(--mono);font-size:10px;color:var(--dim);margin-bottom:8px;letter-spacing:1px}
.scr-nm{font-size:14px;font-weight:700;color:var(--txt);margin-bottom:6px;line-height:1.4}
.scr-d{font-size:12px;color:var(--muted);line-height:1.6;margin-bottom:10px}
.stags{display:flex;gap:6px;flex-wrap:wrap}
.stag{font-size:10px;font-weight:600;padding:2px 8px;border-radius:50px;font-family:var(--mono)}
.flow{display:flex;flex-direction:column;align-items:center;gap:8px;padding:16px}
.fn{background:rgba(255,255,255,0.05);border:1px solid var(--brd);border-radius:10px;padding:10px 20px;font-size:13px;color:var(--muted);text-align:center;max-width:320px;width:100%}
.fn.hi{border-color:" + color + @"50}
.fn.err{background:rgba(239,68,68,0.15);border-color:rgba(239,68,68,0.4);color:#fca5a5}
.fn.start{border-color:" + color + @"35}
.fa{font-size:18px;color:var(--dim);opacity:0.6}
.fsplit{display:flex;gap:24px;justify-content:center;width:100%}
.fb{display:flex;flex-direction:column;align-items:center;gap:8px}
.fd{margin-top:12px;font-size:13px;color:var(--muted);text-align:left;width:100%;min-height:60px;padding:10px}
.journey{background:rgba(255,255,255,0.03);border:1px solid var(--brd);border-radius:12px;padding:16px}
.jt{font-size:13px;font-weight:700;color:var(--txt);margin-bottom:12px}
.steps{display:flex;flex-direction:column;gap:8px}
.step{display:flex;align-items:center;gap:12px;font-size:13px}
.stepn{width:24px;height:24px;border-radius:50%;display:flex;align-items:center;justify-content:center;font-size:11px;font-weight:700;color:var(--al);font-family:var(--mono);flex-shrink:0}
.feat-list{display:flex;flex-direction:column;gap:8px;margin-bottom:12px}
.feat{display:flex;align-items:flex-start;gap:12px;padding:10px 14px;background:rgba(255,255,255,0.03);border:1px solid var(--brd);border-radius:10px}
.p{font-size:10px;font-weight:800;padding:3px 8px;border-radius:50px;flex-shrink:0;font-family:var(--mono)}
.p1{background:rgba(239,68,68,0.15);border:1px solid rgba(239,68,68,0.3);color:#fca5a5}
.p2{background:rgba(245,158,11,0.15);border:1px solid rgba(245,158,11,0.3);color:#fcd34d}
.p3{background:rgba(148,163,184,0.1);border:1px solid rgba(148,163,184,0.2);color:#94a3b8}
.clr-grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(150px,1fr));gap:14px;margin-bottom:14px}
.cc{background:rgba(255,255,255,0.03);border:1px solid var(--brd);border-radius:14px;overflow:hidden}
.cs{height:72px;width:100%}
.ci{padding:12px}
.cn{font-size:13px;font-weight:700;color:var(--txt);margin-bottom:3px}
.chx{font-size:12px;color:var(--al);margin-bottom:4px}
.cu{font-size:11px;color:var(--dim);line-height:1.5;min-height:28px}
.sp-scale{display:flex;flex-direction:column;gap:10px}
.sp-r{display:flex;align-items:center;gap:14px;padding:8px 12px;border-radius:8px;background:rgba(255,255,255,0.02)}
.sp-t{font-size:12px;color:var(--al);width:90px;flex-shrink:0}
.sp-v{font-size:12px;color:var(--dim);width:38px;flex-shrink:0}
.sp-u{font-size:12px;color:var(--muted);flex:1}
.schema{display:flex;flex-direction:column;gap:14px}
.stbl{background:rgba(255,255,255,0.02);border:1px solid var(--brd);border-radius:14px;overflow:hidden}
.sth{display:flex;align-items:center;justify-content:space-between;padding:12px 16px;border-bottom:1px solid var(--brd);background:rgba(255,255,255,0.04)}
.sth .mono{font-size:14px;font-weight:700;color:var(--al)}
.stag{font-size:10px;font-weight:600;padding:3px 10px;border-radius:50px;font-family:var(--mono)}
.scols{padding:0}
.scol-h,.scol{display:grid;grid-template-columns:140px 140px 200px 1fr;gap:0}
.scol-h span,.scol span{padding:9px 14px;font-size:12px;border-bottom:1px solid rgba(255,255,255,0.04)}
.scol:last-child span{border-bottom:none}
.scol-h span{font-weight:700;letter-spacing:0.8px;text-transform:uppercase;color:var(--dim);font-size:10.5px;background:rgba(255,255,255,0.02)}
.tt{color:#7dd3fc;background:rgba(14,165,233,0.08);border-radius:4px}
.ct{color:#86efac;font-size:11px}
.api-tbl{border:1px solid var(--brd);border-radius:14px;overflow:hidden}
.api-h{display:grid;grid-template-columns:80px 200px 80px 1fr;padding:10px 14px;background:rgba(255,255,255,0.04);border-bottom:1px solid var(--brd)}
.api-h span{font-size:11px;font-weight:700;letter-spacing:0.8px;text-transform:uppercase;color:var(--dim)}
.api-r{display:grid;grid-template-columns:80px 200px 80px 1fr;padding:10px 14px;border-bottom:1px solid rgba(255,255,255,0.04);align-items:center;transition:background .15s}
.api-r:last-child{border-bottom:none}
.api-r:hover{background:rgba(255,255,255,0.02)}
.m{font-size:11px;font-weight:800;padding:3px 7px;border-radius:6px;font-family:var(--mono);text-align:center;display:inline-block}
.get{background:rgba(16,185,129,0.15);color:#34d399;border:1px solid rgba(16,185,129,0.3)}
.post{background:rgba(59,130,246,0.15);color:#60a5fa;border:1px solid rgba(59,130,246,0.3)}
.patch{background:rgba(245,158,11,0.15);color:#fbbf24;border:1px solid rgba(245,158,11,0.3)}
.del{background:rgba(239,68,68,0.15);color:#f87171;border:1px solid rgba(239,68,68,0.3)}
.ab{font-size:11px;font-weight:600;color:var(--muted)}
.ab.pub{color:#86efac}
.api-r span:last-child{font-size:13px;color:var(--muted)}
.code-block{border:1px solid var(--brd);border-radius:12px;overflow:hidden}
.code-pre{display:block;padding:20px;font-size:13px;line-height:1.8;white-space:pre;overflow-x:auto;outline:none;min-height:150px}
.code-pre:not(.env){color:#a5b4fc;background:rgba(99,102,241,0.04)}
.code-pre.env{color:#86efac;background:rgba(16,185,129,0.04)}
.code-pre:focus{box-shadow:inset 0 0 0 2px " + color + @"30}
.add-btn{display:inline-flex;align-items:center;gap:6px;padding:8px 16px;border-radius:8px;font-size:12.5px;font-weight:600;cursor:pointer;font-family:var(--font);transition:all .2s ease;margin-top:8px;border:none}
.toast{display:none;position:fixed;bottom:24px;right:24px;color:#fff;padding:12px 20px;border-radius:12px;font-size:14px;font-weight:600;z-index:999;box-shadow:0 8px 30px rgba(0,0,0,0.4)}
::-webkit-scrollbar{width:6px;height:6px}
::-webkit-scrollbar-track{background:transparent}
::-webkit-scrollbar-thumb{background:rgba(255,255,255,0.1);border-radius:3px}
::-webkit-scrollbar-thumb:hover{background:rgba(255,255,255,0.18)}
@media (max-width: 760px) {
  .scol-h,.scol{grid-template-columns:110px 110px 1fr}
  .api-h,.api-r{grid-template-columns:60px 140px 1fr}
  .topbar-nav{flex-wrap:wrap}
  .sp-r{flex-wrap:wrap}
}
</style>
</head>
<body>
<div class='bg-orbs'><div class='orb orb-1'></div><div class='orb orb-2'></div></div>
<div class='shell'>
  <div class='doc-nav'>
    <a class='nav-back' href='black://home'>&#x2190; Home</a>
    <span class='nav-bc'><span>Vibe Coding</span><span style='opacity:0.4'>&#x203A;</span><span style='color:" + colorLight + @"'>" + SimpleHtmlEncode(docNum) + @"</span></span>
    <button class='nav-cpy' id='cpyBtn' onclick='doCopy()' style='background:" + color + @"22;border:1px solid " + color + @"44;color:" + colorLight + @"'>&#x1F4CB; Copy as Prompt</button>
  </div>
  <div class='doc-hdr'>
    <div class='hdr-icon' style='background:" + color + @"18;border:1px solid " + color + @"30'>" + icon + @"</div>
    <div style='flex:1'>
      <div class='hdr-num'>" + SimpleHtmlEncode(docNum) + @"</div>
      <div class='hdr-title'>" + title + @"</div>
      <div class='hdr-sub'>Click any field to edit. Fill in your project details. Use &quot;Copy as Prompt&quot; to export for AI.</div>
      <div class='hdr-line' style='background:" + gradient + @"'></div>
    </div>
  </div>
");
            for (int i = 0; i < sections.Length; i++)
            {
                sb.Append("  <div class='doc-sec' style='animation-delay:");
                sb.Append((i * 0.08f).ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                sb.Append("s'>\n");
                sb.Append("    " + sections[i] + "\n");
                sb.Append("  </div>\n");
            }

            sb.Append(@"</div>
<div class='toast' id='toast'>&#x2713; Copied to clipboard!</div>
<script>
function doCopy(){
  var t=document.querySelector('.hdr-title').textContent.trim();
  var parts=['# '+t,''];
  document.querySelectorAll('.doc-sec').forEach(function(s){
    var h=s.querySelector('.sec-t');
    if(h) parts.push('## '+h.textContent.trim(),'');
    s.querySelectorAll('[contenteditable]').forEach(function(el){
      var v=el.textContent.trim();
      if(v&&v!==el.getAttribute('data-p'))parts.push(v,'');
    });
  });
  var txt=parts.join('\n');
  if(navigator.clipboard){navigator.clipboard.writeText(txt).then(showToast);}
  else{var ta=document.createElement('textarea');ta.value=txt;document.body.appendChild(ta);ta.select();document.execCommand('copy');document.body.removeChild(ta);showToast();}
}
function showToast(){
  var t=document.getElementById('toast');
  t.style.background='" + color + @"';
  t.style.display='block';
  setTimeout(function(){t.style.display='none';},2500);
}
function addFeat(){
  var l=document.getElementById('featureList');if(!l)return;
  var d=document.createElement('div');d.className='feat';
  d.innerHTML=""<span class='p p2'>P2</span><span class='ed' contenteditable='true' data-p='New feature...'></span>"";
  l.appendChild(d);d.querySelector('.ed').focus();
}
function addScr(){
  var g=document.getElementById('screensGrid');if(!g)return;
  var n=g.children.length+1;
  var d=document.createElement('div');d.className='scr';
  d.innerHTML=""<div class='scr-n'>0""+n+""</div><div class='scr-nm ed' contenteditable='true' data-p='Screen Name'></div><div class='scr-d ed' contenteditable='true' data-p='Description...'></div><div class='stags'><span class='stag'>new</span></div>"";
  g.appendChild(d);d.querySelector('.ed').focus();
}
function addClr(){
  var g=document.getElementById('colorGrid');if(!g)return;
  var d=document.createElement('div');d.className='cc';
  d.innerHTML=""<div class='cs' style='background:#6366f1'></div><div class='ci'><div class='cn ed' contenteditable='true' data-p='Color Name'></div><div class='chx ed mono' contenteditable='true' data-p='#000000'></div><div class='cu ed' contenteditable='true' data-p='Usage...'></div></div>"";
  g.appendChild(d);d.querySelector('.ed').focus();
}
function addRoute(){
  var t=document.getElementById('apiTbl');if(!t)return;
  var d=document.createElement('div');d.className='api-r';
  d.innerHTML=""<span class='m get'>GET</span><span class='mono ed' contenteditable='true' data-p='/api/v1/...'></span><span class='ab'>JWT</span><span class='ed' contenteditable='true' data-p='Description...'></span>"";
  t.appendChild(d);d.querySelector('.ed').focus();
}
</script>
</body>
</html>
");
            return sb.ToString();
        }
    }
}
