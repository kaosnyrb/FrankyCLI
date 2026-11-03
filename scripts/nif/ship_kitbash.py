"""A whole vanilla ship as a kitbash recipe: one part per placed mesh, ready for kitbash.py.

    FrankyCLI packin ship <ship GBFM FormKey> ship.json         the Blueprint as a PackIn file, expanded
    python packin_assets.py ship.json assets.json                every mesh it uses, extracted
    python ship_kitbash.py assets.json <out dir> --collision keep|base-only [--name N]

Writes <out dir>/<name>.json (Kim's recipe form: "nif": "nif/<file>") and copies every mesh it names into
<out dir>/nif/, so `kitbash.py <name>.json <name>.nif` builds from that folder as it does for her recipes.

His idea, 2026-11-03: "taking existing ships from the game and turning them into kitbashes. There's a whole
set of crashsite POIs that could benifet from that", and his fact: a ship is a GenericBaseForm whose
Blueprint component holds each module's position and rotation (manual part 01). Each Blueprint row is a
module's exterior PackIn, so a ship is a PackIn of PackIns and the transform walk is packin_render.py's:
    world = row(pos, rot) @ ref(pos, rot, scale) @ ... @ the mesh's own root
A part is a mesh whose world matrix kitbash.py can reproduce: translate, uniform scale, and a rotation
decomposed into its `rotate` (degrees, R = Rz Ry Rx, sfnif.rot_xyz) and RECOMPOSED as a check, so a
decomposition that loses the turn refuses instead of placing a part wrong.

THE ROTATION CONVENTION: placed refs use packin_render.DEFAULT_ROT (measured clockwise on yaw, 2026-10-09).
⚠ The Blueprint rows ride on the same convention and that is NOT measured: the ship's own OBND does not
match the assembled geometry under any candidate (the Bayonet: 34 m off), so it cannot grade one. Rows turned
by a half turn are the same under every candidate; a row turned on X or Y (the Bayonet's two wing guns at
60 degrees) is NAMED on every run until his eye or a snap-node check settles it.

--collision is required, because the default is a choice somebody lives with: `keep` leaves every mesh its
own Havok body (each a moving one, so in zero-g the parts drift apart, his MB51a play 2026-11-02); `base-only`
keeps part 0's and drops the rest (the fix that held OT-02's wrecks together).
Markers (flagged, or made only of EditorMarker geometry) are skipped and counted; a mesh packin_assets.py
never extracted is skipped and NAMED; a mesh with a geometry the renderer could not texture is still pasted.
Part 0 is the base and kitbash.py will not move it, so the recipe is in the base's frame: the UNTURNED mesh
nearest the ship's origin, every other part shifted by its offset, and the ship's origin written into `_how`
(the Bayonet's cockpit sits exactly at it; the Stroud freighter has no mesh there). A ship with no unturned
mesh is refused by name.
"""
from __future__ import annotations
import json, math, shutil, sys
from pathlib import Path
import numpy as np
import packin_render as pr
import sfnif

EPS = 1e-4


def walk(refs, assets, conv, parent, out, skipped, depth=0):
    for r in refs:
        M = parent @ pr.ref_matrix(r, conv)
        nif = r.get("nif")
        if nif:
            a = assets.get(nif)
            if a is None:
                skipped.append(f"{nif} ({r.get('eid', r['base'])}): not extracted")
            elif a["marker"] or not any(not g["name"].startswith("EditorMarker") for g in a["geometries"]):
                skipped.append(None)        # a marker, flagged or made only of EditorMarker geometry
            else:
                # Pasted whole even when the renderer could not texture a geometry of it: kitbash.py copies
                # the NIF's bytes, so a material the renderer missed is the renderer's gap, not the part's.
                out.append((M, nif, a["nif"], r.get("eid", r["base"]), depth))
        if r.get("refs"):
            walk(r["refs"], assets, conv, M, out, skipped, depth + 1)


def unpastable(data: bytes) -> str | None:
    """Why kitbash.py would refuse these NIF bytes, asked the way sfnif.merge asks it, or None."""
    n = sfnif.Nif(data)
    if n.footer != b"\x01\x00\x00\x00\x00\x00\x00\x00":
        return "footer is not one root at block 0"
    for i, bl in enumerate(n.blocks):
        t = n.type_of(i)
        try:
            w = sfnif.walk(t, bl)
        except Exception as ex:
            return f"block {i} {t}: {type(ex).__name__}"
        if w.used != len(bl):
            return f"block {i} {t} does not walk ({w.used}/{len(bl)})"
    return None


def decompose(M):
    """(translate, scale, rotate degrees for sfnif.rot_xyz) or a reason it cannot be one."""
    R3 = M[:3, :3]
    s = [float(np.linalg.norm(R3[:, k])) for k in range(3)]
    if max(s) - min(s) > EPS:
        return None, f"non-uniform scale {s}"
    sc = s[0]
    R = R3 / sc
    if np.linalg.det(R) < 0:
        return None, "a mirror (negative determinant), which a rotation cannot carry"
    # R = Rz(z) Ry(y) Rx(x):  R[2,0] = -sin y
    y = math.asin(max(-1.0, min(1.0, -R[2, 0])))
    if abs(math.cos(y)) > 1e-6:
        x = math.atan2(R[2, 1], R[2, 2])
        z = math.atan2(R[1, 0], R[0, 0])
    else:                                   # gimbal: x and z share one axis, put it all on z
        x = 0.0
        z = math.atan2(-R[0, 1], R[1, 1])
    deg = [math.degrees(x), math.degrees(y), math.degrees(z)]
    back = np.array(sfnif.rot_xyz(deg))
    if np.abs(back - R).max() > 1e-4:
        return None, f"rotation does not recompose (max error {np.abs(back - R).max():.2e})"
    return ([round(float(v), 4) for v in M[:3, 3]], round(sc, 6), [round(v, 4) for v in deg]), None


