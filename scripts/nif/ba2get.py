"""Pull ONE file out of a BTDX archive, by path. Read-only on the archive.

    python ba2get.py <archive.ba2> <member path> <out file>
    python ba2get.py --vanilla <member path> <out file>   # search the game's mesh archives, patch first
    python ba2get.py --texture <archive.ba2> <member path> <out.png> [--max 1024]

GNRL (v2): header 24 B (magic, version, type, count@12, nameTableOffset@16 u64) + 8 B, then
`count` records of 36 B in name-table order: nameHash u32, ext 4s, dirHash u32, flags u32,
offset u64, packedSize u32, size u32, align u32. packedSize 0 = stored; else zlib.

DX10 (textures; v2 = mods, v3 = the game's own): the same 24 B header + 8 B, plus a u32 COMPRESSION
METHOD on v3 (0 zlib, 3 LZ4 block; the game's archives are 3). Records are VARIABLE length: 24 B
(nameHash, ext, dirHash, u8, chunkCount u8, chunkHeaderSize u16, height u16, width u16, mips u8,
DXGI format u8, cubemap u8, tile u8), then chunkCount x 24 B (offset u64, packedSize u32, size u32,
firstMip u16, lastMip u16, align u32). The DDS header is NOT stored; it is rebuilt from the record.
Chunks split by mip, smallest mips last, so `texture()` decompresses only the chunk it needs: that is
what makes a pure-Python LZ4 decoder fast enough, and keeps this folder at Python 3 + numpy + Pillow.
"""
from __future__ import annotations
import io, struct, sys, zlib
from pathlib import Path

import os
# The game's Data folder. Override with SF_DATA for a non-default Steam library.
DATA = Path(os.environ.get("SF_DATA", r"C:/Program Files (x86)/Steam/steamapps/common/Starfield/Data"))
# Newest first: a patch archive overrides the base archives it ships after.
VANILLA = ["Starfield - MeshesPatch.ba2", "Starfield - Meshes02.ba2", "Starfield - Meshes01.ba2"]
REC = 36


def norm(p: str) -> str:
    return p.lower().replace("\\", "/")


def header(ba2: Path) -> tuple[bytes, int, int]:
    """(type, version, compression method). Refuses anything this file has not been taught."""
    with ba2.open("rb") as f:
        head = f.read(36)
    kind, ver = head[8:12], struct.unpack_from("<I", head, 4)[0]
    if head[0:4] != b"BTDX" or kind not in (b"GNRL", b"DX10"):
        raise SystemExit(f"{ba2.name}: not a BTDX GNRL or DX10 archive")
    if (kind, ver) not in ((b"GNRL", 2), (b"DX10", 2), (b"DX10", 3)):
        raise SystemExit(f"{ba2.name}: BTDX {kind.decode()} version {ver}, this reader knows GNRL 2, DX10 2 and 3")
    comp = struct.unpack_from("<I", head, 32)[0] if ver == 3 else 0
    if comp not in (0, 3):
        raise SystemExit(f"{ba2.name}: compression method {comp}, this reader knows 0 (zlib) and 3 (LZ4)")
    return kind, ver, comp


def index(ba2: Path) -> dict[str, int]:
    header(ba2)                              # the name table is the same shape in both types
    with ba2.open("rb") as f:
        head = f.read(24)
        count = struct.unpack_from("<I", head, 12)[0]
        f.seek(struct.unpack_from("<Q", head, 16)[0])
        out = {}
        for i in range(count):
            ln = struct.unpack_from("<H", f.read(2))[0]
            out[norm(f.read(ln).decode("ascii"))] = i
    return out


def extract(ba2: Path, member: str) -> bytes | None:
    if header(ba2)[0] != b"GNRL":
        raise SystemExit(f"{ba2.name}: a DX10 archive; use texture()")
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


