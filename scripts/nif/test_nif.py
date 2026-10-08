"""Controls for scripts/nif. Every one has been watched FAILING before its pass was trusted.

    python test_nif.py                      # exit 0 all pass, 1 a failure, 2 a leg could not run

Fixtures are EXTRACTED from the installed game into a temp folder at run time and never committed: this
repo is public and the game's assets are not ours to redistribute. Set SF_DATA if the game is not in the
default Steam library. Optionally set NIF_FIXTURES to a folder of extra NIFs (kitbashes made in NifSkope)
for the walk and round-trip legs.
"""
from __future__ import annotations
import json, os, struct, sys, tempfile
from pathlib import Path
import ba2get, sfnif, kbounds, kitbash, clay
from sfnif import Nif

fail = 0
cannot = 0
TMP = Path(tempfile.mkdtemp(prefix="nif_test_"))


def check(label, ok, detail=""):
    global fail
    print(("  PASS  " if ok else "  FAIL  ") + label + ("   " + detail if detail else ""))
    fail += not ok


def vanilla(member: str, name: str) -> Path | None:
    global cannot
    out = TMP / name
    for a in ba2get.VANILLA:
        ba2 = ba2get.DATA / a
        if not ba2.exists():
            continue
        data = ba2get.extract(ba2, member)
        if data is not None:
            out.write_bytes(data); return out
    print(f"  CANNOT RUN: {member} not found under {ba2get.DATA} (set SF_DATA)"); cannot += 1
    return None


DISH = vanilla("meshes/setdressing/satellitedish/satellitedish_a_01.nif", "dish.nif")
TRIPOD = vanilla("meshes/setdressing/satellitedish/satellitedish_a_tripodfloor01.nif", "tripod.nif")
CART = vanilla("meshes/setdressing/waterfiltrationcart/waterfiltrationcart01.nif", "cart.nif")
VAN = [p for p in (DISH, TRIPOD, CART) if p]
EXTRA = sorted(Path(os.environ["NIF_FIXTURES"]).glob("*.nif")) if os.environ.get("NIF_FIXTURES") else []
if not EXTRA:
    print("  note: NIF_FIXTURES unset or empty; walk/round-trip run on vanilla only")

print("walk + round trip")
for p in VAN + EXTRA:
    raw = p.read_bytes(); n = Nif(raw)
    bad = 0
    for i, bl in enumerate(n.blocks):
        bad += sfnif.walk(n.type_of(i), bl).used != len(bl)
    check(f"walk {p.name} ({len(n.blocks)} blocks)", bad == 0, f"{bad} block(s) mis-sized")
    check(f"round trip {p.name}", sfnif.serialise(n) == raw)

print("bite the walk")
if CART:
    real = sfnif.walk
    def broken(t, b):
        w = real(t, b)
        if t == "BSLightingShaderProperty": w.used -= 4
        return w
    sfnif.walk = broken
    n = Nif(CART.read_bytes())
    caught = sum(sfnif.walk(n.type_of(i), bl).used != len(bl) for i, bl in enumerate(n.blocks))
    sfnif.walk = real
    check("a layout missing one link is caught", caught > 0, f"{caught} block(s) flagged")

print("bounds against the game's own OBND records")
expect = {"dish": (DISH, (-2.23, -2.23, 0.0), (2.26, 2.25, 3.96)),
          "cart": (CART, (-0.40, -0.53, 0.0), (0.40, 0.49, 1.25)),
          "tripod": (TRIPOD, (-0.81, -1.19, -2.14), (1.26, 1.19, 0.06))}
for k, (p, lo_w, hi_w) in expect.items():
    if not p: continue
    lo, hi, _ = kbounds.bounds(Nif(p.read_bytes()))
    miss = max(max(abs(a - b) for a, b in zip(lo, lo_w)), max(abs(a - b) for a, b in zip(hi, hi_w)))
    check(f"bounds {k}", miss <= 0.02, f"worst axis off by {miss:.3f} m")

