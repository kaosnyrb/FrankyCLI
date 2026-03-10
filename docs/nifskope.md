# NifSkope — Key Details

NIF file viewer/editor for Bethesda games (Morrowind → Starfield). Qt-based, OpenGL 4 rendering, written in C++20.

**Repo:** `C:\Git\nifskope`
**Build:** qmake (`NifSkope.pro`) — not CMake
**License:** BSD

---

## Build

```bash
qmake6 NifSkope.pro
make -j8
# Output: release/ or debug/
```

Requires Qt 6.4+ (or Qt 5.15). MSVC 2015+, GCC 13+, or Clang. AVX2 enabled by default; override with `noavx2=1`.

---

## Architecture

Qt MVC pattern with a compile-time "Spell" plugin system.

```
NifModel (QAbstractItemModel)
    ↓ data
NifProxyModel (hierarchy/filter)
    ↓ view
NifTreeView + GLView (OpenGL viewport)
```

**IPC:** UDP socket on port 12583 (release) / 12584 (debug) for single-instance file loading and `nif://` URL scheme.

---

## Key Directories

| Path | Purpose |
|------|---------|
| `src/model/` | NIF data model (NifModel, NifItem, NifValue) |
| `src/gl/` | OpenGL renderer, scene graph, mesh/texture rendering |
| `src/spells/` | All edit operations (the "spell" plugin system) |
| `src/ui/` | Qt widgets, dialogs, settings |
| `src/io/` | Binary NIF stream read/write, material file parsing |
| `src/xml/` | NIF.XML parser + expression evaluator |
| `lib/libfo76utils/` | Starfield/FO76 material DB, BA2 archive reader, DDS decompressor |
| `lib/meshoptimizer/` | Mesh simplification/optimization (submodule) |
| `build/nif.xml` | Master NIF format specification (XML-driven schema) |
| `res/shaders/` | GLSL shaders (per-game variants) |

---

## Key Source Files

| File | Role |
|------|------|
| `src/main.cpp` | Entry point, IPC setup, XML loading |
| `src/nifskope.cpp` | Main window, file I/O, menus |
| `src/model/nifmodel.cpp` | Core NIF data model |
| `src/gl/renderer.cpp` | OpenGL 4 renderer |
| `src/gl/glscene.cpp` | Scene graph, transforms, animation |
| `src/gl/BSMesh.cpp` | Modern BSTriShape rendering (Fallout 4+, Starfield) |
| `src/gl/bsshape.cpp` | BS* shape types rendering |
| `src/gl/gltex.cpp` | Texture loading/caching |
| `src/spellbook.cpp` | Spell registry, context menus |
| `src/gamemanager.cpp` | Game detection, archive loading, material DB |

---

## Spell System

Spells are compile-time plugins — no runtime loading. Each spell is a subclass of `Spell` registered with a macro:

```cpp
class MySpell : public Spell {
    QString name() const override { return "My Spell"; }
    QString page() const override { return "Mesh"; }
    bool isApplicable(const NifModel*, const QModelIndex&) override { ... }
    QModelIndex cast(NifModel*, const QModelIndex&) override { ... }
};
REGISTER_SPELL(MySpell);
```

`SpellBook` auto-collects spells into context menus by `page()`. Some spells are "instant" (toolbar buttons) or "sanity" (run on sanitize pass).

Spell categories: Animation, Blocks, Bounds, Color, Flags, Havok, Light, MaterialEdit, Mesh, Normalize, Optimize, Sanitize, Simplify, Skeleton, TangentSpace, Texture, Transform.

---

## Starfield-Specific Features

