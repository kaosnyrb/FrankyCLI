Scriptname duo_worldprobe Hidden
{A PROBE, not a mission. Answers one question before anything is designed on it: what placed
references can a script find around a player standing on a planet surface, WITHOUT an alias?

His ask, 2026-09-24: place the first Delve crate away from the drawn POI, on a real marker the
world already holds, so it reads as actually lost. The query API is FindAllReferencesOfType and it
sees only the LOADED area, so the probe measures that area, centred on the player, because a board
quest of this kind draws its POI in the worldspace the player is already standing in.

NO PLUGIN RECORD. Global functions only, so it runs from the console and nothing in any .esm changes:
    cgf "duo_worldprobe.Run" 0          all four radii
    cgf "duo_worldprobe.Run" 250        one radius
Output goes to a message box and to the Papyrus log.

The bases are the RE overlay marker statics that POIs carry (read off OEJM001Location's special
references and the REOverlay* statics in Starfield.esm, 2026-09-24) plus the plain map marker.}

Function Run(float afRadius = 0.0) global
    ObjectReference player = Game.GetPlayer()

    Form[] bases = new Form[5]
    String[] names = new String[5]
    bases[0] = Game.GetFormFromFile(0x0E3800, "Starfield.esm")
    names[0] = "TravelA1"
    bases[1] = Game.GetFormFromFile(0x0E37FD, "Starfield.esm")
    names[1] = "TravelB1"
    bases[2] = Game.GetFormFromFile(0x0E37F4, "Starfield.esm")
    names[2] = "Center"
    bases[3] = Game.GetFormFromFile(0x20748B, "Starfield.esm")
    names[3] = "MarkerMed"
    bases[4] = Game.GetFormFromFile(0x000010, "Starfield.esm")
    names[4] = "MapMarker"

    Float[] radii
    If afRadius > 0.0
        radii = new Float[1]
        radii[0] = afRadius
    Else
        radii = new Float[4]
        radii[0] = 100.0
        radii[1] = 400.0
        radii[2] = 1600.0
        radii[3] = 6400.0
    EndIf

    ; NUMBERS ONLY. The message box renders HTML, and a form cast to a string prints
    ; "[Location <name> (id)]": the "<" opened a tag and ate every line after it (his first run).
    String out = "WORLD PROBE"
    out += "\npos " + (player.GetPositionX() as Int) + ", " + (player.GetPositionY() as Int) + ", " + (player.GetPositionZ() as Int)

    Int r = 0
    While r < radii.Length
        out += "\n\nr=" + (radii[r] as Int)
        Int b = 0
        While b < bases.Length
            If bases[b] == None
                out += "\n  " + names[b] + ": BASE DID NOT RESOLVE"
            Else
                ObjectReference[] found = player.FindAllReferencesOfType(bases[b], radii[r])
                Float near = -1.0
                Float far = -1.0
                Int i = 0
                While i < found.Length
                    Float d = player.GetDistance(found[i])
                    If near < 0.0 || d < near
                        near = d
                    EndIf
                    If d > far
                        far = d
                    EndIf
                    i += 1
                EndWhile
                out += "\n  " + names[b] + ": " + found.Length
                If found.Length > 0
                    out += "  near " + (near as Int) + "  far " + (far as Int)
                EndIf
            EndIf
            b += 1
        EndWhile
        r += 1
    EndWhile

    ; EACH MAP MARKER'S LOCATION, asked whether it CLAIMS the RE kit. Separates "this place has no
    ; travel markers" from "it has them in the record and the query cannot see them", which the counts
    ; above cannot do on their own (his second run: 0 RE markers beside a POI, 2 map markers found).
    LocationRefType a1 = Game.GetFormFromFile(0x05F478, "Starfield.esm") as LocationRefType
    LocationRefType b1 = Game.GetFormFromFile(0x05F47B, "Starfield.esm") as LocationRefType
    LocationRefType ctr = Game.GetFormFromFile(0x05F198, "Starfield.esm") as LocationRefType
    ObjectReference[] maps = player.FindAllReferencesOfType(bases[4], radii[radii.Length - 1])
    out += "\n\nmap markers, and what their LOCATION claims (A1/B1/Centre):"
    Int m = 0
    While m < maps.Length
        Location l = maps[m].GetCurrentLocation()
        out += "\n  d " + (player.GetDistance(maps[m]) as Int)
        If l == None
            out += "  no location"
        Else
            out += "  " + (l.HasRefType(a1) as Int) + "/" + (l.HasRefType(b1) as Int) + "/" + (l.HasRefType(ctr) as Int)
            out += "  playerHere " + (player.IsInLocation(l) as Int)
        EndIf
        m += 1
    EndWhile

    Debug.Trace(out)
    Debug.MessageBox(out)
EndFunction
