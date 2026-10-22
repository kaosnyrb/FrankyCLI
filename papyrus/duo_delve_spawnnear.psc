Scriptname duo_delve_spawnnear extends ReferenceAlias
{ FrankyCLI, on a recover beat's HOLDER alias when the recipe sets outside: [min, max]. The step's fragment calls
  PlaceOutside: a spot goes out at range into this alias (the objective points there), and when the spot's CELL LOADS
  the spot is put on the navmesh, the holder and his gang are spawned at it, and this alias takes him.

  The spot is placed at the POI marker's height, because its ground is not loaded and cannot be read, so over a valley
  it hangs well above the ground. A snap that finds no navmesh does not move it and says so only in the log, so every
  snap is CHECKED by position. On a miss the spot comes down to the player's height and snaps again, retried once a
  second up to MaxTries; if every try misses he is spawned at the player's height, never at the marker's.
  Each try is traced with both heights, so a play says which route worked. }

; History, kept out of the docstring because a long .pex string crashes the game (compile.py):
; 2026-10-20 his play: placed when the step began, the holder waited in the air over unloaded ground and fell
;   ("when I got close he fell out of the sky and died"). Spawning on loaded ground is what works.
; 2026-10-22 06:58 his "A": OnCellLoad replaced a once-a-second flat distance timer.
; 2026-10-22 08:52 his play of 05a: the cell loaded with him 229 away, MoveToNearestNavmeshLocation logged
;   "No Navmesh found, object not moved", and the thief was spawned at the marker's height and fell ("thief fell from
;   a height, I was flying did he match my height?"). Nothing checked the snap. This version does.

Int Property MaxTries = 10 AutoReadOnly
Float Property RetryEvery = 1.0 AutoReadOnly

FormList gang
Form item
Int gangMin
Int gangMax
ObjectReference spot   ; None once the holder exists: this alias then holds him, and his cell loads must not respawn
Int tries
Bool settling          ; a snap is under way (retries pending), so a second cell load must not start another

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
        Settle()
    EndIf
EndFunction

Event OnCellLoad()
    If spot && !settling && GetOwningQuest().IsRunning()
        Settle()
    EndIf
EndEvent

Event OnTimer(int aiTimerID)
    If spot && GetOwningQuest().IsRunning()
        TrySnap()
    EndIf
EndEvent

Function Settle()
    settling = True
    tries = 0
    TrySnap()
EndFunction

; One try: snap where the spot is; on a miss bring it to the player's height and snap again. A hit spawns him.
Function TrySnap()
    tries += 1
    If Snapped("as placed") || Snapped(DownToPlayer())
        Spawn()
    ElseIf tries < MaxTries
        StartTimer(RetryEvery)
    Else
        Debug.Trace(Self + " [spawnnear] no navmesh after " + tries + " tries, spawning at the player's height")
        DownToPlayer()
        Spawn()
    EndIf
EndFunction

String Function DownToPlayer()
    spot.SetPosition(spot.GetPositionX(), spot.GetPositionY(), Game.GetPlayer().GetPositionZ())
    Return "at the player's height"
EndFunction

; True when MoveToNearestNavmeshLocation actually moved the spot. It reports a miss only in the log.
Bool Function Snapped(String asHow)
    Float x = spot.GetPositionX()
    Float y = spot.GetPositionY()
    Float z = spot.GetPositionZ()
    spot.MoveToNearestNavmeshLocation()
    Bool moved = spot.GetPositionX() != x || spot.GetPositionY() != y || spot.GetPositionZ() != z
    Actor player = Game.GetPlayer()
    Debug.Trace(Self + " [spawnnear] try " + tries + " " + asHow + ": spot z " + z + " -> " + spot.GetPositionZ() + ", player z " + player.GetPositionZ() + ", " + player.GetDistance(spot) + " away, snapped " + moved)
    Return moved
EndFunction

Function Spawn()
    ObjectReference here = spot
    spot = None
    settling = False
    duo_delve_lib.SpawnHolderAt(here, Self, gang, item, gangMin, gangMax)
    here.Delete()
EndFunction
