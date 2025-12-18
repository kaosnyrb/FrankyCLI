using FrankyCLI.questgen_tools;
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
            theme = "rg_roomlist_" + theme;
            var key = roomTemplates[theme].Items[RandomUtils.random.Next(roomTemplates[theme].Items.Count)].FormKey;
            return gen_quest_main.myMod.PackIns[key].EditorID;
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

        private List<RgConnectorInstance> GetConnectors(RoomPrefab prefab)
        {
            return prefab.Markers
                .Select(m => new RgConnectorInstance
                {
                    EditorId = m.MarkerEditorId,
                    Parsed = RgConnectorParser.Parse(m.MarkerEditorId),
                    LocalPos = m.Position
                })
                .Where(x => x.Parsed.IsValid)
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


        public void GenerateDungeon(Cell cell, string theme)
        {
            var startingMarker = cell.Persistent
                .OfType<PlacedObject>()
                .FirstOrDefault(m => m.EditorID.Contains("rg_conn_n"));

            var startingConnector = RgConnectorParser.Parse(startingMarker.EditorID);

            if (startingMarker == null) throw new Exception("rg_conn_n not found.");
            

            var roomPrefab = new RoomPrefab(GetRoom(startingConnector.Tileset));

            var connectors = roomPrefab.Markers
                .Select(m => new
                {
                    Marker = m,
                    Conn = RgConnectorParser.Parse(m.MarkerEditorId)
                })
                .Where(x => x.Conn.IsValid)
                .ToList();

            var south0 = connectors
                .FirstOrDefault(x => x.Conn.Direction == ConnectorDirection.South)
                ?.Marker;

            var north0 = connectors
                .FirstOrDefault(x => x.Conn.Direction == ConnectorDirection.North)
                ?.Marker;

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
            int maxRoomsToPlace = 10;          // hard limit (rooms)
            int maxAttempts = 50;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -1f; // tweak: world units clearance
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
                Rotation = new P3Float(),
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
                    var nextConnectors = GetConnectors(nextPrefab);

                    var compatible = nextConnectors
                        .Where(c =>
                            c.Parsed.Direction == requiredDir &&
                            string.Equals(c.Parsed.DoorSize, target.Parsed.DoorSize, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(c.Parsed.Tileset, target.Parsed.Tileset, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (compatible.Count == 0)
                        continue;

                    var chosen = compatible[rng.Next(compatible.Count)];

                    // Align
                    P3Float nextPos = target.WorldPos - chosen.LocalPos;

                    // Collision test (AABB vs all placed rooms)
                    var candidateAabb = ToWorldAabb(nextPrefab.packin_instance.ObjectBounds, nextPos);
                    if (CollidesWithAny(candidateAabb, placedRooms, collisionPadding))
                        continue;

                    // Place it
                    cell.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
                    {
                        Count = 1,
                        Rotation = new P3Float(),
                        Position = nextPos,
                        Base = nextPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                    });

                    // Record for second pass
                    placedRooms.Add(new PlacedRoom
                    {
                        Prefab = nextPrefab,
                        WorldPos = nextPos,
                        Rotation = new P3Float(),
                        Connectors = nextConnectors
                    });

                    roomsPlaced++;
                    placed = true;

                    // Add newly-open connectors except the consumed one
                    foreach (var c in nextConnectors)
                    {
                        if (c.EditorId == chosen.EditorId && c.LocalPos.Equals(chosen.LocalPos))
                            continue;

                        openConnectors.Add(new OpenConnector
                        {
                            Parsed = c.Parsed,
                            WorldPos = nextPos + c.LocalPos
                        });
                    }

                    break;
                }

                // If we couldn't place anything for this connector, we just move on.
                // (We already removed it from openConnectors to ensure forward progress.)
                if (!placed)
                {
                    continue;
                }
            }
        }
    }
}
