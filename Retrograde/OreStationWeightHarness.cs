using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using FrankyCLI.Retrograde.Passes;
using FrankyCLI.Retrograde.StationDesigns;
using Mutagen.Bethesda.Starfield;

namespace FrankyCLI.Retrograde
{
    // Generates station layouts across many scoring weight combinations and surfaces the best performer.
    public class OreStationWeightHarness
    {
        public record WeightRunSummary(
            ScoringSystem Weights,
            double AverageScore,
            double BestScore,
            double WorstScore,
            PlanScore LastRunScore,
            int Runs);

        private readonly Func<IStationDesign> _designFactory;
        private readonly List<string> _trunkRoomLists;
        private readonly string _faction;
        private readonly string _size;
        private readonly Random _rng = new Random();

        public OreStationWeightHarness(
            Func<IStationDesign>? designFactory = null,
            IEnumerable<string>? trunkRoomLists = null,
            string faction = "spacer",
            string size = "Small")
        {
            _designFactory = designFactory ?? (() => new OreStation());
            _trunkRoomLists = trunkRoomLists?.ToList() ?? new List<string> { "rg_trunklist" };
            _faction = faction;
            _size = size;
        }

        public WeightRunSummary FindBest(
            Cell cell,
            Location location,
            int runs,
            IEnumerable<ScoringSystem>? customWeights = null)
        {
            if (cell == null) throw new ArgumentNullException(nameof(cell));
            if (location == null) throw new ArgumentNullException(nameof(location));
            if (runs <= 0) throw new ArgumentOutOfRangeException(nameof(runs), "Runs must be positive.");

            var log = new List<string>();
            void WriteLog(string message)
            {
                log.Add(message);
                Console.WriteLine(message);
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var reportPath = Path.Combine(Environment.CurrentDirectory, $"OreStationHarness_{timestamp}.txt");

            var weightSets = (customWeights ?? BuildDefaultWeightSets())
                .Select(CloneWeights)
                .ToList();

            if (weightSets.Count == 0)
                throw new ArgumentException("No weight sets supplied.", nameof(customWeights));

            WeightRunSummary? best = null;

            int total = runs;
            int done = 0;

            for (int runIndex = 0; runIndex < runs; runIndex++)
            {
                done++;

                var line = new string('-', 64);
                var bestSoFar = best != null
                    ? $"current leader avg {best.AverageScore:0.00} (best {best.BestScore:0.00})"
                    : "no leader yet";

                WriteLog(line);
                WriteLog($"Run {done}/{total} | {bestSoFar}");
                WriteLog(line);

                var weights = CloneWeights(weightSets[_rng.Next(weightSets.Count)]);
                var lastScore = GenerateOnce(cell, location, weights);

                var summary = new WeightRunSummary(
                    CloneWeights(weights),
                    lastScore.Total,
                    lastScore.Total,
                    lastScore.Total,
                    lastScore!,
                    runIndex + 1);

                WriteLog(
                    $"Weights:\n" +
                    $"  PlacementWeight            {weights.PlacementWeight:0.##}\n" +
                    $"  BridgingWeight             {weights.BridgingWeight:0.##}\n" +
                    $"  BridgingOverlapWeight      {weights.BridgingOverlapWeight:0.##}\n" +
                    $"  NorthBiasWeight            {weights.NorthBiasWeight:0.##}\n" +
                    $"  NewConnectorsWeight        {weights.NewConnectorsWeight:0.##}\n" +
                    $"  AreaWeight                 {weights.AreaWeight:0.##}\n" +
                    $"  ClusteringWeight           {weights.ClusteringWeight:0.##}\n" +
                    $"  SizeDiversityWeight        {weights.SizeDiversityWeight:0.##}\n" +
                    $"  RoomReuseWeight            {weights.RoomReuseWeight:0.##}\n" +
                    $"  ConnectorViabilityWeight   {weights.ConnectorViabilityWeight:0.##}\n" +
                    $"  Effort                     {weights.Effort}\n" +
                    $"Results:\n" +
                    $"  Score {summary.LastRunScore.Total:0.00}");

                if (best == null || summary.AverageScore > best.AverageScore)
                {
                    best = summary;
                }
            }

            if (best != null)
            {
                WriteLog("==== Overall best result after all tests ====");
                WriteLog(
                    $"Best weights after {best.Runs} runs:");
                WriteLog(
                    $"  PlacementWeight            {best.Weights.PlacementWeight:0.##}\n" +
                    $"  BridgingWeight             {best.Weights.BridgingWeight:0.##}\n" +
                    $"  BridgingOverlapWeight      {best.Weights.BridgingOverlapWeight:0.##}\n" +
                    $"  NorthBiasWeight            {best.Weights.NorthBiasWeight:0.##}\n" +
                    $"  NewConnectorsWeight        {best.Weights.NewConnectorsWeight:0.##}\n" +
                    $"  AreaWeight                 {best.Weights.AreaWeight:0.##}\n" +
                    $"  ClusteringWeight           {best.Weights.ClusteringWeight:0.##}\n" +
                    $"  SizeDiversityWeight        {best.Weights.SizeDiversityWeight:0.##}\n" +
                    $"  RoomReuseWeight            {best.Weights.RoomReuseWeight:0.##}\n" +
                    $"  ConnectorViabilityWeight   {best.Weights.ConnectorViabilityWeight:0.##}\n" +
                    $"  Effort                     {best.Weights.Effort}");

                File.WriteAllText(reportPath, string.Join(Environment.NewLine, log));
                WriteLog($"Report written to {reportPath}");
                return best;
            }

            throw new InvalidOperationException("Harness failed to evaluate any weight set.");
        }

        private PlanScore GenerateOnce(Cell cell, Location location, ScoringSystem weights)
        {
            var design = _designFactory();
            design.scoringSystem = CloneWeights(weights);

            var state = new DungeonState(cell, location)
            {
                Faction = _faction,
                Size = _size,
                TrunkRoomLists = new List<string>(_trunkRoomLists),
                scoringSystem = CloneWeights(weights),
                passes = design.stationPasses,
                IsHarnessRun = true
            };
            state.BridgePrefabKeys = BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists, state.GetRoomUtils);

            foreach (var pass in state.passes)
            {
                pass.RunPass(state);
            }

            return ScoreState(state, weights);
        }

