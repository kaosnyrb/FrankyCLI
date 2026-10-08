#!/usr/bin/env bash
# Bite suite for gen_aliaslint, over the LIVE load order (read only; nothing is written).
#
# ONE control, graded on the tool's OWN words, never a bare exit code:
#   POSITIVE: no duo_ quest may come back CANNOT START. Shipped, working missions are the control
#   that stops a linter that refuses everything from reading as a working one.
#
# ⛔ RETIRED 2026-10-08, his ruling, and this suite is WEAKER for it, said so rather than papered:
# the two NEGATIVE controls lived in test quests that came out of du_overtime before a release.
#   R3: duo_delvetest_fail (an LCRT minted so that no Location has it) -> must be CANNOT on R3.
#   R1: duo_delve04t (the unfixed alias-order twin of duo_delve04, a REAL previous version)
#       -> must be CANNOT on R1.
# With both gone this suite cannot show the linter is able to say CANNOT at all. Both records are
# in the snapshot esmbackups/08_10_26_11_22 ("pre-delve-cleanup") in C:\modding\DU_Overtime, and
# the controls are in this file's history (git log -p scripts/test_aliaslint.sh). The choice he
# did not take was a separate never-shipped test plugin holding both; that is the way back.
cd /c/Git/FrankyCLI || exit 1
fails=0

out=$(timeout 900 dotnet run -- gen_aliaslint duo_ 2>&1)
echo "$out" | grep -q "SUMMARY over" || { echo "FAIL: no SUMMARY line, the tool did not run"; exit 1; }

others=$(echo "$out" | grep -E "^  CANNOT START" | awk '{print $3}')
if [ -z "$others" ]; then
  echo "[ OK ] no duo_ quest is CANNOT ($(echo "$out" | grep SUMMARY | sed 's/^ *//'))"
else
  echo "[FAIL] unexpected CANNOT START:"; echo "$others" | sed 's/^/         /'; fails=$((fails+1))
fi

echo
[ $fails -eq 0 ] && echo "ALL CONTROLS HELD" || echo "$fails CONTROL(S) FAILED"
exit $fails
