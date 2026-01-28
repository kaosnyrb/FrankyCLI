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
                new StationSetupPass(),
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
                BridgingWeight = 9.5,
                BridgingOverlapWeight = -1.19,
                NorthBiasWeight = 0.95,
                NewConnectorsWeight = 0.89,
                PlacementWeight = 77.16,
                AreaWeight = -0.15,
                ClusteringWeight = 2.37,
                SizeDiversityWeight = 5.94,
                RoomReuseWeight = -1.19,
                ConnectorViabilityWeight = 0.67,
                Effort = 100
            };
        }
    }
}
