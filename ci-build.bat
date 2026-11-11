@echo off
title Black Browser CI Build
echo [CI] Building Black Browser x64...
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo [CI] ERROR: .NET Framework 4.8 csc.exe not found at %CSC%
    exit /b 1
)
if not exist "Microsoft.Web.WebView2.Core.dll" (
    echo [CI] ERROR: Microsoft.Web.WebView2.Core.dll missing from workspace root.
    exit /b 1
)
if not exist "Microsoft.Web.WebView2.WinForms.dll" (
    echo [CI] ERROR: Microsoft.Web.WebView2.WinForms.dll missing from workspace root.
    exit /b 1
)
if not exist "WebView2Loader.dll" (
    echo [CI] ERROR: WebView2Loader.dll missing from workspace root.
    exit /b 1
)

"%CSC%" /nologo /platform:x64 /out:GenRes.exe GenRes.cs || exit /b 1
GenRes.exe icon.ico black.res || exit /b 1
"%CSC%" /nologo /platform:x64 /target:winexe /win32res:black.res /reference:System.IO.Compression.FileSystem.dll /reference:System.IO.Compression.dll /reference:Microsoft.Web.WebView2.Core.dll /reference:Microsoft.Web.WebView2.WinForms.dll /reference:System.Security.dll /out:Black.exe src\*.cs || exit /b 1

if not exist "assets\icons" mkdir "assets\icons"
xcopy /s /y /q "src\assets\icons\*.png" "assets\icons\" >nul

echo [CI] Build OK: Black.exe
exit /b 0