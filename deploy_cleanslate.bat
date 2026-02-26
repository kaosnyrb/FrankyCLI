@echo off
set SRC=C:\modding\DU_Phaseshift
set DEST=C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data
set NAME=%~1
if "%NAME%"=="" set NAME=cleanslate

copy /Y "%SRC%\cleanslate.esm" "%DEST%\%NAME%.esm"
copy /Y "%SRC%\cleanslate.esp" "%DEST%\%NAME%.esp"

echo Done.
