using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace BlackBrowser
{
    public static class AdShieldEngine
    {
        private static readonly string[] AdDomains = new string[]
        {
            "doubleclick.net", "googlesyndication.com", "googleadservices.com",
            "adservice.google.com", "adnxs.com", "rubiconproject.com",
            "pubmatic.com", "openx.net", "criteo.com", "taboola.com",
            "outbrain.com", "scorecardresearch.com", "quantserve.com",
            "/pagead/", "/api/stats/ads", "/ptracking", "/get_midroll_info"
        };

        private static readonly BloomFilter _bloomFilter = new BloomFilter(100, 0.005);
        private static bool _bloomInitialized = false;

        private static void EnsureBloomFilter()
        {
            if (_bloomInitialized) return;
            foreach (string d in AdDomains)
            {
                _bloomFilter.Add(d.Trim('/'));
            }
            _bloomInitialized = true;
        }

        private static readonly string[] AllowedStreamDomains = new string[]
        {
            "googlevideo.com", "ytimg.com", "netflix.com",
            "nflxvideo.net", "twitch.tv", "ttvnw.net", "vimeo.com",
            "akamaized.net", "cloudfront.net", ".m3u8", ".mpd", "blob:"
        };

        private const string GlobalAdShieldScript = @"
            (function() {
                if (window.__blackAdShieldInjected) return;
                window.__blackAdShieldInjected = true;

                // Set to false by the host when the user disables AdShield for this site.
                function shieldActive() { return window.__blackShieldEnabled !== false; }

                // Layer 1: Hide all banner, inline, feed & player ad elements instantly
                function injectCSS() {
                    if (document.getElementById('black-adshield-css')) return;
                    var style = document.createElement('style');
                    style.id = 'black-adshield-css';
                    style.innerHTML = `
                        .video-ads, .ytp-ad-module, .ytp-ad-overlay-container,
                        [id^='google_ads_'], .ad-container, .ad-wrapper, #masthead-ad, #player-ads,
                        ytd-promoted-video-renderer, ytd-display-ad-renderer, ytd-statement-banner-renderer,
                        ytd-in-feed-ad-layout-renderer, ytd-banner-promo-renderer-background,
                        ytd-ad-slot-renderer, ytd-rich-item-renderer:has(ytd-ad-slot-renderer),
                        iframe[src*='doubleclick'], iframe[src*='googlesyndication'] {
                            display: none !important; visibility: hidden !important; width: 0 !important; height: 0 !important; opacity: 0 !important; pointer-events: none !important;
                        }
                    `;
                    (document.head || document.documentElement).appendChild(style);
                }

                // Layer 2: API JSON Payload Stripper (Removes ad slots from YouTube API before player loads them)
                try {
                    const adKeys = [
                        'adPlacements','playerAds','adSlots','promotedVideoRenderer',
                        'inlineAdLayoutRenderer','carouselAdRenderer','searchVideoRenderer',
                        'adBreak','adBreakBegin','adBreakEnd','adBreakLength',
                        'adBreakOffset','adBreakType','adPlacement','adInfoRenderer',
                        'adFeedbackDialog','adVideoId','adVideoIds','adBreakIndex',
                        'interstitialPlayerConfig','interstitialPlayerOverlay','midroll',
                        'postroll','preroll','paidVideoOverlay','adRenderer',
                        'slotRenderer','hotkeyAd','adServiceEndpoint','adLayoutEndpoint',
                        'adSlot','adBadge','adBadgeText','adBadgePosition','adHint',
                        'adHintText','adCaption','adCaptionText','adOverlay',
                        'adOverlayRenderer','adOverlayStyle','adTriggerType',
                        'adTriggerValue','adTriggerPosition','adTriggerOffset'
                    ];

                    function stripAds(obj) {
                        if (!obj || typeof obj !== 'object') return;
                        if (Array.isArray(obj)) {
                            obj.forEach(stripAds);
                            return;
                        }
                        Object.keys(obj).forEach(key => {
                            if (adKeys.indexOf(key) !== -1) {
                                delete obj[key];
                            } else {
                                stripAds(obj[key]);
                            }
                        });
                    }

                    const origParse = JSON.parse;
                    JSON.parse = function(t, r) {
                        try {
                            const obj = origParse.call(this, t, r);
                            if (window.__blackShieldEnabled === false) return obj;
                            if (obj && typeof obj === 'object') stripAds(obj);
                            return obj;
                        } catch(e) {
                            return origParse.call(this, t, r);
                        }
                    };

                    const origFetch = window.fetch;
                    if (origFetch) {
                        window.fetch = function(i, init) {
                            return origFetch.apply(this, arguments).then(function(r) {
                                const url = typeof i === 'string' ? i : (i && i.url ? i.url : '');
                                if (window.__blackShieldEnabled === false) return r;
                                if (url.includes('youtubei.googleapis.com') || url.includes('/youtubei/v1/')) {
                                    return r.clone().json().then(function(d) {
                                        stripAds(d);
                                        return new Response(JSON.stringify(d), { status: r.status, headers: r.headers });
                                    }).catch(function() { return r; });
                                }
                                return r;
                            });
                        };
                    }
                } catch(e) {}

                // Layer 3: Ultra-Fast 200ms Video Ad Mute-Skipper & Auto-Clicker
                injectCSS();
                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', injectCSS);
                }

                setInterval(function() {
                    try {
                        if (!shieldActive()) {
                            var cssEl = document.getElementById('black-adshield-css');
                            if (cssEl) cssEl.remove();
                            return;
                        }
                        injectCSS();
                        var skipBtn = document.querySelector('.ytp-ad-skip-button, .ytp-ad-skip-button-modern, .ytp-skip-ad-button, .ytp-ad-skip-button-slot, .ytp-ad-skip-button-container button, .ytp-ad-text.ytp-ad-skip-button-text');
                        if (skipBtn) {
                            skipBtn.click();
                        }
                        var video = document.querySelector('video');
                        if (video) {
                            var adShowing = document.querySelector('.ad-showing, .ad-interrupting, .ytp-ad-player-overlay');
                            if (adShowing) {
                                video.muted = true;
                                video.playbackRate = 16.0;
                                if (isFinite(video.duration) && video.duration > 0 && video.currentTime < video.duration - 0.5) {
                                    video.currentTime = video.duration - 0.1;
                                }
                            }
                        }
                        var overlay = document.querySelector('.ytp-ad-overlay-close-button');
                        if (overlay) overlay.click();
                    } catch(e) {}
                }, 200);
            })();
        ";

        public static string Script { get { return GlobalAdShieldScript; } }

        public static async void AttachAdShield(WebView2 wv, Action onAdBlocked)
        {
            if (wv == null || wv.CoreWebView2 == null) return;

            string currentHost = "";

            wv.CoreWebView2.SourceChanged += (s, e) =>
            {
                if (wv.Source != null) currentHost = AdShieldState.GetHost(wv.Source.ToString());
            };

            // Network Layer Filtering
            EnsureBloomFilter();
            wv.CoreWebView2.WebResourceRequested += (sender, args) =>
            {
                string uri = args.Request.Uri.ToLower();

                // Never interfere with the top-level document (main frame)
                // navigation or its redirect chain. Streaming/mirror sites
                // frequently bounce through ad-looking URLs (googlesyndication,
                // /pagead/, /ptracking, ...) before landing on the real page;
                // replacing those responses with an empty 200 breaks the redirect.
                if (args.ResourceContext == CoreWebView2WebResourceContext.Document)
                    return;

                // Per-site toggle: skip blocking when the user disabled AdShield for this host.
                if (!AdShieldState.IsEnabled(currentHost))
                    return;

                // Allow media byte transfer from media CDNs
                foreach (string streamDomain in AllowedStreamDomains)
                {
                    if (uri.Contains(streamDomain))
                        return;
                }

                foreach (string domain in AdDomains)
                {
                    if (uri.Contains(domain))
                    {
                        args.Response = wv.CoreWebView2.Environment.CreateWebResourceResponse(
                            null, 200, "OK", "Content-Type: text/plain");

                        if (onAdBlocked != null) onAdBlocked();
                        break;
                    }
                }
            };

            foreach (string domain in AdDomains)
            {
                wv.CoreWebView2.AddWebResourceRequestedFilter("*" + domain + "*", CoreWebView2WebResourceContext.All);
            }

            // Document Created Injection (Ensures scripts are attached before DOM loads on every page/SPA frame)
            try
            {
                await wv.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(GlobalAdShieldScript);
                await wv.CoreWebView2.ExecuteScriptAsync(GlobalAdShieldScript);
            }
            catch { }

            wv.CoreWebView2.NavigationCompleted += async (s, e) =>
            {
                try
                {
                    bool enabled = AdShieldState.IsEnabled(currentHost);
                    await wv.CoreWebView2.ExecuteScriptAsync(
                        "window.__blackShieldEnabled = " + (enabled ? "true" : "false") + ";");
                    if (enabled)
                        await wv.CoreWebView2.ExecuteScriptAsync(GlobalAdShieldScript);
                }
                catch { }
            };
        }
    }

    public static class AdShieldState
    {
        private static readonly HashSet<string> DisabledSites = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "black-webview2", "shield_sites.txt");

        static AdShieldState()
        {
            try
            {
                if (File.Exists(FilePath))
                    foreach (string line in File.ReadAllLines(FilePath))
                        if (!string.IsNullOrWhiteSpace(line))
                            DisabledSites.Add(line.Trim());
            }
            catch { }
        }

        public static bool IsEnabled(string host)
        {
            return string.IsNullOrEmpty(host) || !DisabledSites.Contains(host);
        }

        public static bool Toggle(string host)
        {
            if (string.IsNullOrEmpty(host)) return true;
            if (DisabledSites.Remove(host))
            {
                Save();
                return true;
            }
            DisabledSites.Add(host);
            Save();
            return false;
        }

        public static string GetHost(string uri)
        {
            if (string.IsNullOrEmpty(uri)) return "";
            try
            {
                string h = new Uri(uri).Host.ToLowerInvariant();
                if (string.IsNullOrEmpty(h)) return "";
                return h.StartsWith("www.") ? h.Substring(4) : h;
            }
            catch { return ""; }
        }

        private static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllLines(FilePath, DisabledSites);
            }
            catch { }
        }
    }
}
