using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.IO;
using Retrograde;
using Retrograde.Passes;
using Retrograde.StationDesigns;
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
        private static readonly ThreadLocal<Random> _rng = new(() => new Random(Guid.NewGuid().GetHashCode()));
        private readonly object _logLock = new();
        private readonly object _bestLock = new();

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

            Parallel.For(0, runs, runIndex =>
            {
                var current = Interlocked.Increment(ref done);

                var weights = CloneWeights(weightSets[_rng.Value!.Next(weightSets.Count)]);
                var normWeights = NormalizeWeights(weights, 100.0);
                var lastScore = GenerateOnce(cell, location, normWeights);

                var summary = new WeightRunSummary(
                    CloneWeights(normWeights),
                    lastScore.Total,
                    lastScore.Total,
                    lastScore.Total,
                    lastScore!,
                    runIndex + 1);

                lock (_bestLock)
                {
                    if (best == null || summary.AverageScore > best.AverageScore)
                    {
                        best = summary;
                    }
                }

                WeightRunSummary? bestSnapshot;
                lock (_bestLock)
                {
                    bestSnapshot = best;
                }

                lock (_logLock)
                {
                    WriteLog(
                        $"Run {current}/{total} | score {summary.LastRunScore.Total:0.00} | weights PW {normWeights.PlacementWeight:0.##}, BW {normWeights.BridgingWeight:0.##}, Area {normWeights.AreaWeight:0.##}, Conn {normWeights.ConnectorViabilityWeight:0.##} | leader avg {(bestSnapshot?.AverageScore ?? 0):0.00}");
                }
            });

            if (best != null)
            {
                WriteLog("==== Overall best result after all tests ====");
                WriteLog("scoringSystem = new ScoringSystem()");
                WriteLog("{");
                WriteLog($"    BridgingWeight = {best.Weights.BridgingWeight:0.##},");
                WriteLog($"    BridgingOverlapWeight = {best.Weights.BridgingOverlapWeight:0.##},");
                WriteLog($"    NorthBiasWeight = {best.Weights.NorthBiasWeight:0.##},");
                WriteLog($"    NewConnectorsWeight = {best.Weights.NewConnectorsWeight:0.##},");
                WriteLog($"    PlacementWeight = {best.Weights.PlacementWeight:0.##},");
                WriteLog($"    AreaWeight = {best.Weights.AreaWeight:0.##},");
                WriteLog($"    ClusteringWeight = {best.Weights.ClusteringWeight:0.##},");
                WriteLog($"    SizeDiversityWeight = {best.Weights.SizeDiversityWeight:0.##},");
                WriteLog($"    RoomReuseWeight = {best.Weights.RoomReuseWeight:0.##},");
                WriteLog($"    ConnectorViabilityWeight = {best.Weights.ConnectorViabilityWeight:0.##},");
                WriteLog($"    Effort = {best.Weights.Effort}");
                WriteLog("};");

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
                IsHarnessRun = true
            };
            state.BridgePrefabKeys = BridgeUtil.BuildBridgePrefabKeys(state.TrunkRoomLists, state.GetRoomUtils);

            // Run main room passes
            foreach (var pass in design.MainRoomPasses)
            {
                pass.RunPass(state);
            }

            // Run optional room passes (chance-based)
            foreach (var optPass in design.OptionalRoomPasses)
            {
                if (_rng.Value!.NextDouble() < optPass.Chance)
                {
                    optPass.Pass.RunPass(state);
                }
            }

            // Run connector sealing passes
            foreach (var pass in design.ConnectorSealingPasses)
            {
                pass.RunPass(state);
            }

            // Run content passes
            foreach (var pass in design.ContentPasses)
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

        private static ScoringSystem NormalizeWeights(ScoringSystem src, double budget)
        {
            double total =
                Math.Abs(src.PlacementWeight) +
                Math.Abs(src.BridgingWeight) +
                Math.Abs(src.BridgingOverlapWeight) +
                Math.Abs(src.NorthBiasWeight) +
                Math.Abs(src.NewConnectorsWeight) +
                Math.Abs(src.AreaWeight) +
                Math.Abs(src.ClusteringWeight) +
                Math.Abs(src.SizeDiversityWeight) +
                Math.Abs(src.RoomReuseWeight) +
                Math.Abs(src.ConnectorViabilityWeight);

            if (total <= 0.0001)
                return CloneWeights(src);

            var scale = budget / total;

            return new ScoringSystem
            {
                PlacementWeight = src.PlacementWeight * scale,
                BridgingWeight = src.BridgingWeight * scale,
                BridgingOverlapWeight = src.BridgingOverlapWeight * scale,
                NorthBiasWeight = src.NorthBiasWeight * scale,
                NewConnectorsWeight = src.NewConnectorsWeight * scale,
                AreaWeight = src.AreaWeight * scale,
                ClusteringWeight = src.ClusteringWeight * scale,
                SizeDiversityWeight = src.SizeDiversityWeight * scale,
                RoomReuseWeight = src.RoomReuseWeight * scale,
                ConnectorViabilityWeight = src.ConnectorViabilityWeight * scale,
                Effort = src.Effort
            };
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
