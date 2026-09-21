@echo off
taskkill /f /im AturOS.exe >nul 2>&1
timeout /t 1 /nobreak >nul
if exist "%~dp0bin\Release\v2\AturOS.exe" (
    xcopy /y /e /q "%~dp0bin\Release\v2\*" "%~dp0bin\Release\app\" >nul 2>&1
)
start "" "%~dp0bin\Release\v2\AturOS.exe"