- **Game mode:** `Game::GameMode::STARFIELD` in `src/gamemanager.h`
- **Material DB:** `lib/libfo76utils/src/bsmatcdb.hpp` — CE2MaterialDB for `.mat` files
- **Geometry:** `BSMesh.cpp` / `bsshape.cpp` — BSTriShape with GPU vertex buffers
- **Material export:** `src/spells/sfmatexport.cpp`
- **Archives:** BA2 format via `lib/libfo76utils/src/ba2file.hpp`
- **PBR rendering:** Radiance IBL, cubemaps, PBR LUT (`lib/libfo76utils/src/pbr_lut.cpp`)
- **glTF export/import:** Geometry-only (no textures on import); Starfield geometry supported
- **Internal geometry:** NIF-embedded mesh data (not external `.mesh` files) supported

---

## Game Manager

`GameManager` handles multi-game resource resolution. Key enum:

```cpp
namespace Game {
    enum GameMode {
        OTHER, MORROWIND, OBLIVION, FALLOUT_3NV,
        SKYRIM, SKYRIM_SE, FALLOUT_4, FALLOUT_76, STARFIELD
    };
}
```

Auto-detects game from NIF version numbers. Loads game paths, archives, and material databases on startup.

---

## Format Spec: nif.xml

`build/nif.xml` is the master NIF format definition. `NifExpr` evaluates XML `<condition>` attributes at runtime (e.g. `NumPartitions > 0`). Changes to nif.xml don't require recompilation of the model code — the parser is data-driven.

---

## Starfield Mesh & Geometry Pipeline

### Overview: NIF → .mesh relationship

Starfield separates geometry from the scene graph. The `.nif` holds the block hierarchy, transforms, materials, and metadata. Actual vertex/triangle data lives in a separate binary `.mesh` file stored in the `geometries/` folder.

```
meshes/retrograde/marker_small.nif          ← scene graph, BSGeometry block
    └─ Mesh Path: "retrograde\rg_small_marker"
geometries/retrograde/rg_small_marker.mesh  ← vertex/triangle binary
```

The NIF stores only the short relative path (no folder prefix, no extension). NifSkope's `GameManager::get_full_path()` normalises it at runtime: lowercases, prepends `geometries/`, appends `.mesh`.

### NIF Block: BSGeometry (Starfield only)

Defined in `build/nif.xml`. The key block replacing BSTriShape for Starfield:

```
BSGeometry
├── Bounding Sphere     (NiBound)
├── Bounding Box        (BSBoundingBox)
├── Skin                (Ref → NiObject / BSSkin::Instance)
├── Shader Property     (Ref → BSShaderProperty)
├── Alpha Property      (Ref → NiAlphaProperty)
└── Meshes[4]           (BSMeshArray — one entry per LOD level)
        └── BSMesh
                ├── Mesh Path   (string, used when Flags == 0)  ← external .mesh
                └── Mesh Data   (BSMeshData, used when Flags & 0x200)  ← inline
```

Up to 4 LOD levels. LOD 0 = highest detail. If `Flags & 0x200` is set the geometry is embedded inline in the NIF; otherwise NifSkope loads the external `.mesh` file.

### .mesh Binary Format

Parsed by `src/io/MeshFile.cpp`. All values little-endian.

```
uint32  version         — 0, 1, or 2 (>2 = invalid)
uint32  indices_count   — number of uint16 index values (triangles = indices_count / 3)
uint16  indices[]       — triangle indices, indices_count entries
float   scale           — world-space scale applied to all positions
uint32  weights_per_vert
uint32  num_verts

uint32  num_verts       (num_verts × 6 bytes each)
byte[6] positions[]     — compressed SNorm16: 4 bytes xy packed, 2 bytes z
                          decode: x = int16(xy & 0xFFFF) / 32767.0 × scale

uint32  num_uv1         (num_uv1 × 4 bytes each — float16 pair)
uint32  num_uv2         (num_uv2 × 4 bytes each — float16 pair)
uint32  num_colors      (num_colors × 4 bytes each — BGRA uint8, shuffle to RGBA)
uint32  num_normals     (num_normals × 4 bytes each — UDecVector4 packed 10:10:10:2)
uint32  num_tangents    (num_tangents × 4 bytes each — UDecVector4, W = bitangent sign)
uint32  num_weights     (num_weights × 4 bytes each — uint16 boneIndex | uint16 weight)

# Only present if version >= 1:
uint32  num_lods
  per LOD:
    uint32  lod_indices_count
    uint16  lod_indices[]

# Only present if version == 2:
meshlet and cull data blocks (GPU-driven culling)
```

