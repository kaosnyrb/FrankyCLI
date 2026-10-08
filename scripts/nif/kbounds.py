"""Overall bounds of a NIF tree: every BSGeometry's stored box, carried through its NiNode chain.

    python kbounds.py <nif>...

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


def bounds(n: Nif):
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
            box = w.xform + 52 + 4 + 16           # after transform, collision link, NiBound
            c = struct.unpack_from("<3f", bl, box); d = struct.unpack_from("<3f", bl, box + 12)
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
    for p in sys.argv[1:]:
        lo, hi, g = bounds(Nif(Path(p).read_bytes()))
        print(f"{Path(p).name:26s} {g:3d} geoms  x {lo[0]:6.2f}..{hi[0]:6.2f}  y {lo[1]:6.2f}..{hi[1]:6.2f}  "
              f"z {lo[2]:6.2f}..{hi[2]:6.2f}   verdict {('STANDS' if abs(lo[2]) <= 0.05 else 'zmin %.2f' % lo[2])}")
