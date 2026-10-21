Scriptname duo_podprobe Hidden
{A PROBE, not a mission: can a drop pod come down by script (route B)? Console only, no plugin record.
    cgf "duo_podprobe.Run" 0 0 0 0 1 2 1     the whole arrival (distance height speed pod boom people sound)
    cgf "duo_podprobe.Clear" 0               deletes every probe object and worker within 1000
Every number must be passed: the console fills an omitted one with 0, not with the default here.
The full history, and why this docstring is short, is in the ; comments below.}

; ⛔ WHY THIS DOCSTRING IS SHORT (2026-10-21 14:3x): every build whose longest .pex string was OVER 4096 bytes
; crashed the game the moment cgf called it (v4 4468 B up to v7 5237 B; Starfield.exe+0x28281B5, a read of
; 0xFFFFFFFFFFFFFFFF, identical in all five dumps), and every build under it ran (v1 2225, v2 3269, v3 3857).
; The {} docstring is the string that grew; ; comments are not compiled into the .pex. Papyrus debug info is
; loaded on Defiant (bLoadDebugInformation=1 since 11:08). Vanilla's longest doc block is 878 B of 4448.
; Reading until this build runs: a docstring over 4096 bytes crashes the game. KEEP {} DOCSTRINGS SHORT.
;
; ----- the header as it was, verbatim -----
; A PROBE, not a mission. One question before a drop pod is designed on it: can TranslateTo carry an
; object DOWN from a height so it reads as a fall, and does it arrive where it was sent?
;
; His idea, 2026-10-21: drop pods, off Watchtower (kinggathcreations_spaceship.esm), "They spawn in the sky,
; fall and explode then people get out". Watchtower fires its pod as a SPELL PROJECTILE from height (manual
; part 32, "how a DROP POD comes down"); this is route B, script only, and his caution is the reason it is a
; probe: "the fact they didn't do it suggests there might be a reason". Vanilla's only TranslateTo callers turn
; Starborn puzzle rings in place, so a long move is unproven here. What could go wrong, and what says so:
;   - a STATIC may not translate at all        -> "NOT LANDED", the heights never change
;   - it may stop on the way                   -> "NOT LANDED", the heights stop part way
;   - GetPositionZ may not follow the 3D        -> the numbers and HIS EYE disagree; his eye wins
;   - the ground point may be wrong             -> "ground vs player" is printed so a slope shows
;
; ⛔ FIRST PLAY, 2026-10-21 13:4x, his glass: "pod didn't move". The numbers said LANDED in 3.2 s, 0 off target,
; while the Gemini hung at 150 up: TranslateTo moved a STATIC's position and NOT ITS MESH. So the trail below is a
; claim about the reference, never about what he sees. Vanilla's own TranslateTo target is StarbornRingSpin00, an
; ACTIVATOR whose model is an invisible RotationHelperFree01, and AttachTo is "Attach this reference to the
; specified parent (only works on created refs)". Hence two more routes: pod 4, a vanilla MOVEABLE static moved
; directly, and carrier 1, the pod attached to that invisible helper and the helper moved.
; ⭐ SECOND PLAYS, 2026-10-21 13:5x, his glass: CARRIER 1 WORKS, "causes the capsule to fall and land", while the
; pod's own position still read 150 up and the helper's read 0. So an attached ref's position is STALE and the
; helper's is the truth; everything after the landing reads off the mover. POD 4, the MoveableStatic, read landed
; in 2.9 s, but "The box takes longer and has phyics": its havok body falls on its own, tumbling, so a moveable
; static does not follow the move either. The helper route is the one: upright, on time, on target.
;
; ⭐ THIRD PLAY, 2026-10-21 14:0x: "works, pod spawns, falls, people spawn", and his three notes: "there isn't
; sound, impact explosion, and I feel like it should start higher and go faster". FXLandingEngineDamagePush is a
; push with nothing to see or hear, so boom 1 is now a frag grenade (his pick, after a car nuke was read and refused), a descent sound is played on the
; mover, and the defaults went from 150 up at 50 to 400 up at 200.
;
; ⛔ FOURTH ROUND, 2026-10-21 14:1x: every build with EIGHT declared parameters froze the game "the moment I press
; enter", with eight numbers passed and with seven, and no step line ever reached the log, while v3 (seven
; parameters) ran all afternoon. Reading, not established: cgf cannot call a function with more than seven
; parameters. So carrier is RETIRED (the helper is the settled route and is always used) and sound takes its slot.
;
; NO PLUGIN RECORD. Global functions only, so it runs from the console and nothing in any .esm changes. Pass ALL
; SEVEN numbers (his first run printed boom 0 and people 0; whether he typed them or the console zero-fills an
; omitted int is unread):
;     cgf "duo_podprobe.Run" 0 0 0 0 1 2 1     the whole arrival: frag boom, 2 people, fly-by sound
;     cgf "duo_podprobe.Run" 0 800 300 0 1 2 1     higher and faster
;     cgf "duo_podprobe.Clear" 0               deletes every probe object and worker within 1000
; Run(distance, height, speed, pod, boom, people, sound): 0 means the default for the three floats (25,
; 400, 200). pod 0 Gemini, 1 Mercury, 2 Apollo (NASA), 3 Apollo_Capsule, 4 Neon_AuroraCargoContainer01 (a vanilla
; MoveableStatic); boom 0 none, 1 fragGrenadeExplosion (his pick: "ah carnuke isn't good, use frag grenade for
; now"), 2 RocketlauncherExplosion (outer radius 5, damage 115), 3 unused, 4 FXLandingEngineDamagePush (invisible and silent, kept as the control). ⛔ 3 WAS CarNukeLargeVehicles
; and came out before it was ever run: outer radius 900, damage 200, force 6000, a nuke mesh, so it kills the
; player at the 25 it lands from him (gen_inspect explosion, 2026-10-21); people 0 to 4 (UC_NA_MASTWorkerMale01, as
; duo_offpoiprobe), placed 0.3 s after the boom so it cannot hit them; the pod always hangs from an invisible
; StarbornRingSpin00 and that is what moves (pod 4, the MoveableStatic, now rides the helper too); sound 0 none, 1 WwiseEvent_VEHNPCFlyByClose, 2
; WwiseEvent_VEHNPCFlyByFar, 3 WwiseEvent_VEH_Takeoff_Sequence_F_FlyBy, played on the MOVER as it starts down.
; The four capsules are vanilla set dressing and every one STANDS on its origin (gen_inspect bounds, 2026-10-21).
; Clear deletes by BASE, so never run it at the NASA museum, where those capsules stand for real, nor in New
; Atlantis, where the worker does, nor in a Starborn temple, where the helper does.
; NUMBERS ONLY in the output (duo_worldprobe's lesson: a form cast to a string carries a "<" that the message
; box reads as a tag). Units are whatever GetPositionZ returns; read them, do not assume them.

Form Function PodBase(int aiPod) global
    If aiPod == 1
        Return Game.GetForm(0x23E3F1)   ; Nasa_MercuryCapsule01, 2.6 x 4.1 x 2.5
    ElseIf aiPod == 2
        Return Game.GetForm(0x23E2B6)   ; Nasa_ApolloCapsule01, 4.5 x 4.5 x 3.5
    ElseIf aiPod == 3
        Return Game.GetForm(0x1F9D47)   ; Apollo_Capsule, 5.1 x 4.4 x 4.5
    ElseIf aiPod == 4
        Return Game.GetForm(0x55F4)     ; Neon_AuroraCargoContainer01, a MoveableStatic
    ElseIf aiPod == 5
        Return Game.GetForm(0x1BA30)    ; StarbornRingSpin00, the invisible helper (Clear only)
    EndIf
    Return Game.GetForm(0x23E2CF)       ; Nasa_GeminiCapsule01, 2.7 x 2.7 x 3.0, upright
EndFunction

Form Function BoomBase(int aiBoom) global
    If aiBoom == 1
        Return Game.GetForm(0xE574F)    ; fragGrenadeExplosion, his pick 2026-10-21: "use frag grenade for now"
    ElseIf aiBoom == 2
        Return Game.GetForm(0x49B53)    ; RocketlauncherExplosion
    ElseIf aiBoom == 4
        Return Game.GetForm(0x1FF72B)   ; FXLandingEngineDamagePush: no visual, no sound (his third play)
    EndIf
    Return None
EndFunction

; ⛔ v7, 2026-10-21 14:2x: THE SOUND IS OUT, AND IT IS THE ONLY CHANGE. Seven parameters froze the game at Enter
; exactly as eight had (v6), while v3 still ran; the freeze comes before step 1's trace, so the game dies resolving
; the script rather than running it, and the one type every crashing build referenced and v3 never did was
; WwiseEvent (a function returning it, a local of it, its Play). The sound argument is kept so his command keeps its
; shape, and is IGNORED. Was: DescentSound returning Game.GetForm(0x2B16D7 / 0x2B16DA / 0xBD626) as WwiseEvent, and
; sound.Play(mover) as the helper started down (FrankyCLI b6164a7).

; ⛔ His run of v4/v5 (0 0 0 0 1 2 1 1), 2026-10-21 14:1x: "crashes to desktop", with no Papyrus error in the log and
; no probe line at all, because the only trace was at the end. So every step now traces BEFORE it runs; the last
; "POD PROBE step" line in Papyrus.0.log names the step reached (a CTD may drop the final lines: a hint, not proof).
Function Step(String asWhat) global
    Debug.Trace("POD PROBE step " + asWhat)
EndFunction

Function Run(float afDistance = 0.0, float afHeight = 0.0, float afSpeed = 0.0, int aiPod = 0, int aiBoom = 1, int aiPeople = 2, int aiSound = 0) global
    Actor player = Game.GetPlayer()
    Float dist = afDistance
    If dist <= 0.0
        dist = 25.0
    EndIf
    Float height = afHeight
    If height <= 0.0
        height = 400.0
    EndIf
    Float speed = afSpeed
    If speed <= 0.0
        speed = 200.0
    EndIf
    Form pod = PodBase(aiPod)
    Form marker = Game.GetForm(0x3B)    ; XMarker
    Form helperBase = PodBase(5)
    Form boom = BoomBase(aiBoom)
    String out = "POD PROBE  d=" + (dist as Int) + "  h=" + (height as Int) + "  v=" + (speed as Int) + "  pod=" + aiPod + "  boom=" + aiBoom + "  people=" + aiPeople + "  sound=" + aiSound
    If aiBoom > 0 && boom == None
        out += "\nBOOM " + aiBoom + " DID NOT RESOLVE"
    EndIf
    If aiSound > 0
        out += "\nSOUND " + aiSound + " IGNORED (v7: out while the crash is bisected)"
    EndIf
    If pod == None || marker == None || helperBase == None
        out += "\nA BASE DID NOT RESOLVE (pod, XMarker or helper)"
        Debug.Trace(out)
        Debug.MessageBox(out)
        Return
    EndIf

    ; The landing point, straight ahead of him so he can watch it come down. World-frame offsets computed
    ; here (MoveTo's own rotate flag did not turn an offset on 2026-10-12, manual Inbox), then dropped onto
    ; the navmesh, whose height is the ground the pod is sent to.
    Float ang = player.GetAngleZ()
    Float dx = dist * Math.sin(ang)
    Float dy = dist * Math.cos(ang)
    Float pz = player.GetPositionZ()
    Step("1 ground marker  h=" + (height as Int) + " v=" + (speed as Int) + " boom=" + aiBoom + " sound=" + aiSound)
    ObjectReference ground = player.PlaceAtMe(marker)
    ground.MoveTo(player, dx, dy, 0.0, false)
    ground.MoveToNearestNavmeshLocation()
    Float gx = ground.GetPositionX()
    Float gy = ground.GetPositionY()
    Float gz = ground.GetPositionZ()
    out += "\nground vs player " + ((gz - pz) as Int) + "   from player " + (player.GetDistance(ground) as Int)

    Step("2 pod")
    ObjectReference p = player.PlaceAtMe(pod)
    p.MoveTo(ground, 0.0, 0.0, height, false)
    ; mover is what TranslateTo is called on: the invisible helper the pod hangs from, always (carrier retired).
    Step("3 helper")
    ObjectReference mover = player.PlaceAtMe(helperBase)
    mover.MoveTo(ground, 0.0, 0.0, height, false)
    Utility.Wait(0.2)
    Step("4 attach")
    p.AttachTo(mover)
    Utility.Wait(0.2)
    Float z0 = p.GetPositionZ()
    out += "\nstart " + ((z0 - gz) as Int) + " up   3D loaded " + p.Is3DLoaded()
    out += "   helper " + ((mover.GetPositionZ() - gz) as Int) + " up, 3D " + mover.Is3DLoaded()

    Step("6 translate")
    Float t0 = Utility.GetCurrentRealTime()
    mover.TranslateTo(gx, gy, gz, mover.GetAngleX(), mover.GetAngleY(), mover.GetAngleZ(), speed)
    Float limit = height / speed * 2.0 + 5.0
    Float t = 0.0
    Float z = z0
    Bool landed = false
    Int n = 0
    String trail = ""
    ; Checked every 0.1 s so the boom follows the touchdown closely at speed; the trail keeps every third reading.
    While t < limit && !landed
        Utility.Wait(0.1)
        t = Utility.GetCurrentRealTime() - t0
        z = mover.GetPositionZ()
        n += 1
        If n % 3 == 0 && n <= 60
            trail += " " + ((z - gz) as Int)
        EndIf
        landed = Math.abs(z - gz) < 0.5
    EndWhile
    out += "\nmover height every 0.3 s:" + trail
    If landed
        out += "\nLANDED in " + ((t * 10.0) as Int) + " tenths of a s (straight line says " + ((height / speed * 10.0) as Int) + ")"
    Else
        mover.StopTranslation()
        out += "\nNOT LANDED after " + (t as Int) + " s, still " + ((z - gz) as Int) + " up"
    EndIf
    out += "\nthe pod itself READS " + ((p.GetPositionZ() - gz) as Int) + " up (stale once attached: he saw it land, 2026-10-21)"
    out += "   off target " + (ground.GetDistance(mover) as Int)

    Step("7 landed " + landed + ", boom")
    If boom != None
        ground.PlaceAtMe(boom)
        Utility.Wait(0.3)   ; the people come after the blast, so it cannot hit them
    EndIf

    ; People step out beside it, on the navmesh, the way duo_offpoiprobe's route B put a person down. Placed off the
    ; MOVER: an attached pod's own position is stale (still 150 up on his second play while he watched it land), so
    ; people placed at the pod would appear in the sky.
    ActorBase who = Game.GetForm(0x19AD3E) as ActorBase
    Int i = 0
    Int placed = 0
    Step("8 people")
    While i < aiPeople && i < 4 && who != None
        Float side = 3.0
        If i % 2 == 1
            side = -3.0
        EndIf
        Actor a = mover.PlaceActorAtMe(who)
        If a != None
            a.MoveTo(mover, side * Math.cos(ang), -side * Math.sin(ang), 0.0, false)
            a.MoveToNearestNavmeshLocation()
            placed += 1
        EndIf
        i += 1
    EndWhile
    out += "\npeople placed " + placed

    Step("9 done")
    Debug.Trace(out)
    Debug.MessageBox(out)
EndFunction

Function Clear(float afRadius = 0.0) global
    ObjectReference player = Game.GetPlayer()
    Float radius = afRadius
    If radius <= 0.0
        radius = 1000.0
    EndIf
    Int gone = 0
    Int k = 0
    While k < 7
        Form base = PodBase(k)
        If k == 6
            base = Game.GetForm(0x19AD3E)
        EndIf
        ObjectReference[] found = player.FindAllReferencesOfType(base, radius)
        Int i = 0
        While i < found.Length
            found[i].Disable()
            found[i].Delete()
            i += 1
        EndWhile
        gone += found.Length
        k += 1
    EndWhile
    Debug.MessageBox("POD PROBE  cleared " + gone + " within " + (radius as Int))
EndFunction
