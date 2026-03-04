using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using Retrograde.Nouns.WorldspaceNouns;
using Retrograde.Passes.Worldspace;
using Retrograde.WorldspaceDesigns;
using System;
using System.Collections.Generic;
using System.IO;

namespace FrankyCLI
{
    /// <summary>
    /// Coordinate system test for TileInstantiationPass.
    /// Creates a worldspace and places NavMeshGenSeedMarker statics at:
    ///   - Every cell corner (multiples of 4096) within the grid
    ///   - The computed tile grid NW/NE/SW/SE/CENTER corners
    ///   - The flat area centre (from TerrainFlattenPass)
    ///
    /// Load the output in CK, open the worldspace, and verify:
    ///   - ct_g_E0_N0       sits at world origin (0,0) — junction of cells (-1..0,−1..0)
    ///   - ct_g_E1_N0/etc   spaced 50 world-units apart, all on-terrain
    ///   - ct_flat_center   is inside the visually-flattened terrain plateau
    ///   - tile_NW / tile_NE / tile_SW / tile_SE  bound the tile grid correctly
    ///   - tile_CENTER aligns with the flat area centre
    ///
    /// Usage: &lt;modname&gt; gen_coordtest dummy dummy dummy [seed]
    /// Example bat: frankycli.exe coordtest gen_coordtest dummy dummy dummy 42
    /// </summary>
    public class gen_coordtest
    {
        public static int Generate(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("Usage: <modname> gen_coordtest dummy dummy dummy [seed]");
                return 1;
            }

            string modname = args[0];
            string datapath = "";

            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                gen_quest_main.StarfieldModKey = new ModKey("Starfield", ModType.Master);
                datapath = env.DataFolderPath;
                gen_quest_main._StarfieldMod = env.LoadOrder[0].Mod!;

                ModKey newMod = new ModKey(modname, ModType.Master);
                gen_quest_main.myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);

                ModContextImpl.TemplateModsList = new List<IStarfieldModGetter>();
                for (int i = 0; i < env.LoadOrder.Count; i++)
                {
                    var listing = env.LoadOrder[i];
                    var fileName = listing.FileName.ToString();
                    if (listing.Mod != null &&
                        (fileName.Contains("template", StringComparison.OrdinalIgnoreCase)
                         || fileName.Equals("Starfield.esm", StringComparison.OrdinalIgnoreCase))
                        && fileName.EndsWith(".esm", StringComparison.OrdinalIgnoreCase))
                    {
                        ModContextImpl.TemplateModsList.Add(listing.Mod);
                    }
                }

                // Sync statics required by WorldspaceNoun / downstream code
                gen_quest_main.StarfieldModKey = new ModKey("Starfield", ModType.Master);
                gen_quest_main._StarfieldMod = env.LoadOrder[0].Mod!;

                // Populate MasterFlagsCache so BuildWriteParams() works.
                // Must happen inside the using block while the load order is alive.
                gen_quest_main.BuildReadParams(env.LoadOrder);

                RetrogradeContext.Current = new ModContextImpl();

                int seed = args.Length > 5 ? int.Parse(args[5]) : 42;
                var design = new CoordTestDesign();

