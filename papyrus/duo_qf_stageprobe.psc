Scriptname duo_qf_stageprobe Extends Quest Hidden Const
{THE STAGE PROBE: a quest with NO driver. The stage graph is the state machine (his ruling,
2026-10-08: "The stages of the quest are the state machine. All of vanilla Starfield was built this
way."). Stock DefaultAliasOnActivate hooks set the stages; this fragment script does the one half
that is Papyrus-only, the objectives. Same shape as vanilla's Fragments:Quests:QF_* scripts, bound
per stage by gen_stageprobe. A SPIKE: graduates into gen_delve or is deleted, see home-office
office/projects/delves/stages-not-a-driver.md.}

; Stage 0 runs on start (RunOnStart): show the first objective.
Function Fragment_Stage_0000_Item_00()
    SetObjectiveDisplayed(10)
EndFunction

; Stage 50: the first object was activated. Close 10, show 20.
Function Fragment_Stage_0050_Item_00()
    SetObjectiveCompleted(10)
    SetObjectiveDisplayed(20)
EndFunction

; Stage 100: the second object was activated. Close 20 and end, as vanilla's treasure-map fragment does.
Function Fragment_Stage_0100_Item_00()
    SetObjectiveCompleted(20)
    CompleteQuest()
    Stop()
EndFunction
