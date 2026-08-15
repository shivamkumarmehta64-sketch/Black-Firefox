@echo off
title Black Firefox Setup
echo ========================================================
echo             Black Firefox v9.0 - Setup Build
echo ========================================================
echo.
echo [INFO] Compiling from src\ (v9.0 codebase)...
echo.

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

if not exist "%CSC%" goto :nocsc

echo [1/3] Building version resource (black.res)...
"%CSC%" /nologo /out:GenRes.exe GenRes.cs
if errorlevel 1 goto :errgenres
GenRes.exe icon.ico black.res
if errorlevel 1 goto :errres
echo [OK] black.res generated (FileDescription = "Black Firefox")!

echo [2/3] Compiling src\*.cs...
"%CSC%" /nologo /target:winexe /win32res:black.res /reference:System.IO.Compression.FileSystem.dll /reference:System.IO.Compression.dll /reference:Microsoft.Web.WebView2.Core.dll /reference:Microsoft.Web.WebView2.WinForms.dll /out:Black.exe src\*.cs
if errorlevel 1 goto :errcompile
echo [OK] Black.exe compiled successfully!
echo.

echo [3/3] Creating Desktop shortcut...
powershell -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $sc = $ws.CreateShortcut([System.IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'Black Firefox.lnk')); $sc.TargetPath = '%~dp0Black.exe'; $sc.WorkingDirectory = '%~dp0'; $sc.IconLocation = '%~dp0icon.ico,0'; $sc.Description = 'Black Firefox v9.0 - Windows 11 Fluent 2 Edition'; $sc.Save()"

echo [OK] Desktop shortcut created!
echo.
echo ========================================================
echo Setup complete! Launch Black Firefox from your Desktop.
echo ========================================================
echo.
goto :eof

:nocsc
echo [ERROR] .NET Framework 4.8 compiler (csc.exe) not found.
echo        Install .NET Framework 4.8 from:
echo        https://dotnet.microsoft.com/download/dotnet-framework/net48
goto :eof

:errgenres
echo [ERROR] GenRes.cs compilation failed.
goto :eof

:errres
echo [ERROR] Resource generation failed.
goto :eof

:errcompile
echo [ERROR] Compilation failed. Check error messages above.
goto :eof