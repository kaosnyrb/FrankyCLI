using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde;
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
    public class RoomUtils
    {
        public FormList roomlist;

        public Dictionary<string, FormList> roomTemplates;
        private readonly Dictionary<string, Dictionary<string, List<string>>> cachedCandidates = new(StringComparer.OrdinalIgnoreCase);

        public string listName;

        public RoomUtils(string listname) {
            roomlist = gen_quest_main.myMod.FormLists.FirstOrDefault(fl => fl.EditorID == listname);

            roomTemplates = new Dictionary<string, FormList>();
            foreach (var f in roomlist.Items)
            {
                ; var list = gen_quest_main.myMod.FormLists.FirstOrDefault(fl => fl.FormKey == f.FormKey);
                roomTemplates.Add(list.EditorID, list);
            }
            listName = listname;
            PrebuildCandidateCache();
        }

        public string GetRoom(string theme, string type = null)
        {
            var listKey = listName + "_" + theme;
            var typeKey = type ?? string.Empty;

            if (!roomTemplates.TryGetValue(listKey, out var formList) || formList?.Items == null || formList.Items.Count == 0)
                throw new Exception($"Room theme list not found or empty: {listKey}");

            if (!cachedCandidates.TryGetValue(listKey, out var typeMap))
            {
                typeMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                cachedCandidates[listKey] = typeMap;
            }

            if (!typeMap.TryGetValue(typeKey, out var candidates))
            {
                candidates = BuildCandidates(listKey, formList, type);
                typeMap[typeKey] = candidates;
            }

            if (candidates.Count == 0)
                throw new Exception($"No PackIns resolved for list: {listKey}");

            // Prefer rooms over blockers (simple heuristic: exclude blocker IDs first)
            var rooms = candidates
                .Where(id => id.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) < 0)
                .ToList();

            if (rooms.Count > 0)
                return rooms[RandomUtils.random.Next(rooms.Count)];

            // Fallback: if the list only contains blockers, return one of them
            return candidates[RandomUtils.random.Next(candidates.Count)];
        }

        private void EnsureConnectorsWithinBounds(string listKey, PackIn packIn)
        {
            var prefab = PrefabCache.GetPrefab(packIn.EditorID);
            var bounds = packIn.ObjectBounds;

            var minX = Math.Min(bounds.First.X, bounds.Second.X);
            var minY = Math.Min(bounds.First.Y, bounds.Second.Y);
            var minZ = Math.Min(bounds.First.Z, bounds.Second.Z);
            var maxX = Math.Max(bounds.First.X, bounds.Second.X);
            var maxY = Math.Max(bounds.First.Y, bounds.Second.Y);
            var maxZ = Math.Max(bounds.First.Z, bounds.Second.Z);

            const float edgeTolerance = 0.01f; // allow connectors sitting right on the boundary
            const float integerTolerance = 0.01f; // allow minor floating point drift from whole numbers

            foreach (var marker in prefab.Markers)
            {
                if (string.IsNullOrWhiteSpace(marker.MarkerEditorId) ||
                    !marker.MarkerEditorId.StartsWith("rg_conn_", StringComparison.OrdinalIgnoreCase))
                    continue;

                var pos = marker.Position;

                if (!IsAlmostInteger(pos.X, integerTolerance) ||
                    !IsAlmostInteger(pos.Y, integerTolerance) ||
                    !IsAlmostInteger(pos.Z, integerTolerance))
                {
                    //throw new Exception(
                    //    $"Connector marker '{marker.MarkerEditorId}' in prefab '{packIn.EditorID}' (list '{listKey}') must be on integer coordinates but is at ({pos.X:F2},{pos.Y:F2},{pos.Z:F2}).");
                }

                if (pos.X < minX - edgeTolerance || pos.X > maxX + edgeTolerance ||
                    pos.Y < minY - edgeTolerance || pos.Y > maxY + edgeTolerance ||
                    pos.Z < minZ - edgeTolerance || pos.Z > maxZ + edgeTolerance)
                {
                    /*
                    Console.WriteLine(
                        $"Connector marker '{marker.MarkerEditorId}' in prefab '{packIn.EditorID}' (list '{listKey}') is outside prefab bounds. " +
                        $"Position=({pos.X:F2},{pos.Y:F2},{pos.Z:F2}) BoundsMin=({minX:F2},{minY:F2},{minZ:F2}) BoundsMax=({maxX:F2},{maxY:F2},{maxZ:F2})");
                    */
                }
            }
        }

        private static bool IsAlmostInteger(float value, float tolerance)
        {
            return Math.Abs(value - MathF.Round(value)) <= tolerance;
        }

        private void PrebuildCandidateCache()
        {
            foreach (var kvp in roomTemplates)
            {
                var listKey = kvp.Key;
                var formList = kvp.Value;
                var typeMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                cachedCandidates[listKey] = typeMap;

                // Always include the default (no filter) bucket
                var typeKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { string.Empty };

                // Derive type tokens from editor IDs: raw tokens and underscore-wrapped variants (e.g. "_trk_")
                foreach (var item in formList.Items)
                {
                    if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn) || string.IsNullOrWhiteSpace(packIn?.EditorID))
                        continue;

                    var tokens = packIn.EditorID.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var token in tokens)
                    {
                        typeKeys.Add(token);
                        var wrapped = $"_{token}_";
                        if (packIn.EditorID.Contains(wrapped, StringComparison.OrdinalIgnoreCase))
                        {
                            typeKeys.Add(wrapped);
                        }
                    }
                }

                foreach (var typeKey in typeKeys)
                {
                    typeMap[typeKey] = BuildCandidates(listKey, formList, typeKey.Length == 0 ? null : typeKey);
                }
            }
        }

        private List<string> BuildCandidates(string listKey, FormList formList, string type)
        {
            var candidates = new List<string>(formList.Items.Count);

            foreach (var item in formList.Items)
            {
                // Defensive: skip unresolved keys
                if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn) || packIn?.EditorID == null)
                    continue;

                if (type != null)
                {
                    if (!packIn.EditorID.Contains(type))
                        continue;
                }

                EnsureConnectorsWithinBounds(listKey, packIn);

                candidates.Add(packIn.EditorID);
            }

            return candidates;
        }
    }
}
