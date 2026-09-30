"""Shipbuilder icons: render with the Creation Kit, solve each part's framing, convert.

    python scripts/icon_solve.py solve   <modname> [--target 330] [--passes 8] [--no-presets]
    python scripts/icon_solve.py render  <modname> [--no-presets]
    python scripts/icon_solve.py convert <modname> <out_dir>
    python scripts/icon_solve.py selftest

WHAT IT DOES. `CreationKit.exe -GenerateShipBuilderIcons:<plugin>.esm` renders one 512x512 icon
per constructible PackIn, framed ENTIRELY by the PackIn's Ship transform (PTT2 slot 2 -> TRNS).
`solve` loops: render -> measure every icon -> solve each part's transform POSITION so the part is
centred at --target px -> write them with `FrankyCLI seticontransform` -> render again, until every
icon is whole, centred and on size. `convert` then writes the shipping DDS.

THE CAMERA MODEL, measured 2026-09-30 on Defiant by a calibration run (9 parts moved across 9
vanilla transforms, 124 untouched controls within 1 px) -- the [Bethesda] manual, part 23:
    TRNS position is CAMERA space: x = screen right, y = depth, z = screen up.
    D = y - 0.44;  icon size ~ 1/D;  dx = +507 (x + ox) / D;  dy = -488 (z + oz) / D
(ox, oz) is the part's own offset from its origin, read off a measured icon, so no mesh or bound
is ever parsed. The camera aims at the part's ORIGIN, not its bounds -- which is why a generated
part, whose origin is wherever its snap wanted it, frames badly on a shared vanilla transform.

STATELESS BY DESIGN. Each part's current position is read from the plugin (PKIN -> PTT2 Ship ->
TRNS, in the plugin itself or the master that owns it), never from a side file, so nothing here
can drift from what the CK will actually render.

THE ICON FORMAT is 128x128 BC1, one mip, 1-bit alpha: byte for byte the shape of Avontech
Raceyard's QA-passed icons. `-srgbnoconvert` is REQUIRED: the CK writes sRGB-tagged TIFs and
without it xtexconv silently linearises them and every icon ships darker (185 -> 128 mean grey),
with a header indistinguishable from the correct one.

SAFETY. Refuses while Starfield or the CK is running. The IconGenerator presets (lighting; they
halve the run) are overlaid onto the live CK inis key by key and restored BYTE FOR BYTE in a
finally, with a hash check. The plugin is written only by FrankyCLI, which is all-or-nothing and
idempotent. Back the plugin up before a first solve: the command rewrites it.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import tempfile
from pathlib import Path

from PIL import Image

STEAM = Path(r"C:\Program Files (x86)\Steam\steamapps")
GAME = STEAM / "common" / "Starfield"
DATA = GAME / "Data"
ICON_SRC = STEAM / "Source" / "TGATextures" / "Interface" / "ShipBuilderIcons"
PRESETS = GAME / "Tools" / "IconGenerator"
XTEXCONV = GAME / "Tools" / "AssetWatcher" / "Plugins" / "Starfield" / "xtexconv.exe"
FRANKY = Path(__file__).resolve().parent.parent / "bin" / "Debug" / "net8.0" / "FrankyCLI.exe"

KX, KZ, Y_OFF = 507.0, 488.0, 0.44          # the measured camera model (see docstring)
PULL_FIRST, PULL_NEAR, MAX_ZOOM_IN = 3.0, 1.35, 2.0
# Converged = centred within TOL_PX of the 512 render and on size within 5 %. The tolerance is set
# by the SHIPPED icon: 8 px at 512 is 2 px at 128. It was 3 on the first run (0.75 px shipped) and
# 13 deep, off-origin vent flip members hovered at 3.5-6 px for eight passes: the model's slope is
# a little off for them, so each correction overshoots by a pixel. Precision below what ships.
TOL_PX, TOL_SIZE = 8.0, 0.05


# ---------------------------------------------------------------- plugin reading (read-only)
def _walk(path, want):
    """Yield (sig, formid, {subrecord: first payload}) for the record types in `want`."""
    b = path.read_bytes()
    pos = 24 + struct.unpack_from("<I", b, 4)[0]
    while pos < len(b):
        sig = b[pos:pos + 4]
        size = struct.unpack_from("<I", b, pos + 4)[0]
        if sig == b"GRUP":
            if struct.unpack_from("<I", b, pos + 12)[0] == 0 and b[pos + 8:pos + 12] not in want:
                pos += size
                continue
            pos += 24
            continue
        flags, fid = struct.unpack_from("<II", b, pos + 8)
        if sig in want:
            d = b[pos + 24:pos + 24 + size]
            if flags & 0x40000:
                import zlib
                d = zlib.decompress(d[4:])
            subs, j = {}, 0
            while j + 6 <= len(d):
                s = d[j:j + 4]
                ln = struct.unpack_from("<H", d, j + 4)[0]
                subs.setdefault(s, d[j + 6:j + 6 + ln])
                j += 6 + ln
            yield sig, fid, subs
        pos += 24 + size


def masters(path):
    head = path.read_bytes()[:24 + struct.unpack_from("<I", path.read_bytes(), 4)[0]]
    return [m.decode() for m in re.findall(rb"MAST.{2}([^\x00]+)\x00", head, re.S)]


def current_positions(modname):
    """{pkin editorid: (formid_local, (x, y, z), own)} as the CK will render them now.
    `own` = the Ship transform is a record of this plugin (a part solved before), not a master's."""
    plugin = DATA / f"{modname}.esm"
    files = masters(plugin) + [plugin.name]
    own_index = len(files) - 1
    # The plugin's own TRNS AND its overrides of a master's win, as the CK loads the plugin last.
    in_plugin = {fid: struct.unpack_from("<3f", s[b"DATA"]) for _, fid, s in _walk(plugin, {b"TRNS"})}
    master_cache = {}

    def master_pos(fid):
        f = DATA / files[fid >> 24]
        if f not in master_cache:          # a master's own records carry ITS own index
            mi = len(masters(f))
            master_cache[f] = {x & 0xFFFFFF: struct.unpack_from("<3f", s[b"DATA"])
                               for _, x, s in _walk(f, {b"TRNS"}) if x >> 24 == mi}
        return master_cache[f].get(fid & 0xFFFFFF)

    out = {}
    for _, fid, s in _walk(plugin, {b"PKIN"}):
        if b"PTT2" not in s:
            continue
        ship = struct.unpack_from("<8I", s[b"PTT2"])[2]
        if not ship:
            continue
        edid = s.get(b"EDID", b"").rstrip(b"\0").decode()
        pos = in_plugin.get(ship) or master_pos(ship)
        if pos is None:
            sys.exit(f"{edid}: Ship transform {ship:08X} does not resolve -- refusing to solve blind")
        out[edid] = (fid & 0xFFFFFF, pos, ship >> 24 == own_index)
    return out


