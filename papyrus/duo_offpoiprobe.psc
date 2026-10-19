Scriptname duo_offpoiprobe Hidden
{A PROBE, not a mission. Answers one question before anything is designed on it: can a script put a
person on open ground a random distance and angle from where the player stands, OUTSIDE a POI?

His idea, 2026-10-19: "We take a mapmarker for a poi, go 59 metres in the air and fire a raytrace
towards the ground at a random angle. place an npc at that spot and bam, objective outside poi".
Papyrus has no raycast that returns a hit (vanilla's FXLandingRaycasts only plays impact FX), so
this tries the three natives that do the drop instead, side by side on ONE random target point:

  A  PlaceActorAtMe(base, offset, abSnapOffsetToNavmesh = true)   one native call does it all
  B  PlaceActorAtMe, MoveTo(player, x, y, height), MoveToNearestNavmeshLocation()
     (vanilla's own shape: OE_ShaunD_LootedCorpses, OE_AustinF_Prospectors)
  C  B's float GetSafePosition(searchRadius, safeRadius) at the raised point, then SetPosition there

NO PLUGIN RECORD. Global functions only, so it runs from the console and nothing in any .esm changes:
    cgf "duo_offpoiprobe.Run" 0 0        distance 150, height 59
    cgf "duo_offpoiprobe.Run" 300 59     any distance and height
    cgf "duo_offpoiprobe.Clear" 0        deletes every UC_NA_MASTWorkerMale01 within 1000 of the player
Stand OUTSIDE a POI's walls for the real question, and once at a POI's centre as the control.
The person is UC_NA_MASTWorkerMale01 [19AD3E], the base our Delve people are cloned from. Clear
deletes by BASE, so never run it in New Atlantis, where that base stands for real.
NUMBERS ONLY in the output (duo_worldprobe's lesson: a form cast to a string carries a "<" that the
message box reads as a tag). Units are whatever GetPositionX returns; read them, do not assume them.}

Float Function Dist2(Float ax, Float ay, Float bx, Float by) global
    Float dx = ax - bx
    Float dy = ay - by
    Return Math.sqrt(dx * dx + dy * dy)
EndFunction

String Function Report(String label, ObjectReference r, Float tx, Float ty, Float pz) global
    If r == None
        Return "\n" + label + ": NOT PLACED"
    EndIf
    Float x = r.GetPositionX()
    Float y = r.GetPositionY()
    Float z = r.GetPositionZ()
    Return "\n" + label + ": " + (x as Int) + ", " + (y as Int) + ", " + (z as Int) + "   off target " + (Dist2(x, y, tx, ty) as Int) + "   z vs player " + ((z - pz) as Int) + "   from player " + (Game.GetPlayer().GetDistance(r) as Int)
EndFunction

Function Run(float afDistance = 0.0, float afHeight = 0.0) global
    ObjectReference player = Game.GetPlayer()
    ActorBase who = Game.GetForm(0x19AD3E) as ActorBase
    Float dist = afDistance
    If dist <= 0.0
        dist = 150.0
    EndIf
    Float height = afHeight
    If height <= 0.0
        height = 59.0
    EndIf
    String out = "OFF-POI PROBE  d=" + (dist as Int) + "  h=" + (height as Int)
    If who == None
        out += "\nBASE 19AD3E DID NOT RESOLVE"
        Debug.MessageBox(out)
        Return
    EndIf
    Float px = player.GetPositionX()
    Float py = player.GetPositionY()
    Float pz = player.GetPositionZ()
    Float ang = Utility.RandomFloat(0.0, 360.0)
    Float dx = dist * Math.cos(ang)
    Float dy = dist * Math.sin(ang)
    Float tx = px + dx
    Float ty = py + dy
    out += "\nplayer " + (px as Int) + ", " + (py as Int) + ", " + (pz as Int) + "   angle " + (ang as Int)
    out += "\ntarget " + (tx as Int) + ", " + (ty as Int)

    ; A: the native offset placement with its navmesh snap. The offset may be in the player's own frame;
    ; "off target" says whether it is.
    Float[] offset = new Float[3]
    offset[0] = dx
    offset[1] = dy
    offset[2] = height
    ObjectReference a = player.PlaceActorAtMe(who, 4, None, false, false, true, offset, true)
    out += Report("A snap-offset", a, tx, ty, pz)

    ; B: placed at the player, moved out and up, then dropped onto the nearest navmesh.
    ObjectReference b = player.PlaceActorAtMe(who)
    Float[] safe
    If b != None
        b.MoveTo(player, dx, dy, height, true, false)
        ; C's question is asked HERE, from the raised point, before B is dropped.
        safe = b.GetSafePosition(dist, 2.0)
        b.MoveToNearestNavmeshLocation()
    EndIf
    out += Report("B navmesh", b, tx, ty, pz)

    ; C: wherever GetSafePosition says there is room.
    If safe == None || safe.Length < 3
        out += "\nC safe-pos: EMPTY (no safe position found)"
    Else
        ObjectReference c = player.PlaceActorAtMe(who)
        If c != None
            c.SetPosition(safe[0], safe[1], safe[2])
        EndIf
        out += Report("C safe-pos", c, tx, ty, pz)
    EndIf

    Debug.Trace(out)
    Debug.MessageBox(out)
EndFunction

Function Clear(float afRadius = 0.0) global
    ObjectReference player = Game.GetPlayer()
    Form who = Game.GetForm(0x19AD3E)
    Float radius = afRadius
    If radius <= 0.0
        radius = 1000.0
    EndIf
    ObjectReference[] found = player.FindAllReferencesOfType(who, radius)
    Int i = 0
    While i < found.Length
        found[i].Disable()
        found[i].Delete()
        i += 1
    EndWhile
    Debug.MessageBox("OFF-POI PROBE  cleared " + found.Length + " within " + (radius as Int))
EndFunction
