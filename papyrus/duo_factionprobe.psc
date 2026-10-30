Scriptname duo_factionprobe
{Plan OT-01: does a site's faction roll follow its LocEnc keywords? Console only. Remove when it answers.}

; cgf "duo_factionprobe.Narrow" 0
; Takes the site duo_delvetest_f2 picked (alias 5) and removes every vanilla LocEnc*_Exclusive keyword but
; Ecliptic's, so only Ecliptic is left to roll. Prints the mask of what it removed, for Restore.
Function Narrow(Int aiUnused) Global
    Location loc = Site()
    If loc == None
        Debug.MessageBox("factionprobe: no site. Take Runaway Plant F2 first.")
        Return
    EndIf
    Keyword ecliptic = Game.GetFormFromFile(0x283581, "Starfield.esm") as Keyword
    If !loc.HasKeyword(ecliptic)
        Debug.MessageBox("factionprobe: " + loc + " does not list Ecliptic. Nothing changed; take another.")
        Return
    EndIf
    Int mask = 0
    Int bit = 1
    Int i = 0
    While i < 3
        Keyword k = Other(i)
        If loc.HasKeyword(k)
            loc.RemoveKeyword(k)
            mask += bit
        EndIf
        bit *= 2
        i += 1
    EndWhile
    Debug.MessageBox("factionprobe: " + loc + " narrowed to Ecliptic. Restore later with: cgf \"duo_factionprobe.Restore\" " + mask)
EndFunction

; cgf "duo_factionprobe.Restore" <mask>
Function Restore(Int aiMask) Global
    Location loc = Site()
    If loc == None
        Debug.MessageBox("factionprobe: no site to restore.")
        Return
    EndIf
    Int bit = 1
    Int i = 0
    While i < 3
        If (aiMask / bit) % 2 == 1
            loc.AddKeyword(Other(i))
        EndIf
        bit *= 2
        i += 1
    EndWhile
    Debug.MessageBox("factionprobe: " + loc + " restored, mask " + aiMask)
EndFunction

Location Function Site() Global
    Quest q = Game.GetFormFromFile(0x00438F, "du_overtime.esm") as Quest
    If q == None
        Return None
    EndIf
    LocationAlias la = q.GetAlias(5) as LocationAlias
    If la == None
        Return None
    EndIf
    Return la.GetLocation()
EndFunction

; 0 Crimson Fleet, 1 Spacers, 2 House Varuun (Starfield.esm LocEnc*_Exclusive)
Keyword Function Other(Int i) Global
    If i == 0
        Return Game.GetFormFromFile(0x023305, "Starfield.esm") as Keyword
    ElseIf i == 1
        Return Game.GetFormFromFile(0x283585, "Starfield.esm") as Keyword
    EndIf
    Return Game.GetFormFromFile(0x283580, "Starfield.esm") as Keyword
EndFunction