# ---------------------------------------------------------------- measuring and solving
def measure(tif):
    """(dx, dy, size, clipped) of the part's silhouette vs the frame centre, or None if blank."""
    a = Image.open(tif).convert("RGBA").split()[3].point(lambda v: 255 if v > 16 else 0)
    bb = a.getbbox()
    if not bb:
        return None
    w, h = a.size
    clipped = bb[0] <= 1 or bb[1] <= 1 or bb[2] >= w - 1 or bb[3] >= h - 1
    return (bb[0] + bb[2]) / 2 - w / 2, (bb[1] + bb[3]) / 2 - h / 2, max(bb[2] - bb[0], bb[3] - bb[1]), clipped


def solve_one(pos, m, target, first_contact):
    """Next position for a part rendered at `pos` whose icon measured `m`."""
    x, y, z = pos
    dx, dy, size, clipped = m
    D = y - Y_OFF
    ox = dx * D / KX - x
    oz = -dy * D / KZ - z
    if clipped:          # a clipped icon has no trustworthy size: pull back, solve next pass
        Dn = D * (PULL_FIRST if first_contact else PULL_NEAR)
    else:                # zoom-in capped per pass: an offset read off a small icon is magnified
        Dn = max(D * size / target, D / MAX_ZOOM_IN)
    return (round(-ox, 4), round(Dn + Y_OFF, 4), round(-oz, 4))


def converged(m, target):
    return m is not None and not m[3] and abs(m[0]) <= TOL_PX and abs(m[1]) <= TOL_PX \
        and abs(m[2] - target) <= TOL_SIZE * target


