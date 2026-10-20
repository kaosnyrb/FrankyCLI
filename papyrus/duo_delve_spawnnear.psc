Scriptname duo_delve_spawnnear extends ReferenceAlias
{ FrankyCLI, on a recover beat's HOLDER alias when the recipe sets outside: [min, max]. The step's fragment calls
  PlaceOutside: a spot goes out at range into this alias (the objective points there), and when the player comes
  within NearDistance of it ACROSS THE MAP the holder and his gang are spawned at it and this alias takes him.

  WHY: placed when the step began, a holder 750 to 2000 out waited in the air over ground that was not loaded and
  fell when it loaded (his play, 2026-10-20: "when I got close he fell out of the sky and died"); a navmesh snap on
  his OnLoad did not save him. Spawning near the player means spawning on loaded ground, which is what worked at
  150 to 300. His pick, "yeah C".

  ⛔ FLAT DISTANCE, NOT 3D (his play of C the same day: "My first test base was on a mountian, the marker then went
  out to over the lower ground and was above 250m above the ground level. I was able to trigger it by jumping my car
  up."). The spot is placed at the POI marker's height, because the ground out there is not loaded and its height
  cannot be read; over a valley it hangs hundreds of metres up. So a timer compares x and y only, and only while the
  player is in the spot's worldspace (another planet's x and y mean nothing here). When he is near, the spot comes
  down to HIS height before the holder is placed, so the proven placement (59 up, snapped) starts near the ground.
  ⚠ The limit, stated: a drop of more than about 59 between him and the spot can still beat the snap. }

; Under the 300 that placed a holder on loaded ground at 05a, 2026-10-19.
Float NearDistance = 250.0
; Seconds between checks while the spot waits.
Float CheckEvery = 1.0

FormList gang
Form item
Int gangMin
Int gangMax
ObjectReference spot
WorldSpace world

Function PlaceOutside(ReferenceAlias akMarker, FormList akGang, Form akItem, Int aiMin, Int aiMax, Float afMin, Float afMax)
    gang = akGang
    item = akItem
    gangMin = aiMin
    gangMax = aiMax
    ; Read off the marker while the player stands at the site: the spot's own ground is not loaded yet.
    world = akMarker.GetRef().GetWorldSpace()
    spot = duo_delve_lib.PlaceSpotOutside(akMarker, Self, afMin, afMax)
    StartTimer(CheckEvery)
EndFunction

Event OnTimer(int aiTimerID)
    If !GetOwningQuest().IsRunning()
        Return
    EndIf
    Actor player = Game.GetPlayer()
    Float dx = player.GetPositionX() - spot.GetPositionX()
    Float dy = player.GetPositionY() - spot.GetPositionY()
    If player.GetWorldSpace() != world || dx * dx + dy * dy > NearDistance * NearDistance
        StartTimer(CheckEvery)
        Return
    EndIf
    ObjectReference here = spot
    spot = None
    here.SetPosition(here.GetPositionX(), here.GetPositionY(), player.GetPositionZ())
    duo_delve_lib.SpawnHolderAt(here, Self, gang, item, gangMin, gangMax)
    here.Delete()
EndEvent
