"""Render a whole PackIn from the ONE JSON packin_assets.py writes: no archives, no game folder, no Mutagen.

    python packin_render.py <assets.json> <out.png> [--size 480] [--grey] [--rot <convention>]
    python packin_render.py <assets.json> --measure

His ask, 2026-10-09 ("next is rendering these"). Every ref's NIF geometry is placed by
    world = ref(pos, rot, scale) @ geometry.xform @ v
and a nested PackIn's refs compose under their parent's transform. Editor markers are not drawn.
The four views, the shading and the texturing are clay.py's own (contact_sheet, render), so a PackIn
and a single NIF can never be drawn by two different rasterisers.

THE REFR ROTATION CONVENTION is the one thing here that is not read off a file, so it is MEASURED, not
assumed: --measure composes the PackIn under every candidate in CONVENTIONS and compares the result
against the PackIn's own OBND (`bounds`, written by `FrankyCLI packin export`). The default is the
convention that measurement chose; DEFAULT_ROT records the evidence. A candidate that changes nothing
on a given PackIn (no turned pieces, or only half turns) cannot be told apart there and is reported so.
"""
from __future__ import annotations
import json, math, sys
from pathlib import Path
from types import SimpleNamespace
import numpy as np
from PIL import Image
import clay


def _rx(a): c, s = math.cos(a), math.sin(a); return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
def _ry(a): c, s = math.cos(a), math.sin(a); return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])
def _rz(a): c, s = math.cos(a), math.sin(a); return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


# name -> rotation matrix from a REFR's (x, y, z) radians. "cw" negates each angle (a clockwise turn seen
# from +axis); the order is which axis applies first to a vertex (rightmost in the product).
CONVENTIONS = {
    "ccw-zyx": lambda x, y, z: _rz(z) @ _ry(y) @ _rx(x),
    "cw-zyx": lambda x, y, z: _rz(-z) @ _ry(-y) @ _rx(-x),
    "ccw-xyz": lambda x, y, z: _rx(x) @ _ry(y) @ _rz(z),
    "cw-xyz": lambda x, y, z: _rx(-x) @ _ry(-y) @ _rz(-z),
}
# MEASURED 2026-10-09 on SciIntHallSm2Way02__SC (01803B, a corner with pieces at pi/2): the cw candidates
# reproduce its OBND to 1 mm, the ccw ones overshoot by 0.68 m. Agrees with docs/designlib/sci_hallway.md
# ("Starfield uses CW rotation convention", found in game). Only YAW is measured: zyx vs xyz cannot be told
# apart on pure yaw, so a ref turned on X or Y is named on every render until a tilted PackIn settles it.
DEFAULT_ROT = "cw-zyx"


def ref_matrix(r: dict, conv: str) -> np.ndarray:
    M = np.eye(4)
    M[:3, :3] = CONVENTIONS[conv](*r["rot"]) * r.get("scale", 1.0)
    M[:3, 3] = r["pos"]
    return M


def placed(refs: list, assets: dict, conv: str, parent=np.eye(4), out=None):
    """(world matrix, geometry, nif) for every drawable geometry, nested PackIns composed."""
    out = [] if out is None else out
    for r in refs:
        if abs(r["rot"][0]) > 1e-6 or abs(r["rot"][1]) > 1e-6:
            TILTED.add(f"{r.get('eid', r['base'])} rot {r['rot']}")
        M = parent @ ref_matrix(r, conv)
        a = assets.get(r.get("nif")) if r.get("nif") else None
        if a and not a["marker"]:
            for g in a["geometries"]:
                out.append((M @ np.array(g["xform"]), g, r["nif"]))
        if r.get("refs"):
            placed(r["refs"], assets, conv, M, out)
    return out


TILTED: set[str] = set()        # refs whose X/Y rotation rides on the UNMEASURED axis order
_TEX: dict[str, np.ndarray] = {}


