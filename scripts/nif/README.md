# scripts/nif: read, kitbash and look at Starfield NIFs, without NifSkope

Python 3, numpy and Pillow. Nothing here writes to the game folder; outputs go where you point them.

| tool | what it does |
|---|---|
| `sfnif.py` | The format: a NIF as header + blocks + footer, a field walk of the 8 block types a kitbash uses (NiNode, BSGeometry, BSLightingShaderProperty, NiIntegerExtraData, BSXFlags, NiStringExtraData, bhkNPCollisionObject, bhkPhysicsSystem), a byte-exact writer, and `merge`. Any other block type is REFUSED, because a block it cannot walk is a block whose links it cannot re-index. |
| `ba2get.py` | Pull one file out of a BTDX v2 GNRL archive by path. `--vanilla` searches the game's mesh archives, patch first. `--texture` reads a DX10 texture archive (v2 zlib, and the game's own v3 LZ4) and writes one mip as a PNG, decompressing only the chunk that holds it. |
| `smat.py` | What a material looks like: its first layer's albedo (texture or flat colour), tint and UV tiling, read off the material's own `Summary` block, following `Parent` where a layer inherits. Sources: a mod's `Main` archive, then the CK's raw `.mat` files in `Tools/ContentResources.zip`. |
| `kbounds.py` | A NIF tree's overall bounds, every geometry's box carried through its node transforms. Matches the game's own OBND records to within a few mm. |
| `kparts.py` | The same, per pasted object and per named geometry: what to anchor a part to. |
| `kitbash.py` | Build a kitbash from a JSON spec. Part 0 is the base and its root is the file's root (the shape NifSkope's Copy Branch produces); every other part is pasted under it, placed by raw `translate` or by an **anchor** resolved from measured boxes. Asserts each anchored part landed within 1 cm. |
| `packin_assets.py` | A PackIn as ONE render-ready JSON: the refs from `FrankyCLI packin export`, plus every NIF they use walked (the same walk `clay.py` draws with) to its geometries, each with its in-NIF `xform`, its `.mesh` and its first-layer albedo (texture PNG, or flat colour, plus tint and UV tiling). The files are extracted to `%TEMP%\FrankyCLI\asset-cache\` (override `PACKIN_ASSETS`) and the JSON points at them, so a consumer needs no archive code. A NIF's record is reused while every archive and the material zip it could read from are unchanged. The ref's own `pos`/`rot` is NOT applied: the REFR rotation axis order is unmeasured. Editor markers (`meshes/markers/`) carry `marker: true`. |
| `clay.py` | A grey, flat-shaded render, four views in one PNG, with the ground plane in red. A mesh it cannot find is listed and drawn as a box, never dropped. `--textured` paints each geometry with its material's first-layer albedo through the mesh UVs; an unresolved material is drawn grey and listed with the reason, and a material with no colour layer (normal-only decals, effects) is listed and not drawn. |

```json
{"name": "relaydish01", "parts": [
  {"nif": "path/to/base.nif"},
  {"nif": "path/to/dish.nif", "scale": 0.25, "rotate": [0, 0, 90],
   "anchor": {"on": "AntennaTech03:1", "at": "top", "sink": 0.04}}
]}
```

```
python kitbash.py spec.json out.nif
python clay.py out.nif out.png [--mod <plugin name>] [--textured]
python packin_assets.py packin.json out.json [--mod <plugin name>] [--max 512]
python test_nif.py
```

**Assets.** These tools extract and read the game's files on your own machine. Do not commit an
extracted NIF, `.mesh` or a render of one: this repo is public and those assets are not ours to
redistribute. The tests extract their fixtures into a temp folder at run time for the same reason.

**Paths.** `SF_DATA` overrides the default Steam `Data` folder. `SF_MATERIALS` overrides the material
sources (default: `Tools/ContentResources.zip` beside `Data`). `NIF_FIXTURES` adds a folder of your own
NIFs to the walk and round-trip tests.

**Textured renders, what they are not.** First layer only: grunge, dirt and paint-over layers are blended
by masks this does not evaluate, so a part reads cleaner than in game. No normal maps, no lighting beyond
the flat shade. The material sources are the CK's copy with their own date, so a later game patch can
change a material they do not know about: a preview, never evidence of what ships. Console texture
archives are skipped (tiled). ⚠ **The mod material path is UNPROVEN:** built and tested on vanilla only,
on a machine where no installed mod packs its own `.mat` files.

**Known limits.** ⚠ **No kitbash built by these tools has been seen IN GAME yet** (2026-10-08: the first, `relaydish01`, is checked in NifSkope and awaiting a play). Only yaw rotation has been exercised; an oblique turn is unproven. A kitbash
references Bethesda's `.mesh` files by hash, so a game update that repacks them can break it, the same
exposure as a kitbash made in NifSkope.
