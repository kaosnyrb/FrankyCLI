Scriptname duo_delve_driver extends Quest
{The Delve driver, four beats at one place: find the load, find the other half gone, take it back
off whoever has it, finish the job. FrankyCLI's Papyrus library, source of truth in FrankyCLI/papyrus;
gen_delve writes the stages, objectives, prose and every property below from a recipe.

WHY A SCRIPT AT ALL: the objective layer is Papyrus-only. The stage graph can be authored from
records with stock hooks, and the objectives a player actually reads cannot. So each beat's
objective is displayed here, and everything that is CONTENT stays in the recipe.

THE STATE IS ONE INTEGER AND EVERY EVENT CHECKS IT FIRST. An activation or a pickup that arrives in
the wrong beat does nothing, so a player cannot finish out of order and a stray event cannot move
the mission.}

; --- aliases, all written by gen_delve --------------------------------------------------------------
ReferenceAlias Property LoadTarget Auto Const Mandatory
{Beat 1. The dropped load, create-obj'd at a travel marker inside the drawn place.}
ReferenceAlias Property CentreTarget Auto Const Mandatory
{Beats 2 and 4. Where the work was being done. Visited twice, and the second visit is the point.}
ReferenceAlias Property CarrierMarker Auto Const Mandatory
{Beat 3. Where whoever took the other half is standing when the player finds it gone.}
ReferenceAlias Property Carrier Auto Const Mandatory
{Beat 3. EMPTY and optional until the carrier is spawned into it, so objective 30 follows HIM rather
than the spot he was placed at. His eye, first play: the marker pointed at the spawn point.}

; --- things ------------------------------------------------------------------------------------------
Form Property LoadItem Auto Const Mandatory
{What the player carries from beat 1.}
Form Property MissingItem Auto Const Mandatory
{The other half. The carrier holds it; picking it up is beat 3.}
FormList Property GangMembers Auto Const Mandatory
{Actor bases. The carrier is drawn from it, and so is anybody standing with him.}
Int Property MinGangMembers Auto Const Mandatory
Int Property MaxGangMembers Auto Const Mandatory
{How many stand with the carrier. The carrier himself is always there: he is the key, not an extra.}
Message Property FailMessage Auto Const Mandatory
{Shown when the player activates the centre without what that beat needs.}

; --- LOSING THE LOAD. Defaults, not written per mission; metres (read off his HUD 2026-09-24). ------
; His ask: the crate should read as actually LOST, not sit at the site's own edge. The world probe
; (duo_worldprobe.psc) showed a POI's markers become queryable only from ~200 m out and only on the
; near side, so the move waits for the player's approach and anchors to the travel marker nearest
; them: a real marker the world holds, found with no alias.
Float Property LoseDistance = 250.0 Auto Const
{How close to the centre the player comes before the load is moved out to where it was lost.}
Float Property PushMin = 60.0 Auto Const
Float Property PushMax = 100.0 Auto Const
{How far past the site's near edge, away from the centre, the load ends up.}
Float Property FallbackEdge = 100.0 Auto Const
{If no travel marker is loaded yet: the edge is taken as this far from the centre, towards the player.}
Float Property RingSearch = 400.0 Auto Const
{Radius around the player searched for the near travel ring.}
Bool Property DebugNotes = True Auto Const
{TESTING: say on screen which way the load was placed. Off before release.}

; --- the stage graph. Defaults match gen_delve's four-beat template; not written per mission. -----
Int Property StageTaken = 50 Auto Const
Int Property StageAbsent = 60 Auto Const
Int Property StageRecovered = 70 Auto Const
Int Property StageComplete = 100 Auto Const

int CONST_Aggression_VeryAggressive = 2 Const
int CONST_Suspicious_DetectedActor = 2 Const

; 1 find the load | 2 take it to the centre | 3 recover what was taken | 4 finish the job | 5 done
Int Beat

Event OnQuestStarted()
    Beat = 1
    SetObjectiveDisplayed(10, True, False)
    RegisterForRemoteEvent(LoadTarget.GetRef(), "OnActivate")
    RegisterForDistanceLessThanEvent(Game.GetPlayer(), CentreTarget, LoseDistance)
EndEvent

; Beat 1, before it is found. Fires once, on the player's approach.
Event OnDistanceLessThan(ObjectReference akObj1, ObjectReference akObj2, float afDistance, int aiEventID)
    If Beat == 1
        LoseTheLoad()
    EndIf
EndEvent

; Move the load out past the near edge of the site, so it reads as dropped short of where it was
; going. The anchor is the nearest loaded travel marker; failing that, the centre.
Function LoseTheLoad()
    ObjectReference player = Game.GetPlayer()
    ObjectReference centre = CentreTarget.GetRef()
    ObjectReference load = LoadTarget.GetRef()

    ObjectReference anchor = NearestTravelMarker(player)
    Float edge = 0.0
    Float dx
    Float dy
    String how
    If anchor != None
        ; Outward is centre -> anchor: the side of the site the player is coming from.
        dx = anchor.GetPositionX() - centre.GetPositionX()
        dy = anchor.GetPositionY() - centre.GetPositionY()
        how = "travel marker"
    Else
        anchor = centre
        edge = FallbackEdge
        dx = player.GetPositionX() - centre.GetPositionX()
        dy = player.GetPositionY() - centre.GetPositionY()
        how = "FALLBACK, no ring loaded"
    EndIf
    Float len = Math.Sqrt(dx * dx + dy * dy)
    If len < 1.0
        ; Degenerate: anchor on top of the centre. Any direction is honest; use the player's.
        dx = player.GetPositionX() - centre.GetPositionX()
        dy = player.GetPositionY() - centre.GetPositionY()
        len = Math.Sqrt(dx * dx + dy * dy)
        If len < 1.0
            dx = 1.0
            dy = 0.0
            len = 1.0
        EndIf
    EndIf
    Float dist = edge + Utility.RandomFloat(PushMin, PushMax)

    ; A ZERO-ROTATION ORIGIN, so the offset below is in world axes whichever way the anchor faces.
    ObjectReference origin = anchor.PlaceAtMe(Game.GetFormFromFile(0x00003B, "Starfield.esm"), 1, False, False, True, None, None, False) ; XMarker
    origin.SetAngle(0.0, 0.0, 0.0)
    Float[] off = new Float[6]
    off[0] = dx / len * dist
    off[1] = dy / len * dist
    off[2] = 0.0

    ; HIS TRICK (ccs_missioninfestation01.SpawnNest): an actor placed with navmesh snap lands on
    ; walkable ground where an object would not, so place one, put the load on it, remove it.
    ; Initially disabled, so the player never sees who stood there.
    ObjectReference helper = origin.PlaceAtMe(GangMembers.GetAt(0), 1, False, True, True, off, None, True)
    load.MoveTo(helper)
    helper.Delete()
    origin.Delete()

    If DebugNotes
        Debug.Notification("Delve: load lost via " + how + ", " + (dist as Int) + " m out, now " + (load.GetDistance(centre) as Int) + " m from centre")
    EndIf
EndFunction

; The nearest REOverlayTravel* reference to the player, or None. The bases are Starfield.esm's
; (gen_delve bases: 434-448 of ~450 POIs place their travel markers on exactly these).
ObjectReference Function NearestTravelMarker(ObjectReference player)
    Int[] ids = new Int[6]
    ids[0] = 0x0E3800 ; REOverlayTravelA1
    ids[1] = 0x0E37FF ; A2
    ids[2] = 0x0E37FE ; A3
    ids[3] = 0x0E37FD ; B1
    ids[4] = 0x0E37FC ; B2
    ids[5] = 0x0E37FB ; B3
    ObjectReference best = None
    Float bestD = -1.0
    Int b = 0
    While b < ids.Length
        ObjectReference[] found = player.FindAllReferencesOfType(Game.GetFormFromFile(ids[b], "Starfield.esm"), RingSearch)
        Int i = 0
        While i < found.Length
            Float d = player.GetDistance(found[i])
            If bestD < 0.0 || d < bestD
                best = found[i]
                bestD = d
            EndIf
            i += 1
        EndWhile
        b += 1
    EndWhile
    Return best
EndFunction

Event ObjectReference.OnActivate(ObjectReference akSender, ObjectReference akActionRef)
    ObjectReference player = Game.GetPlayer()
    If akActionRef != player
        Return
    EndIf

    If akSender == LoadTarget.GetRef()
        If Beat == 1
            TakeLoad(player)
        EndIf
    ElseIf akSender == CentreTarget.GetRef()
        If Beat == 2
            If player.GetItemCount(LoadItem) >= 1
                FindItGone()
            Else
                FailMessage.Show()
            EndIf
        ElseIf Beat == 4
            If player.GetItemCount(LoadItem) >= 1 && player.GetItemCount(MissingItem) >= 1
                FinishTheJob(player)
            Else
                FailMessage.Show()
            EndIf
        EndIf
    EndIf
EndEvent

; Beat 1 -> 2. The load goes into the player's hands and the centre starts listening.
Function TakeLoad(ObjectReference player)
    ObjectReference load = LoadTarget.GetRef()
    load.BlockActivation(True, True)
    player.AddItem(LoadItem, 1, False)
    SetObjectiveCompleted(10, True)
    SetStage(StageTaken)
    SetObjectiveDisplayed(20, True, False)
    RegisterForRemoteEvent(CentreTarget.GetRef(), "OnActivate")
    Beat = 2
EndFunction

; Beat 2 -> 3. THE ABSENCE. Nothing is handed over here: the player arrives carrying half of
; something and the other half is not where it should be. The journal line on StageAbsent says so,
; and the carrier appears only now, so he cannot be met before the player knows he matters.
Function FindItGone()
    SetObjectiveCompleted(20, True)
    SetStage(StageAbsent)
    SpawnCarrier()
    RegisterForRemoteEvent(Game.GetPlayer(), "OnItemAdded")
    AddInventoryEventFilter(MissingItem)
    SetObjectiveDisplayed(30, True, False)
    Beat = 3
EndFunction

Function SpawnCarrier()
    ObjectReference marker = CarrierMarker.GetRef()
    ActorValue Suspicious = Game.GetFormFromFile(748, "Starfield.esm") as ActorValue ; Suspicious [AVIF:000002EC]
    ActorValue Aggression = Game.GetFormFromFile(700, "Starfield.esm") as ActorValue ; Aggression [AVIF:000002BC]

    ; Filled into the Carrier alias AS it is placed, before objective 30 is displayed, so the
    ; objective never shows with nothing to point at.
    Actor holder = marker.PlaceAtMe(GangMembers.GetAt(Utility.RandomInt(0, GangMembers.GetSize() - 1)), 1, True, False, True, None, Carrier, True) as Actor
    holder.AddItem(MissingItem, 1, False)
    holder.SetValue(Suspicious, CONST_Suspicious_DetectedActor)
    holder.SetValue(Aggression, CONST_Aggression_VeryAggressive)

    Float[] placePosition = new Float[6]
    Int n = Utility.RandomInt(MinGangMembers, MaxGangMembers)
    While n > 0
        placePosition[0] = Utility.RandomFloat(-50, 50)
        placePosition[1] = Utility.RandomFloat(-50, 50)
        placePosition[2] = 0
        Actor enemy = marker.PlaceAtMe(GangMembers.GetAt(Utility.RandomInt(0, GangMembers.GetSize() - 1)), 1, True, False, True, placePosition, None, True) as Actor
        enemy.SetValue(Suspicious, CONST_Suspicious_DetectedActor)
        enemy.SetValue(Aggression, CONST_Aggression_VeryAggressive)
        n -= 1
    EndWhile
EndFunction

; Beat 3 -> 4. Recovered by however the player got it: looted, picked up, handed over.
Event ObjectReference.OnItemAdded(ObjectReference akSender, Form akBaseItem, Int aiItemCount, ObjectReference akItemReference, ObjectReference akSourceContainer, Int aiTransferReason)
    If Beat == 3 && akBaseItem == MissingItem
        UnregisterForRemoteEvent(Game.GetPlayer(), "OnItemAdded")
        SetObjectiveCompleted(30, True)
        SetStage(StageRecovered)
        SetObjectiveDisplayed(40, True, False)
        Beat = 4
    EndIf
EndEvent

; Beat 4 -> done. Back at the centre, and this time the player is the one who brought it.
Function FinishTheJob(ObjectReference player)
    player.RemoveItem(LoadItem, 1, False, None)
    player.RemoveItem(MissingItem, 1, False, None)
    CentreTarget.GetRef().BlockActivation(True, True)
    SetObjectiveCompleted(40, True)
    SetStage(StageComplete)
    CompleteQuest()
    Beat = 5
EndFunction

Event OnQuestRejected()
    SetObjectiveDisplayed(10, False, False)
    Stop()
EndEvent
