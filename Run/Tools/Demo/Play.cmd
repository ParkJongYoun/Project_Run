@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Launcher\StartDemo.ps1"
if errorlevel 1 pause
