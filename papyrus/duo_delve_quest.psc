Scriptname duo_delve_quest extends Quest
{The ONE quest-level script a beats Delve carries, and it does one job: a TURNED-DOWN mission stops.
A beats Delve has no driver (his ruling 2026-10-08: the stages are the state machine), and the driver it
replaces (dou_artifact_ground_dualact_local_qst) was the only thing handling OnQuestRejected. Without it a
rejected Delve kept running, and a repeatable quest that is running cannot start again (his catch,
2026-10-14). It cannot live in the fragment script: a fragment script is not attached to the quest, so a
quest event has nowhere to land (the OnTimer lesson, manual Inbox 2026-10-12).
Vanilla's shape (SQ_TreasureMapQuestScript.HandleOnQuestRejected, every Overtime driver): hide the
objective, then Stop(). Objectives is every objective the build wrote, so nothing is left on screen
whichever step was showing. Written by gen_delve; source of truth in FrankyCLI/papyrus.}

Int[] Property Objectives Auto Const

Event OnQuestRejected()
    Int i = 0
    While i < Objectives.Length
        SetObjectiveDisplayed(Objectives[i], False, False)
        i += 1
    EndWhile
    Stop()
EndEvent