**Retrograde marker_small stats (confirmed from binary):**
- version=2, 24 triangles, 48 verts, scale=3.1 Starfield units
- uv1=48, no uv2, no vertex colors, 48 normals, 48 tangents, not skinned
- 0 LODs, ~48 bytes of meshlet/cull data at end

### Vertex Compression Details

| Attribute | Storage | Decode |
|-----------|---------|--------|
| Position | 6 bytes: uint32 XY + uint16 Z (SNorm16) | `int16 / 32767.0 × scale` |
| UV | 4 bytes: two float16 | Direct float16 to float32 |
| Color | 4 bytes: BGRA uint8 | Shuffle to RGBA, divide by 255 |
| Normal | 4 bytes: UDecVector4 (10:10:10:2) | Map [0,1023] → [-1, 1] |
| Tangent | 4 bytes: UDecVector4, W = bitangent sign | W used for: `bitangent = cross(N, T × W)` |
| Bone weight | 4 bytes: uint16 boneIndex + uint16 weight | `weight / 65535.0` |

### Path Resolution

`GameManager::get_full_path(path, "geometries/", ".mesh")`:
1. Lowercase + forward slashes
2. If `geometries/` not found in path → prepend it
3. Replace or append `.mesh` extension

So `"retrograde\rg_small_marker"` → `"geometries/retrograde/rg_small_marker.mesh"`.

Then looked up in BA2 archives or loose files via `GameResources::get_file()`.

### LOD Strategy

Two approaches Starfield uses:
- **Internal LODs:** Single `.mesh` with `num_lods > 0` — LOD triangles share vertex pool
- **Separate LOD files:** Four distinct `.mesh` paths in BSGeometry's `Meshes[0..3]`

NifSkope picks: if `meshes[0]->lods.size() > 0` use internal; otherwise index into the meshes array by `lodLevel`.

### Source Files for Geometry

| File | Role |
|------|------|
| `src/gl/BSMesh.cpp` | Loads up to 4 LOD MeshFiles, picks active LOD, uploads to GPU |
| `src/gl/BSMesh.h` | `BSMesh` class — `QVector<shared_ptr<MeshFile>> meshes` |
| `src/gl/bsshape.cpp` | Older engine BSTriShape (SSE/FO4/FO76) with inline vertex data |
| `src/io/MeshFile.cpp` | Binary `.mesh` parser + BSMeshData inline reader |
| `src/io/MeshFile.h` | `MeshFile` struct: positions, coords1/2, colors, normals, tangents, lods |
| `src/spells/meshfilecopy.cpp` | Copies internal geometry between NIF records |
| `src/gamemanager.cpp` | `get_full_path()`, BA2 archive lookup |

### Reading Retrograde Meshes in NifSkope

1. Open `meshes/retrograde/marker_small.nif`
2. In the block list, select `BSGeometry` → expand `Meshes` → `BSMesh[0]` — shows `Mesh Path`
3. NifSkope auto-loads `geometries/retrograde/rg_small_marker.mesh` from the Starfield Data folder (must have Starfield path configured in Settings → Resources)
4. The 3D view renders the mesh with the assigned `BSEffectShaderProperty` material
5. Right-click the BSGeometry block for mesh spells (bounds update, vertex inspection, glTF export)

---

## Case Study: Beowulf Weapon — NIF Structure

`meshes/weapons/beowulf/` — 17 NIF files, each a single piece of the modular weapon system. Geometry `.mesh` files for vanilla content live inside BA2 archives (not loose files).

### ConnectPoint system

Starfield weapons snap attachments together via **ConnectPoint** nodes embedded in every NIF:

