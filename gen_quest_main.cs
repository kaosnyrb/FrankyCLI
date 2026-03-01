using Mutagen.Bethesda;
using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Binary.Parameters;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Starfield;
using Noggog;
using Retrograde;
using Retrograde.AI;
using Retrograde.Chains;
using Retrograde.Chains.Interfaces;
using Retrograde.Utils;
using System;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    public class gen_quest_main
    {
        public static ModKey StarfieldModKey;
        public static IStarfieldModGetter _StarfieldMod;
        public static StarfieldMod myMod;

        /// <summary>
        /// Cached master flags lookup, built once from the load order.
        /// Uses lightweight snapshots so it survives GameEnvironment disposal.
        /// </summary>
        public static Cache<IModMasterStyledGetter, ModKey> MasterFlagsCache;

        /// <summary>
        /// Lightweight snapshot of a mod's master style, so we don't hold
        /// references to disposed GameEnvironment mod objects.
        /// </summary>
        private class MasterStyleSnapshot : IModMasterStyledGetter
        {
            public ModKey ModKey { get; init; }
            public MasterStyle MasterStyle { get; init; }
        }

        /// <summary>
        /// Builds BinaryReadParameters with MasterFlagsLookup from the load order.
        /// Required by Mutagen 0.46+ for CreateFromBinary and WriteToBinary.
        /// </summary>
        public static BinaryReadParameters BuildReadParams<TMod>(ILoadOrderGetter<IModListingGetter<TMod>> loadOrder)
            where TMod : class, IModGetter
        {
            MasterFlagsCache = new Cache<IModMasterStyledGetter, ModKey>(m => m.ModKey);
            foreach (var listing in loadOrder.ListedOrder)
            {
                if (listing.Mod != null)
                    MasterFlagsCache.Set(new MasterStyleSnapshot
                    {
                        ModKey = listing.Mod.ModKey,
                        MasterStyle = listing.Mod.MasterStyle
                    });
            }
            return new BinaryReadParameters() { MasterFlagsLookup = MasterFlagsCache };
        }

        /// <summary>
        /// Builds BinaryWriteParameters with MasterFlagsLookup and NoCheck for FormID uniqueness.
        /// </summary>
        public static BinaryWriteParameters BuildWriteParams()
        {
            return new BinaryWriteParameters()
            {
                MasterFlagsLookup = MasterFlagsCache,
            };
        }

        /// <summary>
        /// After loading a mod via CreateFromBinary, bump NextFormID past the highest
        /// existing record to prevent FormKey collisions when adding new records.
        /// </summary>
        public static void FixNextFormId(StarfieldMod mod)
        {
            uint max = mod.ModHeader.Stats.NextFormID;
            foreach (var rec in mod.EnumerateMajorRecords())
            {
                uint id = rec.FormKey.ID;
                if (id >= max)
                    max = id + 1;
            }
            mod.ModHeader.Stats.NextFormID = max;
        }

        public static int Generate(string[] args)
        {
            Random random = RandomProvider.Random;
            //StarfieldMod myMod;
            string modname = args[0];
            string mode = args[1];
            string prefix = args[2];
            string item = args[3];
            string form = args[4];

            using var _ = PluginsActivator.ActivateTemplates();
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
                            myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield, BuildReadParams(env.LoadOrder));
                            FixNextFormId(myMod);
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

                // Initialize the Retrograde context for library access
                RetrogradeContext.Current = new ModContextImpl();

                //We have different styles of quest chains, so randomly choose one.

                AITools.AIMODE = true;
                SpeechTools.generateWavs = true;
                
                bool setmissions = false;
                if (setmissions)
                {
                    var outlawQuest = new StaticLayoutQuestChain(myMod)
                    {
                        InvestigationTemplate = "Space Destroy - unguarded IceCrystals",
                        DeepTempalte = "Space Destroy - unguarded",
                        ShowdownTemplate = "City Bounty - Paradiso"
                    };
                    outlawQuest.GenerateQuest();                    
                }
                else
                {
                    List<IQuestchain> questchains = new List<IQuestchain>
                    {
                        new LoopingLayoutQuestChain(myMod),
                        new StaticLayoutQuestChain(myMod),
                    };
                    var outlawQuest = questchains[random.Next(questchains.Count)];
                    outlawQuest.GenerateQuest();
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
            AITools.ExportConversation();
            Console.WriteLine("Finished");
            return 0;
        }
        

    }
}