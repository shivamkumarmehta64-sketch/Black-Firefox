/* Shared site script: PostHog analytics + GitHub live stats + history timeline */
(function () {
  'use strict';

  /* ---------- PostHog ---------- */
  if (window.posthog && typeof window.posthog.init === 'function') {
    window.posthog.init('phc_kVSLe9S9kJvDtkdZh4PtzcvA93DLwcUa8E2odXrFsJpJ', {
      api_host: 'https://us.i.posthog.com',
      disable_session_recording: true,
      capture_dead_clicks: false,
      capture_heatmaps: false,
    });
  }

  /* ---------- Page enter animation ---------- */
  document.body.classList.add('page-enter');
  setTimeout(function () { document.body.classList.remove('page-enter'); }, 550);

  /* ---------- Page-to-page transition ---------- */
  (function () {
    var fade = document.createElement('div');
    fade.id = 'page-fade';
    document.body.appendChild(fade);

    function isInternal(url) {
      var a = document.createElement('a');
      a.href = url;
      return a.origin === location.origin;
    }

    document.addEventListener('click', function (e) {
      var a = e.target.closest('a[href]');
      if (!a) return;
      if (a.target && a.target !== '_self') return;
      if (e.defaultPrevented || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
      if (!isInternal(a.href)) return;
      if (a.hash && a.hash.length > 1 && a.pathname === location.pathname) return;
      var dest = a.href;
      if (dest === location.href) return;
      e.preventDefault();
      document.body.classList.add('page-leaving');
      setTimeout(function () { window.location.href = dest; }, 300);
    });
  })();

  /* ---------- Scroll reveal ---------- */
  var reveal = document.querySelectorAll('.reveal');
  if ('IntersectionObserver' in window && reveal.length) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (en.isIntersecting) { en.target.classList.add('revealed'); io.unobserve(en.target); }
      });
    }, { threshold: 0.12 });
    reveal.forEach(function (el) { io.observe(el); });
  } else {
    reveal.forEach(function (el) { el.classList.add('revealed'); });
  }

  /* ---------- Count-up stats ---------- */
  function countUp(el) {
    var target = parseFloat(el.getAttribute('data-target') || '') ;
    if (isNaN(target)) return;
    var isInt = el.hasAttribute('data-int');
    var dur = isInt ? 1200 : 1600;
    var start = null;
    function step(ts) {
      if (!start) start = ts;
      var p = Math.min((ts - start) / dur, 1);
      var eased = 1 - Math.pow(1 - p, 3);
      el.textContent = isInt ? Math.round(target * eased).toLocaleString() : (target * eased).toFixed(1);
      if (p < 1) requestAnimationFrame(step);
      else el.textContent = isInt ? Math.round(target).toLocaleString() : target.toFixed(1);
    }
    requestAnimationFrame(step);
  }
  function runCountUps() {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (en.isIntersecting) { countUp(en.target); io.unobserve(en.target); }
      });
    }, { threshold: 0.4 });
    document.querySelectorAll('.stat-num[data-target]').forEach(function (el) { io.observe(el); });
  }
  if ('IntersectionObserver' in window && document.querySelector('.stat-num[data-target]')) runCountUps();

  /* ---------- GitHub helpers ---------- */
  var API = 'https://api.github.com/repos/';
  var USER = 'shivamkumarmehta64-sketch';
  var FALLBACK = function (repo) { return 'https://github.com/' + repo + '/releases'; };

  function fetchJson(url) {
    return fetch(url, { headers: { Accept: 'application/vnd.github+json' } })
      .then(function (r) { if (!r.ok) throw new Error('HTTP ' + r.status); return r.json(); });
  }

  function pickAsset(assets, exts) {
    for (var i = 0; i < exts.length; i++) {
      var a = assets.find(function (x) { return x.name.toLowerCase().endsWith(exts[i]); });
      if (a) return a;
    }
    return null;
  }

  function fmtK(n) {
    if (n == null) return '';
    return n >= 1000 ? (n / 1000).toFixed(1) + 'k' : String(n);
  }

  function ago(iso) {
    if (!iso) return '';
    var d = (Date.now() - new Date(iso).getTime()) / 86400000;
    if (d < 1) return 'today';
    if (d < 30) return Math.floor(d) + 'd ago';
    if (d < 365) return Math.floor(d / 30) + 'mo ago';
    return (d / 365).toFixed(1) + 'y ago';
  }

  function agoTime(iso) {
    if (!iso) return '';
    var d = (Date.now() - new Date(iso).getTime()) / 1000;
    if (d < 60) return 'just now';
    if (d < 3600) return Math.floor(d / 60) + 'm ago';
    if (d < 86400) return Math.floor(d / 3600) + 'h ago';
    if (d < 2592000) return Math.floor(d / 86400) + 'd ago';
    return new Date(iso).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  }

  /* Populate repo-level live stats within [data-repo] scopes */
  function wireRepo(repo, r) {
    var scope = document.querySelectorAll('[data-repo="' + repo + '"]');
    if (!scope.length) return;
    scope.forEach(function (root) {
      var set = function (sel, txt) {
        var el = root.querySelector(sel);
        if (el && txt) el.textContent = txt;
      };
      set('[data-stat="stars"]', fmtK(r.stargazers_count));
      set('[data-stat="forks"]', fmtK(r.forks_count));
      set('[data-stat="watch"]', fmtK(r.subscribers_count));
      set('[data-stat="lang"]', r.language);
      set('[data-stat="updated"]', ago(r.pushed_at));
      set('[data-stat="desc"]', r.description);
      if (r.license && r.license.spdx_id) set('[data-stat="license"]', r.license.spdx_id);
      root.querySelectorAll('[data-repo-href]').forEach(function (a) { a.href = r.html_url; });
    });
  }

  var repoScopes = new Set();
  document.querySelectorAll('[data-repo]').forEach(function (el) { repoScopes.add(el.getAttribute('data-repo')); });
  repoScopes.forEach(function (repo) {
    fetchJson(API + repo).then(function (meta) { if (meta && !meta.message) wireRepo(repo, meta); }).catch(function () {});
  });

  /* ---------- Releases (version badges + download links) ---------- */
  function wireRelease(rel, repo) {
    var v = rel.tag_name || rel.name || 'latest';
    document.querySelectorAll('[data-version="' + repo + '"]').forEach(function (el) { el.textContent = v; });
    document.querySelectorAll('[data-download="' + repo + '"]').forEach(function (el) {
      var exts = (el.getAttribute('data-exts') || '.exe,.zip').split(',');
      var asset = pickAsset(rel.assets, exts);
      if (asset) { el.href = asset.browser_download_url; el.setAttribute('download', ''); }
      else { el.href = FALLBACK(repo); }
    });
    document.querySelectorAll('[data-meta="' + repo + '"]').forEach(function (el) {
      var total = (rel.assets || []).reduce(function (s, a) { return s + (a.download_count || 0); }, 0);
      el.textContent = v + (total > 0 ? ' \u00b7 ' + fmtK(total) + ' downloads' : '');
    });
  }

  var repos = new Set();
  document.querySelectorAll('[data-version], [data-download], [data-meta]').forEach(function (el) {
    var repo = el.getAttribute('data-version') || el.getAttribute('data-download') || el.getAttribute('data-meta');
    if (repo) repos.add(repo);
  });
  repos.forEach(function (repo) {
    fetchJson(API + repo + '/releases')
      .then(function (list) { return Array.isArray(list) && list.length ? list[0] : null; })
      .then(function (rel) { if (rel) wireRelease(rel, repo); })
      .catch(function () {});
  });

  /* ---------- Profile + totals (achievements page) ---------- */
  var profileEl = document.getElementById('gh-profile');
  if (profileEl) {
    var setText = function (id, txt) {
      var el = document.getElementById(id);
      if (el && txt) el.textContent = txt;
    };
    var setNum = function (id, n) {
      var el = document.getElementById(id);
      if (el && n != null) { el.setAttribute('data-target', n); el.setAttribute('data-int', ''); countUp(el); }
    };

    fetchJson('https://api.github.com/users/' + USER).then(function (u) {
      if (!u || u.message) return;
      var a = document.getElementById('gh-avatar');
      if (a) { a.src = u.avatar_url; a.removeAttribute('hidden'); }
      var since = u.created_at ? new Date(u.created_at) : null;
      if (since) setText('gh-since', 'On GitHub since ' + since.toLocaleDateString(undefined, { month: 'long', year: 'numeric' }));
      setNum('stat-followers', u.followers || 0);
    }).catch(function () {});

    fetchJson('https://api.github.com/users/' + USER + '/repos?per_page=100&sort=updated').then(function (repos) {
      if (!Array.isArray(repos)) return;
      var stars = 0, fork = 0;
      repos.forEach(function (r) { stars += (r.stargazers_count || 0); fork += (r.forks_count || 0); });
      setNum('stat-repos', repos.length);
      setNum('stat-stars', stars);
      setNum('stat-forks', fork);
    }).catch(function () {});
  }

  /* ---------- History timeline (history.html) ---------- */
  var timelineEl = document.getElementById('gh-activity');
  if (timelineEl) {
    function ev(emoji, msg, repo, created) {
      var item = document.createElement('div');
      item.className = 't-item';
      item.innerHTML =
        '<span class="t-dot pulse">' + emoji + '</span>' +
        '<p class="t-msg">' + msg + (repo ? ' <span class="t-repo">' + repo + '</span>' : '') + '</p>' +
        '<p class="t-meta">' + agoTime(created) + '</p>';
      timelineEl.appendChild(item);
    }

    var seen = 0;
    fetchJson('https://api.github.com/users/' + USER + '/events/public?per_page=8').then(function (events) {
      if (!Array.isArray(events)) return;
      events.forEach(function (e) {
        if (seen >= 5) return;
        var type = e.type || '';
        var repoName = e.repo && e.repo.name ? e.repo.name.split('/')[1] : '';
        if (type === 'PushEvent') {
          var commits = (e.payload && e.payload.commits) || [];
          if (!commits.length) return;
          seen++;
          var branch = (e.payload.ref || 'main').replace('refs/heads/', '');
          ev('⬆', 'Pushed ' + commits.length + ' commit' + (commits.length > 1 ? 's' : '') + ' to ' + branch, repoName, e.created_at);
        } else if (type === 'CreateEvent') {
          seen++; ev('✨', 'Created ' + (e.payload.ref_type === 'tag' ? 'tag' : e.payload.ref_type), repoName, e.created_at);
        } else if (type === 'ForkEvent') {
          seen++; ev('🔀', 'Forked the repository', repoName, e.created_at);
        } else if (type === 'ReleaseEvent') {
          seen++; ev('📦', 'Published release', repoName, e.created_at);
        } else if (type === 'WatchEvent') {
          seen++; ev('⭐', 'Starred the repository', repoName, e.created_at);
        }
      });
      if (!seen) ev('💻', '', 'No recent public activity yet.', new Date().toISOString());
    }).catch(function () {
      ev('💻', '', 'Could not load activity.', new Date().toISOString());
    });
  }

  /* ---------- Scroll UI ---------- */
  // smooth scroll (Lenis, self-hosted so it matches the site CSP)
  (function () {
    function initLenis() {
      try {
        window.__lenis = new window.Lenis({ duration: 1.1, smoothWheel: true });
        function raf(time) { window.__lenis.raf(time); requestAnimationFrame(raf); }
        requestAnimationFrame(raf);
        if (window.__progress) window.__lenis.on('scroll', window.__progress);
        document.addEventListener('click', function (e) {
          var a = e.target.closest('a[href^="#"]');
          if (a && window.__lenis) {
            var el = document.querySelector(a.getAttribute('href'));
            if (el) { e.preventDefault(); window.__lenis.scrollTo(el, { offset: -80 }); }
          }
        });
      } catch (err) { /* no shorthand */ }
    }
    if (window.Lenis) { initLenis(); }
    else {
      var scripts = document.getElementsByTagName('script');
      var app = scripts[scripts.length - 1];
      var base = app.src.substring(0, app.src.lastIndexOf('/'));
      var s = document.createElement('script');
      s.src = base + '/lenis.min.js';
      s.async = true;
      s.onload = function () { initLenis(); };
      document.head.appendChild(s);
    }
  })();

  // scroll progress bar
  window.__progress = function () {
    var h = document.documentElement;
    var max = h.scrollHeight - h.clientHeight;
    var p = max > 0 ? (h.scrollTop / max) * 100 : 0;
    if (window.__progressBar) window.__progressBar.style.width = p + '%';
  };
  window.__progressBar = document.getElementById('scroll-progress');
  if (!window.__progressBar) {
    window.__progressBar = document.createElement('div');
    window.__progressBar.id = 'scroll-progress';
    document.body.appendChild(window.__progressBar);
  }
  window.addEventListener('scroll', window.__progress, { passive: true });
  window.__progress();

  // parallax drift for elements tagged [data-parallax="<speed>"]
  (function () {
    var els = document.querySelectorAll('[data-parallax]');
    if (!els.length) return;
    var ticking = false;
    function update() {
      var y = window.scrollY || window.pageYOffset || 0;
      els.forEach(function (el) {
        var speed = parseFloat(el.getAttribute('data-parallax')) || 0.2;
        el.style.transform = 'translate3d(0,' + (y * speed) + 'px,0)';
      });
      ticking = false;
    }
    window.addEventListener('scroll', function () {
      if (!ticking) { requestAnimationFrame(update); ticking = true; }
    }, { passive: true });
    update();
  })();
})();