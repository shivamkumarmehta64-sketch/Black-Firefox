using System;
using System.Text;

namespace BlackBrowser
{
    public static class LegalPagesManager
    {
        private static string HtmlEncode(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c == '<') sb.Append("&lt;");
                else if (c == '>') sb.Append("&gt;");
                else if (c == '&') sb.Append("&amp;");
                else if (c == '"') sb.Append("&quot;");
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static string BuildBaseWrapper(string title, string icon, string activeTab, string bodyContent, bool dark)
        {
            string bg = dark ? "#0a0b10" : "#f8fafc";
            string cardBg = dark ? "rgba(22, 24, 34, 0.75)" : "#ffffff";
            string borderClr = dark ? "rgba(255, 255, 255, 0.1)" : "rgba(0, 0, 0, 0.08)";
            string textClr = dark ? "#e2e8f0" : "#1e293b";
            string textMuted = dark ? "#94a3b8" : "#64748b";
            string accentClr = "#3b82f6";
            string navBg = dark ? "rgba(255,255,255,0.05)" : "rgba(0,0,0,0.04)";
            string hoverBg = dark ? "rgba(255,255,255,0.08)" : "rgba(0,0,0,0.06)";

            string aboutActive = activeTab == "about" ? " active" : "";
            string termsActive = activeTab == "terms" ? " active" : "";
            string privacyActive = activeTab == "privacy" ? " active" : "";
            string trademarkActive = activeTab == "trademark" ? " active" : "";

            return "<!DOCTYPE html>\n" +
"<html lang='en'>\n" +
"<head>\n" +
"<meta charset='utf-8'>\n" +
"<meta name='viewport' content='width=device-width, initial-scale=1'>\n" +
"<title>" + HtmlEncode(title) + " — Black Browser</title>\n" +
"<style>\n" +
"*{margin:0;padding:0;box-sizing:border-box}\n" +
"body{background:" + bg + ";color:" + textClr + ";font-family:'Segoe UI Variable Display','Segoe UI',system-ui,-apple-system,sans-serif;line-height:1.6;min-height:100vh;padding:32px 20px}\n" +
".container{max-width:920px;margin:0 auto}\n" +
".header{display:flex;align-items:center;justify-content:space-between;margin-bottom:28px;padding-bottom:20px;border-bottom:1px solid " + borderClr + "}\n" +
".brand{display:flex;align-items:center;gap:14px}\n" +
".brand-icon{width:48px;height:48px;background:linear-gradient(135deg,#3b82f6,#8b5cf6);border-radius:14px;display:flex;align-items:center;justify-content:center;font-size:24px;box-shadow:0 4px 20px rgba(59,130,246,0.4)}\n" +
".brand-title{font-family:'Segoe UI Variable Display','Segoe UI',sans-serif;font-size:22px;font-weight:800;background:linear-gradient(90deg,#60a5fa,#a78bfa);-webkit-background-clip:text;-webkit-text-fill-color:transparent}\n" +
".brand-sub{font-size:12px;color:" + textMuted + ";font-weight:600;letter-spacing:1px;text-transform:uppercase}\n" +
".nav{display:flex;gap:8px;background:" + navBg + ";padding:4px;border-radius:12px;border:1px solid " + borderClr + "}\n" +
".nav-item{padding:8px 16px;border-radius:8px;font-size:13px;font-weight:600;color:" + textMuted + ";text-decoration:none;transition:all 0.2s ease}\n" +
".nav-item:hover{color:" + textClr + ";background:" + hoverBg + "}\n" +
".nav-item.active{color:#ffffff;background:linear-gradient(135deg,#3b82f6,#6366f1);box-shadow:0 2px 10px rgba(59,130,246,0.3)}\n" +
".card{background:" + cardBg + ";border:1px solid " + borderClr + ";border-radius:20px;padding:36px;backdrop-filter:blur(16px);box-shadow:0 8px 32px rgba(0,0,0,0.2)}\n" +
".page-title{font-family:'Segoe UI Variable Display','Segoe UI',sans-serif;font-size:28px;font-weight:700;margin-bottom:8px;display:flex;align-items:center;gap:12px}\n" +
".page-subtitle{color:" + textMuted + ";font-size:14px;margin-bottom:28px}\n" +
"h2{font-family:'Segoe UI Variable Display','Segoe UI',sans-serif;font-size:18px;font-weight:700;margin:24px 0 10px 0;color:" + accentClr + "}\n" +
"p,li{font-size:14.5px;color:" + textClr + ";margin-bottom:12px}\n" +
"ul{padding-left:20px;margin-bottom:16px}\n" +
".badge{display:inline-block;padding:4px 10px;border-radius:6px;font-size:11px;font-weight:600;font-family:'Cascadia Code','Consolas',monospace;background:rgba(59,130,246,0.15);color:#60a5fa;border:1px solid rgba(59,130,246,0.3);margin-bottom:16px}\n" +
".footer{margin-top:32px;text-align:center;font-size:12px;color:" + textMuted + ";border-top:1px solid " + borderClr + ";padding-top:20px}\n" +
"</style>\n" +
"</head>\n" +
"<body>\n" +
"<div class='container'>\n" +
"  <div class='header'>\n" +
"    <div class='brand'>\n" +
"      <div class='brand-icon'>&#x1F310;</div>\n" +
"      <div>\n" +
"        <div class='brand-title'>Black Browser</div>\n" +
"        <div class='brand-sub'>Legal &amp; Governance Hub</div>\n" +
"      </div>\n" +
"    </div>\n" +
"    <div class='nav'>\n" +
"      <a href='black://about' class='nav-item" + aboutActive + "'>About</a>\n" +
"      <a href='black://terms' class='nav-item" + termsActive + "'>Terms</a>\n" +
"      <a href='black://privacy' class='nav-item" + privacyActive + "'>Privacy Policy</a>\n" +
"      <a href='black://trademark' class='nav-item" + trademarkActive + "'>Trademark</a>\n" +
"    </div>\n" +
"  </div>\n" +
"  <div class='card'>\n" +
    bodyContent + "\n" +
"  </div>\n" +
"  <div class='footer'>\n" +
"    Black Browser Engine &bull; Privacy First &bull; Local Storage Only &bull; Licensed under MIT\n" +
"  </div>\n" +
"</div>\n" +
"</body>\n" +
"</html>";
        }

