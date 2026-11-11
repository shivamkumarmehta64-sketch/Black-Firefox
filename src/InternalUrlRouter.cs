using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace BlackBrowser
{
    public static class InternalUrlRouter
    {
        public const string VirtualHost = "black.internal";
        public const string VirtualOrigin = "https://black.internal";

        public static bool IsInternalOrigin(string source)
        {
            if (string.IsNullOrEmpty(source)) return false;
            Uri u;
            return Uri.TryCreate(source, UriKind.Absolute, out u) &&
                   u.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
                   u.Host.Equals(VirtualHost, StringComparison.OrdinalIgnoreCase);
        }

        public static string ResolveToVirtualUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input) ||
                input.Equals("about:blank", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("black://home", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("black://newtab", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/home";

            string u = input.Trim();
            if (u.Equals("black://history", StringComparison.OrdinalIgnoreCase) || u.Equals("about:history", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/history";
            if (u.Equals("black://cookies", StringComparison.OrdinalIgnoreCase) || u.Equals("about:cookies", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/cookies";
            if (u.Equals("black://bookmarks", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/bookmarks";
            if (u.Equals("black://downloads", StringComparison.OrdinalIgnoreCase) || u.Equals("about:downloads", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/downloads";
            if (u.Equals("black://passwords", StringComparison.OrdinalIgnoreCase) || u.Equals("about:passwords", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/passwords";
            if (u.Equals("black://about", StringComparison.OrdinalIgnoreCase) || u.Equals("about:about", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/about";
            if (u.Equals("black://terms", StringComparison.OrdinalIgnoreCase) || u.Equals("about:terms", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/terms";
            if (u.Equals("black://privacy", StringComparison.OrdinalIgnoreCase) || u.Equals("about:privacy", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/privacy";
            if (u.Equals("black://trademark", StringComparison.OrdinalIgnoreCase) || u.Equals("about:trademark", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/trademark";
            if (u.Equals("black://extensions", StringComparison.OrdinalIgnoreCase) || u.Equals("about:extensions", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/extensions";
            if (u.Equals("black://doc-prd", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/doc-prd";
            if (u.Equals("black://doc-appflow", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/doc-appflow";
            if (u.Equals("black://doc-techstack", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/doc-techstack";
            if (u.Equals("black://doc-frontend", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/doc-frontend";
            if (u.Equals("black://doc-backend", StringComparison.OrdinalIgnoreCase))
                return VirtualOrigin + "/doc-backend";

            return null;
        }

        public static string GetDisplayUrl(string sourceUri)
        {
            if (string.IsNullOrEmpty(sourceUri) || sourceUri == "about:blank") return "";
            if (!IsInternalOrigin(sourceUri)) return sourceUri;

            Uri u;
            if (!Uri.TryCreate(sourceUri, UriKind.Absolute, out u)) return "";
            string path = (u.AbsolutePath ?? "/").Trim('/').ToLowerInvariant();
            if (path == "" || path == "home" || path == "newtab") return "";
            return "black://" + path;
        }

        public static string GetDefaultTitle(string sourceOrVirtualUrl)
        {
            string lower = (sourceOrVirtualUrl ?? "").ToLowerInvariant();
            if (lower.EndsWith("/home") || lower == "black://home" || lower == "black://newtab") return "New Tab";
            if (lower.EndsWith("/history") || lower == "black://history") return "Local History";
            if (lower.EndsWith("/cookies") || lower == "black://cookies") return "Local Cookies";
            if (lower.EndsWith("/bookmarks") || lower == "black://bookmarks") return "Local Bookmarks";
            if (lower.EndsWith("/downloads") || lower == "black://downloads") return "Local Downloads";
            if (lower.EndsWith("/passwords") || lower == "black://passwords") return "Saved Passwords";
            if (lower.EndsWith("/about") || lower == "black://about") return "About Black Browser";
            if (lower.EndsWith("/terms") || lower == "black://terms") return "Terms of Service";
            if (lower.EndsWith("/privacy") || lower == "black://privacy") return "Privacy Policy";
            if (lower.EndsWith("/trademark") || lower == "black://trademark") return "Trademark Notice";
            if (lower.EndsWith("/extensions") || lower == "black://extensions") return "Extensions";
            if (lower.EndsWith("/doc-prd") || lower == "black://doc-prd") return "PRD Template";
            if (lower.EndsWith("/doc-appflow") || lower == "black://doc-appflow") return "App Flow Template";
            if (lower.EndsWith("/doc-techstack") || lower == "black://doc-techstack") return "Tech Stack Template";
            if (lower.EndsWith("/doc-frontend") || lower == "black://doc-frontend") return "UI/UX Guidelines";
            if (lower.EndsWith("/doc-backend") || lower == "black://doc-backend") return "Backend Schema Template";
            if (lower.EndsWith("/reader")) return "Reader Mode";
            return null;
        }

        public static async Task TryServeVirtualRequest(
            WebView2 wv,
            CoreWebView2WebResourceRequestedEventArgs args,
            Func<bool> getIsDarkMode,
            Func<string> getReaderHtml)
        {
            if (wv == null || wv.CoreWebView2 == null || args == null || args.Request == null) return;
            string uri = args.Request.Uri ?? "";
            if (!IsInternalOrigin(uri)) return;

            Uri u;
            if (!Uri.TryCreate(uri, UriKind.Absolute, out u)) return;
            string slug = (u.AbsolutePath ?? "/").Trim('/').ToLowerInvariant();
            bool dark = getIsDarkMode != null ? getIsDarkMode() : true;

            if (slug == "cookies")
            {
                CoreWebView2Deferral deferral = args.GetDeferral();
                try
                {
                    var cookies = await wv.CoreWebView2.CookieManager.GetCookiesAsync(null);
                    string cookieHtml = CookieManager.GetCookiesHtml(dark, cookies);
                    args.Response = CreateHtmlResponse(wv.CoreWebView2.Environment, cookieHtml);
                }
                catch
                {
                    args.Response = CreateHtmlResponse(wv.CoreWebView2.Environment, CookieManager.GetCookiesHtml(dark, null));
                }
                finally
                {
                    deferral.Complete();
                }
                return;
            }

            string html = null;
            if (slug == "" || slug == "home" || slug == "newtab") html = SpeedDialPage.GetHtml(dark);
            else if (slug == "history") html = HistoryManager.GetHistoryHtml(dark);
            else if (slug == "bookmarks") html = BookmarksManager.GetBookmarksHtml(dark);
            else if (slug == "downloads") html = DownloadsManager.GetDownloadsHtml(dark);
            else if (slug == "passwords") html = PasswordManager.GetPasswordsHtml(dark);
            else if (slug == "about") html = LegalPagesManager.GetAboutHtml(dark);
            else if (slug == "terms") html = LegalPagesManager.GetTermsHtml(dark);
            else if (slug == "privacy") html = LegalPagesManager.GetPrivacyHtml(dark);
            else if (slug == "trademark") html = LegalPagesManager.GetTrademarkHtml(dark);
            else if (slug == "extensions") html = ExtensionManager.GetExtensionsHtml(dark);
            else if (slug == "doc-prd") html = SpeedDialPage.GetPrdHtml();
            else if (slug == "doc-appflow") html = SpeedDialPage.GetAppFlowHtml();
            else if (slug == "doc-techstack") html = SpeedDialPage.GetTechStackHtml();
            else if (slug == "doc-frontend") html = SpeedDialPage.GetFrontendGuidelinesHtml();
            else if (slug == "doc-backend") html = SpeedDialPage.GetBackendStructureHtml();
            else if (slug == "reader") html = getReaderHtml != null ? (getReaderHtml() ?? "") : "";
            else html = "<!DOCTYPE html><html><body style='background:#0b0d1b;color:#fff;font-family:sans-serif;padding:40px'><h2>Page not found</h2></body></html>";

            args.Response = CreateHtmlResponse(wv.CoreWebView2.Environment, html);
        }

        private static CoreWebView2WebResourceResponse CreateHtmlResponse(CoreWebView2Environment env, string html)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(html ?? "");
            MemoryStream stream = new MemoryStream(bytes);
            return env.CreateWebResourceResponse(
                stream, 200, "OK",
                "Content-Type: text/html; charset=utf-8\r\n" +
                "Cache-Control: no-store\r\n" +
                "X-Frame-Options: DENY\r\n" +
                "X-Content-Type-Options: nosniff\r\n" +
                "Content-Security-Policy: default-src 'none'; script-src 'unsafe-inline'; style-src 'unsafe-inline'; img-src 'self' data: https:; connect-src 'none'; font-src 'self' data:; object-src 'none'; frame-ancestors 'none'; base-uri 'none'");
        }
    }
}

