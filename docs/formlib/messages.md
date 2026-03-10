# Message Records (MESG)

`Message` records display text to the player — either as a brief on-screen notification
or as a modal dialog box requiring a button choice. They are referenced by Papyrus
scripts which call `ShowMessage()` at the right time.

---

## Record fields

| Field | Type | Notes |
|---|---|---|
| `Description` | `TranslatedString` | **The message body text.** Always set this. |
| `Name` | `TranslatedString?` | Title / heading shown above the body. Optional for notifications. |
| `ShortTitle` | `TranslatedString?` | Abbreviated title. Rarely used. |
| `Flags` | `Message.Flag` | Notification vs. MessageBox — see below |
| `DisplayTime` | `uint?` | Auto-dismiss time in seconds. Only for notifications (no MessageBox flag). |
| `OwnerQuest` | `IFormLinkNullable<IQuestGetter>` | Quest this message is scoped to. Optional. |
| `BNAM` | `uint?` | Opaque binary field. Always clone from a template. |
| `INAM` | `int` | Opaque field. Leave default. |
| `MenuButtons` | `ExtendedList<MessageButton>` | Choice buttons. Only used when `MessageBox` flag is set. |

### `Message.Flag`

| Flag | Value | Effect |
|---|---|---|
| `MessageBox` | 1 | Modal dialog with buttons. Player must click to dismiss. |
| `DelayInitialDisplay` | 2 | Delay before the message appears. |

**Two distinct message types** — distinguished by whether `MessageBox` is set:

| Type | Flag | Buttons | Dismiss |
|---|---|---|---|
| Notification | `0` (no flag) | None | Auto-dismiss after `DisplayTime` seconds |
| MessageBox | `MessageBox = 1` | 1–N `MenuButtons` | Player must click a button |

---

## `MessageButton` — choice button entry

Each `MenuButton` in a MessageBox appears as a clickable option. The Papyrus
`ShowMessage()` return value is the zero-based index of the button clicked.

| Field | Type | Notes |
|---|---|---|
| `Text` | `TranslatedString?` | Label shown on the button |
| `Conditions` | `ExtendedList<Condition>` | When this button is available/visible |

Buttons without conditions are always shown. Add conditions to hide a button based on
game state (e.g. skill check, item requirement).

---

## Template FormIDs in this project

Both templates live in the DU template mod and are cloned by `MessageNoun`.

| FormID | Purpose | Flags | Buttons |
|---|---|---|---|
| `0x000844` | Notification popup — item/object activation flavour text | no MessageBox | 0 |
| `0x0008BA` | Branching-choice dialog — player picks which quest lead to follow | MessageBox | 2 |

---

## Creation patterns

### Via `MessageNoun` — standard approach

`MessageNoun` clones `Name`, `BNAM`, `Flags`, and `MenuButtons` from the template,
then sets `Description` to the new text. Use this for all new messages.

```csharp
// Notification — clone 0x000844, set body text
var message = new MessageNoun(0x000844, "You found the cache. Encrypted data slate inside.");

// MessageBox with choices — clone 0x0008BA, set body + button text
var message = new MessageNoun(0x0008BA, "Two leads. Which do you follow?");
message.SetChoice(0, "Investigate the Crimsonfleet contact.");
message.SetChoice(1, "Check the abandoned relay station.");
```

Then wire the message to a script property:

```csharp
activator.SetScriptProperty("duout_destroy_completenstart", "messagetext",
    message.instance.ToLink<IStarfieldMajorRecordGetter>());
```

---

### Direct creation — when you need full control

When not using `MessageNoun`, you must still clone `BNAM` and `Flags` from a template
record. These fields contain opaque data that will silently break display if left default.

```csharp
var source = RecordLookup.Find<IMessageGetter>(0x000844, m => m.Messages);
var msg = new Message(targetMod)
{
    EditorID     = "msg_" + questID,
    Description  = bodyText,
    Name         = titleText,          // optional
    Flags        = source.Flags,       // clone — don't invent
    BNAM         = source.BNAM,        // clone — opaque, required
    MenuButtons  = source.MenuButtons  // clone if you need buttons
};
targetMod.Messages.Add(msg);
```

**Do not** set `Description` inside the constructor initializer block if it contains a
nullable FormLink — but `TranslatedString` is safe to set inline.

---

## How messages are displayed

Messages are not self-displaying. A Papyrus script attached to a quest or activator
calls `akMessage.Show()` or uses the `ShowMessage` global function. The message record
is passed to that script as a property.

For activators: set the `"messagetext"` script property to the message FormLink.
For quests: set the equivalent quest script property.

The `DefaultAliasMapMarkerScript.UnexploredName` property also takes a Message —
it controls the label shown on an undiscovered map marker.

---

## `<Alias=Name>` tokens in message text

Like quest objective text, `Description` supports dynamic substitution:

```
"Meet <Alias=Contact> at <Alias=MeetingLocation>."
```

These resolve at display time against the owning quest's aliases. Only works when
`OwnerQuest` is set and the quest is running.

---

## Gotchas

- **`Description` is the body, `Name` is the title** — easy to confuse. Vanilla
  notification messages often have no `Name` at all.
- **`BNAM` is required** — this opaque field must be cloned from a valid vanilla/template
  source. A message with `BNAM = null` will not display correctly.
- **Button count must match** — if you clone a 2-button template but only call `SetChoice`
  once, the second button keeps its template text. Always set all buttons.
- **`MenuButtons` is shared on deep-copy** — after `source.DeepCopy()`, `MenuButtons`
  is a new list but each button's `Text` still references the source string until you
  replace it. Call `SetChoice` for every slot.
