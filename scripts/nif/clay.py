"""CLAY RENDER of a Starfield NIF: grey, flat-shaded, four views in one PNG, so I can LOOK at a model.

    python clay.py <nif> <out.png> [--size 360] [--mod <plugin name>] [--textured]

His ask, 2026-10-08: "so can we use nifskope to give you eyes on the nifs?" NifSkope has no render-to-file
command, so this reads what NifSkope reads: the node tree (sfnif walk, the transform convention proven
against three vanilla OBNDs in kbounds.py) and each BSGeometry's LOD0 .mesh (from Data/geometries loose,
else the mod's and the game's archives). Default: shape, placement and silhouette only, in grey.
"Does it look good" stays his eye.

--textured (his ask, 2026-10-08 evening): each geometry's material -> its FIRST LAYER's albedo (smat.py)
-> the texture out of the archives (ba2get.texture, a <=512 px mip) -> sampled through the .mesh's UVs,
times the material tint, times the same flat shading. Nearest texel, sRGB values shaded as they come.
Grunge and paint-over layers are not blended, so it reads cleaner than the game; no normal maps.
A geometry whose material or texture cannot be resolved is drawn in grey and LISTED with the reason.

Views: front (from -Y), side (from +X), top (from +Z), and a 3/4 view (yaw 35, pitch 25). The z = 0
ground line is drawn in red on the elevations, so a hanging or floating mesh is visible at a glance.
A geometry whose .mesh cannot be found is LISTED, never silently skipped, and its box is drawn in outline.
"""
from __future__ import annotations
import math, struct, sys
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw
import sfnif, ba2get, smat
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


def mesh_member(rel: str) -> str:
    """A BSGeometry's LOD0 path as the archive member that holds it."""
    return "geometries/" + rel.lower().replace("\\", "/") + ".mesh"


def fetch(member: str, mod_hint: str | None) -> bytes | None:
    """A file from Data: loose first, then the mod's and the game's mesh archives."""
    loose = DATA / member
    if loose.exists():
        return loose.read_bytes()
    for ba2 in archives(mod_hint):
        if ba2 not in _IDX:
            _IDX[ba2] = ba2get.index(ba2)
        if ba2get.norm(member) in _IDX[ba2]:
            return ba2get.extract(ba2, member)
    return None


def fetch_mesh(rel: str, mod_hint: str | None) -> bytes | None:
    return fetch(mesh_member(rel), mod_hint)


def parse_mesh(d: bytes):
    ni = struct.unpack_from("<I", d, 4)[0]
    idx = np.frombuffer(d, dtype="<u2", count=ni, offset=8).reshape(-1, 3)
    o = 8 + ni * 2
    scale = struct.unpack_from("<f", d, o)[0]
    nv = struct.unpack_from("<I", d, o + 8)[0]
    q = np.frombuffer(d, dtype="<i2", count=nv * 3, offset=o + 12).reshape(nv, 3)
    return q.astype(np.float64) / 32767.0 * scale, idx.astype(np.int64)


def parse_uv(d: bytes):
    """The UV block straight after the positions: u32 count == vertexCount, then half-float (u, v) pairs.
    V is NOT flipped (measured against baked AO on five parts, bethesda/02). None if the count disagrees."""
    ni = struct.unpack_from("<I", d, 4)[0]
    o = 8 + ni * 2
    nv = struct.unpack_from("<I", d, o + 8)[0]
    o += 12 + nv * 6
    if struct.unpack_from("<I", d, o)[0] != nv:
        return None
    return np.frombuffer(d, dtype="<f2", count=nv * 2, offset=o + 4).reshape(nv, 2).astype(np.float64)


_TEX: dict[str, Path] = {}


