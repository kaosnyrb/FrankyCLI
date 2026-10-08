"""Kitbash Starfield NIFs without NifSkope (2026-10-08).

His ask: "have a look at kitbashing new models yourself instead of me". A kitbash is N vanilla NIFs
hung under one new root NiNode, each with a placement transform -- measured off five of his own
(machinebase, longrangescan, fuel, tradeauth, nest): one CLOSED set of 8 block types, one NiNode +
BSXFlags + collision per pasted object. So this is a MERGE with re-indexing, not general NIF editing.

    python sfnif.py --walk <nif>...                 # field-walk every block; exit 1 if any size disagrees
    python sfnif.py --roundtrip <nif>...            # serialise(parse(x)) == x, byte for byte
    python sfnif.py --merge spec.json out.nif       # build a kitbash

Layouts are NifSkope's nif.xml (github.com/fo76utils/nifskope, build/nif.xml) at BSVER >= 170 (Starfield). Any other block type
is REFUSED, never passed through blind: a block we cannot walk is a block whose links we cannot
re-index, and an un-reindexed link points into somebody else's geometry.
"""
from __future__ import annotations
import json, math, struct, sys
from pathlib import Path



class Nif:
    """A Starfield NIF as header fields + OPAQUE block bytes + footer. `serialise` below is its exact
    inverse, and test_nif.py proves the pair byte-identical on every fixture: that round trip is what
    licenses this reader, not resemblance to any other one. (Same layout as avontech_stardust's
    nif_from_template.Nif, which this replaced here so FrankyCLI does not import from another repo.)"""

    def __init__(self, raw: bytes):
        self.raw = raw
        o = 0

        def u32():
            nonlocal o
            v = struct.unpack_from("<I", raw, o)[0]; o += 4; return v

        def u16():
            nonlocal o
            v = struct.unpack_from("<H", raw, o)[0]; o += 2; return v

        def sized():
            nonlocal o
            n = u32(); v = raw[o:o + n].decode("utf-8"); o += n; return v

        def u8str():
            nonlocal o
            n = raw[o]; o += 1; v = raw[o:o + n]; o += n; return v

        self.magic = raw[o:o + 38]; o += 38
        self.version = raw[o:o + 5]; o += 5
        self.endian = raw[o]; o += 1
        self.user_version = u32()
        num_blocks = u32()
        self.bs_version = u32()
        self.author = u8str()
        self.unk1 = u32()
        self.process_script = u8str()
        self.unk2 = u8str()
        self.types = [sized() for _ in range(u16())]
        self.type_indices = [u16() for _ in range(num_blocks)]
        self.sizes = [u32() for _ in range(num_blocks)]
        num_strings = u32()
        self.max_string = u32()
        self.strings = [sized() for _ in range(num_strings)]
        if u32():
            raise SystemExit("REFUSED: num_groups != 0 (game NIFs are always 0)")
        self.blocks: list[bytes] = []
        for size in self.sizes:
            self.blocks.append(raw[o:o + size]); o += size
        self.footer = raw[o:]

    def type_of(self, i: int) -> str:
        return self.types[self.type_indices[i]]


NONE = 0xFFFFFFFF
KNOWN = {"NiNode", "BSGeometry", "BSLightingShaderProperty", "NiIntegerExtraData", "BSXFlags",
         "NiStringExtraData", "bhkNPCollisionObject", "bhkPhysicsSystem"}


class Walk:
    """Field offsets inside one block: strs (u32 string index), refs (i32 block link), xform (offset of
    the NiAVObject translation), and how many bytes the layout consumed."""
    def __init__(self):
        self.strs: list[int] = []
        self.refs: list[int] = []
        self.xform: int | None = None
        self.used = 0


def walk(t: str, b: bytes) -> Walk:
    if t not in KNOWN:
        raise SystemExit(f"REFUSED: block type {t!r} is outside the 8 this tool can re-index")
    w = Walk(); o = 0

    def u32():
        nonlocal o
        v = struct.unpack_from("<I", b, o)[0]; o += 4; return v

    def s():            # a header-string index
        nonlocal o
        w.strs.append(o); o += 4

    def r():            # a block link (Ref or Ptr)
        nonlocal o
        w.refs.append(o); o += 4

    def net():          # NiObjectNET
        s()
        for _ in range(u32()): r()
        r()

    def av():           # NiAVObject (BSVER > 26: u32 flags; no properties list)
        nonlocal o
        net()
        flags = u32()
        w.xform = o
        o += 12 + 36 + 4    # translation, rotation 3x3, scale
        r()                 # collision object
        return flags

    if t == "NiNode":
        av()
        for _ in range(u32()): r()
    elif t == "BSGeometry":
        flags = av()
        if flags & 512:
            raise SystemExit("REFUSED: BSGeometry with internal mesh data (flag 512); vanilla references .mesh files")
        o += 16 + 24        # NiBound, BSBoundingBox
        r(); r(); r()       # skin, shader property, alpha property
        for _ in range(4):
            has = b[o]; o += 1
            if has == 1:
                o += 12     # indices size, num verts, flags
                o += 4 + struct.unpack_from("<I", b, o)[0]   # SizedString mesh path, inline
            elif has != 0:
                raise SystemExit(f"REFUSED: BSMeshArray Has Mesh = {has}")
    elif t == "BSLightingShaderProperty":
        net()               # at BSVER >= 170 every other field is version-gated out
    elif t in ("NiIntegerExtraData", "BSXFlags"):
        s(); o += 4
    elif t == "NiStringExtraData":
        s(); s()
    elif t == "bhkNPCollisionObject":
        r(); o += 2; r(); o += 4    # target ptr, flags u16, data ref, body id
    elif t == "bhkPhysicsSystem":
        o += 4 + struct.unpack_from("<I", b, o)[0]
    w.used = o
    return w


