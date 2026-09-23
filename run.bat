@echo off
taskkill /f /im AturOS.exe >nul 2>&1
timeout /t 1 /nobreak >nul
if exist "%~dp0publish_release\AturOS.exe" (
    copy /y "%~dp0publish_release\AturOS.exe" "%~dp0publish_build\AturOS.exe" >nul 2>&1
    copy /y "%~dp0publish_release\AturOS.exe" "%~dp0publish\AturOS.exe" >nul 2>&1
    copy /y "%~dp0publish_release\AturOS.exe" "%~dp0publish_latest\AturOS.exe" >nul 2>&1
) else if exist "%~dp0publish_build\AturOS.exe" (
    copy /y "%~dp0publish_build\AturOS.exe" "%~dp0publish\AturOS.exe" >nul 2>&1
    copy /y "%~dp0publish_build\AturOS.exe" "%~dp0publish_latest\AturOS.exe" >nul 2>&1
)
start "" "%~dp0publish\AturOS.exe"

