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
"""
from __future__ import annotations
import subprocess, sys, time
from pathlib import Path

HERE = Path(__file__).resolve().parent
STARFIELD = Path(r"C:/Program Files (x86)/Steam/steamapps/common/Starfield")
COMPILER = STARFIELD / "Tools" / "Papyrus Compiler" / "PapyrusCompiler.exe"
BASE_SRC = STARFIELD / "Data" / "Scripts" / "Source"
FLAGS = BASE_SRC / "Starfield_Papyrus_Flags.flg"
NAME_LIMIT = 38


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
            print(f"[FAIL] no such library script: {psc}")
            failed += 1
            continue
        if len(stem) > NAME_LIMIT:
            print(f"[FAIL] {stem} is {len(stem)} chars; the compiler refuses over {NAME_LIMIT}.")
            failed += 1
            continue
        pex = out / f"{stem}.pex"
        started = time.time()
        r = subprocess.run([str(COMPILER), psc.name, f"-i={HERE};{BASE_SRC}", f"-o={out}", f"-f={FLAGS}"],
                           capture_output=True, text=True, errors="replace")
        fresh = pex.exists() and pex.stat().st_mtime >= started - 1
        if "Compilation succeeded" in (r.stdout or "") and fresh:
            print(f"[ok]   {stem} -> {pex} ({pex.stat().st_size} B)")
        else:
            failed += 1
            print(f"[FAIL] {stem}")
            for line in (r.stdout or "").splitlines() + (r.stderr or "").splitlines():
                if line.strip() and not line.startswith(("Papyrus Compiler", "Copyright", "Starting", "Batch")):
                    print("       " + line.strip())
            if not fresh:
                print(f"       and no fresh .pex at {pex}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
