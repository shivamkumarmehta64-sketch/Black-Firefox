@echo off
setlocal enabledelayedexpansion
title Black Firefox - Package Distribution

echo ========================================================
echo        Black Firefox - Local Release Packager
echo ========================================================
echo.

echo [1/4] Building Black.exe...
call ci-build.bat
if errorlevel 1 (
    echo [ERROR] Build failed!
    exit /b 1
)

set "VERSION=v9.0.0"
set "DIST_DIR=dist\BlackFirefox-%VERSION%-Windows-x64"
set "ZIP_FILE=dist\BlackFirefox-%VERSION%-Windows-x64.zip"

echo.
echo [2/4] Assembling distribution folder...
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

echo [3/4] Creating distribution ZIP...
powershell -NoProfile -Command "Compress-Archive -Path '%DIST_DIR%\*' -DestinationPath '%ZIP_FILE%' -Force"

echo.
echo [4/4] Release Package Ready!
echo ========================================================
echo Output ZIP: %ZIP_FILE%
echo Folder:     %DIST_DIR%
echo ========================================================
echo Ready to distribute through GitHub Releases!
echo.
exit /b 0
