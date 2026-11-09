Scriptname duo_delve_bring_use extends duo_delve_use
{ FrankyCLI, on a deliver beat with "bring": with ItemCount of Item on the player it takes them and sets the stage; short, it shows ShortMessage and waits. Plan OT-31. }

; WHY THIS SHAPE: his own drop-off (ccs_planet_activator_act_delivery) and vanilla's Red Tape deposit bin
; (CityCYRedTape01DepositBinScript) both check GetItemCount >= the count on activate and then RemoveItem; his shows a
; FailMessage when short. Not DefaultAliasOnActivateRemoveItems: its parent sets the stage FIRST and checks after
; (gen_delve object.cs), so it would complete the beat empty-handed.
;
; The count is checked HERE, before duo_delve_use hides the prompt, so a player who is short keeps the prompt and can
; come back. The items go in BeforeStage, which duo_delve_use calls after the prompt is hidden and before the stage is
; set, so nothing after the set touches a reference (OT-91). Every decision is traced under "[bring]".
;
; Counts the player's own inventory only, as both precedents do: what sits in the ship's hold does not count.

Form Property Item Auto Const Mandatory
Int Property ItemCount = 1 Auto Const
Message Property ShortMessage Auto Const Mandatory

Event OnActivate(ObjectReference akActionRef)
    Quest q = GetOwningQuest()
    Actor player = Game.GetPlayer()
    bool live = akActionRef == player && (PrereqStage < 0 || q.GetStageDone(PrereqStage)) && !q.GetStageDone(StageToSet)
    if live
        Int have = player.GetItemCount(Item)
        Debug.Trace(Self + " [bring] activated holding " + have + " of " + ItemCount + " " + Item)
        if have < ItemCount
            ShortMessage.Show()
            return
        endif
    endif
    Parent.OnActivate(akActionRef)
EndEvent

Function BeforeStage(ObjectReference akActionRef)
    Game.GetPlayer().RemoveItem(Item, ItemCount)
    Debug.Trace(Self + " [bring] took " + ItemCount + " " + Item)
EndFunction
