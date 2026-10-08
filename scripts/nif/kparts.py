"""Per-part boxes of a NIF tree: each child of the root, and any geometry whose name matches.

    python kparts.py <nif> [name-substring ...]

Same transform walk as kbounds.py, but a box per root child (one pasted object each, his shape) and per
named BSGeometry, so a placement can be measured against the thing it should sit on.
"""
import struct, sys
from pathlib import Path
import sfnif
from sfnif import Nif
from kbounds import xf, mv, mm

NONE = sfnif.NONE


def name_of(n, i):
    v = struct.unpack_from("<I", n.blocks[i], 0)[0]
    return n.strings[v] if v != NONE else "(unnamed)"


def boxes(n, start, PT, PR, PS, out, label, want):
    lo, hi = [1e9] * 3, [-1e9] * 3

    def visit(i, PT, PR, PS):
        t, bl = n.type_of(i), n.blocks[i]
        w = sfnif.walk(t, bl)
        T, R, S = xf(bl, w)
        WT = [a + b for a, b in zip(mv(PR, [PS * x for x in T]), PT)]
        WR, WS = mm(PR, R), PS * S
        if t == "NiNode":
            ne = struct.unpack_from("<I", bl, 4)[0]
            for off in w.refs[ne + 2:]:
                c = struct.unpack_from("<I", bl, off)[0]
                if c != NONE and n.type_of(c) in ("NiNode", "BSGeometry"):
                    visit(c, WT, WR, WS)
        elif t == "BSGeometry":
            box = w.xform + 52 + 4 + 16
            c = struct.unpack_from("<3f", bl, box); d = struct.unpack_from("<3f", bl, box + 12)
            glo, ghi = [1e9] * 3, [-1e9] * 3
            for sx in (-1, 1):
                for sy in (-1, 1):
                    for sz in (-1, 1):
                        p = [c[0] + sx * d[0], c[1] + sy * d[1], c[2] + sz * d[2]]
                        q = [a + b for a, b in zip(mv(WR, [WS * x for x in p]), WT)]
                        for k in range(3):
                            lo[k] = min(lo[k], q[k]); hi[k] = max(hi[k], q[k])
                            glo[k] = min(glo[k], q[k]); ghi[k] = max(ghi[k], q[k])
            nm = name_of(n, i)
            if any(s.lower() in nm.lower() for s in want):
                out.append((f"  geom {nm}", glo, ghi))

    visit(start, PT, PR, PS)
    out.append((label, lo, hi))


def fmt(label, lo, hi):
    c = [(a + b) / 2 for a, b in zip(lo, hi)]
    return (f"{label:44s} x {lo[0]:6.2f}..{hi[0]:6.2f}  y {lo[1]:6.2f}..{hi[1]:6.2f}  z {lo[2]:6.2f}..{hi[2]:6.2f}"
            f"   centre xy ({c[0]:5.2f}, {c[1]:5.2f})")


if __name__ == "__main__":
    n = Nif(Path(sys.argv[1]).read_bytes()); want = sys.argv[2:]
    root = n.blocks[0]; w = sfnif.walk("NiNode", root); ne = struct.unpack_from("<I", root, 4)[0]
    I = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
    T, R, S = xf(root, w)
    out = []
    for off in w.refs[ne + 2:]:
        c = struct.unpack_from("<I", root, off)[0]
        if c != NONE and n.type_of(c) in ("NiNode", "BSGeometry"):
            boxes(n, c, T, R, S, out, f"[{c}] {n.type_of(c)} {name_of(n, c)}", want)
    for row in out:
        print(fmt(*row))
