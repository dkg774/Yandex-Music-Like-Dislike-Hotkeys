@echo off
setlocal
msbuild "%~dp0Yandex-Music-Like-Dislike-Hotkeys.sln" /m /p:Configuration=Release
if errorlevel 1 (
  echo.
  echo Build failed. Run this file from a Visual Studio Developer Command Prompt or install Visual Studio Build Tools.
  pause
  exit /b 1
)
echo.
echo Build completed: App\bin\Release\Yandex-Music-Like-Dislike-Hotkeys.exe
pause
