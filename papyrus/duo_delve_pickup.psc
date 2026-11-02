Scriptname duo_delve_pickup extends duo_delve_use
{ FrankyCLI, on a pickup beat's object alias: when the object is activated, once PrereqStage is done, hides its prompt, gives ItemToGive to whoever activated it, and sets StageToSet. Our copy of DefaultAliasOnActivateGiveItem (gen_delve, plan OT-93). }

; WHY (his 2026-10-30 "make a ticket to move the other three over", after OT-91c proved duo_delve_use): the stock
; DefaultAliasOnActivateGiveItem gives its item only after SafeSetStage returns, which waits out the stage's fragment (a
; hold's wave can hold it 180 s), so on a re-dealt board quest the item can arrive in the wrong run. Here the object is
; hidden and the item given BEFORE the stage is set, in duo_delve_use's BeforeStage, and the parent's OnActivate keeps
; its once-guard (the stage) and its "[use]" traces.
;
; One item, to the activating reference, as the stock hook's defaults (AmountToGive 1, not silent). ⚠ The stock script's
; SOURCE (Data/Scripts/Source, 2026-11-02) adds the item only through ReferenceAliasToAddTo or a collection, which a
; Delve never set, yet every Delve pickup has given its item in play: the shipped .pex and that source disagree. This
; copy does not depend on either: it calls AddItem itself.

Form Property ItemToGive Auto Const Mandatory

Function BeforeStage(ObjectReference akActionRef)
    if akActionRef
        akActionRef.AddItem(ItemToGive, 1)
    endif
    Debug.Trace(Self + " [pickup] gave " + ItemToGive + " to " + akActionRef)
EndFunction
