"""The A/B safety net for refactoring gen_delve: build recipes with the code as it is, change the code, build
them again, and compare every output by bytes.

    python scripts/delves/ab.py emit <label> [id ...]     build each recipe --dry --emit into <root>/<label>/<id>/
    python scripts/delves/ab.py compare <before> <after>  every output file of every recipe, byte for byte
    python scripts/delves/ab.py selftest                  bite compare's refusals on planted states, no game needed

With no ids, emit builds COVERAGE: the 8 recipes his 2026-10-15 ruling set per step, plus missionboard02a so the
board template's path is in the net too. Each recipe takes about 75 s (one game-data load each).

What a recipe's outputs are, all of them on disk: the would-be plugin and the emitted model (gen_delve --emit
writes both), AND the fragment script source, which a dry build still writes into papyrus/gen/ in this repo. The
plugin names the script, not its text, so a refactor that changed only the fragment would pass a plugin-only
compare. emit copies that .psc into the label folder beside the plugin.

compare refuses, rather than reporting IDENTICAL, when the result could not mean "the code change kept the output":
  - the two labels ran the SAME compiled code (our dlls hash equal): nothing was tested;
  - their INPUTS differ (the live plugin, a recipe, a shell, templates.json): FormKeys are allocated from the
    live plugin's state, so a rebuild between the two emits makes every byte differ, or worse, match by accident;
  - a recipe is missing on one side, failed to build, or has no files: the count of files compared is printed and
    a recipe with none is a refusal, never a pass.
Exit 0 only when every file of every recipe is identical and none of the refusals fired.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
DEFAULT_ROOT = "C:/tmp/delve_ab"
DEFAULT_PLUGIN = "C:/Program Files (x86)/Steam/steamapps/common/Starfield/Data/du_overtime.esm"
COVERAGE = ["duo_delve07a", "duo_delve02a", "duo_delve03a", "duo_delve06a", "duo_delve08a", "duo_delve10a",
            "duo_delve11a", "duo_delve_remote06a", "duo_delve_missionboard02a"]
OUR_DLLS = ["FrankyCLI.dll", "Retrograde.dll"]
MANIFEST = "manifest.json"
NOT_OUTPUT = {"build.log"}


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def json_sha256(path):
    """A JSON input's hash with CRLF read as LF. A JSON string cannot hold a raw newline, so line endings are
    invisible to the parser, and a checkout under core.autocrlf rewrites them: the first real compare refused 25
    recipes that differed in nothing else (a worktree of 1b30110 against this tree, 2026-10-15)."""
    return hashlib.sha256(open(path, "rb").read().replace(b"\r\n", b"\n")).hexdigest()


def input_fingerprint(plugin):
    """Every file a dry build reads that is not code. Named one by one so a difference says WHICH.
    The plugin is binary and hashed raw; the JSON inputs ignore line endings (json_sha256)."""
    out = {"plugin": sha256(plugin)}
    data = os.path.join(REPO, "data", "delves")
    out["templates.json"] = json_sha256(os.path.join(data, "templates.json"))
    for sub in ("recipes", "shells"):
        d = os.path.join(data, sub)
        for name in sorted(os.listdir(d)):
            if name.endswith(".json"):
                out[sub + "/" + name] = json_sha256(os.path.join(d, name))
    return out


def code_fingerprint(bindir):
    out = {}
    for name in OUR_DLLS:
        p = os.path.join(bindir, name)
        if not os.path.exists(p):
            sys.exit(f"REFUSED: {p} is not there after the build; the code fingerprint would be empty.")
        out[name] = sha256(p)
    return out


def emit(args):
    ids = args.ids or COVERAGE
    dest = os.path.join(args.root, args.label)
    if os.path.exists(dest):
        sys.exit(f"REFUSED: {dest} exists. A label is written once; pick a new one or delete it by hand.")
    print("building FrankyCLI (a stale binary would make every compare pass)...", flush=True)
    b = subprocess.run(["dotnet", "build", os.path.join(REPO, "FrankyCLI.csproj"), "-nologo", "-v", "q"],
                       cwd=REPO, capture_output=True, text=True)
    if b.returncode != 0:
        print(b.stdout[-4000:], b.stderr[-2000:])
        sys.exit("REFUSED: the build failed; nothing emitted.")
    bindir = os.path.join(REPO, "bin", "Debug", "net8.0")
    exe = os.path.join(bindir, "FrankyCLI.exe")
    manifest = {"label": args.label, "ids": ids, "code": code_fingerprint(bindir),
                "inputs": input_fingerprint(args.plugin), "results": {}}
    os.makedirs(dest)
    for i, rid in enumerate(ids, 1):
        out = os.path.join(dest, rid)
        os.makedirs(out)
        print(f"[{i}/{len(ids)}] {rid} ...", flush=True)
        p = subprocess.run([exe, "gen_delve", "build", rid, "--dry", "--emit", out],
                           cwd=REPO, capture_output=True, text=True, encoding="utf-8", errors="replace")
        log = p.stdout + p.stderr
        with open(os.path.join(out, "build.log"), "w", encoding="utf-8") as f:
            f.write(log)
        wrote = "--dry --emit: wrote" in log
        psc = re.findall(r"fragments: .*?; source (.+\.psc)", log)
        if psc:
            src = psc[-1].strip()
            if not os.path.exists(src):
                wrote = False
                print(f"   the log names {src} and it is not on disk")
            else:
                shutil.copyfile(src, os.path.join(out, os.path.basename(src)))
        ok = p.returncode == 0 and wrote
        manifest["results"][rid] = {"exit": p.returncode, "emitted": wrote, "fragment": bool(psc)}
        print(f"   {'ok' if ok else 'FAILED'}  exit {p.returncode}, files: {sorted(os.listdir(out))}", flush=True)
    # The inputs are read again at the end: a plugin rebuilt DURING the run is as bad as one rebuilt between runs.
    if input_fingerprint(args.plugin) != manifest["inputs"]:
        manifest["inputs_moved_during_run"] = True
        print("WARNING: an input changed while this label was emitting; compare will refuse it.")
    with open(os.path.join(dest, MANIFEST), "w", encoding="utf-8") as f:
        json.dump(manifest, f, indent=1)
    bad = [r for r, v in manifest["results"].items() if not (v["exit"] == 0 and v["emitted"])]
    print(f"\nlabel {args.label}: {len(ids) - len(bad)}/{len(ids)} emitted" + (f"; FAILED: {bad}" if bad else ""))
    return 1 if bad else 0


def compare_dirs(root, a, b, allow_same_code=False, expect_changed=()):
    """Returns (exit code, lines). Pure, so selftest can drive it on planted folders.
    expect_changed: input keys the step changes ON PURPOSE (a template row deleted with its kind). Each is named in
    the verdict, and one named but NOT changed is a refusal too: an exemption that exempts nothing is a typo."""
    lines = []
    ma = json.load(open(os.path.join(root, a, MANIFEST), encoding="utf-8"))
    mb = json.load(open(os.path.join(root, b, MANIFEST), encoding="utf-8"))
    refusals = []
    if ma["code"] == mb["code"] and not allow_same_code:
        refusals.append("the two labels ran the SAME compiled code; an IDENTICAL here would test nothing")
    for m in (ma, mb):
        if m.get("inputs_moved_during_run"):
            refusals.append(f"an input changed while {m['label']} was emitting")
    moved = sorted(k for k in set(ma["inputs"]) | set(mb["inputs"]) if ma["inputs"].get(k) != mb["inputs"].get(k))
    unexpected = [k for k in moved if k not in expect_changed]
    if unexpected:
        refusals.append("inputs differ between the labels: " + ", ".join(unexpected))
    for k in expect_changed:
        if k not in moved:
            refusals.append(f"--expect-input-change {k} names an input that did NOT change")
        else:
            lines.append(f"  input changed ON PURPOSE (declared): {k}")
    if sorted(ma["ids"]) != sorted(mb["ids"]):
        refusals.append(f"different recipe sets: {sorted(set(ma['ids']) ^ set(mb['ids']))}")
    differ, files_compared = [], 0
    for rid in sorted(set(ma["ids"]) & set(mb["ids"])):
        for m in (ma, mb):
            r = m["results"].get(rid, {})
            if not (r.get("exit") == 0 and r.get("emitted")):
                refusals.append(f"{rid} did not build cleanly in {m['label']}")
        da, db = os.path.join(root, a, rid), os.path.join(root, b, rid)
        fa = sorted(set(os.listdir(da)) - NOT_OUTPUT) if os.path.isdir(da) else []
        fb = sorted(set(os.listdir(db)) - NOT_OUTPUT) if os.path.isdir(db) else []
        if not fa or not fb:
            refusals.append(f"{rid} has no output files on one side")
            continue
        if fa != fb:
            differ.append(f"{rid}: file sets differ, {a} {fa} vs {b} {fb}")
        for name in sorted(set(fa) & set(fb)):
            files_compared += 1
            pa, pb = os.path.join(da, name), os.path.join(db, name)
            ba, bb = open(pa, "rb").read(), open(pb, "rb").read()
            if ba != bb:
                at = next((i for i, (x, y) in enumerate(zip(ba, bb)) if x != y), min(len(ba), len(bb)))
                differ.append(f"{rid}/{name}: DIFFER, {len(ba):,} B vs {len(bb):,} B, first difference at byte {at:,}")
        lines.append(f"  {rid}: {len(set(fa) & set(fb))} file(s) compared")
    lines.append(f"files compared: {files_compared} across {len(set(ma['ids']) & set(mb['ids']))} recipe(s)")
    for d in differ:
        lines.append("  " + d)
    for r in refusals:
        lines.append("REFUSED: " + r)
    if refusals:
        return 2, lines
    if differ:
        lines.append(f"DIFFER: {len(differ)} file(s)")
        return 1, lines
    lines.append("IDENTICAL: every output of every recipe, byte for byte")
    return 0, lines


def compare(args):
    code, lines = compare_dirs(args.root, args.before, args.after, args.allow_same_code, args.expect_input_change)
    print("\n".join(lines))
    return code


def selftest(_args):
    """Each refusal is driven by planting the broken state, and the clean case must pass. A test that never
    watched compare fail would be the same blind net it exists to prevent."""
    fails = 0

    def plant(root, label, code="c1", inputs=None, files=None, ok=True, ids=("r1", "r2")):
        d = os.path.join(root, label)
        os.makedirs(d)
        files = files if files is not None else {"du_overtime.esm": b"\x01\x02\x03", "r.json": b"{}", "s.psc": b"x"}
        for rid in ids:
            os.makedirs(os.path.join(d, rid))
            for n, data in files.items():
                open(os.path.join(d, rid, n), "wb").write(data)
            open(os.path.join(d, rid, "build.log"), "w").write(label)
        m = {"label": label, "ids": list(ids), "code": {"FrankyCLI.dll": code},
             "inputs": inputs or {"plugin": "p1"}, "results": {r: {"exit": 0 if ok else 1, "emitted": ok} for r in ids}}
        json.dump(m, open(os.path.join(d, MANIFEST), "w"))

    def case(name, want, expect=(), **after):
        nonlocal fails
        root = tempfile.mkdtemp()
        try:
            plant(root, "a")
            plant(root, "b", **({"code": "c2"} | after))
            got, lines = compare_dirs(root, "a", "b", expect_changed=expect)
            ok = got == want
            fails += 0 if ok else 1
            print(f"  {'ok ' if ok else 'BAD'} {name}: exit {got}, wanted {want}  ({lines[-1]})")
        finally:
            shutil.rmtree(root)

    case("clean: different code, same inputs, same bytes", 0)
    case("one byte flipped in the plugin", 1, files={"du_overtime.esm": b"\x01\x02\x04", "r.json": b"{}", "s.psc": b"x"})
    case("the fragment source changed and the plugin did not", 1,
         files={"du_overtime.esm": b"\x01\x02\x03", "r.json": b"{}", "s.psc": b"y"})
    case("a file missing on one side", 1, files={"du_overtime.esm": b"\x01\x02\x03", "r.json": b"{}"})
    case("same compiled code on both sides", 2, code="c1")
    case("the live plugin moved between the labels", 2, inputs={"plugin": "p2"})
    case("a declared input change, same bytes", 0, expect=("plugin",), inputs={"plugin": "p2"})
    case("a declared change that did not happen", 2, expect=("plugin",))
    case("a declared change beside an undeclared one", 2, expect=("plugin",), inputs={"plugin": "p2", "templates.json": "t2"})
    case("a recipe failed to build", 2, ok=False)
    case("a recipe missing from one label", 2, ids=("r1",))
    case("a recipe with no output files", 2, files={})
    # The JSON fingerprint: blind to line endings, never to content.
    d = tempfile.mkdtemp()
    try:
        paths = {}
        for name, data in (("lf", b'{"a": 1,\n "b": "x"}\n'), ("crlf", b'{"a": 1,\r\n "b": "x"}\r\n'), ("edit", b'{"a": 2,\n "b": "x"}\n')):
            paths[name] = os.path.join(d, name + ".json")
            open(paths[name], "wb").write(data)
        for name, want, got in (("CRLF and LF hash the same", True, json_sha256(paths["lf"]) == json_sha256(paths["crlf"])),
                                ("a changed value hashes differently", False, json_sha256(paths["lf"]) == json_sha256(paths["edit"]))):
            ok = got == want
            fails += 0 if ok else 1
            print(f"  {'ok ' if ok else 'BAD'} json fingerprint: {name}")
    finally:
        shutil.rmtree(d)
    print(f"selftest: {'all bites caught' if fails == 0 else str(fails) + ' BAD'}")
    return 1 if fails else 0


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--root", default=DEFAULT_ROOT, help=f"where labels live (default {DEFAULT_ROOT}, outside any repo)")
    ap.add_argument("--plugin", default=DEFAULT_PLUGIN, help="the live plugin whose state allocates FormKeys")
    sub = ap.add_subparsers(dest="cmd", required=True)
    e = sub.add_parser("emit")
    e.add_argument("label")
    e.add_argument("ids", nargs="*")
    c = sub.add_parser("compare")
    c.add_argument("before")
    c.add_argument("after")
    c.add_argument("--allow-same-code", action="store_true",
                   help="the determinism control only: the same binary twice must be identical")
    c.add_argument("--expect-input-change", action="append", default=[], metavar="KEY",
                   help="an input the step changes on purpose (e.g. templates.json when a template row leaves); repeatable")
    sub.add_parser("selftest")
    args = ap.parse_args()
    return {"emit": emit, "compare": compare, "selftest": selftest}[args.cmd](args)


if __name__ == "__main__":
    sys.exit(main())
