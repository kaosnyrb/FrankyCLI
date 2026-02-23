#!/usr/bin/env bash
# Dump all placed objects in a worldspace to a file.
# Usage: dump_ws.sh <WorldspaceEditorId> [outputFile]
# Default output: C:/tmp/ws_<WorldspaceEditorId>.txt
WS="$1"
OUT="${2:-C:/tmp/ws_${WS}.txt}"
mkdir -p "C:/tmp"
cd /c/Git/FrankyCLI
dotnet run -- gen_inspect worldspace_objects "$WS" > "$OUT" 2>&1
echo "Dumped $WS -> $OUT  ($(wc -l < "$OUT") lines)"
