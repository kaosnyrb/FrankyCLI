Scriptname duo_podprobe Hidden
{A PROBE, not a mission. One question before a drop pod is designed on it: can TranslateTo carry an
object DOWN from a height so it reads as a fall, and does it arrive where it was sent?

His idea, 2026-10-21: drop pods, off Watchtower (kinggathcreations_spaceship.esm), "They spawn in the sky,
fall and explode then people get out". Watchtower fires its pod as a SPELL PROJECTILE from height (manual
part 32, "how a DROP POD comes down"); this is route B, script only, and his caution is the reason it is a
probe: "the fact they didn't do it suggests there might be a reason". Vanilla's only TranslateTo callers turn
Starborn puzzle rings in place, so a long move is unproven here. What could go wrong, and what says so:
  - a STATIC may not translate at all        -> "NOT LANDED", the heights never change
  - it may stop on the way                   -> "NOT LANDED", the heights stop part way
  - GetPositionZ may not follow the 3D        -> the numbers and HIS EYE disagree; his eye wins
  - the ground point may be wrong             -> "ground vs player" is printed so a slope shows

⛔ FIRST PLAY, 2026-10-21 13:4x, his glass: "pod didn't move". The numbers said LANDED in 3.2 s, 0 off target,
while the Gemini hung at 150 up: TranslateTo moved a STATIC's position and NOT ITS MESH. So the trail below is a
claim about the reference, never about what he sees. Vanilla's own TranslateTo target is StarbornRingSpin00, an
ACTIVATOR whose model is an invisible RotationHelperFree01, and AttachTo is "Attach this reference to the
specified parent (only works on created refs)". Hence two more routes: pod 4, a vanilla MOVEABLE static moved
directly, and carrier 1, the pod attached to that invisible helper and the helper moved.

NO PLUGIN RECORD. Global functions only, so it runs from the console and nothing in any .esm changes. Pass ALL
SEVEN numbers (on his first run the omitted ints read 0, not their declared defaults):
    cgf "duo_podprobe.Run" 0 0 0 0 0 0 1     Gemini on the invisible helper, nothing else
    cgf "duo_podprobe.Run" 0 0 0 4 0 0 0     the cargo container, moved directly
    cgf "duo_podprobe.Run" 0 0 0 0 1 2 1     the helper route with the landing push and 2 people
    cgf "duo_podprobe.Clear" 0               deletes every probe object and worker within 1000
Run(distance, height, speed, pod, boom, people, carrier): 0 means the default for the three floats (25, 150,
50). pod 0 Gemini, 1 Mercury, 2 Apollo (NASA), 3 Apollo_Capsule, 4 Neon_AuroraCargoContainer01 (a vanilla
MoveableStatic); boom 0 none, 1 FXLandingEngineDamagePush (a landing ship's push), 2 fragGrenadeExplosion;
people 0 to 4 (UC_NA_MASTWorkerMale01, as duo_offpoiprobe); carrier 0 move the pod itself, 1 attach it to an
invisible StarbornRingSpin00 and move that. The four capsules are vanilla set dressing and every one STANDS on
its origin (gen_inspect bounds, 2026-10-21).
Clear deletes by BASE, so never run it at the NASA museum, where those capsules stand for real, nor in New
Atlantis, where the worker does, nor in a Starborn temple, where the helper does.
NUMBERS ONLY in the output (duo_worldprobe's lesson: a form cast to a string carries a "<" that the message
box reads as a tag). Units are whatever GetPositionZ returns; read them, do not assume them.}

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

