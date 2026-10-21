Scriptname duo_delve_landing extends ReferenceAlias
{ FrankyCLI, on a fight beat's LANDING marker alias when the recipe gives the beat a `support` team. The step's fragment
  calls Arm; when the player comes within NearDistance of the marker ACROSS THE MAP, a ship is placed on a landing marker
  there and comes down from orbit by itself (his fact, 2026-10-21: "Doing this on a planet causes the ship to appear from
  orbit and land", his landed bounties qst12a to qst16d). Once it reports landed its ramps open; once a ramp is down (or
  RampChecks seconds pass) the team is placed INTO akTeam, a collection wearing the Delve's run-to-player package, so
  they come down the ramp to him; the ship takes off once its landing deck is clear (IsLandingDeckClear, after at least
  DeckMinChecks seconds, at most DeckChecks), and both refs are deleted CleanUpAfter seconds after that.
  His first play of duo_delve33a, 2026-10-21: placed at the hatch with no package, "The UC marines didn't get off the
  ship and just hung out in the landing bay, then the ship took off".

  WHY: his "build a new delve mission with the landings" (2026-10-21), after the strike team test on Overtime's qst11
  quests was rolled back under his rule that a shipped quest is never edited. The team is FRIENDLY: nothing here sets
  aggression, so they fight whoever the place's enemies are, as the strike team's spawn always did (manual 09).

  ⚠ Where they appear: vanilla's bounty hunters stand on the ship's deck markers (Ship_LandDeck_Marker_RefType, filled
  through aliases off the ship's exterior location, QF_OE_KT_Trait_Wanted_Bounty). LocationRefType extends Keyword, so
  GetExteriorRefs(it) is tried first; vanilla never calls GetExteriorRefs, so whether it finds them is UNMEASURED, and
  the trace says which. The exterior load door is the fallback.
  ⚠ Flat distance, as duo_delve_spawnnear: the marker's height over unloaded ground means nothing.

  WHERE IT COMES DOWN, A PROBE (2026-10-21, his "find a best fit landing location via script", after his first play
  came down "in the middle of a fence and tree"): before the ship is called, the pad asks the engine for a clear spot
  round the landing marker, GetSafePosition(SearchRadius, r) at r = 40, 25 and 15, and moves to the first answer; with
  none it stays on the marker. Vanilla never searches for a landing spot (every NPC landing is on a hand-placed marker,
  OE_ShipsAddonScript) and GetSafePosition has no caller anywhere, so what it counts as clear and its units are what his
  plays measure: every answer is traced, the defaults' answer too. His ordering, when it graduates: a hand-placed
  landing marker if the place has one, then this, then the marker. }

Float CheckEvery = 1.0 Const
Int LandChecks = 60 Const
Int RampChecks = 20 Const
Int DeckMinChecks = 5 Const
Int DeckChecks = 60 Const
Float CleanUpAfter = 30.0 Const
Float SearchRadius = 100.0 Const

int TimerApproach = 1 Const
int TimerLanding = 2 Const
int TimerRamp = 3 Const
int TimerDeck = 4 Const
int TimerCleanUp = 5 Const

Form ship
FormList crew
Int crewMin
Int crewMax
Float near
RefCollectionAlias team
WorldSpace world
ObjectReference pad
SpaceshipReference craft
Int checks

Function Arm(Form akShip, FormList akCrew, Int aiMin, Int aiMax, Float afNear, RefCollectionAlias akTeam)
    team = akTeam
    ship = akShip
    crew = akCrew
    crewMin = aiMin
    crewMax = aiMax
    near = afNear
    world = GetRef().GetWorldSpace()
    Debug.Trace(Self + " [landing] armed on " + GetRef() + " in " + world + ", near " + near)
    StartTimer(CheckEvery, TimerApproach)
EndFunction

Function CallShip()
    Furniture ShipLandingMarker_80m_Large = Game.GetFormFromFile(28997, "Starfield.esm") as Furniture;ShipLandingMarker_80m_Large [FURN:00007145]
    pad = GetRef().PlaceAtMe(ShipLandingMarker_80m_Large, 1, True, False, True, None, None, True)
    MovePadToClearSpot()
    craft = pad.PlaceShipAtMe(ship, 4, true, false, false, true, none, None, None, true)
    if !craft
      ;PROBE ONLY, comes out when this graduates: a ship form that places nothing.
      Debug.Notification("Support ship could not be placed")
      Debug.Trace(Self + " [landing] PlaceShipAtMe(" + ship + ") returned None, team placed at the landing marker")
      SpawnCrew(pad, 8.0)
      return
    endif
    Debug.Trace(Self + " [landing] ship " + craft + " called to " + pad)
    checks = 0
    StartTimer(CheckEvery, TimerLanding)
EndFunction

;The probe (see the header): the first clear spot the engine finds, largest radius first, in world offsets from the
;marker (MoveTo's offset does not turn with anything when abRotateOffset is False, manual Inbox 2026-10-12).
Function MovePadToClearSpot()
    ObjectReference here = GetRef()
    Debug.Trace(Self + " [landing] GetSafePosition() at its defaults -> " + Spot(here, here.GetSafePosition()))
    Float[] radii = new Float[3]
    radii[0] = 40.0
    radii[1] = 25.0
    radii[2] = 15.0
    Int i = 0
    While i < radii.Length
      Float[] at = here.GetSafePosition(SearchRadius, radii[i])
      Debug.Trace(Self + " [landing] GetSafePosition(" + SearchRadius + ", " + radii[i] + ") -> " + Spot(here, at))
      if at && at.Length >= 3
        pad.MoveTo(here, at[0] - here.GetPositionX(), at[1] - here.GetPositionY(), at[2] - here.GetPositionZ(), True)
        ;PROBE ONLY, comes out when this graduates: say on screen which rung ran.
        Debug.Notification("Landing: clear spot at r" + (radii[i] as Int) + ", " + (Flat(here, at) as Int) + " off the marker")
        return
      endif
      i += 1
    EndWhile
    ;PROBE ONLY, comes out when this graduates.
    Debug.Notification("Landing: no clear spot found, on the marker")
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
EndFunction

;One crew member per deck marker, round robin, so they stand where vanilla's landing crews stand.
Function SpawnCrewOnDecks(ObjectReference[] akDecks)
    Int n = Utility.RandomInt(crewMin, crewMax)
    Int i = 0
    Float[] offset = new Float[6]
    While i < n
      akDecks[i % akDecks.Length].PlaceAtMe(crew.GetAt(Utility.RandomInt(0, crew.GetSize() - 1)), 1, True, False, True, offset, team, True)
      i += 1
    EndWhile
EndFunction

Function Unload()
    Keyword deck = Game.GetFormFromFile(0x07BB02, "Starfield.esm") as Keyword;Ship_LandDeck_Marker_RefType [LCRT:0007BB02]
    ObjectReference[] decks = craft.GetExteriorRefs(deck)
    ObjectReference[] doors = craft.GetExteriorLoadDoors()
    Debug.Trace(Self + " [landing] " + decks.Length + " deck marker(s), " + doors.Length + " exterior load door(s)")
    if decks.Length > 0
      SpawnCrewOnDecks(decks)
    elseif doors.Length > 0
      SpawnCrew(doors[0], 2.0)
    else
      Debug.Trace(Self + " [landing] ship has no deck marker and no exterior load door, team placed at the landing marker")
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
        CallShip()
      endif

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
      pad.Delete()
      craft = None
      pad = None
    endif
EndEvent
