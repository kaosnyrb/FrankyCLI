#!/usr/bin/env bash
# Identify a Starfield FormKey by trying Static, PackIn, and Activator in sequence.
# Usage: lookup_fk.sh <formId>   (without 0x prefix)
# Example: lookup_fk.sh 075B8D
FORMID="0x$1"
mkdir -p "C:/tmp"
cd /c/Git/FrankyCLI
for TYPE in Static PackIn Activator; do
    OUT="C:/tmp/_lookup_${TYPE}_${1}.txt"
    dotnet run -- gen_inspect "$TYPE" "$FORMID" > "$OUT" 2>&1
    COUNT=$(grep "Total records found:" "$OUT" | sed 's/.*Total records found: //')
    if [ "$COUNT" != "0" ] && [ -n "$COUNT" ]; then
        echo "[$TYPE] $FORMID"
        grep -E "EditorID:|FormKey:|File:" "$OUT" | head -4
        rm -f "$OUT"
        exit 0
    fi
    rm -f "$OUT"
done
echo "$FORMID — not found in Static/PackIn/Activator. Likely MiscItem, Container, or NPC form."
