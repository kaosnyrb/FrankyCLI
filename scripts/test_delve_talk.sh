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
case('token',    lambda x: x['beats'][0]['exchanges'][1].__setitem__('reply', ['Out at the <Place> for eleven years.']))
case('long',     lambda x: x['beats'][0]['exchanges'][2].__setitem__('prompt', 'x' * 61))
case('box',      lambda x: x['beats'][0].__setitem__('message', {'title': 'T', 'text': 'x'}))
case('noask',    lambda x: x['beats'][0].__setitem__('exchanges', []))
case('stray',    lambda x: x['beats'][1].__setitem__('greeting', ['hello']))
# schema 2 (2026-10-19, his "Need to add multiple line support"): a spoken field is a list of responses, 1 to 4.
case('multi',    lambda x: x['beats'][0]['exchanges'][1].__setitem__('reply', ['Eleven years out here.', 'Long enough.', 'Too long, some days.']))
case('toomany',  lambda x: x['beats'][0]['exchanges'][1].__setitem__('reply', ['a.', 'b.', 'c.', 'd.', 'e.']))
case('nolines',  lambda x: x['beats'][0].__setitem__('greeting', []))
case('tokentwo', lambda x: x['beats'][0]['exchanges'][1].__setitem__('reply', ['Fine.', 'Out at the <Place> for years.']))
case('schema1',  lambda x: (x.__setitem__('schema', 1), x['beats'][0].__setitem__('greeting', 'An old string.')))
# exchange perks, a [Background] / [Skill] / [Trait] choice (his ask 2026-10-14: "make recipes support this")
case('perkok',   lambda x: x['beats'][0]['exchanges'][1].__setitem__('perk', 'BackgroundDiplomat'))
case('perkzero', lambda x: x['beats'][0]['exchanges'][0].__setitem__('perk', 'Skill_Medicine'))
case('perkbad',  lambda x: x['beats'][0]['exchanges'][1].__setitem__('perk', 'BackgroundNoSuchThing'))
case('perkodd',  lambda x: x['beats'][0]['exchanges'][1].__setitem__('perk', 'CREW_Ship_Shields'))
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
run PASS   multi    'LINT PASSES'
run REFUSE toomany  'exchanges[1].reply has 5 responses; the limit is 4'
run REFUSE nolines  'greeting is empty'
run REFUSE tokentwo 'exchanges[1].reply[1] carries a <Token>'
run REFUSE schema1  'is schema 1; this tool speaks 2'
run PASS   perkok   'LINT PASSES'
run REFUSE perkzero 'it is the choice that moves the quest'
run REFUSE perkbad  "is not a Perk in"
run PASS   perkodd  'is not a background, trait, skill or faction perk'
echo "failing: $fails"
[ $fails -eq 0 ]
