"""CLAY RENDER of a Starfield NIF: grey, flat-shaded, four views in one PNG, so I can LOOK at a model.

    python clay.py <nif> <out.png> [--size 360] [--mod <plugin name>]

His ask, 2026-10-08: "so can we use nifskope to give you eyes on the nifs?" NifSkope has no render-to-file
command, so this reads what NifSkope reads: the node tree (sfnif walk, the transform convention proven
against three vanilla OBNDs in kbounds.py) and each BSGeometry's LOD0 .mesh (from Data/geometries loose,
else the mod's and the game's archives). Shape, placement and silhouette only: NO materials, NO textures.
"Does it look good" stays his eye.

Views: front (from -Y), side (from +X), top (from +Z), and a 3/4 view (yaw 35, pitch 25). The z = 0
ground line is drawn in red on the elevations, so a hanging or floating mesh is visible at a glance.
A geometry whose .mesh cannot be found is LISTED, never silently skipped, and its box is drawn in outline.
"""
from __future__ import annotations
import math, struct, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
import sfnif, ba2get
from sfnif import Nif
from kbounds import xf, mv, mm

NONE = sfnif.NONE
DATA = ba2get.DATA
_IDX: dict[Path, dict[str, int]] = {}


def archives(mod_hint: str | None):
    out = []
    if mod_hint:
        out += sorted(DATA.glob(f"{mod_hint} - Main*.ba2"))
    out += [DATA / n for n in ba2get.VANILLA]
    return out


def fetch_mesh(rel: str, mod_hint: str | None) -> bytes | None:
    rel = "geometries/" + rel.lower().replace("\\", "/") + ".mesh"
    loose = DATA / rel
    if loose.exists():
        return loose.read_bytes()
    for ba2 in archives(mod_hint):
        if ba2 not in _IDX:
            _IDX[ba2] = ba2get.index(ba2)
        if ba2get.norm(rel) in _IDX[ba2]:
            return ba2get.extract(ba2, rel)
    return None


def parse_mesh(d: bytes):
    ni = struct.unpack_from("<I", d, 4)[0]
    idx = np.frombuffer(d, dtype="<u2", count=ni, offset=8).reshape(-1, 3)
    o = 8 + ni * 2
    scale = struct.unpack_from("<f", d, o)[0]
    nv = struct.unpack_from("<I", d, o + 8)[0]
    q = np.frombuffer(d, dtype="<i2", count=nv * 3, offset=o + 12).reshape(nv, 3)
    return q.astype(np.float64) / 32767.0 * scale, idx.astype(np.int64)


def collect(n: Nif, mod_hint: str | None):
    tris, missing, boxes = [], [], []

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
            return
        if t != "BSGeometry":
            return
        o = w.xform + 52 + 4 + 16 + 24 + 12      # transform, collision, NiBound, box, 3 links
        path = None
        if bl[o] == 1:                           # LOD0
            ln = struct.unpack_from("<I", bl, o + 13)[0]
            path = bl[o + 17:o + 17 + ln].decode("utf-8")
        nm_i = struct.unpack_from("<I", bl, 0)[0]
        nm = n.strings[nm_i] if nm_i != NONE else "(unnamed)"
        raw = fetch_mesh(path, mod_hint) if path else None
        if raw is None:
            missing.append((nm, path))
            box = w.xform + 52 + 4 + 16
            c = np.array(struct.unpack_from("<3f", bl, box)); d = np.array(struct.unpack_from("<3f", bl, box + 12))
            corners = np.array([[c[0] + sx * d[0], c[1] + sy * d[1], c[2] + sz * d[2]]
                                for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)])
            boxes.append(corners @ (np.array(WR).T * WS) + np.array(WT))
            return
        v, f = parse_mesh(raw)
        world = v @ (np.array(WR).T * WS) + np.array(WT)
        tris.append(world[f])

    I = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
    visit(0, [0, 0, 0], I, 1.0)
    return (np.concatenate(tris) if tris else np.zeros((0, 3, 3))), missing, boxes


def view_matrix(yaw, pitch):
    y, p = math.radians(yaw), math.radians(pitch)
    Rz = np.array([[math.cos(y), -math.sin(y), 0], [math.sin(y), math.cos(y), 0], [0, 0, 1]])
    Rx = np.array([[1, 0, 0], [0, math.cos(p), -math.sin(p)], [0, math.sin(p), math.cos(p)]])
    return Rx @ Rz            # camera looks along +Y after this; screen x = X, screen up = Z


VIEWS = [("front", 0, 0), ("side", -90, 0), ("top", 0, 90), ("3/4", 35, 25)]


