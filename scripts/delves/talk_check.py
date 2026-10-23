"""Does anything set up each talk prompt? A sheet for a person to read, before he plays.

    python scripts/delves/talk_check.py                 # every recipe with a talk beat
    python scripts/delves/talk_check.py duo_delve11b    # one recipe
    python scripts/delves/talk_check.py --flagged       # only the menus with a flag
    python scripts/delves/talk_check.py --rev HEAD~3    # the recipes as they were at a commit (the control)

WHY (his plays, 2026-10-22 and 23): 19a "he never tells you the sattlie is bust", 11a "the growing line is
unprompted. It's a fine line just nothing sets it up", 11b the same about the drill. A prompt the player is
offered has to be about something the person has already said. gen_delve lint cannot see this: it is about
meaning, not structure. So this is a MANUAL step with a mechanical helper.

WHAT COUNTS AS HEARD. Only what is sure to have played before the menu opens:
  - the greeting (it always plays);
  - for a second or third talk, an earlier talk's CLOSING reply (exchange 0, the one that ends that talk),
    because a side reply may never have been chosen;
  - the names of items the player is carrying by then.
The briefing card and the radio are printed as context but do NOT count: he may never read the card, and
the radio can play out of earshot or before he is paying attention. A hello does not count either: a person
with two hellos plays one of them.
  ⚠ EXCHANGE 0 IS SHOWN LAST AND ENDS THE TALK, so a prompt can never be set up by exchange 0's own reply
(14a's lunch). The sheet says so when that is the only place a flagged word appears.

* OR ?. A * prompt names something the person says THEMSELVES, in a reply or a hello, but not before the
menu: the writer knew the subject and assumed the player did. That is the shape of all five he has caught
(19a's satellite, 11b's drill, 14a's lunch, city01a's "heavy"; 11a's growing is a ? because nothing of hers
says "grow", only "beans"). Read the * first.
WHAT A FLAG MEANS. A content word in the prompt that appears nowhere in what was heard. It is a CANDIDATE,
not a fault: "Buy you a drink when you're off?" is the player's own idea and is fine. Read every flagged
prompt with the HEARD block above it and ask one question: would a player who heard only this know what the
prompt is asking about? Exit code is always 0; the person reading is the gate.
"""
import argparse
import json
import re
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RECIPES = ROOT / "data" / "delves" / "recipes"
REL = "data/delves/recipes"

# Words that carry no subject: a prompt made only of these is never flagged.
STOP = set("""
a an the and or but if so of to in on at by for with from up down out off over into onto about as is are was were be been
being am do does did done have has had having i you he she it we they me him her us them my your his its our their mine
yours this that these those there here what which who whom whose why how when where will would can could should shall may
might must not no yes just only very really then than too also any some all every each much many more most other another
own same such get got go going gone come came take took make made let know think tell told say said see look want need like
give gave find found keep kept back again still ever never one two three thing things anything something nothing everything
anyone someone everyone nobody way well good right okay ok oh hey sorry thanks thank please lot bit
else now long yourself myself himself herself happen happened mean wait sure fine best better worse
i'll i'm i've i'd you're you've you'll you'd he's she's it's we're we'll they're they've don't doesn't didn't isn't aren't
wasn't won't can't couldn't wouldn't shouldn't that's what's who's there's here's let's
""".split())


def stem(w):
    for suf in ("iest", "ier", "ies", "ing", "es", "ed", "er", "s", "e", "y"):
        if w.endswith(suf) and len(w) - len(suf) >= 3:
            return w[: -len(suf)]
    return w


def words(text):
    """Content words, unstemmed (for printing)."""
    out = set()
    for w in re.findall(r"[a-z][a-z']*", text.lower()):
        w = w.strip("'")
        if w.endswith("'s"):
            w = w[:-2]
        if w and w not in STOP:
            out.add(w)
    return out


def stemset(text):
    return {stem(w) for w in words(text)}


def flat(v):
    if v is None:
        return ""
    if isinstance(v, str):
        return v
    if isinstance(v, list):
        return " ".join(flat(x) for x in v)
    return ""


