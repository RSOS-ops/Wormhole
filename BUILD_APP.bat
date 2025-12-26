@echo off
set "Framework=C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
set "SrcDir=.\src"
set "AssetsDir=.\assets"

echo ================================================
echo   Compiling Wormhole Console (Admin Mode)
echo ================================================

:: 1. Resource Validation
if not exist "%AssetsDir%\wormhole.ico" (
    echo ERROR: wormhole.ico not found in assets folder!
    pause
    exit /b
)

if not exist "app.manifest" (
    echo ERROR: app.manifest not found!
    pause
    exit /b
)

:: 2. Compilation
:: Note: /win32manifest embeds the admin requirement
"%Framework%\csc.exe" /target:winexe /out:WormholeConsole.exe ^
  /win32icon:"%AssetsDir%\wormhole.ico" ^
  /win32manifest:"app.manifest" ^
  /r:System.dll ^
  /r:System.Drawing.dll ^
  /r:System.Windows.Forms.dll ^
  /r:System.ServiceProcess.dll ^
  /r:"%AssetsDir%\Microsoft.Web.WebView2.Core.dll" ^
  /r:"%AssetsDir%\Microsoft.Web.WebView2.WinForms.dll" ^
  "%SrcDir%\Wormhole.cs"

:: 3. Post-Build: Copy Runtime DLLs
:: The EXE fails without these specific files in the same folder
if %errorlevel% == 0 (
    echo.
    echo   > Copying WebView2 Dependencies...
    copy /Y "%AssetsDir%\Microsoft.Web.WebView2.Core.dll" . >nul
    copy /Y "%AssetsDir%\Microsoft.Web.WebView2.WinForms.dll" . >nul
    
    :: Check for Loader (Critical for runtime)
    if exist "%AssetsDir%\WebView2Loader.dll" (
        copy /Y "%AssetsDir%\WebView2Loader.dll" . >nul
    )

    echo.
    echo SUCCESS! WormholeConsole.exe created with Admin privileges.
) else (
    echo.
    echo BUILD FAILED. Check errors above.
)

pause