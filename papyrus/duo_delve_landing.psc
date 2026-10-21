Scriptname duo_delve_landing extends ReferenceAlias
{ FrankyCLI, on a fight beat's LANDING marker alias when the recipe gives the beat a `support` team. The step's fragment
  calls Arm; when the player comes within NearDistance of the marker ACROSS THE MAP, a ship is placed on a landing marker
  there and comes down from orbit by itself (his fact, 2026-10-21: "Doing this on a planet causes the ship to appear from
  orbit and land", his landed bounties qst12a to qst16d). Once it reports landed its ramps open; once a ramp is down (or
  RampChecks seconds pass) the team is placed INTO akTeam, a collection wearing their own Travel to the fight, ON THE
  GROUND IN A RING ROUND THE SHIP, outside its hull, and each one LEAVES akTeam as he comes within ArriveWithin of akFight
  (the fight's marker), so the Travel stops and his own AI takes over (his play, 2026-10-21: "Once the marines reach the
  marker they huddle there doing nothing ... Maybe even just stopping the travel package"; a sandbox round the fight,
  tried first, left them "very chill, don't seem to want to engage in comabt"); the ship takes off once its landing deck is clear
  (IsLandingDeckClear, after at least DeckMinChecks seconds, at most DeckChecks), and both refs are deleted CleanUpAfter
  seconds after that.

  WHY: his "build a new delve mission with the landings" (2026-10-21), after the strike team test on Overtime's qst11
  quests was rolled back under his rule that a shipped quest is never edited. The team is FRIENDLY: nothing here sets
  aggression, so they fight whoever the place's enemies are, as the strike team's spawn always did (manual 09).

  WHY ROUND THE SHIP AND NOT ON IT, his plays 2026-10-21: at the hatch they "hung out in the landing bay", and at the
  exterior load door they leave only if the ramp meets the ground: "unless the ramp is perfectly aligned with the floor
  the people can[not] pathfind off the ship", then his "spawn the marines around the ship rather than in the landing
  bay". The hull radius is vanilla's own for a ship (SQ_StationTrafficManagerQuestScript: half width and half length
  as a circle), so the ring fits whichever ship lands.
  ⚠ Flat distance, as duo_delve_spawnnear: the marker's height over unloaded ground means nothing.

  WHERE IT COMES DOWN, his ruling (2026-10-21, after his first play came down "in the middle of a fence and tree"):
  "spawn a ship if there is a land pad, or if there is a suitable spot. Otherwise we just spawn the npcs on the ground".
  (1) The place's OWN pad: the closest hand-placed landing marker within PadRadius of the landing marker, skipping one
  his own ship sits on. (2) Else a clear spot the engine finds, GetSafePosition(SearchRadius, r) at r = 40, 25 and 15,
  where a pad of ours is placed. (3) Else no ship: the team is placed on the landing marker and runs in on its package.
  Vanilla never searches for a landing spot (every NPC landing is on a hand-placed marker, OE_ShipsAddonScript) and
  GetSafePosition has no caller anywhere. Its radii are in position units (an answer at r 15 came back 88 from the
  marker, inside the 100 search); with logging on, 1 of his first 3 plays on Jemison found a spot and 2 walked in, so
  rung 3 is common, not rare. A pad of the place's is never deleted; only ours is. }

Float CheckEvery = 1.0 Const
Int LandChecks = 60 Const
Int RampChecks = 20 Const
Int DeckMinChecks = 5 Const
Int DeckChecks = 60 Const
Float CleanUpAfter = 30.0 Const
Float SearchRadius = 100.0 Const
Float PadRadius = 250.0 Const
Float OccupiedWithin = 40.0 Const
Float MinHull = 10.0 Const
Float RingGapMin = 3.0 Const
Float RingGapMax = 8.0 Const

int TimerApproach = 1 Const
int TimerLanding = 2 Const
int TimerRamp = 3 Const
int TimerDeck = 4 Const
int TimerCleanUp = 5 Const
int TimerSearch = 6 Const
int TimerArrive = 7 Const

;A Marine within this of the fight's marker leaves the team; past ArriveChecks seconds every one left does too.
Float ArriveWithin = 20.0 Const
Int ArriveChecks = 180 Const

