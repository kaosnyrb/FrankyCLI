#!/usr/bin/env bash
# Bite suite for a place on the boss marker, the talk that ENDS a Delve, and a pay tier on a single ending (built 2026-10-13
# for duo_delve11a, his "11a").
#
# Lint only: nothing is written to du_overtime.esm. Each case is duo_delve11a with ONE field changed, in its own
# folder so the file name still matches the id. A verdict needs the lint's OWN words, never a bare exit code.
# Same harness as test_delve_talk.sh. The build half (unused base slots removed, the bag taken on hand-back, the
# tier on stage 100) is graded off disk by the build's own verification block, not here.
cd /c/Git/FrankyCLI || exit 1
TMP="${TMPDIR:-/tmp}/delvedungeon"
rm -rf "$TMP"; mkdir -p "$TMP"
fails=0

python - "$TMP" <<'EOF' || exit 1
import json, sys, copy, os
T = sys.argv[1]
d = json.load(open('data/delves/recipes/duo_delve11a.json', encoding='utf-8'))
def case(name, f):
    x = copy.deepcopy(d); f(x)
    os.makedirs(f"{T}/{name}", exist_ok=True)
    json.dump(x, open(f"{T}/{name}/duo_delve11a.json", 'w', encoding='utf-8'), indent=1)
case('clean',    lambda x: None)
# A marker no location carries must be a ZERO pool: the control that the census reads markers at all.
case('nomarker', lambda x: x['beats'][0].__setitem__('at', 'LocDungeonBossContainerLocRefXX'))
case('midpay',   lambda x: x['beats'][0].__setitem__('reward', 'hard'))
case('badtier',  lambda x: x['beats'][1].__setitem__('reward', 'epic'))
case('tier',     lambda x: x['beats'][1].__setitem__('reward', 'easy'))
EOF

run() { # run WANT case needle
  out=$(timeout 900 ./bin/Debug/net8.0/FrankyCLI.exe gen_delve lint "$TMP/$2/duo_delve11a.json" 2>&1); code=$?
  got=UNCLEAR
  [ "$1" = REFUSE ] && [ $code -ne 0 ] && echo "$out" | grep -qF "$3" && got=REFUSE
  [ "$1" = PASS ]   && [ $code -eq 0 ] && echo "$out" | grep -qF "$3" && got=PASS
  if [ "$got" = "$1" ]; then echo "[ok  ] $2 :: $1"; else echo "[FAIL] $2 :: wanted $1, code $code"; echo "$out" | tail -4; fails=$((fails+1)); fi
}
run PASS   clean    'plus every location carrying LocDungeonBossLocRef'
run REFUSE nomarker 'LocDungeonBossContainerLocRefXX'
run REFUSE midpay   'sets reward and is not an ending'
run REFUSE badtier  'no global duo_reward_creds_epic'
run PASS   tier     'LINT PASSES'
echo "failing: $fails"
[ $fails -eq 0 ]
