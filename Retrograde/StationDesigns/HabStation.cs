using FrankyCLI.Retrograde.Passes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde.StationDesigns
{
    public class HabStation : IStationDesign
    {
        public HabStation()
        {
            stationPasses = new List<IGenPass>()
            {
                //Place rooms
                new TrunkTopologyPass(4),
                    new BossTopologyPass("boss"),
                    new DistrictTopologyPass("rg_hablist", 4, "hab"),
                    //new BridgeHelperPass(),
                    new BridgingTopologyPass(),
                    new UtilTopologyPass("rg_utillist", 0.8f),
                //Seal connectors
                    new WindowSealingPass(),
                    new ConnectorSealingPass(),
                //Doors and plugs
                    new PlugPass(),
                    new DoorPass(),
                //Fill content
                    new EnemyPass(),
                    new ContentPass(),
                    new ShipMarkerPass(),
                //util
                    new LightOccluderPass()
            };

            scoringSystem = new ScoringSystem()
            {
                BridgingWeight = 10,
                BridgingOverlapWeight = -1,
                NorthBiasWeight = 0.8,
                NewConnectorsWeight = 0.75,
                PlacementWeight = 50,
                AreaWeight = -0.25,
                ClusteringWeight = 2,
                SizeDiversityWeight = 5,
                RoomReuseWeight = -1,
                ConnectorViabilityWeight = 0.75,
                Effort = 100
            };
        }
    }
}
