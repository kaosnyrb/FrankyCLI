#!/usr/bin/env bash
# Short alias for gen_inspect.
# Usage: gi.sh <RecordType> <search>
# Example: gi.sh PackIn SC_CounterScience01
#          gi.sh placed 0x01F329
#          gi.sh worldspace_objects OESF003World
cd /c/Git/FrankyCLI
dotnet run -- gen_inspect "$1" "$2" 2>&1 | grep -v "^Using launch\|warning CS\|^FrankyCLI$"