def serialise(n: Nif) -> bytes:
    out = bytearray()
    out += n.magic + n.version + bytes([n.endian])
    out += struct.pack("<III", n.user_version, len(n.blocks), n.bs_version)
    for v in (n.author,):
        out += bytes([len(v)]) + v
    out += struct.pack("<I", n.unk1)
    out += bytes([len(n.process_script)]) + n.process_script
    out += bytes([len(n.unk2)]) + n.unk2
    out += struct.pack("<H", len(n.types))
    for t in n.types:
        e = t.encode("utf-8"); out += struct.pack("<I", len(e)) + e
    out += b"".join(struct.pack("<H", i) for i in n.type_indices)
    out += b"".join(struct.pack("<I", len(bl)) for bl in n.blocks)
    out += struct.pack("<II", len(n.strings), n.max_string)
    for st in n.strings:
        e = st.encode("utf-8"); out += struct.pack("<I", len(e)) + e
    out += struct.pack("<I", 0)
    out += b"".join(n.blocks)
    out += n.footer
    return bytes(out)


def check_walk(path: Path) -> int:
    n = Nif(path.read_bytes()); bad = 0
    for i, bl in enumerate(n.blocks):
        t = n.type_of(i); w = walk(t, bl)
        if w.used != len(bl):
            print(f"  {path.name} block {i} {t}: layout used {w.used} of {len(bl)} B"); bad += 1
        for off in w.strs:
            v = struct.unpack_from("<I", bl, off)[0]
            if v != NONE and v >= len(n.strings):
                print(f"  {path.name} block {i} {t}: string index {v} out of {len(n.strings)}"); bad += 1
        for off in w.refs:
            v = struct.unpack_from("<I", bl, off)[0]
            if v != NONE and v >= len(n.blocks):
                print(f"  {path.name} block {i} {t}: link {v} out of {len(n.blocks)}"); bad += 1
    print(f"  walk {path.name}: {len(n.blocks)} blocks, {bad} problem(s)")
    return bad


# ------------------------------------------------------------------ the merge

def rot_xyz(deg):
    """Rows of R = Rz @ Ry @ Rx, degrees. ⚠ Only Z (yaw) is exercised so far."""
    x, y, z = (math.radians(a) for a in deg)
    cx, sx, cy, sy, cz, sz = math.cos(x), math.sin(x), math.cos(y), math.sin(y), math.cos(z), math.sin(z)
    Rx = [[1, 0, 0], [0, cx, -sx], [0, sx, cx]]
    Ry = [[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]]
    Rz = [[cz, -sz, 0], [sz, cz, 0], [0, 0, 1]]
    mm = lambda A, B: [[sum(A[i][k] * B[k][j] for k in range(3)) for j in range(3)] for i in range(3)]
    return mm(Rz, mm(Ry, Rx))


