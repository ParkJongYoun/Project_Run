@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Serve.ps1"
if errorlevel 1 pause