# ---------------------------------------------------------------- the CK, with presets
def _refuse_if_running():
    r = subprocess.run(["tasklist"], capture_output=True, text=True).stdout.lower()
    for exe in ("starfield.exe", "creationkit.exe"):
        if exe in r:
            sys.exit(f"{exe} is running -- close it first (it holds the plugin)")


def _overlay(live, preset):
    """Set the preset's keys into the live ini, keeping every other line. CRLF out."""
    lines = live.read_text(encoding="utf-8").splitlines()
    sec_of, cur = [], None
    for ln in lines:
        m = re.match(r"^\s*\[(.+)\]\s*$", ln)
        cur = m.group(1) if m else cur
        sec_of.append(cur)
    sec = None
    for raw in preset.read_text(encoding="utf-8").splitlines():
        s = raw.strip()
        if not s or s.startswith(";"):
            continue
        m = re.match(r"^\[(.+)\]$", s)
        if m:
            sec = m.group(1)
            continue
        k, v = (t.strip() for t in s.split("=", 1))
        hit = next((i for i, ln in enumerate(lines) if sec_of[i] and sec_of[i].lower() == sec.lower()
                    and "=" in ln and ln.split("=", 1)[0].strip().lower() == k.lower()), None)
        if hit is not None:
            lines[hit] = f"{k}={v}"
            continue
        idx = [i for i, sc in enumerate(sec_of) if sc and sc.lower() == sec.lower()]
        if idx:
            lines.insert(idx[-1] + 1, f"{k}={v}")
            sec_of.insert(idx[-1] + 1, sec)
        else:
            lines += ["", f"[{sec}]", f"{k}={v}"]
            sec_of += [None, sec, sec]
    live.write_bytes(("\r\n".join(lines) + "\r\n").encode("utf-8"))


def render(modname, presets=True):
    """Run the CK generator; return the folder of fresh TIFs (the old ones are cleared first)."""
    _refuse_if_running()
    out = ICON_SRC / f"{modname}.esm"
    if out.exists():
        shutil.rmtree(out)
    pairs = [(GAME / "CreationKitCustom.ini", PRESETS / "CreationKitCustom_ShipbuilderIcons.ini"),
             (GAME / "CreationKitPrefs.ini", PRESETS / "CreationKitPrefs_ShipbuilderIcons.ini")]
    saved = {live: live.read_bytes() for live, _ in pairs} if presets else {}
    try:
        for live, preset in (pairs if presets else []):
            _overlay(live, preset)
        # The generator ends with "CRASH (UNKNOWN ERROR:)", exit 0 and a .dmp: that is its
        # NORMAL finish (bThrowProcessCompleteExceptions). Judge by the output, not the exit.
        subprocess.run([str(GAME / "CreationKit.exe"), f"-GenerateShipBuilderIcons:{modname}.esm"],
                       cwd=GAME, capture_output=True)
    finally:
        for live, data in saved.items():
            live.write_bytes(data)
            assert hashlib.sha256(live.read_bytes()).digest() == hashlib.sha256(data).digest(), f"{live} NOT restored"
    if not out.is_dir() or not any(out.iterdir()):
        sys.exit(f"the CK wrote no icons to {out}")
    return out


def write_plan(modname, plan):
    if not FRANKY.exists():
        sys.exit(f"FrankyCLI is not built: {FRANKY}")
    with tempfile.NamedTemporaryFile("w", suffix=".json", delete=False) as f:
        json.dump(plan, f)
    try:
        r = subprocess.run([str(FRANKY), "seticontransform", modname, f.name], capture_output=True, text=True)
        tail = [ln for ln in r.stdout.splitlines() if "created" in ln or "Finished" in ln or "Error" in ln or "Nothing" in ln]
        print("   ", " | ".join(t.strip() for t in tail))
        if r.returncode != 0 or "Error" in r.stdout:
            sys.exit(r.stdout)
    finally:
        os.unlink(f.name)


