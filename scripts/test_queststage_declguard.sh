#!/usr/bin/env bash
# Bite suite for the queststage declaration guard + --set-alias.
# EVERY case is --dry: nothing is written to du_overtime.esm by this script.
#
# ⛔ THE FIRST VERSION OF THIS HARNESS GRADED ON THE EXIT CODE ALONE AND WAS VACUOUS.
# The invocation was wrong (mode comes BEFORE modname), so every case printed the
# top-level usage and exited 1 -- which read as six correct refusals and two real
# failures. A refusal suite where every input produces the same output has tested
# nothing, and the tell was the UNIFORMITY. So a REFUSE verdict now requires the
# guard's OWN words in the output, and a PASS requires the declaration line.
cd /c/Git/FrankyCLI || exit 1
Q=duo_delve01_layer1
DIST=DefaultAliasOnDistanceLessThan
fails=0
run() {
  want="$1"; needle="$2"; label="$3"; shift 3
  out=$(timeout 600 dotnet run -- queststage du_overtime "$@" --dry 2>&1)
  code=$?
  # "Legacy mode 'queststage'" is printed on the NORMAL path by Program.cs before it
  # dispatches, so it is NOT evidence of a non-dispatch. Grepping for it was a second
  # harness fault and it failed in the ACCUSING direction, over correct work.
  if echo "$out" | grep -qi "Unknown mode"; then
    echo "[FAIL] harness :: $label -- the tool never dispatched"; fails=$((fails+1)); echo; return
  fi
  got=UNCLEAR
  if [ "$want" = REFUSE ] && [ $code -ne 0 ] && echo "$out" | grep -q "$needle"; then got=REFUSE; fi
  if [ "$want" = PASS ] && [ $code -eq 0 ] && echo "$out" | grep -q "$needle"; then got=PASS; fi
  # The counter must NOT be incremented inside $( ): that is a subshell and the increment is lost, which is how this suite reported "0 failing" under a [FAIL] line until 2026-09-24.
  if [ "$got" = "$want" ]; then verdict="  OK "; else verdict="FAIL"; fails=$((fails+1)); fi
  echo "[$verdict] want=$want got=$got :: $label"
  echo "$out" | grep -E "REFUSED|ABSTAIN|declarations:|-> alias |sets stage|--dry:" | sed 's/^/          /'
  echo
}

echo "=== 1. the hole that motivated this: --turnoff on a script with no TurnOffStage ==="
run REFUSE "declares no property named 'TurnOffStage'" "--turnoff on $DIST" \
  hook $Q MBRIR02Player $DIST --stage 300 --turnoff 400 --set-alias TargetAlias=BountyTargetMarker --set TargetDistance=1000

echo "=== 2. a misspelled property name ==="
run REFUSE "declares no property named 'TargetDistnce'" "typo TargetDistnce" \
  hook $Q MBRIR02Player $DIST --stage 300 --set-alias TargetAlias=BountyTargetMarker --set TargetDistnce=1000

echo "=== 3. a Mandatory property left unset ==="
run REFUSE "MANDATORY and this call does not set it" "no TargetAlias / TargetDistance" \
  hook $Q MBRIR02Player $DIST --stage 300

echo "=== 4. --set on an alias-typed property ==="
run REFUSE "use --set-alias TargetAlias" "--set TargetAlias" \
  hook $Q MBRIR02Player $DIST --stage 300 --set TargetAlias=BountyTargetMarker --set TargetDistance=1000

echo "=== 5. --set-alias on an int property ==="
run REFUSE "declares it as 'int', not ReferenceAlias" "--set-alias StageToSet" \
  hook $Q MBRIR02Player $DIST --stage 300 --set-alias StageToSet=BountyTargetMarker --set-alias TargetAlias=BountyTargetMarker --set TargetDistance=1000

echo "=== 6. an alias name that is not on the quest ==="
run REFUSE "no alias named 'NoSuchAliasHere'" "alias NoSuchAliasHere" \
  hook $Q MBRIR02Player $DIST --stage 300 --set-alias TargetAlias=NoSuchAliasHere --set TargetDistance=1000

echo "=== 7. THE QUIET DIRECTION: correct work on an INHERITED-property script must still pass ==="
run PASS "declarations:" "DefaultAliasOnActivate via the extends chain" \
  hook $Q BountyTargetMarker DefaultAliasOnActivate --stage 999

echo "=== 8. the real call ==="
run PASS "TargetAlias -> alias" "$DIST fully specified" \
  hook $Q MBRIR02Player $DIST --stage 300 --prereq 200 --set-alias TargetAlias=BountyTargetMarker --set TargetDistance=1000

echo "================ $fails failing case(s) ================"
exit $fails