        public static string GetAboutHtml(bool dark)
        {
            string body =
"<div class='page-title'>&#x1F310; About Black Browser</div>\n" +
"<div class='page-subtitle'>An ultra-minimal, privacy-focused desktop browser for Windows.</div>\n" +
"<div class='badge'>VERSION 9.1.0 — GLASSMORPHIC EDITION</div>\n" +
"<h2>Architecture &amp; Tech Stack</h2>\n" +
"<p>Black Browser is built with native <strong>C# .NET 4.8</strong> and Microsoft <strong>WebView2 (Chromium engine)</strong>. It offers full hardware acceleration, low RAM consumption (~35-50 MB background footprint), Container Tabs, and a custom dark glassmorphic UI.</p>\n" +
"<h2>Core Pillars</h2>\n" +
"<ul>\n" +
"  <li><strong>Zero Telemetry:</strong> No analytics, tracking, or network calls home.</li>\n" +
"  <li><strong>Local First:</strong> Bookmarks, browsing history, and preferences are stored exclusively on your device.</li>\n" +
"  <li><strong>3-Layer AdShield:</strong> Built-in multi-layered ad &amp; tracker blocking engine with URL tracking stripper.</li>\n" +
"  <li><strong>GitHub Auto-Updates:</strong> Seamless in-app version checks and 1-click updates from official GitHub releases.</li>\n" +
"  <li><strong>Smart Address Bar:</strong> Autocomplete suggestions from local bookmarks, history, open tabs, and instant math.</li>\n" +
"</ul>\n" +
"<h2>GitHub &amp; Updates</h2>\n" +
"<p>\n" +
"  Official Repository: <a href='https://github.com/shivamkumarmehta64-sketch/Black-Firefox' target='_blank' style='color:#60a5fa;'>github.com/shivamkumarmehta64-sketch/Black-Firefox</a><br>\n" +
"  Latest Releases: <a href='https://github.com/shivamkumarmehta64-sketch/Black-Firefox/releases' target='_blank' style='color:#60a5fa;'>View Releases &amp; Changelog</a>\n" +
"</p>\n" +
"<h2>Quick Nav</h2>\n" +
"<p>\n" +
"  &bull; <a href='black://terms' style='color:#60a5fa;'>Terms of Service</a><br>\n" +
"  &bull; <a href='black://privacy' style='color:#60a5fa;'>Privacy Policy</a><br>\n" +
"  &bull; <a href='black://trademark' style='color:#60a5fa;'>Trademark Guidelines &amp; Disclaimer</a>\n" +
"</p>";
            return BuildBaseWrapper("About", "&#x1F310;", "about", body, dark);
        }