def main(argv):
    if len(argv) < 2 or "--collision" not in argv:
        raise SystemExit(__doc__.split("\n\n")[0] + "\n\n--collision keep|base-only is required (see the docstring)")
    assets_path, out_dir = Path(argv[0]), Path(argv[1])
    collision = argv[argv.index("--collision") + 1]
    if collision not in ("keep", "base-only"):
        raise SystemExit(f"--collision {collision!r}: keep or base-only")
    doc = json.loads(assets_path.read_text(encoding="utf-8"))
    name = argv[argv.index("--name") + 1] if "--name" in argv else doc["name"].lower()
    conv = pr.DEFAULT_ROT

    placed, skipped = [], []
    walk(doc["refs"], doc["assets"], conv, np.eye(4), placed, skipped)
    why_not = {}
    for p in placed:
        if p[1] not in why_not:
            why_not[p[1]] = unpastable(Path(p[2]).read_bytes())
    for p in placed:
        if why_not[p[1]]:
            skipped.append(f"{p[1]} ({p[3]}): kitbash.py cannot paste it, {why_not[p[1]]}")
    placed = [p for p in placed if not why_not[p[1]]]
    if not placed:
        raise SystemExit("REFUSED: nothing drawable was placed")

    parts, nif_files, refused = [], {}, []
    # Part 0 is the base and kitbash.py will not move it, so the recipe's frame is the base's frame. The base is
    # the UNTURNED, unscaled mesh nearest the ship's origin (often exactly at it); every other part is shifted
    # by its offset, and the offset is written down, so the ship's own origin stays recoverable. A turned base
    # would turn the whole ship off its axes, so a ship with no unturned mesh is refused.
    upright = [k for k, p in enumerate(placed) if np.abs(p[0][:3, :3] - np.eye(3)).max() < EPS]
    if not upright:
        raise SystemExit("REFUSED: no mesh sits unturned and unscaled, so no part 0 keeps the ship on its axes")
    base = min(upright, key=lambda k: float(np.linalg.norm(placed[k][0][:3, 3])))
    shift = placed[base][0][:3, 3].copy()
    origin_at = [round(float(-v), 4) for v in shift]
    order = [base] + [k for k in range(len(placed)) if k != base]
    for k in order:
        M, nif, local, eid, _ = placed[k]
        M = M.copy()
        M[:3, 3] -= shift
        fname = Path(nif).name
        src = Path(local)
        if fname in nif_files and nif_files[fname] != src.read_bytes():
            raise SystemExit(f"REFUSED: two different meshes are both named {fname}; nif/ is flat")
        nif_files[fname] = src.read_bytes()
        part = {"nif": f"nif/{fname}"}
        if k != base:
            got, why = decompose(M)
            if why:
                refused.append(f"{eid} ({nif}): {why}")
                continue
            t, sc, rot = got
            part["translate"] = t
            if any(abs(v) > EPS for v in rot):
                part["rotate"] = rot
            if abs(sc - 1.0) > EPS:
                part["scale"] = sc
            if collision == "base-only":
                part["collision"] = False
        parts.append(part)
    if refused:
        raise SystemExit("REFUSED, a part kitbash.py cannot place:\n  " + "\n  ".join(refused))

    tilted = sorted({f"{r['note']}  rot {r['rot']}" for r in doc["refs"]
                     if abs(r["rot"][0]) > 1e-6 or abs(r["rot"][1]) > 1e-6})
    (out_dir / "nif").mkdir(parents=True, exist_ok=True)
    for fname, data in nif_files.items():
        (out_dir / "nif" / fname).write_bytes(data)
    spec = {
        "name": name,
        "_how": (f"Generated by FrankyCLI scripts/nif/ship_kitbash.py from {doc.get('name')} [{doc.get('source')}]: "
                 f"`packin ship`, then packin_assets.py, then this; refs at {conv}, the Blueprint rows on the same "
                 f"convention (UNMEASURED for a row turned on X or Y: {len(tilted)} here). collision {collision}. "
                 f"Part 0 is {placed[base][3]}; the ship's own origin sits at {origin_at} in this recipe's frame. "
                 f"Regenerate, never hand-edit; a crash-site look is a recipe of its own built from this one."),
        "parts": parts,
    }
    out = out_dir / f"{name}.json"
    out.write_text(json.dumps(spec, indent=1) + "\n", encoding="utf-8")
    markers = sum(1 for s in skipped if s is None)
    unread = [s for s in skipped if s]
    print(f"  {out}: {len(parts)} parts (base: {placed[base][3]}, the ship's origin at {origin_at}), "
          f"{len(nif_files)} meshes into {out_dir / 'nif'}")
    print(f"  skipped: {markers} marker(s), {len(unread)} skipped and named below")
    for s in unread:
        print(f"    SKIPPED {s}")
    for t in tilted:
        print(f"  UNPROVEN ROW TURN {t}: X/Y rotation on a Blueprint row rides on {conv}")


if __name__ == "__main__":
    main(sys.argv[1:])
