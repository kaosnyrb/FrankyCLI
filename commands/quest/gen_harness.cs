using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using Retrograde.StationDesigns;
using System;

namespace FrankyCLI
{
    public class gen_harness
    {
        public static int Generate(string[] args)
        {
            string modname = args[0];
            string faction = args.Length > 3 ? args[3] : "spacer";
            string size = args.Length > 4 ? args[4] : "Small";
            int runs = 15;

            string datapath = "";
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                gen_quest_main.StarfieldModKey = new ModKey("Starfield", ModType.Master);
                datapath = env.DataFolderPath;
                gen_quest_main._StarfieldMod = env.LoadOrder[0].Mod!;

                ModKey newMod = new ModKey(modname, ModType.Master);
                gen_quest_main.myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                if (env.LoadOrder.ModExists(newMod))
                {
                    for (int i = 0; i < env.LoadOrder.Count; i++)
                    {
                        if (env.LoadOrder[i].FileName == modname + ".esm")
                        {
                            ModPath modPath = System.IO.Path.Combine(env.DataFolderPath, env.LoadOrder[i].FileName);
                            gen_quest_main.myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                            gen_quest_main.FixNextFormId(gen_quest_main.myMod);
                        }
                    }
                }

                RetrogradeContext.Current = new ModContextImpl();

                // Create a throwaway cell and location for the harness
                var cell = new Cell(gen_quest_main.myMod)
                {
                    EditorID = "rg_harness_cell"
                };

                // StationSetupPass needs an rg_conn_n marker in the cell's Persistent list
                var startMarker = new PlacedObject(gen_quest_main.myMod)
                {
                    EditorID = "rg_conn_n_D1_station",
                    Position = new P3Float(0, 0, 0),
                    Rotation = new P3Float(0, 0, 0)
                };
                cell.Persistent.Add(startMarker);

                var location = new Location(gen_quest_main.myMod)
                {
                    EditorID = "rg_harness_loc",
                    LocationCellUniqueReferences = new ExtendedList<LocationCellUniqueReference>()
                    
                };

                Console.WriteLine($"=== Weight Harness ===");
                Console.WriteLine($"Faction: {faction}, Size: {size}, Runs: {runs}");

                var harness = new OreStationWeightHarness(
                    designFactory: () => new OreStation(),
                    faction: faction,
                    size: size);

                var best = harness.FindBest(cell, location, runs);

                Console.WriteLine($"\nBest average score: {best.AverageScore:0.00}");
            }

            gen_quest_main.PrintNounRegistry();
            return 0;
        }
    }
}
