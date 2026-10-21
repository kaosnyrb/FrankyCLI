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

; THE RECOVER BEAT OUT ON OPEN GROUND, in two halves (his idea, 2026-10-19: "go 59 metres in the air and fire a
; raytrace towards the ground at a random angle. place an npc at that spot and bam, objective outside poi").
; ⛔ WHY TWO HALVES: placing the holder when the step began worked at 150 to 300, where the ground was loaded, and
; at 750 to 2000 he waited in the air and fell when it loaded (his play, 2026-10-20: "when I got close he fell out of
; the sky and died"); a snap on his OnLoad did not save him either. So the step places only an invisible SPOT (a
; marker has no physics, so unloaded ground cannot hurt it) and the holder is spawned there when the player comes
; near, on ground that is loaded by then. duo_delve_spawnnear, on the holder's alias, holds the spot and the wait.

; Half one: an XMarker [STAT:0000003B] afMin to afMax from the marker at a random angle, at the marker's height,
; placed straight INTO akHolder so the objective points out there at once. Persistent while the alias holds it; no
; navmesh snap (there may be no loaded navmesh under it, and a marker needs none). The offset is in the marker's own
; frame, which a random angle does not care about.
ObjectReference Function PlaceSpotOutside(ReferenceAlias akMarker, ReferenceAlias akHolder, Float afMin, Float afMax) Global
    Float ang = Utility.RandomFloat(0.0, 360.0)
    Float dist = Utility.RandomFloat(afMin, afMax)
    Float[] out = new Float[3]
    out[0] = dist * Math.cos(ang)
    out[1] = dist * Math.sin(ang)
    out[2] = 0.0
    Return akMarker.GetRef().PlaceAtMe(Game.GetFormFromFile(0x00003B, "Starfield.esm"), 1, True, False, True, out, akHolder, False) ; XMarker
EndFunction

; Half two: the holder at the spot, 59 up, snapped to navmesh by PlaceAtMe itself (route A of duo_offpoiprobe, which
; landed at the asked distance and near ground height 4 of 4 on loaded ground), INTO akHolder, which takes him in
; place of the spot. His gang scatters round HIM.
Function SpawnHolderAt(ObjectReference akSpot, ReferenceAlias akHolder, FormList akGang, Form akItem, Int aiMin, Int aiMax) Global
    ActorValue Suspicious = Game.GetFormFromFile(748, "Starfield.esm") as ActorValue ; Suspicious [AVIF:000002EC]
    ActorValue Aggression = Game.GetFormFromFile(700, "Starfield.esm") as ActorValue ; Aggression [AVIF:000002BC]
    Float detected = 2.0
    Float veryAggressive = 2.0

    Float[] up = new Float[3]
    up[2] = 59.0
    Actor holder = akSpot.PlaceAtMe(akGang.GetAt(Utility.RandomInt(0, akGang.GetSize() - 1)), 1, True, False, True, up, akHolder, True) as Actor
    holder.AddItem(akItem, 1, False)
    holder.SetValue(Suspicious, detected)
    holder.SetValue(Aggression, veryAggressive)

    Float[] placePosition = new Float[6]
    Int n = Utility.RandomInt(aiMin, aiMax)
    While n > 0
        placePosition[0] = Utility.RandomFloat(-50, 50)
        placePosition[1] = Utility.RandomFloat(-50, 50)
        placePosition[2] = 0
        Actor enemy = holder.PlaceAtMe(akGang.GetAt(Utility.RandomInt(0, akGang.GetSize() - 1)), 1, True, False, True, placePosition, None, True) as Actor
        enemy.SetValue(Suspicious, detected)
        enemy.SetValue(Aggression, veryAggressive)
        n -= 1
    EndWhile
EndFunction

; THE HOLD BEAT. One wave: aiMin to aiMax enemies from the gang list, each placed INTO akWave (PlaceAtMe's
; akAliasToFill on a RefCollectionAlias, as vanilla's MissionBoardCargoContainerScript fills its
; CargoCollection), all aggressive so they come for the player. A stock DefaultCollectionAliasOnDeath on
; akWave sets the next stage when every one of them is dead, so nothing here watches anything. Persistent
; while the quest holds them, so a wave cannot be unloaded out of its own collection.
; WHERE they arrive changes per wave (his first play, 2026-10-09: "enemies spawn in the same spot each wave
; ... bonus points if they aren't near the player"); since 2026-10-12 it is AROUND HIM, never in front (see
; WaveOrigins). aiOrigins is the wave's style, written by gen_delve from the recipe: 1 is a SQUAD (one group
; from anywhere but in front), 2 is a HORDE (the wave dealt round-robin across his left and right, so it closes
; from two sides). Each group scatters within 10 m of its origin, snapped to navmesh. They then come FOR him because each wave collection wears a Travel-to-player package
; (gen_delve writes it), not because of anything here.
Function SpawnWave(ReferenceAlias akMarker, RefCollectionAlias akWave, FormList akGang, Int aiMin, Int aiMax, Int aiOrigins) Global
    ObjectReference[] origins = WaveOrigins(akMarker.GetRef(), Game.GetPlayer(), aiOrigins)
    ActorValue Suspicious = Game.GetFormFromFile(748, "Starfield.esm") as ActorValue ; Suspicious [AVIF:000002EC]
    ActorValue Aggression = Game.GetFormFromFile(700, "Starfield.esm") as ActorValue ; Aggression [AVIF:000002BC]
    Float detected = 2.0       ; Suspicious: DetectedActor
    ; Aggression 1 (Aggressive: enemies only), NOT his gang idiom's 2 (VeryAggressive, which also attacks
    ; neutrals): a wave crossing open ground at 2 stopped to fight every animal it met (his play of 09,
    ; 2026-10-10: "the waves tend to try and attack any wildlife they run into").
    Float aggressive = 1.0

    Float[] placePosition = new Float[6]
    ; A HORDE is the wave's size PER SIDE (his play of 09: "third wave seems small"), sized on aiOrigins.
    Int n = Utility.RandomInt(aiMin, aiMax) * aiOrigins
    Int k = 0
    While k < n
        placePosition[0] = Utility.RandomFloat(-10, 10)
        placePosition[1] = Utility.RandomFloat(-10, 10)
        placePosition[2] = 0
        ObjectReference origin = origins[k % origins.Length]
        Actor enemy = origin.PlaceAtMe(akGang.GetAt(Utility.RandomInt(0, akGang.GetSize() - 1)), 1, True, False, True, placePosition, akWave, True) as Actor
        enemy.SetValue(Suspicious, detected)
        enemy.SetValue(Aggression, aggressive)
        ; Pick up the wave's Travel package NOW. Without this they stood where they spawned (his second play,
        ; 2026-10-09): being filled into the alias does not make an actor re-pick its package. Vanilla's idiom
        ; after a change of what an actor should be doing (QF_OE_KT_Trait_Wanted_Bounty stage 150,
        ; QF_BE_CF02_Ragana_BoardingQu, QF_OE_KT_UCMilitaryTrainingE).
        enemy.EvaluatePackage()
        ; NO StartCombat here. Tried 2026-10-10 for pace and dropped on his next play: "emenies spawn in combat
        ; but seem to drop out before reaching the player" (40 m+ out of sight they cannot detect him, so combat
        ; lapses into a search). Pace comes from the wave package running instead (gen_delve sets it).
        k += 1
    EndWhile
    ; The origins behind him were temporary markers; the site marker is the quest's and stays.
    Int o = 0
    While o < origins.Length
        If origins[o] != akMarker.GetRef()
            origins[o].Delete()
        EndIf
        o += 1
    EndWhile
