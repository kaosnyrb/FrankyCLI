Scriptname duo_delve_bring extends ReferenceAlias
{ FrankyCLI, on the player's alias in a Delve with a "bring" deliver: keeps the objective's counter at how many of Item the player carries, at most Total, while the step shows. Plan OT-31. }

; VANILLA'S RED TAPE SHAPE (CityCYRedTape01PlayerScript + CityCYRedTape01QuestScript.ResourceCheck): an inventory
; event filter on the item, a recount on every add and every remove, the count into a global the objective's text
; shows as <Global=...>, UpdateCurrentInstanceGlobal, and the objective redisplayed so the new number shows.
;
; It counts only while the step shows (ArmStage done, DoneStage not), read off the stages every time, so nothing is
; stored here and a re-dealt board quest starts clean. Begin() is called by the step's own fragment, so the first
; number does not wait for an item to move; it does not redisplay, because the fragment shows the objective next.
; The handover itself is duo_delve_bring_use on the object. Every count is traced under "[bring]".

Form Property Item Auto Const Mandatory
Int Property Total Auto Const Mandatory
GlobalVariable Property Counter Auto Const Mandatory
Int Property Objective Auto Const Mandatory
Int Property ArmStage Auto Const Mandatory
Int Property DoneStage Auto Const Mandatory

Event OnAliasInit()
    AddInventoryEventFilter(Item)
EndEvent

Function Begin()
    Recount(True)
EndFunction

Event OnItemAdded(Form akBaseItem, int aiItemCount, ObjectReference akItemReference, ObjectReference akSourceContainer, int aiTransferReason)
    Recount(False)
EndEvent

Event OnItemRemoved(Form akBaseItem, int aiItemCount, ObjectReference akItemReference, ObjectReference akDestContainer, int aiTransferReason)
    Recount(False)
EndEvent

Function Recount(bool abBegin)
    Quest q = GetOwningQuest()
    if !abBegin && (!q.GetStageDone(ArmStage) || q.GetStageDone(DoneStage))
        return
    endif
    Int have = Game.GetPlayer().GetItemCount(Item)
    Int shown = have
    if shown > Total
        shown = Total
    endif
    Counter.SetValue(shown as float)
    q.UpdateCurrentInstanceGlobal(Counter)
    if !abBegin
        q.SetObjectiveDisplayed(Objective, True, True)
    endif
    Debug.Trace(Self + " [bring] holding " + have + " of " + Total + " " + Item + ", showing " + shown + ", begin " + abBegin)
EndFunction
