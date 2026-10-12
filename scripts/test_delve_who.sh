#!/usr/bin/env bash
# Bite suite for a place's "who" (the who's-there dial, his 2026-10-11; built 2026-10-12 for duo_delve06b).
#
# Lint only: nothing is written to du_overtime.esm. Each case is duo_delve06b with ONE field broken, in its
# own folder so the file name still matches the id (a mismatched name is refused before the lint runs, and
# that refusal exits 1 too: the first run of these bites read four of them as passes until the needle check
# said otherwise). A REFUSE verdict needs the lint's OWN words, never a bare exit code.
cd /c/Git/FrankyCLI || exit 1
TMP="${TMPDIR:-/tmp}/delvewho"
rm -rf "$TMP"; mkdir -p "$TMP"
fails=0

python - "$TMP" <<'EOF' || exit 1
import json, sys, copy, os
T = sys.argv[1]
d = json.load(open('data/delves/recipes/duo_delve06b.json', encoding='utf-8'))
def case(name, f):
    x = copy.deepcopy(d); f(x)
    os.makedirs(f"{T}/{name}", exist_ok=True)
    json.dump(x, open(f"{T}/{name}/duo_delve06b.json", 'w', encoding='utf-8'), indent=1)
case('clean',  lambda x: None)
case('kind',   lambda x: x['place']['second']['who'].__setitem__('kind', 'spacers'))
case('list',   lambda x: x['place']['second']['who'].__setitem__('list', 'duo_NoSuchList'))
case('anchor', lambda x: x['place'].__setitem__('who', {'kind': 'infested', 'list': 'duo_GangMembersList_PreditorMegalist_A', 'min': 2, 'max': 3}))
case('count',  lambda x: x['place']['second']['who'].__setitem__('min', 0))
# offer.say (2026-10-12) rides the same recipe: a box AND a line on the offer is his "either or".
case('offerboth', lambda x: x['offer'].__setitem__('message', {'title': 'Incoming Message', 'text': 'x'}))
EOF

run() { # run WANT case needle
  out=$(timeout 900 ./bin/Debug/net8.0/FrankyCLI.exe gen_delve lint "$TMP/$2/duo_delve06b.json" 2>&1); code=$?
  got=UNCLEAR
  [ "$1" = REFUSE ] && [ $code -ne 0 ] && echo "$out" | grep -qF "$3" && got=REFUSE
  [ "$1" = PASS ]   && [ $code -eq 0 ] && echo "$out" | grep -qF "$3" && got=PASS
  if [ "$got" = "$1" ]; then echo "[ok  ] $2 :: $1"; else echo "[FAIL] $2 :: wanted $1, code $code"; echo "$out" | tail -4; fails=$((fails+1)); fi
}
run PASS   clean  'place.second.who: infested'
run REFUSE kind   'only "infested" is built'
run REFUSE list   'is not a FormList'
run REFUSE anchor "already hooks that object's distance"
run REFUSE count  'needs 1 <= min <= max'
run REFUSE offerboth 'offer has both a message and a say'
echo "failing: $fails"
[ $fails -eq 0 ]