print("kitbash: anchor, merge, un-merge")
if DISH and CART:
    spec = {"name": "test", "parts": [{"nif": str(CART)},
            {"nif": str(DISH), "scale": 0.25, "rotate": [0, 0, 90],
             "anchor": {"on": "WaterFiltrationCart01", "at": "top", "sink": 0.0}}]}
    out = TMP / "kb.nif"
    pristine = json.dumps(spec)          # resolve() writes the translate back into the spec it is given
    wanted = kitbash.resolve(spec)
    sfnif.merge(spec, out)
    check("merged file walks clean", sfnif.check_walk(out) == 0)
    check("anchored part landed within 1 cm", kitbash.check(spec, out, wanted) == 0)
    m = Nif(out.read_bytes()); src = Nif(DISH.read_bytes()); base = len(Nif(CART.read_bytes()).blocks)
    diffs = 0
    for i, bl in enumerate(src.blocks):
        t = src.type_of(i); mb = m.blocks[base + i]; w = sfnif.walk(t, bl)
        a, b = bytearray(bl), bytearray(mb)
        for off in w.strs:
            v, u = struct.unpack_from("<I", bl, off)[0], struct.unpack_from("<I", mb, off)[0]
            diffs += (v == sfnif.NONE) != (u == sfnif.NONE) or (v != sfnif.NONE and src.strings[v] != m.strings[u])
            a[off:off + 4] = b[off:off + 4] = b"\0" * 4
        for off in w.refs:
            v, u = struct.unpack_from("<I", bl, off)[0], struct.unpack_from("<I", mb, off)[0]
            diffs += (v == sfnif.NONE) != (u == sfnif.NONE) or (v != sfnif.NONE and u - base != v)
            a[off:off + 4] = b[off:off + 4] = b"\0" * 4
        if i == 0:
            a[w.xform:w.xform + 52] = b[w.xform:w.xform + 52] = b"\0" * 52
        diffs += a != b
    check("un-merge: every pasted block maps back to its source", diffs == 0, f"{diffs} difference(s)")

    print("bite the kitbash")
    spec2 = json.loads(pristine); w2 = kitbash.resolve(spec2)
    spec2["parts"][1]["translate"][0] += 0.11
    sfnif.merge(spec2, TMP / "kb_off.nif")
    check("an 11 cm misplacement reads MISS", kitbash.check(spec2, TMP / "kb_off.nif", w2) == 1)
    bad_spec = json.loads(pristine); bad_spec["parts"][1]["anchor"]["on"] = "NoSuchNode"
    try:
        kitbash.resolve(bad_spec); check("an unknown anchor is refused", False)
    except SystemExit:
        check("an unknown anchor is refused", True)
    m2 = Nif(out.read_bytes())
    for i in range(base, len(m2.blocks)):
        if m2.type_of(i) == "BSGeometry":
            w = sfnif.walk("BSGeometry", m2.blocks[i]); nb = bytearray(m2.blocks[i]); off = w.refs[-2]
            struct.pack_into("<I", nb, off, struct.unpack_from("<I", nb, off)[0] + 1)
            m2.blocks[i] = bytes(nb); break
    planted = Nif(sfnif.serialise(m2))
    pd = 0
    for i, bl in enumerate(src.blocks):
        w = sfnif.walk(src.type_of(i), bl)
        for off in w.refs:
            v, u = struct.unpack_from("<I", bl, off)[0], struct.unpack_from("<I", planted.blocks[base + i], off)[0]
            pd += v != sfnif.NONE and u - base != v
    check("a planted wrong link is caught by the un-merge", pd > 0, f"{pd} link(s) flagged")

print("kitbash: collision false")
if DISH and CART:
    nc = json.loads(pristine); nc["parts"][1]["collision"] = False
    kitbash.resolve(nc)
    sfnif.merge(nc, TMP / "kb_nocoll.nif")
    with_c, no_c = Nif((TMP / "kb.nif").read_bytes()), Nif((TMP / "kb_nocoll.nif").read_bytes())
    count = lambda n, t: sum(n.type_of(i) == t for i in range(len(n.blocks)))
    dish_src = Nif(DISH.read_bytes())
    havok = count(dish_src, "bhkNPCollisionObject") + count(dish_src, "bhkPhysicsSystem")
    check("the pasted part brought Havok blocks to drop", havok > 0, f"{havok} in the dish")
    check("exactly the part's Havok blocks are gone", len(with_c.blocks) - len(no_c.blocks) == havok,
          f"{len(with_c.blocks)} -> {len(no_c.blocks)}")
    check("the base keeps its collision", count(no_c, "bhkNPCollisionObject") == count(Nif(CART.read_bytes()), "bhkNPCollisionObject"))
    check("geometry count unchanged", count(no_c, "BSGeometry") == count(with_c, "BSGeometry"))
    check("walks clean", sfnif.check_walk(TMP / "kb_nocoll.nif") == 0)
    bad = 0
    for i, bl in enumerate(no_c.blocks):
        w = sfnif.walk(no_c.type_of(i), bl)
        for off in w.refs:
            v = struct.unpack_from("<I", bl, off)[0]
            bad += v != sfnif.NONE and v >= len(no_c.blocks)
    check("every link in range", bad == 0, f"{bad} out of range")
    try:
        b0 = json.loads(pristine); b0["parts"][0]["collision"] = False
        sfnif.merge(b0, TMP / "x.nif"); check("collision:false on the base is refused", False)
    except SystemExit:
        check("collision:false on the base is refused", True)

print("clay render")
for p, zmax_below in ((TRIPOD, True), (DISH, False)):
    if not p: continue
    n = Nif(p.read_bytes())
    tris, missing, _ = clay.collect(n, None)
    check(f"clay {p.name}: every mesh found", not missing, f"{len(missing)} missing")
    zlo = float(tris[..., 2].min()) if len(tris) else 0.0
    check(f"clay {p.name}: lowest z {'below' if zmax_below else 'at'} the ground", (zlo < -2.0) if zmax_below else abs(zlo) < 0.05, f"{zlo:.2f}")
    clay.main([str(p), str(TMP / (p.stem + ".png"))])

print(f"\n{'ALL PASS' if not fail and not cannot else f'{fail} FAILED, {cannot} COULD NOT RUN'}   (scratch: {TMP})")
sys.exit(1 if fail else (2 if cannot else 0))
