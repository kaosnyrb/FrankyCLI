"""Pull ONE file out of a BTDX v2 GNRL archive, by path. Read-only on the archive.

    python ba2get.py <archive.ba2> <member path> <out file>
    python ba2get.py --vanilla <member path> <out file>   # search the game's mesh archives, patch first

Layout (v2): header 24 B (magic, version, type, count@12, nameTableOffset@16 u64) + 8 B, then
`count` records of 36 B in name-table order: nameHash u32, ext 4s, dirHash u32, flags u32,
offset u64, packedSize u32, size u32, align u32. packedSize 0 = stored; else zlib.
"""
from __future__ import annotations
import struct, sys, zlib
from pathlib import Path

import os
# The game's Data folder. Override with SF_DATA for a non-default Steam library.
DATA = Path(os.environ.get("SF_DATA", r"C:/Program Files (x86)/Steam/steamapps/common/Starfield/Data"))
# Newest first: a patch archive overrides the base archives it ships after.
VANILLA = ["Starfield - MeshesPatch.ba2", "Starfield - Meshes02.ba2", "Starfield - Meshes01.ba2"]
REC = 36


def norm(p: str) -> str:
    return p.lower().replace("\\", "/")


def index(ba2: Path) -> dict[str, int]:
    with ba2.open("rb") as f:
        head = f.read(32)
        if head[0:4] != b"BTDX" or head[8:12] != b"GNRL":
            raise SystemExit(f"{ba2.name}: not a BTDX GNRL archive")
        ver = struct.unpack_from("<I", head, 4)[0]
        if ver != 2:
            raise SystemExit(f"{ba2.name}: BTDX version {ver}, this reader knows 2 only")
        count = struct.unpack_from("<I", head, 12)[0]
        f.seek(struct.unpack_from("<Q", head, 16)[0])
        out = {}
        for i in range(count):
            ln = struct.unpack_from("<H", f.read(2))[0]
            out[norm(f.read(ln).decode("ascii"))] = i
    return out


def extract(ba2: Path, member: str) -> bytes | None:
    i = index(ba2).get(norm(member))
    if i is None:
        return None
    with ba2.open("rb") as f:
        f.seek(32 + i * REC)
        r = f.read(REC)
        off, packed, size, align = struct.unpack_from("<QIII", r, 16)
        if align != 0xBAADF00D:
            raise SystemExit(f"{ba2.name}: record {i} align {align:#x}, expected 0xBAADF00D -- layout misread")
        f.seek(off)
        data = zlib.decompress(f.read(packed)) if packed else f.read(size)
    if len(data) != size:
        raise SystemExit(f"{member}: got {len(data)} B, record says {size}")
    return data


def main(argv: list[str]) -> int:
    if argv[0] == "--vanilla":
        member, out = argv[1], Path(argv[2])
        for name in VANILLA:
            data = extract(DATA / name, member)
            if data is not None:
                out.write_bytes(data)
                print(f"  {member} <- {name}  ({len(data):,} B)")
                return 0
        print(f"  {member}: in none of {', '.join(VANILLA)} -- all three WERE searched")
        return 1
    ba2, member, out = Path(argv[0]), argv[1], Path(argv[2])
    data = extract(ba2, member)
    if data is None:
        print(f"  {member}: not in {ba2.name} (searched)")
        return 1
    out.write_bytes(data)
    print(f"  {member} <- {ba2.name}  ({len(data):,} B)")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
