Scriptname duo_delve_landing extends ReferenceAlias
{ FrankyCLI, on a fight beat's LANDING marker alias when the recipe gives the beat a `support` team. The step's fragment
  calls Arm; when the player comes within NearDistance of the marker ACROSS THE MAP, the team arrives one of two ways,
  his ruling 2026-10-21: "let's just do Pad or Squad. Safe Position isn't really helping us."
  (1) PAD: the place's own hand-placed landing marker, the closest within PadRadius of the landing marker, skipping one
  his own ship sits on. A ship is placed on it and comes down from orbit by itself (his fact: "Doing this on a planet
  causes the ship to appear from orbit and land", his landed bounties qst12a to qst16d). Once it reports landed its
  ramps open; once a ramp is down (its OnShipRampDown, or RampWait seconds) the team is placed INTO akTeam at the ship's exterior load
  door, so they come down the ramp, a collection wearing their own Travel to the fight; each one LEAVES akTeam as he
  comes within ArriveWithin of akFight (the fight's marker), so the Travel stops and his own AI takes over. The ship
  takes off once its landing deck is clear (IsLandingDeckClear, after at least DeckMinChecks seconds, at most
  DeckChecks) and is deleted CleanUpAfter seconds after that. The place's pad is never deleted.
  (2) SQUAD: no pad, so no ship: the team is placed on the landing marker and runs in on the same Travel. A ship that
  never lands, or has no exterior load door, puts its team down the same way, on the pad.
  Drop pods are the middle ground now (his, the same ruling), so nothing here looks for open ground.

  WHY THE RAMP AGAIN: at the load door a team leaves only if the ramp meets the ground ("unless the ramp is perfectly
  aligned with the floor the people can[not] pathfind off the ship"), which on open ground it did not; a hand-placed pad
  is flat by its author, so with the open-ground spots gone the door is back (his "we can bring back the landing ramp").
  Unplayed on a pad as of this change.

  The team is FRIENDLY: nothing here sets aggression, so they fight whoever the place's enemies are, as the strike
  team's spawn always did (manual 09).

  EVENTS, NOT POLLS (his "fix the other timers to use the events", 2026-10-22): the approach is the engine's distance
  event (3D; the landing marker is a real one of the place, so its height is true), the landing and the ramp are the
  ship's own OnShipLanding and OnShipRampDown, and each Marine's arrival is a distance event to the fight's marker. Each
  wait keeps ONE one-shot timer as its fallback, vanilla's shape (BELandingQuestScript patched one in because a landing
  event can be missed). Only the deck-clear wait still polls: there is no event for it, and it runs under a minute,
  only after a ship has landed beside him. }

;HISTORY, kept out of the docstring (a long .pex string crashes the game, papyrus/compile.py):
;- 2026-10-21 10:4x to 16:4x a second rung found open ground with GetSafePosition(100, r) at r 40, 25 and 15 and put a
;  pad of ours there. Its cost, off his logged plays (manual 09): r 40 3.1 to 3.6 s, r 25 1.7 to 1.9 s, r 15 0.7 to 0.9 s
;  on a world with no spot, about 6 s of stutter on a failed search; r 40 and 25 found nothing in 4 of 4, r 15 2 of 4.
;  Retired on his "Safe Position isn't really helping us".
;- 11:21 to 16:4x the team stood in a ring round the hull (vanilla's ship radius, SQ_StationTrafficManagerQuestScript),
;  because on open ground the ramp did not meet it. Retired with the open-ground rung.
;- Deck markers were tried first: GetExteriorRefs(Ship_LandDeck_Marker_RefType) found 0 on the UC A-class transport.
;- A sandbox at the fight left them "very chill, don't seem to want to engage in comabt"; the Travel plus the release
;  at ArriveWithin is what fought.

;Fallbacks, each a single timer: how long to wait for the landing event, then the ramp event, before going on without.
Float LandWait = 60.0 Const
Float RampWait = 20.0 Const
;The deck-clear wait is the one poll left (no event exists): every second, at least DeckMinChecks, at most DeckChecks.
Float CheckEvery = 1.0 Const
Int DeckMinChecks = 5 Const
Int DeckChecks = 60 Const
Float CleanUpAfter = 30.0 Const
Float PadRadius = 250.0 Const
Float OccupiedWithin = 40.0 Const

int TimerLanding = 2 Const
int TimerRamp = 3 Const
int TimerDeck = 4 Const
int TimerCleanUp = 5 Const
int TimerArrive = 6 Const

;A Marine within this of the fight's marker leaves the team (his own distance event); at ArriveWait every one left does too.
Float ArriveWithin = 20.0 Const
Float ArriveWait = 180.0 Const

Form ship
FormList crew
Int crewMin
Int crewMax
Float near
RefCollectionAlias team
ReferenceAlias fight
ObjectReference pad
SpaceshipReference craft
Int checks
Bool called    ; the approach fired: the distance event is not trusted to fire only once
Bool landed    ; the landing went on, by its event or its fallback, whichever came first
Bool unloaded  ; the team came out, by the ramp's event or its fallback
Int nextEvent  ; a distance event id per Marine, so one registration never updates another's

Function Arm(Form akShip, FormList akCrew, Int aiMin, Int aiMax, Float afNear, RefCollectionAlias akTeam, ReferenceAlias akFight)
    team = akTeam
    fight = akFight
    ship = akShip
    crew = akCrew
    crewMin = aiMin
    crewMax = aiMax
    near = afNear
    Debug.Trace(Self + " [landing] armed on " + GetRef() + ", near " + near)
    RegisterForDistanceLessThanEvent(Self, Game.GetPlayer(), near)
EndFunction

Event OnDistanceLessThan(ObjectReference akObj1, ObjectReference akObj2, float afDistance, int aiEventID)
    if !GetOwningQuest().IsRunning()
      return
    endif
    if aiEventID > 0
      Arrived(akObj1, akObj2)
    elseif !called
      called = true
      Debug.Trace(Self + " [landing] the player is within " + afDistance + " of " + GetRef())
      PadOrSquad()
    endif
EndEvent

;The header's two: the place's own pad and a ship onto it, else the squad on foot from the landing marker.
Function PadOrSquad()
    ObjectReference here = GetRef()
    Float t0 = Utility.GetCurrentRealTime()
    pad = OwnPad(here)
    Debug.Trace(Self + " [landing] pad search took " + (Utility.GetCurrentRealTime() - t0) + " s, found " + pad)
    if pad
      ;PROBE ONLY, comes out when this graduates: say on screen which way they came.
      Debug.Notification("Landing: on the place's own pad, " + (here.GetDistance(pad) as Int) + " off the marker")
      CallShip()
      return
    endif
    ;PROBE ONLY, comes out when this graduates: say on screen which way they came.
    Debug.Notification("Landing: no pad here, the team arrives on foot")
    SpawnCrew(here, 8.0)
EndFunction

;The ship onto the place's pad.
Function CallShip()
    craft = pad.PlaceShipAtMe(ship, 4, true, false, false, true, none, None, None, true)
    if !craft
      ;PROBE ONLY, comes out when this graduates: a ship form that places nothing.
      Debug.Notification("Support ship could not be placed")
      Debug.Trace(Self + " [landing] PlaceShipAtMe(" + ship + ") returned None, team placed at the pad")
      SpawnCrew(pad, 8.0)
      return
    endif
    Debug.Trace(Self + " [landing] ship " + craft + " called to " + pad)
    RegisterForRemoteEvent(craft, "OnShipLanding")
    if craft.IsLanded()
      Landed()
    else
      StartTimer(LandWait, TimerLanding)
    endif
EndFunction

Event SpaceshipReference.OnShipLanding(SpaceshipReference akSource, bool abComplete)
    Debug.Trace(Self + " [landing] OnShipLanding " + akSource + " complete " + abComplete)
    if abComplete && akSource == craft
      Landed()
    endif
EndEvent

;Down: open its ramps and wait for one to be down (its event, or RampWait at most).
Function Landed()
    if landed
      return
    endif
    landed = true
    CancelTimer(TimerLanding)
    UnregisterForRemoteEvent(craft, "OnShipLanding")
    RegisterForRemoteEvent(craft, "OnShipRampDown")
    ObjectReference[] ramps = craft.GetLandingRamps()
    int i = 0
    While i < ramps.Length
      ramps[i].SetOpen(true)
      i += 1
    EndWhile
    Debug.Trace(Self + " [landing] landed, " + ramps.Length + " ramp(s) opened")
    if craft.IsRampDown()
      Unload()
    else
      StartTimer(RampWait, TimerRamp)
    endif
EndFunction

Event SpaceshipReference.OnShipRampDown(SpaceshipReference akSource)
    Debug.Trace(Self + " [landing] OnShipRampDown " + akSource)
    if akSource == craft
      Unload()
    endif
EndEvent

;The closest hand-placed landing marker within PadRadius. Five furniture bases carry every one in his load order
;(gen_delve bases, 2026-10-21: 40m_Small 124 refs, 60m_Medium 108, 80m_Large 54, the two Vertical 6 and 3). One his
;own ship sits on is his, and skipped. ⚠ The search returns the closest of EACH base, so a second free pad of the same
;base further out is not seen, and that place gets the squad.
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

;Every placed Marine gets his own distance event to the fight's marker (Arrived), and one ArriveWait timer releases
;whoever is left, dead or stuck, so nobody stays on a Travel for good. SpawnCrew is the one spawn, so no path that puts
;a team down can miss either.
Function SpawnCrew(ObjectReference akAt, Float afSpread)
    ObjectReference at = fight.GetRef()
    Int n = Utility.RandomInt(crewMin, crewMax)
    Float[] offset = new Float[6]
    While n > 0
      offset[0] = Utility.RandomFloat(-afSpread, afSpread)
      offset[1] = Utility.RandomFloat(-afSpread, afSpread)
      ObjectReference m = akAt.PlaceAtMe(crew.GetAt(Utility.RandomInt(0, crew.GetSize() - 1)), 1, True, False, True, offset, team, True)
      if m && at
        nextEvent += 1
        RegisterForDistanceLessThanEvent(m, at, ArriveWithin, nextEvent)
      endif
      n -= 1
    EndWhile
    StartTimer(ArriveWait, TimerArrive)
EndFunction

;A Marine reached the fight: he leaves the team, so the Travel stops and his own AI takes over.
Function Arrived(ObjectReference akObj1, ObjectReference akObj2)
    ObjectReference at = fight.GetRef()
    Actor m = akObj1 as Actor
    if akObj1 == at
      m = akObj2 as Actor
    endif
    if m && team.Find(m) >= 0
      team.RemoveRef(m)
      m.EvaluatePackage()
      Debug.Trace(Self + " [landing] " + m + " reached the fight and left the team")
    endif
EndFunction

;ArriveWait is up: everyone still on the team leaves it.
Function ReleaseAll()
    Int i = team.GetCount() - 1
    While i >= 0
      Actor m = team.GetAt(i) as Actor
      if m
        team.RemoveRef(m)
        m.EvaluatePackage()
        Debug.Trace(Self + " [landing] " + m + " left the team at the " + ArriveWait + " s fallback, dead " + m.IsDead())
      endif
      i -= 1
    EndWhile
EndFunction

;The team out of the ship's exterior load door, so they come down the ramp onto the pad (see the header for why a pad
;and not open ground).
Function Unload()
    if unloaded
      return
    endif
    unloaded = true
    CancelTimer(TimerRamp)
    UnregisterForRemoteEvent(craft, "OnShipRampDown")
    ObjectReference[] doors = craft.GetExteriorLoadDoors()
    Debug.Trace(Self + " [landing] " + doors.Length + " exterior load door(s)")
    if doors.Length > 0
      SpawnCrew(doors[0], 2.0)
    else
      ;PROBE ONLY, comes out when this graduates: a ship with no door to come out of.
      Debug.Notification("Support ship has no exterior load door, team placed at the pad")
      SpawnCrew(pad, 8.0)
    endif
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

    if aiTimerID == TimerArrive
      ReleaseAll()

    elseif aiTimerID == TimerLanding
      ;The fallback: no OnShipLanding in LandWait. Landed after all (a missed event, vanilla's patched case), or not.
      if landed
        return
      elseif craft.IsLanded()
        Debug.Trace(Self + " [landing] landed with no OnShipLanding in " + LandWait + " s")
        Landed()
      else
        landed = true
        unloaded = true
        UnregisterForRemoteEvent(craft, "OnShipLanding")
        ;PROBE ONLY, comes out when this graduates: say on screen which path ran. Not out of the door: a ship that never
        ;landed may still be in the air.
        Debug.Notification("Support ship never landed, team placed at the pad")
        Debug.Trace(Self + " [landing] never reported landed in " + LandWait + " s")
        SpawnCrew(pad, 8.0)
        checks = 0
        StartTimer(CheckEvery, TimerDeck)
      endif

    elseif aiTimerID == TimerRamp
      ;The fallback: no OnShipRampDown in RampWait. Out of the door anyway, as the poll always did.
      Debug.Trace(Self + " [landing] no OnShipRampDown in " + RampWait + " s, ramp down " + craft.IsRampDown())
      Unload()

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
      pad = None
    endif
EndEvent
