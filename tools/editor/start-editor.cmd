@echo off
rem ===================================================================
rem  Starts the merder level editor.
rem
rem  Double-click it, or run it from a terminal. Either way it builds
rem  first (incremental, so usually a second or two) and then opens the
rem  window. Pass "release" to build a Release copy instead.
rem
rem  Works from any working directory - every path below is derived from
rem  where this script lives, not from where you ran it.
rem ===================================================================
setlocal

set "ROOT=%~dp0"
set "APP=%ROOT%Merder.Editor.App"

set "CONFIG=Debug"
if /i "%~1"=="release" set "CONFIG=Release"

set "EXE=%APP%\bin\%CONFIG%\net10.0\Merder.Editor.App.exe"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo.
    echo   Can't find "dotnet" on your PATH.
    echo   Install the .NET SDK from https://dotnet.microsoft.com/download
    goto :failed
)

echo Building the editor ^(%CONFIG%^)...
dotnet build "%APP%" --configuration %CONFIG% --nologo --verbosity quiet
if errorlevel 1 (
    echo.
    echo   Build failed - see the errors above.
    goto :failed
)

if not exist "%EXE%" (
    echo.
    echo   Built, but no exe at:
    echo   %EXE%
    goto :failed
)

echo Starting...

rem /d sets the new process's working directory, so it never inherits
rem whatever folder this script was launched from.
start "merder level editor" /d "%APP%\bin\%CONFIG%\net10.0" "%EXE%"

endlocal
exit /b 0

:failed
rem Only pause when double-clicked - in a terminal the output is already
rem on screen and a stray "press any key" is just in the way.
echo %cmdcmdline% | find /i "%~nx0" >nul && pause
endlocal
exit /b 1