EndFunction

; Where a wave arrives: AROUND HIM, ANYWHERE BUT IN FRONT, at a random distance, not at the site's travel
; rings (his asks, 2026-10-12: "spawning the enemies closer to the player to make the defense mission more
; intense", then, after he could aim where they landed: "Let's have the distance between 30-100m and the
; arc choosen randoming outside of a 45 angle where the player is facing"). The rings were the wrong
; instrument for close: they sit ~70 m from a site's centre and ~133 m from each other (manual part 32),
; so a ring wave started 60 to 130 m out, where he saw them stutter (2026-10-10), and a horde rarely had
; both rings loaded to come from two sides.
; Each origin is a temporary XMarker [STAT:0000003B] placed NearMin to NearMax metres from him in WORLD
; axes, the direction worked out from his heading the way vanilla does it: facing is (sin, cos) of
; GetAngleZ() (HeavyActorPushOnImpact.psc pushes along exactly that), so behind is that angle + 180.
; ⛔ NOT MoveTo's abRotateOffset, which the first build used: his play 2026-10-12, "they seemed to spawn in
; the same place" every wave. Vanilla only ever pairs abRotateOffset with abMatchRotation = True (four
; TIF_City_CY_Psych01 fragments) and the first build passed False, so the offset most likely stayed in
; world axes, the same compass side of him every wave.
; THE ARC: any bearing outside a 45-degree cone centred on his facing, so more than FrontHalf (22.5) degrees
; either side of it (Eliza's reading of "outside of a 45 angle": the cone is 45 wide; widen FrontHalf if he
; meant 45 each side). aiCount 1 (SQUAD): one origin anywhere on that arc. 2 (HORDE): one on his left half
; of it and one on his right, so it still closes from two sides. Height is his; SpawnWave's PlaceAtMe snaps each enemy to navmesh round it.
; Not at the site (more than SiteReach from the hold's marker, e.g. the pity timer firing while he has
; wandered off): the wave forms at the marker instead, as every hold did before rings.
; The caller owns the returned markers: SpawnWave deletes every origin that is not akSite.
ObjectReference[] Function WaveOrigins(ObjectReference akSite, Actor akPlayer, Int aiCount) Global
    Float NearMin = 30.0       ; metres, his numbers 2026-10-12 (25 to 35 first: he could aim where they landed)
    Float NearMax = 100.0
    Float FrontHalf = 22.5     ; degrees either side of his facing that a wave never comes from
    Float SiteReach = 150.0    ; metres from the hold's marker; the median POI is ~133 m across (part 32)
    ObjectReference[] picked = new ObjectReference[0]
    If akPlayer.GetDistance(akSite) > SiteReach
        picked.Add(akSite)
        Return picked
    EndIf
    Static XMarker = Game.GetFormFromFile(0x00003B, "Starfield.esm") as Static
    Int i = 0
    While i < aiCount
        ; Degrees clockwise from his facing: right half (FrontHalf, 180], left half [180, 360 - FrontHalf).
        Float off = Utility.RandomFloat(FrontHalf, 360.0 - FrontHalf)
        If aiCount >= 2 && i == 0
            off = Utility.RandomFloat(FrontHalf, 180.0)
        ElseIf aiCount >= 2
            off = Utility.RandomFloat(180.0, 360.0 - FrontHalf)
        EndIf
        Float d = Utility.RandomFloat(NearMin, NearMax)
        Float heading = akPlayer.GetAngleZ() + off   ; read per wave, so it follows him round
        ObjectReference m = akPlayer.PlaceAtMe(XMarker, 1, False, False, True)
        m.MoveTo(akPlayer, d * Math.sin(heading), d * Math.cos(heading), 0.0, False)
        picked.Add(m)
        i += 1
    EndWhile
    Return picked
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
; the retired choice driver's PlacePerson / PlaceCompany (duo_delve_choice.psc, removed 2026-10-15; in git).
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

