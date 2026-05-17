@echo off
setlocal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-release.ps1" %*
set "EXIT_CODE=%ERRORLEVEL%"

if not "%CI%"=="true" pause
exit /b %EXIT_CODE%
