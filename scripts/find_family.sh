#!/usr/bin/env bash
# List all PackIn variants matching an EditorID prefix, with their FormKeys.
# Usage: find_family.sh <prefix>
# Example: find_family.sh SC_LD_CratesMedium
#          find_family.sh DesktopClutter_Science
cd /c/Git/FrankyCLI
dotnet run -- gen_inspect PackIn "$1" 2>&1 | \
    grep -E "EditorID:|FormKey:" | \
    paste - -
