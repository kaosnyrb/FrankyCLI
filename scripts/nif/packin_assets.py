"""A PackIn as ONE render-ready JSON: the refs from `FrankyCLI packin export`, plus every NIF they use
walked to its geometries, meshes and first-layer textures, all EXTRACTED to a local folder the JSON points at.

    python packin_assets.py <packin.json> <out.json> [--mod <plugin name>] [--max 512]

His ask, 2026-10-09: "can we link the textures and nifs into the cache somehow? thinking one json file
that is all the info needed captured", option B: files on disk, not archive references, so a renderer,
a .glb writer or a browser needs no .ba2 code at all.

The asset cache is %TEMP%\\FrankyCLI\\asset-cache\\ (override with PACKIN_ASSETS):
    meshes/...nif, geometries/...mesh      the bytes, copied out under their own Data paths
    textures/....<max>.png                 one mip, at most --max px, as ba2get.texture decodes it
    nifs/<nif path>.json                   a NIF's walked record, reused while its sources hold
A record is reused only while the SOURCES it was read from are unchanged: every archive and the material
zip this run would search, by size and modified time. Vanilla archives never change, so in practice a
record is built once. ⚠ A NEW loose file dropped into Data over an archived one is not noticed until that
NIF's record is deleted: loose files are checked first on a build, but nothing stamps their absence.

Per geometry: `xform` is a 4x4 (row-major) with world = xform @ [x, y, z, 1] inside the NIF, from the same
walk clay.py draws with (clay.geometries). The ref's own pos/rot is NOT applied here: the axis order of a
REFR's rotation is unmeasured, and baking a guess into the cache would make a wrong answer look settled.
Anything that cannot be resolved is LISTED with its reason, never dropped, the same rule as clay.py.
Nothing here writes to the game folder, and nothing extracted may be committed (see README: Assets).
"""
from __future__ import annotations
import json, os, sys
from pathlib import Path
import ba2get, clay, smat
from sfnif import Nif

ROOT = Path(os.environ.get("PACKIN_ASSETS", Path(os.environ.get("TEMP", "/tmp")) / "FrankyCLI" / "asset-cache"))
RECORD_VERSION = 1      # bump when the shape of a nifs/*.json record changes


def stamp(p: Path) -> str:
    return f"{p.stat().st_size}|{int(p.stat().st_mtime)}" if p.exists() else "absent"


def sources(mod_hint: str | None, max_px: int) -> dict[str, str]:
    """Every file a build could read from, stamped. A record built against a different set is rebuilt."""
    arcs = set(clay.archives(mod_hint)) | set(clay.texture_index(mod_hint).values())
    if mod_hint:
        arcs |= set(ba2get.DATA.glob(f"{mod_hint} - Main*.ba2"))
    s = {a.name: stamp(a) for a in sorted(arcs)}
    s[smat.ZIP.name] = stamp(smat.ZIP)
    s["#max_px"] = str(max_px)
    s["#version"] = str(RECORD_VERSION)
    return s


def put(member: str, data: bytes) -> str:
    p = ROOT / member
    if not p.exists():
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_bytes(data)
    return p.as_posix()


def texture_file(member: str, mod_hint: str | None, max_px: int) -> str | None:
    p = ROOT / f"{member}.{max_px}.png"
    if not p.exists():
        arc = clay.texture_index(mod_hint).get(member)
        if arc is None:
            return None
        p.parent.mkdir(parents=True, exist_ok=True)
        ba2get.texture(arc, member, max_px=max_px).save(p)
    return p.as_posix()


