Scriptname duo_ftprobe Hidden
{A PROBE, not a mission: does EnableFastTravel(False) on a board Delve's map marker stop travel to its site? Console only, no plugin record.
    cgf "duo_ftprobe.Read" 18494        reads only (18494 = MB100b 0x483E, 18532 = MB100a 0x4864)
    cgf "duo_ftprobe.Set" 18494 0       blocks fast travel to the site's marker
    cgf "duo_ftprobe.Set" 18494 1       puts it back}

; His ask, 2026-11-02 16:05: "lets try the script EnableFastTravel one, just to find out either way", after he
; could fast travel straight to the MB100 sites on a save that had visited Akila (a fresh save landed him by ship).
; Manual Inbox, same day: ObjectReference.EnableFastTravel is per map marker and no vanilla script ever passes
; False to it on a POI. The questions his play answers, in order:
;   1. does CanFastTravelToMarker() read False after Set 0 (the call landed)
;   2. on the PLANET SURFACE MAP, does fast travel to the site refuse ("You haven't discovered this location")
;   3. from ORBIT / the star map, can he still land at the site (a different path; the block may not reach it)
;   4. does Set 1 put it back (CanFastTravelToMarker() True again)
; Alias 11 is MapMarker on every board Delve (the board shell, his MB28a) and on vanilla MB_Bounty01Far.
; Accept the mission first: on the board the quest is running but its aliases are filled at the listing.
; Output: one message box and a trace line. Print FormIDs, never a ref: a message box eats a '<' as markup.

Function Read(int aiQuest) global
    ObjectReference m = Marker(aiQuest)
    if m
        Report("read", aiQuest, m, m.CanFastTravelToMarker(), m.CanFastTravelToMarker())
    endif
EndFunction

Function Set(int aiQuest, int aiEnable) global
    ObjectReference m = Marker(aiQuest)
    if m
        bool before = m.CanFastTravelToMarker()
        m.EnableFastTravel(aiEnable == 1)
        Report("set " + aiEnable, aiQuest, m, before, m.CanFastTravelToMarker())
    endif
EndFunction

ObjectReference Function Marker(int aiQuest) global
    Quest q = Game.GetFormFromFile(aiQuest, "du_overtime.esm") as Quest
    if !q
        Debug.MessageBox("ftprobe: no quest " + aiQuest + " in du_overtime.esm")
        return None
    endif
    ReferenceAlias a = q.GetAlias(11) as ReferenceAlias
    if !a
        Debug.MessageBox("ftprobe: quest " + aiQuest + " has no alias 11")
        return None
    endif
    ObjectReference m = a.GetRef()
    if !m
        Debug.MessageBox("ftprobe: alias 11 is EMPTY (running " + q.IsRunning() + ", stage " + q.GetStage() + ")")
        return None
    endif
    return m
EndFunction

Function Report(string asWhat, int aiQuest, ObjectReference akMarker, bool abBefore, bool abAfter) global
    string s = "ftprobe " + asWhat + ": quest " + aiQuest + " marker " + akMarker.GetFormID() + " can fast travel: before " + abBefore + ", after " + abAfter
    Debug.Trace(s)
    Debug.MessageBox(s)
EndFunction
