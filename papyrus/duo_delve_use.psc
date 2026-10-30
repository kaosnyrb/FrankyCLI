Scriptname duo_delve_use extends ReferenceAlias
{ FrankyCLI, on a use or deliver beat's object alias: when the object is activated, once PrereqStage is done, hides its prompt and sets StageToSet. Our copy of DefaultAliasOnActivate (gen_delve, plan OT-91). }

; WHY OUR OWN COPY, his "yeah make our own activate hook", 2026-10-30, after Runaway Plant's second run on one save had a
; console with no prompt that a forced activate could not move. The stock hook calls SafeSetStage, which waits for the
; stage's fragment to finish (measured 2026-10-26, remote_planet_qst21a's canister), and a hold's wave fragment can sleep
; 180 s; only THEN does it hide the prompt, on GetReference(), which is whatever the alias holds by then. A board deals the
; SAME quest again, so by then the alias can hold the NEXT run's console: the last run's hook hides the new one.
; So here the console is captured and hidden BEFORE the stage is set, and nothing after the set touches a reference.
;
; No Busy state and no Done state: the stock hook's are per script instance, which a re-dealt quest keeps. The one guard
; this needs is the stage itself (GetStageDone(StageToSet)), which a new run resets.
;
; Every decision is traced to the Papyrus log under "[use]" with Debug.Trace, which a release build keeps. The stock
; DefaultScriptFunctions.Trace is DebugOnly and stripped, so the stock hook can never say what it did.

Int Property StageToSet = -1 Auto Const Mandatory
Int Property PrereqStage = -1 Auto Const
Bool Property HideAfterUse = true Auto Const

Event OnAliasInit()
    ObjectReference me = GetReference()
    if me
        Debug.Trace(Self + " [use] filled with " + me + ", activation blocked " + me.IsActivationBlocked() + "; sets " + StageToSet + " after " + PrereqStage)
    else
        Debug.Trace(Self + " [use] filled with NONE; sets " + StageToSet + " after " + PrereqStage)
    endif
EndEvent

Event OnLoad()
    ObjectReference me = GetReference()
    if me
        Debug.Trace(Self + " [use] " + me + " loaded, activation blocked " + me.IsActivationBlocked())
    endif
EndEvent

Event OnActivate(ObjectReference akActionRef)
    ObjectReference me = GetReference()
    Quest q = GetOwningQuest()
    bool prereqDone = PrereqStage < 0 || q.GetStageDone(PrereqStage)
    bool alreadyDone = q.GetStageDone(StageToSet)
    Debug.Trace(Self + " [use] " + me + " activated by " + akActionRef + "; prereq " + PrereqStage + " done " + prereqDone + ", stage " + StageToSet + " done " + alreadyDone)
    if alreadyDone || !prereqDone
        return
    endif
    if HideAfterUse && me
        me.BlockActivation(true, true)
    endif
    q.SetStage(StageToSet)
    Debug.Trace(Self + " [use] " + me + " set stage " + StageToSet)
EndEvent
