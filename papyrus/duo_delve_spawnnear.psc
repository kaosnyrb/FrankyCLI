Scriptname duo_delve_spawnnear extends ReferenceAlias
{ FrankyCLI, on a recover beat's HOLDER alias when the recipe sets outside: [min, max]. The step's fragment calls
  PlaceOutside: a spot goes out at range into this alias (the objective points there), and when the spot's CELL LOADS
  the spot is moved onto the navmesh, the holder and his gang are spawned at it, and this alias takes him.

  WHY: placed when the step began, a holder 750 to 2000 out waited in the air over ground that was not loaded and fell
  when it loaded (his play, 2026-10-20: "when I got close he fell out of the sky and died"). Spawning on loaded ground
  is what works.

  WHY THE CELL LOAD, his "A", 2026-10-22, on "fix the other timers to use the events": the spot is placed at the POI
  marker's height, because its ground is not loaded and cannot be read, so over a valley it hangs hundreds of metres up
  (his play of 10-20: "I was able to trigger it by jumping my car up"). A distance event measures 3D and would bring
  that back, and the flat check before this was a once-a-second timer. OnCellLoad is the event for the thing actually
  wanted, ground under the spot, and once it is loaded the navmesh gives the spot a true height.
  ⚠ Unmeasured as of this change: how far away the cell loads (likely past the old 250), and whether a spot placed by
  script receives its cell's load. Both are one play of 05a. }

FormList gang
Form item
Int gangMin
Int gangMax
ObjectReference spot   ; None once the holder exists: this alias then holds him, and his cell loads must not respawn

Function PlaceOutside(ReferenceAlias akMarker, FormList akGang, Form akItem, Int aiMin, Int aiMax, Float afMin, Float afMax)
    gang = akGang
    item = akItem
    gangMin = aiMin
    gangMax = aiMax
    spot = duo_delve_lib.PlaceSpotOutside(akMarker, Self, afMin, afMax)
    Cell c = spot.GetParentCell()
    Debug.Trace(Self + " [spawnnear] spot " + spot + " in " + c + ", loaded " + (c && c.IsLoaded()))
    ; A short throw can land in a cell that is ALREADY loaded, whose load event has been and gone.
    If c && c.IsLoaded()
        SpawnAtSpot()
    EndIf
EndFunction

Event OnCellLoad()
    If spot && GetOwningQuest().IsRunning()
        SpawnAtSpot()
    EndIf
EndEvent

Function SpawnAtSpot()
    ObjectReference here = spot
    spot = None
    here.MoveToNearestNavmeshLocation()
    Debug.Trace(Self + " [spawnnear] ground loaded under " + here + ", the player " + Game.GetPlayer().GetDistance(here) + " away")
    duo_delve_lib.SpawnHolderAt(here, Self, gang, item, gangMin, gangMax)
    here.Delete()
EndFunction
