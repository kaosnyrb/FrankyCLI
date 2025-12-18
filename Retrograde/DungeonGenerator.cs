using FrankyCLI.questgen_tools;
using FrankyCLI.Retrograde;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Starfield;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI
{
    public class DungeonGenerator
    {
        /*
        Assembles prefabs into a dungeons.

        Each Room is labelled so:
        rg_<tileset>_<roomtype>_<variant>

        egs
        rg_station_corridor_01
        rg_station_deadend_01
        rg_industrial_room_small_02
        rg_research_lab_medium_01
        rg_habitation_sleep_quarters_03

        We have a formlist called rg_roomlist which contains a link to all the formlists for the tilesets.
        Eg
        rg_roomlist_station

        Each prefab has various markers inside it.

        First is the connectors, these are used to build the topolgy of the dungeon:
        rg_conn_<dir>_<door>_<tileset>[_<flags>]

        eg:
        rg_conn_n_D1_station
        rg_conn_s_D1_station
        rg_conn_e_D3_military_airlock
        rg_conn_w_D2_derelict_damaged

        The last part are the slots. These represent the contents of the room and are assigned at build.
        We do a two pass approach, first we layout the rooms then we fill them with stuff.
        Each tag has a form list that of the same name which contains the prefabs that can be there.
        

        eg:
        rg_slot_room_feature
        rg_slot_crate_large
        rg_slot_loot_rare
        rg_slot_enemy_guard
        rg_slot_clutter_large
        rg_slot_light_main
        */

        public FormList roomlist;
        public Dictionary<string, FormList> roomTemplates;

        public DungeonGenerator() {
            roomlist = gen_quest_main.myMod.FormLists.FirstOrDefault(fl => fl.EditorID == "rg_roomlist");

            roomTemplates = new Dictionary<string, FormList>();
            foreach (var f in roomlist.Items)
            {
;               var list = gen_quest_main.myMod.FormLists.FirstOrDefault(fl => fl.FormKey == f.FormKey);
                roomTemplates.Add(list.EditorID, list);
            }

            //Load the prefabs for the theme

            Console.WriteLine("Lists: " + roomTemplates.Count);
        }

        public string GetRoom(string theme)
        {
            var listKey = "rg_roomlist_" + theme;

            if (!roomTemplates.TryGetValue(listKey, out var formList) || formList?.Items == null || formList.Items.Count == 0)
                throw new Exception($"Room theme list not found or empty: {listKey}");

            // Resolve all candidate EditorIDs once
            var candidates = new List<string>(formList.Items.Count);

            foreach (var item in formList.Items)
            {
                // Defensive: skip unresolved keys
                if (!gen_quest_main.myMod.PackIns.TryGetValue(item.FormKey, out var packIn) || packIn?.EditorID == null)
                    continue;

                candidates.Add(packIn.EditorID);
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

        private ConnectorDirection Opposite(ConnectorDirection d)
        {
            return d switch
            {
                ConnectorDirection.North => ConnectorDirection.South,
                ConnectorDirection.South => ConnectorDirection.North,
                ConnectorDirection.East => ConnectorDirection.West,
                ConnectorDirection.West => ConnectorDirection.East,
                _ => ConnectorDirection.Unknown
            };
        }

        private static List<RgConnectorInstance> GetConnectors(RoomPrefab prefab, int yawSteps = 0)
        {
            return prefab.Markers
                .Select(m =>
                {
                    var parsed = RgConnectorParser.Parse(m.MarkerEditorId);
                    if (!parsed.IsValid) return (RgConnectorInstance?)null;

                    return new RgConnectorInstance
                    {
                        EditorId = m.MarkerEditorId,
                        Parsed = new RgConnector
                        {
                            RawEditorId = parsed.RawEditorId,
                            Direction = RgRotation.RotateDir(parsed.Direction, yawSteps),
                            DoorSize = parsed.DoorSize,
                            Tileset = parsed.Tileset,
                            IsValid = parsed.IsValid
                        },
                        LocalPos = RgRotation.RotateYaw90(m.Position, yawSteps)
                    };
                })
                .Where(x => x.HasValue)
                .Select(x => x.Value)
                .ToList();
        }

        private static RgAabb ToWorldAabb(ObjectBounds boundsLocal, P3Float worldPos)
        {
            // Local AABB translated into world space (no rotation assumed)
            return new RgAabb
            {
                Min = worldPos + boundsLocal.First,
                Max = worldPos + boundsLocal.Second
            };
        }

        private static bool Intersects(RgAabb a, RgAabb b, float padding = 0f)
        {
            // Optional padding expands A slightly to keep a clearance gap.
            return
                a.Min.X - padding <= b.Max.X && a.Max.X + padding >= b.Min.X &&
                a.Min.Y - padding <= b.Max.Y && a.Max.Y + padding >= b.Min.Y &&
                a.Min.Z - padding <= b.Max.Z && a.Max.Z + padding >= b.Min.Z;
        }

        private static bool CollidesWithAny(RgAabb candidate, List<PlacedRoom> placedRooms, float padding = 0f)
        {
            foreach (var r in placedRooms)
            {
                var placedAabb = ToWorldAabb(r.Prefab.packin_instance.ObjectBounds, r.WorldPos);
                if (Intersects(candidate, placedAabb, padding))
                    return true;
            }
            return false;
        }

        private static RgAabb ToWorldAabbRotated(ObjectBounds boundsLocal, P3Float worldPos, int yawSteps)
        {
            // Rotate 8 corners of the local AABB; then take min/max in world space.
            var min = boundsLocal.First;
            var max = boundsLocal.Second;

            P3Float[] corners =
            {
                new P3Float(min.X, min.Y, min.Z),
                new P3Float(min.X, min.Y, max.Z),
                new P3Float(min.X, max.Y, min.Z),
                new P3Float(min.X, max.Y, max.Z),
                new P3Float(max.X, min.Y, min.Z),
                new P3Float(max.X, min.Y, max.Z),
                new P3Float(max.X, max.Y, min.Z),
                new P3Float(max.X, max.Y, max.Z),
            };

            var first = worldPos + RgRotation.RotateYaw90(corners[0], yawSteps);
            float minX = first.X, minY = first.Y, minZ = first.Z;
            float maxX = first.X, maxY = first.Y, maxZ = first.Z;

            for (int i = 1; i < corners.Length; i++)
            {
                var w = worldPos + RgRotation.RotateYaw90(corners[i], yawSteps);
                if (w.X < minX) minX = w.X;
                if (w.Y < minY) minY = w.Y;
                if (w.Z < minZ) minZ = w.Z;
                if (w.X > maxX) maxX = w.X;
                if (w.Y > maxY) maxY = w.Y;
                if (w.Z > maxZ) maxZ = w.Z;
            }

            return new RgAabb
            {
                Min = new P3Float(minX, minY, minZ),
                Max = new P3Float(maxX, maxY, maxZ),
            };
        }

        private string GetDoorBlocker(string doorSize, string tileset)
        {
            // Prefer: tileset-specific blockers, fallback to generic
            // e.g. rg_blocker_D1_station, rg_blocker_D1_generic
            // Replace with your real IDs / lookup.
            return doorSize switch
            {
                "D1" => $"rg_blocker_D1_{tileset}",
                "D2" => $"rg_blocker_D2_{tileset}",
                _ => $"rg_blocker_{tileset}"
            };
        }



        public void GenerateDungeon(Cell cell, string theme)
        {
            var startingMarker = cell.Persistent
                .OfType<PlacedObject>()
                .FirstOrDefault(m => m.EditorID.Contains("rg_conn_n"));

            if (startingMarker == null) throw new Exception("rg_conn_n not found.");
            var startingConnector = RgConnectorParser.Parse(startingMarker.EditorID);

            RoomPrefab roomPrefab = null;
            PrefabMarker south0 = new PrefabMarker();
            PrefabMarker north0 = new PrefabMarker();

            for (int i = 0; i < 20; i++)
            {
                var candidate = new RoomPrefab(GetRoom(startingConnector.Tileset));

                var candConnectors = candidate.Markers
                    .Select(m => new
                    {
                        Marker = m,
                        Conn = RgConnectorParser.Parse(m.MarkerEditorId)
                    })
                    .Where(x => x.Conn.IsValid)
                    .ToList();

                var entry = candConnectors.FirstOrDefault(x => x.Conn.Direction == ConnectorDirection.South)?.Marker;

                if (entry != null && candConnectors.Any(x => x.Conn.Direction != ConnectorDirection.South))
                {
                    roomPrefab = candidate;
                    var connectors = candConnectors;
                    south0 = connectors.First(x => x.Conn.Direction == ConnectorDirection.South).Marker;
                    north0 = connectors.FirstOrDefault(x => x.Conn.Direction == ConnectorDirection.North)?.Marker;
                    break;
                }
            }

            if (roomPrefab == null)
                throw new Exception("Failed to find a starting room with open connectors.");



            // Place first prefab so its SOUTH marker lands on the starting marker.
            // prefabWorldPos + southLocal = startWorld  =>  prefabWorldPos = startWorld - southLocal
            P3Float prefabWorldPos = startingMarker.Position - south0.Position;

            cell.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
            {
                Count = 1,
                Rotation = new P3Float(),
                Position = prefabWorldPos,
                Base = roomPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
            });

            // Inputs / knobs
            int maxRoomsToPlace = 50;          // hard limit (rooms)
            int maxAttempts = 500;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -2f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 8; // avoid thrashing on a single open connector

            var rng = new Random();

            // This will be used for the second pass
            var placedRooms = new List<PlacedRoom>();

            // Build initial room record (assumes you already placed roomPrefab at prefabWorldPos)
            var startConnectors = GetConnectors(roomPrefab);

            placedRooms.Add(new PlacedRoom
            {
                Prefab = roomPrefab,
                WorldPos = prefabWorldPos,
                YawSteps = 0,
                Connectors = startConnectors
            });

            // Seed open connectors from the starting room (all connectors become candidates)
            var openConnectors = new List<OpenConnector>();
            foreach (var c in startConnectors)
            {
                if (c.Parsed.Direction != ConnectorDirection.South)
                {
                    openConnectors.Add(new OpenConnector
                    {
                        Parsed = c.Parsed,
                        YawSteps = 0,
                        WorldPos = prefabWorldPos + c.LocalPos
                    });

                }
            }

            // Main placement loop: iterates over open connectors, but bounded
            int roomsPlaced = 0;
            int attempts = 0;

            while (roomsPlaced < maxRoomsToPlace && openConnectors.Count > 0 && attempts < maxAttempts)
            {
                attempts++;

                // Choose a random open connector to fill
                int openIndex = rng.Next(openConnectors.Count);
                var target = openConnectors[openIndex];

                if (target.WorldPos.Y < startingMarker.Position.Y)
                {
                    //MAKE SURE YOU DON'T GO -Y
                    continue;
                }

                // Remove it now to ensure we "try to iterate through all open connectors"
                // (if we fail to place, we can choose to discard or re-add; discarding avoids loops)
                openConnectors.RemoveAt(openIndex);

                // We need a connector on nextPrefab that is OPPOSITE direction to target,
                // and compatible on door/tileset (simple equality checks here).
                var requiredDir = Opposite(target.Parsed.Direction);

                bool placed = false;

                for (int prefabTry = 0; prefabTry < maxCandidatePrefabsPerConnector; prefabTry++)
                {
                    var nextPrefab = new RoomPrefab(GetRoom(target.Parsed.Tileset));

                    for (int yawSteps = 0; yawSteps < 4; yawSteps++)
                    {
                        var nextConnectors = GetConnectors(nextPrefab, yawSteps);

                        var compatible = nextConnectors
                            .Where(c =>
                                c.Parsed.Direction == requiredDir &&
                                string.Equals(c.Parsed.DoorSize, target.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(c.Parsed.Tileset, target.Parsed.Tileset, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (compatible.Count == 0)
                            continue;

                        var chosen = compatible[rng.Next(compatible.Count)];

                        // Align using ROTATED local connector
                        P3Float nextPos = target.WorldPos - chosen.LocalPos;

                        // Collision using ROTATED bounds
                        var candidateAabb = ToWorldAabbRotated(nextPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                        if (CollidesWithAny(candidateAabb, placedRooms, collisionPadding))
                            continue;

                        // Place it with rotation
                        cell.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
                        {
                            Count = 1,
                            Rotation = RgRotation.RotationToP3Float(yawSteps),
                            Position = nextPos,
                            Base = nextPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                        });

                        placedRooms.Add(new PlacedRoom
                        {
                            Prefab = nextPrefab,
                            WorldPos = nextPos,
                            YawSteps = yawSteps,
                            Connectors = nextConnectors
                        });

                        roomsPlaced++;
                        placed = true;

                        foreach (var c in nextConnectors)
                        {
                            if (c.EditorId == chosen.EditorId && c.LocalPos.Equals(chosen.LocalPos))
                                continue;

                            openConnectors.Add(new OpenConnector
                            {
                                Parsed = c.Parsed,
                                YawSteps = yawSteps,
                                WorldPos = nextPos + c.LocalPos
                            });
                        }

                        break;
                    }

                    if (placed)
                        break;
                }

                // If we couldn't place anything for this connector, we just move on.
                // (We already removed it from openConnectors to ensure forward progress.)
                if (!placed)
                {
                    openConnectors.Add(target);//Return it to the list so we close it later.
                    continue;
                }
            }

            // ------------------------
            // SECOND PASS: Door blockers
            // ------------------------
            foreach (var open in openConnectors)
            {
                // Pick blocker prefab based on door size / tileset
                var blockerId = GetDoorBlocker(open.Parsed.DoorSize, open.Parsed.Tileset);
                var blockerPrefab = new RoomPrefab(blockerId);

                // Blocker should have a connector that will attach to the OPEN connector.
                // If the open connector faces North, the blocker needs a South-facing connector to mate.
                var requiredDir = Opposite(open.Parsed.Direction);

                // Try yaw steps 0..3 to orient blocker correctly (same approach as rooms)
                bool placed = false;

                for (int yawSteps = 0; yawSteps < 4; yawSteps++)
                {
                    var blockerConns = GetConnectors(blockerPrefab, yawSteps);

                    // Find a connector on the blocker that matches required direction and same door size/tileset.
                    // If your blocker is generic and doesn’t encode tileset, drop that constraint.
                    var attach = blockerConns.FirstOrDefault(c =>
                        c.Parsed.Direction == requiredDir &&
                        string.Equals(c.Parsed.DoorSize, open.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(c.Parsed.Tileset, open.Parsed.Tileset, StringComparison.OrdinalIgnoreCase));

                    if (!attach.Parsed.IsValid)
                        continue;

                    // Align blocker so its attach connector lands exactly at the open connector position
                    var blockerPos = open.WorldPos - attach.LocalPos;

                    cell.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = RgRotation.RotationToP3Float(yawSteps),
                        Position = blockerPos,
                        Base = blockerPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                    });

                    placed = true;
                    break;
                }

                // Optional: log missing blocker connector rather than hard fail
                if (!placed)
                {
                    // You may want to add logging here, e.g. Debug.WriteLine(...)
                }
            }

            //Final pass - Light occluder
            lightoccluder lightoccluder = new lightoccluder();
            lightoccluder.BuildHollowCubeAroundDungeon(
                cell,
                placedRooms,
                panelPackInEditorId: "rg_panel_8x4_lightoccluder",
                padding: 48f
            );
        }
    }
}
