"""Grade the TEXT a Delve shows the player (title, board card, objectives, journal) against vanilla's own style.

    python scripts/delves/text_check.py                  # every recipe
    python scripts/delves/text_check.py duo_MB50a_qst    # one recipe
    python scripts/delves/text_check.py --rev HEAD~3     # the recipes as they were at a commit (the control)

voice_check.py's sibling: that one grades what is HEARD, this one what is READ. His, 2026-10-29: "can we lint/rule
these so they dont creep back in?", after the style pass that measured our Delves against Bethesda manual 33
(home-office office/projects/bethesda/33-vanilla-quest-text-style.md, 1,624 vanilla quests) and fixed them.

PER-LINE FAULTS (each fails the run):
  TYPE     the title's word before the colon is not on the closed list below. His board vocabulary
           (home-office office/projects/overtime/board-names.md) plus the Delve words he has kept. A new type
           is a ruling of his, added here in the same commit, never a word a recipe invents.
  PAYER    a board card does not open on who is paying ("Local Contract."), as all 51 vanilla postings do.
  CLOSER   a board card does not end on a stock closer ("Payment on contract completion.").
  CARD     a board card body is over 25 words. Vanilla's median is 9 and its longest 47; ours were 26 before.
  OBJ      an objective that does not open on a verb, ends in punctuation, or runs past 12 words (vanilla's max).
  NEXT     a mid-quest journal entry that never says what to do next ("I need to", "I have to", "I should").
           Vanilla's shape is what just happened, then what next, in 46%. Exempt: a grouped beat (any order,
           so it may not say first or last), any ending (the last beat, or a choose ending), and a beat whose
           next step is said by the objective of a talk that follows in the same place.
  DASH     an em dash in any of it (his 2026-09-11 ruling; vanilla has none either).
  PEOPLE   the words put allies beside the player (a count of them, "four of us", "you make five", "held the
           middle with four settlers", "stand with us") and nobody friendly is placed: no `who` on a place, no talk person's `company`, no `support` team. His, 2026-10-29:
           "do we have a check on when we mention people who should be present?", on 43b's sergeant counting four
           shooters at a site where the build places nobody. Every field the player reads OR hears, because the
           claim is about the world, not the wording. ⚠ `who` has no armed friendly kind (workers are civilians),
           so a count of shooters is never true today: the fix is the words.
  NAME     two recipes place a person with the same name. The subtitle shows it, so a replaying player meets one
           person in two jobs that disagree about her (14a and 16a were both "Sergeant Okafor", 2026-10-29).
  SPEAKER  two letter variants of one family share a speaker name. The subtitles show it (his, 2026-10-29), so
           the same name tells a replaying player it is the same person, whatever the lines do. Frances Hale's
           forced pick on the raiders, 43a and 43e, both "Settler".

CORPUS RATES (WARN only, the exit code is unchanged, because the fix is a judgement):
  journal entries in first person, floor 75% (vanilla 87%; ours 57% before the pass, 79% after).

WHAT IT CANNOT SEE: whether a journal's next step is the RIGHT next step, or whether a card is dull. Read them.
Exit 1 if anything is flagged, so it can gate a commit.
"""
import argparse
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from voice_check import load  # noqa: E402  same loader, same --rev behaviour

TYPES = {"Recovery", "Repairs", "Hauling", "Infestation", "Survey", "Strike", "Sabotage", "Bounty", "Fabrication",
         "Defense", "Hunt", "Rescue", "Dismantle", "Danger", "Requisition"}
PAYER = re.compile(r"^(Local|Private|Insurance|Administrative|[A-Z][\w']+(?: [A-Z][\w']+)*) Contract\.")
CLOSERS = ("Payment on contract completion.", "Payment on delivery.", "Payment on completion.")
VERB = re.compile(r"^(\(Optional\) )?[A-Z][a-z]+")
NEXT = re.compile(r"\b(I need to|I have to|I should|I'll need to|I've got to)\b")
FIRST = re.compile(r"\b(I|I've|I'm|I'd|I'll|me|my)\b")
NOT_VERBS = {"The", "A", "An", "One", "This", "That", "Their", "Her", "His"}
DASH = "—"
NUM = r"(?:two|three|four|five|six|seven|eight|\d+)"
PEOPLE = re.compile(
    rf"\b{NUM} of us (?:can|could|will|are|left|holding|here)\b|\byou make {NUM}\b|\b{NUM} (?:people|settlers|soldiers|of them) who can (?:shoot|fight)\b"
    rf"|\bwith {NUM} (?:settlers|soldiers|people|others)\b"
    r"|\b(?:held|hold|holding|fought|stand|standing|stood)\b[^.]{0,40}?\bwith (?:us|them|the (?:settlers|soldiers|people|others))\b",
    re.I)