def build(nif_member: str, mod_hint: str | None, max_px: int, src: dict) -> dict:
    rec = {"nif": None, "marker": nif_member.startswith("meshes/markers/"), "geometries": [], "missing": [],
           "sources": src}
    raw = clay.fetch(nif_member, mod_hint)
    if raw is None:
        rec["missing"].append(f"nif not found loose or in any mesh archive: {nif_member}")
        return rec
    rec["nif"] = put(nif_member, raw)
    alb_cache: dict = {}
    for g in clay.geometries(Nif(raw)):
        S, R, T = g["S"], g["R"], g["T"]
        geo = {"name": g["name"],
               "xform": [[S * R[i][0], S * R[i][1], S * R[i][2], T[i]] for i in range(3)] + [[0, 0, 0, 1]],
               "mesh": None, "material": g["material"], "albedo": None}
        if g["mesh"]:
            m = clay.mesh_member(g["mesh"])
            data = clay.fetch(m, mod_hint)
            if data is None:
                rec["missing"].append(f"{g['name']}: mesh not found: {m}")
            else:
                geo["mesh"] = put(m, data)
        else:
            rec["missing"].append(f"{g['name']}: no LOD0 mesh path")
        if g["material"]:
            if g["material"] not in alb_cache:
                alb_cache[g["material"]] = smat.albedo(g["material"], mod_hint)
            a = alb_cache[g["material"]]
            if isinstance(a, str):
                rec["missing"].append(f"{g['name']}: {a}")
            else:
                tex = texture_file(a.file, mod_hint, max_px) if a.file else None
                if a.file and tex is None:
                    rec["missing"].append(f"{g['name']}: texture not in any archive: {a.file}")
                geo["albedo"] = {"texture": tex, "texture_member": a.file, "flat": a.flat,
                                 "tint": a.tint, "scale": a.scale, "offset": a.offset,
                                 "colour": a.file is not None or a.flat is not None}
        else:
            rec["missing"].append(f"{g['name']}: geometry has no material")
        rec["geometries"].append(geo)
    return rec


def record(nif_member: str, mod_hint: str | None, max_px: int, src: dict, stats: dict) -> dict:
    path = ROOT / "nifs" / f"{nif_member}.json"
    if path.exists():
        rec = json.loads(path.read_text(encoding="utf-8"))
        files = [rec["nif"]] + [g["mesh"] for g in rec["geometries"]] + \
                [g["albedo"]["texture"] for g in rec["geometries"] if g["albedo"]]
        if rec.get("sources") == src and all(Path(f).exists() for f in files if f):
            stats["hit"] += 1
            return rec
    rec = build(nif_member, mod_hint, max_px, src)
    stats["built"] += 1
    if rec["nif"]:                       # a NIF that could not even be found is not frozen into the cache
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(rec, indent=1), encoding="utf-8")
    return rec


def nifs(refs: list) -> list[str]:
    out: list[str] = []
    for r in refs:
        if r.get("nif") and r["nif"] not in out:
            out.append(r["nif"])
        for n in nifs(r.get("refs") or []):
            if n not in out:
                out.append(n)
    return out


def main(argv: list[str]) -> int:
    src_json, out = Path(argv[0]), Path(argv[1])
    mod_hint = argv[argv.index("--mod") + 1] if "--mod" in argv else None
    max_px = int(argv[argv.index("--max") + 1]) if "--max" in argv else 512
    doc = json.loads(src_json.read_text(encoding="utf-8"))
    src = sources(mod_hint, max_px)
    stats = {"hit": 0, "built": 0}
    assets = {}
    for n in nifs(doc["refs"]):
        rec = dict(record(n, mod_hint, max_px, src, stats))
        rec.pop("sources")
        assets[n] = rec
    doc["asset_root"] = ROOT.as_posix()
    doc["assets"] = assets
    out.write_text(json.dumps(doc, indent=1), encoding="utf-8")
    geos = sum(len(a["geometries"]) for a in assets.values())
    tex = sum(1 for a in assets.values() for g in a["geometries"] if g["albedo"] and g["albedo"]["texture"])
    print(f"  {out}: {len(assets)} nifs ({stats['hit']} cached, {stats['built']} built), {geos} geometries, "
          f"{tex} textured")
    for n, a in assets.items():
        if a["marker"]:                  # an editor marker is not drawn; its reasons stay in the JSON
            continue
        for why in a["missing"]:
            print(f"  MISSING {n}: {why}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
