"""What a Starfield material LOOKS like, at the level a preview needs: its first layer's albedo.

    python smat.py <material path> [--mod <plugin name>]

Sources, first hit wins: a mod's own `<mod> - Main*.ba2`, then the Creation Kit's raw material sources in
`Tools/ContentResources.zip` (48k `.mat` JSON files; the game itself ships them only compiled, inside
`materials/materialsbeta.cdb`, which nothing here reads). Override the zip with SF_MATERIALS.

A `.mat` carries a `Summary` block, Bethesda's own resolved account of each layer: albedo file or flat
replacement, tint, UV scale and offset. This reads `Summary.Layer1`; where its `Textures` is null the layer
inherits, so it follows the layer's `Parent` material (child fields win). ONLY LAYER 1: grunge, dirt and
paint-over layers are blended by masks this does not evaluate, so a render reads cleaner than the game.

⚠ The zip is the CK's copy and carries its own date; a game patch after it can change a material this
copy does not know about. A preview, not evidence of what ships.
"""
from __future__ import annotations
import json, os, sys, zipfile
from dataclasses import dataclass
from pathlib import Path
import ba2get

ZIP = Path(os.environ.get("SF_MATERIALS", ba2get.DATA.parent / "Tools" / "ContentResources.zip"))
_zip: zipfile.ZipFile | None = None
_zipnames: dict[str, str] = {}
_mod: dict[Path, dict[str, int]] = {}


@dataclass
class Albedo:
    """Three states, explicit: `file` set = a texture; `flat` set = a constant colour; BOTH None = the
    layer has no colour at all (a normal-only decal: bolts, bevels, trim lines draw only into the normal
    map), so there is nothing to paint and a preview should not invent a colour for it."""
    file: str | None              # a `textures/...` member path
    flat: tuple | None            # RGB 0..1 when the layer replaces its texture with a constant
    tint: tuple                   # RGB 0..1, multiplied in
    scale: tuple                  # UV scale
    offset: tuple                 # UV offset


def norm_mat(p: str) -> str:
    p = ba2get.norm(p).lstrip("/")
    if p.startswith("data/"):
        p = p[5:]
    return p if p.startswith("materials/") else "materials/" + p


def norm_tex(p: str) -> str:
    p = ba2get.norm(p).lstrip("/")
    if p.startswith("data/"):
        p = p[5:]
    return p if p.startswith("textures/") else "textures/" + p


def read_mat(path: str, mod_hint: str | None) -> dict | None:
    global _zip
    key = norm_mat(path)
    if mod_hint:
        for ba2 in sorted(ba2get.DATA.glob(f"{mod_hint} - Main*.ba2")):
            if ba2 not in _mod:
                _mod[ba2] = ba2get.index(ba2)
            if key in _mod[ba2]:
                return json.loads(ba2get.extract(ba2, key))
    if _zip is None:
        if not ZIP.exists():
            raise SystemExit(f"no material sources at {ZIP} (set SF_MATERIALS)")
        _zip = zipfile.ZipFile(ZIP)
        _zipnames.update({ba2get.norm(n): n for n in _zip.namelist()})
    if key in _zipnames:
        return json.loads(_zip.read(_zipnames[key]))
    return None


def _xyz(c, default):
    return (c["x"], c["y"], c["z"]) if c else default


def albedo(path: str, mod_hint: str | None = None) -> Albedo | str:
    """The first layer's albedo, or a one-line REASON it could not be resolved (never a guess)."""
    tint = scale = offset = None
    seen = []
    while True:
        if path in seen or len(seen) > 8:
            return f"parent chain loops or runs past 8: {' -> '.join(seen)}"
        seen.append(path)
        d = read_mat(path, mod_hint)
        if d is None:
            return f"material not found: {path}"
        lay = (d.get("Summary") or {}).get("Layer1")
        if lay is None:
            return f"no Summary.Layer1 in {path}"
        tint = tint or _xyz(lay.get("Tint"), None)
        uv = lay.get("UVStream") or {}
        scale = scale or ((uv["Scale"]["x"], uv["Scale"]["y"]) if uv.get("Scale") else None)
        offset = offset or ((uv["Offset"]["x"], uv["Offset"]["y"]) if uv.get("Offset") else None)
        alb = (lay.get("Textures") or {}).get("Albedo")
        if alb:
            if alb.get("UseReplacement"):
                return Albedo(None, _xyz(alb.get("Replacement"), (1, 1, 1)), tint or (1, 1, 1), scale or (1, 1), offset or (0, 0))
            if not alb.get("File"):
                return Albedo(None, None, tint or (1, 1, 1), scale or (1, 1), offset or (0, 0))
            return Albedo(norm_tex(alb["File"]), None, tint or (1, 1, 1), scale or (1, 1), offset or (0, 0))
        parent = lay.get("Parent")
        if not parent:
            return f"Layer1 has no albedo and no parent in {path}"
        path = parent


def main(argv):
    mod = argv[argv.index("--mod") + 1] if "--mod" in argv else None
    print(albedo(argv[0], mod))
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
