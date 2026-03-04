#!/usr/bin/env bash
# Short alias for gen_inspect.
# Usage: gi.sh <RecordType> <search>
# Example: gi.sh PackIn SC_CounterScience01
#          gi.sh PlacedObject doout_city_showdown_marker
#          gi.sh PlacedObject 0x000B0C        (search by FormID)
#          gi.sh WorldspaceStructure NeonCity  (structural summary for crash diagnosis)
#          gi.sh refr 0x01F329
#          gi.sh worldspace_objects OESF003World
cd /c/Git/FrankyCLI
dotnet run -- gen_inspect "$1" "$2" 2>&1 | grep -v "^Using launch\|warning CS\|^FrankyCLI$"
