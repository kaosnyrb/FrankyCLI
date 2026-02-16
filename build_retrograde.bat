@echo off
setlocal enabledelayedexpansion

rem ===== Configuration =====
set EXE="C:\Git\FrankyCLI\bin\Debug\net6.0\FrankyCLI.exe"
set MODNAME=du_outlaws_template

rem ===== Quest definitions: faction stationDesign questType count =====
rem Written to a temp file so we can iterate and keep COUNT in scope

set TOTAL=150
set COUNT=0

rem ===== Start timer =====
set START=%TIME%

rem ========================================
rem  BOUNTY - Hab Station (50)
rem ========================================
for /L %%i in (1,1,15) do call :Run Spacer       "Hab Station" bounty
for /L %%i in (1,1,15) do call :Run Crimsonfleet "Hab Station" bounty
for /L %%i in (1,1,10) do call :Run Varuun       "Hab Station" bounty
for /L %%i in (1,1,10) do call :Run Ecliptic     "Hab Station" bounty

rem ========================================
rem  BOUNTY - Ore Station / Industry (50)
rem ========================================
for /L %%i in (1,1,15) do call :Run Spacer       "Ore Station" bounty
for /L %%i in (1,1,15) do call :Run Crimsonfleet "Ore Station" bounty
for /L %%i in (1,1,10) do call :Run Varuun       "Ore Station" bounty
for /L %%i in (1,1,10) do call :Run Ecliptic     "Ore Station" bounty

rem ========================================
rem  POI - Hab Station (25)
rem ========================================
for /L %%i in (1,1,10) do call :Run Spacer       "Hab Station" poi
for /L %%i in (1,1,10) do call :Run Crimsonfleet "Hab Station" poi
for /L %%i in (1,1,5)  do call :Run Ecliptic     "Hab Station" poi

rem ========================================
rem  POI - Ore Station / Industry (25)
rem ========================================
for /L %%i in (1,1,10) do call :Run Spacer       "Ore Station" poi
for /L %%i in (1,1,10) do call :Run Crimsonfleet "Ore Station" poi
for /L %%i in (1,1,5)  do call :Run Ecliptic     "Ore Station" poi

rem ===== Done =====
call :ElapsedTime "%START%" "%TIME%" ELAPSED
cls
echo ============================================
echo   All %TOTAL% quests completed!
echo   Total Time: !ELAPSED!
echo ============================================
exit /b


:Run
rem Args: faction stationDesign questType
set /a COUNT+=1
set /a PCT=(COUNT*100)/TOTAL
set /a BARS=PCT/2

set BAR=
for /L %%A in (1,1,!BARS!) do set BAR=!BAR!#
for /L %%A in (!BARS!,1,49) do set BAR=!BAR!.

call :ElapsedTime "%START%" "%TIME%" ELAPSED

cls
echo ============================================
echo   Quest !COUNT! of %TOTAL%  ^(%~3 / %~1 / %~2^)
echo   Progress: [!BAR!] !PCT!%%
echo   Time Elapsed: !ELAPSED!
echo ============================================

%EXE% %MODNAME% gen_retrograde 0 0 0 %~1 "%~2" %~3

timeout /t 2 /nobreak >nul
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
