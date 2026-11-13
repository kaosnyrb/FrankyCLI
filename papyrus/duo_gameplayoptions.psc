Scriptname duo_gameplayoptions extends Quest
{Overtime's gameplay options, mirrored into globals that Story Manager conditions read.
Vanilla's own shape (PEO:SQ_PEO_QuestScript): a node cannot read an option, so a quest copies each
option's value into a global and the conditions read the global. Attached to duo_GameplayOptionsQuest
(start game enabled) by gen_delve legacy, which writes both properties.

Classic Missions: 0 = off (the Delves only), non-zero = on (the original missions are mixed back in,
through each pool's _Legacy node).}

GameplayOption Property ClassicMissionsOption Auto Const Mandatory
{The Classic Missions slider in the settings menu.}
GlobalVariable Property ClassicMissionsGlobal Auto Const Mandatory
{duo_ClassicMissions: 1 when the option is on, 0 when off. Read by every _Legacy node.}

GameplayOption Property CommsSatOption Auto Const Mandatory
{The Comms Satellite Chance slider, 0 to 100 in steps of 10 (gen_delve settings, his 2026-11-13).}
GlobalVariable Property CommsSatGlobal Auto Const Mandatory
{duo_CommsSatChance: the percent a space Delve's object places an installation when it loads.}
GameplayOption Property ShipSlateOption Auto Const Mandatory
{The Dataslate Drop Chance slider, 0 to 100 in steps of 10.}
GlobalVariable Property ShipSlateGlobal Auto Const Mandatory
{duo_ShipSlateChance: the percent a hostile ship a Delve spawns carries a remote dataslate.}

Event OnQuestInit()
    ; Register first so a change made while we sync is not missed, then sync once: an option set at
    ; the start menu, or before this quest first ran on an existing save, never fires a change event.
    RegisterForGameplayOptionChangedEvent()
    Sync()
EndEvent

Event OnGameplayOptionChanged(GameplayOption[] aChangedOptions)
    Sync()
EndEvent

Function Sync()
    if ClassicMissionsOption.GetValue() != 0
        ClassicMissionsGlobal.SetValue(1)
    else
        ClassicMissionsGlobal.SetValue(0)
    endif
    ; The two chances are percents: the slider's value is the global's.
    CommsSatGlobal.SetValue(CommsSatOption.GetValue())
    ShipSlateGlobal.SetValue(ShipSlateOption.GetValue())
EndFunction
