@echo off
setlocal enabledelayedexpansion
title Black Browser - Package Distribution

echo ========================================================
echo        Black Browser - Local Release Packager
echo ========================================================
echo.

echo [1/5] Building Black.exe (x64)...
call ci-build.bat
if errorlevel 1 (
    echo [ERROR] Build failed!
    exit /b 1
)

set "VERSION=%~1"
if "%VERSION%"=="" set "VERSION=v9.1.0"
set "APP_VER=%VERSION:v=%"
set "DIST_DIR=dist\BlackBrowser-%VERSION%-Windows-x64"
set "ZIP_FILE=dist\BlackBrowser-%VERSION%-Windows-x64.zip"
set "SETUP_EXE=dist\BlackBrowser-%VERSION%-Windows-x64-Setup.exe"

echo.
echo [2/5] Assembling portable distribution folder...
if exist "dist" rmdir /s /q "dist"
mkdir "%DIST_DIR%"
mkdir "%DIST_DIR%\assets\icons"

copy /y "Black.exe" "%DIST_DIR%\" >nul
copy /y "Microsoft.Web.WebView2.Core.dll" "%DIST_DIR%\" >nul
copy /y "Microsoft.Web.WebView2.WinForms.dll" "%DIST_DIR%\" >nul
copy /y "WebView2Loader.dll" "%DIST_DIR%\" >nul
copy /y "icon.ico" "%DIST_DIR%\" >nul
copy /y "setup.bat" "%DIST_DIR%\" >nul
copy /y "run.bat" "%DIST_DIR%\" >nul
copy /y "LICENSE" "%DIST_DIR%\" >nul
copy /y "README.md" "%DIST_DIR%\" >nul
copy /y "assets\icons\*.png" "%DIST_DIR%\assets\icons\" >nul

echo [3/5] Creating distribution ZIP...
powershell -NoProfile -Command "Compress-Archive -Path '%DIST_DIR%\*' -DestinationPath '%ZIP_FILE%' -Force"
if errorlevel 1 (
    echo [ERROR] Failed to create ZIP archive.
    exit /b 1
)

echo [4/5] Building Inno Setup installer if ISCC.exe is installed...
set "ISCC="
if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not defined ISCC goto :skip_iscc

"%ISCC%" /Q "/DMyAppVersion=%APP_VER%" "/DMySetupBaseName=BlackBrowser-%VERSION%-Windows-x64-Setup" "/Odist" installer.iss
if errorlevel 1 (
    echo [WARN] Inno Setup compilation failed; continuing with ZIP distribution.
) else (
    echo [OK] Installer built: %SETUP_EXE%
)
goto :after_iscc

:skip_iscc
echo [INFO] Inno Setup 6 not installed locally; skipping Setup.exe - CI builds it automatically.

:after_iscc

echo [5/5] Generating SHA256SUMS.txt...
powershell -NoProfile -Command "$items = @(Get-ChildItem -Path 'dist' -File | Where-Object { $_.Extension -in '.zip','.exe' }); $items += Get-Item 'Black.exe'; $lines = foreach ($f in $items) { $h = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant(); \"$h  $($f.Name)\" }; Set-Content -Path 'dist\SHA256SUMS.txt' -Value $lines -Encoding ASCII"

echo.
echo Release Package Ready!
echo ========================================================
echo Output ZIP: %ZIP_FILE%
if exist "%SETUP_EXE%" echo Setup EXE:  %SETUP_EXE%
echo Checksums:  dist\SHA256SUMS.txt
echo Folder:     %DIST_DIR%
echo ========================================================
echo Ready to distribute through GitHub Releases!
echo.
exit /b 0
