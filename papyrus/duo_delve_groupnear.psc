Scriptname duo_delve_groupnear extends ReferenceAlias
{ FrankyCLI, on a clear beat's SPAWN marker alias. The step's fragment calls Arm; when the player comes within
  NearDistance of the marker ACROSS THE MAP, the enemies are placed there into akGroup by duo_delve_lib.SpawnGroup (the
  call the fragment itself made until 2026-10-21), and aiSpawnedStage is set: the objective, which has pointed at this
  marker until now (its target lit while that stage is NOT done), points at them alone. His play the same day: "marker
  spawning works, but marker stays as a visable objective", when the marker was a plain second target.

  WHY: placed when the step began, a far site's enemies stood over ground that was not loaded yet. His play of
  duo_delve33a, 2026-10-21: "The spacers are spawned from a distance, which reads bad on the map and puts them in mid
  air. Spawn them when the player gets close". The same fault the recover holder had on 10-20 (duo_delve_spawnnear):
  spawning near the player means spawning on loaded ground.

  ⛔ FLAT DISTANCE, NOT 3D, as duo_delve_spawnnear and duo_delve_landing: the marker's height over unloaded ground means
  nothing, so a timer compares x and y only, and only while the player is in the marker's worldspace. Unlike the
  holder's spot this marker is a real one of the place, so once he is near, its ground is loaded and its height is
  true. NearDistance is the holder's 250, which put him on loaded ground at 05a. }

Float NearDistance = 250.0 Const
Float CheckEvery = 1.0 Const

RefCollectionAlias enemies   ; not "group": a reserved word since Starfield's Group blocks
FormList list
Int countMin
Int countMax
Bool oneSpecies
FormList loot
Int lootChance
Int spawnedStage
WorldSpace world
Int pods     ; 0 = the enemies appear (SpawnGroup); above 0 they arrive by drop pod, perPod out of each
Int perPod

; Drop pods (2026-10-21, his "3 per pod and 3 pods in different points"): set by the step's fragment BEFORE Arm, a
; function of its own rather than new Arm parameters, so every clear built before it still calls an Arm it matches.
Function SetPods(Int aiPods, Int aiPer)
    pods = aiPods
    perPod = aiPer
EndFunction

Function Arm(RefCollectionAlias akGroup, FormList akList, Int aiMin, Int aiMax, Bool abOneSpecies, FormList akLoot, Int aiChance, Int aiSpawnedStage)
    enemies = akGroup
    list = akList
    countMin = aiMin
    countMax = aiMax
    oneSpecies = abOneSpecies
    loot = akLoot
    lootChance = aiChance
    spawnedStage = aiSpawnedStage
    world = GetRef().GetWorldSpace()
    Debug.Trace(Self + " [groupnear] armed on " + GetRef() + " in " + world + ", near " + NearDistance)
    StartTimer(CheckEvery)
EndFunction

Event OnTimer(int aiTimerID)
    if !GetOwningQuest().IsRunning()
      return
    endif
    ObjectReference here = GetRef()
    if !world
      world = here.GetWorldSpace()
    endif
    Actor player = Game.GetPlayer()
    Float dx = player.GetPositionX() - here.GetPositionX()
    Float dy = player.GetPositionY() - here.GetPositionY()
    if !world || player.GetWorldSpace() != world || dx * dx + dy * dy > NearDistance * NearDistance
      StartTimer(CheckEvery)
      return
    endif
    If pods > 0
        Debug.Trace(Self + " [groupnear] " + pods + " drop pods of " + perPod + " round " + here)
        duo_delve_lib.DropPods(here, pods, perPod, enemies, list, oneSpecies, loot, lootChance)
    Else
        duo_delve_lib.SpawnGroup(Self, enemies, list, countMin, countMax, oneSpecies, loot, lootChance)
    EndIf
    GetOwningQuest().SetStage(spawnedStage)
    Debug.Trace(Self + " [groupnear] spawned " + enemies.GetCount() + " at " + here + ", the player " + Math.sqrt(dx * dx + dy * dy) + " away; stage " + spawnedStage + " set")
EndEvent
