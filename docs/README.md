# Shivam's Apps — Portfolio Website

Single-page portfolio hub for all projects: **ytube** & **mtube** (featured working apps), **Black Browser (C#)**, **Electronics Calculator**.

- Dark glassmorphism theme (TailwindCSS, pre-built to `assets/tailwind.css`)
- Mobile responsive
- Download buttons auto-fetch the latest GitHub release via the GitHub API — new releases appear automatically
- **PostHog analytics** — `pageview` events are captured on every load
- 100% free hosting: Vercel → https://blackbrowser.vercel.app

## Files

```
index.html        — the website
assets/           — resized app icons + compiled tailwind.css
src/input.css     — Tailwind source (build with `npm run build:css`)
tailwind.config.js
package.json
```

## Deploy to Vercel (free, no credit card)

The project is connected to Vercel — pushing to `main` on GitHub auto-deploys:

```bash
git add .
git commit -m "Update site"
git push origin main
```

## How downloads auto-update

On page load the site fetches:

- `shivamkumarmehta64-sketch/mtube/releases/latest` → Download points to `.exe`/`.zip`
- `shivamkumarmehta64-sketch/ytube/releases/latest` → Download points to `.exe`/`.zip`
- `shivamkumarmehta64-sketch/Electronics_Calculator/releases/latest` → `.apk` if present

Publish a new release in any repo → the site button and version badge update automatically. No redeploy needed.

## Analytics

- PostHog is configured with the project API key in `index.html` (`posthog.init('phc_...', { api_host: 'https://us.i.posthog.com' })`).
- Session recording and dead-click capture are disabled in the config to avoid extra blocked-network requests.
- The Content-Security-Policy allows `us.i.posthog.com` in `script-src` and `connect-src`.

## Rebuilding the CSS

When you change utility classes in `index.html`, rebuild the stylesheet:

```bash
npm install
npm run build:css
```

## Notes

- GitHub API is unauthenticated (60 requests/hour per IP) — plenty for a landing page (3 calls per load).
- If the API fails (offline/rate limit), all buttons fall back to the repo releases pages.
