# scripts/nif: read, kitbash and look at Starfield NIFs, without NifSkope

Python 3, numpy and Pillow. Nothing here writes to the game folder; outputs go where you point them.

| tool | what it does |
|---|---|
| `sfnif.py` | The format: a NIF as header + blocks + footer, a field walk of the 8 block types a kitbash uses (NiNode, BSGeometry, BSLightingShaderProperty, NiIntegerExtraData, BSXFlags, NiStringExtraData, bhkNPCollisionObject, bhkPhysicsSystem), a byte-exact writer, and `merge`. Any other block type is REFUSED, because a block it cannot walk is a block whose links it cannot re-index. |
| `ba2get.py` | Pull one file out of a BTDX v2 GNRL archive by path. `--vanilla` searches the game's mesh archives, patch first. |
| `kbounds.py` | A NIF tree's overall bounds, every geometry's box carried through its node transforms. Matches the game's own OBND records to within a few mm. |
| `kparts.py` | The same, per pasted object and per named geometry: what to anchor a part to. |
| `kitbash.py` | Build a kitbash from a JSON spec. Part 0 is the base and its root is the file's root (the shape NifSkope's Copy Branch produces); every other part is pasted under it, placed by raw `translate` or by an **anchor** resolved from measured boxes. Asserts each anchored part landed within 1 cm. |
| `clay.py` | A grey, flat-shaded render, four views in one PNG, with the ground plane in red. Shape, placement and silhouette only: no materials or textures. A mesh it cannot find is listed and drawn as a box, never dropped. |

```json
{"name": "relaydish01", "parts": [
  {"nif": "path/to/base.nif"},
  {"nif": "path/to/dish.nif", "scale": 0.25, "rotate": [0, 0, 90],
   "anchor": {"on": "AntennaTech03:1", "at": "top", "sink": 0.04}}
]}
```

```
python kitbash.py spec.json out.nif
python clay.py out.nif out.png [--mod <plugin name>]
python test_nif.py
```

**Assets.** These tools extract and read the game's files on your own machine. Do not commit an
extracted NIF, `.mesh` or a render of one: this repo is public and those assets are not ours to
redistribute. The tests extract their fixtures into a temp folder at run time for the same reason.

**Paths.** `SF_DATA` overrides the default Steam `Data` folder. `NIF_FIXTURES` adds a folder of your own
NIFs to the walk and round-trip tests.

**Known limits.** ⚠ **No kitbash built by these tools has been seen IN GAME yet** (2026-10-08: the first, `relaydish01`, is checked in NifSkope and awaiting a play). Only yaw rotation has been exercised; an oblique turn is unproven. A kitbash
references Bethesda's `.mesh` files by hash, so a game update that repacks them can break it, the same
exposure as a kitbash made in NifSkope.
