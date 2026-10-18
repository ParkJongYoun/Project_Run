@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0StartTestScenes.ps1"
if errorlevel 1 pause
