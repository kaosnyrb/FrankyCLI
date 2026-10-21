"""Compile FrankyCLI's Papyrus library into a mod's Data/scripts.

  python papyrus/compile.py <mod Data dir> <script> [<script> ...]
  python papyrus/compile.py C:/modding/DU_Overtime/Data duo_delve_driver

ONE SOURCE, THE ARTIFACT WHERE THE MOD SHIPS IT. The .psc lives here, beside the generator that
writes its properties, so the two cannot drift apart across repos. Only the compiled .pex goes into
the mod, into the folder that mod already tracks and packs. No second copy of the source is made,
because two copies of a script are two scripts the first time somebody edits one.

THE 38-CHARACTER WALL is the compiler's own, refused here BEFORE the compiler runs so the reason is
one line rather than a (0,0) error. See DU_Overtime's README for the five scripts already over it.

A compile is proven by the .pex it produces, not by the compiler's exit code: the output file is
checked for existence and a fresh modification time, and anything else is a failure.

⛔ THE LONG-STRING WALL, found 2026-10-21 on Defiant: a .pex whose longest string (in practice the {}
docstring) is too long CRASHES THE GAME the moment the script is first called. duo_podprobe crashed six
times running with its longest string at 4,468 to 5,237 bytes, and ran at 2,225, 3,269, 3,857 and 481 (the
same code with the docstring cut, his "works, landed faster and exploded"). Every crash dump was one fault:
Starfield.exe+0x28281B5, reading 0xFFFFFFFFFFFFFFFF. Papyrus debug info was loaded (bLoadDebugInformation=1);
whether it crashes with that off is unmeasured. So the compiler writes into a temp folder, the string table
is read, and a .pex goes into the mod only if its longest string is within PROVEN_STRING: the largest
measured to RUN, not a guess at the wall (4,096 is the guess). Put history in ; comments: they are not
compiled.
"""
from __future__ import annotations
import shutil, struct, subprocess, sys, tempfile, time
from pathlib import Path

HERE = Path(__file__).resolve().parent
STARFIELD = Path(r"C:/Program Files (x86)/Steam/steamapps/common/Starfield")
COMPILER = STARFIELD / "Tools" / "Papyrus Compiler" / "PapyrusCompiler.exe"
BASE_SRC = STARFIELD / "Data" / "Scripts" / "Source"
FLAGS = BASE_SRC / "Starfield_Papyrus_Flags.flg"
NAME_LIMIT = 38
PROVEN_STRING = 3857   # bytes: the longest .pex string measured to run (duo_podprobe v3); 4,468 crashed


def longest_string(pex: Path) -> tuple[int, str]:
    """The longest entry in a .pex string table, and its first words. Header: magic, version, game id,
    compile time (16 bytes), then three length-prefixed strings (source, user, machine), then the table."""
    b = pex.read_bytes()
    i = 16
    for _ in range(3):
        n = struct.unpack_from("<H", b, i)[0]
        i += 2 + n
    count = struct.unpack_from("<H", b, i)[0]
    i += 2
    best, text = 0, ""
    for _ in range(count):
        n = struct.unpack_from("<H", b, i)[0]
        if n > best:
            best, text = n, b[i + 2:i + 2 + 60].decode("utf-8", "replace").split("\n")[0]
        i += 2 + n
    return best, text


def main(argv: list[str]) -> int:
    if len(argv) < 3:
        print(__doc__)
        return 2
    data = Path(argv[1])
    out = data / "scripts"
    for p, what in ((COMPILER, "compiler"), (BASE_SRC, "base game script source"), (out, "mod scripts folder")):
        if not p.exists():
            print(f"[FAIL] {what} not found: {p}")
            return 2

    failed = 0
    for name in argv[2:]:
        stem = name[:-4] if name.lower().endswith(".psc") else name
        psc = HERE / f"{stem}.psc"
        if not psc.exists():
            # gen_delve writes a beats Delve's fragment script here (generated, never hand-edited).
            psc = HERE / "gen" / f"{stem}.psc"
        if not psc.exists():
            print(f"[FAIL] no such library script: {psc}")
            failed += 1
            continue
        if len(stem) > NAME_LIMIT:
            print(f"[FAIL] {stem} is {len(stem)} chars; the compiler refuses over {NAME_LIMIT}.")
            failed += 1
            continue
        pex = out / f"{stem}.pex"
        tmp = Path(tempfile.mkdtemp(prefix="papyrus_"))
        built = tmp / f"{stem}.pex"
        started = time.time()
        r = subprocess.run([str(COMPILER), psc.name, f"-i={psc.parent};{HERE};{BASE_SRC}", f"-o={tmp}", f"-f={FLAGS}"],
                           capture_output=True, text=True, errors="replace")
        fresh = built.exists() and built.stat().st_mtime >= started - 1
        if "Compilation succeeded" in (r.stdout or "") and fresh:
            longest, text = longest_string(built)
            if longest > PROVEN_STRING:
                failed += 1
                print(f"[FAIL] {stem}: its longest string is {longest} B, over the {PROVEN_STRING} B proven to run; "
                      f"4,468 B crashed the game on first call (see this file's header). Starts: {text!r}")
                print(f"       nothing written to {out}; move the history into ; comments")
            else:
                shutil.copy2(built, pex)
                print(f"[ok]   {stem} -> {pex} ({pex.stat().st_size} B, longest string {longest} B)")
            shutil.rmtree(tmp, ignore_errors=True)
        else:
            shutil.rmtree(tmp, ignore_errors=True)
            failed += 1
            print(f"[FAIL] {stem}")
            for line in (r.stdout or "").splitlines() + (r.stderr or "").splitlines():
                if line.strip() and not line.startswith(("Papyrus Compiler", "Copyright", "Starting", "Batch")):
                    print("       " + line.strip())
            if not fresh:
                print(f"       and no fresh .pex at {built}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