def texture_index(mod_hint: str | None) -> dict[str, Path]:
    """member -> archive, patch archives first so they win, a mod's own textures before the game's."""
    if not _TEX:
        arcs = sorted(ba2get.DATA.glob("Starfield - TexturesPatch*.ba2"), reverse=True)
        arcs += sorted(ba2get.DATA.glob("Starfield - Textures[0-9]*.ba2"))
        if mod_hint:
            arcs = sorted(ba2get.DATA.glob(f"{mod_hint} - Textures*.ba2")) + arcs
        for a in arcs:
            if a.name.lower().endswith(("_xbox.ba2", "_ps.ba2")):
                continue                       # console archives are tiled; Pillow cannot read them
            for k in ba2get.index(a):
                _TEX.setdefault(k, a)
    return _TEX


def surface(matpath: str | None, mod_hint: str | None, cache: dict):
    """(RGB float array 0..1, Albedo) for a material, or a reason string."""
    if not matpath:
        return "geometry has no material"
    if matpath in cache:
        return cache[matpath]
    a = smat.albedo(matpath, mod_hint)
    if isinstance(a, str):
        out = a
    elif a.file is None and a.flat is None:
        out = (None, a)                        # no colour layer: not drawn, and listed
    elif a.file is None:
        out = (np.array(a.flat, dtype=np.float64).reshape(1, 1, 3), a)
    else:
        arc = texture_index(mod_hint).get(a.file)
        if arc is None:
            out = f"texture not in any archive: {a.file}"
        else:
            im = ba2get.texture(arc, a.file, max_px=512).convert("RGB")
            out = (np.asarray(im, dtype=np.float64) / 255.0, a)
    cache[matpath] = out
    return out


def geometries(n: Nif):
    """Every BSGeometry in the tree, in file order, carried through its node transforms. ONE walk, shared
    by collect() below and packin_assets.py, so the transform convention cannot fork. Each is a dict:
    name, T (world translation), R (3x3), S (uniform scale): world = S * R @ v + T; `mesh` the LOD0 path
    or None; `material` the shader's name string (the material path) or None; `box` its own centre and
    half-extents, in its local space."""
    out = []

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
        box = w.xform + 52 + 4 + 16
        sh = struct.unpack_from("<I", bl, w.refs[-2])[0]             # skin, SHADER, alpha: the shader's
        mat = None                                                  # name string is the material path
        if sh != NONE:
            si = struct.unpack_from("<I", n.blocks[sh], 0)[0]
            mat = n.strings[si] if si != NONE else None
        out.append({"name": n.strings[nm_i] if nm_i != NONE else "(unnamed)", "T": WT, "R": WR, "S": WS,
                    "mesh": path, "material": mat,
                    "box": (struct.unpack_from("<3f", bl, box), struct.unpack_from("<3f", bl, box + 12))})

    I = [[1, 0, 0], [0, 1, 0], [0, 0, 1]]
    visit(0, [0, 0, 0], I, 1.0)
    return out


def collect(n: Nif, mod_hint: str | None, textured: bool = False):
    """(triangles, missing meshes, boxes, surfaces). `surfaces` is per-triangle-run: (count, uv (N,3,2)
    or None, texture or None, Albedo or None, geometry name, reason); filled only when `textured`."""
    tris, missing, boxes, surfs = [], [], [], []
    cache: dict = {}
    for g in geometries(n):
        nm, WT, WR, WS = g["name"], g["T"], g["R"], g["S"]
        raw = fetch_mesh(g["mesh"], mod_hint) if g["mesh"] else None
        if raw is None:
            missing.append((nm, g["mesh"]))
            c, d = np.array(g["box"][0]), np.array(g["box"][1])
            corners = np.array([[c[0] + sx * d[0], c[1] + sy * d[1], c[2] + sz * d[2]]
                                for sx in (-1, 1) for sy in (-1, 1) for sz in (-1, 1)])
            boxes.append(corners @ (np.array(WR).T * WS) + np.array(WT))
            continue
        v, f = parse_mesh(raw)
        world = v @ (np.array(WR).T * WS) + np.array(WT)
        tris.append(world[f])
        if textured:
            uv = parse_uv(raw)
            s = surface(g["material"], mod_hint, cache)
            if uv is None:
                surfs.append((len(f), None, None, None, nm, "the .mesh has no UV block"))
            elif isinstance(s, str):
                surfs.append((len(f), None, None, None, nm, s))
            else:
                surfs.append((len(f), uv[f], s[0], s[1], nm, None))
    return (np.concatenate(tris) if tris else np.zeros((0, 3, 3))), missing, boxes, surfs


