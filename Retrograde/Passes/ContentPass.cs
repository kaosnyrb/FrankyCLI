using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde.Passes;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde
{
    public struct MarkerSlot
    {
        public string SlotId;     // MarkerEditorId (non-connector)
        public P3Float LocalPos;  // marker.Position (local)
        public P3Float LocalRot;  // marker.Rotation (local) - optional if available
    }

    public class ContentPass : IGenPass
    {
        private readonly Dictionary<string, FormList> _slotListsCache = new(StringComparer.OrdinalIgnoreCase);

        public string enemyType = "";

        private static string StripNumericSuffix(string id)
        {
            int lastUnderscore = id.LastIndexOf('_');
            if (lastUnderscore <= 0)
                return id;

            return id.Substring(0, lastUnderscore);
        }

        private FormList FindSlotList(string slotId)
        {
            if (string.IsNullOrWhiteSpace(slotId))
                return null;

            // Normalize marker ID:
            // rg_slot_crate_large_02 -> rg_slot_crate_large
            var normalizedId = StripNumericSuffix(slotId);

            if (_slotListsCache.TryGetValue(normalizedId, out var cached))
                return cached;

            Mutagen.Bethesda.Starfield.FormList found = null;

            foreach (var fl in gen_quest_main.myMod.FormLists)
            {
                if (string.Equals(fl.EditorID, normalizedId, StringComparison.OrdinalIgnoreCase))
                {
                    found = fl;
                    break;
                }
            }

            _slotListsCache[normalizedId] = found;
            return found;
        }

        private string PickRandomPackInEditorIdFromFormList(FormList list)
        {
            if (list?.Items == null || list.Items.Count == 0)
                return null;

            var item = list.Items[RandomUtils.random.Next(list.Items.Count)];


            if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn) || packIn?.EditorID == null)
                return null;

            return packIn.EditorID;
            
        }

        public void RunPass(DungeonState state)
        {
            foreach (var placed in state.placedRooms)
            {
                // Iterate all markers that are NOT connectors
                foreach (var marker in placed.Prefab.Markers)
                {
                    var id = marker.MarkerEditorId;

                    if (string.IsNullOrWhiteSpace(id))
                        continue;

                    // Skip connectors
                    if (id.StartsWith("rg_conn_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Optional: only consider rg_ markers
                    if (!id.StartsWith("rg_", StringComparison.OrdinalIgnoreCase))
                        continue;

                    //TODO Extract enemy logic
                    if (id.StartsWith("rg_enemy_spawn", StringComparison.OrdinalIgnoreCase))
                    {
                        //If it's an enemy then we need to choose a faction.
                        if (enemyType == "")
                        {
                            enemyType = "spacer";
                        }
                        id = "rg_enemy_spawn_" + enemyType + "_001";
                    }
                    else
                    {
                        //Culling -- We don't want EVERY marker filled, do 50%
                        if (RandomUtils.random.Next(10) < 5)
                            continue;
                    }


                    // Look up slot list (FormList EditorID == marker EditorID)
                    var list = FindSlotList(id);
                    if (list == null)
                        continue;

                    var contentPackInId = PickRandomPackInEditorIdFromFormList(list);
                    if (contentPackInId == null)
                        continue;

                    var contentPrefab = new RoomPrefab(contentPackInId);

                    // Compute world position for marker:
                    // world = roomWorld + rotate(markerLocal, roomYaw)
                    var rotatedLocal = RgRotation.RotateYaw90(marker.Position, placed.YawSteps);
                    var worldPos = placed.WorldPos + rotatedLocal;

                    // Compute final rotation:
                    // combine marker's local rotation with the room's yaw
                    var worldRot = marker.Rotation + RgRotation.RotationToP3Float(placed.YawSteps);

                    state.instance.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = worldRot,
                        Position = worldPos,
                        Base = contentPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                    });
                }
            }
        }
    }
}
