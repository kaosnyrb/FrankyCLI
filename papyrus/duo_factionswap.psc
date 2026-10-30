Scriptname duo_factionswap
{Plan OT-01: does a site's roll follow its one LocEnc keyword when we swap it before it loads? Console only.}

; cgf "duo_factionswap.Swap" 0
; Removes whichever vanilla LocEnc*_Exclusive keywords duo_delvetest_f3's site carries and adds Ecliptic's.
; Prints a mask of what it removed (1 CF, 2 Spacers, 4 Varuun, 8 Ecliptic) for Restore.
Function Swap(Int aiUnused) Global
    Location loc = Site()
    If loc == None
        Debug.MessageBox("factionswap: no site. Take Runaway Plant F3 first.")
        Return
    EndIf
    Int mask = 0
    Int bit = 1
    Int i = 0
    While i < 4
        Keyword k = Enc(i)
        If loc.HasKeyword(k)
            loc.RemoveKeyword(k)
            mask += bit
        EndIf
        bit *= 2
        i += 1
    EndWhile
    loc.AddKeyword(Enc(3))
    Debug.Trace("factionswap: site " + loc.GetFormID() + " swapped to Ecliptic, restore mask " + mask)
    Debug.MessageBox("factionswap: site " + loc.GetFormID() + " swapped to Ecliptic. Restore mask " + mask)
EndFunction

; cgf "duo_factionswap.Restore" <mask>
Function Restore(Int aiMask) Global
    Location loc = Site()
    If loc == None
        Debug.MessageBox("factionswap: no site to restore.")
        Return
    EndIf
    loc.RemoveKeyword(Enc(3))
    Int bit = 1
    Int i = 0
    While i < 4
        If (aiMask / bit) % 2 == 1
            loc.AddKeyword(Enc(i))
        EndIf
        bit *= 2
        i += 1
    EndWhile
    Debug.MessageBox("factionswap: site " + loc.GetFormID() + " restored, mask " + aiMask)
EndFunction

Location Function Site() Global
    Quest q = Game.GetFormFromFile(0x00439D, "du_overtime.esm") as Quest
    If q == None
        Return None
    EndIf
    LocationAlias la = q.GetAlias(5) as LocationAlias
    If la == None
        Return None
    EndIf
    Return la.GetLocation()
EndFunction

; 0 Crimson Fleet, 1 Spacers, 2 House Varuun, 3 Ecliptic (Starfield.esm LocEnc*_Exclusive)
Keyword Function Enc(Int i) Global
    If i == 0
        Return Game.GetFormFromFile(0x023305, "Starfield.esm") as Keyword
    ElseIf i == 1
        Return Game.GetFormFromFile(0x283585, "Starfield.esm") as Keyword
    ElseIf i == 2
        Return Game.GetFormFromFile(0x283580, "Starfield.esm") as Keyword
    EndIf
    Return Game.GetFormFromFile(0x283581, "Starfield.esm") as Keyword
EndFunction
