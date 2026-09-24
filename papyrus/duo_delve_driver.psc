Scriptname duo_delve_driver extends Quest
{The Delve driver, four beats at one place: find the load, find the other half gone, take it back
off whoever has it, finish the job. FrankyCLI's Papyrus library, source of truth in FrankyCLI/papyrus;
gen_delve writes the stages, objectives, prose and every property below from a recipe.

WHY A SCRIPT AT ALL: the objective layer is Papyrus-only. The stage graph can be authored from
records with stock hooks, and the objectives a player actually reads cannot. So each beat's
objective is displayed here, and everything that is CONTENT stays in the recipe.

THE STATE IS ONE INTEGER AND EVERY EVENT CHECKS IT FIRST. An activation or a pickup that arrives in
the wrong beat does nothing, so a player cannot finish out of order and a stray event cannot move
the mission.}

; --- aliases, all written by gen_delve --------------------------------------------------------------
ReferenceAlias Property LoadTarget Auto Const Mandatory
{Beat 1. The dropped load, create-obj'd at a travel marker inside the drawn place.}
ReferenceAlias Property CentreTarget Auto Const Mandatory
{Beats 2 and 4. Where the work was being done. Visited twice, and the second visit is the point.}
ReferenceAlias Property CarrierMarker Auto Const Mandatory
{Beat 3. Where whoever took the other half is standing when the player finds it gone.}

; --- things ------------------------------------------------------------------------------------------
Form Property LoadItem Auto Const Mandatory
{What the player carries from beat 1.}
Form Property MissingItem Auto Const Mandatory
{The other half. The carrier holds it; picking it up is beat 3.}
FormList Property GangMembers Auto Const Mandatory
{Actor bases. The carrier is drawn from it, and so is anybody standing with him.}
Int Property MinGangMembers Auto Const Mandatory
Int Property MaxGangMembers Auto Const Mandatory
{How many stand with the carrier. The carrier himself is always there: he is the key, not an extra.}
Message Property FailMessage Auto Const Mandatory
{Shown when the player activates the centre without what that beat needs.}

; --- the stage graph. Defaults match gen_delve's four-beat template; not written per mission. -----
Int Property StageTaken = 50 Auto Const
Int Property StageAbsent = 60 Auto Const
Int Property StageRecovered = 70 Auto Const
Int Property StageComplete = 100 Auto Const

int CONST_Aggression_VeryAggressive = 2 Const
int CONST_Suspicious_DetectedActor = 2 Const

; 1 find the load | 2 take it to the centre | 3 recover what was taken | 4 finish the job | 5 done
Int Beat

Event OnQuestStarted()
    Beat = 1
    SetObjectiveDisplayed(10, True, False)
    RegisterForRemoteEvent(LoadTarget.GetRef(), "OnActivate")
EndEvent

Event ObjectReference.OnActivate(ObjectReference akSender, ObjectReference akActionRef)
    ObjectReference player = Game.GetPlayer()
    If akActionRef != player
        Return
    EndIf

    If akSender == LoadTarget.GetRef()
        If Beat == 1
            TakeLoad(player)
        EndIf
    ElseIf akSender == CentreTarget.GetRef()
        If Beat == 2
            If player.GetItemCount(LoadItem) >= 1
                FindItGone()
            Else
                FailMessage.Show()
            EndIf
        ElseIf Beat == 4
            If player.GetItemCount(LoadItem) >= 1 && player.GetItemCount(MissingItem) >= 1
                FinishTheJob(player)
            Else
                FailMessage.Show()
            EndIf
        EndIf
    EndIf
EndEvent

; Beat 1 -> 2. The load goes into the player's hands and the centre starts listening.
Function TakeLoad(ObjectReference player)
    ObjectReference load = LoadTarget.GetRef()
    load.BlockActivation(True, True)
    player.AddItem(LoadItem, 1, False)
    SetObjectiveCompleted(10, True)
    SetStage(StageTaken)
    SetObjectiveDisplayed(20, True, False)
    RegisterForRemoteEvent(CentreTarget.GetRef(), "OnActivate")
    Beat = 2
EndFunction

; Beat 2 -> 3. THE ABSENCE. Nothing is handed over here: the player arrives carrying half of
; something and the other half is not where it should be. The journal line on StageAbsent says so,
; and the carrier appears only now, so he cannot be met before the player knows he matters.
Function FindItGone()
    SetObjectiveCompleted(20, True)
    SetStage(StageAbsent)
    SpawnCarrier()
    RegisterForRemoteEvent(Game.GetPlayer(), "OnItemAdded")
    AddInventoryEventFilter(MissingItem)
    SetObjectiveDisplayed(30, True, False)
    Beat = 3
EndFunction

Function SpawnCarrier()
    ObjectReference marker = CarrierMarker.GetRef()
    ActorValue Suspicious = Game.GetFormFromFile(748, "Starfield.esm") as ActorValue ; Suspicious [AVIF:000002EC]
    ActorValue Aggression = Game.GetFormFromFile(700, "Starfield.esm") as ActorValue ; Aggression [AVIF:000002BC]

    Actor carrier = marker.PlaceAtMe(GangMembers.GetAt(Utility.RandomInt(0, GangMembers.GetSize() - 1)), 1, True, False, True, None, None, True) as Actor
    carrier.AddItem(MissingItem, 1, False)
    carrier.SetValue(Suspicious, CONST_Suspicious_DetectedActor)
    carrier.SetValue(Aggression, CONST_Aggression_VeryAggressive)

    Float[] placePosition = new Float[6]
    Int n = Utility.RandomInt(MinGangMembers, MaxGangMembers)
    While n > 0
        placePosition[0] = Utility.RandomFloat(-50, 50)
        placePosition[1] = Utility.RandomFloat(-50, 50)
        placePosition[2] = 0
        Actor enemy = marker.PlaceAtMe(GangMembers.GetAt(Utility.RandomInt(0, GangMembers.GetSize() - 1)), 1, True, False, True, placePosition, None, True) as Actor
        enemy.SetValue(Suspicious, CONST_Suspicious_DetectedActor)
        enemy.SetValue(Aggression, CONST_Aggression_VeryAggressive)
        n -= 1
    EndWhile
EndFunction

; Beat 3 -> 4. Recovered by however the player got it: looted, picked up, handed over.
Event ObjectReference.OnItemAdded(ObjectReference akSender, Form akBaseItem, Int aiItemCount, ObjectReference akItemReference, ObjectReference akSourceContainer, Int aiTransferReason)
    If Beat == 3 && akBaseItem == MissingItem
        UnregisterForRemoteEvent(Game.GetPlayer(), "OnItemAdded")
        SetObjectiveCompleted(30, True)
        SetStage(StageRecovered)
        SetObjectiveDisplayed(40, True, False)
        Beat = 4
    EndIf
EndEvent

; Beat 4 -> done. Back at the centre, and this time the player is the one who brought it.
Function FinishTheJob(ObjectReference player)
    player.RemoveItem(LoadItem, 1, False, None)
    player.RemoveItem(MissingItem, 1, False, None)
    CentreTarget.GetRef().BlockActivation(True, True)
    SetObjectiveCompleted(40, True)
    SetStage(StageComplete)
    CompleteQuest()
    Beat = 5
EndFunction

Event OnQuestRejected()
    SetObjectiveDisplayed(10, False, False)
    Stop()
EndEvent
