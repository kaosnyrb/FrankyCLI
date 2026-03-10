# Starfield Vanilla Scripts — Reference for Procedural Quests

Bethesda ships a rich library of reusable Papyrus scripts. When wiring a quest from C#
you set the script name and properties on `QuestScriptEntry` / `AliasScriptEntry` entries
inside the `VirtualMachineAdapter`. You never write Papyrus — you just declare which
vanilla script to use and fill in its properties.

All scripts live in `Data/scripts/Source/` as `.psc` files. The compiled `.pex` binaries
(what the engine actually loads) are in `Data/scripts/`.

---

## Architecture — the Default* inheritance tree

Most vanilla scripts share a common contract defined in two base types:

```
DefaultAliasParent  (ReferenceAlias)
  └── DefaultAlias          (PlayerOnly = true by default)
        └── DefaultAliasOnDeath
        └── DefaultAliasOnActivate
        └── DefaultAliasOnTriggerEnter
        └── DefaultAliasOnItemAddedScript
        └── ... (one per event type)

DefaultRefParent    (ObjectReference)
  └── DefaultRefOnTriggerEnter
        └── DefaultStartQuestOnTriggerEnter

DefaultQuest        (Quest)
  └── DefaultCounterQuest
  └── DefaultCounterQuestIncOnDeath  (actually ReferenceAlias — increments owner)

TopicInfo
  └── DefaultTopicInfo
        └── DefaultTopicInfoSetStage
  └── DefaultTopicInfoSetGlobal
```

**Central helper:** `DefaultScriptFunctions` (const, hidden) — all Default* scripts
call through it. It provides:

| Function | Purpose |
|---|---|
| `TryToSetStage(...)` | Validates conditions then calls `quest.SetStage()` |
| `IsValidToSetStage(...)` | Conditions check without the stage set |
| `SafeSetStage(quest, stage)` | Null-safe `quest.SetStage()` |
| `CheckForStages(...)` | PrereqStage / TurnOffStage / TurnOffStageDone guards |
| `CheckForConditionForm(...)` | ConditionForm.IsTrue() guard |
| `CheckForReferenceMatches(...)` | PlayerOnly / alias / faction ref filter |
| `CheckForLocationMatches(...)` | Location / LocationAlias filter |
| `IsDead(ref)` | Handles both Actor and SpaceshipReference |
| `BuildParentScriptFunctionParams(ref, loc)` | Bundles params into a struct |

---

## Shared properties on every Default* alias script

These properties appear on `DefaultAliasParent` and are inherited by all alias events:

| Property | Type | Notes |
|---|---|---|
| `StageToSet` | int | Stage to set when the event fires. -1 = no stage |
| `PrereqStage` | int | Owning quest must have this stage set first |
| `TurnOffStage` | int | If quest stage ≥ this, do nothing |
| `TurnOffStageDone` | int | If this stage is specifically done, do nothing |
| `DoOnce` | bool | Fire once then enter Done state (default: false) |
| `ConditionFormToTest` | ConditionForm | Extra condition gate |
| `FailOnDeadActor` | bool | Skip if the alias's ref is dead |
| `ShowTraces` | bool | Debug logging (default: false) |

**DefaultAlias** (the standard middle tier) adds:

| Property | Type | Notes |
|---|---|---|
| `PlayerOnly` | bool | RefToCheck must be the player (default: **true**) |
| `ReferencesToCheckAgainst` | ObjectReference[] | Allowed activators |
| `AliasesToCheckAgainst` | ReferenceAlias[] | Allowed activators by alias |
| `FactionsToCheckAgainst` | Faction[] | Allowed activators by faction |
| `LocationsToCheckAgainst` | Location[] | Required current location |
| `LocationAliasesToCheckAgainst` | LocationAlias[] | Required location by alias |
| `LocationMatchIfChild` | bool | Accept child locations (default: false) |

---

## Alias event scripts

### `DefaultAliasOnDeath`
Sets a quest stage when the aliased actor dies.

Extra properties:
- `UseOnDyingInstead` — fire on `OnDying` (instant) rather than `OnDeath` (delayed)
- `ClearAliasOnDeath` — clear the alias after death (alias must be Optional)
- All `RefToCheck` / `LocationToCheck` arrays inherited from `DefaultAlias`

`RefToCheck` = the killer. `LocationToCheck` = location of the dead actor.

---

### `DefaultAliasOnActivate`
Sets a quest stage when the aliased object is activated.

Extra properties:
- `ShouldDisableAfterSuccessfulActivation` — disables the object after (default: false)
- `ShouldHideActivationAfterSuccessfulActivation` — hides the prompt (default: false)
- `AllowDuringCombat` — if false, block activation while activator is in combat (default: true)
- `NotAllowedDuringCombat` — Message to show when blocked by combat check

