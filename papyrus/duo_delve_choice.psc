Scriptname duo_delve_choice extends Quest
{The choice Delve: find a thing with a name on it at one place, then take it to whoever owns it at a
second place or sell it to whoever wants it more at a third. Walking to one ends the mission and the
other objective closes. Jessica's Type 2, "Keep It or Give It Back"; his ruling 2026-10-08 that the
choice is made by WHERE YOU GO, not by a dialogue menu. FrankyCLI's Papyrus library, source of truth
in FrankyCLI/papyrus; gen_delve writes the stages, objectives, prose and every property below.

THE STATE IS ONE INTEGER AND EVERY EVENT CHECKS IT FIRST, as in duo_delve_driver: an activation that
arrives in the wrong beat does nothing, so neither ending can fire before the find, and the second
ending cannot fire after the first.

THE PAY IS NOT HERE. A completing stage carries its own reward (QRCR credits, QRXP xp, each a link to
one of Overtime's duo_reward_* globals), and gen_delve points each ending's stage at a tier. This
script paid on top of that once, and the buyer's ending paid twice (his first play, 2026-10-08).}

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

; The PEOPLE at each ending, OPTIONAL: a recipe with no person leaves the property unset and nobody is
; placed. His playtest 2026-10-08: "Returning the medal talks about a person who isn't there." Each is
; an NPC gen_delve clones from a friendly vanilla template (NPCTools' talkable set), named, unaggressive.
ActorBase Property OwnerPerson Auto Const
{Stands beside the owner's delivery point.}
ActorBase Property BuyerPerson Auto Const
{Stands beside the buyer's delivery point.}
Float Property PersonDistance = 250.0 Auto Const
{How close the player comes to a delivery point before its person is placed, as duo_delve_driver
places its civilians: a ref placed at a site the world has not loaded has no ground to stand on.}
Float Property PersonOffset = 1.5 Auto Const
{How far beside the delivery point the person stands, in metres.}

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
            Finish(player, StageOwner, ObjOwner, ObjBuyer, Beat2Message)
        ElseIf akSender == BuyerTarget.GetRef()
            Finish(player, StageBuyer, ObjBuyer, ObjOwner, Beat3Message)
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
    If OwnerPerson
        RegisterForDistanceLessThanEvent(Game.GetPlayer(), OwnerTarget, PersonDistance)
    EndIf
    If BuyerPerson
        RegisterForDistanceLessThanEvent(Game.GetPlayer(), BuyerTarget, PersonDistance)
    EndIf
    Beat = 2
EndFunction

Bool OwnerPlaced
Bool BuyerPlaced

; The first approach to each delivery point puts its person beside it. Once each.
Event OnDistanceLessThan(ObjectReference akObj1, ObjectReference akObj2, float afDistance, int aiEventID)
    ObjectReference owner = OwnerTarget.GetRef()
    ObjectReference buyer = BuyerTarget.GetRef()
    If !OwnerPlaced && (akObj1 == owner || akObj2 == owner)
        OwnerPlaced = True
        PlacePerson(owner, OwnerPerson)
    ElseIf !BuyerPlaced && (akObj1 == buyer || akObj2 == buyer)
        BuyerPlaced = True
        PlacePerson(buyer, BuyerPerson)
    EndIf
EndEvent

Function PlacePerson(ObjectReference target, ActorBase person)
    Float[] pos = new Float[6]
    pos[0] = PersonOffset
    pos[1] = 0
    pos[2] = 0
    target.PlaceAtMe(person, 1, False, False, True, pos, None, True)
EndFunction

; Beat 2 -> done, on whichever ending the player walked to. The other objective is HIDDEN, not failed:
; Jessica's rule is that neither side is the villain, so the road not taken must not read as a loss.
Function Finish(ObjectReference player, Int stage, Int chosen, Int other, Message m)
    Beat = 3
    player.RemoveItem(Item, 1, False, None)
    OwnerTarget.GetRef().BlockActivation(True, True)
    BuyerTarget.GetRef().BlockActivation(True, True)
    SetObjectiveDisplayed(other, False, False)
    SetObjectiveCompleted(chosen, True)
    ShowBeat(m)
    SetStage(stage)
    CompleteQuest()
    Stop()
EndFunction

Event OnQuestRejected()
    SetObjectiveDisplayed(ObjFind, False, False)
    Stop()
EndEvent