def render(tris, boxes, yaw, pitch, size, lo, hi):
    M = view_matrix(yaw, pitch)
    P = tris @ M.T
    allpts = np.array([[x, y, z] for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]) @ M.T
    smin, smax = allpts.min(0), allpts.max(0)
    span = max(smax[0] - smin[0], smax[2] - smin[2]) * 1.1 or 1.0
    cx, cz = (smin[0] + smax[0]) / 2, (smin[2] + smax[2]) / 2
    k = size / span

    def to_px(p):
        return (p[..., 0] - cx) * k + size / 2, size / 2 - (p[..., 2] - cz) * k

    img = np.full((size, size), 235.0)
    zb = np.full((size, size), np.inf)
    light = np.array([0.35, -0.6, 0.72]); light /= np.linalg.norm(light)
    nrm = np.cross(tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0])
    ln = np.linalg.norm(nrm, axis=1); ok = ln > 1e-12
    shade = np.full(len(tris), 0.6); shade[ok] = 0.25 + 0.7 * np.abs((nrm[ok] / ln[ok, None]) @ light)
    xs, ys = to_px(P)
    for t in range(len(P)):
        x0, y0 = xs[t], ys[t]
        bx0, bx1 = int(max(0, math.floor(x0.min()))), int(min(size - 1, math.ceil(x0.max())))
        by0, by1 = int(max(0, math.floor(y0.min()))), int(min(size - 1, math.ceil(y0.max())))
        if bx0 > bx1 or by0 > by1:
            continue
        gx, gy = np.meshgrid(np.arange(bx0, bx1 + 1) + 0.5, np.arange(by0, by1 + 1) + 0.5)
        (ax, bx_, cx_), (ay, by_, cy_) = x0, y0
        den = (by_ - cy_) * (ax - cx_) + (cx_ - bx_) * (ay - cy_)
        if abs(den) < 1e-12:
            continue
        l1 = ((by_ - cy_) * (gx - cx_) + (cx_ - bx_) * (gy - cy_)) / den
        l2 = ((cy_ - ay) * (gx - cx_) + (ax - cx_) * (gy - cy_)) / den
        l3 = 1 - l1 - l2
        inside = (l1 >= 0) & (l2 >= 0) & (l3 >= 0)
        if not inside.any():
            continue
        depth = l1 * P[t, 0, 1] + l2 * P[t, 1, 1] + l3 * P[t, 2, 1]
        sub = zb[by0:by1 + 1, bx0:bx1 + 1]
        win = inside & (depth < sub)
        sub[win] = depth[win]
        img[by0:by1 + 1, bx0:bx1 + 1][win] = 255 * shade[t]
    rgb = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8)).convert("RGB")
    d = ImageDraw.Draw(rgb)
    if pitch != 90:                              # the ground line on an elevation
        g = np.array([[lo[0] - 1, 0, 0], [hi[0] + 1, 0, 0], [0, lo[1] - 1, 0], [0, hi[1] + 1, 0]]) @ M.T
        gx_, gy_ = to_px(g)
        d.line([(gx_[0], gy_[0]), (gx_[1], gy_[1])], fill=(220, 40, 40))
        d.line([(gx_[2], gy_[2]), (gx_[3], gy_[3])], fill=(220, 40, 40))
    for b in boxes:                               # meshes that could not be found, in outline
        bx_, by_ = to_px(b @ M.T)
        for a_, c_ in [(0, 1), (2, 3), (4, 5), (6, 7), (0, 2), (1, 3), (4, 6), (5, 7), (0, 4), (1, 5), (2, 6), (3, 7)]:
            d.line([(bx_[a_], by_[a_]), (bx_[c_], by_[c_])], fill=(30, 90, 220))
    return rgb


def main(argv):
    src, out = Path(argv[0]), Path(argv[1])
    size = int(argv[argv.index("--size") + 1]) if "--size" in argv else 360
    # --mod NAME also searches "NAME - Main*.ba2" (a mod's own packed meshes) before the game's archives.
    mod_hint = argv[argv.index("--mod") + 1] if "--mod" in argv else None
    n = Nif(src.read_bytes())
    tris, missing, boxes = collect(n, mod_hint)
    pts = [tris.reshape(-1, 3)] + [b for b in boxes]
    allp = np.concatenate(pts) if len(tris) or boxes else np.zeros((1, 3))
    lo, hi = allp.min(0), allp.max(0)
    sheet = Image.new("RGB", (size * 2, size * 2 + 40), (255, 255, 255))
    for k, (label, yaw, pitch) in enumerate(VIEWS):
        im = render(tris, boxes, yaw, pitch, size, lo, hi)
        ImageDraw.Draw(im).text((6, 6), label, fill=(0, 0, 0))
        sheet.paste(im, ((k % 2) * size, (k // 2) * size))
    ImageDraw.Draw(sheet).text((6, size * 2 + 6),
        f"{src.name}  {len(tris):,} tris  x {lo[0]:.2f}..{hi[0]:.2f}  y {lo[1]:.2f}..{hi[1]:.2f}  z {lo[2]:.2f}..{hi[2]:.2f}"
        f"  missing meshes {len(missing)}", fill=(0, 0, 0))
    sheet.save(out)
    print(f"  {out}  {len(tris):,} triangles  z {lo[2]:.2f}..{hi[2]:.2f}")
    for nm, p in missing:
        print(f"  MISSING mesh for {nm}: {p}  (drawn as a blue box)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