def merge(spec: dict, out_path: Path) -> None:
    parts = spec["parts"]
    first = Nif(Path(parts[0]["nif"]).read_bytes())
    types: list[str] = []
    strings: list[str] = []
    blocks: list[bytes] = []
    tix: list[int] = []

    def ti(t):
        if t not in types: types.append(t)
        return types.index(t)

    def si(st):
        if st not in strings: strings.append(st)
        return strings.index(st)

    # HIS SHAPE, measured off all ten of his kitbashes: the root is the FIRST object's own vanilla root
    # (named GenericMachines_A01, HiveNestMedium01, ...; flags 0xe), and every other object is a branch
    # pasted under it. So part 0 is the base, unplaced, and parts 1..n become children of its root.
    root_children: list[int] = []
    if any(k in parts[0] for k in ("translate", "rotate", "scale")):
        raise SystemExit("REFUSED: part 0 is the BASE (its root is the file's root); place the others relative to it")

    for p in parts:
        src = Nif(Path(p["nif"]).read_bytes())
        if src.footer != struct.pack("<II", 1, 0):
            raise SystemExit(f"REFUSED: {p['nif']} footer is not one root at block 0")
        base = len(blocks)
        # "collision": false drops the part's Havok blocks. Measured 2026-10-08 in game: collision does
        # NOT follow a NiNode's scale, so a quarter-scale dish kept a full-size invisible collider.
        dropped = set()
        if p.get("collision", True) is False:
            if base == 0:
                raise SystemExit("REFUSED: collision:false on the BASE part; it is the object you stand by")
            dropped = {i for i in range(len(src.blocks))
                       if src.type_of(i) in ("bhkNPCollisionObject", "bhkPhysicsSystem")}
        remap, k = {}, 0
        for i in range(len(src.blocks)):
            if i not in dropped:
                remap[i] = base + k; k += 1
        for i, bl in enumerate(src.blocks):
            t = src.type_of(i); w = walk(t, bl)
            if w.used != len(bl):
                raise SystemExit(f"REFUSED: {p['nif']} block {i} {t} does not walk ({w.used}/{len(bl)})")
            if i in dropped:
                continue
            nb = bytearray(bl)
            for off in w.strs:
                v = struct.unpack_from("<I", nb, off)[0]
                if v != NONE: struct.pack_into("<I", nb, off, si(src.strings[v]))
            for off in w.refs:
                v = struct.unpack_from("<I", nb, off)[0]
                if v == NONE:
                    continue
                if v in dropped:
                    # Only a NiAVObject's own collision link may point at a dropped block; it becomes "none".
                    if w.xform is None or off != w.xform + 52:
                        raise SystemExit(f"REFUSED: {p['nif']} block {i} {t} links dropped block {v} "
                                         "through a field that is not its collision link")
                    struct.pack_into("<I", nb, off, NONE)
                else:
                    struct.pack_into("<I", nb, off, remap[v])
            if i == 0 and base > 0:
                if t != "NiNode":
                    raise SystemExit(f"REFUSED: {p['nif']} root is {t}, not NiNode")
                o = w.xform
                tx, ty, tz = struct.unpack_from("<3f", nb, o)
                R0 = [list(struct.unpack_from("<3f", nb, o + 12 + 12 * r)) for r in range(3)]
                s0 = struct.unpack_from("<f", nb, o + 48)[0]
                P = rot_xyz(p.get("rotate", [0, 0, 0])); sp = float(p.get("scale", 1.0))
                tp = p.get("translate", [0, 0, 0])
                T0 = [tx, ty, tz]
                Tn = [sum(P[r][k] * sp * T0[k] for k in range(3)) + tp[r] for r in range(3)]
                Rn = [[sum(P[r][k] * R0[k][c] for k in range(3)) for c in range(3)] for r in range(3)]
                struct.pack_into("<3f", nb, o, *Tn)
                for r in range(3): struct.pack_into("<3f", nb, o + 12 + 12 * r, *Rn[r])
                struct.pack_into("<f", nb, o + 48, s0 * sp)
                root_children.append(base)
            blocks.append(bytes(nb)); tix.append(ti(t))

    # Append the pasted branches to the base root's children list (count, then the links, at the end).
    if blocks[0] and tix[0] != types.index("NiNode"):
        raise SystemExit("REFUSED: base part's root is not a NiNode")
    w0 = walk("NiNode", blocks[0])
    ne = struct.unpack_from("<I", blocks[0], 4)[0]
    count_off = w0.refs[ne + 1] + 4                 # just after the collision link
    old = struct.unpack_from("<I", blocks[0], count_off)[0]
    if count_off + 4 + 4 * old != len(blocks[0]):
        raise SystemExit("REFUSED: base root's children list is not the block's tail")
    blocks[0] = (blocks[0][:count_off] + struct.pack("<I", old + len(root_children))
                 + blocks[0][count_off + 4:] + b"".join(struct.pack("<I", c) for c in root_children))

    n = first
    n.types, n.type_indices, n.blocks, n.strings = types, tix, blocks, strings
    n.max_string = max(len(s_.encode("utf-8")) for s_ in strings)
    n.footer = struct.pack("<II", 1, 0)
    out_path.write_bytes(serialise(n))
    print(f"  merged {len(parts)} part(s) -> {out_path}  ({len(blocks)} blocks, {len(strings)} strings)")


def main(argv):
    if argv[0] == "--walk":
        return 1 if sum(check_walk(Path(p)) for p in argv[1:]) else 0
    if argv[0] == "--roundtrip":
        bad = 0
        for p in argv[1:]:
            raw = Path(p).read_bytes(); ok = serialise(Nif(raw)) == raw
            print(f"  roundtrip {Path(p).name}: {'IDENTICAL' if ok else 'DIFFERS'}"); bad += not ok
        return 1 if bad else 0
    if argv[0] == "--merge":
        merge(json.loads(Path(argv[1]).read_text(encoding="utf-8")), Path(argv[2]))
        return check_walk(Path(argv[2])) and 1
    raise SystemExit(__doc__)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
