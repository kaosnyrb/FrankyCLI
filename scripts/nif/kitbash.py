"""Build a kitbash from a spec, placing parts by ANCHOR, then check where everything landed.

    python kitbash.py spec.json out.nif

A part is placed either by raw "translate" or by an "anchor" resolved from MEASURED boxes:

    {"nif": "...", "scale": 0.25, "rotate": [0, 0, 90],
     "anchor": {"on": "AntennaTech03:1", "at": "top", "sink": 0.04}}

  on    a BSGeometry or NiNode name in the BASE part (part 0), matched exactly
  at    "top": the part's own box bottom-centre goes on the target box's top-centre
  sink  metres to lower it into the target so it reads as attached (default 0)

Why (2026-10-08): the first hand-typed placement put the dish at the console's ORIGIN, and the mast is
0.11 m off that origin and 0.29 m higher than guessed. He caught it in NifSkope. Coordinates typed by
hand are a guess; a measured anchor is not.

After building, prints every pasted part's box and, for each anchored part, ASSERTS its bottom-centre
landed on the anchor within 1 cm. Exit 1 on a miss.
"""
from __future__ import annotations
import json, struct, sys
from pathlib import Path
import sfnif
from sfnif import Nif
from kbounds import xf, mv, mm, bounds

NONE = sfnif.NONE
I = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]


def named_boxes(n: Nif) -> dict[str, tuple[list, list]]:
    """Every named NiNode / BSGeometry in the tree -> its world box (from the file's root)."""
    found: dict[str, tuple[list, list]] = {}

    def visit(i, PT, PR, PS):
        t, bl = n.type_of(i), n.blocks[i]
        w = sfnif.walk(t, bl)
        T, R, S = xf(bl, w)
        WT = [a + b for a, b in zip(mv(PR, [PS * x for x in T]), PT)]
        WR, WS = mm(PR, R), PS * S
        lo, hi = [1e9] * 3, [-1e9] * 3
        if t == "NiNode":
            ne = struct.unpack_from("<I", bl, 4)[0]
            for off in w.refs[ne + 2:]:
                c = struct.unpack_from("<I", bl, off)[0]
                if c != NONE and n.type_of(c) in ("NiNode", "BSGeometry"):
                    clo, chi = visit(c, WT, WR, WS)
                    lo = [min(a, b) for a, b in zip(lo, clo)]; hi = [max(a, b) for a, b in zip(hi, chi)]
        else:
            box = w.xform + 52 + 4 + 16
            c = struct.unpack_from("<3f", bl, box); d = struct.unpack_from("<3f", bl, box + 12)
            for sx in (-1, 1):
                for sy in (-1, 1):
                    for sz in (-1, 1):
                        p = [c[0] + sx * d[0], c[1] + sy * d[1], c[2] + sz * d[2]]
                        q = [a + b for a, b in zip(mv(WR, [WS * x for x in p]), WT)]
                        lo = [min(a, b) for a, b in zip(lo, q)]; hi = [max(a, b) for a, b in zip(hi, q)]
        v = struct.unpack_from("<I", bl, 0)[0]
        if v != NONE:
            nm = n.strings[v]
            if nm in found:
                found[nm] = ("AMBIGUOUS", None)
            else:
                found[nm] = (lo, hi)
        return lo, hi

    visit(0, [0, 0, 0], I, 1.0)
    return found


def own_box(nif_path: str, rotate, scale):
    """The part's box in the frame it will be pasted with (its rotation and scale, no translation)."""
    lo, hi, _ = bounds(Nif(Path(nif_path).read_bytes()))
    R = sfnif.rot_xyz(rotate)
    plo, phi = [1e9] * 3, [-1e9] * 3
    for x in (lo[0], hi[0]):
        for y in (lo[1], hi[1]):
            for z in (lo[2], hi[2]):
                q = mv(R, [scale * x, scale * y, scale * z])
                plo = [min(a, b) for a, b in zip(plo, q)]; phi = [max(a, b) for a, b in zip(phi, q)]
    return plo, phi


def resolve(spec: dict) -> list[tuple[int, list]]:
    base = named_boxes(Nif(Path(spec["parts"][0]["nif"]).read_bytes()))
    wanted = []
    for k, p in enumerate(spec["parts"][1:], start=1):
        a = p.get("anchor")
        if not a:
            continue
        if "translate" in p:
            raise SystemExit(f"REFUSED: part {k} has both translate and anchor")
        tgt = base.get(a["on"])
        if tgt is None:
            raise SystemExit(f"REFUSED: part {k} anchor {a['on']!r} is not a name in the base part")
        if tgt[0] == "AMBIGUOUS":
            raise SystemExit(f"REFUSED: part {k} anchor {a['on']!r} names more than one block in the base")
        if a.get("at", "top") != "top":
            raise SystemExit(f"REFUSED: anchor 'at' {a.get('at')!r}; only 'top' is built")
        tlo, thi = tgt
        plo, phi = own_box(p["nif"], p.get("rotate", [0, 0, 0]), float(p.get("scale", 1.0)))
        target_pt = [(tlo[0] + thi[0]) / 2, (tlo[1] + thi[1]) / 2, thi[2] - float(a.get("sink", 0.0))]
        part_pt = [(plo[0] + phi[0]) / 2, (plo[1] + phi[1]) / 2, plo[2]]
        p["translate"] = [t - q for t, q in zip(target_pt, part_pt)]
        print(f"  part {k}: anchor {a['on']} top ({target_pt[0]:.3f}, {target_pt[1]:.3f}, {target_pt[2]:.3f})"
              f"  -> translate ({p['translate'][0]:.3f}, {p['translate'][1]:.3f}, {p['translate'][2]:.3f})")
        wanted.append((k, target_pt))
    return wanted


def check(spec: dict, out: Path, wanted) -> int:
    n = Nif(out.read_bytes())
    root = n.blocks[0]; w = sfnif.walk("NiNode", root); ne = struct.unpack_from("<I", root, 4)[0]
    kids = [struct.unpack_from("<I", root, o)[0] for o in w.refs[ne + 2:]]
    pasted = kids[-(len(spec["parts"]) - 1):] if len(spec["parts"]) > 1 else []
    boxes = named_boxes(n)
    fail = 0
    for k, target in wanted:
        nm = n.strings[struct.unpack_from("<I", n.blocks[pasted[k - 1]], 0)[0]]
        lo, hi = boxes[nm]
        got = [(lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, lo[2]]
        miss = max(abs(a - b) for a, b in zip(got, target))
        ok = miss <= 0.01
        print(f"  CHECK part {k} {nm}: bottom-centre ({got[0]:.3f}, {got[1]:.3f}, {got[2]:.3f}) vs anchor "
              f"({target[0]:.3f}, {target[1]:.3f}, {target[2]:.3f})  miss {miss * 100:.1f} cm  {'OK' if ok else 'MISS'}")
        fail += not ok
    lo, hi, g = bounds(n)
    print(f"  WHOLE: {g} geoms  x {lo[0]:.2f}..{hi[0]:.2f}  y {lo[1]:.2f}..{hi[1]:.2f}  z {lo[2]:.2f}..{hi[2]:.2f}"
          f"  {'STANDS' if abs(lo[2]) <= 0.05 else 'zmin %.2f' % lo[2]}")
    return fail


if __name__ == "__main__":
    spec = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
    out = Path(sys.argv[2])
    wanted = resolve(spec)
    sfnif.merge(spec, out)
    bad = sfnif.check_walk(out) + check(spec, out, wanted)
    sys.exit(1 if bad else 0)