- **`BSConnectPoint::Parents`** — named `P-*` — sockets this NIF *exposes* (attachment points for child parts to plug into)
- **`BSConnectPoint::Children`** — named `C-*` — the slot this NIF *occupies* on its parent

Every attachment NIF has exactly one `C-*` entry identifying what category it fills (barrel, grip, etc.). The receiver NIF has several `P-*` entries, one per attachment slot.

### Attachment graph

```
beowulf.nif  (receiver — C-Receiver)
│  exposes: P-Barrel, P-Grip, P-Foregrip, P-ScopeFront, P-ScopeMiddle, P-Scope
│
├── P-Barrel ──→ beowulf_barrel.nif         (C-Barrel)
│                beowulf_barrel_long.nif    (C-Barrel)  — alt
│                beowulf_barrel_short.nif   (C-Barrel)  — alt
│                  └── P-Muzzle, P-ProjectileNode
│                       └── P-Muzzle ──→ beowulf_muzzlebrake.nif      (C-Muzzle)
│                                        beowulf_muzzleflashhider.nif  (C-Muzzle)
│                                        beowulf_muzzlesuppressor.nif  (C-Muzzle)
│
├── P-Grip ───→ beowulf_gripstandard.nif    (C-Grip)
│               beowulf_griptactical.nif   (C-Grip)    — alt
│
├── P-Foregrip → beowulf_foregrip.nif          (C-Foregrip)
│                beowulf_foregriplightlaser.nif (C-Foregrip) — alt
│
└── P-Scope ──→ beowulf_scope.nif           (C-Scope)
│               beowulf_scope2.nif          (C-Scope)   — alt
│               beowulf_sightsiron.nif      (C-Scope)   — alt
│               beowulf_sightsreflex.nif    (C-Scope)   — alt
│
── (standalone) beowulf_mag.nif
── (standalone) beowulf_ironsights_update_substance.nif
```

### Per-NIF summary

| NIF | C- slot | P- slots exposed | Materials (`.mat`) |
|-----|---------|------------------|--------------------|
| `beowulf.nif` | C-Receiver | P-Barrel, P-Grip, P-Foregrip, P-ScopeFront, P-ScopeMiddle, P-Scope | Beowulf_Receiver, Beowulf_Decals, Beowulf_MagCover |
| `beowulf_barrel.nif` | C-Barrel | P-Muzzle, P-ProjectileNode | Beowulf_Barrel, Beowulf_Decals, Beowulf_BarrelShort |
| `beowulf_barrel_long.nif` | C-Barrel | P-Muzzle, P-ProjectileNode | Beowulf_BarrelLong |
| `beowulf_barrel_short.nif` | C-Barrel | P-ProjectileNode, P-Muzzle | Beowulf_BarrelShort |
| `beowulf_foregrip.nif` | C-Foregrip | — | Beowulf_Foregrip, Beowulf_Decals |
| `beowulf_foregriplightlaser.nif` | C-Foregrip | — | Beowulf_Laser_Foregrip |
| `beowulf_gripstandard.nif` | C-Grip | — | Beowulf_Grip |
| `beowulf_griptactical.nif` | C-Grip | — | Beowulf_GripTactical |
| `beowulf_mag.nif` | — | — | Grendel_Mag, Grendel_Decals, CombaTech_EInk_* |
| `beowulf_muzzlebrake.nif` | C-Muzzle | P-ProjectileNode | Beowulf_MuzzleBrake |
| `beowulf_muzzleflashhider.nif` | C-Muzzle | P-ProjectileNode | Beowulf_Muzzle |
| `beowulf_muzzlesuppressor.nif` | C-Muzzle | P-ProjectileNode | Beowulf_Suppressor |
| `beowulf_scope.nif` | C-Scope | — | Beowulf_Scope_1, Grendel_ScopeDecals |
| `beowulf_scope2.nif` | C-Scope | — | Beowulf_Scope_2, Grendel_ScopeDecals |
| `beowulf_sightsiron.nif` | C-Scope | — | Beowulf_Sights, Grendel_Tritium |
| `beowulf_sightsreflex.nif` | C-Scope | — | Beowulf_ReflexSight, Reticle_Beowulf_1, Solstice_Reflex_Sight_Glass |
| `beowulf_ironsights_update_substance.nif` | — | — | (legacy BGSM references, older format) |

