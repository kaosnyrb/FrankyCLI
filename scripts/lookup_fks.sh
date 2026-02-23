#!/usr/bin/env bash
# Batch FormID lookup — resolves each ID as Static → PackIn → Activator.
# Usage: lookup_fks.sh <formId1> [formId2 ...]
# Example: lookup_fks.sh 0DB962 0DB963 066838 2ACD6C
# Output: one block per ID showing EditorID, type, and mesh path.
cd /c/Git/FrankyCLI
for id in "$@"; do
  echo "=== $id ==="
  bash scripts/lookup_fk.sh "$id" 2>&1 | grep -v "^Using launch\|warning CS\|^FrankyCLI$"
done
