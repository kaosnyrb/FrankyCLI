Scriptname duo_delve_give extends TopicInfo Const
{ FrankyCLI, on the player's line of a talk exchange with "give": takes ItemCount of Item from the player when the line ends. }

; WHY OUR OWN COPY OF DefaultTopicRemoveItem, his "own copy", 2026-11-09 (plan OT-34): the stock script is the right
; shape (a TopicInfo script on the player's [Give ...] line, removing on OnEnd; vanilla's lemonade stand, SE_FAB25),
; but its item property is typed MiscObject, so it can hold credits and cannot hold a med pack (Aid_MedPack is a
; Potion). This is the stock script with the property typed Form, so any item a player carries can be given.
; Vanilla's other route, a stage whose fragment calls RemoveItem (OE_PredatorsVsGuard stage 185), needs a stage per
; give line; this works on any exchange, whether or not it moves the quest.
; Only the OnEnd half is copied: nothing of ours removes on OnBegin, so that switch would be dead.
; The line is offered only while the player carries ItemCount (a GetItemCount condition on the same line, written by
; gen_delve), so RemoveItem here does not run short.

Form Property Item Auto Const Mandatory
Int Property ItemCount = 1 Auto Const

Event OnEnd(ObjectReference akSpeakerRef, bool abHasBeenSaid)
    Game.GetPlayer().RemoveItem(Item, ItemCount)
EndEvent
