"""Grade every SPOKEN line in the Delve recipes against the faults he has caught by ear.

    python scripts/delves/voice_check.py                  # every recipe
    python scripts/delves/voice_check.py duo_MB50a_qst    # one recipe
    python scripts/delves/voice_check.py --rev HEAD~3     # the recipes as they were at a commit (the control)

WHY (his, 2026-10-27, after three rewordings in one morning): "Feels like we need a linter here". The words
are graded by ear at his glass, one Delve at a time, so a fault he catches on one is still sitting in five
others. This reads every voiced line at once.

WHAT IS SPOKEN. broadcast, recapSay, each beat's say, a talk's greeting, its exchanges' replies and its
person's hellos. Never the card, briefing, objectives or journals: those are text, a total conversion can
patch them, and they are not heard. (A craft.description is text too.)

THE CHECKS. Each one is a fault he caught, with the catch that earned it.
  PAY    a line that promises pay. His: "I know i'll be paid" (Seismic Sensors). Pay as the STORY is fine
         (a crew's missing wages, "The owners pay once"), so this matches PROMISES only, never the word.
  DASH   an em dash. His house rule, 2026-09-11.
  SHARD  a field split into takes that would fit in ONE (250 characters). ElevenLabs renders each take alone,
         with no context, so short takes come out flat. His, 2026-10-21 ("when you split the lines that small
         Elevenlabs has no context so the delivery is worse") and again 2026-10-27 ("we've sharded our lines too
         hard"). Split only past 250, or where the scene truly turns; a turn that fits is still ONE take.
  NOUN   a faction, a place or currency, in a Delve that is not NARROW. His 2026-10-12: a total conversion
         patches our text but cannot re-record our audio, so the voice names roles. A narrow Delve (one planet
         or faction) is exempt, his 2026-10-26; it says so with "NARROW" in a _note.

THE TICS, counted ACROSS the corpus, not per line (his, 2026-10-27: "make a note of the tics so we can watch out
for them later"). These are MINE: every Delve not written in a pen name is in my voice, and each habit is fine in
one Delve and a drone across sixty. Each has a cap on how many recipes may carry it; over the cap WARNS (the exit
code is unchanged, because the fix is a judgement about which ones earn it). Measured 2026-10-27 before the first
pass, in the 70 Delves without a pen name: "Good." as a beat 24, a closing "Thank you" 20, the board's "You're at
the site?" opener 16, "Is that you/them?" 6, "That's it. That's X" 4, "Tell me that's a X" 3.
Four more added 2026-10-29 after his "Sassy ending lines are becoming a tic", measured in recipes BEFORE that pass:
a check-in question opening a say 40, "X? Then Y." 27, an apologising hello 5, a broadcast's short last line 13. THE SASSY TAIL ITSELF IS NOT HERE: a wry last clause is meaning, not pattern (about
45 of 63 closing lines by eye, 2026-10-29), so it is read, not counted.

WHAT IT CANNOT SEE, stated rather than papered. A LONE line ("They may still be watching for it.", his catch
on the Culture Canister): each list item is its own take, so a fragment that only works after the line before
is delivered flat. Whether a line stands alone is meaning, not pattern; a short-line count fires on "Thank you.
Truly." and misses a long orphan. Read the voiced lines aloud, one per breath, before shipping.

Exit 1 if anything is flagged, so it can gate a commit.
"""
import argparse
import json
import re
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RECIPES = ROOT / "data" / "delves" / "recipes"

PAY = re.compile(
    r"\byou'?ll be paid\b|\byou will be paid\b|\bwe'?ll pay\b|\bwe will pay\b|\bthere'?s pay in it\b"
    r"|\bpaid on completion\b|\bpayment'?s sent\b|\bpayment on\b|\bthe job'?s paid\b|\bpay for your time\b"
    r"|\bpays well\b|\bget paid\b|\bgets? you paid\b|\bpayment'?s on its way\b|\bworth your while\b"
    r"|\bmake the trip worth\b|\bpaying you\b|\bwe'?ll make it worth\b",
    re.I)
NOUN = re.compile(
    r"\b(UC|United Colonies|Freestar|Collective|Crimson Fleet|Ecliptic|Va'?ruun|House Va'?ruun|Constellation"
    r"|Rangers?|Vanguard|SysDef|Ryujin|Neon|Akila|New Atlantis|Jemison|Cydonia|Paradiso|Volii|Cheyenne|Alpha Centauri"
    r"|credits?|creds|Spacers?)\b")
