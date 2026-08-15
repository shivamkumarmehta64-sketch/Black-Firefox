@echo off
title Black Firefox CI Build
echo [CI] Building Black Firefox (signpath.io pipeline)...
set "CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

"%CSC%" /nologo /out:GenRes.exe GenRes.cs || exit /b 1
GenRes.exe icon.ico black.res || exit /b 1
"%CSC%" /nologo /target:winexe /win32res:black.res /reference:System.IO.Compression.FileSystem.dll /reference:System.IO.Compression.dll /reference:Microsoft.Web.WebView2.Core.dll /reference:Microsoft.Web.WebView2.WinForms.dll /out:Black.exe src\*.cs || exit /b 1

echo [CI] Build OK: Black.exe
exit /b 0