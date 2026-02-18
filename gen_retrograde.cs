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
            if (args.Length < 5)
            {
                Console.WriteLine("Usage: retrograde <modname> <mode> <prefix> <item> <form> [faction] [stationdesign] [poi|bounty] [quiet] [exportai]");
                Console.WriteLine("  modname        - Output mod name (without .esm)");
                Console.WriteLine("  mode           - Generation mode");
                Console.WriteLine("  prefix         - EditorID prefix");
                Console.WriteLine("  item           - Item identifier");
                Console.WriteLine("  form           - Form identifier");
                Console.WriteLine("  faction        - (optional) Crimsonfleet, Ecliptic, Varuun, Spacer");
                Console.WriteLine("  stationdesign  - (optional) Station design name");
                Console.WriteLine("  poi|bounty     - (optional) Quest type (default: bounty)");
                Console.WriteLine("  quiet          - (optional) Suppress score output");
                Console.WriteLine("  exportai       - (optional) Export AI conversation to file");
                return 1;
            }

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
                            myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                            gen_quest_main.FixNextFormId(myMod);
                        }
                    }
                }

                // Sync statics to gen_quest_main so ModContextImpl can access them
                gen_quest_main.StarfieldModKey = StarfieldModKey;
                gen_quest_main._StarfieldMod = _StarfieldMod;
                gen_quest_main.myMod = myMod;

                // Initialize the Retrograde context for library access
                RetrogradeContext.Current = new ModContextImpl();

                // Load global room usage tracker for cross-station room diversity
                var roomTrackerPath = Path.Combine(datapath, modname + "_room_usage.json");
                GlobalRoomTracker.Load(roomTrackerPath);

                //We have different styles of quest chains, so randomly choose one.

                AITools.AIMODE = false;

                // Parse optional parameters: faction, station design, type (poi/bounty), quiet
                string faction = args.Length > 5 ? args[5] : null;
                string stationDesignName = args.Length > 6 ? args[6] : null;
                string questType = args.Length > 7 ? args[7] : null; // "poi" or "bounty"
                string quietFlag = args.Length > 8 ? args[8] : null; // "quiet" to suppress scores
                string exportAiFlag = args.Length > 9 ? args[9] : null; // "exportai" to dump AI conversation

                RetrogradeContext.Quiet = string.Equals(quietFlag, "quiet", StringComparison.OrdinalIgnoreCase);
                AITools.EXPORT_CONVERSATION = string.Equals(exportAiFlag, "exportai", StringComparison.OrdinalIgnoreCase);

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

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            GlobalRoomTracker.Save();
            AITools.ExportConversation();
            Console.WriteLine("Finished");
            return 0;
        }
        

    }
}