        private static PlanScore ScoreState(DungeonState state, ScoringSystem weights)
        {
            var rooms = state.placedRooms ?? new List<PlacedRoom>();
            var opens = state.openConnectors ?? new List<OpenConnector>();

            int roomsPlaced = rooms.Count;
            var bridgeKeys = state.BridgePrefabKeys ?? BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists, state.GetRoomUtils);
            int bridgeablePairs = BridgeUtil.CountBridgeablePairs(opens, state.YMin, 40f, 8f, bridgeKeys);
            int newConnectors = opens.Count;
            double area = ScoringUtil.CalculateTotalArea(rooms);
            double clustering = ScoringUtil.CalculateAverageMinimumDistance(rooms);
            double sizeDiversity = ScoringUtil.CalculateSmallRoomChainPenalty(rooms);
            double roomReuse = ScoringUtil.CalculateRoomReuseScore(rooms);
            double connectorViability = ScoringUtil.CalculateConnectorViabilityArea(rooms, opens);

            return ScoringUtil.ScorePlan(
                weights,
                roomsPlaced,
                bridgeablePairs,
                0,
                newConnectors,
                area,
                clustering,
                sizeDiversity,
                roomReuse,
                connectorViability);
        }

        private List<ScoringSystem> BuildDefaultWeightSets()
        {
            var baseWeights = _designFactory().scoringSystem;

            var bridging = new[] { baseWeights.BridgingWeight * 0.8, baseWeights.BridgingWeight, baseWeights.BridgingWeight * 1.2 };
            var placement = new[] { baseWeights.PlacementWeight * 0.7, baseWeights.PlacementWeight, baseWeights.PlacementWeight * 1.3 };
            var area = new[] { baseWeights.AreaWeight * 0.5, baseWeights.AreaWeight, baseWeights.AreaWeight * 1.5 };
            var connector = new[] { baseWeights.ConnectorViabilityWeight * 0.75, baseWeights.ConnectorViabilityWeight, baseWeights.ConnectorViabilityWeight * 1.25 };

            var combos = new List<ScoringSystem>();
            foreach (var b in bridging)
            foreach (var p in placement)
            foreach (var a in area)
            foreach (var cv in connector)
            {
                var w = CloneWeights(baseWeights);
                w.BridgingWeight = b;
                w.PlacementWeight = p;
                w.AreaWeight = a;
                w.ConnectorViabilityWeight = cv;
                combos.Add(w);
            }

            return combos;
        }

        private static ScoringSystem CloneWeights(ScoringSystem src)
        {
            return new ScoringSystem
            {
                PlacementWeight = src.PlacementWeight,
                BridgingWeight = src.BridgingWeight,
                BridgingOverlapWeight = src.BridgingOverlapWeight,
                NorthBiasWeight = src.NorthBiasWeight,
                NewConnectorsWeight = src.NewConnectorsWeight,
                AreaWeight = src.AreaWeight,
                ClusteringWeight = src.ClusteringWeight,
                SizeDiversityWeight = src.SizeDiversityWeight,
                RoomReuseWeight = src.RoomReuseWeight,
                ConnectorViabilityWeight = src.ConnectorViabilityWeight,
                Effort = src.Effort
            };
        }
    }
}