def view_matrix(yaw, pitch):
    y, p = math.radians(yaw), math.radians(pitch)
    Rz = np.array([[math.cos(y), -math.sin(y), 0], [math.sin(y), math.cos(y), 0], [0, 0, 1]])
    Rx = np.array([[1, 0, 0], [0, math.cos(p), -math.sin(p)], [0, math.sin(p), math.cos(p)]])
    return Rx @ Rz            # camera looks along +Y after this; screen x = X, screen up = Z


VIEWS = [("front", 0, 0), ("side", -90, 0), ("top", 0, 90), ("3/4", 35, 25)]


def flatten(surfs):
    """Per-triangle (uv (N,3,2), texture id (N,), textures [(array, tint, scale, offset)]);
    id -1 = grey (unresolved), -2 = not drawn (the material has no colour layer)."""
    n = sum(s[0] for s in surfs)
    uv, tid, texs, ids = np.zeros((n, 3, 2)), np.full(n, -1), [], {}
    k = 0
    for count, u, arr, alb, _, _ in surfs:
        if arr is None and alb is not None:
            tid[k:k + count] = -2
        elif arr is not None:
            key = id(arr), alb.tint, alb.scale, alb.offset
            if key not in ids:
                ids[key] = len(texs)
                texs.append((arr, np.array(alb.tint), np.array(alb.scale), np.array(alb.offset)))
            uv[k:k + count], tid[k:k + count] = u, ids[key]
        k += count
    return uv, tid, texs


def shading(tris):
    """Per-triangle flat shade from the world-space normal, both faces lit."""
    light = np.array([0.35, -0.6, 0.72]); light /= np.linalg.norm(light)
    nrm = np.cross(tris[:, 1] - tris[:, 0], tris[:, 2] - tris[:, 0])
    ln = np.linalg.norm(nrm, axis=1); ok = ln > 1e-12
    shade = np.full(len(tris), 0.6); shade[ok] = 0.25 + 0.7 * np.abs((nrm[ok] / ln[ok, None]) @ light)
    return shade


def raster(img, zb, xs, ys, depth, shade, tex=None, invw=None):
    """THE triangle fill, shared by the orthographic views and packin_render's perspective camera.
    xs, ys (N,3) pixel coords; depth (N,3) per vertex, smaller is nearer; tex as flatten() returns.
    invw (N,3) = 1/view-depth for a PERSPECTIVE projection, so depth and UVs interpolate correctly;
    None for orthographic, which keeps the plain (and byte-identical to before) linear interpolation."""
    H, W = zb.shape
    for t in range(len(xs)):
        if tex is not None and tex[1][t] == -2:
            continue
        x0, y0 = xs[t], ys[t]
        bx0, bx1 = int(max(0, math.floor(x0.min()))), int(min(W - 1, math.ceil(x0.max())))
        by0, by1 = int(max(0, math.floor(y0.min()))), int(min(H - 1, math.ceil(y0.max())))
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
        if invw is None:
            def lerp(a): return l1 * a[0] + l2 * a[1] + l3 * a[2]
        else:
            q = l1 * invw[t, 0] + l2 * invw[t, 1] + l3 * invw[t, 2]
            def lerp(a, w=invw[t]): return (l1 * a[0] * w[0] + l2 * a[1] * w[1] + l3 * a[2] * w[2]) / q
        d = lerp(depth[t])
        sub = zb[by0:by1 + 1, bx0:bx1 + 1]
        win = inside & (d < sub)
        sub[win] = d[win]
        if tex is not None and tex[1][t] >= 0:
            arr, tint, sc, of = tex[2][tex[1][t]]
            u = lerp(tex[0][t, :, 0])[win] * sc[0] + of[0]
            v = lerp(tex[0][t, :, 1])[win] * sc[1] + of[1]
            th, tw = arr.shape[:2]
            col = arr[(np.floor(v * th).astype(np.int64) % th), (np.floor(u * tw).astype(np.int64) % tw)]
            img[by0:by1 + 1, bx0:bx1 + 1][win] = 255 * col * tint * shade[t]
        else:
            img[by0:by1 + 1, bx0:bx1 + 1][win] = 255 * shade[t]


