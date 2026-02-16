using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using Retrograde.AI;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.StationDesigns;
using System;
using System.IO;

namespace FrankyCLI
{
    public class gen_retrograde
    {
        public static ModKey StarfieldModKey;
        public static IStarfieldModGetter _StarfieldMod;
        public static StarfieldMod myMod;

        public static int Generate(string[] args)
        {
            Random random = RandomProvider.Random;
            //StarfieldMod myMod;
            string modname = args[0];
            string mode = args[1];
            string prefix = args[2];
            string item = args[3];
            string form = args[4];

            string datapath = "";
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                StarfieldModKey = new ModKey("Starfield", ModType.Master);
                var immutableLoadOrderLinkCache = env.LoadOrder.ToImmutableLinkCache();
                datapath = env.DataFolderPath;
                _StarfieldMod = env.LoadOrder[0].Mod;
                //Find the modkey 
                ModKey newMod = new ModKey(modname, ModType.Master);
                myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                if (!env.LoadOrder.ModExists(newMod))
                {
                    myMod = new StarfieldMod(newMod, StarfieldRelease.Starfield);
                }
                else
                {
                    for (int i = 0; i < env.LoadOrder.Count; i++)
                    {

                        if (env.LoadOrder[i].FileName == modname + ".esm")
                        {
                            ModPath modPath = Path.Combine(env.DataFolderPath, env.LoadOrder[i].FileName);
                            myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield);
                        }
                    }
                }

                // Initialize the Retrograde context for library access
                RetrogradeContext.Current = new ModContextImpl();

                //We have different styles of quest chains, so randomly choose one.

                AITools.AIMODE = false;

                // Parse optional parameters: faction, station design, type (poi/bounty)
                string faction = args.Length > 5 ? args[5] : null;
                string stationDesignName = args.Length > 6 ? args[6] : null;
                string questType = args.Length > 7 ? args[7] : null; // "poi" or "bounty"

                // Resolve faction - random if not specified
                List<string> Factions = new List<string>()
                {
                    "Crimsonfleet","Ecliptic","Varuun","Spacer"
                };
                if (string.IsNullOrEmpty(faction))
                {
                    faction = Factions[RandomProvider.Random.Next(Factions.Count)];
                }

                // Resolve station design - default to HabStation if not specified or not found
                IStationDesign stationDesign;
                if (!string.IsNullOrEmpty(stationDesignName) && StationDesignRegistry.Designs.TryGetValue(stationDesignName, out var designFactory))
                {
                    stationDesign = designFactory();
                }
                else
                {
                    stationDesign = new HabStation();
                }

                // Resolve type - default to bounty
                bool isPOI = string.Equals(questType, "poi", StringComparison.OrdinalIgnoreCase);

                var stationname = stationDesign.GenerateStationName(faction);
                var size = "Large";

                if (isPOI)
                {
                    var poiQuest = new RetrogradeQuest();
                    poiQuest.GenerateQuest(stationname, faction, size, stationDesign);
                }
                else
                {
                    var bountyQuest = new RetrogradeBountyQuest();
                    bountyQuest.GenerateQuest(stationname, faction, size, stationDesign);
                }
            }
            foreach (var rec in myMod.EnumerateMajorRecords())
            {
                //if (rec.EditorID != null)
                //{
                //    Console.WriteLine(rec.EditorID.ToString() + " : " + rec.FormKey.ToString());
                //}
                rec.IsCompressed = false;
            }

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            AITools.ExportConversation();
            Console.WriteLine("Finished");
            return 0;
        }
        

    }
}