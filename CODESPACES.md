Black-Firefox Codespaces / Devcontainer

This repository targets .NET Framework 4.8 and Windows-specific APIs (WebView2). GitHub Codespaces provides a Linux-based container development environment; because of that, this devcontainer is configured for editing, IntelliSense and lightweight tasks only — it cannot build or run the Windows .NET Framework 4.8 binary.

What this devcontainer provides
- C# IntelliSense and language features (OmniSharp) via the ms-dotnettools.csharp VS Code extension
- dotnet SDK (Linux) for code navigation and tooling (does NOT compile .NET Framework 4.8 assemblies)
- Git tooling and common editor extensions (GitLens, EditorConfig)

What you can do in Codespaces
- Edit source, refactor, run text search, and run git operations (commit/push)
- Run cross-platform .NET Core / .NET 6+ tools if present
- Run linters and unit tests that target .NET Core (if the project includes them)

What you cannot do in Codespaces (limitations)
- Build or run the Windows-only .NET Framework 4.8 application or WebView2-based UI in a Linux container
- Test the Windows UI or produce Windows installers from within Codespaces

Recommended workflow
1. Use Codespaces for editing, code review, and CI-ready changes (tests, linting, refactors).
2. For building and testing the final Windows binary, use one of:
   - A local Windows machine with Visual Studio targeting .NET Framework 4.8
   - GitHub Actions with runs-on: windows-latest to build and produce artifacts
   - A self-hosted Windows runner or VM that has Visual Studio and WebView2 SDK installed

How to open a Codespace
- On GitHub: go to this repository > Code > Codespaces > New codespace
- Or use the GitHub CLI: gh codespace create --repo shivamkumarmehta64-sketch/Black-Firefox --branch add/devcontainer-codespaces

Notes for maintainers
- If you want full Windows build support in cloud, consider adding a GitHub Actions workflow that builds on windows-latest and publishes artifacts.