def lz4_block(src: bytes, size: int) -> bytes:
    """An LZ4 BLOCK (no frame) decoder. The record's unpacked size is the check: a misread stream
    cannot land on it by accident."""
    dst, i, n = bytearray(), 0, len(src)
    while i < n:
        tok = src[i]; i += 1
        lit = tok >> 4
        if lit == 15:
            while True:
                b = src[i]; i += 1; lit += b
                if b != 255: break
        dst += src[i:i + lit]; i += lit
        if i >= n:
            break
        off = src[i] | src[i + 1] << 8; i += 2
        ml = tok & 15
        if ml == 15:
            while True:
                b = src[i]; i += 1; ml += b
                if b != 255: break
        ml += 4
        if off == 0 or off > len(dst):
            raise SystemExit(f"LZ4: match offset {off} outside {len(dst)} B of output -- stream misread")
        start = len(dst) - off
        if off >= ml:
            dst += dst[start:start + ml]
        else:                                # an overlapping match repeats its last `off` bytes
            dst += (dst[start:] * (ml // off + 1))[:ml]
    if len(dst) != size:
        raise SystemExit(f"LZ4: decoded {len(dst)} B, record says {size}")
    return bytes(dst)


# Bytes per 4x4 block for block-compressed DXGI formats, bytes per pixel for the plain ones.
# Anything else is refused: a wrong stride decodes to a plausible-looking wrong image.
_BLOCK = {**{f: 8 for f in (70, 71, 72, 79, 80, 81)}, **{f: 16 for f in (73, 74, 75, 76, 77, 78, 82, 83, 84, 94, 95, 96, 97, 98, 99)}}
_PIXEL = {28: 4, 29: 4, 87: 4, 91: 4, 61: 1}


def mip_bytes(fmt: int, w: int, h: int) -> int:
    if fmt in _BLOCK:
        return max(1, (w + 3) // 4) * max(1, (h + 3) // 4) * _BLOCK[fmt]
    if fmt in _PIXEL:
        return w * h * _PIXEL[fmt]
    raise SystemExit(f"DXGI format {fmt} is not in this reader's stride table")


# BC1-BC5 go out under their legacy FourCC: Pillow 10.0 decodes them that way and refuses them behind a
# DX10 header ("Unimplemented DXGI format 72"). Everything else goes out as DX10.
_FOURCC = {**dict.fromkeys((70, 71, 72), b"DXT1"), **dict.fromkeys((73, 74, 75), b"DXT3"),
           **dict.fromkeys((76, 77, 78), b"DXT5"), **dict.fromkeys((79, 80), b"ATI1"), **dict.fromkeys((82, 83), b"ATI2")}


def dds_file(fmt: int, w: int, h: int, data: bytes) -> bytes:
    """A one-mip DDS around raw surface bytes."""
    cc = _FOURCC.get(fmt, b"DX10")
    pf = struct.pack("<II4s5I", 32, 0x4, cc, 0, 0, 0, 0, 0)
    hd = struct.pack("<7I44s32s4I4x", 124, 0x1 | 0x2 | 0x4 | 0x1000 | 0x80000, h, w, len(data), 0, 1,
                     b"\0" * 44, pf, 0x1000, 0, 0, 0)
    ext = struct.pack("<5I", fmt, 3, 0, 1, 0) if cc == b"DX10" else b""
    return b"DDS " + hd + ext + data


def texture_record(ba2: Path, member: str):
    """(width, height, mips, DXGI format, [(offset, packed, size, firstMip, lastMip)]) or None."""
    kind, ver, comp = header(ba2)
    if kind != b"DX10":
        raise SystemExit(f"{ba2.name}: not a DX10 archive")
    i = index(ba2).get(norm(member))
    if i is None:
        return None
    with ba2.open("rb") as f:
        f.seek(36 if ver == 3 else 32)
        for k in range(i + 1):                 # variable-length records: walk to the i-th
            r = f.read(24)
            nchunk, chs = r[13], struct.unpack_from("<H", r, 14)[0]
            if chs != 24:
                raise SystemExit(f"{ba2.name}: chunk header size {chs}, expected 24 -- layout misread")
            chunks = [struct.unpack("<QIIHHI", f.read(24)) for _ in range(nchunk)]
        h, w = struct.unpack_from("<HH", r, 16)
        mips, fmt = r[20], r[21]
    for c in chunks:
        if c[5] != 0xBAADF00D:
            raise SystemExit(f"{member}: chunk align {c[5]:#x}, expected 0xBAADF00D -- layout misread")
    return w, h, mips, fmt, [c[:5] for c in chunks]


def texture(ba2: Path, member: str, max_px: int = 1024, mip: int | None = None):
    """One mip of a texture as a Pillow image: the largest mip no wider or taller than `max_px` (or
    exactly `mip`), decompressing only the chunk that holds it. None if the member is absent."""
    from PIL import Image
    rec = texture_record(ba2, member)
    if rec is None:
        return None
    w, h, mips, fmt, chunks = rec
    if mip is None:
        mip = next((m for m in range(mips) if max(w >> m, h >> m) <= max_px), mips - 1)
    chunk = next((c for c in chunks if c[3] <= mip <= c[4]), None)
    if chunk is None:
        raise SystemExit(f"{member}: no chunk holds mip {mip}")
    off, packed, size, m0, m1 = chunk
    _, _, comp = header(ba2)
    with ba2.open("rb") as f:
        f.seek(off)
        raw = f.read(packed or size)
    data = raw if not packed else (lz4_block(raw, size) if comp == 3 else zlib.decompress(raw))
    if len(data) != size:
        raise SystemExit(f"{member}: chunk is {len(data)} B, record says {size}")
    skip = sum(mip_bytes(fmt, max(1, w >> m), max(1, h >> m)) for m in range(m0, mip))
    mw, mh = max(1, w >> mip), max(1, h >> mip)
    surf = data[skip:skip + mip_bytes(fmt, mw, mh)]
    return Image.open(io.BytesIO(dds_file(fmt, mw, mh, surf)))


def main(argv: list[str]) -> int:
    if argv[0] == "--texture":
        ba2, member, out = Path(argv[1]), argv[2], Path(argv[3])
        mx = int(argv[argv.index("--max") + 1]) if "--max" in argv else 1024
        im = texture(ba2, member, mx)
        if im is None:
            print(f"  {member}: not in {ba2.name} (searched)")
            return 1
        im.save(out)
        print(f"  {member} <- {ba2.name}  {im.size[0]}x{im.size[1]} {im.mode}")
        return 0
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
