using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace BlackBrowser
{
    public static class AdShieldEngine
    {
        private static readonly string[] AdDomainList = new string[]
        {
            "doubleclick.net", "googlesyndication.com", "googleadservices.com",
            "pagead2.googlesyndication.com", "adservice.google.com", "google-analytics.com",
            "googletagmanager.com", "googletagservices.com", "adsystem.com", "amazon-adsystem.com",
            "adnxs.com", "rubiconproject.com", "pubmatic.com", "openx.net", "criteo.com",
            "criteo.net", "taboola.com", "outbrain.com", "scorecardresearch.com",
            "quantserve.com", "quantcount.com", "hotjar.com", "mixpanel.com",
            "segment.io", "segment.com", "amplitude.com", "heapanalytics.com",
            "fullstory.com", "clarity.ms", "mouseflow.com", "crazyegg.com",
            "optimizely.com", "branch.io", "adjust.com", "appsflyer.com",
            "kochava.com", "ads.twitter.com", "analytics.twitter.com", "connect.facebook.net",
            "pixel.facebook.com", "ads.linkedin.com", "snap.licdn.com", "analytics.tiktok.com",
            "ads.tiktok.com", "ads.reddit.com", "alb.reddit.com", "adroll.com",
            "adsafeprotected.com", "moatads.com", "doubleverify.com", "mediavine.com",
            "adthrive.com", "revcontent.com", "mgid.com", "zedo.com",
            "propellerads.com", "popads.net", "popcash.net", "adsterra.com",
            "hilltopads.net", "exoclick.com", "trafficjunky.net", "chartbeat.com",
            "newrelic.com", "nr-data.net", "bugsnag.com", "sentry.io",
            "rollbar.com", "logrocket.com", "intercom.io", "bat.bing.com",
            "mc.yandex.ru", "ad.mail.ru", "advertising.com", "bidswitch.net",
            "casalemedia.com", "contextweb.com", "demdex.net", "omtrdc.net",
            "2o7.net", "everesttech.net", "flashtalking.com", "indexww.com",
            "krxd.net", "lijit.com", "mathtag.com", "media.net",
            "mookie1.com", "owneriq.net", "rlcdn.com", "rkdms.com",
            "sharethrough.com", "simpli.fi", "sitescout.com", "smartadserver.com",
            "sonobi.com", "spotxchange.com", "tapad.com", "teads.tv",
            "tremorhub.com", "tribalfusion.com", "turn.com", "yieldmo.com",
            "3lift.com", "33across.com", "coinhive.com", "coin-hive.com"
        };

        private static readonly string[] AdPathFragments = new string[]
        {
            "/pagead/", "/api/stats/ads", "/ptracking", "/get_midroll_info",
            "/ads/ga-audiences", "/pixel.gif", "/beacon.js", "/g/collect?"
        };

        private static readonly HashSet<string> TrackingQueryParams = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content", "utm_id",
            "fbclid", "gclid", "dclid", "yclid", "msclkid", "mc_cid", "mc_eid",
            "igshid", "igsh", "_hsenc", "_hsmi", "mkt_tok", "ref_src"
        };

        private static readonly HashSet<string> AdDomainSet = new HashSet<string>(AdDomainList, StringComparer.OrdinalIgnoreCase);
        private static readonly BloomFilter _bloomFilter = new BloomFilter(1024, 0.002);
        private static bool _bloomInitialized = false;
        private static readonly object _bloomLock = new object();
        private static long _totalBlockedCount = 0;

        public static long TotalBlockedCount
        {
            get { return Interlocked.Read(ref _totalBlockedCount); }
        }

        private static void EnsureBloomFilter()
        {
            if (_bloomInitialized) return;
            lock (_bloomLock)
            {
                if (_bloomInitialized) return;
                foreach (string d in AdDomainList)
                {
                    _bloomFilter.Add(d);
                }
                _bloomInitialized = true;
            }
        }

        private static bool IsBlockedDomain(string host)
        {
            if (string.IsNullOrEmpty(host)) return false;
            string cur = host;
            while (!string.IsNullOrEmpty(cur))
            {
                if (_bloomFilter.MightContain(cur) && AdDomainSet.Contains(cur))
                    return true;
                int dot = cur.IndexOf('.');
                if (dot < 0 || dot + 1 >= cur.Length) break;
                cur = cur.Substring(dot + 1);
            }
            return false;
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

                // Never inject into internal browser pages
                if (location.hostname === 'black.internal') return;

                function shieldActive() { return window.__blackShieldEnabled !== false; }

                // Layer 1: Hide common banner, inline, feed & player ad elements
                function injectCSS() {
                    if (!shieldActive()) return;
                    if (document.getElementById('black-adshield-css')) return;
                    var style = document.createElement('style');
                    style.id = 'black-adshield-css';
                    style.textContent = `
                        .video-ads, .ytp-ad-module, .ytp-ad-overlay-container,
                        [id^='google_ads_'], .ad-container, .ad-wrapper, #masthead-ad, #player-ads,
                        ytd-promoted-video-renderer, ytd-display-ad-renderer, ytd-statement-banner-renderer,
                        ytd-in-feed-ad-layout-renderer, ytd-banner-promo-renderer-background,
                        ytd-ad-slot-renderer, ytd-rich-item-renderer:has(ytd-ad-slot-renderer),
                        iframe[src*='doubleclick'], iframe[src*='googlesyndication'] {
                            display: none !important; visibility: hidden !important; width: 0 !important; height: 0 !important; opacity: 0 !important; pointer-events: none !important;
                        }
                    `;
                    var root = document.head || document.documentElement;
                    if (root) root.appendChild(style);
                }

                injectCSS();
                if (document.readyState === 'loading') {
                    document.addEventListener('DOMContentLoaded', injectCSS);
                }

                // Layers 2 & 3: YouTube-only payload stripper & reactive video ad skipper
                var isYouTube = /(^|\.)youtube\.com$|(^|\.)youtube-nocookie\.com$|(^|\.)youtu\.be$/.test(location.hostname);
                if (!isYouTube) return;

                try {
                    const adKeys = {
                        adPlacements:1, playerAds:1, adSlots:1, promotedVideoRenderer:1,
                        inlineAdLayoutRenderer:1, carouselAdRenderer:1, searchVideoRenderer:1,
                        adBreak:1, adBreakBegin:1, adBreakEnd:1, adBreakLength:1,
                        adBreakOffset:1, adBreakType:1, adPlacement:1, adInfoRenderer:1,
                        adFeedbackDialog:1, adVideoId:1, adVideoIds:1, adBreakIndex:1,
                        interstitialPlayerConfig:1, interstitialPlayerOverlay:1, midroll:1,
                        postroll:1, preroll:1, paidVideoOverlay:1, adRenderer:1,
                        slotRenderer:1, hotkeyAd:1, adServiceEndpoint:1, adLayoutEndpoint:1,
                        adSlot:1, adBadge:1, adBadgeText:1, adBadgePosition:1, adHint:1,
                        adHintText:1, adCaption:1, adCaptionText:1, adOverlay:1,
                        adOverlayRenderer:1, adOverlayStyle:1, adTriggerType:1,
                        adTriggerValue:1, adTriggerPosition:1, adTriggerOffset:1
                    };

                    function stripAds(obj) {
                        if (!obj || typeof obj !== 'object') return;
                        if (Array.isArray(obj)) {
                            for (var i = 0; i < obj.length; i++) stripAds(obj[i]);
                            return;
                        }
                        var keys = Object.keys(obj);
                        for (var i = 0; i < keys.length; i++) {
                            var k = keys[i];
                            if (adKeys[k]) {
                                delete obj[k];
                            } else {
                                stripAds(obj[k]);
                            }
                        }
                    }

                    const origParse = JSON.parse;
                    JSON.parse = function(t, r) {
                        const obj = origParse.call(this, t, r);
                        if (window.__blackShieldEnabled !== false && obj && typeof obj === 'object') {
                            try { stripAds(obj); } catch(e) {}
                        }
                        return obj;
                    };

                    const origFetch = window.fetch;
                    if (origFetch) {
                        window.fetch = function(i, init) {
                            return origFetch.apply(this, arguments).then(function(r) {
                                const url = typeof i === 'string' ? i : (i && i.url ? i.url : '');
                                if (window.__blackShieldEnabled === false) return r;
                                if (url.indexOf('youtubei.googleapis.com') !== -1 || url.indexOf('/youtubei/v1/') !== -1) {
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

                var adCheckScheduled = false;
                function handleYouTubeAds() {
                    adCheckScheduled = false;
                    try {
                        if (!shieldActive()) {
                            var cssEl = document.getElementById('black-adshield-css');
                            if (cssEl) cssEl.remove();
                            return;
                        }
                        injectCSS();
                        var skipBtn = document.querySelector('.ytp-ad-skip-button, .ytp-ad-skip-button-modern, .ytp-skip-ad-button, .ytp-ad-skip-button-slot, .ytp-ad-skip-button-container button, .ytp-ad-text.ytp-ad-skip-button-text');
                        if (skipBtn) skipBtn.click();

                        var adShowing = document.querySelector('.ad-showing, .ad-interrupting, .ytp-ad-player-overlay');
                        if (adShowing) {
                            var video = document.querySelector('video');
                            if (video) {
                                video.muted = true;
                                video.playbackRate = 16.0;
                                if (isFinite(video.duration) && video.duration > 0 && video.currentTime < video.duration - 0.5) {
                                    video.currentTime = video.duration - 0.1;
                                }
                            }
                            // Only keep short polling alive while an ad is actively showing
                            setTimeout(scheduleAdCheck, 180);
                        }
                        var overlay = document.querySelector('.ytp-ad-overlay-close-button');
                        if (overlay) overlay.click();
                    } catch(e) {}
                }

                function scheduleAdCheck() {
                    if (adCheckScheduled) return;
                    adCheckScheduled = true;
                    setTimeout(handleYouTubeAds, 120);
                }

                try {
                    var obs = new MutationObserver(scheduleAdCheck);
                    var startObs = function() {
                        if (document.body) {
                            obs.observe(document.body, { childList: true, subtree: true, attributes: true, attributeFilter: ['class'] });
                            handleYouTubeAds();
                        }
                    };
                    if (document.body) startObs();
                    else document.addEventListener('DOMContentLoaded', startObs);
                } catch(e) {}
            })();
        ";

        public static string Script { get { return GlobalAdShieldScript; } }

        public static string StripTrackingParameters(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return null;
            Uri u;
            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out u)) return null;
            if (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps) return null;
            string q = u.Query;
            if (string.IsNullOrEmpty(q) || q.Length <= 1) return null;

            string rawQuery = q.Substring(1);
            string[] parts = rawQuery.Split('&');
            List<string> kept = new List<string>();
            bool stripped = false;
            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                int eq = part.IndexOf('=');
                string key = eq >= 0 ? part.Substring(0, eq) : part;
                string decodedKey = Uri.UnescapeDataString(key);
                if (TrackingQueryParams.Contains(decodedKey))
                {
                    stripped = true;
                    continue;
                }
                kept.Add(part);
            }
            if (!stripped) return null;

            UriBuilder ub = new UriBuilder(u);
            ub.Query = kept.Count > 0 ? string.Join("&", kept.ToArray()) : "";
            return ub.Uri.AbsoluteUri;
        }

        public static Task AttachAdShield(WebView2 wv, Action onAdBlocked)
        {
            return AttachAdShield(wv, (domain) => { if (onAdBlocked != null) onAdBlocked(); });
        }

        public static async Task AttachAdShield(WebView2 wv, Action<string> onBlockedDomain)
        {
            if (wv == null || wv.CoreWebView2 == null) return;

            string currentHost = "";

            wv.CoreWebView2.NavigationStarting += (s, e) =>
            {
                if (!string.IsNullOrEmpty(e.Uri))
                {
                    string h = AdShieldState.GetHost(e.Uri);
                    if (!string.IsNullOrEmpty(h)) currentHost = h;
                }
            };

            wv.CoreWebView2.SourceChanged += (s, e) =>
            {
                if (wv.Source != null)
                {
                    string h = AdShieldState.GetHost(wv.Source.ToString());
                    if (!string.IsNullOrEmpty(h)) currentHost = h;
                }
            };

            // Network Layer Filtering
            EnsureBloomFilter();
            wv.CoreWebView2.WebResourceRequested += (sender, args) =>
            {
                string rawUri = args.Request.Uri ?? "";
                string uri = rawUri.ToLowerInvariant();

                // Never interfere with internal pages
                if (uri.StartsWith("https://black.internal/", StringComparison.Ordinal))
                    return;

                // Never interfere with top-level/document navigation redirects
                if (args.ResourceContext == CoreWebView2WebResourceContext.Document)
                    return;

                // Global & per-site toggle: skip blocking when disabled.
                if (!SettingsStore.AdShieldEnabled || !AdShieldState.IsEnabled(currentHost))
                    return;

                // Allow media byte transfer from media CDNs
                foreach (string streamDomain in AllowedStreamDomains)
                {
                    if (uri.Contains(streamDomain))
                        return;
                }

                bool blocked = false;
                string blockedHost = "";
                Uri parsed;
                if (Uri.TryCreate(rawUri, UriKind.Absolute, out parsed))
                {
                    blockedHost = parsed.Host ?? "";
                    blocked = IsBlockedDomain(blockedHost);
                }

                if (!blocked)
                {
                    foreach (string frag in AdPathFragments)
                    {
                        if (uri.Contains(frag))
                        {
                            blocked = true;
                            if (string.IsNullOrEmpty(blockedHost)) blockedHost = frag;
                            break;
                        }
                    }
                }

                if (blocked)
                {
                    args.Response = wv.CoreWebView2.Environment.CreateWebResourceResponse(
                        null, 200, "OK", "Content-Type: text/plain");
                    Interlocked.Increment(ref _totalBlockedCount);
                    if (onBlockedDomain != null) onBlockedDomain(blockedHost);
                }
            };

            foreach (string domain in AdDomainList)
            {
                wv.CoreWebView2.AddWebResourceRequestedFilter("*" + domain + "*", CoreWebView2WebResourceContext.All);
            }
            foreach (string frag in AdPathFragments)
            {
                wv.CoreWebView2.AddWebResourceRequestedFilter("*" + frag + "*", CoreWebView2WebResourceContext.All);
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
                    if (wv.IsDisposed || wv.CoreWebView2 == null) return;
                    bool enabled = SettingsStore.AdShieldEnabled && AdShieldState.IsEnabled(currentHost);
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

        public static int DisabledSitesCount
        {
            get { return DisabledSites.Count; }
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
