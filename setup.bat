@echo off
title Black Browser Setup
echo ========================================================
echo             Black Browser v8.5 - Setup Build
echo ========================================================
echo.
echo [INFO] Compiling from src\ (v8.5 codebase)...
echo.

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" (
    echo [ERROR] .NET Framework 4.8 compiler (csc.exe) not found.
    echo        Install .NET Framework 4.8 from:
    echo        https://dotnet.microsoft.com/download/dotnet-framework/net48
    pause
    exit /b 1
)

echo [1/3] Building version resource (black.res)...
"%CSC%" /nologo /out:GenRes.exe GenRes.cs
if errorlevel 1 (
    echo [ERROR] GenRes.cs compilation failed.
    pause
    exit /b 1
)
GenRes.exe icon.ico black.res
if errorlevel 1 (
    echo [ERROR] Resource generation failed.
    pause
    exit /b 1
)
echo [OK] black.res generated (FileDescription = "Black Browser")!

echo [2/3] Compiling src\*.cs...
"%CSC%" /nologo /target:winexe /win32res:black.res /reference:System.IO.Compression.FileSystem.dll /reference:System.IO.Compression.dll /reference:Microsoft.Web.WebView2.Core.dll /reference:Microsoft.Web.WebView2.WinForms.dll /out:Black.exe src\*.cs

if errorlevel 1 (
    echo [ERROR] Compilation failed. Check error messages above.
    pause
    exit /b 1
)
echo [OK] Black.exe compiled successfully!
echo.

echo [3/3] Creating Desktop shortcut...
powershell -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $sc = $ws.CreateShortcut([System.IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'Black Browser.lnk')); $sc.TargetPath = '%~dp0Black.exe'; $sc.WorkingDirectory = '%~dp0'; $sc.IconLocation = '%~dp0icon.ico,0'; $sc.Description = 'Black Browser v8.9 - Windows 11 Fluent 2 Edition'; $sc.Save()"

echo [OK] Desktop shortcut created!
echo.
echo ========================================================
echo Setup complete! Launch Black Browser from your Desktop.
echo ========================================================
echo.
pause
