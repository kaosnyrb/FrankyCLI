# PlacedObject (REFR)

A PlacedObject is an **instance** of a base record placed in a Cell (or worldspace cell). It stores position, rotation, scale, and a large set of optional override properties.

## Key fields

| Field | Type | Notes |
|-------|------|-------|
| `Base` | `IFormLink<IPlaceableObjectGetter>` | The base record — Static, Activator, PackIn, NPC, Light, etc. |
| `Position` | `P3Float` | Absolute world position (overlay units for worldspace cells, local units for interior cells) |
| `Rotation` | `P3Float` | Euler angles in **radians** (X, Y, Z) |
| `Scale` | `float?` | Uniform scale — null = 1.0 |
| `EditorID` | `string?` | Required for connector/spawn markers; optional for structural objects |
| `Primitive` | `PlacedPrimitive?` | Attached trigger volume (Box, Sphere, etc.) |
| `VolumeData` | `Xvl2VolumeData?` | Post-processing volume (ImageSpace override) |
| `EnableParent` | `EnableParent?` | Link to a reference that enables/disables this object |
| `LinkedReferences` | `ExtendedList<LinkedReference>?` | Named reference links (e.g. teleport targets) |
| `TeleportDestination` | `TeleportDestination?` | For doors: destination cell + position |
| `StarfieldMajorRecordFlags` | flags | Various CK flags |

## Copying a PlacedObject

**Never use `DeepCopy()` when duplicating** — it preserves the original FormKey, causing ID collisions. Create a `new PlacedObject(targetMod)` for a fresh FormKey and copy all fields manually.

See `CellTools.CloneCellById` for the canonical full-field copy. Complete field list:

```csharp
var placed = new PlacedObject(RetrogradeContext.Current.TargetMod)
{
    Action = source.Action,
    AttachRef = source.AttachRef,
    Base = source.Base,
    BlueprintPartOrigin = source.BlueprintPartOrigin,
    BOLV = source.BOLV,
    Collision = source.Collision,
    Comments = source.Comments,
    Components = source.Components,
    ConstrainedDecal = source.ConstrainedDecal,
    Count = source.Count,
    CurrentZoneCell = source.CurrentZoneCell,
    DebugText = source.DebugText,
    EditorID = source.EditorID,
    Emittance = source.Emittance,
    EnableParent = source.EnableParent,
    EncounterZone = source.EncounterZone,
    ExternalEmittance = source.ExternalEmittance,
    FactionRank = source.FactionRank,
    GeometryDirtinessScale = source.GeometryDirtinessScale,
    GroupedPackIn = source.GroupedPackIn,
    HeadTrackingWeight = source.HeadTrackingWeight,
    HealthPercent = source.HealthPercent,
    IsActivationPoint = source.IsActivationPoint,
    IsIgnoredBySandbox = source.IsIgnoredBySandbox,
    IsLinkedRefTransient = source.IsLinkedRefTransient,
    Layer = source.Layer,
    LayeredMaterialSwaps = source.LayeredMaterialSwaps,
    LevelModifier = source.LevelModifier,
    LightArea = source.LightArea,
    LightBarndoorData = source.LightBarndoorData,
    LightColors = source.LightColors,
    LightFlicker = source.LightFlicker,
    LightGobo = source.LightGobo,
    Lighting = source.Lighting,
    LightLayerData = source.LightLayerData,
    LightRoundedness = source.LightRoundedness,
    LightStaticShadowMap = source.LightStaticShadowMap,
    LightVolumetricData = source.LightVolumetricData,
    LinkedReferences = source.LinkedReferences,
    LocationRefTypes = source.LocationRefTypes,
    Lock = source.Lock,
    MapMarker = source.MapMarker,
    NavigationDoorLink = source.NavigationDoorLink,
    NumTraversalFluffBytes = source.NumTraversalFluffBytes,
    OpenByDefault = source.OpenByDefault,
    Ownership = source.Ownership,
    Patrol = source.Patrol,
    PersistentLocation = source.PersistentLocation,
    PowerLinks = source.PowerLinks,
    Primitive = source.Primitive,
    ProjectedDecal = source.ProjectedDecal,
    ProjectedDecalReferences = source.ProjectedDecalReferences,
    Properties = source.Properties,
    Radius = source.Radius,
    RagdollBipedRotation = source.RagdollBipedRotation,
    RagdollData = source.RagdollData,
    ReferenceGroup = source.ReferenceGroup,
    Rotation = source.Rotation,
    Scale = source.Scale,
    ShipArrival = source.ShipArrival,
    SnapLinks = source.SnapLinks,
    SourcePackIn = source.SourcePackIn,
    Spline = source.Spline,
    StarfieldMajorRecordFlags = source.StarfieldMajorRecordFlags,
    TeleportDestination = source.TeleportDestination,
    TeleportName = source.TeleportName,
    TimeOfDay = source.TimeOfDay,
    Traversals = source.Traversals,
    VirtualMachineAdapter = source.VirtualMachineAdapter,
    VolumeData = source.VolumeData,
    XALG = source.XALG,
    XCZA = source.XCZA,
    XFLG = source.XFLG,
    XNSE = source.XNSE,
    XPCK = source.XPCK,
    Position = worldPos, // override position/rotation as needed
};
```

### Cloning from getter types (template mods)

Template mods return `IPlacedObjectGetter`. Conversion table:

