using FrankyCLI.Retrograde.Passes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI.Retrograde.StationDesigns
{
    public class OreStation : IStationDesign
    {

        public OreStation()
        {
            stationPasses = new List<IGenPass>()
            {
                //Place rooms
                new TrunkTopologyPass(2),
                  new DistrictTopologyPass("rg_orelist",1,"ore", new List<string>(){}),
                  new DistrictTopologyPass("rg_trunklist",1),
                  new DistrictTopologyPass("rg_orelist",1,"ore"),
                    new BossTopologyPass("boss"),
                    new BridgingTopologyPass(),
                    new UtilTopologyPass("rg_utillist", 0.5f),
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
                BridgingWeight = 12,
                BridgingOverlapWeight = -1,
                NorthBiasWeight = 0.8,
                NewConnectorsWeight = 0.75,
                PlacementWeight = 650,
                AreaWeight = 0.05,
                ClusteringWeight = 2,
                SizeDiversityWeight = 5,
                RoomReuseWeight = -1,
                ConnectorViabilityWeight = 0.56,
                Effort = 250
            };
        }
    }
}
