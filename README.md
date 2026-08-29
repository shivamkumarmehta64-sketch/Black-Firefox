# 🖤 Black Firefox

[![Release](https://img.shields.io/badge/Release-v9.0.0-0C63E8.svg)](https://github.com/shivamkumarmehta64-sketch/Black-Firefox/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011-0078D6.svg)](https://microsoft.com/windows)
[![Engine](https://img.shields.io/badge/Engine-Microsoft%20WebView2%20(Chromium)-00C7B7.svg)](https://developer.microsoft.com/en-us/microsoft-edge/webview2/)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4.svg)](https://dotnet.microsoft.com/download/dotnet-framework/net48)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Zero Telemetry](https://img.shields.io/badge/Telemetry-0%25%20Local%20Only-emerald.svg)](#privacy--security)

**Black Firefox** is an ultra-fast, minimal, privacy-first web browser for Windows 10/11 built natively in **C# .NET 4.8** with **Microsoft WebView2 (Chromium)**.

It delivers a sleek dark glassmorphic UI, a built-in 3-layer AdShield ad blocker, DPAPI-encrypted password storage, customizable Speed Dial dashboard, and zero cloud telemetry.

---

## ✨ Features

- 🎨 **Glassmorphic Fluent UI:** Immersive dark title bar, 185px fixed-width tab pills, custom animated aurora chrome (`ChromePainter`), and transparent glass buttons.
- 🛡️ **3-Layer AdShield:** Network-level ad domain blocking, YouTube API ad-payload stripping, and auto-click video ad skipping.
- 🔒 **DPAPI Password Manager:** Local-only password capture with OS-level Windows DPAPI encryption. No cloud, no external tracking.
- ⭐ **Local Bookmarks & History:** Searchable bookmarks and history with Netscape HTML export and import.
- 🚀 **Speed Dial New Tab (`black://home`):** 5 theme presets (Aurora, Cyberpunk, Nebula, Obsidian, Light), live digital clock, custom search engine chips (Google, DuckDuckGo, Bing, YouTube, GitHub, Wikipedia), user-defined shortcuts, and scratchpad.
- 🧩 **Chrome/Edge Extension Support:** Load and manage unpacked MV2 extensions directly.
- ⚡ **Lightweight & Efficient:** Single ~460 KB executable, under 60 MB RAM footprint, background non-blocking GC memory trimmer.
- 🌓 **Eye Care & Screen Dimmer:** Built-in warm amber blue-light filter and brightness reduction modes.
- 📱 **Instant QR Code:** Generate a QR code for the current URL to open any page on your phone instantly.

---

## 📥 Download & Installation

### Option 1: Download from GitHub Releases (Recommended)
1. Download the latest **`BlackFirefox-v9.0.0-Windows-x64.zip`** from [GitHub Releases](https://github.com/shivamkumarmehta64-sketch/Black-Firefox/releases).
2. Extract the archive anywhere on your PC.
3. Run **`Black.exe`** (or execute `setup.bat` to create a Desktop shortcut).

### Option 2: Build from Source
No Visual Studio or MSBuild installation is required! Built using the native Windows .NET 4.8 compiler:

```cmd
# Clone repository
git clone https://github.com/shivamkumarmehta64-sketch/Black-Firefox.git
cd Black-Firefox

# Build and run
ci-build.bat
Black.exe
```

### Option 3: Package Distribution ZIP Locally
```cmd
package-release.bat
```
Generates a ready-to-distribute `dist\BlackFirefox-v9.0.0-Windows-x64.zip` package.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| <kbd>Ctrl</kbd> + <kbd>T</kbd> | New Tab (Opens Speed Dial) |
| <kbd>Ctrl</kbd> + <kbd>W</kbd> | Close Current Tab |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>T</kbd> | Reopen Last Closed Tab |
| <kbd>Ctrl</kbd> + <kbd>Tab</kbd> / <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>Tab</kbd> | Next / Previous Tab |
| <kbd>Ctrl</kbd> + <kbd>1</kbd> .. <kbd>8</kbd> | Switch to Tab 1–8 |
| <kbd>Ctrl</kbd> + <kbd>9</kbd> | Switch to Last Tab |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>P</kbd> | New InPrivate Tab |
| <kbd>Ctrl</kbd> + <kbd>H</kbd> | Browsing History (`black://history`) |
| <kbd>Ctrl</kbd> + <kbd>J</kbd> | Downloads (`black://downloads`) |
| <kbd>Ctrl</kbd> + <kbd>D</kbd> | Bookmark Current Page |
| <kbd>Ctrl</kbd> + <kbd>Shift</kbd> + <kbd>X</kbd> | Extension Manager (`black://extensions`) |
| <kbd>Ctrl</kbd> + <kbd>F</kbd> | Find in Page |
| <kbd>Ctrl</kbd> + <kbd>+</kbd> / <kbd>-</kbd> / <kbd>0</kbd> | Zoom In / Out / Reset (100%) |
| <kbd>F5</kbd> / <kbd>Ctrl</kbd> + <kbd>R</kbd> | Reload Page |
| <kbd>F11</kbd> | Fullscreen Mode |
| <kbd>Alt</kbd> + <kbd>Left</kbd> / <kbd>Right</kbd> | Navigate Back / Forward |
| <kbd>Alt</kbd> + <kbd>Home</kbd> | Navigate to Home Page |

---

## 🏛️ Architecture & Internal URLs

| URL | Description |
|---|---|
| `black://home` | Dynamic Speed Dial new tab dashboard |
| `black://bookmarks` | Local bookmark management and HTML import/export |
| `black://history` | Local browsing history search and cleanup |
| `black://downloads` | Download manager and file shelf |
| `black://passwords` | DPAPI-encrypted credential manager |
| `black://cookies` | Cookie viewer and cache manager |
| `black://extensions` | Chrome/Edge unpacked extension loader |
| `black://about` | Browser details and architecture specifications |
| `black://privacy` | Zero-cloud telemetry commitment |
| `black://terms` | Open-source MIT License terms |

---

## 🔒 Privacy & Security

All browser configuration, cache, and user data are stored **100% locally** in `%LOCALAPPDATA%\black-webview2\`:
- No telemetry pings or analytical trackers
- No user account or cloud login requirements
- Passwords protected via Windows DPAPI hardware-tied keys
- Fully auditable open-source codebase

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).
