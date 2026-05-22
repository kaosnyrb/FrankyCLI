using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Retrograde;
using Retrograde.Abstractions;
using Retrograde.Nouns.Hunt;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace FrankyCLI
{
    /// <summary>
    /// Builds a real ESM containing one PredatorHuntTarget NPC per probe planet.
    /// Load the resulting hunttest.esm in xEdit (or in-game) to verify the NPCs
    /// are wired correctly and CCT generates appropriate creature appearances.
    ///
    /// Usage: gen_hunttest [modname]   (defaults to "hunttest")
    /// </summary>
    public static class gen_hunttest
    {
        public static int Run(string modname = "hunttest")
        {
            Console.WriteLine($"=== gen_hunttest: building {modname}.esm ===");
            Console.WriteLine();

            string datapath;

            using (var env = GameEnvironment.Typical
                .Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield)
                .Build())
            {
                gen_quest_main.StarfieldModKey = new ModKey("Starfield", ModType.Master);
                gen_quest_main._StarfieldMod   = env.LoadOrder[0].Mod!;
                datapath                       = env.DataFolderPath;

                ModKey newMod = new ModKey(modname, ModType.Master);
                gen_quest_main.myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);

                ModContextImpl.DiscoverTemplateMods(env.LoadOrder, datapath);
                gen_quest_main.BuildReadParams(env.LoadOrder);
                RetrogradeContext.Current = new ModContextImpl();

                PrintPredatorSurvey(RetrogradeContext.Current.StarfieldMod);

                var planets = new[]
                {
                    "Nemeria-II",
                    "Jemison",
                    "Gagarin",
                    "Algorab-II",
                    "Ternion-III",
                };

                var hunt = new PredatorHuntTarget();
                foreach (var planet in planets)
                {
                    Console.WriteLine($"── {planet} ──");
                    try
                    {
                        var (targetList, npc, huntName) = hunt.GetHuntTarget(planet);
                        Console.WriteLine($"  {huntName}  →  {npc.EditorID}  ({npc.FormKey})");
                        Console.WriteLine($"  formlist:  {targetList.FormKey}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ERROR: {ex.Message}");
                    }
                }
                Console.WriteLine();
            }

            foreach (var rec in gen_quest_main.myMod.EnumerateMajorRecords())
                rec.IsCompressed = false;

            string outPath = Path.Combine(datapath, modname + ".esm");
            gen_quest_main.myMod.WriteToBinary(outPath, gen_quest_main.BuildWriteParams());

            Console.WriteLine($"NPCs:     {gen_quest_main.myMod.Npcs.Count}");
            Console.WriteLine($"FormLists: {gen_quest_main.myMod.FormLists.Count}");
            Console.WriteLine();
            Console.WriteLine($"Written: {outPath}");
            Console.WriteLine("Load in xEdit to verify Race / DefaultTemplate / Name fields.");
            return 0;
        }

        private static void PrintPredatorSurvey(IStarfieldModGetter starfield)
        {
            var pattern = new Regex(@"^PCM_(?<system>[^_]+)_(?<planet>[^_]+)_Predator\d+(?:_\w+)?$",
                RegexOptions.IgnoreCase);

            var aquaticTokens = new[] { "Swimmer", "Sea", "Lionfish", "Sunfish" };
            var aquaticSkinOmods = starfield.ObjectModifications
                .Where(o => o.EditorID != null
                    && o.EditorID.StartsWith("mod_CCT_Skin_", StringComparison.OrdinalIgnoreCase)
                    && aquaticTokens.Any(t => o.EditorID.Contains(t, StringComparison.OrdinalIgnoreCase)))
                .Select(o => o.FormKey)
                .ToHashSet();

            int total = 0, aquatic = 0;
            var planetsAll = new HashSet<string>();
            var planetsLand = new HashSet<string>();

            foreach (var npc in starfield.Npcs)
            {
                if (npc.EditorID == null) continue;
                var m = pattern.Match(npc.EditorID);
                if (!m.Success) continue;

                total++;
                var planet = m.Groups["planet"].Value;
                planetsAll.Add(planet);

                bool isAquatic = false;
                if (npc.ObjectTemplates != null && npc.ObjectTemplates.Count > 0)
                {
                    var inc = npc.ObjectTemplates[0].Includes;
                    if (inc != null)
                        foreach (var i in inc)
                            if (aquaticSkinOmods.Contains(i.Mod.FormKey)) { isAquatic = true; break; }
                }

                if (isAquatic) aquatic++;
                else planetsLand.Add(planet);
            }

            Console.WriteLine("── Predator survey ─────────────────────────────");
            Console.WriteLine($"  Total PCM_*_Predator NPCs: {total}");
            Console.WriteLine($"  Aquatic (filtered out):    {aquatic}");
            Console.WriteLine($"  Land predators available:  {total - aquatic}");
            Console.WriteLine($"  Planets with any predator: {planetsAll.Count}");
            Console.WriteLine($"  Planets with land predator: {planetsLand.Count}");
            Console.WriteLine();
        }
    }
}