TICS = [  # (name, pattern, cap on recipes, flags[, where]): where, if given, is a regex on the field the line sits in
    ('"X? Good." / "Good." as a beat', r"\bGood\.", 6, 0),
    ('"Thank you" to close', r"\bthank you\b", 8, re.I),
    ('"You\'re at the site?" opener', r"^You're at the site\?", 2, 0),
    ('"Is that you/them?" opener', r"^(Is|Was) (that|it) (you|them|her|the)", 6, 0),
    ('"That\'s it. That\'s X."', r"^That's (it|them)\. That's", 1, 0),
    ('"Tell me that\'s a X"', r"Tell me that'?s", 1, 0),
    # 2026-10-29, after his "Sassy ending lines are becoming a tic" and the audit that followed it.
    ('a say opening on a check-in question ("You\'ve got it?")', r"^[^.!?]{1,40}\?", 15, 0, r"say|recapSay"),
    ('"X? Then Y." (a question, then its own answer)', r"\?\s+Then\b", 6, 0),
    ('a hello that apologises ("Sorry, ...")', r"^(Oh\. )?(Hello\. )?Sorry\b", 2, 0, r"hello"),
    ('a broadcast ending on a short line of its own', r"[.?!]\s+(\S+\s+){0,3}\S+[.?!]$", 6, 0, r"^broadcast$"),
]
DASH = "—"


def fields(d):
    """Every voiced FIELD (a list of takes), with where it sits; hellos are alternatives, so each is its own field."""
    out = []
    for k in ("broadcast", "recapSay"):
        if d.get(k):
            out.append((k, d[k]))
    for i, b in enumerate(d.get("beats") or [], 1):
        if b.get("say"):
            out.append((f"beat {i} say", b["say"]))
        if b.get("greeting"):
            out.append((f"beat {i} greeting", b["greeting"]))
        for j, e in enumerate(b.get("exchanges") or []):
            if e.get("reply"):
                out.append((f"beat {i} reply {j}", e["reply"]))
        for h in (b.get("person") or {}).get("hellos") or []:
            out.append((f"beat {i} hello", h))
    return out


def spoken(d):
    """Every voiced line in a recipe, with where it sits."""
    out = [("broadcast", s) for s in d.get("broadcast") or []]
    out += [("recapSay", s) for s in d.get("recapSay") or []]
    for i, b in enumerate(d.get("beats") or [], 1):
        out += [(f"beat {i} say", s) for s in b.get("say") or []]
        out += [(f"beat {i} greeting", s) for s in b.get("greeting") or []]
        for j, e in enumerate(b.get("exchanges") or []):
            out += [(f"beat {i} reply {j}", s) for s in e.get("reply") or []]
        for h in (b.get("person") or {}).get("hellos") or []:
            out += [(f"beat {i} hello", s) for s in h]
    return out


def narrow(d):
    notes = [v for k, v in d.items() if k.startswith("_")]
    flat = json.dumps(notes)
    return "NARROW" in flat


def load(rev, rid=None):
    if rev is None:
        files = sorted(RECIPES.glob("*.json"))
        if rid:
            files = [f for f in files if f.stem == rid]
        return [(f.stem, json.loads(f.read_text(encoding="utf-8-sig"))) for f in files]
    rel = "data/delves/recipes"
    names = subprocess.run(["git", "-C", str(ROOT), "ls-tree", "--name-only", rev, rel + "/"],
                           capture_output=True, text=True, check=True).stdout.split()
    out = []
    for n in names:
        if not n.endswith(".json") or (rid and Path(n).stem != rid):
            continue
        txt = subprocess.run(["git", "-C", str(ROOT), "show", f"{rev}:{n}"],
                             capture_output=True, text=True, encoding="utf-8", check=True).stdout
        out.append((Path(n).stem, json.loads(txt.lstrip("﻿"))))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id", nargs="?")
    ap.add_argument("--rev")
    a = ap.parse_args()
    hits = 0
    for rid, d in load(a.rev, a.id):
        is_narrow = narrow(d)
        for where, line in spoken(d):
            faults = []
            if PAY.search(line):
                faults.append("PAY")
            if DASH in line:
                faults.append("DASH")
            m = NOUN.search(line)
            if m and not is_narrow:
                faults.append(f"NOUN({m.group(0)})")
            for f in faults:
                hits += 1
                print(f"{rid:40} {f:14} {where:16} {line}")
        for where, takes in fields(d):
            if len(takes) > 1 and len(" ".join(takes)) <= 250:
                hits += 1
                print(f"{rid:40} {'SHARD':14} {where:16} " + " | ".join(takes))
    tic_hits = {t[0]: set() for t in TICS}
    for rid, d in load(a.rev, a.id):
        for where, line in spoken(d):
            for name, pat, cap, fl, *w in TICS:
                if w and w[0] and not re.search(w[0], where):
                    continue
                if re.search(pat, line, fl):
                    tic_hits[name].add(rid)
    print("\nTICS (recipes carrying each; over the cap warns, the exit code is unchanged):")
    for name, pat, cap, *_ in TICS:
        n = len(tic_hits[name])
        print(f"  {'WARN' if n > cap else 'ok  '} {n:3}/{cap:<3} {name}")
    print(f"\n{hits} flagged line(s). PAY, DASH, NOUN and SHARD are his catches; a LONE line is not checked (see the header).")
    return 1 if hits else 0


if __name__ == "__main__":
    sys.exit(main())
