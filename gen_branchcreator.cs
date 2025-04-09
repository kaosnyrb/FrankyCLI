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
    internal class gen_branchcreator
    {

        public static int Generate(string[] args)
        {
            Random random = new Random();
            StarfieldMod myMod;
            string modname = args[0];
            string mode = args[1];
            string prefix = args[2];
            string item = args[3];
            string modelpath = args[4];

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
                            myMod = StarfieldMod.CreateFromBinary(modPath, StarfieldRelease.Starfield);

                        }
                    }
                }
                IFormLinkNullable<IPlanetContentManagerBranchNode> pctree = new FormKey(myMod.ModKey, 0x0000A21).ToNullableLink<IPlanetContentManagerBranchNode>();
                for(int entry = 2; entry <= 256; entry++)
                {
                    var pcmbn = new PlanetContentManagerBranchNode(myMod)
                    {
                        EditorID = "PIP_Quest_" + entry.ToString("000"),
                        NAM1 = 2,
                        NAM2 = 0,
                        NAM5 = false
                    };
                    try
                    {
                        myMod.PlanetContentManagerBranchNodes.Add(pcmbn);
                        myMod.PlanetContentManagerBranchNodes[pctree.FormKey].Nodes.Add(pcmbn);
                        
                    }
                    catch(Exception e) {
                        Console.WriteLine("Clashed formkey: " + e.Message); 
                        entry--; 
                    }
                }

            }

            myMod.WriteToBinary(datapath + "\\" + modname + ".esm");
            Console.WriteLine("Finished");


            return 0;
        }
    }
}
