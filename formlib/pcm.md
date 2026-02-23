# Planet Content Manager (PCM)

The PCM system controls which worldspace POIs appear on planets and under what conditions. Three Mutagen record types are involved.

## Tree structure

```
PlanetContentManagerBranchNode  (NodeType=BranchNode, NodeTypeOption=1)
  └─ PlanetContentManagerBranchNode  (NodeType=ContentNode, NodeTypeOption=2)
       │   Has BGSPlanetContentManagerContentProperties_Component
       └─ PlanetContentManagerContentNode  (leaf)
                Content = Worldspace FormKey
```

Parent–child wiring is **bidirectional**:
- Child sets `ParentNode` → parent's FormKey
- Parent adds child to `Nodes` (`ExtendedList<IFormLinkGetter<IPlanetNodeGetter>>`)

> **Note**: `Nodes` on a BranchNode is the child link list, but Starfield builds the runtime tree from each child's `ParentNode` back-reference. The `Nodes` list is legacy/optional — **do not add `Nodes.Add(...)` calls** in PCM passes; the game ignores it.

## Root hook nodes in Starfield.esm

| PCM category | Root EditorID | FormID |
|---|---|---|
| Block creation (spawned POIs) | `PCM_BlockCreation_PrimaryContent` | `Starfield.esm:00225373` |
| Planet scan (visible from orbit) | `PCM_ScanPlanet_General` | `Starfield.esm:0026F5DF` |
| Quest location requests | `PCM_LocationRequest_General` | `Starfield.esm:000F35E4` |

The top BranchNode's `ParentNode` must point to the appropriate root or the game ignores the entry.

## Record types and Mutagen names

| CK name | Mutagen class | Mod group |
|---------|---------------|-----------|
| `PCMB` branch node | `PlanetContentManagerBranchNode` | `targetMod.PlanetContentManagerBranchNodes` |
| `PCMC` content node | `PlanetContentManagerContentNode` | `targetMod.PlanetContentManagerContentNodes` |

## Creating a full PCM entry

### Step 1: Top BranchNode (find-or-create, shared per mod run)

```csharp
var topBranch = targetMod.PlanetContentManagerBranchNodes
    .FirstOrDefault(n => n.EditorID == branchNodeEditorId);
if (topBranch == null)
{
    topBranch = new PlanetContentManagerBranchNode(targetMod)
    {
        EditorID       = branchNodeEditorId,
        NodeTypeOption = 1, // BranchNode
    };
    // FormLink set after construction:
    topBranch.ParentNode = rootHookFormKey.ToNullableLink<IPlanetParentNodeGetter>();
    targetMod.PlanetContentManagerBranchNodes.Add(topBranch);
}
```

### Step 2: ContentNode-type BranchNode (find-or-create, shared)

```csharp
var contentBranch = targetMod.PlanetContentManagerBranchNodes
    .FirstOrDefault(n => n.EditorID == contentBranchEditorId);
if (contentBranch == null)
{
    contentBranch = new PlanetContentManagerBranchNode(targetMod)
    {
        EditorID       = contentBranchEditorId,
        NodeTypeOption = 2, // ContentNode
        Components     = new ExtendedList<AComponent>(),
    };
    contentBranch.ParentNode = topBranch.FormKey.ToNullableLink<IPlanetParentNodeGetter>();

    contentBranch.Components.Add(new PlanetContentManagerContentPropertiesComponent
    {
        ZNAM = 0, YNAM = 1, XNAM = 0, WNAM = 0, VNAM = 0, UNAM = 0,
        NAM1 = 0f,
        NAM3 = 0,
        NAM4 = new byte[] { 0x00, 0xFF, 0x00, 0x00 },
        NAM5 = 0, NAM6 = 0, NAM7 = 0, NAM8 = 0,
        NAM9 = 1,  // ← required — missing this causes the component not to register
    });
    targetMod.PlanetContentManagerBranchNodes.Add(contentBranch);
}
```

`YNAM=1` and `NAM9=1` are the only non-zero values for a standard block-creation content node (verified from `du_takeover_blockcontent`).

### Step 3: ContentNode leaf (always new per worldspace)

```csharp
var contentNode = new PlanetContentManagerContentNode(targetMod)
{
    EditorID = contentNodeEditorId,
};
// FormLinks set after construction:
contentNode.ParentNode = contentBranch.FormKey.ToNullableLink<IPlanetContentManagerBranchNodeGetter>();
contentNode.Content    = worldspace.FormKey.ToNullableLink<IPlanetContentTargetGetter>();
targetMod.PlanetContentManagerContentNodes.Add(contentNode);
```

`Worldspace` implements `IPlanetContentTargetGetter` — use that interface for `Content`.

## Type details

| Property | Type | Notes |
|----------|------|-------|
| `PlanetContentManagerBranchNode.NodeTypeOption` | `int` | `1` = BranchNode, `2` = ContentNode |
| `BranchNode.ParentNode` | `IFormLinkNullable<IPlanetParentNodeGetter>` | Both BranchNodes and Starfield root nodes implement this |
| `ContentNode.ParentNode` | `IFormLinkNullable<IPlanetContentManagerBranchNodeGetter>` | Narrower type than BranchNode's ParentNode |
| `ContentNode.Content` | `IFormLinkNullable<IPlanetContentTargetGetter>` | Use `worldspace.FormKey.ToNullableLink<IPlanetContentTargetGetter>()` |

## Find-or-create pattern

The top BranchNode and ContentNode-type BranchNode are **shared** across all worldspaces generated in the same run. Only the leaf `PlanetContentManagerContentNode` is always created fresh per worldspace. Always search `targetMod.PlanetContentManagerBranchNodes` by EditorID before creating.

## Pass files

| File | Category | Root FormID |
|------|----------|-------------|
| `PlanetContentManagerPass.cs` | Block creation | `00225373` |
| `PlanetScanPass.cs` | Planet scan | `0026F5DF` |
| `PlanetQuestPass.cs` | Quest location | `000F35E4` |

All three accept `(branchNodeEditorId, contentBranchEditorId, contentNodeEditorId)` as constructor parameters.

## Gotchas

- **All FormLink properties must be set after construction** (Mutagen nullable FormLink rule — `IFormLinkNullable` is a struct, never null)
- **Do not add `Nodes.Add(...)` calls** — the `Nodes` child list on BranchNode is not used at runtime; Starfield derives the tree from `ParentNode` back-references
- **`NAM9 = 1` is required** in `PlanetContentManagerContentPropertiesComponent` — missing it causes the content node not to register
- **`ContentNode.ParentNode` is a narrower type** than `BranchNode.ParentNode` — don't mix them up when setting FormLinks