; WHO IS THERE: INFESTED (his dial, 2026-10-11; first used by duo_delve06b, the medal whose find has nested).
; ONE species from the list, as his infestation missions do (duo_missioninfestation_lists picks one Heatleech and
; places the rest as copies), so it reads as a nest rather than a zoo. aiMin to aiMax of them scattered within
; afRadius of the object, snapped to navmesh, set to attack. Placed by a 250 m distance hook on that object, so
; they are there before the player is. Aggression 2 (his gang idiom, which also attacks neutrals) is right here,
; unlike a wave's 1: a nest does not cross open ground, it stays round what it is guarding.
Function PlaceNest(ReferenceAlias akCentre, FormList akList, Int aiMin, Int aiMax, Float afRadius) Global
    ObjectReference centre = akCentre.GetRef()
    Form species = akList.GetAt(Utility.RandomInt(0, akList.GetSize() - 1))
    ActorValue Suspicious = Game.GetFormFromFile(748, "Starfield.esm") as ActorValue ; Suspicious [AVIF:000002EC]
    ActorValue Aggression = Game.GetFormFromFile(700, "Starfield.esm") as ActorValue ; Aggression [AVIF:000002BC]
    Float[] pos = new Float[6]
    Int n = Utility.RandomInt(aiMin, aiMax)
    While n > 0
        pos[0] = Utility.RandomFloat(-afRadius, afRadius)
        pos[1] = Utility.RandomFloat(-afRadius, afRadius)
        pos[2] = 0
        Actor creature = centre.PlaceAtMe(species, 1, True, False, True, pos, None, True) as Actor
        creature.SetValue(Suspicious, 2.0) ; DetectedActor
        creature.SetValue(Aggression, 2.0) ; VeryAggressive
        n -= 1
    EndWhile
