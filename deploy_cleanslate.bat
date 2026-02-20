@echo off
set SRC=C:\modding\DU_Phaseshift
set DEST=C:\Program Files (x86)\Steam\steamapps\common\Starfield\Data

copy /Y "%SRC%\cleanslate.esm" "%DEST%\cleanslate.esm"
copy /Y "%SRC%\cleanslate.esp" "%DEST%\cleanslate.esp"

echo Done.
