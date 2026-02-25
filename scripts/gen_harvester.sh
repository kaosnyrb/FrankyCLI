#!/usr/bin/env bash
# gen_harvester.sh — Starfield Furniture Prefab Harvester
#
# Usage: gen_harvester.sh <baseForm> [radius] [maxVariants] [outputMod]
#
#   baseForm     EditorID (partial, case-insensitive) or 0xFormID
#   radius       Capture radius in game units (default: 150)
#   maxVariants  Max PackIn prefabs to generate (default: 50)
#   outputMod    Output ESM name without extension (default: harvested_prefabs)
#
# Examples:
#   gen_harvester.sh MedBed
#   gen_harvester.sh ChairOffice 120 20
#   gen_harvester.sh 0x00012345 200 10 my_prefabs

cd /c/Git/FrankyCLI
dotnet run -- gen_harvester "$@" 2>&1
