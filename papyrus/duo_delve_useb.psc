Scriptname duo_delve_useb extends duo_delve_use
{ FrankyCLI, on a RETURN beat's object alias (a use the quest comes back to): duo_delve_use under a second name, because one alias cannot carry the same script twice. Our copy of DefaultAliasOnActivateA, which is the stock hook's one duplicate for the same reason (gen_delve, plan OT-93). }

; Nothing of its own: the parent's OnActivate runs on this instance with this instance's properties, so the first visit's
; duo_delve_use and the return's duo_delve_useb each check their own prereq and stage. A beat is returned to once, so one
; duplicate is enough (the lint refuses a second return).