                Console.WriteLine($"Running coord test with seed {seed}");
                var noun = new WorldspaceNoun(design, "Spacer", seed, datapath);
                Console.WriteLine($"Worldspace created: {noun.Worldspace.EditorID}");
            }

            foreach (var rec in gen_quest_main.myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            gen_quest_main.myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine("Export complete!");
            return 0;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Minimal design: terrain-flatten only, then place coordinate markers.
    // ─────────────────────────────────────────────────────────────────────────
    internal class CoordTestDesign : IWorldspaceDesign
    {
        public List<IWorldspacePass> MapPasses { get; set; }
        public List<IWorldspacePass> CellBuildPasses { get; set; }
        public List<IWorldspacePass> ContentPasses { get; set; }

        public string TemplateWorldspaceEditorId => "DR001World";
        public int MapSize => 50;
        public float TileWorldSize => 4f;
        public string DesignName => "CoordTest";

        public CoordTestDesign()
        {
            MapPasses = new List<IWorldspacePass> { new TerrainFlattenPass() };
            CellBuildPasses = new List<IWorldspacePass>();
            ContentPasses = new List<IWorldspacePass> { new CoordMarkerPass() };
        }

        public string GeneratePOIName(int seed) => "Coord Test " + seed;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ContentPass: places NavMeshGenSeedMarker statics at key world positions.
    // Runs once globally (ContentPasses phase) so CellLookup is fully built.
    // ─────────────────────────────────────────────────────────────────────────
    internal class CoordMarkerPass : IWorldspacePass
    {
        // NavMeshGenSeedMarker [STAT:000001F8] — visible in CK as a small coloured dot.
        private const uint NavmeshMarkerFormId = 0x000001F8;

        public void RunPass(WorldspaceState state)
        {
            var targetMod = RetrogradeContext.Current.TargetMod;
            var sfKey = RetrogradeContext.Current.StarfieldModKey;

            IFormLinkNullable<IPlaceableObject> marker =
                new FormKey(sfKey, NavmeshMarkerFormId).ToNullableLink<IPlaceableObject>();

            float h = state.TerrainHeight;
            int blocksize = (int)state.TileWorldSize; // 4

            Console.WriteLine();
            Console.WriteLine("=== CoordMarkerPass ===");
            Console.WriteLine($"  Unit systems:");
            Console.WriteLine($"    BTD internal    : 4096 units/cell, 32 units/vertex");
            Console.WriteLine($"    Overlay worldspace: 100 units/cell, ~0.78 units/vertex");
            Console.WriteLine($"    Z (height)      : BTD 8x-scaled, divide by 8 for PlacedObject");
            Console.WriteLine($"  TerrainHeight (overlay Z, /8 applied): {h:F4}");
            Console.WriteLine($"  FlatAreaWorldX (overlay units): {state.FlatAreaWorldX?.ToString("F2") ?? "null"}");
            Console.WriteLine($"  FlatAreaWorldY (overlay units): {state.FlatAreaWorldY?.ToString("F2") ?? "null"}");
            Console.WriteLine();
            Console.WriteLine("  Expected: ct_flat_center inside the visual plateau (near origin for flat BTD).");
            Console.WriteLine("  Cell size = 100 overlay units. World spans [-200, +200] for a 4x4 BTD.");

            // ── 1. Reference grid markers ──────────────────────────────────────
            // 5×5 grid at 50-unit spacing (-100..+100).
            // VERIFY IN CK: each marker should be 50 overlay-units apart on terrain.
            // Cell boundaries: X or Y = 0, ±100, ±200 (multiples of 100).
            const float step = 50f;
            Console.WriteLine();
            Console.WriteLine("  Reference grid markers (50-unit spacing):");
            for (int cx = -2; cx <= 2; cx++)
            {
                for (int cy = -2; cy <= 2; cy++)
                {
                    float wx = cx * step;
                    float wy = cy * step;
                    string xLabel = cx >= 0 ? $"E{cx}" : $"W{-cx}";
                    string yLabel = cy >= 0 ? $"N{cy}" : $"S{-cy}";
                    string eid = $"ct_g_{xLabel}_{yLabel}";
                    Place(targetMod, marker, state, new P3Float(wx, wy, h), eid);
                    Console.WriteLine($"    {eid,-28} ({wx,8:F0}, {wy,8:F0}, {h:F2})");
                }
            }

            // ── 2. Tile grid corner / centre markers ───────────────────────────
            // Mirrors the origin calculation in TileInstantiationPass exactly.
            float originX = state.FlatAreaWorldX.HasValue
                ? state.FlatAreaWorldX.Value - blocksize * (state.Map.xsize / 2f)
                : -94f;
            float originY = state.FlatAreaWorldY.HasValue
                ? state.FlatAreaWorldY.Value + blocksize * (state.Map.ysize / 2f)
                : 94f;

            Console.WriteLine();
            Console.WriteLine($"  Tile grid origin (tile[0,0] position): ({originX:F1}, {originY:F1})");
            Console.WriteLine($"  Tile grid extent: {state.Map.xsize * blocksize} × {state.Map.ysize * blocksize} world units");
            Console.WriteLine("  Tile markers:");

            var tilePoints = new (int tx, int ty, string label)[]
            {
                (0,                      0,                      "tile_NW"),
                (state.Map.xsize - 1,    0,                      "tile_NE"),
                (0,                      state.Map.ysize - 1,    "tile_SW"),
                (state.Map.xsize - 1,    state.Map.ysize - 1,    "tile_SE"),
                (state.Map.xsize / 2,    state.Map.ysize / 2,    "tile_CENTER"),
            };

            foreach (var (tx, ty, label) in tilePoints)
            {
                float wx = originX + blocksize * tx;
                float wy = originY - blocksize * ty;
                Place(targetMod, marker, state, new P3Float(wx, wy, h), label);
                Console.WriteLine($"    {label,-28} tile[{tx,2},{ty,2}] ({wx,8:F1}, {wy,8:F1}, {h:F2})");
            }

            // ── 3. Flat area centre ────────────────────────────────────────────
            if (state.FlatAreaWorldX.HasValue && state.FlatAreaWorldY.HasValue)
            {
                float fx = state.FlatAreaWorldX.Value;
                float fy = state.FlatAreaWorldY.Value;
                Place(targetMod, marker, state, new P3Float(fx, fy, h), "ct_flat_center");
                Console.WriteLine();
                Console.WriteLine($"  ct_flat_center (TerrainFlattenPass result): ({fx:F1}, {fy:F1}, {h:F2})");
                Console.WriteLine("  ► In CK: verify this marker is inside the visually-flattened plateau.");
                Console.WriteLine("  ► If it is in the WRONG quadrant, FlatAreaWorldY is Y-inverted.");
            }

            Console.WriteLine("=== End CoordMarkerPass ===");
            Console.WriteLine();
        }

        /// <summary>
        /// Routes the placed object to the correct sub-cell based on world position.
        /// Falls back to TopCell.Persistent when the cell isn't in the lookup
        /// (e.g. markers placed exactly on the outer boundary).
        /// </summary>
        private static void Place(StarfieldMod mod,
            IFormLinkNullable<IPlaceableObject> baseForm,
            WorldspaceState state,
            P3Float pos,
            string editorId)
        {
            var placed = new PlacedObject(mod)
            {
                EditorID = editorId,
                Base = baseForm,
                Position = pos,
                Rotation = new P3Float(0, 0, 0),
            };

            // Route to the sub-cell that owns this world position.
            // Overlay worldspace cell size = 100 overlay units.
            // (Using 4096 here is equivalent for positions within ±400 units since
            //  floor(x/100) == floor(x/4096) for |x| < 4096, which covers all fort positions.)
            int cellX = (int)Math.Floor(pos.X / 100f);
            int cellY = (int)Math.Floor(pos.Y / 100f);
            if (state.CellLookup.TryGetValue(new P2Int(cellX, cellY), out var cell))
                state.PlacementUtil.AddToTemporary(cell, placed);
            else
                state.PlacementUtil.AddToPersistent(placed);
        }
    }
}
