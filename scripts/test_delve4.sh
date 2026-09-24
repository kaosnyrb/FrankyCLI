#!/usr/bin/env bash
# Bite suite for gen_delve's delve4 template kind (carry, absence, recover, return).
#
# Lint only: nothing is written to du_overtime.esm. Same rule as test_delve_extrabeats.sh: a REFUSE
# verdict requires the lint's OWN words, never a bare exit code, and there is a PASS control so a
# lint that refused everything could not read as a working one.
#
# Every case is the real recipe with ONE field broken, so a refusal is attributable to that field.
cd /c/Git/FrankyCLI || exit 1
TMP="${TMPDIR:-/tmp}/delve4bite"
mkdir -p "$TMP"
REAL="data/delves/recipes/duo_delve03.json"
fails=0

mut() {  # mut <name> <python expression over r>
  python - "$REAL" "$TMP/$1.json" "$2" <<'EOF'
import json, sys
r = json.load(open(sys.argv[1], encoding="utf-8"))
exec(sys.argv[3])
json.dump(r, open(sys.argv[2], "w", encoding="utf-8"))
EOF
}

run() { # run WANT needle label file
  want="$1"; needle="$2"; label="$3"; f="$4"
  out=$(timeout 900 dotnet run -- gen_delve lint "$f" 2>&1); code=$?
  got=UNCLEAR
  [ "$want" = REFUSE ] && [ $code -ne 0 ] && echo "$out" | grep -qF "$needle" && got=REFUSE
  [ "$want" = PASS   ] && [ $code -eq 0 ] && echo "$out" | grep -qF "$needle" && got=PASS
  v=$([ "$got" = "$want" ] && echo "  OK " || { fails=$((fails+1)); echo "FAIL"; })
  echo "[$v] want=$want got=$got :: $label"
  echo "$out" | grep -E "FATAL|LINT" | sed 's/^/          /'
  echo
}

run PASS "LINT PASSES" "the real recipe (control)" "$REAL"

mut noreturn 'r["beats"][3]["at"] = "RETravelA1LocRef"'
run REFUSE "is a RETURN to beat 2's place" "beat 4 does not go back to the centre" "$TMP/noreturn.json"

mut noitems 'del r["items"]'
run REFUSE "items.load and items.missing are both required" "no item names" "$TMP/noitems.json"

mut token 'r["items"]["missing"] = "Half of the <Place>"'
run REFUSE "an item name carries a <Token>" "a token in an item name" "$TMP/token.json"

mut three 'del r["beats"][2]'
run REFUSE "is exactly 4 beats" "three beats on a four-beat template" "$TMP/three.json"

mut noobj 'r["beats"][2]["objective"] = None'
run REFUSE "has no objective text" "beat 3 with no objective" "$TMP/noobj.json"

# --- the second place (2026-09-24, his "the box should have been at a different POI") ---------------
REAL="data/delves/recipes/duo_delve04.json"
run PASS "LINT PASSES" "duo_delve04, the load at a second POI (control)" "$REAL"

mut beat2second 'r["beats"][1]["place"] = "second"'
run REFUSE "only beat 1 may be at the second place" "beat 2 at the second place" "$TMP/beat2second.json"

mut badplace 'r["beats"][0]["place"] = "elsewhere"'
run REFUSE "the places are" "a misspelt place name" "$TMP/badplace.json"

# --- message boxes and item names (2026-09-24 pm, Jessica's message boxes) -----------------------------
mut nomsgtext 'r["beats"][1]["message"]["text"] = ""'
run REFUSE "has a message with no text" "beat 2 message with empty text" "$TMP/nomsgtext.json"

mut clash 'r["items"]["load"] = "Terran Reclaimer"'
run REFUSE "is already the name of" "an item named exactly like a vanilla record" "$TMP/clash.json"

echo "================ $fails failing case(s) ================"
exit $fails
