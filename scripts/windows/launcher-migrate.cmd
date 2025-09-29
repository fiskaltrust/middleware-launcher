@echo off
setlocal enableextensions
cd /d "%~dp0%"
net.exe session 1>nul 2>nul || (echo This script requires elevated rights. & exit /b 1)

if not exist fiskaltrust.exe (
	echo The file fiskaltrust.exe does not exist in the current folder.
	echo See http://link.fiskaltrust.cloud/launcher/migration-script for more information on how to use the script.
	pause
	exit /b 1
)

set _cmd="%cd%\fiskaltrust.exe"
for /f "skip=1 tokens=1-6 delims=, " %%A in ('wmic service get name^, PathName^') do ( 
	if %_cmd% == %%B (
		if not defined ftServiceName (
			set ftServiceName=%%A
		) else (
			echo More than one service is registered for fiskaltrust.exe. This installation can not be migrated automatically.
			echo See http://link.fiskaltrust.cloud/launcher/migration-script for more information on how to use the script.
			pause
			exit /b 1
		)
	)
)
echo
if exist .backup\ (
	echo The Backup folder: '.backup' already exists. Rename this folder to not loose data.
	pause
	exit /b 1
)
if defined ftServiceName (
	goto ResolveInitialState
)

if not defined ftServiceName (
	echo No installed service was found for fiskaltrust.exe. This installation can not be migrated automatically.
	echo See http://link.fiskaltrust.cloud/launcher/migration-script for more information on how to use the script.
	pause
	exit /b 1
)

:ResolveInitialState
sc query %ftServiceName% | find "STATE" | find "RUNNING" >NUL
if errorlevel 0 if not errorlevel 1 goto StopService
SC query %ftServiceName% | find "STATE" | find "STOPPED" >NUL
if errorlevel 0 if not errorlevel 1 goto StopedService
SC query %ftServiceName% | find "STATE" | find "PAUSED" >NUL
if errorlevel 0 if not errorlevel 1 goto SystemOffline
echo Service State is changing, waiting for service to resolve its state before making changes
sc query %ftServiceName% | find "STATE"
timeout /t 2 /nobreak >NUL
goto ResolveInitialState

:StopService
echo Stopping %ftServiceName%
sc stop %ftServiceName% >NUL

goto StopingService

:SystemOffline
echo System is offline
exit /b 1

:StopingServiceDelay
timeout /t 2 /nobreak >NUL

:StopingService
echo Waiting for %ftServiceName% to stop
sc query %ftServiceName% | find "STATE" | find "STOPPED" >NUL
if errorlevel 1 goto StopingServiceDelay

:StopedService
echo %ftServiceName% is stopped

sc delete %ftServiceName%

mkdir .backup

move *.dll .backup\ >nul
move fiskaltrust.exe .backup\ >nul
move fiskaltrust.InstallLog .backup\ >nul
move fiskaltrust.InstallState .backup\ >nul
move install-service.cmd .backup\ >nul
move test.cmd .backup\ >nul
move uninstall-service.cmd .backup\ >nul
copy fiskaltrust.exe.config .backup\ >nul

fiskaltrust.Launcher.exe config get > .backup\launcher-config-backup.json
fiskaltrust.Launcher.exe install --service-name %ftServiceName%

pause