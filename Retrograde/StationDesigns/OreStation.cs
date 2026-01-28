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
                new StationSetupPass(),
                  new DistrictTopologyPass("rg_orelist",4,"ore", new List<string>(){"rg_sts_ore_inc_003"}),
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
                BridgingWeight = 23.12,
                BridgingOverlapWeight = -3.21,
                NorthBiasWeight = 2.3,
                NewConnectorsWeight = 26.61,
                PlacementWeight = 17.33,
                AreaWeight = 0,
                ClusteringWeight = 6.42,
                SizeDiversityWeight = 16.06,
                RoomReuseWeight = -3.21,
                ConnectorViabilityWeight = 1.73,
                Effort = 250
            };
        }
    }
}
