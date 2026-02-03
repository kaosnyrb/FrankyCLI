using Mutagen.Bethesda.Starfield;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde.Passes
{
    internal class StationSetupPass : IGenPass
    {
        public void RunPass(DungeonState state)
        {
            var startingMarker = state.instance.Persistent
                .OfType<PlacedObject>()
                .FirstOrDefault(m => m.EditorID != null && m.EditorID.Contains("rg_conn_n"));

            if (startingMarker == null) throw new Exception("rg_conn_n not found.");

            var startingConnector = RgConnectorParser.Parse(startingMarker.EditorID);
            if (!startingConnector.IsValid)
                throw new Exception($"Invalid starting connector on marker {startingMarker.EditorID}");

            state.StartingPosition = startingMarker.Position;
            state.StartingConnector = startingConnector;
            state.YMin = startingMarker.Position.Y;

            // Seed the dungeon with the initial open connector so later passes can attach rooms.
            state.openConnectors.Clear();
            state.openConnectors.Add(new OpenConnector
            {
                Parsed = startingConnector,
                YawSteps = 0,
                WorldPos = startingMarker.Position,
                DistrictType = "trunk"
            });

            if (!state.IsHarnessRun)
                Console.WriteLine($"[StationSetup] Starting position: ({startingMarker.Position.X:F0}, {startingMarker.Position.Y:F0}, {startingMarker.Position.Z:F0})");
        }
    }
}
