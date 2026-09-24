Scriptname duo_worldprobe Hidden
{A PROBE, not a mission. Answers one question before anything is designed on it: what placed
references can a script find around a player standing on a planet surface, WITHOUT an alias?

His ask, 2026-09-24: place the first Delve crate away from the drawn POI, on a real marker the
world already holds, so it reads as actually lost. The query API is FindAllReferencesOfType, it
matches on a placed ref's BASE, and it sees only the LOADED area.

SEARCHES THE STATICS, NOT THE TAGS (his steer). A LocationRefType such as RETravelA1LocRef is a tag
in the Location record; the thing in the world is a REFR whose base is a STAT such as
REOverlayTravelA1 [0E3800]. Every REOverlay* static in Starfield.esm is listed below, plus the map
marker family and XMarkerHeading (the base the ~2% of POIs without an REOverlay A1 use).

NO PLUGIN RECORD. Global functions only, so it runs from the console and nothing in any .esm changes:
    cgf "duo_worldprobe.Run" 0          radius 6400
    cgf "duo_worldprobe.Run" 400        any radius
Only bases with at least one hit are printed, so the box stays readable; the rest are counted.
NUMBERS ONLY in the output: the message box renders HTML, and a form cast to a string prints
"[Location <name> (id)]", whose "<" opened a tag and ate every line after it (his first run).}

Function Run(float afRadius = 0.0) global
    ObjectReference player = Game.GetPlayer()
    Float radius = afRadius
    If radius <= 0.0
        radius = 6400.0
    EndIf

    Int[] ids = new Int[28]
    String[] names = new String[28]
    ids[0] = 0x0E3800
    names[0] = "TravelA1"
    ids[1] = 0x0E37FF
    names[1] = "TravelA2"
    ids[2] = 0x0E37FE
    names[2] = "TravelA3"
    ids[3] = 0x0E37FD
    names[3] = "TravelB1"
    ids[4] = 0x0E37FC
    names[4] = "TravelB2"
    ids[5] = 0x0E37FB
    names[5] = "TravelB3"
    ids[6] = 0x0E37F4
    names[6] = "Center"
    ids[7] = 0x0E37FA
    names[7] = "SceneA1"
    ids[8] = 0x0E37F9
    names[8] = "SceneA2"
    ids[9] = 0x0E37F8
    names[9] = "SceneA3"
    ids[10] = 0x0E37F7
    names[10] = "SceneB1"
    ids[11] = 0x0E37F6
    names[11] = "SceneB2"
    ids[12] = 0x0E37F5
    names[12] = "SceneB3"
    ids[13] = 0x0E37F2
    names[13] = "InteriorMarker"
    ids[14] = 0x0E37F3
    names[14] = "ExteriorMarker"
    ids[15] = 0x0E518D
    names[15] = "MarkerSmallImportant"
    ids[16] = 0x18E542
    names[16] = "LeaderMarker"
    ids[17] = 0x204006
    names[17] = "MarkerDetail"
    ids[18] = 0x204008
    names[18] = "MarkerLargeFloor"
    ids[19] = 0x20748A
    names[19] = "MarkerLargeGround"
    ids[20] = 0x20748B
    names[20] = "MarkerMedium"
    ids[21] = 0x20748C
    names[21] = "MarkerSmall"
    ids[22] = 0x37C3B5
    names[22] = "MarkerSmallLiving"
    ids[23] = 0x37C3B6
    names[23] = "MarkerSmallWorking"
    ids[24] = 0x000010
    names[24] = "MapMarker"
    ids[25] = 0x254245
    names[25] = "MapMarker_ShortRange"
    ids[26] = 0x254244
    names[26] = "MapMarker_LongRange"
    ids[27] = 0x000034
    names[27] = "XMarkerHeading"

    String out = "WORLD PROBE  r=" + (radius as Int)
    out += "\npos " + (player.GetPositionX() as Int) + ", " + (player.GetPositionY() as Int) + ", " + (player.GetPositionZ() as Int)

    Int empty = 0
    Int unresolved = 0
    Int b = 0
    While b < ids.Length
        Form base = Game.GetFormFromFile(ids[b], "Starfield.esm")
        If base == None
            unresolved += 1
            out += "\n  " + names[b] + ": BASE DID NOT RESOLVE"
        Else
            ObjectReference[] found = player.FindAllReferencesOfType(base, radius)
            If found.Length == 0
                empty += 1
            Else
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
                out += "\n  " + names[b] + ": " + found.Length + "  near " + (near as Int) + "  far " + (far as Int)
            EndIf
        EndIf
        b += 1
    EndWhile
    out += "\n\n" + empty + " of " + ids.Length + " bases found nothing"
    If unresolved > 0
        out += ", " + unresolved + " did not resolve"
    EndIf

    Debug.Trace(out)
    Debug.MessageBox(out)
EndFunction
