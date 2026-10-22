Scriptname duo_delve_near extends ReferenceAlias
{ FrankyCLI, on a fight beat's marker alias: sets StageToSet when the player (TargetAlias) comes within TargetDistance,
  once PrereqStage is done. The stage's fragment then spawns the fight (gen_delve FightBeat.Enter).

  WHY OUR OWN COPY OF DefaultAliasOnDistanceLessThan, his "A", 2026-10-22: an alias carries a script once, and a board
  Delve's step-one marker already wears the stock one for the arrival call. A second stock copy for the spawn could not
  live beside it (missionboard01a's verify caught it), and vanilla ships no lettered duplicate of this script as it does
  for DefaultAliasOnItemAddedScript. So every fight's spawn uses this name, and it can never collide with a stock hook.

  WHY THE ENGINE'S EVENT, his 2026-10-22 on duo_delve_groupnear's once-a-second timer: "that won't scale very well".
  RegisterForDistanceLessThanEvent costs nothing until it fires. ⚠ It measures 3D; the marker is a real one of the
  place, so its height is true. ⚠ Unmeasured: whether it fires at once if he is already inside the distance when it
  registers (ScriptObject.psc does not say). }

ReferenceAlias Property TargetAlias Auto Const Mandatory
Float Property TargetDistance Auto Const Mandatory
Int Property StageToSet = -1 Auto Const Mandatory
Int Property PrereqStage = -1 Auto Const

Event OnAliasInit()
    Quest q = GetOwningQuest()
    if PrereqStage > -1 && !q.GetStageDone(PrereqStage)
        RegisterForRemoteEvent(q, "OnStageSet")
    else
        RegisterForDistanceLessThanEvent(Self, TargetAlias, TargetDistance)
    endif
EndEvent

Event Quest.OnStageSet(Quest akSender, int auiStageID, int auiItemID)
    if auiStageID == PrereqStage
        RegisterForDistanceLessThanEvent(Self, TargetAlias, TargetDistance)
        UnregisterForRemoteEvent(GetOwningQuest(), "OnStageSet")
    endif
EndEvent

Event OnDistanceLessThan(ObjectReference akObj1, ObjectReference akObj2, float afDistance, int aiEventID)
    Debug.Trace(Self + " [near] " + akObj1 + " within " + afDistance + " of " + akObj2 + "; stage " + StageToSet)
    if StageToSet > -1
        GetOwningQuest().SetStage(StageToSet)
    endif
EndEvent