;ONE clear-spot search per tick, SearchGap apart (his 2026-10-21 play: "there is a large stutter on the search if it
;can't find a place"). Four searches ran back to back in one call, the first at the engine's defaults for the log only;
;that one is gone and the other three each get their own tick, timed into the log.
Float SearchGap = 1.0 Const
Int SafeRadiusCount = 3 Const

Form ship
FormList crew
Int crewMin
Int crewMax
Float near
RefCollectionAlias team
ReferenceAlias fight
Int arriveTicks   ; not "arriveChecks": Papyrus is case-insensitive and ArriveChecks is the constant
WorldSpace world
ObjectReference pad
SpaceshipReference craft
Bool padIsOurs
Int checks
Int searchStep

Function Arm(Form akShip, FormList akCrew, Int aiMin, Int aiMax, Float afNear, RefCollectionAlias akTeam, ReferenceAlias akFight)
    team = akTeam
    fight = akFight
    ship = akShip
    crew = akCrew
    crewMin = aiMin
    crewMax = aiMax
    near = afNear
    world = GetRef().GetWorldSpace()
    Debug.Trace(Self + " [landing] armed on " + GetRef() + " in " + world + ", near " + near)
    StartTimer(CheckEvery, TimerApproach)
EndFunction

;Rung 1 (the header's three): the place's own pad, in this tick; else rung 2 starts on the timer, one search a tick.
Function FindOwnPad()
    ObjectReference here = GetRef()
    Float t0 = Utility.GetCurrentRealTime()
    ObjectReference own = OwnPad(here)
    Debug.Trace(Self + " [landing] pad search took " + (Utility.GetCurrentRealTime() - t0) + " s, found " + own)
    if own
      pad = own
      padIsOurs = False
      ;PROBE ONLY, comes out when this graduates: say on screen which rung ran.
      Debug.Notification("Landing: on the place's own pad, " + (here.GetDistance(own) as Int) + " off the marker")
      CallShip()
      return
    endif
    searchStep = 0
    StartTimer(SearchGap, TimerSearch)
EndFunction

;Rung 2, one radius per tick, largest first; past the last, rung 3: no ship, the team arrives on foot.
Function SearchStep()
    ObjectReference here = GetRef()
    Float r = SafeRadius(searchStep)
    Float t0 = Utility.GetCurrentRealTime()
    Float[] at = here.GetSafePosition(SearchRadius, r)
    Debug.Trace(Self + " [landing] GetSafePosition(" + SearchRadius + ", " + r + ") took " + (Utility.GetCurrentRealTime() - t0) + " s -> " + Spot(here, at))
    if at && at.Length >= 3
      ;PROBE ONLY, comes out when this graduates: say on screen which rung ran.
      Debug.Notification("Landing: clear spot at r" + (r as Int) + ", " + (Flat(here, at) as Int) + " off the marker")
      Furniture ShipLandingMarker_80m_Large = Game.GetFormFromFile(0x007145, "Starfield.esm") as Furniture;[FURN:00007145]
      pad = here.PlaceAtMe(ShipLandingMarker_80m_Large, 1, True, False, True, None, None, True)
      ;World offsets: MoveTo's offset does not turn with anything when abRotateOffset is False (manual Inbox 2026-10-12).
      pad.MoveTo(here, at[0] - here.GetPositionX(), at[1] - here.GetPositionY(), at[2] - here.GetPositionZ(), True)
      padIsOurs = True
      CallShip()
      return
    endif
    searchStep += 1
    if searchStep < SafeRadiusCount
      StartTimer(SearchGap, TimerSearch)
      return
    endif
    ;PROBE ONLY, comes out when this graduates: say on screen which rung ran.
    Debug.Notification("Landing: no pad and no clear spot, the team arrives on foot")
    SpawnCrew(here, 8.0)
EndFunction

Float Function SafeRadius(Int aiStep)
    if aiStep == 0
      return 40.0
    elseif aiStep == 1
      return 25.0
    endif
    return 15.0
EndFunction

;The ship onto the pad FindOwnPad or SearchStep chose.
Function CallShip()
    craft = pad.PlaceShipAtMe(ship, 4, true, false, false, true, none, None, None, true)
    if !craft
      ;PROBE ONLY, comes out when this graduates: a ship form that places nothing.
      Debug.Notification("Support ship could not be placed")
      Debug.Trace(Self + " [landing] PlaceShipAtMe(" + ship + ") returned None, team placed at the pad")
      SpawnCrew(pad, 8.0)
      DropPad()
      return
    endif
    Debug.Trace(Self + " [landing] ship " + craft + " called to " + pad)
    checks = 0
    StartTimer(CheckEvery, TimerLanding)
EndFunction

;The closest hand-placed landing marker within PadRadius. Five furniture bases carry every one in his load order
;(gen_delve bases, 2026-10-21: 40m_Small 124 refs, 60m_Medium 108, 80m_Large 54, the two Vertical 6 and 3). One his
;own ship sits on is his, and skipped. ⚠ The search returns the closest of EACH base, so a second free pad of the same
;base further out is not seen; the clear spot catches that case.
ObjectReference Function OwnPad(ObjectReference akHere)
    Int[] ids = new Int[5]
    ids[0] = 0x1D6E5E ;ShipLandingMarker_40m_Small
    ids[1] = 0x17DE66 ;ShipLandingMarker_60m_Medium
    ids[2] = 0x007145 ;ShipLandingMarker_80m_Large
    ids[3] = 0x2E3E47 ;ShipLandingMarkerVertical_40m_Small
    ids[4] = 0x2E3E48 ;ShipLandingMarkerVertical_60m_Medium
    SpaceshipReference home = Game.GetPlayerHomeSpaceShip()
    ObjectReference best = None
    Float bestAt = 0.0
    Int i = 0
    While i < ids.Length
      ObjectReference found = Game.FindClosestReferenceOfTypeFromRef(Game.GetFormFromFile(ids[i], "Starfield.esm"), akHere, PadRadius)
      if found
        Float d = akHere.GetDistance(found)
        Bool his = home && home.GetDistance(found) < OccupiedWithin
        Debug.Trace(Self + " [landing] pad " + found + " " + d + " from the marker, his ship on it " + his)
        if !his && (!best || d < bestAt)
          best = found
          bestAt = d
        endif
      endif
      i += 1
    EndWhile
    return best
EndFunction

;Only a pad of ours is ever deleted; the place's own stays where its author put it.
Function DropPad()
    if pad && padIsOurs
      pad.Delete()
    endif
    pad = None
EndFunction

String Function Spot(ObjectReference akFrom, Float[] akAt)
    if !akAt || akAt.Length < 3
      return "nothing"
    endif
    return akAt[0] + ", " + akAt[1] + ", " + akAt[2] + " (flat " + Flat(akFrom, akAt) + " from the marker, dz " + (akAt[2] - akFrom.GetPositionZ()) + ")"
EndFunction

Float Function Flat(ObjectReference akFrom, Float[] akAt)
    Float dx = akAt[0] - akFrom.GetPositionX()
    Float dy = akAt[1] - akFrom.GetPositionY()
    return Math.sqrt(dx * dx + dy * dy)
EndFunction

Function SpawnCrew(ObjectReference akAt, Float afSpread)
    Int n = Utility.RandomInt(crewMin, crewMax)
    Float[] offset = new Float[6]
    While n > 0
      offset[0] = Utility.RandomFloat(-afSpread, afSpread)
      offset[1] = Utility.RandomFloat(-afSpread, afSpread)
      akAt.PlaceAtMe(crew.GetAt(Utility.RandomInt(0, crew.GetSize() - 1)), 1, True, False, True, offset, team, True)
      n -= 1
    EndWhile
    StartArrive()
EndFunction

;A ring round akAt between afMin and afMax, random bearing each; PlaceAtMe snaps each offset to the navmesh, so they
;stand on walkable ground (offsets turn with akAt's facing, which a random bearing makes immaterial).
Function SpawnCrewAround(ObjectReference akAt, Float afMin, Float afMax)
    Int n = Utility.RandomInt(crewMin, crewMax)
    Float[] offset = new Float[6]
    While n > 0
      Float bearing = Utility.RandomFloat(0.0, 360.0)
      Float r = Utility.RandomFloat(afMin, afMax)
      offset[0] = r * Math.sin(bearing)
      offset[1] = r * Math.cos(bearing)
      akAt.PlaceAtMe(crew.GetAt(Utility.RandomInt(0, crew.GetSize() - 1)), 1, True, False, True, offset, team, True)
      n -= 1
    EndWhile
    StartArrive()
EndFunction

;Watch the team on its way in: every placed member leaves it at the fight (ArriveStep). Started by both spawns, so no
;path that puts a team down can miss it.
Function StartArrive()
    arriveTicks = 0
    StartTimer(CheckEvery, TimerArrive)
EndFunction

;Each member leaves the team as he reaches the fight's marker, dead or not found ones too, and past ArriveChecks
;seconds everyone left, so nobody stays on a Travel for good. Leaving the collection drops its package.
Function ArriveStep()
    arriveTicks += 1
    ObjectReference at = fight.GetRef()
    Int i = team.GetCount() - 1
    While i >= 0
      Actor m = team.GetAt(i) as Actor
      if m && (arriveTicks >= ArriveChecks || !at || m.IsDead() || m.GetDistance(at) < ArriveWithin)
        team.RemoveRef(m)
        m.EvaluatePackage()
        Debug.Trace(Self + " [landing] " + m + " left the team after " + arriveTicks + " checks, dead " + m.IsDead())
      endif
      i -= 1
    EndWhile
    if team.GetCount() > 0
      StartTimer(CheckEvery, TimerArrive)
    endif
EndFunction

;The team on the ground round the ship (see the header for why not on it), centred on the pad it came down on.
Function Unload()
    Float halfW = craft.GetWidth() * 0.5
    Float halfL = craft.GetLength() * 0.5
    Float hull = Math.sqrt(halfW * halfW + halfL * halfL)
    Debug.Trace(Self + " [landing] ship " + craft.GetWidth() + " wide, " + craft.GetLength() + " long, hull radius " + hull)
    if hull < MinHull
      hull = MinHull
    endif
    SpawnCrewAround(pad, hull + RingGapMin, hull + RingGapMax)
    checks = 0
    StartTimer(CheckEvery, TimerDeck)
EndFunction

Event OnTimer(int aiTimerID)
    if !GetOwningQuest().IsRunning()
      if craft
        craft.DisableWithTakeOffOrLandingNoWait()
      endif
      return
    endif

    if aiTimerID == TimerApproach
      ObjectReference here = GetRef()
      if !world
        world = here.GetWorldSpace()
      endif
      Actor player = Game.GetPlayer()
      Float dx = player.GetPositionX() - here.GetPositionX()
      Float dy = player.GetPositionY() - here.GetPositionY()
      if !world || player.GetWorldSpace() != world || dx * dx + dy * dy > near * near
        StartTimer(CheckEvery, TimerApproach)
      else
        FindOwnPad()
      endif

    elseif aiTimerID == TimerSearch
      SearchStep()

    elseif aiTimerID == TimerArrive
      ArriveStep()

    elseif aiTimerID == TimerLanding
      checks += 1
      if craft.IsLanded()
        ObjectReference[] ramps = craft.GetLandingRamps()
        int i = 0
        While i < ramps.Length
          ramps[i].SetOpen(true)
          i += 1
        EndWhile
        Debug.Trace(Self + " [landing] landed after " + checks + " checks, " + ramps.Length + " ramp(s) opened")
        checks = 0
        StartTimer(CheckEvery, TimerRamp)
      elseif checks >= LandChecks
        ;PROBE ONLY, comes out when this graduates: say on screen which path ran.
        Debug.Notification("Support ship never landed")
        Debug.Trace(Self + " [landing] never reported landed after " + checks + " checks")
        Unload()
      else
        StartTimer(CheckEvery, TimerLanding)
      endif

    elseif aiTimerID == TimerRamp
      checks += 1
      if craft.IsRampDown() || checks >= RampChecks
        Debug.Trace(Self + " [landing] ramp down " + craft.IsRampDown() + " after " + checks + " checks")
        Unload()
      else
        StartTimer(CheckEvery, TimerRamp)
      endif

    elseif aiTimerID == TimerDeck
      checks += 1
      if (checks >= DeckMinChecks && craft.IsLandingDeckClear()) || checks >= DeckChecks
        Debug.Trace(Self + " [landing] taking off after " + checks + " checks, deck clear " + craft.IsLandingDeckClear())
        craft.DisableWithTakeOffOrLandingNoWait()
        StartTimer(CleanUpAfter, TimerCleanUp)
      else
        StartTimer(CheckEvery, TimerDeck)
      endif

    elseif aiTimerID == TimerCleanUp
      craft.Delete()
      craft = None
      DropPad()
    endif
EndEvent
