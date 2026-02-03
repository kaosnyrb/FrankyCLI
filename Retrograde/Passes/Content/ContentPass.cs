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

        private bool ShouldCullMarker(PlacedRoom room)
        {
            var cullChance = GetCullChance(room.DistrictType);
            return RandomUtils.random.NextDouble() < cullChance;
        }

        private static bool IsNearDuplicate(
            string packInId,
            P3Float position,
            Dictionary<string, List<P3Float>> placedByPackIn,
            float minDistance)
        {
            if (!placedByPackIn.TryGetValue(packInId, out var positions) || positions == null)
                return false;

            float thresholdSq = minDistance * minDistance;
            for (int i = 0; i < positions.Count; i++)
            {
                if (MathUtil.DistanceSquared(positions[i], position) <= thresholdSq)
                    return true;
            }

            return false;
        }

        private static void RecordPlacement(
            string packInId,
            P3Float position,
            Dictionary<string, List<P3Float>> placedByPackIn)
        {
            if (!placedByPackIn.TryGetValue(packInId, out var positions) || positions == null)
            {
                positions = new List<P3Float>();
                placedByPackIn[packInId] = positions;
            }

            positions.Add(position);
        }


        private static double GetCullChance(string districtType)
        {
            if (string.IsNullOrWhiteSpace(districtType))
                return 0.3; // preserve previous default

            if (districtType.IndexOf("boss", StringComparison.OrdinalIgnoreCase) >= 0)
                return 0.1; // keep boss rooms packed

            if (districtType.IndexOf("util", StringComparison.OrdinalIgnoreCase) >= 0)
                return 0.55; // utility areas stay sparse

            if (districtType.IndexOf("living", StringComparison.OrdinalIgnoreCase) >= 0)
                return 0.1; // more clutter in living spaces

            if (districtType.IndexOf("trunk", StringComparison.OrdinalIgnoreCase) >= 0)
                return 0; // corridors have all dressing

            return 0.3;
        }

        public void RunPass(DungeonState state)
        {
            const float duplicateSpacing = 30f;
            var placedByPackIn = new Dictionary<string, List<P3Float>>(StringComparer.OrdinalIgnoreCase);
            int itemsPlaced = 0;
            int markersCulled = 0;
            int markersSkipped = 0;

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

                    //TODO enemies are handled in the enemy pass logic
                    if (id.StartsWith("rg_enemy_spawn", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    else
                    {
                        //Culling tuned by district
                        if (ShouldCullMarker(placed))
                        {
                            markersCulled++;
                            continue;
                        }
                    }


                    // Look up slot list (FormList EditorID == marker EditorID)
                    var list = FindSlotList(id);
                    if (list == null)
                        continue;

                    var contentPackInId = PickRandomPackInEditorIdFromFormList(list);
                    if (contentPackInId == null)
                        continue;

                    var contentPrefab = PrefabCache.GetPrefab(contentPackInId);

                    // Compute world position for marker:
                    // world = roomWorld + rotate(markerLocal, roomYaw)
                    var rotatedLocal = RgRotation.RotateYaw90(marker.Position, placed.YawSteps);
                    var worldPos = placed.WorldPos + rotatedLocal;
                    if (IsNearDuplicate(contentPackInId, worldPos, placedByPackIn, duplicateSpacing))
                    {
                        markersSkipped++;
                        continue;
                    }

                    // Compute final rotation:
                    // combine marker's local rotation with the room's yaw
                    var worldRot = marker.Rotation + RgRotation.RotationToP3Float(placed.YawSteps);

                    state.PlacementUtil.AddToTemporary(state.instance, new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = worldRot,
                        Position = worldPos,
                        Base = contentPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                    });

                    RecordPlacement(contentPackInId, worldPos, placedByPackIn);
                    itemsPlaced++;
                }
            }

            if (!state.IsHarnessRun)
                Console.WriteLine($"[Content] Placed {itemsPlaced} items, {markersCulled} culled, {markersSkipped} duplicates skipped");
        }
    }
}