### Key observations

- **Every NIF has exactly 1 `BSGeometry` block** — one mesh piece per file, no LOD splitting at the NIF level
- **All use `BSLightingShaderProperty`** — PBR lit shader, not unlit effect shader
- **`P-ProjectileNode`** — present on all barrels and muzzles; this is where bullets spawn
- **Shared assets:** `beowulf_mag` reuses Grendel (Bullpup rifle) mag materials; scopes share `Grendel_ScopeDecals`
- **`.mat` files live in `Materials\Weapons\Beowulf\`** — CE2 material format, loaded via CE2MaterialDB
- **Havok physics** (`bhkNPCollisionObject`, `bhkPhysicsSystem`) is embedded in the receiver NIF only
- **`beowulf_ironsights_update_substance.nif`** uses old `.BGSM` material format — legacy substance painter export, likely superseded by the `.mat` variants

### Implication for Retrograde

When creating a custom weapon NIF:
1. One `BSGeometry` per NIF piece
2. Embed `BSConnectPoint::Children` with the correct `C-*` slot name matching the parent's `P-*`
3. `P-ProjectileNode` must be present on the barrel/muzzle that fires
4. Material path goes into `BSLightingShaderProperty` → material field
5. Geometry `.mesh` path is stored in `BSGeometry → Meshes[0] → Mesh Path` (relative, no prefix/extension)

---

## Starfield Geometry Bridge (StarfieldMeshConverter)

**Repo:** `C:\Git\StarfieldMeshConverter-master`
**Nexus:** nexusmods.com/starfield/mods/4360
**Blender:** 3.5–3.6 only (incompatible with 4.0+)

This is the answer to both open questions: **.mesh file creation** and **meshlet format**. It is a Blender plugin + C++ DLL pipeline that exports Blender meshes directly to Starfield `.mesh` and `.nif` files.

### Architecture

```
Blender mesh (Python plugin)
    → utils_primitive.Primitive.to_mesh_numpy_dict()   ← gather verts/normals/UVs/weights
    → MeshConverter.ExportMeshFromNumpy(numpy_dict, path)  ← C++ DLL call
        → DirectXMesh library  ← meshlet generation, vertex cache opt
        → Eigen / Miniball     ← transforms, bounding sphere
        → writes binary .mesh
    → NifIO C++ library  ← writes .nif
```

The C++ DLL wraps DirectXMesh (Microsoft), Eigen (linear algebra), and Miniball (bounding sphere). The Python side is Blender glue only.

### .mesh export pipeline

`MeshIO.py → ExportMesh() → MeshToJson() → MeshConverter.ExportMeshFromNumpy()`

Key steps:
1. Duplicate + triangulate mesh (`Triangulate` modifier via `get_obj_proxy`)
2. `Primitive.gather()` — collect positions, UVs, normals, vertex colors, weights from Blender loops
3. `to_mesh_numpy_dict()` — pack into numpy arrays
4. `ExportMeshFromNumpy(dict, path)` — C++ DLL writes binary `.mesh`

The DLL handles: vertex compression (SNorm16 positions, float16 UVs, UDecVector4 normals/tangents), meshlet generation, cull data, LOD indices.

### Meshlet cull data format — CONFIRMED

`CULLDATA_VERSION = 2` (as of current source). Each cull data entry is **6 floats = 24 bytes**:

```
float[3]  min_bounds   ← AABB minimum XYZ
float[3]  max_bounds   ← AABB maximum XYZ
```

This is a per-meshlet axis-aligned bounding box. Not a cone (that was version 1). Our Retrograde marker had 48 trailing bytes = **2 meshlets × 24 bytes**.

### ConnectPoint struct — CONFIRMED

`BSConnectPointParents::ConnectPoint` binary layout (40 bytes fixed + 2 length-prefixed strings):

```cpp
std::string parent_name;    // e.g. "Beowulf" (the NiNode name of the receiver mesh)
std::string child_name;     // e.g. "P-Barrel" (the socket name)
float rot_quat[4];          // quaternion [x, y, z, w]  — identity = {0,0,0,1}
float translation[3];       // position of socket in local space
float scale;                // usually 1.0
```

Total size per entry: `parent_name.length() + child_name.length() + 40`

### ConnectPoint workflow in Blender

The plugin uses a naming convention on Blender empty objects:

```
Objects named "CPA:<child_name>" → become ConnectPoint entries
    child_obj.name[4:]          → child_name  (e.g. "P-Barrel")
    parent mesh object.name     → parent_name (e.g. "Beowulf")
    child_obj.location          → translation
    child_obj.rotation_quaternion → rot_quat
    child_obj.scale[0]          → scale
