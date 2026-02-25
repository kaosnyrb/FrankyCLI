@echo off
set SRC=C:\modding\DU_Phaseshift
set DEST=C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data

copy /Y "%SRC%\cleanslate.esm" "%DEST%\RG_Racetrack.esm"
copy /Y "%SRC%\cleanslate.esp" "%DEST%\RG_Racetrack.esp"

echo Done.
