Scriptname duo_delve_spawnnear extends ReferenceAlias
{ FrankyCLI, on a recover beat's HOLDER alias when the recipe sets outside: [min, max]. The step's fragment calls
  PlaceOutside: a spot goes out at range into this alias (the objective points there), and when the player comes
  within NearDistance of it the holder and his gang are spawned at it and this alias takes him.

  WHY: placed when the step began, a holder 750 to 2000 out waited in the air over ground that was not loaded and
  fell when it loaded (his play, 2026-10-20: "when I got close he fell out of the sky and died"); a navmesh snap on
  his OnLoad did not save him. Spawning near the player means spawning on loaded ground, which is what worked at
  150 to 300. His pick, "yeah C". The wait is a distance event this script registers ITSELF, because
  RegisterForDistanceLessThanEvent is selfonly: the same event vanilla's DefaultAliasOnDistanceLessThan uses, and the
  one every Delve's arrival hook already relies on. UNPROVEN until played: that it fires for a spot out on ground
  that is not loaded when it is registered. }

; Under the 300 that placed a holder on loaded ground at 05a, 2026-10-19.
Float NearDistance = 250.0

FormList gang
Form item
Int gangMin
Int gangMax
ObjectReference spot

Function PlaceOutside(ReferenceAlias akMarker, FormList akGang, Form akItem, Int aiMin, Int aiMax, Float afMin, Float afMax)
    gang = akGang
    item = akItem
    gangMin = aiMin
    gangMax = aiMax
    spot = duo_delve_lib.PlaceSpotOutside(akMarker, Self, afMin, afMax)
    RegisterForDistanceLessThanEvent(Game.GetPlayer(), spot, NearDistance)
EndFunction

Event OnDistanceLessThan(ObjectReference akObj1, ObjectReference akObj2, float afDistance, int aiEventID)
    If spot == None
        Return
    EndIf
    ObjectReference here = spot
    spot = None
    duo_delve_lib.SpawnHolderAt(here, Self, gang, item, gangMin, gangMax)
    here.Delete()
EndEvent