```

So in Blender: add an Empty object, name it `CPA:P-Barrel`, position it at the barrel socket location on the receiver mesh, and parent it to the receiver object. The plugin picks it up automatically on export.

### NIF template system

Three C++ template classes (in `include/NifIO.h`) drive NIF construction:

| Template | Use |
|----------|-----|
| `NiArmatureTemplate` | Skeleton/node hierarchy + ConnectPoints + Havok physics |
| `NiSimpleGeometryTemplate` | Static geometry: mesh LOD paths, material path, bounding box |
| `NiSkinInstanceTemplate` | Skinned geometry: adds bone names, bone transforms |

The template is populated from JSON (from Blender), then `ToNif()` writes the NIF binary.

**Weapon NIF detection**: if root node is named `"WEAPON"` → `SubTemplate::Weapon` → `bsx_flags = 74`.

### Blender → Weapon NIF workflow

1. Model weapon parts in Blender (Blender 3.5–3.6)
2. Name the root armature/mesh `"WEAPON"` for the receiver
3. Add `CPA:P-*` empty objects as children of the receiver mesh, positioned at socket locations
4. Rig moving parts (trigger, slide, mag) to the weapon skeleton bones
5. In the SGB panel: disable Weights if static, enable if skinned
6. Hit "Export .mesh" — writes `.mesh` to geometries folder
7. NIF export also available — writes full `.nif` with BSGeometry, ConnectPoints, BSXFlags, material paths
8. Assets folder must point to loose-file extracted BA2s (only `meshes01.ba2` + `meshes02.ba2` needed for geometry lookup)

### Weapon skeleton bone names (from NifIO.h)

Key bones relevant to weapons:
```
WEAPON, WeaponLeft       ← weapon attachment bones on character
Root, COM, C_Spine*      ← character spine chain
R_Arm / L_Arm            ← hand/arm bones for grip IK
R_AnimObject1/2/3        ← generic attachment objects
L_AnimObject1/2/3
```

Moving weapon parts (trigger, slide, bolt) would be skinned to custom bones added as children of `WEAPON` in the weapon's own skeleton.

### Dependencies (C++ DLL)

| Lib | Use |
|-----|-----|
| DirectXMesh | Meshlet generation, vertex cache optimization |
| Eigen | Linear algebra (matrix decomposition, transforms) |
| Miniball | Minimum bounding sphere computation |
| nlohmann/json | JSON serialization between Python ↔ C++ |

---

## Notable Third-Party Libs

| Lib | Use |
|-----|-----|
| `lib/tiny_gltf.h` | glTF 2.0 read/write (header-only) |
| `lib/json.hpp` | JSON parsing (header-only) |
| `lib/meshoptimizer/` | Mesh simplification and vertex cache optimization |
| `lib/gli/` | GPU image library |
| `lib/qhull/` | Convex hull for collision shapes |
| `lib/miniball/` | Minimum bounding sphere |
| `lib/coacd.*` | Convex decomposition (approximate) |
