@echo off
setlocal enableextensions enabledelayedexpansion
SET "ftServiceName="
set _cmd="%cd%\fiskaltrust.exe"
for /f "skip=1 tokens=1-6 delims=, " %%A in ('wmic service get name^, PathName^') do ( 
	if %_cmd% == %%B (
		if .!ftServiceName!==. (
			SET ftServiceName=%%A
		) ELSE (
			echo "More than one service is registered. This can not be migrated automatically."
			exit /b 1
		)
	)
)
echo
if .!ftServiceName!==. (
	GOTO ResolveInitialState
)

if .ftServiceName!==. (
	echo  "No service installed"
	exit /b 1
)

:ResolveInitialState
SC query %ftServiceName% | FIND "STATE" | FIND "RUNNING" >NUL
IF errorlevel 0 IF NOT errorlevel 1 GOTO StopService
SC query %ftServiceName% | FIND "STATE" | FIND "STOPPED" >NUL
IF errorlevel 0 IF NOT errorlevel 1 GOTO StopedService
SC query %ftServiceName% | FIND "STATE" | FIND "PAUSED" >NUL
IF errorlevel 0 IF NOT errorlevel 1 GOTO SystemOffline
echo Service State is changing, waiting for service to resolve its state before making changes
sc query %ftServiceName% | Find "STATE"
timeout /t 2 /nobreak >NUL
GOTO ResolveInitialState

:StopService
echo Stopping %ftServiceName%
sc stop %ftServiceName% >NUL

GOTO StopingService
:StopingServiceDelay
echo Waiting for %ftServiceName% to stop
timeout /t 2 /nobreak >NUL
:StopingService
SC query %ftServiceName% | FIND "STATE" | FIND "STOPPED" >NUL
IF errorlevel 1 GOTO StopingServiceDelay

:StopedService
echo %ftServiceName% is stopped

sc delete %ftServiceName%

if exist .backup\ (
	echo "The Backup folder: '.backup' already exists. Rename this folder to not loose data."
	exit /b 1
)
mkdir .backup
set cpath=%cd%
FOR /R %cd% %%F in (*.dll) do ( 
	move %%F %cpath%\.backup
)
move %cpath%\fiskaltrust.exe %cpath%\.backup
move %cpath%\fiskaltrust.InstallLog %cpath%\.backup
move %cpath%\fiskaltrust.InstallState %cpath%\.backup
move %cpath%\install-service.cmd %cpath%\.backup
move %cpath%\test.cmd %cpath%\.backup
move %cpath%\uninstall-service.cmd %cpath%\.backup

fiskaltrust.Launcher.exe install --service-name %ftServiceName%