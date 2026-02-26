# Mutagen API Patterns

Cross-cutting patterns for reading and writing Starfield records with Mutagen.Bethesda.

## Required usings

```csharp
using Mutagen.Bethesda;           // ToLink<T>(), ToNullableLink<T>() extension methods
using Mutagen.Bethesda.Plugins;   // FormKey, ModKey
using Mutagen.Bethesda.Starfield; // All Starfield record types
using Noggog;                     // ExtendedList<T>, P2Int, P3Float, etc.
```

`using Mutagen.Bethesda;` is the most commonly forgotten. Without it, `ToLink<T>()` and `ToNullableLink<T>()` are not found — even if `Mutagen.Bethesda.Plugins` is present.

## FormLink patterns

### Creating a FormKey

```csharp
var formKey = new FormKey(ModKey.FromFileName("Starfield.esm"), 0x00000043);
// or from a known mod key:
var formKey = new FormKey(sfModKey, 0x02447F);
```

### ToLink vs ToNullableLink

```csharp
// Non-nullable (required reference)
IFormLink<IStaticGetter> link = formKey.ToLink<IStaticGetter>();
// or from a record:
IFormLink<IPlaceableObjectGetter> link = record.ToLink<IPlaceableObjectGetter>();

// Nullable (optional reference)
IFormLinkNullable<ICellGetter> link = cell.ToNullableLink<ICellGetter>();
```

### IFormLinkNullable is a struct — never null

`IFormLinkNullableGetter<T>` is a **value type**. When the link is unset, `.IsNull = true` and `.FormKey = FormKey.Null`. Comparing to C# `null` always returns false; assigning C# `null` crashes inside Mutagen's `SetTo`.

```csharp
// Correct check:
if (!source.Emittance.IsNull)
    placed.Emittance = source.Emittance.FormKey.ToNullableLink<IEmittanceGetter>();

// Wrong — do not do this:
placed.Emittance = source.Emittance?.FormKey.ToNullableLink<IEmittanceGetter>(); // BAD
placed.Emittance = source.Emittance.IsNull ? null : ...;                         // BAD
```

### Set FormLinks after construction, not in initializer

```csharp
// Correct:
var packin = new PackIn(targetMod) { EditorID = "..." };
packin.Cell = cell.ToNullableLink<ICellGetter>(); // after construction

// Wrong:
var packin = new PackIn(targetMod)
{
    Cell = cell.ToNullableLink<ICellGetter>(), // BAD — crashes at runtime
};
```

## Cloning an entire record from starfieldMod (IStarfieldModGetter)

`starfieldMod` (the literal `Starfield.esm` reference) is an `IStarfieldModGetter`. Every property it exposes returns a getter interface (e.g. `ICellGetter`), not a mutable type. Attempting to copy fields directly causes **CS0266** (cannot convert `IFooGetter` to `Foo`).

**Fix: call `.DeepCopy()` on the entire record first**, then copy fields from the fully mutable result:

```csharp
// Find the source getter
ICellGetter? sourceGetter = null;
foreach (var block in starfieldMod.Cells)
    foreach (var sub in block.SubBlocks)
        foreach (var c in sub.Cells)
            if (c.FormKey.ID == 0x00138C3E) { sourceGetter = c; break; }

// DeepCopy() → fully mutable Cell; safe to read all fields
Cell srcCell = sourceGetter.DeepCopy();

// Now copy into new record (struct copies are safe in initializer)
var newCell = new Cell(targetMod)
{
    Music     = srcCell.Music,      // direct struct copy — ok
    Lighting  = srcCell.Lighting,   // complex sub-record already mutable via DeepCopy
};
```

Same pattern for `PlacedObject` entries inside a cell's Persistent/Temporary lists:

```csharp
foreach (var poRef in srcCell.Persistent)
{
    var src = poRef.DeepCopy(); // mutable PlacedObject
    var po  = new PlacedObject(targetMod) { Position = src.Position, ... };
}
```

