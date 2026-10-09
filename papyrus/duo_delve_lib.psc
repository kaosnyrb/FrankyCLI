Scriptname duo_delve_lib Hidden
{The Delve helpers a stage fragment calls: the work that is Papyrus-only and too long to generate inline
into every quest's fragment script. FrankyCLI's Papyrus library, source of truth in FrankyCLI/papyrus.
A beats Delve has NO DRIVER (his ruling 2026-10-08: the stages are the state machine), so these are
Global functions holding no state: everything they touch is passed in by the fragment that calls them.

Ported from duo_delve_driver.psc, which they replace as each Delve moves onto the beats kind.}

; THE RECOVER BEAT. Whoever took the thing appears at the marker holding it, with a gang drawn from the
; same list, all aggressive. The holder is placed INTO akHolder as he spawns (PlaceAtMe's akAliasToFill),
; so the objective, which targets that alias, follows HIM rather than the spot (his eye, first play of
; duo_delve03). Recovered by however the player gets the item: a stock OnItemAdded hook on the player
; sets the stage, so looting, picking up and being handed it all count.
Function SpawnHolder(ReferenceAlias akMarker, ReferenceAlias akHolder, FormList akGang, Form akItem, Int aiMin, Int aiMax) Global
    ObjectReference marker = akMarker.GetRef()
    ActorValue Suspicious = Game.GetFormFromFile(748, "Starfield.esm") as ActorValue ; Suspicious [AVIF:000002EC]
    ActorValue Aggression = Game.GetFormFromFile(700, "Starfield.esm") as ActorValue ; Aggression [AVIF:000002BC]
    ; A Global function sees no script variables, so the driver's two constants are literals here:
    Float detected = 2.0     ; Suspicious: DetectedActor
    Float veryAggressive = 2.0 ; Aggression: VeryAggressive

    Actor holder = marker.PlaceAtMe(akGang.GetAt(Utility.RandomInt(0, akGang.GetSize() - 1)), 1, True, False, True, None, akHolder, True) as Actor
    holder.AddItem(akItem, 1, False)
    holder.SetValue(Suspicious, detected)
    holder.SetValue(Aggression, veryAggressive)

    Float[] placePosition = new Float[6]
    Int n = Utility.RandomInt(aiMin, aiMax)
    While n > 0
        placePosition[0] = Utility.RandomFloat(-50, 50)
        placePosition[1] = Utility.RandomFloat(-50, 50)
        placePosition[2] = 0
        Actor enemy = marker.PlaceAtMe(akGang.GetAt(Utility.RandomInt(0, akGang.GetSize() - 1)), 1, True, False, True, placePosition, None, True) as Actor
        enemy.SetValue(Suspicious, detected)
        enemy.SetValue(Aggression, veryAggressive)
        n -= 1
    EndWhile
EndFunction

; THE HOLD BEAT. One wave: aiMin to aiMax enemies from the gang list at the spawn marker, each placed INTO
; akWave (PlaceAtMe's akAliasToFill on a RefCollectionAlias, as vanilla's MissionBoardCargoContainerScript
; fills its CargoCollection), all aggressive so they come for the player. A stock DefaultCollectionAliasOnDeath
; on akWave sets the next stage when every one of them is dead, so nothing here watches anything. Persistent
; while the quest holds them, so a wave cannot be unloaded out of its own collection. Scattered within 10 m
; of the marker, snapped to navmesh, so they arrive as one group from one side.
Function SpawnWave(ReferenceAlias akMarker, RefCollectionAlias akWave, FormList akGang, Int aiMin, Int aiMax) Global
    ObjectReference marker = akMarker.GetRef()
    ActorValue Suspicious = Game.GetFormFromFile(748, "Starfield.esm") as ActorValue ; Suspicious [AVIF:000002EC]
    ActorValue Aggression = Game.GetFormFromFile(700, "Starfield.esm") as ActorValue ; Aggression [AVIF:000002BC]
    Float detected = 2.0       ; Suspicious: DetectedActor
    Float veryAggressive = 2.0 ; Aggression: VeryAggressive

    Float[] placePosition = new Float[6]
    Int n = Utility.RandomInt(aiMin, aiMax)
    While n > 0
        placePosition[0] = Utility.RandomFloat(-10, 10)
        placePosition[1] = Utility.RandomFloat(-10, 10)
        placePosition[2] = 0
        Actor enemy = marker.PlaceAtMe(akGang.GetAt(Utility.RandomInt(0, akGang.GetSize() - 1)), 1, True, False, True, placePosition, akWave, True) as Actor
        enemy.SetValue(Suspicious, detected)
        enemy.SetValue(Aggression, veryAggressive)
        n -= 1
    EndWhile
EndFunction

; THE APPROACH (stage 5 on a beats Delve, set by a stock DefaultAliasOnDistanceLessThan on the player).
; People at the site, placed the way his dual-activator driver placed them at a delivery: around the
; target, within 25 m, snapped to navmesh. His ruling 2026-09-24: "the one with the delivery should have
; people there." Lifted from duo_delve_driver.PlaceCivilians.
Function PlaceCivilians(ReferenceAlias akCentre, FormList akList, Int aiMin, Int aiMax) Global
    ObjectReference centre = akCentre.GetRef()
    Float[] pos = new Float[6]
    Int n = Utility.RandomInt(aiMin, aiMax)
    While n > 0
        pos[0] = Utility.RandomFloat(-25, 25)
        pos[1] = Utility.RandomFloat(-25, 25)
        pos[2] = 0
        centre.PlaceAtMe(akList.GetAt(Utility.RandomInt(0, akList.GetSize() - 1)), 1, True, False, True, pos, None, True)
        n -= 1
    EndWhile
EndFunction

; LOSING THE LOAD. Move it out past the near edge of the site, so it reads as dropped short of where it
; was going (his ask: actually LOST, not sitting at the site's own edge). A POI's markers become
; queryable only from ~200 m out and only on the near side (duo_worldprobe.psc), so this runs on the
; approach and anchors to the travel marker nearest the player; failing that, the centre. Metres, read
; off his HUD 2026-09-24. Lifted from duo_delve_driver.LoseTheLoad; its DebugNotes line is not ported.
Function LoseTheLoad(ReferenceAlias akLoad, ReferenceAlias akCentre, Form akHelper) Global
    Float PushMin = 60.0
    Float PushMax = 100.0
    Float FallbackEdge = 100.0
    ObjectReference player = Game.GetPlayer()
    ObjectReference centre = akCentre.GetRef()
    ObjectReference load = akLoad.GetRef()

    ObjectReference anchor = NearestTravelMarker(player)
    Float edge = 0.0
    Float dx
    Float dy
    If anchor != None
        ; Outward is centre -> anchor: the side of the site the player is coming from.
        dx = anchor.GetPositionX() - centre.GetPositionX()
        dy = anchor.GetPositionY() - centre.GetPositionY()
    Else
        anchor = centre
        edge = FallbackEdge
        dx = player.GetPositionX() - centre.GetPositionX()
        dy = player.GetPositionY() - centre.GetPositionY()
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

    ; HIS TRICK (ccs_missioninfestation01.SpawnNest): an actor placed with navmesh snap lands on walkable
    ; ground where an object would not, so place one, put the load on it, remove it. Initially disabled,
    ; so the player never sees who stood there.
    ObjectReference helper = origin.PlaceAtMe(akHelper, 1, False, True, True, off, None, True)
    load.MoveTo(helper)
    helper.Delete()
    origin.Delete()
EndFunction

; The nearest REOverlayTravel* reference to the player, or None. The bases are Starfield.esm's
; (gen_delve bases: 434-448 of ~450 POIs place their travel markers on exactly these).
ObjectReference Function NearestTravelMarker(ObjectReference player) Global
    Float RingSearch = 400.0
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

; THE PEOPLE AT AN ENDING (a choose beat's person, on the first approach to its delivery point after the
; find). His playtest 2026-10-08: "Returning the medal talks about a person who isn't there." A ref placed
; at a site the world has not loaded has no ground to stand on, so this runs on the approach. Lifted from
; duo_delve_choice.PlacePerson / PlaceCompany.
Function PlacePerson(ReferenceAlias akTarget, ActorBase akPerson, Float afOffset) Global
    Float[] pos = new Float[6]
    pos[0] = afOffset
    pos[1] = 0
    pos[2] = 0
    akTarget.GetRef().PlaceAtMe(akPerson, 1, False, False, True, pos, None, True)
EndFunction

; Nameless friendly NPCs around the delivery point, so the named person does not stand alone in an empty
; POI (his play, 2026-10-08: "the created NPCs are alone at the POIs which looks wierd").
Function PlaceCompany(ReferenceAlias akTarget, FormList akCompany, Int aiMin, Int aiMax, Float afRadius) Global
    If !akCompany || akCompany.GetSize() == 0 || aiMax <= 0
        Return
    EndIf
    ObjectReference target = akTarget.GetRef()
    Float[] pos = new Float[6]
    Int n = Utility.RandomInt(aiMin, aiMax)
    While n > 0
        pos[0] = Utility.RandomFloat(-afRadius, afRadius)
        pos[1] = Utility.RandomFloat(-afRadius, afRadius)
        pos[2] = 0
        target.PlaceAtMe(akCompany.GetAt(Utility.RandomInt(0, akCompany.GetSize() - 1)), 1, False, False, True, pos, None, True)
        n -= 1
    EndWhile
EndFunction
