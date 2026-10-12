"""Print a Delve recipe as something a person reads on a phone.

    python scripts/delves/read.py duo_delve16a            # markdown to stdout
    python scripts/delves/read.py duo_delve16a --notes    # with the author's _notes
    python scripts/delves/read.py --all --write           # data/delves/read/<id>.md for every recipe

It shows what the player meets, in play order: the name, the briefing, the radio, then each beat
with its words. Engine detail (alias names, keyword lists, voice ids) is cut to one plain line or
dropped. No tables, because a table on a phone is a sideways scroll.

The written files are GENERATED: regenerate, never edit. The recipe JSON is the source.
"""
import argparse
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RECIPES = ROOT / "data" / "delves" / "recipes"
OUT = ROOT / "data" / "delves" / "read"

WHERE = {
    "RECenterLocRef": "the centre of the site",
    "RETravelA1LocRef": "out at the edge of the site",
    "RETravelB1LocRef": "out at the far edge of the site",
    "LocDungeonBossLocRef": "at the boss",
}

THEMES = {
    "LocTypeOE_NonHostile": "a quiet, non-hostile site",
    "LocTypeOE_ThemeNaturalKeyword": "natural landmarks",
    "LocTypeOE_ThemeCaveKeyword": "caves",
}


def theme_line(theme):
    req = [THEMES.get(k, k) for k in theme.get("require", [])]
    exc = [THEMES.get(k, k) for k in theme.get("exclude", [])]
    parts = []
    if req:
        parts.append(" and ".join(req))
    else:
        parts.append("anywhere")
    if exc:
        parts.append("never " + " or ".join(exc))
    return ", ".join(parts)


def model_name(path):
    return path.replace("\\", "/").rsplit("/", 1)[-1] if path else None


def render(recipe, notes):
    out = []
    say = out.append
    prose = recipe.get("prose", {})
    say(f"# {prose.get('name', recipe['id'])}")
    say("")
    say(f"*{recipe['id']} · by {recipe.get('author', '?')}*")
    say("")
    if prose.get("briefing"):
        say(f"**Briefing:** {prose['briefing']}")
        say("")

    place = recipe.get("place", {})
    if place:
        kind = "the dungeon pool" if place.get("kind") == "dungeon" else theme_line(place.get("theme", {}))
        say(f"**Where:** {kind}.")
        if "second" in place:
            say(f"**Then:** a second place, {theme_line(place['second'].get('theme', {}))}.")
        say("")

    speaker = recipe.get("speaker", {})
    if recipe.get("broadcast"):
        who = speaker.get("name", "the radio")
        say(f"📻 **{who}, on the radio:**")
        say(f"> {recipe['broadcast']}")
        say("")

    for n, beat in enumerate(recipe.get("beats", []), 1):
        kind = beat.get("type", "?")
        at = WHERE.get(beat.get("at"), beat.get("at", ""))
        if beat.get("place") == "second":
            at += ", at the second place"
        say("---")
        say("")
        say(f"## {n}. {kind.upper()} · {at}")
        say("")
        if beat.get("objective"):
            say(f"🎯 {beat['objective']}")
            say("")
        if beat.get("item"):
            say(f"📦 Item: **{beat['item']}**")
        obj = [x for x in (beat.get("name"), model_name(beat.get("model"))) if x]
        if obj:
            say(f"🧱 Object: {' · '.join(obj)}")
        if beat.get("item") or obj:
            say("")

        person = beat.get("person")
        if person:
            say(f"🗣️ **{person.get('name', 'someone')}**")
            say("")
            if beat.get("greeting"):
                say(f"> {beat['greeting']}")
                say("")
            for i, ex in enumerate(beat.get("exchanges", [])):
                tag = " *(ends the talk)*" if i == 0 else ""
                say(f"**▸ {ex.get('prompt', '')}**{tag}")
                say(f"> {ex.get('reply', '')}")
                say("")

        slate = beat.get("slate")
        if slate:
            say(f"📄 **Slate: {slate.get('title', '')}**" + (" *(optional)*" if beat.get("optional") else ""))
            for line in slate.get("text", "").splitlines():
                say(f"> {line}" if line else ">")
            say("")

        msg = beat.get("message")
        if msg:
            say(f"🪧 **Box: {msg.get('title', '')}**")
            say(f"> {msg.get('text', '')}")
            say("")
        if beat.get("say"):
            who = speaker.get("name", "voice")
            say(f"🔊 **{who}:**")
            say(f"> {beat['say']}")
            say("")
        if beat.get("journal"):
            say(f"📓 *Journal:* {beat['journal']}")
            say("")

    deadline = recipe.get("deadline")
    if deadline:
        mins, secs = divmod(deadline.get("seconds", 0), 60)
        clock = f"{mins} min" + (f" {secs} s" if secs else "")
        say("---")
        say("")
        say(f"## ⏱️ DEADLINE · {clock}")
        say("")
        say(f"Starts when beat {deadline.get('after')} is done. Beat {deadline.get('until')} by then, or the mission fails.")
        say("")
        fail = deadline.get("fail", {})
        if fail.get("say"):
            say(f"🔊 **{speaker.get('name', 'voice')}, on failure:**")
            say(f"> {fail['say']}")
            say("")
        if fail.get("journal"):
            say(f"📓 *Journal, failed:* {fail['journal']}")
            say("")

    if notes:
        say("---")
        say("")
        say("## Author's notes")
        say("")
        for key, val in recipe.items():
            if key.startswith("_"):
                lines = val if isinstance(val, list) else [val]
                say(" ".join(str(x) for x in lines))
                say("")
    return "\n".join(out).rstrip() + "\n"


def load(name):
    path = Path(name)
    if not path.exists():
        path = RECIPES / (name if name.endswith(".json") else name + ".json")
    return json.loads(path.read_text(encoding="utf-8"))


def main():
    ap = argparse.ArgumentParser(description="Print a Delve recipe for reading on a phone.")
    ap.add_argument("recipe", nargs="?", help="recipe id or path")
    ap.add_argument("--all", action="store_true", help="every recipe in data/delves/recipes")
    ap.add_argument("--notes", action="store_true", help="include the author's _notes")
    ap.add_argument("--write", action="store_true", help="write data/delves/read/<id>.md instead of printing")
    args = ap.parse_args()
    if not args.all and not args.recipe:
        ap.error("give a recipe id, or --all")

    recipes = [json.loads(p.read_text(encoding="utf-8")) for p in sorted(RECIPES.glob("*.json"))] if args.all else [load(args.recipe)]
    sys.stdout.reconfigure(encoding="utf-8")
    if args.write:
        OUT.mkdir(parents=True, exist_ok=True)
        for r in recipes:
            head = f"<!-- GENERATED by scripts/delves/read.py from recipes/{r['id']}.json. Regenerate, never edit. -->\n\n"
            (OUT / f"{r['id']}.md").write_text(head + render(r, args.notes), encoding="utf-8", newline="\n")
        print(f"wrote {len(recipes)} file(s) to {OUT.relative_to(ROOT)}")
    else:
        for r in recipes:
            print(render(r, args.notes))


if __name__ == "__main__":
    main()