def render(tris, boxes, yaw, pitch, size, lo, hi, tex=None):
    M = view_matrix(yaw, pitch)
    P = tris @ M.T
    allpts = np.array([[x, y, z] for x in (lo[0], hi[0]) for y in (lo[1], hi[1]) for z in (lo[2], hi[2])]) @ M.T
    smin, smax = allpts.min(0), allpts.max(0)
    span = max(smax[0] - smin[0], smax[2] - smin[2]) * 1.1 or 1.0
    cx, cz = (smin[0] + smax[0]) / 2, (smin[2] + smax[2]) / 2
    k = size / span

    def to_px(p):
        return (p[..., 0] - cx) * k + size / 2, size / 2 - (p[..., 2] - cz) * k

    img = np.full((size, size, 3), 235.0)
    zb = np.full((size, size), np.inf)
    xs, ys = to_px(P)
    raster(img, zb, xs, ys, P[..., 1], shading(tris), tex)
    rgb = Image.fromarray(np.clip(img, 0, 255).astype(np.uint8))
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


def contact_sheet(tris, boxes, tex, size, caption):
    """The four VIEWS on one sheet with a caption line; shared by main() and packin_render.py.
    Returns (image, lo, hi), the bounds every view was framed on."""
    pts = [tris.reshape(-1, 3)] + [b for b in boxes]
    allp = np.concatenate(pts) if len(tris) or boxes else np.zeros((1, 3))
    lo, hi = allp.min(0), allp.max(0)
    sheet = Image.new("RGB", (size * 2, size * 2 + 40), (255, 255, 255))
    for k, (label, yaw, pitch) in enumerate(VIEWS):
        im = render(tris, boxes, yaw, pitch, size, lo, hi, tex)
        ImageDraw.Draw(im).text((6, 6), label, fill=(0, 0, 0))
        sheet.paste(im, ((k % 2) * size, (k // 2) * size))
    ImageDraw.Draw(sheet).text((6, size * 2 + 6),
        f"{caption}  {len(tris):,} tris  x {lo[0]:.2f}..{hi[0]:.2f}  y {lo[1]:.2f}..{hi[1]:.2f}  z {lo[2]:.2f}..{hi[2]:.2f}",
        fill=(0, 0, 0))
    return sheet, lo, hi


def main(argv):
    src, out = Path(argv[0]), Path(argv[1])
    size = int(argv[argv.index("--size") + 1]) if "--size" in argv else 360
    # --mod NAME also searches "NAME - Main*.ba2" (a mod's own packed meshes) before the game's archives.
    mod_hint = argv[argv.index("--mod") + 1] if "--mod" in argv else None
    textured = "--textured" in argv
    n = Nif(src.read_bytes())
    tris, missing, boxes, surfs = collect(n, mod_hint, textured)
    tex = flatten(surfs) if textured else None
    sheet, lo, hi = contact_sheet(tris, boxes, tex, size, f"{src.name}  missing meshes {len(missing)}")
    sheet.save(out)
    print(f"  {out}  {len(tris):,} triangles  z {lo[2]:.2f}..{hi[2]:.2f}")
    for nm, p in missing:
        print(f"  MISSING mesh for {nm}: {p}  (drawn as a blue box)")
    if textured:
        bare = [s for s in surfs if s[2] is None and s[3] is not None]
        done = sum(1 for s in surfs if s[2] is not None)
        print(f"  textured {done} of {len(surfs)} geometries, {len(bare)} with no colour layer")
        for _, _, _, _, nm, why in surfs:
            if why:
                print(f"  UNTEXTURED {nm}: {why}  (drawn in grey)")
        for _, _, _, _, nm, _ in bare:
            print(f"  NOT DRAWN {nm}: its material has no colour layer (a normal-only decal or an effect)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