`RefToCheck` = the activator.

---

### `DefaultAliasOnTriggerEnter`
Sets a quest stage when any actor enters the aliased trigger volume.

Extra properties:
- `DeleteWhenTriggeredSuccessfully` — deletes the trigger after success (default: **true**)

Also registers for `Quest.OnStageSet` to re-check refs already inside the volume
when `PrereqStage` fires.

`RefToCheck` = the entering actor.

---

### `DefaultAliasOnItemAddedScript`
Sets a quest stage when a specific item is picked up into this aliased container.

Extra properties:
- `ItemFilter` (**Mandatory**) — Form filter passed to `AddInventoryEventFilter`

`RefToCheck` = the player.

---

### `DefaultAliasOnLocationChange`
Sets a quest stage when the aliased actor changes location.

Extra properties:
- `CheckNewLocation` — if true (default), check the new location; if false, check the old

`RefToCheck` = None (location-only check).

---

### `DefaultAliasOnLoad`
Sets a quest stage when the aliased object loads into a cell.
No extra properties beyond the shared set.

---

### `DefaultAliasOnCombatStateChanged`
Sets a quest stage when the aliased actor enters/exits combat. See also `...A` variant.

---

### `DefaultAliasOnContainerChangedTo` / `...From`
Sets a stage when this item's container changes. `To` = new container; `From` = old.
`RefToCheck` = the new (or old) container.

---

### `DefaultAliasOnShipLand` / `OnShipTakeOff` / `OnShipDock` / `OnShipUndock` / `OnShipGravJump`
Ship event variants — same shared property pattern, event fires on the named ship event.

---

### `DefaultAliasMapMarkerScript`
Configures a map marker alias at quest start or alias change. Does **not** set stages.

Properties:
| Property | Type | Default | Notes |
|---|---|---|---|
| `AllowGravJump` | bool | false | Space map markers only |
| `VisibleOnStarMap` | bool | true | |
| `Discovered` | bool | false | Auto-discover on init |
| `UndiscoveredVisibility` | int | -1 | -1=don't change, 0=Always, 1=System, 2=Planet |
| `MapMarkerType` | int | -1 | -1=don't change; enum from CS wiki |
| `MapMarkerCategory` | int | -1 | -1=don't change |
| `UnexploredName` | Message | None | Unexplored label |
| `EnableOnInit` | bool | true | Enables the marker object on init |

Fires `UpdateMapMarkerFlags()` on both `OnAliasInit` and `OnAliasChanged`.

---

## Quest-level scripts

### `DefaultCounterQuest`
Tracks a numeric counter; sets a stage when a target count is reached.

| Property | Notes |
|---|---|
| `StageToSet` | Stage fired when count ≥ TargetValue |
| `TargetValue` | Target count (can be set at runtime by other scripts) |

Paired with **`DefaultCounterQuestIncOnDeath`** on each alias:
- `CheckForOnDyingInstead` — use OnDying instead of OnDeath
- `SupportRespawning` — don't enter AlreadyDied state after increment (lets it re-fire for respawned actors)

`(GetOwningQuest() as DefaultCounterQuest).Increment()` is called by the alias script.

**Typical kill-X pattern:**
1. Quest script = `DefaultCounterQuest`, `TargetValue = 5`, `StageToSet = 100`
2. Each enemy alias script = `DefaultCounterQuestIncOnDeath`

---

### `DefaultEnableAliasesQuestScript`
Enables or disables aliases on quest start or specific stage transitions.

```
struct AliasEnableDatum
    Alias  AliasToEnable
    int    StageToEnable  ; -1 = on quest start
EndStruct
AliasEnableDatum[] AliasEnableData  ;Mandatory
```

Works for both `ReferenceAlias` (→ `TryToEnable()`) and `RefCollectionAlias`
(→ `EnableAll()`). One script handles all aliases across all stages.

---

## TopicInfo scripts

TopicInfo scripts attach to `DialogResponses` records and fire during dialogue.

### `DefaultTopicInfo`
Base for all topic info scripts. Properties:
- `StageToSet` (**Mandatory**) — stage to set
- `PrereqStage`, `TurnOffStage`, `TurnOffStageDone` — guards
- `QuestToSetStageOn` — if None, uses the owning quest (default)

### `DefaultTopicInfoSetStage`
Extends `DefaultTopicInfo`. Calls `TryToSetStage()` on `OnBegin` or `OnEnd`.

| Property | Default | Notes |
|---|---|---|
| `SetStageOnEnd` | true | If false, set stage on dialogue begin instead |

**Typical use:** wire to a DialogResponses record to advance quest stage when player
picks a dialogue option.

