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
    r"|credits?|creds)\b")
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
    print(f"\n{hits} flagged line(s). PAY, DASH, NOUN and SHARD are his catches; a LONE line is not checked (see the header).")
    return 1 if hits else 0


if __name__ == "__main__":
    sys.exit(main())
