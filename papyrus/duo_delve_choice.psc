Scriptname duo_delve_choice extends Quest
{The choice Delve: find a thing with a name on it at one place, then take it to whoever owns it at a
second place or sell it to whoever wants it more at a third. Walking to one ends the mission and the
other objective closes. Jessica's Type 2, "Keep It or Give It Back"; his ruling 2026-10-08 that the
choice is made by WHERE YOU GO, not by a dialogue menu. FrankyCLI's Papyrus library, source of truth
in FrankyCLI/papyrus; gen_delve writes the stages, objectives, prose and every property below.

THE STATE IS ONE INTEGER AND EVERY EVENT CHECKS IT FIRST, as in duo_delve_driver: an activation that
arrives in the wrong beat does nothing, so neither ending can fire before the find, and the second
ending cannot fire after the first.

THE PAY IS OURS. The Delves sit on Overtime's self-driven artifact generation, which has no reward
machinery (no RewardGlobal, no MissionQuestScript), so a credit reward exists only if the driver
gives one. Two numbers on the record, one per ending, so the gap is tuned without a recompile.}

; --- aliases, all written by gen_delve --------------------------------------------------------------
ReferenceAlias Property FindTarget Auto Const Mandatory
{Beat 1. Where the thing is stashed, create-obj'd at a marker inside the find place.}
ReferenceAlias Property OwnerTarget Auto Const Mandatory
{Beat 2. Whoever it belongs to, at the main place.}
ReferenceAlias Property BuyerTarget Auto Const Mandatory
{Beat 3. Whoever will pay more for it, at the THIRD place, which gen_delve creates.}

; --- things ------------------------------------------------------------------------------------------
Form Property Item Auto Const Mandatory
{What the player carries from beat 1 to whichever ending they choose.}
Form Property Credits Auto Const Mandatory
{Starfield.esm's Credits, written by gen_delve and checked off disk.}
Int Property OwnerReward Auto Const Mandatory
{Credits paid on the owner's ending.}
Int Property BuyerReward Auto Const Mandatory
{Credits paid on the buyer's ending. The design has the buyer paying more; the gap is his number.}

; One pausing box per beat, all OPTIONAL: a recipe with no message for a beat leaves the property
; unset and nothing is shown. Each is a MESG whose OwnerQuest is this quest, so <Alias=> tokens resolve.
Message Property Beat1Message Auto Const
{Beat 1: picking the thing up.}
Message Property OfferMessage Auto Const
{Straight after beat 1, the surprise: someone has heard and will pay more.}
Message Property Beat2Message Auto Const
{The owner's ending.}
Message Property Beat3Message Auto Const
{The buyer's ending.}

; --- the stage graph. Defaults match gen_delve's choice template; not written per mission. ----------
Int Property StageTaken = 50 Auto Const
{Picked up. Its journal line points at the owner.}
Int Property StageOffer = 60 Auto Const
{The offer arrives. Its journal line names the buyer and says it is the player's call.}
Int Property StageOwner = 100 Auto Const
{Ending A: returned. A completing stage with its own recap.}
Int Property StageBuyer = 110 Auto Const
{Ending B: sold. A completing stage with its own recap.}

Int Property ObjFind = 10 Auto Const
Int Property ObjOwner = 20 Auto Const
Int Property ObjBuyer = 30 Auto Const

; 1 find it | 2 choose | 3 done
Int Beat

Event OnQuestStarted()
    Beat = 1
    SetObjectiveDisplayed(ObjFind, True, False)
    RegisterForRemoteEvent(FindTarget.GetRef(), "OnActivate")
EndEvent

Event ObjectReference.OnActivate(ObjectReference akSender, ObjectReference akActionRef)
    ObjectReference player = Game.GetPlayer()
    If akActionRef != player
        Return
    EndIf

    If akSender == FindTarget.GetRef()
        If Beat == 1
            TakeIt(player)
        EndIf
    ElseIf Beat == 2 && player.GetItemCount(Item) >= 1
        If akSender == OwnerTarget.GetRef()
            Finish(player, StageOwner, ObjOwner, ObjBuyer, OwnerReward, Beat2Message)
        ElseIf akSender == BuyerTarget.GetRef()
            Finish(player, StageBuyer, ObjBuyer, ObjOwner, BuyerReward, Beat3Message)
        EndIf
    EndIf
EndEvent

Function ShowBeat(Message m)
    If m
        m.Show()
    EndIf
EndFunction

; Beat 1 -> 2. The thing goes into the player's hands, then the offer arrives, then BOTH endings show.
; Neither delivery point listens until now, so neither can be reached before the find.
Function TakeIt(ObjectReference player)
    ObjectReference found = FindTarget.GetRef()
    found.BlockActivation(True, True)
    player.AddItem(Item, 1, False)
    found.Disable(False)
    SetObjectiveCompleted(ObjFind, True)
    SetStage(StageTaken)
    ShowBeat(Beat1Message)
    SetStage(StageOffer)
    ShowBeat(OfferMessage)
    SetObjectiveDisplayed(ObjOwner, True, False)
    SetObjectiveDisplayed(ObjBuyer, True, False)
    RegisterForRemoteEvent(OwnerTarget.GetRef(), "OnActivate")
    RegisterForRemoteEvent(BuyerTarget.GetRef(), "OnActivate")
    Beat = 2
EndFunction

; Beat 2 -> done, on whichever ending the player walked to. The other objective is HIDDEN, not failed:
; Jessica's rule is that neither side is the villain, so the road not taken must not read as a loss.
Function Finish(ObjectReference player, Int stage, Int chosen, Int other, Int reward, Message m)
    Beat = 3
    player.RemoveItem(Item, 1, False, None)
    OwnerTarget.GetRef().BlockActivation(True, True)
    BuyerTarget.GetRef().BlockActivation(True, True)
    SetObjectiveDisplayed(other, False, False)
    SetObjectiveCompleted(chosen, True)
    player.AddItem(Credits, reward, False)
    ShowBeat(m)
    SetStage(stage)
    CompleteQuest()
    Stop()
EndFunction

Event OnQuestRejected()
    SetObjectiveDisplayed(ObjFind, False, False)
    Stop()
EndEvent
