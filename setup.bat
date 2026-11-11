@echo off
setlocal
title Black Firefox Setup
echo ========================================================
echo             Black Firefox v9.0 - Setup
echo ========================================================
echo.

if not exist "%~dp0src\BrowserForm.cs" goto :portable_install
if not exist "%~dp0GenRes.cs" goto :portable_install

echo [INFO] Source checkout detected. Compiling Black Firefox (x64)...
echo.

set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" goto :nocsc

echo [1/3] Building version resource (black.res)...
"%CSC%" /nologo /platform:x64 /out:"%~dp0GenRes.exe" "%~dp0GenRes.cs"
if errorlevel 1 goto :errgenres
"%~dp0GenRes.exe" "%~dp0icon.ico" "%~dp0black.res"
if errorlevel 1 goto :errres
echo [OK] black.res generated (FileDescription = "Black Firefox")!

echo [2/3] Compiling src\*.cs (x64)...
pushd "%~dp0"
"%CSC%" /nologo /platform:x64 /target:winexe /win32res:black.res /reference:System.IO.Compression.FileSystem.dll /reference:System.IO.Compression.dll /reference:Microsoft.Web.WebView2.Core.dll /reference:Microsoft.Web.WebView2.WinForms.dll /reference:System.Security.dll /out:Black.exe src\*.cs
set "BUILD_RC=%errorlevel%"
popd
if not "%BUILD_RC%"=="0" goto :errcompile
echo [OK] Black.exe compiled successfully!

if not exist "%~dp0assets\icons" mkdir "%~dp0assets\icons"
xcopy /s /y /q "%~dp0src\assets\icons\*.png" "%~dp0assets\icons\" >nul
echo [OK] UI icons deployed to assets\icons\
echo.
goto :check_runtime

:portable_install
echo [INFO] Packaged release detected. Verifying prebuilt binaries...
if not exist "%~dp0Black.exe" goto :missingbin
if not exist "%~dp0WebView2Loader.dll" goto :missingbin
echo [OK] Prebuilt x64 binaries verified.
echo.

:check_runtime
set "WV2_FOUND="
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv >nul 2>&1 && set "WV2_FOUND=1"
reg query "HKLM\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv >nul 2>&1 && set "WV2_FOUND=1"
reg query "HKCU\Software\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv >nul 2>&1 && set "WV2_FOUND=1"

if defined WV2_FOUND (
    echo [OK] Microsoft Edge WebView2 Runtime is installed.
) else (
    echo [WARN] Microsoft Edge WebView2 Runtime was not detected in registry.
    echo        Black Firefox will prompt to install it on first launch, or you can
    echo        download it now from: https://go.microsoft.com/fwlink/p/?LinkId=2124703
)
echo.

echo [3/3] Creating Desktop and Start Menu shortcuts...
powershell -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $targets = @([System.IO.Path]::Combine([Environment]::GetFolderPath('Desktop'), 'Black Firefox.lnk'), [System.IO.Path]::Combine([Environment]::GetFolderPath('Programs'), 'Black Firefox.lnk')); foreach ($p in $targets) { $dir = [System.IO.Path]::GetDirectoryName($p); if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }; $sc = $ws.CreateShortcut($p); $sc.TargetPath = '%~dp0Black.exe'; $sc.WorkingDirectory = '%~dp0'; $sc.IconLocation = '%~dp0icon.ico,0'; $sc.Description = 'Black Firefox v9.0 - Native WebView2 Browser'; $sc.Save() }"
if errorlevel 1 echo [WARN] Shortcut creation encountered an issue.

echo [OK] Shortcuts created!
echo.
echo ========================================================
echo Setup complete! Launch Black Firefox from Desktop or Start Menu.
echo ========================================================
echo.
exit /b 0

:missingbin
echo [ERROR] Black.exe or WebView2Loader.dll is missing in "%~dp0".
echo         Please re-extract the full release ZIP archive and run setup.bat again.
exit /b 1

:nocsc
echo [ERROR] .NET Framework 4.8 compiler (csc.exe) not found.
echo         Install .NET Framework 4.8 from:
echo         https://dotnet.microsoft.com/download/dotnet-framework/net48
exit /b 1

:errgenres
echo [ERROR] GenRes.cs compilation failed.
exit /b 1

:errres
echo [ERROR] Resource generation failed.
exit /b 1

:errcompile
echo [ERROR] Compilation failed. Check error messages above.
exit /b 1