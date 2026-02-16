# FrankyCLI

Starfield procedural dungeon generation tool using Mutagen.Bethesda.

## PlacedPrimitives

PlacedPrimitives are invisible volume boxes/shapes used for trigger areas in Starfield. They're attached to `PlacedObject` records.

### Creating a PlacedPrimitive Box

```csharp
var placed = new PlacedObject(gen_quest_main.myMod)
{
    Count = 1,
    Position = new P3Float(x, y, z),
    Rotation = new P3Float(0, 0, 0),
    Base = activator.ToLink<IPlaceableObjectGetter>(),
    Primitive = new PlacedPrimitive()
    {
        Bounds = new P3Float(sizeX, sizeY, sizeZ),  // extents, not half-extents
        Color = Color.FromArgb(255, 100, 100),       // editor visualization color
        Type = PlacedPrimitive.TypeEnum.Box
    }
};

state.PlacementUtil.AddToTemporary(state.instance, placed);
```

### Key Points

- `Base` links to an `IActivatorGetter` that defines the trigger behavior (e.g., enemy alert types)
- `Bounds` is the full size of the box (not half-extents)
- `Type` can be `Box`, `Sphere`, etc.
- Use `PlacementUtil.AddToTemporary()` to add to the dungeon cell
- Activators are looked up from `gen_quest_main._StarfieldMod.Activators` by EditorID

### Enemy Alert Activators

Common alert activator EditorIDs:
- `DMP_Room_SandboxEngagedPreferredDefend` - Defend behavior (used for boss rooms)
- `DMP_Room_PreferredDefend`
- `DMP_Room_EngagedPreferred`
- `DMP_Room_Engaged`

## XVL2 Volume Data

PlacedObjects can have `VolumeData` for post-processing effects using `Xvl2VolumeData`.

### Post-Effect Volume Example

```csharp
var placed = new PlacedObject(gen_quest_main.myMod)
{
    Position = new P3Float(x, y, z),
    Base = postEffectStatic.ToLink<IPlaceableObjectGetter>(),
    Primitive = new PlacedPrimitive()
    {
        Bounds = new P3Float(sx, sy, sz),
        Color = Color.FromArgb(128, 200, 255),
        Type = PlacedPrimitive.TypeEnum.Box
    },
    VolumeData = new Xvl2VolumeData()
    {
        ImageSpace = imageSpaceFormKey.ToLink<IImageSpaceGetter>()
    }
};
```

### Key FormKeys

- `PostEffectVolume [STAT:00000043]` - Static used as base for post-effect volumes
- `LGT_LUT_SpaceStation_General_curve [IMGS:0015078D]` - Example ImageSpace for space station look

### Creating FormKeys for Starfield Records

```csharp
var formKey = new FormKey(ModKey.FromFileName("Starfield.esm"), 0x00000043);
```

## Copying PlacedObjects

When duplicating a `PlacedObject` from a prefab cell into the world, do NOT use `DeepCopy()` — it preserves the original FormKey, causing ID collisions. Instead, create a `new PlacedObject(RetrogradeContext.Current.TargetMod)` (which assigns a fresh FormKey) and copy all properties manually. See `CellTools.CloneCellById` for the canonical pattern.

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
    Radius = source.Radius,
    RagdollBipedRotation = source.RagdollBipedRotation,
    Properties = source.Properties,
    RagdollData = source.RagdollData,
    ReferenceGroup = source.ReferenceGroup,
    StarfieldMajorRecordFlags = source.StarfieldMajorRecordFlags,
    Rotation = source.Rotation,
    Scale = source.Scale,
    ShipArrival = source.ShipArrival,
    SnapLinks = source.SnapLinks,
    SourcePackIn = source.SourcePackIn,
    TeleportDestination = source.TeleportDestination,
    TeleportName = source.TeleportName,
    Spline = source.Spline,
    TimeOfDay = source.TimeOfDay,
    Traversals = source.Traversals,
    VolumeData = source.VolumeData,
    VirtualMachineAdapter = source.VirtualMachineAdapter,
    XALG = source.XALG,
    XCZA = source.XCZA,
    XFLG = source.XFLG,
    XNSE = source.XNSE,
    XPCK = source.XPCK,
    Position = worldPos // override position/rotation as needed
};
```