| Source type | Assignment pattern |
|---|---|
| Simple value (int, float, bool, enum, P3Float) | Assign directly |
| `IFormLinkNullableGetter<T>` | `source.Foo.FormKey.ToNullableLink<T>()` — guard with `if (!source.Foo.IsNull)` |
| `IFormLinkGetter<T>` | `source.Foo.FormKey.ToLink<T>()` |
| Complex sub-record (`IFooGetter`) | `source.Foo?.DeepCopy()` |
| `IReadOnlyList<IFooGetter>` | `source.Foos?.Select(x => x.DeepCopy()).ToExtendedList()` |
| `IReadOnlyList<IFormLinkGetter<T>>` | `source.Foos?.ToExtendedList()` |
| `ReadOnlyMemorySlice<byte>?` | `source.Foo?.ToArray()` |

Required usings: `using Mutagen.Bethesda;` (extension methods), `using Mutagen.Bethesda.Starfield;`, `using Noggog;` (ExtendedList).

## Adding to a cell

```csharp
// Interior cell — use PlacementUtil helpers or direct list:
cell.Temporary.Add(placed);   // structural objects, lights
cell.Persistent.Add(placed);  // markers, connectors, game-logic objects

// Worldspace cell — PlacementUtil routes to correct SubCell:
state.PlacementUtil.AddToTemporary(state.instance, placed);
```

## PlacedPrimitive (trigger volume)

Invisible volume attached to a PlacedObject. Used for trigger boxes, room alert zones, post-effect volumes, etc.

```csharp
var placed = new PlacedObject(targetMod)
{
    Base = activator.ToLink<IPlaceableObjectGetter>(),
    Position = new P3Float(x, y, z),
    Rotation = new P3Float(0, 0, 0),
    Primitive = new PlacedPrimitive()
    {
        Bounds = new P3Float(sizeX, sizeY, sizeZ), // full extents (NOT half-extents)
        Color  = Color.FromArgb(255, 100, 100),    // editor visualization color
        Type   = PlacedPrimitive.TypeEnum.Box,
    }
};
```

- `Base` links to an `IActivatorGetter` defining trigger behavior
- `Bounds` is the **full** box size (half-extents would be half of this)
- `Type` can be `Box`, `Sphere`, etc.

### Enemy alert activators (Starfield.esm)

| EditorID | Behavior |
|----------|----------|
| `DMP_Room_SandboxEngagedPreferredDefend` | Defend (boss rooms) |
| `DMP_Room_PreferredDefend` | Prefer defend |
| `DMP_Room_EngagedPreferred` | Prefer engage |
| `DMP_Room_Engaged` | Engage |

Looked up via `gen_quest_main._StarfieldMod.Activators` by EditorID.

## VolumeData (post-processing)

```csharp
var placed = new PlacedObject(targetMod)
{
    Base = new FormKey(sfModKey, 0x00000043).ToLink<IPlaceableObjectGetter>(), // PostEffectVolume [STAT]
    Position = new P3Float(x, y, z),
    Primitive = new PlacedPrimitive()
    {
        Bounds = new P3Float(sx, sy, sz),
        Color  = Color.FromArgb(128, 200, 255),
        Type   = PlacedPrimitive.TypeEnum.Box,
    },
    VolumeData = new Xvl2VolumeData()
    {
        ImageSpace = imageSpaceFormKey.ToLink<IImageSpaceGetter>()
    }
};
```

### Known FormKeys for volume setup

| Purpose | Record | FormID |
|---------|--------|--------|
| Post-effect volume base | `PostEffectVolume [STAT]` | `Starfield.esm:00000043` |
| Space station ImageSpace | `LGT_LUT_SpaceStation_General_curve [IMGS]` | `Starfield.esm:0015078D` |

## World transform when unpacking prefabs

Rotate local position by a tile's yaw, then add to tile world origin:

```csharp
// From tile map (rotation in degrees, multiples of 90)
int yawSteps = map.tiles[x][y].rotation / 90;
var rotatedLocal = RgRotation.RotateYaw90(source.Position, yawSteps);
var worldPos = tilePos + rotatedLocal;
var worldRot = source.Rotation + RgRotation.RotationToP3Float(yawSteps);

// From parent PlacedObject (rotation in radians)
int yawSteps = (int)Math.Round(parent.Rotation.Z / (MathF.PI / 2f));
yawSteps = ((yawSteps % 4) + 4) % 4;
```

## Key files

- `PlacementUtil.cs` — `EnsureBaseImported`, `EnsureLightImported`, `ClonePlacedObject`
- `CellTools.cs` — `CloneCellById` (full field copy from getter), `EnsureImageSpaceImported`
- `TileInstantiationPass.cs` — worldspace prefab unpacking with cross-cell routing
- `WorldspacePlacementUtil.cs` — `PlacedObject` and `PlacedNpc` overloads

## Gotchas

- **Never `DeepCopy()`** a PlacedObject you intend to re-place — use `new PlacedObject(targetMod)` + manual copy
- **`IFormLinkNullableGetter<T>` is a struct** — never cast or compare to `null`; use `.IsNull` to check
- **Filter template-mod base records**: `if (source.Base.FormKey.ModKey.Name != "Starfield") return null;`
  — or use `EnsureBaseImported` to clone the foreign record into the target mod
- **`new T(targetMod)` only allocates a FormKey** — you must call `targetMod.RecordGroup.Add(record)` separately
