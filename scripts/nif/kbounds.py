"""Overall bounds of a NIF tree: every BSGeometry's stored box, carried through its NiNode chain.

    python kbounds.py <nif>...
    python kbounds.py --json <nif>      one line, {"min": [x, y, z], "max": [x, y, z], "geoms": n}, for gen_delve
                                        (a reskinned activator's OBND, plan OT-6E 2026-11-03); refuses a nif with no geometry.

Transform per NiAVObject: world.T = P.R @ (P.S * T) + P.T ; world.R = P.R @ R ; world.S = P.S * S.
The 3x3 is applied as stored rows (R @ v). ⚠ The row/column convention is unproven for a
non-trivial rotation; a box corner set is symmetric under the sign of a pure 90 or 180 degree yaw,
so a wrong convention can only show up on oblique turns.
"""
import struct, sys
from pathlib import Path
import sfnif
from sfnif import Nif

NONE = sfnif.NONE


def xf(bl, w):
    o = w.xform
    T = list(struct.unpack_from("<3f", bl, o))
    R = [list(struct.unpack_from("<3f", bl, o + 12 + 12 * r)) for r in range(3)]
    S = struct.unpack_from("<f", bl, o + 48)[0]
    return T, R, S


def mv(R, v): return [sum(R[i][k] * v[k] for k in range(3)) for i in range(3)]
def mm(A, B): return [[sum(A[i][k] * B[k][j] for k in range(3)) for j in range(3)] for i in range(3)]


# A stored box of centre AND extent float-max is Bethesda's "no box" (2026-11-03: 11 geometries in vanilla's Stroud
# freighter modules, decals and details, carry it). Added in, one such box made the whole ship +-6.8e38 and would have
# been written onto an activator's OBND. So it is skipped, and COUNTED, never silently.
UNSET = 3.0e38


def stored_box(bl, w):
    """A BSGeometry's stored box (centre, extent), or None when it stores Bethesda's float-max "no box". The ONE reader
    of that box: kitbash.py, kparts.py and clay.py take it from here, so the sentinel cannot be read raw again."""
    box = w.xform + 52 + 4 + 16           # after transform, collision link, NiBound
    c = struct.unpack_from("<3f", bl, box); d = struct.unpack_from("<3f", bl, box + 12)
    return None if any(abs(x) >= UNSET for x in c + d) else (c, d)


def bounds(n: Nif, unboxed: list | None = None):
    lo, hi = [1e9] * 3, [-1e9] * 3
    geoms = 0

    def visit(i, PT, PR, PS):
        nonlocal geoms
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
            geoms += 1
            sb = stored_box(bl, w)
            if sb is None:
                if unboxed is not None:
                    unboxed.append(i)
                return
            c, d = sb
            for sx in (-1, 1):
                for sy in (-1, 1):
                    for sz in (-1, 1):
                        p = [c[0] + sx * d[0], c[1] + sy * d[1], c[2] + sz * d[2]]
                        q = [a + b for a, b in zip(mv(WR, [WS * x for x in p]), WT)]
                        for k in range(3):
                            lo[k] = min(lo[k], q[k]); hi[k] = max(hi[k], q[k])

    I = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
    visit(0, [0, 0, 0], I, 1.0)
    return lo, hi, geoms


if __name__ == "__main__":
    if sys.argv[1:2] == ["--json"]:
        import json
        if len(sys.argv) != 3:
            sys.exit("REFUSED: kbounds.py --json takes exactly one nif")
        unboxed = []
        lo, hi, g = bounds(Nif(Path(sys.argv[2]).read_bytes()), unboxed)
        if g == 0:
            sys.exit(f"REFUSED: {sys.argv[2]} has no BSGeometry, so it has no bounds")
        if g == len(unboxed):
            sys.exit(f"REFUSED: every one of {sys.argv[2]}'s {g} BSGeometry stores no box, so it has no bounds")
        out = {"min": [round(v, 4) for v in lo], "max": [round(v, 4) for v in hi], "geoms": g}
        if unboxed:
            out["unboxed"] = len(unboxed)   # said, and only when it happened, so an unaffected nif prints as before
        print(json.dumps(out))
        sys.exit(0)
    for p in sys.argv[1:]:
        unboxed = []
        lo, hi, g = bounds(Nif(Path(p).read_bytes()), unboxed)
        print(f"{Path(p).name:26s} {g:3d} geoms  x {lo[0]:6.2f}..{hi[0]:6.2f}  y {lo[1]:6.2f}..{hi[1]:6.2f}  "
              f"z {lo[2]:6.2f}..{hi[2]:6.2f}   verdict {('STANDS' if abs(lo[2]) <= 0.05 else 'zmin %.2f' % lo[2])}"
              + (f"   ({len(unboxed)} geom(s) store no box, skipped)" if unboxed else ""))
