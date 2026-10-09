Scriptname duo_delve_lib Hidden
{The Delve helpers a stage fragment calls: the work that is Papyrus-only and too long to generate inline
into every quest's fragment script. FrankyCLI's Papyrus library, source of truth in FrankyCLI/papyrus.
A beats Delve has NO DRIVER (his ruling 2026-10-08: the stages are the state machine), so these are
Global functions holding no state: everything they touch is passed in by the fragment that calls them.

Ported from duo_delve_driver.psc, which they replace as each Delve moves onto the beats kind.}

; THE RECOVER BEAT. Whoever took the thing appears at the marker holding it, with a gang drawn from the
; same list, all aggressive. The holder is placed INTO akHolder as he spawns (PlaceAtMe's akAliasToFill),
; so the objective, which targets that alias, follows HIM rather than the spot (his eye, first play of
; duo_delve03). Recovered by however the player gets the item: a stock OnItemAdded hook on the player
; sets the stage, so looting, picking up and being handed it all count.
Function SpawnHolder(ReferenceAlias akMarker, ReferenceAlias akHolder, FormList akGang, Form akItem, Int aiMin, Int aiMax) Global
    ObjectReference marker = akMarker.GetRef()
    ActorValue Suspicious = Game.GetFormFromFile(748, "Starfield.esm") as ActorValue ; Suspicious [AVIF:000002EC]
    ActorValue Aggression = Game.GetFormFromFile(700, "Starfield.esm") as ActorValue ; Aggression [AVIF:000002BC]
    ; A Global function sees no script variables, so the driver's two constants are literals here:
    Float detected = 2.0     ; Suspicious: DetectedActor
    Float veryAggressive = 2.0 ; Aggression: VeryAggressive

    Actor holder = marker.PlaceAtMe(akGang.GetAt(Utility.RandomInt(0, akGang.GetSize() - 1)), 1, True, False, True, None, akHolder, True) as Actor
    holder.AddItem(akItem, 1, False)
    holder.SetValue(Suspicious, detected)
    holder.SetValue(Aggression, veryAggressive)

    Float[] placePosition = new Float[6]
    Int n = Utility.RandomInt(aiMin, aiMax)
    While n > 0
        placePosition[0] = Utility.RandomFloat(-50, 50)
        placePosition[1] = Utility.RandomFloat(-50, 50)
        placePosition[2] = 0
        Actor enemy = marker.PlaceAtMe(akGang.GetAt(Utility.RandomInt(0, akGang.GetSize() - 1)), 1, True, False, True, placePosition, None, True) as Actor
        enemy.SetValue(Suspicious, detected)
        enemy.SetValue(Aggression, veryAggressive)
        n -= 1
    EndWhile
EndFunction