Function Run(float afDistance = 0.0, float afHeight = 0.0, float afSpeed = 0.0, int aiPod = 0, int aiBoom = 1, int aiPeople = 2, int aiCarrier = 0) global
    Actor player = Game.GetPlayer()
    Float dist = afDistance
    If dist <= 0.0
        dist = 25.0
    EndIf
    Float height = afHeight
    If height <= 0.0
        height = 150.0
    EndIf
    Float speed = afSpeed
    If speed <= 0.0
        speed = 50.0
    EndIf
    Form pod = PodBase(aiPod)
    Form marker = Game.GetForm(0x3B)    ; XMarker
    Form helperBase = PodBase(5)
    String out = "POD PROBE  d=" + (dist as Int) + "  h=" + (height as Int) + "  v=" + (speed as Int) + "  pod=" + aiPod + "  boom=" + aiBoom + "  people=" + aiPeople + "  carrier=" + aiCarrier
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
    ObjectReference ground = player.PlaceAtMe(marker)
    ground.MoveTo(player, dx, dy, 0.0, false)
    ground.MoveToNearestNavmeshLocation()
    Float gx = ground.GetPositionX()
    Float gy = ground.GetPositionY()
    Float gz = ground.GetPositionZ()
    out += "\nground vs player " + ((gz - pz) as Int) + "   from player " + (player.GetDistance(ground) as Int)

    ObjectReference p = player.PlaceAtMe(pod)
    p.MoveTo(ground, 0.0, 0.0, height, false)
    ; mover is what TranslateTo is called on: the pod itself, or the invisible helper the pod hangs from.
    ObjectReference mover = p
    If aiCarrier == 1
        mover = player.PlaceAtMe(helperBase)
        mover.MoveTo(ground, 0.0, 0.0, height, false)
        Utility.Wait(0.2)
        p.AttachTo(mover)
    EndIf
    Utility.Wait(0.2)
    Float z0 = p.GetPositionZ()
    out += "\nstart " + ((z0 - gz) as Int) + " up   3D loaded " + p.Is3DLoaded()
    If aiCarrier == 1
        out += "   helper " + ((mover.GetPositionZ() - gz) as Int) + " up, 3D " + mover.Is3DLoaded()
    EndIf

    Float t0 = Utility.GetCurrentRealTime()
    mover.TranslateTo(gx, gy, gz, mover.GetAngleX(), mover.GetAngleY(), mover.GetAngleZ(), speed)
    Float limit = height / speed * 2.0 + 5.0
    Float t = 0.0
    Float z = z0
    Bool landed = false
    Int n = 0
    String trail = ""
    While t < limit && !landed
        Utility.Wait(0.25)
        t = Utility.GetCurrentRealTime() - t0
        z = p.GetPositionZ()
        n += 1
        If n <= 12 || n % 4 == 0
            trail += " " + ((z - gz) as Int)
        EndIf
        landed = Math.abs(z - gz) < 0.5
    EndWhile
    out += "\npod height every 0.25 s:" + trail
    If landed
        out += "\nLANDED in " + ((t * 10.0) as Int) + " tenths of a s (straight line says " + ((height / speed * 10.0) as Int) + ")"
    Else
        mover.StopTranslation()
        out += "\nNOT LANDED after " + (t as Int) + " s, still " + ((z - gz) as Int) + " up"
    EndIf
    If aiCarrier == 1
        out += "\nhelper ended " + ((mover.GetPositionZ() - gz) as Int) + " up"
    EndIf
    out += "   off target " + (ground.GetDistance(p) as Int)

    If aiBoom == 1
        ground.PlaceAtMe(Game.GetForm(0x1FF72B))   ; FXLandingEngineDamagePush
    ElseIf aiBoom == 2
        ground.PlaceAtMe(Game.GetForm(0xE574F))    ; fragGrenadeExplosion
    EndIf

    ; People step out beside it, on the navmesh, the way duo_offpoiprobe's route B put a person down.
    ActorBase who = Game.GetForm(0x19AD3E) as ActorBase
    Int i = 0
    Int placed = 0
    While i < aiPeople && i < 4 && who != None
        Float side = 3.0
        If i % 2 == 1
            side = -3.0
        EndIf
        Actor a = p.PlaceActorAtMe(who)
        If a != None
            a.MoveTo(p, side * Math.cos(ang), -side * Math.sin(ang), 0.0, false)
            a.MoveToNearestNavmeshLocation()
            placed += 1
        EndIf
        i += 1
    EndWhile
    out += "\npeople placed " + placed

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
