Scriptname duo_delve_snaponload extends ReferenceAlias
{ FrankyCLI, on a recover beat's HOLDER alias when the recipe sets outside: [min, max].

  SpawnHolderOutside places the holder far out on open ground, 59 up, and asks PlaceAtMe to snap him to navmesh.
  Out past the loaded ground there is no navmesh to find, so he waits in the air, and when the player comes close
  and his area loads he falls. His 2026-10-20 play of 05a at 750 to 2000: "marker spawned a good distance away but
  when I got close he fell out of the sky and died". 150 to 300 had played fine the day before.

  So the drop happens when his 3D loads, onto the nearest navmesh, which is vanilla's own call for putting an
  actor on the ground (UCR05QuestScript.SpawnTarget). It runs on every load, which is harmless once he is standing
  on navmesh. UNPROVEN until played: that this runs before the fall does. }

Event OnLoad()
    ObjectReference holder = GetReference()
    If holder
        holder.MoveToNearestNavmeshLocation()
    EndIf
EndEvent
