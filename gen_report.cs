using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    public class gen_report
    {
        public static int Generate(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("Usage: gen_report <modname> gen_report <prefix> <item> <form>");
                Console.WriteLine("  Generates a room usage report for the target mod.");
                return 1;
            }

            string modname = args[0];

            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                ModKey newMod = new ModKey(modname, ModType.Master);
                if (!env.LoadOrder.ModExists(newMod))
                {
                    Console.WriteLine($"Mod '{modname}.esm' not found in load order.");
                    return 1;
                }

                // Load existing mod from disk
                StarfieldMod mod = null;
                for (int i = 0; i < env.LoadOrder.Count; i++)
                {
                    if (env.LoadOrder[i].FileName == modname + ".esm")
                    {
                        ModPath modPath = Path.Combine(env.DataFolderPath, env.LoadOrder[i].FileName);
                        mod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield);
                    }
                }

                if (mod == null)
                {
                    Console.WriteLine($"Failed to load mod '{modname}.esm'.");
                    return 1;
                }

                // --- 1. Collect all available room PackIns from FormLists ---
                var roomListNames = new List<string> { "rg_trunklist", "rg_bosslist", "rg_hablist", "rg_orelist", "rg_utillist", "rg_lootroom" };

                // Map: PackIn EditorID -> which list(s) it belongs to
                var allAvailableRooms = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                // Map: sub-list name -> list of PackIn EditorIDs
                var subListRooms = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

                foreach (var listName in roomListNames)
                {
                    var topList = mod.FormLists.FirstOrDefault(fl => fl.EditorID == listName);
                    if (topList == null)
                        continue;

                    foreach (var subItem in topList.Items)
                    {
                        var subList = mod.FormLists.FirstOrDefault(fl => fl.FormKey == subItem.FormKey);
                        if (subList == null)
                            continue;

                        var subListId = subList.EditorID ?? subItem.FormKey.ToString();
                        if (!subListRooms.ContainsKey(subListId))
                            subListRooms[subListId] = new List<string>();

                        foreach (var packInItem in subList.Items)
                        {
                            if (!mod.PackIns.TryGetValue(packInItem.FormKey, out var packIn) || string.IsNullOrWhiteSpace(packIn?.EditorID))
                                continue;

                            var eid = packIn.EditorID;
                            subListRooms[subListId].Add(eid);

                            if (!allAvailableRooms.ContainsKey(eid))
                                allAvailableRooms[eid] = new List<string>();
                            if (!allAvailableRooms[eid].Contains(subListId))
                                allAvailableRooms[eid].Add(subListId);
                        }
                    }
                }

                // --- 2. Scan all cells for placed PackIns ---
                // Collect all PackIn FormKeys for quick lookup
                var packInFormKeys = new Dictionary<FormKey, string>();
                foreach (var packIn in mod.PackIns)
                {
                    if (!string.IsNullOrWhiteSpace(packIn.EditorID))
                        packInFormKeys[packIn.FormKey] = packIn.EditorID;
                }

                // Count placements: PackIn EditorID -> (count, list of cell EditorIDs)
                var placedCounts = new Dictionary<string, (int Count, List<string> Cells)>(StringComparer.OrdinalIgnoreCase);

                for (int i = 0; i < mod.Cells.Count; i++)
                {
                    for (int j = 0; j < mod.Cells[i].SubBlocks.Count; j++)
                    {
                        for (int k = 0; k < mod.Cells[i].SubBlocks[j].Cells.Count; k++)
                        {
                            var cell = mod.Cells[i].SubBlocks[j].Cells[k];
                            var cellName = cell.EditorID ?? cell.FormKey.ToString();

                            // Check Temporary placed objects
                            foreach (var placed in cell.Temporary)
                            {
                                if (placed is PlacedObject po)
                                {
                                    CheckPlacedObject(po, cellName, packInFormKeys, placedCounts);
                                }
                            }

                            // Check Persistent placed objects
                            foreach (var placed in cell.Persistent)
                            {
                                if (placed is PlacedObject po)
                                {
                                    CheckPlacedObject(po, cellName, packInFormKeys, placedCounts);
                                }
                            }
                        }
                    }
                }

                // --- 3. Print report ---
                Console.WriteLine("=== Room Usage Report ===");
                Console.WriteLine($"Mod: {modname}.esm");
                Console.WriteLine($"Total available room PackIns: {allAvailableRooms.Count}");
                Console.WriteLine();

                // Placed rooms
                var placedRoomIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                Console.WriteLine("--- Placed Rooms ---");

                // Sort by sub-list, then by EditorID
                var placedByList = new Dictionary<string, List<(string EditorId, int Count)>>(StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in placedCounts)
                {
                    var editorId = kvp.Key;
                    var count = kvp.Value.Count;
                    if (!allAvailableRooms.ContainsKey(editorId))
                        continue;

                    placedRoomIds.Add(editorId);
                    foreach (var listName in allAvailableRooms[editorId])
                    {
                        if (!placedByList.ContainsKey(listName))
                            placedByList[listName] = new List<(string, int)>();
                        placedByList[listName].Add((editorId, count));
                    }
                }

                foreach (var listKvp in placedByList.OrderBy(x => x.Key))
                {
                    Console.WriteLine($"  [{listKvp.Key}]");
                    foreach (var room in listKvp.Value.OrderByDescending(r => r.Count))
                    {
                        Console.WriteLine($"    {room.EditorId} x{room.Count}");
                    }
                }

                Console.WriteLine();
                Console.WriteLine($"Total placed room types: {placedRoomIds.Count}/{allAvailableRooms.Count}");
                Console.WriteLine();

                // Build connector profile from placed rooms for compatibility checks
                var placedConnectorProfiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                float placedAvgVolume = 0f;
                int placedVolumeCount = 0;
                foreach (var placedId in placedRoomIds)
                {
                    var pi = mod.PackIns.FirstOrDefault(p => string.Equals(p.EditorID, placedId, StringComparison.OrdinalIgnoreCase));
                    if (pi == null) continue;

                    // Collect bounding box volume
                    if (pi.ObjectBounds != null)
                    {
                        var bMin = pi.ObjectBounds.First;
                        var bMax = pi.ObjectBounds.Second;
                        float vol = Math.Abs(bMax.X - bMin.X) * Math.Abs(bMax.Y - bMin.Y) * Math.Abs(bMax.Z - bMin.Z);
                        placedAvgVolume += vol;
                        placedVolumeCount++;
                    }

                    // Collect connector DoorSize/Tileset profiles
                    var piCell = FindCellByFormKey(mod, pi.Cell.FormKey);
                    if (piCell == null) continue;
                    foreach (var entry in piCell.Temporary)
                    {
                        if (entry is PlacedObject po && !string.IsNullOrEmpty(po.EditorID)
                            && po.EditorID.StartsWith("rg_conn_", StringComparison.OrdinalIgnoreCase))
                        {
                            var parsed = RgConnectorParser.Parse(po.EditorID);
                            if (parsed.IsValid)
                                placedConnectorProfiles.Add($"{parsed.DoorSize}|{parsed.Tileset}");
                        }
                    }
                    foreach (var entry in piCell.Persistent)
                    {
                        if (entry is PlacedObject po && !string.IsNullOrEmpty(po.EditorID)
                            && po.EditorID.StartsWith("rg_conn_", StringComparison.OrdinalIgnoreCase))
                        {
                            var parsed = RgConnectorParser.Parse(po.EditorID);
                            if (parsed.IsValid)
                                placedConnectorProfiles.Add($"{parsed.DoorSize}|{parsed.Tileset}");
                        }
                    }
                }
                if (placedVolumeCount > 0)
                    placedAvgVolume /= placedVolumeCount;

                // Unplaced rooms with reasons
                var unplacedRooms = allAvailableRooms.Keys
                    .Where(id => !placedRoomIds.Contains(id))
                    .OrderBy(id => id)
                    .ToList();

                if (unplacedRooms.Count > 0)
                {
                    Console.WriteLine("--- Unplaced Rooms ---");
                    foreach (var roomId in unplacedRooms)
                    {
                        var lists = allAvailableRooms[roomId];
                        var reason = DiagnoseUnplacedRoom(roomId, lists, mod, placedConnectorProfiles, placedAvgVolume);
                        Console.WriteLine($"  {roomId}");
                        Console.WriteLine($"    Lists: {string.Join(", ", lists)}");
                        Console.WriteLine($"    Reason: {reason}");
                    }
                }
                else
                {
                    Console.WriteLine("All available rooms have been placed at least once.");
                }

                // Also report any placed PackIns that are NOT in the room lists (non-room placements)
                var nonRoomPlacements = placedCounts
                    .Where(kvp => !allAvailableRooms.ContainsKey(kvp.Key))
                    .OrderBy(kvp => kvp.Key)
                    .ToList();

                if (nonRoomPlacements.Count > 0)
                {
                    Console.WriteLine();
                    Console.WriteLine("--- Non-Room PackIn Placements ---");
                    foreach (var kvp in nonRoomPlacements)
                    {
                        Console.WriteLine($"  {kvp.Key} x{kvp.Value.Count}");
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Report complete.");
            return 0;
        }

        private static void CheckPlacedObject(
            PlacedObject po,
            string cellName,
            Dictionary<FormKey, string> packInFormKeys,
            Dictionary<string, (int Count, List<string> Cells)> placedCounts)
        {
            var baseFormKey = po.Base.FormKey;
            if (packInFormKeys.TryGetValue(baseFormKey, out var packInEditorId))
            {
                if (!placedCounts.TryGetValue(packInEditorId, out var entry))
                {
                    entry = (0, new List<string>());
                }
                entry.Count++;
                if (!entry.Cells.Contains(cellName))
                    entry.Cells.Add(cellName);
                placedCounts[packInEditorId] = entry;
            }
        }

        private static string DiagnoseUnplacedRoom(string roomId, List<string> lists, StarfieldMod mod,
            HashSet<string> placedConnectorProfiles, float placedAvgVolume)
        {
            // Check if it's a blocker (deprioritized by RoomUtils.GetRoom)
            if (roomId.IndexOf("rg_blocker", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Blocker prefab (deprioritized by room selection - only used as fallback)";

            // Look up the PackIn to check connectors
            var packIn = mod.PackIns.FirstOrDefault(p => string.Equals(p.EditorID, roomId, StringComparison.OrdinalIgnoreCase));
            if (packIn == null)
                return "PackIn not found in mod (missing or orphaned FormList entry)";

            // Check the prefab cell for connector markers
            var cellFormKey = packIn.Cell.FormKey;
            Cell prefabCell = FindCellByFormKey(mod, cellFormKey);
            if (prefabCell == null)
                return "Prefab cell not found (PackIn references missing cell)";

            var connectors = new List<string>();
            var slotCount = 0;
            foreach (var entry in prefabCell.Temporary)
            {
                if (entry is PlacedObject po && !string.IsNullOrEmpty(po.EditorID))
                {
                    if (po.EditorID.StartsWith("rg_conn_", StringComparison.OrdinalIgnoreCase))
                        connectors.Add(po.EditorID);
                    if (po.EditorID.StartsWith("rg_slot_", StringComparison.OrdinalIgnoreCase))
                        slotCount++;
                }
            }
            foreach (var entry in prefabCell.Persistent)
            {
                if (entry is PlacedObject po && !string.IsNullOrEmpty(po.EditorID))
                {
                    if (po.EditorID.StartsWith("rg_conn_", StringComparison.OrdinalIgnoreCase))
                        connectors.Add(po.EditorID);
                    if (po.EditorID.StartsWith("rg_slot_", StringComparison.OrdinalIgnoreCase))
                        slotCount++;
                }
            }

            if (connectors.Count == 0)
                return "No connectors found (room has no rg_conn_ markers - cannot attach to dungeon)";

            // Parse connectors to check compatibility
            var parsedConnectors = connectors.Select(RgConnectorParser.Parse).Where(c => c.IsValid).ToList();
            if (parsedConnectors.Count == 0)
                return $"Has {connectors.Count} connector marker(s) but none parsed as valid rg_conn_ connectors";

            var directions = parsedConnectors.Select(c => c.Direction).Distinct().ToList();
            var connectorSummary = string.Join(", ", parsedConnectors.Select(c => $"{c.Direction}/{c.DoorSize}/{c.Tileset}"));

            // --- Connector compatibility check ---
            // A room's connectors must match DoorSize+Tileset of at least one placed room's connector
            var roomProfiles = parsedConnectors
                .Select(c => $"{c.DoorSize}|{c.Tileset}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var matchingProfiles = roomProfiles.Where(p => placedConnectorProfiles.Contains(p)).ToList();
            var unmatchedProfiles = roomProfiles.Where(p => !placedConnectorProfiles.Contains(p)).ToList();

            if (matchingProfiles.Count == 0)
            {
                var profileDesc = string.Join(", ", roomProfiles.Select(p => p.Replace("|", "/")));
                return $"Connector mismatch - no placed room shares DoorSize/Tileset [{profileDesc}]. Connectors: [{connectorSummary}]";
            }

            // --- Bounding box / collision risk check ---
            string collisionNote = "";
            if (packIn.ObjectBounds != null)
            {
                var bMin = packIn.ObjectBounds.First;
                var bMax = packIn.ObjectBounds.Second;
                float sizeX = Math.Abs(bMax.X - bMin.X);
                float sizeY = Math.Abs(bMax.Y - bMin.Y);
                float sizeZ = Math.Abs(bMax.Z - bMin.Z);
                float volume = sizeX * sizeY * sizeZ;

                if (placedAvgVolume > 0 && volume > placedAvgVolume * 2f)
                    collisionNote = $" Bounds {sizeX:F0}x{sizeY:F0}x{sizeZ:F0} (vol {volume:F0}) is >{2}x avg placed room vol ({placedAvgVolume:F0}) - high collision risk.";
                else
                    collisionNote = $" Bounds {sizeX:F0}x{sizeY:F0}x{sizeZ:F0} (vol {volume:F0}).";
            }

            // --- Direction coverage check ---
            if (directions.Count == 1)
                return $"Single-direction connectors only ({directions[0]}). [{connectorSummary}].{collisionNote} Harder to fit during placement.";

            // Partial profile match warning
            if (unmatchedProfiles.Count > 0)
            {
                var unmatchedDesc = string.Join(", ", unmatchedProfiles.Select(p => p.Replace("|", "/")));
                return $"Partial connector match - [{unmatchedDesc}] not used by placed rooms. [{connectorSummary}], {connectors.Count} connector(s), {slotCount} slot(s).{collisionNote}";
            }

            return $"Not selected during generation (random selection or collision). [{connectorSummary}], {connectors.Count} connector(s), {slotCount} slot(s).{collisionNote}";
        }

        private static Cell FindCellByFormKey(StarfieldMod mod, FormKey formKey)
        {
            for (int i = 0; i < mod.Cells.Count; i++)
            {
                for (int j = 0; j < mod.Cells[i].SubBlocks.Count; j++)
                {
                    for (int k = 0; k < mod.Cells[i].SubBlocks[j].Cells.Count; k++)
                    {
                        if (mod.Cells[i].SubBlocks[j].Cells[k].FormKey == formKey)
                            return mod.Cells[i].SubBlocks[j].Cells[k];
                    }
                }
            }
            return null;
        }
    }
}
