using Mutagen.Bethesda.Environments;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Starfield;
using Mutagen.Bethesda;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FrankyCLI
{
    class gen_pluginmerger
    {
        public static int Generate(string[] args)
        {
            Random random = new Random();
            StarfieldMod myMod;
            string modname = args[0];
            string mode = args[1];
            string importmodname = args[2];
            string item = args[3];
            string form = args[4];

            string datapath = "";
            using (var env = GameEnvironment.Typical.Builder<IStarfieldMod, IStarfieldModGetter>(GameRelease.Starfield).Build())
            {
                var immutableLoadOrderLinkCache = env.LoadOrder.ToImmutableLinkCache();
                datapath = env.DataFolderPath;
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
                //Merge
                for (int i = 0; i < env.LoadOrder.Count; i++)
                {
                    if (env.LoadOrder[i].FileName == importmodname + ".esm")
                    {
                        ModPath importmodPath = Path.Combine(env.DataFolderPath, env.LoadOrder[i].FileName);
                        var importmod = StarfieldMod.CreateFromBinary(importmodPath, StarfieldRelease.Starfield, gen_quest_main.BuildReadParams(env.LoadOrder));
                        gen_quest_main.FixNextFormId(importmod);
                        myMod.DeepCopyIn(importmod);
                        //myMod.ModHeader.MasterReferences.Clear();
                        /*
                        foreach (var weapon in importmod.EnumerateMajorRecords<Weapon>())
                        {
                            myMod.Weapons.DuplicateInAsNewRecord(weapon);
                        }*/                        
                    }
                }
            }
            myMod.WriteToBinary(datapath + "\\" + modname + ".esm", gen_quest_main.BuildWriteParams());
            Console.WriteLine("Finished");
            return 0;
        }
    }
}