def load(rev):
    if rev is None:
        return {p.stem: json.loads(p.read_text(encoding="utf-8-sig")) for p in sorted(RECIPES.glob("duo_delve*.json"))}
    names = subprocess.run(["git", "ls-tree", "--name-only", rev, REL + "/"], cwd=ROOT, capture_output=True,
                           text=True, check=True).stdout.split()
    out = {}
    for n in names:
        if not n.endswith(".json") or "/duo_delve" not in n:
            continue
        raw = subprocess.run(["git", "show", f"{rev}:{n}"], cwd=ROOT, capture_output=True, check=True).stdout
        out[Path(n).stem] = json.loads(raw.decode("utf-8-sig"))
    return out


STARS = [0]


def sheet(rid, d):
    """Yield (beat number, lines, flag count) for each talk beat."""
    heard_before = []          # closing replies of earlier talks
    stars = STARS
    carried = []               # item names picked up or recovered so far
    for i, b in enumerate(d.get("beats", [])):
        if b.get("type") == "talk":
            ex = b.get("exchanges", [])
            heard = flat(b.get("greeting")) + " " + " ".join(heard_before) + " " + " ".join(carried)
            hw = stemset(heard)
            closing = stemset(flat(ex[0]["reply"])) if ex else set()
            own = stemset(" ".join(flat(e["reply"]) for e in ex) + " " + flat(b.get("person", {}).get("hellos")))
            lines = [f"== {rid}  beat {i + 1} (talk, {b.get('place', 'main')} place)"]
            lines.append(f"   card   (context only): {d.get('prose', {}).get('briefing', '')}")
            if d.get("broadcast"):
                lines.append(f"   radio  (context only): {flat(d['broadcast'])}")
            if heard_before:
                lines.append(f"   HEARD  earlier closing reply: {' / '.join(heard_before)}")
            if carried:
                lines.append(f"   HEARD  carrying: {', '.join(carried)}")
            lines.append(f"   HEARD  greeting: {flat(b.get('greeting'))}")
            flags = 0
            for j, e in enumerate(ex):
                pw = stemset(e["prompt"])
                missing = sorted(w for w in words(e["prompt"]) if not (stemset(w) & hw))
                tag = "  (closes)" if j == 0 else ""
                if missing and j > 0:
                    flags += 1
                    only0 = [w for w in missing if stemset(w) & closing]
                    star = [w for w in missing if stemset(w) & own]
                    stars[0] += bool(star)
                    note = f"   <- not heard: {', '.join(missing)}"
                    if star:
                        note += f"; * THEY SAY IT, just not before the menu: {', '.join(star)}"
                    if only0:
                        note += f"; ONLY in exchange 0's reply, which ends the talk: {', '.join(only0)}"
                    lines.append(f"   {'*' if star else '?'}  > {e['prompt']}{tag}{note}")
                else:
                    lines.append(f"      > {e['prompt']}{tag}")
            if ex:
                heard_before.append(flat(ex[0]["reply"]))
            yield i + 1, lines, flags
        if b.get("item") and b.get("type") in ("pickup", "recover"):
            carried.append(b["item"])


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("ids", nargs="*")
    ap.add_argument("--flagged", action="store_true", help="print only menus with at least one flag")
    ap.add_argument("--rev", help="read the recipes from this git commit instead of the working tree")
    a = ap.parse_args()
    recipes = load(a.rev)
    ids = a.ids or [k for k in recipes if "delvetest" not in k]
    menus = total = 0
    for rid in ids:
        if rid not in recipes:
            raise SystemExit(f"no recipe {rid}")
        for _, lines, flags in sheet(rid, recipes[rid]):
            menus += 1
            total += flags
            if a.flagged and not flags:
                continue
            print("\n".join(lines))
            print()
    print(f"{menus} talk menu(s), {total} flagged prompt(s), {STARS[0]} of them *. Read the * first: the person "
          "names the thing in their own lines but not before the menu, which is how every fault he has caught looked. "
          "A ? is the player's own idea as often as not. Read each against HEARD, then decide.")


if __name__ == "__main__":
    main()
