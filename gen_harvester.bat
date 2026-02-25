@echo off
:: gen_harvester.bat — Starfield Furniture Prefab Harvester
::
:: Usage: gen_harvester.bat <baseForm> [radius] [maxVariants] [outputMod]
::
::   baseForm     EditorID (partial, case-insensitive) or 0xFormID
::   radius       Capture radius in game units (default: 150)
::   maxVariants  Max PackIn prefabs to generate (default: 50)
::   outputMod    Output ESM name without extension (default: harvested_prefabs)
::
:: Examples:
::   gen_harvester.bat MedBed
::   gen_harvester.bat ChairOffice 120 20
::   gen_harvester.bat 0x00012345 200 10 my_prefabs

dotnet build || goto :eof
bin\Debug\net8.0\FrankyCLI.exe gen_harvester %*