def scene(doc: dict, conv: str, textured: bool):
    """clay.render's inputs: triangles, boxes (none), and the texture tuple, plus what was not drawn."""
    tris, surfs, notes = [], [], []
    for W, g, nif in placed(doc["refs"], doc["assets"], conv):
        if not g["mesh"]:
            notes.append(f"{g['name']} in {nif}: no mesh")
            continue
        raw = Path(g["mesh"]).read_bytes()
        v, f = clay.parse_mesh(raw)
        world = (np.c_[v, np.ones(len(v))] @ W.T)[:, :3]
        tris.append(world[f])
        if not textured:
            continue
        alb, uv = g["albedo"], clay.parse_uv(raw)
        if uv is None or alb is None:
            surfs.append((len(f), None, None, None, g["name"], "no UVs" if uv is None else "no material"))
        elif not alb["colour"]:
            surfs.append((len(f), None, None, SimpleNamespace(), g["name"], None))   # normal-only: not drawn
        else:
            if alb["texture"]:
                if alb["texture"] not in _TEX:
                    _TEX[alb["texture"]] = np.asarray(Image.open(alb["texture"]).convert("RGB"), dtype=np.float64) / 255.0
                arr = _TEX[alb["texture"]]
            else:
                arr = np.array(alb["flat"], dtype=np.float64).reshape(1, 1, 3)
            surfs.append((len(f), uv[f], arr, SimpleNamespace(tint=tuple(alb["tint"]), scale=tuple(alb["scale"]),
                                                            offset=tuple(alb["offset"])), g["name"], None))
    T = np.concatenate(tris) if tris else np.zeros((0, 3, 3))
    return T, (clay.flatten(surfs) if textured else None), notes


def measure(doc: dict) -> int:
    if not doc.get("bounds"):
        print("  no `bounds` in this file: export it with `FrankyCLI packin export` to measure")
        return 2
    lo_ref, hi_ref = np.array(doc["bounds"][0]), np.array(doc["bounds"][1])
    print(f"  OBND  {np.round(lo_ref, 3)} .. {np.round(hi_ref, 3)}")
    results = {}
    for conv in CONVENTIONS:
        T, _, _ = scene(doc, conv, textured=False)
        p = T.reshape(-1, 3)
        lo, hi = p.min(0), p.max(0)
        err = float(max(np.abs(lo - lo_ref).max(), np.abs(hi - hi_ref).max()))
        results[conv] = (err, lo, hi)
        print(f"  {conv:8}  {np.round(lo, 3)} .. {np.round(hi, 3)}   worst corner off by {err:.3f}")
    best = min(results, key=lambda c: results[c][0])
    ties = [c for c in results if abs(results[c][0] - results[best][0]) < 1e-3]
    print(f"  best: {', '.join(ties)}" + ("   (indistinguishable on this PackIn)" if len(ties) > 1 else ""))
    return 0


def main(argv: list[str]) -> int:
    doc = json.loads(Path(argv[0]).read_text(encoding="utf-8"))
    if "--measure" in argv:
        return measure(doc)
    out = Path(argv[1])
    size = int(argv[argv.index("--size") + 1]) if "--size" in argv else 480
    conv = argv[argv.index("--rot") + 1] if "--rot" in argv else DEFAULT_ROT
    if conv not in CONVENTIONS:
        raise SystemExit(f"rotation convention {conv!r} unknown or not yet measured; pass --rot one of "
                         f"{', '.join(CONVENTIONS)} (run --measure on a PackIn with turned pieces first)")
    T, tex, notes = scene(doc, conv, textured="--grey" not in argv)
    sheet, lo, hi = clay.contact_sheet(T, [], tex, size, f"{doc.get('name', '?')}  rot {conv}")
    sheet.save(out)
    print(f"  {out}  {len(T):,} triangles  rot {conv}")
    for n in notes:
        print(f"  NOT DRAWN {n}")
    for t in sorted(TILTED):
        print(f"  UNPROVEN TILT {t}: X/Y rotation, axis order not yet measured ({conv})")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
