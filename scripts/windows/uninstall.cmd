@echo off
cd /d "%~dp0%"
net.exe session 1>nul 2>nul || (echo This script requires elevated rights. & exit /b 1)

fiskaltrust.Launcher.exe uninstall

pause