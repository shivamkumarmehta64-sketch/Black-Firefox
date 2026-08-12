# Black Firefox Family

A suite of minimal, privacy-focused browsers sharing the same design philosophy.

## 🖤 Black Firefox (Windows Native)
**Repo:** `shivamkumarmehta64-sketch/Black-Firefox`  
**Stack:** C# .NET 4.8 + WebView2  
**Platform:** Windows 10/11  
**Binary:** ~86 KB  

- Firefox Proton dark theme (#0060df accent)
- 3-layer AdShield ad blocker
- Local bookmarks, history, downloads, reading list
- Firefox View (recently closed + history + bookmarks)
- Tab pinning, copy clean link, library button
- One-click uBlock Origin MV2 install
- Great Sage AI Dock (OpenAI-compatible)
- ~35–50 MB RAM when minimized

[View on GitHub →](https://github.com/shivamkumarmehta64-sketch/Black-Firefox)

---

## 💙 Blue Browser (Tauri Cross-Platform)
**Repo:** `shivamkumarmehta64-sketch/Blue-Browser-Tauri`  
**Stack:** TypeScript/React + Rust (Tauri)  
**Platform:** Windows / macOS / Linux  

- Modern UI with React + Tailwind
- Native performance via Tauri
- Companion to Black Firefox for non-Windows platforms

[View on GitHub →](https://github.com/shivamkumarmehta64-sketch/Blue-Browser-Tauri)

---

## 🌐 Website / Documentation
**Repo:** `shivamkumarmehta64-sketch/black-browser-website`  
**Stack:** HTML/Tailwind (static)  

- Landing page, downloads, changelog
- Hosted at GitHub Pages or deployed separately

[View on GitHub →](https://github.com/shivamkumarmehta64-sketch/black-browser-website)

---

## Quick Start

### Black Firefox (Windows)
```cmd
# Download Black.exe from Releases
# Or build from source:
setup.bat
```

### Blue Browser (Cross-Platform)
```bash
cd Blue-Browser-Tauri
npm install
npm run tauri dev
```

---

## Shared Philosophy

| Principle | Implementation |
|-----------|----------------|
| **Zero bloat** | No Electron, no telemetry, no accounts |
| **Local-first** | All data stored in `%LOCALAPPDATA%` |
| **Ad-free** | 3-layer blocking (domain, JSON, DOM) |
| **Firefox-inspired** | Proton UI, WebExtensions compatible |
| **Open source** | MIT License, community-driven |

---

## License
MIT — see individual repos for details.
