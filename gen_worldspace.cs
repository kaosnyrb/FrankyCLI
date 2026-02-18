using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using Retrograde.Nouns.WorldspaceNouns;
using Retrograde.WorldspaceDesigns;
using System;
using System.IO;

namespace FrankyCLI
{
    public class gen_worldspace
    {
        public static ModKey StarfieldModKey;
        public static IStarfieldModGetter _StarfieldMod;
        public static StarfieldMod myMod;

        public static int Generate(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("Usage: gen_worldspace <modname> <mode> <prefix> <item> <form> [seed] [faction] [design] [quiet]");
                Console.WriteLine("  modname  - Output mod name (without .esm)");
                Console.WriteLine("  mode     - gen_worldspace");
                Console.WriteLine("  prefix   - EditorID prefix");
                Console.WriteLine("  item     - Item identifier");
                Console.WriteLine("  form     - Form identifier");
                Console.WriteLine("  seed     - (optional) Generation seed (default: random)");
                Console.WriteLine("  faction  - (optional) Spacer, Crimsonfleet, Ecliptic, Varuun");
                Console.WriteLine("  design   - (optional) Worldspace design name (default: Fort)");
                Console.WriteLine("  quiet    - (optional) Suppress output");
                return 1;
            }

            string modname = args[0];
            string datapath = "";

            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                StarfieldModKey = new ModKey("Starfield", ModType.Master);
                var immutableLoadOrderLinkCache = env.LoadOrder.ToImmutableLinkCache();
                datapath = env.DataFolderPath;
                _StarfieldMod = env.LoadOrder[0].Mod;

                // Load or create the mod
                ModKey newMod = new ModKey(modname, ModType.Master);
                myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                if (env.LoadOrder.ModExists(newMod))
                {
                    for (int i = 0; i < env.LoadOrder.Count; i++)
                    {
                        if (env.LoadOrder[i].FileName == modname + ".esm")
                        {
                            ModPath modPath = Path.Combine(env.DataFolderPath, env.LoadOrder[i].FileName);
                            myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                            gen_quest_main.FixNextFormId(myMod);
                        }
                    }
                }

                // Discover template mods from load order (any mod with "template" in filename + Starfield.esm)
                ModContextImpl.TemplateModsList = new System.Collections.Generic.List<IStarfieldModGetter>();
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
                        Console.WriteLine($"Template mod found: {listing.FileName}");
                    }
                }

                // Sync statics so ModContextImpl can access them
                gen_quest_main.StarfieldModKey = StarfieldModKey;
                gen_quest_main._StarfieldMod = _StarfieldMod;
                gen_quest_main.myMod = myMod;

                // Initialize Retrograde context
                RetrogradeContext.Current = new ModContextImpl();

                // Parse optional parameters
                int seed = args.Length > 5 ? int.Parse(args[5]) : new Random().Next();
                string faction = args.Length > 6 ? args[6] : "Spacer";
                string designName = args.Length > 7 ? args[7] : "Fort";
                string quietFlag = args.Length > 8 ? args[8] : null;

                RetrogradeContext.Quiet = string.Equals(quietFlag, "quiet", StringComparison.OrdinalIgnoreCase);

                // Resolve worldspace design
                IWorldspaceDesign design;
                if (!string.IsNullOrEmpty(designName) && WorldspaceDesignRegistry.Designs.TryGetValue(designName, out var designFactory))
                {
                    design = designFactory();
                }
                else
                {
                    design = new FortDesign();
                }

                Console.WriteLine($"Generating worldspace with seed {seed}, faction {faction}, design {design.DesignName}");

                // Generate the worldspace
                var worldspaceNoun = new WorldspaceNoun(design, faction, seed, datapath);

                Console.WriteLine($"Worldspace generated: {worldspaceNoun.Worldspace.EditorID}");
            }

            // Strip compression flags (Mutagen workaround)
            foreach (var rec in myMod.EnumerateMajorRecords())
            {
                rec.IsCompressed = false;
            }

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine("Export complete!");
            return 0;
        }
    }
}
