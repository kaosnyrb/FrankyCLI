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
using Retrograde.Quests;
using Retrograde.Utils;
using System;
using System.IO;
using System.Linq;

namespace FrankyCLI
{
    public class gen_quest_main
    {
        public static ModKey StarfieldModKey;
        public static IStarfieldModGetter _StarfieldMod = null!;
        public static StarfieldMod myMod = null!;

        /// <summary>
        /// Cached master flags lookup, built once from the load order.
        /// Uses lightweight snapshots so it survives GameEnvironment disposal.
        /// </summary>
        public static Cache<IModMasterStyledGetter, ModKey> MasterFlagsCache = null!;

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
            // The two numbers here are in DIFFERENT UNITS and comparing them raw is why
            // this guard used to never fire.
            //
            // The header's NextFormID is stored NAMESPACED -- it carries the load-order /
            // master-index byte (avontechstardust.esm has 0x01000800 on disk). FormKey.ID
            // is the LOCAL 24-bit id with that byte already stripped (0x00088A for the
            // highest record in the same plugin). So `0x00088A >= 0x01000800` is false for
            // every record ever, max stayed at the stale header value, and allocation
            // restarted at 0x000800 -- underneath 70 existing REFRs. Mutagen then threw
            // "Two records with the same FormKey were encountered" at write time, or
            // "An item with the same key has already been added" at group-add time,
            // depending on which record hit an occupied id first.
            //
            // Compare local against local; put the index byte back when writing.
            uint raw = mod.ModHeader.Stats.NextFormID;
            uint indexByte = raw & 0xFF000000;
            uint max = raw & 0x00FFFFFF;
            foreach (var rec in mod.EnumerateMajorRecords())
            {
                uint id = rec.FormKey.ID & 0x00FFFFFF;
                if (id >= max)
                    max = id + 1;
            }
            mod.ModHeader.Stats.NextFormID = indexByte | max;
        }

        public static void PrintNounRegistry()
        {
            var registry = RetrogradeContext.NounRegistry;
            if (registry.Count == 0) return;
            Console.WriteLine();
            Console.WriteLine("=== Noun Registry ===");
            foreach (var g in registry.GroupBy(n => n.GetType().Name).OrderBy(g => g.Key))
                Console.WriteLine($"  {g.Key,-24} × {g.Count()}");
            Console.WriteLine($"  Total: {registry.Count}");
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
                _StarfieldMod = env.LoadOrder[0].Mod!;
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
                ModContextImpl.DiscoverTemplateMods(env.LoadOrder, datapath);

                // Initialize the Retrograde context for library access
                RetrogradeContext.Current = new ModContextImpl();

                //We have different styles of quest chains, so randomly choose one.

                bool setmissions = false;
                if (setmissions)
                {
                    var outlawQuest = new StaticLayoutQuestChain(myMod)
                    {
                        InvestigationTemplate = new Templates_Cities_Conversation_Neon().InvestigationTemplates[random.Next(new Templates_Cities_Conversation_Neon().InvestigationTemplates.Count)].Name,
                        DeepTempalte = new Templates_Cities_Conversation_Neon().InvestigationTemplates[random.Next(new Templates_Cities_Conversation_Neon().InvestigationTemplates.Count)].Name,
                        ShowdownTemplate = "City Bounty - Neon Rooftops"
                    };
                    outlawQuest.GenerateQuest();                    
                }
                else
                {
                    List<IQuestchain> questchains = new List<IQuestchain>
                    {
                        new LoopingLayoutQuestChain(myMod),
                        //new StaticLayoutQuestChain(myMod),
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
            PrintNounRegistry();
            Console.WriteLine("Finished");
            return 0;
        }
        

    }
}