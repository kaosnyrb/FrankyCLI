#!/usr/bin/env bash
# Bite suite for gen_aliaslint, over the LIVE load order (read only; nothing is written).
#
# Three controls, each graded on the tool's OWN words, never a bare exit code:
#   1. NEGATIVE, R3: duo_delvetest_fail carries an LCRT minted so that no Location has it. It must
#      come back CANNOT START on R3 (zero locations). If it does not, the pool evaluator is blind.
#   2. NEGATIVE, R1: every quest listed in KNOWN_FORWARD must come back CANNOT START on R1. These are
#      REAL previous versions, not synthetic mutations (a mutation is authored by the mind that wrote
#      the check, and only probes what it already thought of).
#   3. POSITIVE: no other duo_ quest may come back CANNOT START. Shipped, working missions are the
#      control that stops a linter that refuses everything from reading as a working one.
#
# ⚠ KNOWN_FORWARD is a statement about THIS plugin on 2026-09-24: duo_delve04t is the unfixed
# diagnostic twin of duo_delve04. When it is removed before a release, delete it from this list.
cd /c/Git/FrankyCLI || exit 1
KNOWN_FORWARD="duo_delve04t"
fails=0

out=$(timeout 900 dotnet run -- gen_aliaslint duo_ 2>&1)
echo "$out" | grep -q "SUMMARY over" || { echo "FAIL: no SUMMARY line, the tool did not run"; exit 1; }

# 1. R3 negative control
if echo "$out" | grep -A3 "CANNOT START     duo_delvetest_fail " | grep -q "R3 place .* matches ZERO locations"; then
  echo "[ OK ] duo_delvetest_fail is CANNOT on R3 (the minted, empty ref type)"
else
  echo "[FAIL] duo_delvetest_fail was not CANNOT on R3"; fails=$((fails+1))
fi

# 2. R1 negative controls
for q in $KNOWN_FORWARD; do
  if echo "$out" | grep -A3 "CANNOT START     $q " | grep -q "\[CANNOT \] R1 "; then
    echo "[ OK ] $q is CANNOT on R1 (a real forward reference)"
  else
    echo "[FAIL] $q was not CANNOT on R1"; fails=$((fails+1))
  fi
done

# 3. positive control: nothing else is CANNOT
others=$(echo "$out" | grep -E "^  CANNOT START" | awk '{print $3}' | grep -vxF -e duo_delvetest_fail $(for q in $KNOWN_FORWARD; do echo "-e $q"; done))
if [ -z "$others" ]; then
  echo "[ OK ] no other duo_ quest is CANNOT ($(echo "$out" | grep SUMMARY | sed 's/^ *//'))"
else
  echo "[FAIL] unexpected CANNOT START:"; echo "$others" | sed 's/^/         /'; fails=$((fails+1))
fi

echo
[ $fails -eq 0 ] && echo "ALL CONTROLS HELD" || echo "$fails CONTROL(S) FAILED"
exit $fails