## Cloning from getter types (template mods)

Template mods are `IStarfieldModGetter` and return getter interfaces. Conversion table when copying into a mutable record:

| Source type | Assignment pattern |
|---|---|
| `int`, `float`, `bool`, `enum`, `P3Float` | Assign directly |
| `IFormLinkNullableGetter<T>` | `if (!src.Foo.IsNull) dest.Foo = src.Foo.FormKey.ToNullableLink<T>();` |
| `IFormLinkGetter<T>` | `dest.Foo = src.Foo.FormKey.ToLink<T>();` |
| Complex sub-record (`IFooGetter`) | `dest.Foo = src.Foo?.DeepCopy();` |
| `IReadOnlyList<IFooGetter>` | `dest.Foos = src.Foos?.Select(x => x.DeepCopy()).ToExtendedList();` |
| `IReadOnlyList<IFormLinkGetter<T>>` | `dest.Foos = src.Foos?.ToExtendedList();` (compatible types) |
| `ReadOnlyMemorySlice<byte>?` | `dest.Foo = src.Foo?.ToArray();` |

## Creating new records

`new T(targetMod)` allocates a **fresh FormKey** but does **not** add the record to the mod. You must call `targetMod.RecordGroup.Add(record)` separately.

```csharp
var light = new Light(targetMod) { EditorID = "...", Radius = 300 };
targetMod.Lights.Add(light); // REQUIRED
```

## Filtering records by mod source

```csharp
if (source.Base.FormKey.ModKey.Name != "Starfield") return null; // skip non-Starfield bases
```

## Importing foreign base records

When a placed object's `Base` points to a template mod record (not Starfield.esm), clone it into the target mod before placing:

```csharp
private static FormKey EnsureLightImported(ILightGetter source, StarfieldMod targetMod)
{
    var existing = targetMod.Lights.FirstOrDefault(l => l.EditorID == source.EditorID);
    if (existing != null) return existing.FormKey;

    var copy = new Light(targetMod) { EditorID = source.EditorID, Radius = source.Radius };
    targetMod.Lights.Add(copy);
    return copy.FormKey;
}
```

Same pattern for `Statics`, `Activators`, `ImageSpaces`, etc. See `PlacementUtil.cs` (`EnsureBaseImported`, `EnsureLightImported`) and `CellTools.cs` (`EnsureImageSpaceImported`).

## Inspecting unknown Mutagen types with ilspycmd

When you don't know what properties a Mutagen type has, decompile the DLL directly. This is much faster than searching NuGet docs.

### Find the installed DLL

```bash
ls "C:/Users/kaosn/.nuget/packages/mutagen.bethesda.starfield/"
# → 0.53.1  (or whatever version is installed)
```

Path: `C:/Users/kaosn/.nuget/packages/mutagen.bethesda.starfield/<version>/lib/net8.0/Mutagen.Bethesda.Starfield.dll`

### Decompile a specific type

```bash
ilspycmd "C:/Users/kaosn/.nuget/packages/mutagen.bethesda.starfield/0.53.1/lib/net8.0/Mutagen.Bethesda.Starfield.dll" \
  -t "Mutagen.Bethesda.Starfield.SpaceshipAIActorComponent"
```

### Find the Mutagen class name from a CK name

CK record types follow the pattern `BGSFoo_Component` → Mutagen class `FooComponent` (strip `BGS` prefix and `_` separator).

```bash
ilspycmd "...Mutagen.Bethesda.Starfield.dll" -l type 2>&1 | grep -i "spaceshipai"
```

## Namespace / folder naming hazard

Avoid naming namespaces or folders the same as Mutagen Starfield record types (e.g. `Worldspace`, `Cell`, `Location`). This causes `CS0118: 'X' is a namespace but is used like a type`.

If a clash is unavoidable, add a using alias:

```csharp
using SfWorldspace = Mutagen.Bethesda.Starfield.Worldspace;
```
