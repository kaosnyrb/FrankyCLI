@echo off
setlocal enabledelayedexpansion

rem ===== Configuration =====
set EXE="C:\Git\FrankyCLI\bin\Debug\net8.0\FrankyCLI.exe"
set MODNAME=cleanslate

set TOTAL=1
set COUNT=0

rem ===== Start timer =====
set START=%TIME%

rem ========================================
rem  Single station for testing
rem ========================================
call :Run Spacer "Hab Station" bounty


rem ===== Done =====
call :ElapsedTime "%START%" "%TIME%" ELAPSED
echo ============================================
echo   Done! Total Time: !ELAPSED!
echo ============================================
exit /b


:Run
rem Args: faction stationDesign questType
set /a COUNT+=1
echo ============================================
echo   Station !COUNT! of %TOTAL%  ^(%~3 / %~1 / %~2^)
echo ============================================

%EXE% %MODNAME% gen_retrograde 0 0 0 %~1 "%~2" %~3

exit /b


:ElapsedTime
rem Args: startTime endTime outputVar
setlocal
set START=%~1
set END=%~2

for /f "tokens=1-4 delims=:." %%a in ("%START%") do (
    set /a S_H=%%a, S_M=%%b, S_S=%%c
)
for /f "tokens=1-4 delims=:." %%a in ("%END%") do (
    set /a E_H=%%a, E_M=%%b, E_S=%%c
)

set /a STARTSEC=S_H*3600 + S_M*60 + S_S
set /a ENDSEC=E_H*3600 + E_M*60 + E_S

if %ENDSEC% LSS %STARTSEC% set /a ENDSEC+=86400

set /a ELAPSEC=ENDSEC-STARTSEC
set /a H=ELAPSEC/3600
set /a M=(ELAPSEC%%3600)/60
set /a S=ELAPSEC%%60

endlocal & set "%~3=%H%h %M%m %S%s"
exit /b
