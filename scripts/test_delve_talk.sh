#!/usr/bin/env bash
# Bite suite for the TALK beat (built 2026-10-12 for duo_delve14a, his "lets try the talk beat next").
#
# Lint only: nothing is written to du_overtime.esm. Each case is duo_delve14a with ONE field broken, in its own
# folder so the file name still matches the id (a mismatched name is refused before the lint runs, and exits 1
# too). A REFUSE verdict needs the lint's OWN words, never a bare exit code. Same harness as test_delve_who.sh.
cd /c/Git/FrankyCLI || exit 1
TMP="${TMPDIR:-/tmp}/delvetalk"
rm -rf "$TMP"; mkdir -p "$TMP"
fails=0

python - "$TMP" <<'EOF' || exit 1
import json, sys, copy, os
T = sys.argv[1]
d = json.load(open('data/delves/recipes/duo_delve14a.json', encoding='utf-8'))
def case(name, f):
    x = copy.deepcopy(d); f(x)
    os.makedirs(f"{T}/{name}", exist_ok=True)
    json.dump(x, open(f"{T}/{name}/duo_delve14a.json", 'w', encoding='utf-8'), indent=1)
case('clean',    lambda x: None)
case('noperson', lambda x: x['beats'][0].pop('person'))
case('token',    lambda x: x['beats'][0]['exchanges'][1].__setitem__('reply', 'Out at the <Place> for eleven years.'))
case('long',     lambda x: x['beats'][0]['exchanges'][2].__setitem__('prompt', 'x' * 61))
case('box',      lambda x: x['beats'][0].__setitem__('message', {'title': 'T', 'text': 'x'}))
case('noask',    lambda x: x['beats'][0].__setitem__('exchanges', []))
case('stray',    lambda x: x['beats'][1].__setitem__('greeting', 'hello'))
EOF

run() { # run WANT case needle
  out=$(timeout 900 ./bin/Debug/net8.0/FrankyCLI.exe gen_delve lint "$TMP/$2/duo_delve14a.json" 2>&1); code=$?
  got=UNCLEAR
  [ "$1" = REFUSE ] && [ $code -ne 0 ] && echo "$out" | grep -qF "$3" && got=REFUSE
  [ "$1" = PASS ]   && [ $code -eq 0 ] && echo "$out" | grep -qF "$3" && got=PASS
  if [ "$got" = "$1" ]; then echo "[ok  ] $2 :: $1"; else echo "[FAIL] $2 :: wanted $1, code $code"; echo "$out" | tail -4; fails=$((fails+1)); fi
}
run PASS   clean    'LINT PASSES'
run REFUSE noperson 'has no person: nobody to talk to'
run REFUSE token    'carries a <Token>; a voice cannot say one'
run REFUSE long     'characters; the limit is 60'
run REFUSE box      'a talk beat has none of them'
run REFUSE noask    'has no exchanges'
run REFUSE stray    'only a talk beat has a conversation'
echo "failing: $fails"
[ $fails -eq 0 ]