def spoken_and_read(d):
    """(field, text) for everything the player reads or hears; maker notes (_keys) and record fields skipped."""
    skip = {"at", "model", "target", "list", "template", "outfit", "voice", "elevenlabs", "type", "place", "item",
            "name", "style", "size", "species", "deal", "id", "author", "source", "schema", "theme", "choose", "group"}

    def walk(o, key):
        if isinstance(o, str):
            if key not in skip:
                yield key, o
        elif isinstance(o, list):
            for x in o:
                yield from walk(x, key)
        elif isinstance(o, dict):
            for k, v in o.items():
                if not k.startswith("_"):
                    yield from walk(v, k)
    for k in ("prose", "broadcast", "beats", "recap", "recapSay", "offer", "deadline"):
        if k in d:
            yield from walk(d[k], k)


def words(t):
    return len(re.findall(r"[A-Za-z0-9<>'=]+", t))


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("id", nargs="?")
    ap.add_argument("--rev")
    a = ap.parse_args()
    hits = 0
    journals = first = 0

    def flag(rid, kind, where, text):
        nonlocal hits
        hits += 1
        print(f"{rid:40} {kind:7} {where:10} {text}")

    for rid, d in load(a.rev, a.id):
        if "delvetest" in rid:
            continue
        prose = d.get("prose") or {}
        name = prose.get("name", "")
        if ":" in name and name.split(":", 1)[0] not in TYPES:
            flag(rid, "TYPE", "name", name)
        card = prose.get("card")
        if card:
            if not PAYER.match(card):
                flag(rid, "PAYER", "card", card)
            if not card.rstrip().endswith(CLOSERS):
                flag(rid, "CLOSER", "card", card)
            body = card.split(".", 1)[1] if "." in card else card
            if words(body) > 25:
                flag(rid, "CARD", "card", f"{words(body)} words: {card}")
        place = d.get("place") or {}
        placed = bool(place.get("who")) or any(isinstance(place.get(k), dict) and place[k].get("who")
                                                for k in ("second", "third"))
        placed = placed or any((b.get("person") or {}).get("company") or b.get("support") for b in d.get("beats") or [])
        if not placed:
            for field, t in spoken_and_read(d):
                m = PEOPLE.search(t)
                if m:
                    flag(rid, "PEOPLE", field, f"\"{m.group(0)}\" but nobody is placed: {t}")
        beats = d.get("beats") or []
        for i, b in enumerate(beats):
            o = b.get("objective")
            if o:
                if not VERB.match(o) or o.split()[0] in NOT_VERBS:
                    flag(rid, "OBJ", f"beat {i+1}", f"no verb first: {o}")
                if o.rstrip()[-1] in ".!?":
                    flag(rid, "OBJ", f"beat {i+1}", f"ends in punctuation: {o}")
                if words(o) > 12:
                    flag(rid, "OBJ", f"beat {i+1}", f"{words(o)} words: {o}")
            j = b.get("journal")
            if j:
                journals += 1
                first += bool(FIRST.search(j))
                last = i == len(beats) - 1
                ending = last or b.get("choose") or b.get("ending")
                grouped = b.get("group")
                talk_next = i + 1 < len(beats) and beats[i + 1].get("type") == "talk"
                if not (ending or grouped or talk_next) and not NEXT.search(j):
                    flag(rid, "NEXT", f"beat {i+1}", j)
            for field in (name, card or "", o or "", j or ""):
                if DASH in field:
                    flag(rid, "DASH", f"beat {i+1}", field)
                    break
    families = {}
    for rid, d in load(a.rev):
        m = re.match(r"^(.*?)([a-z])(_qst)?$", rid)
        if not m or "delvetest" in rid:
            continue
        name = (d.get("speaker") or {}).get("name")
        if name:
            families.setdefault(m.group(1) + (m.group(3) or ""), {}).setdefault(name, []).append(rid)
    for fam, names in families.items():
        if a.id and not a.id.startswith(fam.replace("_qst", "")):
            continue
        for name, ids in names.items():
            if len(ids) > 1:
                flag(", ".join(sorted(ids)), "SPEAKER", "speaker", f"letters share \"{name}\"")
    people = {}
    for rid, d in load(a.rev):
        if "delvetest" in rid:
            continue
        for b in d.get("beats") or []:
            n = (b.get("person") or {}).get("name")
            if n:
                people.setdefault(n, set()).add(rid)
    for n, ids in people.items():
        if len(ids) > 1 and (not a.id or a.id in ids):
            flag(", ".join(sorted(ids)), "NAME", "person", f"two recipes place \"{n}\"")
    if journals:
        pct = 100 * first / journals
        print(f"\nRATES: journal first person {pct:.0f}% {'WARN, floor 75%' if pct < 75 else 'ok (floor 75%, vanilla 87%)'}")
    print(f"\n{hits} flagged. TYPE, PAYER, CLOSER, CARD, OBJ, NEXT, DASH, PEOPLE, NAME and SPEAKER (see the header).")
    return 1 if hits else 0


if __name__ == "__main__":
    sys.exit(main())
