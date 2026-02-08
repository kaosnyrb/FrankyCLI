using FrankyCLI.questgen_tools;
using FrankyCLI.questgen_tools.Interfaces;
using FrankyCLI.questgen_tools.Utils;
using Retrograde;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Noggog.StructuredStrings.CSharp;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI
{
    public class gen_quest_main
    {
        public static ModKey StarfieldModKey;
        public static IStarfieldModGetter _StarfieldMod;
        public static StarfieldMod myMod;

        public static int Generate(string[] args)
        {
            Random random = RandomUtils.random;
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

                var outlawQuest = new RetrogradeQuest(myMod);

                //var outlawQuest = new StaticLayoutQuestChain(myMod);
                //outlawQuest.InvestigationTemplate = "Space Station Activator - spacer Medium light guard";
                //outlawQuest.DeepTempalte = "Space Destroy - unguarded";
                //outlawQuest.ShowdownTemplate = "Planet side Bounty - breathable atmosphere";
                /*
                List<IQuestchain> questchains = new List<IQuestchain>
                {
                   new LoopingLayoutQuestChain(myMod),
                   new StaticLayoutQuestChain(myMod),
                };

                var outlawQuest = questchains[random.Next(questchains.Count)];
                */
                outlawQuest.GenerateQuest();
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