Scriptname duo_delve_landing extends ReferenceAlias
{ FrankyCLI, on a fight beat's LANDING marker alias when the recipe gives the beat a `support` team. The step's fragment
  calls Arm; when the player comes within NearDistance of the marker ACROSS THE MAP, a ship is placed on a landing marker
  there and comes down from orbit by itself (his fact, 2026-10-21: "Doing this on a planet causes the ship to appear from
  orbit and land", his landed bounties qst12a to qst16d). Once it reports landed its ramps open; once a ramp is down (or
  RampChecks seconds pass) the team is placed at its exterior load door, the hatch; TakeOffAfter seconds later it takes
  off, and both refs are deleted CleanUpAfter seconds after that.

  WHY: his "build a new delve mission with the landings" (2026-10-21), after the strike team test on Overtime's qst11
  quests was rolled back under his rule that a shipped quest is never edited. The team is FRIENDLY: nothing here sets
  aggression, so they fight whoever the place's enemies are, as the strike team's spawn always did (manual 09).

  ⚠ Vanilla's bounty hunters put their crew on the ship's deck markers (Ship_LandDeck_Marker_RefType, filled through
  aliases off the ship's exterior location, QF_OE_KT_Trait_Wanted_Bounty). The load door is the no-alias stand-in; if
  the hatch reads wrong at his glass, the deck markers are the next rung.
  ⚠ Flat distance, as duo_delve_spawnnear: the marker's height over unloaded ground means nothing. }

Float CheckEvery = 1.0 Const
Int LandChecks = 60 Const
Int RampChecks = 20 Const
Float TakeOffAfter = 20.0 Const
Float CleanUpAfter = 30.0 Const

int TimerApproach = 1 Const
int TimerLanding = 2 Const
int TimerRamp = 3 Const
int TimerTakeOff = 4 Const
int TimerCleanUp = 5 Const

Form ship
FormList crew
Int crewMin
Int crewMax
Float near
WorldSpace world
ObjectReference pad
SpaceshipReference craft
Int checks

Function Arm(Form akShip, FormList akCrew, Int aiMin, Int aiMax, Float afNear)
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

Function SpawnCrew(ObjectReference akAt, Float afSpread)
    Int n = Utility.RandomInt(crewMin, crewMax)
    Float[] offset = new Float[6]
    While n > 0
      offset[0] = Utility.RandomFloat(-afSpread, afSpread)
      offset[1] = Utility.RandomFloat(-afSpread, afSpread)
      akAt.PlaceAtMe(crew.GetAt(Utility.RandomInt(0, crew.GetSize() - 1)), 1, True, False, True, offset, None, True)
      n -= 1
    EndWhile
EndFunction

Function Unload()
    ObjectReference[] doors = craft.GetExteriorLoadDoors()
    if doors.Length > 0
      SpawnCrew(doors[0], 2.0)
    else
      Debug.Trace(Self + " [landing] ship has no exterior load door, team placed at the landing marker")
      SpawnCrew(pad, 8.0)
    endif
    StartTimer(TakeOffAfter, TimerTakeOff)
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

    elseif aiTimerID == TimerTakeOff
      craft.DisableWithTakeOffOrLandingNoWait()
      StartTimer(CleanUpAfter, TimerCleanUp)

    elseif aiTimerID == TimerCleanUp
      craft.Delete()
      pad.Delete()
      craft = None
      pad = None
    endif
EndEvent
