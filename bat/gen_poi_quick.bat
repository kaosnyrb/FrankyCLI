@echo off
setlocal enabledelayedexpansion

rem ===== Paths =====
set EXE=C:\Git\FrankyCLI\bin\Debug\net8.0\FrankyCLI.exe
set PROJ=C:\Git\FrankyCLI\FrankyCLI.csproj
set SRC=C:\modding\DU_Phaseshift
set DATA=C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data
set PLUGINS=%LOCALAPPDATA%\Starfield\Plugins.txt

rem ===== Random mod name =====
set MODNAME=rg_poi_%RANDOM%
echo Mod name: %MODNAME%

rem ===== Build =====
echo.
echo [1/4] Building...
dotnet build "%PROJ%" || goto :error

rem ===== Deploy cleanslate seed =====
echo.
echo [2/4] Seeding %MODNAME%.esm in Data...
copy /Y "%SRC%\cleanslate.esm" "%DATA%\%MODNAME%.esm" || goto :error
copy /Y "%SRC%\cleanslate.esp" "%DATA%\%MODNAME%.esp" || goto :error

rem ===== Register in plugins.txt =====
echo.
echo [3/4] Registering in Plugins.txt...
findstr /i /c:"%MODNAME%.esm" "%PLUGINS%" >nul 2>&1
if errorlevel 1 (
    echo %MODNAME%.esm >> "%PLUGINS%"
    echo   Added %MODNAME%.esm
) else (
    echo   Already present
)

rem ===== Generate 5 Hab Station + 5 Ore Station POIs =====
echo.
echo [4/4] Generating 10 POIs (5 Hab Station, 5 Ore Station)...
for /L %%i in (1,1,5) do (
    echo   Hab Station POI %%i of 5...
    "%EXE%" gen_retrograde %MODNAME% "" "Hab Station" poi quiet || goto :error
)
for /L %%i in (1,1,5) do (
    echo   Ore Station POI %%i of 5...
    "%EXE%" gen_retrograde %MODNAME% "" "Ore Station" poi quiet || goto :error
)

echo.
echo ============================================
echo   Done!  %MODNAME%.esm  ^(10 POIs^)
echo   5 x Hab Station  +  5 x Ore Station
echo ============================================
goto :eof

:error
echo.
echo ERROR - aborting.
exit /b 1
