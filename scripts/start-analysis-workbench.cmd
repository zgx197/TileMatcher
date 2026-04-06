@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
set "ARGS="

:parse_args
if "%~1"=="" goto run

if /I "%~1"=="--no-open" (
  set "ARGS=%ARGS% -NoOpen"
  shift
  goto parse_args
)

if /I "%~1"=="--port" (
  if not "%~2"=="" (
    set "ARGS=%ARGS% -Port %~2"
    shift
  )
  shift
  goto parse_args
)

set "ARGS=%ARGS% %~1"
shift
goto parse_args

:run
powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%start-analysis-workbench.ps1" %ARGS%
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
  pause
)

exit /b %EXIT_CODE%