        public static string GetTermsHtml(bool dark)
        {
            string body =
"<div class='page-title'>&#x1F4DC; Terms of Service</div>\n" +
"<div class='page-subtitle'>Effective Date: January 1, 2026</div>\n" +
"<h2>1. Acceptance of Terms</h2>\n" +
"<p>By downloading, installing, or using Black Browser (&quot;the Software&quot;), you agree to be bound by these Terms of Service. If you do not agree to these terms, do not install or use the Software.</p>\n" +
"<h2>2. Software License</h2>\n" +
"<p>Black Browser is open-source software provided under the <strong>MIT License</strong>. You are granted a non-exclusive, royalty-free license to use, modify, and distribute the Software in accordance with the MIT License terms.</p>\n" +
"<h2>3. User Responsibilities &amp; Acceptable Use</h2>\n" +
"<p>You agree to use Black Browser only for lawful purposes. You are solely responsible for all browsing activities, content accessed, and data stored locally through the browser application.</p>\n" +
"<h2>4. Disclaimer of Warranties</h2>\n" +
"<p>THE SOFTWARE IS PROVIDED &quot;AS IS&quot;, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.</p>\n" +
"<h2>5. Limitation of Liability</h2>\n" +
"<p>IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE.</p>";
            return BuildBaseWrapper("Terms of Service", "&#x1F4DC;", "terms", body, dark);
        }

        public static string GetPrivacyHtml(bool dark)
        {
            string body =
"<div class='page-title'>&#x1F512; Privacy Policy</div>\n" +
"<div class='page-subtitle'>Zero Telemetry Guarantee &bull; Local Data Storage Only</div>\n" +
"<h2>1. Our Privacy Commitment</h2>\n" +
"<p>Black Browser was engineered from the ground up to respect user privacy. We believe your web browsing history and personal data belong to you and only you.</p>\n" +
"<h2>2. Zero Data Collection</h2>\n" +
"<p>Black Browser <strong>does not collect, transmit, track, sell, or monetize</strong> any personal data, IP addresses, browsing metrics, or application telemetry.</p>\n" +
"<h2>3. Local Storage</h2>\n" +
"<p>All browser data—including bookmarks, history records, custom dials, settings, and temporary caches—is stored strictly on your local device under <code>%LOCALAPPDATA%\\black-webview2</code>.</p>\n" +
"<h2>4. Third-Party Web Traffic</h2>\n" +
"<p>When you navigate to third-party websites, your HTTP requests are handled directly by the target servers and Microsoft WebView2 engine. Black Browser includes a built-in 3-layer AdShield filter to restrict third-party tracking scripts and advertisement domains.</p>";
            return BuildBaseWrapper("Privacy Policy", "&#x1F512;", "privacy", body, dark);
        }

        public static string GetTrademarkHtml(bool dark)
        {
            string body =
"<div class='page-title'>&#x2696;&#xFE0F; Trademark &amp; Brand Guidelines</div>\n" +
"<div class='page-subtitle'>Independent Branding &amp; Non-Affiliation Notice</div>\n" +
"<h2>1. Independent Identity</h2>\n" +
"<p><strong>Black Browser</strong> is an independent open-source software project developed cleanly under its own name with zero affiliation with, endorsement by, or sponsorship from Mozilla Corporation, Microsoft Corporation, or Google LLC.</p>\n" +
"<h2>2. Third-Party Marks</h2>\n" +
"<p>Earlier prototypes referenced dark Proton-inspired aesthetics; all product branding uses the independent <strong>Black Browser</strong> mark to respect third-party trademarks. Windows&reg; and WebView2 are trademarks of Microsoft Corporation.</p>\n" +
"<h2>3. Redistribution Guidelines</h2>\n" +
"<p>Forks and redistributions under the MIT License may retain the name Black Browser cleanly or choose their own distinct branding without implying upstream vendor endorsement.</p>";
            return BuildBaseWrapper("Trademark Notice", "&#x2696;&#xFE0F;", "trademark", body, dark);
        }
    }
}
