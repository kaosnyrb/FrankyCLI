@echo off
setlocal

set EXE="C:\Git\FrankyCLI\bin\Debug\net8.0\FrankyCLI.exe"
set MODNAME=RG_RotTest
set DATAPATH=C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data

echo ============================================
echo   Rotation Test: rg_sts_trk_scom_001
echo   Prefab unpacked at 0 / 90 / 180 / 270 deg
echo ============================================

%EXE% gen_rottest %MODNAME%

echo.
echo ============================================
echo   Converting ESM to ESP for editor...
echo ============================================

set BRIDGE="%DATAPATH%\StarfieldPluginBridge.exe"
%BRIDGE% "%DATAPATH%\%MODNAME%.esm"

echo.
echo ============================================
echo   Done. Load %MODNAME%.esp in CK to inspect.
echo ============================================

endlocal
