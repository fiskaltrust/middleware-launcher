@echo off
:: Checking for administrator privileges
NET SESSION >nul 2>&1
IF %ERRORLEVEL% == 0 (
    ECHO Running with administrator privileges.
    :: Uninstall command
    fiskaltrust.Launcher.exe uninstall
) ELSE (
    ECHO Restarting with administrator privileges...
    PowerShell -NoProfile -ExecutionPolicy Bypass -Command "& {Start-Process cmd.exe -ArgumentList '/c \"%~f0\"' -Verb RunAs}"
    EXIT /B
)