@echo off
setlocal DisableDelayedExpansion

pushd "%~dp0"
if errorlevel 1 (
    echo Could not open the folder containing this BAT file.
    pause
    exit /b 1
)

if not exist "BepInEx\plugins\" if not exist "SPT_Runtime\user\mods\" (
    echo Put this BAT in your SPT game folder, then double-click it again.
    echo It belongs beside the BepInEx and SPT_Runtime folders.
    popd
    pause
    exit /b 1
)

set "REPORT=SPT-Mod-List.txt"
> "%REPORT%" type nul
if errorlevel 1 (
    echo Could not create SPT-Mod-List.txt. Check that this folder is writable.
    popd
    pause
    exit /b 1
)

set "ROOT=%CD%"
if not "%ROOT:~-1%"=="\" set "ROOT=%ROOT%\"
set "ROOT_LENGTH=0"
:CountRoot
if not defined ROOT goto ListMods
set "ROOT=%ROOT:~1%"
set /a ROOT_LENGTH+=1
goto CountRoot

:ListMods
call :ListFolder "BepInEx\plugins"
call :ListFolder "SPT_Runtime\user\mods"

echo Saved SPT-Mod-List.txt
popd
pause
exit /b 0

:ListFolder
if not exist "%~1\" exit /b 0
for /f "delims=" %%F in ('dir "%~1\*.dll" /S /B /A:-D 2^>nul') do (
    set "DLL=%%F"
    setlocal EnableDelayedExpansion
    >> "%REPORT%" echo(!DLL:~%ROOT_LENGTH%!
    endlocal
)
exit /b 0