### `DefaultTopicInfoSetGlobal`
Sets a GlobalVariable to a float value on begin/end.

| Property | Notes |
|---|---|
| `SetGlobalOnBegin` | GlobalVariable to set when dialogue begins |
| `SetGlobalOnEnd` | GlobalVariable to set when dialogue ends |
| `OnBeginValue` | Value (default 1.0) |
| `OnEndValue` | Value (default 1.0) |

---

## Ref / world scripts

### `DefaultStartQuestOnTriggerEnter`
Extends `DefaultRefOnTriggerEnter`. Starts a quest when a trigger is entered.

| Property | Notes |
|---|---|
| `QuestToStart` (**Mandatory**) | Quest to call `.Start()` on |

Inherits full `PlayerOnly` / `RefToCheck` / stage-guard suite from the ref parent.

### `DefaultSendStoryEventOnLoad`
When this ref loads, fires `StoryEventKeyword.SendStoryEventAndWait(...)` to hook
into the Story Manager.

| Property | Notes |
|---|---|
| `StoryEventKeyword` (**Mandatory**) | Keyword that triggers the story event |
| `Value1` | aiValue1 param (default 0) |
| `Value2` | aiValue2 param (default 0) |

---

## Wiring scripts from C#

In Mutagen these are `ScriptEntry` records on a `VirtualMachineAdapter`. The key
fields are `Name` (script filename without `.psc`) and `Properties` (a list of
`ScriptProperty` subtypes).

```csharp
// Attach DefaultAliasOnDeath to a ReferenceAlias
var aliasVM = new VirtualMachineAdapter();
var scriptEntry = new ScriptEntry { Name = "DefaultAliasOnDeath" };

scriptEntry.Properties.Add(new ScriptIntProperty
{
    Name = "StageToSet",
    Data = 100
});
scriptEntry.Properties.Add(new ScriptBoolProperty
{
    Name = "UseOnDyingInstead",
    Data = false
});

aliasVM.Scripts.Add(scriptEntry);
questRefAlias.VirtualMachineAdapter = aliasVM;
```

### ScriptProperty subtypes (Mutagen names)

| Papyrus type | Mutagen class | `Data` field type |
|---|---|---|
| `int` | `ScriptIntProperty` | `int` |
| `float` | `ScriptFloatProperty` | `float` |
| `bool` | `ScriptBoolProperty` | `bool` |
| `string` | `ScriptStringProperty` | `string` |
| `Object` (form ref) | `ScriptObjectProperty` | `ScriptObjectPropertyData` |
| `Object[]` (array) | `ScriptObjectListProperty` | `IList<ScriptObjectPropertyData>` |

`ScriptObjectPropertyData` fields:
- `Object` — `IFormLinkGetter<IMajorRecordGetter>` (use `.ToLink()`)
- `Alias` — `short` (alias ID, or -1 if not an alias ref)

```csharp
// Reference to a form
new ScriptObjectProperty
{
    Name = "QuestToSetStageOn",
    Data = new ScriptObjectPropertyData
    {
        Object = otherQuest.ToLink<IMajorRecordGetter>(),
        Alias = -1
    }
}

// Reference to an alias on the same quest (alias ID 3)
new ScriptObjectProperty
{
    Name = "AliasRef",
    Data = new ScriptObjectPropertyData
    {
        Object = owningQuest.ToLink<IMajorRecordGetter>(),
        Alias = 3
    }
}
```

---

## Quick lookup — which script for which trigger

| "I want to..." | Script name | Goes on |
|---|---|---|
| Set stage when NPC dies | `DefaultAliasOnDeath` | ReferenceAlias |
| Kill N enemies to advance | `DefaultCounterQuest` + `DefaultCounterQuestIncOnDeath` | Quest + each alias |
| Set stage when player picks up item | `DefaultAliasOnItemAddedScript` | ReferenceAlias on item |
| Set stage when player activates object | `DefaultAliasOnActivate` | ReferenceAlias on object |
| Set stage when player enters trigger | `DefaultAliasOnTriggerEnter` | ReferenceAlias on trigger |
| Set stage when player reaches location | `DefaultAliasOnLocationChange` | ReferenceAlias on player |
| Set stage from dialogue choice | `DefaultTopicInfoSetStage` | TopicInfo/DialogResponses |
| Set global from dialogue | `DefaultTopicInfoSetGlobal` | TopicInfo/DialogResponses |
| Start quest on entering area | `DefaultStartQuestOnTriggerEnter` | ObjectReference trigger |
| Configure a map marker | `DefaultAliasMapMarkerScript` | ReferenceAlias on marker |
| Enable aliases at quest start or stage | `DefaultEnableAliasesQuestScript` | Quest |
