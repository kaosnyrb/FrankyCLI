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

namespace FrankyCLI
{
    public class TopologyPass : IGenPass
    {
        public void RunPass(DungeonState state)
        {
            var startingMarker = state.instance.Persistent
                .OfType<PlacedObject>()
                .FirstOrDefault(m => m.EditorID.Contains("rg_conn_n"));

            if (startingMarker == null) throw new Exception("rg_conn_n not found.");
            var startingConnector = RgConnectorParser.Parse(startingMarker.EditorID);

            RoomPrefab roomPrefab = null;
            PrefabMarker south0 = new PrefabMarker();
            PrefabMarker north0 = new PrefabMarker();

            RoomUtils roomUtils = new RoomUtils();

            for (int i = 0; i < 20; i++)
            {
                var candidate = new RoomPrefab(roomUtils.GetRoom(startingConnector.Tileset));

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

            state.instance.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
            {
                Count = 1,
                Rotation = new P3Float(),
                Position = prefabWorldPos,
                Base = roomPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
            });

            // Inputs / knobs
            int maxRoomsToPlace = 15;          // hard limit (rooms)
            int maxAttempts = 500;              // hard limit (failed tries) to avoid infinite loops
            float collisionPadding = -2f; // tweak: world units clearance
            int maxCandidatePrefabsPerConnector = 8; // avoid thrashing on a single open connector

            var rng = new Random();

            // This will be used for the second pass
            
            // Build initial room record (assumes you already placed roomPrefab at prefabWorldPos)
            var startConnectors = ConnectorUtils.GetConnectors(roomPrefab);

            state.placedRooms.Add(new PlacedRoom
            {
                Prefab = roomPrefab,
                WorldPos = prefabWorldPos,
                YawSteps = 0,
                Connectors = startConnectors
            });

            // Seed open connectors from the starting room (all connectors become candidates)
            
            foreach (var c in startConnectors)
            {
                if (c.Parsed.Direction != ConnectorDirection.South)
                {
                    state.openConnectors.Add(new OpenConnector
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

            while (roomsPlaced < maxRoomsToPlace && state.openConnectors.Count > 0 && attempts < maxAttempts)
            {
                attempts++;

                // Choose a random open connector to fill
                int openIndex = rng.Next(state.openConnectors.Count);
                var target = state.openConnectors[openIndex];

                if (target.WorldPos.Y < startingMarker.Position.Y)
                {
                    //MAKE SURE YOU DON'T GO -Y
                    continue;
                }

                // Remove it now to ensure we "try to iterate through all open connectors"
                // (if we fail to place, we can choose to discard or re-add; discarding avoids loops)
                state.openConnectors.RemoveAt(openIndex);

                // We need a connector on nextPrefab that is OPPOSITE direction to target,
                // and compatible on door/tileset (simple equality checks here).
                var requiredDir = ConnectorUtils.Opposite(target.Parsed.Direction);

                bool placed = false;

                for (int prefabTry = 0; prefabTry < maxCandidatePrefabsPerConnector; prefabTry++)
                {
                    var nextPrefab = new RoomPrefab(roomUtils.GetRoom(target.Parsed.Tileset));

                    for (int yawSteps = 0; yawSteps < 4; yawSteps++)
                    {
                        var nextConnectors = ConnectorUtils.GetConnectors(nextPrefab, yawSteps);

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
                        var candidateAabb = ConnectorUtils.ToWorldAabbRotated(nextPrefab.packin_instance.ObjectBounds, nextPos, yawSteps);
                        if (ConnectorUtils.CollidesWithAny(candidateAabb, state.placedRooms, collisionPadding))
                            continue;

                        // Place it with rotation
                        state.instance.Temporary.Add(new PlacedObject(gen_quest_main.myMod)
                        {
                            Count = 1,
                            Rotation = RgRotation.RotationToP3Float(yawSteps),
                            Position = nextPos,
                            Base = nextPrefab.packin_instance.ToLink<IPlaceableObjectGetter>()
                        });

                        state.placedRooms.Add(new PlacedRoom
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

                            state.openConnectors.Add(new OpenConnector
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
                    state.openConnectors.Add(target);//Return it to the list so we close it later.
                    continue;
                }
            }
        }

    }
}
