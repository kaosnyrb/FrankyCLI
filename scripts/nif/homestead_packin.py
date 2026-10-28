"""One building out of a Homestead outpost JSON, as a designed PackIn JSON (`packin resolve`'s input).

    python homestead_packin.py <outpost.json> <out.json> [--building N]
    python homestead_packin.py <outpost.json> --list

Homestead (kaosnyrb/Homestead) reads a player outpost out of a save and splits it into BUILDINGS by its
link tree (format 4). Each building lists what a PackIn of it must place: PackIn roots and placed pieces,
`position_local` relative to the building's anchor and NOT rotated, `yaw_radians` as the save stores it
(his `getangle z` read the same value in degrees, so it is the REFR's own z rotation). Content is not
listed: each placed PackIn rebuilds its own. His idea, 2026-10-28: "capturing the buildings rather than
the outpost".

So this file is a translation and decides nothing: FormID -> FormKey through Homestead's `plugin_guess`
(the save's load-order index read in order, labelled a guess there), `pos` = position_local,
`rot` = [0, 0, yaw]. A wrong guess or a wrong convention shows on the render rather than hiding in it:
`packin resolve` names each base's record type and model, and the four views put every piece in sight.
Refused, never skipped: a created form, a form with no plugin, an unplaceable piece (Homestead names
those per building), a Homestead format other than 4.
"""
from __future__ import annotations
import argparse, json, sys
from pathlib import Path

FORMAT = 4


def form_key(ref: dict) -> str:
    if ref["kind"] == "created" or not ref.get("plugin_guess"):
        raise ValueError(f"{ref['id']}: no plugin to resolve it in (kind {ref['kind']})")
    return f"{int(ref['id'], 16) & 0xFFFFFF:06X}:{ref['plugin_guess']}"


def designed(doc: dict, i: int) -> dict:
    b = doc["buildings"][i]
    if b["unplaceable"]:
        names = ", ".join(f"{u['base']['id']} ({u['problem']})" for u in b["unplaceable"])
        raise ValueError(f"building {i} has pieces that cannot be placed: {names}")
    refs = []
    for p in b["place"]:
        r = {"base": form_key(p["form"]), "pos": p["position_local"],
             "rot": [0.0, 0.0, p.get("yaw_radians", 0.0)], "note": f"{p['role']} {p['self']}"}
        refs.append(r)
    return {"name": f"homestead_{Path(doc['save']).stem.split('_')[0]}_{b['kind']}{i}",
            "source": f"Homestead format {doc['homestead']}, {doc['save']}, building {i} (anchor {b['anchor']})",
            "refs": refs}


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("outpost")
    ap.add_argument("out", nargs="?")
    ap.add_argument("--building", type=int, default=0)
    ap.add_argument("--list", action="store_true")
    a = ap.parse_args(argv)
    doc = json.load(open(a.outpost, encoding="utf-8"))
    if doc.get("homestead") != FORMAT:
        print(f"homestead_packin: {a.outpost} is Homestead format {doc.get('homestead')}, this reads {FORMAT}",
              file=sys.stderr)
        return 2
    if a.list:
        for i, b in enumerate(doc["buildings"]):
            print(f"{i}: {b['kind']:8s} {len(b['pieces']):4d} pieces, {len(b['place']):3d} to place, "
                  f"{len(b['unplaceable'])} unplaceable, origin {b['origin']}")
        return 0
    if not a.out:
        ap.error("an output path is required unless --list")
    try:
        out = designed(doc, a.building)
    except ValueError as e:
        print(f"homestead_packin: {e}", file=sys.stderr)
        return 2
    with open(a.out, "w", encoding="utf-8") as f:
        json.dump(out, f, indent=2)
    print(f"homestead_packin: {a.out}: {len(out['refs'])} refs from building {a.building}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
