#!/usr/bin/env bash
# Generate Science Hallway PackIn variants into generated_templates.esm
# Usage: gen_roompackin.sh
# Output: Starfield/Data/generated_templates.esm
cd /c/Git/FrankyCLI
dotnet run -- gen_roompackin 2>&1