# ---------------------------------------------------------------- commands
def cmd_solve(a):
    for n in range(1, a.passes + 1):
        cur = current_positions(a.modname)
        icons = render(a.modname, not a.no_presets)
        plan, done, blank, clipped = {}, 0, [], 0
        for edid, (fid, pos, own) in sorted(cur.items()):
            tif = icons / f"{fid:08X}CL.tif"
            if not tif.exists():
                continue                                   # not a builder item: no icon
            m = measure(tif)
            if m is None:
                blank.append(edid)                         # renders nothing at any camera: not framing
                continue
            clipped += m[3]
            if converged(m, a.target):
                done += 1
                continue
            # First contact = still on an inherited master transform: a clipped part may be far
            # too big for it, so it is pulled back hard; after that, gently.
            plan[edid] = solve_one(pos, m, a.target, first_contact=not own)
        print(f"pass {n}: {done} converged, {len(plan)} to move ({clipped} clipped), {len(blank)} blank")
        if not plan:
            print(f"CONVERGED after {n} render(s).")
            if blank:
                print("  blank (renders nothing -- a mesh/model problem, not framing):", ", ".join(blank))
            return 0
        write_plan(a.modname, plan)
    print(f"NOT converged after {a.passes} passes -- the last pass was written; re-run to continue.")
    return 1


def cmd_convert(a):
    src = ICON_SRC / f"{a.modname}.esm"
    out = Path(a.out_dir)
    out.mkdir(parents=True, exist_ok=True)
    tifs = sorted(src.glob("*.tif"))
    if not tifs:
        sys.exit(f"no TIFs in {src} -- run render or solve first")
    r = subprocess.run([str(XTEXCONV), "-nologo", "-y", "-w", "128", "-h", "128", "-m", "1", "-f", "BC1_UNORM",
                        "-if", "CUBIC", "-l", "-srgbnoconvert", "-o", str(out)] + [t.name for t in tifs],
                       cwd=src, capture_output=True, text=True)
    bad = [ln for ln in r.stdout.splitlines() if "FAIL" in ln.upper()]
    if r.returncode or bad:
        sys.exit(r.stdout)
    for t in tifs:                                    # every file must be exactly the shape QA passed
        d = (out / (t.stem.lower() + ".dds")).read_bytes()
        h, w = struct.unpack_from("<II", d, 12)
        assert (len(d), w, h, struct.unpack_from("<I", d, 28)[0], d[84:88]) == (8320, 128, 128, 1, b"DXT1"), t.name
    print(f"{len(tifs)} icons -> {out} (128x128 BC1, 1 mip, sRGB values kept)")
    return 0


def cmd_selftest(_):
    """The solver's own control: a forward model rendered through the solve must return the
    position that centres it, and a clipped icon must pull back rather than solve."""
    true_ox, true_oz, size_at_1 = -3.2, 1.1, 4000.0
    pos = (0.0, 15.0, 0.5)
    for _ in range(6):
        D = pos[1] - Y_OFF
        m = (KX * (pos[0] + true_ox) / D, -KZ * (pos[2] + true_oz) / D, size_at_1 / D, False)
        if converged(m, 330):
            break
        pos = solve_one(pos, m, 330, first_contact=False)
    assert abs(pos[0] - 3.2) < 1e-3 and abs(pos[2] + 1.1) < 1e-3, pos
    assert abs(size_at_1 / (pos[1] - Y_OFF) - 330) < 1, pos
    assert solve_one((0, 15, 0.5), (0, 0, 512, True), 330, True)[1] > 40, "clipped must pull back"
    print("selftest OK: a synthetic part converges to centre and size; a clipped one pulls back")
    return 0


def main():
    p = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    sp = p.add_subparsers(dest="cmd", required=True)
    s = sp.add_parser("solve"); s.add_argument("modname"); s.add_argument("--target", type=float, default=330.0)
    s.add_argument("--passes", type=int, default=8); s.add_argument("--no-presets", action="store_true")
    r = sp.add_parser("render"); r.add_argument("modname"); r.add_argument("--no-presets", action="store_true")
    c = sp.add_parser("convert"); c.add_argument("modname"); c.add_argument("out_dir")
    sp.add_parser("selftest")
    a = p.parse_args()
    if a.cmd == "render":
        print(render(a.modname, not a.no_presets))
        return 0
    return {"solve": cmd_solve, "convert": cmd_convert, "selftest": cmd_selftest}[a.cmd](a)


if __name__ == "__main__":
    sys.exit(main())