EndFunction

; WHO IS THERE: SPACERS (his dial, 2026-10-11; built 2026-10-13, his "crowds driven from the json"). A hostile
; crew holding the site: a MIX from the gang list (unlike a nest's one species), aiMin to aiMax of them scattered
; within afRadius of the object, snapped to navmesh. Aggression 2, his gang idiom (as SpawnHolder): a crew holds
; its ground round the object rather than crossing open country, which is where a wave's 1 was needed.
; WORKERS need no function of their own: PlaceCompany is the friendly mix, called with a wider radius.
Function PlaceCrew(ReferenceAlias akCentre, FormList akGang, Int aiMin, Int aiMax, Float afRadius) Global
    ObjectReference centre = akCentre.GetRef()
    ActorValue Suspicious = Game.GetFormFromFile(748, "Starfield.esm") as ActorValue ; Suspicious [AVIF:000002EC]
    ActorValue Aggression = Game.GetFormFromFile(700, "Starfield.esm") as ActorValue ; Aggression [AVIF:000002BC]
    Float[] pos = new Float[6]
    Int n = Utility.RandomInt(aiMin, aiMax)
    While n > 0
        pos[0] = Utility.RandomFloat(-afRadius, afRadius)
        pos[1] = Utility.RandomFloat(-afRadius, afRadius)
        pos[2] = 0
        Actor member = centre.PlaceAtMe(akGang.GetAt(Utility.RandomInt(0, akGang.GetSize() - 1)), 1, True, False, True, pos, None, True) as Actor
        member.SetValue(Suspicious, 2.0) ; DetectedActor
        member.SetValue(Aggression, 2.0) ; VeryAggressive
        n -= 1
    EndWhile
EndFunction

; THE DEFEAT BEAT (spike 2026-10-14-defeat-and-clear-beats; the Bounty driver's shape). The target appears at the
; marker INTO akTarget, the alias the objective follows and a stock DefaultAliasOnDeath sits on, so his death sets
; the step's stage and nothing here watches anything. A gang (optional: akGang None) stands with him and need not
; die; abOneSpecies picks ONE entry for the whole gang. Each enemy, the target too, carries one thing from akLoot
; with aiChance percent (the drivers: artifacts, 25). Suspicious 2 / Aggression 2, the shipped drivers' values: a
; site fight does not cross open ground, which is what made the waves drop to 1.
Function SpawnTarget(ReferenceAlias akMarker, ReferenceAlias akTarget, Form akBase, FormList akGang, Int aiMin, Int aiMax, Bool abOneSpecies, FormList akLoot, Int aiChance) Global
    ObjectReference marker = akMarker.GetRef()
    Actor target = marker.PlaceAtMe(akBase, 1, True, False, True, None, akTarget, True) as Actor
    Alert(target, akLoot, aiChance)
    If akGang != None
        PlaceGroup(marker, None, akGang, aiMin, aiMax, abOneSpecies, akLoot, aiChance)
    EndIf
EndFunction

; THE CLEAR BEAT (the Hunt driver's shape). aiMin to aiMax enemies at the marker, each INTO akGroup, a collection with
; no package (they hold the site), and a stock DefaultCollectionAliasOnDeath on it sets the step's stage when every
; one is dead. abOneSpecies (the Hunt's way) picks one entry from akList for them all.
Function SpawnGroup(ReferenceAlias akMarker, RefCollectionAlias akGroup, FormList akList, Int aiMin, Int aiMax, Bool abOneSpecies, FormList akLoot, Int aiChance) Global
    PlaceGroup(akMarker.GetRef(), akGroup, akList, aiMin, aiMax, abOneSpecies, akLoot, aiChance)
EndFunction

; Shared by the two above: scatter within 50 m of the marker (the drivers' spread), into akGroup when one is given.
Function PlaceGroup(ObjectReference akCentre, RefCollectionAlias akGroup, FormList akList, Int aiMin, Int aiMax, Bool abOneSpecies, FormList akLoot, Int aiChance) Global
    PlaceAround(akCentre, akGroup, akList, OneOf(akList, abOneSpecies), Utility.RandomInt(aiMin, aiMax), 50.0, akLoot, aiChance)
EndFunction

; The species for a whole group: one entry of the list when abOneSpecies, else None (each enemy draws its own).
Form Function OneOf(FormList akList, Bool abOneSpecies) Global
    If abOneSpecies
        Return akList.GetAt(Utility.RandomInt(0, akList.GetSize() - 1))
    EndIf
    Return None
EndFunction

; aiCount enemies within afSpread of akCentre (snapped to the navmesh), into akGroup when one is given: akOne for all,
; or one drawn from akList each when akOne is None.
Function PlaceAround(ObjectReference akCentre, RefCollectionAlias akGroup, FormList akList, Form akOne, Int aiCount, Float afSpread, FormList akLoot, Int aiChance) Global
    Float[] pos = new Float[6]
    Int n = aiCount
    While n > 0
        pos[0] = Utility.RandomFloat(-afSpread, afSpread)
        pos[1] = Utility.RandomFloat(-afSpread, afSpread)
        pos[2] = 0
        Form who = akOne
        If who == None
            who = akList.GetAt(Utility.RandomInt(0, akList.GetSize() - 1))
        EndIf
        Actor enemy = akCentre.PlaceAtMe(who, 1, True, False, True, pos, akGroup, True) as Actor
        Alert(enemy, akLoot, aiChance)
        n -= 1
    EndWhile
EndFunction

; DROP PODS (2026-10-21, his "3 per pod and 3 pods in different points"), the arrival duo_podprobe proved at his glass
; (manual 32, "how a DROP POD comes down"): each pod is hung by AttachTo from an invisible StarbornRingSpin00 (a placed
; STATIC moves its position and not its mesh; the helper's mesh-less move carries it); the UC04 distant explosion as it
; appears ("a clear winner, big BOOM in the atmosphere"); 400 up at 200, 2 s; a frag at touchdown; aiPer enemies 0.3 s
; later. THE POD IS KIM'S HERO MODEL since 2026-10-21 15:3x (his "Kim has made a droppod, v10", then "go"), was
; vanilla Nasa_GeminiCapsule01: duo_DropPod01 [STAT 003445], Meshes\ccs\droppod01.nif, built from Overtime
; kitbash/droppod01.json, 7.6 x 6.4 x 11.8 m. Its origin is the riser's, its lowest vertices (the two BLADE TIPS, not
; the heat shield, which I first wrote) 5.196 m below it, so it rests LIFT above the ground: 5.1, the tips bedded ~10 cm
; (the box bounds said 5.41, a 0.21 m hover). Its door is its -Y face, so pod and helper are turned to the pod's bearing
; and the door faces akCentre; the crew come out within 1.5 of a marker 4.5 in front of the door (the body face is
; 1.82 out), off the navmesh, never round the middle, which is inside her. aiPods pods land at even bearings round
; akCentre, 15 to 25 out, a random turn and jitter each, ONE AFTER ANOTHER 2 TO 4 S APART AT RANDOM (his 15:0x: "the
; spacer pods should be randomly staggered by 2-4 seconds so they don't all land at once"; it was 0.4 s). A 0.1 s loop
; launches whichever pod is due and lands whichever is down, so each pod's frag and crew come the moment IT lands and a
; later launch never holds an earlier landing back. The attached pod's own position reads 400 up all the way down, so
; everything after the landing is read off the HELPER or the GROUND marker (his checks on the Gemini: solid where it
; lands, and a save and reload keeps it on the ground; both UNMEASURED on Kim's pod, whose solid parts are ship
; modules). Latent: returns when the last pod's crew is out.
; ⛔ HIS FIRST PLAY OF HER, 15:4x: "Pod was hovering", "about shoulder height", "it's also leaning forward", while
; "Spacers spawned ok" off the same navmesh and the trace read every helper 0 off its rest height. So the ground and
; the helper were right and the POD's drawn pose was not. Unproven why: the model may have loaded in the pose PlaceAtMe
; gave it (akCentre's, and a POI marker tilts with its slope) before SetAngle, or been attached a frame into the fall
; (200 m/s at 144 fps is 1.39 m a frame). Two changes, so the resting pose depends on neither: (1) pod and helper are
; placed DISABLED, posed, then enabled, and the pod attached only once WaitFor3DLoad says its model exists; (2) AT
; TOUCHDOWN THE FALLING POD IS SWAPPED for a fresh one placed disabled at the rest pose (the ground marker, LIFT up,
; turned to the bearing) and then enabled, and the falling pod and its helper are disabled and deleted under the frag.
; The pod he fights round is an ordinary placed static, so a save keeps it as any other. If he sees a drop or a
; straighten at touchdown, (1) did not cure the fall and (2) is doing the work.
Function DropPods(ObjectReference akCentre, Int aiPods, Int aiPer, RefCollectionAlias akGroup, FormList akList, Bool abOneSpecies, FormList akLoot, Int aiChance) Global
    Form marker = Game.GetFormFromFile(0x3B, "Starfield.esm")            ; XMarker
    Form podBase = Game.GetFormFromFile(0x3445, "du_overtime.esm")       ; duo_DropPod01, Kim's droppod_v10
    Form helperBase = Game.GetFormFromFile(0x1BA30, "Starfield.esm")     ; StarbornRingSpin00
    Form frag = Game.GetFormFromFile(0xE574F, "Starfield.esm")           ; fragGrenadeExplosion
    WwiseEvent boom = Game.GetFormFromFile(0x2D4441, "Starfield.esm") as WwiseEvent   ; WEF_QST_UC04_Distant_Explosion
    Float height = 400.0
    Float speed = 200.0
    Float fall = height / speed
    Float lift = 5.1      ; the pod's origin above its lowest vertex, less ~10 cm (see above)
    Float doorOut = 4.5   ; the crew marker, in front of the door
    Form one = OneOf(akList, abOneSpecies)
    Int count = aiPods
    If count > 4
        count = 4
    EndIf
    ObjectReference[] grounds = new ObjectReference[4]
    ObjectReference[] helpers = new ObjectReference[4]
    ObjectReference[] doors = new ObjectReference[4]
    ObjectReference[] pods = new ObjectReference[4]
    Float[] bearings = new Float[4]
    Float[] launchAt = new Float[4]   ; seconds after the first; the first is 0
    Int[] stage = new Int[4]          ; 0 waiting, 1 falling, 2 down and crewed
    Int i = 1
    While i < count
        launchAt[i] = launchAt[i - 1] + Utility.RandomFloat(2.0, 4.0)
        i += 1
    EndWhile
    Float turn = Utility.RandomFloat(0.0, 360.0)
    Float t0 = Utility.GetCurrentRealTime()
    Int down = 0
    While down < count
        Float now = Utility.GetCurrentRealTime() - t0
        i = 0
        While i < count
            If stage[i] == 0 && now >= launchAt[i]
                Float bearing = turn + i * 360.0 / count + Utility.RandomFloat(-20.0, 20.0)
                Float r = Utility.RandomFloat(15.0, 25.0)
                ObjectReference g = akCentre.PlaceAtMe(marker)
                g.MoveTo(akCentre, r * Math.sin(bearing), r * Math.cos(bearing), 0.0, False)
                g.MoveToNearestNavmeshLocation()
                ; The door (-Y) toward akCentre: the pod sits at heading bearing from it, so +Y points away.
                ObjectReference d = g.PlaceAtMe(marker)
                d.MoveTo(g, -doorOut * Math.sin(bearing), -doorOut * Math.cos(bearing), 0.0, False)
                d.MoveToNearestNavmeshLocation()
                ; Placed disabled and posed BEFORE their models exist, then enabled (see above).
                ObjectReference h = akCentre.PlaceAtMe(helperBase, 1, False, True)
                h.MoveTo(g, 0.0, 0.0, height + lift, False)
                h.SetAngle(0.0, 0.0, bearing)
                h.Enable()
                ObjectReference p = akCentre.PlaceAtMe(podBase, 1, False, True)
                p.MoveTo(g, 0.0, 0.0, height + lift, False)
                p.SetAngle(0.0, 0.0, bearing)
                p.Enable()
                Bool loaded = p.WaitFor3DLoad()
                h.WaitFor3DLoad()
                p.AttachTo(h)
                If boom != None
                    boom.Play(h)
                EndIf
                h.TranslateTo(g.GetPositionX(), g.GetPositionY(), g.GetPositionZ() + lift, h.GetAngleX(), h.GetAngleY(), h.GetAngleZ(), speed)
                grounds[i] = g
                helpers[i] = h
                doors[i] = d
                pods[i] = p
                bearings[i] = bearing
                stage[i] = 1
                Debug.Trace("[duo_delve_lib] drop pod " + (i + 1) + " launched, model loaded " + loaded)
            ; Down when the helper reaches LIFT above its ground, or called down where it is at twice its fall plus 5 s.
            ElseIf stage[i] == 1 && (Math.abs(helpers[i].GetPositionZ() - grounds[i].GetPositionZ() - lift) <= 0.5 || now > launchAt[i] + fall * 2.0 + 5.0)
                ; The swap: the pod he fights round is a fresh one, posed before its model exists.
                ObjectReference rest = grounds[i].PlaceAtMe(podBase, 1, False, True)
                rest.MoveTo(grounds[i], 0.0, 0.0, lift, False)
                rest.SetAngle(0.0, 0.0, bearings[i])
                rest.Enable()
                Bool restLoaded = rest.WaitFor3DLoad()
                Int off = (helpers[i].GetPositionZ() - grounds[i].GetPositionZ() - lift) as Int
                pods[i].Disable()
                pods[i].Delete()
                helpers[i].Disable()
                helpers[i].Delete()
                grounds[i].PlaceAtMe(frag)
                Utility.Wait(0.3)
                PlaceAround(doors[i], akGroup, akList, one, aiPer, 1.5, akLoot, aiChance)
                Debug.Trace("[duo_delve_lib] drop pod " + (i + 1) + " of " + count + " down at " + (now as Int) + " s (launched at " + (launchAt[i] as Int) + "), helper " + off + " off its rest height, swapped (loaded " + restLoaded + ", angles " + (rest.GetAngleX() as Int) + " " + (rest.GetAngleY() as Int) + " " + (rest.GetAngleZ() as Int) + "), " + aiPer + " out at its door")
                stage[i] = 2
                down += 1
            EndIf
            i += 1
        EndWhile
        Utility.Wait(0.1)
    EndWhile
EndFunction

Function Alert(Actor akEnemy, FormList akLoot, Int aiChance) Global
    ActorValue Suspicious = Game.GetFormFromFile(748, "Starfield.esm") as ActorValue ; Suspicious [AVIF:000002EC]
    ActorValue Aggression = Game.GetFormFromFile(700, "Starfield.esm") as ActorValue ; Aggression [AVIF:000002BC]
    akEnemy.SetValue(Suspicious, 2.0) ; DetectedActor
    akEnemy.SetValue(Aggression, 2.0) ; VeryAggressive
    If akLoot != None && Utility.RandomInt(1, 100) <= aiChance
        akEnemy.AddItem(akLoot.GetAt(Utility.RandomInt(0, akLoot.GetSize() - 1)), 1, False)
    EndIf
EndFunction
