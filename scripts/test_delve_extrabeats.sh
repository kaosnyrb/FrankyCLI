#!/usr/bin/env bash
# Bite suite for the CREATED beat slot in gen_delve.
#
# Every case is --dry or lint: nothing is written to du_overtime.esm by this script.
# A REFUSE verdict requires the lint's OWN words in the output, never a bare exit code --
# an earlier suite in this repo graded on the code alone, hit a usage print that also
# exits 1, and read six vacuous refusals as passes. The tell was UNIFORMITY.
cd /c/Git/FrankyCLI || exit 1
TMP="${TMPDIR:-/tmp}/delvebite"
mkdir -p "$TMP"
fails=0

mk() {  # mk <file> <beats-json>
  cat > "$TMP/$1.json" <<EOF
{ "schema": 1, "id": "duo_bite", "template": "two-beat-one-place",
  "place": { "theme": { "require": [], "exclude": [] } },
  "prose": { "name": "n <Place>", "briefing": "b <Place>" },
  "beats": $2 }
EOF
}

run() { # run WANT needle label file
  want="$1"; needle="$2"; label="$3"; f="$4"
  out=$(timeout 900 dotnet run -- gen_delve lint "$TMP/$f.json" 2>&1); code=$?
  if echo "$out" | grep -qi "Unknown mode"; then
    echo "[FAIL] harness :: $label -- never dispatched"; fails=$((fails+1)); return; fi
  got=UNCLEAR
  [ "$want" = REFUSE ] && [ $code -ne 0 ] && echo "$out" | grep -q "$needle" && got=REFUSE
  [ "$want" = PASS   ] && [ $code -eq 0 ] && echo "$out" | grep -q "$needle" && got=PASS
  # The counter must NOT be incremented inside $( ): that is a subshell and the increment is lost, which is how this suite reported "0 failing" under a [FAIL] line until 2026-09-24.
  if [ "$got" = "$want" ]; then v="  OK "; else v="FAIL"; fails=$((fails+1)); fi
  echo "[$v] want=$want got=$got :: $label"
  echo "$out" | grep -E "FATAL|warn \]|\+slot|\+hook|LINT" | sed 's/^/          /'
  echo
}

B1='[{"at":"RETravelA1LocRef","objective":"o1","journal":"j1"}]'
B2='[{"at":"RETravelA1LocRef","objective":"o1","journal":"j1"},{"at":"RECenterLocRef","objective":"o2"}]'
B3='[{"at":"RETravelA1LocRef","objective":"o1","journal":"j1"},{"at":"REMarkerMediumLocRef","journal":"mid"},{"at":"RECenterLocRef","objective":"o2"}]'
B3OBJ='[{"at":"RETravelA1LocRef","objective":"o1","journal":"j1"},{"at":"REMarkerMediumLocRef","objective":"nope","journal":"mid"},{"at":"RECenterLocRef","objective":"o2"}]'
B3NOJ='[{"at":"RETravelA1LocRef","objective":"o1","journal":"j1"},{"at":"REMarkerMediumLocRef"},{"at":"RECenterLocRef","objective":"o2"}]'
B9='[{"at":"RETravelA1LocRef","objective":"o1","journal":"j1"},{"at":"RETravelA2LocRef","journal":"m"},{"at":"RETravelA3LocRef","journal":"m"},{"at":"RETravelB1LocRef","journal":"m"},{"at":"RETravelB2LocRef","journal":"m"},{"at":"RETravelB3LocRef","journal":"m"},{"at":"REContainerLocRef","journal":"m"},{"at":"RECorpseLocRef","journal":"m"},{"at":"REMarkerSmallLocRef","journal":"m"},{"at":"RECenterLocRef","objective":"o2"}]'

mk one "$B1";     run REFUSE "needs at least 2"                    "one beat is too few"            one
mk two "$B2";     run PASS   "LINT PASSES"                         "two beats, the driver's own"    two
mk three "$B3";   run PASS   "1 extra beat(s) will be CREATED"     "three beats, one created"       three
mk objx "$B3OBJ"; run REFUSE "is an EXTRA beat and carries an objective" "objective on an extra"    objx
mk noj "$B3NOJ";  run REFUSE "no journal line"                     "extra beat with nothing to say" noj
mk nine "$B9";    run REFUSE "tops out at 8"                       "ten beats is past maxBeats"     nine

echo "================ $fails failing case(s) ================"
exit $fails
